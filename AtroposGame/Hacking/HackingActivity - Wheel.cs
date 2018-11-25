
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
using Atropos.Communications;
using Message = Atropos.Communications.Message;
using Atropos.Melee;

namespace Atropos.Hacking.Wheel
{
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait, WindowSoftInputMode = SoftInput.AdjustPan)]
    public class HackingActivity
        : MachineLearningActivity<DKS>
    {
        public const string HACKING = "Hacking";
        public const string PROPOSE = "ProposeChoreographer";

        private const string _tag = "Atropos|HackingActivity";

        public static new HackingActivity Current { get { return (HackingActivity)CurrentActivity; } set { CurrentActivity = value; } }
        //protected static new MachineLearningStage CurrentStage
        //{
        //    get { return (MachineLearningStage)BaseActivity.CurrentStage; }
        //    set { BaseActivity.CurrentStage = value; }
        //}

        #region Required inheritance members
        protected override void ResetStage(string label)
        {
            //CurrentStage = new MachineLearningStage(label, Dataset,
            //    new LoggingSensorProvider<DKS>(new AdvancedProviders.GrabItAllSensorProvider(new GravGyroOrientationProvider())));
            CurrentStage = new MachineLearningStage(label, Dataset, ContinuousLogger);
        }
        #endregion

        #region Data Members (and tightly linked functions / properties)

        protected bool _userTrainingMode { get; set; } = false;
        protected bool _singleMode { get; set; } = false;
        protected bool _seriesMode { get; set; } = true;

        protected bool _collectingData = false;
        protected bool _listeningForGesture = false;
        protected bool _advancedCueMode { get => false; }// { get { return !_userTrainingMode && _seriesMode; } }
        protected IChoreographer HackingChoreographer { get; set; }
        protected ChoreographyCue CurrentCue;

        protected System.Diagnostics.Stopwatch Stopwatch = new System.Diagnostics.Stopwatch();
        protected System.Diagnostics.Stopwatch SingleClickWatch = new System.Diagnostics.Stopwatch();
        protected TimeSpan Delay;

        public static string ActiveNarrativeName;

        public virtual double GetAccelForDatapoint(DKS datapoint) { return datapoint.LinAccel.Length(); }
        #endregion

        #region Activity Lifecycle Methods
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.SimpleListPage);

            Dataset = new DataSet<DKS>();
            Classifier = new Classifier(); // Just in case; it's not actually going to get used until we've loaded the proper one.
        }

        protected override async void OnResume()
        {
            await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: false);
            LoadHackingClassifier();
            ContinuousLogger.TrueActivate(StopToken);

            //Communications.Bluetooth.BluetoothMessageCenter.OnReceiveMessage += HandleProposal;
        }

        protected override void OnPause()
        {
            base.OnPause();
            //Communications.Bluetooth.BluetoothMessageCenter.OnReceiveMessage -= HandleProposal;
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
                        RelayToast("Listening...");
                        _collectingData = true;

                        ResetStage("Hacking stage");
                        //CuePrompter?.MarkGestureStart();
                        Stopwatch.Stop();
                        SingleClickWatch.Restart();
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
                        if (SingleClickWatch.Elapsed < ((HackingChoreographer == null) ? TimeSpan.FromSeconds(0.5) : TimeSpan.FromSeconds(0.2)))
                        {
                            RelayToast("Single-clicked.");
                            if (HackingChoreographer == null)
                            {
                                AwaitChoreographer();
                            }
                            else
                            {
                                HackingChoreographer.Deactivate();
                                Task.Delay(100).Wait();
                                HackingChoreographer = null;
                                CurrentCue = default(ChoreographyCue);
                                Speech.Say("Shutting down.");
                            }
                            // Either way, go no further handling this button-press (and discard the sequence unlooked-at).
                            CurrentStage.Deactivate();
                            Stopwatch.Reset();
                            _collectingData = false;
                            return true;
                        }
                        //Log.Debug("MachineLearning", $"{keyCode} up");

                        if (HackingChoreographer == null)
                        {
                            RelayToast("No choreographer.");
                            _collectingData = false;
                            return true; // Long press without "Get Ready" first - ignore.
                        }

                        Task.Run(async () =>
                        {
                            await Task.Delay(250);

                            // Halt the gesture-collection stage and query it.
                            var resultData = await CurrentStage.StopAndReturnResultsAsync();
                            RelayToast("Evaluating.");
                            var resultSeq = new Sequence<DKS>() { SourcePath = resultData };
                            var meta = CurrentStage.GetMetadata(GetAccelForDatapoint);
                            meta.Delay = Delay;
                            resultSeq.Metadata = meta;
                            ResolveEndOfGesture(resultSeq);
                        });

                        return true; // Handled it, thanks.  
                    }
                }
            }
            return base.OnKeyUp(keyCode, e);
        }
        #endregion

        public void BeginListeningFor(NarrativeCondition condition)
        {

        }

        //private Message opponentsProposal;
        //private AsyncAutoResetEvent opponentsProposalSignal = new AsyncAutoResetEvent();
        protected async void AwaitChoreographer()
        {
            HackingChoreographer = await InitChoreographer();
            //while (this.Choreographer == null)
            //    await Task.Delay(50);

            HackingChoreographer.OnPromptCue += RespondToPromptCue;
            HackingChoreographer.Activate();
            Speech.Say("Get ready.");
        }
        protected async Task<IChoreographer> InitChoreographer()
        {
            if (!CueClassifiers.ContainsKey(HACKING)) throw new Exception($"{HACKING} classifier not found.");

            return new SimpleChoreographer(CueClassifiers); // Placeholder to make it compile.

            //// Establish the choreographer - this depends on whether you're connected or not (and on Solipsism mode)
            //if (Communications.Bluetooth.BluetoothMessageCenter.TemporaryAddressBook_SingleEntry == null)
            //{
            //    if (!Res.SolipsismMode) return new SimpleChoreographer(CueClassifiers);
            //    else return new SolipsisticChoreographer(CueClassifiers);
            //}
            //else
            //{
            //    Message myProposal = new Message(MsgType.Notify, PROPOSE);
            //    Communications.Bluetooth.BluetoothMessageCenter.TemporaryAddressBook_SingleEntry.SendMessage(myProposal);
            //    Log.Debug(_tag, "Sent proposal message.");
            //    await opponentsProposalSignal.WaitAsync();
            //    if (String.Compare(myProposal.ID, opponentsProposal.ID) == 0)
            //    {
            //        Log.Debug(_tag, "Tie GUID encountered - WTF???");
            //        return await InitChoreographer();
            //    }
            //    else if (String.Compare(myProposal.ID, opponentsProposal.ID) > 0)
            //    {
            //        Log.Debug(_tag, "My GUID wins - I'm choreographer.");
            //        return new SendingChoreographer(Communications.Bluetooth.BluetoothMessageCenter.TemporaryAddressBook_SingleEntry, CueClassifiers);
            //    }
            //    else
            //    {
            //        Log.Debug(_tag, "My GUID loses - the other guy is choreographer this time.");
            //        return new ReceivingChoreographer(Communications.Bluetooth.BluetoothMessageCenter.TemporaryAddressBook_SingleEntry);
            //    }
            //}
        }
        //private void HandleProposal(object sender, EventArgs<Message> e)
        //{
        //    var msg = e.Value;
        //    if (msg.Type != Communications.MsgType.Notify || !msg.Content.StartsWith(PROPOSE)) return;
        //    Log.Debug(_tag, "Received opponent's proposal message.");
        //    opponentsProposal = msg;
        //    opponentsProposalSignal.Set();
        //}

        protected async void RespondToPromptCue(object o, EventArgs<ChoreographyCue> eargs)
        {
            CurrentCue = eargs.Value;
            Classifier = CueClassifiers[CurrentCue.ClassifierKey];
            //await GimmeCue(eargs.Value.GestureClass);
            SelectedGestureClass = Classifier.MatchingDatasetClasses[CurrentCue.GestureClassIndex];
            if (CurrentCue.CueTime < DateTime.Now)
            {
                Log.Debug(_tag, $"Timing issue - CurrentCue.CueTime is {CurrentCue.CueTime}, now is {DateTime.Now}.");
                CurrentCue.CueTime = DateTime.Now + TimeSpan.FromMilliseconds(250);
            }
            var delay = CurrentCue.CueTime - DateTime.Now;
            await Task.Delay(delay);
            Speech.Say(SelectedGestureClass.className, SoundOptions.AtSpeed(2.0));
            Stopwatch.Restart();
            if (_singleMode) HackingChoreographer.Deactivate();
        }

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
                if (CurrentCue == null)
                {
                    CurrentCue = new ChoreographyCue();
                }
                else if (MostRecentSample.RecognizedAsIndex == -1)
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
                HackingChoreographer.SubmitResult(CurrentCue);
            }
        }

        protected void LoadHackingClassifier()
        {
            foreach (var option in new string[] { HACKING }) // Might want to add more classifiers later on.
            {
                try
                {
                    ClassifierTree cTree = null;
                    string contents;

                    AssetManager assets = this.Assets;
                    var filename = $"{option}.{Classifier.FileExtension}";
                    var filepath = $"{GetExternalFilesDir(null)}/{filename}";

                    try
                    {
                        using (var streamReader = new StreamReader(filepath))
                        {
                            contents = streamReader.ReadToEnd();

                            cTree = Serializer.Deserialize<ClassifierTree>(contents);
                            if (cTree != null) Log.Debug("Loading classifier", $"Loading our {option} classifier tree (from phone-specific file)");
                        }
                    }
                    catch (Exception)
                    {
                        cTree = null;
                    }

                    if (cTree == null)
                    {
                        using (StreamReader sr = new StreamReader(assets.Open(filename)))
                        {
                            contents = sr.ReadToEnd();
                            cTree = Serializer.Deserialize<ClassifierTree>(contents);
                            if (cTree != null) Log.Debug("Loading classifier", $"Loaded our {option} classifier tree (from asset file).");
                        }
                    }
                    if (cTree == null) throw new Exception($"Classifier deserialization failed - filename {filepath}");

                    CueClassifiers[option] = cTree.MainClassifier;
                    if (option == HACKING) Classifier = cTree.MainClassifier;
                    Dataset = new DataSet<DKS> { Name = cTree.MainClassifier.MatchingDatasetName };
                    foreach (var gClass in cTree.GestureClasses)
                    {
                        Dataset.AddClass(gClass);
                    }
                    SelectedGestureClass = Dataset.Classes.FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Log.Debug("MachineLearning|Load Classifier", ex.ToString());
                    Speech.Say("Cannot launch hacking - no hacking classifier found.");
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
    }
}