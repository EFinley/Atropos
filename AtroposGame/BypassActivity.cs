
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
//
//
// using Accord.Math;
// using Accord.Statistics;
using Android.Content;
using System.Threading.Tasks;
using System.Numerics;
using Vector3 = System.Numerics.Vector3;
using System.Threading;
using Nito.AsyncEx;
using Android.Graphics;
using Android.Views;
using MiscUtil;
using ZXing.Mobile;
using ZXing;

namespace Atropos
{
    /// <summary>
    /// This activity is launched when we select one of the "hack electronics" tools - examine, multimeter, solderer, or wirecutters.
    /// TODO: Will also be launched if the phone [in Atropos 'null' mode] detects an NFC tag flagged as a toolkit target.
    /// </summary>
    /// 

    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class BypassActivity : BaseActivity_Portrait
    {
        private Toolkit ThePlayersToolkit;
        private List<RelativeLayout> ToolButtons = new List<RelativeLayout>(); 
        private IEffect WhistleFX;
        public static BypassActivity Current { get { return (BypassActivity)CurrentActivity; } }

        private TaskCompletionSource tcs = new TaskCompletionSource();
        private AsyncAutoResetEvent gate = new AsyncAutoResetEvent();
        private TimeSpan minimumInterval = TimeSpan.FromMilliseconds(750);
        private DateTime soonestTapAllowed = DateTime.Now;

        private BypassInfoPanel InfoPanel;
        public TextView OutputText;

        public static event EventHandler<EventArgs<string>> OnBypassCompleted;  // <string> simply because it's not worth re-coding Raise() to handle EventArgs without-generic-argument type.
        public static event EventHandler<EventArgs<string>> OnBypassSuccessful; // ditto
        public static event EventHandler<EventArgs<string>> OnBypassFailed;     // ditto
        public static void AnnounceSuccess(string details) { OnBypassSuccessful.Raise(details); }
        public static void AnnounceFailure(string details) { OnBypassFailed.Raise(details); }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.BypassTools);

            SetUpToolButtons();
            OutputText = FindViewById<TextView>(Resource.Id.display_bypass_info_detailtext);

            //SetTagRemovalResult(() => { }); // Has to exist or else we omit that whole infrastructure.  Will get *changed* later.

            // See if the current kit is already in our (local, for now) library, and load it if so.
            var tkString = Res.SpecificTags.Get(InteractionLibrary.CurrentSpecificTag);
            if (tkString != null)
            {
                ThePlayersToolkit = Toolkit.FromString(tkString, InteractionLibrary.CurrentSpecificTag);
            }
            else if (InteractionLibrary.CurrentSpecificTag == null) // I.E. this is a "launch directly" order.
            {
                ThePlayersToolkit = new MemorylessToolkit();
            }
            else
            {
                ThePlayersToolkit = new Toolkit(InteractionLibrary.CurrentSpecificTag);
            }

            SecurityPanel.Initialize(this);
            InfoPanel = new BypassInfoPanel(this, SecurityPanel.CurrentPanel);
            SetTypeface(Resource.Id.display_bypass_info_headertext, "FTLTLT.TTF"); 
            CurrentStage = GestureRecognizerStage.NullStage;

            WhistleFX = new Effect("Whistle", Resource.Raw._98195_whistling);
            FindViewById(Resource.Id.choice_bypass_examine).CallOnClick();

