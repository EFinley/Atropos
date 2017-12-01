
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;

using Android;
using Android.App;
using Android.Nfc;
using Android.OS;
using Android.Widget;
using Android.Util;
using DeviceMotion.Plugin;
using DeviceMotion.Plugin.Abstractions;
// using Accord.Math;
// using Accord.Statistics;
using Android.Content;
using System.Threading.Tasks;
using PerpetualEngine.Storage;
using Android.Media;
using System.Numerics;
using Vector3 = System.Numerics.Vector3;

namespace Atropos
{
    public class Stroke
    {
        public string StrokeName;
        public bool IsStatic { get; set; } = false; // Gonna have them all 'active' for now (no passive parries).
        //public Quaternion FinalOrientation { get; set; }
        public Quaternion InitialOrientation { get; set; }
        public double OrientationSigma { get; set; }
        public Vector3 Axis { get; set; }
        public double AxisSigma { get; set; }
        public double TotalAngleTraversed { get; set; }
        public double TotalAngleSigma { get; set; }
        public double TotalTimeTaken { get; set; }
        public double TotalTimeSigma { get; set; }

        public List<StrokeSnapshot> Snapshots { get; set; } = new List<StrokeSnapshot>();
        
        public Stroke()
        {
            InitialOrientation = Quaternion.Identity;
        }

        public override string ToString()
        {
            var valuesArray = new double[] 
            { InitialOrientation.X, InitialOrientation.Y, InitialOrientation.Z, InitialOrientation.W,
                OrientationSigma,
                Axis.X, Axis.Y, Axis.Z,
                AxisSigma,
                TotalAngleTraversed,
                TotalAngleSigma,
                TotalTimeTaken,
                TotalTimeSigma
            };
            var stringsArray = valuesArray.Select(d => d.ToString("f4")).ToArray();
            return stringsArray.Join(",");
        }

        public static Stroke FromString(string inputStr)
        {
            var inputArray = inputStr.Trim('(', ')').Split(',');
            var vals = inputArray.Select(i => float.Parse(i)).ToArray();
            return new Stroke()
            {
                InitialOrientation = new Quaternion(vals[0], vals[1], vals[2], vals[3]),
                OrientationSigma = vals[4],
                Axis = new Vector3(vals[5], vals[6], vals[7]),
                AxisSigma = vals[8],
                TotalAngleTraversed = vals[9],
                TotalAngleSigma = vals[10],
                TotalTimeTaken = vals[11],
                TotalTimeSigma = vals[12]
            };
        }

        public static Stroke None = new Stroke()
        {
            InitialOrientation = Quaternion.Identity,
            OrientationSigma = 0.0001,
            Axis = -Vector3.UnitZ, // Default is a motion where the phone arcs in the plane of the screen, CW with the screen up.
            AxisSigma = 0.0001,
            TotalAngleTraversed = 0,
            TotalAngleSigma = 0.0001,
            TotalTimeTaken = 0,
            TotalTimeSigma = 0.0001
        };
    }

    // Class used for recording data during training, and (optionally?) later for recording
    // "keyframes" in the "animation" the stroke represents.
    public class StrokeSnapshot
    {
        public Quaternion Orientation;
        public Vector3 Axis;
        public float RotationVelocity;
        public Vector3 Accel;
        public long Timestamp;
        public override string ToString()
        {
            return $"{Orientation.ToStringFormatted("f3")} - Axis {Axis:f3} - Rot {RotationVelocity:f4} - Accel {Accel:f3} - At {Timestamp % 1000000}";
        }
    }

    /// <summary>
    /// More than just a data holding class, this contains the code for overlaying voice AND SFX at the same time.
    /// </summary>
    public class Prompt
    {
        public static float defaultFXvolume = 0.5f;

        public string Verbal { get; set; }
        public IEffect Aural { get; set; }
        public float SpeakPitch { get; set; } = 1.0f;
        public float SpeakRate { get; set; } = 1.0f;

        public async Task SynchSay(string verbal = null, IEffect aural = null, 
            float? pitch = null, float? rate = null, float? FXvolume = null)
        {
            Verbal = verbal ?? Verbal;
            Aural = aural ?? Aural;
            SpeakPitch = pitch ?? SpeakPitch;
            SpeakRate = rate ?? SpeakRate;

            //await Speech.SayAllOf(Verbal, pitch: SpeakPitch, speakRate: SpeakRate, 
            //    doOnStart: () => { Aural.Play(FXvolume ?? defaultFXvolume); });
            await Task.WhenAll(Speech.SayAllOf(Verbal, pitch: SpeakPitch, speakRate: SpeakRate), Aural.PlayToCompletion(FXvolume ?? defaultFXvolume));
            //if (Aural.IsPlaying) Aural.Stop();
        }

