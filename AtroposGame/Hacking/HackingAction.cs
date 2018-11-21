using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using MiscUtil;

namespace Atropos.Hacking
{
    public class HackGesture
    {
        public int GestureIndex;
        public int IconID;

        public HackGesture(int gestIndex, int iconID)
        {
            GestureIndex = gestIndex;
            IconID = iconID;
        }

        //public static HackGesture DiagDownRight = new HackGesture(0, Resource.Drawable.diagonal_downright);
        //public static HackGesture DiagUpRight = new HackGesture(1, Resource.Drawable.diagonal_upright);
        //public static HackGesture DiagDownLeft = new HackGesture(2, Resource.Drawable.diagonal_downleft);
        //public static HackGesture DiagUpLeft = new HackGesture(3, Resource.Drawable.diagonal_upleft);
        
        //public static HackGesture DownThenRight = new HackGesture(0, Resource.Drawable.down_then_right);
        //public static HackGesture UpThenRight = new HackGesture(1, Resource.Drawable.up_then_right);
        //public static HackGesture DownThenLeft = new HackGesture(2, Resource.Drawable.down_then_left);
        //public static HackGesture UpThenLeft = new HackGesture(3, Resource.Drawable.up_then_left);
        //public static HackGesture RightThenDown = new HackGesture(4, Resource.Drawable.right_then_down);
        //public static HackGesture RightThenUp = new HackGesture(5, Resource.Drawable.right_then_up);
        //public static HackGesture LeftThenDown = new HackGesture(6, Resource.Drawable.left_then_down);
        //public static HackGesture LeftThenUp = new HackGesture(7, Resource.Drawable.left_then_up);
        //public static HackGesture SwipeRight = new HackGesture(8, Resource.Drawable.swipe_right);
        //public static HackGesture SwipeLeft = new HackGesture(9, Resource.Drawable.swipe_left);
        public static HackGesture Left = new HackGesture(0, Resource.Drawable.swipe_left);
        public static HackGesture Right = new HackGesture(1, Resource.Drawable.swipe_right);
        public static HackGesture Up = new HackGesture(2, Resource.Drawable.swipe_up);
        public static HackGesture Down = new HackGesture(3, Resource.Drawable.swipe_down);
        public static HackGesture Typing = new HackGesture(4, Resource.Drawable.typing_gesture_icon);
        public static HackGesture DoNothing = new HackGesture(-3, Resource.Drawable.do_nothing_gesture_icon);
        public static HackGesture None = new HackGesture(-4, Resource.Drawable.blank_gesture_icon);

        public static List<HackGesture> IndexedGestures = new List<HackGesture>() { Left, Right, Up, Down, Typing };

        public static bool operator ==(HackGesture first, HackGesture second) =>
            //(!Object.Equals(first, null) && !Object.Equals(second, null)) 
            //&& first.GestureIndex == second.GestureIndex 
            //&& first.IconID == second.IconID;
            Object.ReferenceEquals(first, second);
        public static bool operator !=(HackGesture first, HackGesture second) =>
            //(Object.Equals(first, null) || Object.Equals(second, null))
            //|| first.GestureIndex != second.GestureIndex 
            //|| first.IconID != second.IconID;
            !Object.ReferenceEquals(first, second);
    }

    //public class AvailabilityState
    //{
    //    public bool IsVisible;
    //    public bool IsKnownBlocked;

    //    public static AvailabilityState Available = new AvailabilityState() { IsVisible = true };
    //    public static AvailabilityState Unavailable = new AvailabilityState() { IsVisible = false };
    //    public static AvailabilityState KnownBlocked = new AvailabilityState() { IsVisible = true, IsKnownBlocked = true };

    //    public static AvailabilityState operator +(AvailabilityState first, AvailabilityState second)
    //    {
    //        return new AvailabilityState() { IsVisible = first.IsVisible && second.IsVisible, IsKnownBlocked = first.IsKnownBlocked || second.IsKnownBlocked };
    //    }
    //}

    //public class HackingCondition
    //{
    //    public virtual bool IsMet { get; set; }
    //    public Func<HackingAction, AvailabilityState> OnBeingMet;
    //    public Func<HackingAction, AvailabilityState> OnBeingUnmet;

