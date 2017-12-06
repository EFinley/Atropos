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
using MiscUtil;

namespace Atropos.Communications
{
    public class WifiClient : WifiBaseClass
    {
        protected new string _tag = "WifiClient";

        public string myName;
        public string myAddress;
        public Role myRole = Role.Any;

        //public Dictionary<string, Socket> Connections = new Dictionary<string, Socket>();
        public Dictionary<string, ClientThread> ClientThreads = new Dictionary<string, ClientThread>();

        public event EventHandler<EventArgs<string>> OnConnectionSuccess; // The string in this case is the IP address in question.
        public event EventHandler<EventArgs<string>> OnConnectionFailure; // Ditto.
        public event EventHandler<EventArgs<Message>> OnMessageSent;

        public bool DoACK = true;

        public WifiClient(CancellationToken? stopToken = null) : base(stopToken) { }

        
        public virtual Task Connect(string hostName, int numRetries = 5)
        {
            return Task.Run(() =>
            {
                // Do we already have an open connection to the target?  If so, fine, we're done.
                if (ClientThreads.TryGetValue(hostName, out ClientThread cThread) && cThread.IsConnected) return;

                // More normally, we need to establish a connection and store it as a ClientThread.
                var clientThread = new ClientThread(this, hostName);
                clientThread.Connect(numRetries);
                ClientThreads[hostName] = clientThread;
            });
        }

        public virtual void Disconnect(string hostName)
        {
            if (!ClientThreads.TryGetValue(hostName, out ClientThread cThread)) return;
            ClientThreads.Remove(hostName);
            cThread.Stop();
        }

        public virtual void SendMessage(string message)
        {
            SendMessage(ALL, message);
        }
        public virtual void SendMessage(string toWhom, string message)
        {
            // We allow names (or potentially even roles) in the toWhom field... that's what AddressBook.Resolve() is for.
            // But ultimately we need just an IP address.
            if (!AddressBook.IPaddresses.Contains(toWhom) && toWhom != ALL)
                toWhom = AddressBook.IPaddresses[AddressBook.Targets.IndexOf(AddressBook.Resolve(toWhom))];

            else if (toWhom == ALL)
            {
                foreach (var tgt in AddressBook.Targets)
                    SendMessage(tgt.IPaddress, message);
                return;
            }

            Connect(toWhom); // No-op if already connected.
            var cThread = ClientThreads[toWhom];

            //if (!socket.IsConnected)
            //{
            //    if (FailQuietly)
            //        Log.Error(_tag, $"No connection to communications server! Unable to send message ({message}) to {toWhom}.");
            //    else
            //        throw new InvalidOperationException($"No connection to communications server! Unable to send message ({message}) to {toWhom}.");
            //}
            //else if (socket.IsClosed)
            //{
            //    if (FailQuietly)
            //        Log.Error(_tag, $"Connection to communications server is closed. Unable to send message ({message}) to {toWhom}.");
            //    else
            //        throw new InvalidOperationException($"Connection to communications server is closed. Unable to send message ({message}) to {toWhom}.");
            //}

            Task.Run(async () =>
            {
                message = $"{toWhom}{NEXT}{myAddress}{NEXT}{message}";
                var SendingTask = cThread.TrySendMessage(message);
                if (!await SendingTask)
                {
                    Disconnect(toWhom);
                    await Task.Delay(50);
                    Connect(toWhom, 0);
                    Log.Debug(_tag, $"Retrying connection to {toWhom}, resending {message}.");
                    if (!await cThread.TrySendMessage(message)) return;
                }
                if (!message.StartsWith(ACK)) OnMessageSent.Raise(message);
            });
        }

        // Overloads to take a Message rather than a string - presumably because the sender etc. are already embedded.
        public virtual void SendMessage(Message message)
        {
            if (!String.IsNullOrEmpty(message.To))
                SendMessage(message.To, message.Content);
            else SendMessage(ALL, message.Content);
        }
        public virtual void SendMessage(string toWhom, Message message)
        {
            message.To = toWhom;
            SendMessage(message);
        }

        //private void HandleClientCommand(string sender, string commandMessage)
        //{

        //}

        public void RegisterInfo(string groupOwnerAddress, string asWhom, string atAddress, params Role[] roles)
        {
            // Step 1: Define myself as a TeamMember, put myself in my address book
            var myself = new TeamMember()
            {
                Name = asWhom,
                IPaddress = atAddress,
                Roles = roles.ToList()
            };
            myself.Roles.Add(Role.Self);
            AddressBook.Add(myself);
            myName = myself.Name;
            myRole = myself.Role;

            // Step 2: Set up a message handler to manage it when an introduction message arrives
            WiFiMessageReceiver.Server.OnMessageReceived += (o, e) =>
            {
                if (e.Value.Content.StartsWith($"{INTRODUCING}{NEXT}"))
                {
                    var introducedTeamMember = TeamMember.FromIntroductionString(e.Value.Content);
                    if (introducedTeamMember.IPaddress != myAddress)
                        AddressBook.Add(introducedTeamMember);
                }
            };

            // Step 3: Send out our own introduction message (or "archive" it awaiting player #2's introduction message).
            if (groupOwnerAddress != myself.IPaddress)
                SendMessage(groupOwnerAddress, myself.IntroductionString());
            else WifiServer.GroupOwnerIntroMessageLog.Add(myself.IntroductionString());
        }

