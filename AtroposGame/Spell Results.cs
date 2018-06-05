
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Numerics;
using Android.Util;
using MiscUtil;
using Nito.AsyncEx;
using Atropos.Characters;

namespace Atropos
{
    public class SpellCaster
    {
        public const string EFFICACY = "SpellEfficacy";
        public static SpellCaster Me = new SpellCaster();

        public double BaseEfficacy = 1.0;
        public Dictionary<string, double> EfficacyMultipliers = new Dictionary<string, double>();
        public double SpellEfficacy
        {
            get
            {
                var eff = BaseEfficacy;
                foreach (var amt in EfficacyMultipliers.Values) eff *= amt;
                return eff;
            }
        }

        public double BaseEaseOfCasting = 1.0;
        public Dictionary<string, double> EaseMultipliers = new Dictionary<string, double>();
        public double EaseOfCasting
        {
            get 
            {
                var ease = BaseEaseOfCasting;
                foreach (var amt in EaseMultipliers.Values) ease *= amt;
                return ease;
            }
        }

        // The primary factors in EaseOfCasting, aside from static factors affecting the base ease, are Daze effects.
        public string AddDaze(double amount) // Use this version when multiple instances from the same source (e.g. Paralysis) are allowed to stack / overlap
        {
            var key = Guid.NewGuid().ToString();
            AddDaze(key, amount);
            return key;
        }
        public void AddDaze(string key, double amount) // Use this version when you know the name and want to override rather than stack
        {
            if (key != "Clarity" && EaseMultipliers.ContainsKey("Clarity")) return; // Blocked by Clarity effect.
            EaseMultipliers[key] = 1.0 / amount;
        }
        public void RemoveDaze(string key)
        {
            if (EaseMultipliers.ContainsKey(key)) EaseMultipliers.Remove(key);
        }
    }

    public abstract class GameEffect
    {
        public abstract string Name { get; }
        public abstract Task OnGenerate(object data);
        public abstract Task OnReceiving(Communications.CommsContact source, object data);

        public GameEffect()
        {
            //AllEffects.Add(this);
        }

        public static List<GameEffect> AllEffects = new List<GameEffect>();
        public static Dictionary<string, GameEffect> Lookup
        {
            get { return AllEffects.ToDictionary(geff => geff.Name); }
        }
    }

    public class SpellResult : GameEffect
    {
        public const string SpellPrefix = "Spell.";
        public string SpellName;
        public override string Name { get => SpellPrefix + SpellName; }
        public List<Glyph> Glyphs;

        public Glyph Magnitude { get => Glyphs.ElementAtOrDefault(0); }
        public Glyph SpellType { get => Glyphs.ElementAtOrDefault(1); }
        public Glyph KeyGlyph { get => Glyphs.ElementAtOrDefault(2); }

        public int CastSFXresourceID;
        public string CastSFXname;
        public virtual Effect CastSFX
        {
            get
            {
                if (CastSFXresourceID > 0) return new Effect(SpellName + ".SFX", CastSFXresourceID);
                else return MasterSpellLibrary.SpellSFX.GetValueOr(CastSFXname, Effect.None) as Effect;
            }
        }
        public SoundOptions CastSFXSoundOptions = SoundOptions.OnSpeakers;

        //public static event EventHandler<EventArgs<object>> UponCasting;
        protected Func<object, Task> onCast;
        public async void OnCast(object data)
        {
            CastSFX.Play(CastSFXSoundOptions);
            Log.Debug(_tag, $"Successfully cast {SpellName}.");
            await Task.Run(() => 
            {
                //UponCasting.Raise(data);
                onCast?.Invoke(data);
            });
        }
        public override async Task OnGenerate(object data)
        {
            CastSFX.Play(CastSFXSoundOptions);
            Log.Debug(_tag, $"Successfully cast {SpellName}.");
            //UponCasting.Raise(data);
            await onCast?.Invoke(data);
        }

