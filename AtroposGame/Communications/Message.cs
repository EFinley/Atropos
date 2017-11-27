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
using System.Net.Sockets;
using Android.Util;
using System.Threading.Tasks;
using MiscUtil;
using Nito.AsyncEx;
using System.Threading;
using System.Reflection;

namespace com.Atropos.Communications
{
    public struct Message
    {
        #region Thread-safe ID number generator
        private static int _idCounter = 10000; // Just to get us out of range of easily-confused anything else.
        private int _idNumber;
        public int IDnumber
        {
            get
            {
                if (_idNumber == default(int))
                {
                    _idNumber = Interlocked.Increment(ref _idCounter);
                    _idNumber = Interlocked.CompareExchange(ref _idCounter, 10000, int.MaxValue - 1);
                }
                return _idNumber;
            }
        } 
        #endregion
        public string Content;
        public IMessageReceiver ReturnAddress; // If null, just means "myself" - this is the most common case.  Will be filled in upon Send()ing.
        public MessageTarget Recipient; // Ditto (although less commonly null, of course).
        public List<Message> SubMessages;

        public void Send(IMessageReceiver From, MessageTarget To)
        {
            Recipient = To;
            ReturnAddress = From;

            if (Recipient is Team)
            {
                SubMessages = new List<Message>();
                foreach (var member in (Recipient as Team).Members)
                {
                    var subMessage = new Message() { Content = this.Content };
                    SubMessages.Add(subMessage);
                    subMessage.Send(From, To);
                }
            }
            else
            {
                // TODO: This needs the real logic! Includes wait-for-ack.
                // DoStuffWith(message.Content);
                // MakeSureToIncludeTransmitOf(message.IDnumber);
                // AlsoSupplyTheTeamMemberAs(message.Recipient) & YourselfAs(message.ReturnAddress);

            }
        }

        public static implicit operator string(Message m)
        {
            return m.Content;
        }
        public static implicit operator Message(string str)
        {
            return new Message() { Content = str };
        }

        #region Equality checking (only compares IDnumber)
        public static bool operator==(Message m1, Message m2)
        {
            return m1.IDnumber == m2.IDnumber;
        }
        public static bool operator !=(Message m1, Message m2)
        {
            return m1.IDnumber != m2.IDnumber;
        }
        public override bool Equals(object obj)
        {
            return (obj is Message) ? (this == (Message)obj) : base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return IDnumber.GetHashCode();
        }
        #endregion
    }
}