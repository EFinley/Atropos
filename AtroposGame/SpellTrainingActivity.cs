
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

namespace com.Atropos
{
    /// <summary>
    /// This is the activity started when we detect a "train spell gestures" NFC tag.
    /// Players will also be able to enter this mode with a specific "spell" - to be written later
    /// (probably via such a "train spell gestures" tag!).
    /// </summary>
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class SpellTrainingActivity : BaseActivity_Portrait, IRelayMessages
    {
        private Focus ThePlayersFocus;
        private Spell SpellBeingTrained, SpellBeingRetrained;
        private TextView currentSignalsDisplay, bestSignalsDisplay, resultsDisplay;
        private EditText spellNameTextbox;
        private List<Button> spellButtons = new List<Button>();
        private TextView glyphCountDisplay;
        private Button undoGlyphButton, inscribeButton, setSpellNameButton;

        protected static SpellTrainingActivity Current { get { return (SpellTrainingActivity)CurrentActivity; } set { CurrentActivity = value; } }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.SpellTraining);

            currentSignalsDisplay = FindViewById<TextView>(Resource.Id.current_signals_text);
            bestSignalsDisplay = FindViewById<TextView>(Resource.Id.best_signals_text);
            resultsDisplay = FindViewById<TextView>(Resource.Id.result_text);
            
            SetUpSpellButtons();

            // See if the current focus is already in our (local, for now) library, and load it if so.  Otherwise, take us to calibration.
            var focusString = Res.SpecificTags.Get(InteractionLibrary.CurrentSpecificTag);
            if (focusString != null && focusString.Length > 0)
            {
                ThePlayersFocus = Focus.FromString(focusString, InteractionLibrary.CurrentSpecificTag);
            }
            else if (InteractionLibrary.CurrentSpecificTag == InteractionLibrary.SpellTeaching.Name + "0000")
            {
                Focus.InitMasterFocus();
                InteractionLibrary.CurrentSpecificTag = Focus.MasterFocus.TagID;
                Log.Info("Training", "Using master focus.");
                ThePlayersFocus = Focus.MasterFocus;
            }
            else
            {
                ThePlayersFocus = new Focus(InteractionLibrary.CurrentSpecificTag);
            }

