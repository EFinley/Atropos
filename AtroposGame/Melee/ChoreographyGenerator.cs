using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Atropos.Machine_Learning;
using MiscUtil;
using Nito.AsyncEx;

namespace Atropos.Melee
{
    public interface IChoreographyGenerator : IActivator
    {
        event EventHandler<EventArgs<ExchangeOfBlows>> OnExchangeChosen;
        void SubmitResults(ChoreographyCue ResultCueA, ChoreographyCue ResultCueB);
    }

    public class SimpleChoreographyGenerator : ActivatorBase, IChoreographyGenerator
    {
        protected int GapMean { get; set; }
        protected int GapSigma { get; set; }
        private AsyncAutoResetEvent readyToProceed = new AsyncAutoResetEvent();
        protected Dictionary<string, Classifier> Classifiers;

        protected Dictionary<int, int> AppropriateParries;

        public SimpleChoreographyGenerator(Dictionary<string, Classifier> classifiers, int millisecondsGapMean = 1000, int millisecondsGapSigma = 500)
        {
            Classifiers = classifiers;
            GapMean = millisecondsGapMean;
            GapSigma = millisecondsGapSigma;

            // Prep the appropriate parries table - for now, via hardcoding
            AppropriateParries = new Dictionary<int, int>()
            {
                { 0, 0 }, // High slash => high parry
                { 1, 2 }, // Left slash => right parry
                { 2, 1 }, // Right slash => left parry
                { 3, 1 }  // Thrust => left parry
            };
        }

        public event EventHandler<EventArgs<ExchangeOfBlows>> OnExchangeChosen;
        public void SubmitResults(ChoreographyCue MyResultCue, ChoreographyCue OppResultCue)
        {
            // This variant completely ignores the CONTENTS of what you just sent back; all it cares about is the fact that you sent 'em now.
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
                //await Task.Delay(timeGap);
                if (StopToken.IsCancellationRequested) break; // Just so that a "stop" doesn't mean "do one last cue" it means stop right away.

                // TODO: Later on, this won't just be a plain random draw.
                var offenseKey = MeleeBetaActivity.OFFENSE;
                var defenseKey = MeleeBetaActivity.DEFENSE;
                var offendersMoveIndex = StaticRandom.Next(4);
                var defendersMoveIndex = AppropriateParries[offendersMoveIndex];
                var goTime = DateTime.Now + timeGap;

                ExchangeOfBlows exchange;
                if (Res.CoinFlip)
                {
                    exchange = new ExchangeOfBlows()
                    {
                        CueTime = goTime,
                        ExchangeID = Guid.NewGuid(),
                        MyClassifierKey = offenseKey,
                        MyGestureIndex = offendersMoveIndex,
                        OppClassifierKey = defenseKey,
                        OppGestureIndex = defendersMoveIndex
                    };
                }
                else
                {
                    exchange = new ExchangeOfBlows()
                    {
                        CueTime = goTime,
                        ExchangeID = Guid.NewGuid(),
                        MyClassifierKey = defenseKey,
                        MyGestureIndex = defendersMoveIndex,
                        OppClassifierKey = offenseKey,
                        OppGestureIndex = offendersMoveIndex
                    };
                }                

                // Now actually send out the event.
                OnExchangeChosen.Raise(exchange);

                await Task.WhenAny(readyToProceed.WaitAsync(), StopToken.AsTask()).Result;
            }
        }
    }
}