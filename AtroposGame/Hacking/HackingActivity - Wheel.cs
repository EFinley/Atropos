
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

        // Necessary for new implementation
        public NarrativeNode CurrentNode;
        #endregion

        #region Activity Lifecycle Methods
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.SimpleListPage);

            Dataset = new DataSet<DKS>();
            Classifier = new Classifier(); // Just in case; it's not actually going to get used until we've loaded the proper one.

            var Parser = new Narrative_Parser();
            var TestHackString = "Entering the tutorial hacking sequence.[Then]Most of what you'll hear is descriptions of what you see or encounter." +
                "[Wait one]When your input is possible, you'll hear a sound effect cue.  For instance... [Cue Alpha1: left]This tone is the cue for a left swipe; " +
                "in this case let's say it's some paydata you've spotted. Note that these aren't mandatory; you're always welcome to decide the paydata isn't " +
                "worth getting distracted over.[Alpha1: yes]Smart aleck. Well done.[Alpha1: no]Here we go.][End Alpha1]" +
                "[Cue Alpha: left]Swipe left now! That's the audio cue again.[Alpha: yes]You've chased the paydata; downloading it now.[Wait four]Download complete.[Alpha: no]You've given it a " +
                "miss this time around.[End Alpha]This sentence will play whichever path you choose.[Cue Beta: right]This cue is for right swipe. If you swiped left " +
                "before, try doing nothing this time, or vice versa.[Beta: yes]Swiped right.[Beta: no]Skipped the swipe this time.[End Beta]" +
                "[Then]A different cue indicates you've run across an intrusion countermeasure, or ice.[Ice Gamma]This is that sound cue. Try responding with a " +
                "wheel sequence such as left and then up, or right and then click." +
                    "[Gamma: success]You've beaten the ice! Congrats.|You've hacked it. Well done." +
                    "[Gamma: fail]Maybe too casual; you failed to beat the ice. Try again.[Retry Gamma][End Gamma]" +
                "[Then]You've beaten the ice, but accumulated some alert status and some insight points, depending on what you picked.[Check Delta1: Alert > 25]" +
                "[Delta1: yes][Check Delta2: Alert > 50][Delta2: yes]Alert is over fifty.][Delta2: no]Alert is between 25 and 50.[End Delta2][Delta1: no]Alert is " +
                "under 25.[End Delta1][TransitionBy Gamma] to the next server node, where you find your objective: the end of the tutorial. Congrats![Disconnect]";
            NarrativeList.Current = Parser.Parse(TestHackString);
            CurrentNode = new NarrativeNode() { NextNode = NarrativeList.Current[0] };
        }

        protected override async void OnResume()
        {
            await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: false);
            LoadHackingClassifier();
            ContinuousLogger.TrueActivate(StopToken);

            Task.Run(DoNodeLoop).LaunchAsOrphan("Node Loop");

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
                        //Stopwatch.Stop();
                        //SingleClickWatch.Restart();
                        //Delay = Stopwatch.Elapsed;
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
                        //// Respond to single-click events
                        //if (SingleClickWatch.Elapsed < ((HackingChoreographer == null) ? TimeSpan.FromSeconds(0.5) : TimeSpan.FromSeconds(0.2)))
                        //{
                        //    RelayToast("Single-clicked.");
                        //    if (HackingChoreographer == null)
                        //    {
                        //        AwaitChoreographer();
                        //    }
                        //    else
                        //    {
                        //        HackingChoreographer.Deactivate();
                        //        Task.Delay(100).Wait();
                        //        HackingChoreographer = null;
                        //        CurrentCue = default(ChoreographyCue);
                        //        Speech.Say("Shutting down.");
                        //    }
                        //    // Either way, go no further handling this button-press (and discard the sequence unlooked-at).
                        //    CurrentStage.Deactivate();
                        //    Stopwatch.Reset();
                        //    _collectingData = false;
                        //    return true;
                        //}
                        //Log.Debug("MachineLearning", $"{keyCode} up");

                        //if (HackingChoreographer == null)
                        //{
                        //    RelayToast("No choreographer.");
                        //    _collectingData = false;
                        //    return true; // Long press without "Get Ready" first - ignore.
                        //}

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

        public async Task DoNodeLoop()
        {
            while (!StopToken.IsCancellationRequested)
            {
                await CurrentNode.ExecuteNode();
                CurrentNode = CurrentNode.NextNode;
            }
        }

        private bool ListeningForGesture = false;
        private NarrativeGestureConditionNode ListeningForNode;

        public void BeginListeningFor(NarrativeGestureConditionNode conditionNode)
        {
            ListeningForNode = conditionNode;
            ListeningForGesture = true;
        }

        public void StopListening()
        {
            ListeningForNode = null;
            ListeningForGesture = false;
        }

        public void DoDisconnect()
        {
            Finish();
        }

        ////private Message opponentsProposal;
        ////private AsyncAutoResetEvent opponentsProposalSignal = new AsyncAutoResetEvent();
        //protected async void AwaitChoreographer()
        //{
        //    HackingChoreographer = await InitChoreographer();
        //    //while (this.Choreographer == null)
        //    //    await Task.Delay(50);

        //    HackingChoreographer.OnPromptCue += RespondToPromptCue;
        //    HackingChoreographer.Activate();
        //    Speech.Say("Get ready.");
        //}
        //protected async Task<IChoreographer> InitChoreographer()
        //{
        //    if (!CueClassifiers.ContainsKey(HACKING)) throw new Exception($"{HACKING} classifier not found.");

        //    return new SimpleChoreographer(CueClassifiers); // Placeholder to make it compile.

        //    //// Establish the choreographer - this depends on whether you're connected or not (and on Solipsism mode)
        //    //if (Communications.Bluetooth.BluetoothMessageCenter.TemporaryAddressBook_SingleEntry == null)
        //    //{
        //    //    if (!Res.SolipsismMode) return new SimpleChoreographer(CueClassifiers);
        //    //    else return new SolipsisticChoreographer(CueClassifiers);
        //    //}
        //    //else
        //    //{
        //    //    Message myProposal = new Message(MsgType.Notify, PROPOSE);
        //    //    Communications.Bluetooth.BluetoothMessageCenter.TemporaryAddressBook_SingleEntry.SendMessage(myProposal);
        //    //    Log.Debug(_tag, "Sent proposal message.");
        //    //    await opponentsProposalSignal.WaitAsync();
        //    //    if (String.Compare(myProposal.ID, opponentsProposal.ID) == 0)
        //    //    {
        //    //        Log.Debug(_tag, "Tie GUID encountered - WTF???");
        //    //        return await InitChoreographer();
        //    //    }
        //    //    else if (String.Compare(myProposal.ID, opponentsProposal.ID) > 0)
        //    //    {
        //    //        Log.Debug(_tag, "My GUID wins - I'm choreographer.");
        //    //        return new SendingChoreographer(Communications.Bluetooth.BluetoothMessageCenter.TemporaryAddressBook_SingleEntry, CueClassifiers);
        //    //    }
        //    //    else
        //    //    {
        //    //        Log.Debug(_tag, "My GUID loses - the other guy is choreographer this time.");
        //    //        return new ReceivingChoreographer(Communications.Bluetooth.BluetoothMessageCenter.TemporaryAddressBook_SingleEntry);
        //    //    }
        //    //}
        //}
        ////private void HandleProposal(object sender, EventArgs<Message> e)
        ////{
        ////    var msg = e.Value;
        ////    if (msg.Type != Communications.MsgType.Notify || !msg.Content.StartsWith(PROPOSE)) return;
        ////    Log.Debug(_tag, "Received opponent's proposal message.");
        ////    opponentsProposal = msg;
        ////    opponentsProposalSignal.Set();
        ////}

        //protected async void RespondToPromptCue(object o, EventArgs<ChoreographyCue> eargs)
        //{
        //    CurrentCue = eargs.Value;
        //    Classifier = CueClassifiers[CurrentCue.ClassifierKey];
        //    //await GimmeCue(eargs.Value.GestureClass);
        //    SelectedGestureClass = Classifier.MatchingDatasetClasses[CurrentCue.GestureClassIndex];
        //    if (CurrentCue.CueTime < DateTime.Now)
        //    {
        //        Log.Debug(_tag, $"Timing issue - CurrentCue.CueTime is {CurrentCue.CueTime}, now is {DateTime.Now}.");
        //        CurrentCue.CueTime = DateTime.Now + TimeSpan.FromMilliseconds(250);
        //    }
        //    var delay = CurrentCue.CueTime - DateTime.Now;
        //    await Task.Delay(delay);
        //    Speech.Say(SelectedGestureClass.className, SoundOptions.AtSpeed(2.0));
        //    Stopwatch.Restart();
        //    if (_singleMode) HackingChoreographer.Deactivate();
        //}

        protected void ResolveEndOfGesture(Sequence<DKS> resultSequence)
        {
            MostRecentSample = resultSequence;
            //if (!_userTrainingMode) MostRecentSample.TrueClassIndex = SelectedGestureClass.index;
            //Dataset?.AddSequence(MostRecentSample);

            //string StageLabel = (!_userTrainingMode)
            //    ? $"Cued gesture {SelectedGestureClass.className}#{SelectedGestureClass.numExamples + SelectedGestureClass.numNewExamples}"
            //    : $"User gesture (#{Dataset.SequenceCount + 1})";
            //if (!_advancedCueMode) ResetStage(StageLabel);

            _collectingData = false;
            if (Classifier == null) return;
            MostRecentSample = Analyze(MostRecentSample).Result;

            if (!ListeningForGesture)
            {
                RelayToast($"Received gesture {(Gest)MostRecentSample.RecognizedAsIndex}.");
                return;
            }

            ListeningForNode.SubmitResult((Gest)MostRecentSample.RecognizedAsIndex);

            // If right or wrong, tweak the display properties of the sample.  This depends on the active mode.
            //DisplaySampleInfo(MostRecentSample);

            //if (_userTrainingMode && MostRecentSample.RecognizedAsIndex >= 0)
            //{
            //    var sc = MostRecentSample.RecognitionScore;
            //    var prefix = (sc < 1) ? "Arguably " :
            //                 (sc < 1.5) ? "Maybe " :
            //                 (sc < 2) ? "Probably " :
            //                 (sc < 2.5) ? "Clearly " :
            //                 (sc < 3) ? "Certainly " :
            //                 "A perfect ";
            //    Speech.Say(prefix + MostRecentSample.RecognizedAsName);
            //}
            //else if (!_userTrainingMode)
            //{
            //    if (CurrentCue == null)
            //    {
            //        CurrentCue = new ChoreographyCue();
            //    }
            //    else if (MostRecentSample.RecognizedAsIndex == -1)
            //    {
            //        CurrentCue.Score = double.NaN;
            //    }
            //    else if (MostRecentSample.RecognizedAsIndex != MostRecentSample.TrueClassIndex)
            //    {
            //        CurrentCue.Score = -1 * MostRecentSample.RecognitionScore;
            //        CurrentCue.GestureClassIndex = MostRecentSample.RecognizedAsIndex;
            //    }
            //    //CuePrompter?.ReactToFinalizedGesture(MostRecentSample);
            //    else
            //    {
            //        var points = MostRecentSample.RecognitionScore;
            //        var delay = MostRecentSample.Metadata.Delay;

            //        CurrentCue.Score = points;
            //        CurrentCue.Delay = delay;
            //    }
            //    //if (!_singleMode) Choreographer?.ProceedWithNextCue();
            //    HackingChoreographer.SubmitResult(CurrentCue);
            //}
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