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

using Atropos.Machine_Learning;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using MiscUtil;

namespace Atropos.Melee
{
    public class Choreographer : ActivatorBase
    {
        public IDataset Dataset { get; set; }
        protected int GapMean { get; set; }
        protected int GapSigma { get; set; }
        public Choreographer(IDataset dataset, int millisecondsGapMean = 1000, int millisecondsGapSigma = 500)
        {
            Dataset = dataset;
            GapMean = millisecondsGapMean;
            GapSigma = millisecondsGapSigma;
        }

        public event EventHandler<EventArgs<GestureClass>> OnSendCue;
        private AsyncAutoResetEvent readyToProceed = new AsyncAutoResetEvent();
        public void ProceedWithNextCue()
        {
            readyToProceed.Set();
        }

        public override void Activate(CancellationToken? externalStopToken = null)
        {
            base.Activate(externalStopToken);

            Task.Run(RunPromptingLoop);
        }

        protected async Task RunPromptingLoop()
        {
            while (!StopToken.IsCancellationRequested)
            {
                var timeGap = TimeSpan.FromMilliseconds(Res.GetRandomCoefficient(GapMean, GapSigma));
                await Task.Delay(timeGap);
                if (StopToken.IsCancellationRequested) break; // Just so that a "stop" doesn't mean "do one last cue" it means stop right away.

                // TODO: Later on, this won't just be a plain random draw.
                var cuedGesture = Dataset.Classes.GetRandom();
                OnSendCue.Raise(cuedGesture);

                await Task.WhenAny(readyToProceed.WaitAsync(), StopToken.AsTask());
            }
        }
    }
}