    //    //public class CategoryCondition : HackingCondition
    //    //{
    //    //    //public HackingActionGroup Target;
    //    //    public HackingActionGroup Target { get => (HackingActionGroup)HackingActivity.Current.HackingActionsList.ActionsList[TargetName]; }
    //    //    public string TargetName;
    //    //    //public CategoryCondition(HackingActionGroup target)
    //    //    public CategoryCondition(string targetName)
    //    //    {
    //    //        //Target = target;
    //    //        TargetName = targetName;
    //    //    }
    //    //    public override bool IsMet
    //    //    {
    //    //        //get => HackingNavigation.ActionStack.Contains(Target);
    //    //        get => HackingNavigation.ActionStack.Any(h => h.Name == TargetName);
    //    //        set => throw new ArgumentException("Cannot set the status of a CategoryCondition directly!");
    //    //    }
    //    //}
    //    ////public static CategoryCondition Category(HackingActionGroup target) { return new CategoryCondition(target); }
    //    //public static CategoryCondition Category(string targetName) { return new CategoryCondition(targetName); }

    //    public class CountermeasureCondition : HackingCondition
    //    {
    //        public override bool IsMet
    //        {
    //            get => !Target.IsBlocking;
    //            set => Target.IsBlocking = !value;
    //        }
    //        public HackingAction Target { get => HackingActivity.Current.HackingActionsList.ActionsList[TargetName]; }
    //        public string TargetName;
    //        public HackingCondition Placeholder;
    //        //public CountermeasureCondition(HackingCondition placeholder, HackingAction target)
    //        public CountermeasureCondition(HackingCondition placeholder, string targetName)
    //        {
    //            Placeholder = placeholder;
    //            AllCountermeasures.Add(this);
    //            TargetName = targetName;
    //            OnBeingUnmet = (h) =>
    //            {
    //                if (Target.IsKnown) return AvailabilityState.KnownBlocked;
    //                else return AvailabilityState.Available;
    //            };
    //        }
    //    }
    //    public static List<CountermeasureCondition> AllCountermeasures = new List<CountermeasureCondition>();

    //    public static HackingCondition Firewall { get; set; } = new HackingCondition() { IsMet = true }; // Placeholder condition - see 
    //    public static HackingCondition Proxy { get; set; } = new HackingCondition() { IsMet = true }; // Placeholder condition

    //    public static HackingCondition ShellAccess = new HackingCondition();
    //    public static HackingCondition RootAccess = new HackingCondition();

    //    public class TriggeredCondition : HackingCondition
    //    {
    //        public DateTime FiredAt { get; set; }
    //        public TimeSpan DelayBeforeConsequence;
    //        public event EventHandler OnConsequence;
    //        public Action DoAsConsequence;
    //        private CancellationTokenSource _cts;
    //        public void Fire(TimeSpan? delay = null)
    //        {
    //            FiredAt = DateTime.Now;
    //            DelayBeforeConsequence = delay ?? DelayBeforeConsequence;
    //            _cts = new CancellationTokenSource();
    //            Task delayTask = Task.Delay(DelayBeforeConsequence, _cts.Token);
    //            delayTask.ContinueWith(t => 
    //            {
    //                OnConsequence?.Invoke(this, EventArgs.Empty);
    //                DoAsConsequence?.Invoke();
    //            }, 
    //            TaskContinuationOptions.OnlyOnRanToCompletion).ConfigureAwait(false);
    //        }
    //        public void UnFire()
    //        {
    //            _cts?.Cancel();
    //        }
    //    }

    //    public static HackingCondition ActiveTraceTriggered = new TriggeredCondition() { DoAsConsequence = () => { Speech.Say("You have been traced! Enemy ice is frying your deck now..."); } };
    //    public static HackingCondition PassiveTraceTriggered = new TriggeredCondition() { DoAsConsequence = () => { Speech.Say("You have been physically traced! Here comes 'I Got a Rock.'"); } };

    //    public static HackingCondition SysopOnline = new HackingCondition();
    //    public static HackingCondition SysopSuspicious = new HackingCondition();

