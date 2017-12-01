//
//using System;
//using System.Text;
//using System.Linq;
//using System.Collections.Generic;

//using Android;
//using Android.App;
//using Android.Nfc;
//using Android.OS;
//using Android.Widget;
//using Android.Util;
//using DeviceMotion.Plugin;
//using DeviceMotion.Plugin.Abstractions;
////using Accord.Math;
////using Accord.Statistics;
//using Android.Content;
//using System.Threading.Tasks;
//using System.Numerics;
//using Vector3 = System.Numerics.Vector3;
//using Android.Views;

//using static System.Math;
//using Log = Android.Util.Log; // Disambiguating with Math.Log( )... heh!

//namespace Atropos.Locks
//{
//    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
//    public class LockTrainingActivity : LockedObjectOpeningActivity
//    {
//        private TextView currentSignalsDisplay, bestSignalsDisplay, resultsDisplay;
//        private EditText lockNameTextbox;
//        private List<Button> lockButtons = new List<Button>();
//        private TextView tumblerCountDisplay;
//        private Button undoTumblerButton, inscribeButton, setLockNameButton;

//        protected static LockTrainingActivity Current { get { return (LockTrainingActivity)CurrentActivity; } set { CurrentActivity = value; } }

//        protected override void OnCreate(Bundle savedInstanceState)
//        {
//            base.OnCreate(savedInstanceState);
//            SetContentView(Resource.Layout.LockTraining);

//            currentSignalsDisplay = FindViewById<TextView>(Resource.Id.current_signals_text);
//            bestSignalsDisplay = FindViewById<TextView>(Resource.Id.best_signals_text);
//            resultsDisplay = FindViewById<TextView>(Resource.Id.result_text);
            
//            SetUpLockButtons();

//            // See if the current focus is already in our (local, for now) library, and load it if so.  Otherwise, take us to calibration.
//            var focusString = Res.SpecificTags.Get(InteractionLibrary.CurrentSpecificTag);
//            if (focusString != null && focusString.Length > 0)
//            {
//                ThePlayersFocus = Focus.FromString(focusString, InteractionLibrary.CurrentSpecificTag);
//            }
//            else if (InteractionLibrary.CurrentSpecificTag == InteractionLibrary.LockTeaching.Name + "0000")
//            {
//                Focus.InitMasterFocus();
//                InteractionLibrary.CurrentSpecificTag = Focus.MasterFocus.TagID;
//                Log.Info("Training", "Using master focus.");
//                ThePlayersFocus = Focus.MasterFocus;
//            }
//            else
//            {
//                ThePlayersFocus = new Focus(InteractionLibrary.CurrentSpecificTag);
//            }

//            CurrentStage = GestureRecognizerStage.NullStage;
//        }

//        protected override async void OnResume()
//        {
//            await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: true);
//        }

//        protected override void OnPause()
//        {
//            base.OnPause();
//        }

//        private void SetUpLockButtons()
//        {
//            var layoutpanel = FindViewById<LinearLayout>(Resource.Id.Lock_casting_layoutpane);

//            foreach (string lockName in MasterLockLibrary.lockNames?.DefaultIfEmpty() ?? new string[0])
//            {
//                var lock = MasterLockLibrary.Get(lockName);
//                var lockButton = new Button(this);
//                lockButton.SetText(lockName + " (Retrain)", TextView.BufferType.Normal);
//                lockButton.SetPadding(20,20,20,20);
//                layoutpanel.AddView(lockButton);

//                lockButton.Click += async (o, e) =>
//                {
//                    if (LockBeingTrained != null) return; // Debouncing, basically.
//                    LockBeingRetrained = lock;
//                    LockBeingTrained = new Lock(lockName);
//                    foreach (Button btn in lockButtons) btn.Visibility = ViewStates.Gone;
//                    setLockNameButton.Text = "Erase lock";
//                    lockNameTextbox.Text = lockName;
//                    lockNameTextbox.Focusable = false;
//                    CheckTumblerCount();
//                    await Speech.SayAllOf($"Retraining {lockName}.");
//                    CurrentStage = new Lock_Training_TutorialStage($"Retraining {lockName}", ThePlayersFocus, true);
//                };
//            }