        //public event EventHandler<EventArgs<object>> UponReceiving;
        protected Func<Communications.CommsContact, object, Task> onHitBy;
        //public async void OnHitBy(Communications.CommsContact source, object data)
        //{
        //    var recipientSoundOptions = CastSFXSoundOptions;
        //    recipientSoundOptions.UseSpeakers = false; // No need for us both to put it up on speakers, but target might not be in a position to hear the speakers from the caster.
        //    CastSFX.Play(recipientSoundOptions);

        //    await Task.Run(() => { onHitBy?.Invoke(source, data); });
        //}
        public override async Task OnReceiving(Communications.CommsContact source, object data)
        {
            var recipientSoundOptions = CastSFXSoundOptions;
            recipientSoundOptions.UseSpeakers = false; // No need for us both to put it up on speakers, but target might not be in a position to hear the speakers from the caster.
            CastSFX.Play(recipientSoundOptions);
            Log.Debug(_tag, $"Being affected by {SpellName}.");

            await onHitBy?.Invoke(source, data);
        }

        public async Task SendDamage(DamageType type, double baseMagnitude, double toBarrier = 1.0)
        {
            var damage = new Damage() { Type = type, Magnitude = baseMagnitude * Res.GetRandomCoefficient(0.25) * SpellCaster.Me.SpellEfficacy, ToBarriers = toBarrier };
            await SendEffect(Communications.Bluetooth.BluetoothMessageCenter.TemporaryAddressBook_SingleEntry, damage.ToString());
        }

        public async Task SendEffect(Communications.CommsContact target = null, object data = null)
        {
            target = target ?? Atropos.Communications.Bluetooth.BluetoothMessageCenter.TemporaryAddressBook_SingleEntry ?? Communications.CommsContact.Nobody;

            if (onHitBy == null)
            {
                Log.Warn(_tag, $"Attempted to send the OnHitBy effect of {SpellName}... which doesn't have one... to {target.Name}.");
                return;
            }
            if (target == Communications.CommsContact.Nobody && !Res.SolipsismMode)
            {
                Log.Warn(_tag, $"Attempted to send the OnHitBy effect of {SpellName} to... nobody.  Okay... done.");
                return;
            }

            string addedData = "";
            if (data is string dstr) addedData = dstr;
            else if (data != null)
            {
                try
                {
                    addedData = Serializer.Serialize(data);
                }
                catch
                {
                    addedData = "";
                }
            }

            var message = new Communications.Message(Communications.MsgType.PushEffect, Name + Communications.Bluetooth.BluetoothCore.NEXT + addedData);
            if (!Res.SolipsismMode)
                target.SendMessage(message);
            else
                Communications.Bluetooth.BluetoothMessageCenter.ActOnMessage(message);

            await Task.CompletedTask;
        }

        public static Task SufferDamage(DamageType type, double baseMagnitude, double toBarrier = 1.0)
        {
            return SufferDamage(new Damage() { Type = type, Magnitude = Res.GetRandomCoefficient(0.25) * baseMagnitude, ToBarriers = toBarrier });
        }
        public static Task SufferDamage(object damageStrObj)
        {
            if (!(damageStrObj is string damageStr)) throw new ArgumentException();
            return SufferDamage(Damage.FromString(damageStr));
        }
        public static async Task SufferDamage(Damage damage)
        {
            Damageable.Me.Suffer(damage);
            await Task.CompletedTask;
        }

        // Helper booleans for keeping track of the spell's effects.  TEMPORARY.  Probably.
        public bool IsInEffectAsCaster = false;
        public bool IsInEffectAsTarget = false;

        private const string _tag = "SpellResults";

        public SpellResult(string name, params Glyph[] glyphs) : base()
        {
            SpellName = name;
            Glyphs = glyphs.ToList();
            AllEffects.Add(this);
        }

        //protected static 

        //public static SpellResult SonarDemo = new SpellResult("Nonworking_sonar_demo")
        //{
        //    onCast = async (o) =>
        //    {
        //        var m = MasterSpellLibrary.SpellSFX["Magic.SonarAlert"] as Effect;
        //        await m.PlayFromTo(20, 38.5);
        //    }
        //};

