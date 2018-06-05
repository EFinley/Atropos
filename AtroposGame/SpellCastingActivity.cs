
using Android.App;
using Android.Hardware;
using Android.OS;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
//using Accord.Math;
//using Accord.Statistics;
using System.Threading.Tasks;
using static System.Math;
using Log = Android.Util.Log; // Disambiguating with Math.Log( )... heh!

namespace Atropos
{
    /// <summary>
    /// This is the activity started when we detect a "cast spell" NFC tag.
    /// </summary>
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class SpellCastingActivity : BaseActivity_Portrait // , IRelayMessages
    {
        private Focus ThePlayersFocus;
        private Spell SpellBeingCast;
        private TextView currentSignalsDisplay, bestSignalsDisplay, resultsDisplay;
        private List<Button> spellButtons = new List<Button>();

        protected GlyphDisplayAdapter adapter;
        protected ListView listView;
        protected RelativeLayout mainPanel;
        protected static SpellCastingActivity Current { get { return (SpellCastingActivity)CurrentActivity; } set { CurrentActivity = value; } }

        private CancellationTokenSource _cts;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.SpellCasting);

            var lView = FindViewById(Resource.Id.list);
            var mPanel = FindViewById(Resource.Id.spell_page_main);

            listView = lView as ListView;
            mainPanel = mPanel as RelativeLayout;

            //SetUpSpellButtons();

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

            //// Debugging purposes - give everybody all the spells.
            //ThePlayersFocus.KnownSpells.Clear();
            //foreach (string spellName in MasterSpellLibrary.spellNames)
            //{
            //    if (spellName == Spell.None.SpellName) continue;
            //    ThePlayersFocus.LearnSpell(MasterSpellLibrary.Get(spellName));
            //}

            //CurrentStage = GestureRecognizerStage.NullStage; // Used if we're relying purely on buttons onscreen, as in debugging.
            //CurrentStage = new BeginCastingSelectedSpellsStage("Scanning for zero stances", ThePlayersFocus, ThePlayersFocus.KnownSpells);