//            lockNameTextbox = FindViewById<EditText>(Resource.Id.lock_name_textbox);
//            setLockNameButton = FindViewById<Button>(Resource.Id.Set_lock_name_button);
//            tumblerCountDisplay = FindViewById<TextView>(Resource.Id.tumbler_count_display);
//            undoTumblerButton = FindViewById<Button>(Resource.Id.undo_tumbler_button);
//            inscribeButton = FindViewById<Button>(Resource.Id.inscribe_lock_button);

//            lockNameTextbox.KeyPress += (object sender, View.KeyEventArgs e) =>
//            {
//                e.Handled = false;
//                if (e.Event.Action == KeyEventActions.Down && e.KeyCode == Keycode.Enter && lockNameTextbox.Text.Length > 0)
//                {
//                    setLockNameButton.CallOnClick();
//                }
//                if (lockNameTextbox.Text.Length > 0) setLockNameButton.Text = "Train";
//                else setLockNameButton.Text = "Random";
//            };
//            lockNameTextbox.ClearFocus(); // Not working, dunno why.

//            setLockNameButton.Click += async (o, e) =>
//            {
//                if (LockBeingRetrained != null)
//                {
//                    if (setLockNameButton.Text == "Erase lock") { setLockNameButton.Text = "Confirm erasure"; return; }
//                    else if (setLockNameButton.Text == "Confirm erasure")
//                    {
//                        MasterLockLibrary.Erase(LockBeingRetrained.LockName);
//                        ThePlayersFocus.ForgetLock(LockBeingRetrained.LockName);
//                        await Speech.SayAllOf($"Deleting {LockBeingRetrained.LockName} from the master library.");
//                        CurrentStage.Deactivate();
//                        CurrentStage = GestureRecognizerStage.NullStage;
//                        Current.Finish();
//                        return;
//                    }
//                }
//                LockBeingRetrained = null;
//                if (lockNameTextbox.Text.Length == 0) lockNameTextbox.Text = GenerateRandomLockName();
//                await Task.Delay(100); // Let the screen update.
//                LockBeingTrained = new Lock(lockNameTextbox.Text);
//                foreach (Button btn in lockButtons) btn.Visibility = ViewStates.Gone;
//                lockNameTextbox.Focusable = false;
//                CheckTumblerCount();
//                await Speech.SayAllOf($"Training {LockBeingTrained.LockName}.");
//                CurrentStage = new Lock_Training_TutorialStage($"Init training for {LockBeingTrained.LockName}", ThePlayersFocus, true);
//            };

//            undoTumblerButton.Click += async (o, e) => 
//            {
//                if (LockBeingTrained.Tumblers.Count == 0)
//                {
//                    foreach (Button btn in lockButtons) btn.Enabled = true;
//                    lockNameTextbox.Text = "";
//                    lockNameTextbox.Focusable = true;
//                    setLockNameButton.Enabled = true;
//                    setLockNameButton.Text = "Random";
//                    LockBeingRetrained = null;
//                    LockBeingTrained = null;
//                    CurrentStage.Deactivate();
//                    CurrentStage = GestureRecognizerStage.NullStage;
//                    await Speech.SayAllOf("Aborting lock training.");
//                }
//                else
//                {
//                    LockBeingTrained.UndoAddTumbler();
//                    await Speech.SayAllOf("Removing most recent tumbler.");
//                }
//                CheckTumblerCount();
//            };

//            var feedbackSFXbtn = FindViewById<Button>(Resource.Id.lock_feedback_sfx_button);
//            var progressSFXbtn = FindViewById<Button>(Resource.Id.lock_progress_sfx_button);
//            var successSFXbtn = FindViewById<Button>(Resource.Id.lock_success_sfx_button);

