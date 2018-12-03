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
using Log = Android.Util.Log;

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
            TransitionBy,
            Disconnect,
            Then
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
        public int SeqID;
        //public string Tag;
        public string SpeechString = "";
        public string SpeechOverride = null;
        //public NarrativeCondition Condition = NarrativeCondition.ToNext;
        public virtual async Task ExecuteNode()
        {
            // If speech has been overridden, use override, then clear the override.
            var speech = SpeechOverride ?? SpeechString;
            SpeechOverride = null;

            // If speech has alternatives, select one.
            var speechOptions = speech.Split('|');
            var selectedSpeech = speechOptions.GetRandom();

            await Speech.SayAllOf(selectedSpeech, interrupt: true);
        }
        public NarrativeNode NextNode { get; set; }

        //public NarrativeNode PreviousNode; // Won't be set during parsing, not until it's actually entered in play.
    }

    public class NarrativeWaitNode : NarrativeNode
    {
        public TimeSpan DelayLength;
        public override async Task ExecuteNode()
        {
            await Task.Delay(DelayLength);
            await base.ExecuteNode();
        }
    }

    public class NarrativeSetNode : NarrativeNode
    {
        public string Variable;
        public string Value;
        public override async Task ExecuteNode()
        {
            await base.ExecuteNode();
            NarrativeList.Current.KnownVariables[Variable] = Value;
        }
    }

    public class NarrativeDisconnectNode : NarrativeNode
    {
        public override async Task ExecuteNode()
        {
            await base.ExecuteNode();
            HackingActivity.Current.DoDisconnect();
        }
    }

    public class NarrativeTransitionNode : NarrativeNode
    {
        public string Tag;
        public override async Task ExecuteNode()
        {
            var originNode = NarrativeList.Current.ConditionTagged(Tag);
            string transitionPhr = "Error! Problem locating transition phrase.";
            if (originNode is NarrativeIceConditionNode iceNode)
            {
                transitionPhr = Wheel.GetTransitionPhraseFor(iceNode.DirectionChosen.Index);
            }
            SpeechOverride = transitionPhr + " " + SpeechString;
            await base.ExecuteNode();
        }
    }

    public class NarrativeEndNode : NarrativeNode
    {
        public string Tag;
    }

    public class NarrativeTagNode : NarrativeNode
    {
        public string Tag;
        public string Value;
    }

    public class NarrativeRetryNode : NarrativeNode
    {
        public string Tag;
        public override async Task ExecuteNode()
        {
            await Task.CompletedTask; // Suppress errors
            var target = NarrativeList.Current.ConditionTagged(Tag);
            target.SpeechOverride = (!String.IsNullOrEmpty(SpeechString)) ? SpeechString : null;
            NextNode = target;
        }
    }

    public class NarrativeConditionNode : NarrativeNode
    {
        public string Tag;
        //private string Result;
        //private AsyncAutoResetEvent ResultSubmitted = new AsyncAutoResetEvent();
        public Dictionary<string, NarrativeNode> Alternatives = new Dictionary<string, NarrativeNode>();
        //public virtual void SubmitResult(string result) { Result = result; ResultSubmitted.Set(); }
        //public override async Task ExecuteNode()
        //{
        //    await base.ExecuteNode();
        //    await ResultSubmitted.WaitAsync();
        //    NextNode = Alternatives[Result];
        //}
    }

    public class NarrativeLabelNode : NarrativeConditionNode
    {
        //public override async Task ExecuteNode()
        //{
        //    Alternatives["only"] = NextNode;
        //    SubmitResult("only");
        //    await base.ExecuteNode();
        //}
    }

    public class NarrativeCheckConditionNode : NarrativeConditionNode
    {
        public string CheckedVariable;
        public string CheckedCondition;
        public override async Task ExecuteNode()
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

            bool SelectTheYesOption = PredicateFunc(NarrativeList.Current.KnownVariables.GetValueOrDefault(CheckedVariable));
            NextNode = Alternatives[(SelectTheYesOption) ? "yes" : "no"];

            await base.ExecuteNode();
        }
    }

    public class NarrativeGestureConditionNode : NarrativeConditionNode
    {
        protected Gest? Result;
        protected AsyncAutoResetEvent ResultSubmitted = new AsyncAutoResetEvent();
        public virtual void SubmitResult(Gest result) { Result = result; ResultSubmitted.Set(); }
    }

    public class NarrativeCueConditionNode : NarrativeGestureConditionNode
    {
        public Gest TargetGesture;
        public bool? CueFollowed; // Null = not ready yet, true = gesture done, false = gesture not done (& time is up).

        public static Dictionary<Gest, int> SFXcueIDs = new Dictionary<Gest, int>()
        {
            { Gest.Left, Resource.Raw._175957_shing },
            { Gest.Right, Resource.Raw.fzazzle_magic }
        };

        public async override Task ExecuteNode()
        {
            HackingActivity.Current.BeginListeningFor(this);
            var sfxID = SFXcueIDs.GetValueOr(TargetGesture, Resource.Raw.kblaa_magic);
            var fx = new Effect($"{TargetGesture} cue", sfxID);

            // Wait for the duration of the verbiage (and also of the audio cue), plus a little more time if needed, before deciding if they skipped it.
            //await Task.WhenAll(base.ExecuteNode(), fx.PlayToCompletion());
            var SpeechTask = base.ExecuteNode();
            await fx.PlayToCompletion();
            if (Result == null) await Task.WhenAny(Task.Delay(5000), ResultSubmitted.WaitAsync());
            HackingActivity.Current.StopListening();

            if (Result == TargetGesture)
            {
                BaseActivity.CurrentToaster.RelayToast($"Correctly read as {Result}.  Option chosen, maximal insight gained.");
                CueFollowed = true;
            }
            else if (Result > Gest.Unknown)
            {
                CueFollowed = true;
                BaseActivity.CurrentToaster.RelayToast($"Read gesture not as {TargetGesture} but as {Result}.  Option chosen, but some insight lost.");
            }
            else
            {
                if (Result == Gest.Unknown)
                    BaseActivity.CurrentToaster.RelayToast("Gesture unrecognized; treating it as if nothing was entered.");
                CueFollowed = false;
                await SpeechTask;
            }

            NextNode = Alternatives[(CueFollowed.Value) ? "yes" : "no"];
        }
    }

    public class NarrativeIceConditionNode : NarrativeGestureConditionNode
    {
        // Parse-time characteristics
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

        public NarrativeIceConditionNode()
        {
            DifficultyMin = 25;
            DifficultyDelta = 50;
            RiskMin = 20;
            RiskDelta = 40;
            RewardMin = 20;
            RewardDelta = 40;
            TimeMin = 1500;
            TimeDelta = 2000;
        }

        public WheelDir DirectionChosen;
        protected Gest primaryGesture, secondaryGesture;

        public async override Task ExecuteNode()
        {
            HackingActivity.Current.BeginListeningFor(this);
            var fx = new Effect("ICE cu(b)e", Resource.Raw._349905_slowGlassShatter);

            await Task.WhenAll(fx.PlayToCompletion(), base.ExecuteNode(), UserChoosesDirection());

            var icebreaker = Wheel.GetIcebreakerFor(DirectionChosen.Index).Evaluate();
            await Speech.SayAllOf("Loading " + icebreaker.name);
            await Speech.SayAllOf(icebreaker.speechI);
            var succString = await Evaluate();

            await Speech.SayAllOf((succString == "success") ? icebreaker.speechS : icebreaker.speechF);
            NextNode = Alternatives[succString];
        }

        private async Task UserChoosesDirection()
        {
            await ResultSubmitted.WaitAsync();
            primaryGesture = Result.Value; // Should be guaranteed to have a value due to structure of SubmitResult().

            await ResultSubmitted.WaitAsync();
            secondaryGesture = Result.Value;

            DirectionChosen = Wheel.Directions
                                .FirstOrDefault(dir => dir.Primary == primaryGesture && dir.Secondary == secondaryGesture);
            if (DirectionChosen == null)
                DirectionChosen = Wheel.Directions
                                    .FirstOrDefault(dir => dir.Primary == primaryGesture && dir.Secondary == Gest.Click);
            if (DirectionChosen != null)
            {
                BaseActivity.CurrentToaster.RelayToast($"Received {primaryGesture} then {secondaryGesture}, implying {DirectionChosen.Descriptor}.");
            }
            else
            {
                Log.Debug("Atropos|ICEconditionNode", $"Found no match for gesture sequence {primaryGesture} // {secondaryGesture}. Using random.");
                DirectionChosen = Wheel.Directions.GetRandom();
            }
        }

        public async Task<string> Evaluate()
        {
            var successTgt = 0.01 * ((100 - DifficultyMin) - (DifficultyDelta * DirectionChosen.SuccessCoefficient));
            var success = Res.Random < successTgt;

            var time = TimeSpan.FromMilliseconds(TimeMin + TimeDelta * DirectionChosen.TimeCoefficient);
            await Task.Delay(time);

            var riskBaseline = RiskMin + RiskDelta * DirectionChosen.RiskCoefficient;
            var riskMoS = Math.Max(0, riskBaseline - Res.Random * 100.0);
            if (riskMoS > 0)
            {
                NarrativeList.Current.KnownVariables["Alert"] = (double.Parse(NarrativeList.Current.KnownVariables["Alert"]) + riskMoS).ToString("f1");
            }

            var rewardBaseline = RewardMin + RewardDelta * DirectionChosen.FortuneCoefficient;
            var rewardMoS = Math.Max(0, rewardBaseline - Res.Random * 100.0);
            if (rewardMoS > 0)
            {
                NarrativeList.Current.KnownVariables["Fortune"] = (double.Parse(NarrativeList.Current.KnownVariables["Fortune"]) + rewardMoS).ToString("f1");
            }

            return (success) ? "success" : "fail";
        }
    }

    //public class NarrativeCondition
    //{
    //    public NodeType Type { get => OriginNode.Type; }
    //    public string Tag { get => OriginNode.Tag; }
    //    public NarrativeNode OriginNode;
    //    public Dictionary<string, NarrativeNode> Alternatives = new Dictionary<string, NarrativeNode>();
    //    public virtual void BeginListening() { throw new NotImplementedException(); }
    //    public virtual Task<NarrativeNode> Evaluate() { throw new NotImplementedException(); }
    //    //public virtual void SubmitInfo(object input) { throw new NotImplementedException(); }

    //    // The default if not otherwise specified.
    //    public static NarrativeCondition ToNext = new NarrativeCondition();
    //}

    //public class LabellingCondition : NarrativeCondition
    //{
    //    public override void BeginListening() { }
    //    public override Task<NarrativeNode> Evaluate() { return Task.FromResult(OriginNode.GetNextNode("ignored")); }
    //}

    //public class CheckCondition : NarrativeCondition
    //{
    //    public override void BeginListening() { }
    //    public string CheckedTag;
    //    public string CheckedCondition;
    //    public NarrativeList TagStateSource;
    //    public override Task<NarrativeNode> Evaluate()
    //    {
    //        Func<string, bool> PredicateFunc;
    //        if (CheckedCondition.StartsWith(">") || CheckedCondition.StartsWith("<"))
    //        {
    //            if (double.TryParse(CheckedCondition.TrimStart('>', '<'), out double parsedValue))
    //            {
    //                PredicateFunc = (s) =>
    //                {
    //                    if (!double.TryParse(s, out double currentValue)) return false;
    //                    if (CheckedCondition[0] == '<') return currentValue < parsedValue;
    //                    else return currentValue > parsedValue;
    //                };
    //            }
    //            else throw new Exception($"Cannot parse condition '{CheckedCondition}'... greater-than or less-than have to go with a number, silly!");
    //        }
    //        else PredicateFunc = (s) => s.ToLower() == CheckedCondition.ToLower();

    //        bool SelectTheYesOption = PredicateFunc(TagStateSource.KnownVariables.GetValueOrDefault(CheckedTag));
    //        return Task.FromResult(Alternatives[(SelectTheYesOption) ? "yes" : "no"]);
    //    }
    //}

    //public class CueCondition : NarrativeCondition
    //{
    //    public Gest targetGesture;
    //    protected static IEffect GetGestureEffect(Gest gesture)
    //    {
    //        if (gesture == Gest.Left) return new Effect("Left", Resource.Raw._1470_clash);
    //        else if (gesture == Gest.Right) return new Effect("Right", Resource.Raw._175957_shing);
    //        else if (gesture == Gest.Up) return new Effect("Up", Resource.Raw.zwip_magic);
    //        else if (gesture == Gest.Down) return new Effect("Down", Resource.Raw._156140_dull_platic_or_metal_impact);
    //        else return new Effect("Click", Resource.Raw._288949_click);
    //    }

    //    public override void BeginListening()
    //    {
    //        GetGestureEffect(targetGesture).Play();
    //        HackingActivity.Current.BeginListeningFor(this);
    //    }

    //    private AsyncManualResetEvent cueFinishedSignal = new AsyncManualResetEvent();
    //    private bool userSignalledYes;
    //    public void SubmitResult(bool cueWasFollowed)
    //    {
    //        userSignalledYes = cueWasFollowed;
    //        cueFinishedSignal.Set();
    //    }

    //    public override async Task<NarrativeNode> Evaluate()
    //    {
    //        await cueFinishedSignal.WaitAsync();
    //        return Alternatives[(userSignalledYes) ? "yes" : "no"];
    //    }
    //}

    //public class IceCondition : NarrativeCondition
    //{
    //    private double[] coefficients = new double[8];
    //    public double DifficultyMin { get => coefficients[0]; set => coefficients[0] = value; }
    //    public double DifficultyDelta { get => coefficients[1]; set => coefficients[1] = value; }
    //    public double RiskMin { get => coefficients[2]; set => coefficients[2] = value; }
    //    public double RiskDelta { get => coefficients[3]; set => coefficients[3] = value; }
    //    public double RewardMin { get => coefficients[4]; set => coefficients[4] = value; }
    //    public double RewardDelta { get => coefficients[5]; set => coefficients[5] = value; }
    //    public double TimeMin { get => coefficients[6]; set => coefficients[6] = value; }
    //    public double TimeDelta { get => coefficients[7]; set => coefficients[7] = value; }

    //    public static string[] ArgumentNames = new string[] { "Diff", "Risk", "Reward", "Time" };

    //    public NarrativeList AlertAndFortuneSource;

    //    public IceCondition()
    //    {
    //        DifficultyMin = 30;
    //        DifficultyDelta = 40;
    //        RiskMin = 20;
    //        RiskDelta = 40;
    //        RewardMin = 20;
    //        RewardDelta = 40;
    //        TimeMin = 1500;
    //        TimeDelta = 2000;
    //    }

    //    public override void BeginListening()
    //    {
    //        new Effect("Ice prompt", Resource.Raw._349905_slowGlassShatter).Play();
    //        HackingActivity.Current.BeginListeningFor(this);
    //    }

    //    private AsyncManualResetEvent cueFinishedSignal = new AsyncManualResetEvent();
    //    private WheelDir styleDirection;
    //    public void SubmitResult(WheelDir chosenDirection)
    //    {
    //        styleDirection = chosenDirection;
    //        cueFinishedSignal.Set();
    //    }

    //    public override async Task<NarrativeNode> Evaluate()
    //    {
    //        await cueFinishedSignal.WaitAsync();

    //        var successTgt = 0.01 * ((100 - DifficultyMin) - (DifficultyDelta * styleDirection.SuccessCoefficient));
    //        var success = Res.Random < successTgt;

    //        var time = TimeSpan.FromMilliseconds(TimeMin + TimeDelta * styleDirection.TimeCoefficient);
    //        await Task.Delay(time);

    //        var riskBaseline = RiskMin + RiskDelta * styleDirection.RiskCoefficient;
    //        var riskMoS = Math.Max(0, riskBaseline - Res.Random * 100.0);
    //        if (riskMoS > 0)
    //        {
    //            AlertAndFortuneSource.KnownVariables["Alert"] = (double.Parse(AlertAndFortuneSource.KnownVariables["Alert"]) + riskMoS).ToString("f1");
    //        }

    //        var rewardBaseline = RewardMin + RewardDelta * styleDirection.FortuneCoefficient;
    //        var rewardMoS = Math.Max(0, rewardBaseline - Res.Random * 100.0);
    //        if (rewardMoS > 0)
    //        {
    //            AlertAndFortuneSource.KnownVariables["Fortune"] = (double.Parse(AlertAndFortuneSource.KnownVariables["Fortune"]) + rewardMoS).ToString("f1");
    //        }

    //        return Alternatives[(success) ? "success" : "fail"];
    //    }

    //    public void AssignArgument(int index, double min, double? delta)
    //    {
    //        if (delta.HasValue) // Two-argument version - specifies min and delta
    //        {
    //            coefficients[index * 2] = min;
    //            coefficients[index * 2 + 1] = delta.Value;
    //        }
    //        else // One-argument version - specifies mean, assumes default delta
    //        {
    //            coefficients[index * 2] = min - 0.5 * coefficients[index * 2 + 1];
    //        }
    //    }
    //}

    public class NarrativeList : List<NarrativeNode>
    {
        public static NarrativeList Current { get; set; }

        public class VariableAndState : EventArgs { public string Variable; public object State; }
        public event EventHandler<VariableAndState> OnVariableStateChange;

        public Dictionary<string, string> KnownVariables = new Dictionary<string, string>() { { "Alert", "0" }, { "Fortune", "0" } };

        public Dictionary<int, NarrativeNode> BySeqID => this.ToDictionary(nnode => nnode.SeqID);
        public NarrativeNode ConditionTagged(string tag)
        {
            return this.First(nnode =>
            {
                var nCond = nnode as NarrativeConditionNode;
                if (nCond == null) return false;
                return nCond.Tag == tag;
            });
        }
    }

    public class Narrative_Parser
    {
        private Stack<NarrativeConditionNode> NestedTagStructure = new Stack<NarrativeConditionNode>();
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

            // Trim initial open bracket ("split" on open bracket symbol, discard meaningless empty first element, keep only what comes after).
            //inputStr.TrimStart(OnOpenBracket);

            int seqCounter = 0;
            while (true)
            {
                if (String.IsNullOrEmpty(inputStr)) break;

                var currentStr = inputStr.Before(OnOpenBracket); // Should have the form "TypeOrTagName Tag: option, option, option]text|text
                inputStr = inputStr.After(OnOpenBracket); // Should have the form above, plus zero or more "[(same again)" on the end.

                if (currentStr == "") continue;

                // Parse out the speech string (always present, if sometimes empty)
                var speechString = currentStr.After(OnCloseBracket);
                currentStr = currentStr.Before(OnCloseBracket);

                PreviousNode = CurrentNode;

                var firstWord = currentStr.Before(OnSpaceOrColon);
                if (Enum.TryParse<NodeType>(firstWord, out NodeType nodeWord) && nodeWord != NodeType.Tag) // First word is the name of a node type.
                {
                    if (nodeWord.IsOneOf(NodeConsts.ConditionOpeners)) // To wit, "Begin", "Check", "Cue", or "Ice".
                    {
                        var nodeTag = currentStr.Split(OnSpaceOrColon)[1].ToLower(); // Second word - likely of more than two! - is therefore the tag.

                        if (NestedTagStructure.Any(ncond => ncond.Tag == nodeTag))
                            throw new Exception($"Error parsing input string - tag {nodeTag} already exists and cannot be reused.");

                        // Specific implementations for each type of condition node
                        NarrativeConditionNode curNode;
                        if (nodeWord == NodeType.Begin)
                            curNode = new NarrativeLabelNode();
                        else if (nodeWord == NodeType.Check)
                        {
                            var argument = currentStr.After(OnColon).Trim();
                            var varName = argument.Before(OnSpace); // Or should this be the part that's on colon, instead?  TBD.
                            var conditionValue = argument.After(OnSpace);

                            curNode = new NarrativeCheckConditionNode()
                            {
                                CheckedVariable = varName,
                                CheckedCondition = conditionValue
                            };
                        }
                        else if (nodeWord == NodeType.Cue)
                        {
                            var argument = currentStr.After(OnColon).Trim();
                            var argAsGest = (Gest)Enum.Parse(typeof(Gest), argument, ignoreCase: true);

                            curNode = new NarrativeCueConditionNode()
                            {
                                TargetGesture = argAsGest
                            };
                        }
                        else if (nodeWord == NodeType.Ice)
                        {
                            curNode = new NarrativeIceConditionNode();

                            var argumentStr = currentStr.After(OnColon).Trim();
                            var arguments = argumentStr.Split(',').Select(s => s.Trim());

                            foreach (var argument in arguments)
                            {
                                if (argument == "") continue;

                                var target = argument.Before(OnSpace);
                                var amount = argument.After(OnSpace);

                                // Which variable are they assigning?
                                var targetIndex = NarrativeIceConditionNode.ArgumentNames.ToList().IndexOf(target);
                                if (targetIndex == -1) throw new Exception($"Cannot parse argument '{target}' - target must be one of {NarrativeIceConditionNode.ArgumentNames.Join()}");

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
                                (curNode as NarrativeIceConditionNode).AssignArgument(targetIndex, firstAmount, secondAmount);
                            }
                        }
                        else throw new Exception(); // Should be impossible to hit this but flow control demands that it exist.

                        // In any of these cases, the current inmost-nested condition set is now this one, and the Tag has been set.
                        curNode.Tag = nodeTag;
                        NestedTagStructure.Push(curNode);
                        CurrentNode = curNode;
                    }
                    else if (nodeWord == NodeType.End)
                    {
                        var nodeTag = currentStr.After(OnSpaceOrColon).ToLower(); // Second word is therefore the tag.

                        if (NestedTagStructure.Peek().Tag != nodeTag)
                            throw new Exception($"Failure to parse input string - tag {nodeTag} is being closed out of sequence.");
                        NestedTagStructure.Pop();

                        CurrentNode = new NarrativeEndNode() { Tag = nodeTag };
                    }
                    else if (nodeWord == NodeType.Wait)
                    {
                        TimeSpan waitTime;
                        var secondWord = currentStr.After(OnSpaceOrColon).ToLower();
                        if (double.TryParse(secondWord, out double value)) waitTime = TimeSpan.FromMilliseconds(value);
                        else
                        {
                            int numBeats;
                            TimeSpan oneBeat = TimeSpan.FromMilliseconds(400);
                            if (secondWord == "one") numBeats = 1;
                            else if (secondWord == "two") numBeats = 2;
                            else if (secondWord == "three") numBeats = 3;
                            else if (secondWord == "four") numBeats = 4;
                            else throw new Exception($"Do not understand 'Wait {secondWord}' - use milliseconds instead.");
                            waitTime = oneBeat.MultipliedBy(numBeats);
                        }

                        CurrentNode = new NarrativeWaitNode() { DelayLength = waitTime };
                    }
                    else if (nodeWord == NodeType.Retry)
                    {
                        var nodeTag = currentStr.After(OnSpaceOrColon).ToLower(); // Second word is therefore the tag.

                        CurrentNode = new NarrativeRetryNode() { Tag = nodeTag };
                    }
                    else if (nodeWord == NodeType.TransitionBy)
                    {
                        var nodeTag = currentStr.After(OnSpaceOrColon).Replace("by ", "").ToLower(); // Second/third word is therefore the tag.

                        CurrentNode = new NarrativeTransitionNode() { Tag = nodeTag };
                    }
                    else if (nodeWord == NodeType.Set)
                    {
                        var argument = currentStr.After(OnSpace);
                        var variableName = argument.Before(OnSpaceOrColon);
                        var variableValue = argument.After(OnSpaceOrColon);

                        CurrentNode = new NarrativeSetNode() { Variable = variableName, Value = variableValue };
                    }
                    else if (nodeWord == NodeType.Disconnect)
                    {
                        CurrentNode = new NarrativeDisconnectNode();
                    }
                    else if (nodeWord == NodeType.Then)
                    {
                        CurrentNode = new NarrativeNode(); // Absolute plainest type possible, not worth deriving.  Basically ends up describing a brief pause and/or simply provides logical structure to the author.
                    }

                    // For all of these, the Type of node is simply the node word.  (Type is really only relevant for the Tag type, below.)
                    CurrentNode.Type = nodeWord;
                }
                else if (NestedTagStructure.Peek().Tag == firstWord.ToLower()) // First word is the name of our current most-deeply-nested condition; that's acceptable too.
                {
                    var targetValue = currentStr.After(OnColon).Trim().ToLower();

                    CurrentNode = new NarrativeTagNode() { Type = NodeType.Tag, Tag = firstWord.ToLower(), Value = targetValue };

                    NestedTagStructure.Peek().Alternatives.Add(targetValue, CurrentNode);
                }
                else throw new Exception($"Unable to parse '{currentStr}'!");

                // True for all of these is that the position in the sequence is a cumulative counter, they belong in the narrative list, and their speech string is the bit we parsed out way back at the top.
                CurrentNode.SeqID = seqCounter++;
                CurrentNarrativeList.Add(CurrentNode);
                CurrentNode.SpeechString = speechString;

            }

            // Go through and assign NextNode based on sequence position, with Tag alterna-nodes redirecting to their associated End node, and a nameless Disconnect node as the final one's NextNode.
            foreach (var node in CurrentNarrativeList)
            {
                if (node == CurrentNarrativeList.Last())
                    node.NextNode = new NarrativeDisconnectNode();
                else
                {
                    var naturalNextNode = CurrentNarrativeList[node.SeqID + 1];
                    if (naturalNextNode.Type != NodeType.Tag) node.NextNode = naturalNextNode;
                    else
                    {
                        var nodesTag = ((NarrativeTagNode)naturalNextNode).Tag;
                        node.NextNode = CurrentNarrativeList.First(n => n is NarrativeEndNode nEnd && nEnd.Tag == nodesTag);
                    }
                }
            }

            return CurrentNarrativeList;
        }
    }
}