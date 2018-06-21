using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using MiscUtil;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace Atropos.Machine_Learning
{
    public abstract class CuePrompter<T> where T : struct
    {
        private MachineLearningActivity<T>.IMachineLearningActivity Current;
        public CuePrompter(MachineLearningActivity<T>.IMachineLearningActivity parentActivity)
        {
            Current = parentActivity;
            //cueEffect = new Effect("Cue sound", Resource.Raw._215028_shing);
        }

        private Stopwatch Stopwatch = new Stopwatch();
        //private CancellationTokenSource _cts;
        private CancelableTimer cancelableTimer;
        public bool ListeningForFalseStart { get; set; } = false;
        private TimeSpan? _overrideTimeElapsed;
        public TimeSpan TimeElapsed { get { return _overrideTimeElapsed ?? Stopwatch.Elapsed; } set { _overrideTimeElapsed = value; } }

        //public IEffect cueEffect;

        public abstract void SetButtonEnabledState(bool state);

        public async void DoOnFalseStart()
        {
            SetButtonEnabledState(false);
            if (cancelableTimer == null) // First time only
            {
                await Speech.SayAllOf("False start!");
                cancelableTimer = new CancelableTimer();
            }

            await cancelableTimer.Delay( 1000, () => 
            {
                ListeningForFalseStart = false;
                SetButtonEnabledState(true);
            });
        }

        public async Task WaitBeforeCue(int millisecondTimeBase = 1250, int millisecondTimeSigma = 500)
        {
            ListeningForFalseStart = true;
            SetButtonEnabledState(false);

            var milliSecDelay = Res.GetRandomCoefficient(millisecondTimeBase, millisecondTimeSigma);
            await Task.Delay((int)milliSecDelay);

            //await Speech.SayAllOf(gestureClass.className, speakRate: 2.0);
            //await Task.Delay(400);
            //cueEffect.Play();

            ListeningForFalseStart = false;
        }

        public void ProvideCue(GestureClass gestureClass = null)
        {
            Current.SelectedGestureClass = gestureClass ?? Current.Dataset.Classes.GetRandom();
            Speech.Say(Current.SelectedGestureClass.className, speakRate: 2.0);
            Stopwatch.Start();
        }

        public void MarkGestureStart()
        {
            Stopwatch.Stop();
        }

        public abstract void ReactToFinalizedGesture(Sequence<T> Seq);
    }

    public class MlrnCuePrompter<T> : CuePrompter<T> where T : struct
    {
        private MachineLearningActivity<T> Current;
        public MlrnCuePrompter(MachineLearningActivity<T> parentActivity) : base(parentActivity)
        {
            Current = parentActivity;
        }

        public override void SetButtonEnabledState(bool state)
        {
            Current.FindViewById<Button>(Resource.Id.mlrn_cue_button).Enabled = state;
        }

        public override async void ReactToFinalizedGesture(Sequence<T> Seq)
        {
            SetButtonEnabledState(true);
            var button = Current.FindViewById<Button>(Resource.Id.mlrn_cue_button);

            if (Seq.RecognizedAsIndex < 0) button.Text = "(Unrecognized?!?) ...Again!";
            else if (Seq.RecognizedAsIndex != Current.SelectedGestureClass.index) button.Text = $"(Looked like {Seq.RecognizedAsName}!) ...Again!";
            else
            {
                var score = Seq.RecognitionScore;
                //var interval = Stopwatch.Elapsed;
                button.Text = $"({score:f2} pts / {TimeElapsed.TotalMilliseconds:f0} ms) ...Again!";
            }

            if (button.Visibility == ViewStates.Visible && Current.FindViewById<CheckBox>(Resource.Id.mlrn_cue_repeat_checkbox).Checked)
            {
                await Task.Delay((int)(1000 * (1 + 2 * Res.Random)));
                button.CallOnClick();
            }
        }
    }

    public class CancelableTimer
    {
        public enum ReentryOptions
        {
            Cancel,
            Restart,
            Ignore,
            CancelAndRestart
        }
        private CancellationTokenSource cts;
        private IDisposable cancellationReg;
        private Stopwatch stopwatch;
        private TimeSpan? timeToWait;
        private Action actionToTake, actionIfCanceled;
        private ReentryOptions? doIfReentered;

        public CancelableTimer(TimeSpan? timeToWait = null, Action actionToTake = null, Action actionIfCanceled = null, ReentryOptions? doIfReentered = null)
        {
            this.timeToWait = timeToWait;
            this.actionToTake = actionToTake;
            this.actionIfCanceled = actionIfCanceled;
            this.doIfReentered = doIfReentered;
        }
        public CancelableTimer(int millisecondsToWait, Action actionToTake = null, Action actionIfCanceled = null, ReentryOptions? doIfReentered = null)
            : this(TimeSpan.FromMilliseconds(millisecondsToWait), actionToTake, actionIfCanceled, doIfReentered) { }

        public async Task Delay(TimeSpan? timeToWait = null, Action actionToTake = null, Action actionIfCanceled = null, ReentryOptions? doIfReentered = null)
        {
            this.timeToWait = timeToWait ?? this.timeToWait ?? TimeSpan.FromSeconds(5);
            this.actionToTake = actionToTake ?? this.actionToTake ?? (() => { });
            this.actionIfCanceled = actionIfCanceled ?? this.actionIfCanceled ?? (() => { });
            this.doIfReentered = doIfReentered ?? this.doIfReentered ?? ReentryOptions.Restart;

            if (cts != null) // We are indeed reentering it!
            {
                if (this.doIfReentered == ReentryOptions.Cancel) { cts.Cancel(); cts = null; return; } // Cancel the old AND ignore the new.
                else if (this.doIfReentered == ReentryOptions.Ignore) { return; } // Ignore the new, continue the old.

                if (this.doIfReentered == ReentryOptions.Restart) cancellationReg.Dispose();
                cts.Cancel();
                cts = null; // Cancel the old, *use* the new.
            }

            cts = new CancellationTokenSource();
            stopwatch = new Stopwatch();
            stopwatch.Start();
            cancellationReg = cts.Token.Register(() =>
            {
                this.actionIfCanceled.Invoke();
                OnInterruption?.Invoke(this, new EventArgs<TimeSpan>(stopwatch.Elapsed));
            });

            await Task.Delay(this.timeToWait.Value, cts.Token)
                .ContinueWith(_ =>
                {
                    this.actionToTake.Invoke();
                    OnUninterruptedWait?.Invoke(this, new EventArgs<TimeSpan>(stopwatch.Elapsed));
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
        public async Task Delay(int millisecondsToWait, Action actionToTake = null, Action actionIfCanceled = null, ReentryOptions? doIfReentered = null)
        {
            await Delay(TimeSpan.FromMilliseconds(millisecondsToWait), actionToTake, actionIfCanceled, doIfReentered);
        }

        public void Cancel()
        {
            cts.Cancel();
            cts = null;
        }

        public void Stop()
        {
            cancellationReg.Dispose();
            Cancel();
        }

        public event EventHandler<EventArgs<TimeSpan>> OnUninterruptedWait; // Basically an alternative for if you (say) need to sign up after starting the timer.
        public event EventHandler<EventArgs<TimeSpan>> OnInterruption; // Fires on Cancel or CancelAndRestart modes if reentered, or on Cancel().


    }
}