namespace Atropos
{
    
    using System;
    using System.Text;
    using System.Linq;
    using System.Collections.Generic;

    using Android;
    using Android.App;
    using Android.Nfc;
    using Android.Nfc.Tech;
    using Android.OS;
    using Android.Widget;
    using Android.Util;
    //
    //
    //using Accord.Math;
    //using Accord.Statistics;
    using Android.Content;
    using System.Threading.Tasks;
    using System.Threading;
    using Android.Views;
    using Android.Runtime;

    /// <summary>
    /// This is the base type which collects all the stuff we need in, essentially, every activity.
    /// </summary>
    public class BaseActivity : Activity, IRelayToasts
    {
        protected NfcAdapter _nfcAdapter;
        protected PowerManager.WakeLock _wakeLock;
        protected double _wakeLockMinutes = 15.0;
        protected bool _autoRestart = true;

        private static BaseActivity _currentActivity;
        private static BaseActivity _previousActivity;
        private static IRelayToasts _currentToastRelay;
        internal static BaseActivity CurrentActivity
        {
            get { return _currentActivity; }
            set
            {
                _previousActivity = _currentActivity;
                _currentActivity = value;
                _currentToastRelay = value;
            }
        }
        internal static BaseActivity PreviousActivity { get { return _previousActivity; } }
        internal static IRelayToasts CurrentToaster { get { return _currentToastRelay; } set { _currentToastRelay = value; } }

        private static IActivator _currentStage;
        internal static IActivator CurrentStage
        {
            get { return _currentStage; }
            set
            {
                if (!value.Equals(_currentStage))
                {
                    _currentStage?.Deactivate();
                    _currentStage = value;
                }
            }
        }
        private static List<IActivator> _backgroundStages = new List<IActivator>();
        // NOTE! Unlike CurrentStage, which auto-deactivates the previous stage, you have to manually make sure to deactivate BackgroundStage members.
        internal static List<IActivator> BackgroundStages
        {
            get { return _backgroundStages; }
        }
        public static void AddBackgroundStage(IActivator stage)
        {
            lock (_backgroundStages)
            {
                _backgroundStages.Add(stage);
                stage.StopToken.Register(() =>
                {
                    lock (_backgroundStages)
                    {
                        //_backgroundStages.Remove(stage);
                        var i = _backgroundStages.IndexOf(stage);
                        _backgroundStages[i] = Activator.NeverActive;
                    }
                });
            }
        }
        protected ScreenOffReceiver powerButtonInterceptor;

        // Usage: In the derived class, call DoOnResume(stuff) inside your OnResume() call.
        protected void DoOnResume<T>(Action<T> DoBeforeRestart, T Argument, bool? AutoRestart = null, double? MinutesOfWakelock = null)
        {
            StartResuming();
            DoBeforeRestart?.Invoke(Argument);
            FinishResuming(AutoRestart, MinutesOfWakelock);
        }
        protected void DoOnResume(Action DoBeforeRestart = null, bool? AutoRestart = null, double? MinutesOfWakelock = null)
        {
            StartResuming();
            DoBeforeRestart?.Invoke();
            FinishResuming(AutoRestart, MinutesOfWakelock);
        }
        protected async Task DoOnResumeAsync<T>(Func<T, Task> DoBeforeRestart, T Argument, bool? AutoRestart = null, double? MinutesOfWakelock = null)
        {
            StartResuming();
            await (DoBeforeRestart?.Invoke(Argument) ?? Task.CompletedTask);
            FinishResuming(AutoRestart, MinutesOfWakelock);
        }
        protected async Task DoOnResumeAsync(Func<Task> DoBeforeRestart = null, bool? AutoRestart = null, double? MinutesOfWakelock = null)
        {
            StartResuming();
            await (DoBeforeRestart?.Invoke() ?? Task.CompletedTask);
            FinishResuming(AutoRestart, MinutesOfWakelock);
        }
        protected async Task DoOnResumeAsync(Task DoBeforeRestart = null, bool? AutoRestart = null, double? MinutesOfWakelock = null)
        {
            StartResuming();
            await DoBeforeRestart;
            FinishResuming(AutoRestart, MinutesOfWakelock);
        }

