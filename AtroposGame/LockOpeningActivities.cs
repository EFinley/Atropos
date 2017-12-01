
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
//using Accord.Math;
//using Accord.Statistics;
using Android.Content;
using System.Threading.Tasks;
using System.Numerics;
using Vector3 = System.Numerics.Vector3;
using Android.Views;
using Android.Hardware;

using static System.Math;
using Log = Android.Util.Log; // Disambiguating with Math.Log( )... heh!
using Nito.AsyncEx;
using System.Threading;
using Android.Graphics;

namespace Atropos.Locks

{
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class LockedObjectOpeningActivity : BargraphDisplayActivity
    {
        protected Toolkit ThePlayersToolkit;
        protected Lock LockBeingOpened;
        private static LockedObjectOpeningActivity Current { get { return (LockedObjectOpeningActivity)CurrentActivity; } set { CurrentActivity = value; } }
        protected Effect WhistleFX;
        //protected BargraphData StillnessDisplay;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // See if the current kit is already in our (local, for now) library, and load it if so.
            var tkString = Res.SpecificTags.Get(InteractionLibrary.CurrentSpecificTag);
            if (tkString != null)
            {
                ThePlayersToolkit = Toolkit.FromString(tkString, InteractionLibrary.CurrentSpecificTag);
            }
            else if (InteractionLibrary.CurrentSpecificTag == null
                    || (InteractionLibrary.CurrentSpecificTag.StartsWith("kit_")
                    && InteractionLibrary.CurrentSpecificTag.EndsWith("0000"))) // I.E. this is a "launch directly" order.
            {
                ThePlayersToolkit = new MemorylessToolkit();
            }
            else
            {
                ThePlayersToolkit = new Toolkit(InteractionLibrary.CurrentSpecificTag);
            }
        }

        protected override void OnResume()
        {
            WhistleFX = new Effect("Whistle", Resource.Raw._98195_whistling);
            //StillnessDisplay = new BargraphData(this, "Steadiness");

            base.DoOnResume(async () =>
            {
                await System.Threading.Tasks.Task.Delay(150);
                WhistleFX.PlayDiminuendo(TimeSpan.FromMilliseconds(750));
                //await System.Threading.Tasks.Task.Delay(500);
                //await Speech.SayAllOf("Cracking open the toolkit.  Select a tool onscreen.  Note, sometimes you'll get further information from repeating a measurement.");
            });
        }

        protected override void OnPause()
        {
            WhistleFX.Deactivate();
            base.OnPause();
        }

        protected void RelayMessage(int ResourceID, string message)
        {
            var v = FindViewById<TextView>(ResourceID);
            if (v == null)
            {
                Log.Error("LockOpeningActivity:RelayMessage", $"Unable to find field {ResourceID} so as to pass message << {message} >>");
                return;
            }
            RunOnUiThread(() => 
            {
                v.Text = message;
                v.Visibility = ViewStates.Visible;
            });
        }

        public class BeginOpeningProcessStage : GestureRecognizerStage
        {
            private Toolkit Kit;
            private StillnessProvider Stillness;
            //private ConsistentAxisAngleProvider Gravity;

            public BeginOpeningProcessStage(string label, Toolkit kit, bool AutoStart = true) : base(label)
            {
                Kit = kit;
                Stillness = new StillnessProvider();
                SetUpProvider(Stillness, allowProviderToPersist: true);
                //Gravity = new ConsistentAxisAngleProvider();
                //Gravity.Activate();

                Current.FindViewById(Resource.Id.vault_notification).Visibility = ViewStates.Visible;
                Current.FindViewById(Resource.Id.vault_dial_text).Visibility = ViewStates.Gone;

                if (AutoStart) Activate();
            }

            protected override bool interimCriterion()
            {
                //return Stillness.IsItDisplayUpdateTime();
                return true;
            }

            protected override void interimAction()
            {
                //Stillness.DoDisplayUpdate();
                //Current.StillnessDisplay.Update(Stillness.StillnessScore); // + 20f);
                Current.RelayMessage(Resource.Id.bargraph_text1, $"Steadiness score: {Stillness.StillnessScore,-10:f3}");
            }

