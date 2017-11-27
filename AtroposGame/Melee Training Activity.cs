
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

using static System.Math;
using Log = Android.Util.Log; // Disambiguating with Math.Log( )... heh!
using Android.Views.InputMethods;

namespace com.Atropos
{
    /// <summary>
    /// This is the activity started when we detect a "train form gestures" NFC tag.
    /// Players will also be able to enter this mode with a specific "form" - to be written later
    /// (probably via such a "train form gestures" tag!).
    /// </summary>
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class MeleeTrainingActivity : BaseActivity_Portrait, IRelayMessages
    {
        private Sword ThePlayersSword;
        private Form FormBeingTrained, FormBeingRetrained;
        private TextView currentSignalsDisplay, bestSignalsDisplay, resultsDisplay;
        private EditText formNameTextbox, paramAbox, paramBbox, paramCbox;
        private List<Button> formButtons = new List<Button>();
        private TextView strokeCountDisplay;
        private Button reassessButton, pauseButton, finalizeButton, setFormNameButton;
        private bool? AppendMode = null; // Null means it's not retraining at all; true means add to the existing set, false means start over.
        private const string RetrainEnGardeText = "Retrain En Garde";

        protected static MeleeTrainingActivity Current { get { return (MeleeTrainingActivity)CurrentActivity; } set { CurrentActivity = value; } }

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.MeleeTraining);

            currentSignalsDisplay = FindViewById<TextView>(Resource.Id.current_signals_text);
            bestSignalsDisplay = FindViewById<TextView>(Resource.Id.best_signals_text);
            resultsDisplay = FindViewById<TextView>(Resource.Id.result_text);
            
            SetUpFormButtons();
            
            // See if the current sword is already in our (local, for now) library, and load it if so.  Otherwise, create one.
            var swordString = Res.SpecificTags.Get(InteractionLibrary.CurrentSpecificTag);
            if (swordString != null && swordString.Length > 0)
            {
                ThePlayersSword = Sword.FromString(swordString, InteractionLibrary.CurrentSpecificTag);
                CurrentStage = GestureRecognizerStage.NullStage;
                return;
            }
            else if (InteractionLibrary.CurrentSpecificTag == InteractionLibrary.MeleeTeaching.Name + "0000")
            {
                Sword.InitMasterSword();
                InteractionLibrary.CurrentSpecificTag = Sword.MasterSword.TagID;
                Log.Info("Training", "Using master sword.");
                ThePlayersSword = Sword.MasterSword;
            }
            else
            {
                ThePlayersSword = new Sword(InteractionLibrary.CurrentSpecificTag);
            }