//            if (!MasterLockLibrary.GetSFXReadyTask().Wait(5000))
//                Log.Error("Lock training", "Can't prep the buttons (as is, anyway) without our SFX loaded, which doesn't seem to be happening.");
//            var feedbackSFXoptions = new SimpleCircularList<string>("Magic.Ethereal", 
//                "Magic.Aura", "Magic.DeepVenetian", "Magic.InfiniteAubergine", "Magic.Ommm", "Magic.AfricanDrums", 
//                "Magic.Rommble", "Magic.MidtonePianesque", "Magic.FemReverbDSharp","Magic.FemReverbCSharp",
//                "Magic.FemReverbF", "Magic.FemReverbE", "Magic.AlienTheremin",
//                "Magic.TrompingBuzzPulse", "Magic.GrittyDrone", "Magic.Galewinds", "Magic.NanobladeLoop", 
//                "Magic.ViolinLoop", "Magic.StrongerThanTheDark", "Magic.MelodicPad");
//            var progressSFXoptions = new SimpleCircularList<string>(MasterLockLibrary.LockSFX.Keys.Where(sfx => !feedbackSFXoptions.Contains(sfx)).DefaultIfEmpty().ToArray());
//            var successSFXoptions = new SimpleCircularList<string>(MasterLockLibrary.CastingResults.Keys.ToArray());
//            while (progressSFXoptions.Next != MasterLockLibrary.defaultProgressSFXName) { } // Cycle the list to the correct starting point.
//            while (successSFXoptions.Next != "Play " + MasterLockLibrary.defaultSuccessSFXName) { } // Cycle the list to the correct starting point.

//            inscribeButton.Click += async (o, e) =>
//            {
//                if (inscribeButton.Text == "Inscribe Lock")
//                {
//                    // Halt ongoing processeses
//                    MasterLockLibrary.LockFeedbackSFX.Deactivate();
//                    CurrentStage.Deactivate();
//                    CurrentStage = GestureRecognizerStage.NullStage;

//                    // Display SFX modification buttons
//                    feedbackSFXbtn.Visibility = ViewStates.Visible;
//                    progressSFXbtn.Visibility = ViewStates.Visible;
//                    successSFXbtn.Visibility = ViewStates.Visible;

//                    IEffect sampleSFX = null;
//                    Func<SimpleCircularList<string>, Button, string, EventHandler> HandlerFactory
//                        = (circList, btn, label) =>
//                        {
//                            return (ob, ev) =>
//                            {
//                                sampleSFX?.Stop();
//                                btn.Text = $"{label} ('{circList.Next}')";
//                                if (MasterLockLibrary.LockSFX.ContainsKey(circList.Current.Split(' ').Last()))
//                                {
//                                    sampleSFX = MasterLockLibrary.LockSFX[circList.Current.Split(' ').Last()];
//                                    sampleSFX.Play();
//                                }
//                                else
//                                {
//                                }
//                            };
//                        };
//                    feedbackSFXbtn.Click += HandlerFactory(feedbackSFXoptions, feedbackSFXbtn, "Feedback SFX");
//                    progressSFXbtn.Click += HandlerFactory(progressSFXoptions, progressSFXbtn, "Progress SFX");
//                    successSFXbtn.Click += HandlerFactory(successSFXoptions, successSFXbtn, "Success Func");

//                    inscribeButton.Text = "Finish Inscribing";
//                }
//                else
//                {
//                    feedbackSFXbtn.Visibility = ViewStates.Gone;
//                    progressSFXbtn.Visibility = ViewStates.Gone;
//                    successSFXbtn.Visibility = ViewStates.Gone;

//                    LockBeingTrained.CastingResult = MasterLockLibrary.CastingResults[successSFXoptions.Current];
//                    foreach (var tumbler in LockBeingTrained.Tumblers)
//                    {
//                        tumbler.FeedbackSFXName = feedbackSFXoptions.Current;
//                        tumbler.ProgressSFXName = progressSFXoptions.Current;
//                    }
//                    MasterLockLibrary.Inscribe(LockBeingTrained);
//                    ThePlayersFocus.LearnLock(LockBeingTrained);
                    
