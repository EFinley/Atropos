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
using static Atropos.Communications.WifiBaseClass;
using MiscUtil;

namespace Atropos.Communications
{
    public class WifiBaseClass
    {
        protected string _tag = "WifiBaseClass";

        // A central cancellation coordinator
        protected CancellationTokenSource _cts;
        public CancellationToken StopToken { get { return _cts?.Token ?? CancellationToken.None; } }

        public WifiBaseClass(CancellationToken? stopToken = null)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(stopToken ?? CancellationToken.None);
        }

        public static void SendString(DataOutputStream stream, string message)
        {
            Task.Run(() =>
            {
                stream.WriteChars(message);
                stream.WriteChar(0);
            });
        }

        public virtual string ReadString(DataInputStream inStream)
        {
            string resultString = String.Empty;
            var res = 255;
            Task.Run(() =>
            {
                while (res > 0 && !StopToken.IsCancellationRequested)
                {
                    res = inStream.ReadChar();
                    //Log.Debug(_tag, $"Received '{res}' (aka {(char)res}).");
                    if (res > 0) resultString += (char)res;
                }
            }).Wait();
            //Log.Debug(_tag, $"Received '{resultString}'.");
            return resultString;
        }

        public static string ACK = "ACK";
        public static string CMD = "CMD";
        public static string POLL_FOR_NAMES = "POLL_FOR_NAMES";
        public static string POLL_RESPONSE = "POLL_RESPONSE";
        public static string ALL = "ALL";
        public static string SERVER = "SERVER";
        public static string CONNECTED_AS = " - Connected as ";
    }

    public class WifiServer : WifiBaseClass
    {
        // We use a single fixed port number for all of our work here.
        public const int Port = 42445;

        // A boolean indicating whether this server should send ACKnowledgment messages
        public bool DoACK = true;

        // The ServerSocket (aka Socket Factory!) we'll use for accepting new connections.
        private ServerSocket serverSocket;

        // A lookup table for the pre-generated output streams associated with each socket accepted - saves on allocations of new streams.
        private Dictionary<Socket, DataOutputStream> outputStreams = new Dictionary<Socket, DataOutputStream>();

        // Another lookup table for the sockets based on the supplied address for their recipient
        private Dictionary<string, Socket> connections = new Dictionary<string, Socket>();

        protected new string _tag = "WifiServer";
        private object _lock = new object();

        public event EventHandler<EventArgs<String>> OnServerCommandReceived;

        public WifiServer(CancellationToken? stopToken = null) : base(stopToken) { }

        public void Listen()
        {
            List<ServerThread> serverThreads = new List<ServerThread>();

            Task.Run(() =>
            {
                try
                {
                    // Create the ServerSocket
                    serverSocket = new ServerSocket();
                    var sAddr = new InetSocketAddress(Port);
                    serverSocket.ReuseAddress = true;
                    serverSocket.Bind(sAddr);
                    Log.Debug(_tag, $"Server listening on {Port}.");

                    while (!_cts.IsCancellationRequested)
                    {
                        // Grab the next incoming connection
                        Socket socket = serverSocket.Accept();
                        Log.Debug(_tag, $"Server accepted a connection from {socket.InetAddress.CanonicalHostName}.");

                        lock (_lock)
                        {
                            // Cache the socket, filed under its address
                            connections[socket.InetAddress.CanonicalHostName] = socket;

                            // Create and cache a DataOutputStream for sending data over it
                            var dOutStream = new DataOutputStream(socket.OutputStream);
                            outputStreams.Add(socket, dOutStream);
                        }

                        // Create a new thread for this connection
                        serverThreads.Add(new ServerThread(this, socket).Start());

                        // Ack back to the connection to let it know you're hearing it (and to pass it the address you'll know it by)
                        ForwardTo(socket.InetAddress.CanonicalHostName, SERVER, ACK + CONNECTED_AS + socket.InetAddress.CanonicalHostName);
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

        public void RemoveConnection(Socket s)
        {
            // Lock just in case we're in the middle of enumerating our sockets for something else (e.g. a broadcast signal)
            lock(_lock)
            {
                connections.Remove(s.InetAddress.CanonicalHostName);
                outputStreams.Remove(s);
                Log.Debug(_tag, $"Removing connection to {s.InetAddress.CanonicalHostName}");

                // Make sure it's closed
                try { s.Close(); }
                catch (Java.IO.IOException e)
                {
                    Log.Error(_tag, $"Error closing connection to {s.InetAddress.CanonicalHostName}");
                }
            }
        }

        public void Forward(string sender, string message)
        {
            
            foreach (string conn in connections.Keys)
            {
                if (conn != sender) 
                    ForwardTo(conn, sender, message);
            }
        }

        public void ForwardTo(string address, string sender, string message)
        {
            if (address == ALL)
            {
                foreach (string conn in connections.Keys) ForwardTo(conn, sender, message);
                return;
            }
            var recipientSocket = connections[address];
            var dOutStream = outputStreams[recipientSocket];

            SendString(dOutStream, $"{address}|{sender}|{message}");
        }

        public class ServerThread : WifiBaseClass
        {
            protected new string _tag = "ServerThread";

            // The server that spawned us
            private WifiServer server;

            // The socket that connects us to our (singular, for a given ServerThread) client.
            private Socket socket;

            public ServerThread(WifiServer server, Socket socket) : base(server.StopToken)
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
                bool Handled;

                while (!StopToken.IsCancellationRequested)
                {
                    Handled = true;

                    // Get the next message
                    try
                    {
                        var data = ReadString(dInStream).Split("|".ToArray(), 3);
                        if (data.Length != 3)
                        {
                            Log.Debug(_tag, $"Data has <3 entries; [0] is {data.ElementAtOrDefault(0)}, [1] is {data.ElementAtOrDefault(1)}, [2] is {data.ElementAtOrDefault(2)}");
                        }
                        var address = data.ElementAtOrDefault(0);
                        var sender = data.ElementAtOrDefault(1);
                        var message = data.ElementAtOrDefault(2);

                        //if (address != SERVER) server.ForwardTo(address, sender, message); 
                        //else server.OnServerCommandReceived?.Invoke(this, new EventArgs<string>($"{sender}|{message}"));
                        server.Forward(sender, message);

                        if (server.DoACK && !message.StartsWith(ACK))
                            server.ForwardTo(sender, "Server", $"{ACK}: relaying [{message}] from {sender}.");
                    }
                    catch (Java.IO.EOFException e)
                    {
                        Log.Debug(_tag, $"EOFException: {e.Message} ({e}).  Trying to reopen stream.");
                        dInStream.Dispose();
                        dInStream = new DataInputStream(socket.InputStream);
                        //Handled = true;
                        continue;
                    }
                    catch (Exception e)
                    {
                        if (e.InnerException != null && e.InnerException is Java.IO.EOFException)
                        {
                            //Log.Debug(_tag, $"Wrapped EOFException: {e}.  Trying to reopen stream.  Status of socket is: IsInputShutdown? {socket.IsInputShutdown}. KeepAlive? {socket.KeepAlive}. Linger? {socket.SoLinger}.  Timeout? {socket.SoTimeout}. ReuseAddress? {socket.ReuseAddress}. Channel? {socket.Channel}.");
                            //dInStream.Dispose();
                            //dInStream = new DataInputStream(socket.InputStream);
                            ////Handled = true;
                            continue;
                        }
                        else
                        {
                            Handled = false;
                            throw e;
                        }
                    }
                    finally
                    {
                        if (!Handled)
                        {
                            Log.Debug(_tag, $"ServerThread to {socket.InetAddress.CanonicalHostName} self-disrupted.");
                            Stop();
                        }
                    }
                }
            }

            public override string ReadString(DataInputStream inStream)
            {
                var result = base.ReadString(inStream);
                if (server.DoACK) // Serves as a proxy for "should I also report to the log file?" as well as "should I send ACKs on the comm channel?"
                    Log.Debug(_tag, $"Server received raw string [{result}]");
                return result;
            }
        }
    }
}