        public class ClientThread : WifiBaseClass
        {
            protected new string _tag = "ClientThread";

            // The client that spawned us
            private WifiClient client;

            // The socket that connects us to our (singular, for a given ClientThread) server-endpoint.
            private Socket socket;
            public bool IsConnected { get { return socket?.IsConnected ?? false; } }

            public int SocketTimeout = 2500;
            private string hostAddress;


            public ClientThread(WifiClient client, string hostAddress) : base(client.StopToken)
            {
                this.client = client;
                this.hostAddress = hostAddress; //new InetSocketAddress(hostAddress, WifiServer.Port);
            }

            public void Connect(int numberOfRetries = 5)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        // Connect to the server
                        socket = new Socket();
                        socket.Bind(null);
                        socket.Connect(new InetSocketAddress(hostAddress, WifiServer.Port), SocketTimeout);

                        if (socket.IsConnected)
                        {
                            Log.Debug(_tag, $"Client: Connection accepted to {socket.InetAddress.CanonicalHostName}.");
                            client.OnConnectionSuccess.Raise(this, hostAddress);

                            // Weirdly, up to this point we don't necessarily know our own IP address; let's fix that.
                            client.myAddress = socket.LocalAddress.CanonicalHostName;
                            
                        }
                        else
                        {
                            client.OnConnectionFailure.Raise(this, hostAddress);
                            if (numberOfRetries > 0)
                            {
                                Log.Debug(_tag, $"Client: Connection unsuccessful; retrying in {SocketTimeout} ms.");
                                await Task.Delay(SocketTimeout)
                                    .ContinueWith((_) => { Connect(numberOfRetries - 1); })
                                    .ConfigureAwait(false);
                                return;
                            }
                            else
                            {
                                Log.Debug(_tag, "Client: Connection unsuccessful, retry limit exhausted.");
                                _cts.Cancel();
                            }
                            return;
                        }

                        //inStream = new DataInputStream(socket.InputStream);
                        //outStream = new DataOutputStream(socket.OutputStream);

                        // Now, instead of exiting this function, pass off control but remain pending;
                        // when our StopToken is cancelled, finish off with your finally clause.
                        await _cts.Token.AsTask();
                    }
                    finally
                    {
                        socket.Close();
                    }
                });
            }

            //public ClientThread Start() // "Fluent" idiom
            //{
            //    Task.Run((Action)Run);
            //    return this;
            //}

            public void Stop()
            {
                _cts.Cancel(); // No-op if already done.
                client.Disconnect(hostAddress); // Likewise.
            }

            public async Task<bool> TrySendMessage(string message)
            {
                DataOutputStream dOutStream = null;
                DataInputStream dInStream = null;
                bool SendingSuccess = false;
                TimeSpan timeOut = TimeSpan.FromMilliseconds(1500);

                try
                {
                    // Create a DataOutputStream for communication; the server is using a DataInputStream to listen to us.
                    dOutStream = new DataOutputStream(socket.OutputStream);
                    // We'll also want a DataInputStream, very briefly, to receive our anticipated ACK character.
                    dInStream = new DataInputStream(socket.InputStream);

                    // We have three end conditions... we cancel it externally, it times out, or we succeed.
                    //var CancelTask = StopToken.AsTask(); // But this one is taken care of by StopToken inside the Task.Run() below.
                    var TimeoutTask = Task.Delay(timeOut);
                    var SendingTask = Task.Run(() =>
                    {
                        SendString(dOutStream, message);
                        var ackback = dInStream.ReadChar(); // Block until we receive a single character - ascii ACK - on the opposite line.
                        if (ackback != ACKchar)
                            throw new TaskCanceledException($"Somehow we received {ackback} (#{(int)ackback}) rather than ACK (#{(int)ACKchar}); no idea why.");
                    }, StopToken);

                    // WhenAny's semantics are, frankly, fucked up.  The WhenAny is returning a Task<Task>,
                    // whose inner Task is guaranteed to be completed / faulted / cancelled (and to be the
                    // first one that happened to).  So we have to await both (or just reference the Result
                    // of the inner one, since it's done already, but that has issues with no-Result-because-cancelled etc).
                    await (await Task.WhenAny(TimeoutTask, SendingTask).ConfigureAwait(false))
                        .ContinueWith(t => { SendingSuccess = (t == SendingTask && t.Status == TaskStatus.RanToCompletion); })
                        .ConfigureAwait(false);
                }
                finally
                {
                    dOutStream?.Close();
                    dInStream?.Close();
                }
                return SendingSuccess;
            }
        }

        private class NullClientClass : WifiClient
        {
            protected new string _tag = "Null_WifiClient";

            public NullClientClass() : base(CancellationToken.None)
            {
                //IsConnected = true;
            }

            public override Task Connect(string hostname, int numberOfRetries = 5)
            {
                myName = "NullClient";
                myRole = Role.None;
                return Task.CompletedTask;
            }

            public override void SendMessage(string message)
            {
                Log.Debug(_tag, $"Discarding message <<{message}>> without acknowledgment.");
            }

            public override void SendMessage(string toWhom, string message)
            {
                Log.Debug(_tag, $"Discarding message <<{message}>> to ({toWhom}), without acknowledgment.");
            }
        }

        public static WifiClient NullClient = new NullClientClass();
    }
}