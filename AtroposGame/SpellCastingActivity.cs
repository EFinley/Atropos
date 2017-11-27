
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

namespace com.Atropos
{
    /// <summary>
    /// This is the activity started when we detect a "cast spell" NFC tag.
    /// </summary>
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class SpellCastingActivity : BaseActivity_Portrait, IRelayMessages
    {
        private Focus ThePlayersFocus;
        private Spell SpellBeingCast;
        private TextView currentSignalsDisplay, bestSignalsDisplay, resultsDisplay;
        private List<Button> spellButtons = new List<Button>();
        protected static SpellCastingActivity Current { get { return (SpellCastingActivity)CurrentActivity; } set { CurrentActivity = value; } }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.SpellCasting);

            currentSignalsDisplay = FindViewById<TextView>(Resource.Id.current_signals_text);
            bestSignalsDisplay = FindViewById<TextView>(Resource.Id.best_signals_text);
            resultsDisplay = FindViewById<TextView>(Resource.Id.result_text);

            SetUpSpellButtons();

            // See if the current focus is already in our (local, for now) library, and load it if so.  Otherwise, just use the Master focus entry.
            var focusString = Res.SpecificTags.Get(InteractionLibrary.CurrentSpecificTag);
            if (focusString != null && focusString.Length > 0)
            {
                ThePlayersFocus = Focus.FromString(focusString, InteractionLibrary.CurrentSpecificTag);
            }
            else if (InteractionLibrary.CurrentSpecificTag == InteractionLibrary.SpellCasting.Name + "0000")
            {
                Focus.InitMasterFocus();
                InteractionLibrary.CurrentSpecificTag = Focus.MasterFocus.TagID;
                ThePlayersFocus = Focus.MasterFocus;
            }
            else
            {
                ThePlayersFocus = new Focus(InteractionLibrary.CurrentSpecificTag);
            }

            // Debugging purposes - give everybody all the spells.
            if (ThePlayersFocus.KnownSpells.Count == 0)
            {
                foreach (string spellName in MasterSpellLibrary.spellNames)
                {
                    ThePlayersFocus.LearnSpell(MasterSpellLibrary.Get(spellName));
                }
            }

            //CurrentStage = GestureRecognizerStage.NullStage; // Used if we're relying purely on buttons onscreen, as in debugging.
            CurrentStage = new BeginCastingSelectedSpellsStage("Scanning for zero stances", ThePlayersFocus, ThePlayersFocus.KnownSpells);
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

