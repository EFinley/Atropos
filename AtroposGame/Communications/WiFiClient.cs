//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;

//using Android.App;
//using Android.Content;
//using Android.OS;
//using Android.Runtime;
//using Android.Views;
//using Android.Widget;
//using Java.IO;
//using System.IO;
//using System.Threading.Tasks;
//using Nito.AsyncEx;
//using Android.Util;
//using Java.Net;
//using System.Threading;
//using MiscUtil;
//using Role = Atropos.Characters.Role;


//namespace Atropos.Communications
//{
//    public class WifiClient : WifiCore
//    {
//        protected new string _tag = "WifiClient";

//        // The socket connecting us to the server
//        private Socket socket;
//        private InetSocketAddress hostAddress;
//        public string myName = "My Name";
//        public string myAddress = "unknown";
//        public Role myRole = Role.Any;

//        public bool IsConnected = false;
//        public bool FailQuietly = true;

//        //public Dictionary<string, string> Addresses = new Dictionary<string, string>();
//        //public List<string> TeammateNames { get { return Addresses.Keys.ToList(); } }

//        // The streams we use to communicate with the server; these come from the socket.
//        protected DataInputStream inStream;
//        protected DataOutputStream outStream;

//        public event EventHandler<EventArgs> OnConnectionSuccess;
//        public event EventHandler<EventArgs> OnConnectionFailure;
//        public event EventHandler<EventArgs<string>> OnMessageSent;
//        public event EventHandler<EventArgs<string>> OnMessageReceived;
//        //public event EventHandler<EventArgs<CommsContact>> OnTeammateDetected;
//        public int SocketTimeout = 2500;

//        public bool DoACK = true;

//        public WifiClient(string hostname, CancellationToken? stopToken = null) : base(stopToken)
//        {
//            if (String.IsNullOrEmpty(hostname)) return;
//            hostAddress = new InetSocketAddress(hostname, WifiServer.Port);
//            _cts.Token.Register(() =>
//            {
//                inStream?.Dispose();
//                outStream?.Dispose();
//                Log.Debug(_tag, "CTS token fired.");
//            });
//        }

//        public virtual void Connect(string asWhatName, Role asWhatRole = Role.Any, int numberOfRetries = 5)
//        {
//            Task.Run(async () =>
//            {
//                try
//                {
//                    // Connect to the server
//                    socket = new Socket();
//                    socket.Bind(null);
                
//                    socket.Connect(hostAddress, SocketTimeout);
//                    if (socket.IsConnected)
//                    {
//                        Log.Debug(_tag, $"Client: Connection accepted to {socket.InetAddress.CanonicalHostName}.");
//                        OnConnectionSuccess?.Invoke(this, EventArgs.Empty);
//                        IsConnected = true;

//                        myAddress = socket.InetAddress.CanonicalHostName;
//                        myName = asWhatName;
//                        myRole = asWhatRole;
//                        //Addresses.Add(myName, myAddress);

//                        AddressBook.Add(new CommsContact()
//                        {
//                            Name = myName,
//                            Roles = new List<Role>() { asWhatRole, Role.Self },
//                            IPaddress = myAddress
//                        });
//                    }
//                    else
//                    {
//                        OnConnectionFailure?.Invoke(this, EventArgs.Empty);
//                        IsConnected = false;
//                        if (numberOfRetries > 0)
//                        {
//                            Log.Debug(_tag, $"Client: Connection unsuccessful; retrying in {SocketTimeout} ms.");
//                            await Task.Delay(SocketTimeout)
//                                .ContinueWith((_) => { Connect(asWhatName, asWhatRole, numberOfRetries - 1); })
//                                .ConfigureAwait(false);
//                        }
//                        else
//                        {
//                            Log.Debug(_tag, "Client: Connection unsuccessful, retry limit exhausted.");
//                        }
//                        return;
//                    }

//                    inStream = new DataInputStream(socket.InputStream);
//                    outStream = new DataOutputStream(socket.OutputStream);

//                    WifiCore.RunSendingLooper(this);

//                    await Task.Delay(250)
//                        .ContinueWith(_ => SendMessage($"{POLL_FOR_NAMES}"))
//                        .ConfigureAwait(false);

//                    await Task.Run((Action)Listen); // When finished, *should* mean that it's received its stop signal and is thus ready to close.

//                    Log.Debug(_tag, "Closing client connection normally(??).");
//                    socket.Close();
//                    IsConnected = false;
//                }
//                catch (Exception e)
//                {
//                    throw e;
//                }
//            });
//        }

//        public virtual void SendMessage(string message)
//        {
//            SendMessage(ALL, message);
//        }

//        public virtual void SendMessage(string toWhom, string message)
//        {
//            if (!IsConnected || !socket.IsConnected)
//            {
//                if (FailQuietly)
//                    Log.Error(_tag, $"No connection to communications server! Unable to send message ({message}) to {toWhom}.");
//                else
//                    throw new InvalidOperationException($"No connection to communications server! Unable to send message ({message}) to {toWhom}.");
//            }
//            else if (socket.IsClosed)
//            {
//                if (FailQuietly)
//                    Log.Error(_tag, $"Connection to communications server is closed. Unable to send message ({message}) to {toWhom}.");
//                else
//                    throw new InvalidOperationException($"Connection to communications server is closed. Unable to send message ({message}) to {toWhom}.");
//            }
//            else if (socket.IsOutputShutdown)
//            { 
//                if (FailQuietly)
//                    Log.Error(_tag, $"Connection to communications server is output-shutdown. Unable to send message ({message}) to {toWhom}.");
//                else
//                    throw new InvalidOperationException($"Connection to communications server is output-shutdown. Unable to send message ({message}) to {toWhom}.");
//            }

