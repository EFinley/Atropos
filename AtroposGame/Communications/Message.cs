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

using Atropos.Communications.Bluetooth;
using static Atropos.Communications.Bluetooth.BluetoothCore;

namespace Atropos.Communications
{
    public enum MsgType
    {
        Ack,
        Query,
        Notify,
        PushSFX,
        PushSpeech,
        PushEffect,
        SetScenarioVariable,
        LaunchActivity,
        Reply
    }

    public interface IMessage
    {
        string From { get; set; }
        MsgType Type { get; set; }
        string ID { get; set; }
        string Content { get; set; }
    }

    public struct Message : IMessage
    {
        public string From { get; set; }
        public MsgType Type { get; set; }
        public string ID { get; set; }
        public string Content { get; set; }

        public Message(MsgType type, string content) : this(type, string.Empty, content)
        {
            ID = Guid.NewGuid().ToString();
        }

        public Message(MsgType type, string id, string content)
        {
            From = BluetoothMessageCenter.Server?.MyMACaddress ?? "Me";
            Type = type;
            ID = id;
            Content = content;
        }

        public string ToCharStream()
        {
            return $"{(int)Type}{NEXT}{ID}{NEXT}{Content}";
        }

        public static Message FromCharStream(string MACaddress, string charStream)
        {
            var result = new Message();
            result.From = MACaddress;

            var pieces = charStream.Split(onNEXT, 3);
            //var regEx = Regex.Match(pieces[0], @"^(?'To'[^<>]*)<(?'As'[^<>]*)>$");
            //if (!regEx.Success)
            //{
            //    result.To = pieces[0];
            //    result.ReferredToAs = null;
            //}
            //else
            //{
            //    result.To = regEx.Groups["To"].Value;
            //    result.ReferredToAs = regEx.Groups["As"].Value;
            //}
            //result.From = pieces[1];

            if (charStream == null || pieces.Count() != 3) return new Message(MsgType.Notify, "Error reading original character stream.");

            try
            {
                result.Type = (MsgType)int.Parse(pieces[0]);
            }
            catch (Exception e)
            {
                Log.Error("Message.FromCharStream", $"{e}");
            }
            result.ID = pieces[1];
            result.Content = pieces[2];
            return result;
        }

        public static implicit operator string(Message m)
        {
            return m.Content;
        }

        #region Equality checking (only compares IDnumber)
        public static bool operator ==(Message m1, Message m2)
        {
            return m1.ID == m2.ID;
        }
        public static bool operator !=(Message m1, Message m2)
        {
            return m1.ID != m2.ID;
        }
        public override bool Equals(object obj)
        {
            return (obj is Message) ? (this == (Message)obj) : base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }
        #endregion
    }

    public struct MessageSet : IMessage
    {
        public CommsGroup To { get; set; }
        public string From { get; set; }
        public MsgType Type { get; set; }
        public string ID { get; set; }
        public string Content { get; set; }

        public List<Message> submessages { get; set; }

        public MessageSet(CommsGroup to, MsgType type, string content) : this(to, type, null, content)
        {
            ID = Guid.NewGuid().ToString();
        }

        public MessageSet(CommsGroup to, MsgType type, string id, string content)
        {
            To = to;
            Type = type;
            ID = id;
            Content = content;
            From = BluetoothMessageCenter.Server.MyMACaddress;

            submessages = new List<Message>();
            foreach (var teammate in To.Contacts)
            {
                submessages.Add(new Message(Type, Content));
            }
        }
    }
}