            CurrentStage = GestureRecognizerStage.NullStage;
        }

        protected override async void OnResume()
        {
            await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: true);
        }

        protected override void OnPause()
        {
            base.OnPause();
        }

        private void SetUpSpellButtons()
        {
            var layoutpanel = FindViewById<LinearLayout>(Resource.Id.Spell_casting_layoutpane);

            foreach (string spellName in MasterSpellLibrary.spellNames?.DefaultIfEmpty() ?? new string[0])
            {
                var spell = MasterSpellLibrary.Get(spellName);
                var spellButton = new Button(this);
                spellButton.SetText(spellName + " (Retrain)", TextView.BufferType.Normal);
                spellButton.SetPadding(20,20,20,20);
                layoutpanel.AddView(spellButton);

                spellButton.Click += (o, e) =>
                {
                    if (SpellBeingTrained != null) return; // Debouncing, basically.
                    SpellBeingRetrained = spell;
                    SpellBeingTrained = new Spell(spellName);
                    foreach (Button btn in spellButtons) btn.Visibility = ViewStates.Gone;
                    setSpellNameButton.Text = "Erase spell";
                    spellNameTextbox.Text = spellName;
                    spellNameTextbox.Focusable = false;
                    CheckGlyphCount();
                    Speech.Say($"Retraining {spellName}.");
                    CurrentStage = new Spell_Training_TutorialStage($"Retraining {spellName}", ThePlayersFocus, true);
                };
            }

            spellNameTextbox = FindViewById<EditText>(Resource.Id.spell_name_textbox);
            setSpellNameButton = FindViewById<Button>(Resource.Id.Set_spell_name_button);
            glyphCountDisplay = FindViewById<TextView>(Resource.Id.glyph_count_display);
            undoGlyphButton = FindViewById<Button>(Resource.Id.undo_glyph_button);
            inscribeButton = FindViewById<Button>(Resource.Id.inscribe_spell_button);

            spellNameTextbox.KeyPress += (object sender, View.KeyEventArgs e) =>
            {
                e.Handled = false;
                if (e.Event.Action == KeyEventActions.Down && e.KeyCode == Keycode.Enter && spellNameTextbox.Text.Length > 0)
                {
                    setSpellNameButton.CallOnClick();
                }
                if (spellNameTextbox.Text.Length > 0) setSpellNameButton.Text = "Train";
                else setSpellNameButton.Text = "Random";
            };
            spellNameTextbox.ClearFocus(); // Not working, dunno why.

            setSpellNameButton.Click += async (o, e) =>
            {
                if (SpellBeingRetrained != null)
                {
                    if (setSpellNameButton.Text == "Erase spell") { setSpellNameButton.Text = "Confirm erasure"; return; }
                    else if (setSpellNameButton.Text == "Confirm erasure")
                    {
                        MasterSpellLibrary.Erase(SpellBeingRetrained.SpellName);
                        ThePlayersFocus.ForgetSpell(SpellBeingRetrained.SpellName);
                        await Speech.SayAllOf($"Deleting {SpellBeingRetrained.SpellName} from the master library.");
                        CurrentStage.Deactivate();
                        CurrentStage = GestureRecognizerStage.NullStage;
                        Current.Finish();
                        return;
                    }
                }
                SpellBeingRetrained = null;
                if (spellNameTextbox.Text.Length == 0) spellNameTextbox.Text = GenerateRandomSpellName();
                await Task.Delay(100); // Let the screen update.
                SpellBeingTrained = new Spell(spellNameTextbox.Text);
                foreach (Button btn in spellButtons) btn.Visibility = ViewStates.Gone;
                spellNameTextbox.Focusable = false;
                CheckGlyphCount();
                await Speech.SayAllOf($"Training {SpellBeingTrained.SpellName}.");
                CurrentStage = new Spell_Training_TutorialStage($"Init training for {SpellBeingTrained.SpellName}", ThePlayersFocus, true);
            };

            undoGlyphButton.Click += async (o, e) => 
            {
                if (SpellBeingTrained.Glyphs.Count == 0)
                {
                    foreach (Button btn in spellButtons) btn.Enabled = true;
                    spellNameTextbox.Text = "";
                    spellNameTextbox.Focusable = true;
                    setSpellNameButton.Enabled = true;
                    setSpellNameButton.Text = "Random";
                    SpellBeingRetrained = null;
                    SpellBeingTrained = null;
                    CurrentStage.Deactivate();
                    CurrentStage = GestureRecognizerStage.NullStage;
                    await Speech.SayAllOf("Aborting spell training.");
                }
                else
                {
                    SpellBeingTrained.UndoAddGlyph();
                    await Speech.SayAllOf("Removing most recent glyph.");
                }
                CheckGlyphCount();
            };

            var feedbackSFXbtn = FindViewById<Button>(Resource.Id.spell_feedback_sfx_button);
            var progressSFXbtn = FindViewById<Button>(Resource.Id.spell_progress_sfx_button);
            var successSFXbtn = FindViewById<Button>(Resource.Id.spell_success_sfx_button);

            if (!MasterSpellLibrary.GetSFXReadyTask().Wait(5000))
                Log.Error("Spell training", "Can't prep the buttons (as is, anyway) without our SFX loaded, which doesn't seem to be happening.");
            var feedbackSFXoptions = new SimpleCircularList<string>("Magic.Ethereal", 
                "Magic.Aura", "Magic.DeepVenetian", "Magic.InfiniteAubergine", "Magic.Ommm", "Magic.AfricanDrums", 
                "Magic.Rommble", "Magic.MidtonePianesque", "Magic.FemReverbDSharp","Magic.FemReverbCSharp",
                "Magic.FemReverbF", "Magic.FemReverbE", "Magic.AlienTheremin",
                "Magic.TrompingBuzzPulse", "Magic.GrittyDrone", "Magic.Galewinds", "Magic.NanobladeLoop", 
                "Magic.ViolinLoop", "Magic.StrongerThanTheDark", "Magic.MelodicPad");
            var progressSFXoptions = new SimpleCircularList<string>(MasterSpellLibrary.SpellSFX.Keys.Where(sfx => !feedbackSFXoptions.Contains(sfx)).DefaultIfEmpty().ToArray());
            var successSFXoptions = new SimpleCircularList<string>(MasterSpellLibrary.CastingResults.Keys.ToArray());
            while (progressSFXoptions.Next != MasterSpellLibrary.defaultProgressSFXName) { } // Cycle the list to the correct starting point.
            while (successSFXoptions.Next != "Play " + MasterSpellLibrary.defaultSuccessSFXName) { } // Cycle the list to the correct starting point.

            inscribeButton.Click += async (o, e) =>
            {
                if (inscribeButton.Text == "Inscribe Spell")
                {
                    // Halt ongoing processeses
                    MasterSpellLibrary.SpellFeedbackSFX.Deactivate();
                    CurrentStage.Deactivate();
                    CurrentStage = GestureRecognizerStage.NullStage;

                    // Display SFX modification buttons
                    feedbackSFXbtn.Visibility = ViewStates.Visible;
                    progressSFXbtn.Visibility = ViewStates.Visible;
                    successSFXbtn.Visibility = ViewStates.Visible;

                    IEffect sampleSFX = null;
                    Func<SimpleCircularList<string>, Button, string, EventHandler> HandlerFactory
                        = (circList, btn, label) =>
                        {
                            return (ob, ev) =>
                            {
                                sampleSFX?.Stop();
                                btn.Text = $"{label} ('{circList.Next}')";
                                if (MasterSpellLibrary.SpellSFX.ContainsKey(circList.Current.Split(' ').Last()))
                                {
                                    sampleSFX = MasterSpellLibrary.SpellSFX[circList.Current.Split(' ').Last()];
                                    sampleSFX.Play();
                                }
                                else
                                {
                                }
                            };
                        };
                    feedbackSFXbtn.Click += HandlerFactory(feedbackSFXoptions, feedbackSFXbtn, "Feedback SFX");
                    progressSFXbtn.Click += HandlerFactory(progressSFXoptions, progressSFXbtn, "Progress SFX");
                    successSFXbtn.Click += HandlerFactory(successSFXoptions, successSFXbtn, "Success Func");

                    inscribeButton.Text = "Finish Inscribing";
                }
                else
                {
                    feedbackSFXbtn.Visibility = ViewStates.Gone;
                    progressSFXbtn.Visibility = ViewStates.Gone;
                    successSFXbtn.Visibility = ViewStates.Gone;

                    SpellBeingTrained.CastingResult = MasterSpellLibrary.CastingResults[successSFXoptions.Current];
                    foreach (var glyph in SpellBeingTrained.Glyphs)
                    {
                        glyph.FeedbackSFXName = feedbackSFXoptions.Current;
                        glyph.ProgressSFXName = progressSFXoptions.Current;
                    }
                    MasterSpellLibrary.Inscribe(SpellBeingTrained);
                    ThePlayersFocus.LearnSpell(SpellBeingTrained);
                    
                    //ResetSpell();
                    if (SpellBeingRetrained == null)
                        await Speech.SayAllOf($"Adding {SpellBeingTrained.SpellName} to the master library.");
                    else await Speech.SayAllOf($"Updating spell listing for {SpellBeingTrained.SpellName}.");
                    Log.Info("SpellTraining", $"Here's the spell string for copy-and-pasting as a constant: {SpellBeingTrained.ToString()}");
                    Current.Finish();
                }
                
            };
        }

        private void CheckGlyphCount()
        {
            bool oneGlyph = SpellBeingTrained.Glyphs.Count > 0;
            inscribeButton.Enabled = oneGlyph;
            undoGlyphButton.Text = (SpellBeingTrained != null) ?
                                        ((oneGlyph) ? "Undo" : "Delete") :
                                        ("Undo / Delete");
            string wasBefore = (SpellBeingRetrained != null) ? $" (was {SpellBeingRetrained.Glyphs.Count})" : "";
            string isNow = (oneGlyph) ? SpellBeingTrained.Glyphs.Count.ToString() + wasBefore : "None";
            glyphCountDisplay.Text = $"Glyphs: {isNow}";
        }

        private string GenerateRandomSpellName()
        {
            string[] Elements = { "Manna", "Power", "Fire", "Ice", "Acid", "Spark", "Toxic", "Chocolate", "Mental", "Shard", "Gravity", "Illusion", "Deception" };
            string[] SpellTypes = { "Bolt", "Dart", "Lance", "Ball", "Wave", "Blast", "Stream", "Pachinko", "Snooker", "Shield", "Aura", "Vortex", "Sneeze" };
            return Elements.GetRandom() + " " + SpellTypes.GetRandom();
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

        public class Spell_Training_TutorialStage : GestureRecognizerStage
        {
            private Focus Implement;
            private Spell SpellBeingTrained;
            private StillnessProvider Stillness;
            private FrameShiftedOrientationProvider AttitudeProvider;
            private Task sayIt;

            public Spell_Training_TutorialStage(string label, Focus Focus, bool AutoStart = false) : base(label)
            {
                Implement = Focus;
                Stillness = new StillnessProvider();
                SetUpProvider(Stillness);
                Stillness.StartDisplayLoop(Current, 1000);
                SpellBeingTrained = Current.SpellBeingTrained;

                AttitudeProvider = new GravityOrientationProvider();
                AttitudeProvider.Activate();

                if (AutoStart) Activate();
            }

            protected override async void startAction()
            {
                sayIt = Speech.SayAllOf($"Hold device at each position until you hear the tone.  Take your zero stance to begin.", volume: 0.5);
                await sayIt;
            }

            protected override bool interimCriterion()
            {
                return Stillness.IsItDisplayUpdateTime();
            }

            protected override void interimAction()
            {
                Stillness.DoDisplayUpdate();
            }

            protected override async Task<bool> nextStageCriterionAsync()
            {
                await sayIt;
                return (Stillness.ReadsMoreThan(8f));// && sayIt.Status == TaskStatus.RanToCompletion);
            }

            protected override async Task nextStageActionAsync()
            {
                await AttitudeProvider.SetFrameShiftFromCurrent();
                SpellBeingTrained.ZeroStance = AttitudeProvider.FrameShift;

                await Speech.SayAllOf("Begin");
                CurrentStage = new GlyphTrainingStage($"Glyph 0", Implement, AttitudeProvider);
            }
        }

        public class GlyphTrainingStage : GestureRecognizerStage
        {
            private Focus Implement;
            public StillnessProvider Stillness;
            private float Volume;
            private AdvancedRollingAverageQuat AverageAttitude;
            private OrientationSensorProvider AttitudeProvider;
            private Quaternion lastOrientation;

            public GlyphTrainingStage(string label, Focus Focus, OrientationSensorProvider Provider = null) : base(label)
            {
                Implement = Focus;
                Res.DebuggingSignalFlag = true;

                Stillness = new StillnessProvider();
                Stillness.StartDisplayLoop(Current, 750);

                SetUpProvider(Stillness);
                Volume = 0.1f;

                AverageAttitude = new AdvancedRollingAverageQuat(timeFrameInPeriods: 15);
                AttitudeProvider = Provider ?? new GravityOrientationProvider(Implement.FrameShift);
                AttitudeProvider.Activate();

                if (Current.SpellBeingTrained.Glyphs.Count == 0) lastOrientation = Quaternion.Identity;
                else lastOrientation = Current.SpellBeingTrained.Glyphs.Last().Orientation;

                Activate();
            }

            protected override void startAction()
            {
                MasterSpellLibrary.SpellFeedbackSFX.Play(Volume, true);
            }

            protected override bool nextStageCriterion()
            {
                // Must (A) satisfy stillness criterion, (B) not be right away after the last one, 
                // and (C) be at least thirty degrees away from previous (if any).  Also, if the attitude provider
                // has a frame shift but that frame shift is [still] the identity quaternion - meaning the awaitable
                // hasn't come back and set it to something nonzero - then we are not allowed to proceed regardless.
                if (Stillness.StillnessScore + Math.Sqrt(Stillness.RunTime.TotalSeconds) > 4f)
                {
                    return (AttitudeProvider.Quaternion.AngleTo(lastOrientation) > 30.0f); // Done as a separate clause for debugging reasons only.
                }
                return false;
            }
            protected override async void nextStageAction()
            {
                try
                {
                    MasterSpellLibrary.SpellFeedbackSFX.Stop();
                    await Task.Delay(150);
                    MasterSpellLibrary.SpellProgressSFX.Play();
                    await Task.Delay(1000); // Give a moment to get ready.
                    
                    Current.SpellBeingTrained.AddGlyph(new Glyph(AverageAttitude, Stillness.StillnessScore, AverageAttitude.StdDev));
                    Current.CheckGlyphCount();

                    var EulerAngles = AverageAttitude.ToEulerAngles();
                    //Log.Debug("SpellTraining", $"Saved glyph at yaw {EulerAngles.X:f2}, pitch {EulerAngles.Y}, and roll {EulerAngles.Z} from zero stance.");
                    Log.Debug("SpellTraining", $"Saved {((GestureRecognizerStage)CurrentStage).Label} at {EulerAngles:f1} from zero stance.  That's {AverageAttitude.AngleTo(lastOrientation):f0} degrees from the last one.  Baselines are {Stillness.StillnessScore:f2} for stillness, {AverageAttitude.StdDev:f2} for orientation.");
                    CurrentStage = new GlyphTrainingStage($"Glyph {Current.SpellBeingTrained.Glyphs.Count}", Implement, AttitudeProvider);
                }
                catch (Exception e)
                {
                    Log.Error("Glyph training stage progression", e.Message);
                    throw;
                }
            }

           protected override bool interimCriterion()
            {
                Stillness.IsItDisplayUpdateTime(); // Updates max and min values.
                return true;
            }

            protected override void interimAction()
            {
                Volume = (float)Exp(-(Sqrt((15 - Stillness) / 2) - 0.5));
                MasterSpellLibrary.SpellFeedbackSFX.SetVolume(Volume);

                if (Stillness.IsItDisplayUpdateTime())
                {
                    Stillness.DoDisplayUpdate();
                }

                AverageAttitude.Update(AttitudeProvider);
            }
        }

    }
}