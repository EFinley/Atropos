////
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
//
//
////using Accord.Math;
////using Accord.Statistics;
//using Android.Content;
//using System.Threading.Tasks;
//using System.Numerics;
//using Vector3 = System.Numerics.Vector3;
//using Android.Views;
//using Android.Hardware;

//using static System.Math;
//using Log = Android.Util.Log; // Disambiguating with Math.Log( )... heh!
//using Nito.AsyncEx;
//using System.Threading;

//namespace Atropos
//{
//    /// <summary>
//    /// This is the activity started when we detect a "cast spell" NFC tag.
//    /// </summary>
//    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
//    public class StressTestingActivity : BaseActivity, IRelayMessages
//    {
//        private TextView outcomesDisplay;
//        private Spinner fileSpinner, fileTypeSpinner, updateSpeedSpinner, updateTypeSpinner;
//        private CheckBox doAdditionalMath;
//        private Button beginTestButton;
//        protected static StressTestingActivity Current { get { return (StressTestingActivity)CurrentActivity; } set { CurrentActivity = value; } }

//        private SFX testSFX;
//        private string fileDesc, fileExtension, updateType;
//        private int? updateIntervalInMillisec;

//        protected override void OnCreate(Bundle savedInstanceState)
//        {
//            base.OnCreate(savedInstanceState);
//            SetContentView(Resource.Layout.StressTestLayout);

//            outcomesDisplay = FindViewById<TextView>(Resource.Id.outcomeTextView);
//            fileSpinner = FindViewById<Spinner>(Resource.Id.spinnerTestSound);
//            fileTypeSpinner = FindViewById<Spinner>(Resource.Id.spinnerTestSoundFormat);
//            updateSpeedSpinner = FindViewById<Spinner>(Resource.Id.spinnerRefreshRate);
//            updateTypeSpinner = FindViewById<Spinner>(Resource.Id.spinnerRefreshType);
//            doAdditionalMath = FindViewById<CheckBox>(Resource.Id.doMathCheckbox);
//            beginTestButton = FindViewById<Button>(Resource.Id.beginTestButton);
            
//            fileSpinner.ItemSelected += new EventHandler<AdapterView.ItemSelectedEventArgs>(spinner_ItemSelected);
//            var fs_adapter = ArrayAdapter.CreateFromResource(
//                this, Resource.Array.SoundFilesArray, Android.Resource.Layout.SimpleSpinnerItem);
//            fs_adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
//            fileSpinner.Adapter = fs_adapter;

//            fileTypeSpinner.ItemSelected += new EventHandler<AdapterView.ItemSelectedEventArgs>(spinner_ItemSelected);
//            var fts_adapter = ArrayAdapter.CreateFromResource(
//                this, Resource.Array.SoundFileTypesArray, Android.Resource.Layout.SimpleSpinnerItem);
//            fts_adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
//            fileTypeSpinner.Adapter = fts_adapter;

//            updateSpeedSpinner.ItemSelected += new EventHandler<AdapterView.ItemSelectedEventArgs>(spinner_ItemSelected);
//            var us_adapter = ArrayAdapter.CreateFromResource(
//                this, Resource.Array.UpdateSpeedsArray, Android.Resource.Layout.SimpleSpinnerItem);
//            us_adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
//            updateSpeedSpinner.Adapter = us_adapter;

//            updateTypeSpinner.ItemSelected += new EventHandler<AdapterView.ItemSelectedEventArgs>(spinner_ItemSelected);
//            var uts_adapter = ArrayAdapter.CreateFromResource(
//                this, Resource.Array.UpdateTypesArray, Android.Resource.Layout.SimpleSpinnerItem);
//            uts_adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
//            updateTypeSpinner.Adapter = uts_adapter;

//            beginTestButton.Click += (o, e) =>
//            {
//                SetUIRunMode(false);
//                CurrentStage = new PerformStressTestStage("Performing stress test");
//            };

//            CurrentStage = GestureRecognizerStage.NullStage; // Don't launch automatically - wait for the button push.
//        }
//        protected override async void OnResume()
//        {
//            await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: true);
//            SetUIRunMode(true);
//        }

//        protected override void OnPause()
//        {
//            base.OnPause();
//        }

//        public void RelayMessage(string message, int relayTargetID = -1) { }

//        public void SetUIRunMode(bool status)
//        {
//            beginTestButton.Enabled = status;
//            fileSpinner.Focusable = status;
//            fileTypeSpinner.Focusable = status;
//            updateSpeedSpinner.Focusable = status;
//            updateSpeedSpinner.Focusable = status;
//        }

//        public void spinner_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
//        {
//            Spinner spinner = (Spinner)sender;
//            string selectedText = (string)spinner.GetItemAtPosition(e.Position);

//            //string toast = string.Format("The planet is {0}", spinner.GetItemAtPosition(e.Position));
//            if (spinner == fileSpinner)
//            {

