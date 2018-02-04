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

using System.Threading;
using System.Threading.Tasks;
using Atropos.Locks;
using System.Numerics;
using MiscUtil;

namespace Atropos.Encounters
{
    public partial class Scenario
    {
        public enum State
        {
            InitialState,
            InProgress,
            PartialSuccess,
            Success,
            Failed,
            Obtained,
            Lost,
            Hidden,
            Detected,
            True,
            False,
            NoneOfTheAbove,
            Hitter,
            Hacker,
            Sorceror,
            Spy,
            Locked,
            Unlocked,
            Checked
        }

        public static Scenario Current { get; set; }
        public string Name { get; set; }
        public string Prefix { get { return $"Atropos : {Name} : "; } }
        private OrderedDictionary<string, Func<Task>> Actions = new OrderedDictionary<string, Func<Task>>();
        public OrderedDictionary<string, State> Variables = new OrderedDictionary<string, State>();
        private DoubleDictionary<string, State, List<Action>> VariableSetResults = new DoubleDictionary<string, State, List<Action>>();

        protected static string UserRole = "__UserRole__";
        protected static string LockPrefix = "__Lock__";
        protected static string PassPrefix = "__Pass__";
        public State this[string name]
        {
            get
            {
                if (name != UserRole) return Variables[name];
                else
                {
                    var activityType = RoleActivity.CurrentActivity.GetType();
                    if (activityType == typeof(SamuraiActivity)) return State.Hitter;
                    if (activityType == typeof(DeckerActivity)) return State.Hacker;
                    if (activityType == typeof(MageActivity)) return State.Sorceror;
                    else return State.Spy;
                }
            }
            set
            {
                SetVariable(name, value);
            }
        }

        public Scenario OnQR(string qrString, Func<Task> action)
        {
            if (!qrString.StartsWith("http")) qrString = Prefix + qrString;
            if (Actions.ContainsKey(qrString)) throw new ArgumentException($"Duplicate QRstring '{qrString}' not allowed!");
            Actions.Add(qrString, action);
            return this;
        }

        public Scenario OnQR(string qrString, Action action)
        {
            return OnQR(qrString, () => { action?.Invoke(); return Task.CompletedTask; });
        }
        public Scenario OnQR(string qrString, IEffect effect, SoundOptions options = null, bool awaitCompletion = false)
        {
            if (awaitCompletion)
                return OnQR(qrString, () => { return effect.PlayToCompletion(options ?? SoundOptions.Default); });
            else return OnQR(qrString, () => { effect.Play(options ?? SoundOptions.Default); });
        }
        public Scenario OnQR(string qrString, int effectID, SoundOptions options = null, bool awaitCompletion = false)
        {
            var effect = new Effect(qrString, effectID);
            return OnQR(qrString, effect, options, awaitCompletion);
        }
        public Scenario OnQR(string qrString, string contents, SoundOptions options = null, bool awaitCompletion = false)
        {
            if (awaitCompletion)
                return OnQR(qrString, () => { return Speech.SayAllOf(contents, options ?? SoundOptions.Default); });
            else return OnQR(qrString, () => { Speech.Say(contents, options ?? SoundOptions.Default); });
        }

        public async Task ExecuteQR(string qrString)
        {
            if (!Actions.ContainsKey(qrString)) throw new ArgumentException($"Cannot find <{qrString}> in scenario.");
            var doThis = Actions[qrString];
            await doThis?.Invoke();
        }

        public Scenario AddVariable(string name, State initialState = State.InitialState)
        {
            if (Variables.ContainsKey(name)) throw new ArgumentException($"Duplicate variable name '{name}' not allowed!");
            Variables.Add(name, initialState);
            return this;
        }
        public static Action ClearResponses = () => { };
        public Scenario OnVariable(string variableName, State tgtState, Action action)
        {
            if (!VariableSetResults.ContainsKeypair(variableName, tgtState))
                VariableSetResults.Add(variableName, tgtState, new List<Action>());

            if (object.ReferenceEquals(action, ClearResponses)) VariableSetResults.Remove(variableName, tgtState);
            else VariableSetResults[variableName, tgtState].Add(action);

            return this;
        }
        public void SetVariable(string name, State tgtState, bool broadcast = true)
        {
            if (Variables[name] == tgtState) return;
            Variables[name] = tgtState;
            if (VariableSetResults.ContainsKeypair(name, tgtState))
            {
                foreach (Action action in VariableSetResults[name, tgtState]) action?.Invoke();
            }
            if (broadcast) Communications.HeyYou.Everybody.SetScenarioVariable(name, tgtState);
        }


        public static void LaunchActivity(Type activity)
        {
            var intent = new Intent(Application.Context, activity);
            intent.AddFlags(ActivityFlags.SingleTop);
            intent.AddFlags(ActivityFlags.NewTask);
            Application.Context.StartActivity(intent);
        }

