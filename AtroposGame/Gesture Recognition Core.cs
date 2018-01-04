
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

//using Accord.MachineLearning.VectorMachines;
//using Accord.Statistics.Filters;
//using Accord.Math;
using DeviceMotion.Plugin;
using DeviceMotion.Plugin.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Android.Util;
using System.ComponentModel;
using System.Numerics;

namespace Atropos
{
    
    public interface IGestureRecognizerStage : IActivator
    {
        string Label { get; }
        IProvider DataProvider { get; }
    }

    public class GestureRecognizerStage : ActivatorBase, IGestureRecognizerStage
    {
        public string Label { get; private set; }
        public static IGestureRecognizerStage NullStage = new NullGestureRecogStage();

        public volatile bool isMet = false;
        protected DateTime startTime;
        private DateTime nextInterimTriggerAt;
        protected TimeSpan InterimInterval { get; set; } = TimeSpan.FromMilliseconds(100);
        public TimeSpan RunTime { get { return DataProvider?.RunTime ?? (DateTime.Now - startTime); } }

        public IProvider DataProvider { get; set; }
        private bool allowProviderToPersist;
        private bool verboseLogging = false;
        protected void verbLog(string message) { if (verboseLogging) Log.Debug($"GestureRecog|VerbLog|{Label}", message); }

        public GestureRecognizerStage(string label = "GestureRecog") : base()
        {
            Label = label;
        }
        
        protected virtual void SetUpProvider(IProvider dataProvider, bool allowProviderToPersist = false)
        {
            DataProvider = dataProvider;
            this.allowProviderToPersist = allowProviderToPersist;
            //if (!allowProviderToPersist) dataProvider.DependsOn(StopToken);
        }

        public override async void Activate(CancellationToken? externalToken = null)
        {
            if (DataProvider == null) throw new Exception($"Gesture recog stage {Label} unable to Activate because it has no Provider!");
            verbLog("Early activation");
            base.Activate(externalToken);
            StopToken.Register(Deactivate);

            verbLog("Provider activation");
            if (!allowProviderToPersist) DataProvider.Activate(StopToken);
            else DataProvider.Activate();
            DataProvider.Proceed();

            prestartAction();

            // Now make sure to bounce it off to a background thread to do its work.
            await Task.Run(ListenForData, StopToken)
                .ConfigureAwait(false); // ConfigureAwait false means we're "surrendering" our connection to the UI thread.  Useful but limiting; we'll need to do things to link back to the UI thread when required.
        }

        protected virtual Task NextUpdateAwaiter()
        {
            return DataProvider.WhenDataReady();
        }

        protected async Task ListenForData()
        {
            isMet = false;
            startTime = nextInterimTriggerAt = DateTime.Now;
            Log.Info("GestureRecognitionCore", $"Starting gesture stage {Label}.");

            // If overridden in a derived class, do this now.
            verbLog("Starting listen loop");
            startAction();
            await startActionAsync();

            while (IsActive)
            {
                // First, wait for our stream to have data ready
                verbLog("Awaiting data");
                await NextUpdateAwaiter();

                // Then check criteria for interim activities
                if ( (interimCriterion() || await interimCriterionAsync()) && DateTime.Now >= nextInterimTriggerAt)
                {
                    verbLog("Interim steps");
                    nextInterimTriggerAt += InterimInterval;
                    interimAction();
                    await interimActionAsync();
                }

                // Then check criteria both for proceeding and for aborting
                if (nextStageCriterion() || await nextStageCriterionAsync())
                {
                    verbLog("Next stage steps");
                    isMet = true;
                    nextStageAction();
                    await nextStageActionAsync();
                    Deactivate();
                }
                else if (abortCriterion() || await abortCriterionAsync())
                {
                    verbLog("Abort steps");
                    isMet = false;
                    abortAction();
                    await abortActionAsync();
                    Deactivate();
                }

                // Finally, give the high sign that we're ready to receive data again anytime.
                DataProvider.Proceed();
            }
        }

