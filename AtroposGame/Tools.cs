
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Nfc;
using Android.OS;
using Android.Widget;
using Android.Util;


// using Accord.Math;
// using Accord.Statistics;
using Android.Content;
using System.Threading.Tasks;
using System.Numerics;
using Vector3 = System.Numerics.Vector3;

namespace Atropos
{
    public abstract class Tool //: ITrackSteadiness
    {
        public string TagID { get; set; }

        public Tool(string tagID = null)
        {
            // Need an arbitrary, essentially random, identifier... but GUIDs in string form are too long, so we just use the (still very arbitrary) first substring of one.
            TagID = tagID ?? InteractionLibrary.CurrentSpecificTag ?? Guid.NewGuid().ToString().Split('-')[0];

            // Temporary - set the prop's axes the same as the phone axes.
            // (If phone is face-up, long axis pointed toward target, this will be essentially correct.)
            pitch_axis = new Vector3(1.0f, 0.0f, 0.0f);
            roll_axis = new Vector3(0.0f, 1.0f, 0.0f);
            yaw_axis = new Vector3(0.0f, 0.0f, 1.0f);
        }

        public double SteadinessBaseline { get; set; } = 1000; // Large but noninfinite value - implies "you were incredibly steady during calibration."

        // Geometric measures - how is the phone oriented inside the prop?
        //protected Quaternion propRotation;
        protected Vector3 pitch_axis, roll_axis, yaw_axis;

        // And how do we use those externally?
        public Vector3 Local(Vector3 deviceGyroVec)
        {
            return new Vector3(Pitch(deviceGyroVec), Roll(deviceGyroVec), Yaw(deviceGyroVec));
            //return Vector3.Transform(deviceGyroVec, propRotation);
        }
        public float Pitch(Vector3 deviceGyroVec)
        {
            return Vector3.Dot(pitch_axis, deviceGyroVec);
            //return Local(deviceGyroVec).X;
        }
        public float Roll(Vector3 deviceGyroVec)
        {
            return Vector3.Dot(roll_axis, deviceGyroVec);
            //return Local(deviceGyroVec).Y;
        }
        public float Yaw(Vector3 deviceGyroVec)
        {
            return Vector3.Dot(yaw_axis, deviceGyroVec);
            //return Local(deviceGyroVec).Z;
        }
        
        public virtual void SaveSpecifics()
        {
            Res.SpecificTags.Put(TagID, this.ToString());
        }
    }

    /// <summary>
    /// Collects all the information about a particular weapon, both game-mechanical and informational
    /// </summary>
    public class Gun : Tool
    {
        public enum AmmoType
        {
            Standard,
            Penetrating,
            ArmorPiercing,
            Flechette,
            Explosive
        }

        public enum FireMode
        {
            SingleShot,
            BurstFire,
            FullAuto
        }

        public enum IFFMode
        {
            NeverFriendlyFire,
            RiskOfFriendlyFire,
            IntentionalFriendlyFire
        }

        public Gun(string tagID = null) : base(tagID)
        {
            CurrentAmmoCount = MaxAmmoCapacity;
            CurrentAmmoType = AmmoType.Standard;
            CurrentFireMode = FireMode.SingleShot;
        }

        // Values set during the gun calibration gesture series - unnecessary, once you've derived the propRotation.
        public Vector3 vectorPointedForward = Vector3.UnitY, vectorPointedDown = Vector3.UnitX;
        public Quaternion orientationPointedForward, orientationPointedDown;
        public void GunCalibrationFinalize()
        {
            // Old way
            Vector3.Normalize(vectorPointedForward);
            Vector3.Normalize(vectorPointedDown);
            pitch_axis = Vector3.Cross(vectorPointedDown, vectorPointedForward);
            Vector3.Normalize(pitch_axis);
            var roll_axis_approx = Vector3.Add(vectorPointedForward, Vector3.Cross(pitch_axis, vectorPointedDown));
            Vector3.Normalize(roll_axis_approx);
            yaw_axis = Vector3.Cross(pitch_axis, roll_axis_approx);
            Vector3.Normalize(yaw_axis);
            roll_axis = Vector3.Cross(yaw_axis, pitch_axis); // No need to normalize this one.

            // New way
            //propRotation = ReferenceFrame.CalibratePropOrientation(orientationPointedForward, orientationPointedDown);
            SaveSpecifics();
        }