        private void StartResuming()
        {
            base.OnResume();
            //PreviousActivity = CurrentActivity;
            CurrentActivity = this;
            Res.CurrentActivity = this;
        }
        private void FinishResuming(bool? AutoRestart, double? MinutesOfWakelock)
        {
            _wakeLockMinutes = MinutesOfWakelock ?? _wakeLockMinutes;
            Poke();
            //Res.SFX.ResumeAll();

            powerButtonInterceptor = new ScreenOffReceiver(this);
            RegisterReceiver(powerButtonInterceptor, new IntentFilter(Intent.ActionScreenOff));

            StartLookingForTagRepeatsAndRemovals();

            SensorProvider.ResumeAllListeners();
            //CurrentActivity.HideKeyboard();

            _autoRestart = AutoRestart ?? _autoRestart;
            if (_autoRestart)
            {
                //StopStartables.ResumeAll();
                if (!CurrentStage?.IsActive ?? false) CurrentStage.Activate();
                //if (CurrentStage?.IsPaused ?? false) CurrentStage?.Resume();
                foreach (var backStage in BackgroundStages)
                {
                    if (!backStage?.IsActive ?? false) backStage.Activate();
                }
            }
            else
            {
                //StopStartables.ResumeAll(Except: CurrentStage ?? GestureRecognizerStage.NullStage);
            }
        }

        protected override void OnResume()
        {
            DoOnResume();
        }

        // Usage: In the derived class, call DoOnPause(stuff) inside your OnPause() call.
        protected void DoOnPause<T>(Action<T> DoBeforePausing, T Argument, bool ReleaseWakelock = true)
        {
            base.OnPause();
            DoBeforePausing?.Invoke(Argument);
            FinishPausing(ReleaseWakelock);
        }
        protected void DoOnPause(Action DoBeforePausing = null, bool ReleaseWakelock = true)
        {
            base.OnPause();
            DoBeforePausing?.Invoke();
            FinishPausing(ReleaseWakelock);
        }
        protected async void DoOnPauseAsync<T>(Func<T, Task> DoBeforePausing, T Argument, bool ReleaseWakelock = true)
        {
            base.OnPause();
            await (DoBeforePausing?.Invoke(Argument) ?? Task.CompletedTask);
            FinishPausing(ReleaseWakelock);
        }
        protected async void DoOnPauseAsync(Func<Task> DoBeforePausing, bool ReleaseWakelock = true)
        {
            base.OnPause();
            await (DoBeforePausing?.Invoke() ?? Task.CompletedTask);
            FinishPausing(ReleaseWakelock);
        }

        private void FinishPausing(bool ReleaseWakelock)
        {
            if (ReleaseWakelock && (_wakeLock?.IsHeld ?? false)) { Task.Delay(250).ContinueWith((t) => _wakeLock?.Release()); }
            NeverMindWeAreShuttingDown?.Cancel();

            Res.SFX.StopAll();
            SensorProvider.PauseAllListeners();

            CurrentStage?.Deactivate();
            foreach (var backstage in BackgroundStages) backstage?.Deactivate();
            InteractionLibrary.Current = null;
            UnregisterReceiver(powerButtonInterceptor);
            //Task.Delay(500).ContinueWith(t => UnregisterReceiver(powerButtonInterceptor));
        }

        protected override void OnPause()
        {
            DoOnPause();
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            MainActivity.InitializeAll();
            CurrentActivity = this;
            if (Intent == null) return;
            _nfcAdapter = NfcAdapter.GetDefaultAdapter(this);
            _wakeLock = ((PowerManager)this.GetSystemService(Context.PowerService)).NewWakeLock(WakeLockFlags.ScreenDim, "Atropos_Wakelock");
            //Log.Info("BaseActivity", $"Just checking... this message generated by an object of type {this.GetType().Name}.  Parent class, or derived one?");
        }

        //public override void OnBackPressed()
        //{
        //    //base.OnBackPressed();
        //}

        public void Poke()
        {
            if (_wakeLock.IsHeld) _wakeLock.Release();

            if (_wakeLockMinutes == double.PositiveInfinity) _wakeLock.Acquire();
            else if (_wakeLockMinutes > 0) _wakeLock?.Acquire((long)Math.Round(_wakeLockMinutes * 60 * 1000));
            else throw new ArgumentOutOfRangeException("Wakelock cannot be requested for zero or negative amount of time!");
        }

