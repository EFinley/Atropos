
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

namespace Atropos.Melee
{
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait, WindowSoftInputMode = SoftInput.AdjustPan)]
    public class MeleeBetaActivity 
        : MachineLearningActivity<DKS>
    {
        public const string OFFENSE = "MeleeOffense";
        public const string DEFENSE = "MeleeDefense";
        public const string PROPOSE = "ProposeChoreographer";

        private const string _tag = "Atropos|MeleeActivity";

        protected static new MeleeBetaActivity Current { get { return (MeleeBetaActivity)CurrentActivity; } set { CurrentActivity = value; } }
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
        protected IChoreographer MeleeChoreographer { get; set; }
        protected ChoreographyCue CurrentCue;

        protected System.Diagnostics.Stopwatch Stopwatch = new System.Diagnostics.Stopwatch();
        protected System.Diagnostics.Stopwatch SingleClickWatch = new System.Diagnostics.Stopwatch();
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

            // Debugging
            var q1 = Quaternion.CreateFromAxisAngle(Vector3.One, 0.25f);
            var q2 = Quaternion.Identity;
            Log.Debug(_tag, $"First test: quat {q1} + quat {q2} = quat {q1 + q2}.");
            Log.Debug(_tag, $"Second test: Operator.Add(q1, q2) = {Operator.Add(q1, q2)}");
            var dks1 = new DKS();
            dks1.Values.Value4 = q1;
            var dks2 = new DKS();
            dks2.Values.Value4 = q2;
            var dksSum = dks1 + dks2;
            Log.Debug(_tag, $"Third test: dks1 + dks2 = dks3 w/ orientation {dksSum.Orientation}");
            var dksSum2 = Operator.Add(dks1, dks2);
            Log.Debug(_tag, $"Fourth test: Operator.Add(dks1, dks2) = dks3 w/ orientation {dksSum2.Orientation}");
        }

        protected override async void OnResume()
        {
            await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: false);
            LoadMeleeClassifier();
            ContinuousLogger.TrueActivate(StopToken);

            Communications.Bluetooth.BluetoothMessageCenter.OnReceiveMessage += HandleProposal;

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
            Communications.Bluetooth.BluetoothMessageCenter.OnReceiveMessage -= HandleProposal;
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

                        ResetStage("Melee stage");
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
                        if (SingleClickWatch.Elapsed < ((MeleeChoreographer == null) ? TimeSpan.FromSeconds(0.5) : TimeSpan.FromSeconds(0.2)))
                        {
                            RelayToast("Single-clicked.");
                            if (MeleeChoreographer == null)
                            {
                                AwaitChoreographer();
                            }
                            else
                            {
                                MeleeChoreographer.Deactivate();
                                Task.Delay(100).Wait();
                                MeleeChoreographer = null;
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

                        if (MeleeChoreographer == null)
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
                            //RelayToast("Evaluating.");
                            var resultSeq = new Sequence<DKS>() { SourcePath = resultData };
                            var meta = CurrentStage.GetMetadata(GetAccelForDatapoint);
                            meta.Delay = Delay;
                            resultSeq.Metadata = meta;
                            ResolveEndOfGesture(resultSeq);
                        });


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

                        return true; // Handled it, thanks.  
                    }
                }
            }
            return base.OnKeyUp(keyCode, e);
        }
        #endregion

        private Message opponentsProposal;
        private AsyncAutoResetEvent opponentsProposalSignal = new AsyncAutoResetEvent();
        protected async void AwaitChoreographer()
        {
            MeleeChoreographer = await InitChoreographer();
            //while (this.Choreographer == null)
            //    await Task.Delay(50);

            MeleeChoreographer.OnPromptCue += RespondToPromptCue;
            MeleeChoreographer.Activate();
            Speech.Say("Get ready.");
        }
        protected async Task<IChoreographer> InitChoreographer()
        {
            if (!CueClassifiers.ContainsKey(OFFENSE)) throw new Exception($"{OFFENSE} classifier not found.");
            if (!CueClassifiers.ContainsKey(DEFENSE)) throw new Exception($"{DEFENSE} classifier not found.");

            // Establish the choreographer - this depends on whether you're connected or not (and on Solipsism mode)
            if (Communications.Bluetooth.BluetoothMessageCenter.TemporaryAddressBook_SingleEntry == null)
            {
                if (!Res.SolipsismMode) return new SimpleChoreographer(CueClassifiers);
                else return new SolipsisticChoreographer(CueClassifiers);
            }
            else
            {
                Message myProposal = new Message(MsgType.Notify, PROPOSE);
                Communications.Bluetooth.BluetoothMessageCenter.TemporaryAddressBook_SingleEntry.SendMessage(myProposal);
                Log.Debug(_tag, "Sent proposal message.");
                await opponentsProposalSignal.WaitAsync();
                if (String.Compare(myProposal.ID, opponentsProposal.ID) == 0)
                {
                    Log.Debug(_tag, "Tie GUID encountered - WTF???");
                    return await InitChoreographer();
                }
                else if (String.Compare(myProposal.ID, opponentsProposal.ID) > 0)
                {
                    Log.Debug(_tag, "My GUID wins - I'm choreographer.");
                    return new SendingChoreographer(Communications.Bluetooth.BluetoothMessageCenter.TemporaryAddressBook_SingleEntry, CueClassifiers);
                }
                else
                {
                    Log.Debug(_tag, "My GUID loses - the other guy is choreographer this time.");
                    return new ReceivingChoreographer(Communications.Bluetooth.BluetoothMessageCenter.TemporaryAddressBook_SingleEntry);
                }
            }
        }
        private void HandleProposal(object sender, EventArgs<Message> e)
        {
            var msg = e.Value;
            if (msg.Type != Communications.MsgType.Notify || !msg.Content.StartsWith(PROPOSE)) return;
            Log.Debug(_tag, "Received opponent's proposal message.");
            opponentsProposal = msg;
            opponentsProposalSignal.Set();
        }

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
            if (_singleMode) MeleeChoreographer.Deactivate();
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
                MeleeChoreographer.SubmitResult(CurrentCue);
            }
        }

        protected void LoadMeleeClassifier()
        {
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