//            }
//        }

//        public class PerformStressTestStage : GestureRecognizerStage
//        {
//            private StillnessProvider Stillness;
//            private float Volume;
//            private AdvancedRollingAverageQuat AverageAttitude;
//            private GravityOrientationProvider AttitudeProvider;
//            private Quaternion targetOrientation = Quaternion.Identity; // For now!

//            public PerformStressTestStage(string label, bool AutoStart = true) : base(label)
//            {
//                Stillness = new StillnessProvider();
//                SetUpProvider(Stillness);

//                Volume = 0.01f;

//                AverageAttitude = new AdvancedRollingAverageQuat(timeFrameInPeriods: 10);
//                AttitudeProvider = new GravityOrientationProvider();
//                AttitudeProvider.Activate();
//                AverageAttitude.Update(AttitudeProvider);

//                if (AutoStart) Activate();
//            }

//            protected override void startAction()
//            {
//                targetGlyph.FeedbackSFX.Play(Volume, true);
//            }

//            private double score;
//            protected override bool nextStageCriterion()
//            {
//                score = targetOrientation.AngleTo(AttitudeProvider) - Stillness.StillnessScore / 4f - Sqrt(Stillness.RunTime.TotalSeconds);
//                //return (score < 12f && FrameShiftFunctions.CheckIsReady(AttitudeProvider));
//                return (score < 12f);
//            }
//            protected override async Task nextStageActionAsync()
//            {
//                try
//                {
//                    Log.Info("Casting stages", $"Success on {this.Label}. Angle was {targetGlyph.AngleTo(AttitudeProvider):f2} degrees [spell baseline on this being {targetGlyph.OrientationSigma:f2}], " +
//                        $"steadiness was {Stillness.StillnessScore:f2} [baseline {targetGlyph.SteadinessScoreWhenDefined:f2}], time was {Stillness.RunTime.TotalSeconds:f2}s [counted as {Math.Sqrt(Stillness.RunTime.TotalSeconds):f2} degrees].");
//                    targetGlyph.FeedbackSFX.Stop();
//                    await Task.Delay(150);

//                    if (targetGlyph.NextGlyph == Glyph.EndOfSpell)
//                    {
//                        if (Implement != null) Implement.ZeroOrientation = Quaternion.Identity;
//                        AttitudeProvider = null;

//                        Plugin.Vibrate.CrossVibrate.Current.Vibration(50 + 15 * Current.SpellBeingCast.Glyphs.Count);
//                        await Current.SpellBeingCast.CastingResult(this).Before(StopToken);
//                        CurrentStage?.Deactivate();
//                        CurrentStage = NullStage;
//                        if (Current == null) return;
//                        Current.SpellBeingCast = null;
//                        Current.Finish();
//                    }
//                    else
//                    {
//                        Plugin.Vibrate.CrossVibrate.Current.Vibration(25 + 10 * Current.SpellBeingCast.Glyphs.IndexOf(targetGlyph));
//                        targetGlyph.ProgressSFX.Play(1.0f);
//                        await Task.Delay(300); // Give a moment to get ready.
//                        CurrentStage = new GlyphCastingStage($"Glyph {Current.SpellBeingCast.Glyphs.IndexOf(targetGlyph) + 1}", Implement, targetGlyph.NextGlyph, AttitudeProvider);
//                    }
//                }
//                catch (Exception e)
//                {
//                    Log.Error("Glyph casting stage progression", e.Message);
//                    throw;
//                }
//            }

//            protected override bool interimCriterion()
//            {
//                Stillness.IsItDisplayUpdateTime(); // Updates max and min values.
//                return true;
//            }

//            protected override void interimAction()
//            {
//                AverageAttitude.Update(AttitudeProvider.Quaternion);
//                Volume = (float)Exp(-0.45f * (AttitudeProvider.Quaternion.AngleTo(targetGlyph.Orientation) / 5f - 1f));
//                //Volume = (float)Exp(-0.5f * (Sqrt(AttitudeProvider.Quaternion.AngleTo(targetGlyph.Orientation)) - 1f)); // Old version
//                targetGlyph.FeedbackSFX.SetVolume(Volume);

//                var EulerAnglesOfError = Quaternion.Divide(AttitudeProvider, targetGlyph.Orientation).ToEulerAngles();
//                string respString = null;

//                if (Stillness.IsItDisplayUpdateTime())
//                {
//                    Stillness.DoDisplayUpdate();
//                    respString = $"Casting {this.Label}.\n" +
//                        $"Angle to target {targetGlyph.AngleTo(AttitudeProvider):f1} degrees (score {score:f1})\n" +
//                        $"Volume set to {Volume * 100f:f1}%.";
//                    Current.RelayMessage(respString, true);
//                }
//            }
//        }

//    }
//}