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
using Role = Atropos.Characters.Role;
using Android.Bluetooth;

namespace Atropos.Communications.Bluetooth
{
    public class BluetoothClient : BluetoothCore
    {
        protected new string _tag = "BluetoothClient";

        // The socket connecting us to the server
        private BluetoothSocket socket;

        // The streams we use to communicate with the server; these come from the socket.
        protected DataInputStream inStream;
        protected DataOutputStream outStream;

        public event EventHandler<EventArgs> OnConnectionSuccess;
        public event EventHandler<EventArgs> OnConnectionFailure;
        public event EventHandler<EventArgs> OnDisconnection;
        public event EventHandler<EventArgs<Message>> OnMessageSent;
        public event EventHandler<EventArgs<Message>> OnMessageReceived;
        //public event EventHandler<EventArgs<CommsContact>> OnTeammateDetected;
        public int SocketTimeout = 2500;

        public virtual bool IsConnected { get; set; } = false;
        public bool DoACK = true;
        private bool FailQuietly = false;

        public BluetoothClient(CancellationToken? stopToken = null) : base(stopToken)
        {
            _cts.Token.Register(() =>
            {
                inStream?.Dispose();
                outStream?.Dispose();
                socket?.Close();
                OnDisconnection?.Invoke(this, EventArgs.Empty);
                Log.Debug(_tag, "CTS token fired.");
            });
        }

        public virtual void Connect(BTPeer peer, int numberOfRetries = 5)
        {
            Task.Run(async () =>
            {
                try
                {
                    socket = peer.Device.CreateRfcommSocketToServiceRecord(BluetoothServer.ServiceUUID);
                    socket.Connect();

                    if (socket.IsConnected)
                    {
                        Log.Debug(_tag, $"Client: Connection accepted to {socket.RemoteDevice.Address}.");
                        OnConnectionSuccess?.Invoke(this, EventArgs.Empty);
                        IsConnected = true;
                    }
                    else
                    {
                        OnConnectionFailure?.Invoke(this, EventArgs.Empty);
                        IsConnected = false;
                        if (numberOfRetries > 0)
                        {
                            Log.Debug(_tag, $"Client: Connection unsuccessful; retrying in {SocketTimeout} ms.");
                            await Task.Delay(SocketTimeout)
                                .ContinueWith((_) => { Connect(peer, numberOfRetries - 1); })
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

                    BluetoothCore.RunSendingLooper(this);

                    await Task.Delay(250)
                        .ContinueWith(_ =>
                            SendMessage(new Message(MsgType.Notify,
                                $"{CONFIRM_AS_CLIENT}{NEXT}{socket.RemoteDevice.Address}")))
                        .ConfigureAwait(false);

                    await Task.Delay(100);
                    var ListeningLoop = Task.Run((Action)Listen); // When finished, *should* mean that it's received its stop signal and is thus ready to close.

                    //foreach (var teammate in HeyYou.MyTeammates.TeamMembers)
                    //{
                    //    var teamMate = teammate as CommsContact;
                    //    if (teamMate == null) continue;
                    //    if (teamMate.IPaddress == socket.RemoteDevice.Address) continue; // Don't bother suggesting themselves as a teammate!
                    //    SendMessage(new Message(MsgType.Notify, $"{SUGGEST_TEAMMATE}{NEXT}{teamMate.IPaddress}"));
                    //}

                    await ListeningLoop;

                }
                catch (Exception e)
                {
                    Log.Debug(_tag, e.ToString());
                }
                finally
                {
                    Log.Debug(_tag, "Closing client connection normally (I think).");
                    //socket.Close();
                    //IsConnected = false;
                    Disconnect();
                }
            });
        }

        public virtual void Disconnect()
        {
            _cts.Cancel();
        }

        public virtual void SendMessage(IMessage message)
        {
            if (!IsConnected || !socket.IsConnected)
            {
                if (FailQuietly)
                    Log.Error(_tag, $"No connection to communications server! Unable to send message ({message}) to {socket.RemoteDevice.Address}.");
                else
                    throw new InvalidOperationException($"No connection to communications server! Unable to send message ({message}) to {socket.RemoteDevice.Address}.");
            }

            //if (!AddressBook.IPaddresses.Contains(socket.RemoteDevice.Address) && toWhom != ALL)
            //    toWhom = AddressBook.IPaddresses[AddressBook.Targets.IndexOf(AddressBook.Resolve(toWhom))];

            SendString(outStream, inStream, ((Message)message).ToCharStream());

            //if (message.Type != MsgType.Ack) OnMessageSent.Raise(message);
            //if (message.Type == MsgType.Query) messagesAwaitingReply.Add(message.ID, message);
        }

        //private Dictionary<string, Message> messagesAwaitingReply = new Dictionary<string, Message>();
        private int missedListenAttempts = 0;
        private void TallyMissedAttempt()
        {
            missedListenAttempts++;
            Task.Delay(100).Wait();
        }
        private void Listen()
        {
            while (!StopToken.IsCancellationRequested && missedListenAttempts < 60)
            {
                try
                {
                    // Get the next inbound message
                    var data = ReadString(inStream, outStream);
                    var message = Message.FromCharStream(socket.RemoteDevice.Address, data);

                    if (message.Type == MsgType.Ack) Log.Info(_tag, $"Received server ack of {message.Content}");
                    else
                    {
                        OnMessageReceived.Raise(message);
                        if (DoACK) SendMessage(new Message(MsgType.Ack, $"{message.Type}: {message.Content}"));
                    }
                }
                catch (Java.IO.IOException)  // Signals one end or the other of the pipe being closed when we go to read from it.
                {
                    //Disconnect();
                    TallyMissedAttempt();
                }
                catch (System.AggregateException e)
                {
                    if (e.InnerExceptions.All(ex => ex is Java.IO.IOException || ex.InnerException is Java.IO.IOException)) TallyMissedAttempt(); //Disconnect();
                    else throw e;
                }
                catch (Exception e)
                {
                    if (e.InnerException is Java.IO.IOException) TallyMissedAttempt(); // Disconnect();
                    else throw e;
                }
            }
            Log.Debug(_tag, $"Reached end of Listen() loop - about to close connection.");
        }

        //public override string ReadString(DataInputStream inStream, DataOutputStream outStream)
        //{
        //    var result = base.ReadString(inStream, outStream);
        //    if (DoACK) // Serves as a proxy for "should I also report to the log file?" as well as "should I send ACKs on the comm channel?"
        //        Log.Debug(_tag, $"Client received raw string [{Readable(result)}]");
        //    return result;
        //}

        private class NullClientClass : BluetoothClient
        {
            public NullClientClass() : base(CancellationToken.None) { }

            public override bool IsConnected { get => true; set { } }


            public override void Connect(BTPeer peer, int numberOfRetries = 5)
            {
                throw new NotImplementedException();
            }

            public override void SendMessage(IMessage message)
            {
            }
            
        }

        public class SelfClientClass : BluetoothClient
        {
            public SelfClientClass() : base(CancellationToken.None) { }

            public override bool IsConnected { get => true; set { } }

            public override void SendMessage(IMessage message)
            {
                BluetoothMessageCenter.ActOnMessage((Message)message);
            }
        }

        public class GroupClientClass : BluetoothClient
        {
            private CommsGroup _group;
            public GroupClientClass(CommsGroup group, CancellationToken? externalToken = null) : base(externalToken)
            {
                _group = group;
            }

            public override void SendMessage(IMessage message)
            {
                foreach (var contact in _group.Contacts) SendMessage(new Message(message.Type, message.Content));
            }
        }
    }
}