        // Sound effects
        public IEffect CockSFX;
        public IEffect ClickEmptySFX;
        public IEffect ReloadSFX;
        public IEffect ShotSFX;
        public IEffect SteadinessHintSFX;
        
        // Firing characteristic values (constant for now)
        public double MinimumRecoilJerk = 2.0;
        public TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);
        public TimeSpan MinimumAimTime = TimeSpan.FromSeconds(1.25);
        public TimeSpan MaximumAimTime = TimeSpan.FromSeconds(5.0);
        public TimeSpan OptimumRecoilCompensationTime = TimeSpan.FromSeconds(0.7);
        public TimeSpan CooldownPeriod = TimeSpan.FromSeconds(0.5);
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(5.0);
        public double RecoilAmount = 5.0; // Testing

        // Capabilities and scalars 
        public int MaxAmmoCapacity = 12; // Constant for now.
        public AmmoType[] AmmoTypesAvailable = new AmmoType[] { AmmoType.Standard };
        public bool AutoSelectAmmo = false;
        public bool SupportsBurstFire = false;
        public bool SupportsFullAutomatic = false;
        public bool SupportsRiskyIFFMode = false;
        public double CoverImpliedByHorizontalHold = 0.25;

        // Tracking variables, current state, etc
        public int CurrentAmmoCount;
        public AmmoType CurrentAmmoType;
        public FireMode CurrentFireMode;
        public IFFMode CurrentIFFMode;
        //public bool IsReadyToFire = true;

        public override string ToString()
        {
            var values = new double[] {pitch_axis.X, pitch_axis.Y, pitch_axis.Z,
                                        roll_axis.X, roll_axis.Y, roll_axis.Z,
                                        yaw_axis.X, yaw_axis.Y, yaw_axis.Z,
                                        SteadinessBaseline, MinimumRecoilJerk };
            var times = new TimeSpan[] { UpdateInterval, MinimumAimTime, MaximumAimTime, OptimumRecoilCompensationTime, CooldownPeriod };
            values = values.Concat(times.Select(t => t.TotalSeconds)).ToArray();
            return values
                   .Select(v => v.ToString("f4"))
                   .Join(";", "");
        }
        
