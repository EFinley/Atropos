
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
    public class Glyph
    {
        public Quaternion Orientation { get; set; }
        public float AngleTo(Quaternion other) { return Orientation.AngleTo(other); }
        public Glyph NextGlyph { get; set; } // Or a *list* of NextGlyphs, associated with different spells?  Would need a way to know *which* spell is thus indicated... though not if spells are uniquely taught with no common gestures.
        public double SteadinessScoreWhenDefined { get; set; }
        public double OrientationSigma { get; set; }
        public string FeedbackSFXName { get; set; }
        public string ProgressSFXName { get; set; }
        public IEffect FeedbackSFX { get { return MasterSpellLibrary.SpellSFX[FeedbackSFXName]; } }
        public IEffect ProgressSFX { get { return MasterSpellLibrary.SpellSFX[ProgressSFXName]; } }

        public Glyph(Quaternion orientation, double steadinessBase, double orientationSigma, string progressSFXname = MasterSpellLibrary.defaultProgressSFXName, string feedbackSFXname = MasterSpellLibrary.defaultFeedbackSFXName)
        {
            Orientation = orientation;
            SteadinessScoreWhenDefined = steadinessBase;
            OrientationSigma = orientationSigma;
            ProgressSFXName = progressSFXname;
            FeedbackSFXName = feedbackSFXname;
            NextGlyph = EndOfSpell;
        }

        public override string ToString()
        {
            var valuesArray = new double[] { Orientation.X, Orientation.Y, Orientation.Z, Orientation.W, SteadinessScoreWhenDefined, OrientationSigma };
            var stringsArray = valuesArray.Select(d => d.ToString("f4")).ToArray();
            stringsArray = stringsArray.Concat(new string[] { ProgressSFXName, FeedbackSFXName }).ToArray();
            return stringsArray.Join(",");
        }

        public static Glyph FromString(string inputStr)
        {
            try
            {
                var inputArray = inputStr.Trim('(', ')').Split(',');
                var floatsArray = new float[6];
                foreach (int i in Enumerable.Range(0, 6)) floatsArray[i] = float.Parse(inputArray[i]);

                var pName = (inputArray.Length > 6) ? inputArray[6] : MasterSpellLibrary.defaultProgressSFXName;
                var fName = (inputArray.Length > 7) ? inputArray[7] : MasterSpellLibrary.defaultFeedbackSFXName;
                var Orientation = new Quaternion(floatsArray[0], floatsArray[1], floatsArray[2], floatsArray[3]);
                var outGlyph = new Glyph(Orientation, floatsArray[4], floatsArray[5], pName, fName);
                return outGlyph;
            }
            catch (Exception)
            {
                return EndOfSpell;
            }
        }

        public static Glyph EndOfSpell = new Glyph(Quaternion.Identity, 0.0, 0.0, MasterSpellLibrary.defaultSuccessSFXName);
    }

    public class Spell
    {
        public string SpellName { get; set; }
        public Quaternion ZeroStance { get; set; } = Quaternion.Identity;
        public float AngleTo(Quaternion orientation) { return ZeroStance.AngleTo(orientation); }
        public List<Glyph> Glyphs { get; set; }
        private string castResultName;
        private string failResultName;
        private Func<object, Task> castResultFunc;
        private Func<object, Task> failResultFunc;
        public Func<object, Task> CastingResult
        {
            get { return castResultFunc; }
            set { castResultName = MasterSpellLibrary.AddResultFunction(value); castResultFunc = value; }
        }
        public Func<object, Task> FailResult
        {
            get { return failResultFunc; }
            set { failResultName = MasterSpellLibrary.AddResultFunction(value); failResultFunc = value; }
        }

        public Spell(string name, Quaternion? zeroStance = null, string castResult = null, string failResult = null)
        {
            SpellName = name;
            Glyphs = new List<Glyph>();
            ZeroStance = zeroStance ?? Quaternion.Identity;
            castResultName = castResult ?? MasterSpellLibrary.nullCastResultName;
            failResultName = failResult ?? MasterSpellLibrary.nullCastResultName;
            if (!MasterSpellLibrary.CastingResults.TryGetValue(castResultName, out castResultFunc))
                                        castResultFunc = MasterSpellLibrary.nullCastResultFunction;
            if (!MasterSpellLibrary.CastingResults.TryGetValue(failResultName, out failResultFunc))
                                        failResultFunc = MasterSpellLibrary.nullCastResultFunction;
        }

        // Handle old saved spells gracefully.
        public Spell(string name, string successSFXname, string castResult = MasterSpellLibrary.nullCastResultName, string failResult = MasterSpellLibrary.nullCastResultName)
            :this(name, default(Quaternion), castResult, failResult)
        {
            ZeroStance = Quaternion.Identity;
            if (castResult == MasterSpellLibrary.nullCastResultName && MasterSpellLibrary.SpellSFX.ContainsKey(successSFXname))
            {
                castResultName = "Play " + successSFXname;
                castResultFunc = MasterSpellLibrary.CastingResults[castResultName];
            }
        }

        public void AddGlyph(Glyph newGlyph)
        {
            if (Glyphs.Count > 0) Glyphs.Last().NextGlyph = newGlyph;
            Glyphs.Add(newGlyph);
        }

        public void UndoAddGlyph()
        {
            Glyphs.RemoveAt(Glyphs.Count - 1);
            if (Glyphs.Count > 0) Glyphs.Last().NextGlyph = Glyph.EndOfSpell;
        }

        public override string ToString()
        {
            return $"{SpellName};" + Glyphs.Select(g => g.ToString()).Join(":", "") + $";{ZeroStance.ToString()};{castResultName};{failResultName}";
        }

        public static Spell FromString(string inputStr)
        {
            var subStrings = inputStr.Split(';');
            Spell resultSpell;
            if (subStrings.Count() == 5) resultSpell = new Spell(subStrings[0], new Quaternion().Parse(subStrings[2]), subStrings[3], subStrings[4]);
            else if (subStrings.Count() == 4) resultSpell = new Spell(subStrings[0], Quaternion.Identity, subStrings[2], subStrings[3]);
            else resultSpell = new Spell(subStrings[0]);
            var glyphStrings = subStrings[1].Split(':');
            foreach (var glyphStr in glyphStrings)
            {
                resultSpell.AddGlyph(Glyph.FromString(glyphStr));
            }
            return resultSpell;
        }

        public static Spell None = new Spell("No Spell");
    }

    public static class MasterSpellLibrary
    {
        public static OrderedDictionary<string, IEffect> SpellSFX;
        public const string defaultFeedbackSFXName = "Magic.Ethereal";
        public const string defaultProgressSFXName = "Magic.Zwip";
        public const string defaultSuccessSFXName = "Magic.Fwoosh";
        public static IEffect SpellFeedbackSFX;
        public static IEffect SpellProgressSFX;
        public static IEffect SpellSuccessSFX;

        public const string nullCastResultName = "Magic.Result.Null";
        public static readonly Func<object, Task> nullCastResultFunction = (o) => { return Task.CompletedTask; };
        public static Dictionary<string, Func<object, Task>> CastingResults 
            = new Dictionary<string, Func<object, Task>> { {nullCastResultName, nullCastResultFunction } };

        private const string persistentLibraryName = "MasterSpellLib";
        private const string masterIndexName = "MasterSpellIndex";
        private static SimpleStorage persistentLibrary { get; set; }
        public static List<string> spellNames { get; set; }

        private static Nito.AsyncEx.AsyncManualResetEvent _sfxReadyFlag = new Nito.AsyncEx.AsyncManualResetEvent();
        public static Task GetSFXReadyTask() {
            return _sfxReadyFlag.WaitAsync(); }

        public static void LoadAll()
        {
            Res.Mark("LoadAll begins");

            persistentLibrary = SimpleStorage.EditGroup(persistentLibraryName);
            spellNames = persistentLibrary.Get<List<string>>(masterIndexName);
            if (spellNames == null) spellNames = new List<string>();
            if (spellNames.Count == 0) Inscribe(Spell.None); // Weird errors get thrown up with an empty master index.

            LoadAllSpellSFX(Application.Context);
            SpellFeedbackSFX = SpellSFX[defaultFeedbackSFXName];
            SpellProgressSFX = SpellSFX[defaultProgressSFXName];
            SpellSuccessSFX = SpellSFX[defaultSuccessSFXName];

            Res.Mark("Spell SFX loaded");

            SetUpResultFunctions();
        }

        public static void LoadAllSpellSFX(Context ctx)
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            SpellSFX = new OrderedDictionary<string, IEffect>
            {
                { defaultFeedbackSFXName, Res.SFX.Preregister(defaultFeedbackSFXName, Resource.Raw.ethereal) },
                { "Magic.Aura", Res.SFX.Preregister("Magic.Aura", Resource.Raw.aura_magic) },
                { "Magic.DeepVenetian", Res.SFX.Preregister("Magic.DeepVenetian", Resource.Raw._170660_DeepVenetian) },
                { "Magic.InfiniteAubergine", Res.SFX.Preregister("Magic.InfiniteAubergine", Resource.Raw._37847_infiniteauubergine) },
                { "Magic.Ommm", Res.SFX.Preregister("Magic.Ommm", Resource.Raw._106561__soepy__om22k) },
                { "Magic.AfricanDrums", Res.SFX.Preregister("Magic.AfricanDrums", Resource.Raw._202419_african_drums) },
                { "Magic.Rommble", Res.SFX.Preregister("Magic.Rommble", Resource.Raw._249186_120bpm6sanser_g) },
                { "Magic.MidtonePianesque", Res.SFX.Preregister("Magic.MidtonePianesque", Resource.Raw._259944_midtonePianoesqueLoop) },
                { "Magic.FemReverbDSharp", Res.SFX.Preregister("Magic.FemReverbDSharp", Resource.Raw._315849_femaleDSharpLoop) },
                { "Magic.FemReverbCSharp", Res.SFX.Preregister("Magic.FemReverbCSharp", Resource.Raw._315850_femaleCSharpLoop) },
                { "Magic.FemReverbF", Res.SFX.Preregister("Magic.FemReverbF", Resource.Raw._315855_femaleFLoop) },
                { "Magic.FemReverbE", Res.SFX.Preregister("Magic.FemReverbE", Resource.Raw._315856_femaleELoop) },
                //{ "Magic.AlmostTooChipper", Res.SFX.Preregister("Magic.AlmostTooChipper", Resource.Raw._342294_almostTooChipper) },
                { "Magic.AlienTheremin", Res.SFX.Preregister("Magic.AlienTheremin", Resource.Raw._344156_alientheremin) },
                { "Magic.TrompingBuzzPulse", Res.SFX.Preregister("Magic.TrompingBuzzPulse", Resource.Raw._344694_tromping_buzzpulse) },
                { "Magic.GrittyDrone", Res.SFX.Preregister("Magic.GrittyDrone", Resource.Raw._371887_gritty_drone) },
                { "Magic.Galewinds", Res.SFX.Preregister("Magic.Galewinds", Resource.Raw._377068_galewinds) },
                { "Magic.NanobladeLoop", Res.SFX.Preregister("Magic.NanobladeLoop", Resource.Raw._49685_nanoblade_loop) },
                { "Magic.ViolinLoop", Res.SFX.Preregister("Magic.ViolinLoop", Resource.Raw._81804_violinloop) },
                { "Magic.StrongerThanTheDark", Res.SFX.Preregister("Magic.StrongerThanTheDark", Resource.Raw._316626_strongerThanTheDark_needsTrim) },
                { "Magic.MelodicPad", Res.SFX.Preregister("Magic.MelodicPad", Resource.Raw._316629_melodicPad_needs_trimming) },
                //{ "Magic.MelodicPad2", Res.SFX.Preregister("Magic.MelodicPad2", Resource.Raw._316628_melodicPad2_could_use_trimming) },
                { "Magic.NecroLoop", Res.SFX.Preregister("Magic.NecroLoop", Resource.Raw._211638_necromantic_loop) },
                { "Magic.AncientSpirits", Res.SFX.Preregister("Magic.AncientSpirits", Resource.Raw._266419_ancient_spirits) },
                { "Magic.Detection", Res.SFX.Preregister("Magic.Detection", Resource.Raw.detection_magic) },
                { defaultProgressSFXName, Res.SFX.Preregister(defaultProgressSFXName, Resource.Raw.zwip_magic) },
                { defaultSuccessSFXName, Res.SFX.Preregister(defaultSuccessSFXName, Resource.Raw.fwoosh_magic) },
                { "Magic.Fzazzle", Res.SFX.Preregister("Magic.Fzazzle", Resource.Raw.fzazzle_magic) },
                { "Magic.Kblaa", Res.SFX.Preregister("Magic.Kblaa", Resource.Raw.kblaa_magic) },
                { "Magic.Riffle", Res.SFX.Preregister("Magic.Riffle", Resource.Raw.riffle_magic) },
                { "Magic.RingBuzz", Res.SFX.Preregister("Magic.RingBuzz", Resource.Raw.ring_buzz_magic) },
                { "Magic.Tinkly", Res.SFX.Preregister("Magic.Tinkly", Resource.Raw.tinkly_magic) },
                { "Magic.DenseMetallicBoom", Res.SFX.Preregister("Magic.DenseMetallicBoom", Resource.Raw._366093_kwongg) },
                { "Magic.ElectricZapSmall", Res.SFX.Preregister("Magic.ElectricZapSmall", Resource.Raw._136542_electricZap_small) },
                { "Magic.ElectricZapMedium", Res.SFX.Preregister("Magic.ElectricZapMedium", Resource.Raw._315918_electricZap_med) },
                //{ "Magic.MetallicBoom", Res.SFX.Preregister("Magic.MetallicBoom", Resource.Raw._209772_metallicBoom) },
                { "Magic.ElectricArc", Res.SFX.Preregister("Magic.ElectricArc", Resource.Raw._277314_electricArc) },
                { "Magic.LightningStrike", Res.SFX.Preregister("Magic.LightningStrike", Resource.Raw._29675_lightningStrike_needs_trimming, 7.75, 12.5) },
                { "Magic.Zwong", Res.SFX.Preregister("Magic.Zwong", Resource.Raw._316631_zwong) },
                { "Magic.Fwehshh", Res.SFX.Preregister("Magic.Fwehshh", Resource.Raw._248116_fwehshh) },
                { "Magic.Fwissh", Res.SFX.Preregister("Magic.Fwissh", Resource.Raw._346916_fwissh) },
                { "Magic.FFwush", Res.SFX.Preregister("Magic.FFwush", Resource.Raw._346917_ffwush) },
                { "Magic.ZoomSlowingDown", Res.SFX.Preregister("Magic.ZoomSlowingDown", Resource.Raw._366092_zoom_slowing_down) },
                { "Magic.WindingDownwnwnnn", Res.SFX.Preregister("Magic.WindingDownwnwnnn", Resource.Raw._216093_windingdownwnwnnn) },
                { "Magic.SonarAlert", Res.SFX.Preregister("Magic.SonarAlert", Resource.Raw._351602_sonar) },
                { "Magic.Transmat", Res.SFX.Preregister("Magic.Transmat", Resource.Raw._234804_transmat) }
            };
            Log.Debug("Spells", $"Spell SFXes loaded after {sw.ElapsedMilliseconds} ms.");
            _sfxReadyFlag.Set();
        }

        public static Spell Get(string spellname)
        {
            if (spellNames.Contains(spellname) && persistentLibrary.Get(spellname) != null)
            {
                return Spell.FromString(persistentLibrary.Get(spellname));
            }
            else
            {
                Log.Error("SpellLibrary", $"Could not find spell '{spellname}' in master library.");
                return new Spell(spellname);
            }
        }

        public static void Inscribe(Spell newspell)
        {
            persistentLibrary.Put(newspell.SpellName, newspell.ToString());
            if (!spellNames?.Contains(newspell.SpellName) ?? false) spellNames.Add(newspell.SpellName);
            persistentLibrary.Put(masterIndexName, spellNames);
        }

        public static void Erase(string spellname)
        {
            persistentLibrary.Delete(spellname);
            spellNames.Remove(spellname);
            persistentLibrary.Put(masterIndexName, spellNames);
        }

        public static string AddResultFunction(string resultName, Func<object, Task> resultFunc)
        {
            if (CastingResults.ContainsKey(resultName)) CastingResults[resultName] = resultFunc;
            else CastingResults.Add(resultName, resultFunc);
            return resultName;
        }
        public static string AddResultFunction(Func<object, Task> resultFunc)
        {
            if (CastingResults.ContainsValue(resultFunc)) return CastingResults
                                                                          .Where(kvp => kvp.Value == resultFunc)
                                                                          .Select(kvp => kvp.Key)
                                                                          .First();
            else return AddResultFunction(Guid.NewGuid().ToString(), resultFunc);
        }

        public static Func<object, Task> PlaySFXResultFunctionFor(string SFXname)
        {
            return (o) =>
            {
                var fx = SpellSFX[SFXname] as Effect;
                if (fx == null) return Task.CompletedTask;
                var prevSpeakerMode = fx.SpeakerMode;
                fx.SpeakerMode = true;
                return fx.PlayToCompletion(1.0).ContinueWith(_ => { fx.SpeakerMode = prevSpeakerMode; });
            };
        }
        public static void SetUpResultFunctions()
        {
            AddResultFunction("Play " + defaultSuccessSFXName, PlaySFXResultFunctionFor(defaultSuccessSFXName));
            AddResultFunction("SonarDemo", Spell_Results.SonarDemo);
            foreach (var fxname in SpellSFX.Keys) AddResultFunction("Play " + fxname, PlaySFXResultFunctionFor(fxname));
        }
    }
}