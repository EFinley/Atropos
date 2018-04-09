
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
using DKS = Atropos.DataStructures.DatapointSpecialVariants.DatapointKitchenSink;
using Atropos.Machine_Learning.Button_Logic;
using Accord.Math;
using static Atropos.DataStructures.DatapointSpecialVariants;
using static Atropos.Machine_Learning.FeatureListExtractor;

namespace Atropos.Machine_Learning
{
    /// <summary>
    /// Specific implementation - must specify the type argument of the underlying base class,
    /// and then in ResetStage launch a stage with an appropriate <see cref="LoggingSensorProvider{T}"/>.
    /// 
    /// <para>Type argument here is constrained (at present) to one of: Vector2, Vector3, <seealso cref="Datapoint{T}"/>, <seealso cref="Datapoint{T1, T2}"/>.</para>
    /// </summary>
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait, WindowSoftInputMode = SoftInput.AdjustPan)]
    public class MachineLearningActivity : MachineLearningPageActivity
    {

        protected new static MachineLearningActivity Current { get { return (MachineLearningActivity)CurrentActivity; } set { CurrentActivity = value; } }

        protected override void ResetStage(string label)
        {
            //if (!_advancedCueMode)
                CurrentGestureStage = new MachineLearningStage(label, Dataset, 
                    new LoggingSensorProvider<DKS>(new AdvancedProviders.GrabItAllSensorProvider(new GravGyroOrientationProvider())));
            //else CurrentStage = new SelfEndpointingSingleGestureRecognizer(label, Classifier,
            //            new ClusterLoggingProvider<Vector3, Vector3>(SensorType.LinearAcceleration, SensorType.Gravity));
        }

        protected override void AlternativeResetStage(string label, params object[] info)
        {
            CurrentGestureStage?.Deactivate();
            CurrentGestureStage = null;
            CurrentGestureStage = new SelfEndpointingSingleGestureRecognizer(label, Classifier,
                        new LoggingSensorProvider<DKS>(new AdvancedProviders.GrabItAllSensorProvider(new GravGyroOrientationProvider())));
        }
    }

    //[Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait, WindowSoftInputMode = SoftInput.AdjustPan)]
    //public class MachineLearningActivityABCVariant : MachineLearningPageActivity<Datapoint<float, float, float>>
    //{

    //    protected new static MachineLearningActivityABCVariant Current { get { return (MachineLearningActivityABCVariant)CurrentActivity; } set { CurrentActivity = value; } }

    //    protected override void ResetStage(string label)
    //    {
    //        //if (!_advancedCueMode)
    //        CurrentGestureStage = new MachineLearningStage(label, Dataset,
    //            new LoggingSensorProvider<Datapoint<float, float, float>>(new AdvancedProviders.ABCgestureCharacterizationProvider()));
    //        //else CurrentStage = new SelfEndpointingSingleGestureRecognizer(label, Classifier,
    //        //            new ClusterLoggingProvider<Vector3, Vector3>(SensorType.LinearAcceleration, SensorType.Gravity));
    //    }

    //    protected override void AlternativeResetStage(string label, params object[] info)
    //    {
    //        CurrentGestureStage?.Deactivate();
    //        CurrentGestureStage = null;
    //        CurrentGestureStage = new SelfEndpointingSingleGestureRecognizer(label, Classifier,
    //                    new LoggingSensorProvider<Datapoint<float, float, float>>(new AdvancedProviders.ABCgestureCharacterizationProvider()));
    //    }
    //}

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

    public abstract partial class MachineLearningActivity<T>
        : BaseActivity_Portrait, MachineLearningActivity<T>.IMachineLearningActivity
        where T : struct
    {
        protected static MachineLearningActivity<T> Current { get { return (MachineLearningActivity<T>)CurrentActivity; } set { CurrentActivity = value; } }
        //protected static MachineLearningStage CurrentGestureStage { get { return (MachineLearningStage)BaseActivity.CurrentStage; } set { BaseActivity.CurrentStage = value; } }
        protected static MachineLearningStage CurrentGestureStage { get; set; }

        public interface IMachineLearningActivity
        {
            GestureClass SelectedGestureClass { get; set; }
            DataSet<T> Dataset { get; set; }
            Sequence<T> MostRecentSample { get; }
            void SetUpAdapters(IDataset dataSet);
        } // Mostly a 'marker' interface, used because the "typeless" MachineLearningActivity is in this case the more derived class, instead of the less derived one.

        #region Data Members (and tightly linked functions / properties)
        public virtual DataSet<T> Dataset
        {
            get
            {
                return DataSet.Current as DataSet<T>;
            }
            set
            {
                DataSet.Current = value as DataSet;
            }
        }

        public GestureClass SelectedGestureClass { get; set; } = GestureClass.NullGesture;

        protected Sequence<T> _mostRecentSample;
        public virtual Sequence<T> MostRecentSample
        {
            get { return _mostRecentSample; }
            set
            {
                _mostRecentSample = value;
            }
        }

        //public Classifier Classifier { get { return Dataset.Classifier; } set { Dataset.Classifier = value; } }
        public Classifier Classifier { get; set; }
        protected Dictionary<GestureClass, Classifier> CueClassifiers = new Dictionary<GestureClass, Classifier>();

        protected abstract void ResetStage(string label);
        protected virtual void AlternativeResetStage(string label, params object[] info) { ResetStage(label); }
        protected CuePrompter<T> CuePrompter { get; set; }
        public virtual void SetUpAdapters(IDataset dataSet) { throw new NotImplementedException(); }
        #endregion

        public virtual async Task<Sequence<T>> Analyze(Sequence<T> sequence)
        {
            await Task.CompletedTask;
            throw new NotImplementedException("Attempt made to use MachineLearningActivity<T>.Analyze directly.");
        }
    }

    public class MachineLearningPageActivity : MachineLearningActivity<DKS>
    {
        #region Data members (and tightly linked properties & functions)
        public override DataSet<DKS> Dataset
        {
            get
            {
                return DataSet.Current as DataSet<DKS>;
            }
            set
            {
                DataSet.Current = value as DataSet;
                ButtonStates.Update(this);
            }
        }
        public override Sequence<DKS> MostRecentSample
        {
            get { return _mostRecentSample; }
            set
            {
                _mostRecentSample = value;
                RunOnUiThread(() => { _latestSampleDisplay.Visibility = (_mostRecentSample != null) ? ViewStates.Visible : ViewStates.Gone; });
            }
        }
        protected RadioButton _teachonly, _guessandteach, _cuemode;
        protected ImageView _sampleVisualization;
        protected TextView _guessField;
        protected Button _discardSampleBtn, 
            _loadDatasetButton, 
            _saveDatasetButton, 
            _clearDatasetButton, 
            _computeButton,
            _computeSingleButton,
            _addNewClassButton,
            _gimmeCueButton;
        protected ListView _listView;
        protected GestureClassListAdapter<DKS> _listAdapter;
        protected Spinner _classnameSpinner;
        protected ArrayAdapter<string> _classnameSpinnerAdapter;
        protected EditText _newClassNameField, _datasetNameField;
        protected LinearLayout _latestSampleDisplay;

        protected string filepath;
        protected ButtonStates ButtonStates = new ButtonStates();

        public bool TeachOnlyMode
        {
            get
            {
                //if (_teachonly.Checked && !_guessandteach.Checked) return true;
                //else if (!_teachonly.Checked && _guessandteach.Checked) return false;
                //else throw new Exception("Radiobutton issue!  Neither (or both) checked simultaneously.  Halp!"); // Probably no longer needed but may as well stay.
                return _teachonly.Checked;
            }
        }

        protected bool _collectingData = false;
        protected bool _listeningForGesture { get { return SelectedGestureClass != GestureClass.NullGesture
                                                    && !_collectingData; } }
        protected bool _advancedCueMode { get { return _cuemode != null && _cuemode.Checked && FindViewById<CheckBox>(Resource.Id.mlrn_cue_advanced_mode).Checked; } }

        protected override void ResetStage(string label) { throw new NotImplementedException(); }
        //private ProgressDialog _progressDialog;

        protected void FindAllViews()
        {
            _loadDatasetButton = FindViewById<Button>(Resource.Id.mlrn_dataset_load_btn);
            _saveDatasetButton = FindViewById<Button>(Resource.Id.mlrn_dataset_save_btn);
            _clearDatasetButton = FindViewById<Button>(Resource.Id.mlrn_dataset_clear_btn);
            _computeButton = FindViewById<Button>(Resource.Id.mlrn_study_dataset_btn);
            _computeSingleButton = FindViewById<Button>(Resource.Id.mlrn_study_gc_btn);

            _teachonly = FindViewById<RadioButton>(Resource.Id.mlrn_trainmode_teachonly);
            _guessandteach = FindViewById<RadioButton>(Resource.Id.mlrn_trainmode_guessandteach);
            _cuemode = FindViewById<RadioButton>(Resource.Id.mlrn_trainmode_cue);
            _gimmeCueButton = FindViewById<Button>(Resource.Id.mlrn_cue_button);

            _discardSampleBtn = FindViewById<Button>(Resource.Id.mlrn_latest_sample_discard);
            _sampleVisualization = FindViewById<ImageView>(Resource.Id.mlrn_latest_sample_visual);
            _guessField = FindViewById<TextView>(Resource.Id.mlrn_latest_sample_guessfield);
            _classnameSpinner = FindViewById<Spinner>(Resource.Id.mlrn_latest_sample_classname_spinner);
            _latestSampleDisplay = FindViewById<LinearLayout>(Resource.Id.mlrn_latest_sample_display);

            _listView = FindViewById<ListView>(Resource.Id.list);

            _addNewClassButton = FindViewById<Button>(Resource.Id.mlrn_add_gesture_class_btn);
            _newClassNameField = FindViewById<EditText>(Resource.Id.mlrn_new_gesture_class_namefield);

            _datasetNameField = FindViewById<EditText>(Resource.Id.mlrn_subheading_datasetnamefield);

            ButtonStates.AssignTargets(this);
        }

        #endregion

        #region Activity Lifecycle Methods
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.MachineLearning);
            FindAllViews();

            //GestureClassList = FragmentManager.FindFragmentById<GestureClassListFragment>(Resource.Id.mlrn_gestureclass_list_fragment);
            //LatestSample = FragmentManager.FindFragmentById<LatestSampleFragment>(Resource.Id.mlrn_latest_sample_display);

            Dataset = new DataSet<DKS>();
            Classifier = new Classifier();

            SetUpButtonClicks();
            SetUpAdapters(Dataset);

            ResetStage("Learning gesture");

            _classnameSpinner.ItemSelected += OnClassnameSpinnerChanged;

            ////// Testing stuff... deserialization issues cropping up.
            ////var v1 = new Datapoint<Vector3, Vector3>() { Value1 = Vector3.One, Value2 = 0.5f * Vector3.UnitX + 0.33f * Vector3.UnitY };
            //var v1 = new Datapoint<float>() { Value = 1.234f };
            //var v2 = new Datapoint<float, float>() { Value1 = 1.33f, Value2 = 168000f };
            //var v3 = new Datapoint<float, float, float>() { Value1 = 1.33f, Value2 = 168000f, Value3 = 0.0003f };
            //var v3b = (Datapoint<float, float, float>)(new Datapoint<float, float, float>()).FromArray(new float[] { 1.33f, 168000f, 0.0003f });
            //Log.Debug("MachineLearning|TEST", $"V3 is {v3.ToString()}; reconstructing it we get {v3b.ToString()}.  They're equal ({v3 == v3b}).");
            //var v4 = new DKS { Values = new Datapoint<Vector3, Vector3, Vector3, Quaternion, double>() { Value1 = Vector3.One, Value4 = Quaternion.Identity, Value5 = 0.1122334455 } };
            //var v5 = new Sequence<DKS>() { SourcePath = new DKS[] { v4 } };
            //////var v5 = new Datapoint<Datapoint<float>>(new Datapoint<float>(0.123f));
            //////var v6 = new Datapoint<Datapoint<float>, Datapoint<float>>() { Value1 = new Datapoint<float>(0.33f), Value2 = new Datapoint<float>(0.66f) };
            //////Log.Debug($"MachineLearning|TEST", $"Round-trip checks... v1 {Serializer.Check(v1)}, v2 {Serializer.Check(v2)}, v3 {Serializer.Check(v3)}, v4 {Serializer.Check(v4)}.");
            ////Log.Debug($"MachineLearning|TEST", $"Round-trip checks... v1 {Serializer.Check(v1)}, v2 {Serializer.Check(v2)}, v3 {Serializer.Check(v3)}.");
            //Log.Debug($"MachineLearning|TEST", $"Round-trip checks... v5 {Serializer.Check(v5)}.");
            //Log.Debug($"MachineLearning|TEST", $"Round-trip checks... v4 {Serializer.Check(v4)}.");
            //Log.Debug($"MachineLearning|TEST", $"Round-trip checks... v3 {Serializer.Check(v3)}.");
            //Log.Debug($"MachineLearning|TEST", $"Round-trip checks... v2 {Serializer.Check(v2)}.");
            //var v1s = Serializer.Serialize<Datapoint<float>>(v1);
            //Log.Debug($"MachineLearning|TEST", $"Round-trip checks... v1 serializes to {v1s}.");
            //var v1d = Serializer.Deserialize<Datapoint<float>>(v1s);
            //Log.Debug($"MachineLearning|TEST", $"Round-trip checks... v1s deserializes to {v1d}.");
            //Log.Debug($"MachineLearning|TEST", $"Round-trip checks... v1 {Serializer.Check(v1)}.");

        }

        protected override async void OnResume()
        {
            await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: false);
            ButtonStates.Update(this);
            // Attempting to prevent the dataset name field acquiring focus initially - we'll see if it works.
            Task.Delay(100).ContinueWith((t) => { _datasetNameField.Enabled = true; }).LaunchAsOrphan();
        }

        protected override void OnPause()
        {
            //_datasetNameField.Enabled = false; // Meaningful?  Not sure.  Probably not, but also harmless.
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
                    if (CuePrompter?.ListeningForFalseStart ?? false)
                    {
                        CuePrompter.DoOnFalseStart();
                        return true;
                    }
                    else if (_listeningForGesture)
                    {
                        _collectingData = true;
                        //Log.Debug("MachineLearning", $"{keyCode} down");

                        AssertRecognition();
                        if (!_advancedCueMode)
                        {
                            CuePrompter?.MarkGestureStart();
                            CurrentGestureStage.Activate();
                        }
                        return true; // Handled it, thanks.
                    }
                }
                if (_collectingData) return true; // While we're collecting, don't treat it as repeated presses
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

                        if (!_advancedCueMode)
                        {
                            // Halt the gesture-collection stage and query it.
                            var resultData = CurrentGestureStage.StopAndReturnResults();
                            var resultSeq = new Sequence<DKS>() { SourcePath = resultData };
                            resultSeq.Metadata = CurrentGestureStage.GetMetadata();
                            ResolveEndOfGesture(resultSeq);
                        }
                        else // Advanced Cue Mode - where releasing the button is NOT the end of the gesture... merely the start of the cue timer.
                        {
                            GimmeCue(true).LaunchAsOrphan("Cue");
                        }
                        return true; // Handled it, thanks.  
                    }
                }
            }
            return base.OnKeyUp(keyCode, e);
        }

        protected void ResolveEndOfGesture(Sequence<DKS> resultSequence)
        {
            var SelectedGestureClassIndex = SelectedGestureClass.index;

            MostRecentSample = resultSequence;
            if (TeachOnlyMode) MostRecentSample.TrueClassIndex = SelectedGestureClassIndex;

            //if (TeachOnlyMode) Dataset.AddSequence(MostRecentSample);
            Dataset?.AddSequence(MostRecentSample);

            //CurrentStage.Deactivate();
            //CurrentStage = new MachineLearningStage<DKS>($"Learning gesture {SelectedGestureClass.className}#{SelectedGestureClass.numExamples + SelectedGestureClass.numNewExamples}", Dataset);
            string StageLabel = (TeachOnlyMode)
                ? $"Learning gesture {SelectedGestureClass.className}#{SelectedGestureClass.numExamples + SelectedGestureClass.numNewExamples}"
                : $"Reading gesture (#{Dataset.SequenceCount + 1})";
            if (!_advancedCueMode) ResetStage(StageLabel);

            //var vNormal = ((MachineLearningStage)CurrentStage).

            //FindViewById(Resource.Id.mlrn_latest_sample_display).Visibility = ViewStates.Visible;
            _collectingData = false;

            //Task.Run(() =>
            //{
            MostRecentSample = Analyze(MostRecentSample).Result;

            // If right or wrong, tweak the display properties of the sample.  This may depend on TeachMode.
            DisplaySampleInfo(MostRecentSample);
            if (_guessandteach.Checked && MostRecentSample.RecognizedAsIndex >= 0)
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
            else if (_cuemode.Checked)
            {
                CuePrompter?.ReactToFinalizedGesture(MostRecentSample);
            }
            //});

            _listView.Adapter = _listAdapter;
            _listView.RequestLayout();
            _latestSampleDisplay.RequestLayout();
            ButtonStates.Update(this);
        }

        protected virtual void SetUpButtonClicks()
        {
            if (_loadDatasetButton != null) _loadDatasetButton.Click += async (o, e) =>
            {
                SimpleFileDialog fileDialog = new SimpleFileDialog(this, SimpleFileDialog.FileSelectionMode.FileOpen);
                filepath = await fileDialog.GetFileOrDirectoryAsync(this.GetExternalFilesDir(null).ToString());
                if (!String.IsNullOrEmpty(filepath))
                {
                    #region Load Dataset
                    if (filepath.EndsWith(DataSet.FileExtension))
                    {
                        //DataSet<DKS> oldDataset = (DataSet<DKS>)Dataset.Clone(); // TODO - implement using Serialize/Deserialize.
                        DataSet<DKS> oldDataset = Serializer.Deserialize<DataSet<DKS>>(
                                                Serializer.Serialize<DataSet<DKS>>(Dataset) ); // Because Clone() wasn't working.
                        using (var streamReader = new StreamReader(filepath))
                        {
                            var contents = streamReader.ReadToEnd();
                            Log.Debug("Loading dataset", $"Loading our dataset, it currently contains({contents.Length} chars): \n\n{contents}\n");
                            try
                            {
                                Dataset = Serializer.Deserialize<DataSet<DKS>>(contents);
                                if (Dataset == null) throw new Exception($"Deserialization failed - filename {filepath}");
                            }
                            catch (Exception ex)
                            {
                                Log.Debug("MachineLearning|Load Dataset", ex.ToString());
                                AskToDelete("Failure to load dataset; this may be due to a versioning issue.  Delete unloadable file?", filepath);
                                Dataset = new DataSet<DKS>();
                                return;
                            }

                        }

                        // If we're loading a dataset that MATCHES the one we already have, then assume we want to load the old data AND keep the current set.
                        if (Dataset.Name == oldDataset.Name
                            && Dataset.ClassNames.ToString() == oldDataset.ClassNames.ToString()
                            && Dataset.SequenceCount > 0 && oldDataset.SequenceCount > 0)
                        {
                            Toast.MakeText(this, "Merging loaded data with current...", ToastLength.Short).Show();
                            int currentSelectedGestureClass = SelectedGestureClass.index;

                            // Put them both into a form which we can compare (turns out Union() doesn't work even if you implement IEquality, not worth pursuing further).
                            var oldSeqs = oldDataset.Samples.Select(seq => seq.SourcePath.ToString()).ToList();
                            var newSeqs = Dataset.Samples.Select(seq => seq.SourcePath.ToString()).ToList();

                            // The new set should get added onto the end of the old set, so as to preserve order (not that it actually matters at the moment).
                            for (int i = 0; i < Dataset.Samples.Count; i++)
                            {
                                if (!oldSeqs.Contains(newSeqs[i])) oldDataset.AddSequence(Dataset.Samples[i]);
                            }
                            // Now we have no more use for the 'new' set, and it can go pfft in favour of the augmented 'old' set.
                            Dataset = oldDataset;
                            SelectedGestureClass = Dataset.Classes[currentSelectedGestureClass];
                        }
                        else
                        {
                            SelectedGestureClass = Dataset.Classes?.FirstOrDefault();
                        }

                        SetUpAdapters(Dataset);

                        //// Co-deserialization possibilities... are we working with a deserialized (already-loaded) Classifier?
                        //string FeedbackString;
                        //if (Classifier?.MatchingDatasetName != null)
                        //{
                        //    // If so, does it match the dataset we just loaded?
                        //    if (Classifier.MatchingDatasetName == Dataset.Name 
                        //        && Classifier.MatchingDatasetClasses.ToString() == Dataset.ClassNames?.ToArray().ToString())
                        //    {
                        //        // Okay... now which is (roughly) more up-to-date, this dataset, or the classifier?
                        //        if (Dataset.SequenceCount > Classifier.MatchingDatasetSequenceCount)
                        //        {
                        //            // Fine, no problem, next time we recompute the extra data will get woven in.
                        //            FeedbackString = "Dataset is more recent than classifier; recompute recommended.";
                        //        }
                        //        else if (Dataset.SequenceCount == Classifier.MatchingDatasetSequenceCount)
                        //        {
                        //            FeedbackString = null;
                        //        }
                        //        else
                        //        {
                        //            // This one's potentially a problem, though...
                        //            FeedbackString = "Caution - classifier was saved with more data than is in this dataset.  Recomputing may lose progress.";
                        //        }
                        //    }
                        //    // If not, then the dataset we just loaded is just plain different, silently oust the existing Classifier - this is just plain new data.
                        //    else
                        //    {
                        //        FeedbackString = null;
                        //        Classifier = new Classifier();
                        //    }
                        //}
                        //else // It's not a loaded Classifier, so we're clearly overwriting it, no problem.
                        //{
                        //    FeedbackString = null;
                        //    Classifier = new Classifier();
                        //}

                        //if (FeedbackString != null)
                        //    Toast.MakeText(this, FeedbackString, ToastLength.Short).Show();
                    }
                    #endregion
                    #region Load Classifier
                    else if (filepath.EndsWith(Classifier.FileExtension))
                    {
                        using (var streamReader = new StreamReader(filepath))
                        {
                            var contents = streamReader.ReadToEnd();
                            Log.Debug("Loading classifier", $"Loading our classifier, it currently contains: \n\n{contents}\n");
                            try
                            {
                                //Classifier = Serializer.Deserialize<ClusterClassifier>(contents) ?? Serializer.Deserialize<Classifier>(contents);
                                var cTree = Serializer.Deserialize<ClassifierTree>(contents);
                                if (cTree == null) throw new Exception($"Classifier deserialization failed - filename {filepath}");
                                Classifier = cTree.MainClassifier;
                                CueClassifiers = cTree.CueClassifiers;
                                Dataset = new DataSet<DKS> { Name = Classifier.MatchingDatasetName };
                                foreach (var gC in cTree.GestureClasses) Dataset.AddClass(gC);
                                SelectedGestureClass = Dataset.Classes.FirstOrDefault();
                            }
                            catch (Exception ex)
                            {
                                Log.Debug("MachineLearning|Load Classifier", ex.ToString());
                                Classifier = new Classifier();
                                AskToDelete("Failure to load classifier; this may be due to a versioning issue.  Delete unloadable file?", filepath);
                                return;
                            }
                        }

                        // Simpler variant of co-deserialization, for now...
                        if (Dataset == null || Dataset.SequenceCount == 0)
                        {
                            Dataset = Dataset ?? new DataSet<DKS>();
                            Dataset.Name = Classifier.MatchingDatasetName;
                            foreach (var className in Classifier.MatchingDatasetClasses) Dataset.AddClass(className);
                            SelectedGestureClass = Dataset.Classes.FirstOrDefault();
                        }

                        //// Co-deserialization possibilities... are we working with a trivial (essentially empty) dataset?
                        //string FeedbackString;
                        //if (Dataset == null || Dataset.Classes.Count == 0)
                        //{
                        //    FeedbackString = "Note - classifier loaded without its matching data.  If you do not load the dataset as well, further teaching here will 'forget' all the unloaded data.";
                        //}
                        //// No?  Okay.  Is the nontrivial dataset one that matches the classifier we just loaded?
                        //else if (Dataset.ClassNames.ToArray().ToString() == Classifier.MatchingDatasetClasses.ToString()
                        //        && Dataset.Name == Classifier.MatchingDatasetName)
                        //{
                        //    // Okay... now which is (roughly) more up-to-date, this classifier, or the dataset?
                        //    if (Dataset.SequenceCount > Classifier.MatchingDatasetSequenceCount)
                        //    {
                        //        // Fine, no problem, next time we recompute the extra data will get woven in.
                        //        FeedbackString = "Dataset is more recent than classifier; recompute recommended.";
                        //    }
                        //    else if (Dataset.SequenceCount == Classifier.MatchingDatasetSequenceCount)
                        //    {
                        //        FeedbackString = null;
                        //    }
                        //    else // The dataset is less-trained than the classifier... either it's an empty template that matches only in theory, or it includes independent progress which will conflict or be lost.
                        //    {
                        //        if (Dataset.SequenceCount == 0)
                        //        {
                        //            FeedbackString = "Note - classifier loaded without its matching data.  If you do not load the dataset as well, further teaching here will 'forget' all the unloaded data.";
                        //        }
                        //        else
                        //        {
                        //            FeedbackString = "Note - Classifier was created with /different/ data than you have here.  Recommend loading the dataset - you will not lose your current data in doing so.";
                        //        }
                        //    }
                        //}
                        //// The name or class list don't match, so the classifier we just loaded is just plain different.  Refuse to throw out the existing data.  (They can always Clear to make it possible.)
                        //else
                        //{
                        //    FeedbackString = "No way!  This classifier doesn't match your current data at all.  To load it, first use the Clear button to confirm that you want to throw all this out.";
                        //    Classifier = new Classifier();
                        //}

                        //if (FeedbackString != null)
                        //    Toast.MakeText(this, FeedbackString, ToastLength.Short).Show();
                    }
                    #endregion
                }
                //await Task.CompletedTask; // Oh, shut up, warning message.
                //if (DataSet<DKS>.DatasetIndex.Count(s => s != Dataset.Name) == 0)
                //    { Toast.MakeText(this, "Nothing to load!", ToastLength.Short).Show(); return; }
                //Log.Debug("LoadDataset", $"Datasets currently in storage: {DataSet<DKS>.DatasetIndex.Join()}.");
                //var loadDialogBuilder = createDataSetChooserDialog();
                //var loadDialog = loadDialogBuilder.Create();
                //loadDialog.Show();

                ButtonStates.Update(this);
                if (Dataset.SequenceCount > 0) ButtonStates.Load.State = ButtonStates.JustLoaded;
                _listView.RequestLayout();
            };

            if (_saveDatasetButton != null) _saveDatasetButton.Click += async (o, e) =>
            {
                if (ButtonStates.Save.State != ButtonStates.CanSaveClassifier)
                {
                    SimpleFileDialog fileDialog = new SimpleFileDialog(this, SimpleFileDialog.FileSelectionMode.FileSave);
                    fileDialog.DefaultFileName = Dataset.Name + "." + DataSet.FileExtension;
                    filepath = await fileDialog.GetFileOrDirectoryAsync(this.GetExternalFilesDir(null).ToString());
                    if (!String.IsNullOrEmpty(filepath))
                    {
                        using (var streamWriter = new StreamWriter(filepath))
                        {
                            Dataset.SavedAsName = filepath;
                            var serialForm = Serializer.Serialize<DataSet>(Dataset as DataSet);
                            Log.Debug("Saving dataset", $"Saving our dataset, it currently contains ({serialForm.Length} chars): \n\n{serialForm}\n");
                            //Log.Debug("Saving dataset", $"Checking serialization: MostRecentSample {((Serializer.Check(MostRecentSample)) ? "Check" : "Nope")}.");
                            streamWriter.Write(serialForm);

                            var ClipboardMgr = (ClipboardManager)GetSystemService(Service.ClipboardService);
                            var myClip = ClipData.NewPlainText("AtroposSavedDataset", serialForm);
                            ClipboardMgr.PrimaryClip = myClip;
                            Toast.MakeText(this, "Dataset compressed and saved to clipboard.", ToastLength.Short).Show();
                        }
                    }

                    //Dataset.Save();
                    ButtonStates.Update(this);
                    ButtonStates.Save.State = (Classifier != null && Classifier.MachineOnline) ?
                            ButtonStates.CanSaveClassifier :
                            ButtonStates.JustSaved; 
                }
                else // Special case - they JUST saved the dataset using the above functionality, /and/ have a classifier they could save too, /and/ hit the button to do so.
                {
                    // Reuse the exact same name and directory, except for the extension.
                    var fpPieces = filepath.Split('.');
                    fpPieces[fpPieces.Length - 1] = Classifier.FileExtension;
                    filepath = fpPieces.Join(".", "");

                    if (!String.IsNullOrEmpty(filepath))
                    {
                        // Exception: if the name of the Dataset is exactly "Melee" then use the asset file instead...
                        Stream assetStream = null;
                        //if (Dataset.Name == "Melee")
                        //{
                        //    assetStream = this.Assets.Open("MeleeClassifier.txt");
                        //}

                        using (var streamWriter = (assetStream != null) ? new StreamWriter(assetStream) : new StreamWriter(filepath))
                        {
                            //var newDataSet = Dataset.Clone();
                            //newDataSet.Samples.Clear(); // Take out the actual sample data, to reduce the footprint of the Classifier-only dataset
                            //var serialForm = Serializer.Serialize<DataSet>(newDataSet);

                            //var serialForm = (Classifier is ClusterClassifier cc) ? Serializer.Serialize(cc) : Serializer.Serialize(Classifier); 

                            var cTree = new ClassifierTree(Dataset, Classifier, CueClassifiers);
                            var serialForm = Serializer.Serialize(cTree);
                            Log.Debug("Saving classifier", $"Saving our classifier structure, it currently contains: \n\n{serialForm}\n");
                            streamWriter.Write(serialForm);

                            var ClipboardMgr = (ClipboardManager)GetSystemService(Service.ClipboardService);
                            try
                            {
                                var myClip = ClipData.NewPlainText("AtroposSavedClassifier", serialForm);
                                ClipboardMgr.PrimaryClip = myClip;
                                Toast.MakeText(this, "Classifier compressed and saved to clipboard.", ToastLength.Short); 
                            }
                            catch
                            {
                                Toast.MakeText(this, "Error copying classifier to clipboard - presumably too long for it.", ToastLength.Short);
                            }
                        }
                    }

                    ButtonStates.Update(this);
                    ButtonStates.Save.State = ButtonStates.JustSaved;
                }
            };

            if (_clearDatasetButton != null) _clearDatasetButton.Click += (o, e) =>
            {
                if (ButtonStates.Clear.State == ButtonStates.CanClear)
                {
                    Dataset.Clear();
                    SetUpAdapters(Dataset);
                    SelectedGestureClass = GestureClass.NullGesture;
                    _listView.RequestLayout();
                    MostRecentSample = null;
                    Classifier = new Classifier();
                }
                else if (ButtonStates.Clear.State == ButtonStates.CanClearNewData)
                {
                    MostRecentSample = null;
                    foreach (var seq in Dataset.Samples.ToList()) // ToList() causes a copy to be made.
                    {
                        if (!seq.HasContributedToClassifier) Dataset.Samples.Remove(seq);
                    }
                    Dataset.TallySequences();
                }
                else if (ButtonStates.Clear.State == ButtonStates.IsClearedExceptName)
                {
                    if (!String.IsNullOrEmpty(Dataset.SavedAsName))
                    {
                        (new File(Dataset.SavedAsName)).Delete();
                        Toast.MakeText(this, $"Saved dataset <{Dataset.SavedAsName}> deleted.", ToastLength.Short).Show();
                    }
                    //Toast.MakeText(this, "In future, this will delete the dataset file.  Right now, nothing happened.", ToastLength.Short).Show();
                    Dataset.Name = null;
                }
                ButtonStates.Update(this);
            };

            if (_computeButton != null) _computeButton.Click += async (o, e) =>
            {
                //var sw1 = new System.Diagnostics.Stopwatch();

                if (ButtonStates.Compute.State != ButtonStates.DoFullReassess)
                {
                    AssertRecognition();
                    Classifier = Classifier ?? new Classifier(); // Not sure if this is needed or not - have waffled on whether a null Classifier is a legit state or not.

                    // This is the heavy thinking part...
                    ButtonStates.Compute.State = ButtonStates.IsComputing;
                    ShowProgressIndicator("Calculating", "Please hold for the next available AI...");
                    //await Task.Run(() => Classifier.CreateMachine(Dataset));
                    Classifier = await ClassifierSelection.FindBestClassifier(Dataset);

                    //var subDataset = new DataSet<DKS>();
                    //foreach (var gc in Dataset.ActualGestureClasses) subDataset.AddClass(gc);
                    //subDataset.TallySequences();
                    //Dataset.Samples.Shuffle();
                    //foreach (var seq in Dataset.Samples) if (subDataset.MinSamplesInAnyClass() < 3 || Res.Random < 0.25) subDataset.AddSequence(seq);
                    //subDataset.TallySequences();
                    //sw1.Start();
                    //Classifier.CreateMachine(subDataset);
                    //sw1.Stop();
                    //var t1 = sw1.Elapsed.TotalMilliseconds;
                    //Log.Debug("MachineLearning|Calculate", $"Classifier for sub-dataset consisting of {subDataset.Samples.Count} samples ({Dataset.ActualGestureClasses.Count} classes) generated in {sw1.Elapsed.TotalMilliseconds} ms.");
                    //sw1.Restart();

                    //Classifier.CreateMachine(Dataset);

                    //sw1.Stop();
                    //Log.Debug("MachineLearning|Calculate", $"Classifier for dataset consisting of {Dataset.Samples.Count} samples ({Dataset.ActualGestureClasses.Count} classes) generated in {sw1.Elapsed.TotalMilliseconds} ms ({(sw1.Elapsed.TotalMilliseconds / t1):2f}x as long).");

                    DismissProgressIndicator();
                    ShowProgressIndicator("Calculating", "Reassessing data set using new AI...");
                    ButtonStates.Compute.State = ButtonStates.IsReassessing;
                    int minSamples = Dataset.MinSamplesInAnyClass();
                    double percentage = (minSamples < 20) ? 100.0 :
                                        (minSamples < 40) ? 50.0 :
                                        (minSamples < 80) ? 25.0 :
                                                            10.0;
                    //double percentage = 100.0;
                    //double percentage = 25.0;
                    //sw1.Restart();
                    await Classifier.Assess(Dataset, percentage);
                    //sw1.Stop();
                    //Log.Debug("MachineLearning|Calculate", $"25% of samples (i.e. {Dataset.Samples.Count * 0.25}) assessed in {sw1.Elapsed.TotalMilliseconds} ms.");

                    Dataset.TallySequences(true);
                    DismissProgressIndicator();
                    ButtonStates.Update(this);
                    if (percentage < 100) ButtonStates.Compute.State = ButtonStates.DoFullReassess;
                }
                else // Available only immediately following a click of the Compute button as per above; signals a desire to run a *full* reassess
                {
                    ShowProgressIndicator("Calculating", "Reassessing all data using new AI...");

                    //sw1.Start();
                    await Task.Run(() => Classifier.Assess(Dataset, 100.0));
                    //sw1.Stop();
                    //Log.Debug("MachineLearning|Calculate", $"100% of samples (i.e. {Dataset.Samples.Count}) assessed in {sw1.Elapsed.TotalMilliseconds} ms.");

                    Dataset.TallySequences(true);
                    DismissProgressIndicator();
                    ButtonStates.Update(this);
                }
            };

            // Special debugging test operation
            if (_computeSingleButton != null) _computeSingleButton.Click += async (o, e) =>
            {
                //foreach (var currentGC in Dataset.ActualGestureClasses)
                //{
                //    currentGC.CueClassifier = await ClassifierSelection.FindBestClassifier(Dataset, currentGC);
                //}
                CueClassifiers.Add(SelectedGestureClass, await ClassifierSelection.FindBestClassifier(Dataset, SelectedGestureClass));
            };

            if (_addNewClassButton != null) _addNewClassButton.Click +=
                (o, e) =>
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

                        Dataset.AddClass(newName);
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

                    ButtonStates.Update(this);
                    string identifier = (TeachOnlyMode) ?
                        $"{SelectedGestureClass.className}#{SelectedGestureClass.numExamples + SelectedGestureClass.numNewExamples}" :
                        $"Unknown Sequence #{Dataset.SequenceCount}";
                    ResetStage($"Learning gesture {identifier}");

                    _newClassNameField.Text = null;
                    _newClassNameField.ClearFocus();
                    this.HideKeyboard();
                };

            if (_newClassNameField != null)
            {
                _newClassNameField.KeyPress += (object sender, View.KeyEventArgs e) =>
                {
                    e.Handled = false;
                    if (e.Event.Action == KeyEventActions.Down && e.KeyCode == Keycode.Enter && _newClassNameField.Text.Length > 0)
                    {
                        _addNewClassButton.CallOnClick();
                    }
                };

                _newClassNameField.TextChanged += (object sender, Android.Text.TextChangedEventArgs e) =>
                {
                    _addNewClassButton.Text = "Add";
                    _addNewClassButton.Enabled = (_newClassNameField.Text.Length > 0);
                };
            }

            if (_datasetNameField != null)
            {
                _datasetNameField.TextChanged += (object sender, Android.Text.TextChangedEventArgs e) =>
                    {
                        if (_datasetNameField.Text.Length > 0) Dataset.Name = e.Text.ToString();
                        else Dataset.Name = null;
                    };

                _datasetNameField.KeyPress += (object sender, View.KeyEventArgs e) =>
                {
                    e.Handled = false;
                    if (e.Event.Action == KeyEventActions.Down && e.KeyCode == Keycode.Enter && _datasetNameField.Text.Length > 0)
                    {
                        _newClassNameField.ClearFocus();
                    }
                }; 
            }

            if (_gimmeCueButton != null) _gimmeCueButton.Click += async (o, e) =>
            {
                if (_advancedCueMode) return; // Does nothing in this mode - click volume button(s) instead.
                else await GimmeCue();
            };

            if (_discardSampleBtn != null) _discardSampleBtn.Click += (o, e) =>
            {
                MostRecentSample = null;
                Dataset?.RemoveSequence();
                if (Dataset == null || Dataset.Samples.Count == 0) return;
                MostRecentSample = Dataset.Samples.Last();
                DisplaySampleInfo(MostRecentSample);
            };

            if (_guessandteach != null) _guessandteach.CheckedChange += (o, e) => ButtonStates.Update(this);
            if (_teachonly != null) _teachonly.CheckedChange += (o, e) => ButtonStates.Update(this);
            if (_cuemode != null) _cuemode.CheckedChange += (o, e) => ButtonStates.Update(this);

            if (_listView != null) _listView.ItemClick += OnListItemClick;
        }

        public async Task GimmeCue(bool advancedMode = false)
        {
            if (!advancedMode)
            {
                CuePrompter = new MlrnCuePrompter<DKS>(this);
                await CuePrompter.WaitBeforeCue();
                CuePrompter.ProvideCue();
                return;
            }

            else // Advanced mode
            {
                SelectedGestureClass = Dataset.Classes.GetRandom();
                //CurrentStage = new SelfEndpointingSingleGestureRecognizer($"Cueing {targetClass?.className}", Classifier,
                //        new ClusterLoggingProvider<Vector3, Vector3>(SensorType.LinearAcceleration, SensorType.Gravity));

                AlternativeResetStage($"Cueing {SelectedGestureClass?.className}");

                CuePrompter = new MlrnCuePrompter<DKS>(this);
                await CuePrompter.WaitBeforeCue();
                CuePrompter.ProvideCue(SelectedGestureClass);
                //CurrentGestureStage.Activate(); // Is included in "RunUntilFound()" below.

                //// Retrieve "this is where we are now, as the cue is given" info from the current stage
                //var CurrentProvider = ((CurrentStage as MachineLearningStage).DataProvider as ClusterLoggingProvider<Vector3, Vector3>);
                //var PromptStartTimeStamp = CurrentProvider.Timestamp;
                //var PromptStartIndex = CurrentProvider.LoggedData.Count - 1;
                var PromptStartTimeStamp = TimeSpan.Zero;

                await Task.Run(async () =>
                {
                    // Now start the gesture recognition stage and run until it comes back saying it's got one.
                    var resultSeq = await ((SelfEndpointingSingleGestureRecognizer)CurrentGestureStage).RunUntilFound(SelectedGestureClass);

                    //// To work out the "promptness" component of their score, we need to see where the best fit *beginning* of their gesture was.
                    //var reversePeakFinder = new PeakFinder<DKS>.IncrementingStartIndexVariant<DKS>(
                    //    resultSeq.SourcePath.ToList(),
                    //    (seq) =>
                    //    {
                    //        var Seq = new Sequence<DKS>() { SourcePath = seq.ToArray() };
                    //        var analyzedSeq = Current.Analyze(Seq).Result;

                    //        if (SelectedGestureClass != null && Seq.RecognizedAsIndex != SelectedGestureClass.index) return double.NaN;
                    //        else return analyzedSeq.RecognitionScore;
                    //    }, thresholdScore: 1.5, minLength: Dataset?.MinSequenceLength ?? 5); // Might need to tweak this depending on how the parsing of the stuff works out.

                    //resultSeq = new Sequence<DKS>() { SourcePath = reversePeakFinder.FindBestSequence().ToArray() };
                    ////MostRecentSample = Current.Analyze(resultSeq).Result;

                    CuePrompter.TimeElapsed = (CurrentGestureStage as MachineLearningStage).RunTime - PromptStartTimeStamp;
                    ResolveEndOfGesture(resultSeq);

                });
                //Dataset.AddSequence(MostRecentSample); // All of this got incorporated into ResolveEndOfGesture().
                //DisplaySampleInfo(MostRecentSample);
                //_listView.Adapter = _listAdapter;
                //_listView.RequestLayout();
                //_latestSampleDisplay.RequestLayout();
            }
        }

        protected void AskToDelete(string prompt, string filepath)
        {
            new AlertDialog.Builder(this)
                .SetMessage(prompt)
                .SetPositiveButton("Yes", (o, e) =>
                {
                    (new File(filepath)).Delete();
                    Toast.MakeText(this, "Deleted.", ToastLength.Short).Show();
                })
                .SetNegativeButton("No", (o, e) => { })
                .Show();
        }
        private int lastClassnameSpinnerSelection = -1;
        protected void OnClassnameSpinnerChanged(object sender, AdapterView.ItemSelectedEventArgs e)
        {
            if (MostRecentSample == null) return;
            if (SpinnerChangeIsSilent) { SpinnerChangeIsSilent = false; return; }

            //bool itChanged = (_teachonly.Checked) 
            //    ? (MostRecentSample.TrueClassIndex != e.Position)
            //    : (MostRecentSample.RecognizedAsIndex != e.Position);
            bool itChanged = (e.Position != lastClassnameSpinnerSelection);
            lastClassnameSpinnerSelection = e.Position;
            MostRecentSample.TrueClassIndex = e.Position;
            Dataset.TallySequences();
            DisplaySampleInfo(MostRecentSample);
            ButtonStates.Update(this);
            if (_guessandteach.Checked && itChanged) Speech.Say($"Corrected to {MostRecentSample.TrueClassName}");
        }

        protected bool SpinnerChangeIsSilent = false;
        protected void SilentlySetClassnameSpinner(int position)
        {
            if (position < 0) return;
            SpinnerChangeIsSilent = true;
            Task.Delay(5).ContinueWith(_ => RunOnUiThread(() => _classnameSpinner.SetSelection(position, true)));
            //_classnameSpinner.Adapter = _classnameSpinnerAdapter;
            _latestSampleDisplay.RequestLayout();
            //ButtonStates.Update(this);
        }

        public void OnListItemClick(object sender, Android.Widget.AdapterView.ItemClickEventArgs e)
        {
            var listView = sender as ListView;
            AssertRecognition();
            SelectedGestureClass = Dataset.Classes[e.Position];
            SetUpAdapters(Dataset);
            //foreach (var i in Enumerable.Range(0, _listAdapter.Count))
            //{
            //    var gV = _listView.GetChildAt(i);
            //    var gVi = gV.FindViewById<ImageView>(Resource.Id.mlrn_gesture_icon);
            //    gVi.Alpha = (i == e.Position) ? 1.0f : 0.25f;
            //}
            //Toast.MakeText(this, $"Selected {SelectedGestureClass.className}.", ToastLength.Short).Show();

            // Allow (briefly, only if done immediately after selecting the gesture class from the list)
            if (String.IsNullOrEmpty(_newClassNameField.Text))
            {
                _addNewClassButton.Text = "Remove";
                _addNewClassButton.Enabled = true;
            }
        }

        public void DismissProgressIndicator()
        {
            //if (_progressDialog != null && _progressDialog.IsShowing)
            //    _progressDialog.Dismiss();
        }

        public void ShowProgressIndicator(string title, string message)
        {
            //_progressDialog = ProgressDialog.Show(this, title, message, true, true);
        }
        #endregion

        public override async Task<Sequence<DKS>> Analyze(Sequence<DKS> sequence)
        {
            if (Classifier != null && Classifier.MachineOnline)
            {
                sequence.RecognizedAsIndex = await Classifier.Recognize(sequence);
                //sequence.TrueClassIndex = SelectedGestureClass.index;
            }
            return sequence;
        }

        public override void SetUpAdapters(IDataset dataSet)
        {
            SpinnerChangeIsSilent = true;

            _classnameSpinnerAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem);
            _classnameSpinnerAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);

            _classnameSpinnerAdapter.AddAll(dataSet.ClassNames);
            _classnameSpinner.Adapter = _classnameSpinnerAdapter;

            _listAdapter = new GestureClassListAdapter<DKS>(this, dataSet);
            _listView.Adapter = _listAdapter;
            _listView.DisableScrolling();
        }

        public virtual void DisplaySampleInfo(ISequence sequence)
        {
            RunOnUiThread(() =>
            {
                if (sequence == null || (sequence as Sequence<DKS>).SourcePath.Length < 3) return;

                _sampleVisualization.SetImageBitmap(sequence.Bitmap);
                bool SubmitButtonPermitted = true;

                // Contents of the text field, and selected item in the spinner, both depend on a number of settings & factors.

                // (A) Which mode are we in?
                if (_teachonly.Checked)
                {
                    SilentlySetClassnameSpinner(sequence.TrueClassIndex); // Does nothing if index < 0 (aka "I dunno"), although how that'd happen is unclear.

                    // (B) Did we run it through an existing classifier?
                    if (sequence.RecognizedAsIndex >= 0)
                    {
                        // (C) Did we get it right?
                        if (sequence.TrueClassIndex == sequence.RecognizedAsIndex)
                        {
                            _guessField.Text = $"Correctly read as {sequence.TrueClassName}";
                            _guessField.SetTextColor(Android.Graphics.Color.Green);
                        }
                        // (C') We got it wrong, alas.
                        else
                        {
                            _guessField.Text = $"{sequence.TrueClassName} (Read as {sequence.RecognizedAsName})";
                            _guessField.SetTextColor(Android.Graphics.Color.Red);
                        }
                    }
                    // (B') Nope, we didn't recognize it (probably because we haven't trained a classifier yet)
                    else
                    {
                        _guessField.Text = $"Recorded as {sequence.TrueClassName}";
                        _guessField.SetTextColor(Android.Graphics.Color.Gray);
                    }
                }
                // (A') We're in guess-and-teach mode
                else if (_guessandteach.Checked)
                {
                    // (B) Did we recognize it as anything at all?
                    if (sequence.RecognizedAsIndex >= 0)
                    {
                        // (C) Have we not told it yet what it really is?
                        if (sequence.TrueClassIndex < 0)
                        {
                            SilentlySetClassnameSpinner(sequence.RecognizedAsIndex);
                            _guessField.Text = $"Is that a {sequence.RecognizedAsName}?";
                            _guessField.SetTextColor(Android.Graphics.Color.Blue);
                        }
                        // (C') Okay, we've set the spinner (or used another input method like the volume button & AssertRecognition) and thus told it what it is.
                        else
                        {
                            SilentlySetClassnameSpinner(sequence.TrueClassIndex);
                            // (D) Did our guess get it right?  Yup, awesome!
                            if (sequence.RecognizedAsIndex == sequence.TrueClassIndex)
                            {
                                _guessField.Text = $"Correctly read as {sequence.TrueClassName}";
                                _guessField.SetTextColor(Android.Graphics.Color.Green);
                            }
                            // (D') Nope, and we've corrected it with the spinner.
                            else
                            {
                                _guessField.Text = $"Recorded as {sequence.TrueClassName} (not {sequence.RecognizedAsName})";
                                _guessField.SetTextColor(Android.Graphics.Color.Magenta);
                            }
                        }
                    }
                    // (B') Nope, we didn't recognize it - which probably means we REALLY screwed it up.
                    else
                    {
                        _guessField.Text = $"Gesture not recognized";
                        _guessField.SetTextColor(Android.Graphics.Color.Gray);
                        SubmitButtonPermitted = false;
                    }
                }
                else // (A") We're in cued mode
                {
                    SilentlySetClassnameSpinner(SelectedGestureClass.index);
                    string formatString;

                    // (B) Did the user perform the *correct* gesture?
                    if (sequence.RecognizedAsIndex != SelectedGestureClass.index)
                    {
                        formatString = $"Not {sequence.RecognizedAsName}, {{0}}!";
                        _guessField.SetTextColor(Android.Graphics.Color.Red);
                    }

                    else if (sequence.RecognitionScore < 1.5)
                    {
                        formatString = "Sloppy {0}";
                        _guessField.SetTextColor(Android.Graphics.Color.Red);
                    }
                    else if (sequence.RecognitionScore < 2.25)
                    {
                        formatString = "Respectable {0}";
                        _guessField.SetTextColor(Android.Graphics.Color.Magenta);
                    }
                    else
                    {
                        formatString = "Excellent {0}";
                        _guessField.SetTextColor(Android.Graphics.Color.Green);
                    }

                    _guessField.Text = String.Format(formatString, SelectedGestureClass.className);
                    SubmitButtonPermitted = false;
                }

                _latestSampleDisplay.Visibility = ViewStates.Visible;
                _discardSampleBtn.Enabled = SubmitButtonPermitted;

                //_listView.Adapter = null;
                //_listView.Adapter = _listAdapter;
                Dataset.TallySequences(); // Note sure if these are still needed or not, given the existence of ButtonStates.Update()... but we'll let it stand in any event.
                _listView.RequestLayout();
            });
        }

        public void AssertRecognition()
        {
            if (MostRecentSample == null) return;

            // Do we have an existing sample which we read, presumably correctly, but haven't actually inscribed as anything officially?
            // Mostly this happens when in Guess & Train mode, if the classifier got it right and we haven't bothered to say anything about it,
            // but we're executing something like selecting a new gesture class which means we need to make that reading official.
            if (MostRecentSample != null && MostRecentSample.TrueClassIndex < 0 && MostRecentSample.RecognizedAsIndex >= 0)
            {
                MostRecentSample.TrueClassIndex = MostRecentSample.RecognizedAsIndex;
                Dataset.TallySequences();
                DisplaySampleInfo(MostRecentSample);
            }
        }
    }

    public class GestureClassListAdapter<DKS> : BaseAdapter<GestureClass>
    {
        private readonly Activity _context;
        private readonly IDataset _dataset;
        private readonly List<GestureClass> _items;

        public GestureClassListAdapter(Activity context, IDataset dataset)
            : base()
        {
            _context = context;
            _dataset = dataset;
            _items = dataset.Classes;
        }

        public override long GetItemId(int position)
        {
            return position;
        }
        public override GestureClass this[int position]
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

            v = v ?? _context.LayoutInflater.Inflate(Resource.Layout.MachineLearning_gestureClassRepresentation, null);

            var IconField = v.FindViewById<ImageView>(Resource.Id.mlrn_gesture_icon);
            var NameField = v.FindViewById<TextView>(Resource.Id.mlrn_gesture_classname);
            var VisualizationField = v.FindViewById<ImageView>(Resource.Id.mlrn_gesture_class_visualization);
            var PercentageField = v.FindViewById<TextView>(Resource.Id.mlrn_gesture_class_percentage_textview);
            var DetailsField = v.FindViewById<TextView>(Resource.Id.mlrn_gesture_class_detail_textview);
            var AddedItemsField = v.FindViewById<TextView>(Resource.Id.mlrn_gesture_class_addeditems_textview);

            GestureClass gC = _items[position];
            if (gC == null) return v;

            var ctx = (MachineLearningPageActivity)_context;
            IconField.Alpha = (ctx.SelectedGestureClass.className == gC.className) ?
                              ((ctx.TeachOnlyMode) ? 1.0f : 0.35f) : 0.25f;
            NameField.Text = gC.className;
            VisualizationField.SetImageBitmap(gC.visualization);
            PercentageField.Text = gC.PercentageText;
            DetailsField.Text = gC.DetailsText;
            AddedItemsField.Text = gC.AddedItemsText;

            return v;
        }
    }
}