    //    public static HackingCondition SilentAlarmSounded = new HackingCondition();
    //    public static HackingCondition AudibleAlarmSounded = new HackingCondition();        

    //    public class NegateCondition : HackingCondition
    //    {
    //        public HackingCondition Source;
    //        public NegateCondition(HackingCondition source) { Source = source; OnBeingMet = source.OnBeingMet; OnBeingUnmet = source.OnBeingUnmet; }
    //        public override bool IsMet { get => !Source.IsMet; set => Source.IsMet = !value; }
    //    }
    //    public static HackingCondition Not(HackingCondition source)
    //    {
    //        return new NegateCondition(source);
    //    }
    //}

    // Thus, given a "using static HackingCondition" directive, we can say something like:
    //
    //       HackingAction FalseTrail, BounceSignal, HackFirewall, HackProxy;
    //
    //       FalseTrail = new HackingAction(HackGesture.DiagDownLeft, "Establish False Trail", Not(ShellAccess)).Covers(BounceSignal);
    //       BounceSignal = new HackingAction(HackGesture.Typing, "Bounce Signal");
    //       HackFirewall = new HackingActionGroup(null, "Hack Firewall", Category(AttemptLogon)) { IsKnown = false };
    //       HackingCondition.Firewall = new CountermeasureCondition(HackFirewall);
    //       

    //public static class HackingNavigation
    //{
    //    private static HackingActionGroup _root;
    //    public static HackingActionGroup Root
    //    {
    //        get
    //        {
    //            if (_root == null) _root = new HackingActionGroup("Root", HackGesture.None, "", null);
    //            return _root;
    //        }
    //    }

    //    public static Stack<HackingAction> ActionStack = new Stack<HackingAction>(new HackingAction[] { Root });
    //    public static HackingAction CurrentAction { get => (ActionStack.Count > 0) ? ActionStack.Peek() : Root; } // Should never *be* zero, but you never know.
    //    public static int NumTotalActions { get; set; }
    //}

    public class HackingAction_New
    {
        public string Name;
        public HackGesture Gesture;
        public Action<HackingAction_New, HackingMapNode> OnExecute;

        public static HackingAction_New GoLeft = new HackingAction_New() { Name = "Go Left", Gesture = HackGesture.Left };
        public static HackingAction_New GoRight = new HackingAction_New() { Name = "Go Right", Gesture = HackGesture.Right };
        public static HackingAction_New GoUp = new HackingAction_New() { Name = "Go Up", Gesture = HackGesture.Up };
        public static HackingAction_New GoDown = new HackingAction_New() { Name = "Go Down", Gesture = HackGesture.Down };
    }

    //public class HackingAction
    //{
    //    public List<HackingCondition> Conditions;
    //    public AvailabilityState Availability
    //    {
    //        get
    //        {
    //            var result = AvailabilityState.Available;
    //            if (!IsKnown || Parent == null || !HackingNavigation.ActionStack.Contains(Parent)) return AvailabilityState.Unavailable;
    //            foreach (var cond in Conditions)
    //            {
    //                if (cond == null) continue;
    //                if (cond.IsMet) result += cond.OnBeingMet?.Invoke(this) ?? AvailabilityState.Available;
    //                else result += cond.OnBeingUnmet?.Invoke(this) ?? AvailabilityState.Unavailable;
    //            }
    //            return result;
    //        }
    //    }
    //    public string Name;
    //    public string Description;
    //    public virtual string DisplayDescription { get => Description; }
    //    protected HackGesture _gesture;
    //    protected HackGesture coverGesture;
    //    public HackGesture Gesture
    //    {
    //        get => (HackingNavigation.ActionStack.Contains(this) && !Object.ReferenceEquals(HackingNavigation.CurrentAction, this)) ? HackGesture.None
    //             : (coverGesture ?? _gesture);
    //        set => coverGesture = value;
    //    }
    //    public HackingActionGroup Parent;