        public ScenarioCondition OnQR_DependingOn(string qrString, string variableName)
        {
            return new ScenarioCondition() { qrString = qrString, scenario = this, variableName = variableName };
        }

        public ScenarioCondition OnQR_Locked(string qrString, Lock tgtLock)
        {
            var lockString = LockPrefix + qrString;
            AddVariable(lockString, State.Locked);
            return new ScenarioCondition()
            {
                qrString = qrString,
                scenario = this,
                variableName = lockString,
                dataO = tgtLock
            };
        }

        public class ScenarioCondition
        {
            public string qrString;
            public Scenario scenario;
            public string variableName;
            public object dataO;

            public List<ScenarioConditionConsequence> consequences = new List<ScenarioConditionConsequence>();

            public ScenarioConditionConsequence IfItIs(params State[] states)
            {
                return new ScenarioConditionConsequence() { condition = this, states = states };
            }

            public ScenarioConditionConsequence Otherwise()
            {
                return IfItIs(State.NoneOfTheAbove);
            }

            public Scenario End_DependingOn()
            {
                scenario.OnQR(qrString, async () =>
                {
                    bool foundIt = false;
                    ScenarioConditionConsequence noneOfTheAboveConsequence = null;
                    foreach (var consequence in consequences)
                    {
                        if (scenario[variableName].IsOneOf(consequence.states))
                        {
                            foundIt = true;
                            await consequence.action.Invoke();
                            break;
                        }
                        if (State.NoneOfTheAbove.IsOneOf(consequence.states))
                        {
                            noneOfTheAboveConsequence = consequence;
                        }
                    }
                    if (!foundIt && noneOfTheAboveConsequence != null)
                    {
                        await noneOfTheAboveConsequence.action.Invoke();
                    }
                });
                return scenario;
            }

            public Scenario End_Locked(Action onProceedingThrough = null)
            {
                var lockString = LockPrefix + qrString;
                var checkResult = consequences.FirstOrDefault(conseq => conseq.states.Contains(State.Unlocked));
                var lockedResult = consequences.FirstOrDefault(conseq => conseq.states.Contains(State.Locked));
                Lock.Current = (Lock)dataO;
                Lock.Current.OnLockOpened += (o, e) => { Current[lockString] = State.Success; };

                return scenario.OnQR(qrString, async () =>
                {
                    if (Current[lockString] == State.Checked)
                    {
                        onProceedingThrough?.Invoke();
                    }
                    else if (Current[lockString] == State.Unlocked || object.ReferenceEquals(Lock.Current, Lock.None))
                    {
                        Current[lockString] = State.Checked;
                        await (checkResult?.action?.Invoke() ?? Speech.SayAllOf("Proceed."));
                    }
                    else
                    {
                        if (RoleActivity.CurrentActivity.GetType() != typeof(ToolkitActivity)
                            || object.ReferenceEquals(Lock.Current, Lock.Special))
                        {
                            await (lockedResult?.action?.Invoke() ?? Speech.SayAllOf("Locked."));
                        }
                        else if (Lock.Current.LockType == Lock.LType.KeyLock)
                            LaunchActivity(typeof(LockPickingActivity));
                        else if (Lock.Current.LockType == Lock.LType.SafeDial)
                            LaunchActivity(typeof(SafecrackingActivity));
                    }
                });
            }
        }

        public class ScenarioConditionConsequence
        {
            public ScenarioCondition condition;
            public State[] states;
            public Func<Task> action;

            public ScenarioCondition Then(Func<Task> action)
            {
                this.action = action;
                condition.consequences.Add(this);
                return condition;
            }

            public ScenarioCondition Then(Action action)
            {
                return Then(() => { action?.Invoke(); return Task.CompletedTask; });
            }

            public ScenarioCondition Then(IEffect effect, SoundOptions options = null, bool awaitCompletion = false)
            {
                if (awaitCompletion)
                    return Then(() => { return effect.PlayToCompletion(options ?? SoundOptions.Default); });
                else return Then(() => { effect.Play(options ?? SoundOptions.Default); });
            }
            public ScenarioCondition Then(int effectID, SoundOptions options = null, bool awaitCompletion = false)
            {
                var effect = new Effect(condition.qrString, effectID);
                return Then(effect, options, awaitCompletion);
            }
            public ScenarioCondition Then(string contents, SoundOptions options = null, bool awaitCompletion = false)
            {
                if (awaitCompletion)
                    return Then(() => { return Speech.SayAllOf(contents, options ?? SoundOptions.Default); });
                else return Then(() => { Speech.Say(contents, options ?? SoundOptions.Default); });
            }
        }
    }
}