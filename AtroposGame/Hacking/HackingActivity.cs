
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
using Android.Animation;

namespace Atropos.Hacking
{
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class HackingActivity : BaseActivity_Portrait
    {
        protected HackTaskDisplayAdapter adapter;
        public HackingTask CurrentTask;
        public List<HackingTask> TaskList = new List<HackingTask>();
        protected ListView listView;
        protected RelativeLayout mainPanel;
        protected static HackingActivity Current { get { return (HackingActivity)CurrentActivity; } set { CurrentActivity = value; } }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.SimpleListPage);

            var lView = FindViewById(Resource.Id.list);
            var mPanel = FindViewById(Resource.Id.listpage_backdrop);

            listView = lView as ListView;
            mainPanel = mPanel as RelativeLayout;
            
            useVolumeTrigger = true;
            OnVolumeButtonPressed += (o, e) => PerformSpellSelection();
        }
        protected override async void OnResume()
        {
            await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: true);
            InitHackingOpportunity();
        }

        protected override void OnPause()
        {
            base.OnPause();
        }

        protected async void PerformSpellSelection()
        {
            //useVolumeTrigger = false;

            //try
            //{
            //    //Log.Debug("SpellSelection", $"Diagnostics: #sensors {Res.NumSensors}; CurrentStage {(CurrentStage as GestureRecognizerStage)?.Label}; Background stages activity: {BaseActivity.BackgroundStages?.Select(s => $"{(s as GestureRecognizerStage)?.Label}:{s.IsActive}").Join()}");
            //    var MagnitudeGlyph = await new GlyphCastStage(Glyph.MagnitudeGlyphs, Glyph.StartOfSpell, 2.0).AwaitResult();
            //    double timeCoefficient = (MagnitudeGlyph == Glyph.L) ? 2.0 :
            //                             (MagnitudeGlyph == Glyph.M) ? 0.75 :
            //                             (MagnitudeGlyph == Glyph.H) ? -2.0 :
            //                             -5.0; // for Magnitude == Glyph.G
            //    var SpellTypeGlyph = await new GlyphCastStage(Glyph.SpellTypeGlyphs, MagnitudeGlyph, timeCoefficient).AwaitResult();

            //    // Now figure out what possible third "key" glyphs exist given our spell list.
            //    var PossibleSpells = HackTaskDefinition.AllSpells
            //                                        .Where(s => s.Magnitude == MagnitudeGlyph && s.SpellType == SpellTypeGlyph)
            //                                        .ToDictionary(s => s.KeyGlyph);
            //    var KeyGlyph = await new GlyphCastStage(PossibleSpells.Keys, SpellTypeGlyph, timeCoefficient).AwaitResult();
            //    ChosenSpell = PossibleSpells.GetValueOrDefault(KeyGlyph);

            //    // Now that we know that...
            //    var lastGlyph = KeyGlyph;
            //    var RemainingGlyphs = ChosenSpell.Glyphs.Skip(3); // The three we already extracted above, of course.
            //    foreach (var glyph in RemainingGlyphs)
            //    {
            //        await new GlyphCastStage(glyph, lastGlyph, timeCoefficient).AwaitResult();
            //        lastGlyph = glyph;
            //    }
            //    Task.Run(() => ChosenSpell?.OnSelection(Current)) // Passing the Activity allows us to ask it to do things (like animate properties or other UI stuff)
            //        .LaunchAsOrphan($"Effects of {ChosenSpell.TaskName}");
            //    //Finish();
            //}
            //catch (TaskCanceledException)
            //{
            //    //ChosenSpell?.FailResult?.Invoke(null);
            //    Log.Debug("GlyphCast", $"Spell selection / casting cancelled during glyph casting.");
            //    //Finish();
            //}
            //finally
            //{
            //    useVolumeTrigger = true;
            //}
        }

        private void InitHackingOpportunity()
        {
            TaskList.AddRange(new List<HackingTask>()
            {
                new HackingTask("System Scan", "clickDragTR"),
                new HackingTask("Elevate Privileges", "loopTL")
            });

            //// Set up the barrier glow effect
            //ImageView barrierEffect = new ImageView(this);
            //barrierEffect.SetImageResource(Resource.Drawable.barrier_glow);
            //mainPanel.AddView(barrierEffect);

            //SpellDefinition.Barrier.OnChange += (o, e) =>
            //{
            //    barrierEffect.Alpha = (float)(double.Parse(e.Value["Magnitude"]) / (double)SpellDefinition.Barrier.Parameters["BarrierBaseMagnitude"]);
            //};

            //// Set up the shield spell visual and its animator
            //ImageView shieldSpellGraphic = new ImageView(this);
            //shieldSpellGraphic.Visibility = ViewStates.Gone;
            //shieldSpellGraphic.SetImageResource(Resource.Drawable.shield_rune_ring);
            //mainPanel.AddView(shieldSpellGraphic);

            //var ObjAnimator = ObjectAnimator.OfFloat(shieldSpellGraphic, "rotation", 0, 360);
            //ObjAnimator.RepeatMode = ValueAnimatorRepeatMode.Restart;
            //ObjAnimator.RepeatCount = ValueAnimator.Infinite;
            //ObjAnimator.SetDuration(2500);
            //StopToken.Register(ObjAnimator.End);

            //SpellDefinition.Shield.OnStart += (o, e) =>
            //{
            //    Current.RunOnUiThread(() =>
            //    {
            //        shieldSpellGraphic.Visibility = ViewStates.Visible;
            //        // TODO - Begin spinning here.
            //        ObjAnimator.Start();
            //    });
            //};
            //SpellDefinition.Shield.OnEnd += (o, e) =>
            //{
            //    Current.RunOnUiThread(() =>
            //    {
            //        shieldSpellGraphic.Visibility = ViewStates.Gone;
            //        ObjAnimator.End();
            //        shieldSpellGraphic.Rotation = 0;
            //    });
            //};
        }

        public void UpdateListView()
        {
            adapter = new HackTaskDisplayAdapter(this, TaskList);
            listView.Adapter = adapter;
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
                AverageStillness = new RollingAverage<float>(70);

                //AttitudeProvider = new GravityOrientationProvider();
                //AttitudeProvider.Activate();
                GravityProvider = new Vector3Provider(SensorType.Gravity);
                GravityProvider.Activate(StopToken);

                ValidGlyphs = validGlyphs.ToList();
                LastGlyph = lastGlyph;
                TimeCoefficient = timeCoefficient;

                var activity = HackingActivity.Current;
                activity.adapter = new HackTaskDisplayAdapter(activity, Current.TaskList);
                activity.RunOnUiThread(() => activity.listView.Adapter = activity.adapter);

                DependsOn(Current.StopToken);
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
                var ease = Hacker.Me.EaseOfTasks;
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

                    var activity = HackingActivity.Current;
                    activity.adapter = new HackTaskDisplayAdapter(activity, Current.TaskList);
                    activity.RunOnUiThread(() => activity.listView.Adapter = activity.adapter);

                    //AttitudeProvider.Deactivate();
                    //GravityProvider.Deactivate();

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
                    var ease = Hacker.Me.EaseOfTasks;
                    GetFeedbackFX(s).Volume = 1.2f * Exp(-0.45f * (s.AngleTo(GravityProvider) / ease / 5f - 1f));
                }
                //AverageStillness.Update(Stillness.StillnessScore);
            }

            protected override bool abortCriterion()
            {
                AverageStillness.Update(Stillness.StillnessScore);
                return Stillness.RunTime > TimeSpan.FromMilliseconds(1250) && AverageStillness < -18;
            }

            protected override void abortAction()
            {
                foreach (var glyph in ValidGlyphs)
                {
                    var fx = glyph.FeedbackSFX;
                    fx.Stop();
                    fx.Deactivate();
                    glyph.FeedbackSFX = null;
                }

                Plugin.Vibrate.CrossVibrate.Current.Vibration(50);

                var activity = HackingActivity.Current;
                activity.adapter = new HackTaskDisplayAdapter(activity, Current.TaskList);
                activity.RunOnUiThread(() => activity.listView.Adapter = activity.adapter);

                StageCancel();
            }
        }
    }

    public class HackTaskDisplayAdapter : BaseAdapter<HackingTask>
    {
        private readonly Activity _context;
        private readonly List<HackingTask> _items;

        public HackTaskDisplayAdapter(Activity context, List<HackingTask> items)
            : base()
        {
            _context = context;
            _items = items;
        }

        public override long GetItemId(int position)
        {
            return position;
        }
        public override HackingTask this[int position]
        {
            get { return _items[position]; }
        }
        public override int Count
        {
            get { return _items.Count; }
        }

        protected LinearLayout.LayoutParams PercentLayout(double amt, bool horizontal = false)
        {
            if (!horizontal)
                return new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MatchParent, 0, (float)amt);
            else return new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MatchParent, (float)amt);
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var v = convertView;

            v = v ?? _context.LayoutInflater.Inflate(Resource.Layout.GlyphDisplayListItemRepresentation, null);

            HackingTask htask = _items[position];
            if (htask == null) return v;

            htask.SuccessBar.Bar = v.FindViewById<LinearLayout>(Resource.Id.htask_success_bar);
            htask.RiskBar.Bar = v.FindViewById<LinearLayout>(Resource.Id.htask_risk_bar);
            htask.Graphic.GraphicsPane = v.FindViewById<RelativeLayout>(Resource.Id.htask_graphic);
            htask.BackgroundView = v.FindViewById<View>(Resource.Id.htask_background);
            var SymbolsPane = v.FindViewById<LinearLayout>(Resource.Id.htask_symbols);

            var ctx = (HackingActivity)_context;
            foreach (var Bar in new HackingBar[] { htask.SuccessBar, htask.RiskBar })
            {
                var black_part = new View(_context) { LayoutParameters = PercentLayout(Bar.RemainingPercentage), Alpha = 0.1f };
                black_part.SetBackgroundColor(Bar.Colour);
                Bar.Bar.AddView(black_part);

                var upper_part = new View(_context) { LayoutParameters = PercentLayout(Bar.TopPercentage), Alpha = 0.45f };
                upper_part.SetBackgroundColor(Bar.Colour);
                Bar.Bar.AddView(upper_part);

                var lower_part = new View(_context) { LayoutParameters = PercentLayout(Bar.BottomPercentage) };
                lower_part.SetBackgroundColor(Bar.Colour);
                Bar.Bar.AddView(lower_part);
            }

            htask.Graphic.SetImageResource(_context, htask);

            if (htask == ((HackingActivity)BaseActivity.CurrentActivity).CurrentTask)
            {
                htask.BackgroundView.SetBackgroundColor(Android.Graphics.Color.CornflowerBlue);
                htask.BackgroundView.Alpha = 0.1f;

                if (htask.GestureA != null)
                {
                    var symbolA = new ImageView(_context);
                    symbolA.SetImageResource(htask.GestureA.IconID);
                    SymbolsPane.AddView(symbolA);
                }
                if (htask.GestureB != null)
                {
                    var symbolB = new ImageView(_context);
                    symbolB.SetImageResource(htask.GestureB.IconID);
                    SymbolsPane.AddView(symbolB);
                }
            }
            else
            {
                if (htask.SelectionGesture != null)
                {
                    var symbol = new ImageView(_context);
                    symbol.SetImageResource(htask.SelectionGesture.IconID);
                    SymbolsPane.AddView(symbol);
                }
            }

            return v;
        }
    }
}