            foreach (string spellName in MasterSpellLibrary.spellNames)
            {
                var spell = MasterSpellLibrary.Get(spellName);
                var spellButton = new Button(this);
                spellButton.SetText(spellName, TextView.BufferType.Normal);
                spellButton.SetPadding(20, 20, 20, 20);
                layoutpanel.AddView(spellButton);
                spellButtons.Add(spellButton);

                spellButton.Click += (o, e) =>
                {
                    if (SpellBeingCast != null || SpellBeingCast != spell) SpellBeingCast = spell;
                    else return;
                    //CurrentStage = new BeginCastingSpecificSpellStage($"Initiating {spellName}", ThePlayersFocus, true);
                    CurrentStage?.Deactivate();
                    CurrentStage = new BeginCastingSelectedSpellsStage($"Initiating {spellName}", ThePlayersFocus, spell);
                };
            }
        }



        public void RelayMessage(string message, int RelayTargetId = 1)
        {
            //try
            //{
            //    if (RelayTargetId != 1) RunOnUiThread(() =>
            //    {
            //        currentSignalsDisplay.Text = message;
            //    });
            //    else RunOnUiThread(() => { bestSignalsDisplay.Text = message; });
            //}
            //catch (Exception)
            //{
            //    throw;
            //}
        }

        public class BeginCastingSpecificSpellStage : GestureRecognizerStage
        {
            private Focus Implement;
            private StillnessProvider Stillness;
            private GravityOrientationProvider Gravity;
            private Task sayIt;

            public BeginCastingSpecificSpellStage(string label, Focus Focus, bool AutoStart = false) : base(label)
            {
                Implement = Focus;
                Stillness = new StillnessProvider();
                Stillness.StartDisplayLoop(Current, 500);
                SetUpProvider(Stillness);
                Gravity = new GravityOrientationProvider();
                Gravity.Activate();

                if (AutoStart) Activate();
            }

            protected override async void startAction()
            {
                sayIt = Speech.SayAllOf($"Casting {Current.SpellBeingCast.SpellName}.  Take your zero stance to begin.");
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

            protected override bool nextStageCriterion()
            {
                return (Stillness.ReadsMoreThan(4f)
                    && Current.SpellBeingCast.AngleTo(Gravity.Quaternion) < 30f);
            }
            protected override async void nextStageAction()
            {
                var newProvider = new GravityOrientationProvider();
                newProvider.Activate();
                await newProvider.SetFrameShiftFromCurrent();
                await Speech.SayAllOf("Begin");
                CurrentStage = new GlyphCastingStage($"Glyph 0", Implement, Current.SpellBeingCast.Glyphs[0], newProvider);
            }
        }

        public class BeginCastingSelectedSpellsStage : GestureRecognizerStage
        {
            private Focus Focus;
            public StillnessProvider Stillness;
            private AdvancedRollingAverageQuat AverageAttitude;
            private FrameShiftedOrientationProvider AttitudeProvider;

            private List<Spell> PreparedSpells;
            private List<Spell> SpellsSortedByAngle { get {
                    return PreparedSpells
                            .OrderBy(sp => sp.AngleTo(AttitudeProvider))
                            .ToList();
                } }
            private IEffect GetFeedbackFX(Spell spell)
            {
                return spell?.Glyphs?[0]?.FeedbackSFX ?? Effect.None;
            }
            private Task sayIt;

            public BeginCastingSelectedSpellsStage(string label, Focus focus, IEnumerable<Spell> possibleSpells, bool autoStart = true) : base(label)
            {
                Focus = focus;
                Res.DebuggingSignalFlag = true;

                Stillness = new StillnessProvider();
                Stillness.StartDisplayLoop(Current, 500);
                SetUpProvider(Stillness);

                AverageAttitude = new AdvancedRollingAverageQuat(timeFrameInPeriods: 10); // Not sure I'm even going to bother using this.
                AttitudeProvider = new GravityOrientationProvider();
                AttitudeProvider.Activate();
                AverageAttitude.Update(AttitudeProvider);

                PreparedSpells = possibleSpells.ToList();

                if (autoStart) Activate();
            }

            public BeginCastingSelectedSpellsStage(string label, Focus focus, Spell singleSpell, bool autoStart = true)
                : this(label, focus, new Spell[] { singleSpell }, autoStart) { }

            protected override async Task startActionAsync()
            {
                string[] spellNames = PreparedSpells.Select(s => s.SpellName).ToArray();
                if (spellNames.Length == 0)
                {
                    await Speech.SayAllOf("Entering spell casting mode but with no spells prepared!");
                    Deactivate();
                    return;
                }
                else if (spellNames.Length == 1)
                {
                    Spell singleSpell = PreparedSpells.Single();
                    sayIt = Speech.SayAllOf($"Casting {singleSpell.SpellName}.  Take your zero stance to begin.");
                }
                else
                {
                    string spellNamesList;
                    if (spellNames.Length == 2) spellNamesList = $"{spellNames[0]} and {spellNames[1]}";
                    else spellNamesList = $"{spellNames.Length} spells";
                    sayIt = Speech.SayAllOf($"Enter spell casting mode.  You have {spellNamesList} ready for casting.  Take a spell's zero stance to begin.");
                }
                await sayIt;
                foreach (var fx in PreparedSpells.Select(s => GetFeedbackFX(s))) fx.Play(0.0, true);
            }

            protected override bool nextStageCriterion()
            {
                //if (!FrameShiftFunctions.CheckIsReady(AttitudeProvider)) return false;
                var leeWay = Stillness.StillnessScore + 2f * Sqrt(Stillness.RunTime.TotalSeconds); // VIDEO way too easy for normal use.
                if (SpellsSortedByAngle[0].AngleTo(AttitudeProvider) < 25f + leeWay)
                {
                    if (SpellsSortedByAngle.Count < 2) return true;
                    else return (SpellsSortedByAngle[0].AngleTo(AttitudeProvider) < 0.5 * SpellsSortedByAngle[1].AngleTo(AttitudeProvider));
                }
                return false;
            }

            protected override void nextStageAction()
            {
                try
                {
                    foreach (var fx in SpellsSortedByAngle.Select(s => GetFeedbackFX(s))) fx.Deactivate();

                    Current.SpellBeingCast = SpellsSortedByAngle[0];

                    Plugin.Vibrate.CrossVibrate.Current.Vibration(15);
                    Current.SpellBeingCast.Glyphs?[0]?.ProgressSFX?.Play();
                    AttitudeProvider.FrameShift = Current.SpellBeingCast.ZeroStance;
                    CurrentStage = new GlyphCastingStage($"{Current.SpellBeingCast.SpellName} Glyph 0", Focus, Current.SpellBeingCast.Glyphs[0], AttitudeProvider);
                }
                catch (Exception e)
                {
                    Log.Error("Spell selection stage", e.Message);
                    throw;
                }
            }

            protected override bool interimCriterion()
            {
                return true;
            }

            protected override void interimAction()
            {
                foreach (var s in SpellsSortedByAngle)
                {
                    //FeedbackFX(s).Volume = Exp(-Sqrt(s.AngleTo(AttitudeProvider) / 8.0) + 0.65); // Old version
                    GetFeedbackFX(s).Volume = 1.2f * Exp(-0.45f * (s.AngleTo(AttitudeProvider) / 5f - 1f));

                }
            }
        }

        public class GlyphCastingStage : GestureRecognizerStage
        {
            private Focus Implement;
            public StillnessProvider Stillness;
            private float Volume;
            private AdvancedRollingAverageQuat AverageAttitude;
            private FrameShiftedOrientationProvider AttitudeProvider;
            private Glyph targetGlyph;

            public GlyphCastingStage(string label, Focus Focus, Glyph tgtGlyph, FrameShiftedOrientationProvider oProvider = null) : base(label)
            {
                Implement = Focus;
                targetGlyph = tgtGlyph;
                Res.DebuggingSignalFlag = true;

                Stillness = new StillnessProvider();
                Stillness.StartDisplayLoop(Current, 500);
                
                SetUpProvider(Stillness);
                Volume = 0.01f;

                AverageAttitude = new AdvancedRollingAverageQuat(timeFrameInPeriods: 10);
                AttitudeProvider = oProvider ?? new GravityOrientationProvider(Implement.FrameShift);
                AttitudeProvider.Activate();
                AverageAttitude.Update(AttitudeProvider);

                Activate();
            }

            protected override void startAction()
            {
                targetGlyph.FeedbackSFX.Play(Volume, true);
            }

            private double score;
            protected override bool nextStageCriterion()
            {
                score = targetGlyph.AngleTo(AttitudeProvider) - Stillness.StillnessScore / 4f - Sqrt(Stillness.RunTime.TotalSeconds);
                return (score < 12f && FrameShiftFunctions.CheckIsReady(AttitudeProvider));
            }
            protected override async Task nextStageActionAsync()
            {
                try
                {
                    Log.Info("Casting stages", $"Success on {this.Label}. Angle was {targetGlyph.AngleTo(AttitudeProvider):f2} degrees [spell baseline on this being {targetGlyph.OrientationSigma:f2}], " +
                        $"steadiness was {Stillness.StillnessScore:f2} [baseline {targetGlyph.SteadinessScoreWhenDefined:f2}], time was {Stillness.RunTime.TotalSeconds:f2}s [counted as {Math.Sqrt(Stillness.RunTime.TotalSeconds):f2} degrees].");
                    targetGlyph.FeedbackSFX.Stop();
                    await Task.Delay(150);

                    if (targetGlyph.NextGlyph == Glyph.EndOfSpell)
                    {
                        if (Implement != null) Implement.ZeroOrientation = Quaternion.Identity;
                        AttitudeProvider = null;

                        Plugin.Vibrate.CrossVibrate.Current.Vibration(50 + 15 * Current.SpellBeingCast.Glyphs.Count);
                        await Current.SpellBeingCast.CastingResult(this).Before(StopToken);
                        CurrentStage?.Deactivate();
                        CurrentStage = NullStage;
                        if (Current == null) return;
                        Current.SpellBeingCast = null;
                        Current.Finish();
                    }
                    else
                    {
                        Plugin.Vibrate.CrossVibrate.Current.Vibration(25 + 10 * Current.SpellBeingCast.Glyphs.IndexOf(targetGlyph));
                        targetGlyph.ProgressSFX.Play(1.0f);
                        await Task.Delay(300); // Give a moment to get ready.
                        CurrentStage = new GlyphCastingStage($"Glyph {Current.SpellBeingCast.Glyphs.IndexOf(targetGlyph) + 1}", Implement, targetGlyph.NextGlyph, AttitudeProvider);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Glyph casting stage progression", e.Message);
                    throw;
                }
            }

            private DateTime allowUpdate = DateTime.Now;
            private TimeSpan interval = TimeSpan.FromMilliseconds(100);
            protected override bool interimCriterion()
            {
                Stillness.IsItDisplayUpdateTime(); // Updates max and min values.
                if (DateTime.Now > allowUpdate)
                {
                    allowUpdate += interval;
                    return true;
                }
                else return false;
            }

            protected override void interimAction()
            {
                AverageAttitude.Update(AttitudeProvider.Quaternion);
                Volume = (float)Exp(-0.45f * (AttitudeProvider.Quaternion.AngleTo(targetGlyph.Orientation) / 5f - 1f));
                //Volume = (float)Exp(-0.5f * (Sqrt(AttitudeProvider.Quaternion.AngleTo(targetGlyph.Orientation)) - 1f)); // Old version
                targetGlyph.FeedbackSFX.SetVolume(Volume);

                var EulerAnglesOfError = Quaternion.Divide(AttitudeProvider, targetGlyph.Orientation).ToEulerAngles();
                string respString = null;

                if (Stillness.IsItDisplayUpdateTime())
                {
                    Stillness.DoDisplayUpdate();
                    respString = $"Casting {this.Label}.\n" +
                        $"Angle to target {targetGlyph.AngleTo(AttitudeProvider):f1} degrees (score {score:f1})\n" +
                        $"Volume set to {Volume * 100f:f1}%.";
                    Current.RelayMessage(respString, 2);
                }
            }
        }

    }
}