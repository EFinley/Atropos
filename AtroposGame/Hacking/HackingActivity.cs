
using Android.App;
using Android.Hardware;
using Android.OS;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
//using Accord.Math;
//using Accord.Statistics;
using System.Threading.Tasks;
using static System.Math;
using Log = Android.Util.Log; // Disambiguating with Math.Log( )... heh!
using Android.Animation;
using Atropos.Machine_Learning;
using Atropos.DataStructures;
using DKS = Atropos.DataStructures.DatapointSpecialVariants.DatapointKitchenSink;
using Android.Runtime;
using Android.Content.Res;
using System.IO;
using Atropos.Utilities;

namespace Atropos.Hacking
{
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait, WindowSoftInputMode = SoftInput.AdjustPan)]
    public class HackingActivity
        : MachineLearningActivity<DKS>
    {
        public const string HACKING = "Hacking"; // Used for filename of classifier to load
        private const string _tag = "Atropos|HackingActivity";

        public static new HackingActivity Current { get { return (HackingActivity)CurrentActivity; } set { CurrentActivity = value; } }

        #region Required inheritance members
        protected override void ResetStage(string label)
        {
            //CurrentStage = new MachineLearningStage(label, Dataset,
            //    new LoggingSensorProvider<DKS>(new AdvancedProviders.GrabItAllSensorProvider(new GravGyroOrientationProvider())));
            CurrentStage = new MachineLearningStage(label, Dataset, ContinuousLogger);
        }
        #endregion

        #region Data Members (and tightly linked functions / properties)
        protected bool _collectingData = false;
        protected bool _finalizingGesture = false;

        //protected HackTaskDisplayAdapter adapter;
        //public HackingActionsList HackingActionsList { get; set; }
        public HackingMap Map { get; set; }

        //public List<HackingAction> RootLevelActions = new List<HackingAction>();
        protected ListView listView;
        protected RelativeLayout mainPanel;

        protected System.Diagnostics.Stopwatch Stopwatch = new System.Diagnostics.Stopwatch();
        protected System.Diagnostics.Stopwatch SingleClickWatch = new System.Diagnostics.Stopwatch();
        //protected int SingleClickCount = 0;

        public virtual double GetAccelForDatapoint(DKS datapoint) { return datapoint.LinAccel.Length(); }
        #endregion

        #region Activity Lifecycle Methods
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.SimpleListPage);
            //FindAllViews();

            Dataset = new DataSet<DKS>();
            Classifier = new Classifier(); // Just in case; it's not actually going to get used in this version.

            var lView = FindViewById(Resource.Id.list);
            var mPanel = FindViewById(Resource.Id.listpage_backdrop);

            listView = lView as ListView;
            mainPanel = mPanel as RelativeLayout;

            //listView.ItemClick += ListView_ItemClick;

            //HackingActionsList = new HackingActionsList();
            //HackingActionsList.SetUpBasicNetwork();
            //HackingActionsList.SetUpFirewall();
            //HackingActionsList.RegisterAllActions();

            Map = new HackingMap();
            Map.SetUpDefaults();
            Map.SetupObjectiveChain("Root Access", "Security Server", "Suppress Cameras");
            Map.SetupFirewall();
            //Map.OnTransition += (o, e) => UpdateMapView();
            UpdateMapView();

            Speech.Say("Penetrating access point. Raising hud and gestural interface. Loading icebreakers. Scrambler countermeasures detected and partially neutralized.", SoundOptions.AtSpeed(2.0));

            useVolumeTrigger = true;
            //OnVolumeButtonPressed += (o, e) => PerformSpellSelection();
        }

        //private void ListView_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        //{
        //    var selectedAction = ((HackTaskDisplayAdapter) listView.Adapter)._items[e.Position];

        //    if (HackingNavigation.CurrentAction == null || !Object.ReferenceEquals(selectedAction, HackingNavigation.CurrentAction))
        //        selectedAction.Select();
        //    else selectedAction.Execute();

        //    UpdateListView();
        //}

        protected override async void OnResume()
        {
            await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: false);
            ContinuousLogger.TrueActivate(StopToken);
            LoadHackingClassifier();
            //InitHackingOpportunity();
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
                    if (!_collectingData && !_finalizingGesture)
                    {
                        _collectingData = true;

                        ResetStage("Hacking stage");
                        //SingleClickWatch.Restart();
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
                if (_collectingData && !_finalizingGesture)
                {
                    lock (SelectedGestureClass)
                    {
                        _collectingData = false;
                        _finalizingGesture = true;

                        // Halt the gesture-collection stage and query it.
                        Task.Run(async () =>
                        {
                            await Task.Delay(250);

                            var resultData = CurrentStage.StopAndReturnResults();
                            var resultSeq = new Sequence<DKS>() { SourcePath = resultData };
                            resultSeq.Metadata = CurrentStage.GetMetadata(GetAccelForDatapoint);
                            ResolveEndOfGesture(resultSeq);

                        });
                        return true; // Handled it, thanks.  
                    }
                }
            }
            return base.OnKeyUp(keyCode, e);
        }
        #endregion

        protected async void ResolveEndOfGesture(Sequence<DKS> resultSequence)
        {
            MostRecentSample = resultSequence;

            //string StageLabel = $"User gesture (#{Dataset.SequenceCount + 1})";

            if (Classifier == null) return;
            MostRecentSample = Analyze(MostRecentSample).Result;

            // If right or wrong, tweak the display properties of the sample.  This depends on the active mode.
            //DisplaySampleInfo(MostRecentSample);            

            //if (MostRecentSample.RecognizedAsIndex >= 0)
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
            //else
            //{
            //    Speech.Say("Gesture unrecognized.");
            //    return;
            //}

            await ActOnGestureIndex(MostRecentSample.RecognizedAsIndex);

            //_collectingData = false;
            _finalizingGesture = false;
        }

        public async Task ActOnGestureIndex(int gestureIndex)
        {
            if (gestureIndex < 0) return; // Means it wasn't recognized as any gesture at all.
            HackingAction_New chosenAction;

            try
            {
                //Func<HackingAction, bool> mpredicate = (HackingAction h) =>
                //{
                //    if (h == null) return false;
                //    if (!h.Availability.IsVisible) return false;
                //    if (h.Availability.IsKnownBlocked) return false;
                //    if (h.Gesture == null) return false;
                //    if (h.Gesture == HackGesture.None) return false;
                //    return true;
                //};
                //var ActionsWithGestures = HackingActionsList
                ////.AllActionsList
                //.ActionsList.Values
                ////.Where(h => h != null && h.Availability.IsVisible && !h.Availability.IsKnownBlocked && h.Gesture != null && h.Gesture != HackGesture.None)
                //.Where(mpredicate);
                //var ActionsByGesture = ActionsWithGestures.ToDictionary(h => h.Gesture.GestureIndex);
                //chosenAction = ActionsByGesture.GetValueOr(gestureIndex, null);
                //Log.Debug(_tag, $"Selected action: {chosenAction.Name}");
                var AvailableActions = new List<HackingAction_New>();
                if (Map.CurrentLeft != null) AvailableActions.Add(HackingAction_New.GoLeft);
                if (Map.CurrentRight != null) AvailableActions.Add(HackingAction_New.GoRight);
                if (Map.CurrentAbove != null) AvailableActions.Add(HackingAction_New.GoUp);
                if (Map.CurrentBelow != null) AvailableActions.Add(HackingAction_New.GoDown);
                AvailableActions.AddRange(Map.CurrentNode.Actions);
                var ActionsByGesture = AvailableActions.ToDictionary(h => h.Gesture.GestureIndex);
                chosenAction = ActionsByGesture.GetValueOr(gestureIndex, null);
                Log.Debug(_tag, $"Selected action: {chosenAction?.Name}");
            }
            catch (NullReferenceException)
            {
                Log.Debug(_tag, $"Null reference exception when trying to construct dictionary.");
                return;
            }

            //if (gestureIndex == HackGesture.RightThenUp.GestureIndex && HackingNavigation.CurrentAction != null)
            //{
            //    HackingNavigation.CurrentAction.Cancel();
            //    PulseImage(HackGesture.RightThenUp.IconID, MostRecentSample.Bitmap);
            //    UpdateListView();
            //    return;
            //}
            //if (chosenAction == null) { Log.Debug(_tag, $"Cannot find action with gesture index {gestureIndex}."); return; }


            //if (chosenAction.IsSelectable && (HackingNavigation.CurrentAction == null || !Object.ReferenceEquals(chosenAction, HackingNavigation.CurrentAction)))
            //{
            //    if (gestureIndex != HackGesture.Typing.GestureIndex) PulseImage(chosenAction.Gesture.IconID, MostRecentSample.Bitmap);
            //    else PulseImage(chosenAction.Gesture.IconID); // No bitmap to pulse
            //    chosenAction.Select();
            //}
            //else
            //{
            //    if (gestureIndex != HackGesture.Typing.GestureIndex) PulseImage(chosenAction.Gesture.IconID, MostRecentSample.Bitmap, 500);
            //    else PulseImage(chosenAction.Gesture.IconID, null, 350); // No bitmap to pulse
            //    chosenAction.Execute();
            //}

            //UpdateListView();

            PulseImage(HackGesture.IndexedGestures[gestureIndex].IconID, MostRecentSample.Bitmap);

            // Validate the gesture - are we really allowed to do what's just been requested?
            if (!Map.CurrentNode.ValidateGesture(HackGesture.IndexedGestures[gestureIndex]))
            {
                Map.CurrentNode.OnValidationFailed(HackGesture.IndexedGestures[gestureIndex]);
                return;
            }

            if (chosenAction == HackingAction_New.GoLeft && Map.CurrentLeft != null) await TransitionNode(HackingMapNode.Dir.Left);
            else if (chosenAction == HackingAction_New.GoRight && Map.CurrentRight != null)
            {
                if (!Map.CurrentNode.IsBlocking) await TransitionNode(HackingMapNode.Dir.Right);
                else Speech.Say("Blocked. Clear current ice first.");
            }
            else if (chosenAction == HackingAction_New.GoUp && Map.CurrentAbove != null) await TransitionNode(HackingMapNode.Dir.Above);
            else if (chosenAction == HackingAction_New.GoDown && Map.CurrentBelow != null) await TransitionNode(HackingMapNode.Dir.Below);
            else
            {
                Map.ExecutableActionsCounter++;
                chosenAction?.OnExecute?.Invoke(chosenAction, Map.CurrentNode);
                await FadeMapView();
                UpdateMapView();
            }
        }

        public async Task TransitionNode(HackingMapNode.Dir direction)
        {
            await FadeMapView();
            Map.RaiseTransition(direction);
            UpdateMapView();
        }

        public void PulseImage(int? resourceID = null, Android.Graphics.Bitmap bitmap = null, int duration_ms = 1000)
        {
            var NewPanel = new LinearLayout(this);
            NewPanel.LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
            NewPanel.Orientation = Android.Widget.Orientation.Vertical;
            NewPanel.Elevation = 10;
            NewPanel.SetGravity(GravityFlags.Center);
            NewPanel.ScaleX = 1.5f;
            NewPanel.ScaleY = 1.5f;
            //NewPanel.Alpha = 0;

            if (resourceID.HasValue)
            {
                var IconImage = new ImageView(this);
                IconImage.LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
                IconImage.SetImageResource(resourceID.Value);
                NewPanel.AddView(IconImage);
            }
            if (bitmap != null)
            {
                var BitmapImage = new ImageView(this);
                BitmapImage.LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
                BitmapImage.SetImageBitmap(bitmap);
                BitmapImage.ScaleX = 2.0f;
                BitmapImage.ScaleY = 2.0f;
                BitmapImage.TranslationY = 0.65f * bitmap.Height;
                NewPanel.AddView(BitmapImage);
            }

            RunOnUiThread(() =>
            {
                mainPanel.AddView(NewPanel);
                mainPanel.RequestLayout();
                mainPanel.Invalidate();

                var mPropertyAnimator = Android.Animation.ObjectAnimator.OfFloat(NewPanel, "alpha", 0);
                mPropertyAnimator.SetDuration(duration_ms);
                mPropertyAnimator.AnimationEnd += (o, e) =>
                {
                    mainPanel.RemoveView(NewPanel);
                };
                mPropertyAnimator.Start();
            });

            //Task.Delay(1000).Wait();

            //RunOnUiThread(() =>
            //{ 
            //    mainPanel.RemoveView(NewPanel);

            //});
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
            }
            return sequence;
        }        

        //private void InitHackingOpportunity()
        //{
        //    RootLevelActions.Clear();
        //    //RootLevelActions.AddRange(HackingActionsList.AllActionsList.Where(h => h != null && h is HackingActionGroup hGroup && hGroup.IsRootLevel));
        //    RootLevelActions = HackingNavigation.Root.SubActions;

        //    UpdateListView();
        //}

        //public void UpdateListView()
        //{
        //    RunOnUiThread(() =>
        //   {
        //       adapter = new HackTaskDisplayAdapter(this, RootLevelActions.Where(h => h != null && h.Availability.IsVisible).ToList());
        //       listView.Adapter = adapter;
        //   });
        //}
        
        private TextView SetUpMapNavTextView(string text, bool isVertical, bool isTopDown, params LayoutRules[] rules)
        {
            TextView result;
            if (!isVertical) result = new TextView(this);
            else
            {
                //result = new VerticalTextView(this);
                //((VerticalTextView)result).SetTopDown(isTopDown);
                result = new TextView(this);
                if (isTopDown) this.RotateText(result, Resource.Animation.rotatetext_right);
                else this.RotateText(result, Resource.Animation.rotatetext_left);
            }
            var layoutParameters = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
            foreach (var rule in rules) layoutParameters.AddRule(rule);
            result.LayoutParameters = layoutParameters;
            result.Text = " " + text + " ";
            result.TextSize = 40;
            result.SetTextColor(Android.Graphics.Color.WhiteSmoke);
            result.SetPadding(2, 7, 2, 7);
            result.SetBackgroundResource(Android.Resource.Drawable.AlertDarkFrame);
            result.Alpha = 0;
            result.Elevation = 20;
            mainPanel.AddView(result);
            var anim = ObjectAnimator.OfFloat(result, "alpha", 1);
            anim.SetDuration(1250);
            anim.Start();
            return result;
        }

        protected TextView topText, bottomText, rightText, leftText;
        protected LinearLayout panelContents;
        public void UpdateMapView()
        {
            RunOnUiThread(() =>
            {
                if (topText != null) mainPanel.RemoveView(topText);
                if (bottomText != null) mainPanel.RemoveView(bottomText);
                if (leftText != null) mainPanel.RemoveView(leftText);
                if (rightText != null) mainPanel.RemoveView(rightText);
                if (panelContents != null) mainPanel.RemoveView(panelContents);

                panelContents = new LinearLayout(this);
                panelContents.Orientation = Android.Widget.Orientation.Vertical;
                panelContents.SetGravity(GravityFlags.Center);
                mainPanel.SetGravity(GravityFlags.Center);
                var panelLayout = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
                panelLayout.AddRule(LayoutRules.CenterInParent);
                //panelLayout.TopMargin = panelLayout.BottomMargin = panelLayout.LeftMargin = panelLayout.RightMargin = 50;
                //panelContents.SetPadding(50, 50, 50, 50); // Doesn't work properly

                var label = new TextView(this);
                label.Text = Map.CurrentNode.Longname;
                label.TextSize = 40;
                panelContents.AddView(label);

                foreach (var action in Map.CurrentNode.Actions)
                {
                    var promptLabel = new LinearLayout(this);
                    promptLabel.Orientation = Android.Widget.Orientation.Horizontal;
                    promptLabel.LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
                    promptLabel.SetBackgroundResource(Android.Resource.Drawable.AlertDarkFrame);

                    var promptIcon = new ImageView(this);
                    promptIcon.SetBackgroundResource(action.Gesture.IconID);
                    promptIcon.ScaleX = promptIcon.ScaleY = 0.3f;
                    promptLabel.AddView(promptIcon);

                    var promptText = new TextView(this);
                    promptText.Text = action.Name + " ";
                    promptText.TextSize = 30;
                    promptLabel.AddView(promptText);

                    panelContents.AddView(promptLabel);
                }

                if (Map.CurrentAbove != null)
                {
                    topText = SetUpMapNavTextView(Map.CurrentAbove.Shortname, false, false, LayoutRules.AlignParentTop, LayoutRules.CenterHorizontal);
                    panelLayout.AddRule(LayoutRules.Below, topText.Id);
                }

                if (Map.CurrentBelow != null)
                {
                    bottomText = SetUpMapNavTextView(Map.CurrentBelow.Shortname, false, false, LayoutRules.AlignParentBottom, LayoutRules.CenterHorizontal);
                    panelLayout.AddRule(LayoutRules.Above, bottomText.Id);
                }

                if (Map.CurrentLeft != null)
                {
                    leftText = SetUpMapNavTextView(Map.CurrentLeft.Shortname, true, true, LayoutRules.AlignParentLeft, LayoutRules.CenterVertical);
                    panelLayout.AddRule(LayoutRules.RightOf, leftText.Id);
                }

                if (Map.CurrentRight != null)
                {
                    rightText = SetUpMapNavTextView(Map.CurrentRight.Shortname, true, false, LayoutRules.AlignParentRight, LayoutRules.CenterVertical);
                    if (Map.CurrentNode.IsBlocking) rightText.SetTextColor(Android.Graphics.Color.Red);
                    panelLayout.AddRule(LayoutRules.LeftOf, rightText.Id);
                }

                panelContents.LayoutParameters = panelLayout;
                mainPanel.AddView(panelContents);

            });
        }

        public void FadeMapNavTextView(View view, int duration = 250)
        {
            RunOnUiThread(() =>
            {
                var mPropertyAnimator = Android.Animation.ObjectAnimator.OfFloat(view, "alpha", 0);
                mPropertyAnimator.SetDuration(duration.Clamp(0, int.MaxValue));
                mPropertyAnimator.AnimationEnd += (o, e) =>
                {
                    mainPanel.RemoveView(view);
                    view = null;
                };
                mPropertyAnimator.Start();
            });
        }

        public async Task FadeMapView(int duration = 250)
        {
            if (Map.CurrentAbove != null) FadeMapNavTextView(topText, duration);
            if (Map.CurrentBelow != null) FadeMapNavTextView(bottomText, duration);
            if (Map.CurrentRight != null) FadeMapNavTextView(rightText, duration);
            if (Map.CurrentLeft != null) FadeMapNavTextView(leftText, duration);

            FadeMapNavTextView(panelContents, duration);
            await Task.Delay(duration);
        }
    }

    //public class HackTaskDisplayAdapter : BaseAdapter<HackingAction>
    //{
    //    private readonly Activity _context;
    //    public readonly List<HackingAction> _items;

    //    public HackTaskDisplayAdapter(Activity context, List<HackingAction> items)
    //        : base()
    //    {
    //        _context = context;
    //        _items = items;
    //    }

    //    public override long GetItemId(int position)
    //    {
    //        return position;
    //    }
    //    public override HackingAction this[int position]
    //    {
    //        get { return _items[position]; }
    //    }
    //    public override int Count
    //    {
    //        get { return _items.Count; }
    //    }

    //    protected LinearLayout.LayoutParams PercentLayout(double amt, bool horizontal = false)
    //    {
    //        if (!horizontal)
    //            return new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MatchParent, 0, (float)amt);
    //        else return new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MatchParent, (float)amt);
    //    }

    //    public override View GetView(int position, View convertView, ViewGroup parent)
    //    {
    //        var v = convertView;

    //        v = v ?? _context.LayoutInflater.Inflate(Resource.Layout.HackActionListItemRepresentation, null);

    //        HackingAction htask = _items[position];
    //        if (htask == null) return v;

    //        var namefield = v.FindViewById<TextView>(Resource.Id.htask_namefield);
    //        var iconview = v.FindViewById<ImageView>(Resource.Id.htask_gestureicon);
    //        var listview = v.FindViewById<ListView>(Resource.Id.htask_subtasks);

    //        namefield.Text = htask.DisplayDescription;
    //        iconview.SetImageResource(htask.Gesture.IconID);

    //        if (htask is HackingActionGroup hgroup && HackingNavigation.ActionStack.Contains(htask))
    //        {
    //            var adapter = new HackTaskDisplayAdapter(_context, hgroup.SubActions);
    //            listview.Adapter = adapter;
    //            listview.Visibility = ViewStates.Visible;
    //            listview.DisableScrolling();
    //        }
    //        else listview.Visibility = ViewStates.Gone;

    //        if (htask.Availability.IsVisible)
    //        {
    //            v.Visibility = ViewStates.Visible;
    //            if (htask.Availability.IsKnownBlocked)
    //            {
    //                v.SetBackgroundColor(Android.Graphics.Color.Argb(100, 255, 0, 0));
    //                iconview.SetImageResource(Resource.Drawable.blank_gesture_icon);
    //            }
    //            else v.SetBackgroundColor(Android.Graphics.Color.Argb(0, 0, 0, 0));
    //        }
    //        else
    //            v.Visibility = ViewStates.Gone;

    //        //htask.SuccessBar.Bar = v.FindViewById<LinearLayout>(Resource.Id.htask_success_bar);
    //        //htask.RiskBar.Bar = v.FindViewById<LinearLayout>(Resource.Id.htask_risk_bar);
    //        //htask.Graphic.GraphicsPane = v.FindViewById<RelativeLayout>(Resource.Id.htask_graphic);
    //        //htask.BackgroundView = v.FindViewById<View>(Resource.Id.htask_background);
    //        //var SymbolsPane = v.FindViewById<LinearLayout>(Resource.Id.htask_symbols);

    //        //var ctx = (HackingActivity)_context;
    //        //foreach (var Bar in new HackingBar[] { htask.SuccessBar, htask.RiskBar })
    //        //{
    //        //    var black_part = new View(_context) { LayoutParameters = PercentLayout(Bar.RemainingPercentage), Alpha = 0.1f };
    //        //    black_part.SetBackgroundColor(Bar.Colour);
    //        //    Bar.Bar.AddView(black_part);

    //        //    var upper_part = new View(_context) { LayoutParameters = PercentLayout(Bar.TopPercentage), Alpha = 0.45f };
    //        //    upper_part.SetBackgroundColor(Bar.Colour);
    //        //    Bar.Bar.AddView(upper_part);

    //        //    var lower_part = new View(_context) { LayoutParameters = PercentLayout(Bar.BottomPercentage) };
    //        //    lower_part.SetBackgroundColor(Bar.Colour);
    //        //    Bar.Bar.AddView(lower_part);
    //        //}

    //        //htask.Graphic.SetImageResource(_context, htask);

    //        //if (htask == ((HackingActivity)BaseActivity.CurrentActivity).CurrentTask)
    //        //{
    //        //    htask.BackgroundView.SetBackgroundColor(Android.Graphics.Color.CornflowerBlue);
    //        //    htask.BackgroundView.Alpha = 0.1f;

    //        //    if (htask.GestureA != null)
    //        //    {
    //        //        var symbolA = new ImageView(_context);
    //        //        symbolA.SetImageResource(htask.GestureA.IconID);
    //        //        SymbolsPane.AddView(symbolA);
    //        //    }
    //        //    if (htask.GestureB != null)
    //        //    {
    //        //        var symbolB = new ImageView(_context);
    //        //        symbolB.SetImageResource(htask.GestureB.IconID);
    //        //        SymbolsPane.AddView(symbolB);
    //        //    }
    //        //}
    //        //else
    //        //{
    //        //    if (htask.SelectionGesture != null)
    //        //    {
    //        //        var symbol = new ImageView(_context);
    //        //        symbol.SetImageResource(htask.SelectionGesture.IconID);
    //        //        SymbolsPane.AddView(symbol);
    //        //    }
    //        //}

    //        return v;
    //    }
    //}
}