            useVolumeTrigger = true;
            OnVolumeButtonPressed += (o, e) => PerformSpellSelection();
        }
        protected override async void OnResume()
        {
            await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: true);
            DoSpellUILoop();
            //PerformSpellSelection();
        }

        protected override void OnPause()
        {
            _cts?.Cancel();
            base.OnPause();
        }

        protected async void PerformSpellSelection()
        {
            SpellResult ChosenSpell = null;
            useVolumeTrigger = false;

            try
            {
                Log.Debug("SpellSelection", $"Diagnostics: #sensors {Res.NumSensors}; CurrentStage {(CurrentStage as GestureRecognizerStage)?.Label}; Background stages activity: {BaseActivity.BackgroundStages?.Select(s => $"{(s as GestureRecognizerStage)?.Label}:{s.IsActive}").Join()}");
                var MagnitudeGlyph = await new GlyphCastStage(Glyph.MagnitudeGlyphs, Glyph.StartOfSpell, 2.0).AwaitResult();
                double timeCoefficient = (MagnitudeGlyph == Glyph.L) ? 2.0 :
                                         (MagnitudeGlyph == Glyph.M) ? 0.75 :
                                         (MagnitudeGlyph == Glyph.H) ? -0.25 :
                                         -1.0; // for Magnitude == Glyph.G
                var SpellTypeGlyph = await new GlyphCastStage(Glyph.SpellTypeGlyphs, MagnitudeGlyph, timeCoefficient).AwaitResult();

                // Now figure out what possible third "key" glyphs exist given our spell list.
                var PossibleSpells = SpellResult.AllSpells
                                                    .Where(s => s.Magnitude == MagnitudeGlyph && s.SpellType == SpellTypeGlyph)
                                                    .ToDictionary(s => s.KeyGlyph);
                var KeyGlyph = await new GlyphCastStage(PossibleSpells.Keys, SpellTypeGlyph, timeCoefficient).AwaitResult();
                ChosenSpell = PossibleSpells.GetValueOrDefault(KeyGlyph);

                // Now that we know that...
                var lastGlyph = KeyGlyph;
                var RemainingGlyphs = ChosenSpell.Glyphs.Skip(3); // The three we already extracted above, of course.
                foreach (var glyph in RemainingGlyphs)
                {
                    await new GlyphCastStage(glyph, lastGlyph, timeCoefficient).AwaitResult();
                    lastGlyph = glyph;
                }
                Task.Run(() => ChosenSpell?.OnCast(Current)) // Passing the Activity allows us to ask it to do things (like animate properties or other UI stuff)
                    .LaunchAsOrphan($"Effects of {ChosenSpell.SpellName}");
                //Finish();
            }
            catch (TaskCanceledException)
            {
                //ChosenSpell?.FailResult?.Invoke(null);
                Log.Debug("GlyphCast", $"Spell selection / casting cancelled during glyph casting.");
                //Finish();
            }
            finally
            {
                useVolumeTrigger = true;
            }
        }

        private async void DoSpellUILoop()
        {
            _cts = new CancellationTokenSource();

            // Set up the shield spell visual and its animator
            ImageView shieldSpellGraphic = new ImageView(this);
            shieldSpellGraphic.Visibility = ViewStates.Gone;
            shieldSpellGraphic.SetImageResource(Resource.Drawable.qr_code_reticle_with_glow);

            // Set up the barrier glow effect
            ImageView barrierEffect = new ImageView(this);
            barrierEffect.Alpha = (float)(Characters.Damageable.Me.Barrier / SpellResult.BarrierBaseMagnitude);
            barrierEffect.SetImageResource(Resource.Drawable.qr_code_window);
            mainPanel.AddView(barrierEffect);

            while (!_cts.IsCancellationRequested)
            {
                await Task.Delay(100);
                var BarrierOpacity = (Characters.Damageable.Me.Barrier / SpellResult.BarrierBaseMagnitude ).Clamp(0, 1);
            }
        }

        public class GlyphCastStage : AwaitableStage<Glyph>
        {
            public StillnessProvider Stillness;
            //private FrameShiftedOrientationProvider AttitudeProvider;
            private Vector3Provider GravityProvider;
            private RollingAverage<float> AverageStillness;

            private double TimeCoefficient;

            private List<Glyph> ValidGlyphs;
            private Glyph LastGlyph;
            private List<Glyph> GlyphsSortedByAngle
            {
                get
                {
                    return ValidGlyphs
                            .OrderBy(g => g.AngleTo(GravityProvider))
                            .ToList();
                }
            }
            private IEffect GetFeedbackFX(Glyph glyph)
            {
                if (glyph.FeedbackSFX == null) glyph.FeedbackSFX = new Effect(glyph.FeedbackSFXName, glyph.FeedbackSFXid);
                return glyph.FeedbackSFX;
                //return glyph?.FeedbackSFX ?? Effect.None;
            }

            public GlyphCastStage(IEnumerable<Glyph> validGlyphs, Glyph lastGlyph, double timeCoefficient, bool autoStart = true)
                : base($"Selecting glyphs among {String.Join("/", validGlyphs)}.")
            {
                Res.DebuggingSignalFlag = true;

                Stillness = new StillnessProvider();
                //Stillness.StartDisplayLoop(Current, 500);
                SetUpProvider(Stillness);
                AverageStillness = new RollingAverage<float>(30);

                //AttitudeProvider = new GravityOrientationProvider();
                //AttitudeProvider.Activate();
                GravityProvider = new Vector3Provider(SensorType.Gravity);
                GravityProvider.Activate();

                ValidGlyphs = validGlyphs.ToList();
                LastGlyph = lastGlyph;
                TimeCoefficient = timeCoefficient;

                var activity = SpellCastingActivity.Current;
                activity.adapter = new GlyphDisplayAdapter(activity, ValidGlyphs);
                activity.RunOnUiThread(() => activity.listView.Adapter = activity.adapter);

                if (autoStart) Activate();
            }

            public GlyphCastStage(Glyph glyph, Glyph lastGlyph, double timeCoefficient, bool autoStart = true)
                : this(new Glyph[] { glyph }, lastGlyph, timeCoefficient, autoStart) { }

            protected override async Task startActionAsync()
            {
                foreach (var fx in ValidGlyphs.Select(s => GetFeedbackFX(s))) fx.Play(0.0, true);

                if (ValidGlyphs == null || ValidGlyphs.Count == 0)
                {
                    await Speech.SayAllOf("Casting mistake.  You don't know any spells which begin with that sequence of glyphs.");
                    Deactivate();
                }
            }

            protected override bool nextStageCriterion()
            {
                //if (!FrameShiftFunctions.CheckIsReady(AttitudeProvider)) return false;
                if (Stillness.RunTime.TotalSeconds < 0.75) return false;
                var leeWay = Stillness.StillnessScore + TimeCoefficient * Sqrt(Stillness.RunTime.TotalSeconds);
                var ease = SpellCaster.Me.EaseOfCasting;
                if (GlyphsSortedByAngle[0].AngleTo(GravityProvider) < ease * (10f + 1.5 * leeWay)) // Was 25.0f + leeway, seems awfully generous
                {
                    if (LastGlyph != Glyph.StartOfSpell && GlyphsSortedByAngle[0].AngleTo(GravityProvider) > LastGlyph.AngleTo(GravityProvider)) return false;
                    if (ValidGlyphs.Count < 2) return true;
                    else return (GlyphsSortedByAngle[0].AngleTo(GravityProvider) < 0.5 * GlyphsSortedByAngle[1].AngleTo(GravityProvider));
                }
                return false;
            }

            protected override void nextStageAction()
            {
                try
                {
                    //foreach (var fx in ValidGlyphs.Select(s => GetFeedbackFX(s))) fx.Deactivate();
                    foreach (var glyph in ValidGlyphs)
                    {
                        var fx = glyph.FeedbackSFX;
                        fx.Stop();
                        fx.Deactivate();
                        glyph.FeedbackSFX = null;
                    }

                    //Current.SpellBeingCast = GlyphsSortedByAngle[0];
                    //Current.SetAllSpellButtonsEnabledState(false);

                    Plugin.Vibrate.CrossVibrate.Current.Vibration(15);
                    GlyphsSortedByAngle[0].ProgressSFX?.Play();
                    //AttitudeProvider.FrameShift = Current.SpellBeingCast.ZeroStance;
                    //CurrentStage = new GlyphCastingStage($"{Current.SpellBeingCast.SpellName} Glyph 0", Focus, Current.SpellBeingCast.Glyphs[0], AttitudeProvider);

                    var activity = SpellCastingActivity.Current;
                    activity.adapter = new GlyphDisplayAdapter(activity, new List<Glyph>());
                    activity.RunOnUiThread(() => activity.listView.Adapter = activity.adapter);

                    //AttitudeProvider.Deactivate();
                    GravityProvider.Deactivate();

                    StageReturn(GlyphsSortedByAngle[0]);
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
                foreach (var s in GlyphsSortedByAngle.Take(1))
                {
                    //FeedbackFX(s).Volume = Exp(-Sqrt(s.AngleTo(AttitudeProvider) / 8.0) + 0.65); // Old version
                    var ease = SpellCaster.Me.EaseOfCasting;
                    GetFeedbackFX(s).Volume = 1.2f * Exp(-0.45f * (s.AngleTo(GravityProvider) / ease / 5f - 1f));
                }
                //AverageStillness.Update(Stillness.StillnessScore);
            }

            protected override bool abortCriterion()
            {
                AverageStillness.Update(Stillness.StillnessScore);
                return Stillness.RunTime > TimeSpan.FromMilliseconds(1250) && AverageStillness < -15;
            }

            protected override void abortAction()
            {
                StageCancel();
            }
        }
    }

    public class GlyphDisplayAdapter : BaseAdapter<Glyph>
    {
        private readonly Activity _context;
        private readonly List<Glyph> _items;

        public GlyphDisplayAdapter(Activity context, List<Glyph> items)
            : base()
        {
            _context = context;
            _items = items;
        }

        public override long GetItemId(int position)
        {
            return position;
        }
        public override Glyph this[int position]
        {
            get { return _items[position]; }
        }
        public override int Count
        {
            get { return _items.Count; }
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var v = convertView;

            v = v ?? _context.LayoutInflater.Inflate(Resource.Layout.GlyphDisplayListItemRepresentation, null);

            var PictureField = v.FindViewById<ImageView>(Resource.Id.glyph_picture);
            var NameField = v.FindViewById<TextView>(Resource.Id.glyph_name);
            var InstructionField = v.FindViewById<TextView>(Resource.Id.glyph_instruction);

            Glyph glyph = _items[position];
            if (glyph == null) return v;

            var ctx = (SpellCastingActivity)_context;
            NameField.Text = glyph.Name;
            InstructionField.Text = glyph.Instruction_Short;

            return v;
        }
    }
}