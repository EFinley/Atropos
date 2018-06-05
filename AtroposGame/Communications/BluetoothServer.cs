using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.IO;
using System.IO;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Android.Util;
using Java.Net;
using System.Threading;
using static Atropos.Communications.Bluetooth.BluetoothCore;
using MiscUtil;
using Android.Bluetooth;
using Java.Util;

namespace Atropos.Communications.Bluetooth
{
    public class BluetoothServer : BluetoothCore
    {
        private static string UIDstring = "ffe0ecd2-3d16-4f8d-90de-e89e7fc396a5";
        public static readonly UUID ServiceUUID = UUID.FromString(UIDstring);
        public static readonly Guid ServiceGUID = Guid.Parse(UIDstring);

        public string MyMACaddress = "unknown";

        // A boolean indicating whether this server should send ACKnowledgment messages
        public bool DoACK = true;

        // The ServerSocket (aka Socket Factory!) we'll use for accepting new connections.
        private BluetoothServerSocket serverSocket;

        // A lookup table for the pre-generated output streams associated with each socket accepted - saves on allocations of new streams.
        private Dictionary<BluetoothSocket, DataOutputStream> outputStreams = new Dictionary<BluetoothSocket, DataOutputStream>();
        private Dictionary<BluetoothSocket, DataInputStream> inputStreams = new Dictionary<BluetoothSocket, DataInputStream>();

        // Another lookup table for the sockets based on the supplied address for their recipient
        private Dictionary<string, BluetoothSocket> connections = new Dictionary<string, BluetoothSocket>();

        protected new string _tag = "BluetoothServer";
        private object _lock = new object();

        public int numMessagesReceived = 0;
        public int numMessagesSentOut = 0;
        public int numAcksSentOut = 0;
        public int numConnections { get { return connections.Count; } }

        private BluetoothAdapter bluetoothAdapter;

        public BluetoothServer(CancellationToken? stopToken = null) : base(stopToken) { }

        public void Listen()
        {
            List<ServerThread> serverThreads = new List<ServerThread>();
            bluetoothAdapter = BluetoothAdapter.DefaultAdapter;

            if (bluetoothAdapter == null)
                Log.Debug(_tag, "No Bluetooth adapter found.");
            else if (!bluetoothAdapter.IsEnabled)
                Log.Debug(_tag, "Bluetooth adapter is not enabled.");
            else Log.Info(_tag, "Bluetooth adapter ready.");

            Task.Run(async () =>
            {
                try
                {
                    while (true) // !_cts.IsCancellationRequested
                    {
                        bluetoothAdapter.CancelDiscovery(); // Always do this before connecting.
                        await Task.Delay(100);
                        serverSocket = bluetoothAdapter.ListenUsingRfcommWithServiceRecord("AtroposBluetooth", ServiceUUID);
                        await Task.Delay(100);
                        Log.Debug(_tag, "Server socket ready and waiting.");
                        // Grab the next incoming connection
                        BluetoothSocket socket = serverSocket.Accept();
                        //socket.Connect();
                        await Task.Delay(100);
                        serverSocket.Close();
                        Log.Debug(_tag, $"Server accepted a connection from {socket.RemoteDevice.Address}.");

                        lock (_lock)
                        {
                            // Cache the socket, filed under its address
                            connections[socket.RemoteDevice.Address] = socket;

                            // Create and cache a DataOutputStream for sending data over it
                            outputStreams.Add(socket, new DataOutputStream(socket.OutputStream));
                            inputStreams.Add(socket, new DataInputStream(socket.InputStream));
                        }

                        // Create a new thread for this connection
                        await Task.Delay(150);
                        serverThreads.Add(new ServerThread(this, socket).Start());

                        //AddressBook.Add(new CommsContact() { Name = socket.RemoteDevice.Name, IPaddress = socket.RemoteDevice.Address });

                        // Ack back to the connection to let it know you're hearing it (and to pass it the address you'll know it by)
                        //ForwardTo(socket.RemoteDevice.Address, SERVER, ACK + CONNECTED_AS + socket.RemoteDevice.Address);
                    }
                }
                catch (Exception e)
                {
                    Log.Debug(_tag, $"Exception in server socket listening: \n{e}\n");
                }
                finally
                {
                    Log.Debug(_tag, $"Releasing all connections ({serverThreads.Count} of them).");
                    serverSocket.Close();
                    foreach (var sThread in serverThreads) sThread.Stop();
                }
            });
        }

        public void RemoveConnection(BluetoothSocket s)
        {
            // Lock just in case we're in the middle of enumerating our sockets for something else (e.g. a broadcast signal)
            lock(_lock)
            {
                connections.Remove(s.RemoteDevice.Address);
                outputStreams.Remove(s);
                Log.Debug(_tag, $"Removing connection to {s.RemoteDevice.Address}");

                // Make sure it's closed
                try { s.Close(); }
                catch (Java.IO.IOException e)
                {
                    Log.Error(_tag, $"Error closing connection to {s.RemoteDevice.Address}");
                }
            }
        }

        //public void Forward(string sender, string message)
        //{
            
        //    foreach (string conn in connections.Keys)
        //    {
        //        if (conn != sender) 
        //            ForwardTo(conn, sender, message);
        //    }
        //}

        //public void ForwardTo(string address, string sender, string message)
        //{
        //    if (address == ALL)
        //    {
        //        foreach (string conn in connections.Keys) ForwardTo(conn, sender, message);
        //        return;
        //    }
        //    var recipientSocket = connections[address];
        //    var dOutStream = outputStreams[recipientSocket];
        //    var dInStream = inputStreams[recipientSocket];

