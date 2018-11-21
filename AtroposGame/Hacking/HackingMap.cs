using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

using MiscUtil;
using Direction = Atropos.Hacking.HackingMapNode.Dir;

namespace Atropos.Hacking
{
    public class HackingMapNode
    {
        public enum Dir : int
        {
            Left,
            Right,
            Above,
            Below
        }

        public HackingMapNode(string shortname, string longname)
        {
            Shortname = shortname;
            Longname = longname;
            //Actions = actions?.ToList();
            Owner = HackingActivity.Current?.Map;
            //HackingActivity.Current?.Map?.AllNodes?.Add(this);
        }
        public HackingMapNode(string shortname) 
            : this(shortname, shortname) { }

        public HackingMap Owner { get; private set; }
        public List<HackingAction_New> Actions { get; set; } = new List<HackingAction_New>();
        public string Shortname { get; set; }
        public string Longname { get; set; }

        public Dictionary<Dir, HackingMapNode> AdjacentNodes = new Dictionary<Dir, HackingMapNode>() { { Dir.Left, null }, { Dir.Right, null }, { Dir.Above, null }, { Dir.Below, null } };
        public HackingMapNode ToLeft { get => AdjacentNodes[Dir.Left]; set => AdjacentNodes[Dir.Left] = value; }
        public HackingMapNode ToRight { get => AdjacentNodes[Dir.Right]; set => AdjacentNodes[Dir.Right] = value; }
        public HackingMapNode ToAbove { get => AdjacentNodes[Dir.Above]; set => AdjacentNodes[Dir.Above] = value; }
        public HackingMapNode ToBelow { get => AdjacentNodes[Dir.Below]; set => AdjacentNodes[Dir.Below] = value; }

        //public bool IsPresent = true;
        public bool IsHidden;
        public bool IsBlocking;
        //public string IsConstrainedBy;
        //public bool AllowedToContinue()
        //{
        //    if (!String.IsNullOrEmpty(IsConstrainedBy) && Owner.Nodes[IsConstrainedBy].IsBlocking)
        //    {
        //        Owner.Nodes[IsConstrainedBy].IsHidden = false;
        //        return false;
        //    }
        //    else return true;
        //}
        public Func<HackGesture, bool> ValidateGesture = (h) => true; // Default: Yes, you may, regardless of the gesture being requested.
        public Action<HackGesture> OnValidationFailed;

        public void IsRightOf(HackingMapNode other)
        {
            ToLeft = other;
            other.ToRight = this;
        }

        public void IsLeftOf(HackingMapNode other)
        {
            ToRight = other;
            other.ToLeft = this;
        }

        public virtual void IsAbove(params HackingMapNode[] others)
        {
            foreach (var other in others)
            {
                //ToBelow = other;
                other.ToAbove = this;
            }

            OnEntry += (o, e) => { ToBelow = Owner.PreviousNode; };
        }

        public virtual void IsBelow(params HackingMapNode[] others)
        {
            foreach (var other in others)
            {
                other.ToBelow = this;
            }

            OnEntry += (o, e) => { ToAbove = Owner.PreviousNode; };
        }

        // Setting up actions (aside from standard navigation commands)
        public void AddAction(string name, HackGesture gesture, Action<HackingAction_New, HackingMapNode> action)
        {
            Actions.Add(new HackingAction_New() { Name = name, Gesture = gesture, OnExecute = action });
        }

        public void RemoveAction(string name) { Actions.Remove(Actions.First(a => a.Name == name)); }
        public void RemoveAction(HackGesture gesture) { Actions.Remove(Actions.First(a => a.Gesture == gesture)); }

        // Triggered events upon navigating to, or away from, this node.
        public string OnEntrySay = "";
        public event EventHandler<EventArgs> OnEntry;
        public void Enter()
        {
            Counter_ThisAccess = 0;
            Speech.Say(OnEntrySay, new SoundOptions() { Speed = 1.2 + 0.05 * OnEntrySay.Length, Interrupt = true });
            OnEntry.Raise();
        }
        public event EventHandler OnExit;
        public void Exit()
        {
            OnExit.Raise();
        }