        public static SpellResult Dart = new SpellResult("Dart", Glyph.L, Glyph.A, Glyph.P)
        {
            CastSFXresourceID = Resource.Raw.fzazzle_magic,
            CastSFXname = "Magic.Fzazzle",
            onCast = async (o) => await Dart.SendDamage(DamageType.Piercing, 20),
            onHitBy = async (caster, o) => await SufferDamage(o)
        };

        public static SpellResult Zap = new SpellResult("Zap", Glyph.L, Glyph.A, Glyph.Z, Glyph.P)
        {
            CastSFXresourceID = Resource.Raw._136542_electricZap_small,
            CastSFXname = "Magic.ElectricZapSmall",
            onCast = async (o) => await Zap.SendDamage(DamageType.Lightning, 25, 0.75),
            //{
            //    Zap.SendEffect();
            //    //if (opponent != null)
            //    //{
                    
            //    //    //opponent.SendMessage(Communications.MsgType, "You have been zapped.");
            //    //    Log.Debug(_tag, $"Sent 'You have been zapped' to {opponent.Name}.");
            //    //}
            //    //else
            //    //    Log.Debug(_tag, "Unable to send zapped message; no opponent available.");
            //},
            onHitBy = async (caster, o) =>
            {
                //if (!Shield.IsInEffectAsCaster) Speech.Say("You have been zapped.");
                //else Speech.Say("Your shield blocked it!");
                await SufferDamage(o);

                if (Damageable.Me.Shielded) return;
                Speech.Say("Also, you are slightly dazed for a few seconds.");

                var dazeKey = SpellCaster.Me.AddDaze(1.1);
                await Task.Delay(10000);
                SpellCaster.Me.RemoveDaze(dazeKey);
            }
        };

        public static DateTime LastBarrierCasting = DateTime.Now - TimeSpan.FromSeconds(180);
        private static TimeSpan MinBarrierInterval = TimeSpan.FromSeconds(15);
        public static double BarrierBaseMagnitude = 80.0;
        public static SpellResult Barrier = new SpellResult("Barrier", Glyph.L, Glyph.D, Glyph.S)
        {
            CastSFXresourceID = Resource.Raw.ring_buzz_magic,
            CastSFXname = "Magic.RingBuzz",
            onCast = async (o) =>
            {
                var x = (DateTime.Now - LastBarrierCasting).TotalSeconds / MinBarrierInterval.TotalSeconds;
                if (x < 1)
                {
                    Speech.Say("Too soon; unable to erect new barrier");
                    LastBarrierCasting = DateTime.Now;
                    return;
                }
                Speech.Say("Barrier raised.");
                var y = (0.66 * Math.Acos(1.0 / x)); // Just above 0 at x just above 1, then about 70% at x = 2, 80% at x = 3, 85% at x = 4, etc.
                var barrierAmount = BarrierBaseMagnitude * SpellCaster.Me.SpellEfficacy * y;
                Damageable.Me.Barrier = Math.Max(Damageable.Me.Barrier, barrierAmount);
                LastBarrierCasting = DateTime.Now + MinBarrierInterval.MultipliedBy(1 - y);
            }
        };

        public static SpellResult Shield = new SpellResult("Shield", Glyph.M, Glyph.D, Glyph.X, Glyph.P)
        {
            CastSFXresourceID = Resource.Raw.kblaa_magic,
            CastSFXname = "Magic.Kblaa",
            onCast = async (o) =>
            {
                // Wait to make sure their hand is raised
                var handVerticalStage = new ConditionStage<Vector3>(new Vector3Provider(Android.Hardware.SensorType.Gravity), (v) => v.Dot(Vector3.UnitY) > 0.75 * 9.81);
                await handVerticalStage.ConditionMet();
                Speech.Say("Shield summoned.");
                Damageable.Me.Shielded = true;
                Log.Debug(_tag, $"Initiating shield spell effect...");

                // Wait (with SFX) until their hand is lowered.
                var handNotVerticalStage = new ConditionStage<Vector3>(new Vector3Provider(Android.Hardware.SensorType.Gravity), (v) => v.Dot(Vector3.UnitY) < 0.75 * 9.81);
                new Effect("ShieldSFX", Resource.Raw._49685_nanoblade_loop).Play(new SoundOptions() //MasterSpellLibrary.SpellSFX["Magic.NanobladeLoop"].Play(new SoundOptions()
                {
                    Looping = true,
                    UseSpeakers = true,
                    CancelToken = handNotVerticalStage.AwaitableToken
                });
                await handNotVerticalStage.ConditionMet();
                Damageable.Me.Shielded = false;
                Log.Debug(_tag, $"Ending shield spell effect...");
            }
        };

