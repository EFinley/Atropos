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
    public class CuePrompter<T> where T : struct
    {
        private MachineLearningActivity<T> Current;
        public CuePrompter(MachineLearningActivity<T> parentActivity)
        {
            Current = parentActivity;
            //cueEffect = new Effect("Cue sound", Resource.Raw._215028_shing);
        }

        private Stopwatch Stopwatch = new Stopwatch();
        //private CancellationTokenSource _cts;
        private CancelableTimer cancelableTimer;
        public bool ListeningForFalseStart { get; set; } = false;

        //public IEffect cueEffect;

        public async void DoOnFalseStart()
        {
            var button = Current.FindViewById<Button>(Resource.Id.mlrn_cue_button);
            button.Enabled = false;
            if (cancelableTimer == null) // First time only
            {
                await Speech.SayAllOf("False start!");
                cancelableTimer = new CancelableTimer();
            }

            await cancelableTimer.Delay( 1000, () => 
            {
                    ListeningForFalseStart = false;
                    button.Enabled = true;
            });
        }

        public async Task SetAndProvideCue(GestureClass gestureClass = null, int millisecondTimeBase = 1250)
        {
            if (gestureClass == null) gestureClass = Current.SelectedGestureClass = Current.Dataset.Classes.GetRandom();
            ListeningForFalseStart = true;
            Current.FindViewById<Button>(Resource.Id.mlrn_cue_button).Enabled = false;

            var milliSecDelay = millisecondTimeBase * (0.75 + 0.5 * Res.Random);
            await Task.Delay((int)milliSecDelay);

            await Speech.SayAllOf(gestureClass.className, speakRate: 2.0);
            //await Task.Delay(400);
            //cueEffect.Play();

            ListeningForFalseStart = false;
            Stopwatch.Start();
        }

        public void MarkGestureStart()
        {
            Stopwatch.Stop();
        }

        public async void ReactToFinalizedGesture(ISequence sequence)
        {
            var Seq = sequence as Sequence<T>;
            if (Seq == null) throw new ArgumentException("ISequence not convertible to Sequence<T>");

            var button = Current.FindViewById<Button>(Resource.Id.mlrn_cue_button);
            button.Enabled = true;

            if (Seq.RecognizedAsIndex < 0) button.Text = "(Unrecognized?!?) ...Again!";
            else if (Seq.RecognizedAsIndex != Current.SelectedGestureClass.index) button.Text = $"(Looked like {Seq.RecognizedAsName}!) ...Again!";
            else
            {
                var score = Seq.RecognitionScore;
                var interval = Stopwatch.Elapsed;
                button.Text = $"({score:f2} pts / {interval.TotalSeconds:f1}s) ...Again!";
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
            cancellationReg = cts.Token.Register(() => OnInterruption?.Invoke(this, new EventArgs<TimeSpan>(stopwatch.Elapsed)));

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