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

using static Atropos.Communications.Bluetooth.BluetoothCore;

namespace Atropos.Communications.Bluetooth
{
    public static class BluetoothMessageCenter
    {
        public static string _tag = "BTMessageCenter";
        public static event EventHandler<EventArgs<Message>> OnReceiveMessage;

        public static BluetoothClient Client;
        public static BluetoothServer Server;

        public static void MakeClientConnectionTo(string MACaddress)
        {
            Log.Debug(_tag, $"Here we verify the address isn't already linked, and attempt to create a client-to-server link to {MACaddress}.");
        }

        public static void ActOnMessage(object sender, EventArgs<Message> messageArg)
        {
            ActOnMessage(messageArg.Value);
        }
        public static void ActOnMessage(Message message)
        {
            if (message.Type == MsgType.Ack) Log.Info(_tag, $"Received client ack of {message.Content}");
            else
            {
                OnReceiveMessage.Raise(message);
                var doThis = ParseMessageToAction(message);
                doThis?.Invoke(BaseActivity.CurrentActivity); // Note - nothing (currently) actually *uses* the argument of doThis; passing it the current activity is basically a placeholder here.
            }
        }
        public static Action<object> ParseMessageToAction(Message message)
        {
            if (message.Type == MsgType.Reply) throw new InvalidOperationException("Server received MsgType.Reply... those are meant to only flow the other way.");
            if (message.Type == MsgType.Notify) return null; // Notify messages ONLY raise the OnReceiveMessage event and nothing else.
            if (message.Type == MsgType.LaunchActivity)
            {
                var substrings = message.Content.Split(onNEXT);
                var type = Type.GetType(substrings[0]);
                var tdata = Type.GetType(substrings[1]);
                var serializedPassedInfo = substrings[2]; // Currently nothing is done with this, since it'll be a right pain in the arse to do.

                return (o) =>
                {
                    // Todo - embed the PassedInfo into somewhere (Res? try again for Bundle syntax?) before this.
                    Application.Context.StartActivity(type);
                };
            }
            if (message.Type == MsgType.PushSFX)
            {
                
                IEffect FX;
                if (!Res.SFX.Effects.TryGetValue($"RequestedFX{NEXT}{message.Content}", out FX))
                {
                    if (!int.TryParse(message.Content, out int ResourceID))
                        ResourceID = typeof(Resource.Id).GetStaticProperty<int>(message.Content);
                    FX = Res.SFX.Register($"RequestedFX{NEXT}{message.Content}", ResourceID);
                }
                if (FX == null) return null;

                return (o) =>
                {
                    FX.Activate();
                    FX.Play();
                };
            }
            if (message.Type == MsgType.PushSpeech)
            {
                return (o) =>
                {
                    Speech.Say(message.Content);
                };
            }
            if (message.Type == MsgType.PushEffect)
            {
                var substrings = message.Content.Split(onNEXT, 2);
                var effectName = substrings[0];
                var effectParams = substrings[1];
                if (!MasterSpellLibrary.CastingResults.ContainsKey(effectName))
                {
                    Log.Warn(_tag, $"Unable to locate game effect '{effectName}'.");
                    return null;
                }

                var doFunc = MasterSpellLibrary.CastingResults[effectName];
                return (o) =>
                {
                    doFunc?.Invoke(effectParams);
                };
            }
            if (message.Type == MsgType.Query)
            {
                return (o) =>
                {
                    var target = AddressBook.Resolve(message.From);
                    target.SendMessage(DataLibrarian.FetchRequestedData(message, o) );
                };
            }
            if (message.Type == MsgType.SetScenarioVariable)
            {
                var substrings = message.Content.Split(onNEXT);
                Encounters.Scenario.Current.SetVariable(substrings[0],
                    (Encounters.Scenario.State)Enum.Parse(typeof(Encounters.Scenario.State), substrings[1]), false); // False meaning don't rebroadcast it.
            }
            throw new NotImplementedException();
        }
    }
}