        public static SpellResult Lance = new SpellResult("Lance", Glyph.M, Glyph.A, Glyph.R, Glyph.P)
        {
            CastSFXresourceID = Resource.Raw._366093_kwongg,
            CastSFXname = "Magic.DenseMetallicBoom",
            onCast = async (o) => await Lance.SendDamage(DamageType.Impact, 50),
            onHitBy = async (caster, o) => await SufferDamage(o)
        };

        //public static SpellResult Frost = new SpellResult("Frost", Glyph.M, Glyph.A, Glyph.I, Glyph.P)
        //{
        //    CastSFXresourceID = Resource.Raw._346916_fwissh,
        //    CastSFXname = "Magic.Fwissh",
        //    onCast = async (o) => await Frost.SendEffect(data: $"{SpellCaster.EFFICACY}:{SpellCaster.Me.SpellEfficacy:f2}"),
        //    onHitBy = async (caster, o) =>
        //    {
        //        if (Damageable.Me.Shielded)
        //        {
        //            await SufferDamage(DamageType.Ice, 10); // Magnitude doesn't matter, we just want to trigger the shield reports as if it were an ordinary instance of damage.
        //            return;
        //        }

        //        if (!(o is string oStr) || !oStr.StartsWith(SpellCaster.EFFICACY)) throw new ArgumentException();
        //        var efficacy = double.Parse(oStr.Split(':')[1]);

        //        var galewinds = new Effect("Galewinds", Resource.Raw._377068_galewinds); // MasterSpellLibrary.SpellSFX["Magic.Galewinds"] as Effect;
        //        galewinds.SpeakerMode = true;
        //        galewinds.PlayDiminuendo(TimeSpan.FromMilliseconds(60), 0.65);

        //        var accelProvider = new Vector3Provider(Android.Hardware.SensorType.LinearAcceleration);
        //        while (galewinds.WhenFinishedPlaying.Status.IsOneOf(TaskStatus.WaitingForActivation, TaskStatus.Running))
        //        {
        //            var cutoffAccel = (3.5 - 3 * galewinds.Volume) / efficacy;
        //            var movedTooFast = new ConditionStage<Vector3>(accelProvider, 
        //                (v, t) =>
        //                {
        //                    return v.Length() > cutoffAccel && t > TimeSpan.FromMilliseconds(300);
        //                },
        //                (v, t) => t > TimeSpan.FromMilliseconds(750),
        //                (v, t) =>
        //                {
        //                    Log.Debug(_tag, $"Frost interim: accel is {v.Length():f2}, vs cutoff of {cutoffAccel:f2}.");
        //                });
        //            await movedTooFast.ConditionMet()
        //                           .ContinueWith(
        //                                    _ =>
        //                                    {
        //                                        SufferDamage(DamageType.Ice, 5 * efficacy * (1 + accelProvider.Data.Length() + galewinds.Volume), 0.5);
        //                                        Plugin.Vibrate.CrossVibrate.Current.Vibration(15);
        //                                    }, 
        //                                    TaskContinuationOptions.OnlyOnRanToCompletion
        //                                    );
        //        }
        //    }
        //};

