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

        public Dictionary<string, string> Addresses = new Dictionary<string, string>();

        // The streams we use to communicate with the server; these come from the socket.
        private DataInputStream inStream;
        private DataOutputStream outStream;

        public event EventHandler<EventArgs> OnConnectionSuccess;
        public event EventHandler<EventArgs> OnConnectionFailure;
        public event EventHandler<EventArgs<string>> OnMessageSent;
        public event EventHandler<EventArgs<string>> OnMessageReceived;
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

        public void Connect(string asWhatName, int numberOfRetries = 5)
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
                        myAddress = socket.LocalAddress.CanonicalHostName;
                        myName = asWhatName;
                        Addresses.Add(myName, myAddress);
                        //Task.Delay(250)
                        //    .ContinueWith(_ => SendMessage("ALL", $"{POLL_FOR_NAMES}"))
                        //    .ConfigureAwait(false);
                    }
                    else
                    {
                        Log.Debug(_tag, $"Client: Connection unsuccessful.");
                        OnConnectionFailure?.Invoke(this, EventArgs.Empty);
                        if (numberOfRetries > 0)
                            await Task.Delay(SocketTimeout)
                                .ContinueWith((_) => { Connect(asWhatName, numberOfRetries - 1); })
                                .ConfigureAwait(false);
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
                }
            });
        }

        public void SendMessage(string toWhom, string message)
        {
            if (!socket.IsConnected) throw new InvalidOperationException($"Cannot send message without a connection!");
            if (socket.IsClosed) throw new InvalidOperationException($"Cannot send message over a closed connection!");

            if (Addresses.ContainsKey(toWhom))
                toWhom = Addresses[toWhom];

            SendString(outStream, $"{toWhom}|{myAddress}|{message}");

            if (!message.StartsWith(ACK)) OnMessageSent?.Invoke(this, new EventArgs<string>($"(To {toWhom}) {message}"));
        }

        public void SendMessage(string message)
        {
            SendMessage(ALL, message);
        }

        private void Listen()
        {
            try
            {
                while (!StopToken.IsCancellationRequested)
                {
                    // Get the next message
                    var data = ReadString(inStream).Split("|".ToArray(), 3);
                    if (data.Length != 3)
                    {
                        Log.Debug(_tag, $"Data has <3 entries; [0] is {data.ElementAtOrDefault(0)}, [1] is {data.ElementAtOrDefault(1)}, [2] is {data.ElementAtOrDefault(2)}");
                    }
                    var address = data.ElementAtOrDefault(0);
                    var sender = data.ElementAtOrDefault(1);
                    var message = data.ElementAtOrDefault(2);

                    if (address != myAddress) continue;

                    if (!message.StartsWith(ACK)) OnMessageReceived?.Invoke(this, new EventArgs<string>(message));

                    //if (message.StartsWith(CMD))
                    //    HandleClientCommand(sender, message.Replace($"{CMD}|", ""));

                    //if (message == POLL_FOR_NAMES)
                    //    SendMessage(ALL, $"{ACK}|{POLL_RESPONSE}|{myName}");
                    //else if (message.StartsWith($"{ACK}|{POLL_RESPONSE}|"))
                    //{
                    //    Addresses[message.Split('|')[2]] = sender;
                    //}
                    //else if (DoACK && !message.StartsWith(ACK))
                    if (DoACK && !message.StartsWith(ACK))
                        SendMessage(sender, $"{ACK}: received [{message}].");
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        //private void HandleClientCommand(string sender, string commandMessage)
        //{

        //}

        
    }
}