//                    //ResetLock();
//                    if (LockBeingRetrained == null)
//                        await Speech.SayAllOf($"Adding {LockBeingTrained.LockName} to the master library.");
//                    else await Speech.SayAllOf($"Updating lock listing for {LockBeingTrained.LockName}.");
//                    Log.Info("LockTraining", $"Here's the lock string for copy-and-pasting as a constant: {LockBeingTrained.ToString()}");
//                    Current.Finish();
//                }
                
//            };
//        }

//        private void CheckTumblerCount()
//        {
//            bool oneTumbler = LockBeingTrained.Tumblers.Count > 0;
//            inscribeButton.Enabled = oneTumbler;
//            undoTumblerButton.Text = (LockBeingTrained != null) ?
//                                        ((oneTumbler) ? "Undo" : "Delete") :
//                                        ("Undo / Delete");
//            string wasBefore = (LockBeingRetrained != null) ? $" (was {LockBeingRetrained.Tumblers.Count})" : "";
//            string isNow = (oneTumbler) ? LockBeingTrained.Tumblers.Count.ToString() + wasBefore : "None";
//            tumblerCountDisplay.Text = $"Tumblers: {isNow}";
//        }

//        private string GenerateRandomLockName()
//        {
//            string[] Elements = { "Manna", "Power", "Fire", "Ice", "Acid", "Spark", "Toxic", "Chocolate", "Mental", "Shard", "Gravity", "Illusion", "Deception" };
//            string[] LockTypes = { "Bolt", "Dart", "Lance", "Ball", "Wave", "Blast", "Stream", "Pachinko", "Snooker", "Shield", "Aura", "Vortex", "Sneeze" };
//            return Elements.GetRandom() + " " + LockTypes.GetRandom();
//        }

//        public void RelayMessage(string message, bool useSecondaryDisplay = false)
//        {
//            try
//            {
//                if (!useSecondaryDisplay) RunOnUiThread(() =>
//                        {
//                            currentSignalsDisplay.Text = message;
//                        });
//                else RunOnUiThread(() => { bestSignalsDisplay.Text = message; });
//            }
//            catch (Exception)
//            {
//                throw;
//            }
//        }

//        public class Lock_Training_TutorialStage : GestureRecognizerStage
//        {
//            private Focus Implement;
//            private Lock LockBeingTrained;
//            private StillnessProvider Stillness;
//            private FrameShiftedOrientationProvider AttitudeProvider;
//            private Task sayIt;

//            public Lock_Training_TutorialStage(string label, Focus Focus, bool AutoStart = false) : base(label)
//            {
//                Implement = Focus;
//                Stillness = new StillnessProvider();
//                SetUpProvider(Stillness);
//                Stillness.StartDisplayLoop(Current, 1000);
//                LockBeingTrained = Current.LockBeingTrained;

//                AttitudeProvider = new GravityOrientationProvider();
//                AttitudeProvider.Activate();

//                if (AutoStart) Activate();
//            }

//            protected override async void startAction()
//            {
//                sayIt = Speech.SayAllOf($"Hold device at each position until you hear the tone.  Take your zero stance to begin.", volume: 0.5);
//                await sayIt;
//            }

//            protected override bool interimCriterion()
//            {
//                return Stillness.IsItDisplayUpdateTime();
//            }

//            protected override void interimAction()
//            {
//                Stillness.DoDisplayUpdate();
//            }

//            protected override async Task<bool> nextStageCriterionAsync()
//            {
//                await sayIt;
//                return (Stillness.ReadsMoreThan(8f));// && sayIt.Status == TaskStatus.RanToCompletion);
//            }

//            protected override async Task nextStageActionAsync()
//            {
//                await AttitudeProvider.SetFrameShiftFromCurrent();
//                LockBeingTrained.ZeroStance = AttitudeProvider.FrameShift;

//                await Speech.SayAllOf("Begin");
//                CurrentStage = new TumblerTrainingStage($"Tumbler 0", Implement, AttitudeProvider);
//            }
//        }