        public static SpellResult Frost = new SpellResult("Frost", Glyph.M, Glyph.A, Glyph.I, Glyph.P)
        {
            CastSFXresourceID = Resource.Raw._346916_fwissh,
            CastSFXname = "Magic.Fwissh",
            onCast = async (o) => await Frost.SendEffect(data: $"{SpellCaster.EFFICACY}:{SpellCaster.Me.SpellEfficacy:f2}"),
            onHitBy = async (caster, o) =>
            {
                if (Damageable.Me.Shielded)
                {
                    await SufferDamage(DamageType.Ice, 10); // Magnitude doesn't matter, we just want to trigger the shield reports as if it were an ordinary instance of damage.
                    return;
                }

                if (!(o is string oStr) || !oStr.StartsWith(SpellCaster.EFFICACY)) throw new ArgumentException();
                var efficacy = double.Parse(oStr.Split(':', ';')[1]);

                var galewinds = new Effect("Galewinds", Resource.Raw._377068_galewinds); // MasterSpellLibrary.SpellSFX["Magic.Galewinds"] as Effect;
                galewinds.SpeakerMode = true;
                galewinds.Play(new SoundOptions() { UseSpeakers = true, Looping = true, Volume = 0.5 });

                //galewinds.PlayDiminuendo(TimeSpan.FromMilliseconds(60), 0.65);

                //var stillnessProvider = new SmoothLoggingProvider<float>(new StillnessProvider(), 10);
                var accelProvider = new Vector3Provider(Android.Hardware.SensorType.LinearAcceleration);
                var averageAccel = new RollingAverage<float>(10);
                var galeVolume = new RollingAverage<float>(10, 0.5f);

                var duration = TimeSpan.FromSeconds(30 * efficacy);

                double accumulatedDamage = 0, damageThreshold = 10;
                Task speakingTask = Speech.SayAllOf("You're frozen and slowed.  Keep your movements gradual.", SoundOptions.AtSpeed(1.5));
                var movedHowFast = new ConditionStage<Vector3>(accelProvider,
                    (f, t) =>
                    {
                        //var cutoffScore = -15; // + stillnessProvider.RunTime.TotalSeconds;
                        ////return f < cutoffScore && t > TimeSpan.FromMilliseconds(1250);
                        //averageAccel.Update(f);
                        //var result = (averageAccel.Average < cutoffScore && t > TimeSpan.FromMilliseconds(1250));
                        //if (result) Log.Debug(_tag, $"Current stillness average {averageAccel.Average:f2} after {t.TotalMilliseconds:f1} ms.");
                        //return result;
                        averageAccel.Update(f.Length());
                        return (accelProvider.RunTime > duration);
                    },
                    (f, t) => false,
                    async (f, t) =>
                    {
                        var limitAccel = 0.35 * (1.0 + 0.05 * accelProvider.RunTime.TotalSeconds) / efficacy;
                        
                        Log.Debug(_tag, $"Frost progress - accel is {f.Length():f2}, average is {averageAccel.Average:f2}, wants to be less than {limitAccel:f2}.");
                        
                        var fierceness = 3 * (0.35 + Math.Exp(-limitAccel/f.Length())); // Range is from (approx) 1 to 4
                        galeVolume.Update((float)fierceness / 4);
                        galewinds.Volume = galeVolume;

                        if (f.Length() < limitAccel) // You're safe then.
                        {
                            return;
                        }

                        Log.Debug(_tag, $"Frost damage - fierceness is {fierceness:f2}, barrier is {Damageable.Me.Barrier:f2}, accumulated damage is {accumulatedDamage:f2}.");

                        var imColdStrings = new string[] { "Burr", "Birr", "Ow", "Cold!", "Freezing", "Crack snap" };
                        if (Res.Random < 0.15 && speakingTask.IsCompleted) speakingTask = Speech.SayAllOf($"{imColdStrings.GetRandom()}");

                        if (Damageable.Me.Barrier > 0)
                        {
                            if (Damageable.Me.Barrier > fierceness)
                            {
                                Damageable.Me.Barrier -= fierceness;
                            }
                            else
                            {
                                accumulatedDamage += fierceness - Damageable.Me.Barrier;
                                Damageable.Me.Barrier = 0;
                                speakingTask = Speech.SayAllOf("Barrier down. Cold cold cold cold cold.");
                            }
                        }
                        else
                        {
                            accumulatedDamage += fierceness;
                            if (accumulatedDamage > damageThreshold && Res.Random < 0.5 && speakingTask.IsCompleted)
                            {
                                var dmg = new Damage() { Type = DamageType.Ice, Magnitude = accumulatedDamage, ShieldPiercing = true };

                                if (damageThreshold == 10)
                                {
                                    Damageable.Me.Suffer(dmg, raiseEvents: false);
                                }
                                else // Subsequent frostburns just worsen the previous one
                                {
                                    var impact = Damageable.Me.DamageSuffered.Last(i => i.IncurringHit.Type == DamageType.Ice);
                                    Damageable.Me.DamageSuffered.Remove(impact);
                                    Damageable.Me.Suffer(dmg, impact.Location, false);
                                }
                                damageThreshold = accumulatedDamage + 10;

                                var impct = Damageable.Me.DamageSuffered.Last(i => i.IncurringHit.Type == DamageType.Ice);
                                speakingTask = Speech.SayAllOf(impct.Description);
                            }
                        }
                        Plugin.Vibrate.CrossVibrate.Current.Vibration(Res.Random * 15);
                    });
                movedHowFast.InterimInterval = TimeSpan.FromMilliseconds(250);
                await movedHowFast.ConditionMet();
                await Task.Delay(200);
                if (accumulatedDamage > 0)
                {
                    var dmg = new Damage() { Type = DamageType.Ice, Magnitude = accumulatedDamage, ShieldPiercing = true };
                    var impact = Damageable.Me.DamageSuffered.Last(i => i.IncurringHit.Type == DamageType.Ice);
                    Damageable.Me.DamageSuffered.Remove(impact);
                    Damageable.Me.Suffer(dmg, impact.Location, false);

                    impact = Damageable.Me.DamageSuffered.Last(i => i.IncurringHit.Type == DamageType.Ice);
                    await speakingTask;
                    Speech.Say(impact.FullDescription);
                }
                galewinds.Stop();
            }
        };