            OnBypassSuccessful += (o, e) => { OnBypassCompleted.Raise(e.Value); };
            OnBypassFailed += (o, e) => { OnBypassCompleted.Raise(e.Value); };
        }

        protected override void OnResume()
        {
            base.DoOnResume();
            if (DateTime.Now < soonestTapAllowed) return;
            //base.DoOnResume(async () =>
            //{
            //    await System.Threading.Tasks.Task.Delay(750);
            //    WhistleFX.Play();
            //    Task.Run(async () =>
            //    {
            //        for (int i = 0; i < 10; i++)
            //        {
            //            await Task.Delay(1000);
            //            WhistleFX.Volume = (1.0f - 0.1f * i);
            //            Log.Debug("Toolkit", $"Whisper volume {WhistleFX.Volume:f2}");
            //        }
            //    }).LaunchAsOrphan("Whistle dying down");
            //    await System.Threading.Tasks.Task.Delay(500);
            //    //await Speech.SayAllOf("Cracking open the toolkit.  Select a tool onscreen.  Note, sometimes you'll get further information from repeating a measurement.");
            //    
            //});

            Toast.MakeText(this, "Cracking open the bypass tools.  Select a tool onscreen.  Note, sometimes you'll get further information from repeating a measurement.", ToastLength.Long).Show();
            EnableForegroundDispatch();

            //Task.Delay(50).ContinueWith(_ =>
            //{
            //    RunOnUiThread(() => 
            //    { 
            //        var SelfView = InfoPanel.SelfView;
            //        var bmp = BitmapFactory.DecodeResource(Application.Context.Resources, Resource.Drawable.securitypanel_gray);
            //        var ht = bmp.Height;
            //        var wd = bmp.Width;
            //        var lp = SelfView.LayoutParameters;
            //        var selfWd = SelfView.MeasuredWidth;
            //        lp.Height = (int)(lp.Width * (double)ht / wd);
            //        Log.Debug("BypassInfoPanel", $"Ht = {ht}, wd = {wd}, selfWd = {selfWd}");
            //        //SelfView.Measure(selfWd, (int)((double)selfWd * (double)ht / (double)wd));
            //        //SelfView.SetMinimumHeight((int)((double)selfWd * (double)ht / (double)wd));

            //        SelfView.LayoutParameters = lp;
            //        FindViewById(Resource.Id.linearLayout1).RequestLayout();
            //    });
            //});

            if (UseQRScanner)
            {
                var scanBtn = FindViewById<RelativeLayout>(Resource.Id.choice_bypass_scanQR);
                scanBtn.Visibility = ViewStates.Visible;
                scanBtn.Click += (o, e) => { StartScanning(); };
            }
        }

        protected override void OnPause()
        {
            base.DoOnPause(() => { Res.AllowNewActivities = true; });
        }

        private void SetUpToolButtons()
        {
            //var layoutpanel = FindViewById<LinearLayout>(Resource.Id.Toolkit_layoutpane);

            //var buttonA = FindViewById<Button>(Resource.Id.buttonA);
            //var buttonB = FindViewById<Button>(Resource.Id.buttonB);
            //var buttonC = FindViewById<Button>(Resource.Id.buttonC);
            //var buttonD = FindViewById<Button>(Resource.Id.buttonD);

            //var curr = CurrentStage as Toolkit_StageBase;
            //SecurityPanel.CurrentPanel.NodeForTag.Add("buttonA", SecurityPanel.CurrentPanel.Nodes.SingleOrDefault(n => n.Code == "A"));
            //SecurityPanel.CurrentPanel.NodeForTag.Add("buttonB", SecurityPanel.CurrentPanel.Nodes.SingleOrDefault(n => n.Code == "B"));
            //SecurityPanel.CurrentPanel.NodeForTag.Add("buttonC", SecurityPanel.CurrentPanel.Nodes.SingleOrDefault(n => n.Code == "C"));
            //SecurityPanel.CurrentPanel.NodeForTag.Add("buttonD", SecurityPanel.CurrentPanel.Nodes.SingleOrDefault(n => n.Code == "D"));

            //buttonA.Click += async (o, e) =>
            //{
            //    await curr.ActOnTag("buttonA");
            //    soonestTapAllowed = DateTime.Now + minimumInterval;
            //};
            //buttonB.Click += async (o, e) =>
            //{
            //    await curr.ActOnTag("buttonB");
            //    soonestTapAllowed = DateTime.Now + minimumInterval;
            //};
            //buttonC.Click += async (o, e) =>
            //{
            //    await curr.ActOnTag("buttonC");
            //    soonestTapAllowed = DateTime.Now + minimumInterval;
            //};
            //buttonD.Click += async (o, e) =>
            //{
            //    await curr.ActOnTag("buttonD");
            //    soonestTapAllowed = DateTime.Now + minimumInterval;
            //};

            //foreach (Toolkit.Toolname function in Toolkit.InteractionModes.Keys)
            //{
            //    var func = function; // Local variable copy to ensure the closures work okay below.
            //    var funcButton = new Button(this);
            //    funcButton.SetText(func.ToString(), TextView.BufferType.Normal);
            //    funcButton.SetPadding(20, 40, 20, 40);
            //    layoutpanel.AddView(funcButton);
            //    ToolButtons.Add(funcButton);

            //    funcButton.Click += (o, e) =>
            //    {
            //        if (CurrentStage != GestureRecognizerStage.NullStage) CurrentStage.Deactivate();

            //        if (func == Toolkit.Toolname.Examine) CurrentStage = new Toolkit_ExamineStage("Examine");
            //        else if (func == Toolkit.Toolname.Multimeter) CurrentStage = new Toolkit_MultimeterStage("Multimeter");
            //        else if (func == Toolkit.Toolname.Wirecutter) CurrentStage = new Toolkit_WirecutterStage("Wirecutters");
            //        else if (func == Toolkit.Toolname.Solderer) CurrentStage = new Toolkit_SolderingStage("Soldering");
            //        else if (func == Toolkit.Toolname.Lockpicks) CurrentStage = new Toolkit_LockpickingStage("Lockpicks");
            //        else if (func == Toolkit.Toolname.Safecracking) CurrentStage = new Toolkit_SafecrackingStage("Safecracking");

            //        CurrentStage.Activate();
            //    };
            //}

            //AssignStageToButton(Resource.Id.choice_bypass_multimeter, new Toolkit_MultimeterStage());
            //AssignStageToButton(Resource.Id.choice_bypass_soldering, new Toolkit_SolderingStage());
            //AssignStageToButton(Resource.Id.choice_bypass_examine, new Toolkit_ExamineStage());
            //AssignStageToButton(Resource.Id.choice_bypass_wirecutting, new Toolkit_WirecutterStage());

            AssignStageToButton(Resource.Id.choice_bypass_multimeter, () => { InfoPanel.SetUpMeasureMode(); });
            AssignStageToButton(Resource.Id.choice_bypass_soldering, () => { InfoPanel.SetUpSolderMode(); });
            AssignStageToButton(Resource.Id.choice_bypass_examine, () => { InfoPanel.SetUpExamineMode(); });
            AssignStageToButton(Resource.Id.choice_bypass_wirecutting, () => { InfoPanel.SetUpWirecutterMode(); });
        }

        //private void AssignStageToButton(int ButtonId, Toolkit_StageBase stageToLaunch)
        private void AssignStageToButton(int ButtonId, Action SetButtonResult)
        {
            var Btn = FindViewById<RelativeLayout>(ButtonId);
            ToolButtons.Add(Btn);
            SetTypeface(Btn, "FTLTLT.TTF");
            Btn.Click += (o, e) =>
            {
                //if (CurrentStage != GestureRecognizerStage.NullStage) CurrentStage.Deactivate();
                //CurrentStage = stageToLaunch;
                //CurrentStage.Activate();
                foreach (RelativeLayout btn in ToolButtons)
                {
                    //if (btn.Id == ButtonId) btn.Visibility = ViewStates.Gone;
                    //else btn.Visibility = ViewStates.Visible;
                    btn.Enabled = (btn.Id != ButtonId);
                }

                InfoPanel.FirstNode = InfoPanel.SecondNode = SecurityPanelNode.Unknown;
                SetButtonResult.Invoke();
                InfoPanel.RecognizeNode(SecurityPanelNode.Unknown);
            };
        }

        #region SetTypeface - imported from SelectorActivity
        protected void SetTypeface(int resId, string fontFilename)
        {
            var tgtView = FindViewById(resId);
            SetTypeface(tgtView, fontFilename);
        }

        protected void SetTypeface(Android.Views.View tgtView, string fontFilename)
        {
            if (tgtView == null) return;
            Typeface tf = Typeface.CreateFromAsset(this.Assets, fontFilename);

            var vg = tgtView as Android.Views.ViewGroup;
            if (vg != null)
            {
                foreach (int i in Enumerable.Range(0, vg.ChildCount))
                    SetTypeface(vg.GetChildAt(i), fontFilename);
            }

            else (tgtView as TextView)?.SetTypeface(tf, TypefaceStyle.Normal);
        }
        #endregion

        /// <summary>
        /// Identify to Android that this activity wants to be notified when 
        /// an NFC tag is discovered. 
        /// </summary>
        private void EnableForegroundDispatch()
        {
            // Create an intent filter for when an NFC tag is discovered.
            var tagDetected = new IntentFilter(NfcAdapter.ActionTagDiscovered);
            var filters = new[] { tagDetected };

            // When an NFC tag is detected, Android will use the PendingIntent to come back to this activity.
            // The OnNewIntent method will be invoked by Android.
            var intent = new Intent(this, this.GetType()).AddFlags(ActivityFlags.SingleTop);
            var pendingIntent = PendingIntent.GetActivity(this, 0, intent, 0);

            if (_nfcAdapter == null)
            {
                //var alert = new AlertDialog.Builder(this).Create();
                //alert.SetMessage("NFC is not supported on this device.");
                //alert.SetTitle("NFC Unavailable");
                //alert.SetButton("OK", delegate
                //{
                //    _writeTagButton.Enabled = false;
                //    _nfcText.Text = "NFC is not supported on this device.";
                //});
                //alert.Show();
                Speech.Say("Sorry, this device doesn't seem to support NFC.");
            }
            else
            {
                Res.AllowNewActivities = false;
                _nfcAdapter.EnableForegroundDispatch(this, pendingIntent, filters, null);
            }

        }

        /// <summary>
        /// This method will be called when an NFC tag is discovered by the application,
        /// as long as we've enabled 'foreground dispatch' - send it to us, don't go looking
        /// for another program to respond to the tag.
        /// </summary>
        /// <param name="intent">The Intent representing the occurrence of "hey, we spotted an NFC!"</param>
        protected override async void OnNewIntent(Intent intent)
        {
            //// Checks to make sure we're actually in a toolkit mode, and so that we know which one we're in later.
            //var curr = CurrentStage as Toolkit_StageBase;
            //if (curr == null) return;

            var nfcTag = InteractionLibrary.CurrentTagHandle = intent.GetParcelableExtra(NfcAdapter.ExtraTag) as Tag;

            if (nfcTag == null || DateTime.Now < soonestTapAllowed) return;
            soonestTapAllowed = DateTime.Now + minimumInterval; // Will also be reset after acting on the tag but this gives a minimum that will effectively debounce things.

            var nfcID = InteractionLibrary.CurrentSpecificTag = nfcTag.tagID();
            //Log.Info("Toolkit Use", "NFC tag ID is " + nfcID ?? "(none)");

            var rawMessages = intent.GetParcelableArrayExtra(NfcAdapter.ExtraNdefMessages);
            var msg = (NdefMessage)rawMessages[0];
            var nfcRecordBody = msg.GetRecords();
            string _nfcTagID = InteractionLibrary.CurrentTagHandle.tagID();
            string _nfcTagInteractionMode = Encoding.ASCII.GetString(nfcRecordBody[0].GetPayload());

            string s = (Res.InteractionModes.ContainsKey(_nfcTagInteractionMode))
                ? ", which is in the Mode library." 
                : (SecurityPanel.CurrentPanel.Nodes.Select(n => n.Code).Contains(_nfcTagInteractionMode))
                    ? ", which matches a known security panel node." 
                    : ", which is unknown.";
            Log.Debug("Toolkit Use", $"Found tag #{nfcID}. Defined interaction mode is {_nfcTagInteractionMode}{s}");

            if (_nfcTagInteractionMode.Length == 1) // TODO: Make this more official.  But for now, one letter means a hotspot.
            {
                if (!SecurityPanel.CurrentPanel.NodeForTag.ContainsKey(_nfcTagID))
                    SecurityPanel.CurrentPanel.NodeForTag.Add(_nfcTagID, SecurityPanel.CurrentPanel.Nodes.SingleOrDefault(n => n.Code == _nfcTagInteractionMode));

                //await curr.ActOnTag(_nfcTagID);
                InfoPanel.RecognizeNode(SecurityPanel.CurrentPanel.NodeForTag[_nfcTagID]);
                soonestTapAllowed = DateTime.Now + minimumInterval;
            }
        }

        //private class Toolkit_StageBase : GestureRecognizerStage
        //{
        //    protected StillnessProvider Stillness;
        //    public Toolkit_StageBase(string label, bool autoStart = false) : base(label)
        //    {
        //        Stillness = new StillnessProvider();
        //        SetUpProvider(Stillness);
        //        Kit = Current.ThePlayersToolkit;

        //        if (autoStart) Activate();
        //    }
        //    public CancellationTokenSource TagRemovalCTS = new CancellationTokenSource();
        //    protected Toolkit Kit;

        //    protected string TutorialMessage;

        //    protected override async Task startActionAsync()
        //    {
        //        //CurrentActivity.SetTagRemovalResult(TagRemovalCTS.Cancel, 0.5, 0.1);
        //        //await Speech.SayAllOf(TutorialMessage);
        //        //ToastMessage(TutorialMessage);
        //        if (TutorialMessage != "") Toast.MakeText(Application.Context, TutorialMessage, ToastLength.Long).Show();
        //        TutorialMessage = "";
        //    }
        //    protected override bool interimCriterion()
        //    {
        //        return true;
        //    }

        //    protected override async Task interimActionAsync()
        //    {
        //        await Task.Delay(50);
        //    }

        //    protected bool AwaitingSecondTagOfSequence = false;
        //    protected static SecurityPanelNode lastNode;
        //    protected virtual async Task ActOnFirstTag(SecurityPanelNode node1)
        //        { await Task.CompletedTask; }
        //    protected virtual async Task ActOnSecondTag(SecurityPanelNode node2)
        //        { await Task.CompletedTask; }
        //    public async Task ActOnTag(string tagID)
        //    {
        //        var node = SecurityPanel.CurrentPanel.NodeForTag[tagID];
        //        CurrentActivity.FindViewById<TextView>(Resource.Id.display_bypass_info_headertext).Text = node.Name;
        //        if (!AwaitingSecondTagOfSequence)
        //            { await ActOnFirstTag(node); AwaitingSecondTagOfSequence = true; }
        //        else
        //            { await ActOnSecondTag(node); AwaitingSecondTagOfSequence = false; }
        //        lastNode = node;

        //    }

        //    protected void ToastMessage(string message)
        //    {
        //        //if (message != "") Toast.MakeText(Application.Context, message, ToastLength.Short).Show();
        //        //if (message != "") (CurrentActivity as BypassActivity)?.OutputText.SetText(message, TextView.BufferType.Normal);
        //        if (message != "") (CurrentActivity as BypassActivity).OutputText.Text = message;
        //    }
        //    //public virtual void DoOnLongHold()
        //    //{
        //    //    throw new NotImplementedException();
        //    //}
        //    //public virtual async void ActOnLongHold()
        //    //{
        //    //    var combinedToken = Nito.AsyncEx.CancellationTokenHelpers.Normalize(StopToken, TagRemovalCTS.Token).Token;
        //    //    await Task.Delay(2000, combinedToken)
        //    //        .ContinueWith((t) => DoOnLongHold(), TaskContinuationOptions.OnlyOnRanToCompletion);
        //    //}
        //}

        //private class Toolkit_ExamineStage : Toolkit_StageBase
        //{
        //    public Toolkit_ExamineStage(bool autoStart = false) : base("Examine", autoStart)
        //    {
        //        TutorialMessage = "Examine tool: basic descriptions.";
        //    }
        //    public static bool TutorialHasBeenGiven { get; set; } = false;
        //    protected override async Task ActOnFirstTag(SecurityPanelNode node)
        //    {
        //        AwaitingSecondTagOfSequence = false; // Doesn't have 'em.
        //        //if (node != lastNode && DateTime.Now > node.ShortExamineGivenAt + TimeSpan.FromSeconds(5))
        //        //{
        //        //    //await Speech.SayAllOf(node.ShortExamineResult);
        //        //    ToastMessage(node.ShortExamineResult);
        //        //    node.ShortExamineGivenAt = DateTime.Now;
        //        //}
        //        //else if (DateTime.Now > node.LongExamineGivenAt + TimeSpan.FromSeconds(5)
        //        //    && DateTime.Now > node.ShortExamineGivenAt + TimeSpan.FromSeconds(2))
        //        //{
        //        //    //await Speech.SayAllOf(node.LongExamineResult);
        //        //    ToastMessage(node.LongExamineResult);
        //        //    node.LongExamineGivenAt = DateTime.Now;
        //        //}
        //        ToastMessage(node.Results[node.NumberOfTimesExamined]);
        //        node.NumberOfTimesExamined++;
        //    }
        //}

        //private class Toolkit_MultimeterStage : Toolkit_StageBase
        //{
        //    public Toolkit_MultimeterStage(bool autoStart = false) : base("Multimeter", autoStart)
        //    {
        //        TutorialMessage = "Multimeter tool: measures what's passing from point A to point B.  Tap them in that order. The opposite order is a different piece of information.";
        //    }
        //    public static bool TutorialHasBeenGiven { get; set; } = false;
        //    private static Effect _measureEffect;
        //    public static Effect MeasureEffect
        //    {
        //        get
        //        {
        //            if (_measureEffect == null)
        //            {
        //                _measureEffect = new Effect("Multimeter.Measure", Resource.Raw._37847_infiniteauubergine);
        //            }
        //            return _measureEffect;
        //        }
        //    }
        //    protected override async Task ActOnFirstTag(SecurityPanelNode node)
        //    {
        //        MeasureEffect.Play(0.2, true);
        //        await Task.CompletedTask;
        //    }
        //    protected override async Task ActOnSecondTag(SecurityPanelNode node2)
        //    {
        //        MeasureEffect.Stop();
        //        var link = SecurityPanel.CurrentPanel.Linkages[lastNode, node2];
        //        //if (link.ShortMultimeterGivenAt == null || link.ShortMultimeterGivenAt + TimeSpan.FromSeconds(10) < DateTime.Now)
        //        //{
        //        //    //await Speech.SayAllOf(link.ShortMultimeterResult);
        //        //    ToastMessage(link.ShortMultimeterResult);
        //        //    link.ShortMultimeterGivenAt = DateTime.Now;
        //        //}
        //        //else if (link.LongMultimeterGivenAt + TimeSpan.FromSeconds(10) < DateTime.Now)
        //        //{
        //        //    //await Speech.SayAllOf(link.LongMultimeterResult);
        //        //    ToastMessage(link.LongMultimeterResult);
        //        //    link.LongMultimeterGivenAt = DateTime.Now;
        //        //}
        //        ToastMessage(link.MeasureResults[link.NumberOfTimesMultimetered]);
        //        link.NumberOfTimesMultimetered++;
        //    }
        //}

        //private class Toolkit_WirecutterStage : Toolkit_StageBase
        //{
        //    public Toolkit_WirecutterStage(bool autoStart = false) : base("WireCutters", autoStart)
        //    {
        //        TutorialMessage = "Wirecutter tool: Cut the connection from point A to point B.  Hover over them in that order.";
        //    }
        //    public static bool TutorialHasBeenGiven { get; set; } = false;
        //    protected override async Task ActOnFirstTag(SecurityPanelNode node)
        //    {
        //        await Task.CompletedTask;
        //    }
        //    protected override async Task ActOnSecondTag(SecurityPanelNode node2)
        //    {
        //        var link = SecurityPanel.CurrentPanel.Linkages[lastNode, node2];
        //        if (!link.IsLinked)
        //        {
        //            //await Speech.SayAllOf("Hmmm.  There's no link there to cut.");
        //            ToastMessage("Hmm. There's no link there to cut.");
        //            return;
        //        }
        //        link.WirecutterResult?.Invoke();
        //        link.IsLinked = false;
        //    }
        //}

        //private class Toolkit_SolderingStage : Toolkit_StageBase
        //{
        //    public Toolkit_SolderingStage(bool autoStart = false) : base("Soldering", autoStart)
        //    {
        //        TutorialMessage = "Soldering Iron: Creates a link from point A to point B.";
        //    }
        //    public static bool TutorialHasBeenGiven { get; set; } = false;
        //    protected override async Task ActOnFirstTag(SecurityPanelNode node)
        //    {
        //        await Task.CompletedTask;
        //    }
        //    protected override async Task ActOnSecondTag(SecurityPanelNode node2)
        //    {
        //        var link = SecurityPanel.CurrentPanel.Linkages[lastNode, node2];
        //        if (link.IsLinked)
        //        {
        //            //await Speech.SayAllOf("Hmm? There's; already; a link between those nodes.");
        //            ToastMessage("Hmm? There's *already* a link between those nodes.");
        //            return;
        //        }
        //        link.SolderingResult?.Invoke();
        //        link.IsLinked = true;
        //    }
        //}

        protected class BypassInfoPanel
        {
            protected Activity Parent;
            public RelativeLayout SelfView;
            protected ImageView BackgroundImageView, BackgroundOverlay;
            protected TextView HeaderTextView, DetailTextView;
            protected SurfaceView Canvas;
            protected ImageButton InSituMeasure, InSituSolder, InSituWirecut, InSituCancel;
            
            protected string HeaderText { get { return HeaderTextView?.Text; } set { HeaderTextView?.SetText(value, TextView.BufferType.Normal); } }
            protected string DetailText { get { return DetailTextView?.Text; } set { DetailTextView?.SetText(value, TextView.BufferType.Normal); } }

            public SecurityPanel SecurityPanel;
            public SecurityPanelNode FirstNode = SecurityPanelNode.Unknown,
                                     SecondNode = SecurityPanelNode.Unknown;
            public SecurityPanelNodeLink NodeLink { get; set; }
            protected SecurityPanelNodeLink GetNodeLink() { return NodeLink; }
            protected bool AllowSecondNode = false;

            public BypassInfoPanel(Activity parent, SecurityPanel securityPanel)
            {
                Parent = parent;
                SecurityPanel = securityPanel;

                SelfView = Parent.FindViewById<RelativeLayout>(Resource.Id.display_bypass_information);
                BackgroundImageView = SelfView.FindViewById<ImageView>(Resource.Id.display_bypass_info_picture);
                BackgroundOverlay = SelfView.FindViewById<ImageView>(Resource.Id.display_bypass_info_overlay);
                HeaderTextView = SelfView.FindViewById<TextView>(Resource.Id.display_bypass_info_headertext);
                DetailTextView = SelfView.FindViewById<TextView>(Resource.Id.display_bypass_info_detailtext);
                //Canvas = SelfView.FindViewById<SurfaceView>(Resource.Id.display_bypass_canvas);
                InSituMeasure = SelfView.FindViewById<ImageButton>(Resource.Id.display_bypass_insituMeasure);
                InSituSolder = SelfView.FindViewById<ImageButton>(Resource.Id.display_bypass_insituSolder);
                InSituWirecut = SelfView.FindViewById<ImageButton>(Resource.Id.display_bypass_insituWirecut);
                InSituCancel = SelfView.FindViewById<ImageButton>(Resource.Id.display_bypass_insituCancel);

                //if (new View[] { SelfView, BackgroundImageView, HeaderTextView, DetailTextView, Canvas, ModalOne, ModalTwo, ModalThree}
                if (new View[] { SelfView, BackgroundImageView, HeaderTextView, DetailTextView, InSituMeasure, InSituSolder, InSituWirecut, InSituCancel }
                        .Any(v => v == null))
                {
                    Log.Error("Bypass|InfoPanel", "Problem locating all the sub-views needed for the Bypass Info Panel.");
                }

                InSituCancel.Click += (o, e) => 
                {
                    FirstNode = SecondNode = SecurityPanelNode.Unknown;
                    SetUpExamineMode();
                    RecognizeNode(SecurityPanelNode.Unknown);
                };
                InSituMeasure.Click += (o, e) =>
                {
                    var firstNode = FirstNode;
                    var secondNode = SecondNode;
                    FirstNode = SecondNode = SecurityPanelNode.Unknown; // Allows it to be assessed anew (as if rescanned under the new mode)
                    SetUpMeasureMode();
                    RecognizeNode(firstNode);
                    Task.Delay(250).Wait();
                    RecognizeNode(secondNode);
                };
                InSituSolder.Click += (o, e) =>
                {
                    var firstNode = FirstNode;
                    var secondNode = SecondNode;
                    FirstNode = SecondNode = SecurityPanelNode.Unknown; // Allows it to be assessed anew (as if rescanned under the new mode)
                    SetUpSolderMode();
                    RecognizeNode(firstNode);
                    Task.Delay(500).Wait();
                    RecognizeNode(secondNode);
                };
                InSituWirecut.Click += (o, e) =>
                {
                    var firstNode = FirstNode;
                    var secondNode = SecondNode;
                    FirstNode = SecondNode = SecurityPanelNode.Unknown; // Allows it to be assessed anew (as if rescanned under the new mode)
                    SetUpWirecutterMode();
                    RecognizeNode(firstNode);
                    Task.Delay(500).Wait();
                    RecognizeNode(secondNode);
                };

                SetUpExamineMode();
            }

            private string _headerFormatString, _headerNullString;
            private Func<string> _getDetailString;
            public string GetHeaderString() {
                return (FirstNode == SecurityPanelNode.Unknown) 
                    ? _headerNullString 
                    //: (SecondNode == SecurityPanelNode.Unknown)
                    //? String.Format(_headerFormatString, FirstNode.Name, "...") // Unnecessary... the "name" of SecurityPanelNode.Unknown is "..." already.
                    : String.Format(_headerFormatString, FirstNode.Name, SecondNode.Name); }
            public string GetDetailString() { return _getDetailString?.Invoke(); }
            private Action _resolveFirstNode, _resolveSecondNode;
            public event EventHandler<EventArgs<SecurityPanelNode>> OnFirstNode;
            public event EventHandler<EventArgs<SecurityPanelNode>> OnSecondNode;
            private CancellationTokenSource _cts;

            public void RecognizeNode(SecurityPanelNode node)
            {
                if (node.OverlayResourceId > 0)
                {
                    BackgroundOverlay.SetImageResource(node.OverlayResourceId);
                    BackgroundOverlay.Visibility = ViewStates.Visible;
                }
                else
                {
                    BackgroundOverlay.Visibility = ViewStates.Invisible;
                }
                Action resolveNode;
                EventHandler<EventArgs<SecurityPanelNode>> onResolve;

                if (FirstNode != SecurityPanelNode.Unknown && SecondNode == SecurityPanelNode.Unknown && AllowSecondNode)
                {
                    SecondNode = node;
                    NodeLink = SecurityPanel.Linkages[FirstNode, SecondNode];
                    resolveNode = _resolveSecondNode;
                    onResolve = OnSecondNode;
                }
                else if (node != SecurityPanelNode.Unknown)
                {
                    FirstNode = node;
                    SecondNode = SecurityPanelNode.Unknown;
                    resolveNode = _resolveFirstNode;
                    onResolve = OnFirstNode;
                }
                else
                {
                    resolveNode = null;
                    onResolve = null;
                }

                _cts?.Cancel();
                HeaderText = GetHeaderString();
                DetailText = GetDetailString();
                resolveNode?.Invoke();
                onResolve?.Invoke(this, new EventArgs<SecurityPanelNode>(node));
            }

            public void SetUpExamineMode()
            {
                _headerFormatString = "Examining {0}";
                _headerNullString = "Examine ...";
                _getDetailString = () => { return FirstNode.Results.At(FirstNode.NumberOfTimesExamined); };
                AllowSecondNode = false;
                _resolveFirstNode = () => { FirstNode.NumberOfTimesExamined++; };
                InSituMeasure.Visibility = InSituSolder.Visibility = InSituWirecut.Visibility = InSituCancel.Visibility = ViewStates.Gone;
            }

            public void SetUpMeasureMode()
            {
                _headerFormatString = "Measure from {0} to {1}";
                _headerNullString = "Measure ... ";
                _getDetailString = () => 
                {
                    if (FirstNode == SecurityPanelNode.Unknown || SecondNode == SecurityPanelNode.Unknown) return String.Empty;
                    //var nodeLink = GetNodeLink();
                    return NodeLink.MeasureResults.At(NodeLink.NumberOfTimesMultimetered);
                };
                AllowSecondNode = true;
                _resolveFirstNode = () => {
                    //Toolkit_MultimeterStage.MeasureEffect.Play(0.2, true);
                    _cts = new CancellationTokenSource();
                    _cts.Token.Register(() => Toolkit.MeasureEffect.Stop());
                    Toolkit.MeasureEffect.Play(new SoundOptions() { Volume = 0.2, Looping = true, CancelToken = _cts.Token });
                    InSituSolder.Visibility = InSituWirecut.Visibility = ViewStates.Gone;
                };
                _resolveSecondNode = () => 
                {
                    NodeLink.NumberOfTimesMultimetered++;
                    //Toolkit_MultimeterStage.MeasureEffect.Stop();
                    //Toolkit.MeasureEffect.Stop();
                    InSituSolder.Visibility = InSituWirecut.Visibility = ViewStates.Visible;
                };
                InSituMeasure.Visibility = InSituSolder.Visibility = InSituWirecut.Visibility = ViewStates.Gone;
                InSituCancel.Visibility = ViewStates.Visible;
            }

            public void SetUpSolderMode()
            {
                _headerFormatString = "Connect up {0} to {1}";
                _headerNullString = "Connect ... ";
                _getDetailString = () => { return ""; };
                AllowSecondNode = true;
                _resolveFirstNode = () => 
                {
                    InSituMeasure.Visibility = InSituWirecut.Visibility = ViewStates.Gone;
                };
                _resolveSecondNode = () =>
                {
                    var nodeLink = GetNodeLink();
                    if (nodeLink.IsLinked) DetailText = "(Already connected)";
                    nodeLink.IsLinked = true;
                    nodeLink.SolderingResult?.Invoke();
                    InSituMeasure.Visibility = InSituWirecut.Visibility = ViewStates.Visible;
                };
                InSituMeasure.Visibility = InSituSolder.Visibility = InSituWirecut.Visibility = ViewStates.Gone;
                InSituCancel.Visibility = ViewStates.Visible;
            }

            public void SetUpWirecutterMode()
            {
                _headerFormatString = "Cut link from {0} to {1}";
                _headerNullString = "Measure ... ";
                _getDetailString = () => { return ""; };
                AllowSecondNode = true;
                _resolveFirstNode = () =>
                {
                    InSituMeasure.Visibility = InSituSolder.Visibility = ViewStates.Gone;
                };
                _resolveSecondNode = () =>
                {
                    var nodeLink = GetNodeLink();
                    if (!nodeLink.IsLinked) DetailText = "(Nothing to cut)";
                    nodeLink.IsLinked = false;
                    nodeLink.WirecutterResult?.Invoke();
                    InSituMeasure.Visibility = InSituSolder.Visibility = ViewStates.Visible;
                };
                InSituMeasure.Visibility = InSituSolder.Visibility = InSituWirecut.Visibility = ViewStates.Gone;
                InSituCancel.Visibility = ViewStates.Visible;
            }
        }

        #region QR scanning - taken from RoleActivities.cs
        public static bool UseQRScanner { get { return !Res.AllowNfc; } }
        private MobileBarcodeScanner scanner;

        protected async void StartScanning()
        {
            scanner = new MobileBarcodeScanner() { UseCustomOverlay = true };

            var customOverlay = LayoutInflater.FromContext(this).Inflate(Resource.Layout.QRoverlay, null);

            customOverlay.FindViewById<ImageButton>(Resource.Id.qr_flashlight_button).Click += (ob, ev) =>
            {
                scanner.ToggleTorch();
            };
            customOverlay.FindViewById<ImageButton>(Resource.Id.qr_cancel_button).Click += (ob, ev) =>
            {
                scanner.Cancel();
            };

            scanner.CustomOverlay = customOverlay;

            var opts = new MobileBarcodeScanningOptions()
            { AutoRotate = true, PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE } };

            var result = await scanner.Scan(opts);

            HandleScanResult(result);
        }

        protected void HandleScanResult(ZXing.Result result)
        {
            string msg = "";

            if (result != null && !string.IsNullOrEmpty(result.Text))
                msg = "Found QR code: " + result.Text;
            else
                msg = "Scanning Canceled!";

            this.RunOnUiThread(() => Toast.MakeText(this, msg, ToastLength.Short).Show());

            if (msg != "Scanning Canceled!")
            {
                var tagCode = result.Text.Last().ToString();
                InfoPanel.RecognizeNode(SecurityPanel.CurrentPanel.Nodes.SingleOrDefault(n => n.Code == tagCode));
            }
        }

        //public static void RecognizeFakeNFC(string tagCode)
        //{
        //    if (BaseActivity.CurrentActivity is BypassActivity CurrentBypass)
        //    {
        //        CurrentBypass.InfoPanel.RecognizeNode(SecurityPanel.CurrentPanel.Nodes.SingleOrDefault(n => n.Code == tagCode));
        //        //CurrentBypass.soonestTapAllowed = DateTime.Now + CurrentBypass.minimumInterval;
        //    }
        //    else BaseActivity.CurrentToaster.RelayToast($"FakeNFC \"{tagCode}\" not valid outside of Bypass activity.");
        //}
        #endregion
    }
}