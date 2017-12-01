
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
using Android.Media;
using Android.Util;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Atropos
{
    public class SFX //: Java.Lang.Object, MediaPlayer.IOnCompletionListener, MediaPlayer.IOnErrorListener
    {

        // TODO: Change this so that each Activity (from BaseActivity etc) carries an SFX *instance* instead of putting
        // it all under the Resource Manager.  That way the dictionaries, lists, etc. will be unique per activity, and
        // they won't haul one anothers' effects out of "pause".  A detail, though.
        public Dictionary<string, IEffect> Effects = new Dictionary<string, IEffect>();

        private CancellationTokenSource stopAllCTS = new CancellationTokenSource();
        public CancellationToken StopAllToken { get { return stopAllCTS.Token; } }
        //public void StopAll() { stopAllCTS.Cancel(); stopAllCTS = new CancellationTokenSource(); } // Revised, see below.

        public IEnumerable<IEffect> AllActiveFX { get { return Effects.Values.Where(fx => fx.IsActive); } }
        public IEnumerable<IEffect> AllPlayingFX { get { return Effects.Values.Where(fx => fx.IsPlaying); } }
        public IEnumerable<IEffect> AllPausedFX { get { return Effects.Values.Where(fx => fx.IsPaused); } }

        public SFX()
        {
            SessionIDHeadphones = audioManager.GenerateAudioSessionId();
            SessionIDSpeakers = audioManager.GenerateAudioSessionId();
        }
        public Effect Register(string name, int resourceID, string groupName = null)
        {
            Effect e = new Effect(name, resourceID, groupName);
            RegisterEffect(e, groupName);
            e.PrepForPlayback();
            return e;
        }

        public IEffect Preregister(string name, int resourceID, string groupName = null)
        {
            return RegisterEffect(new Effect(name, resourceID, groupName), groupName);
        }

        public IEffect Preregister(string name, int resourceID, double startSecs, double endSecs, string groupName = null)
        {
            var e = new TrimmedEffect(name, resourceID, startSecs, endSecs, groupName);
            return RegisterEffect(e, groupName);
        }

        public IEffect RegisterEffect(IEffect effect, string groupName = null)
        {
            // Add or update the dictionary entry.
            if (!Effects.ContainsKey(effect.Name)) Effects.Add(effect.Name, effect);
            else Effects[effect.Name] = effect;

            // If present, the group name can be requested instead of a specific effect's name;
            // the result will be a randomly chosen entry from those in the group.
            // This call is a no-op if the group name is null.
            AddToGroup(effect, groupName);

            return effect;
        }

        public void AddToGroup(IEffect effect, string groupName)
        {
            if (groupName == null) return;

            if (!Effects.ContainsKey(groupName))
            {
                Effects.Add(groupName, new EffectGroup(groupName));
            }
            else if (Effects[groupName] is EffectGroup)
            {
                var e = Effects[groupName] as EffectGroup;
                if (!e.Effects.Contains(effect)) e.Effects.Add(effect);
            }
            else throw new ArgumentException($"SFX: Cannot add {effect.Name} to {groupName}; {groupName} is already present and is not a group.");
        }

        public void RemoveFromGroup(Effect effect, string groupName)
        {
            var e = Effects[groupName] as EffectGroup;
            e?.Effects.Remove(effect);
        }

        public void Unregister(Effect effect)
        {
            Effects.Remove(effect.Name);
            foreach (var fx in Effects.Values)
            {
                var fxGrp = fx as EffectGroup;
                if (fxGrp != null && fxGrp.Effects.Contains(effect)) fxGrp.Effects.Remove(effect);
            }
        }

        public void PlaySound(string name)
        {
            if (Effects.ContainsKey(name))
            {
                Effects[name].Activate();
                Effects[name].Play();
            }
            else Log.Warn("SFX", "Sound " + name + " not found.");
        }
        public static void Play(string name)
        {
            Res.SFX.PlaySound(name);
        }
        public static Effect PlayByID(Activity activity, int resourceID)
        {
            var e = new Effect(activity.Resources.GetResourceEntryName(resourceID), resourceID);
            e.Play();
            e.WhenFinishedPlaying.ContinueWith(_ => { e.Deactivate(); e.Dispose(); });
            return e;
        }

        public void PauseSound(string name)
        {
            if (!Effects.ContainsKey(name)) return;
            Effects[name].Pause();     
        }

        public void StopAll()
        {
            foreach (var fx in Effects.Values)
            {
                if (fx.IsActive) fx.Stop();
            }
        }

        public static int NumberOfMediaPlayersCreated = 0;
        public static AudioManager audioManager = Application.Context.GetSystemService(Context.AudioService) as AudioManager;
        public static int SessionIDSpeakers, SessionIDHeadphones; 

    }

    public interface IEffect : IActivator
    {
        string Name { get; }
        void SeekTo(int msec);
        bool Looping { get; set; }
        bool IsPlaying { get; }
        bool IsPaused { get; }
        void Play(double? playVolume = null, bool? playLooping = null, bool? useSpeakers = null, bool stopIfNecessary = false);
        Task PlayToCompletion(double? playVolume = null, bool? useSpeakers = null);
        Task PlayFromTo(double startSeconds = 0, double endSeconds = -1, double? playVolume = null, bool? playLooping = null, bool? useSpeakers = null, bool stopIfNecessary = false);
        void Pause();
        void Stop();
        Task WhenFinishedPlaying { get; }
        TimeSpan RunTime { get; }
        double Volume { get; set; }
        void SetVolume(double vol, double? vol2 = null);
        double Pitch { get; set; }
        void SetPitch(double pitch);
        double Speed { get; set; }
        void SetSpeed(double speed);
    }

    public class EffectGroup : IEffect
    {
        public List<IEffect> Effects { get; set; }
        private IEffect _lastSelectedEffect = Effect.None;
        private IEffect _currentSelectedEffect;
        private IEffect _selectedEffect
        {
            get { return _currentSelectedEffect; }
            set
            {
                _lastSelectedEffect = _currentSelectedEffect ?? _lastSelectedEffect;
                _currentSelectedEffect = value;
            }
        }
        public string Name { get; set; }

        public EffectGroup(string name, params IEffect[] effects)
        {
            Name = name;
            Effects = effects.ToList();
        }
        public Task WhenFinishedPlaying { get { return _selectedEffect?.WhenFinishedPlaying.ContinueWith((t) => _selectedEffect = null) ?? Task.CompletedTask; } }

        public bool IsPlaying { get { return _selectedEffect?.IsPlaying ?? false; } }
        public bool IsPaused { get { return _selectedEffect?.IsPaused ?? false; } }
        public bool IsActive { get { return _selectedEffect?.IsActive ?? false; } }

        public bool Looping { get { return _selectedEffect?.Looping ?? false; } set { if (_selectedEffect != null) _selectedEffect.Looping = value; } }

        public CancellationToken StopToken { get { return _selectedEffect?.StopToken ?? CancellationToken.None; } }
        public TimeSpan RunTime { get { return _selectedEffect?.RunTime ?? TimeSpan.Zero; } }

        public void Pause() { _selectedEffect?.Pause(); }
        public void Stop() { _selectedEffect?.Stop(); _selectedEffect = null; }
        public void SeekTo(int msec) { _selectedEffect?.SeekTo(msec); }
        public double Volume { get { return _selectedEffect?.Volume ?? 1.0f; } set { _selectedEffect?.SetVolume(value); } }
        public void SetVolume(double vol, double? vol2 = null) { _selectedEffect?.SetVolume(vol, vol2); }
        public double Pitch { get { return _selectedEffect?.Pitch ?? 1.0f; } set { _selectedEffect?.SetPitch(value); } }
        public void SetPitch(double pitch) { _selectedEffect?.SetPitch(pitch); }
        public double Speed { get { return _selectedEffect?.Speed ?? 1.0f; } set { _selectedEffect?.SetSpeed(value); } }
        public void SetSpeed(double speed) { _selectedEffect?.SetSpeed(speed); }

        private CancellationTokenSource _newEffectCTS;

        public void Play(double? playVolume = null, bool? playLooping = null, bool? useSpeakers = null, bool stopIfNecessary = false)
        {
            if (Effects.Count == 0) return;

            // If an effect is already playing, CANCEL its attempt to reset _selectedEffect to null upon completion.
            _newEffectCTS?.Cancel();
            _newEffectCTS = new CancellationTokenSource();

            _selectedEffect = _selectedEffect ?? SelectRandomEffect(_lastSelectedEffect);

            // NOTE - I don't think the below is actually being triggered, but nonetheless as a belt-and-suspenders thing I figure it can stay in.
            // Basically it does what it says on the tin - it picks a *different* sound effect from this group and sets it up as a fallback should the first one throw.
            if (Effects.Count > 1)
            {
                var sEff = _selectedEffect as Effect;
                var subEff = SelectRandomEffect(_selectedEffect);
                //Log.Debug("SFX|Group Substitution", $"Assigning {subEff.Name} as a sub for {sEff.Name}");
                PlaySubstitute = (o, e) =>
                {
                    if (sEff != null) sEff.Error -= PlaySubstitute;
                    _selectedEffect = subEff;
                    Log.Debug("SFX|Group Substitution", $"Error playing {sEff.Name}.  Attempting to sub in {subEff.Name} instead.");
                    Play(playVolume, playLooping, useSpeakers);
                };
                sEff.Error += PlaySubstitute;
            }

            _selectedEffect.WhenFinishedPlaying.ContinueWith(t => _selectedEffect = null, _newEffectCTS.Token);
            _selectedEffect.Play(playVolume, playLooping, useSpeakers, stopIfNecessary);
        }

        public EventHandler<MediaPlayer.ErrorEventArgs> PlaySubstitute;

        public Task PlayToCompletion(double? playVolume = null, bool? useSpeakers = null)
        {
            if (Effects.Count == 0) return TaskConstants.Canceled;
            Play(playVolume, false, useSpeakers);
            return WhenFinishedPlaying;
        }

        public async Task PlayFromTo(double startSeconds = 0, double endSeconds = -1,
            double? playVolume = null, bool? playLooping = null, bool? useSpeakers = null, bool stopIfNecessary = false)
        {
            _selectedEffect = _selectedEffect ?? SelectRandomEffect();
            await _selectedEffect.PlayFromTo(startSeconds, endSeconds, playVolume, playLooping, useSpeakers, stopIfNecessary);
            _selectedEffect = null;
        }

        public IEffect SelectRandomEffect(IEffect exceptThisOne = null)
        {
            //if (Effects.Count == 0) return null;
            var validEffects = Effects.Where(fx => (fx != exceptThisOne) && (!fx.IsPlaying)).ToList();
            if (validEffects.Count == 0) return Effect.None;
            return validEffects.GetRandom();
        }

        public void Activate(CancellationToken? externalToken = null)
        {
            _selectedEffect = SelectRandomEffect();
            //foreach (var fx in Effects) (fx as Effect)?.ActivateIfNecessary(externalToken);
            (_selectedEffect as Effect)?.ActivateIfNecessary(externalToken);
        }
        public void Deactivate()
        {
            foreach (var fx in Effects) if (fx == _selectedEffect || !fx.IsPlaying) fx.Deactivate();
            _selectedEffect = null;
        }

        public void DependsOn(CancellationToken token, Activator.Options options = Activator.Options.Default)
        {
            token.Register(Deactivate);
        }
    }

    //public class EffectQueue : EffectGroup
    //{
    //    public EffectQueue(string name, IEffect[] effects) 
    //        : base(name, effects)
    //    { }

    //    public EffectQueue(string name, IEffect effect, int count) 
    //        : base(name, Enumerable.Repeat(effect, count).ToArray())
    //    { }

    //    private List<IEffect> _effectsPlaying { get; set; } = new List<IEffect>();
    //    public new Task WhenFinishedPlaying { get { return Task.WhenAll(_effectsPlaying.Select(ef => ef.WhenFinishedPlaying)); } }

    //    public new bool IsPlaying { get { return _effectsPlaying?.Any(ef => ef.IsPlaying) ?? false; } }
    //    public new bool IsPaused { get { return _effectsPlaying?.Any(ef => ef.IsPaused) ?? false; } }
    //    public new bool IsActive { get { return _effectsPlaying?.Any(ef => ef.IsActive) ?? false; } }

    //    private bool _looping = false;
    //    public new bool Looping { get { return _looping; } set { _looping = value; } }

    //    protected CancellationTokenSource cts;
    //    protected CancellationTokenRegistration? stopRegistration = null;
    //    public new CancellationToken StopToken { get { return cts?.Token ?? CancellationToken.None; } }
    //    public new void DependsOn(CancellationToken token)
    //    {
    //        if (token.CanBeCanceled || cts == null)
    //            cts = CancellationTokenSource.CreateLinkedTokenSource(StopToken, token);
    //        stopRegistration?.Dispose();
    //        stopRegistration = cts.Token.Register(Stop);
    //    }

    //    private DateTime? _startTime;
    //    public new TimeSpan RunTime { get { return DateTime.Now - (_startTime ?? DateTime.Now); } }

    //    public void Pause() { _selectedEffect?.Pause(); }
    //    public void Stop() { _selectedEffect?.Stop(); _selectedEffect = null; }
    //    public void SeekTo(int msec) { _selectedEffect?.SeekTo(msec); }
    //    public double Volume { get { return _selectedEffect?.Volume ?? 1.0f; } set { _selectedEffect?.SetVolume(value); } }
    //    public void SetVolume(double vol, double? vol2 = null) { _selectedEffect?.SetVolume(vol, vol2); }
    //    public double Pitch { get { return _selectedEffect?.Pitch ?? 1.0f; } set { _selectedEffect?.SetPitch(value); } }
    //    public void SetPitch(double pitch) { _selectedEffect?.SetPitch(pitch); }
    //    public double Speed { get { return _selectedEffect?.Speed ?? 1.0f; } set { _selectedEffect?.SetSpeed(value); } }
    //    public void SetSpeed(double speed) { _selectedEffect?.SetSpeed(speed); }

    //    public void Play(double? playVolume = null, bool? playLooping = null, bool? useSpeakers = null)
    //    {
    //        if (Effects.Count == 0) return;
    //        _selectedEffect = _selectedEffect ?? SelectRandomEffect();

    //        // NOTE - I don't think the below is actually being triggered, but nonetheless as a belt-and-suspenders thing I figure it can stay in.
    //        // Basically it does what it says on the tin - it picks a *different* sound effect from this group and sets it up as a fallback should the first one throw.
    //        if (Effects.Count > 1)
    //        {
    //            var sEff = _selectedEffect as Effect;
    //            var subEff = SelectRandomEffect(_selectedEffect);
    //            //Log.Debug("SFX|Group Substitution", $"Assigning {subEff.Name} as a sub for {sEff.Name}");
    //            PlaySubstitute = (o, e) =>
    //            {
    //                if (sEff != null) sEff.Error -= PlaySubstitute;
    //                _selectedEffect = subEff;
    //                Log.Debug("SFX|Group Substitution", $"Error playing {sEff.Name}.  Attempting to sub in {subEff.Name} instead.");
    //                Play(playVolume, playLooping, useSpeakers);
    //            };
    //            sEff.Error += PlaySubstitute;
    //        }

    //        _selectedEffect.WhenFinishedPlaying.ContinueWith(t => _selectedEffect = null);
    //        _selectedEffect.Play(playVolume, playLooping, useSpeakers);
    //    }

    //    public void PlayOverSpeakers(double? playVolume = null, bool? playLooping = null)
    //    {
    //        //if (Effects.Count == 0) return;
    //        //_selectedEffect = _selectedEffect ?? SelectRandomEffect();
    //        //_selectedEffect.WhenFinishedPlaying.ContinueWith(t => _selectedEffect = null);
    //        //_selectedEffect.PlayOverSpeakers(playVolume, playLooping);
    //        Play(playVolume, playLooping, true);
    //    }
    //    public void PlayOverHeadphones(double? playVolume = null, bool? playLooping = null)
    //    {
    //        //if (Effects.Count == 0) return;
    //        //_selectedEffect = _selectedEffect ?? SelectRandomEffect();
    //        //_selectedEffect.WhenFinishedPlaying.ContinueWith(t => _selectedEffect = null);
    //        //_selectedEffect.PlayOverHeadphones(playVolume, playLooping);
    //        Play(playVolume, playLooping, false);
    //    }

    //    public EventHandler<MediaPlayer.ErrorEventArgs> PlaySubstitute;

    //    public Task PlayToCompletion(double? playVolume = null, bool? useSpeakers = null)
    //    {
    //        if (Effects.Count == 0) return TaskConstants.Canceled;
    //        Play(playVolume, false, useSpeakers);
    //        return WhenFinishedPlaying;
    //    }

    //    public async Task PlayFromTo(double startSeconds = 0, double endSeconds = -1,
    //        double? playVolume = null, bool? playLooping = null, bool? useSpeakers = null)
    //    {
    //        _selectedEffect = _selectedEffect ?? SelectRandomEffect();
    //        await _selectedEffect.PlayFromTo(startSeconds, endSeconds, playVolume, playLooping, useSpeakers);
    //        _selectedEffect = null;
    //    }

    //    public IEffect SelectRandomEffect(IEffect exceptThisOne = null)
    //    {
    //        if (Effects.Count == 0) return null;
    //        else return Effects.Where(fx => fx.Name != exceptThisOne?.Name).ToList().GetRandom();
    //    }

    //    public void Activate(CancellationToken? externalToken = null)
    //    {
    //        _selectedEffect = SelectRandomEffect();
    //        //foreach (var fx in Effects) (fx as Effect)?.ActivateIfNecessary(externalToken);
    //        (_selectedEffect as Effect)?.ActivateIfNecessary(externalToken);
    //    }
    //    public void Deactivate()
    //    {
    //        foreach (var fx in Effects) if (fx == _selectedEffect || !fx.IsPlaying) fx.Deactivate();
    //        _selectedEffect = null;
    //    }

    //    public void DependsOn(CancellationToken token)
    //    {
    //        token.Register(Deactivate);
    //    }
    //}

    public class Effect : Java.Lang.Object, IEffect
    {
        private volatile MediaPlayer __innerEffect, __shadowEffect;
        private object __syncRoot = new object();
        protected MediaPlayer _effect;

        protected int _resourceID;
        public List<EffectGroup> GroupsBelongedTo = new List<EffectGroup>();
        public string Name { get; set; } = "notYetAssigned";

        protected TaskCompletionSource eventualEndOfPlaybackSignal; // Is non-null if we might *eventually* play it.
        protected TaskCompletionSource endOfPlaybackSignal; // Is non-null only when it IS playing.
        protected TaskCompletionSource unPauseSignal; // Is non-null only if it IS paused.
        protected TaskCompletionSource resumeSignal; // Is non-null only if it IS suspended.

        protected CancellationTokenSource cts;
        protected CancellationTokenRegistration? stopRegistration = null;
        public CancellationToken StopToken { get { return cts?.Token ?? CancellationToken.None; } }
        public void DependsOn(CancellationToken token, Activator.Options options = Activator.Options.Default)
        {
            if (token.CanBeCanceled || cts == null)
                cts = CancellationTokenSource.CreateLinkedTokenSource(StopToken, token);
            stopRegistration?.Dispose();
            stopRegistration = cts.Token.Register(Stop);
        }
        
        public bool IsActive { get { return _effect != null; } }
        public bool IsPlaying { get { return endOfPlaybackSignal != null && IsActive; } }
        public bool IsPaused { get { return unPauseSignal != null && IsPlaying; } }

        public TimeSpan RunTime { get { return TimeSpan.FromMilliseconds(_effect?.CurrentPosition ?? 0); } }

        public Effect(string name, int resourceID, string groupName = null)
        {
            // Cache this detail but don't use it right away; only Create( ) the media player once warned we'll need it.
            _resourceID = resourceID;

            Name = name;
            Res.SFX.AddToGroup(this, groupName); // No-op if groupName is empty.

            DependsOn(Res.SFX.StopAllToken);
            eventualEndOfPlaybackSignal = new TaskCompletionSource();
        }

        // Lifecycle management... Activate/Deactivate cause the inner MediaPlayer to be / not be valid, along with our cts & error handlers.
        public void Activate(CancellationToken? cancelToken = null)
        {
            Invalidate();
            DependsOn(cancelToken ?? CancellationToken.None);

            // Generate the inner effect first
            if (__innerEffect == null)
            {
                Generate();
            }
            else Log.Debug("SFX|Activate", $"Activating - __innerEffect in {Name} didn't Invalidate properly.");

            // Then wait for it to be valid and then assign it to the outer one.
            if (_effect == null || _effect != __innerEffect)
            {
                _effect = __innerEffect.EnsureValid(__syncRoot, Name);
            }
            else Log.Debug("SFX|Activate", $"Activating - _effect in {Name} didn't Invalidate properly.");

            Completion += OnPlayCompletion;
            Error += OnPlayError;
        }

        public void Invalidate()
        {
            _effect = null;
            if (__innerEffect != null)
            {
                if (__innerEffect.IsPlaying) __innerEffect.Stop();
                __innerEffect.Reset();
                __innerEffect.Release();
                __innerEffect = null;
                SFX.NumberOfMediaPlayersCreated--;
            }
        }

        private bool _useSpeakerMode = false;
        public void Generate(bool? useSpeakerMode = null)
        {
            AudioAttributes aa;
            int SessionID = SFX.audioManager.GenerateAudioSessionId();

            if (Res.AllowSpeakerSounds)
                _useSpeakerMode = useSpeakerMode ?? _useSpeakerMode;
            else _useSpeakerMode = false;

            if (_useSpeakerMode)
            {
                aa = new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.NotificationRingtone)
                    .Build();
                //SessionID = SFX.SessionIDSpeakers;
                
            }
            else
            {
                aa = new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Unknown)
                    .Build();
                //SessionID = SFX.SessionIDHeadphones;
            }

            __innerEffect = MediaPlayer.Create(Application.Context, _resourceID, aa, SessionID);
            SFX.NumberOfMediaPlayersCreated++;
        }

        public bool SpeakerMode
        {
            get { return (Res.AllowSpeakerSounds) ?_useSpeakerMode : false; }
            //set
            //{
            //    if (value == _useSpeakerMode) return;
            //    if (IsPlaying)
            //    {
            //        WhenFinishedPlaying
            //            .ContinueWith((t) => { SpeakerMode = value; }, TaskContinuationOptions.OnlyOnRanToCompletion)
            //            .LaunchAsOrphan($"Delayed-setting SpeakerMode to {value}");
            //        return;
            //    }
            //    bool wasActive = IsActive;
            //    Deactivate();
            //    _useSpeakerMode = value;
            //    if (wasActive) Activate();
            //}
            set { _useSpeakerMode = (Res.AllowSpeakerSounds) ? value : false; } // Simpler version, because it only takes effect during Generate() anyway, so no on-the-fly changes need be supported.
        }

        public void ActivateIfNecessary(CancellationToken? token = null)
        {
            if (IsActive)
            {
                DependsOn(token ?? CancellationToken.None);
            }
            else
            {
                Activate(token);
            }
        }

        public void Deactivate()
        {
            cts?.Cancel();
            stopRegistration?.Dispose();
            cts = null;

            Completion -= OnPlayCompletion;
            Error -= OnPlayError;
            
            Invalidate();
        }
        
        //public virtual void Play(double? playVolume = null, bool? playLooping = null)
        //{
        //    _play(playVolume, playLooping); // This level of indirection lets us more cleanly invoke "Play()" from derived classes which have to override the function actually named Play().
        //}

        public virtual void Play(double? playVolume = null, bool? playLooping = null, bool? useSpeakers = null, bool stopIfNecessary = false)
        {
            if (stopIfNecessary && IsPlaying)
            {
                Pause();
                SeekTo(0);
            }
            if (useSpeakers == null || useSpeakers == SpeakerMode || !Res.AllowSpeakerSounds) _play(playVolume, playLooping);
            else
            {
                SpeakerMode = !SpeakerMode;
                _play(playVolume, playLooping);
                SpeakerMode = !SpeakerMode;
            }
        }

        public virtual void _play(double? playVolume = null, bool? playLooping = null)
        {
            try
            {
                PrepForPlayback(playVolume, playLooping);
                PlaybackStarting?.Invoke(this, EventArgs.Empty);
                if (IsPaused)
                {
                    unPauseSignal.TrySetResult();
                }
                else Start();
            }
            catch (Java.Lang.IllegalStateException)
            {

                throw;
            }
        }

        protected void Start()
        {
            if (IsActive && !_effect.IsPlaying) _effect.Start();
        }

        public async void Pause()
        {
            if (IsPlaying) _effect?.Pause();
            unPauseSignal = new TaskCompletionSource();

            // Pause (sic) here!  Wait for either the stop token (i.e. cancel the pause) or the unPause signal.
            if (!await unPauseSignal.Task.Before(StopToken)) return;

            unPauseSignal = null;
            Start();
        }

        public void PrepForPlayback(double? playVolume = null, bool? playLooping = null)
        {
            if (!IsActive)
            {
                Activate();
                //IsPrepared().Wait();
                //SpinWait.SpinUntil(() => _effect != null);
                //if (!await IsPrepared().Before(Task.Delay(500)))
                //    Log.Error("Prepare For Playback", "Failure to prep file in time??");
                _effect = __innerEffect.EnsureValid(__syncRoot);
                //_effect = await __innerEffect.EnsureValidAsync(__syncRoot);
            }

            Volume = playVolume ?? Volume;
            Looping = playLooping  ?? defaultToLooping;

            endOfPlaybackSignal = eventualEndOfPlaybackSignal ?? new TaskCompletionSource();
        }

        public void Stop()
        {
            if (IsActive) //_effect.Reset();
            {
                //_effect.Pause();
                //SeekTo(0);

                _effect.Stop();
                //_effect.Reset();
                Invalidate();
            }
            eventualEndOfPlaybackSignal = new TaskCompletionSource();
            endOfPlaybackSignal = null;
            unPauseSignal = null;
        }

        public void SeekTo(int msec) { if (IsActive) _effect.SeekTo(msec); _seekLocation = msec; }
        public bool Looping {
            get { _looping = _effect?.Looping ?? false; return _looping; }
            set
            {
                _looping = value;
                if (_effect != null) { _effect.Looping = value; return; }
                else PlaybackStarting += setLooping; // No longer relevant?  Looping ought to be settable in any state but Error.
            } }

        public virtual async Task PlayFromTo(double startSeconds = 0, double endSeconds = -1, 
            double? playVolume = null, bool? playLooping = null, bool? useSpeakers = null, bool stopIfNecessary = false)
        {
            if (stopIfNecessary && IsPlaying) Stop();
            SeekTo((int)(startSeconds * 1000));
            if (useSpeakers == null || useSpeakers == SpeakerMode) _play(playVolume, playLooping);
            else
            {
                SpeakerMode = !SpeakerMode;
                _play(playVolume, playLooping);
                SpeakerMode = !SpeakerMode;
            }
            if (endSeconds == -1) return;
            var endMsecs = (int)(endSeconds * 1000);
            //await Task.Delay((int)((endSeconds - startSeconds) * 1000)); // Won't be true if we pause or skip around - but for now I don't intend to.
            var remainingTime = endMsecs - _effect.CurrentPosition;
            while (remainingTime > 0)
            {
                if (IsPaused) await unPauseSignal.Task;
                await Task.Delay(remainingTime.Clamp(10, 250));
                remainingTime = endMsecs - _effect.CurrentPosition;
            }
            Stop();
            OnPlayCompletion(this, EventArgs.Empty);
        }

        protected int _seekLocation = 0;
        public int CurrentLocation { get { return _effect?.CurrentPosition ?? _seekLocation; } }

        protected bool _looping = false;
        private void setLooping(object sender, EventArgs e) { Looping = _looping; PlaybackStarting -= setLooping; }
        public bool defaultToLooping = false; // Set this so that it will default to that state; does not change current settings at all.

        protected double _volume = 1.0f;
        public double Volume { get { return _volume; } set { SetVolume(value); } }
        public void SetVolume(double vol, double? vol2 = null)
        {
            if (!IsActive) Activate();
            _effect?.SetVolume((float)vol, (float)(vol2 ?? vol));
            _volume = vol;
        }

        protected double _pitch = 1.0;
        public double Pitch { get { return _pitch; } set { SetPitch(value); } }
        public void SetPitch(double pitch)
        {
            _pitch = pitch;
            if (_effect == null) return;
            _effect.PlaybackParams.SetPitch((float)pitch);
            if (_effect.PlaybackParams.Pitch != pitch) Log.Debug("SFX", $"Warning - you've invoked Effect.Pitch or SetPitch( ), which just don't work in Xamarin currently.");
        }

        protected double _speed = 1.0;
        public double Speed { get { return _speed; } set { SetSpeed(value); } }
        public void SetSpeed(double speed)
        {
            _speed = speed;
            if (_effect == null) return;
            _effect.PlaybackParams.SetSpeed((float)speed);
            if (_effect.PlaybackParams.Speed != speed) Log.Debug("SFX", $"Warning - you've invoked Effect.Speed or SetSpeed( ), which just don't work in Xamarin currently.");
        }

        public Task PlayToCompletion(double? playVolume = null, bool? useSpeakers = null)
        {
            Play(playVolume, false, useSpeakers);
            return endOfPlaybackSignal.Task.SwallowCancellations();
        }
        public Task WhenFinishedPlaying
        {
            get
            {
                if (IsPlaying) return endOfPlaybackSignal.Task.SwallowCancellations();
                else if (IsActive) return eventualEndOfPlaybackSignal.Task.SwallowCancellations();
                else return Task.CompletedTask;
            }
        }

        public void PlayDiminuendo(TimeSpan tenPercentInterval, double? masterVolume = null)
        {
            var OrigVolume = masterVolume ?? Volume;
            Play(OrigVolume, stopIfNecessary: true);
            Task.Run(async () =>
            {
                for (int i = 10; i > 0; i--)
                {
                    Volume = OrigVolume * 0.1f * i;
                    await Task.Delay(tenPercentInterval);
                }
                if (IsPlaying) Stop();
            }).LaunchAsOrphan($"{Name} diminuendo");
        }

        public event EventHandler Completion { add { if (_effect != null) _effect.Completion += value; } remove { if (_effect != null) _effect.Completion -= value; } }
        public event EventHandler<MediaPlayer.ErrorEventArgs> Error { add { if (_effect != null) _effect.Error += value; } remove { if (_effect != null) _effect.Error -= value; } }
        public event EventHandler PlaybackStarting;

        public void OnPlayCompletion(object sender, EventArgs e)
        {
            //Log.Info("SFX", $"Completed playing {this.Name}.");
            endOfPlaybackSignal?.TrySetResult();
            endOfPlaybackSignal = null;

            // Debugging?
            Invalidate();
        }

        protected bool _alreadyInErrorState = false;
        protected int _retries = 0;
        public async void OnPlayError(object sender, MediaPlayer.ErrorEventArgs e)
        {
            //lock (__syncRoot)
            var _mutex = new AsyncLock();
            using (_mutex.Lock(StopToken)) 
            {
                var wasPlaying = IsPlaying;
                var wasPaused = IsPaused;
                var wasAtLocation = CurrentLocation;

                var s = $"Who: {e.Mp}/{this.Name}, what: {e.What}, also: {e.Extra}.  There are {SFX.NumberOfMediaPlayersCreated} media players in residence.";
                Log.Error("SFX|Error", s);

                //Deactivate();
                //__invalidate(); // Suspend all other operations on the current target.
                Invalidate();

                return; // Debugging!
                // Let's try a full restart of the sound in question instead - as long as it hasn't already been tried.
                // For now, a given effect only gets one such chance, until I see if it works.
                if (!_alreadyInErrorState)
                {
                    _retries++;
                    _alreadyInErrorState = Res.Random < 0.1; // Check a few times in succession, maybe it'll work?  Definitely it's time dependent.
                    await Task.Delay(50);
                    Activate();
                    _effect = await _effect.EnsureValidAsync(__syncRoot, Name); // A triple-check that it's valid before we proceed.
                    if (wasAtLocation > 0) SeekTo(wasAtLocation);
                    if (wasPlaying) Play();
                    if (wasPaused) Pause();
                    //e.Handled = true;
                    //await WhenFinishedPlaying.ContinueWith((t) => { _alreadyInErrorState = false; }, TaskContinuationOptions.NotOnFaulted);
                }
                else
                {
                    Log.Error("SFX|Error", $"Sound effect {Name} tried again, for the {_retries}th time, and it still threw.  Count 'im out.");
                    _retries = 0;
                    _alreadyInErrorState = false;
                }
            }
        }

        new public void Dispose()
        {
            Deactivate();
            if (__shadowEffect != null)
            {
                if (__shadowEffect.IsPlaying) __shadowEffect.Stop();
                __shadowEffect.Reset();
                __shadowEffect.Release();
                __shadowEffect = null;
                SFX.NumberOfMediaPlayersCreated--;
            }
            Res.SFX.Unregister(this);
        }
        
        // Static zero entities which nonetheless are legal and won't throw.  MUCH trickier than it sounds!
        public static Effect None { get; private set; }
        public static MediaPlayer NonePlayer { get { return _nonePlayer; } }
        private static MediaPlayer _nonePlayer = new MediaPlayer(); // A backup to the backup.
        static Effect()
        {
            _nonePlayer = MediaPlayer.Create(Application.Context, Resource.Raw.silence1s).EnsureValid();
            None = new Effect("SFX.None", Resource.Raw.silence1s);
        }
    }

    public class TrimmedEffect : Effect
    {
        protected double StartSecs;
        protected double EndSecs;

        public TrimmedEffect(string name, int resourceID, double startSecs = 0, double endSecs = -1, string groupName = null)
            : base(name, resourceID, groupName)
        {
            StartSecs = startSecs;
            EndSecs = endSecs;
        }

        public override void Play(double? playVolume = null, bool? playLooping = null, bool? useSpeakers = null, bool stopIfNecessary = false)
        {
            PlayFromTo(StartSecs, EndSecs, playVolume, playLooping, useSpeakers, stopIfNecessary).LaunchAsOrphan($"TrimmedEffect_{Name}");
        }

        public override Task PlayFromTo(double startSeconds = 0, double endSeconds = -1, double? playVolume = default(double?), bool? playLooping = default(bool?), bool? useSpeakers = default(bool?), bool stopIfNecessary = false)
        {
            var end = (endSeconds == -1) ? EndSecs : endSeconds + StartSecs;
            return base.PlayFromTo(startSeconds + StartSecs, end, playVolume, playLooping, useSpeakers);
        }
    }

    public static class SFXExtensions
    {
        public static bool IsValid(this MediaPlayer source)
        {
            try
            {
                if (source == null) return false;
                source.Looping = source.Looping; // A no-op that *should* throw if it's in an invalid state.
                return true;
            }
            catch (Java.Lang.IllegalStateException)
            {
                return false;
            }
        }

        public static MediaPlayer EnsureValid(this MediaPlayer source, object _syncRoot = null, string label = "Unknown")
        {
            lock (_syncRoot ?? new object())
            { 
                if (source.IsValid() || source == null) return source;
                //if (SpinWait.SpinUntil(() => source.IsValid(), 200)) { Log.Debug("SFX", "Came out in the spin cycle."); return source; }
                foreach (int i in Enumerable.Range(0, 100))
                {
                    Task.Delay(50).Wait(51);
                    if (source.IsValid())
                    {
                        Log.Debug("SFX", $"|||||||||||||||| {label} found valid after {i} iterations of 50ms each."); return source;
                    }
                }
                Log.Error("SFX", $"{label} invalid after 100 reps.  Substituting silence.");
                return Effect.NonePlayer;
            }
        }

        public static Task<MediaPlayer> EnsureValidAsync(this MediaPlayer source, object _syncRoot = null, string label = "Unknown")
        {
            return Task.FromResult(source.EnsureValid(_syncRoot, label));
        }
    }
    
}