        #region NFC Tag Removal / Repetition Checks
        protected Action DoOnTagRemoved;
        protected double SecondsOfLeeway, IntervalBetweenChecks;
        protected CancellationTokenSource OhWaitWeFoundItAgainCancelThat;
        protected CancellationTokenSource NeverMindWeAreShuttingDown;
        /// <summary>
        /// Tells the Activity to do something - like, say, Finish - if the tag is lost and not found again.
        /// </summary>
        /// <param name="actionToTake">What to do when we lose the tag and don't find it again in time.</param>
        /// <param name="secondsOfLeewayBeforeActing">If we do lose the tag, how long before we do the above?  Optional, default 1.5 seconds.</param>
        /// <param name="intervalBetweenChecks">How many seconds can go by before we even bother checking to see if the tag's still there?  Optional, default every half a second.</param>
        /// <remarks>Note!  This will only work if invoked in the OnCreate() method of the Activity (though it could
        /// be changed later if desired).  So if you want to even *maybe* be watching this "channel," you should give it at
        /// least a no-op in your OnCreate().</remarks>
        public virtual void SetTagRemovalResult(Action actionToTake, 
            double secondsOfLeewayBeforeActing = 2.5,
            double intervalBetweenChecks = 0.5)
        {
            DoOnTagRemoved = actionToTake;
            SecondsOfLeeway = secondsOfLeewayBeforeActing;
            IntervalBetweenChecks = intervalBetweenChecks;
        }
        public virtual void CancelTagRemovalResult()
        {
            DoOnTagRemoved = null;
            SecondsOfLeeway = IntervalBetweenChecks = default(double);
            OhWaitWeFoundItAgainCancelThat?.Cancel();
        }
        protected Task UponTagRemoved(Task priorTask)
        {
            DoOnTagRemoved?.Invoke();
            return priorTask; // Will be the Task.Delay that this is designed to come after.  Could just be Task.CompletedTask, but this might help with exception handling.
        }
        protected void StartLookingForTagRepeatsAndRemovals()
        {
            if (DoOnTagRemoved == null) return;

            NeverMindWeAreShuttingDown = new CancellationTokenSource();

            // Repeat handling: create an intent filter for when an NFC tag is discovered during this activity.
            var tagDetected = new IntentFilter(NfcAdapter.ActionTagDiscovered);
            var filters = new[] { tagDetected };

            // When an NFC tag is detected, Android will use the PendingIntent to direct the detection back to this activity.
            // The OnNewIntent() method will then be invoked by Android.
            var intent = new Intent(this, this.GetType()).AddFlags(ActivityFlags.SingleTop);
            var pendingIntent = PendingIntent.GetActivity(this, 0, intent, 0);

            if (_nfcAdapter == null)
            {
                var alert = new AlertDialog.Builder(this).Create();
                alert.SetMessage("NFC is not supported on this device.");
                alert.SetTitle("NFC Unavailable");
                alert.SetButton("OK", delegate
                {
                    Log.Error("NFC", "NFC read error - NFC does not appear to be supported.");
                });
                alert.Show();
            }
            else
            {
                // // These two statements OUGHT to cause us to supersede the more global Intent Filters which would trigger on finding such a tag, but don't seem to be working as planned.
                // // Therefore, TODO: Fix or otherwise handle 'debouncing' of messy NFC contact.
                // // It's also possible that the 
                _nfcAdapter.EnableForegroundDispatch(this, pendingIntent, filters, null);
                NeverMindWeAreShuttingDown?.Token.Register(() => { _nfcAdapter.DisableForegroundDispatch(this); });
            }

            TagCheckingLoop().LaunchAsOrphan(NeverMindWeAreShuttingDown.Token, "Tag Checking");
        }

        protected virtual async Task TagCheckingLoop(Tag tagToCheck = null)
        {
            bool StillThere = true;
            if (tagToCheck != null) { InteractionLibrary.CurrentTagHandle = tagToCheck; InteractionLibrary.CurrentSpecificTag = tagToCheck.tagID(); }
            tagToCheck = tagToCheck ?? InteractionLibrary.CurrentTagHandle;
            if (InteractionLibrary.CurrentTagHandle == null)
            {
                Log.Warn("TagChecking", $"Entering Tag Checking loop with a null CurrentTagHandle.  The ID associated with that null is {InteractionLibrary.CurrentSpecificTag}.");
                return;
            }
            var ndef = Android.Nfc.Tech.Ndef.Get(tagToCheck);
            if (!ndef.IsConnected) ndef.Connect();
            if (!ndef.IsConnected) Log.Debug("TagChecking", "After explicitly saying 'connect' to the NFC tag, we're still not connected.  Timing issue?");

            while (StillThere && !NeverMindWeAreShuttingDown.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(IntervalBetweenChecks));
                try
                {
                    StillThere = ndef?.IsConnected ?? false;
                }
                catch (Exception e)
                {
                    if (e is Java.IO.IOException || e is TagLostException)
                        StillThere = false;
                    else throw;
                }

                if (StillThere)
                {
                    OhWaitWeFoundItAgainCancelThat?.Cancel(); // Not actually certain how we would get here... but it might work. Logging it if so.
                    Log.Info("TagChecking", "Managed to hit the 'manual' OhWaitWeFoundItAgain code.  Not really sure HOW we did so...");
                }
                else
                {
                    if (OhWaitWeFoundItAgainCancelThat == null || OhWaitWeFoundItAgainCancelThat.IsCancellationRequested)
                    {
                        Log.Info("Tag Checking Loop", $"Discovered the absence of tag {tagToCheck}.  Starting {SecondsOfLeeway:f1} second countdown to requested action.");
                        OhWaitWeFoundItAgainCancelThat = new CancellationTokenSource();
                        OhWaitWeFoundItAgainCancelThat.Token.Register(() => { Log.Info("Tag Checking Loop", "Tag rediscovered."); });
                    }

                    await Task.Delay(TimeSpan.FromSeconds(SecondsOfLeeway), OhWaitWeFoundItAgainCancelThat.Token)
                        .ContinueWith(UponTagRemoved, TaskContinuationOptions.OnlyOnRanToCompletion);
                }
            }
        }

