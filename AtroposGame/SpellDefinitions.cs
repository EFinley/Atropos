
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
        public Vector3 OrientationVector { get; set; }
        public float AngleTo(Quaternion other) { return Orientation.AngleTo(other); }
        public double AngleTo(Vector3 other) { return OrientationVector.AngleTo(other); }
        public Glyph NextGlyph { get; set; } // Or a *list* of NextGlyphs, associated with different spells?  Would need a way to know *which* spell is thus indicated... though not if spells are uniquely taught with no common gestures.
        public double SteadinessScoreWhenDefined { get; set; }
        public double OrientationSigma { get; set; }
        public string FeedbackSFXName { get; set; }
        public string ProgressSFXName { get; set; }
        //public IEffect FeedbackSFX { get { return MasterSpellLibrary.SpellSFX[FeedbackSFXName]; } }
        public IEffect FeedbackSFX { get; set; }
        public int FeedbackSFXid { get; set; }
        public IEffect ProgressSFX { get { return MasterSpellLibrary.SpellSFX[ProgressSFXName]; } }

        public string Name { get; set; }
        public string Description { get; set; }
        public string Instruction_Short { get; set; }
        public string Instruction_Long { get; set; }

        public Glyph(Quaternion? orientation = null, double steadinessBase = 0.0, double orientationSigma = 0.0, string progressSFXname = MasterSpellLibrary.defaultProgressSFXName, string feedbackSFXname = MasterSpellLibrary.defaultFeedbackSFXName)
        {
            Orientation = orientation ?? Quaternion.Identity;
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
            stringsArray = stringsArray.Concat(new string[] { Name, ProgressSFXName, FeedbackSFXName }).ToArray();
            return stringsArray.Join(",");
        }

        public static Glyph FromString(string inputStr)
        {
            try
            {
                var inputArray = inputStr.Trim('(', ')').Split(',');
                var floatsArray = new float[6];
                foreach (int i in Enumerable.Range(0, 6)) floatsArray[i] = float.Parse(inputArray[i]);

                string name = (inputArray.Length > 6) ? inputArray[6] : null;
                var pName = (inputArray.Length > 7) ? inputArray[7] : MasterSpellLibrary.defaultFeedbackSFXName;
                var fName = (inputArray.Length > 8) ? inputArray[8] : MasterSpellLibrary.defaultFeedbackSFXName;
                var Orientation = new Quaternion(floatsArray[0], floatsArray[1], floatsArray[2], floatsArray[3]);
                var outGlyph = new Glyph(Orientation, floatsArray[4], floatsArray[5], pName, fName) { Name = name };
                return outGlyph;
            }
            catch (Exception)
            {
                return EndOfSpell;
            }
        }

        private Glyph(double x, double y, double z, double w) : this(new Quaternion((float)x, (float)y, (float)z, (float)w))
        {

        }

        private Glyph(double x, double y, double z)
        {
            OrientationVector = new Vector3((float)x, (float)y, (float)z).Normalize();
            ProgressSFXName = MasterSpellLibrary.defaultProgressSFXName;
            FeedbackSFXName = MasterSpellLibrary.defaultFeedbackSFXName;
        }

        public static Glyph L = new Glyph(0, -1, 0) { Name = "Lesser", Instruction_Short = "Fingers down", FeedbackSFXName = "Magic.Aura", FeedbackSFXid = Resource.Raw.aura_magic  };
        public static Glyph M = new Glyph(0, 0, -1) { Name = "Moderate", Instruction_Short = "Palm up", FeedbackSFXName = "Magic.DeepVenetian", FeedbackSFXid = Resource.Raw._170660_DeepVenetian };
        public static Glyph H = new Glyph(0, 1, 0) { Name = "High", Instruction_Short = "Fingers up", FeedbackSFXName = "Magic.InfiniteAubergine", FeedbackSFXid = Resource.Raw._37847_infiniteauubergine };
        public static Glyph G = new Glyph(1, 0, 1) { Name = "Grand", Instruction_Short = "Thumb down, palm up", FeedbackSFXName = "Magic.Rommble", FeedbackSFXid = Resource.Raw._249186_120bpm6sanser_g };
        public static List<Glyph> MagnitudeGlyphs = new List<Glyph> { L, M, H }; // TODO: Add "G" only if user qualifies for it (i.e. after not just tutorials but active use)

        public static Glyph D = new Glyph(1, 0, 0) { Name = "Defense", Instruction_Short = "Thumb down", FeedbackSFXName = "Magic.MidtonePianesque", FeedbackSFXid = Resource.Raw._259944_midtonePianoesqueLoop };
        public static Glyph A = new Glyph(-1, 0, 0) { Name = "Attack", Instruction_Short = "Thumb up", FeedbackSFXName = "Magic.StrongerThanTheDark", FeedbackSFXid = Resource.Raw._316626_strongerThanTheDark_needsTrim };
        public static Glyph C = new Glyph(0, 0, 1) { Name = "Control", Instruction_Short = "Palm down", FeedbackSFXName = "Magic.MelodicPad", FeedbackSFXid = Resource.Raw._316629_melodicPad_needs_trimming };
        public static List<Glyph> SpellTypeGlyphs = new List<Glyph> { D, A, C };

        public static Glyph P = new Glyph(0, 1, 1) { Name = "Project", Instruction_Short = "Fingers up, palm down", FeedbackSFXName = "Magic.LowSmoothLoop", FeedbackSFXid = Resource.Raw._417781_guitar_loop };
        public static Glyph S = new Glyph(0, 1, -1) { Name = "Summon", Instruction_Short = "Fingers up, palm up", FeedbackSFXName = "Magic.Ommm", FeedbackSFXid = Resource.Raw._106561__soepy__om22k };
        public static Glyph K = new Glyph(0, -1, 1) { Name = "Ken", Instruction_Short = "Fingers down, palm down", FeedbackSFXName = "Magic.GrittyDrone", FeedbackSFXid = Resource.Raw._371887_gritty_drone };
        public static Glyph B = new Glyph(0, -1, -1) { Name = "Become", Instruction_Short = "Fingers down, palm up", FeedbackSFXName = "Magic.FemReverbCSharp", FeedbackSFXid = Resource.Raw._315850_femaleCSharpLoop };
        public static Glyph F = new Glyph(-1, 0, 1) { Name = "Fire", Instruction_Short = "Thumb up, palm down", FeedbackSFXName = "Magic.FireLoop", FeedbackSFXid = Resource.Raw._347706_fire_loop };
        public static Glyph I = new Glyph(1, 0, 1) { Name = "Ice", Instruction_Short = "Thumb down, palm down", FeedbackSFXName = "Magic.Galewinds", FeedbackSFXid = Resource.Raw._377068_galewinds };
        public static Glyph Z = new Glyph(-1, 0, -1) { Name = "Electricity", Instruction_Short = "Thumb up, palm up", FeedbackSFXName = "Magic.ElectricLoop", FeedbackSFXid = Resource.Raw._253324_electricity_loop };
        public static Glyph X = new Glyph(-1, 1, 0) { Name = "Mysteries", Instruction_Short = "Fingers up, thumb up", FeedbackSFXName = "Magic.AlienTheremin", FeedbackSFXid = Resource.Raw._344156_alientheremin };
        public static Glyph N = new Glyph(1, -1, 0) { Name = "Entropy", Instruction_Short = "Fingers down, thumb down", FeedbackSFXName = "Magic.NecroLoop", FeedbackSFXid = Resource.Raw._211638_necromantic_loop };
        public static Glyph R = new Glyph(-1, -1, 0) { Name = "Resonance", Instruction_Short = "Fingers down, thumb up", FeedbackSFXName = "Magic.NanobladeLoop", FeedbackSFXid = Resource.Raw._49685_nanoblade_loop };
        public static Glyph T = new Glyph(1, 1, 0) { Name = "Time", Instruction_Short = "Fingers up, thumb down", FeedbackSFXName = "Magic.ViolinLoop", FeedbackSFXid = Resource.Raw._81804_violinloop };
        public static List<Glyph> AllGlyphs = new List<Glyph> { L, M, H, G, D, A, C, P, S, K, B, F, I, Z, X, N, T, R };
        public static List<Glyph> ElementGlyphs { get => AllGlyphs.Where(g => !MagnitudeGlyphs.Contains(g) && !SpellTypeGlyphs.Contains(g)).ToList(); }

        public static Glyph StartOfSpell = new Glyph();
        public static Glyph EndOfSpell = new Glyph();

        public static bool operator ==(Glyph first, Glyph second)
        {
            return first?.Name == second?.Name && first?.OrientationVector == second?.OrientationVector;
        }
        public static bool operator !=(Glyph first, Glyph second)
        {
            return first?.Name != second?.Name || first?.OrientationVector != second?.OrientationVector;
        }
    }

    public class Spell
    {
        public string SpellName { get; set; }
        private Quaternion _zeroStance = Quaternion.Identity;
        public Quaternion ZeroStance { get { return (IsNewStyle) ? Glyphs[0].Orientation : _zeroStance; } set { _zeroStance = value; } }
        public bool IsNewStyle = false;
        public float AngleTo(Quaternion orientation) { return ZeroStance.AngleTo(orientation); }
        public List<Glyph> Glyphs { get; set; }
        public SpellResult Result { get; set; }

        public string CastResultName;
        public string FailResultName;
        private Func<object, Task> castResultFunc;
        private Func<object, Task> failResultFunc;
        public Func<object, Task> CastingResult
        {
            get { return castResultFunc; }
            set { CastResultName = MasterSpellLibrary.AddResultFunction(value); castResultFunc = value; }
        }
        public Func<object, Task> FailResult
        {
            get { return failResultFunc; }
            set { FailResultName = MasterSpellLibrary.AddResultFunction(value); failResultFunc = value; }
        }

        public Glyph Magnitude { get => Glyphs.ElementAtOrDefault(0); }
        public Glyph SpellType { get => Glyphs.ElementAtOrDefault(1); }
        public Glyph KeyGlyph { get => Glyphs.ElementAtOrDefault(2); }

        // Helper booleans for keeping track of the spell's effects.  TEMPORARY.
        public static bool IsInEffectAsCaster = false;
        public static bool IsInEffectAsTarget = false;

        public Spell(string name, Quaternion? zeroStance = null, string castResult = null, string failResult = null)
        {
            SpellName = name;
            Glyphs = new List<Glyph>();
            ZeroStance = zeroStance ?? Quaternion.Identity;
            CastResultName = castResult ?? MasterSpellLibrary.nullCastResultName;
            FailResultName = failResult ?? MasterSpellLibrary.nullCastResultName;
            if (!MasterSpellLibrary.CastingResults.TryGetValue(CastResultName, out castResultFunc))
                                        castResultFunc = MasterSpellLibrary.nullCastResultFunction;
            if (!MasterSpellLibrary.CastingResults.TryGetValue(FailResultName, out failResultFunc))
                                        failResultFunc = MasterSpellLibrary.nullCastResultFunction;
        }

        public Spell(string name, params Glyph[] glyphs)
        {
            SpellName = name;
            IsNewStyle = true;
            Glyphs = glyphs.ToList();

            castResultFunc = (o) => { return Speech.SayAllOf($"{SpellName} cast."); };
            // Gimmick to make sure that object initialization can happen before this executes
            Task.Delay(50).ContinueWith(_ =>
            {
                if (!MasterSpellLibrary.CastingResults.TryGetValue(CastResultName, out castResultFunc))
                    castResultFunc = (o) => { return Speech.SayAllOf($"Successfully cast {SpellName}."); };
                if (!MasterSpellLibrary.CastingResults.TryGetValue(FailResultName, out failResultFunc))
                    failResultFunc = MasterSpellLibrary.nullCastResultFunction;
            });
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
            return $"{SpellName};" + Glyphs.Select(g => g.ToString()).Join(":", "") + $";{ZeroStance.ToString()};{CastResultName};{FailResultName}";
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

    public static class NewSpellLibrary
    {
        public static List<Spell> AllSpells
            = new List<Spell>
            {
                new Spell("Dart", Glyph.L, Glyph.A, Glyph.P){ Result = SpellResult.Dart },
                new Spell("Zap", Glyph.L, Glyph.A, Glyph.Z, Glyph.P){ Result = SpellResult.Zap },
                new Spell("Barrier", Glyph.L, Glyph.D, Glyph.S){ Result = SpellResult.Barrier },
                new Spell("Shield", Glyph.M, Glyph.D, Glyph.X, Glyph.P){ Result = SpellResult.Shield },
                new Spell("Lance", Glyph.M, Glyph.A, Glyph.K, Glyph.P){ Result = SpellResult.Lance },
                new Spell("Frost", Glyph.M, Glyph.A, Glyph.I, Glyph.P){ Result = SpellResult.Frost },
                new Spell("Flame", Glyph.M, Glyph.A, Glyph.F, Glyph.P){ Result = SpellResult.Flame },
                new Spell("Clarity", Glyph.L, Glyph.C, Glyph.X, Glyph.T){ Result = SpellResult.Clarity },
                new Spell("Dazzle", Glyph.M, Glyph.C, Glyph.X, Glyph.P){ Result = SpellResult.Dazzle },
                new Spell("Paralyze", Glyph.H, Glyph.C, Glyph.B, Glyph.K, Glyph.N){ Result = SpellResult.Paralyze }
            };
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
                { "Magic.Transmat", Res.SFX.Preregister("Magic.Transmat", Resource.Raw._234804_transmat) },
                { "Magic.LowSmoothLoop", Res.SFX.Preregister("Magic.LowSmoothLoop", Resource.Raw._417781_guitar_loop) },
                { "Magic.FireLoop", Res.SFX.Preregister("Magic.FireLoop", Resource.Raw._347706_fire_loop) },
                { "Magic.ElectricLoop", Res.SFX.Preregister("Magic.ElectricLoop", Resource.Raw._253324_electricity_loop) },
                { "Magic.WaterLoop", Res.SFX.Preregister("Magic.WaterLoop", Resource.Raw._366159_water_loop) }
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
            //AddResultFunction("SonarDemo", (o) => { SpellResult.SonarDemo.OnCast(o); return Task.CompletedTask; });
            foreach (var fxname in SpellSFX.Keys) AddResultFunction("Play " + fxname, PlaySFXResultFunctionFor(fxname));
        }
    }
}