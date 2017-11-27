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
using System.Threading.Tasks;
using System.Threading;
using Nito.AsyncEx;
using System.Numerics;
using com.Atropos.Machine_Learning;

namespace com.Atropos.Hacking
{
    internal interface IOpportunityCriterion
    {
        bool IsAvailable(HackingAttemptState givenState);
        //int HasRelevance(HackingAttemptState givenState);
    }

    internal static class OpportunityCriteria
    {
        internal class AlwaysAvailable : IOpportunityCriterion
        {
            //private static int DeclarationIndex = 0;
            //private static int NextIndex { get { return System.Threading.Interlocked.Increment(ref DeclarationIndex); } }

            public virtual bool IsAvailable(HackingAttemptState givenState) { return true; }
            //private int _orderDeclaredIn = NextIndex;
            //public virtual int HasRelevance(HackingAttemptState givenState) { return _orderDeclaredIn; }
        }

        internal class IfStateIs : IOpportunityCriterion
        {
            private StateAspect Benchmark;
            private StateChangeCriterion Criterion;

            public IfStateIs(StateAspect benchmark, StateChangeCriterion criterion)
            {
                Benchmark = benchmark;
                Criterion = criterion;
            }

            public virtual bool IsAvailable(HackingAttemptState state)
            {
                return Criterion(Benchmark, state);
            }

            //public virtual int HasRelevance(HackingAttemptState state)
            //{
            //    return Only.MarginOfSuccess(Benchmark, state, Criterion);
            //}
        }

        internal class IfObjectiveGained : IOpportunityCriterion
        {
            private string conditionString;
            private bool negateResult;

            public IfObjectiveGained(string objective, bool trueIfNotGained = false)
            {
                conditionString = objective;
                negateResult = trueIfNotGained;
            }

            public virtual bool IsAvailable(HackingAttemptState state)
            {
                if (!state.Objectives.ContainsKey(conditionString)) return false;

                var result = state.Objectives[conditionString];
                return (negateResult) ? !result : result;
            }
        }

        internal class IfConditionHolds : IOpportunityCriterion
        {
            private string conditionString;
            private bool negateResult;

            public IfConditionHolds(string condition, bool trueIfNotGained = false)
            {
                conditionString = condition;
                negateResult = trueIfNotGained;
            }

            public virtual bool IsAvailable(HackingAttemptState state)
            {
                if (!state.Conditions.ContainsKey(conditionString)) return false;

                var result = state.Conditions[conditionString];
                return (negateResult) ? !result : result;
            }
        }
    }
}