//            if (!AddressBook.IPaddresses.Contains(toWhom) && toWhom != ALL)
//                toWhom = AddressBook.IPaddresses[AddressBook.Targets.IndexOf(AddressBook.Resolve(toWhom))];

//            SendString(outStream, inStream, $"{toWhom}{NEXT}{myAddress}{NEXT}{message}");

//            if (!message.StartsWith(ACK)) OnMessageSent?.Invoke(this, new EventArgs<string>($"(To {toWhom}) {message}"));
//        }

//        public virtual void SendMessage(Message message)
//        {
//            //if (!String.IsNullOrEmpty(message.To))
//            //    SendMessage(message.To, message.Content);
//            //else SendMessage(ALL, message.Content);
//        }
//        public virtual void SendMessage(string toWhom, Message message)
//        {
//            //message.To = toWhom;
//            //SendMessage(message);
//        }

        

//        private void Listen()
//        {
//            while (!StopToken.IsCancellationRequested)
//            {
//                // Get the next message
//                var data = ReadString(inStream, outStream).Split(onNEXT, 3); // Limiting it to three split pieces means that all the NEXTs inside the message (which is the third piece) remain intact.
//                if (data.Length != 3)
//                {
//                    Log.Debug(_tag, $"Data has <3 entries; [0] is {data.ElementAtOrDefault(0)}, [1] is {data.ElementAtOrDefault(1)}, [2] is {data.ElementAtOrDefault(2)}");
//                }

//                // Parse its components
//                var address = data.ElementAtOrDefault(0);
//                var sender = data.ElementAtOrDefault(1);
//                var message = data.ElementAtOrDefault(2);

//                // If it's not for me, ignore it.  Shouldn't happen in current code anyway.  Nor should receiving ALL *from* the server - it should only exist when sending *to* the server.
//                if (address == ALL) Log.Debug(_tag, $"Received \"{Readable(message)}\" addressed to ALL, clientside; shouldn't happen, debug server.");
//                else if (address != myAddress)
//                {
//                    Log.Debug(_tag, $"Received \"{Readable(message)}\" addressed to {Readable(address)} (I'm {Readable(myAddress)}); I ought to ignore it, but parsing it anyway, 'cause WTF.");
//                    //continue;
//                }

//                //if (message.StartsWith(CMD))
//                //    HandleClientCommand(sender, message.Replace($"{CMD}{NEXT}", ""));

//                // Parse some special cases - name polling and ACKnowledgments.
//                if (message == POLL_FOR_NAMES)
//                {
//                    SendMessage($"{ACK}{NEXT}{POLL_RESPONSE}{NEXT}{myName}{NEXT}{myRole}");
//                    Log.Debug(_tag, $"Received {POLL_FOR_NAMES} from {sender}; replying with my name ({myName}) and role ({myRole}).");
//                }
//                else if (message.StartsWith($"{ACK}{NEXT}{POLL_RESPONSE}{NEXT}"))
//                {
//                    var respName = message.Split(onNEXT)[2];
//                    var respRole = message.Split(onNEXT)[3];
//                    Log.Debug(_tag, $"Received {POLL_RESPONSE} from {sender}, giving their name ({respName}) and role ({respRole}).  Adding to address book.");
//                    AddressBook.Add(new CommsContact()
//                    {
//                        Name = respName,
//                        Roles = new List<Role>() { (Role)Enum.Parse(typeof(Role), respRole) }, // Turns the string "Hitter" into Role.Hitter, etc.
//                        IPaddress = sender
//                    });
//                    WiFiDirectActivity.Current.RefreshDetails();
//                }
//                else if (message.StartsWith(ACK)) 
//                {
//                    if (DoACK) // Serving as a proxy for "should I send that to the output window?" as well as "should I send out ACKs myself?"
//                    {
//                        //Log.Debug(_tag, $"ACKback: {Readable(message)}");
//                    }
//                }

//                // If it doesn't fall into one of our special cases, handle it as a normal message.
//                else 
//                {
//                    OnMessageReceived?.Invoke(this, new EventArgs<string>(message));
//                    //WiFiMessageCenter.ActOnMessage(new Message() { From = sender, To = address, Content = message });
//                    if (DoACK) SendMessage(sender, $"{ACK}: received [{Readable(message)}] from {sender}.");
//                }
//            }
//            Log.Debug(_tag, $"Reached end of Listen() loop - about to close connection.");
//        }

//        public override string ReadString(DataInputStream inStream, DataOutputStream outStream)
//        {
//            var result = base.ReadString(inStream, outStream);
//            if (DoACK) // Serves as a proxy for "should I also report to the log file?" as well as "should I send ACKs on the comm channel?"
//                Log.Debug(_tag, $"Client received raw string [{Readable(result)}]");
//            return result;
//        }

//        //private void HandleClientCommand(string sender, string commandMessage)
//        //{

//        //}

//        private class NullClientClass : WifiClient
//        {
//            public NullClientClass() : base(null, CancellationToken.None)
//            {
//                IsConnected = true;
//            }

//            public override void Connect(string asWhatName, Role asWhatRole, int numberOfRetries = 5)
//            {
//                myName = asWhatName;
//            }

//            public override void SendMessage(string message)
//            {
//            }

//            public override void SendMessage(string toWhom, string message)
//            {
//            }
//        }

//        public static WifiClient NullClient = new NullClientClass();
//    }
//}