            protected override bool nextStageCriterion()
            {
                return (Stillness.ReadsMoreThan(2f));
                    //&& Math.Abs(Vector3.Dot(Gravity.Vector, Vector3.UnitY)) < 0.5);
            }
            protected override async void nextStageAction()
            {
                var newProvider = new ConsistentAxisAngleProvider();
                newProvider.Activate();

                //Current.StillnessDisplay?.Dispose();
                Current.RelayMessage(Resource.Id.bargraph_text1, "Beginning...");

                await newProvider.SetFrameShiftFromCurrent();
                await Speech.SayAllOf("Begin");
                Current.BeginFirstTumbler(newProvider); // Because the specific next step depends on the kind of lock, unlike everything prior to this point.
            }
        }

        protected virtual void BeginFirstTumbler(ConsistentAxisAngleProvider oProvider)
        {
            throw new NotImplementedException();
        }
        
        public class TumblerFindingStage : GestureRecognizerStage
        {
            
            protected Toolkit Kit;
            protected Tumbler targetTumbler;
            protected ConsistentAxisAngleProvider AttitudeProvider;

            protected double LastAngle, Angle, TargetAngle, StartAngle, AngleDelta;
            protected int Direction;

            //protected System.Diagnostics.Stopwatch Stopwatch;
            //protected TimeSpan LastInterval = TimeSpan.Zero;

            protected double minDotProductWithSetAxis, maxDotProductWithGravity;

            public TumblerFindingStage(string label, Toolkit toolkit, Tumbler tgtTumbler, ConsistentAxisAngleProvider oProvider = null) : base(label)
            {
                Kit = toolkit;
                targetTumbler = tgtTumbler;

                AttitudeProvider = oProvider ?? new ConsistentAxisAngleProvider();
                SetUpProvider(AttitudeProvider, allowProviderToPersist: true);

                minDotProductWithSetAxis = Math.Cos(Current.LockBeingOpened.OffAxisMaxDuringFindingPhase * QuaternionExtensions.degToRad);
                maxDotProductWithGravity = Math.Sin(Current.LockBeingOpened.OffAxisMaxDuringFindingPhase * QuaternionExtensions.degToRad);

                LastAngle = Angle = StartAngle = AttitudeProvider.AngleSmoothed;
                AngleDelta = 0.0;
                TargetAngle = tgtTumbler.Angle;
                Direction = tgtTumbler.Direction;

                //Stopwatch = new System.Diagnostics.Stopwatch();
                //Stopwatch.Start();

                InterimInterval = TimeSpan.FromMilliseconds(50);

                Activate();
            }

            protected override bool interimCriterion()
            {
                return true;
            }
            protected override void interimAction()
            {
                LastAngle = Angle;
                Angle = AttitudeProvider.AngleSmoothed;
                var angleChange = (Angle - LastAngle).ClampPlusMinus180();
                AngleDelta += angleChange; // AngleDelta itself *could* be over 180 degrees, but the interval certainly can't.
                if (Math.Abs(angleChange) > 15.0)
                {
                    Log.Debug("LockOpening|InterimAction", $"Caution - Angle changed by {angleChange:f1}.");
                }
                //LastInterval = Stopwatch.Elapsed;
                //Stopwatch.Restart();
            }

            protected override bool nextStageCriterion()
            {
                return Math.Abs((Angle - TargetAngle).ClampPlusMinus180()) < Current.LockBeingOpened.RotationAccuracyRequired
                    && (Math.Sign(AngleDelta) == Direction);
            }

            protected enum AbortReason { None, OffAxis, OffHorizontal, BackwardsTurning, Overshoot, TooJerky }
            protected AbortReason ReasonForAbort; 
            protected override bool abortCriterion()
            {
                if (Abs(AttitudeProvider.DotAxis) < minDotProductWithSetAxis)
                    ReasonForAbort = AbortReason.OffAxis;

                else if (Abs(AttitudeProvider.DotGravity) > maxDotProductWithGravity)
                    ReasonForAbort = AbortReason.OffHorizontal;

                //else if (Math.Abs(AngleDelta) > 3 * Current.LockBeingOpened.RotationAccuracyRequired
                //         && Math.Sign(AngleDelta) != Direction)
                //    ReasonForAbort = AbortReason.BackwardsTurning;

                //else if ((Angle - TargetAngle) * Direction > 2 * Current.LockBeingOpened.RotationAccuracyRequired)
                //    ReasonForAbort = AbortReason.Overshoot;

                return (ReasonForAbort != AbortReason.None);
            }