        public static SpellResult Flame = new SpellResult("Flame", Glyph.H, Glyph.A, Glyph.F, Glyph.P)
        {
            CastSFXresourceID = Resource.Raw._248116_fwehshh,
            CastSFXname = "Magic.Fwehshh",
            onCast = async (o) => await Flame.SendEffect(data: $"{SpellCaster.EFFICACY}:{SpellCaster.Me.SpellEfficacy:f2}"),
            onHitBy = async (caster, o) =>
            {
                if (Damageable.Me.Shielded)
                {
                    await SufferDamage(DamageType.Fire, 10); // Magnitude doesn't matter, we just want to trigger the shield reports as if it were an ordinary instance of damage.
                    return;
                }

                if (!(o is string oStr) || !oStr.StartsWith(SpellCaster.EFFICACY)) throw new ArgumentException();
                var efficacy = double.Parse(oStr.Split(':',';')[1]);

                var burning = new Effect("BurningSFX", Resource.Raw._347706_fire_loop); // MasterSpellLibrary.SpellSFX["Magic.FireLoop"] as Effect;
                burning.SpeakerMode = true;
                burning.Play(new SoundOptions() { UseSpeakers = true, Looping = true, Volume = 0.5 });

                //var stillnessProvider = new SmoothLoggingProvider<float>(new StillnessProvider(), 10);
                var stillnessProvider = new StillnessProvider();
                var averageStillness = new RollingAverage<float>(30);

                double accumulatedDamage = 0, damageThreshold = 10;
                Task speakingTask = Speech.SayAllOf("You're on fire! Shake vigorously to put it out.");
                var movedHowFast = new ConditionStage<float>(stillnessProvider,
                    (f, t) =>
                    {
                        var cutoffScore = -15; // + stillnessProvider.RunTime.TotalSeconds;
                        //return f < cutoffScore && t > TimeSpan.FromMilliseconds(1250);
                        averageStillness.Update(f);
                        var result = ( averageStillness.Average < cutoffScore && t > TimeSpan.FromMilliseconds(1250));
                        if (result) Log.Debug(_tag, $"Current stillness average {averageStillness.Average:f2} after {t.TotalMilliseconds:f1} ms.");
                        return result;
                    },
                    (f, t) => false,
                    async (f, t) =>
                    {
                        var fierceness = ((f >= 0) ? 1.0 : (-19 + stillnessProvider.RunTime.TotalSeconds) / f) * efficacy;
                        burning.Volume = 0.5 * fierceness;
                        Log.Debug(_tag, $"Fire progress - stillness is {f:f2}, average is {averageStillness.Average:f2}, wants to be less than {(-19 + stillnessProvider.RunTime.TotalSeconds):f2}.");
                        //SufferDamage(DamageType.Fire, 10 * fierceness * efficacy, 0.5);

                        var imOnFireStrings = new string[] { "Ow", "Ow", "Ouch", "Burning!", "On fire", "Yipe" };
                        if (Res.Random < 0.15 && speakingTask.IsCompleted) speakingTask = Speech.SayAllOf($"{imOnFireStrings.GetRandom()}");

                        if (Damageable.Me.Barrier > 0)
                        {
                            if (Damageable.Me.Barrier > fierceness / 3)
                            {
                                Damageable.Me.Barrier -= fierceness / 3;
                            }
                            else
                            {
                                accumulatedDamage += fierceness / 3 - Damageable.Me.Barrier;
                                Damageable.Me.Barrier = 0;
                                speakingTask = Speech.SayAllOf("Barrier down. Hot, very hot.");
                            }
                        }
                        else
                        {
                            accumulatedDamage += fierceness / 3;
                            if (accumulatedDamage > damageThreshold && Res.Random < 0.2 && speakingTask.IsCompleted)
                            {
                                var dmg = new Damage() { Type = DamageType.Fire, Magnitude = accumulatedDamage, ShieldPiercing = true };

                                if (damageThreshold == 10)
                                {
                                    Damageable.Me.Suffer(dmg, raiseEvents: false);
                                }
                                else // Subsequent burns just worsen the previous one
                                {
                                    var impact = Damageable.Me.DamageSuffered.Last(i => i.IncurringHit.Type == DamageType.Fire);
                                    Damageable.Me.DamageSuffered.Remove(impact);
                                    Damageable.Me.Suffer(dmg, impact.Location, false);
                                }
                                damageThreshold = accumulatedDamage + 10;

                                var impct = Damageable.Me.DamageSuffered.Last(i => i.IncurringHit.Type == DamageType.Fire);
                                speakingTask = Speech.SayAllOf(impct.Description);
                            }
                        }
                        Plugin.Vibrate.CrossVibrate.Current.Vibration(Res.Random * 15);
                    });
                movedHowFast.InterimInterval = TimeSpan.FromMilliseconds(250);
                await movedHowFast.ConditionMet();
                if (accumulatedDamage > 0)
                {
                    var dmg = new Damage() { Type = DamageType.Fire, Magnitude = accumulatedDamage, ShieldPiercing = true };
                    var impact = Damageable.Me.DamageSuffered.Last(i => i.IncurringHit.Type == DamageType.Fire);
                    Damageable.Me.DamageSuffered.Remove(impact);
                    Damageable.Me.Suffer(dmg, impact.Location, false);

                    impact = Damageable.Me.DamageSuffered.Last(i => i.IncurringHit.Type == DamageType.Fire);
                    await speakingTask;
                    Speech.Say(impact.FullDescription);
                }
                burning.Stop();
            }
        };

