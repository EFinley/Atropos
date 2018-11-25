using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using MiscUtil;
using Nito.AsyncEx;
using static Atropos.Hacking.Wheel.NodeConsts;

namespace Atropos.Hacking.Wheel
{
    public static class NodeConsts
    {
        public enum NodeType
        {
            Begin,
            Cue,
            Tag,
            End,
            Ice,
            Wait,
            Check,
            Set,
            Retry,
            TransitionBy
        }

        public static NodeType[] ConditionOpeners = new NodeType[] { NodeType.Begin, NodeType.Cue, NodeType.Ice, NodeType.Check };

        public static char[] OnOpenBracket = new char[] { '[' };
        public static char[] OnCloseBracket = new char[] { ']' };
        public static char[] OnColon = new char[] { ':' };
        public static char[] OnSpace = new char[] { ' ' };
        public static char[] OnSpaceOrColon = new char[] { ' ', ':' };

        public static string OPENBRACKET = "[";
        public static string CLOSEBRACKET = "]";
        public static string COLON = ":";
        public static string SPACE = " ";
    }

    public class NarrativeNode
    {
        public NodeType Type;
        public Guid Guid;
        public string Tag;
        public string Speech;
        public NarrativeCondition Condition = NarrativeCondition.ToNext;
        public Func<Task> ExecuteNode;
        public Func<string, NarrativeNode> GetNextNode;

        public NarrativeNode PreviousNode; // Won't be set during parsing, not until it's actually entered in play.
    }

    public class NarrativeCondition
    {
        public NodeType Type { get => OriginNode.Type; }
        public string Tag { get => OriginNode.Tag; }
        public NarrativeNode OriginNode;
        public Dictionary<string, NarrativeNode> Alternatives = new Dictionary<string, NarrativeNode>();
        public virtual void BeginListening() { throw new NotImplementedException(); }
        public virtual Task<NarrativeNode> Evaluate() { throw new NotImplementedException(); }
        //public virtual void SubmitInfo(object input) { throw new NotImplementedException(); }

        // The default if not otherwise specified.
        public static NarrativeCondition ToNext = new NarrativeCondition();
    }

    public class LabellingCondition : NarrativeCondition
    {
        public override void BeginListening() { }
        public override Task<NarrativeNode> Evaluate() { return Task.FromResult(OriginNode.GetNextNode("ignored")); }
    }

    public class CheckCondition : NarrativeCondition
    {
        public override void BeginListening() { }
        public string CheckedTag;
        public string CheckedCondition;
        public NarrativeList TagStateSource;
        public override Task<NarrativeNode> Evaluate()
        {
            Func<string, bool> PredicateFunc;
            if (CheckedCondition.StartsWith(">") || CheckedCondition.StartsWith("<"))
            {
                if (double.TryParse(CheckedCondition.TrimStart('>', '<'), out double parsedValue))
                {
                    PredicateFunc = (s) =>
                    {
                        if (!double.TryParse(s, out double currentValue)) return false;
                        if (CheckedCondition[0] == '<') return currentValue < parsedValue;
                        else return currentValue > parsedValue;
                    };
                }
                else throw new Exception($"Cannot parse condition '{CheckedCondition}'... greater-than or less-than have to go with a number, silly!");
            }
            else PredicateFunc = (s) => s.ToLower() == CheckedCondition.ToLower();

            bool SelectTheYesOption = PredicateFunc(TagStateSource.KnownTags.GetValueOrDefault(CheckedTag));
            return Task.FromResult(Alternatives[(SelectTheYesOption) ? "yes" : "no"]);
        }
    }

    public class CueCondition : NarrativeCondition
    {
        public Gest targetGesture;
        protected static IEffect GetGestureEffect(Gest gesture)
        {
            if (gesture == Gest.Left) return new Effect("Left", Resource.Raw._1470_clash);
            else if (gesture == Gest.Right) return new Effect("Right", Resource.Raw._175957_shing);
            else if (gesture == Gest.Up) return new Effect("Up", Resource.Raw.zwip_magic);
            else if (gesture == Gest.Down) return new Effect("Down", Resource.Raw._156140_dull_platic_or_metal_impact);
            else return new Effect("Click", Resource.Raw._288949_click);
        }