        protected virtual void prestartAction()
        {
            // Insert custom logic here - this one happens inside the Activate( ) code rather than the main loop.  For locking purposes, basically.
        }

        protected virtual void startAction()
        {
            // Insert custom logic here.
        }

        protected virtual async Task startActionAsync()
        {
            // Insert custom logic here.
            await Task.CompletedTask;
        }

        protected virtual bool nextStageCriterion()
        {
            // Insert custom logic here - you can be sure that all of the data is ready in the streams.
            return false;
        }

        protected virtual async Task<bool> nextStageCriterionAsync()
        {
            await Task.CompletedTask;
            return false;
        }

        protected virtual void nextStageAction()
        {
            // Insert custom logic here.
            // To start a new stage, the syntax looks like this:
            //      CurrentStage = new Next_Stage_Class("now for the good part!", etc);
        }

        protected virtual async Task nextStageActionAsync()
        {
            await Task.CompletedTask;
        }

        protected virtual bool abortCriterion()
        {
            // Insert custom logic here, as above.
            return false;
        }

        protected virtual async Task<bool> abortCriterionAsync()
        {
            await Task.CompletedTask;
            return false;
        }

        protected virtual void abortAction()
        {
            // See nextStageAction.
        }

        protected virtual async Task abortActionAsync()
        {
            await Task.CompletedTask;
        }

        protected virtual bool interimCriterion()
        {
            // Insert custom logic
            return false;
        }

        protected virtual async Task<bool> interimCriterionAsync()
        {
            await Task.CompletedTask;
            return false;
        }

        protected virtual void interimAction()
        {
            // If the above logic is satisfied then this will be triggered.
        }

        protected virtual async Task interimActionAsync()
        {
            await Task.CompletedTask;
        }
    }

    public class AveragingStage<T> : GestureRecognizerStage where T : struct
    {
        public T Value { get { return (DataProvider as SensorProvider<T>) ?? default(T); } }
        public T Average { get { return Averager.Average; } }

        private RollingAverage<T> Averager;

        public AveragingStage(string Label) : base(Label)
        {
            Averager = new RollingAverage<T>();
        }
    }

    public class ThrottledGestureStage : GestureRecognizerStage
    {
        protected TimeSpan Interval { get; private set; } = TimeSpan.Zero;
        protected TimeSpan MinInterval;
        private System.Diagnostics.Stopwatch stopwatch;
        public ThrottledGestureStage(string Label, int intervalMilliseconds = 50) : base(Label)
        {
            MinInterval = TimeSpan.FromMilliseconds(intervalMilliseconds);
            stopwatch = new System.Diagnostics.Stopwatch();
        }

        protected override Task NextUpdateAwaiter()
        {
            return Task.WhenAll(DataProvider.WhenDataReady(), Task.Delay(MinInterval, StopToken));
        }

        protected sealed override bool interimCriterion()
        {
            Interval = stopwatch.Elapsed;
            if (Interval > MinInterval) // Will usually be true
            {
                stopwatch.Restart();
                return true;
            }
            else return false;
        }


    }

    public class NullGestureRecogStage : IGestureRecognizerStage
    {
        public string Label { get { return "Null"; } }
        public void Activate(CancellationToken? token = null) { }
        public void Pause() { }
        public void Resume() { }
        public void Deactivate() { }
        public CancellationToken StopToken { get { return CancellationToken.None; } }
        public CancellationToken PauseToken { get { return CancellationToken.None; } }
        public void DependsOn(CancellationToken token, Activator.Options options) { }
        public bool IsActive { get; } = true;
        public bool IsPaused { get; } = false;
        public bool IsStopped { get; } = false;
        public TimeSpan RunTime { get { return TimeSpan.Zero; } }
        public IProvider DataProvider { get { return new NullProvider(); } }
    }
    

    public static class GestureDefaults
    {
        public static OrientationSensorProvider defaultProvider;
    }
}