    //    public HackingAction(string name, HackGesture gesture, string description, HackingActionGroup parent, params HackingCondition[] conditions)
    //    {
    //        Name = name;
    //        HackingActivity.Current.HackingActionsList.ActionsList.Add(Name, this);
    //        _gesture = gesture;
    //        Description = description;
    //        Conditions = conditions.ToList();
    //        //foreach (var cond in Conditions)
    //        //{
    //        //    if (cond is HackingCondition.CategoryCondition categ)
    //        //    {
    //        //        categ.Target.SubActions.Add(this);
    //        //        Parent = categ.Target;
    //        //    }
    //        //}
    //        Parent = parent;
    //        Parent?.SubActions.Add(this);

    //        //// Unless otherwise specified, if the gesture associated with this is "typing" then this means that you have to roll for success (see "Execute" below).
    //        //if (gesture == HackGesture.Typing) RollToExecute = true;
    //    }

    //    // Details which we need to know for all such items
    //    public bool IsBlocking = false;
    //    public bool IsKnown = true;
    //    public bool IsSelectable = true;

    //    public int NumSuccesses = 0;
    //    public int TgtSuccesses = 1;

    //    // Details which are only relevant for very specific ones
    //    public bool InitCompleted = false;
    //    public Action<HackingAction> OnInitialize;
    //    public Dictionary<string, object> Data = new Dictionary<string, object>();
    //    public object this[string key] { get => Data[key]; set => Data[key] = value; }
    //    public virtual void InitIfNecessary()
    //    {
    //        if (!InitCompleted)
    //        {
    //            OnInitialize?.Invoke(this);
    //            InitCompleted = true;
    //        }
    //    }

    //    public Action<HackingAction> OnSelect;
    //    public string OnSelectText;
    //    public event EventHandler OnSelectEvent;
    //    public virtual void Select()
    //    {
    //        InitIfNecessary();

    //        if (HackingNavigation.CurrentAction is HackingActionGroup hGroup && hGroup.SubActions.Contains(this))
    //        {
    //            HackingNavigation.ActionStack.Push(this);
    //        }
    //        else // Automatically back up until you're at this one's parent, then step inward to this one.
    //        {
    //            //HackingNavigation.CurrentAction?.Cancel(); // TODO: 
    //            while (HackingNavigation.CurrentAction != this.Parent) HackingNavigation.CurrentAction.Cancel();
    //            HackingNavigation.ActionStack.Push(this);
    //        }
    //        Speech.Say(OnSelectText, interrupt: true);
    //        OnSelect?.Invoke(this);
    //        OnSelectEvent?.Invoke(this, EventArgs.Empty);
    //    }
    //    public Action<HackingAction> OnExecute;
    //    public string OnExecuteText;
    //    public event EventHandler OnExecuteEvent;
    //    public virtual void Execute()
    //    {
    //        InitIfNecessary();

    //        // Do nothing more if task's success count already reached.
    //        if (NumSuccesses >= TgtSuccesses) return;

    //        // Also do nothing (at this point) if it turns out it's blocked by a prerequisite you didn't know about.
    //        foreach (var cond in Conditions)
    //        {
    //            if (cond is HackingCondition.CountermeasureCondition ccond && ccond.Target.IsBlocking)
    //            {
    //                ccond.Target.IsKnown = true;
    //                HackingNavigation.ActionStack.Pop();
    //                Speech.Say($"Blocked by countermeasure. {ccond.Target.Name}.");
    //                return;
    //            }
    //        }

    //        // If you get all the way here, you actually executed the action.
    //        HackingNavigation.NumTotalActions++;
    //        Speech.Say(OnExecuteText, interrupt: true);
    //        OnExecute?.Invoke(this);
    //        OnExecuteEvent?.Invoke(this, EventArgs.Empty);
    //        if (NumSuccesses == TgtSuccesses) SignalCompletion();
    //    }

    //    public event EventHandler OnCompletionEvent;
    //    public void SignalCompletion() { OnCompletionEvent?.Invoke(this, EventArgs.Empty); }

    //    public Action<HackingAction> OnCancel;
    //    public string OnCancelText;
    //    public event EventHandler OnCancelEvent;
    //    public virtual void Cancel()
    //    {
    //        HackingNavigation.ActionStack.Pop();

    //        Speech.Say(OnCancelText, interrupt: true);
    //        OnCancel?.Invoke(this);
    //        OnCancelEvent?.Invoke(this, EventArgs.Empty);