        public static async Task SynchSpeak(string verbal, IEffect aural, 
            float? pitch = null, float? rate = null, float? FXvolume = null)
        {
            var prompt = new Prompt();
            await prompt.SynchSay(verbal, aural, pitch, rate, FXvolume);
            return;
        }
    }

    public class Form
    {
        public string FormName;
        public Quaternion InitialOrientation;
        public Prompt Prompt;
        public bool IsOffense;
        public string FXname;
        public Stroke Stroke;
        public float AngleTo(Quaternion other) { return InitialOrientation.AngleTo(other); }

        private string formResultFuncName;
        private string failResultFuncName;
        private Func<object, Task> formResultFunc;
        private Func<object, Task> failResultFunc;
        public Func<object, Task> FormResult
        {
            get { return formResultFunc; }
            set { formResultFuncName = MasterFechtbuch.AddResultFunction(value); formResultFunc = value; }
        }
        public Func<object, Task> FailResult
        {
            get { return failResultFunc; }
            set { failResultFuncName = MasterFechtbuch.AddResultFunction(value); failResultFunc = value; }
        }

        public List<Stroke> Strokes { get; set; } // Unlike Glyphs, this is repeats of the same stroke, used in training.
        public Quaternion FinalOrientation; // This is just used during the training phase.

        public Form(string name, bool isOffense)
        {
            FormName = name;
            IsOffense = isOffense;
            InitialOrientation = FinalOrientation = Quaternion.Identity;
            FXname = (isOffense) ? MasterFechtbuch.defaultOffensiveSFXName : MasterFechtbuch.defaultDefensiveSFXName;
            Prompt = new Prompt() { Verbal = name, Aural = MasterFechtbuch.FormSFX?[FXname] }; // For now, verbal = name; this may change later, but can always be found as .Prompt.Verbal anyway.
            Stroke = new Stroke();
            Strokes = new List<Stroke>();
        }

        public override string ToString()
        {
            return $"{FormName};{IsOffense};"
                + $"{InitialOrientation.X};{InitialOrientation.Y};{InitialOrientation.Z};{InitialOrientation.W};"
                + $"{Prompt.Verbal};{FXname};{Prompt.SpeakPitch};{Prompt.SpeakRate};"
                + $"{Stroke};{formResultFuncName};{failResultFuncName}";
        }
        public static Form FromString(string formString)
        {
            Func<string,float> fp = float.Parse;
            var vals = formString.Split(';');
            return new Form(vals[0], bool.Parse(vals[1]))
            {
                InitialOrientation = new Quaternion(fp(vals[2]), fp(vals[3]), fp(vals[4]), fp(vals[5])),
                Prompt = new Prompt()
                {
                    Verbal = vals[6],
                    Aural = MasterFechtbuch.FormSFX[vals[7]],
                    SpeakPitch = fp(vals[8]),
                    SpeakRate = fp(vals[9])
                },
                FXname = vals[7],
                Stroke = Stroke.FromString(vals[10]),
                formResultFuncName = vals[11],
                failResultFuncName = vals[12]
            };
        }

        public static Form None = new Form("Formless Void", true);
    }

    public static class MasterFechtbuch
    {
        public static OrderedDictionary<string, IEffect> FormSFX;
        public const string defaultDefensiveSFXName = "Melee.Defense";
        public const string defaultOffensiveSFXName = "Melee.Offense";
        public static IEffect DefensiveSFX;
        public static IEffect OffensiveSFX;

        public const string nullFormResultName = "Melee.Result.Null";
        public static readonly Func<object, Task> nullFormResultFunction = (o) => { return Task.CompletedTask; };
        public static Dictionary<string, Func<object, Task>> FormResults 
            = new Dictionary<string, Func<object, Task>> { { nullFormResultName, nullFormResultFunction } };

        private const string persistentLibraryName = "MasterFechtbuch";
        private const string masterIndexName = "MasterFormIndex";
        private static SimpleStorage persistentLibrary { get; set; }
        public static List<string> formNames { get; set; } = new List<string>();

        private static Nito.AsyncEx.AsyncManualResetEvent _sfxReadyFlag = new Nito.AsyncEx.AsyncManualResetEvent();
        public static Task GetSFXReadyTask() {
            return _sfxReadyFlag.WaitAsync(); }

        public static void LoadAll()
        {
            //Res.Mark("LoadAll begins");

            persistentLibrary = SimpleStorage.EditGroup(persistentLibraryName);
            formNames = (persistentLibrary.Get<string[]>(masterIndexName) ?? new string[0]).ToList();
            foreach (string fname in new List<string>() { "Down Slash", "Left Slash", "Right Slash", "High Parry", "Left Parry", "Right Parry" })
                if (!formNames.Contains(fname)) formNames.Add(fname);

            // Clearing the default Form out - probably only necessary once, while I'm debugging, but safe to leave in regardless.
            formNames.Remove(Form.None.FormName);
            persistentLibrary.Delete(Form.None.FormName);

            LoadAllFormSFX();
            DefensiveSFX = FormSFX[defaultDefensiveSFXName];
            OffensiveSFX = FormSFX[defaultOffensiveSFXName];

            //Res.Mark("Forms SFX loaded");

            SetUpResultFunctions();
        }