        /// <summary>
        /// This method will be called when an NFC tag is discovered by the application,
        /// as long as we've enabled 'foreground dispatch' - send it to us, don't go looking
        /// for another program to respond to the tag.
        /// </summary>
        /// <param name="intent">The Intent representing the occurrence of "hey, we spotted an NFC!"</param>
        protected override void OnNewIntent(Intent intent)
        {
            if (CurrentActivity != null)
            {
                var nfcTag = intent.GetParcelableExtra(NfcAdapter.ExtraTag) as Tag;

                if (nfcTag == null)
                {
                    return;
                }

                var nfcID = nfcTag.tagID();
                if (InteractionLibrary.CurrentSpecificTag != nfcID) // We discovered a *different* tag than last time we checked.  Whups!
                {
                    Log.Warn("NFC Tag Checking", $"While verifying tag {InteractionLibrary.CurrentSpecificTag}, we found {nfcID} instead.  Passing off to launcher.");
                    _nfcAdapter.DisableForegroundDispatch(this);
                    CurrentActivity.Finish();
                    StartActivity(intent); // SHOULD cause our "ActOnFoundTag" listener to trigger.  Needs testing to verify.
                    return;
                }

                Poke();
                OhWaitWeFoundItAgainCancelThat?.Cancel();
            }
        }
        #endregion

        public void RelayToast(string message, ToastLength length = ToastLength.Short)
        {
            RunOnUiThread(() => { Toast.MakeText(this, message, length).Show(); });
        }

        #region Volume trigger use (remember to set useVolumeTrigger to True in your OnCreate())
        protected bool useVolumeTrigger = false;
        protected event EventHandler<EventArgs> OnVolumeButtonPressed;
        protected static object volumeButtonSyncLock = new object();
        public override bool OnKeyDown([GeneratedEnum] Keycode keyCode, KeyEvent e)
        {
            if (useVolumeTrigger)
            {
                if (keyCode == Keycode.VolumeDown || keyCode == Keycode.VolumeUp)
                {
                    lock (volumeButtonSyncLock)
                    {
                        OnVolumeButtonPressed?.Invoke(this, EventArgs.Empty);
                        //((Gunfight_AimStage)CurrentStage).ResolveTriggerPull();
                        return true;
                    }
                }
            }
            return base.OnKeyDown(keyCode, e);
        }
        public override bool OnKeyUp([GeneratedEnum] Keycode keyCode, KeyEvent e)
        {
            if (useVolumeTrigger)
            {
                if (keyCode == Keycode.VolumeDown || keyCode == Keycode.VolumeUp)
                {
                    return true; // Handled it, thanks.
                }
            }
            return base.OnKeyUp(keyCode, e);
        }
        #endregion
    }

    // Causes the device to (effectively) ignore short presses of the Power button. In theory.
    //[BroadcastReceiver()]
    public class ScreenOffReceiver : BroadcastReceiver
    {
        private BaseActivity activity;
        public ScreenOffReceiver(BaseActivity act) : base()
        {
            activity = act;
            //Application.Context.RegisterReceiver(this, new IntentFilter(Intent.ActionScreenOff));
        }

        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action == Intent.ActionScreenOff)
            {
                activity.Poke();
            }
        }
    }

    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public abstract class BaseActivity_Portrait : BaseActivity { }

    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
    public abstract class BaseActivity_Landscape : BaseActivity { }

    public interface IRelayMessages { void RelayMessage(string message, int RelayTargetId = 1); }
    public interface IRelayToasts { void RelayToast(string message, ToastLength length = ToastLength.Short); }
}
