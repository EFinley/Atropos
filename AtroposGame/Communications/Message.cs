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

using System.Text.RegularExpressions;

namespace com.Atropos.Communications
{
    public interface IMessage
    {
        string Content { get; set; }
        string To { get; set; }
        string From { get; set; }
        string ReferredToAs { get; set; }
    }

    public struct Message : IMessage
    {
        public string Content { get; set; }
        //public IMessageReceiver ReturnAddress; // If null, just means "myself" - this is the most common case.  Will be filled in upon Send()ing.
        //public MessageTarget Recipient; // Ditto (although less commonly null, of course).
        public string To { get; set; }
        public string ReferredToAs { get; set; }
        public string From { get; set; }

        public string ToCharStream()
        {
            if (!String.IsNullOrEmpty(ReferredToAs))
                return $"{To}<{ReferredToAs}>|{From}|{Content}";
            else
                return $"{To}|{From}|{Content}";
        }

        public static Message FromCharStream(string charStream)
        {
            var result = new Message();

            var pieces = charStream.Split("|".ToCharArray(), 3);
            var regEx = Regex.Match(pieces[0], @"^(?'To'[^<>]*)<(?'As'[^<>]*)>$");
            if (!regEx.Success)
            {
                result.To = pieces[0];
                result.ReferredToAs = null;
            }
            else
            {
                result.To = regEx.Groups["To"].Value;
                result.ReferredToAs = regEx.Groups["As"].Value;
            }
            result.From = pieces[1];
            result.Content = pieces[2];
            return result;
        }

        public List<Message> SubMessages;

        //public static string MSG = "MSG";

        //public void Send(MessageTarget To)
        //{
        //    Recipient = To;
        //    ReturnAddress = From;

        //    if (Recipient is Team)
        //    {
        //        SubMessages = new List<Message>();
        //        foreach (var member in (Recipient as Team).Members)
        //        {
        //            var subMessage = new Message() { Content = this.Content };
        //            SubMessages.Add(subMessage);
        //            subMessage.Send(From, To);
        //        }
        //    }
        //    else
        //    {
        //        // TODO: This needs the real logic! Includes wait-for-ack.
        //        // DoStuffWith(message.Content);
        //        // MakeSureToIncludeTransmitOf(message.IDnumber);
        //        // AlsoSupplyTheTeamMemberAs(message.Recipient) & YourselfAs(message.ReturnAddress);

        //    }
        //}

        public static implicit operator string(Message m)
        {
            return m.Content;
        }
        public static implicit operator Message(string str)
        {
            return new Message() { Content = str };
        }

        //#region Equality checking (only compares IDnumber)
        //public static bool operator==(Message m1, Message m2)
        //{
        //    return m1.IDnumber == m2.IDnumber;
        //}
        //public static bool operator !=(Message m1, Message m2)
        //{
        //    return m1.IDnumber != m2.IDnumber;
        //}
        //public override bool Equals(object obj)
        //{
        //    return (obj is Message) ? (this == (Message)obj) : base.Equals(obj);
        //}
        //public override int GetHashCode()
        //{
        //    return IDnumber.GetHashCode();
        //}
        //#endregion
    }
}