        public static SpellResult Clarity = new SpellResult("Clarity", Glyph.L, Glyph.C, Glyph.X, Glyph.K)
        {
            CastSFXresourceID = Resource.Raw.tinkly_magic,
            CastSFXname = "Magic.Tinkly",
            onCast = async (o) =>
            {
                foreach (var key in SpellCaster.Me.EaseMultipliers.Keys)
                    SpellCaster.Me.RemoveDaze(key);
                Speech.Say("All daze effects removed.  Clarity of thought returns.");
                SpellCaster.Me.AddDaze("Clarity", 1.0 - 0.2 * SpellCaster.Me.SpellEfficacy);
                var claritySound = new Effect("Spell.Clarity", Resource.Raw._316626_strongerThanTheDark_needsTrim);
                claritySound.Play(0.25, true, false);
                await Task.Delay(20000 + (int)(25000 * SpellCaster.Me.SpellEfficacy));
                claritySound.Stop();
                SpellCaster.Me.RemoveDaze("Clarity");
            }
        };

        public static SpellResult Dazzle = new SpellResult("Dazzle", Glyph.M, Glyph.C, Glyph.X, Glyph.P)
        {
            CastSFXresourceID = Resource.Raw._234804_transmat,
            CastSFXname = "Magic.Transmat",
            onCast = async (o) => await Dazzle.SendEffect(data: $"{SpellCaster.EFFICACY}:{SpellCaster.Me.SpellEfficacy:f2}"),
            onHitBy = async (caster, o) =>
            {
                if (Damageable.Me.Shielded) { await SufferDamage(DamageType.Lightning, 0); return; }
                var dazzleStrings = new string[] { "Bright lights! Bright lights!", "Dazzling lights make you blink", "You're dazzled by flashing sparks" };
                Speech.Say(dazzleStrings.GetRandom(), SoundOptions.AtSpeed(1.5));

                if (!(o is string oStr) || !oStr.StartsWith(SpellCaster.EFFICACY)) throw new ArgumentException();
                var efficacy = double.Parse(oStr.Split(':')[1]);

                var dazeKey = SpellCaster.Me.AddDaze(1.0 + 0.2 * efficacy);
                await Task.Delay(5000 + (int)(5000 * efficacy));
                SpellCaster.Me.RemoveDaze(dazeKey);
            }
        };

