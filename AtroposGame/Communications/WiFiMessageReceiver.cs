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

namespace Atropos.Communications
{
    public static class WiFiMessageReceiver
    {
        public static event EventHandler<EventArgs<Message>> OnReceiveMessage;

        public static WifiClient Client;

        public static void ActOnMessage(Message message)
        {
            ActOnMessage(new EventArgs<Message>(message));
        }
        public static void ActOnMessage(EventArgs<Message> messageEventArgs)
        {
            OnReceiveMessage?.Invoke(Client, messageEventArgs);
            var doThis = ParseMessageToAction(messageEventArgs.Value);
            doThis?.Invoke(BaseActivity.CurrentActivity);
        }
        public static Action<object> ParseMessageToAction(Message message)
        {
            var substrings = message.Content.Split('|');
            if (substrings[0] == "RequestActivity")
            {
                return (o) =>
                {
                    var type = Type.GetType(substrings[1]);
                    // Todo - embed the PassedInfo into somewhere (Res? try again for Bundle syntax?) before this.
                    Application.Context.StartActivity(type);
                };
            }
            else if (substrings[0] == "PrepSFX")
            {
                int ResourceID;
                if (substrings[1] == "int") ResourceID = int.Parse(substrings[2]);
                else ResourceID = typeof(Resource.Id).GetStaticProperty<int>(substrings[2]);
                    
                return (o) =>
                {
                    var FX = Res.SFX.Register($"RequestedFX|{substrings[2]}", ResourceID);
                    FX.Activate();
                };
            }
            else if (substrings[0] == "PlaySFX")
            {
                return (o) =>
                {
                    IEffect FX;
                    if (!Res.SFX.Effects.TryGetValue($"RequestedFX|{substrings[2]}", out FX))
                    {
                        int ResourceID;
                        if (substrings[1] == "int") ResourceID = int.Parse(substrings[2]);
                        else ResourceID = typeof(Resource.Id).GetStaticProperty<int>(substrings[2]);
                        FX = Res.SFX.Register($"RequestedFX|{substrings[2]}", ResourceID);
                    }
                    FX.Activate();
                };
            }
            else if (substrings[0] == "SpeechSay")
            {
                return (o) =>
                {
                    Speech.Say(substrings[2]);
                };
            }
            else if (substrings[0] == "RequestData")
            {
                return (o) =>
                {
                    Client.SendMessage( message.From, DataLibrarian.FetchRequestedData(message, o) );
                };
            }
            // Etc. etc. for the other types of message.  Still to do: PushData, ...?
            throw new NotImplementedException();
        }
    }
}