        //    SendString(dOutStream, dInStream, $"{address}{NEXT}{sender}{NEXT}{message}");

        //    OnServerForwardingMessage.Raise($"Server stats: Rcvd {numMessagesReceived}, Sent {numMessagesSentOut}, Acks {numAcksSentOut}.\nConnections {connections.Keys.Join()}.");
        //}

        public class ServerThread : BluetoothCore
        {
            protected new string _tag = "ServerThread";

            // The server that spawned us
            private BluetoothServer server;

            // The socket that connects us to our (singular, for a given ServerThread) client.
            private BluetoothSocket socket;

            public ServerThread(BluetoothServer server, BluetoothSocket socket) : base(server.StopToken)
            {
                this.server = server; 
                this.socket = socket;
            }

            public ServerThread Start() // "Fluent" idiom - return yourself so the function can be called right when you're created.
            {
                Task.Run((Action)Run);
                return this;
            }

            public void Stop()
            {
                _cts.Cancel();
                server.RemoveConnection(socket);
            }

            public void Run()
            {
                // Create a DataInputStream for communication; the client is using a DataOutputStream to write to us
                var dInStream = new DataInputStream(socket.InputStream);
                var dOutStream = new DataOutputStream(socket.OutputStream);

                Task.Delay(100).Wait();

                try
                {
                    // Get the next message
                    while (!StopToken.IsCancellationRequested)
                    {
                        string data = String.Empty;

                        try
                        {
                            data = ReadString(dInStream, dOutStream);
                        }
                        //catch (Java.IO.IOException)
                        //{
                        //    BTDirectActivity.Current.RelayToast("Server thread error 'Read() returns -1' - connection lost.");
                        //    continue;
                        //}
                        catch (System.AggregateException e)
                        {
                            if (e.InnerExceptions.All(ex => ex is Java.IO.IOException))
                            {
                                BTDirectActivity.Current.RelayToast("Connection to client lost.");
                                break;
                            }
                            else throw e;
                        }
                                                          
                        var message = Message.FromCharStream(socket.RemoteDevice.Address, data);
                        BTDirectActivity.Current.RelayToast($"Server received ({message.Type}) '{message.Content}' from {socket.RemoteDevice.Address}.");
                        server.numMessagesReceived++;

                        // Handle first-connection protocol so we can find out our MAC address
                        if (message.Type == MsgType.Notify && message.Content.StartsWith(CONFIRM_AS_CLIENT))
                        {
                            // Oh, good - now we known our own bliddy MAC address at last!
                            if (server.MyMACaddress == "unknown") server.MyMACaddress = message.Content.Split(onNEXT)[1];
                            BTDirectActivity.Current.UpdateThisDevice();

                            //// Let them know we not only heard them, but heard them from /their/ MAC address so they can record it.
                            //SendString(dOutStream, dInStream,
                            //    new Message(MsgType.Reply, $"{CONFIRM_AS_SERVER}{NEXT}{socket.RemoteDevice.Address}").ToCharStream());

                            // Try to establish a reciprocal connection.
                            //BTDirectActivity.Current.Connect(socket.RemoteDevice);
                        }

                        // Acknowledge receipt of message (including its ID number)
                        if (server.DoACK && message.Type != MsgType.Ack)
                        {
                            SendString(dOutStream, dInStream,
                                new Message()
                                {
                                    From = server.MyMACaddress,
                                    Type = MsgType.Ack,
                                    ID = message.ID,
                                    Content = $"{message.Type}: {message.Content}"
                                }.ToCharStream());
                            server.numAcksSentOut++;
                            Log.Info(_tag, $"Sending ack of '{message.Content}' to {socket.RemoteDevice.Address}");
                        }

                        // Now do something with it!
                        BluetoothMessageCenter.ActOnMessage(message);
                    }
                }
                catch (Exception e)
                {
                    // The EOFException clauses are related to the WiFi version; the BT streams appear to work differently, but this is retained in case it catches something later.
                    if (e.InnerException != null)
                    {
                        if (e.InnerException is Java.IO.EOFException)
                            Log.Debug(_tag, $"Wrapped EOFException: {e}.  Stream seems closed!  Status of socket is: ConnectionType {socket.ConnectionType}, IsConnected {socket.IsConnected}.");
                        else
                        {
                            Log.Debug(_tag, $"Wrapped exception: {e}.  Socket connected: {socket.IsConnected}.");
                            throw e;
                        }
                    }
                    else
                    {
                        if (e is Java.IO.EOFException)
                            Log.Debug(_tag, $"EOFException: {e}.  Stream seems closed!  Status of socket is: ConnectionType {socket.ConnectionType}, IsConnected {socket.IsConnected}.");
                        else
                        {
                            Log.Debug(_tag, $"Exception: {e}.  Socket connected: {socket.IsConnected}.");
                            throw e;
                        }
                    }
                }
                finally
                {
                    Log.Debug(_tag, $"ServerThread to {socket.RemoteDevice.Address} shutting down.");
                    Stop();
                }
            }



            //public override string ReadString(DataInputStream inStream, DataOutputStream outStream)
            //{
            //    var result = base.ReadString(inStream, outStream);
            //    if (server.DoACK) // Serves as a proxy for "should I also report to the log file?" as well as "should I send ACKs on the comm channel?"
            //        Log.Debug(_tag, $"Server received raw string [{Readable(result)}]");
            //    return result;
            //}
        }
    }
}