        // Generic counters and utility events (for when writing event handlers either for the special actions or for OnExit/OnEntry).
        public int Counter_ThisAccess;
        public int Counter_Persistent;
        public Dictionary<string, object> Data = new Dictionary<string, object>();
        //public event EventHandler<EventArgs<string>> OnUpdateUI; // For use (later) with graphical or similar UI features

        #region Equality checking
        public static bool operator ==(HackingMapNode first, HackingMapNode second)
        {
            if ((object)first == null || (object)second == null) return (object)first == (object)second;
            return first?.Shortname == second?.Shortname && first?.Longname == second?.Longname
                && (first?.AdjacentNodes?.Values?.Zip(second?.AdjacentNodes?.Values?.ToList() ?? new List<HackingMapNode>(), (f, s) => f?.Shortname == s?.Shortname).All(x => x) ?? false);
        }
        public static bool operator !=(HackingMapNode first, HackingMapNode second)
        {
            if ((object)first == null || (object)second == null) return (object)first != (object)second;
            return first?.Shortname != second?.Shortname || first?.Longname != second?.Longname
                || (first?.AdjacentNodes?.Values?.Zip(second?.AdjacentNodes?.Values?.ToList() ?? new List<HackingMapNode>(), (f, s) => f?.Shortname != s?.Shortname).Any(x => x) ?? true);
        }
        #endregion
    }

    //public class HackingICENode : HackingMapNode
    //{
    //    public HackingICENode(string shortname, string longname, params HackingAction[] actions) : base(shortname, longname, actions) { }
    //    public HackingICENode(string shortname, params HackingAction[] actions) : base(shortname, actions) { }


    //}

    public class HackingICEBreakerNode : HackingMapNode
    {
        public HackingICEBreakerNode(string shortname, string longname) : base(shortname, longname) { }
        public HackingICEBreakerNode(string shortname) : base(shortname) { }

        public HackingMapNode Unlocks { get; set; }
        public void DoUnlocking()
        {
            Unlocks.IsBlocking = false;
            Owner.UndoTransitionsTo(Unlocks);
        }
    }

    public struct HackingMapTransition
    {
        public HackingMapNode From;
        public HackingMapNode To;
        public Direction Direction;

        public HackingMapTransition Inverse()
        {
            var dir = (Direction == Direction.Above) ? Direction.Below :
                      (Direction == Direction.Below) ? Direction.Above :
                      (Direction == Direction.Right) ? Direction.Left : Direction.Right;
            return new HackingMapTransition() { From = this.To, To = this.From, Direction = dir };
        }

        #region Equality checking
        public static bool operator ==(HackingMapTransition first, HackingMapTransition second) => first.From == second.From && first.To == second.To && first.Direction == second.Direction;
        public static bool operator !=(HackingMapTransition first, HackingMapTransition second) => first.From != second.From || first.To != second.To || first.Direction != second.Direction;
        public override int GetHashCode() => From.GetHashCode() & To.GetHashCode() & Direction.GetHashCode();
        public override bool Equals(object obj) => obj is HackingMapTransition && (HackingMapTransition)obj == this;
        #endregion
    }

    public partial class HackingMap
    {
        private string _tag = "HackingMap";
        //public List<HackingMapNode> AllNodes = new List<HackingMapNode>();
        public Dictionary<string, HackingMapNode> Nodes = new Dictionary<string, HackingMapNode>()
        {
            { "Start", new HackingMapNode("Start") }
        };

        public HackingMap()
        {
        }

        public Stack<HackingMapTransition> TransitionHistory = new Stack<HackingMapTransition>();
        public HackingMapNode StartNode { get => Nodes["Start"]; set => Nodes["Start"] = value; }
        public HackingMapNode CurrentNode
        {
            get
            {
                //if (!IsInitialized) { Init?.Invoke(this); IsInitialized = true; }
                if (TransitionHistory.Count == 0) return StartNode;
                else return TransitionHistory.Peek().To;
            }
        }

