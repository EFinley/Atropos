
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
using Android.Runtime;
using SimpleFileDialog = Atropos.External_Code.SimpleFileDialog;
using System.IO;
using File = Java.IO.File;
using System.Threading;
using Nito.AsyncEx;
using PerpetualEngine.Storage;

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Atropos.Machine_Learning
{
    class NamedField
    {
        public NamedField(Activity act, int resID)
        {
            Target = act.FindViewById<TextView>(resID);
            if (Target == null) throw new Exception($"Unable to find resource {resID}... whassup?");
        }
        public TextView Target; // Button and EditText both derive from TextView, so this covers all three.
        private FieldState _state;
        public FieldState State
        {
            get
            {
                return _state;
            }
            set
            {
                _state = value;
                if (_state.Enabled != null) Target.Enabled = _state.Enabled.Value;
                if (_state.Text != FieldState.NoChange) Target.Text = _state.Text;

                Target.Clickable = _state.Clickable;

                Target.Invalidate(); // Tell the engine we need re-displaying.
            }
        }
    }

    class FieldState
    {
        public const string NoChange = "__NOCHANGE__"; // Lets us set one to Null and actually have it be Null, as compared with "change nothing".
        public string Text = NoChange;
        public bool? Enabled;

        // Less commonly-used fields
        public bool Clickable = true;

        public void def(string text, bool enabled)
        {
            def(text);
            def(enabled);
        }
        public void def(string text)
        {
            Text = text;
        }
        public void def(bool enabled)
        {
            Enabled = enabled;
        }

        public static FieldState Def(string text = NoChange, bool? enabled = null, bool clickable = true)
        {
            return new FieldState() { Text = text, Enabled = enabled, Clickable = clickable };
        }
        public static FieldState Def(bool enabled)
        {
            return new FieldState() { Enabled = enabled };
        }
    }

    static class ButtonStates
    {
        private static Activity parentActivity; // Not currently needed, but if we need RunOnUI permissions, it'll be handy.

        public static NamedField Save;
        public static NamedField Load;
        public static NamedField Clear;
        public static NamedField Compute;
        public static NamedField DatasetName;
        //public static NamedField AddNewClass;
        private static ListView _listview;
        private static RadioButton _guessAndTeach;
        private static EditText _newClassName;

        public static void AssignTargets(Activity activity)
        {
            parentActivity = activity;
            Save = new NamedField(activity, Resource.Id.mlrn_dataset_save_btn);
            Load = new NamedField(activity, Resource.Id.mlrn_dataset_load_btn);
            Clear = new NamedField(activity, Resource.Id.mlrn_dataset_clear_btn);
            Compute = new NamedField(activity, Resource.Id.mlrn_study_dataset_btn);
            DatasetName = new NamedField(activity, Resource.Id.mlrn_subheading_datasetnamefield);
            //AddNewClass = new NamedField(activity, Resource.Id.mlrn_add_gesture_class_btn);
            _listview = parentActivity.FindViewById<ListView>(Resource.Id.list);
            _guessAndTeach = parentActivity.FindViewById<RadioButton>(Resource.Id.mlrn_trainmode_guessandteach);
            _newClassName = parentActivity.FindViewById<EditText>(Resource.Id.mlrn_new_gesture_class_namefield);
        }

        public static FieldState CannotSave = FieldState.Def("Save", false);
        public static FieldState CanSave = FieldState.Def("Save", true);
        public static FieldState CanSaveClassifier = FieldState.Def("Save AI", true);
        public static FieldState JustSaved = FieldState.Def("Saved", false);

        public static FieldState CannotLoad = FieldState.Def("Load", false);
        public static FieldState CanLoad = FieldState.Def("Load", true);
        public static FieldState JustLoaded = FieldState.Def("Loaded", false);

        public static FieldState NothingToClear = FieldState.Def("Clear", false);
        public static FieldState IsClearedExceptName = FieldState.Def("Delete", true);
        public static FieldState CanClear = FieldState.Def("Clear", true);
        public static FieldState CanClearNewData = FieldState.Def("Revert", true);

        public static FieldState CannotCompute = FieldState.Def("Generate Classification AI", false);
        public static FieldState CanCompute = FieldState.Def("Generate Classification AI", true);
        public static FieldState IsComputing = FieldState.Def("Computing...", true, false);
        public static FieldState IsReassessing = FieldState.Def("Reassessing data...", true, false);
        public static FieldState DoFullReassess = FieldState.Def("Reassessing data...", true);
        public static FieldState JustComputed = FieldState.Def("Classifier Up To Date", true, false);
        public static FieldState CanRecompute = FieldState.Def("Incorporate New Data", true);

        //public static FieldState CannotAddNewClass = FieldState.Def("Add", false);
        //public static FieldState CanAddNewClass = FieldState.Def("Add", true);
        //public static FieldState RemoveClass = FieldState.Def("Remove", true);

        public static void Update<T>(MachineLearningActivity<T> activity) where T : struct
        {
            // Silently fail if uninitialized (happens particularly during initialization of properties like Dataset)
            if (Save == null) return;

            // Prep easily-understood locals
            var Dataset = activity.Dataset;
            var Classifier = activity.Classifier;

            // Anything for us to load?  This (only) doesn't depend on the dataset existing at all.
            bool foundAtLeastOneDataset = false;
            var externalDir = parentActivity.GetExternalFilesDir(null).AbsoluteFile;
            var allFiles = externalDir.ListFiles().ToList();
            if (allFiles.Any(f => f.Name.EndsWith("." + DataSet.FileExtension))) foundAtLeastOneDataset = true;
            while (!foundAtLeastOneDataset && allFiles.Any(f => f.IsDirectory))
            {
                var subdir = allFiles.First(f => f.IsDirectory);
                allFiles.Remove(subdir);
                var subfiles = subdir.ListFiles().Select(f => f.AbsoluteFile);
                if (subfiles.Any(f => f.Name.EndsWith("." + DataSet.FileExtension)))
                {
                    foundAtLeastOneDataset = true;
                    break;
                }
                allFiles.AddRange(subfiles);
            }
            //Log.Debug("FileDirTest", $"Files: {allFiles.Select(f => f.Name).Join()}");
            //foreach (File f in allFiles)
            //    if (f.Name.EndsWith("." + DataSet.FileExtension)) Log.Debug("FileDirTest", $"Including dataset {f.Name}");
            if (foundAtLeastOneDataset)
                Load.State = CanLoad;
            else
                Load.State = CannotLoad;

            _guessAndTeach.Enabled = (Classifier != null && Classifier.MachineOnline);
            _newClassName.Text = _newClassName.Text + ""; // Causes it to reassess its "on text changed" event

            // Rapid exit if there is no dataset at all, since the rest of this would be gibberish and throw lots of exceptions.
            if (Dataset == null)
            {
                Save.State = CannotSave;
                Clear.State = NothingToClear;
                Compute.State = CannotCompute;
                _listview.RequestLayout();
                return;
            }

            // Reference conditions needed through the rest of this process
            bool HasBeenNamed = Dataset.NameIsUserChosen;
            bool HasClasses = Dataset.Classes != null && Dataset.Classes.Count > 0;
            bool HasSamples = Dataset.Samples != null && Dataset.Samples.Count > 0;
            bool HasNewSamples = Dataset.Samples.Any(s => !s.HasContributedToClassifier);

            bool DatasetHasChanged = Dataset.HasChanged;
            Dataset.HasChanged = false; // The "has changed" is "compared to the last time we ran ButtonStates.Update()", so now it's false by definition.

            // ======== Condition logic ===========

            // Does the dataset have a name?
            if (HasBeenNamed)
            {
                DatasetName.State = FieldState.Def(Dataset.Name, true);
            }
            else
            {
                DatasetName.State = FieldState.Def(null, true);
                DatasetName.Target.Hint = Dataset.Name;
                if (Dataset.SavedAsName != null)
                {
                    if (Dataset.SavedAsName.Contains('/'))
                    {
                        int startIndex = Dataset.SavedAsName.LastIndexOf('/') + 1;
                        int length = Dataset.SavedAsName.Length - startIndex - DataSet.FileExtension.Length - 1;
                        if (length > 0) DatasetName.Target.Hint = Dataset.SavedAsName.Substring(startIndex, length);
                    }
                    else DatasetName.Target.Hint = Dataset.SavedAsName.Substring(0, Dataset.SavedAsName.Length - DataSet.FileExtension.Length - 1);
                }
            }

            // Is it empty but for (possibly) a name?
            if (!HasClasses)
            {
                Save.State = CannotSave;
                Compute.State = CannotCompute;
                if (HasBeenNamed || Dataset.SavedAsName != null)
                {
                    Clear.State = IsClearedExceptName;
                }
                else
                {
                    Clear.State = NothingToClear;
                }
            }
            // Does it contain classes, but no actual sample data yet?
            else if (!HasSamples)
            {
                Save.State = (DatasetHasChanged) ? CanSave : CannotSave; // Allowed to still save even if all it includes is empty gesture classes.
                Compute.State = CannotCompute;
                Clear.State = CanClear;
            }
            // Okay, so it contains meaningful content.  What kind?
            else
            {
                Save.State = CanSave;

                // Note that the case where it has neither new data, nor already analyzed data, is handled above (since it therefore has NO data at all).

                // Maybe it's got new data but never been analyzed...
                if (HasNewSamples && !Classifier.MachineOnline)
                {
                    Compute.State = (Dataset.Classes.Count > 1) ? CanCompute : CannotCompute;
                    Clear.State = CanClear;
                }
                // Or maybe it's got existing data and a classifier, but no new data...
                else if (!HasNewSamples && Classifier.MachineOnline)
                {
                    Compute.State = JustComputed;
                    Clear.State = CanClear;
                }
                // Or, lastly, maybe it's got both existing data and a classifier to go with them, AND some new data...
                else if (HasNewSamples && Classifier.MachineOnline)
                {
                    Compute.State = CanRecompute;
                    Clear.State = CanClearNewData;
                }
            }

            //// Lastly, request an update of the list of gesture classes so that their data is up to date as well.
            //_listview.RequestLayout(); // Not sure whether this, or Invalidate(), is the more appropriate.
            //_listview.Invalidate();
            //parentActivity.RunOnUiThread(() =>
            //{
            //    _listview.RequestLayout();
            //    _listview.Invalidate();
            //    parentActivity.FindViewById(Resource.Id.mlrn_latest_sample_display).Invalidate();
            //});
            ((MachineLearningActivity)parentActivity).SetUpAdapters(Dataset);
        }
    }
}