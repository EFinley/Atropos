
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

namespace Atropos.Melee
{
    /// <summary>
    /// Specific implementation - must specify the type argument of the underlying base class,
    /// and then in ResetStage launch a stage with an appropriate <see cref="LoggingSensorProvider{T}"/>.
    /// 
    /// <para>Type argument here is constrained (at present) to one of: Vector2, Vector3, <seealso cref="Datapoint{T}"/>, <seealso cref="Datapoint{T1, T2}"/>.</para>
    /// </summary>
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait, WindowSoftInputMode = SoftInput.AdjustPan)]
    public class MeleeBetaActivity : MeleeBetaActivity<Datapoint<Vector3, Vector3>>
    {

        protected new static MeleeBetaActivity Current { get { return (MeleeBetaActivity)CurrentActivity; } set { CurrentActivity = value; } }

        protected override void ResetStage(string label)
        {
            ////if (!_advancedCueMode)
            //    CurrentStage = new MachineLearningActivity.MachineLearningStage(label, Dataset, 
            //        new ClusterLoggingProvider<Vector3, Vector3>(SensorType.LinearAcceleration, SensorType.Gravity));
            ////else CurrentStage = new SelfEndpointingSingleGestureRecognizer(label, Classifier,
            ////            new ClusterLoggingProvider<Vector3, Vector3>(SensorType.LinearAcceleration, SensorType.Gravity));
        }

        protected override void AlternativeResetStage(string label, params object[] info)
        {
            //CurrentStage = new MachineLearningActivity.SelfEndpointingSingleGestureRecognizer(label, Classifier,
            //            new ClusterLoggingProvider<Vector3, Vector3>(SensorType.LinearAcceleration, SensorType.Gravity));
        }

        public override double GetAccelForDatapoint(Datapoint<Vector3, Vector3> datapoint)
        {
            return SequenceMetadata.GetSubvectorOneMagnitude<Datapoint<Vector3, Vector3>, Vector3, Vector3>(datapoint);
        }
    }

    //[Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait, WindowSoftInputMode = SoftInput.AdjustPan)]
    //public class MachineLearningActivity : MachineLearningActivity<Vector6>
    //{
    //    protected new static MachineLearningActivity Current { get { return (MachineLearningActivity)CurrentActivity; } set { CurrentActivity = value; } }

    //    protected override void ResetStage(string label)
    //    {
    //        CurrentStage = new MachineLearningStage(label, Dataset,
    //            new DualVector3LoggingProvider(SensorType.LinearAcceleration, SensorType.Gravity)); // Does NOT autostart!
    //    }
    //}

    //[Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait, WindowSoftInputMode = SoftInput.AdjustPan)]
    //public class MachineLearningActivity : MachineLearningActivity<Vector2>
    //{
    //    protected new static MachineLearningActivity Current { get { return (MachineLearningActivity)CurrentActivity; } set { CurrentActivity = value; } }

    //    protected override void ResetStage(string label)
    //    {
    //        //if (CurrentStage != null && CurrentStage.IsActive) CurrentStage.Deactivate(); // Now happens as part of assigning to CurrentStage.
    //        CurrentStage = new MachineLearningStage(label, Dataset, new PlanarizedGestureProvider()); // Does NOT autostart!
    //    }
    //}

    public abstract partial class MeleeBetaActivity<T> 
        : MachineLearningActivity<T> 
        where T : struct
    {
        protected static new MeleeBetaActivity<T> Current { get { return (MeleeBetaActivity<T>)CurrentActivity; } set { CurrentActivity = value; } }
        protected static new MachineLearningActivity<T>.MachineLearningStage CurrentStage
        {
            get { return (MachineLearningActivity<T>.MachineLearningStage)BaseActivity.CurrentStage; }
            set { BaseActivity.CurrentStage = value; }
        }

        #region Data Members (and tightly linked functions / properties)
        protected TextView _gestureClassLabel,
                           _qualityThis, _qualityAvg, _qualityRatio,
                           _delayThis, _delayAvg, _delayRatio,
                           _durationThis, _durationAvg, _durationRatio,
                           _numptsThis, _numptsAvg, _numptsRatio,
                           _pkaccelThis, _pkaccelAvg, _pkaccelRatio;
        protected Button _userTrainingBtn, 
            _cuedSingleBtn, 
            _cuedSeriesBtn;

        //protected bool _userTrainingMode { get { return _userTrainingBtn?.Checked ?? false; } }
        //protected bool _singleMode { get { return _cuedSingleBtn?.Checked ?? false; } }
        //protected bool _seriesMode { get { return _cuedSeriesBtn?.Checked ?? false; } }
        protected bool _userTrainingMode { get; set; }
        protected bool _singleMode { get; set; }
        protected bool _seriesMode { get; set; }
        protected bool _seriesModeActive { get { return _seriesMode && Choreographer != null; } }

        protected bool _collectingData = false;
        protected bool _advancedCueMode { get { return !_userTrainingMode && _seriesMode; } }
        protected Choreographer Choreographer { get; set; }

        protected override void ResetStage(string label) { throw new NotImplementedException(); }
        protected override void AlternativeResetStage(string label, params object[] info) { ResetStage(label); }
        public abstract double GetAccelForDatapoint(T datapoint);

        protected void FindAllViews()
        {
            _gestureClassLabel = FindViewById<TextView>(Resource.Id.melee_gc_label);

            _qualityThis = FindViewById<TextView>(Resource.Id.melee_qual_this);
            _qualityAvg = FindViewById<TextView>(Resource.Id.melee_qual_avg);
            _qualityRatio = FindViewById<TextView>(Resource.Id.melee_qual_ratio);

            _delayThis = FindViewById<TextView>(Resource.Id.melee_delay_this);
            _delayAvg = FindViewById<TextView>(Resource.Id.melee_delay_avg);
            _delayRatio = FindViewById<TextView>(Resource.Id.melee_delay_ratio);

            _durationThis = FindViewById<TextView>(Resource.Id.melee_dur_this);
            _durationAvg = FindViewById<TextView>(Resource.Id.melee_dur_avg);
            _durationRatio = FindViewById<TextView>(Resource.Id.melee_dur_ratio);

            _numptsThis = FindViewById<TextView>(Resource.Id.melee_numpts_this);
            _numptsAvg = FindViewById<TextView>(Resource.Id.melee_numpts_avg);
            _numptsRatio = FindViewById<TextView>(Resource.Id.melee_numpts_ratio);

            _pkaccelThis = FindViewById<TextView>(Resource.Id.melee_pkaccel_this);
            _pkaccelAvg = FindViewById<TextView>(Resource.Id.melee_pkaccel_avg);
            _pkaccelRatio = FindViewById<TextView>(Resource.Id.melee_pkaccel_ratio);

            _userTrainingBtn = FindViewById<Button>(Resource.Id.melee_user_training_btn);
            _cuedSingleBtn = FindViewById<Button>(Resource.Id.melee_cued_single_btn);
            _cuedSeriesBtn = FindViewById<Button>(Resource.Id.melee_cued_series_btn);
        }

        #endregion

        #region Activity Lifecycle Methods
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.MeleeBeta);
            FindAllViews();

            //GestureClassList = FragmentManager.FindFragmentById<GestureClassListFragment>(Resource.Id.mlrn_gestureclass_list_fragment);
            //LatestSample = FragmentManager.FindFragmentById<LatestSampleFragment>(Resource.Id.mlrn_latest_sample_display);

            Dataset = new DataSet<T>();
            Classifier = new Classifier(); // Just so it's never null - it'll be assigned a more useful value shortly.

            SetUpButtonClicks();
        }

        protected override async void OnResume()
        {
            await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: false);
            LoadMeleeClassifier();
        }

        protected override void OnPause()
        {
            base.OnPause();
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

                        if (_userTrainingMode)
                        {
                            ResetStage("Training stage");
                            CurrentStage.Activate();
                        }
                    }
                    return true; // Handled it, thanks.
                }
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
                        //Log.Debug("MachineLearning", $"{keyCode} up");

                        if (_userTrainingMode)
                        {
                            // Halt the gesture-collection stage and query it.
                            var resultData = CurrentStage.StopAndReturnResults();
                            var resultSeq = new Sequence<T>() { SourcePath = resultData };
                            resultSeq.Metadata = CurrentStage.GetMetadata(GetAccelForDatapoint);
                            ResolveEndOfGesture(resultSeq);
                        }
                        else if (_singleMode)
                        {
                            Choreographer = new Choreographer(Dataset);
                            Choreographer.OnSendCue += async (o, eargs) =>
                            {
                                await GimmeCue(eargs.Value);
                                Choreographer.Deactivate();
                            };
                            Choreographer.Activate();
                        }
                        else if (!_seriesModeActive)
                        {
                            Choreographer = new Choreographer(Dataset);
                            Choreographer.OnSendCue += async (o, eargs) => { await GimmeCue(eargs.Value); };
                            Choreographer.Activate();
                        }
                        else
                        {
                            Choreographer?.Deactivate();
                        }

                        _collectingData = false;
                        return true; // Handled it, thanks.  
                    }
                }
            }
            return base.OnKeyUp(keyCode, e);
        }

        protected void ResolveEndOfGesture(Sequence<T> resultSequence)
        {
            var SelectedGestureClassIndex = SelectedGestureClass.index;

            MostRecentSample = resultSequence;
            Dataset?.AddSequence(MostRecentSample);

            MostRecentSample = Analyze(MostRecentSample).Result;

            // If right or wrong, tweak the display properties of the sample.  This depends on the active mode.
            DisplaySampleInfo(MostRecentSample);
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
            else
            {
                CuePrompter?.ReactToFinalizedGesture(MostRecentSample);
            }
        }

        protected void LoadMeleeClassifier()
        {
            //var assetfile = this.Assets.Open("MeleeClassifier.txt");
            var filepath = this.GetExternalFilesDir(null).ToString() + "/Melee." + Classifier.FileExtension;
            #region Hardcoded classifier string (backup)
            var savedClassifierString = "AAEAAAD/////AQAAAAAAAAAMAgAAAEJBdHJvcG9zR2FtZSwgVmVyc2lvbj0xLjAuMC4wLCBDdWx0dXJlPW5ldXRyYWws"
                +"IFB1YmxpY0tleVRva2VuPW51bGwFAQAAACNBdHJvcG9zLk1hY2hpbmVfTGVhcm5pbmcuQ2xhc3NpZmllcgQAAAAOU1ZNX1NlcmlhbGl6ZWQVTWF0Y2hp"
                +"bmdfRGF0YXNldF9OYW1lGE1hdGNoaW5nX0RhdGFzZXRfQ2xhc3Nlcx5NYXRjaGluZ19EYXRhc2V0X1NlcXVlbmNlQ291bnQHAQQAAidBdHJvcG9zLk1h"
                +"Y2hpbmVfTGVhcm5pbmcuR2VzdHVyZUNsYXNzW10CAAAACAIAAAAJAwAAAAYEAAAABU1lbGVlCQUAAAAWAAAADwMAAAAAAAEAAgABAAAA/////wEAAAAA"
                +"AAAADAIAAABNQWNjb3JkLk1hY2hpbmVMZWFybmluZywgVmVyc2lvbj0zLjguMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPW51bGwF"
                +"AQAAAMABQWNjb3JkLk1hY2hpbmVMZWFybmluZy5WZWN0b3JNYWNoaW5lcy5NdWx0aWNsYXNzU3VwcG9ydFZlY3Rvck1hY2hpbmVgMVtbQWNjb3JkLlN0"
                +"YXRpc3RpY3MuS2VybmVscy5EeW5hbWljVGltZVdhcnBpbmcsIEFjY29yZC5TdGF0aXN0aWNzLCBWZXJzaW9uPTMuOC4wLjAsIEN1bHR1cmU9bmV1dHJh"
                +"bCwgUHVibGljS2V5VG9rZW49bnVsbF1dCAAAAC9NdWx0aWNsYXNzU3VwcG9ydFZlY3Rvck1hY2hpbmVgMytjYWNoZVRocmVzaG9sZBJPbmVWc09uZWAy"
                +"K2luZGljZXMRT25lVnNPbmVgMittb2RlbHMRT25lVnNPbmVgMittZXRob2QhT25lVnNPbmVgMis8VHJhY2s+a19fQmFja2luZ0ZpZWxkMUNsYXNzaWZp"
                +"ZXJCYXNlYDIrPE51bWJlck9mQ2xhc3Nlcz5rX19CYWNraW5nRmllbGQWVHJhbnNmb3JtQmFzZWAyK2lucHV0cxdUcmFuc2Zvcm1CYXNlYDIrb3V0cHV0"
                +"cwMEBAQAAAAADFN5c3RlbS5JbnQzMiJBY2NvcmQuTWFjaGluZUxlYXJuaW5nLkNsYXNzUGFpcltdAgAAALoBQWNjb3JkLk1hY2hpbmVMZWFybmluZy5W"
                +"ZWN0b3JNYWNoaW5lcy5TdXBwb3J0VmVjdG9yTWFjaGluZWAxW1tBY2NvcmQuU3RhdGlzdGljcy5LZXJuZWxzLkR5bmFtaWNUaW1lV2FycGluZywgQWNj"
                +"b3JkLlN0YXRpc3RpY3MsIFZlcnNpb249My44LjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1udWxsXV1bXVtdAgAAAD1BY2NvcmQu"
                +"TWFjaGluZUxlYXJuaW5nLlZlY3Rvck1hY2hpbmVzLk11bHRpY2xhc3NDb21wdXRlTWV0aG9kAgAAAAEICAgCAAAACAhAAAAACQMAAAAJBAAAAAX7////"
                +"PUFjY29yZC5NYWNoaW5lTGVhcm5pbmcuVmVjdG9yTWFjaGluZXMuTXVsdGljbGFzc0NvbXB1dGVNZXRob2QBAAAAB3ZhbHVlX18ACAIAAAABAAAAAQIA"
                +"AAAAAAAAAgAAAAcDAAAAAAEAAAABAAAABCBBY2NvcmQuTWFjaGluZUxlYXJuaW5nLkNsYXNzUGFpcgIAAAAF+v///yBBY2NvcmQuTWFjaGluZUxlYXJu"
                +"aW5nLkNsYXNzUGFpcgIAAAAGY2xhc3MxBmNsYXNzMgAACAgCAAAAAQAAAAAAAAAHBAAAAAEBAAAAAQAAAAS4AUFjY29yZC5NYWNoaW5lTGVhcm5pbmcu"
                +"VmVjdG9yTWFjaGluZXMuU3VwcG9ydFZlY3Rvck1hY2hpbmVgMVtbQWNjb3JkLlN0YXRpc3RpY3MuS2VybmVscy5EeW5hbWljVGltZVdhcnBpbmcsIEFj"
                +"Y29yZC5TdGF0aXN0aWNzLCBWZXJzaW9uPTMuOC4wLjAsIEN1bHR1cmU9bmV1dHJhbCwgUHVibGljS2V5VG9rZW49bnVsbF1dW10CAAAACQcAAAAHBwAA"
                +"AAABAAAAAQAAAAS2AUFjY29yZC5NYWNoaW5lTGVhcm5pbmcuVmVjdG9yTWFjaGluZXMuU3VwcG9ydFZlY3Rvck1hY2hpbmVgMVtbQWNjb3JkLlN0YXRp"
                +"c3RpY3MuS2VybmVscy5EeW5hbWljVGltZVdhcnBpbmcsIEFjY29yZC5TdGF0aXN0aWNzLCBWZXJzaW9uPTMuOC4wLjAsIEN1bHR1cmU9bmV1dHJhbCwg"
                +"UHVibGljS2V5VG9rZW49bnVsbF1dAgAAAAkIAAAADAkAAABIQWNjb3JkLlN0YXRpc3RpY3MsIFZlcnNpb249My44LjAuMCwgQ3VsdHVyZT1uZXV0cmFs"
                +"LCBQdWJsaWNLZXlUb2tlbj1udWxsBQgAAAC2AUFjY29yZC5NYWNoaW5lTGVhcm5pbmcuVmVjdG9yTWFjaGluZXMuU3VwcG9ydFZlY3Rvck1hY2hpbmVg"
                +"MVtbQWNjb3JkLlN0YXRpc3RpY3MuS2VybmVscy5EeW5hbWljVGltZVdhcnBpbmcsIEFjY29yZC5TdGF0aXN0aWNzLCBWZXJzaW9uPTMuOC4wLjAsIEN1"
                +"bHR1cmU9bmV1dHJhbCwgUHVibGljS2V5VG9rZW49bnVsbF1dCwAAACBMT0dMSUtFTElIT09EX0RFQ0lTSU9OX1RIUkVTSE9MRB1TdXBwb3J0VmVjdG9y"
                +"TWFjaGluZWAyK2tlcm5lbCVTdXBwb3J0VmVjdG9yTWFjaGluZWAyK3N1cHBvcnRWZWN0b3JzHlN1cHBvcnRWZWN0b3JNYWNoaW5lYDIrd2VpZ2h0cyBT"
                +"dXBwb3J0VmVjdG9yTWFjaGluZWAyK3RocmVzaG9sZDdTdXBwb3J0VmVjdG9yTWFjaGluZWAyKzxJc1Byb2JhYmlsaXN0aWM+a19fQmFja2luZ0ZpZWxk"
                +"N1N1cHBvcnRWZWN0b3JNYWNoaW5lYDIrTE9HTElLRUxJSE9PRF9ERUNJU0lPTl9USFJFU0hPTERBQmluYXJ5TGlrZWxpaG9vZENsYXNzaWZpZXJCYXNl"
                +"YDErTE9HTElLRUxJSE9PRF9ERUNJU0lPTl9USFJFU0hPTEQxQ2xhc3NpZmllckJhc2VgMis8TnVtYmVyT2ZDbGFzc2VzPmtfX0JhY2tpbmdGaWVsZBZU"
                +"cmFuc2Zvcm1CYXNlYDIraW5wdXRzF1RyYW5zZm9ybUJhc2VgMitvdXRwdXRzAAQDBwAAAAAAAAAGLEFjY29yZC5TdGF0aXN0aWNzLktlcm5lbHMuRHlu"
                +"YW1pY1RpbWVXYXJwaW5nCQAAABFTeXN0ZW0uRG91YmxlW11bXQYGAQYGCAgIAgAAAO85+v5CLua/Bfb///8sQWNjb3JkLlN0YXRpc3RpY3MuS2VybmVs"
                +"cy5EeW5hbWljVGltZVdhcnBpbmcDAAAABWFscGhhBmxlbmd0aAZkZWdyZWUAAAAGCAgJAAAAAAAAAAAA8D8CAAAAAQAAAAkLAAAACQwAAAASiU4wZnfz"
                +"PwHvOfr+Qi7mv+85+v5CLua/AgAAAAAAAAABAAAABwsAAAABAQAAAAwAAAAHBgkNAAAACQ4AAAAJDwAAAAkQAAAACREAAAAJEgAAAAkTAAAACRQAAAAJ"
                +"FQAAAAkWAAAACRcAAAAJGAAAAA8MAAAADAAAAAYBdWOHFAHgPyBVrrwpggPAIFWuvCmCA8BZQUf8ETzvvyBVrrwpggNA4l8PE32MAkAgVa68KYIDQCBV"
                +"rrwpggPAIFWuvCmCA8AgVa68KYIDQKVNsmXTbPu/KA+xudV6AkAPDQAAAPgBAAAGRZq0WLs1JEAa5KrN";
            #endregion
            try
            {
                using (var streamReader = new StreamReader(filepath))
                {
                    var contents = streamReader.ReadToEnd();
                    Log.Debug("Loading classifier", $"Loading our classifier, it currently contains: \n\n{contents}\n");
                
                    Classifier = Serializer.Deserialize<Classifier>(contents);
                    if (Classifier == null) Classifier = Serializer.Deserialize<Classifier>(savedClassifierString);
                    if (Classifier == null) throw new Exception($"Classifier deserialization failed - filename {filepath}");
                }
            }
            catch (Exception ex)
            {
                Log.Debug("MachineLearning|Load Classifier", ex.ToString());
                Speech.SayAllOf("Cannot launch melee - no melee classifier found.").Wait();
                Finish();
            }

            Dataset = new DataSet<T> { Name = Classifier.MatchingDatasetName };
            foreach (var gC in Classifier.MatchingDatasetClasses) Dataset.AddClass(gC);
            SelectedGestureClass = Dataset.Classes.FirstOrDefault();
        }

        protected virtual void SetUpButtonClicks()
        {
            //if (_userTrainingBtn != null) _userTrainingBtn.CheckedChange += ToggleButtons;
            //if (_cuedSingleBtn != null) _cuedSingleBtn.CheckedChange += ToggleButtons;
            //if (_cuedSeriesBtn != null) _cuedSeriesBtn.CheckedChange += ToggleButtons;
            if (_userTrainingBtn != null) _userTrainingBtn.Click += ToggleButtons;
            if (_cuedSingleBtn != null) _cuedSingleBtn.Click += ToggleButtons;
            if (_cuedSeriesBtn != null) _cuedSeriesBtn.Click += ToggleButtons;
        }

        protected void ToggleButtons(object sender, EventArgs e)
        {
            var buttons = new Button[] { _userTrainingBtn, _cuedSingleBtn, _cuedSeriesBtn };
            var booleans = new bool[] { _userTrainingMode, _singleMode, _seriesMode };
            foreach (var i in Enumerable.Range(0, 3))
            {
                var s = (Button)sender;
                if (s == buttons[i])
                {
                    booleans[i] = true;
                    s.SetTextColor(Android.Graphics.Color.Blue);
                }
                else
                {
                    booleans[i] = false;
                    s.SetTextColor(Android.Graphics.Color.Gray);
                }
            }
            _userTrainingMode = booleans[0];
            _singleMode = booleans[1];
            _seriesMode = booleans[2];
        }
        
        public async Task GimmeCue(GestureClass gclass = null)
        {
            SelectedGestureClass = gclass ?? Dataset.Classes.GetRandom();
            AlternativeResetStage($"Cueing {SelectedGestureClass.className}");

            CuePrompter = new MeleeCuePrompter<T>(this);
            CuePrompter.ProvideCue(SelectedGestureClass);

            //CurrentStage.Activate();  // Is included in RunUntilFound, below.

            await Task.Run(async () =>
            {
                // Now start the gesture recognition stage and run until it comes back saying it's got one.
                var currentStage = (MachineLearningActivity<T>.SelfEndpointingSingleGestureRecognizer)CurrentStage;
                var resultSeq = await currentStage.RunUntilFound(SelectedGestureClass);
                resultSeq.Metadata = currentStage.GetMetadata(GetAccelForDatapoint);

                //// To work out the "promptness" component of their score, we need to see where the best fit *beginning* of their gesture was.
                //var reversePeakFinder = new PeakFinder<T>.IncrementingStartIndexVariant<T>(
                //    resultSeq.SourcePath.ToList(),
                //    (seq) =>
                //    {
                //        var Seq = new Sequence<T>() { SourcePath = seq.ToArray() };
                //        var analyzedSeq = Current.Analyze(Seq).Result;

                //        if (SelectedGestureClass != null && Seq.RecognizedAsIndex != SelectedGestureClass.index) return double.NaN;
                //        else return analyzedSeq.RecognitionScore;
                //    }, thresholdScore: 1.5, minLength: Dataset?.MinSequenceLength ?? 5); // Might need to tweak this depending on how the parsing of the stuff works out.

                //resultSeq = new Sequence<T>() { SourcePath = reversePeakFinder.FindBestSequence().ToArray() };
                ////MostRecentSample = Current.Analyze(resultSeq).Result;

                CuePrompter.TimeElapsed = resultSeq.Metadata.Delay;
                ResolveEndOfGesture(resultSeq);
                CuePrompter = null;
                Choreographer?.ProceedWithNextCue();
            });
        }
        #endregion
        

        public virtual void DisplaySampleInfo(Sequence<T> sequence)
        {
            if (sequence == null || sequence.SourcePath.Length < 3 || sequence.RecognizedAsIndex == -1) return;

            var mThis = sequence.Metadata;
            var mAvg = Dataset.ActualGestureClasses[sequence.RecognizedAsIndex].AverageMetadata;

            _qualityThis.Text = $"{mThis.QualityScore:f2} pts";
            _qualityAvg.Text = $"{mAvg.QualityScore:f2} pts";
            _qualityRatio.Text = $"{(mThis.QualityScore / mAvg.QualityScore):f1}x";

            _delayThis.Text = $"{mThis.Delay.TotalMilliseconds:f0} ms";
            _delayAvg.Text = $"{mAvg.Delay.TotalMilliseconds:f0} ms";
            _delayRatio.Text = $"{(mThis.Delay.TotalMilliseconds / mAvg.Delay.TotalMilliseconds):f1}x";

            _durationThis.Text = $"{mThis.Duration.TotalMilliseconds:f0} ms";
            _durationAvg.Text = $"{mAvg.Duration.TotalMilliseconds:f0} ms";
            _durationRatio.Text = $"{(mThis.Duration.TotalMilliseconds / mAvg.Duration.TotalMilliseconds):f1}x";

            _numptsThis.Text = $"{mThis.NumPoints:f0}";
            _numptsAvg.Text = $"{mAvg.NumPoints:f1}";
            _numptsRatio.Text = $"{(mThis.NumPoints / mAvg.NumPoints):f1}x";

            _pkaccelThis.Text = $"{mThis.PeakAccel:f2} m/s2";
            _pkaccelAvg.Text = $"{mAvg.PeakAccel:f2} m/s2";
            _pkaccelRatio.Text = $"{(mThis.PeakAccel / mAvg.PeakAccel):f1}x";
        }

        public class MeleeCuePrompter<T2> : CuePrompter<T2> where T2 : struct
        {
            private MeleeBetaActivity<T2> Current;
            public MeleeCuePrompter(MeleeBetaActivity<T2> parentActivity) : base(parentActivity)
            {
                Current = parentActivity;
            }

            public override void SetButtonEnabledState(bool state)
            {
                Current._userTrainingBtn.Enabled 
                    = Current._cuedSingleBtn.Enabled
                    = Current._cuedSeriesBtn.Enabled
                    = state;
            }

            public override async void ReactToFinalizedGesture(Sequence<T2> Seq) // TODO - needs update!
            {
                //SetButtonEnabledState(true);
                //var button = Current.FindViewById<Button>(Resource.Id.mlrn_cue_button);

                //if (Seq.RecognizedAsIndex < 0) button.Text = "(Unrecognized?!?) ...Again!";
                //else if (Seq.RecognizedAsIndex != Current.SelectedGestureClass.index) button.Text = $"(Looked like {Seq.RecognizedAsName}!) ...Again!";
                //else
                //{
                //    var score = Seq.RecognitionScore;
                //    //var interval = Stopwatch.Elapsed;
                //    button.Text = $"({score:f2} pts / {TimeElapsed.TotalMilliseconds:f0} ms) ...Again!";
                //}

                //if (button.Visibility == ViewStates.Visible && Current.FindViewById<CheckBox>(Resource.Id.mlrn_cue_repeat_checkbox).Checked)
                //{
                //    await Task.Delay((int)(1000 * (1 + 2 * Res.Random)));
                //    button.CallOnClick();
                //}
            }
        }
    }
}