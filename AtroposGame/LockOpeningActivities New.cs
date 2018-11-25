
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

namespace Atropos.Locks.New
{
    //[Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public abstract class LockedObjectOpeningActivity : BaseActivity
    {
        protected Lock LockBeingOpened;
        private static LockedObjectOpeningActivity Current { get { return (LockedObjectOpeningActivity)CurrentActivity; } set { CurrentActivity = value; } }
        protected Effect WhistleFX;
        protected Vector3Provider GravProvider;
        //protected BargraphData StillnessDisplay;

        public Vector3 AxisVector, EstimatedAxis;
        public Vector3 UpVector;

        protected bool _isLimberedUp = false;
        protected bool _collectingData = false;
        protected bool _finalizingGesture = false;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            GravProvider = new Vector3Provider(SensorType.Gravity, StopToken);
            GravProvider.Activate(); // Getting this running right away means that we can ask it for a meaningful value when the player presses the button.

            useVolumeTrigger = true;
            OnVolumeButtonPressed += LimberUpVolumePressed;
            OnVolumeButtonReleased += LimberUpVolumeReleased;

            Speech.Say("Limber up.");
            Speech.Say("Start at zero, hold down button and mime your rotation, let go at zero.", SoundOptions.AtSpeed(1.25));
        }

        protected virtual void LimberUpVolumePressed(object sender, EventArgs e)
        {
            CurrentStage = new LimberingUpStage(EstimatedAxis, GravProvider, AutoStart: true);
        }

        protected virtual async void LimberUpVolumeReleased(object sender, EventArgs e)
        {
            (CurrentStage as LimberingUpStage).StopAndReturnValues(out Vector3 axis, out Vector3 upDir);
            AxisVector = axis;
            UpVector = upDir;
            Log.Debug("Locks|LimberUp", $"Axis Vector is {AxisVector:f2}, up vector is {UpVector:f2}");

            await Task.Delay(250);
            OnVolumeButtonPressed -= LimberUpVolumePressed;
            OnVolumeButtonReleased -= LimberUpVolumeReleased;
            OnVolumeButtonPressed += HandleVolumeButtonPressed;
            OnVolumeButtonReleased += HandleVolumeButtonReleased;
            SeekReadyPosition();
        }