        public static Gun FromString(string inputStr, string TagID = null)
        {
            float[] vals = inputStr
                           .Split(';')
                           .Select(v => float.Parse(v))
                           .ToArray();
            Gun resultGun = new Gun(TagID);
            try
            {
                resultGun.pitch_axis = new Vector3(vals[0], vals[1], vals[2]);
                resultGun.pitch_axis = new Vector3(vals[3], vals[4], vals[5]);
                resultGun.pitch_axis = new Vector3(vals[6], vals[7], vals[8]);
                resultGun.SteadinessBaseline = vals[9];
                resultGun.MinimumRecoilJerk = vals[10];
                resultGun.UpdateInterval = TimeSpan.FromSeconds(vals[11]);
                resultGun.MinimumAimTime = TimeSpan.FromSeconds(vals[12]);
                resultGun.MaximumAimTime = TimeSpan.FromSeconds(vals[13]);
                resultGun.OptimumRecoilCompensationTime = TimeSpan.FromSeconds(vals[14]);
                resultGun.CooldownPeriod = TimeSpan.FromSeconds(vals[15]);
            }
            catch (Exception)
            {
                Log.Error("Gun Specifics", $"Error loading gun from string.  Found {vals.Length} values, needing 16.  String was:\n{inputStr}, Tag ID was {TagID}.");
                throw;
            }
            return resultGun;
        }
    }

    public class Focus : Tool
    {
        public Quaternion ZeroOrientation { get; set; }
        public Quaternion FrameShift { get { return ZeroOrientation.Inverse(); } set { ZeroOrientation = value.Inverse(); } }

        public Focus(string TagID = null) : base(TagID)
        {
            KnownSpells = new List<Spell>();
        }

        public List<Spell> KnownSpells;

        public void LearnSpell(Spell newSpell)
        {
            if (newSpell.SpellName == Spell.None.SpellName) return;
            Spell closestZeroAngleSpell = KnownSpells
                                .Where(s => s.SpellName != newSpell.SpellName && s.ZeroStance != newSpell.ZeroStance && s.ZeroStance != Quaternion.Identity)
                                .DefaultIfEmpty()
                                .OrderBy(s => s?.AngleTo(newSpell.ZeroStance) ?? float.PositiveInfinity)
                                .FirstOrDefault();
            if ((closestZeroAngleSpell?.AngleTo(newSpell.ZeroStance) ?? 31.0) < 30.0)
            {
                Log.Warn("Spell learning", $"Caution - existing spell {closestZeroAngleSpell.SpellName} has a zero stance that's only {closestZeroAngleSpell.AngleTo(newSpell.ZeroStance)} degress away, which is probably too close.  Recommend unlearning it and retraining.");
                MasterSpellLibrary.SpellSFX["Magic.Kblaa"].Play();
            }

            KnownSpells.Add(newSpell);
            SaveSpecifics();
        }
        public void LearnSpell(string spellString)
        {
            LearnSpell(Spell.FromString(spellString));
        }

        public void ForgetSpell(string spellName)
        {
            var firstSuchSpell = KnownSpells.FirstOrDefault(s => s.SpellName == spellName);
            if (firstSuchSpell == default(Spell)) return;
            KnownSpells.Remove(firstSuchSpell);
            SaveSpecifics();
        }

        public static MemorylessFocus MasterFocus;
        public static void InitMasterFocus()
        {
            if (MasterFocus != null) return;
            Log.Debug("Tools", $"Current master spell list: {MasterSpellLibrary.spellNames.Join()}");
            MasterFocus = new MemorylessFocus("MasterFocus");
            MasterFocus.ZeroOrientation = Quaternion.Identity * 0.9999f; // Makes it no longer "isIdentity" in technical terms.
            foreach (var spellName in MasterSpellLibrary.spellNames ?? new List<string>()) MasterFocus.LearnSpell(MasterSpellLibrary.Get(spellName));
        }

        public override string ToString()
        {
            return KnownSpells
                   .Select(s => s.ToString())
                   .Join("|", "");
        }

        public static Focus FromString(string inputStr, string TagID)
        {
            string[] spells = inputStr
                           .Split('|')
                           .ToArray();
            Focus resultFocus = new Focus(TagID);
            try
            {
                foreach (var spellstring in spells)
                {
                    resultFocus.LearnSpell(Spell.FromString(spellstring));
                }
            }
            catch (Exception)
            {
                Log.Error("Focus Specifics", $"Error loading focus from string.  String was:\n{inputStr}, Tag ID was {TagID}.");
                throw;
            }
            return resultFocus;
        }
    }

    public class MemorylessFocus : Focus
    {
        public MemorylessFocus(string TagID = null) : base(TagID) { }

        public override void SaveSpecifics()
        {
            // Do nothing!  This type of Focus is never saved to the file.
        }
    }

    public class Sword : Tool
    {
        public AdvancedRollingAverage<Quaternion> EnGardeOrientation { get; set; }

        public Sword(string TagID = null) : base(TagID)
        {
            KnownForms = new List<Form>();
        }

        public List<Form> KnownForms;

        public void LearnForm(Form newForm)
        {
            //Form closestZeroAngleForm = KnownForms
            //                    .Where(s => s.FormName != newForm.FormName && s.InitialOrientation != newForm.InitialOrientation && s.InitialOrientation != Quaternion.Identity)
            //                    .DefaultIfEmpty()
            //                    .OrderBy(s => s?.AngleTo(newForm.InitialOrientation) ?? float.PositiveInfinity)
            //                    .FirstOrDefault();
            //if ((closestZeroAngleForm?.AngleTo(newForm.InitialOrientation) ?? 31.0) < 30.0)
            //{
            //    Log.Warn("Form learning", $"Caution - existing form {closestZeroAngleForm.FormName} has a zero stance that's only {closestZeroAngleForm.AngleTo(newForm.InitialOrientation)} degress away, which is probably too close.  Recommend unlearning it and retraining.");
            //    MasterFechtbuch.DefensiveSFX.Play();
            //}

            KnownForms.Add(newForm);
            SaveSpecifics();
        }
        public void LearnForm(string formString)
        {
            LearnForm(Form.FromString(formString));
        }

        public void ForgetForm(string formName)
        {
            var firstSuchForm = KnownForms.FirstOrDefault(s => s.FormName == formName);
            if (firstSuchForm == default(Form)) return;
            KnownForms.Remove(firstSuchForm);
            SaveSpecifics();
        }

        //public static MemorylessSword MasterSword;
        public static Sword MasterSword;
        public static void InitMasterSword()
        {
            if (MasterSword != null) return;
            if (Res.SpecificTags.Get("MasterSword") != null)
            {
                try
                {
                    MasterSword = Sword.FromString(Res.SpecificTags.Get("MasterSword"), "MasterSword");
                    // This will often throw if (e.g.) we've changed the definition of FromString since the last time we saved it.
                    // That's fine, it just means treat it as if there were no saved sword at all.
                    if (MasterSword.EnGardeOrientation == null)
                        throw new Exception("en garde null");
                    if (MasterSword.EnGardeOrientation.Average.IsIdentity)
                        throw new Exception("en garde is identity");
                    if (MasterSword.KnownForms != null && MasterSword.KnownForms.Count > 0)
                    {
                        var fLast = MasterSword.KnownForms.Last();
                        if (fLast == null || fLast.InitialOrientation == null || fLast.InitialOrientation.IsIdentity)
                            throw new Exception($"malformed last form ({fLast})."); 
                    }
                }
                catch (Exception e)
                {
                    Log.Debug("MasterSword", $"Malformed Master Sword ({e.Message}).  Resetting it.");
                    Res.SpecificTags.Delete("MasterSword");
                    MasterSword = null;
                }
            }

            if (MasterSword == null)
            {
                MasterSword = new Sword("MasterSword");
                //MasterSword.EnGardeOrientation = new AdvancedRollingAverageQuat(10, null, Quaternion.Identity);
            }
            Log.Debug("Tools", $"Current master form list: {MasterFechtbuch.formNames.Join()}");
            foreach (var formName in MasterFechtbuch.formNames)
            {
                var f = MasterFechtbuch.Get(formName);
                if (f != Form.None) MasterSword.LearnForm(f);
            }
        }

        public override string ToString()
        {
            var st = KnownForms
                   .Select(s => s.ToString())
                   .Join("|", "");
            if (KnownForms.Count > 0) st += "|";
            return string.Concat(st, $"{EnGardeOrientation.Average.ToString()}#{EnGardeOrientation.RelativeStdDev}");
        }

        public static Sword FromString(string inputStr, string TagID)
        {
            string[] forms = inputStr
                           .Split('|')
                           .ToArray();
            Sword resultSword = new Sword(TagID);
            try
            {
                if (forms.Length > 1)
                {
                    foreach (var formstring in forms.Take(forms.Length - 1))
                    {
                        if (formstring.Length == 0 || formstring == null) continue;
                        resultSword.LearnForm(Form.FromString(formstring));
                    } 
                }
                var details = forms.Last().Split('#');
                resultSword.EnGardeOrientation = AdvancedRollingAverage<Quaternion>.Create<Quaternion>(10, new Quaternion().FromString(details[0]), float.Parse(details[1]));
            }
            catch (Exception)
            {
                Log.Error("Sword Specifics", $"Error loading sword from string.  String was: {inputStr}, Tag ID was {TagID}.");
                throw;
            }
            return resultSword;
        }
    }

    public class MemorylessSword : Sword
    {
        public MemorylessSword(string TagID = null) : base(TagID) { }

        public override void SaveSpecifics()
        {
            // Do nothing!  This type of Sword is never saved to the file.
        }
    }

    public class Toolkit : Tool
    {
        //public enum Function
        //{
        //    Examine,
        //    Multimeter,
        //    Wirecutter,
        //    Solderer,
        //    Lockpicks,
        //    Safecracking,
        //    ReturnToStart
        //}
        //// Note - Length variable has to be manually updated if you add/remove items to the above list, AFAIK!
        //public static int FunctionListLength = 6;
        //public static Dictionary<Function, Res.InteractionMode> InteractionModes
        //    = new Dictionary<Function, Res.InteractionMode>()
        //    {
        //        { Function.Examine, InteractionLibrary.SecurityPanel },
        //        { Function.Multimeter, InteractionLibrary.SecurityPanel },
        //        { Function.Wirecutter, InteractionLibrary.SecurityPanel },
        //        { Function.Solderer, InteractionLibrary.SecurityPanel },
        //        { Function.Lockpicks, InteractionLibrary.LockPicking },
        //        { Function.Safecracking, InteractionLibrary.SafeCracking }
        //    };
        // TODO - Turn this into a full-fledged ToolkitDefinitions file like for spells and so forth.
        
        //public Function currentFunction { get; set; }
        //public void NextFunction(bool verbalCue = true)
        //{
        //    currentFunction++;
        //    if (currentFunction == Function.ReturnToStart) currentFunction = Function.Examine;
        //    var optionWords = new string[] { "Picking", "Trying", "Selecting", "Maybe", "Perhaps", "Let's try", "Opting for" };
        //    if (verbalCue) Speech.Say($"{optionWords.GetRandom()} {currentFunction}");
        //}

        public double ExamineSpeedMultiplier = 1.0;
        public double MultimeterSpeedMultiplier = 1.0;
        public double WireCutterSpeedMultiplier = 1.0; // These speeds are dependent on the specific linkage you're attacking.
        public double SolderingSpeedMultiplier = 1.0;

        private static Effect _measureEffect;
        public static Effect MeasureEffect
        {
            get
            {
                if (_measureEffect == null)
                {
                    _measureEffect = new Effect("Multimeter.Measure", Resource.Raw._37847_infiniteauubergine);
                }
                return _measureEffect;
            }
        }

        //public Toolkit(string tagID = null, Function curFunc = Function.Examine) : base(tagID)
        public Toolkit(string tagID = null) : base(tagID)
        {
            //currentFunction = curFunc;
        }

        public override string ToString()
        {
            //return $"Toolkit|{currentFunction}";
            return $"Toolkit|{ExamineSpeedMultiplier}|{MultimeterSpeedMultiplier}|{WireCutterSpeedMultiplier}|{SolderingSpeedMultiplier}";
        }
        public static Toolkit FromString(string tkString, string tagID)
        {
            var tk = tkString.Split('|');
            //return new Toolkit(tagID, (Function)Enum.Parse(typeof(Function), tk[1]));
            return new Toolkit(tagID)
            {
                ExamineSpeedMultiplier = double.Parse(tk[1]),
                MultimeterSpeedMultiplier = double.Parse(tk[2]),
                WireCutterSpeedMultiplier = double.Parse(tk[3]),
                SolderingSpeedMultiplier = double.Parse(tk[4])
            };
        }
    }

    public class MemorylessToolkit : Toolkit
    {
        public MemorylessToolkit(string TagID = null) : base(TagID) { }

        public override void SaveSpecifics()
        {
            // Do nothing!  This type of Sword is never saved to the file.
        }
    }
}