            protected override async Task abortActionAsync()
            {
                if (ReasonForAbort == AbortReason.OffAxis)
                {
                    Log.Debug("TumblerFindingStage", $"Lost 'grip' on tumbler, dotAxis of {AttitudeProvider.DotAxis:f3} < min allowed ({minDotProductWithSetAxis:f3}.");
                    await Speech.SayAllOf("Whoops.  Off axis.  Start over.");
                    AttitudeProvider.Deactivate();
                    CurrentStage = new BeginOpeningProcessStage("Retrying opening process", Kit);
                    return;
                }

                if (ReasonForAbort == AbortReason.OffHorizontal)
                {
                    Log.Debug("TumblerFindingStage", $"Lost 'grip' on tumbler, dotGravity of {AttitudeProvider.DotGravity:f3} > max allowed ({maxDotProductWithGravity:f3}.");
                    await Speech.SayAllOf("Whoops.  Off horizontal.  Start over.");
                    AttitudeProvider.Deactivate();
                    CurrentStage = new BeginOpeningProcessStage("Retrying opening process", Kit);
                    return;
                }

                if (ReasonForAbort == AbortReason.BackwardsTurning)
                {
                    Log.Debug("TumblerFindingStage", $"Backwards turning, by {Math.Abs(AngleDelta)} degrees.");
                    WrongDirectionAction();
                    return;
                }

                if (ReasonForAbort == AbortReason.Overshoot)
                {
                    Log.Debug("TumblerFindingStage", $"Overshoot, by {Math.Abs(Angle - TargetAngle)} degrees.");
                    WrongDirectionAction();
                    return;
                }
            }

            // Another piece which is different depending on type of lock.
            protected virtual void WrongDirectionAction()
            {
                throw new NotImplementedException();
            }
        }

        //private double score;
        //protected override bool nextStageCriterion()
        //{
        //    score = targetTumblerAngleSmoothedTo(AttitudeProvider) - Sqrt(AttitudeProvider.RunTime.TotalSeconds);
        //    return (score < 12f && FrameShiftFunctions.CheckIsReady(AttitudeProvider));
        //}
        //protected override async Task nextStageActionAsync()
        //{
        //    try
        //    {
        //        Log.Info("Casting stages", $"Success on {this.Label}. Angle was {targetTumblerAngleSmoothedTo(AttitudeProvider):f2} degrees [spell baseline on this being {targetTumbler.OrientationSigma:f2}], " +
        //            $"steadiness was {Stillness.StillnessScore:f2} [baseline {targetTumbler.SteadinessScoreWhenDefined:f2}], time was {Stillness.RunTime.TotalSeconds:f2}s [counted as {Math.Sqrt(Stillness.RunTime.TotalSeconds):f2} degrees].");
        //        targetTumbler.FeedbackSFX.Stop();
        //        await Task.Delay(150);

        //        if (targetTumbler.NextGlyph == Glyph.EndOfSpell)
        //        {
        //            if (Kit != null) Kit.ZeroOrientation = Quaternion.Identity;
        //            AttitudeProvider = null;

