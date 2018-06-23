
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
using Android.Runtime;
using SimpleFileDialog = Atropos.External_Code.SimpleFileDialog;
using System.IO;
using Java.IO;
using File = Java.IO.File;
using System.Threading;
using Nito.AsyncEx;
using PerpetualEngine.Storage;
using MiscUtil;
using Atropos.DataStructures;
using Atropos.Machine_Learning;
using Atropos.Machine_Learning.Button_Logic;
using DKS = Atropos.DataStructures.DatapointSpecialVariants.DatapointKitchenSink;
using Android.Content.Res;

namespace Atropos.Melee
{
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait, WindowSoftInputMode = SoftInput.AdjustPan)]
    public class MeleeBetaActivity 
        : MachineLearningActivity<DKS>
    {
        public const string OFFENSE = "MeleeOffense";
        public const string DEFENSE = "MeleeDefense";

        protected static new MeleeBetaActivity Current { get { return (MeleeBetaActivity)CurrentActivity; } set { CurrentActivity = value; } }
        protected static new MachineLearningStage CurrentStage
        {
            get { return (MachineLearningStage)BaseActivity.CurrentStage; }
            set { BaseActivity.CurrentStage = value; }
        }

        #region Required inheritance members
        protected override void ResetStage(string label)
        {
            CurrentStage = new MachineLearningStage(label, Dataset,
                new LoggingSensorProvider<DKS>(new AdvancedProviders.GrabItAllSensorProvider(new GravGyroOrientationProvider())));
        }

        //protected override void AlternativeResetStage(string label, params object[] info)
        //{
        //    Classifier c;

        //    if (info != null && info.Length >= 1)
        //    {
        //        if (info[0] is string s && CueClassifiers.ContainsKey(s)) c = CueClassifiers[s];
        //        else if (info[0] is GestureClass gc && CueClassifiers.ContainsKey(gc.className)) c = CueClassifiers[gc.className];
        //        else c = Classifier;
        //    }
        //    else c = Classifier;
        //    CurrentGestureStage = new SelfEndpointingSingleGestureRecognizer(label, c,
        //                new LoggingSensorProvider<DKS>(new AdvancedProviders.GrabItAllSensorProvider(new GravGyroOrientationProvider())));
        //}
        #endregion

        #region Data Members (and tightly linked functions / properties)
        //protected TextView _gestureClassLabel,
        //                   _qualityThis, _qualityAvg, _qualityRatio,
        //                   _delayThis, _delayAvg, _delayRatio,
        //                   _durationThis, _durationAvg, _durationRatio,
        //                   _numptsThis, _numptsAvg, _numptsRatio,
        //                   _pkaccelThis, _pkaccelAvg, _pkaccelRatio;
        //protected Button _userTrainingBtn, 
        //    _cuedSingleBtn, 
        //    _cuedSeriesBtn;

        protected bool _userTrainingMode { get; set; } = false;
        protected bool _singleMode { get; set; } = false;
        protected bool _seriesMode { get; set; } = true;

        protected bool _collectingData = false;
        protected bool _listeningForGesture = false;
        protected bool _advancedCueMode { get => false; }// { get { return !_userTrainingMode && _seriesMode; } }
        protected IChoreographer Choreographer { get; set; }
        protected ChoreographyCue CurrentCue;

        protected System.Diagnostics.Stopwatch Stopwatch = new System.Diagnostics.Stopwatch();
        protected TimeSpan Delay;

        public virtual double GetAccelForDatapoint(DKS datapoint) { return datapoint.LinAccel.Length(); }

        //protected void FindAllViews()
        //{
        //    _gestureClassLabel = FindViewById<TextView>(Resource.Id.melee_gc_label);

        //    _qualityThis = FindViewById<TextView>(Resource.Id.melee_qual_this);
        //    _qualityAvg = FindViewById<TextView>(Resource.Id.melee_qual_avg);
        //    _qualityRatio = FindViewById<TextView>(Resource.Id.melee_qual_ratio);

        //    _delayThis = FindViewById<TextView>(Resource.Id.melee_delay_this);
        //    _delayAvg = FindViewById<TextView>(Resource.Id.melee_delay_avg);
        //    _delayRatio = FindViewById<TextView>(Resource.Id.melee_delay_ratio);

        //    _durationThis = FindViewById<TextView>(Resource.Id.melee_dur_this);
        //    _durationAvg = FindViewById<TextView>(Resource.Id.melee_dur_avg);
        //    _durationRatio = FindViewById<TextView>(Resource.Id.melee_dur_ratio);

        //    _numptsThis = FindViewById<TextView>(Resource.Id.melee_numpts_this);
        //    _numptsAvg = FindViewById<TextView>(Resource.Id.melee_numpts_avg);
        //    _numptsRatio = FindViewById<TextView>(Resource.Id.melee_numpts_ratio);

        //    _pkaccelThis = FindViewById<TextView>(Resource.Id.melee_pkaccel_this);
        //    _pkaccelAvg = FindViewById<TextView>(Resource.Id.melee_pkaccel_avg);
        //    _pkaccelRatio = FindViewById<TextView>(Resource.Id.melee_pkaccel_ratio);

        //    _userTrainingBtn = FindViewById<Button>(Resource.Id.melee_user_training_btn);
        //    _cuedSingleBtn = FindViewById<Button>(Resource.Id.melee_cued_single_btn);
        //    _cuedSeriesBtn = FindViewById<Button>(Resource.Id.melee_cued_series_btn);
        //}

        #endregion

        #region Activity Lifecycle Methods
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.SimpleListPage);
            //FindAllViews();

            //GestureClassList = FragmentManager.FindFragmentById<GestureClassListFragment>(Resource.Id.mlrn_gestureclass_list_fragment);
            //LatestSample = FragmentManager.FindFragmentById<LatestSampleFragment>(Resource.Id.mlrn_latest_sample_display);

            Dataset = new DataSet<DKS>();
            Classifier = new Classifier(); // Just in case; it's not actually going to get used in this version.

            //SetUpButtonClicks();
        }

        protected override async void OnResume()
        {
            await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: false);
            LoadMeleeClassifier();

            //var newSW = new System.Diagnostics.Stopwatch();
            //var chor = new Choreographer(CueClassifiers[OFFENSE], CueClassifiers[DEFENSE], 500, 100);

            //int i = 0;
            //chor.OnSendCue += (o, e) =>
            //{
            //    Log.Debug("ChoreographerTest", $"Cueing {e.Value.GestureClass.className}.");
            //    Task.Delay(100).Wait();
            //    if (i++ < 20) chor.ProceedWithNextCue();
            //};
            //chor.Activate();
        }

        protected override void OnPause()
        {
            base.OnPause();
        }
        #endregion

        #region Options Menu stuff
        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            var inflater = MenuInflater;
            inflater.Inflate(Resource.Menu.action_items, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if (item.ItemId == Resource.Id.menuaction_character)
            {
                Toast.MakeText(this, Resource.String.popup_placeholder_character, ToastLength.Short).Show();
                return true;
            }
            else if (item.ItemId == Resource.Id.menuaction_wifi)
            {
                LaunchDirectly(typeof(Communications.Bluetooth.BTDirectActivity));
                return true;
            }
            else if (item.ItemId == Resource.Id.menuaction_settings)
            {
                LaunchDirectly(typeof(SettingsActivity));
                return true;
            }
            else return base.OnOptionsItemSelected(item);
        }
        #endregion

        #region Button Click / Volume Button Hold events
        public override bool OnKeyDown([GeneratedEnum] Keycode keyCode, KeyEvent e)
        {
            if (keyCode == Keycode.VolumeDown || keyCode == Keycode.VolumeUp)
            {
                lock (SelectedGestureClass)
                {
                    if (!_collectingData)
                    {
                        _collectingData = true;

                        ResetStage("Melee stage");
                        //CuePrompter?.MarkGestureStart();
                        Stopwatch.Stop();
                        Delay = Stopwatch.Elapsed;
                        CurrentStage.Activate();
                        return true;
                    }
                }
                if (_collectingData) return true;
            }
            return base.OnKeyDown(keyCode, e);
        }
        public override bool OnKeyUp([GeneratedEnum] Keycode keyCode, KeyEvent e)
        {
            if (keyCode == Keycode.VolumeDown || keyCode == Keycode.VolumeUp)
            {
                if (_collectingData)
                {
                    lock (SelectedGestureClass)
                    {
                        // Respond to single-click events
                        if (CurrentStage.RunTime < TimeSpan.FromSeconds(0.25))
                        {
                            if (Choreographer == null)
                            {
                                if (!CueClassifiers.ContainsKey(OFFENSE)) throw new Exception($"{OFFENSE} classifier not found.");
                                if (!CueClassifiers.ContainsKey(DEFENSE)) throw new Exception($"{DEFENSE} classifier not found.");
                                Choreographer = new SimpleChoreographer(CueClassifiers);
                                Choreographer.OnPromptCue += async (o, eargs) =>
                                {
                                    CurrentCue = eargs.Value;
                                    Classifier = CueClassifiers[CurrentCue.ClassifierKey];
                                    //await GimmeCue(eargs.Value.GestureClass);
                                    SelectedGestureClass = Classifier.MatchingDatasetClasses[CurrentCue.GestureClassIndex];
                                    var delay = CurrentCue.CueTime - DateTime.Now;
                                    await Task.Delay(delay);
                                    Speech.Say(SelectedGestureClass.className, SoundOptions.AtSpeed(2.0));
                                    Stopwatch.Restart();
                                    if (_singleMode) Choreographer.Deactivate();
                                };
                                Choreographer.Activate();
                            }
                            else
                            {
                                Choreographer.Deactivate();
                                Task.Delay(100).Wait();
                                Choreographer = null;
                                CurrentCue = default(ChoreographyCue);
                            }
                            // Either way, go no further handling this button-press (and discard the sequence unlooked-at).
                            CurrentStage.Deactivate();
                            _collectingData = false;
                            return true;
                        }
                        //Log.Debug("MachineLearning", $"{keyCode} up");

                        // Halt the gesture-collection stage and query it.
                        var resultData = CurrentStage.StopAndReturnResults();
                        var resultSeq = new Sequence<DKS>() { SourcePath = resultData };
                        var meta = CurrentStage.GetMetadata(GetAccelForDatapoint);
                        meta.Delay = Delay;
                        resultSeq.Metadata = meta;
                        ResolveEndOfGesture(resultSeq);

                        //if (_userTrainingMode)
                        //{
                        //    // Halt the gesture-collection stage and query it.
                        //    var resultData = CurrentStage.StopAndReturnResults();
                        //    var resultSeq = new Sequence<DKS>() { SourcePath = resultData };
                        //    resultSeq.Metadata = CurrentStage.GetMetadata(GetAccelForDatapoint);
                        //    ResolveEndOfGesture(resultSeq);
                        //}
                        //else if (_singleMode)
                        //{
                        //    Choreographer = new Choreographer(Dataset);
                        //    Choreographer.OnSendCue += async (o, eargs) =>
                        //    {
                        //        await GimmeCue(eargs.Value);
                        //        Choreographer.Deactivate();
                        //    };
                        //    Choreographer.Activate();
                        //}
                        //else if (!_seriesModeActive)
                        //{
                        //    Choreographer = new Choreographer(Dataset);
                        //    Choreographer.OnSendCue += async (o, eargs) => { await GimmeCue(eargs.Value); };
                        //    Choreographer.Activate();
                        //}
                        //else
                        //{
                        //    Choreographer?.Deactivate();
                        //}

                        _collectingData = false;
                        return true; // Handled it, thanks.  
                    }
                }
            }
            return base.OnKeyUp(keyCode, e);
        }
        #endregion

        protected void ResolveEndOfGesture(Sequence<DKS> resultSequence)
        {
            MostRecentSample = resultSequence;
            if (!_userTrainingMode) MostRecentSample.TrueClassIndex = SelectedGestureClass.index;
            //Dataset?.AddSequence(MostRecentSample);

            string StageLabel = (!_userTrainingMode)
                ? $"Cued gesture {SelectedGestureClass.className}#{SelectedGestureClass.numExamples + SelectedGestureClass.numNewExamples}"
                : $"User gesture (#{Dataset.SequenceCount + 1})";
            if (!_advancedCueMode) ResetStage(StageLabel);

            _collectingData = false;
            if (Classifier == null) return;
            MostRecentSample = Analyze(MostRecentSample).Result;

            // If right or wrong, tweak the display properties of the sample.  This depends on the active mode.
            //DisplaySampleInfo(MostRecentSample);

            if (_userTrainingMode && MostRecentSample.RecognizedAsIndex >= 0)
            {
                var sc = MostRecentSample.RecognitionScore;
                var prefix = (sc < 1) ? "Arguably " :
                             (sc < 1.5) ? "Maybe " :
                             (sc < 2) ? "Probably " :
                             (sc < 2.5) ? "Clearly " :
                             (sc < 3) ? "Certainly " :
                             "A perfect ";
                Speech.Say(prefix + MostRecentSample.RecognizedAsName);
            }
            else if (!_userTrainingMode)
            {
                if (MostRecentSample.RecognizedAsIndex == -1)
                {
                    CurrentCue.Score = double.NaN;
                }
                else if (MostRecentSample.RecognizedAsIndex != MostRecentSample.TrueClassIndex)
                {
                    CurrentCue.Score = -1 * MostRecentSample.RecognitionScore;
                    CurrentCue.GestureClassIndex = MostRecentSample.RecognizedAsIndex;
                }
                //CuePrompter?.ReactToFinalizedGesture(MostRecentSample);
                else
                {
                    var points = MostRecentSample.RecognitionScore;
                    var delay = MostRecentSample.Metadata.Delay;

                    CurrentCue.Score = points;
                    CurrentCue.Delay = delay;
                }
                //if (!_singleMode) Choreographer?.ProceedWithNextCue();
                Choreographer.SubmitResult(CurrentCue);
            }
        }

        protected void LoadMeleeClassifier()
        {
            #region Hardcoded classifier strings (backup)
            var savedClassifierString = new Dictionary<string, string>()
            {
                {OFFENSE, "AAEAAAD/////AQAAAAAAAAAMAgAAAEJBdHJvcG9zR2FtZSwgVmVyc2lvbj0xLjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPW51bGwFAQAAACdBdHJvcG9z" +
                "Lk1hY2hpbmVfTGVhcm5pbmcuQ2xhc3NpZmllclRyZWUCAAAAGU1haW5DbGFzc2lmaWVyX1NlcmlhbGl6ZWQZQ3VlQ2xhc3NpZmllcnNfU2VyaWFsaXplZAED4gFTeXN0ZW0uQ29sbGVjdGlvbn" +
                "MuR2VuZXJpYy5EaWN0aW9uYXJ5YDJbW1N5c3RlbS5TdHJpbmcsIG1zY29ybGliLCBWZXJzaW9uPTIuMC41LjAsIEN1bHR1cmU9bmV1dHJhbCwgUHVibGljS2V5VG9rZW49N2NlYzg1ZDdiZWE3" +
                "Nzk4ZV0sW1N5c3RlbS5TdHJpbmcsIG1zY29ybGliLCBWZXJzaW9uPTIuMC41LjAsIEN1bHR1cmU9bmV1dHJhbCwgUHVibGljS2V5VG9rZW49N2NlYzg1ZDdiZWE3Nzk4ZV1dAgAAAAYDAAAAsJs" +
                "CQUFFQUFBRC8vLy8vQVFBQUFBQUFBQUFNQWdBQUFFSkJkSEp2Y0c5elIyRnRaU3dnVm1WeWMybHZiajB4TGpBdU1DNHdMQ0JEZFd4MGRYSmxQVzVsZFhSeVlXd3NJRkIxWW14cFkwdGxlVlJ2YT" +
                "JWdVBXNTFiR3dGQVFBQUFDcEJkSEp2Y0c5ekxrMWhZMmhwYm1WZlRHVmhjbTVwYm1jdVEyeDFjM1JsY2tOc1lYTnphV1pwWlhJSEFBQUFEbE5XVFY5VFpYSnBZV3hwZW1Wa0ZVMWhkR05vYVc1b" +
                "lgwUmhkR0Z6WlhSZlRtRnRaUmhOWVhSamFHbHVaMTlFWVhSaGMyVjBYME5zWVhOelpYTWVUV0YwWTJocGJtZGZSR0YwWVhObGRGOVRaWEYxWlc1alpVTnZkVzUwRmtabFlYUjFjbVZmUlhoMGNt" +
                "RmpkRzl5WDA1aGJXVVpVSEpsY0hKdlkyVnpjMjl5WDBOdlpXWm1hV05wWlc1MGN4SkRiSFZ6ZEdWeVEyeGhjM05wWm1sbGNuTUhBUVFBQWdRREFpZEJkSEp2Y0c5ekxrMWhZMmhwYm1WZlRHVmh" +
                "jbTVwYm1jdVIyVnpkSFZ5WlVOc1lYTnpXMTBDQUFBQUNERkJkSEp2Y0c5ekxrMWhZMmhwYm1WZlRHVmhjbTVwYm1jdVVISmxjSEp2WTJWemMyOXlRMjlsWm1acFkybGxiblJ6QWdBQUFJd0JVM2" +
                "x6ZEdWdExrTnZiR3hsWTNScGIyNXpMa2RsYm1WeWFXTXVUR2x6ZEdBeFcxdEJkSEp2Y0c5ekxrMWhZMmhwYm1WZlRHVmhjbTVwYm1jdVEyeGhjM05wWm1sbGNpd2dRWFJ5YjNCdmMwZGhiV1Vz" +
                "SUZabGNuTnBiMjQ5TVM0d0xqQXVNQ3dnUTNWc2RIVnlaVDF1WlhWMGNtRnNMQ0JRZFdKc2FXTkxaWGxVYjJ0bGJqMXVkV3hzWFYwQ0FBQUFDUU1BQUFBR0JBQUFBQXhOWld4bFpVOW1abVZ1YzJ" +
                "VSkJRQUFBQkVBQUFBS0Jmci8vLzh4UVhSeWIzQnZjeTVOWVdOb2FXNWxYMHhsWVhKdWFXNW5MbEJ5WlhCeWIyTmxjM052Y2tOdlpXWm1hV05wWlc1MGN3SUFBQUFGVFdWaGJuTUdVMmxuYldGek" +
                "J3Y0dCZ0lBQUFBS0Nna0hBQUFBRHdNQUFBQUFBQUFBQWdjRkFBQUFBQUVBQUFBREFBQUFCQ1ZCZEhKdmNHOXpMazFoWTJocGJtVmZUR1ZoY201cGJtY3VSMlZ6ZEhWeVpVTnNZWE56QWdBQUFBa" +
                "0lBQUFBQ1FrQUFBQUpDZ0FBQUFRSEFBQUFqQUZUZVhOMFpXMHVRMjlzYkdWamRHbHZibk11UjJWdVpYSnBZeTVNYVhOMFlERmJXMEYwY205d2IzTXVUV0ZqYUdsdVpWOU1aV0Z5Ym1sdVp5NURi" +
                "R0Z6YzJsbWFXVnlMQ0JCZEhKdmNHOXpSMkZ0WlN3Z1ZtVnljMmx2YmoweExqQXVNQzR3TENCRGRXeDBkWEpsUFc1bGRYUnlZV3dzSUZCMVlteHBZMHRsZVZSdmEyVnVQVzUxYkd4ZFhRTUFBQUFH" +
                "WDJsMFpXMXpCVjl6YVhwbENGOTJaWEp6YVc5dUJBQUFKVUYwY205d2IzTXVUV0ZqYUdsdVpWOU1aV0Z5Ym1sdVp5NURiR0Z6YzJsbWFXVnlXMTBDQUFBQUNBZ0pDd0FBQUFNQUFBQURBQUFBQlFn" +
                "QUFBQWxRWFJ5YjNCdmN5NU5ZV05vYVc1bFgweGxZWEp1YVc1bkxrZGxjM1IxY21WRGJHRnpjd29BQUFBRmFXNWtaWGdKWTJ4aGMzTk9ZVzFsQzI1MWJVVjRZVzF3YkdWekhtNTFiVVY0WVcxd2JH" +
                "VnpRMjl5Y21WamRHeDVVbVZqYjJkdWFYcGxaQTV1ZFcxT1pYZEZlR0Z0Y0d4bGN5RnVkVzFPWlhkRmVHRnRjR3hsYzBOdmNuSmxZM1JzZVZKbFkyOW5ibWw2WldRU2JuVnRSWGhoYlhCc1pYTlRZ" +
                "VzF3YkdWa0pXNTFiVVY0WVcxd2JHVnpVMkZ0Y0d4bFpFTnZjbkpsWTNSc2VWSmxZMjluYm1sNlpXUVBRWFpsY21GblpVMWxkR0ZrWVhSaEVXNTFiVTFsZEdGa1lYUmhVRzlwYm5SekFBRUFBQUFB" +
                "QUFBRUFBZ0lDQWdJQ0FncFFYUnliM0J2Y3k1TllXTm9hVzVsWDB4bFlYSnVhVzVuTGxObGNYVmxibU5sVFdWMFlXUmhkR0VDQUFBQUNBSUFBQUFBQUFBQUJnd0FBQUFLU0dsbmFDQlRiR0Z6YUFV" +
                "QUFBQUZBQUFBQUFBQUFBQUFBQUFGQUFBQUJRQUFBQVh6Ly8vL0tVRjBjbTl3YjNNdVRXRmphR2x1WlY5TVpXRnlibWx1Wnk1VFpYRjFaVzVqWlUxbGRHRmtZWFJoQlFBQUFBeFJkV0ZzYVhSNVUy" +
                "TnZjbVVGUkdWc1lYa0lSSFZ5WVhScGIyNEpUblZ0VUc5cGJuUnpDVkJsWVd0QlkyTmxiQUFBQUFBQUJnd01CZ1lDQUFBQWlPOFV1V0ZiQVVBQUFBQUFBQUFBQUFBQUFBQUFBQUFBTXpNek16TXpM" +
                "MEJtWm1aMjZMTkRRQVVBQUFBQkNRQUFBQWdBQUFBQkFBQUFCZzRBQUFBS1RHVm1kQ0JUYkdGemFBWUFBQUFHQUFBQUFBQUFBQUFBQUFBR0FBQUFCZ0FBQUFIeC8vLy84Ly8vLzUvMXVlZi9Ld3BB" +
                "QUFBQUFBQUFBQUFBQUFBQUFBQUFBRlZWVlZWVjFUQkFWVlZWYmM1MVFVQUdBQUFBQVFvQUFBQUlBQUFBQWdBQUFBWVFBQUFBQzFKcFoyaDBJRk5zWVhOb0JnQUFBQVlBQUFBQUFBQUFBQUFBQUFZ" +
                "QUFBQUdBQUFBQWUvLy8vL3ovLy8vaE5ZNTdiT1BBVUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFxcXFxcXFxcUtFQUFBQURRNG8xRVFBWUFBQUFIQ3dBQUFBQUJBQUFBQkFBQUFBUWpRWFJ5YjNCdmN5" +
                "NU5ZV05vYVc1bFgweGxZWEp1YVc1bkxrTnNZWE56YVdacFpYSUNBQUFBQ1JJQUFBQUpFd0FBQUFrVUFBQUFDZ1VTQUFBQUkwRjBjbTl3YjNNdVRXRmphR2x1WlY5TVpXRnlibWx1Wnk1RGJHRnpj" +
                "MmxtYVdWeUJnQUFBQTVUVmsxZlUyVnlhV0ZzYVhwbFpCVk5ZWFJqYUdsdVoxOUVZWFJoYzJWMFgwNWhiV1VZVFdGMFkyaHBibWRmUkdGMFlYTmxkRjlEYkdGemMyVnpIazFoZEdOb2FXNW5YMFJo" +
                "ZEdGelpYUmZVMlZ4ZFdWdVkyVkRiM1Z1ZEJaR1pXRjBkWEpsWDBWNGRISmhZM1J2Y2w5T1lXMWxHVkJ5WlhCeWIyTmxjM052Y2w5RGIyVm1abWxqYVdWdWRITUhBUVFBQVFRQ0owRjBjbTl3YjNN" +
                "dVRXRmphR2x1WlY5TVpXRnlibWx1Wnk1SFpYTjBkWEpsUTJ4aGMzTmJYUUlBQUFBSU1VRjBjbTl3YjNNdVRXRmphR2x1WlY5TVpXRnlibWx1Wnk1UWNtVndjbTlqWlhOemIzSkRiMlZtWm1samFX" +
                "VnVkSE1DQUFBQUFnQUFBQWtWQUFBQUNRUUFBQUFKRndBQUFCRUFBQUFHR0FBQUFCZEJZMk5sYkZCaGNtRnNiR1ZzVkc5SGVYSnZRWGhwY3dIbi8vLy8rdi8vL3drYUFBQUFDUnNBQUFBQkV3QUFB" +
                "QklBQUFBSkhBQUFBQWtFQUFBQUNSNEFBQUFSQUFBQUJoOEFBQUFGUjNseWIxZ0I0UC8vLy9yLy8vOEpJUUFBQUFraUFBQUFBUlFBQUFBU0FBQUFDU01BQUFBSkJBQUFBQWtsQUFBQUVRQUFBQVlt" +
                "QUFBQUNGSnZkRUZ1WjJ4bEFkbi8vLy82Ly8vL0NTZ0FBQUFKS1FBQUFBOFZBQUFBQUNBQUFBSUFBUUFBQVAvLy8vOEJBQUFBQUFBQUFBd0NBQUFBVFVGalkyOXlaQzVOWVdOb2FXNWxUR1ZoY201" +
                "cGJtY3NJRlpsY25OcGIyNDlNeTQ0TGpBdU1Dd2dRM1ZzZEhWeVpUMXVaWFYwY21Gc0xDQlFkV0pzYVdOTFpYbFViMnRsYmoxdWRXeHNCUUVBQUFEQUFVRmpZMjl5WkM1TllXTm9hVzVsVEdWaGNt" +
                "NXBibWN1Vm1WamRHOXlUV0ZqYUdsdVpYTXVUWFZzZEdsamJHRnpjMU4xY0hCdmNuUldaV04wYjNKTllXTm9hVzVsWURGYlcwRmpZMjl5WkM1VGRHRjBhWE4wYVdOekxrdGxjbTVsYkhNdVJIbHVZ" +
                "VzFwWTFScGJXVlhZWEp3YVc1bkxDQkJZMk52Y21RdVUzUmhkR2x6ZEdsamN5d2dWbVZ5YzJsdmJqMHpMamd1TUM0d0xDQkRkV3gwZFhKbFBXNWxkWFJ5WVd3c0lGQjFZbXhwWTB0bGVWUnZhMlZ1" +
                "UFc1MWJHeGRYUWdBQUFBdlRYVnNkR2xqYkdGemMxTjFjSEJ2Y25SV1pXTjBiM0pOWVdOb2FXNWxZRE1yWTJGamFHVlVhSEpsYzJodmJHUVNUMjVsVm5OUGJtVmdNaXRwYm1ScFkyVnpFVTl1WlZa" +
                "elQyNWxZRElyYlc5a1pXeHpFVTl1WlZaelQyNWxZRElyYldWMGFHOWtJVTl1WlZaelQyNWxZRElyUEZSeVlXTnJQbXRmWDBKaFkydHBibWRHYVdWc1pERkRiR0Z6YzJsbWFXVnlRbUZ6WldBeUt6" +
                "eE9kVzFpWlhKUFprTnNZWE56WlhNK2ExOWZRbUZqYTJsdVowWnBaV3hrRmxSeVlXNXpabTl5YlVKaGMyVmdNaXRwYm5CMWRITVhWSEpoYm5ObWIzSnRRbUZ6WldBeUsyOTFkSEIxZEhNREJBUUVB" +
                "QUFBQUF4VGVYTjBaVzB1U1c1ME16SWlRV05qYjNKa0xrMWhZMmhwYm1WTVpXRnlibWx1Wnk1RGJHRnpjMUJoYVhKYlhRSUFBQUM2QVVGalkyOXlaQzVOWVdOb2FXNWxUR1ZoY201cGJtY3VWbVZq" +
                "ZEc5eVRXRmphR2x1WlhNdVUzVndjRzl5ZEZabFkzUnZjazFoWTJocGJtVmdNVnRiUVdOamIzSmtMbE4wWVhScGMzUnBZM011UzJWeWJtVnNjeTVFZVc1aGJXbGpWR2x0WlZkaGNuQnBibWNzSUVG" +
                "alkyOXlaQzVUZEdGMGFYTjBhV056TENCV1pYSnphVzl1UFRNdU9DNHdMakFzSUVOMWJIUjFjbVU5Ym1WMWRISmhiQ3dnVUhWaWJHbGpTMlY1Vkc5clpXNDliblZzYkYxZFcxMWJYUUlBQUFBOVFX" +
                "TmpiM0prTGsxaFkyaHBibVZNWldGeWJtbHVaeTVXWldOMGIzSk5ZV05vYVc1bGN5NU5kV3gwYVdOc1lYTnpRMjl0Y0hWMFpVMWxkR2h2WkFJQUFBQUJDQWdJQWdBQUFBZ0lRQUFBQUFrREFBQUFD" +
                "UVFBQUFBRisvLy8vejFCWTJOdmNtUXVUV0ZqYUdsdVpVeGxZWEp1YVc1bkxsWmxZM1J2Y2sxaFkyaHBibVZ6TGsxMWJIUnBZMnhoYzNORGIyMXdkWFJsVFdWMGFHOWtBUUFBQUFkMllXeDFaVjlm" +
                "QUFnQ0FBQUFBUUFBQUFFREFBQUFBQUFBQUFNQUFBQUhBd0FBQUFBQkFBQUFBd0FBQUFRZ1FXTmpiM0prTGsxaFkyaHBibVZNWldGeWJtbHVaeTVEYkdGemMxQmhhWElDQUFBQUJmci8vLzhnUVdO" +
                "amIzSmtMazFoWTJocGJtVk1aV0Z5Ym1sdVp5NURiR0Z6YzFCaGFYSUNBQUFBQm1Oc1lYTnpNUVpqYkdGemN6SUFBQWdJQWdBQUFBRUFBQUFBQUFBQUFmbi8vLy82Ly8vL0FnQUFBQUFBQUFBQitQ" +
                "Ly8vL3IvLy84Q0FBQUFBUUFBQUFjRUFBQUFBUUVBQUFBQ0FBQUFCTGdCUVdOamIzSmtMazFoWTJocGJtVk1aV0Z5Ym1sdVp5NVdaV04wYjNKTllXTm9hVzVsY3k1VGRYQndiM0owVm1WamRHOXlU" +
                "V0ZqYUdsdVpXQXhXMXRCWTJOdmNtUXVVM1JoZEdsemRHbGpjeTVMWlhKdVpXeHpMa1I1Ym1GdGFXTlVhVzFsVjJGeWNHbHVaeXdnUVdOamIzSmtMbE4wWVhScGMzUnBZM01zSUZabGNuTnBiMjQ5" +
                "TXk0NExqQXVNQ3dnUTNWc2RIVnlaVDF1WlhWMGNtRnNMQ0JRZFdKc2FXTkxaWGxVYjJ0bGJqMXVkV3hzWFYxYlhRSUFBQUFKQ1FBQUFBa0tBQUFBQndrQUFBQUFBUUFBQUFFQUFBQUV0Z0ZCWTJO" +
                "dmNtUXVUV0ZqYUdsdVpVeGxZWEp1YVc1bkxsWmxZM1J2Y2sxaFkyaHBibVZ6TGxOMWNIQnZjblJXWldOMGIzSk5ZV05vYVc1bFlERmJXMEZqWTI5eVpDNVRkR0YwYVhOMGFXTnpMa3RsY201bGJI" +
                "TXVSSGx1WVcxcFkxUnBiV1ZYWVhKd2FXNW5MQ0JCWTJOdmNtUXVVM1JoZEdsemRHbGpjeXdnVm1WeWMybHZiajB6TGpndU1DNHdMQ0JEZFd4MGRYSmxQVzVsZFhSeVlXd3NJRkIxWW14cFkwdGxl" +
                "VlJ2YTJWdVBXNTFiR3hkWFFJQUFBQUpDd0FBQUFjS0FBQUFBQUVBQUFBQ0FBQUFCTFlCUVdOamIzSmtMazFoWTJocGJtVk1aV0Z5Ym1sdVp5NVdaV04wYjNKTllXTm9hVzVsY3k1VGRYQndiM0ow" +
                "Vm1WamRHOXlUV0ZqYUdsdVpXQXhXMXRCWTJOdmNtUXVVM1JoZEdsemRHbGpjeTVMWlhKdVpXeHpMa1I1Ym1GdGFXTlVhVzFsVjJGeWNHbHVaeXdnUVdOamIzSmtMbE4wWVhScGMzUnBZM01zSUZa" +
                "bGNuTnBiMjQ5TXk0NExqQXVNQ3dnUTNWc2RIVnlaVDF1WlhWMGNtRnNMQ0JRZFdKc2FXTkxaWGxVYjJ0bGJqMXVkV3hzWFYwQ0FBQUFDUXdBQUFBSkRRQUFBQXdPQUFBQVNFRmpZMjl5WkM1VGRH" +
                "RjBhWE4wYVdOekxDQldaWEp6YVc5dVBUTXVPQzR3TGpBc0lFTjFiSFIxY21VOWJtVjFkSEpoYkN3Z1VIVmliR2xqUzJWNVZHOXJaVzQ5Ym5Wc2JBVUxBQUFBdGdGQlkyTnZjbVF1VFdGamFHbHVa" +
                "VXhsWVhKdWFXNW5MbFpsWTNSdmNrMWhZMmhwYm1WekxsTjFjSEJ2Y25SV1pXTjBiM0pOWVdOb2FXNWxZREZiVzBGalkyOXlaQzVUZEdGMGFYTjBhV056TGt0bGNtNWxiSE11UkhsdVlXMXBZMVJw" +
                "YldWWFlYSndhVzVuTENCQlkyTnZjbVF1VTNSaGRHbHpkR2xqY3l3Z1ZtVnljMmx2YmowekxqZ3VNQzR3TENCRGRXeDBkWEpsUFc1bGRYUnlZV3dzSUZCMVlteHBZMHRsZVZSdmEyVnVQVzUxYkd4" +
                "ZFhRc0FBQUFnVEU5SFRFbExSVXhKU0U5UFJGOUVSVU5KVTBsUFRsOVVTRkpGVTBoUFRFUWRVM1Z3Y0c5eWRGWmxZM1J2Y2sxaFkyaHBibVZnTWl0clpYSnVaV3dsVTNWd2NHOXlkRlpsWTNSdmNr" +
                "MWhZMmhwYm1WZ01pdHpkWEJ3YjNKMFZtVmpkRzl5Y3g1VGRYQndiM0owVm1WamRHOXlUV0ZqYUdsdVpXQXlLM2RsYVdkb2RITWdVM1Z3Y0c5eWRGWmxZM1J2Y2sxaFkyaHBibVZnTWl0MGFISmxj" +
                "Mmh2YkdRM1UzVndjRzl5ZEZabFkzUnZjazFoWTJocGJtVmdNaXM4U1hOUWNtOWlZV0pwYkdsemRHbGpQbXRmWDBKaFkydHBibWRHYVdWc1pEZFRkWEJ3YjNKMFZtVmpkRzl5VFdGamFHbHVaV0F5" +
                "SzB4UFIweEpTMFZNU1VoUFQwUmZSRVZEU1ZOSlQwNWZWRWhTUlZOSVQweEVRVUpwYm1GeWVVeHBhMlZzYVdodmIyUkRiR0Z6YzJsbWFXVnlRbUZ6WldBeEsweFBSMHhKUzBWTVNVaFBUMFJmUkVW" +
                "RFNWTkpUMDVmVkVoU1JWTklUMHhFTVVOc1lYTnphV1pwWlhKQ1lYTmxZRElyUEU1MWJXSmxjazltUTJ4aGMzTmxjejVyWDE5Q1lXTnJhVzVuUm1sbGJHUVdWSEpoYm5ObWIzSnRRbUZ6WldBeUsy" +
                "bHVjSFYwY3hkVWNtRnVjMlp2Y20xQ1lYTmxZRElyYjNWMGNIVjBjd0FFQXdjQUFBQUFBQUFBQml4QlkyTnZjbVF1VTNSaGRHbHpkR2xqY3k1TFpYSnVaV3h6TGtSNWJtRnRhV05VYVcxbFYyRnlj" +
                "R2x1Wnc0QUFBQVJVM2x6ZEdWdExrUnZkV0pzWlZ0ZFcxMEdCZ0VHQmdnSUNBSUFBQUR2T2ZyK1FpN212d1h4Ly8vL0xFRmpZMjl5WkM1VGRHRjBhWE4wYVdOekxrdGxjbTVsYkhNdVJIbHVZVzFw" +
                "WTFScGJXVlhZWEp3YVc1bkF3QUFBQVZoYkhCb1lRWnNaVzVuZEdnR1pHVm5jbVZsQUFBQUJnZ0lEZ0FBQUFBQUFBQUFBUEEvQWdBQUFBRUFBQUFKRUFBQUFBa1JBQUFBbUgzMzA0SXMxRDhCN3pu" +
                "Ni9rSXU1ci92T2ZyK1FpN212d0lBQUFBQUFBQUFBUUFBQUFFTUFBQUFDd0FBQU84NSt2NUNMdWEvQWU3Ly8vL3gvLy8vQUFBQUFBQUE4RDhDQUFBQUFRQUFBQWtUQUFBQUNSUUFBQUN5S1NEc0pi" +
                "dmFQd0h2T2ZyK1FpN212Kzg1K3Y1Q0x1YS9BZ0FBQUFBQUFBQUJBQUFBQVEwQUFBQUxBQUFBN3puNi9rSXU1cjhCNi8vLy8vSC8vLzhBQUFBQUFBRHdQd0lBQUFBQkFBQUFDUllBQUFBSkZ3QUFB" +
                "TXpDemNwQ25ycS9BZTg1K3Y1Q0x1YS83em42L2tJdTVyOENBQUFBQUFBQUFBRUFBQUFIRUFBQUFBRUJBQUFBQ2dBQUFBY0dDUmdBQUFBSkdRQUFBQWthQUFBQUNSc0FBQUFKSEFBQUFBa2RBQUFB" +
                "Q1I0QUFBQUpId0FBQUFrZ0FBQUFDU0VBQUFBUEVRQUFBQW9BQUFBR1U0V2FNRjlVTUVCVGhab3dYMVF3d0ZPRm1qQmZWREJBVTRXYU1GOVVNTUJUaFpvd1gxUXd3Rk9GbWpCZlZEQkFVNFdhTUY5" +
                "VU1FQlRoWm93WDFRd3dGT0ZtakJmVkREQVU0V2FNRjlVTUVBSEV3QUFBQUVCQUFBQUNBQUFBQWNHQ1NJQUFBQUpHUUFBQUFrZ0FBQUFDU1VBQUFBSkpnQUFBQWtiQUFBQUNSOEFBQUFKS1FBQUFB" +
                "OFVBQUFBQ0FBQUFBWVVQNVgwWWRkZ1FCUS9sZlJoMTJEQUZEK1Y5R0hYWU1BVVA1WDBZZGRnUUJRL2xmUmgxMkJBRkQrVjlHSFhZTUFVUDVYMFlkZGd3QlEvbGZSaDEyQkFCeFlBQUFBQkFRQUFB" +
                "QXdBQUFBSEJna2lBQUFBQ1JnQUFBQUpMQUFBQUFrbEFBQUFDUzRBQUFBSkhRQUFBQWtlQUFBQUNTRUFBQUFKR2dBQUFBa21BQUFBQ1NrQUFBQUpOUUFBQUE4WEFBQUFEQUFBQUFaSXhEamM1Zmd4U" +
                "UVqRU9OemwrREhBU01RNDNPWDRNVUJJeERqYzVmZ3hRRWpFT056bCtERkFTTVE0M09YNE1jQkl4RGpjNWZneHdFakVPTnpsK0RIQVNNUTQzT1g0TWNCSXhEamM1Zmd4UUVqRU9OemwrREZBU01RND" +
                "NPWDRNY0FQR0FBQUFCSUFBQUFHUVRsTWpOMmlJMEF6TjhrSXNLZ2pRS0x4QXlQTUhTUkFlZ2tEREpRcEpFQTZ6OW82UWJ3alFLNFh0SEJvTXlOQUFPc2xJTk93SUVCZTVlcjQxbW9lUURLQVZxTU" +
                "RuaDlBVzV0Z0tud1dJVUN5TW9NV09UZ2hRS0Y5ZTVEOEJTRkFmcHprcFdqeklFQ0NVOSs3QVpVaVFITFY2QnV0MGlKQXoxSWcxSjE5SVVCOEhXd2x3VFVqUUNsNGFkWFBpeVJBRHhrQUFBQVJBQU" +
                "FBQmtFNVRJemRvaU5BdzNCaHZXaGlKRUROUHFJUUlsZ2xRUGk2Q3RNc0N5WkFQU251WkZSRUprRDdmNkM4TU04bFFOQW1OQVBDN2lWQXkwUHRCLzJNSlVDQ3JlemNsc1FrUUh0aFhmOTNwaVJBZH" +
                "VmQXBBcVRKRURzdmJ4a25WNGtRR2J0bUQ5YlJ5UkFYR0R6eW50cEkwQUdhUXQ1eWJZalFKMzNBeVh3VkNWQThySDNPSUVRSkVBUEdnQUFBQkFBQUFBR1FUbE1qTjJpSTBBOFJsUmhjRmtqUUdGd3" +
                "F1RHBBQ05BWW5ldmtjcTRJa0JFdjliZStBTWpRQmpzdFFBSExTSkFXbjg5WCt4UElFQlRGWWpqYXhZZlFON29QeTFoTUNCQWtMT0t2Q0UwSVVCUmhIVVJJZU1oUVB1Nm1YNWdHeUpBbFBWSHA2Zz" +
                "JJMEQ0dXdWUEdNOGlRUFA0K05rOXZ5SkFDSHVITkZha0kwQVBHd0FBQUJBQUFBQUdRVGxNak4yaUkwQUNpNHV2U2s4a1FCS05lTWlzZFNWQTZ3YnArY1V6SjBCV095TlhIVHNuUUZyOEQ4U1NtU1p" +
                "BVVZCUG1ZMlNKa0FleGZDWVBSc21RSHl6YWs4cDhpUkFacElYWlJPOUpFQkhMT01oYlNva1FMWnRrY0xhR1NSQVNPVGJWK3ZlSWtCdEk4T0swUWNqUU1PWDFmai9KaUpBdTVQTkhRS1pJa0FQSEFB" +
                "QUFCQUFBQUFHUVRsTWpOMmlJMEErNXFKU0FRSWtRSmZ2QTVqd3NTUkE5WTdNblJHNkpVQXg2RGttdEpBbVFPOWRydm1URWlaQUV2OHBvVFlxSmtDMUphd1hzZzRtUUpRUEFwUmsxeVJBbGRTdnlGd" +
                "FVKRUJXS3p4cjA2TWtRUFU5bXA2S3ppUkFtTXdqSFpqM0kwQ3NSWEJsYmxVa1FCZ2ZhbHlReXlOQWtpV0k1L2VzSkVBUEhRQUFBQkFBQUFBR1FUbE1qTjJpSTBBYXg5V3kveElqUU1EWVE5Vk56eU" +
                "pBR09iMndMY21JMENKenBsUUNyMGlRREpQWlRLY2J5RkExRHdUaFU0QklFQUtVWXcxaW5jZVFCa0FZc0hoa3g5QW9sZGR2eFdHSVVDQ3VmNDBadUloUUl4S2hrSlJveUZBTFgzR0VWc1ZKVURBcEh" +
                "CK2QySWpRSUlJYS96VFlpSkFGREhsdStlUUpFQVBIZ0FBQUJJQUFBQUdRVGxNak4yaUkwQXlLRXFPMTJZalFJTC96MGdoZFNOQVJVTzh1dGtmSTBBc3lwODYreUFqUUEwclZUcWxyaUpBTXNDMGFB" +
                "SGtJVUI0emVtM0RoSWdRTWErZEZSNW9CNUFLUDc5OS9tU0lFRC9pMjg3TG9VaVFHVnpMNDdZZGlKQWRTSXhNRlMxSWtDWTNIdmR3MklqUU5wcVZMemt4Q0pBVUM3NG9GTTlJMEJxL1dmOWJDRWtRT" +
                "GdlMzlFa2NDUkFEeDhBQUFBUUFBQUFCa0U1VEl6ZG9pTkFENlZOMTFBZEpFQkVyRXpITDVFa1FCb2orYVE1aENaQTVzbWY0OWhqSjBCVWZQcUVMNVluUU83UEFVNURtQ2RBRk9vZ0d2T0ZKa0J1TD" +
                "IxK0RMSW1RQVR4dmMrOGJDWkEvTnQ0ME5YdkprQ2dOcmp2eFRrblFOWlFMbTdqUFNaQXY1SkVJek5hSlVBZDJmblRNWTBqUU83WXd0TVZ2U0pBRHlBQUFBQU5BQUFBQmtFNVRJemRvaU5BOXl3cGI" +
                "xa2hKRURXQ0xXcWxKTWxRTUZNaFlSK1hTZEF6dnF1Y2xjNkowQ0tvbmxKUjlNbVFCQWdlZzB0dnlaQTBwNlVvMW5zSmtEU21GcUpzWjBtUUYrclBFeGZ4eVpBYi9rcUZmMkFKVURQZ1hVZzRqa2x" +
                "RR2xYQ3RQN1lpVkFEeUVBQUFBUUFBQUFCa0U1VEl6ZG9pTkFZR08wTTRtbkkwQ016bCtoejlFalFKeXhXV24ydHlOQTNTb1kzRFdlSWtBUlVjUTFpblFnUUhMT3FHdmQ4QjFBTXZjNWtrdTZIMERJ" +
                "NXhOSEhic2hRTXdjRDU0UUl5SkFQd1lVK3lhV0lVQ05sSVJ2WXR3alFLWVIxTXIxcENKQUpnMC95Zlp4SWtEa0ZIVFJqaVVrUUhQRVZVYWJzQ1JBRHlJQUFBQVFBQUFBQmtFNVRJemRvaU5BN1hXe" +
                "WRVVHVJMERGdmlFcENOMGtRRG5YNXQxemJDVkExbWc5RkM0bkpVQTJ6UzZUYXkwa1FMZWdadUs2MWlSQWE3SjFNZlBTSmtBdEtNSnpyOHNvUURwZk1IZ2xQQ2hBeFIrbDF0SG9KRUMzUTdCOFFYMG" +
                "pRSlFkR3Z4MDd5RkExd0l1NmlhbUlrQk5RV3BrL1BvaVFNSlduZFhFUGlOQUR5VUFBQUFOQUFBQUJrRTVUSXpkb2lOQUhLMHh4RGxVSkVENzNkN0kvNHdrUUYzOEpWQ0NBQ1ZBb21FSDlDL1FKRUJ" +
                "6cGFaVElwd2tRSnFJZjI2aG5DZEFlekQrSVQzbUtFQkVlWitZcG9RbFFBTTJ3YjhvOWlOQUV6YXQ0MFhoSVVERDdwTFdUeUVpUUJILzZ6azV2aUZBRHlZQUFBQU1BQUFBQmtFNVRJemRvaU5BdkFD" +
                "TmNsYUlKRUNuMkFOVkVkd2tRQWxTb3BPSG9pVkF4Ukl3UDBJbUpVQ0xRMHZUVGRBa1FIU3c2Ty8xa1NaQVdGN1RyTTBNSjBBV0x4V1B2dThsUU9qV0dDaUZ2aVJBZVRld0ExUmtJa0MvUkRaN3BDd" +
                "2hRQThwQUFBQUN3QUFBQVpCT1V5TTNhSWpRTS81SWQ0eGNTUkFRK29wczduRUpFQXB1c3NlbzBvbFFNNzZacFZzd2lWQWgyRGZLWXpRSkVDMlRaSVRndVFsUUxYV3JBYkxRQ1pBQnBOaEo5cGJLRU" +
                "NtaDhTSXROSW1RSjhLMkdLeVl5UkFEeXdBQUFBTEFBQUFCa0U1VEl6ZG9pTkFCRkcyQ05PTEkwQVV3dTJ6TmY0alFJVzhLZlZORmlSQWFDU1BnMmZzSkVDUmZBZGZFaTBtUUVxRElYUllrU2RBK2" +
                "UrY1YzN3FKMEMvek5LVWkxc21RRElIUTl1VVNpUkFJYkI3S1Zna0kwQVBMZ0FBQUFzQUFBQUdRVGxNak4yaUkwRHFQV1BEY1lValFJS1dobXlTd1NOQUJ2S2VhcFVXSkVBSVUxR21RL01qUUFsdHJ" +
                "2M2hZU1JBbzFobUd2emtKVURGcXVIMllNNG1RSDR3WU9MMUl5ZEF4Q2FSTENwT0prQmJpUXZSem1FbVFBODFBQUFBRVFBQUFBWkJPVXlNM2FJalFJMmE2QTkwbGlOQXhEaVIwVTl0STBCZHVOdUJ" +
                "TRW9qUUpZMWpDbGh2U05BNFZjc1EyTHZJVUFJQUFGN0E0UWZRRlFjVHNPTTNCNUF6Y0JrazE4UElFQTlHeFEzRWhnaVFCOHRES3N0WlNKQVA2M3NCWXJySVVDam8ybW56OGNpUUwzTHhROXBqeU5" +
                "BVTlzdzRhQ2lJa0RYQmI0d013Y2tRUE1nY2NsU1hDUkFDd0FBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFB" +
                "QUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFB" +
                "QUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQU" +
                "FBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQ" +
                "UFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQ" +
                "UFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQU" +
                "FBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUF" +
                "BQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQWNYQUFBQUFBRUFBQUFEQUFBQUJDVkJkSEp" +
                "2Y0c5ekxrMWhZMmhwYm1WZlRHVmhjbTVwYm1jdVIyVnpkSFZ5WlVOc1lYTnpBZ0FBQUFrSUFBQUFDUWtBQUFBSkNnQUFBQThhQUFBQUFRQUFBQWFQTC9YY1pCdjZQdzhiQUFBQUFRQUFBQWJOSHh" +
                "3ZEtlMGhRQThjQUFBQUFDQUFBQUlBQVFBQUFQLy8vLzhCQUFBQUFBQUFBQXdDQUFBQVRVRmpZMjl5WkM1TllXTm9hVzVsVEdWaGNtNXBibWNzSUZabGNuTnBiMjQ5TXk0NExqQXVNQ3dnUTNWc2R" +
                "IVnlaVDF1WlhWMGNtRnNMQ0JRZFdKc2FXTkxaWGxVYjJ0bGJqMXVkV3hzQlFFQUFBREFBVUZqWTI5eVpDNU5ZV05vYVc1bFRHVmhjbTVwYm1jdVZtVmpkRzl5VFdGamFHbHVaWE11VFhWc2RHbGp" +
                "iR0Z6YzFOMWNIQnZjblJXWldOMGIzSk5ZV05vYVc1bFlERmJXMEZqWTI5eVpDNVRkR0YwYVhOMGFXTnpMa3RsY201bGJITXVSSGx1WVcxcFkxUnBiV1ZYWVhKd2FXNW5MQ0JCWTJOdmNtUXVVM1J" +
                "oZEdsemRHbGpjeXdnVm1WeWMybHZiajB6TGpndU1DNHdMQ0JEZFd4MGRYSmxQVzVsZFhSeVlXd3NJRkIxWW14cFkwdGxlVlJ2YTJWdVBXNTFiR3hkWFFnQUFBQXZUWFZzZEdsamJHRnpjMU4xY0h" +
                "CdmNuUldaV04wYjNKTllXTm9hVzVs" },
                {DEFENSE, " here too!!" }
            };
            #endregion
            foreach (var option in new string[] { OFFENSE, DEFENSE })
            {
                try
                {
                    ClassifierTree cTree;
                    string contents;

                    AssetManager assets = this.Assets;
                    var filename = $"{option}.{Classifier.FileExtension}";
                    var filepath = $"{GetExternalFilesDir(null)}/{filename}";

                    using (StreamReader sr = new StreamReader(assets.Open(filename)))
                    {
                        contents = sr.ReadToEnd();
                        cTree = Serializer.Deserialize<ClassifierTree>(contents);
                        if (cTree != null) Log.Debug("Loading classifier", $"Loaded our {option} classifier tree (from asset file).");
                    }

                    if (cTree == null)
                    {
                        using (var streamReader = new StreamReader(filepath))
                        {
                            contents = streamReader.ReadToEnd();
                            Log.Debug("Loading classifier", $"Loading our {option} classifier tree, it currently contains: \n\n{contents}\n");

                            cTree = Serializer.Deserialize<ClassifierTree>(contents);
                        } 
                    }
                    //if (cTree == null) cTree = Serializer.Deserialize<ClassifierTree>(savedClassifierString[option]);
                    if (cTree == null) throw new Exception($"Classifier deserialization failed - filename {filepath}");

                    CueClassifiers[option] = cTree.MainClassifier;
                    Dataset = new DataSet<DKS> { Name = cTree.MainClassifier.MatchingDatasetName };
                    foreach (var gClass in cTree.GestureClasses)
                    {
                        Dataset.AddClass(gClass);
                        //if (cTree.CueClassifiers.ContainsKey(gClass.className)) CueClassifiers.Add(gClass.className, cTree.CueClassifiers[gClass.className]);
                    }
                    SelectedGestureClass = Dataset.Classes.FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Log.Debug("MachineLearning|Load Classifier", ex.ToString());
                    Speech.SayAllOf("Cannot launch melee - no melee classifier found.").Wait();
                    Finish();
                }
            }            
        }

        public override async Task<Sequence<DKS>> Analyze(Sequence<DKS> sequence)
        {
            if (Classifier != null && Classifier.MachineOnline)
            {
                sequence.RecognizedAsIndex = await Classifier.Recognize(sequence);
                //sequence.TrueClassIndex = SelectedGestureClass.index;
            }
            return sequence;
        }

        //protected virtual void SetUpButtonClicks()
        //{
        //    //if (_userTrainingBtn != null) _userTrainingBtn.CheckedChange += ToggleButtons;
        //    //if (_cuedSingleBtn != null) _cuedSingleBtn.CheckedChange += ToggleButtons;
        //    //if (_cuedSeriesBtn != null) _cuedSeriesBtn.CheckedChange += ToggleButtons;
        //    if (_userTrainingBtn != null) _userTrainingBtn.Click += ToggleButtons;
        //    if (_cuedSingleBtn != null) _cuedSingleBtn.Click += ToggleButtons;
        //    if (_cuedSeriesBtn != null) _cuedSeriesBtn.Click += ToggleButtons;
        //}

        //protected void ToggleButtons(object sender, EventArgs e)
        //{
        //    var buttons = new Button[] { _userTrainingBtn, _cuedSingleBtn, _cuedSeriesBtn };
        //    var booleans = new bool[] { _userTrainingMode, _singleMode, _seriesMode };
        //    RunOnUiThread(() =>
        //    {
        //        foreach (var i in Enumerable.Range(0, 3))
        //        {
        //            var s = (Button)sender;
        //            if (s == buttons[i])
        //            {
        //                booleans[i] = true;
        //                s.SetTextColor(Android.Graphics.Color.Blue);
        //            }
        //            else
        //            {
        //                booleans[i] = false;
        //                s.SetTextColor(Android.Graphics.Color.Gray);
        //            }
        //        }
        //    });
        //    _userTrainingMode = booleans[0];
        //    _singleMode = booleans[1];
        //    _seriesMode = booleans[2];
        //}
        
        //public async Task GimmeCue(GestureClass gclass = null)
        //{
        //    CuePrompter = new MeleeCuePrompter(this);
        //    await CuePrompter.WaitBeforeCue();
        //    CuePrompter.ProvideCue();
        //    return;

        //    //SelectedGestureClass = gclass ?? Dataset.Classes.GetRandom();
        //    //AlternativeResetStage($"Cueing {SelectedGestureClass.className}", SelectedGestureClass);

        //    //CuePrompter = new MeleeCuePrompter(this);
        //    //CuePrompter.ProvideCue(SelectedGestureClass);

        //    ////CurrentStage.Activate();  // Is included in RunUntilFound, below.

        //    //await Task.Run(async () =>
        //    //{
        //    //    // Now start the gesture recognition stage and run until it comes back saying it's got one.
        //    //    var currentStage = (SelfEndpointingSingleGestureRecognizer)CurrentStage;
        //    //    var resultSeq = await currentStage.RunUntilFound(SelectedGestureClass);
        //    //    resultSeq.Metadata = currentStage.GetMetadata(GetAccelForDatapoint);

        //    //    //// To work out the "promptness" component of their score, we need to see where the best fit *beginning* of their gesture was.
        //    //    //var reversePeakFinder = new PeakFinder<T>.IncrementingStartIndexVariant<T>(
        //    //    //    resultSeq.SourcePath.ToList(),
        //    //    //    (seq) =>
        //    //    //    {
        //    //    //        var Seq = new Sequence<T>() { SourcePath = seq.ToArray() };
        //    //    //        var analyzedSeq = Current.Analyze(Seq).Result;

        //    //    //        if (SelectedGestureClass != null && Seq.RecognizedAsIndex != SelectedGestureClass.index) return double.NaN;
        //    //    //        else return analyzedSeq.RecognitionScore;
        //    //    //    }, thresholdScore: 1.5, minLength: Dataset?.MinSequenceLength ?? 5); // Might need to tweak this depending on how the parsing of the stuff works out.

        //    //    //resultSeq = new Sequence<T>() { SourcePath = reversePeakFinder.FindBestSequence().ToArray() };
        //    //    ////MostRecentSample = Current.Analyze(resultSeq).Result;

        //    //    CuePrompter.TimeElapsed = resultSeq.Metadata.Delay;
        //    //    ResolveEndOfGesture(resultSeq);
        //    //    CuePrompter = null;
        //    //    Choreographer?.ProceedWithNextCue();
        //    //});
        //}
        

        //public virtual void DisplaySampleInfo(Sequence<DKS> sequence)
        //{
        //    if (sequence == null || sequence.SourcePath.Length < 3 || sequence.RecognizedAsIndex == -1) return;

        //    var mThis = sequence.Metadata;
        //    var mAvg = Dataset.ActualGestureClasses[sequence.RecognizedAsIndex].AverageMetadata;

        //    _qualityThis.Text = $"{mThis.QualityScore:f2} pts";
        //    _qualityAvg.Text = $"{mAvg.QualityScore:f2} pts";
        //    _qualityRatio.Text = $"{(mThis.QualityScore / mAvg.QualityScore):f1}x";

        //    _delayThis.Text = $"{mThis.Delay.TotalMilliseconds:f0} ms";
        //    _delayAvg.Text = $"{mAvg.Delay.TotalMilliseconds:f0} ms";
        //    _delayRatio.Text = $"{(mThis.Delay.TotalMilliseconds / mAvg.Delay.TotalMilliseconds):f1}x";

        //    _durationThis.Text = $"{mThis.Duration.TotalMilliseconds:f0} ms";
        //    _durationAvg.Text = $"{mAvg.Duration.TotalMilliseconds:f0} ms";
        //    _durationRatio.Text = $"{(mThis.Duration.TotalMilliseconds / mAvg.Duration.TotalMilliseconds):f1}x";

        //    _numptsThis.Text = $"{mThis.NumPoints:f0}";
        //    _numptsAvg.Text = $"{mAvg.NumPoints:f1}";
        //    _numptsRatio.Text = $"{(mThis.NumPoints / mAvg.NumPoints):f1}x";

        //    _pkaccelThis.Text = $"{mThis.PeakAccel:f2} m/s2";
        //    _pkaccelAvg.Text = $"{mAvg.PeakAccel:f2} m/s2";
        //    _pkaccelRatio.Text = $"{(mThis.PeakAccel / mAvg.PeakAccel):f1}x";
        //}

        //public class MeleeCuePrompter : CuePrompter<DKS>
        //{
        //    private MeleeBetaActivity Current;
        //    public MeleeCuePrompter(MeleeBetaActivity parentActivity) : base(parentActivity)
        //    {
        //        Current = parentActivity;
        //    }

        //    public override void SetButtonEnabledState(bool state)
        //    {
        //        Current.RunOnUiThread(() =>
        //        {
        //            Current._userTrainingBtn.Enabled
        //            = Current._cuedSingleBtn.Enabled
        //            = Current._cuedSeriesBtn.Enabled
        //            = state;
        //        });                
        //    }

        //    public override async void ReactToFinalizedGesture(Sequence<DKS> Seq) // TODO - needs update!
        //    {
        //        //SetButtonEnabledState(true);
        //        //var button = Current.FindViewById<Button>(Resource.Id.mlrn_cue_button);

        //        //if (Seq.RecognizedAsIndex < 0) button.Text = "(Unrecognized?!?) ...Again!";
        //        //else if (Seq.RecognizedAsIndex != Current.SelectedGestureClass.index) button.Text = $"(Looked like {Seq.RecognizedAsName}!) ...Again!";
        //        //else
        //        //{
        //        //    var score = Seq.RecognitionScore;
        //        //    //var interval = Stopwatch.Elapsed;
        //        //    button.Text = $"({score:f2} pts / {TimeElapsed.TotalMilliseconds:f0} ms) ...Again!";
        //        //}

        //        //if (button.Visibility == ViewStates.Visible && Current.FindViewById<CheckBox>(Resource.Id.mlrn_cue_repeat_checkbox).Checked)
        //        //{
        //        //    await Task.Delay((int)(1000 * (1 + 2 * Res.Random)));
        //        //    button.CallOnClick();
        //        //}
        //    }
        //}
    }
}