        public HackingMapNode PreviousNode
        {
            get
            {
                if (TransitionHistory.Count == 0) return StartNode;
                else return TransitionHistory.Peek().From;
            }
        }

        //public Action<HackingMap> Init;
        private SoundOptions Interrupt = new SoundOptions() { Interrupt = true };
        public int ExecutableActionsCounter;

        private HackingMapNode VisibleAdjacent(HackingMapNode from, Direction dir)
        {
            if (from.AdjacentNodes[dir] == null) return null;
            else if (from.AdjacentNodes[dir].IsHidden) return VisibleAdjacent(from.AdjacentNodes[dir], dir);
            else return from.AdjacentNodes[dir];
        }
        public HackingMapNode CurrentLeft => VisibleAdjacent(CurrentNode, Direction.Left);
        public HackingMapNode CurrentRight => VisibleAdjacent(CurrentNode, Direction.Right);
        public HackingMapNode CurrentAbove => VisibleAdjacent(CurrentNode, Direction.Above);
        public HackingMapNode CurrentBelow => VisibleAdjacent(CurrentNode, Direction.Below);

        public event EventHandler<EventArgs<HackingMapTransition>> OnTransition;
        public event EventHandler<EventArgs<HackingMapNode>> OnObjectiveCompleted;

        public void RaiseTransition(Direction dir)
        {
            var destinationNode = VisibleAdjacent(CurrentNode, dir);
            if (destinationNode == null) { Log.Warn(_tag, $"Unable to move {dir} from {CurrentNode.Shortname}. Aborting transition."); return; }
            var transition = new HackingMapTransition() { From = CurrentNode, To = destinationNode, Direction = dir };
            RaiseTransition(transition);
        }
        public void InsertTransition(HackingMapTransition transition)
        {
            // Update transition history - in the special case where this is a reversal of the last one, simply delete it from the history, otherwise add it as the next transition.
            if (TransitionHistory.Count > 0 && transition == TransitionHistory.Peek().Inverse()) TransitionHistory.Pop();
            else
            {
                if (transition.From.AdjacentNodes[transition.Direction] == transition.To) // Simplest case - no hidden nodes
                    TransitionHistory.Push(transition);
                else // Break it up into two (or more, via recursion) steps and file them both in our transition path.
                {
                    var nextNode = transition.From.AdjacentNodes[transition.Direction];
                    InsertTransition(new HackingMapTransition() { From = transition.From, To = nextNode, Direction = transition.Direction });
                    InsertTransition(new HackingMapTransition() { From = nextNode, To = transition.To, Direction = transition.Direction });
                    Log.Debug(_tag, $"Breaking up transition from {transition.From.Shortname} to {transition.To.Shortname} with a step to {nextNode.Shortname}.");
                }
            }
        }
        public void RaiseTransition(HackingMapTransition transition)
        {
            InsertTransition(transition);

            // Respond to the transition
            transition.From.Exit();
            transition.To.Enter();
            OnTransition.Raise(transition);
        }
        public void UndoLastTransition()
        {
            if (TransitionHistory.Count == 0) return;
            RaiseTransition(TransitionHistory.Peek().Inverse());
        }
        public void UndoTransitionsTo(HackingMapNode node)
        {
            if (node == StartNode || TransitionHistory.Any(t => t.To == node))
            {
                while (CurrentNode != node) UndoLastTransition();
            }
            else
            {
                Log.Warn(_tag, $"Attempting to step back to node {node.Shortname}, but our history contains {TransitionHistory.Select(t => t.To).Join()}.");
            }
        }

        //public void UndoTransitionsToLeftOf(HackingMapNode node)
        //{
        //    //UndoTransitionsTo(node);
        //    //UndoLastTransition();
        //    while (TransitionHistory.Count > 0)
        //    {
        //        if (CurrentNode.ToRight == node) break;
        //        UndoLastTransition();
        //    }
        //}

    }    
}