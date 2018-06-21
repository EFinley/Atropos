
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Util;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using PerpetualEngine.Storage;
using Nito.AsyncEx;
using MiscUtil;
using System.Threading.Tasks;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace Atropos
{
    public class Res
    {
        #region Singleton Pattern initialization
        private static readonly Res instance = new Res();

        static Res() { } // Empty static constructor for arcane reasons - http://csharpindepth.com/Articles/General/Singleton.aspx

        public static Res ourceManager { get { return instance; } }
        #endregion

        private Res()
        {
            SFX = new SFX();
            Speech = new Speech();
            Speech_Speakers = new Speech(true);
            //Speech.Init(); // Unnecessary in v3.0?? - No, but moved to Speech() ctor.
            Storage = SimpleStorage.EditGroup("Atropos_General");
            SpecificTags = SimpleStorage.EditGroup("Atropos_SpecificTags");
            stopWatch.Start();
        }

        /// <summary>
        /// A mime type for the string that this app will write to NFC tags. Will be
        /// used to help this application identify NFC tags that is has written to.
        /// </summary>
        public const string AtroposMimeType = "text/atropos.nfc_id";

        // String constants (to keep things consistent across different references) - things like key strings for bundle packing/unpacking.
        public const string bundleKey_Directive = "nfcDirective";
        public const string bundleKey_tagID = "nfcRecord";

        private static Random globalRandom = new Random();
        public static double Random { get { return globalRandom.NextDouble(); } }
        public static float RandomF { get { return (float)globalRandom.NextDouble(); } }
        public static double RandomZ { get { return Accord.Math.Special.Ierf(Random); } }
        public static bool CoinFlip { get { return globalRandom.NextDouble() >= 0.5; } }
        public static double GetRandomCoefficient(double sigma) { return GetRandomCoefficient(1.0, sigma); }
        public static double GetRandomCoefficient(double mean, double sigma) // From Wikipedia: for Gamma(theta, k), mean = k * theta, variance ( = std. dev. squared) = theta^2 * k
        {
            var theta = sigma * sigma / mean;
            return new Accord.Statistics.Distributions.Univariate.GammaDistribution(theta, mean / theta).Generate();
        }

        private System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
        public static void Mark(string label) { Android.Util.Log.Debug("Timing", $"{Res.ourceManager.stopWatch.ElapsedMilliseconds}ms to {label}."); }

        public static bool AllowNewActivities = true;

        private static Persistent<bool> _allowSpeaker = new Persistent<bool>("AllowSpeaker");
        public static bool AllowSpeakerSounds { get { return (bool)_allowSpeaker; } set { _allowSpeaker.Value = value; } }
        public static List<IEffect> PersistentSFX = new List<IEffect>(); // TODO - currently this does nothing, should function to keep those effects alive.
                                                                         // Also TODO to go with this: set those up to 'duck' properly when other effects are playing.

        private static Persistent<bool> _solipsismMode = new Persistent<bool>("SolipsismMode");
        public static bool SolipsismMode { get { return _solipsismMode; } set { _solipsismMode.Value = value; } }

        private static Persistent<bool> _lefthandedMode = new Persistent<bool>("LeftHandedMode");
        public static bool LefthandedMode { get { return _lefthandedMode; } set { _lefthandedMode.Value = value; } }

        private static Persistent<bool> _screenFlipMode = new Persistent<bool>("ScreenFlipMode");
        public static bool ScreenFlipMode { get { return _screenFlipMode; } set { _screenFlipMode.Value = value; } }

        private static Persistent<bool> _allowNfc = new Persistent<bool>("AllowNFC");
        public static bool AllowNfc { get { return _allowNfc; } set { _allowNfc.Value = value; } }

        public class InteractionMode
        {
            public string Name;
            public string PromptText;
            public Activity Launches;
            public ActivityFlags Flags;
            public string Directive;
            public InteractionMode() { }
            public InteractionMode(string name, stringOrID prompt, Activity activity, stringOrID directive = default(stringOrID), ActivityFlags flags = ActivityFlags.SingleTop)
            {
                Name = name;
                PromptText = prompt;
                Launches = activity;
                Flags = flags;
                Directive = directive;
            }
        }

        /// <summary>
        ///  This method is used to declare a new tag type and register it with our dictionary so it can be located by name later.
        /// </summary>
        /// <param name="name">The name you'll refer to this tag type by.  Used mostly as a dictionary key.</param>
        /// <param name="promptString">A string, or resource ID, for a "what to do with this" prompt.  May become obsolete.</param>
        /// <param name="activity">An activity, usually a new instance of (the specific Activity-derived class) you want such tags to launch.</param>
        /// <param name="directive">Any additional information you wish to bundle up and include when the Activity is being launched (like "calibration" rather than the real thing) should be stringified into this.</param>
        /// <param name="flags">Android ActivityFlag(s) describing how the launched activity behaves - does it claim the foreground (the default), does it move straight to background, etc.</param>
        /// <returns></returns>
        public static InteractionMode DefineInteractionMode(string name, stringOrID promptString, Activity activity, stringOrID directive = default(stringOrID), ActivityFlags flags = ActivityFlags.SingleTop)
        {
            var direc = (stringOrID)directive;
            InteractionMode newMode = new InteractionMode
            {
                Name = name,
                PromptText = promptString,
                Launches = activity,
                Directive = direc,
                Flags = flags
            };
            if (InteractionModes.ContainsKey(name)) InteractionModes[name] = newMode;
            else InteractionModes.Add(name, newMode);
            return newMode;
        }

        public static OrderedDictionary<string, InteractionMode> InteractionModes { get; set; } = new OrderedDictionary<string, InteractionMode>();
        public static OrderedDictionary<string, Gun> AllGuns { get; set; } = new OrderedDictionary<string, Gun>();
        
        public static SFX SFX { get; set; }
        public static Speech Speech { get; set; }
        public static Speech Speech_Speakers { get; set; }
        public static SimpleStorage Storage { get; set; }
        public static SimpleStorage SpecificTags { get; set; }
        public static bool DebuggingSignalFlag { get; set; } = false;

        //public static Activity CurrentActivity { get; set;}
        //public static DeviceSensorCalibration DeviceAccelerometerCalibration { get; set; }

        private static int _numSensors = 0;
        public static int NumSensors
        {
            get { return _numSensors; }
            set
            {
                _numSensors = value;
                //if (_numSensors > 5) Android.Util.Log.Debug("NumSensors", $"Number of sensors listening: {_numSensors}.");
                //Android.Util.Log.Debug("NumSensors", $"Number of sensors listening: {_numSensors}.");
            }
        }
    }

    /// <summary>
    /// A string which, if it's an int corresponding to an Android resource id number, will come out as the resource's string representation.  
    /// DO NOT USE for arbitrary strings which might actually take a legitimate pretty-big-int value.
    /// </summary>
    /// <example>If /Values/Strings.xml defined the following (curly brackets substituted for angle ones 'cause I can't be arsed to look up escaping in docstring xml code):
    /// {string name="directive_calibrate_gun"}Calibrate Gun{\string}
    /// 
    /// Then the following are all effectively equivalent:
    ///     - "Calibrate Gun"
    ///     - (stringOrID)Resource.String.directive_calibrate_gun
    ///     - Application.Context.Resources.GetString(Resource.String.directive_calibrate_gun)
    ///     
    /// You can see why I created the middle version.
    /// </example>
    public struct stringOrID
    {
        public string textRep;
        public stringOrID(string text = "")
        {
            textRep = text;
        }
        public static implicit operator stringOrID(int resourceIDNumber)
        {
            try { return new stringOrID(Application.Context.Resources.GetString(resourceIDNumber)); }
            catch (Android.Content.Res.Resources.NotFoundException) { return new stringOrID(resourceIDNumber.ToString()); } // Apparently it was just an int after all.
            catch { throw; }
        }
        public static implicit operator stringOrID(string text)
        {
            return new stringOrID(text);
        }
        public static implicit operator string(stringOrID source)
        {
            return source.textRep;
        }
    }

    //public class Persistent<T>
    //{
    //    private class _persistent<T1, T2> : PersistentC<T2> where T2 : struct
    //    {

    //    }
    //    private Persistent<T> _innerValue;
    //    public virtual T Value { get { return _innerValue.Value; } set { _innerValue.Value = value; } }
    //    public Persistent()
    //    {
    //        if (typeof(T).IsValueType)
    //        {
    //            _innerValue = new Persistent<T>();
    //        }
    //        else _innerValue = new Persistent<Nullable<NonNullable<T>>>();
    //    }
    //}

    //public struct NonNullable<T>
    //{
    //    public T Value { get; set; }
    //    public bool HasValue { get; set; }
    //    public static implicit operator T(NonNullable<T> val)
    //    {
    //        return (val.HasValue) ? val.Value : default(T);
    //    }
    //    public static implicit operator NonNullable<T>(T val)
    //    {
    //        return new NonNullable<T>() { Value = val, HasValue = (val != null) };
    //    }
    //}

    //[Serializable]
    //public class PersistentStruct<T> : Persistable<T> where T : struct
    //{
    //    private Persistable<Nullable<T>> _inner;
    //    public override T Value
    //    {
    //        get
    //        {
    //            return _inner.Value ?? default(T);
    //        }

    //        set
    //        {
    //            _inner.Value = value;
    //        }
    //    }
    //}

    [Serializable]
    public class Persistent<T>
    {
        private static string Tag { get { return $"Persistent<{typeof(T).Name}>"; } }

        // This section will give us a repeatable, unique-per-Type, ID based on the order in which things are constructed,
        // and by which functions/properties/etc they've been created.  This is, therefore, not *guaranteed* to
        // create the same ID for the same named variable in your code each time it is run, but unless you're creating
        // a lot of Persistent<T> objects of the same Type in the same function all at once, it should be relatively safe.
        private static Dictionary<string, int> callerNameInvocations = new Dictionary<string, int>();
        private static DateTime lastUse = DateTime.Now;
        private static string lastCallerName = "";
        private static readonly TimeSpan warningInterval = TimeSpan.FromMilliseconds(100);
        private static object syncRoot = new object();
        private string GetKey(string callerName)
        {
            lock (syncRoot)
            {
                // Check that we're not running too strong a risk of ambiguity (multiple Persistent<T> with the same T and origin, within a brief space of time)
                if ((callerName == lastCallerName) && (DateTime.Now > lastUse + warningInterval))
                {
                    Log.Warn(Tag, $"Careful! Caller {callerName} is invoking back-to-back Persistent<{typeof(T).Name}> within a short time of one another.  This might result in issues if the timing is different next time around.  Consider explicitly naming the Persistent objects or separating the routine that creates them.");
                }
                lastCallerName = callerName;
                lastUse = DateTime.Now;

                // Add to dictionary if not already present
                if (!callerNameInvocations.ContainsKey(callerName))
                    callerNameInvocations.Add(callerName, 0);

                // Increment the counter which lets the same function create more than one Persistent<T> yet have them be persistent.
                callerNameInvocations[callerName] = callerNameInvocations[callerName] + 1;

                // Return the created key.  Barring refactoring or adding new, earlier, creations of Persistent<T> *by the same function*, this should keep across loads and even compiles.
                return $"{callerName}{callerNameInvocations[callerName]}";
            }
        }
        public Persistent([CallerMemberName] string keyRoot = "Default", T initialValue = default(T))
        {
            _key = GetKey(keyRoot);
            if (initialValue.ToString() != default(T).ToString()) // Cheater's way of checking equality even with arbitrary types which don't have == operators defined.
            {
                HasValue = true; // If you happen to have set the initial value to the default (say, False), then it won't know this fact. Trust me that it's not worth the headache to figure this out (given nullable and non-nullable types, etc).  Better to just let the first retrieval cope with caching it instead.
                _value = initialValue;
            }
        }

        public bool HasValue { get; set; } = false;

        private T _value;
        public virtual T Value
        {
            get
            {
                if (HasValue) return _value;
                if (storage.HasKey(_key))
                {
                    _value = storage.Get<T>(_key);
                    Log.Debug(Tag, $"Retrieved {Tag}[[{_key}]]: {_value}");
                    HasValue = true;
                    return _value;
                }
                else
                {
                    Log.Debug(Tag, $"Unable to find stored key {_key}; using default.");
                    _value = default(T);
                    return _value;
                }
            }
            set
            {
                _value = value;
                HasValue = true;
                Task.Run(() => 
                {
                    storage.Put<T>(_key, value);
                    Log.Debug(Tag, $"Stored {Tag}[[{_key}]] as {storage.Get<T>(_key)}");
                });
            }
        }

        private string _key;

        public static implicit operator T(Persistent<T> persistent)
        {
            return (persistent != null) ? persistent.Value : default(T);
        }

        public static explicit operator Persistent<T>(T origValue) // Note that this one will always identify as called by *this* operator; we can't get CallerMemberName from a type cast.
        {
            return new Persistent<T>() { Value = origValue };
        }

        private static readonly string _storageName = $"Persistent<{typeof(T).Name}>StorageLocker";
        private static SimpleStorage _storage;
        private static SimpleStorage storage
        {
            get
            {
                if (_storage == null) Log.Debug(Tag, $"Initializing {_storageName}");
                _storage = _storage ?? SimpleStorage.EditGroup(_storageName); // new NamedSimpleStorage(_storageName);
                return _storage;
            }
        }
    }

    public static class PersistentExtensions
    {
        public static Persistent<T> MakePersistent<T>(this T self, [CallerMemberName] string keyRoot = "ExtensionMethod")
        {
            return new Persistent<T>(keyRoot);
        }
    }

    [Serializable]
    public class PersistentList<T> : ObservableCollection<T> // : BindingList<T>
    {
        public string Name { get; private set; }
        private static string prefaceString;
        private string _fullname { get { return prefaceString + Name; } }
        private const string _key = "theList";

        private SimpleStorage _storage;

        public PersistentList(string name)
        {
            // Make sure prefaceString exists
            prefaceString = prefaceString ?? $"PersistentList<{typeof(T).Name}>:";

            Name = name;
            _storage = SimpleStorage.EditGroup(_fullname);
            var existingList = _storage.GetAsync<BindingList<T>>(_key).Result ?? new BindingList<T>();
            Android.Util.Log.Debug("PersistentList", $"Loading persistentList '{_fullname}', containing {existingList.Join()}.");
            //RaiseListChangedEvents = false;
            foreach (T item in existingList) Add(item);
            //RaiseListChangedEvents = true;
            CollectionChanged += (o, e) => Save();
        }


        //protected override void OnListChanged(ListChangedEventArgs e)
        //{
        //    base.OnListChanged(e);
        //    Android.Util.Log.Debug("PersistentList", $"OnListChanged: type {e.ListChangedType}, descriptor.name {e.PropertyDescriptor.Name}.");
        //    Save();
        //}

        //protected override void OnAddingNew(AddingNewEventArgs e)
        //{
        //    base.OnAddingNew(e);
        //    Android.Util.Log.Debug("PersistentList", $"OnAddingNew: newobject {e.NewObject}");
        //    Save();
        //}

        private void Save()
        {
            Android.Util.Log.Debug("PersistentList", $"Saving persistentList '{_fullname}', containing {this.Join()}.");
            //Task.Run(() => { _storage.PutAsync<BindingList<T>>(_key, this).Wait(); });
            Task.Run(() => { _storage.PutAsync<ObservableCollection<T>>(_key, this).Wait(); });
        }
    }

    [Serializable]
    public class PersistentDictionary : SimpleStorage
    {
        public string Name { get { return Group; } } // set { Group = value; } }
        private const string _indexName = "__keys__";
        private SimpleStorage _innerStorage;
        //private const char _separator = '\u2192'; // Rightwards arrow
        //private string _keys { get { return string.Join(_separator.ToString(), Keys); } set { Keys = value.Split(_separator).ToList(); } }
        //private string _keys { get { return ArbitrarySerializer.Serialize(Keys); } set { Keys = ArbitrarySerializer.Deserialize<List<string>>(value); } }
        public List<string> Keys { get; private set; }
        public List<object> Values { get { return Keys.Select(k => Get<object>(k)).ToList(); } }

        static PersistentDictionary()
        {
            SimpleStorage.SetContext(Application.Context);
        }

        public PersistentDictionary(string groupName) : base(groupName)
        {
            _innerStorage = SimpleStorage.EditGroup(groupName);
            if (_innerStorage.HasKeyAsync(_indexName).Result)
                Keys = _innerStorage.GetAsync<List<string>>(_indexName).Result;
            else Keys = new List<string>();
        }

        public void DeleteEntirely()
        {
            Application.Context.DeleteSharedPreferences(Name);
        }

        public override string Get(string key)
        {
            return _innerStorage.GetAsync(key).Result;
        }

        public override async void Put(string key, string value)
        {
            if (!Keys.Contains(key)) Keys.Add(key);
            await _innerStorage.PutAsync(key, value);
            await _innerStorage.PutAsync(_indexName, Keys);
        }

        public override async void Delete(string key)
        {
            Keys.Remove(key);
            _innerStorage.Delete(key);
            await _innerStorage.PutAsync(_indexName, Keys);
        }

        public void Clear()
        {
            var keys = Keys.ToArray(); // Makes a local copy instead of passing by ref.
            foreach (string key in keys) Delete(key);
        }

        public static PersistentDictionary Rename(PersistentDictionary original, string newName)
        {
            var newNSS = new PersistentDictionary(newName);
            foreach(var key in original.Keys)
            {
                newNSS.Put(key, original.Get(key));
            }
            original.DeleteEntirely();
            return newNSS;
        }
    }
}