        public override void BeginListening()
        {
            GetGestureEffect(targetGesture).Play();
            HackingActivity.Current.BeginListeningFor(this);
        }

        private AsyncManualResetEvent cueFinishedSignal = new AsyncManualResetEvent();
        private bool userSignalledYes;
        public void SubmitResult(bool cueWasFollowed)
        {
            userSignalledYes = cueWasFollowed;
            cueFinishedSignal.Set();
        }

        public override async Task<NarrativeNode> Evaluate()
        {
            await cueFinishedSignal.WaitAsync();
            return Alternatives[(userSignalledYes) ? "yes" : "no"];
        }
    }

    public class IceCondition : NarrativeCondition
    {
        private double[] coefficients = new double[8];
        public double DifficultyMin { get => coefficients[0]; set => coefficients[0] = value; }
        public double DifficultyDelta { get => coefficients[1]; set => coefficients[1] = value; }
        public double RiskMin { get => coefficients[2]; set => coefficients[2] = value; }
        public double RiskDelta { get => coefficients[3]; set => coefficients[3] = value; }
        public double RewardMin { get => coefficients[4]; set => coefficients[4] = value; }
        public double RewardDelta { get => coefficients[5]; set => coefficients[5] = value; }
        public double TimeMin { get => coefficients[6]; set => coefficients[6] = value; }
        public double TimeDelta { get => coefficients[7]; set => coefficients[7] = value; }

        public static string[] ArgumentNames = new string[] { "Diff", "Risk", "Reward", "Time" };

        public NarrativeList AlertAndFortuneSource;

        public IceCondition()
        {
            DifficultyMin = 30;
            DifficultyDelta = 40;
            RiskMin = 20;
            RiskDelta = 40;
            RewardMin = 20;
            RewardDelta = 40;
            TimeMin = 1500;
            TimeDelta = 2000;
        }

        public override void BeginListening()
        {
            new Effect("Ice prompt", Resource.Raw._349905_slowGlassShatter).Play();
            HackingActivity.Current.BeginListeningFor(this);
        }

        private AsyncManualResetEvent cueFinishedSignal = new AsyncManualResetEvent();
        private WheelDir styleDirection;
        public void SubmitResult(WheelDir chosenDirection)
        {
            styleDirection = chosenDirection;
            cueFinishedSignal.Set();
        }

        public override async Task<NarrativeNode> Evaluate()
        {
            await cueFinishedSignal.WaitAsync();

            var successTgt = 0.01 * ((100 - DifficultyMin) - (DifficultyDelta * styleDirection.SuccessCoefficient));
            var success = Res.Random < successTgt;

            var time = TimeSpan.FromMilliseconds(TimeMin + TimeDelta * styleDirection.TimeCoefficient);
            await Task.Delay(time);

            var riskBaseline = RiskMin + RiskDelta * styleDirection.RiskCoefficient;
            var riskMoS = Math.Max(0, riskBaseline - Res.Random * 100.0);
            if (riskMoS > 0)
            {
                AlertAndFortuneSource.KnownTags["Alert"] = (double.Parse(AlertAndFortuneSource.KnownTags["Alert"]) + riskMoS).ToString("f1");
            }

            var rewardBaseline = RewardMin + RewardDelta * styleDirection.FortuneCoefficient;
            var rewardMoS = Math.Max(0, rewardBaseline - Res.Random * 100.0);
            if (rewardMoS > 0)
            {
                AlertAndFortuneSource.KnownTags["Fortune"] = (double.Parse(AlertAndFortuneSource.KnownTags["Fortune"]) + rewardMoS).ToString("f1");
            }

            return Alternatives[(success) ? "success" : "fail"];
        }

        public void AssignArgument(int index, double min, double? delta)
        {
            if (delta.HasValue) // Two-argument version - specifies min and delta
            {
                coefficients[index * 2] = min;
                coefficients[index * 2 + 1] = delta.Value;
            }
            else // One-argument version - specifies mean, assumes default delta
            {
                coefficients[index * 2] = min - 0.5 * coefficients[index * 2 + 1];
            }
        }
    }

