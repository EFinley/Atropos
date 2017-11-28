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

namespace com.Atropos.Communications
{
    public class WifiClient : WifiBaseClass
    {
        protected new string _tag = "WifiClient";

        // The socket connecting us to the server
        private Socket socket;
        private InetSocketAddress hostAddress;
        public string myName = "My Name";
        public string myAddress = "unknown";
        public Role myRole = Role.Any;

        public bool IsConnected = false;
        public bool FailQuietly = true;

        //public Dictionary<string, string> Addresses = new Dictionary<string, string>();
        //public List<string> TeammateNames { get { return Addresses.Keys.ToList(); } }

        // The streams we use to communicate with the server; these come from the socket.
        private DataInputStream inStream;
        private DataOutputStream outStream;

        public event EventHandler<EventArgs> OnConnectionSuccess;
        public event EventHandler<EventArgs> OnConnectionFailure;
        public event EventHandler<EventArgs<string>> OnMessageSent;
        public event EventHandler<EventArgs<string>> OnMessageReceived;
        //public event EventHandler<EventArgs<TeamMember>> OnTeammateDetected;
        public int SocketTimeout = 2500;

        public bool DoACK = true;

        public WifiClient(string hostname, CancellationToken? stopToken = null) : base(stopToken)
        {
            hostAddress = new InetSocketAddress(hostname, WifiServer.Port);
            _cts.Token.Register(() =>
            {
                inStream.Dispose();
                outStream.Dispose();
            });
        }

        public virtual void Connect(string asWhatName, Role asWhatRole = Role.Any, int numberOfRetries = 5)
        {
            Task.Run(async () =>
            {
                try
                {
                    // Connect to the server
                    socket = new Socket();
                    socket.Bind(null);

                    socket.Connect(hostAddress, SocketTimeout);
                    if (socket.IsConnected)
                    {
                        Log.Debug(_tag, $"Client: Connection accepted to {socket.InetAddress.CanonicalHostName}.");
                        OnConnectionSuccess?.Invoke(this, EventArgs.Empty);
                        IsConnected = true;

                        myAddress = socket.LocalAddress.CanonicalHostName;
                        myName = asWhatName;
                        myRole = asWhatRole;
                        //Addresses.Add(myName, myAddress);

                        AddressBook.Add(new TeamMember()
                        {
                            Name = myName,
                            Roles = { asWhatRole, Role.Self },
                            IPaddress = myAddress
                        });

                        await Task.Delay(250)
                            .ContinueWith(_ => SendMessage($"{POLL_FOR_NAMES}"))
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        OnConnectionFailure?.Invoke(this, EventArgs.Empty);
                        IsConnected = false;
                        if (numberOfRetries > 0)
                        {
                            Log.Debug(_tag, $"Client: Connection unsuccessful; retrying in {SocketTimeout} ms.");
                            await Task.Delay(SocketTimeout)
                                .ContinueWith((_) => { Connect(asWhatName, asWhatRole, numberOfRetries - 1); })
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            Log.Debug(_tag, "Client: Connection unsuccessful, retry limit exhausted.");
                        }
                        return;
                    }

                    inStream = new DataInputStream(socket.InputStream);
                    outStream = new DataOutputStream(socket.OutputStream);

                    await Task.Run((Action)Listen);
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    socket.Close();
                    IsConnected = false;
                }
            });
        }

        public virtual void SendMessage(string message)
        {
            SendMessage(ALL, message);
        }

        public virtual void SendMessage(string toWhom, string message)
        {
            if (!IsConnected || !socket.IsConnected)
            {
                if (FailQuietly)
                    Log.Error(_tag, $"No connection to communications server! Unable to send message ({message}) to {toWhom}.");
                else
                    throw new InvalidOperationException($"No connection to communications server! Unable to send message ({message}) to {toWhom}.");
            }
            else if (socket.IsClosed)
            {
                if (FailQuietly)
                    Log.Error(_tag, $"Connection to communications server is closed. Unable to send message ({message}) to {toWhom}.");
                else
                    throw new InvalidOperationException($"Connection to communications server is closed. Unable to send message ({message}) to {toWhom}.");
            }

            if (!AddressBook.IPaddresses.Contains(toWhom))
                toWhom = AddressBook.IPaddresses[AddressBook.Targets.IndexOf(AddressBook.Resolve(toWhom))];

            SendString(outStream, $"{toWhom}|{myAddress}|{message}");

            if (!message.StartsWith(ACK)) OnMessageSent?.Invoke(this, new EventArgs<string>($"(To {toWhom}) {message}"));
        }

        public virtual void SendMessage(Message message)
        {

        }
        public virtual void SendMessage(string toWhom, Message message)
        {

        }

        

        private void Listen()
        {
            while (!StopToken.IsCancellationRequested)
            {
                // Get the next message
                var data = ReadString(inStream).Split("|".ToArray(), 3);
                if (data.Length != 3)
                {
                    Log.Debug(_tag, $"Data has <3 entries; [0] is {data.ElementAtOrDefault(0)}, [1] is {data.ElementAtOrDefault(1)}, [2] is {data.ElementAtOrDefault(2)}");
                }

                // Parse its components
                var address = data.ElementAtOrDefault(0);
                var sender = data.ElementAtOrDefault(1);
                var message = data.ElementAtOrDefault(2);

                // If it's not for me, ignore it.  Shouldn't happen in current code anyway.  Nor should receiving ALL *from* the server - it should only exist when sending *to* the server.
                if (!address.IsOneOf(myAddress, ALL)) continue;

                //if (message.StartsWith(CMD))
                //    HandleClientCommand(sender, message.Replace($"{CMD}|", ""));

                // Parse some special cases - name polling and ACKnowledgments.
                if (message == POLL_FOR_NAMES)
                    SendMessage($"{ACK}|{POLL_RESPONSE}|{myName}|{myRole}"); 
                else if (message.StartsWith($"{ACK}|{POLL_RESPONSE}|"))
                {
                    var respName = message.Split('|')[2];
                    var respRole = message.Split('|')[3];
                    AddressBook.Add(new TeamMember()
                    {
                        Name = respName,
                        Role = (Role)Enum.Parse(typeof(Role), respRole),
                        IPaddress = sender
                    });
                }
                else if (message.StartsWith(ACK)) 
                {
                    if (DoACK) // Serving as a proxy for "should I send that to the output window?" as well as "should I send out ACKs myself?"
                        Log.Debug(_tag, $"ACKback: {message}");
                }

                // If it doesn't fall into one of our special cases, handle it as a normal message.
                else 
                {
                    OnMessageReceived?.Invoke(this, new EventArgs<string>(message));
                    if (DoACK) SendMessage(sender, $"{ACK}: received [{message}] from {sender}.");
                }
            }
        }

        //private void HandleClientCommand(string sender, string commandMessage)
        //{

        //}

        private class NullClientClass : WifiClient
        {
            public NullClientClass() : base(null, CancellationToken.None)
            {
                IsConnected = true;
            }

            public override void Connect(string asWhatName, Role asWhatRole, int numberOfRetries = 5)
            {
                myName = asWhatName;
            }

            public override void SendMessage(string message)
            {
            }

            public override void SendMessage(string toWhom, string message)
            {
            }
        }

        public static WifiClient NullClient = new NullClientClass();
    }
}