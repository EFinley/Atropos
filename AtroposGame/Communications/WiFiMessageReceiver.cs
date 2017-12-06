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

using static Atropos.Communications.WifiBaseClass;

namespace Atropos.Communications
{
    public static class WiFiMessageReceiver
    {
        public static event EventHandler<EventArgs<Message>> OnReceiveMessage;

        public static WifiClient Client;
        public static WifiServer Server;

        public static string MyIPaddress;

        public static void ActOnMessage(Message message)
        {
            ActOnMessage(new EventArgs<Message>(message));
        }
        public static void ActOnMessage(EventArgs<Message> messageEventArgs)
        {
            OnReceiveMessage?.Invoke(Server, messageEventArgs);
            var doThis = ParseMessageToAction(messageEventArgs.Value);
            doThis?.Invoke(BaseActivity.CurrentActivity);
        }
        public static Action<object> ParseMessageToAction(Message message)
        {
            var substrings = message.Content.Split(onNEXT);
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
                try
                {
                    if (substrings[1] == "int") ResourceID = int.Parse(substrings[2]);
                    else ResourceID = typeof(Resource.Id).GetStaticProperty<int>(substrings[2]);
                }
                catch (ArgumentNullException)
                {
                    Log.Debug("WiFiMessageReceiver", $"No such SFX found to prepare ({substrings[2]})!");
                    return (o) => { };
                }

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
                    if (!Res.SFX.Effects.TryGetValue($"RequestedFX|{substrings[2]}", out IEffect FX))
                    {
                        int ResourceID;
                        try
                        {
                            if (substrings[1] == "int") ResourceID = int.Parse(substrings[2]);
                            else ResourceID = typeof(Resource.Id).GetStaticProperty<int>(substrings[2]);
                        }
                        catch (ArgumentNullException)
                        {
                            Log.Debug("WiFiMessageReceiver", $"No such SFX found to play ({substrings[2]})!");
                            return;
                        }

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