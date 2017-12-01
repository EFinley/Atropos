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

namespace Atropos.Communications
{
    public abstract class MessageTarget
    {
        #region Address Book functionality
        public virtual string Name { get; set; }
        public virtual string IPaddress { get; set; }
        public static explicit operator MessageTarget(string identifier) => AddressBook.Resolve(identifier);
        #endregion

        public static IMessageReceiver ReturnAddress;

        // Technically this'd happen anyway thanks to the implicit cast, but this way it shows up in Intellisense as the first-and-default option, which it should be.
        public Message SendMessage(string message)
        {
            return SendMessage((Message)message);
        } 
        public Message SendMessage(Message message)
        {
            //message.Send(MessageTarget.ReturnAddress, this);
            WiFiMessageReceiver.Client.SendMessage(Name, message);
            return message;
        }
        public virtual DataRequest<Tdata> RequestData<Tdata>(string WhatData)
        {
            var msg = DataRequest<Tdata>.CreateFrom(new Message() { Content = $"RequestData|{WhatData}|{typeof(Tdata).Name}", To = Name });
            msg.Send();
            return msg;
        }

        public virtual void PrepSFX(string SFXname)
            { SendMessage($"PrepSFX|string|{SFXname}"); }
        public virtual void PrepSFX(int SFXresourceID)
            { SendMessage($"PrepSFX|int|{SFXresourceID}"); }

        public virtual void PlaySFX(string SFXname)
            { SendMessage($"PlaySFX|string|{SFXname}"); }
        public virtual void PlaySFX(int SFXresourceID)
            { SendMessage($"PlaySFX|int|{SFXresourceID}"); }
        // TODO: Consider how best to pass on the many options which can be provided to this function; only some apply, but still.

        public virtual void Speak(string content)
            { SendMessage($"SpeechSay|string|{content}"); }
        // TODO: Consider how best to pass on the many options which can be provided to this function; only some apply, but still.

        public virtual void PushData<Tdata>(string ToWhat, Tdata Data)
        {
            var serializedForm = Serializer.Serialize<Tdata>(Data);
            SendMessage($"PushData|{ToWhat}|{typeof(Tdata).FullName}|{serializedForm}");
        }
        public virtual void RequestActivityLaunch<Tactivity, Tdata>(Tdata PassedInfo = default(Tdata)) where Tactivity : Activity
        {
            if (PassedInfo != null && Operator.NotEqual(PassedInfo, default(Tdata)) && !Serializer.Check(PassedInfo))
            // If it cannot be serialized, but is nontrivial, then...???
            { }
            var serializedForm = Serializer.Serialize<Tdata>(PassedInfo);
            SendMessage($"RequestActivity|{typeof(Tactivity).FullName}|PassedInfo|{typeof(Tdata).FullName}|{serializedForm}");
        }
    }
}