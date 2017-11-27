
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
using SimpleFileDialog = com.Atropos.External_Code.SimpleFileDialog;
using System.IO;
using Java.IO;
using File = Java.IO.File;
using System.Threading;
using Nito.AsyncEx;
using PerpetualEngine.Storage;
using MiscUtil;
using com.Atropos.DataStructures;

namespace com.Atropos.Machine_Learning
{
    /// <summary>
    /// Specific implementation - must specify the type argument of the underlying base class,
    /// and then in ResetStage launch a stage with an appropriate <see cref="LoggingSensorProvider{T}"/>.
    /// 
    /// <para>Type argument here is constrained (at present) to one of: Vector2, Vector3, <seealso cref="Vector6"/>.  
    /// See also <seealso cref="Feature{T}"/> to implement any other types.</para>
    /// </summary>
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait, WindowSoftInputMode = SoftInput.AdjustPan)]
    public class MachineLearningActivity : MachineLearningActivity<Datapoint<Vector3, Vector3>>
    {

        protected new static MachineLearningActivity Current { get { return (MachineLearningActivity)CurrentActivity; } set { CurrentActivity = value; } }

        protected override void ResetStage(string label)
        {
            CurrentStage = new MachineLearningStage(label, Dataset, 
                //new SmoothClusterProvider<Vector3, Vector3>(SensorType.LinearAcceleration, SensorType.Gravity)); 
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

    public abstract partial class MachineLearningActivity<T> 
        : BaseActivity_Portrait, MachineLearningActivity<T>.IMachineLearningActivity 
        where T : struct
    {
        protected static MachineLearningActivity<T> Current { get { return (MachineLearningActivity<T>)CurrentActivity; } set { CurrentActivity = value; } }
        public interface IMachineLearningActivity
        {
            GestureClass SelectedGestureClass { get; }
            ISequence MostRecentSample { get; }
        } // Mostly a 'marker' interface since the "typeless" MachineLearningActivity is actually the more derived class, instead of the less derived one.

        #region Data Members (and tightly linked functions / properties)
        public DataSet<T> Dataset
        {
            get
            {
                return DataSet.Current as DataSet<T>;
            }
            set
            {
                DataSet.Current = value as DataSet;
                ButtonStates.Update(this);
            }
        }

        public GestureClass SelectedGestureClass { get; set; } = GestureClass.NullGesture;

        private Sequence<T> _mostRecentSample;
        public ISequence MostRecentSample
        {
            get { return _mostRecentSample; }
            set
            {
                _mostRecentSample = (Sequence<T>)value;
                _latestSampleDisplay.Visibility = (_mostRecentSample != null) ? ViewStates.Visible : ViewStates.Gone;
            }
        }

        public Classifier Classifier { get; set; }

        protected RadioButton _teachonly, _guessandteach;
        protected ImageView _sampleVisualization;
        protected TextView _guessField;
        protected Button _discardSampleBtn, 
            _loadDatasetButton, 
            _saveDatasetButton, 
            _clearDatasetButton, 
            _computeButton,
            _addNewClassButton;
        protected ListView _listView;
        protected GestureClassListAdapter _listAdapter;
        protected Spinner _classnameSpinner;
        protected ArrayAdapter<string> _classnameSpinnerAdapter;
        protected EditText _newClassNameField, _datasetNameField;
        protected LinearLayout _latestSampleDisplay;

        protected string filepath;

        public bool TeachOnlyMode
        {
            get
            {
                if (_teachonly.Checked && !_guessandteach.Checked) return true;
                else if (!_teachonly.Checked && _guessandteach.Checked) return false;
                else throw new Exception("Radiobutton issue!  Neither (or both) checked simultaneously.  Halp!"); // Probably no longer needed but may as well stay.
            }
        }

        protected bool _collectingData = false;
        protected bool _listeningForGesture { get { return SelectedGestureClass != GestureClass.NullGesture
                                                    && !_collectingData; } }

        protected abstract void ResetStage(string label);

        protected void FindAllViews()
        {
            _loadDatasetButton = FindViewById<Button>(Resource.Id.mlrn_dataset_load_btn);
            _saveDatasetButton = FindViewById<Button>(Resource.Id.mlrn_dataset_save_btn);
            _clearDatasetButton = FindViewById<Button>(Resource.Id.mlrn_dataset_clear_btn);
            _computeButton = FindViewById<Button>(Resource.Id.mlrn_study_dataset_btn);

            _teachonly = FindViewById<RadioButton>(Resource.Id.mlrn_trainmode_teachonly);
            _guessandteach = FindViewById<RadioButton>(Resource.Id.mlrn_trainmode_guessandteach);
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

            Dataset = new DataSet<T>();
            Classifier = new Classifier();

            SetUpButtonClicks();
            SetUpAdapters(Dataset);

            ResetStage("Learning gesture");

            _classnameSpinner.ItemSelected += OnClassnameSpinnerChanged;
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
                    if (_listeningForGesture)
                    {
                        _collectingData = true;
                        //Log.Debug("MachineLearning", $"{keyCode} down");

                        AssertRecognition();

                        CurrentStage.Activate();
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

                        // Halt the gesture-collection stage and query it.
                        var featureVectors = ((MachineLearningStage)CurrentStage).StopAndReturnResults();
                        var SelectedGestureClassIndex = SelectedGestureClass.index;

                        MostRecentSample = new Sequence<T>() { SourcePath = featureVectors };
                        if (TeachOnlyMode) MostRecentSample.TrueClassIndex = SelectedGestureClassIndex;

                        //if (TeachOnlyMode) Dataset.AddSequence(MostRecentSample);
                        Dataset.AddSequence(MostRecentSample);

                        //CurrentStage.Deactivate();
                        //CurrentStage = new MachineLearningStage<T>($"Learning gesture {SelectedGestureClass.className}#{SelectedGestureClass.numExamples + SelectedGestureClass.numNewExamples}", Dataset);
                        string StageLabel = (TeachOnlyMode)
                            ? $"Learning gesture {SelectedGestureClass.className}#{SelectedGestureClass.numExamples + SelectedGestureClass.numNewExamples}"
                            : $"Reading gesture (#{Dataset.SequenceCount + 1})";
                        ResetStage(StageLabel);

                        //var vNormal = ((MachineLearningStage)CurrentStage).

                        //FindViewById(Resource.Id.mlrn_latest_sample_display).Visibility = ViewStates.Visible;
                        _collectingData = false;

                        //Task.Run(() =>
                        //{
                        MostRecentSample = Analyze(MostRecentSample).Result;

                        // If right or wrong, tweak the display properties of the sample.  This may depend on TeachMode.
                        DisplaySampleInfo(MostRecentSample);
                        if (!TeachOnlyMode && MostRecentSample.RecognizedAsIndex >= 0) Speech.Say(MostRecentSample.RecognizedAsName);
                        //});

                        _listView.Adapter = _listAdapter;
                        _listView.RequestLayout();
                        _latestSampleDisplay.RequestLayout();
                        ButtonStates.Update(this);

                        return true; // Handled it, thanks. 
                    }
                }
            }
            return base.OnKeyUp(keyCode, e);
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
        protected void SetUpButtonClicks()
        {
            _loadDatasetButton.Click += async (o, e) =>
            {
                SimpleFileDialog fileDialog = new SimpleFileDialog(this, SimpleFileDialog.FileSelectionMode.FileOpen);
                filepath = await fileDialog.GetFileOrDirectoryAsync(this.GetExternalFilesDir(null).ToString());
                if (!String.IsNullOrEmpty(filepath))
                {
                    #region Load Dataset
                    if (filepath.EndsWith(DataSet.FileExtension))
                    {
                        //DataSet<T> oldDataset = (DataSet<T>)Dataset.Clone(); // TODO - implement using Serialize/Deserialize.
                        DataSet<T> oldDataset = Serializer.Deserialize<DataSet<T>>(
                                                Serializer.Serialize<DataSet<T>>(Dataset) ); // Because Clone() wasn't working.
                        using (var streamReader = new StreamReader(filepath))
                        {
                            var contents = streamReader.ReadToEnd();
                            Log.Debug("Loading dataset", $"Loading our dataset, it currently contains: \n\n{contents}\n");
                            try
                            {
                                Dataset = Serializer.Deserialize<DataSet<T>>(contents);
                                if (Dataset == null) throw new Exception($"Deserialization failed - filename {filepath}");
                            }
                            catch (Exception ex)
                            {
                                Log.Debug("MachineLearning|Load Dataset", ex.ToString());
                                AskToDelete("Failure to load dataset; this may be due to a versioning issue.  Delete unloadable file?", filepath);
                                Dataset = new DataSet<T>();
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
                                Classifier = Serializer.Deserialize<Classifier>(contents);
                                if (Classifier == null) throw new Exception($"Classifier deserialization failed - filename {filepath}");
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
                            Dataset = Dataset ?? new DataSet<T>();
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
                //if (DataSet<T>.DatasetIndex.Count(s => s != Dataset.Name) == 0)
                //    { Toast.MakeText(this, "Nothing to load!", ToastLength.Short).Show(); return; }
                //Log.Debug("LoadDataset", $"Datasets currently in storage: {DataSet<T>.DatasetIndex.Join()}.");
                //var loadDialogBuilder = createDataSetChooserDialog();
                //var loadDialog = loadDialogBuilder.Create();
                //loadDialog.Show();

                ButtonStates.Update(this);
                if (Dataset.SequenceCount > 0) ButtonStates.Load.State = ButtonStates.JustLoaded;
                _listView.RequestLayout();
            };

            _saveDatasetButton.Click += async (o, e) =>
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
                            Log.Debug("Saving dataset", $"Saving our dataset, it currently contains: \n\n{serialForm}\n");
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
                        using (var streamWriter = new StreamWriter(filepath))
                        {
                            var serialForm = Serializer.Serialize<Classifier>(Classifier);
                            Log.Debug("Saving classifier", $"Saving our classifier, it currently contains: \n\n{serialForm}\n");
                            streamWriter.Write(serialForm);

                            var ClipboardMgr = (ClipboardManager)GetSystemService(Service.ClipboardService);
                            var myClip = ClipData.NewPlainText("AtroposSavedClassifier", serialForm);
                            ClipboardMgr.PrimaryClip = myClip;
                            Toast.MakeText(this, "Classifier compressed and saved to clipboard.", ToastLength.Short);
                        }
                    }

                    ButtonStates.Update(this);
                    ButtonStates.Save.State = ButtonStates.JustSaved;
                }
            };

            _clearDatasetButton.Click += (o, e) =>
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

            _computeButton.Click += async (o, e) =>
            {
                if (ButtonStates.Compute.State != ButtonStates.DoFullReassess)
                {
                    AssertRecognition();
                    Classifier = Classifier ?? new Classifier(); // Not sure if this is needed or not - have waffled on whether a null Classifier is a legit state or not.

                    // This is the heavy thinking part...
                    ButtonStates.Compute.State = ButtonStates.IsComputing;
                    //await Task.Run(() => Classifier.CreateMachine(Dataset));
                    Classifier.CreateMachine(Dataset);

                    ButtonStates.Compute.State = ButtonStates.IsReassessing;
                    int minSamples = Dataset.MinSamplesInAnyClass();
                    double percentage = (minSamples < 20) ? 100.0 :
                                        (minSamples < 40) ? 50.0 :
                                        (minSamples < 80) ? 25.0 :
                                                            10.0 ;
                    await Classifier.Assess(Dataset, percentage);

                    Dataset.TallySequences(true);
                    ButtonStates.Update(this);
                    if (percentage < 100) ButtonStates.Compute.State = ButtonStates.DoFullReassess;
                }
                else // Available only immediately following a click of the Compute button as per above; signals a desire to run a *full* reassess
                {
                    await Task.Run(() => Classifier.Assess(Dataset, 100.0));

                    Dataset.TallySequences(true);
                    ButtonStates.Update(this);
                }
                
                
                
            };
            
            _addNewClassButton.Click +=
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
                        var currentClassIndex = SelectedGestureClass.index;

                        Dataset.RemoveClass(SelectedGestureClass);

                        if (currentClassIndex < Dataset.Classes.Count - 1) SelectedGestureClass = Dataset.Classes[currentClassIndex];
                        else if (Dataset.Classes.Count > 0) SelectedGestureClass = Dataset.Classes[0];
                        else SelectedGestureClass = null;
                    }

                    ButtonStates.Update(this);
                    string identifier = (TeachOnlyMode) ?
                        $"{SelectedGestureClass.className}#{SelectedGestureClass.numExamples + SelectedGestureClass.numNewExamples}" :
                        $"Unknown Sequence #{Dataset.SequenceCount}";
                    ResetStage($"Learning gesture {identifier}");

                    _newClassNameField.Text = null;
                };

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

            //FindViewById<RadioGroup>(Resource.Id.mlrn_trainmode_radiobuttons).CheckedChange += (o, e) =>
            //    { _discardSampleBtn.Text = (TeachOnlyMode) ? "Cancel" : "Submit"; };

            _discardSampleBtn.Click += _discardSampleBtn_Click;

            _listView.ItemClick += OnListItemClick;
        }

        private void _discardSampleBtn_Click(object sender, EventArgs e)
        {
            MostRecentSample = null;
            Dataset?.RemoveSequence();
        }

        private void OnClassnameSpinnerChanged(object sender, AdapterView.ItemSelectedEventArgs e)
        {
            if (MostRecentSample == null) return;
            if (SpinnerChangeIsSilent) { SpinnerChangeIsSilent = false; return; }

            bool itChanged = (MostRecentSample.TrueClassIndex != e.Position);
            MostRecentSample.TrueClassIndex = e.Position;
            Dataset.TallySequences();
            DisplaySampleInfo(MostRecentSample);
            ButtonStates.Update(this);
            if (!TeachOnlyMode && itChanged) Speech.Say($"Corrected to {MostRecentSample.TrueClassName}");
        }

        private bool SpinnerChangeIsSilent = false;
        private void SilentlySetClassnameSpinner(int position)
        {
            if (position < 0) return;
            //_classnameSpinner.ItemSelected -= OnClassnameSpinnerChanged;
            SpinnerChangeIsSilent = true;
            _classnameSpinner.SetSelection(position);
            //_classnameSpinner.ItemSelected += OnClassnameSpinnerChanged;
            //SpinnerChangeIsSilent = false;
        }

        //private AlertDialog.Builder createDataSetChooserDialog()
        //{
        //    AlertDialog.Builder dialogBuilder = new AlertDialog.Builder(this);
        //    var _dialogTitleView = new TextView(this);
        //    _dialogTitleView.LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);

        //    _dialogTitleView.Gravity = GravityFlags.CenterVertical;
        //    _dialogTitleView.SetBackgroundColor(Android.Graphics.Color.DarkGray);
        //    _dialogTitleView.SetTextColor(Android.Graphics.Color.White);
        //    _dialogTitleView.Text = "Test1";

        //    // Create custom view for AlertDialog title
        //    LinearLayout titleLayout1 = new LinearLayout(this);
        //    titleLayout1.Orientation = Android.Widget.Orientation.Vertical;
        //    titleLayout1.AddView(_dialogTitleView);

        //    /////////////////////////////////////////////////////
        //    // Create View with folder path and entry text box // 
        //    /////////////////////////////////////////////////////
        //    LinearLayout titleLayout = new LinearLayout(this);
        //    titleLayout.Orientation = Android.Widget.Orientation.Vertical;

        //    var m_titleView = new TextView(this);
        //    m_titleView.LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        //    m_titleView.Gravity = GravityFlags.CenterVertical;
        //    m_titleView.SetBackgroundColor(Android.Graphics.Color.DarkGray);
        //    m_titleView.SetTextColor(Android.Graphics.Color.White);
        //    m_titleView.Text = "Test2";

        //    titleLayout.AddView(m_titleView);

        //    //////////////////////////////////////////
        //    // Set Views and Finish Dialog builder  //
        //    //////////////////////////////////////////
        //    dialogBuilder.SetView(titleLayout);
        //    dialogBuilder.SetCustomTitle(titleLayout1);
        //    var m_listAdapter = new ArrayAdapter(this, Android.Resource.Drawable.ListSelectorBackground, DataSet<T>.DatasetIndex);
        //    EventHandler<DialogClickEventArgs> onListItemClick = 
        //        (object sender, DialogClickEventArgs e) => 
        //        {
        //            var selectedSetName = DataSet<T>.DatasetIndex[e.Which];
        //            Dataset = DataSet<T>.Load<T>(selectedSetName);
        //        };
        //    dialogBuilder.SetSingleChoiceItems(m_listAdapter, -1, onListItemClick);
        //    dialogBuilder.SetCancelable(false);
        //    return dialogBuilder;
        //}

        //private void _teachButtons_Click(object sender, EventArgs e)
        //{
        //    _submitOrCancelBtn.Text = (TeachOnlyMode) ? "Cancel" : "Submit";
        //}

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
        #endregion

        public void SetUpAdapters(IDataset dataSet)
        {
            SpinnerChangeIsSilent = true;

            _classnameSpinnerAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem);
            _classnameSpinnerAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);

            _classnameSpinnerAdapter.AddAll(dataSet.ClassNames);
            _classnameSpinner.Adapter = _classnameSpinnerAdapter;

            _listAdapter = new GestureClassListAdapter(this, dataSet);
            _listView.Adapter = _listAdapter;
            _listView.DisableScrolling();
        }

        public void DisplaySampleInfo(ISequence sequence)
        {
            if (sequence == null || (sequence as Sequence<T>).SourcePath.Length < 3) return;

            _sampleVisualization.SetImageBitmap(sequence.Bitmap);
            bool SubmitButtonPermitted = true;

            // Contents of the text field, and selected item in the spinner, both depend on a number of settings & factors.

            // (A) Which mode are we in?
            if (TeachOnlyMode)
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
            else
            {
                // (B) Did we recognize it as anything at all?
                if (sequence.RecognizedAsIndex >= 0)
                {
                    SilentlySetClassnameSpinner(sequence.RecognizedAsIndex);

                    // (C) Have we not told it yet what it really is?
                    if (sequence.TrueClassIndex < 0)
                    {
                        _guessField.Text = $"Is that a {sequence.RecognizedAsName}?";
                        _guessField.SetTextColor(Android.Graphics.Color.Blue);
                    }
                    // (C') Okay, we've set the spinner (or used another input method like the Submit button) and thus told it what it is.
                    else
                    {
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

            _latestSampleDisplay.Visibility = ViewStates.Visible;
            _discardSampleBtn.Enabled = SubmitButtonPermitted;

            //_listView.Adapter = null;
            //_listView.Adapter = _listAdapter;
            Dataset.TallySequences(); // Note sure if these are still needed or not, given the existence of ButtonStates.Update()... but we'll let it stand in any event.
            _listView.RequestLayout();
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

        public async Task<ISequence> Analyze(ISequence sequence)
        {
            if (Classifier != null && Classifier.MachineOnline)
            {
                sequence.RecognizedAsIndex = await Classifier.Recognize(MostRecentSample as Sequence<T>);
                //sequence.TrueClassIndex = SelectedGestureClass.index;
            }
            return sequence;
        }
    }

    public class GestureClassListAdapter : BaseAdapter<GestureClass>
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

            var ctx = (MachineLearningActivity)_context;
            IconField.Alpha = (ctx.SelectedGestureClass.className == gC.className) ?
                              ((ctx.TeachOnlyMode) ? 1.0f : 0.35f) : 0.25f;
            NameField.Text = gC.className;
            VisualizationField.SetImageBitmap(gC.visualization);
            if (gC.numExamplesSampled == gC.numExamples) // 100% sampling - display actual counts right/wrong
            {
                if (gC.numExamples > 0) PercentageField.Text = $"{(100.0 * gC.numExamplesCorrectlyRecognized / gC.numExamples):f0}%";
                else PercentageField.Text = "0%";
                DetailsField.Text = $"({gC.numExamplesCorrectlyRecognized} / {gC.numExamples})";
            }
            else
            {
                if (gC.numExamplesSampled > 0) PercentageField.Text = $"{(100.0 * gC.numExamplesSampledCorrectlyRecognized / gC.numExamplesSampled):f0}%";
                else PercentageField.Text = "0%";
                DetailsField.Text = $"({gC.numExamples} @ {100.0 * gC.numExamplesSampled / gC.numExamples}%)";
            }
            AddedItemsField.Text = $"+ {gC.numNewExamplesCorrectlyRecognized} / {gC.numNewExamples}";

            return v;
        }
    }
}