        public static SpellResult Paralyze = new SpellResult("Paralyze", Glyph.H, Glyph.C, Glyph.N, Glyph.I, Glyph.B)
        {
            CastSFXresourceID = Resource.Raw._366092_zoom_slowing_down,
            CastSFXname = "Magic.ZoomSlowingDown",
            onCast = async (o) => await Paralyze.SendEffect(data: $"{SpellCaster.EFFICACY}:{SpellCaster.Me.SpellEfficacy:f2}"),
            onHitBy = async (caster, o) =>
            {
                if (Damageable.Me.Shielded)
                {
                    await SufferDamage(DamageType.Ice, 10); // Magnitude doesn't matter, we just want to trigger the shield reports as if it were an ordinary instance of damage.
                    return;
                }

                if (!(o is string oStr) || !oStr.StartsWith(SpellCaster.EFFICACY)) throw new ArgumentException();
                var efficacy = double.Parse(oStr.Split(':')[1]);

                var paralysisSound = new Effect("ParalysisSFX", Resource.Raw._371887_gritty_drone); //MasterSpellLibrary.SpellSFX["Magic.GrittyDrone"] as Effect;
                paralysisSound.SpeakerMode = true;
                paralysisSound.Play(new SoundOptions() { UseSpeakers = false, Looping = true, Volume = 0.3 });

                Speech.Say("You're paralyzed.  The stiller you can stay, the sooner it will wear off.", SoundOptions.AtSpeed(1.5));

                var innerProvider = new StillnessProvider();
                innerProvider.Jostle(20);
                var stillnessProvider = new SmoothLoggingProvider<float>(innerProvider, 15);

                var secondsTimeBase = 20 / efficacy;

                SpellCaster.Me.AddDaze("Paralysis", 100.0);
                var movedHowFast = new ConditionStage<float>(stillnessProvider,
                    (f, t) =>
                    {
                        var cutoffScore = 17 / efficacy - Math.Pow(stillnessProvider.RunTime.TotalSeconds / secondsTimeBase, 2);
                        return f > cutoffScore && t > TimeSpan.FromMilliseconds(2500);
                    });
                await movedHowFast.ConditionMet();
                SpellCaster.Me.RemoveDaze("Paralysis");
                paralysisSound.Stop();
            }
        };

        public static List<SpellResult> AllSpells
        {
            get => GameEffect
                    .AllEffects
                    .Where(geff => geff.Name.StartsWith(SpellPrefix))
                    .Select(geff => geff as SpellResult)
                    .ToList();
        }
    }
}