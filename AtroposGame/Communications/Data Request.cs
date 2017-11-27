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

    public abstract class DataRequest : ActivatorBase, IEquatable<DataRequest>
    {
        public DataRequest() : base() { Activate(); }
        public Message RequestMessage;

        #region Equality checking - Tricksy: Does NOT compare the resulting data, only the Message (which in turn only compares the ID number)
        public override bool Equals(object obj)
        {
            return (obj as DataRequest)?.Equals(this) ?? base.Equals(obj);
        }
        public bool Equals(DataRequest other)
        {
            return RequestMessage == other.RequestMessage;
        }
        public static bool operator ==(DataRequest first, DataRequest second)
        {
            return first.Equals(second);
        }
        public static bool operator !=(DataRequest first, DataRequest second)
        {
            return !first.Equals(second);
        }
        public override int GetHashCode()
        {
            var sumHashes = RequestMessage.GetHashCode();
            if (this.GetType().IsGenericType)
                foreach (var type in this.GetType().GetGenericArguments())
                    sumHashes += type.GetHashCode();
            return sumHashes.GetHashCode();
        }
        #endregion
    }
    public class DataRequest<Tdata> : DataRequest
    {
        public Tdata Data;
        public DataRequest(Message requestMessage) : base()
        {
            if (requestMessage == null) return; // Will be used in the group version since there we don't want to register this callback for the group, just for the individual sub-requests.
            RequestMessage = requestMessage;
            requestMessage.ReturnAddress.OnReceiveMessage += (o, e) =>
            {
                if (e.Value == requestMessage && e.Value.Content.StartsWith("AsRequested|"))
                {
                    Data = Serializer.Deserialize<Tdata>(e.Value.Content.Substring("AsRequested|".Length)); // That is, "skip that many chars" - IMO substring's one-arg version ought to be (count), not (startIndex), but I wasn't consulted.
                    OnRequestedDataAvailable?.Invoke(o, new EventArgs<Tdata>(Data));
                }
            };
        }

        // Factory function to create either a simple, or a group, DataRequest, as appropriate.
        public static DataRequest<Tdata> From(Message requestMessage)
        {
            if (requestMessage.SubMessages == null || requestMessage.SubMessages.Count == 0)
                return new DataRequest<Tdata>(requestMessage);
            else
                return new GroupDataRequest<Tdata>(requestMessage);
        }
        
        public event EventHandler<EventArgs<Tdata>> OnRequestedDataAvailable; // Will probably mostly only be used internally, by AwaitResponse().
        public virtual async Task<Tdata> AwaitResponse()
        {
            var tcs = new TaskCompletionSource<Tdata>();
            EventHandler<EventArgs<Tdata>> HandleReceipt = (o, e) => tcs.SetResult(e.Value);
            OnRequestedDataAvailable += HandleReceipt;
            StopToken.Register(() => { tcs.SetCanceled(); OnRequestedDataAvailable -= HandleReceipt; });
            return await tcs.Task;
        }
        public virtual async Task<Tdata[]> AwaitResponses() // Exists so it can be overridden by the multi-respondent form.
        {
            return new Tdata[] { await AwaitResponse() };
        }
    }
    public class GroupDataRequest<Tdata> : DataRequest<Tdata>
    {
        private List<DataRequest<Tdata>> requests;
        
        public GroupDataRequest(Message requestMessage) : base(null)
        {
            RequestMessage = requestMessage;

            var subMsgs = requestMessage.SubMessages;
            if (subMsgs == null || subMsgs.Count == 0)
                requests = new List<DataRequest<Tdata>>() { DataRequest<Tdata>.From(requestMessage) };
            else
                requests = subMsgs
                            .Select(m => DataRequest<Tdata>.From(m))
                            .ToList();
        }

        public override async Task<Tdata> AwaitResponse()
        {
            return await Task.WhenAny(responseSet).Result;
        }
        public override async Task<Tdata[]> AwaitResponses()
        {
            return await Task.WhenAll(responseSet);
        }
        protected Task<Tdata>[] responseSet { get { return requests.Select(r => r.AwaitResponse()).ToArray(); } }
    }
    
    public static class DataLibrarian
    {
        private static Dictionary<string, object> RootElements
            = new Dictionary<string, object>()
            {
                { "CurrentActivity", BaseActivity.CurrentActivity },
                { "CurrentStage", BaseActivity.CurrentStage }
                // Etc. etc.
            };

        public static Message FetchRequestedData(Message message, object dataObject)
        {
            // One: Parse out the instructions in the message
            var substrings = message.Content.Split('|');
            if (substrings[0] != "RequestData") return null;
            var fromWhat = substrings[1];
            var typeName = substrings[2];
            var rootElementName = fromWhat.Split('.')[0]; // Oh, for a proper slice notation!
            var memberNames = fromWhat.Split('.').Skip(1).ToArray();

            // Two: Get the requested data (as an object)
            object Result = FetchData(rootElementName, typeName, memberNames);

            // Three: Serialize the data
            var TargetType = Type.GetType(typeName);
            var serializer = typeof(Serializer.TypedSerializer<>).MakeGenericType(TargetType);
            var serialForm = serializer.InvokeStaticMethod<string>("Serialize", Result);

            // Four: Alter the content on the Message (but leave its ID number the same) and prep it to be sent back.
            message.Content = $"AsRequested|{serialForm}";
            return message;
        }

        //private static Dictionary<string, object> CachedLookups = new Dictionary<string, object>();

        private static object FetchData(string rootElementName, string typeName, params string[] MemberNames)
        {
            object currentObject, rootElement;

            //var concatString = $"{rootElementName}|{typeName}|{MemberNames.Join(".")}";
            //if (CachedLookups.TryGetValue(concatString, out currentObject)) return currentObject;
            
            if (!RootElements.TryGetValue(rootElementName, out rootElement))
            {
                rootElement = FetchNamedItem(typeof(Res), rootElementName)
                            ?? FetchNamedItem(typeof(BaseActivity), rootElementName)
                            ?? FetchNamedItem(BaseActivity.CurrentActivity, rootElementName);
                if (rootElement == null) throw new ArgumentException($"Root element '{rootElementName}' not in our predefined dictionary of objects-by-name.");
            }

            Type rootType = rootElement.GetType();
            currentObject = rootElement;
            string depthTrace = $"(({rootType.Name}))";

            //if (MemberNames.Length == 1)
            //    MemberNames = MemberNames[0].TrimStart('.').Split('.');
            //else if (MemberNames.Any(mN => mN.Contains(".")))
            //    throw new ArgumentException("Provide either one argument with dot-separated structure, OR as many args as you like with no periods in any of them.");

            foreach (var Name in MemberNames)
            {
                currentObject = FetchNamedItem(currentObject, Name);
                if (currentObject == null)
                {
                    Log.Debug("FetchData<Tdata>", $"Got as far as {depthTrace}, but unable to then locate .{Name} within that.");
                    return null;
                }
                else depthTrace += $".{Name}";
            }

            if (currentObject.GetType().Name != typeName) return null;
            //if (!currentObject.GetType().IsValueType) CachedLookups.Add(concatString, currentObject);
            return currentObject;
        }

        public static object FetchNamedItem(object sourceObj, string name)
        {
            if (sourceObj == null) return null;
            return FetchNamedItem(sourceObj.GetType(), sourceObj, name, true)
                ?? FetchNamedItem(sourceObj.GetType(), sourceObj, name, false);
        }
        public static object FetchNamedItem(Type type, string name)
        {
            return FetchNamedItem(type, null, name, true)
                ?? FetchNamedItem(type, null, name, false);
        }
        public static object FetchNamedItem(Type type, object sourceObj, string name)
        {
            return FetchNamedItem(type, sourceObj, name, true)
                ?? FetchNamedItem(type, sourceObj, name, false)
                ?? FetchNamedItem(type, name);
        }
        public static object FetchNamedItem(Type type, object sourceObj, string name, bool Public)
        {
            var BFlagVisibility = (Public) ? BindingFlags.Public : BindingFlags.NonPublic;
            var BFlagIsInstance = (sourceObj != null) ? BindingFlags.Instance : BindingFlags.Static;
            return type.GetProperty(name, BFlagVisibility | BFlagIsInstance).GetValue(sourceObj)
                ?? type.GetField(name, BFlagVisibility | BFlagIsInstance).GetValue(sourceObj);
        }
    }
}