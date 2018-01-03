
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

namespace Atropos.Machine_Learning
{
    /// <summary>
    /// Specific implementation - must specify the type argument of the underlying base class,
    /// and then in ResetStage launch a stage with an appropriate <see cref="LoggingSensorProvider{T}"/>.
    /// 
    /// <para>Type argument here is constrained (at present) to one of: Vector2, Vector3, <seealso cref="Vector6"/>.  
    /// See also <seealso cref="Feature{T}"/> to implement any other types.</para>
    /// </summary>
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait, WindowSoftInputMode = SoftInput.AdjustPan)]
    public class MeleeAlphaActivity : MeleeAlphaActivity<Datapoint<Vector3, Vector3>>
    {

        protected new static MeleeAlphaActivity Current { get { return (MeleeAlphaActivity)CurrentActivity; } set { CurrentActivity = value; } }

        protected override void ResetStage(string label)
        {
            CurrentStage = new CuedGestureRecognizerStage(label, Classifier,
                new ClusterLoggingProvider<Vector3, Vector3>(SensorType.LinearAcceleration, SensorType.Gravity));
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

    public abstract partial class MeleeAlphaActivity<T>
        : MachineLearningActivity<T>
        where T : struct
    {
        protected new static MeleeAlphaActivity<T> Current { get { return (MeleeAlphaActivity<T>)CurrentActivity; } set { CurrentActivity = value; } }

        #region Gesture Stage for cued gestures

        public class CuedGestureRecognizerStage : MachineLearningActivity<T>.SelfEndpointingSingleGestureRecognizer
        {
            public CuedGestureRecognizerStage(string label, Classifier classifier, ILoggingProvider<T> Provider)
                : base(label, classifier, Provider)
            {

            }
        }

        #endregion

        #region Activity Lifecycle Methods
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            ResetStage("Cueing gesture");
        }
        #endregion

        #region Button Click / Volume Button Hold events
        public override bool OnKeyDown([GeneratedEnum] Keycode keyCode, KeyEvent e)
        {
            if (keyCode == Keycode.VolumeDown || keyCode == Keycode.VolumeUp)
            {
                lock (SelectedGestureClass)
                {
                    if (_listeningForGesture)
                    {
                        _collectingData = true;
                        //Log.Debug("MachineLearning", $"{keyCode} down");

                        AssertRecognition();

                        //CurrentStage.Activate();
                        return true; // Handled it, thanks.
                    }
                }
                if (_collectingData) return true; // While we're collecting, don't treat it as repeated presses
            }
            //return base.OnKeyDown(keyCode, e);
            return false;
        }
        public override bool OnKeyUp([GeneratedEnum] Keycode keyCode, KeyEvent e)
        {
            if (keyCode == Keycode.VolumeDown || keyCode == Keycode.VolumeUp)
            {
                if (_collectingData)
                {
                    lock (SelectedGestureClass)
                    {
                        Log.Debug("MeleeAlpha", $"Giving cue for {SelectedGestureClass.className}.");
                        GiveCueAndWait(SelectedGestureClass);

                        //Log.Debug("MachineLearning", $"{keyCode} up");

                        // Halt the gesture-collection stage and query it.
                        //var featureVectors = ((MachineLearningStage)CurrentStage).StopAndReturnResults();
                        //var SelectedGestureClassIndex = SelectedGestureClass.index;

                        //MostRecentSample = new Sequence<T>() { SourcePath = featureVectors };
                        //if (TeachOnlyMode) MostRecentSample.TrueClassIndex = SelectedGestureClassIndex;

                        //if (TeachOnlyMode) Dataset.AddSequence(MostRecentSample);

                        //CurrentStage.Deactivate();
                        //CurrentStage = new MachineLearningStage<T>($"Learning gesture {SelectedGestureClass.className}#{SelectedGestureClass.numExamples + SelectedGestureClass.numNewExamples}", Dataset);
                        //string StageLabel = (TeachOnlyMode)
                        //    ? $"Learning gesture {SelectedGestureClass.className}#{SelectedGestureClass.numExamples + SelectedGestureClass.numNewExamples}"
                        //    : $"Reading gesture (#{Dataset.SequenceCount + 1})";
                        // ResetStage(StageLabel);

                        //var vNormal = ((MachineLearningStage)CurrentStage).

                        //FindViewById(Resource.Id.mlrn_latest_sample_display).Visibility = ViewStates.Visible;
                        _collectingData = false;

                        //Task.Run(() =>
                        //{
                        //MostRecentSample = Analyze(MostRecentSample).Result;

                        // If right or wrong, tweak the display properties of the sample.  This may depend on TeachMode.
                        //if (!TeachOnlyMode && MostRecentSample.RecognizedAsIndex >= 0)
                        //{
                        //    var sc = MostRecentSample.RecognitionScore;
                        //    var prefix = (sc < 1) ? "Possibly " :
                        //                 (sc < 1.5) ? "Maybe " :
                        //                 (sc < 2) ? "Probably " :
                        //                 (sc < 2.5) ? "Clearly " :
                        //                 (sc < 3) ? "Certainly " :
                        //                 "A perfect ";
                        //    Speech.Say(prefix + MostRecentSample.RecognizedAsName);
                        //}
                        //});

                        _listView.Adapter = _listAdapter;
                        _listView.RequestLayout();
                        _latestSampleDisplay.RequestLayout();
                        //ButtonStates.Update(this);

                        return true; // Handled it, thanks. 
                    }
                }
            }
            //return base.OnKeyUp(keyCode, e);
            return false;
        }
        #endregion

        protected async void GiveCueAndWait(GestureClass targetClass)
        {
            await Task.Run(async () =>
            {
                var initialDelay = (Res.Random * 4 + 2) * (Res.Random * 4 + 1) * 100;
                Log.Debug("MeleeAlphaActivity", $"Cueing {targetClass?.className ?? "Anything"} after an initial delay of {initialDelay} ms.");
                await Task.Delay((int)initialDelay);

                ResetStage($"Cueing {targetClass?.className}");

                await Speech.SayAllOf("Ready...");
                await Task.Delay(500);
                await Speech.SayAllOf(targetClass?.className ?? "Anything");

                // Now start the gesture recognition stage and run until it comes back saying it's got one.
                var currentCued = (CuedGestureRecognizerStage)CurrentStage;
                var resultSeq = await currentCued.RunUntilFound(targetClass);

                // To work out the "promptness" component of their score, we need to see where the best fit *beginning* of their gesture was.
                var reversePeakFinder = new PeakFinder<T>.BackwardsCounting<T>(
                    resultSeq.SourcePath.ToList(),
                    (seq) =>
                    {
                        var Seq = new Sequence<T>() { SourcePath = seq.ToArray() };
                        var analyzedSeq = Current.Analyze(Seq).Result;

                        if (targetClass != null && Seq.RecognizedAsIndex != targetClass.index) return double.NaN;
                        else return analyzedSeq.RecognitionScore;
                    }, thresholdScore: 2.0); // Might need to tweak this depending on how the parsing of the stuff backward works out.
                // Hang on... we want the sequence which starts at t=0 and then changes the start time, which is NOT the same thing as this! D'oh.

                resultSeq = new Sequence<T>() { SourcePath = reversePeakFinder.SeekBestSequence().ToArray() };
                MostRecentSample = Current.Analyze(resultSeq).Result;

            });
            Dataset.AddSequence(MostRecentSample);
            DisplaySampleInfo(MostRecentSample);
            _listView.Adapter = _listAdapter;
            _listView.RequestLayout();
            _latestSampleDisplay.RequestLayout();
        }

        protected override void SetUpButtonClicks()
        {
            // Suspend (by nulling out) any buttons whose old click routines need to be suppressed in favour of new ones.
            var addNewClassBtn = _addNewClassButton;
            _addNewClassButton = null;

            base.SetUpButtonClicks();

            // And then unsuspend them again.
            _addNewClassButton = addNewClassBtn;

            if (_addNewClassButton != null)
                _addNewClassButton.Click += (o, e) =>
                {
                    if (_addNewClassButton.Text != "Remove")
                    {
                        string newName = _newClassNameField.Text;
                        if (Dataset.ClassNames.Contains(newName))
                        {
                            Toast.MakeText(this, $"Dataset already contains a gesture class named {newName}.", ToastLength.Short).Show();
                            return;
                        }
                        if (String.IsNullOrEmpty(newName)) return;

                        AssertRecognition();
                        MostRecentSample = null;

                        Dataset.AddClass(new ColourGestureClass() { className = newName });
                        _listView.SetSelection(Dataset.Classes.Count - 1);
                        SelectedGestureClass = Dataset.Classes.Last();
                        //SetUpAdapters(Dataset);
                    }
                    else
                    {
                        var currentClassIndex = Dataset.Classes.IndexOf(SelectedGestureClass);

                        Dataset.RemoveClass(SelectedGestureClass);

                        if (currentClassIndex < Dataset.Classes.Count) SelectedGestureClass = Dataset.Classes[currentClassIndex];
                        else if (currentClassIndex == Dataset.Classes.Count) SelectedGestureClass = Dataset.Classes.Last();
                        else if (Dataset.Classes.Count > 0) SelectedGestureClass = Dataset.Classes[0];
                        else SelectedGestureClass = null;
                    }

                    ResetStage("NextStage");

                    _newClassNameField.Text = null;
                    _newClassNameField.ClearFocus();
                };
        }

        public override void DisplaySampleInfo(ISequence sequence)
        {
            if (sequence == null || (sequence as Sequence<T>).SourcePath.Length < 3) return;

            _sampleVisualization.SetImageBitmap(sequence.Bitmap);
            bool SubmitButtonPermitted = true;

            // Contents of the text field, and selected item in the spinner, both depend on a number of settings & factors.

            SilentlySetClassnameSpinner(sequence.TrueClassIndex); // Does nothing if index < 0 (aka "I dunno"), although how that'd happen is unclear.

            // (A) Did we run it through an existing classifier?
            if (sequence.RecognizedAsIndex >= 0)
            {
                // (B) Did we get it right?
                if (sequence.TrueClassIndex == sequence.RecognizedAsIndex)
                {
                    _guessField.Text = $"Correctly executed with score NNN";
                    _guessField.SetTextColor(Android.Graphics.Color.Green);
                }
                // (C') We got it wrong, alas.
                else
                {
                    _guessField.Text = $"Incorrectly executed (Looked like {sequence.RecognizedAsName})";
                    _guessField.SetTextColor(Android.Graphics.Color.Red);
                }
            }
            // (A') Nope, we didn't recognize it (probably because we haven't trained a classifier yet)
            else
            {
                _guessField.Text = $"Recorded as {sequence.TrueClassName}";
                _guessField.SetTextColor(Android.Graphics.Color.Gray);
            }

            _latestSampleDisplay.Visibility = ViewStates.Visible;
            _discardSampleBtn.Enabled = SubmitButtonPermitted;

            //_listView.Adapter = null;
            //_listView.Adapter = _listAdapter;
            Dataset.TallySequences(); // Note sure if these are still needed or not, given the existence of ButtonStates.Update()... but we'll let it stand in any event.
            _listView.RequestLayout();
        }
    }
}