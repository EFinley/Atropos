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
                stream.WriteChar(END);
            });
        }

        public virtual string ReadString(DataInputStream inStream)
        {
            string resultString = String.Empty;
            var res = START;
            Task.Run(() =>
            {
                while (res != END && !StopToken.IsCancellationRequested)
                {
                    res = inStream.ReadChar();
                    //Log.Debug(_tag, $"Received '{res}' (aka {(char)res}).");
                    if (res != END) resultString += (char)res;
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
        public static string GROUPSERVER = "SERVER";
        public static string CONNECTED_AS = " - Connected as ";
        public static string INTRODUCING = "Introducing myself";

        public static char ACKchar = (char)6;
        public static char START = (char)2; // Not actually used, except as a placeholder value, but that could change.
        public static char LF = (char)10;
        public static char ENDTRANSBLOCK = (char)23;
        public static char GROUP_SEPARATOR = (char)29;
        public static char RECORD_SEPARATOR = (char)30;
        public static char NEXT = GROUP_SEPARATOR;
        public static char[] onNEXT = new char[] { NEXT }; // Because Split(onThis, numGroups) requires a char array as onThis.
        public static char END = LF;
    }

    public class WifiServer : WifiBaseClass
    {
        // We use a single fixed port number for all of our work here.
        public const int Port = 42445;

        // A boolean indicating whether this server should send ACKnowledgment messages
        public bool DoACK = false;

        // The ServerSocket (aka Socket Factory!) we'll use for accepting new connections.
        private ServerSocket serverSocket;

        //// A lookup table for the pre-generated output streams associated with each socket accepted - saves on allocations of new streams.
        //private Dictionary<Socket, DataOutputStream> outputStreams = new Dictionary<Socket, DataOutputStream>();

        // Another lookup table for the sockets based on the supplied address for their recipient
        private Dictionary<string, Socket> connections = new Dictionary<string, Socket>();
        private Dictionary<string, ServerThread> serverThreads = new Dictionary<string, ServerThread>();

        protected new string _tag = "WifiServer";
        private object _lock = new object();

        public event EventHandler<EventArgs<Message>> OnMessageReceived;
        //public event EventHandler<EventArgs<String>> OnServerCommandReceived;

        public WifiServer(CancellationToken? stopToken = null) : base(stopToken) { }

        public void HandleIncomingConnections()
        {
            //List<ServerThread> serverThreads = new List<ServerThread>();

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

                            //// Create and cache a DataOutputStream for sending data over it
                            //var dOutStream = new DataOutputStream(socket.OutputStream);
                            // outputStreams.Add(socket, dOutStream);
                        }

                        // Create a new thread for this connection
                        serverThreads[socket.InetAddress.CanonicalHostName] = new ServerThread(this, socket).Start();

                        //// Ack back to the connection to let it know you're hearing it (and to pass it the address you'll know it by)
                        //ForwardTo(socket.InetAddress.CanonicalHostName, GROUPSERVER, ACK + CONNECTED_AS + socket.InetAddress.CanonicalHostName);
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
                    foreach (var sThread in serverThreads.Values) sThread.Stop();
                }
            });
        }

        public void RemoveConnection(Socket s)
        {
            // Lock just in case we're in the middle of enumerating our sockets for something else (e.g. a broadcast signal)
            lock(_lock)
            {
                connections.Remove(s.InetAddress.CanonicalHostName);
                serverThreads.Remove(s.InetAddress.CanonicalHostName);
                //outputStreams.Remove(s);
                Log.Debug(_tag, $"Removing connection to {s.InetAddress.CanonicalHostName}");

                // Make sure it's closed
                try { s.Close(); }
                catch (Java.IO.IOException e)
                {
                    Log.Error(_tag, $"Error closing connection to {s.InetAddress.CanonicalHostName}");
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

        //    SendString(dOutStream, $"{address}{NEXT}{sender}{NEXT}{message}");
        //}

        #region Group-Owner Specific: List of intro messages, intro-message handling function
        public static List<string> GroupOwnerIntroMessageLog = new List<string>();
        public static void GroupOwnerForwardingHandler(object sender, EventArgs<Message> e)
        {
            if (e.Value.Content.StartsWith($"{INTRODUCING}{NEXT}") && !GroupOwnerIntroMessageLog.Contains(e.Value.Content))
            {
                var newIntroMessage = e.Value.Content;

                // Send the new introduction to all the previously signed-up folks...
                foreach (var target in AddressBook.Targets)
                {
                    WiFiMessageReceiver.Client.SendMessage(target.IPaddress, newIntroMessage);
                }

                // Also send the prior intro strings to the new guy...
                foreach (var priorIntro in GroupOwnerIntroMessageLog)
                {
                    WiFiMessageReceiver.Client.SendMessage(e.Value.From, priorIntro);
                }

                // ... And log it in with the other prior messages.
                GroupOwnerIntroMessageLog.Add(newIntroMessage);
            }

            // TODO?  We might want a "Please resend me all the intro messages" command which would be caught here.
            // Or anything else which only the group owner can do... although that's not many, in this setup.
        }
        #endregion

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
                // Create a DataInputStream for communication; the client is using a DataOutputStream to write to us.
                var dInStream = new DataInputStream(socket.InputStream);
                // We'll also want a DataOutputStream, very briefly, to send our ACK character.
                var dOutStream = new DataOutputStream(socket.OutputStream);
                bool Handled;

                while (!StopToken.IsCancellationRequested)
                {
                    Handled = false;

                    // Get the next message
                    try
                    {
                        var data = ReadString(dInStream).Split(onNEXT, 3);
                        if (data.Length != 3)
                        {
                            Log.Debug(_tag, $"Data has <3 entries; [0] is {data.ElementAtOrDefault(0)}, [1] is {data.ElementAtOrDefault(1)}, [2] is {data.ElementAtOrDefault(2)}");
                        }
                        var address = data.ElementAtOrDefault(0);
                        var sender = data.ElementAtOrDefault(1);
                        var message = data.ElementAtOrDefault(2);

                        // If it's not for me, ignore it.  Shouldn't happen in current code anyway.
                        if (!address.IsOneOf(WiFiMessageReceiver.MyIPaddress, ALL))
                        {
                            Log.Debug(_tag, $"Received \"{message}\" addressed to {address} (I'm {WiFiMessageReceiver.MyIPaddress}); ignoring it, but you should look into how it happened.");
                            continue;
                        }

                        dOutStream.WriteChar(ACKchar);

                        //if (server.DoACK && !message.StartsWith(ACK))
                        //    server.ForwardTo(sender, "Server", $"{ACK}: relaying [{message}] from {sender}.");

                        // Now handle the message contents
                        if (message.StartsWith($"{INTRODUCING}{NEXT}"))
                        {
                            var newTeammate = TeamMember.FromIntroductionString(message);
                            Log.Debug(_tag, $"Received introduction from {newTeammate.IPaddress}, giving their name ({newTeammate.Name}) and roles {newTeammate.Roles.Join()}.  Adding to address book.");
                            AddressBook.Add(newTeammate);
                        }
                        // If it doesn't fall into one of our special cases, handle it as a normal message.
                        else
                        {
                            server.OnMessageReceived.Raise(new Message()
                            {
                                From = sender,
                                To = address,
                                Content = message
                            });
                            //if (DoACK) SendMessage(sender, $"{ACK}: received [{message}] from {sender}.");
                        }
                    }
                    catch (Java.IO.EOFException e)
                    {
                        Log.Debug(_tag, $"EOFException: {e.Message} ({e}).  Logging & rethrowing.");
                        throw e;
                        //Log.Debug(_tag, $"EOFException: {e.Message} ({e}).  Trying to reopen stream.");
                        //dInStream.Dispose();
                        //dInStream = new DataInputStream(socket.InputStream);
                        //Handled = true;
                        //continue;
                    }
                    catch (Exception e)
                    {
                        if (e.InnerException != null && e.InnerException is Java.IO.EOFException)
                        {
                            Log.Debug(_tag, $"Wrapped EOFException: {e}.  Logging & rethrowing.  Status of socket is: IsInputShutdown? {socket.IsInputShutdown}. KeepAlive? {socket.KeepAlive}. Linger? {socket.SoLinger}.  Timeout? {socket.SoTimeout}. ReuseAddress? {socket.ReuseAddress}. Channel? {socket.Channel}.");
                            throw e.InnerException;
                            //Log.Debug(_tag, $"Wrapped EOFException: {e}.  Trying to reopen stream.  Status of socket is: IsInputShutdown? {socket.IsInputShutdown}. KeepAlive? {socket.KeepAlive}. Linger? {socket.SoLinger}.  Timeout? {socket.SoTimeout}. ReuseAddress? {socket.ReuseAddress}. Channel? {socket.Channel}.");
                            //dInStream.Dispose();
                            //dInStream = new DataInputStream(socket.InputStream);
                            ////Handled = true;
                            //continue;
                        }
                        else
                        {
                            //Handled = false;
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