        protected virtual void HandleVolumeButtonPressed(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        protected virtual void HandleVolumeButtonReleased(object sender, EventArgs e)
        {
            throw new NotImplementedException();
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

            //CurrentStage?.Deactivate();
            //SeekReadyPosition();
        }

        protected override void OnPause()
        {
            WhistleFX.Deactivate();
            base.OnPause();
        }
        
        protected virtual void BeginFirstTumbler()
        {
            throw new NotImplementedException();
        }

        protected virtual void SeekReadyPosition()
        {
            throw new NotImplementedException();
        }

        public class LimberingUpStage : GestureRecognizerStage
        {
            private string _tag = "LockOpening|LimberingUpStage";
            private Vector3 UpDirectionInitial;
            private SimpleAverage<Vector3> AverageAxis;
            private Vector3Provider GravProvider, GyroProvider;

            private bool softStop;

            public LimberingUpStage(Vector3 estimatedAxis, Vector3Provider gravProvider, bool AutoStart = true) : base("Limbering up")
            {
                AverageAxis = new SimpleAverage<Vector3>(estimatedAxis);
                GyroProvider = new Vector3Provider(SensorType.Gyroscope, StopToken);
                GravProvider = gravProvider;
                SetUpProvider(GyroProvider);

                UpDirectionInitial = GravProvider.Vector;

                //Current.RunOnUiThread(() =>
                //{
                //    Current.FindViewById(Resource.Id.vault_notification).Visibility = ViewStates.Visible;
                //    Current.FindViewById(Resource.Id.vault_dial_text).Visibility = ViewStates.Gone;
                //});

                InterimInterval = TimeSpan.FromMilliseconds(20);
                if (AutoStart) Activate(Current.StopToken);
            }

            protected override bool interimCriterion()
            {
                return true;
            }

            protected override void interimAction()
            {
                if (GyroProvider.Vector.Length() > 0.1)
                {
                    var gyroAxis = GyroProvider.Vector.Normalize();
                    if (gyroAxis.Dot(AverageAxis) > 0.7)
                    {
                        AverageAxis.Update(gyroAxis);
                    }
                    else if (gyroAxis.Dot(AverageAxis) < -0.7) // Turning the opposite direction - still counts, for our purposes!
                    {
                        AverageAxis.Update(-1f * gyroAxis);
                    }
                    //else Log.Debug(_tag, $"GyroProvider value is > 0.1 rad/s, yet in direction {gyroAxis} rather than {AverageAxis.Average}.");
                }
            }

            protected override bool nextStageCriterion()
            {
                return softStop;
            }

            public void StopAndReturnValues(out Vector3 Axis, out Vector3 UpDirection)
            {
                softStop = true;

                Axis = AverageAxis.Average.Normalize();

                var roughUpDirection = (GravProvider.Vector + UpDirectionInitial).Normalize();
                UpDirection = GetUpDirection(roughUpDirection, Axis);
            }

            public static Vector3 GetUpDirection(Vector3 roughUpDirection, Vector3 axis)
            {
                return (roughUpDirection - axis * (roughUpDirection.Dot(axis))).Normalize();
            }
        }

        public class SeekReadyPositionStage : GestureRecognizerStage
        {
            private StillnessProvider Stillness;
            private Vector3 UpDirection;
            private Vector3Provider Gravity;

            public SeekReadyPositionStage(string label, Vector3 upDirection, bool AutoStart = true) : base(label)
            {
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
                if (AutoStart) Activate(Current.StopToken);
            }

            protected override bool nextStageCriterion()
            {
                Log.Debug("SeekReadyPosition", $"Steadiness {Stillness.StillnessScore:f1}, angle {Gravity.Vector.AngleTo(UpDirection)}.");
                return (Stillness.ReadsMoreThan(-4f)
                    && Gravity.Vector.AngleTo(UpDirection) < 30);
            }
            protected override async Task nextStageActionAsync()
            {
                await Speech.SayAllOf("Begin");
                Current.BeginFirstTumbler(); // Because the specific next step depends on the kind of lock, unlike everything prior to this point.
            }
        }

        public class TumblerFindingStage : GestureRecognizerStage
        {
            protected Tumbler TargetTumbler;
            protected AngleAxisProvider AttitudeProvider;

            public double Angle
            {
                get => SmoothedAngle.Average;
                set
                {
                    if (SmoothedAngle == null) SmoothedAngle = new RollingAverage<double>(3, value); // Quite a light smoothing.
                    else SmoothedAngle.Update(value);
                }
            }
            protected double LastAngle, TargetAngle;
            protected RollingAverage<double> AngleTrend, SmoothedAngle;
            protected int RequiredDirection;
            protected int CurrentDirection { get { return Math.Sign(AngleTrend.Average); } }

            public TumblerFindingStage(string label, Tumbler tgtTumbler, AngleAxisProvider provider) : base(label)
            {
                TargetTumbler = tgtTumbler;

                AttitudeProvider = provider;
                SetUpProvider(AttitudeProvider, allowProviderToPersist: true);

                LastAngle = Angle = AttitudeProvider.Angle;
                Log.Debug("TumblerStage|Ctor", $"Initial angle: {Angle}");
                TargetAngle = tgtTumbler.Angle;
                RequiredDirection = tgtTumbler.Direction;
                // Certain special tumblers - like ResetToZero - have a designated direction of zero, meaning "opposite to current setting."
                //if (tgtTumbler.Direction == 0) RequiredDirection = -1 * Math.Sign(Angle);

                // CurrentDirection is based on AngleTrend.
                AngleTrend = new RollingAverage<double>(10);

                // SmoothedAngle is used for 

                InterimInterval = TimeSpan.FromMilliseconds(20);

                Activate(Current.StopToken);
            }

            protected override bool interimCriterion()
            {
                return true;
            }
            protected override void interimAction()
            {
                LastAngle = Angle;
                Angle = AttitudeProvider.Angle;
                var angleChange = SmallAngleChange(Angle, LastAngle);
                //Log.Debug("TumblerStage|Interim", $"New angle: {Angle:f2}");
                if (Math.Abs(angleChange) > 15.0)
                {
                    Log.Debug("LockOpening|InterimAction", $"Caution - Angle jumped by {angleChange:f1}.");
                }
                AngleTrend.Update(angleChange);
            }

            protected override bool nextStageCriterion()
            {
                return Math.Abs(SmallAngleChange(Angle, TargetAngle)) < Current.LockBeingOpened.RotationAccuracyRequired
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

            public static double SmallAngleChange(double angleA, double angleB, double totalForCircle = 360)
            {
                if (angleA - angleB > totalForCircle * 0.85) return angleA - (totalForCircle + angleB);  // AngleB must be close to -180 & angleA close to 180.
                if (angleA - angleB < totalForCircle * -0.85) return (totalForCircle + angleA) - angleB; // AngleA must be close to -180 & angleB close to 180.
                else return angleA - angleB;
            }
        }
    }

    [Activity(Label = "Atropos :: Safecracking ::", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class SafecrackingActivity : LockedObjectOpeningActivity
    {
        protected int TumblerNumber;
        private static SafecrackingActivity Current
        {
            get
            {
                if (!(CurrentActivity is SafecrackingActivity))
                {
                    Log.Debug("Locks|Activity", $"Casting error - CurrentActivity is {CurrentActivity.GetType().Name}.");
                }
                return (SafecrackingActivity)CurrentActivity;
            }
            set { CurrentActivity = value; }
        }
        protected IEffect ClickFX, ClackFX, SuccessFX, ResetFX; // 'Clack' is the one you're listening for, 'click' is all the others.

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.Safecrack);
            LockBeingOpened = (Lock.Current != Lock.None) ? Lock.Current : Lock.TestSafe;
            EstimatedAxis = new Vector3(-1f, -2f, 3f).Normalize(); // Empirical estimate
        }

        protected override void OnResume()
        {
            ClickFX = new Effect("Safecracking.Click", Resource.Raw._100804_reloadclick) { Volume = 0.25 };
            ClackFX = new Effect("Safecracking.Clack", Resource.Raw._100804_reloadclick);
            SuccessFX = new Effect("Safecracking.Success", Resource.Raw._213996_bolt_opening);
            ResetFX = new Effect("Safecracking.Reset", Resource.Raw._110538_bolt_closing);

            //DialText = FindViewById<TextView>(Resource.Id.vault_dial_text);
            DialBody = FindViewById(Resource.Id.safedial_body);

            TumblerNumber = 0;
            LockBeingOpened.AngleLeftSittingAt = 0;

            base.OnResume();
        }

        protected override void SeekReadyPosition()
        {
            CurrentStage = new SeekReadyPositionStage("Waiting for zero stance", UpVector);
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
            UpVector = LimberingUpStage.GetUpDirection(GravProvider.Vector, AxisVector);
            var provider = new AngleAxisProvider(UpVector, AxisVector);
            SensorProvider.EnsureIsReady(provider).Wait();
            CurrentStage = new VaultTumblerFindingStage("Tumbler 0", Current.LockBeingOpened.Tumblers[0], provider);
        }

        protected override void OnPause()
        {
            ClickFX.Deactivate();
            ClackFX.Deactivate();
            SuccessFX.Deactivate();
            ResetFX.Deactivate();

            base.OnPause();
        }

        protected override void HandleVolumeButtonPressed(object sender, EventArgs e)
        {
            //UpVector = LimberingUpStage.GetUpDirection(GravProvider.Vector, AxisVector);
            //var provider = new AngleAxisProvider(UpVector, AxisVector);
            //await SensorProvider.EnsureIsReady(provider);
            //CurrentStage = new VaultTumblerFindingStage($"Tumbler {TumblerNumber}", Current.LockBeingOpened.Tumblers[TumblerNumber], provider);
            if (CurrentStage is VaultTumblerFindingStage CurrentFindingStage)
            {
                CurrentFindingStage.ButtonIsHeldDown = true;
                CurrentFindingStage.ButtonHasBeenHeldDownFor = 0;
            }            
        }

        protected override void HandleVolumeButtonReleased(object sender, EventArgs e)
        {
            //CurrentStage.Deactivate();
            //Current.LockBeingOpened.AngleLeftSittingAt = (CurrentStage as VaultTumblerFindingStage).Angle;
            if (CurrentStage is VaultTumblerFindingStage CurrentFindingStage)
            {
                CurrentFindingStage.ButtonIsHeldDown = false;
            }
        }

        public class VaultTumblerFindingStage : TumblerFindingStage
        {
            public bool ButtonIsHeldDown;
            public int ButtonHasBeenHeldDownFor;
            public double DialAngle, LastDialAngle;
            protected int LastClicks, CurrentClicks, TargetClicks, TotalClicks;
            protected double DegreesBetweenClicks;
            protected bool? Overshot = false;

            public VaultTumblerFindingStage(string label, Tumbler tgtTumbler, AngleAxisProvider oProvider, bool buttonIsHeldDown = false, double dialAngle = 0.0) 
                : base(label, tgtTumbler, oProvider)
            {
                ButtonIsHeldDown = buttonIsHeldDown;
                DialAngle = LastDialAngle = dialAngle;
                DegreesBetweenClicks = Current.LockBeingOpened.DegreesBetweenClicks;
                TotalClicks = (int)(360 / DegreesBetweenClicks);
                LastClicks = CurrentClicks = Clicks(DialAngle);
                TargetClicks = Clicks(tgtTumbler.Angle);
                Log.Debug("Safecracking|TumblerFinding", $"Starting to search for {label}");
            }

            protected int Clicks(double angle)
            {
                //return (int)Math.Round((angle % 360) / DegreesBetweenClicks) % TotalClicks;
                return (int)Math.Truncate(SmallAngleChange(angle, 0) / DegreesBetweenClicks);
            }

            protected override async void interimAction()
            {
                base.interimAction();

                if (ButtonIsHeldDown)
                {
                    ButtonHasBeenHeldDownFor++;
                    LastDialAngle = DialAngle;
                    DialAngle += SmallAngleChange(Angle, LastAngle);
                }

                LastClicks = Clicks(LastDialAngle);
                CurrentClicks = Clicks(DialAngle);

                var deltaClicks = (int)SmallAngleChange(CurrentClicks, LastClicks, TotalClicks);
                Current.RunOnUiThread(() => 
                {
                    //Current.DialText.Text = $"{(CurrentClicks + TotalClicks) % TotalClicks:d2}";
                    Current.DialBody.Rotation = -1f * (float)DialAngle;
                });

                // No change? No problem, then we're done here.
                if (LastClicks == CurrentClicks) return;

                // Otherwise, we need to play X clicks over Y milliseconds, to represent the clicks just passed.
                var intervals = (int)Math.Round(InterimInterval.TotalMilliseconds / Math.Abs(deltaClicks));
                var increment = Math.Sign(AngleTrend);
                //for (int clickNum = LastClicks; clickNum * increment < (LastClicks + deltaClicks) * increment; clickNum += increment) // The "lastClicks + deltaClicks" could be "currentClicks" except for the +180/-180 problem.
                for (int clickNum = LastClicks; clickNum * increment < CurrentClicks * increment; clickNum += increment)
                {
                    // Play the appropriate sound effect
                    if (clickNum == TargetClicks - increment && Overshot == false && (increment == RequiredDirection || RequiredDirection == 0))
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
                    if (Sign(AngleTrend) != RequiredDirection && Abs(AngleTrend) > 0.2 && RequiredDirection != 0 && ButtonHasBeenHeldDownFor >= 3)
                    {
                        Log.Debug("Safecracking|InterimAction", $"Turned backward into {clickNum} ({Angle:f2}), target {TargetClicks} ({TargetAngle:f2}) at AngleTrend {AngleTrend.Average:f3}.");
                        Overshot = true;
                        if (TargetTumbler != Tumbler.ResetToZero) Speech.Say("Backward turn");
                    }
                    // (B) Overshooting the combination by at least one (make that two) clicks
                    //else if (clickNum * increment > TargetClicks * increment + 1)
                    else if (DialAngle * increment > TargetAngle * increment + 10) // Make that ten degrees.
                    {
                        Log.Debug("Safecracking|InterimAction", $"Overshot into {clickNum} ({DialAngle:f2}), target {TargetClicks} ({TargetAngle:f2}) at AngleTrend {AngleTrend.Average:f3}.");
                        Overshot = true;
                        if (TargetTumbler != Tumbler.ResetToZero) Speech.Say("Overshot");
                    }

                    await Task.Delay(intervals);
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
                    if (TargetTumbler != Tumbler.ResetToZero)
                    {
                        if (Current.TumblerNumber > 0) Current.LockBeingOpened.NumberOfAttempts++;
                        //Task.Delay(500).ContinueWith(t => Speech.Say("Overshot")).LaunchAsOrphan();
                    }
                    CurrentStage = new VaultTumblerFindingStage("Resetting to zero", Tumbler.ResetToZero, AttitudeProvider, ButtonIsHeldDown, DialAngle);
                }
                else if (object.ReferenceEquals(TargetTumbler, Tumbler.ResetToZero))
                {
                    Current.TumblerNumber = 0;
                    CurrentStage = new VaultTumblerFindingStage("Tumbler 0", Current.LockBeingOpened.Tumblers[0], AttitudeProvider, ButtonIsHeldDown, DialAngle);
                }
                else if (TargetTumbler.NextTumbler != Tumbler.EndOfLock)
                {
                    Current.TumblerNumber++;
                    CurrentStage = new VaultTumblerFindingStage($"Tumbler {Current.TumblerNumber}", TargetTumbler.NextTumbler, AttitudeProvider, ButtonIsHeldDown, DialAngle);
                }
                else
                {
                    await Current.SuccessFX.PlayToCompletion(useSpeakers: true);
                    Current.LockBeingOpened.AnnounceLockOpened();
                    await Speech.SayAllOf("Lock is open; well done.", useSpeakerMode: false);
                    Current.LockBeingOpened = Lock.None;
                    Current.Finish();
                }
            }

            protected override bool abortCriterion()
            {
                return base.abortCriterion();
            }

            protected override void abortAction()
            {
                base.abortAction();
                Current.TumblerNumber = 0;
            }
        }
    }

    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.ReverseLandscape)]
    public class LockPickingActivity : LockedObjectOpeningActivity
    {
        protected List<Tumbler> TumblersNotPicked;
        public Tumbler CurrentTumblerLifted = Tumbler.None;
        private static LockPickingActivity Current { get { return (LockPickingActivity)CurrentActivity; } set { CurrentActivity = value; } }
        protected IEffect LockpickingSFX, TumblerLiftingSFX, TumblerDroppedSFX, SuccessSFX;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.Lockpick); // TODO - change this!
            LockBeingOpened = (Lock.Current != Lock.None) ? Lock.Current : Lock.TestLock;
            EstimatedAxis = new Vector3(0f, -2.5f, 1f).Normalize();
            //TumblersPicked = Enumerable.Repeat(false, Current.LockBeingOpened.Tumblers.Count).ToArray();
            TumblersNotPicked = LockBeingOpened.Tumblers.ToArray().ToList(); // Make a copy.
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
            CurrentStage = new SeekReadyPositionStage("Waiting for zero stance", UpVector);
        }

        protected override void BeginFirstTumbler()
        {
            RunOnUiThread(() =>
            {
                FindViewById(Resource.Id.vault_notification).Visibility = ViewStates.Gone;
                //DialText = FindViewById<TextView>(Resource.Id.vault_dial_text);
                //DialText.Visibility = ViewStates.Visible;
            });
            //LockpickingSFX.Play(1.0, true, useSpeakers: true);
            Log.Debug("Lock|BeginFirstTumbler", $"Before: GravProvider.Vector is {GravProvider.Vector:f2}, UpVector is {UpVector:f2}, angle between them is {GravProvider.Vector.AngleTo(UpVector)}");
            UpVector = LimberingUpStage.GetUpDirection(GravProvider.Vector, AxisVector);
            Log.Debug("Lock|BeginFirstTumbler", $"After: GravProvider.Vector is {GravProvider.Vector:f2}, UpVector is {UpVector:f2}, angle between them is {GravProvider.Vector.AngleTo(UpVector)}");
            var provider = new AngleAxisProvider(UpVector, AxisVector);
            Log.Debug("Lock|BeginFirstTumbler", $"After after: angle axis provider is showing {provider.Angle:f2} degrees.");
            SensorProvider.EnsureIsReady(provider).Wait();
            Log.Debug("Lock|BeginFirstTumbler", $"After after after: angle axis provider is showing {provider.Angle:f2} degrees.");
            CurrentStage = new LockTumblerFindingStage("Tumbler Finding", LockBeingOpened.Tumblers[0], provider);
        }

        protected override void HandleVolumeButtonPressed(object sender, EventArgs e)
        {
            if (CurrentStage is LockTumblerFindingStage TumblerFinding)
            {
                if (TumblerFinding.ActiveTumbler != Tumbler.None)
                {
                    CurrentTumblerLifted = TumblerFinding.ActiveTumbler;
                    LockpickingSFX.Stop();
                    CurrentStage = new LockTumblerLiftingStage("Tumbler Lifting", TumblerFinding.ActiveTumbler, TumblerFinding.Provider);
                }
                else TumblerFinding.VolumeButtonIsPressed = true; // Causes it to suppress the tactile feedback while depressed.
            }            
        }

        protected override async void HandleVolumeButtonReleased(object sender, EventArgs e)
        {
            if (CurrentStage is LockTumblerFindingStage CurrentFindingStage)
            {
                CurrentFindingStage.VolumeButtonIsPressed = false;
            }
            else if (CurrentStage is LockTumblerLiftingStage CurrentLiftingStage)
            {
                CurrentLiftingStage.Deactivate();
                if (Current.LockpickingSFX.IsPlaying) Current.LockpickingSFX.Stop(); // OUGHT to happen automatically as part of the above Deactivate; not clear why it doesn't.
                // Did you get the angle close enough?
                if (Math.Abs(TumblerFindingStage.SmallAngleChange(CurrentLiftingStage.Angle, 0)) < 3 * LockBeingOpened.RotationAccuracyRequired)
                {
                    TumblersNotPicked.Remove(CurrentLiftingStage.tumblerUnderway);
                    // Is that the last of them?
                    if (TumblersNotPicked.Count == 0) // Congrats!
                    {
                        await SuccessSFX.PlayToCompletion(useSpeakers: true);
                        LockBeingOpened.AnnounceLockOpened();
                        await Speech.SayAllOf("Lock is open; well done.", useSpeakerMode: false);
                        Current.LockBeingOpened = Lock.None;
                        Finish();
                    }
                    else // Still more tumbler(s) to go.
                    {
                        Speech.Say("Gotcha!");
                        CurrentStage = new LockTumblerFindingStage("Keep on trucking", Tumbler.None, CurrentLiftingStage.DataProvider as AngleAxisProvider);
                    }
                }
                else // Nope, not close enough to zero.
                {
                    CurrentLiftingStage.actOnSlippedTumbler("Missed"); // This will automatically trigger a CurrentStage = new LockTumblerFindingStage as above.
                }
            }
        }

        public class LockTumblerFindingStage : TumblerFindingStage
        {
            public bool VolumeButtonIsPressed = false;
            public Tumbler ActiveTumbler = Tumbler.None;
            public AngleAxisProvider Provider;
            public LockTumblerFindingStage(string label, Tumbler tgtTumbler, AngleAxisProvider oProvider = null)
                : base(label, tgtTumbler, oProvider)
            {
                Provider = oProvider;
                Log.Debug("Lockpicking|TumblerFinding", $"Starting to search for tumblers.");
            }

            protected override void startAction()
            {
                Current.LockpickingSFX.Play(new SoundOptions() { Looping = true, UseSpeakers = true, CancelToken = StopToken });
            }


            protected bool lastSampleWasVibrated = false;

            protected override void interimAction()
            {
                base.interimAction();
                var AnglesToTumblers = Current.TumblersNotPicked.Select(tumbler => Math.Abs(SmallAngleChange(tumbler.Angle, Angle)));
                var minAngle = AnglesToTumblers.Min();

                // Vibrate if within alpha of the target angle.
                if (minAngle < Current.LockBeingOpened.RotationAccuracyRequired && !VolumeButtonIsPressed)
                {
                    if (!lastSampleWasVibrated) Plugin.Vibrate.CrossVibrate.Current.Vibration(5);
                    lastSampleWasVibrated = true;
                }
                else lastSampleWasVibrated = false;

                // (Silently) accept it as legit if within 2*alpha.
                if (minAngle < 2 * Current.LockBeingOpened.RotationAccuracyRequired && !VolumeButtonIsPressed)
                {
                    ActiveTumbler = Current.TumblersNotPicked[AnglesToTumblers.ToList().FindIndex(a => a == minAngle)];
                }
                else ActiveTumbler = Tumbler.None;

                //Log.Debug("Lockpicking|TumblerFinding", $"Aiming for {TargetAngle}, currently at {Angle}.");
                //Log.Debug("Lockpicking|TumblerFinding", $"Angle to nearest pin {minAngle:f2}");
            }
            protected override bool nextStageCriterion()
            {
                return false;
            }

            protected override void abortAction()
            {
                base.abortAction();
                Current.TumblersNotPicked = Current.LockBeingOpened.Tumblers;
            }
        }

        public class LockTumblerLiftingStage : TumblerFindingStage
        {
            private RollingAverage<float> AngleRateOfChange;
            public Tumbler tumblerUnderway;

            public LockTumblerLiftingStage(string label, Tumbler tgtTumbler, AngleAxisProvider oProvider = null)
                : base(label, Tumbler.PinMoveTarget, oProvider)
            {
                Log.Debug("Lockpicking|TumblerLifting", $"Lifting {label}, starting at angle {oProvider.Angle}.");
                AngleRateOfChange = new RollingAverage<float>(5, 0f);
                tumblerUnderway = tgtTumbler;
            }

            protected override void startAction()
            {
                Current.TumblerLiftingSFX.Play(new SoundOptions() { Looping = true, UseSpeakers = true, CancelToken = StopToken });
            }

            protected override void interimAction()
            {
                base.interimAction();
                var angleChangeAmount = Abs(SmallAngleChange(Angle, LastAngle));
                AngleRateOfChange.Update((float)angleChangeAmount / (float)InterimInterval.TotalSeconds);
                Plugin.Vibrate.CrossVibrate.Current.Vibration((int)Math.Max(1, 3 * AngleRateOfChange / Current.LockBeingOpened.MaxRotationRateInLiftingPhase));
            }

            protected override bool nextStageCriterion()
            {
                return AngleRateOfChange > Current.LockBeingOpened.MaxRotationRateInLiftingPhase;
            }

            protected override void nextStageAction()
            {
                // Did we drop the tumbler (by moving too fast)?
                if (AngleRateOfChange > Current.LockBeingOpened.MaxRotationRateInLiftingPhase)
                {
                    actOnSlippedTumbler("Slipped");
                }
            }

            public void actOnSlippedTumbler(string exclamation)
            {
                Plugin.Vibrate.CrossVibrate.Current.Vibration(10);
                Current.TumblerLiftingSFX.Stop();
                Current.TumblerDroppedSFX.Play();
                Speech.Say($"{exclamation}.  Recheck your tumblers.");

                var i = 0;
                foreach (var tumbler in Current.LockBeingOpened.Tumblers)
                {
                    if (!Current.TumblersNotPicked.Contains(tumbler) && Res.Random < 0.5) // 50% chance of losing each tumbler of progress made to date
                    {
                        Current.TumblersNotPicked.Add(tumbler);
                        i++;
                    }
                }
                Log.Debug("ActOnSlippedTumbler", $"Slipped up; lost {i} additional tumblers. Angle was {Angle:f2}, angle rate was {AngleRateOfChange.Average:f2} deg/sec.");

                Current.LockBeingOpened.NumberOfAttempts++;
                CurrentStage = new LockTumblerFindingStage("Try again to find tumblers", Current.LockBeingOpened.Tumblers[0], AttitudeProvider);
            }

            protected override void abortAction()
            {
                base.abortAction();
                Current.TumblersNotPicked = Current.LockBeingOpened.Tumblers;
            }
        }
    }
}