//        public class TumblerTrainingStage : GestureRecognizerStage
//        {
//            private Focus Implement;
//            public StillnessProvider Stillness;
//            private float Volume;
//            private AdvancedRollingAverageQuat AverageAttitude;
//            private OrientationSensorProvider AttitudeProvider;
//            private Quaternion lastOrientation;

//            public TumblerTrainingStage(string label, Focus Focus, OrientationSensorProvider Provider = null) : base(label)
//            {
//                Implement = Focus;
//                Res.DebuggingSignalFlag = true;

//                Stillness = new StillnessProvider();
//                Stillness.StartDisplayLoop(Current, 750);

//                SetUpProvider(Stillness);
//                Volume = 0.1f;

//                AverageAttitude = new AdvancedRollingAverageQuat(timeFrameInPeriods: 15);
//                AttitudeProvider = Provider ?? new GravityOrientationProvider(Implement.FrameShift);
//                AttitudeProvider.Activate();

//                if (Current.LockBeingTrained.Tumblers.Count == 0) lastOrientation = Quaternion.Identity;
//                else lastOrientation = Current.LockBeingTrained.Tumblers.Last().Orientation;

//                Activate();
//            }

//            protected override void startAction()
//            {
//                MasterLockLibrary.LockFeedbackSFX.Play(Volume, true);
//            }

//            protected override bool nextStageCriterion()
//            {
//                // Must (A) satisfy stillness criterion, (B) not be right away after the last one, 
//                // and (C) be at least thirty degrees away from previous (if any).  Also, if the attitude provider
//                // has a frame shift but that frame shift is [still] the identity quaternion - meaning the awaitable
//                // hasn't come back and set it to something nonzero - then we are not allowed to proceed regardless.
//                if (Stillness.StillnessScore + Math.Sqrt(Stillness.RunTime.TotalSeconds) > 8f)
//                {
//                    return (AttitudeProvider.Quaternion.AngleTo(lastOrientation) > 30.0f); // Done as a separate clause for debugging reasons only.
//                }
//                return false;
//            }
//            protected override async void nextStageAction()
//            {
//                try
//                {
//                    MasterLockLibrary.LockFeedbackSFX.Stop();
//                    await Task.Delay(150);
//                    MasterLockLibrary.LockProgressSFX.Play();
//                    await Task.Delay(1000); // Give a moment to get ready.
                    
//                    Current.LockBeingTrained.AddTumbler(new Tumbler(AverageAttitude, Stillness.StillnessScore, AverageAttitude.StdDev));
//                    Current.CheckTumblerCount();

//                    var EulerAngles = AverageAttitude.ToEulerAngles();
//                    //Log.Debug("LockTraining", $"Saved tumbler at yaw {EulerAngles.X:f2}, pitch {EulerAngles.Y}, and roll {EulerAngles.Z} from zero stance.");
//                    Log.Debug("LockTraining", $"Saved {((GestureRecognizerStage)CurrentStage).Label} at {EulerAngles:f1} from zero stance.  That's {AverageAttitude.AngleTo(lastOrientation):f0} degrees from the last one.  Baselines are {Stillness.StillnessScore:f2} for stillness, {AverageAttitude.StdDev:f2} for orientation.");
//                    CurrentStage = new TumblerTrainingStage($"Tumbler {Current.LockBeingTrained.Tumblers.Count}", Implement, AttitudeProvider);
//                }
//                catch (Exception e)
//                {
//                    Log.Error("Tumbler training stage progression", e.Message);
//                    throw;
//                }
//            }

//           protected override bool interimCriterion()
//            {
//                Stillness.IsItDisplayUpdateTime(); // Updates max and min values.
//                return true;
//            }

//            protected override void interimAction()
//            {
//                Volume = (float)Exp(-(Sqrt((15 - Stillness) / 2) - 0.5));
//                MasterLockLibrary.LockFeedbackSFX.SetVolume(Volume);

//                if (Stillness.IsItDisplayUpdateTime())
//                {
//                    Stillness.DoDisplayUpdate();
//                }

//                AverageAttitude.Update(AttitudeProvider);
//            }
//        }

//    }
//}