            if (ThePlayersSword.EnGardeOrientation == null || ThePlayersSword.EnGardeOrientation.Average.AngleTo(Quaternion.Identity) < 1)
            {
                await Speech.SayAllOf("No pre-existing ahn garde stance found.  Please take and hold your ahn garde stance to begin.");
                CurrentStage = new DefineEnGardeStage("Defining new en garde", true);
            }
            else
            {
                await Speech.SayAllOf("Confirm your saved ahn garde stance to begin.");
                setFormNameButton.Text = RetrainEnGardeText;
                CurrentStage = new EnGardeStage("Confirm en garde", "Okay. Use onscreen buttons to train specific forms.", GestureRecognizerStage.NullStage, true);
            }
            
        }

        protected override async void OnResume()
        {
            await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: true);

            // Hiding the keyboard - it appears by default because formNameTextbox is the only focusable element on screen (?? I think)
            InputMethodManager inputManager = (InputMethodManager)Application.Context.GetSystemService(InputMethodService);
            var currentFocus = CurrentFocus;
            if (currentFocus == null)
            {
                formNameTextbox.RequestFocus();
                currentFocus = CurrentFocus;
            }
            if (currentFocus != null) inputManager.HideSoftInputFromWindow(currentFocus.WindowToken, HideSoftInputFlags.ImplicitOnly);
        }

        protected override void OnPause()
        {
            base.OnPause();
        }

        private void SetUpFormButtons()
        {
            var layoutpanel = FindViewById<LinearLayout>(Resource.Id.Form_list_layoutpane);
            formNameTextbox = FindViewById<EditText>(Resource.Id.form_name_textbox);
            setFormNameButton = FindViewById<Button>(Resource.Id.Set_form_name_button);
            strokeCountDisplay = FindViewById<TextView>(Resource.Id.stroke_count_display);
            reassessButton = FindViewById<Button>(Resource.Id.reassess_button);
            pauseButton = FindViewById<Button>(Resource.Id.pause_button);
            finalizeButton = FindViewById<Button>(Resource.Id.finalize_button);
            paramAbox = FindViewById<EditText>(Resource.Id.parameterAtextbox);
            paramBbox = FindViewById<EditText>(Resource.Id.parameterBtextbox);
            paramCbox = FindViewById<EditText>(Resource.Id.parameterCtextbox);

            foreach (string formName in MasterFechtbuch.formNames?.DefaultIfEmpty() ?? new string[0])
            {
                var form = MasterFechtbuch.Get(formName);
                var formButton = new Button(this);
                var codicil = (form != null && form != Form.None) ? "" : " (Unknown)";
                formButton.SetText(formName + codicil, TextView.BufferType.Normal);
                formButton.SetPadding(20,20,20,20);
                layoutpanel.AddView(formButton);
                formButtons.Add(formButton);

                formButton.Click += async (o, e) =>
                {
                    if (FormBeingTrained != null) return; // Debouncing, basically.
                    if (form != null && form != Form.None) // The target form does already exist in more than theory.
                    {
                        AppendMode = true;
                        FormBeingRetrained = form;
                        FormBeingTrained = form;
                        foreach (Button btn in formButtons) btn.Enabled = false;
                        setFormNameButton.Text = "Retrain Instead";
                        formNameTextbox.Text = formName;
                        formNameTextbox.Focusable = false;
                        CheckStrokeCount();
                        await Speech.SayAllOf($"Adding more training for {formName}. Ahn garde!");
                        CurrentStage = new EnGardeStage($"En Garde for rep {FormBeingTrained.Strokes.Count}", "Setting up another recording. Wait for the cue...",
                            new StrokeTrainingStage($"{Current.FormBeingTrained.FormName} stroke, rep {FormBeingTrained.Strokes.Count}"), true);
                    }
                    else // It only existed as a theory - treat it as if the user had typed in the name.
                    {
                        formNameTextbox.Text = formName;
                        setFormNameButton.CallOnClick();
                    }
                };
            }
            
            formNameTextbox.KeyPress += (object sender, View.KeyEventArgs e) =>
            {
                e.Handled = false;
                if (e.Event.Action == KeyEventActions.Down && e.KeyCode == Keycode.Enter && formNameTextbox.Text.Length > 0)
                {
                    setFormNameButton.CallOnClick();
                }
                if (formNameTextbox.Text.Length > 0) setFormNameButton.Text = "Train";
                else setFormNameButton.Text = "Random";
            };

            setFormNameButton.Click += async (o, e) =>
            {
                if (setFormNameButton.Text == RetrainEnGardeText)
                {
                    CurrentStage.Deactivate();
                    setFormNameButton.Text = "Random";
                    CurrentStage = new DefineEnGardeStage("Redefining en garde",  true);
                    return;
                }
                if (FormBeingRetrained != null)
                {
                    if (AppendMode == true)
                    {
                        AppendMode = false;
                        FormBeingTrained = new Form(FormBeingRetrained.FormName, FormBeingRetrained.IsOffense);
                        setFormNameButton.Text = "Erase form";
                        await Speech.SayAllOf("Retraining from start.");
                        return;
                    }
                    else if (setFormNameButton.Text == "Erase form")
                    {
                        setFormNameButton.Text = "Confirm erasure";
                        return;
                    }
                    else if (setFormNameButton.Text == "Confirm erasure")
                    {
                        MasterFechtbuch.Erase(FormBeingRetrained.FormName);
                        ThePlayersSword.ForgetForm(FormBeingRetrained.FormName);
                        await Speech.SayAllOf($"Deleting {FormBeingRetrained.FormName} from the master library.");
                        CurrentStage.Deactivate();
                        CurrentStage = GestureRecognizerStage.NullStage;
                        Finish();
                        return;
                    }
                }
                FormBeingRetrained = null;
                AppendMode = null;
                if (formNameTextbox.Text.Length == 0) formNameTextbox.Text = GenerateRandomFormName();
                await Task.Delay(100); // Let the screen update.
                FormBeingTrained = new Form(formNameTextbox.Text, !(formNameTextbox.Text.EndsWith("Parry")));
                foreach (Button btn in formButtons) btn.Enabled = false;
                formNameTextbox.Focusable = false;
                await Speech.SayAllOf($"Training {FormBeingTrained.FormName}.  Ahn garde!");
                CheckStrokeCount();
                CurrentStage = new EnGardeStage("EnGarde pre-Form Setup", "", new FormSetupStage($"Init training for {FormBeingTrained.FormName}"), true);
            };

            reassessButton.Click += (o, e) => 
            {
                
            };

            pauseButton.Click += async (o, e) =>
            {
                if (pauseButton.Text == "Pause Sensors")
                {
                    pauseButton.Text = "Resume Sensors";
                    Res.SFX.StopAll();
                    CurrentStage.Deactivate();
                    await Speech.SayAllOf($"Pausing sensors.  Take your time and play with the parameter buttons.");
                    CurrentStage = GestureRecognizerStage.NullStage;
                }
                else
                {
                    pauseButton.Text = "Pause Sensors";
                    await Speech.SayAllOf($"Resume training for {FormBeingTrained.FormName}. Ahn garde!");
                    CurrentStage = new EnGardeStage($"En Garde for rep {FormBeingTrained.Strokes.Count}", "Setting up another recording. Wait for the cue...",
                        new StrokeTrainingStage($"{Current.FormBeingTrained.FormName} stroke, rep {FormBeingTrained.Strokes.Count}"), true);
                }
            };
            
            finalizeButton.Click += async (o, e) =>
            {
                if (finalizeButton.Text == "Finalize")
                {
                    // Halt ongoing processeses (if not already done via the Pause button).
                    Res.SFX.StopAll();
                    CurrentStage.Deactivate();
                    CurrentStage = GestureRecognizerStage.NullStage;
                    finalizeButton.Text = "Commit to Fechtbuch";
                }
                else
                {
                    // TODO: Stuff.
                    MasterFechtbuch.Inscribe(FormBeingTrained);
                    ThePlayersSword.LearnForm(FormBeingTrained);
                    
                    if (FormBeingRetrained == null)
                        await Speech.SayAllOf($"Adding {FormBeingTrained.FormName} to the master fechtbuch.");
                    else await Speech.SayAllOf($"Updating form listing for {FormBeingTrained.FormName}.");
                    Log.Info("MeleeTraining", $"Here's the form string for copy-and-pasting as a constant: {FormBeingTrained.ToString()}");
                    Finish();
                }
                
            };
        }

        protected void CheckStrokeCount()
        {
            bool oneStroke = FormBeingTrained?.Strokes?.Count > 0;
            finalizeButton.Enabled = oneStroke;
            reassessButton.Enabled = oneStroke;
            pauseButton.Enabled = oneStroke;
            string wasBefore = (FormBeingRetrained != null) ? $" (was {FormBeingRetrained.Strokes.Count})" : "";
            string isNow = (oneStroke) ? FormBeingTrained.Strokes.Count.ToString() + wasBefore : "None";
            strokeCountDisplay.Text = $"Strokes: {isNow}";
        }

        private string GenerateRandomFormName()
        {
            string[] Directions = { "High", "Left", "Right" };
            string[] FormTypes = { "Slash", "Parry" };
            return Directions.GetRandom() + " " + FormTypes.GetRandom();
        }

        public void RelayMessage(string message, int RelayTargetId = 1)
        {
            try
            {
                if (RelayTargetId != 1) RunOnUiThread(() =>
                {
                    currentSignalsDisplay.Text = message;
                });
                else RunOnUiThread(() => { bestSignalsDisplay.Text = message; });
            }
            catch (Exception)
            {
                throw;
            }
        }

        public class DefineEnGardeStage : GestureRecognizerStage
        {
            private Sword Blade;
            private StillnessProvider Stillness;
            private GravityOrientationProvider AttitudeProvider;
            private AdvancedRollingAverage<Quaternion> AverageAttitude;

            public DefineEnGardeStage(string label, bool AutoStart = false) : base(label)
            {
                Blade = Current.ThePlayersSword;
                Stillness = new StillnessProvider();
                SetUpProvider(Stillness);

                AttitudeProvider = new GravityOrientationProvider();
                AttitudeProvider.Activate();

                if (AutoStart) Activate();
            }

            protected override async Task startActionAsync()
            {
                AverageAttitude = AdvancedRollingAverage<Quaternion>.Create<Quaternion>(10, await FrameShiftFunctions.OrientationWhenReady(AttitudeProvider));
            }

            protected override bool interimCriterion()
            {
                AverageAttitude.Update(AttitudeProvider.Quaternion);
                //Log.Debug("Melee Training", $">>>> Currently reading {AverageAttitude.Average} ({AttitudeProvider.Quaternion.AngleTo(AverageAttitude.Average)} degrees from mean), with sigma {AverageAttitude.Sigma}.");
                return false;
            }

            protected override bool nextStageCriterion()
            {
                return (Stillness.ReadsMoreThan(6f)
                    && !AverageAttitude.Average.IsIdentity
                    && AverageAttitude.Sigma < 5
                    && AverageAttitude.NumPoints > 20);
            }

            protected override async Task nextStageActionAsync()
            {
                //await AttitudeProvider.SetFrameShiftFromCurrent();
                //Blade.EnGardeOrientation = new AdvancedRollingAverageQuat(10, null, AttitudeProvider.FrameShift.Inverse());
                //Blade.EnGardeOrientation = new AdvancedRollingAverageQuat(10, null, AverageAttitude.Average, 1.0f);
                Blade.EnGardeOrientation = AverageAttitude;
                Blade.EnGardeOrientation.Update(AttitudeProvider.Quaternion);
                Log.Debug("Melee Training", $">>>> Setting en garde to {Blade.EnGardeOrientation.Average}, with sigma {Blade.EnGardeOrientation.Sigma}.  In reference frame {AttitudeProvider.FrameShift}.");
                Blade.SaveSpecifics(); // Saves both the average attitude itself, and also the standard deviation of it.  Important for later!

                await Speech.SayAllOf("Okay.  Use the onscreen controls to train specific forms.");
                CurrentStage = GestureRecognizerStage.NullStage;
                Current.formNameTextbox.Enabled = true;
                //await Speech.SayAllOf("Begin");
                //if (Current.AppendMode == null || Current.AppendMode == false)
                //    CurrentStage = new FormSetupStage($"{Label} form prep", AttitudeProvider);
                //else
                //    CurrentStage = new StrokeTrainingStage($"{Label} appending ({Current.FormBeingTrained.Strokes.Count})", AttitudeProvider); 
            }
        }

        public class EnGardeStage : GestureRecognizerStage
        {
            private Sword Blade;
            private StillnessProvider Stillness;
            private GravityOrientationProvider AttitudeProvider;
            private string NextStageCue;
            private IGestureRecognizerStage NextStage;

            public EnGardeStage(string label, string nextStageCue, IGestureRecognizerStage nextStage, bool AutoStart = false) : base(label)
            {
                Blade = Current.ThePlayersSword;
                NextStageCue = nextStageCue;
                NextStage = nextStage;

                Stillness = new StillnessProvider();
                SetUpProvider(Stillness);

                AttitudeProvider = new GravityOrientationProvider(); // Always a fresh, un-frame-shifted, gravity-only one for this part!
                AttitudeProvider.Activate();

                if (AutoStart) Activate();
            }

            //private DateTime nextScreenUpdate = DateTime.Now;
            //private TimeSpan screenUpdateInterval = TimeSpan.FromSeconds(0.5);
            protected override bool nextStageCriterion()
            {
                //if (DateTime.Now > nextScreenUpdate)
                //{
                //    nextScreenUpdate = DateTime.Now + screenUpdateInterval;
                //    //var isSet = (AttitudeProvider.IsFrameShiftSet) ? "is set" : "not set";
                //    var zScore = AttitudeProvider.Quaternion.AngleTo(Blade.EnGardeOrientation) / Blade.EnGardeOrientation.Sigma;
                //    //Log.Debug("Melee Training", $"---------->  Stillness {Stillness.StillnessScore:f1}; Frame shift {isSet}; Angle {AttitudeProvider.Quaternion.AngleTo(Blade.EnGardeOrientation)} ({zScore} sigma).");
                //    Log.Debug("Melee Training", $"---------->  Stillness {Stillness.StillnessScore:f1}; Angle {AttitudeProvider.Quaternion.AngleTo(Blade.EnGardeOrientation)} ({zScore} sigma).");
                //}
                return (Stillness.ReadsMoreThan(-5f) // Just not ludicrously bad, is all we ask here.
                    //&& AttitudeProvider.IsFrameShiftSet
                    && AttitudeProvider.RunTime > TimeSpan.FromSeconds(1.0)
                    && AttitudeProvider.Quaternion.AngleTo(Blade.EnGardeOrientation) < (1.5 * Blade.EnGardeOrientation.Sigma + 25));
            }

            protected override async Task nextStageActionAsync()
            {
                if (Current.setFormNameButton.Text == RetrainEnGardeText) Current.setFormNameButton.Text = "Random"; // Undoes our little textual flag that lets us retrain the en garde using this button instead of its usual function.

                var oldOrientation = Blade.EnGardeOrientation.Average;
                Blade.EnGardeOrientation.Update(AttitudeProvider.Quaternion); // Subtle trick: we keep averaging the En Garde orientation so it "tracks" the player's shifts in taking it, over time.
                Log.Debug("EnGarde Stage", $"Updating the prior en garde ({oldOrientation}), using the new one ({AttitudeProvider.Quaternion}," +
                    $" {AttitudeProvider.Quaternion.AngleTo(oldOrientation)} degrees away), producing a new one ({Blade.EnGardeOrientation.Average}," +
                    $" {Blade.EnGardeOrientation.Average.AngleTo(oldOrientation)} degrees away from the prior one).");

                //// In order to *pass along* a provider, to an arbitrary Gesture Stage which has already been created and passed to us
                //// (and which might or might not want to use our provider at all), we use this trick.  Activate it in here, and during
                //// activation, a specific STATIC provider is made available.  We lock so that (in theory) two EnGarde Stages which were
                //// running in separate threads wouldn't end up accessing the static value at the wrong times.
                //using (await _asyncLock.LockAsync())
                //{
                //    //EngardeProvider = AttitudeProvider; // Can be referenced from inside the prestartAction of the provided stage, if desired. (Not during the ctor... that's already gone by!)
                //    EngardeProvider = new FrameShiftedOrientationProvider(Android.Hardware.SensorType.GameRotationVector);
                //    await Task.WhenAll(EngardeProvider.SetFrameShiftFromCurrent(), Speech.Say(NextStageCue));
                //    CurrentStage = NextStage;
                //    CurrentStage.Activate();
                //    EngardeProvider = null;
                //}

                // Different approach to solving the above problem.
                var EngardeProvider = new FrameShiftedOrientationProvider(Android.Hardware.SensorType.GameRotationVector);
                await Task.WhenAll(EngardeProvider.SetFrameShiftFromCurrent(), Speech.SayAllOf(NextStageCue));
                CurrentStage = NextStage;
                var cStage = CurrentStage as ITakeAnAttitudeProvider;
                if (cStage != null) cStage.AttitudeProvider = EngardeProvider;
                CurrentStage.Activate();

            }

            //public static FrameShiftedOrientationProvider EngardeProvider;
            //public static Nito.AsyncEx.AsyncLock _asyncLock = new Nito.AsyncEx.AsyncLock();
        }

        private interface ITakeAnAttitudeProvider
        {
            FrameShiftedOrientationProvider AttitudeProvider { set; }
        }

        public class FormSetupStage : GestureRecognizerStage, ITakeAnAttitudeProvider
        {
            private Sword Blade;
            private bool IsInitialPoseEstimate;
            private StillnessProvider Stillness;
            public FrameShiftedOrientationProvider AttitudeProvider { get; set; }

            public FormSetupStage(string label, FrameShiftedOrientationProvider provider = null, bool Autostart = false) : base(label)
            {
                Blade = Current.ThePlayersSword;
                IsInitialPoseEstimate = (Current.FormBeingTrained.InitialOrientation.IsIdentity);

                Stillness = new StillnessProvider();
                SetUpProvider(Stillness);

                //AttitudeProvider = provider ?? new OrientationSensorProvider(Android.Hardware.SensorType.GameRotationVector);
                AttitudeProvider = provider; // If null, we'd bloody well better be planning to set it externally! ?? new FrameShiftedOrientationProvider(Android.Hardware.SensorType.GameRotationVector, Blade.EnGardeOrientation.Average);
                if (provider == null && Autostart) throw new ArgumentNullException("provider", "Provider cannot be null if you're planning to automatically start - that only works if you're setting it externally after ctor but before Activate().");
                //if (!AttitudeProvider.IsActive) AttitudeProvider.Activate();

                if (Autostart) Activate();
            }

            //protected override void prestartAction()
            //{
            //    AttitudeProvider = EnGardeStage.EngardeProvider ?? new FrameShiftedOrientationProvider(
            //        Android.Hardware.SensorType.GameRotationVector, Blade.EnGardeOrientation.Average); // .Inverse()?
            //    Log.Debug("Form training", $"Attitude Provider reference frame is {AttitudeProvider.FrameShift.ToStringFormatted("f4")}");
            //    if (!AttitudeProvider.IsActive) AttitudeProvider.Activate();
            //}

            protected override async Task startActionAsync()
            {
                if (IsInitialPoseEstimate)
                {
                    await Speech.SayAllOf("First we need approximate readings of where the stroke begins and ends.");
                    await Task.Delay(500);
                    await Speech.SayAllOf("Take a still pose at roughly the start of the smooth central portion of the stroke.");
                }
                else await Speech.SayAllOf("Got it.  Next, take a still pose at roughly the end of that smooth central part of the stroke.");
                await SFX.PlayByID(Current, Resource.Raw._175949_clash_t).WhenFinishedPlaying;
            }

            protected override bool nextStageCriterion()
            {
                return (Stillness.StillnessScore + Sqrt(Stillness.RunTime.TotalSeconds) > 5f);
            }

            protected override async Task nextStageActionAsync()
            {
                if (IsInitialPoseEstimate)
                {
                    Current.FormBeingTrained.InitialOrientation = AttitudeProvider.Quaternion;
                    Log.Debug("Stroke still stances", $"Still position #1 set at {AttitudeProvider.Quaternion}, {AttitudeProvider.Quaternion.AngleTo(Quaternion.Identity)} degrees from En Garde.");
                    CurrentStage = new FormSetupStage($"{Current.FormBeingTrained.FormName} form, second still pose", AttitudeProvider, true);
                }
                else
                {
                    await Speech.SayAllOf("Thank you. Take your ahngarde stance again.");
                    Current.FormBeingTrained.FinalOrientation = AttitudeProvider.Quaternion;
                    Log.Debug("Stroke still stances", $"Still position #2 set at {AttitudeProvider.Quaternion}, {AttitudeProvider.Quaternion.AngleTo(Quaternion.Identity)} degrees from En Garde.");
                    CurrentStage = new EnGardeStage("En Garde before stroke training", "Wait for the cue, then execute the form as you wish it to be judged in play.",
                        new StrokeTrainingStage($"{Current.FormBeingTrained.FormName}, stroke training, rep 0"), true);
                }
            }
        }

        public class StrokeTrainingStage : GestureRecognizerStage, ITakeAnAttitudeProvider
        {
            private Sword Blade;
            private Stroke thisStroke;
            private int averagingLength = 5;

            public Vector3Provider GyroProvider;
            private AdvancedRollingAverageVector3 AverageAxis;
            private AdvancedRollingAverageFloat AverageRotationSpeed;

            private Vector3Provider AccelProvider;
            private AdvancedRollingAverageVector3 AverageAccel;

            public FrameShiftedOrientationProvider AttitudeProvider { get; set; }
            //private AdvancedRollingAverageQuat AverageAttitude;

            private float ClosestApproachToFinal = float.PositiveInfinity,
                ClosestApproachToInitial = float.PositiveInfinity;
            private float ApproachF, ApproachI;
            private DateTime TimeOfClosestApproachToFinal;
            
            public StrokeTrainingStage(string label, bool includeRandomWait = true, bool Autostart = false) : base(label)
            {
                Blade = Current.ThePlayersSword;

                GyroProvider = new Vector3Provider(Android.Hardware.SensorType.Gyroscope);
                GyroProvider.Activate();
                SetUpProvider(GyroProvider);
                AverageAxis = new AdvancedRollingAverageVector3(averagingLength);
                AverageRotationSpeed = new AdvancedRollingAverageFloat(averagingLength);

                AccelProvider = new Vector3Provider(Android.Hardware.SensorType.LinearAcceleration);
                AccelProvider.Activate();
                AverageAccel = new AdvancedRollingAverageVector3(averagingLength);

                // AttitudeProvider moved into the prestartAction function, since it wants to inherit the provider of the EnGardeStage launching it.
                //AverageAttitude = new AdvancedRollingAverageQuat(averagingLength);

                thisStroke = new Stroke();

                if (Autostart) Activate();
            }

            //protected override void prestartAction()
            //{
            //    AttitudeProvider = EnGardeStage.EngardeProvider ?? new FrameShiftedOrientationProvider(
            //        Android.Hardware.SensorType.GameRotationVector, Blade.EnGardeOrientation.Average); // .Inverse()?
            //    Log.Debug("Stroke training", $"Attitude Provider reference frame is {AttitudeProvider.FrameShift.ToStringFormatted("f4")}");
            //    if (!AttitudeProvider.IsActive) AttitudeProvider.Activate();
            //}

            protected override async Task startActionAsync()
            {
                await Task.Delay((int)(Res.Random * 2500));
                Current.FormBeingTrained.Prompt.SynchSay().LaunchAsOrphan("Prompt");
            }

            private int count = 0;
            protected override bool interimCriterion() { return true; }
            protected override void interimAction()
            {
                if (AccelProvider.Vector == null || AccelProvider.Vector.LengthSquared() < 0.001) return; // Throw out the initial zeroes before our sensors are live.

                AverageRotationSpeed.Update(GyroProvider.Vector.Length());
                AverageAxis.Update(GyroProvider.Vector.Normalize());
                AverageAccel.Update(AccelProvider.Vector);
                //AverageAttitude.Update(AttitudeProvider.Quaternion);

                thisStroke.Snapshots.Add(new StrokeSnapshot()
                {
                    RotationVelocity = AverageRotationSpeed.Average,
                    Axis = AverageAxis.Average.Normalize(),
                    Accel = AverageAccel.Average,
                    //Orientation = AverageAttitude.Average,
                    Orientation = AttitudeProvider.Quaternion,
                    Timestamp = GyroProvider.nanosecondsElapsed
                });

                count++;
                if (count % 10 == 1) Log.Debug("Snapshot", $"{thisStroke.Snapshots.Last()} - Approach {ClosestApproachToInitial:f2} / {ClosestApproachToFinal:f2}");
            }

            private bool foundInitialOrientation = false,
                distanceFromInitialOrientationIncreasing = false,
                distanceToFinalOrientationLessThanToInitial = false,
                foundFinalOrientation = false,
                distanceFromFinalOrientationIncreasing = false;
            protected override bool nextStageCriterion()
            {
                ApproachI = Current.FormBeingTrained.InitialOrientation.AngleTo(AttitudeProvider);
                ApproachF = Current.FormBeingTrained.FinalOrientation.AngleTo(AttitudeProvider);

                ClosestApproachToInitial = Min(ClosestApproachToInitial, ApproachI);
                if (!foundInitialOrientation)
                {
                    if (ApproachI < 45) { count = 10; foundInitialOrientation = true; Log.Debug("Stroke", "Step 1 - passed within 45 degrees of initial."); }
                    return false;
                }

                if (!distanceFromInitialOrientationIncreasing)
                {
                    if (ApproachI > ClosestApproachToInitial + 5) { count = 10; distanceFromInitialOrientationIncreasing = true; Log.Debug("Stroke", "Step 2 - distance from initial is increasing."); }
                    return false;
                }

                ClosestApproachToFinal = Min(ClosestApproachToFinal, ApproachF);
                if (!distanceToFinalOrientationLessThanToInitial)
                {
                    if (ApproachI > ApproachF + 5) { count = 10; distanceToFinalOrientationLessThanToInitial = true; Log.Debug("Stroke", "Step 3 - closer to final than to initial."); }
                    return false;
                }
                if (!foundFinalOrientation)
                {
                    if (ApproachF < 45) { count = 10; foundFinalOrientation = true; Log.Debug("Stroke", "Step 4 - passed within 45 degrees of final."); }
                    return false;
                }
                if (!distanceFromFinalOrientationIncreasing)
                {
                    if (ApproachF <= ClosestApproachToFinal) TimeOfClosestApproachToFinal = DateTime.Now;
                    if (ApproachF > ClosestApproachToFinal + 5) { count = 10; distanceFromFinalOrientationIncreasing = true; Log.Debug("Stroke", "Step 5 - distance from final is increasing."); }
                    return false;
                }

                if ((DateTime.Now - TimeOfClosestApproachToFinal).TotalSeconds > 0.5) { count = 10; Log.Debug("Stroke", "Finale!  At least 0.5 seconds have passed since the closest approach to final."); return true; }
                else return false;
            }

            protected override async Task nextStageActionAsync()
            {
                Current.FormBeingTrained.Strokes.Add(thisStroke);
                int reps = Current.FormBeingTrained.Strokes.Count;
                await Speech.SayAllOf($"Data recorded.  You now have {reps} reps saved.");
                await Task.Delay(500);
                await Speech.SayAllOf($"Return to your ahn garde to record another repetition for averaging, or use onscreen controls to fiddle with data.");

                Current.CheckStrokeCount();
                CurrentStage = new EnGardeStage($"En Garde for rep {reps}", "Setting up another recording. Wait for it...",
                        new StrokeTrainingStage($"{Current.FormBeingTrained.FormName} stroke, rep {reps}"), true);
            }

            
        }

    }
}