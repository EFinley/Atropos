
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
    //[Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public abstract class LockedObjectOpeningActivity : BargraphDisplayActivity
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

            CurrentStage?.Deactivate();
            SeekReadyPosition();
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
            private Vector3 UpDirection;
            private Vector3Provider Gravity;

            public BeginOpeningProcessStage(string label, Toolkit kit, Vector3 upDirection, bool AutoStart = true) : base(label)
            {
                Kit = kit;
                UpDirection = upDirection;
                Stillness = new StillnessProvider();
                SetUpProvider(Stillness, allowProviderToPersist: true);
                Gravity = new Vector3Provider(SensorType.Gravity, StopToken);
                Gravity.Activate();

                Current.RunOnUiThread(() =>
                {
                    Current.FindViewById(Resource.Id.vault_notification).Visibility = ViewStates.Visible;
                    Current.FindViewById(Resource.Id.vault_dial_text).Visibility = ViewStates.Gone;
                });

                Current.LockBeingOpened.NumberOfAttempts++;
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
                Current.RelayMessage(Resource.Id.bargraph_text1, $"Steadiness: {Stillness.StillnessScore:f3}, Angle: {Gravity.Vector.AngleTo(UpDirection):f2}");
            }

            protected override bool nextStageCriterion()
            {
                return (Stillness.ReadsMoreThan(2f)
                    && Gravity.Vector.AngleTo(UpDirection) < 25);
            }
            protected override async Task nextStageActionAsync()
            {
                //var newProvider = new ConsistentAxisAngleProvider();
                //newProvider.Activate();

                ////Current.StillnessDisplay?.Dispose();
                Current.RelayMessage(Resource.Id.bargraph_text1, "Beginning...");

                //await newProvider.SetFrameShiftFromCurrent();
                await Speech.SayAllOf("Begin");
                Current.BeginFirstTumbler(); // Because the specific next step depends on the kind of lock, unlike everything prior to this point.
            }
        }

        protected virtual void SeekReadyPosition()
        {
            throw new NotImplementedException();
        }

        protected virtual void BeginFirstTumbler()
        {
            throw new NotImplementedException();
        }

        public class TumblerFindingStage : GestureRecognizerStage
        {
            
            protected Toolkit Kit;
            protected Tumbler targetTumbler;
            protected AngleAxisProvider AttitudeProvider;

            protected double LastAngle, Angle, TargetAngle;
            protected RollingAverage<double> AngleTrend;
            protected int RequiredDirection;
            protected int CurrentDirection { get { return Math.Sign(AngleTrend.Average); } }

            public TumblerFindingStage(string label, Toolkit toolkit, Tumbler tgtTumbler, AngleAxisProvider provider) : base(label)
            {
                Kit = toolkit;
                targetTumbler = tgtTumbler;

                AttitudeProvider = provider;
                SetUpProvider(AttitudeProvider, allowProviderToPersist: true);

                LastAngle = Angle = AttitudeProvider.Angle;
                Log.Debug("TumblerStage|Ctor", $"Initial angle: {Angle}");
                TargetAngle = tgtTumbler.Angle;
                RequiredDirection = tgtTumbler.Direction;
                // Certain special tumblers - like ResetToZero - have a designated direction of zero, meaning "opposite to current setting."
                if (tgtTumbler.Direction == 0) RequiredDirection = -1 * Math.Sign(provider.Angle);

                // CurrentDirection is based on AngleTrend.
                AngleTrend = new RollingAverage<double>(5);

                InterimInterval = TimeSpan.FromMilliseconds(20);

                Activate();
            }

            protected override bool interimCriterion()
            {
                return true;
            }
            protected override void interimAction()
            {
                LastAngle = Angle;
                Angle = AttitudeProvider.Angle;
                var angleChange = Angle - LastAngle;
                Log.Debug("TumblerStage|Interim", $"New angle: {Angle}");
                if (Math.Abs(angleChange) > 15.0)
                {
                    Log.Debug("LockOpening|InterimAction", $"Caution - Angle changed by {angleChange:f1}.");
                }
                AngleTrend.Update(angleChange);
            }

            protected override bool nextStageCriterion()
            {
                return Math.Abs(Angle - TargetAngle) < Current.LockBeingOpened.RotationAccuracyRequired
                    && CurrentDirection == RequiredDirection;
            }

            protected override bool abortCriterion()
            {
                var gravityDirectionVector = AttitudeProvider.Vector.Normalize();
                return gravityDirectionVector.Dot(AttitudeProvider.Axis) > 0.5;
            }

            protected override async Task abortActionAsync()
            {
                var gravVector = AttitudeProvider.Vector.Normalize();
                Log.Debug("TumblerStage|Abort", $"Gravity vector ({gravVector}) closer to 'Up' than to 'Horizontal' ({AttitudeProvider.Axis}) - indicates restart.");
                await Speech.SayAllOf("Whoops. Off axis. Start over.");
                AttitudeProvider.Deactivate();
                Current.SeekReadyPosition();
            }

            //protected enum AbortReason { None, BackwardsTurning, Overshoot, TooJerky }
            //protected AbortReason ReasonForAbort; 
            //protected override bool abortCriterion()
            //{
            //    //if (Abs(AttitudeProvider.DotAxis) < minDotProductWithSetAxis)
            //    //    ReasonForAbort = AbortReason.OffAxis;

            //    //else if (Abs(AttitudeProvider.DotGravity) > maxDotProductWithGravity)
            //    //    ReasonForAbort = AbortReason.OffHorizontal;

            //    //if (Math.Abs(AngleDelta) > 3 * Current.LockBeingOpened.RotationAccuracyRequired
            //    if (Math.Abs(Angle) > 3 * Current.LockBeingOpened.RotationAccuracyRequired
            //             //&& Math.Sign(AngleDelta) != Direction)
            //             && Math.Sign(Angle) != Math.Sign(TargetAngle))
            //        ReasonForAbort = AbortReason.BackwardsTurning;

            //    else if ((Angle - TargetAngle) * Direction > 2 * Current.LockBeingOpened.RotationAccuracyRequired)
            //        ReasonForAbort = AbortReason.Overshoot;

            //    return (ReasonForAbort != AbortReason.None);
            //}

            //protected override void abortAction()
            //{
            //    //if (ReasonForAbort == AbortReason.OffAxis)
            //    //{
            //    //    Log.Debug("TumblerFindingStage", $"Lost 'grip' on tumbler, dotAxis of {AttitudeProvider.DotAxis:f3} < min allowed ({minDotProductWithSetAxis:f3}.");
            //    //    await Speech.SayAllOf("Whoops.  Off axis.  Start over.");
            //    //    AttitudeProvider.Deactivate();
            //    //    CurrentStage = new BeginOpeningProcessStage("Retrying opening process", Kit);
            //    //    return;
            //    //}

            //    //if (ReasonForAbort == AbortReason.OffHorizontal)
            //    //{
            //    //    Log.Debug("TumblerFindingStage", $"Lost 'grip' on tumbler, dotGravity of {AttitudeProvider.DotGravity:f3} > max allowed ({maxDotProductWithGravity:f3}.");
            //    //    await Speech.SayAllOf("Whoops.  Off horizontal.  Start over.");
            //    //    AttitudeProvider.Deactivate();
            //    //    CurrentStage = new BeginOpeningProcessStage("Retrying opening process", Kit);
            //    //    return;
            //    //}

            //    if (ReasonForAbort == AbortReason.BackwardsTurning)
            //    {
            //        Log.Debug("TumblerFindingStage", $"Backwards turning, by {Math.Abs(Angle)} degrees.");
            //        WrongDirectionAction();
            //        return;
            //    }

            //    if (ReasonForAbort == AbortReason.Overshoot)
            //    {
            //        Log.Debug("TumblerFindingStage", $"Overshoot, by {Math.Abs(Angle - TargetAngle)} degrees.");
            //        WrongDirectionAction();
            //        return;
            //    }
            //}

            //// Another piece which is different depending on type of lock.
            //protected virtual void WrongDirectionAction()
            //{
            //    throw new NotImplementedException();
            //}
        }
        }

    [Activity(Label = "Atropos :: Safecracking ::", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class SafecrackingActivity : LockedObjectOpeningActivity
    {
        private static SafecrackingActivity Current { get { return (SafecrackingActivity)CurrentActivity; } set { CurrentActivity = value; } }
        protected IEffect ClickFX, ClackFX, SuccessFX, ResetFX; // 'Clack' is the one you're listening for, 'click' is all the others.

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.Safecrack);
            LockBeingOpened = (Lock.Current != Lock.None) ? Lock.Current : Lock.TestSafe;
        }

        protected override void OnResume()
        {
            ClickFX = new Effect("Safecracking.Click", Resource.Raw._100804_reloadclick) { Volume = 0.25 };
            ClackFX = new Effect("Safecracking.Click", Resource.Raw._100804_reloadclick);
            SuccessFX = new Effect("Safecracking.Success", Resource.Raw._213996_bolt_opening);
            ResetFX = new Effect("Safecracking.Reset", Resource.Raw._110538_bolt_closing);

            //DialText = FindViewById<TextView>(Resource.Id.vault_dial_text);
            DialBody = FindViewById(Resource.Id.safedial_body);

            base.OnResume();
        }

        protected override void SeekReadyPosition()
        {
            CurrentStage = new BeginOpeningProcessStage("Waiting for zero stance", ThePlayersToolkit, Vector3.UnitY);
        }

        protected TextView DialText;
        protected View DialBody;
        protected override void BeginFirstTumbler()
        {
            RunOnUiThread(() =>
            {
                FindViewById(Resource.Id.vault_notification).Visibility = ViewStates.Gone;
                //DialText.Visibility = ViewStates.Visible;
                DialBody.Rotation = 0;
            });
            var provider = new AngleAxisProvider(Vector3.UnitY, Vector3.UnitZ);
            SensorProvider.EnsureIsReady(provider);
            CurrentStage = new VaultTumblerFindingStage("Tumbler 0", ThePlayersToolkit, Current.LockBeingOpened.Tumblers[0], provider);
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
            protected double DegreesBetweenClicks;
            protected bool? Overshot = false;

            public VaultTumblerFindingStage(string label, Toolkit toolkit, Tumbler tgtTumbler, AngleAxisProvider oProvider = null) 
                : base(label, toolkit, tgtTumbler, oProvider)
            {
                DegreesBetweenClicks = Current.LockBeingOpened.DegreesBetweenClicks;
                TotalClicks = (int)(360 / DegreesBetweenClicks);
                LastClicks = CurrentClicks = Clicks(oProvider.Angle);
                TargetClicks = Clicks(tgtTumbler.Angle);
                Log.Debug("Safecracking|TumblerFinding", $"Starting to search for {label}");
            }

            protected int Clicks(double angle)
            {
                //return (int)Math.Round((angle % 360) / DegreesBetweenClicks) % TotalClicks;
                return (int)Math.Truncate((angle % 360) / DegreesBetweenClicks);
            }

            protected override async void interimAction()
            {
                base.interimAction();
                LastClicks = Clicks(LastAngle);
                CurrentClicks = Clicks(Angle);

                var deltaClicks = CurrentClicks - LastClicks;
                Current.RunOnUiThread(() => 
                {
                    //Current.DialText.Text = $"{(CurrentClicks + TotalClicks) % TotalClicks:d2}";
                    Current.DialBody.Rotation = -1f * (float)AttitudeProvider.Angle;
                });

                // No change? No problem, then we're done here.
                if (LastClicks == CurrentClicks) return;

                // Otherwise, we need to play X clicks over Y milliseconds, to represent the clicks just passed.
                var intervals = (int)Math.Round(InterimInterval.TotalMilliseconds / Math.Abs(deltaClicks));
                var increment = Math.Sign(deltaClicks);
                //for (int clickNum = LastClicks; clickNum * increment < (LastClicks + deltaClicks) * increment; clickNum += increment) // The "lastClicks + deltaClicks" could be "currentClicks" except for the +180/-180 problem.
                for (int clickNum = LastClicks; clickNum * increment < CurrentClicks * increment; clickNum += increment)
                {
                    // Play the appropriate sound effect
                    if (clickNum == TargetClicks - increment && Overshot == false && increment == RequiredDirection)
                    {
                        Current.ClackFX.Play(useSpeakers: false);
                        Overshot = null;
                        Plugin.Vibrate.CrossVibrate.Current.Vibration(6 / Math.Abs(deltaClicks));
                        var direction = (CurrentDirection > 0) ? "CW" : "CCW";
                        Log.Debug("Safecracking|InterimAction", $"Tagged a tumbler at {TargetClicks} ({direction}).");
                    }
                    else
                    {
                        Current.ClickFX.Play(useSpeakers: false, stopIfNecessary: true);
                    }

                    // Things that would constitute errors requiring you to zero out the lock again:
                    // (A) Turning the wrong direction by at least one click
                    if (increment != RequiredDirection)
                    {
                        Log.Debug("Safecracking|InterimAction", $"Turned backward into {clickNum}.");
                        Overshot = true;
                    }
                    // (B) Overshooting the combination by at least one click
                    else if (clickNum * increment >= TargetClicks * increment)
                    {
                        Log.Debug("Safecracking|InterimAction", $"Overshot a tumbler into {clickNum}.");
                        Overshot = true;
                    }

                    await Task.Delay(intervals);

                    //Current.ClickFX.Play(0.25, useSpeakers: false, stopIfNecessary: true);
                    //if (clickNum == TargetClicks && Overshot == false && increment == targetTumbler.Direction)
                    //{
                    //    //await Task.Delay(25);
                    //    OnSuccessFX.Play(useSpeakers: false); // If I'm really feeling nasty, only the volume (and not the FX) will change here.  Mwah hah hah...
                    //    Overshot = null;
                    //    Plugin.Vibrate.CrossVibrate.Current.Vibration(10);
                    //    var direction = (Direction > 0) ? "CW" : "CCW";
                    //    Log.Debug("Safecracking|InterimAction", $"Tagged a tumbler at {TargetClicks} ({direction}).");
                    //}
                    //else if (clickNum * targetTumbler.Direction > TargetClicks * targetTumbler.Direction) // Overshot == null && 
                    //{
                    //    Overshot = true;
                    //    Log.Debug("Safecracking|InterimAction", $"Overshot a tumbler at {clickNum}.");
                    //}
                    //else if (clickNum * targetTumbler.Direction == 0)
                    //{
                    //    //AngleDelta = Angle; // Just to make sure to reset the tally periodically (passing zero either way makes it possible to reach the target)
                    //    Log.Debug("Safecracking|InterimAction", $"Zeroed out again - carry on!.");
                    //    Overshot = false;
                    //}
                }
            }

            //protected override void WrongDirectionAction()
            //{
            //    CurrentStage = new VaultResetFindingStage("Resetting vault dial", Kit, AttitudeProvider, OffsetAngle);
            //}

            protected override bool nextStageCriterion()
            {
                return Overshot != false;
            }

            protected override async void nextStageAction()
            {
                if (Overshot == true) // Need to reset to zero
                {
                    if (Current.LockBeingOpened.Tumblers.IndexOf(targetTumbler) > 0) Current.LockBeingOpened.NumberOfAttempts++;
                    CurrentStage = new VaultTumblerFindingStage("Resetting to zero", Kit, Tumbler.ResetToZero, AttitudeProvider);
                }
                else if (object.ReferenceEquals(targetTumbler, Tumbler.ResetToZero))
                {
                    CurrentStage = new VaultTumblerFindingStage("Tumbler 0", Kit, Current.LockBeingOpened.Tumblers[0], AttitudeProvider);
                }
                else if (targetTumbler.NextTumbler != Tumbler.EndOfLock)
                {
                    int i = Current.LockBeingOpened.Tumblers.IndexOf(targetTumbler) + 1;
                    CurrentStage = new VaultTumblerFindingStage($"Tumbler {i}", Kit, targetTumbler.NextTumbler, AttitudeProvider);
                }
                else
                {
                    await Current.SuccessFX.PlayToCompletion(useSpeakers: true);
                    Current.LockBeingOpened.AnnounceLockOpened();
                    await Speech.SayAllOf("Lock is open; well done.", useSpeakerMode: false);
                    Current.Finish();
                }
            }

            //protected override bool abortCriterion()
            //{
            //    if (AttitudeProvider.DotAxis < 0.5)
            //        ReasonForAbort = AbortReason.OffAxis;

            //    else if (Overshot == true)
            //        ReasonForAbort = AbortReason.Overshoot;

            //    return ReasonForAbort != AbortReason.None;
            //}

            //protected override void abortAction()
            //{
            //    if (ReasonForAbort == AbortReason.OffAxis)
            //    {
            //        Log.Debug("TumblerFindingStage", $"Lost 'grip' on tumbler, dotAxis of {AttitudeProvider.DotAxis:f3} < min allowed (0.5).");
            //        //await Speech.SayAllOf("Whoops.  Off axis.  Start over.");
            //        Toast.MakeText(Application.Context, "Lost your grip!  Have to start over again.", ToastLength.Short).Show();
            //        AttitudeProvider.Deactivate();
            //        CurrentStage = new BeginOpeningProcessStage("Retrying opening process", Kit);
            //        return;
            //    }

            //    if (ReasonForAbort == AbortReason.Overshoot)
            //    {
            //        Log.Debug("Safecracking|AbortAction", $"Returning to stage zero due to overshoot.");
            //        CurrentStage = new VaultTumblerFindingStage($"Tumbler 0", Kit, Current.LockBeingOpened.Tumblers[0], AttitudeProvider, OffsetAngle);
            //        return;
            //    }
            //}

        }

        //    /// <summary>
        //    /// Special variation of the vault tumbler-finding stage which is more forgiving of off-axis-ness, doesn't care about directionality,
        //    /// and resets the working axis as part of kicking things off again.
        //    /// </summary>
        //    public class VaultResetFindingStage : VaultTumblerFindingStage
        //    {
        //        protected double abortMinDotAxis, abortMaxDotGravity;
        //        private ConsistentAxisAngleProvider newProvider;

        //        public VaultResetFindingStage(string label, Toolkit toolkit, ConsistentAxisAngleProvider oProvider = null, double? offsetangle = null)
        //            : base(label, toolkit, Tumbler.None, oProvider, offsetangle)
        //        {
        //            OnSuccessFX = Current.ResetFX;
        //            Direction = -Math.Sign(Angle);

        //            abortMinDotAxis = Math.Cos(Current.LockBeingOpened.OffAxisMaxDuringFindingPhase * 2.0 * QuaternionExtensions.degToRad);
        //            abortMaxDotGravity = Math.Sin(Current.LockBeingOpened.OffAxisMaxDuringFindingPhase * 2.0 * QuaternionExtensions.degToRad);

        //            // Start a new axis tracker running so that we can establish a fresh working axis using the data during this reset action.
        //            newProvider = new ConsistentAxisAngleProvider(frameShift: AttitudeProvider.FrameShift);
        //            newProvider.Activate();
        //        }

        //        protected override bool abortCriterion()
        //        {
        //            if (AttitudeProvider.DotAxis < abortMinDotAxis)
        //                ReasonForAbort = AbortReason.OffAxis;

        //            //else if (AttitudeProvider.DotGravity > abortMaxDotGravity)
        //            //    ReasonForAbort = AbortReason.OffHorizontal;

        //            return (ReasonForAbort != AbortReason.None);
        //        }

        //        protected override async Task abortActionAsync()
        //        {
        //            if (ReasonForAbort == AbortReason.OffAxis)
        //            {
        //                Log.Debug("TumblerResetStage", $"Lost 'grip' on tumbler, dotAxis of {AttitudeProvider.DotAxis:f3} < min allowed ({minDotProductWithSetAxis:f3}.");
        //                await Speech.SayAllOf("Dammit.  Too crooked.  Once more, with feeling.");
        //                //AttitudeProvider.Deactivate();
        //                CurrentStage = new BeginOpeningProcessStage("Retrying opening process", Kit);
        //                return;
        //            }

        //            if (ReasonForAbort == AbortReason.OffHorizontal)
        //            {
        //                Log.Debug("TumblerResetStage", $"Lost 'grip' on tumbler, dotGravity of {AttitudeProvider.DotGravity:f3} > max allowed ({maxDotProductWithGravity:f3}.");
        //                await Speech.SayAllOf("Rats. Need to stay closer to level.  Try again.");
        //                //AttitudeProvider.Deactivate();
        //                CurrentStage = new BeginOpeningProcessStage("Retrying opening process", Kit);
        //                return;
        //            }
        //        }

        //        protected override bool nextStageCriterion()
        //        {
        //            return (Math.Abs(AttitudeProvider.AngleSmoothed) < 2 * Current.LockBeingOpened.RotationAccuracyRequired) // Close enough to zero degrees
        //                && (AttitudeProvider.DotAxis < minDotProductWithSetAxis) // And close enough that it's not off-axis with respect to the previous axis
        //                && (AttitudeProvider.DotGravity < maxDotProductWithGravity); // And still close enough to horizontal
        //            // NOTE - Ideally I'd like to also wait for newProvider.AxisIsSet, but I'm still uncertain about that; for now omitting that requirement.
        //        }
        //        protected override void nextStageAction()
        //        {
        //            //await newProvider.SetFrameShiftFromCurrent(); // Yes, it already had one, but we want to override that with the current values so that zero degrees is correct.
        //            //Current.BeginFirstTumbler(newProvider); // A subtlety here!  We're now *throwing out* the old AttitudeProvider, and switching to the new one henceforth.

        //            if (newProvider.AxisIsSet) AttitudeProvider.SetNewAxis(newProvider.Axis);
        //            Current.BeginFirstTumbler(AttitudeProvider);
        //        }
        //    }
    }

    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.ReverseLandscape)]
    public class LockPickingActivity : LockedObjectOpeningActivity
    {
        private static LockPickingActivity Current { get { return (LockPickingActivity)CurrentActivity; } set { CurrentActivity = value; } }
        protected IEffect LockpickingSFX, TumblerLiftingSFX, TumblerDroppedSFX, SuccessSFX;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.Lockpick); // TODO - change this!
            LockBeingOpened = (Lock.Current != Lock.None) ? Lock.Current : Lock.TestLock;
        }

        protected override void OnResume()
        {
            LockpickingSFX = new Effect("Lockpicking.Searching", Resource.Raw._365730_lockPicking);
            TumblerLiftingSFX = new Effect("Lockpicking.Lifting", Resource.Raw._232918_lock_keyopen_or_pick);
            TumblerDroppedSFX = new Effect("Lockpicking.DroppedTumbler", Resource.Raw._34249_mild_agh);
            SuccessSFX = new Effect("Lockpicking.Success", Resource.Raw._213996_bolt_opening);

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

        protected override void SeekReadyPosition()
        {
            CurrentStage = new BeginOpeningProcessStage("Waiting for zero stance", ThePlayersToolkit, Vector3.UnitX * -1f);
        }

        protected TextView DialText;
        protected override void BeginFirstTumbler()
        {
            RunOnUiThread(() =>
            {
                FindViewById(Resource.Id.vault_notification).Visibility = ViewStates.Gone;
                //DialText = FindViewById<TextView>(Resource.Id.vault_dial_text);
                //DialText.Visibility = ViewStates.Visible;
            });
            var provider = new AngleAxisProvider(Vector3.UnitX * -1f, Vector3.UnitY);
            SensorProvider.EnsureIsReady(provider);
            CurrentStage = new LockTumblerFindingStage("Tumbler 0", ThePlayersToolkit, Current.LockBeingOpened.Tumblers[0], provider);
        }



        public class LockTumblerFindingStage : TumblerFindingStage
        {

            public LockTumblerFindingStage(string label, Toolkit toolkit, Tumbler tgtTumbler, AngleAxisProvider oProvider = null)
                : base(label, toolkit, tgtTumbler, oProvider)
            {
                Log.Debug("Lockpicking|TumblerFinding", $"Starting to search for {label}");
            }

            protected override void startAction()
            {
                Current.LockpickingSFX.Play(playLooping: true, useSpeakers: true);
            }

            protected override void interimAction()
            {
                base.interimAction();
                if (object.ReferenceEquals(targetTumbler, Tumbler.ResetToZero)
                    && Math.Abs(Angle) < Current.LockBeingOpened.RotationAccuracyRequired * 2)
                {
                    targetTumbler = Current.LockBeingOpened.Tumblers[0];
                    TargetAngle = targetTumbler.Angle;
                    RequiredDirection = targetTumbler.Direction;
                    Plugin.Vibrate.CrossVibrate.Current.Vibration(5);
                }
                Log.Debug("Lockpicking|TumblerFinding", $"Aiming for {TargetAngle}, currently at {Angle}.");
            }

            protected override void nextStageAction()
            {
                CurrentStage = new LockTumblerLiftingStage($"Lifting {Label}", Kit, targetTumbler, AttitudeProvider);
                Current.LockpickingSFX.Stop();
            }

        }

        public class LockTumblerLiftingStage : TumblerFindingStage
        {

            private RollingAverage<float> AngleRateOfChange;
            private Tumbler tumblerUnderway;

            public LockTumblerLiftingStage(string label, Toolkit toolkit, Tumbler tgtTumbler, AngleAxisProvider oProvider = null)
                : base(label, toolkit, Tumbler.PinMoveTarget, oProvider)
            {
                Log.Debug("Lockpicking|TumblerLifting", $"Lifting {label}");
                AngleRateOfChange = new RollingAverage<float>(5, 0f);
                tumblerUnderway = tgtTumbler;
            }

            protected override void startAction()
            {
                Current.TumblerLiftingSFX.Play(playLooping: true, useSpeakers: true);
                StopToken.Register(() => { Current.TumblerLiftingSFX.Stop(); });
            }

            protected override void interimAction()
            {
                base.interimAction();
                AngleRateOfChange.Update((float)(Math.Abs(Angle - LastAngle) / InterimInterval.TotalSeconds));
                Plugin.Vibrate.CrossVibrate.Current.Vibration((int)Math.Max(1, 3 * AngleRateOfChange / Current.LockBeingOpened.MaxRotationRateInLiftingPhase));
            }

            protected override bool nextStageCriterion()
            {
                return base.nextStageCriterion() || AngleRateOfChange > Current.LockBeingOpened.MaxRotationRateInLiftingPhase;
            }

            protected override async void nextStageAction()
            {
                // Did we drop the tumbler (by moving too fast)?
                if (AngleRateOfChange > Current.LockBeingOpened.MaxRotationRateInLiftingPhase)
                {
                    Plugin.Vibrate.CrossVibrate.Current.Vibration(10);
                    Current.TumblerLiftingSFX.Stop();
                    Current.TumblerDroppedSFX.Play();
                    Current.LockBeingOpened.NumberOfAttempts++;
                    CurrentStage = new LockTumblerFindingStage("Try again from tumbler 0", Kit, Current.LockBeingOpened.Tumblers[0], AttitudeProvider);
                }
                else if (tumblerUnderway.NextTumbler != Tumbler.EndOfLock)
                {
                    int i = Current.LockBeingOpened.Tumblers.IndexOf(tumblerUnderway) + 1;
                    CurrentStage = new LockTumblerFindingStage($"Tumbler {i}", Kit, tumblerUnderway.NextTumbler, AttitudeProvider);
                }
                else
                {
                    await Current.SuccessSFX.PlayToCompletion(useSpeakers: true);
                    Current.LockBeingOpened.AnnounceLockOpened();
                    await Speech.SayAllOf("Lock is open; well done.", useSpeakerMode: false);
                    Current.Finish();
                }
            }

        }
    }
}