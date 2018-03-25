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

namespace Atropos.Communications.Bluetooth
{
    public class BluetoothCore // Kind of like a stripped-down version of Activator, without the need to Activate it.  Might become full Activator in future.
    {
        protected string _tag = "BluetoothCore";

        // A central cancellation coordinator
        protected CancellationTokenSource _cts;
        public CancellationToken StopToken { get { return _cts?.Token ?? CancellationToken.None; } }

        public BluetoothCore(CancellationToken? stopToken = null)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(stopToken ?? CancellationToken.None);
        }

        public static void SendString(DataOutputStream outStream, DataInputStream inStream, string message)
        {
            Task.Run(async () =>
            {
                //outStream.WriteChar(START);
                //outStream.WriteChars(message);
                //outStream.WriteChar(END);

                // We use a queue mechanism to make sure that outbound messages go out in an orderly fashion.  Can't do anything about inbound, though; eventually
                // that'll need fixing, but hopefully in more knowledgeable hands than mine.
                using (await Lock.LockAsync())
                {
                    OutStreamQueue.Enqueue(outStream);
                    MessageQueue.Enqueue(START + message + END);
                    MessageReadyFlag.Set();
                }

                //var cts = new CancellationTokenSource();
                //Task.Delay(1500, cts.Token)
                //    .ContinueWith(t => { if (!cts.IsCancellationRequested) Log.Debug("WifiBaseClass", $"Received no ACK after sending {Readable(message)}."); }, TaskContinuationOptions.OnlyOnRanToCompletion)
                //    .ConfigureAwait(false);

                //var reply = inStream.ReadChar();
                //if (reply != ACKchar) throw new Exception($"WTF?  Received {reply} (#{(int)reply} in place of ACK!");
                //else cts.Cancel();
            });
        }

        private static AsyncAutoResetEvent MessageReadyFlag = new AsyncAutoResetEvent();
        private static AsyncLock Lock = new AsyncLock();
        public static Queue<string> MessageQueue = new Queue<string>();
        public static Queue<DataOutputStream> OutStreamQueue = new Queue<DataOutputStream>();
        public static void RunSendingLooper(BluetoothClient client)
        {
            Task.Run(async () =>
            {
                while (!client.StopToken.IsCancellationRequested)
                {
                    await MessageReadyFlag.WaitAsync();
                    using (await Lock.LockAsync())
                    {
                        while (MessageQueue.Count > 0)
                        {
                            var stream = OutStreamQueue.Dequeue();
                            var message = MessageQueue.Dequeue();
                            //await stream.WriteCharsAsync(message);
                            stream.WriteChars(message); 
                        }
                    }
                }
            });
        }

        public virtual string ReadString(DataInputStream inStream, DataOutputStream outStream)
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
                //outStream.WriteChar(ACKchar);
            }).Wait();
            //Log.Debug(_tag, $"Received '{Readable(resultString)}'.");
            return resultString.TrimStart(START); // Using START here is mostly as a buffer - first-character drops seem to be common - so if they happen all they cost us is a START char (which we then silently fail to trim).
        }

        public static string ACK = "ACK";
        public static string CMD = "CMD";
        public static string POLL_FOR_NAMES = "POLL_FOR_NAMES";
        public static string POLL_RESPONSE = "POLL_RESPONSE";
        public static string ALL = "ALL";
        public static string SERVER = "SERVER";
        public static string CONNECTED_AS = " - Connected as ";

        public static string CONFIRM_AS_CLIENT = "Confirm connection from client: (this) to server: ";
        public static string CONFIRM_AS_SERVER = "Confirm connection from server: (this) to client: ";
        public static string SUGGEST_TEAMMATE = "Suggest connecting to teammate at: ";
        public static string PROVIDE_NAME_AND_ROLE = "My name and role are as follows: ";

        public static char ACKchar = (char)6;
        public static char START = (char)2; // Not actually used, except as a placeholder value, but that could change.
        public static char LF = (char)10;
        public static char ENDTRANSBLOCK = (char)23;
        public static char GROUP_SEPARATOR = (char)29;
        public static char RECORD_SEPARATOR = (char)30;
        public static char NEXT = GROUP_SEPARATOR;
        public static char[] onNEXT = new char[] { NEXT }; // Because Split(onThis, numGroups) requires a char array as onThis.
        public static char END = LF;

        // Utility function for troubleshooting those pesky nonprinting ASCII characters...
        public static string Readable(string inputString)
        {
            return inputString
                    .Replace(NEXT, '|')
                    .Replace(START, '<')
                    .Replace(END, '>')
                    .Replace(ACKchar, '#');
                    //+ $" {{{inputString.Length}}}";
        }
    }
}