        //            Plugin.Vibrate.CrossVibrate.Current.Vibration(50 + 15 * Current.LockBeingOpened.Glyphs.Count);
        //            await Current.LockBeingOpened.CastingResult(this).Before(StopToken);
        //            CurrentStage?.Deactivate();
        //            CurrentStage = NullStage;
        //            if (Current == null) return;
        //            Current.LockBeingOpened = null;
        //            Current.Finish();
        //        }
        //        else
        //        {
        //            Plugin.Vibrate.CrossVibrate.Current.Vibration(25 + 10 * Current.LockBeingOpened.Glyphs.IndexOf(targetTumbler));
        //            targetTumbler.ProgressSFX.Play(1.0f);
        //            await Task.Delay(300); // Give a moment to get ready.
        //            CurrentStage = new TumblerFindingStage($"Glyph {Current.LockBeingOpened.Glyphs.IndexOf(targetTumbler) + 1}", Kit, targetTumbler.NextGlyph, AttitudeProvider);
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Log.Error("Glyph casting stage progression", e.Message);
        //        throw;
        //    }
        //}
    }

    [Activity(Label = "Atropos :: Safecracking ::")]
    public class SafecrackingActivity : LockedObjectOpeningActivity
    {
        private static SafecrackingActivity Current { get { return (SafecrackingActivity)CurrentActivity; } set { CurrentActivity = value; } }
        protected IEffect ClickFX, ClackFX, SuccessFX, ResetFX; // 'Clack' is the one you're listening for, 'click' is all the others.
        //protected BargraphData ClicksDisplay;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.Safecrack);
            LockBeingOpened = Lock.TestSafe;
        }

        protected override void OnResume()
        {
            //ClickFX = new EffectGroup("Safecracking.Click",
            //    Enumerable.Range(0, 30).Select(i => new Effect("Safecracking.Click" + i, Resource.Raw._169551_crisp_faint_impact)).ToArray());
            // The above are created as a group in order to be able to "slur" them together without having to stop the first one before the next one can start. 
            ClickFX = new Effect("Safecracking.Click", Resource.Raw._100804_reloadclick);

            ClackFX = new Effect("Safecracking.Clack", Resource.Raw.zwip_magic);
            SuccessFX = new Effect("Safecracking.Success", Resource.Raw._213996_bolt_opening);
            ResetFX = new Effect("Safecracking.Reset", Resource.Raw._110538_bolt_closing);
            //ResetFX.Volume = 0.75;

            //ClicksDisplay = ClicksDisplay ?? new BargraphData(this, "Clicks");

            CurrentStage = new BeginOpeningProcessStage("Waiting for zero stance", ThePlayersToolkit);

            base.OnResume();
        }

        protected TextView DialText;
        protected override void BeginFirstTumbler(ConsistentAxisAngleProvider oProvider)
        {
            FindViewById(Resource.Id.vault_notification).Visibility = ViewStates.Gone;
            DialText = FindViewById<TextView>(Resource.Id.vault_dial_text);
            DialText.Visibility = ViewStates.Visible;
            CurrentStage = new VaultTumblerFindingStage("Tumbler 0", ThePlayersToolkit, Current.LockBeingOpened.Tumblers[0], oProvider);
        }

        protected override void OnPause()
        {
            ClickFX.Deactivate();
            ClackFX.Deactivate();
            SuccessFX.Deactivate();
            ResetFX.Deactivate();

            base.OnPause();
        }

        public class VaultTumblerFindingStage : TumblerFindingStage
        {
            protected int LastClicks, CurrentClicks, TargetClicks, TotalClicks;
            protected double DegreesBetweenClicks, OffsetAngle;
            protected IEffect OnSuccessFX;
            protected bool?
                 Overshot = false;

            public VaultTumblerFindingStage(string label, Toolkit toolkit, Tumbler tgtTumbler, ConsistentAxisAngleProvider oProvider = null, double? offsetangle = null) 
                : base(label, toolkit, tgtTumbler, oProvider)
            {
                DegreesBetweenClicks = Current.LockBeingOpened.DegreesBetweenClicks;
                TotalClicks = (int)(360 / DegreesBetweenClicks);
                LastClicks = CurrentClicks = 0;
                OffsetAngle = offsetangle ?? ( 360.0 - AttitudeProvider.AngleSmoothed ).ClampPlusMinus180();
                TargetClicks = Clicks(tgtTumbler.Angle) % TotalClicks;
                OnSuccessFX = Current.ClackFX;
                Log.Debug("Safecracking|TumblerFinding", $"Starting to search for {label}");
            }

            protected int Clicks(double angle)
            {
                return (int)Math.Round(((angle + OffsetAngle) % 360) / DegreesBetweenClicks) % TotalClicks;
            }

            protected override async void interimAction()
            {
                base.interimAction();
                LastClicks = Clicks(LastAngle);
                CurrentClicks = Clicks(Angle);
                //var deltaClicks = Clicks((Angle - LastAngle).ClampPlusMinus180());
                var deltaClicks = (CurrentClicks - LastClicks + TotalClicks) % TotalClicks;

                //Current.ClicksDisplay.Update(Angle);
                //Current.RelayMessage(Resource.Id.bargraph_text1, $"Tumbler: << {CurrentClicks} >>");
                Current.DialText.Text = $"{CurrentClicks:d2}";

                // No change? No problem, then we're done here.
                if (LastClicks == CurrentClicks) return;

                // Otherwise, we need to play X clicks over Y milliseconds, to represent the clicks just passed.
                var intervals = (int)Math.Round(InterimInterval.TotalMilliseconds / Math.Abs(deltaClicks));
                var increment = Math.Sign(deltaClicks);
                for (int clickNum = LastClicks; clickNum * increment < (LastClicks + deltaClicks) * increment; clickNum += increment) // The "lastClicks + deltaClicks" could be "currentClicks" except for the +180/-180 problem.
                {
                    //Log.Debug("Clicking", $"Sound for click #{clickNum}...");
                    //if (clickNum != TargetClicks) Current.ClickFX.Play(0.25, useSpeakers: false, stopIfNecessary: true);
                    //else OnSuccessFX.Play(useSpeakers: false); // If I'm really feeling nasty, only the volume (and not the FX) will change here.  Mwah hah hah...

                    Current.ClickFX.Play(0.25, useSpeakers: false, stopIfNecessary: true);
                    if (clickNum == TargetClicks && Overshot == false)
                    {
                        await Task.Delay(50);
                        OnSuccessFX.Play(useSpeakers: false); // If I'm really feeling nasty, only the volume (and not the FX) will change here.  Mwah hah hah...
                        Overshot = null;
                        Plugin.Vibrate.CrossVibrate.Current.Vibration(10);
                        var direction = (Direction > 0) ? "CW" : "CCW";
                        Log.Debug("Safecracking|InterimAction", $"Tagged a tumbler at {TargetClicks} ({direction}).");
                    }
                    else if (Overshot == null)
                    {
                        Overshot = true;
                        Log.Debug("Safecracking|InterimAction", $"Overshot a tumbler at {clickNum}.");
                    }
                    if (clickNum == 0)
                    {
                        AngleDelta = Angle; // Just to make sure to reset the tally periodically (passing zero either way makes it possible to reach the target)
                        Log.Debug("Safecracking|InterimAction", $"Zeroed out again - carry on!.");
                        Overshot = false;
                    }
                    await Task.Delay(intervals);
                }
            }

            protected override void WrongDirectionAction()
            {
                CurrentStage = new VaultResetFindingStage("Resetting vault dial", Kit, AttitudeProvider, OffsetAngle);
            }

            protected override bool nextStageCriterion()
            {
                return (CurrentClicks == TargetClicks)
                    && (Math.Sign(AngleDelta) == Direction)
                    && (Overshot != false);
            }

            protected override async void nextStageAction()
            {
                if (targetTumbler.NextTumbler != Tumbler.EndOfLock)
                {
                    int i = Current.LockBeingOpened.Tumblers.IndexOf(targetTumbler) + 1;
                    CurrentStage = new VaultTumblerFindingStage($"Tumbler {i}", Kit, targetTumbler.NextTumbler, AttitudeProvider, OffsetAngle);
                }
                else
                {
                    await Current.SuccessFX.PlayToCompletion(useSpeakers: true);
                    await Speech.SayAllOf("Lock is open; well done.", useSpeakerMode: false);
                    Current.Finish();
                }
            }

            protected override bool abortCriterion()
            {
                if (AttitudeProvider.DotAxis < 0.5)
                    ReasonForAbort = AbortReason.OffAxis;

                else if (Overshot == true)
                    ReasonForAbort = AbortReason.Overshoot;

                return ReasonForAbort != AbortReason.None;
            }

            protected override void abortAction()
            {
                if (ReasonForAbort == AbortReason.OffAxis)
                {
                    Log.Debug("TumblerFindingStage", $"Lost 'grip' on tumbler, dotAxis of {AttitudeProvider.DotAxis:f3} < min allowed (0.5).");
                    //await Speech.SayAllOf("Whoops.  Off axis.  Start over.");
                    Toast.MakeText(Application.Context, "Lost your grip!  Have to start over again.", ToastLength.Short).Show();
                    AttitudeProvider.Deactivate();
                    CurrentStage = new BeginOpeningProcessStage("Retrying opening process", Kit);
                    return;
                }

                if (ReasonForAbort == AbortReason.Overshoot)
                {
                    Log.Debug("Safecracking|AbortAction", $"Returning to stage zero due to overshoot.");
                    CurrentStage = new VaultTumblerFindingStage($"Tumbler 0", Kit, Current.LockBeingOpened.Tumblers[0], AttitudeProvider, OffsetAngle);
                    return;
                }
            }

        }

        /// <summary>
        /// Special variation of the vault tumbler-finding stage which is more forgiving of off-axis-ness, doesn't care about directionality,
        /// and resets the working axis as part of kicking things off again.
        /// </summary>
        public class VaultResetFindingStage : VaultTumblerFindingStage
        {
            protected double abortMinDotAxis, abortMaxDotGravity;
            private ConsistentAxisAngleProvider newProvider;

            public VaultResetFindingStage(string label, Toolkit toolkit, ConsistentAxisAngleProvider oProvider = null, double? offsetangle = null)
                : base(label, toolkit, Tumbler.None, oProvider, offsetangle)
            {
                OnSuccessFX = Current.ResetFX;
                Direction = -Math.Sign(Angle);

                abortMinDotAxis = Math.Cos(Current.LockBeingOpened.OffAxisMaxDuringFindingPhase * 2.0 * QuaternionExtensions.degToRad);
                abortMaxDotGravity = Math.Sin(Current.LockBeingOpened.OffAxisMaxDuringFindingPhase * 2.0 * QuaternionExtensions.degToRad);

                // Start a new axis tracker running so that we can establish a fresh working axis using the data during this reset action.
                newProvider = new ConsistentAxisAngleProvider(frameShift: AttitudeProvider.FrameShift);
                newProvider.Activate();
            }

            protected override bool abortCriterion()
            {
                if (AttitudeProvider.DotAxis < abortMinDotAxis)
                    ReasonForAbort = AbortReason.OffAxis;

                //else if (AttitudeProvider.DotGravity > abortMaxDotGravity)
                //    ReasonForAbort = AbortReason.OffHorizontal;

                return (ReasonForAbort != AbortReason.None);
            }

            protected override async Task abortActionAsync()
            {
                if (ReasonForAbort == AbortReason.OffAxis)
                {
                    Log.Debug("TumblerResetStage", $"Lost 'grip' on tumbler, dotAxis of {AttitudeProvider.DotAxis:f3} < min allowed ({minDotProductWithSetAxis:f3}.");
                    await Speech.SayAllOf("Dammit.  Too crooked.  Once more, with feeling.");
                    //AttitudeProvider.Deactivate();
                    CurrentStage = new BeginOpeningProcessStage("Retrying opening process", Kit);
                    return;
                }

                if (ReasonForAbort == AbortReason.OffHorizontal)
                {
                    Log.Debug("TumblerResetStage", $"Lost 'grip' on tumbler, dotGravity of {AttitudeProvider.DotGravity:f3} > max allowed ({maxDotProductWithGravity:f3}.");
                    await Speech.SayAllOf("Rats. Need to stay closer to level.  Try again.");
                    //AttitudeProvider.Deactivate();
                    CurrentStage = new BeginOpeningProcessStage("Retrying opening process", Kit);
                    return;
                }
            }

            protected override bool nextStageCriterion()
            {
                return (Math.Abs(AttitudeProvider.AngleSmoothed) < 2 * Current.LockBeingOpened.RotationAccuracyRequired) // Close enough to zero degrees
                    && (AttitudeProvider.DotAxis < minDotProductWithSetAxis) // And close enough that it's not off-axis with respect to the previous axis
                    && (AttitudeProvider.DotGravity < maxDotProductWithGravity); // And still close enough to horizontal
                // NOTE - Ideally I'd like to also wait for newProvider.AxisIsSet, but I'm still uncertain about that; for now omitting that requirement.
            }
            protected override void nextStageAction()
            {
                //await newProvider.SetFrameShiftFromCurrent(); // Yes, it already had one, but we want to override that with the current values so that zero degrees is correct.
                //Current.BeginFirstTumbler(newProvider); // A subtlety here!  We're now *throwing out* the old AttitudeProvider, and switching to the new one henceforth.

                if (newProvider.AxisIsSet) AttitudeProvider.SetNewAxis(newProvider.Axis);
                Current.BeginFirstTumbler(AttitudeProvider);
            }
        }
    }

    [Activity(Label = "Atropos :: Lockpicking ::")]
    public class LockPickingActivity : LockedObjectOpeningActivity
    {
        private static LockPickingActivity Current { get { return (LockPickingActivity)CurrentActivity; } set { CurrentActivity = value; } }
        protected IEffect LockpickingSFX, TumblerLiftingSFX, TumblerDroppedSFX, SuccessSFX;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.Safecrack);
            LockBeingOpened = Lock.TestLock;
        }

        protected override void OnResume()
        {
            LockpickingSFX = new Effect("Lockpicking.Searching", Resource.Raw._365730_lockPicking);
            TumblerLiftingSFX = new Effect("Lockpicking.Lifting", Resource.Raw._232918_lock_keyopen_or_pick);
            TumblerDroppedSFX = new Effect("Lockpicking.DroppedTumbler", Resource.Raw._34249_mild_agh);
            SuccessSFX = new Effect("Lockpicking.Success", Resource.Raw._213996_bolt_opening);

            CurrentStage = new BeginOpeningProcessStage("Waiting for zero stance", ThePlayersToolkit);

            base.OnResume();
        }

        protected override void OnPause()
        {
            LockpickingSFX.Deactivate();
            TumblerLiftingSFX.Deactivate();
            TumblerDroppedSFX.Deactivate();
            SuccessSFX.Deactivate();

            base.OnPause();
        }

        

        protected TextView DialText;
        protected override void BeginFirstTumbler(ConsistentAxisAngleProvider oProvider)
        {
            FindViewById(Resource.Id.vault_notification).Visibility = ViewStates.Gone;
            DialText = FindViewById<TextView>(Resource.Id.vault_dial_text);
            DialText.Visibility = ViewStates.Visible;
            CurrentStage = new LockTumblerFindingStage("Tumbler 0", ThePlayersToolkit, Current.LockBeingOpened.Tumblers[0], oProvider);
        }

        public class LockTumblerFindingStage : TumblerFindingStage
        {
            protected IVector3Provider GyroProvider;
            protected RollingAverage<float> GyroMagnitude;

            public LockTumblerFindingStage(string label, Toolkit toolkit, Tumbler tgtTumbler, ConsistentAxisAngleProvider oProvider = null)
                : base(label, toolkit, tgtTumbler, oProvider)
            {
                GyroProvider = new Vector3Provider(SensorType.Gyroscope, StopToken);
                GyroProvider.Activate();

                GyroMagnitude = new RollingAverage<float>(25);
                
            }

            protected override void startAction()
            {
                base.startAction();
                Current.LockpickingSFX.Play(playLooping: true, useSpeakers: true, stopIfNecessary: true);
                Current.LockpickingSFX.DependsOn(StopToken);
            }

            protected override void interimAction()
            {
                base.interimAction();

                Current.DialText.Text = $"Gyro: {GyroMagnitude.Average:f2}";
                
            }

            protected override bool abortCriterion()
            {
                if (Abs(AttitudeProvider.DotAxis) < minDotProductWithSetAxis)
                    ReasonForAbort = AbortReason.OffAxis;

                else if (Abs(AttitudeProvider.DotGravity) > maxDotProductWithGravity)
                    ReasonForAbort = AbortReason.OffHorizontal;

                //else if (Math.Abs(AngleDelta) > 2 * Current.LockBeingOpened.RotationAccuracyRequired
                //         && Math.Sign(AngleDelta) != Direction)
                //    ReasonForAbort = AbortReason.BackwardsTurning;

                //else if ((Angle - TargetAngle) * Direction > 2 * Current.LockBeingOpened.RotationAccuracyRequired)
                //    ReasonForAbort = AbortReason.Overshoot;

                return (ReasonForAbort != AbortReason.None);
            }

            protected override bool nextStageCriterion()
            {
                GyroMagnitude.Update(GyroProvider.Vector.Length());
                return base.nextStageCriterion();
            }

            protected override void nextStageAction()
            {
                CurrentStage = new LockTumblerLiftingStage($"Manipulating tumbler.", Kit, targetTumbler, AttitudeProvider, GyroMagnitude);

                //if (targetTumbler.NextTumbler != Tumbler.EndOfLock)
                //{
                //    int i = Current.LockBeingOpened.Tumblers.IndexOf(targetTumbler) + 1;
                //    //CurrentStage = new LockTumblerFindingStage($"Finding tumbler {i}.", Kit, targetTumbler.NextTumbler, AttitudeProvider, OffsetAngle);
                //    CurrentStage = new LockTumblerManipulatingStage($"Manipulating tumbler {i}.", Kit, AttitudeProvider, OffsetAngle);
                //}
                //else
                //{
                //    await Current.SuccessFX.PlayToCompletion(useSpeakers: true);
                //    await Speech.SayAllOf("Lock is open; well done.", useSpeakerMode: false);
                //    Current.Finish();
                //}
            }

        }

        /// <summary>
        /// Special variation of the vault tumbler-finding stage which is more forgiving of off-axis-ness, doesn't care about directionality,
        /// and resets the working axis as part of kicking things off again.
        /// </summary>
        public class LockTumblerLiftingStage : LockTumblerFindingStage
        {
            protected RollingAverage<float> ShortTermSmoothedGyroMagnitude;
            private ConsistentAxisAngleProvider newProvider;

            public LockTumblerLiftingStage(string label, Toolkit toolkit, Tumbler tgtTumbler, ConsistentAxisAngleProvider oProvider = null, RollingAverage<float> gyromagnitude = null)
                : base(label, toolkit, tgtTumbler, oProvider)
            {
                GyroMagnitude = gyromagnitude ?? new RollingAverage<float>(25);
                Direction = -Math.Sign(Angle);
                ShortTermSmoothedGyroMagnitude = new RollingAverage<float>(3, GyroMagnitude.Average);

                // Start a new axis tracker running so that we can establish a fresh working axis using the data during this reset action.
                newProvider = new ConsistentAxisAngleProvider(frameShift: AttitudeProvider.FrameShift);
                newProvider.Activate();
            }

            protected override void startAction()
            {
                Current.TumblerLiftingSFX.Play(playLooping: true, useSpeakers: true, stopIfNecessary: true);
                Current.TumblerLiftingSFX.DependsOn(StopToken);
            }

            protected override bool abortCriterion()
            {
                if (AttitudeProvider.DotGravity > maxDotProductWithGravity)
                    ReasonForAbort = AbortReason.OffHorizontal;

                if (ShortTermSmoothedGyroMagnitude.Average > 2.0 * GyroMagnitude)
                    ReasonForAbort = AbortReason.TooJerky;

                return (ReasonForAbort != AbortReason.None);
            }

            protected override async Task abortActionAsync()
            {
                
                if (ReasonForAbort == AbortReason.OffHorizontal)
                {
                    Log.Debug("TumblerResetStage", $"Lost 'grip' on tumbler, dotGravity of {AttitudeProvider.DotGravity:f3} > max allowed ({maxDotProductWithGravity:f3}.");
                    await Speech.SayAllOf("Rats. Need to stay closer to level.  Try again.");
                    AttitudeProvider.Deactivate();
                    CurrentStage = new BeginOpeningProcessStage("Retrying opening process", Kit);
                    return;
                }

                if (ReasonForAbort == AbortReason.TooJerky)
                {
                    Current.TumblerLiftingSFX.Stop();
                    await Task.Delay(250);
                    await Current.TumblerDroppedSFX.PlayToCompletion();
                    CurrentStage = new LockTumblerFindingStage("Retrying to find tumbler", Kit, targetTumbler, AttitudeProvider);
                }
            }

            protected override bool nextStageCriterion()
            {
                ShortTermSmoothedGyroMagnitude.Update(GyroProvider.Vector.Length());

                return (Math.Abs(AttitudeProvider.AngleSmoothed) < Current.LockBeingOpened.RotationAccuracyRequired); // Close enough to zero degrees

            }
            protected override async void nextStageAction()
            {
                
                if (targetTumbler.NextTumbler != Tumbler.EndOfLock)
                {
                    int i = Current.LockBeingOpened.Tumblers.IndexOf(targetTumbler) + 1;
                    if (newProvider.AxisIsSet) AttitudeProvider.SetNewAxis(newProvider.Axis);
                    CurrentStage = new LockTumblerFindingStage($"Finding tumbler {i}.", Kit, targetTumbler.NextTumbler, AttitudeProvider);
                }
                else
                {
                    await Current.SuccessSFX.PlayToCompletion(useSpeakers: true);
                    await Speech.SayAllOf("Got it! Nice work.", useSpeakerMode: false);
                    Current.Finish();
                }
            }
        }
    }
}