    public class NarrativeList : List<NarrativeNode>
    {
        public class TagAndState : EventArgs { public string Tag; public object State; }
        public event EventHandler<TagAndState> OnTagStateChange;

        public Dictionary<string, string> KnownTags = new Dictionary<string, string>() { { "Alert", "0" }, { "Fortune", "0" } };

        public Dictionary<Guid, NarrativeNode> ByGuid => this.ToDictionary(nnode => nnode.Guid);
    }

    public class Narrative_Parser
    {
        private List<NarrativeCondition> NestedTagStructure = new List<NarrativeCondition>();
        private NarrativeNode CurrentNode, PreviousNode;
        public NarrativeList CurrentNarrativeList;

        public NarrativeList Parse(string inputStr)
        {
            CurrentNarrativeList = new NarrativeList();

            // Automatically add external block statements enclosing whole thing if not present.
            if (!inputStr.StartsWith($"{OPENBRACKET}{NodeType.Begin}"))
            {
                inputStr = $"{OPENBRACKET}{NodeType.Begin} ALL_CONTENT{CLOSEBRACKET}" + inputStr + $"{OPENBRACKET}{NodeType.End} ALL_CONTENT{CLOSEBRACKET}";
            }

            inputStr.TrimStart(OnOpenBracket);

            while (inputStr.Contains(OPENBRACKET))
            {
                var splitStr = inputStr.Split(OnOpenBracket, 2);
                var currentStr = splitStr[0]; // Should have the form "TypeOrTagName Tag: option, option, option]text|text
                inputStr = splitStr[1]; // Should have the form above, plus zero or more "[(same again)" on the end.

                PreviousNode = CurrentNode;
                CurrentNode = new NarrativeNode() { Guid = Guid.NewGuid() };
                if (PreviousNode.Condition == NarrativeCondition.ToNext) PreviousNode.GetNextNode = (s) => CurrentNode;

                // Parse out the speech string (always present, if sometimes empty)
                CurrentNode.Speech = currentStr.Split(OnCloseBracket)[1];
                currentStr = currentStr.Split(OnCloseBracket)[0];

                var firstWord = currentStr.Split(OnSpaceOrColon)[0];
                if (Enum.TryParse<NodeType>(firstWord, out NodeType nodeWord)) // First word is the name of a node type.
                {
                    CurrentNode.Type = nodeWord;
                    if (CurrentNode.Type.IsOneOf(NodeConsts.ConditionOpeners)) // To wit, "Begin", "Check", "Cue", or "Ice".
                    {
                        CurrentNode.Tag = currentStr.Split(OnSpaceOrColon)[1].ToLower(); // Second word is therefore the tag.

                        //CurrentNarrativeList.KnownTags.Add(CurrentNode.Tag);
                        if (NestedTagStructure.Any(ncond => ncond.Tag == CurrentNode.Tag))
                            throw new Exception($"Error parsing input string - tag {CurrentNode.Tag} already exists and cannot be reused.");

                        //CurrentNode.Condition = new NarrativeCondition() // TODO - break this out into individual subclasses!
                        //{
                        //    OriginNode = CurrentNode
                        //};
                        if (CurrentNode.Type == NodeType.Begin)
                            CurrentNode.Condition = new LabellingCondition() { OriginNode = CurrentNode };
                        else if (CurrentNode.Type == NodeType.Check)
                        {
                            var argument = currentStr.Split(OnColon)[1].Trim();
                            var tagName = argument.Split(OnSpace, 2)[0]; // Or should this be the part that's on colon, instead?  TBD.
                            var conditionValue = argument.Split(OnSpace, 2)[1];

                            CurrentNode.Condition = new CheckCondition()
                            {
                                OriginNode = CurrentNode,
                                CheckedTag = tagName,
                                CheckedCondition = conditionValue,
                                TagStateSource = CurrentNarrativeList
                            };
                        }
                        else if (CurrentNode.Type == NodeType.Cue)
                        {
                            var argument = currentStr.Split(OnColon)[1].Trim();
                            var argAsGest = (Gest)Enum.Parse(typeof(Gest), argument, ignoreCase: true);

                            CurrentNode.Condition = new CueCondition()
                            {
                                OriginNode = CurrentNode,
                                targetGesture = argAsGest
                            };
                        }
                        else if (CurrentNode.Type == NodeType.Ice)
                        {
                            var Ice = new IceCondition()
                            {
                                OriginNode = CurrentNode,
                                AlertAndFortuneSource = CurrentNarrativeList
                            };

                            var argumentStr = currentStr.Split(OnColon)[1].Trim();
                            var arguments = argumentStr.Split(',').Select(s => s.Trim());

                            foreach (var argument in arguments)
                            {
                                var target = argument.Split(OnSpace, 2)[0];
                                var amount = argument.Split(OnSpace, 2)[1];

                                // Which variable are they assigning?
                                var targetIndex = IceCondition.ArgumentNames.ToList().IndexOf(target);
                                if (targetIndex == -1) throw new Exception($"Cannot parse argument '{target}' - target must be one of {IceCondition.ArgumentNames.Join()}");

                                // What are they assigning it to be?  Is it a range (min-max), or a single value (the mean)?
                                double firstAmount;
                                double? secondAmount;
                                try
                                {
                                    if (amount.Contains("-"))
                                    {
                                        firstAmount = double.Parse(amount.Split('-')[0]);
                                        var amountMax = double.Parse(amount.Split('-')[1]);
                                        secondAmount = amountMax - firstAmount;
                                    }
                                    else
                                    {
                                        firstAmount = double.Parse(amount);
                                        secondAmount = null;
                                    }
                                }
                                catch (Exception)
                                {
                                    throw new Exception($"Cannot parse amount '{amount}' - specify either a single value, or a range as 'min-max'.");
                                }

                                // Having parsed that assignment, make it so.
                                Ice.AssignArgument(targetIndex, firstAmount, secondAmount);
                            }

                            CurrentNode.Condition = Ice;
                        }

                        // In any of these cases, the current inmost-nested condition set is now this one.
                        NestedTagStructure.Add(CurrentNode.Condition);
                    }
                    else if (CurrentNode.Type == NodeType.End)
                    {
                        CurrentNode.Tag = currentStr.Split(OnSpaceOrColon)[1].ToLower(); // Second word is therefore the tag.

                        if (NestedTagStructure.Last().Tag != CurrentNode.Tag)
                            throw new Exception($"Failure to parse input string - tag {CurrentNode.Tag} is being closed out of sequence.");
                        NestedTagStructure.Remove(NestedTagStructure.Last());
                    }
                    else if (CurrentNode.Type == NodeType.Wait)
                    {
                        TimeSpan waitTime;
                        var secondWord = currentStr.Split(OnSpaceOrColon)[1].ToLower();
                        if (double.TryParse(secondWord, out double value)) waitTime = TimeSpan.FromMilliseconds(value);
                        else
                        {
                            int numBeats;
                            TimeSpan oneBeat = TimeSpan.FromMilliseconds(500);
                            if (secondWord == "one") numBeats = 1;
                            else if (secondWord == "two") numBeats = 2;
                            else if (secondWord == "three") numBeats = 3;
                            else if (secondWord == "four") numBeats = 4;
                            else throw new Exception($"Do not understand 'Wait {secondWord}' - use milliseconds instead.");
                            waitTime = oneBeat.MultipliedBy(numBeats);
                        }
                        CurrentNode.ExecuteNode = () => Task.Delay(waitTime);
                    }
                }
                else if (NestedTagStructure.Last().Tag == firstWord) // First word is the name of our current most-deeply-nested condition; that's acceptable too.
                {
                    CurrentNode.Type = NodeType.Tag;
                    CurrentNode.Tag = firstWord.ToLower();
                    var targetValue = currentStr.Split(OnColon)[1].Trim().ToLower();
                    NestedTagStructure.Last().Alternatives.Add(targetValue, CurrentNode);
                }
            }

            return CurrentNarrativeList;
        }
    }
}