    //        //if (HackingNavigation.CurrentAction is HackingActionGroup hGroup && hGroup.SubActions.Contains(this) && hGroup.SubActionsSelectable.Count == 1)
    //        //{
    //        //    var parent = HackingNavigation.ActionStack.Pop();
    //        //    if (!String.IsNullOrWhiteSpace(parent.OnCancelText)) Speech.Say(parent.OnCancelText);
    //        //    parent.OnCancel?.Invoke(parent);
    //        //}
    //    }

    //    public static bool operator==(HackingAction first, HackingAction second)
    //    {
    //        //if (Object.Equals(first, null) || Object.Equals(second, null)) return false;
    //        //return first.Name == second.Name && first._gesture.GestureIndex == second._gesture.GestureIndex;
    //        return Object.ReferenceEquals(first, second);
    //    }
    //    public static bool operator !=(HackingAction first, HackingAction second)
    //    {
    //        //if (Object.Equals(first, null) || Object.Equals(second, null)) return true;
    //        //return first.Name != second.Name || first._gesture.GestureIndex != second._gesture.GestureIndex;
    //        return !Object.ReferenceEquals(first, second);
    //    }

    //    public static explicit operator string(HackingAction source) { return source.Name; }
    //    public static implicit operator HackingAction(string name)
    //    {
    //        if (!HackingActivity.Current.HackingActionsList.ActionsList.ContainsKey(name)) throw new ArgumentException($"Not found - hacking action '{name}'.");
    //        return HackingActivity.Current.HackingActionsList.ActionsList[name];
    //    }
    //}

    //public class HackingActionGroup : HackingAction
    //{
    //    public List<HackingAction> SubActions = new List<HackingAction>();
    //    public List<HackingAction> SubActionsVisible
    //    {
    //        get
    //        {
    //            var visibleActions = SubActions.Where(sA => sA.Availability.IsVisible).ToList();
    //            if (visibleActions.Count == 0) { NoEntriesAction.Parent = this; return new List<HackingAction>() { NoEntriesAction }; }
    //            else return visibleActions;
    //        }
    //    }
    //    public List<HackingAction> SubActionsSelectable
    //    {
    //        get
    //        {
    //            var selectableActions = SubActionsVisible.Where(sA => !sA.Availability.IsKnownBlocked).ToList();
    //            if (selectableActions.Count == 1 && selectableActions[0] == NoEntriesAction) return new List<HackingAction>();
    //            else return selectableActions;
    //        }
    //    }
    //    //public bool IsRootLevel = false;

    //    public HackingActionGroup(string name, HackGesture gesture, string description, HackingActionGroup parent, params HackingCondition[] conditions) 
    //        : base(name, gesture, description, parent, conditions) { }

    //    //public HackingActionGroup Covers(params HackingAction[] subActions)
    //    //{
    //    //    foreach (var sA in subActions)
    //    //    {
    //    //        SubActions.Add(sA);
    //    //        sA.Conditions.Add(HackingCondition.Category(this.Name));
    //    //    }
    //    //    return this;
    //    //}

    //    public override void Select()
    //    {
    //        base.Select();
    //        //var visibleActions = SubActions.Where(sA => sA.Availability.IsVisible && !sA.Availability.IsKnownBlocked);
    //        //if (SubActionsSelectable.Count() == 1)
    //        //{
    //        //    SubActionsSelectable.Single().Select();
    //        //}
    //    }
    //    public override void Execute() { } // *Executing* a category's selector icon does nothing.
    //    public override void Cancel()
    //    {
    //        NoEntriesAction.Parent = null; // Clear this if it got set internally.
    //        base.Cancel();
    //        //var visibleActions = SubActions.Where(sA => sA.Availability.IsVisible && !sA.Availability.IsKnownBlocked);
    //        //if (SubActionsSelectable.Count() == 1 && HackingNavigation.CurrentAction == this)
    //        //{
    //        //    HackingNavigation.ActionStack.Pop();
    //        //}
    //    }

    //    public static implicit operator HackingActionGroup(string name)
    //    {
    //        if (!HackingActivity.Current.HackingActionsList.ActionsList.ContainsKey(name)) throw new ArgumentException($"Not found - hacking action '{name}'.");
    //        return HackingActivity.Current.HackingActionsList.ActionsList[name] as HackingActionGroup;
    //    }

