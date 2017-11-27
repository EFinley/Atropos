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
    internal abstract class ICEbreaker
    {
        public abstract Task<IStateChange> Break(); // Typically an async method, but you can't specify that in the abstract class.
    }

    // A simple example ICEbreaker...

    internal class Spastic : ICEbreaker
    {
        public override async Task<IStateChange> Break()
        {
            var stage = new SpasticStage();
            stage.Activate();
            await stage.CompletionSignal.WaitAsync();
            return new StateChange(Authority.Root);
        }

        private class SpasticStage : GestureRecognizerStage
        {
            private StillnessProvider Stillness = new StillnessProvider();
            public AsyncManualResetEvent CompletionSignal = new AsyncManualResetEvent();

            public SpasticStage() : base("Spastic")
            {
                SetUpProvider(Stillness);
            }

            protected override bool nextStageCriterion()
            {
                return (Stillness.StillnessScore < -19);
            }

            protected override void nextStageAction()
            {
                CompletionSignal.Set();
            }
        }
    }
}