        public static void LoadAllFormSFX()
        {
            //var sw = new System.Diagnostics.Stopwatch();
            //sw.Start();
            var shings = new EffectGroup("Melee.Offense", 
                Res.SFX.Preregister("Melee.Offense.1", Resource.Raw._175957_shing),
                Res.SFX.Preregister("Melee.Offense.2", Resource.Raw._178415_shing),
                Res.SFX.Preregister("Melee.Offense.3", Resource.Raw._182350_shing),
                Res.SFX.Preregister("Melee.Offense.4", Resource.Raw._215028_shing),
                Res.SFX.Preregister("Melee.Offense.5", Resource.Raw._218478_shing)
                //Res.SFX.Preregister("Melee.Offense.6", Resource.Raw._221682_gshwingg_compilation, 0.1, 0.2), // TBD - figure out where to chop up these two compilations.
                //Res.SFX.Preregister("Melee.Offense.1", Resource.Raw._85659_shing_compilation)
                );
            var clashes = new EffectGroup("Melee.Defense",
                Res.SFX.Preregister("Melee.Defense.1", Resource.Raw._1470_clash),
                //Res.SFX.Preregister("Melee.Defense.2", Resource.Raw._151462_clash_compilation, 0.1, 0.2),
                //Res.SFX.Preregister("Melee.Defense.3", Resource.Raw._175949_clash_t, 0.1, 0.2),
                Res.SFX.Preregister("Melee.Defense.4", Resource.Raw._257608_clash),
                Res.SFX.Preregister("Melee.Defense.5", Resource.Raw._275123_clash),
                Res.SFX.Preregister("Melee.Defense.6", Resource.Raw._27858_clash),
                //Res.SFX.Preregister("Melee.Defense.7", Resource.Raw._345308_clash_compilation, 0.1, 0.2),
                Res.SFX.Preregister("Melee.Defense.8", Resource.Raw._52458_clash)
                );
            FormSFX = new OrderedDictionary<string, IEffect>
            {
                { defaultDefensiveSFXName, Res.SFX.RegisterEffect(clashes)},
                { defaultOffensiveSFXName, Res.SFX.RegisterEffect(shings) }
            };
            //Log.Debug("Melee", $"Form SFXes loaded after {sw.ElapsedMilliseconds} ms.");
            _sfxReadyFlag.Set();
        }

        public static Form Get(string formname)
        {
            if (formNames.Contains(formname) && persistentLibrary.Get(formname) != null)
            {
                return Form.FromString(persistentLibrary.Get(formname));
            }
            else
            {
                //Log.Error("FormLibrary", $"Could not find form '{formname}' in master library; returning blank form.");
                return Form.None;
            }
        }

        public static void Inscribe(Form newform)
        {
            persistentLibrary.Put(newform.FormName, newform.ToString());
            if (!formNames.Contains(newform.FormName)) formNames.Add(newform.FormName);
            persistentLibrary.Put(masterIndexName, formNames.ToArray());
        }

        public static void Erase(string formname)
        {
            persistentLibrary.Delete(formname);
            formNames = formNames.Where(n => n != formname).ToList();
            persistentLibrary.Put(masterIndexName, formNames.ToArray());
        }

        public static string AddResultFunction(string resultName, Func<object, Task> resultFunc)
        {
            if (FormResults.ContainsKey(resultName)) FormResults[resultName] = resultFunc;
            else FormResults.Add(resultName, resultFunc);
            return resultName;
        }
        public static string AddResultFunction(Func<object, Task> resultFunc)
        {
            if (FormResults.ContainsValue(resultFunc)) return FormResults
                                                        .Single(kvp => kvp.Value == resultFunc)
                                                        .Key;
            else return AddResultFunction(Guid.NewGuid().ToString(), resultFunc);
        }

        public static void SetUpResultFunctions()
        {
            foreach (var fxname in FormSFX.Keys) AddResultFunction("Play " + fxname, (o) => { return FormSFX[fxname].PlayToCompletion(); });
        }

        #region Specific forms / form templates
        public static Form DownSlash { get; set; }
        public static Form LeftSlash { get; set; }
        public static Form RightSlash { get; set; }
        public static Form HighParry { get; set; }
        public static Form LeftParry { get; set; }
        public static Form RightParry { get; set; }
        #endregion
    }
}