    //    //public static HackingAction NoEntriesAction(HackingActionGroup parent) => new HackingAction($"{parent.Name}_noEntries", HackGesture.None, "No Visible Entries", parent);
    //    public static HackingAction NoEntriesAction = new HackingAction($"NoEntries", HackGesture.None, "No Visible Entries", null);
    //}

    //public class HackingObstacle : HackingActionGroup
    //{
    //    public HackingObstacle(string name, HackGesture gesture, string description, HackingActionGroup parent, params HackingCondition[] conditions)
    //        : base(name, gesture, description, parent, conditions)
    //    {
    //        IsBlocking = true;
    //        IsKnown = false;
    //    }

    //    public override void InitIfNecessary()
    //    {
    //        if (!InitCompleted)
    //        {
    //            foreach (var sub in SubActions) sub.OnCompletionEvent += (o, e) => Satisfy();
    //        }
    //        base.InitIfNecessary();
    //    }

    //    public void Satisfy()
    //    {
    //        // Congrats! You did it!
    //        IsBlocking = false;
    //        IsKnown = false;
    //        Cancel();
    //    }
    //}

    //public class HackingItemSimple : HackingAction
    //{
    //    // Details which are only relevant for these
    //    public int NumExecuteClicks = 0;
    //    public double OddsPerClick = 3.0;
    //    public bool OddsAccumulate = true;

    //    public HackingItemSimple(string name, HackGesture gesture, string description, HackingActionGroup parent, params HackingCondition[] conditions)
    //        : base(name, gesture, description, parent, conditions) { }

    //    public HackGesture GestureDuringExecuteMode = HackGesture.Typing;
    //    public override void Select()
    //    {
    //        Gesture = GestureDuringExecuteMode;
    //        base.Select();
    //    }
    //    public override void Cancel()
    //    {
    //        Gesture = null;
    //        base.Cancel();
    //    }

    //    public override void Execute()
    //    {
    //        InitIfNecessary();

    //        // Often, with these, "execute" needs multiple taps.  If so, roll for success and do nothing if no success rolled.
    //        var odds = (OddsAccumulate) ? ++NumExecuteClicks * OddsPerClick : OddsPerClick;
    //        if (Res.Random > (odds / 100.0)) return;
    //        NumExecuteClicks = 0;

    //        base.Execute();
    //    }

    //    // Two levels of customizability... you can override just the N in the standard format "description (n/N)", or the whole thing.
    //    public override string DisplayDescription { get => $"{GenerateDisplayString.Invoke(this)}"; }
    //    public Func<HackingItemSimple, string> GenerateDisplayString = StdDisplayDescription;
    //    public static string StdDisplayDescription(HackingItemSimple h) => $"{h.Description} ({h.NumSuccesses}/{h.DisplayTgtSuccessess.Invoke(h)})";
    //    public Func<HackingItemSimple, string> DisplayTgtSuccessess = StandardTgtSuccesses;

    //    // Two common target-success resolution functions... just display the target outright, or display "?" until the target has been met.
    //    public static string StandardTgtSuccesses(HackingItemSimple h) => $"{h.TgtSuccesses}";
    //    public static string UnknownTgtSuccUntilMet(HackingItemSimple h)
    //    {
    //        if (h.NumSuccesses < h.TgtSuccesses) return $"?";
    //        else return $"{h.TgtSuccesses}";
    //    }
    //}

    //public class HackingIcebreaker : HackingActionGroup
    //{
    //    public HackingIcebreaker(string name, HackGesture gesture, string description, HackingActionGroup parent, params HackingCondition[] conditions)
    //        : base(name, gesture, description, parent, conditions)
    //    {
    //        OnCompletionEvent += (o, e) => _cts?.Cancel();
    //    }

    //    protected CancellationTokenSource _cts;
    //    public CancellationToken StopToken { get => _cts?.Token ?? CancellationToken.None; }
    //    public override void Cancel()
    //    {
    //        _cts?.Cancel();
    //        base.Cancel();
    //    }
    //    public override void Select()
    //    {
    //        _cts = new CancellationTokenSource();
    //        base.Select();
    //    }

    //    public class AccumulateTyping : HackingIcebreaker
    //    {
    //        private double EstimatedTgtSuccesses;
    //        public HackingItemSimple SubItem;

    //        /// <summary>
    //        /// Requires somewhere between 1 and (searchSpace1 * searchSpace2 * searchSpace3) successes to overcome.
    //        /// </summary>
    //        public AccumulateTyping(string name, HackGesture gesture, string baseDescription, string subItemDescription, int searchSpace1, int searchSpace2, int searchSpace3, HackingActionGroup parent, params HackingCondition[] conditions)
    //            : base(name, gesture, baseDescription, parent, conditions)
    //        {
    //            var ExtremeSearchRangeValues = new int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 3, 5, 10, 25 };
    //            TgtSuccesses = StaticRandom.Next(1, searchSpace1 + 1) * StaticRandom.Next(1, searchSpace2 + 1) * StaticRandom.Next(1, searchSpace3 + 1) * ExtremeSearchRangeValues.GetRandom();
    //            EstimatedTgtSuccesses = 0.125 * (searchSpace1 + 1) * (searchSpace2 + 1) * (searchSpace3 + 1); // The average result, more or less.

    //            // In this setup, SubItem basically functions as a proxy for executing the main AccumulateTyping action.
    //            SubItem = new HackingItemSimple($"{name}_subItem", HackGesture.Typing, subItemDescription, this)
    //            {
    //                IsSelectable = false,
    //                //OnExecuteText = "Generating believable trust certificate.",
    //                GenerateDisplayString = (h) => $"{subItemDescription}",
    //                OnExecute = (h) =>
    //                {
    //                    this.NumSuccesses++;
    //                    if (this.NumSuccesses == this.TgtSuccesses) h.NumSuccesses++; // Since h has TgtSuccesses = 1 (the default), this will cause it to fire OnCompletionEvent shortly hereafter.
    //                }
    //            };

    //            SubItem.OnCompletionEvent += (o, e) =>
    //            {
    //                // Congrats! You did it!
    //                this.Cancel();
    //                this.SignalCompletion();
    //            };
    //        }

    //        public override string DisplayDescription => $"{GenerateDisplayString()}";
    //        private string GenerateDisplayString() 
    //        {
    //            //var oddsOfSimpleFailure = Math.Pow(1.0 - OddsOfCrackingPerSuccess, NumSuccesses);
    //            //var simpleFailureIsToBlame = oddsOfSimpleFailure / (1.0 - OddsOfEverWorking + oddsOfSimpleFailure);
    //            //var oddsOfSuccess = OddsOfCrackingPerSuccess * simpleFailureIsToBlame;
    //            //return $"{Description} (~{(oddsOfSuccess * 100.0):f0}%)";
    //            var PlusAndMinuses = new string[] { "+", "++", "+++", "++", "+", "-", "--", "---" };
    //            var effectiveNumSuccesses = (int)Math.Floor(3 * NumSuccesses / EstimatedTgtSuccesses - 1).Clamp(0, PlusAndMinuses.Length - 1); // 3 because we want the peak to happen around "+++", which is third in the list.
    //            return $"{Description} ({PlusAndMinuses[effectiveNumSuccesses]})";
    //        }
    //    }

    //    public class DoOrDoNot : HackingIcebreaker
    //    {
    //        private string[] Prompts1, Prompts2;
    //        public HackingItemSimple SubItem1, SubItem2;
    //        public bool CurrentlyCueingItem1, CueResponseReceived;
    //        public TimeSpan AddedInterval = TimeSpan.FromMilliseconds(1500);

    //        private bool lastSuccess = true;
    //        private int consecutiveSuccesses;

    //        public DoOrDoNot(string name, HackGesture gesture, string baseDescription, string subItemDesc1, string subItemDesc2, string[] prompts1, string[] prompts2, HackingActionGroup parent, params HackingCondition[] conditions)
    //            : base(name, gesture, baseDescription, parent, conditions)
    //        {
    //            Prompts1 = prompts1;
    //            Prompts2 = prompts2;

    //            SubItem1 = new HackingItemSimple($"{name}_subItem1", HackGesture.Typing, subItemDesc1, this)
    //            {
    //                IsSelectable = false,
    //                //OnExecuteText = "Generating believable trust certificate.",
    //                GenerateDisplayString = (h) => $"{subItemDesc1}",
    //                OnExecute = (h) =>
    //                {
    //                    CueResponseReceived = true;
    //                    TallySuccess(CurrentlyCueingItem1);
    //                }
    //            };

    //            SubItem1.OnCompletionEvent += (o, e) =>
    //            {
    //                // Congrats! You did it!
    //                this.Cancel();
    //                this.SignalCompletion();
    //            };

    //            SubItem2 = new HackingItemSimple($"{name}_subItem2", HackGesture.DoNothing, subItemDesc2, this)
    //            {
    //                IsSelectable = false,
    //                //OnExecuteText = "Generating believable trust certificate.",
    //                GenerateDisplayString = (h) => $"{subItemDesc2}",
    //                OnExecute = (h) =>
    //                {
    //                    CueResponseReceived = true;
    //                    TallySuccess(!CurrentlyCueingItem1);
    //                }
    //            };

    //            SubItem2.OnCompletionEvent += (o, e) =>
    //            {
    //                // Congrats! You did it!
    //                this.Cancel();
    //                this.SignalCompletion();
    //            };

    //            OnCancelEvent += (o, e) =>
    //            {
    //                this.InitCompleted = false;
    //            };
    //        }

    //        public override void InitIfNecessary()
    //        {
    //            if (!InitCompleted)
    //            {
    //                DoLooping();
    //            }
    //            base.InitIfNecessary();
    //        }

    //        protected async void DoLooping()
    //        {
    //            while (!StopToken.IsCancellationRequested)
    //            {
    //                CueResponseReceived = false;

    //                // Pick a prompt from list #1 or list #2 (equal odds per entry)
    //                var i = StaticRandom.Next(Prompts1.Length + Prompts2.Length);
    //                string prompt;
    //                if (i < Prompts1.Length)
    //                {
    //                    CurrentlyCueingItem1 = true;
    //                    prompt = Prompts1[i];
    //                }
    //                else
    //                {
    //                    CurrentlyCueingItem1 = false;
    //                    prompt = Prompts2[i - Prompts1.Length];
    //                }

    //                // Give the prompt and also wait a short time.
    //                try
    //                {
    //                    await Speech.SayAllOf(prompt, cancelToken: StopToken);
    //                    await Task.Delay(AddedInterval, StopToken);
    //                }
    //                catch (TaskCanceledException)
    //                {
    //                    return;
    //                }

    //                // If we received a response, we're done for this iteration.
    //                if (CueResponseReceived) continue;
    //                // Otherwise, if one of our prompts was a Do Nothing gesture, count it as successfully "chosen" at this point.
    //                if (SubItem1.Gesture == HackGesture.DoNothing) TallySuccess(CurrentlyCueingItem1);
    //                else if (SubItem2.Gesture == HackGesture.DoNothing) TallySuccess(!CurrentlyCueingItem1);
    //            }
    //        }

    //        public void TallySuccess(bool success)
    //        {
    //            if (success)
    //            {
    //                NumSuccesses++;
    //                if (lastSuccess)
    //                {
    //                    if (consecutiveSuccesses < 2) consecutiveSuccesses++;
    //                }
    //                else
    //                {
    //                    consecutiveSuccesses = 0;
    //                }
    //            }
    //            else
    //            {
    //                NumSuccesses--;
    //                if (!lastSuccess)
    //                {
    //                    if (consecutiveSuccesses < 2) consecutiveSuccesses++;
    //                }
    //                else
    //                {
    //                    consecutiveSuccesses = 0;
    //                }
    //            }
    //            lastSuccess = success;
    //        }

    //        public override string DisplayDescription => $"{GenerateDisplayString()}";
    //        private string GenerateDisplayString()
    //        {
    //            var plusses = new string[] { "+", "++", "+++" };
    //            var minuses = new string[] { "-", "--", "---" };
    //            var signs = (lastSuccess) ? plusses : minuses;
    //            return $"{Description} ({signs[consecutiveSuccesses]})";
    //        }
    //    }

    //}
}