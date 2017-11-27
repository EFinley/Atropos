using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using com.Atropos.TextToSpeech.Abstractions;
using Java.Util;
using Android.Speech.Tts;
using Android.App;
using Nito.AsyncEx;
using Android.Media;

namespace com.Atropos.TextToSpeech
{
    /// <summary>
    /// Text to speech implementation Android
    /// </summary>
    public class TextToSpeech : Java.Lang.Object, ITextToSpeech, Android.Speech.Tts.TextToSpeech.IOnInitListener, IDisposable
    {
        readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        Android.Speech.Tts.TextToSpeech textToSpeech;
        string text;
        CrossLocale? language;
        float pitch, speakRate, volume;
        bool initialized;

        private readonly AsyncLock _mutex = new AsyncLock();

        public TtsProgressListener listener;
        TaskCompletionSource<bool> initTcs;
        public Task Init()
        {
            if (initialized)
                return Task.FromResult(true);

            this.initTcs = new TaskCompletionSource<bool>();

            Console.WriteLine("Current version: " + (int)global::Android.OS.Build.VERSION.SdkInt);
            Android.Util.Log.Info("CrossTTS", "Current version: " + (int)global::Android.OS.Build.VERSION.SdkInt);
            textToSpeech = new Android.Speech.Tts.TextToSpeech(Application.Context, this);

            listener = new TtsProgressListener();
            textToSpeech.SetOnUtteranceProgressListener(listener);

            return this.initTcs.Task;
            //bool hasThrown = false;
            //bool isValid = false;
            //while (!isValid)
            //{
            //    try
            //    {
            //        textToSpeech.Speak("Initializing", QueueMode.Add, null, null);
            //        isValid = true;
            //    }
            //    catch (Exception e)
            //    {
            //        if (!hasThrown)
            //        {
            //            hasThrown = true;
            //            Android.Util.Log.Debug("TTS", "(At least one) failure to initialize TTS.");
            //        }
            //    } 
            //}
            //return Task.CompletedTask;
        }


        #region IOnInitListener implementation
        /// <summary>
        /// OnInit of TTS
        /// </summary>
        /// <param name="status"></param>
        public void OnInit(OperationResult status)
        {
            if (status.Equals(OperationResult.Success))
            {
                this.initTcs.TrySetResult(true);
                this.initialized = true;
            }
            else
            {
                this.initTcs.TrySetException(new ArgumentException("Failed to initialize TTS engine"));
            }
        }
        #endregion

        /// <summary>
        /// Speak back text
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <param name="crossLocale">Locale of voice</param>
        /// <param name="pitch">Pitch of voice</param>
        /// <param name="speakRate">Speak Rate of voice (All) (0.0 - 2.0f)</param>
        /// <param name="volume">Volume of voice (iOS/WP) (0.0-1.0)</param>
        /// <param name="cancelToken">Canelation token to stop speak</param>
        /// <exception cref="ArgumentNullException">Thrown if text is null</exception>
        /// <exception cref="ArgumentException">Thrown if text length is greater than maximum allowed</exception>
        public async Task Speak(string text, CrossLocale? crossLocale = null, float? pitch = null, float? speakRate = null, float? volume = null, CancellationToken? cancelToken = null)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text), "Text can not be null");

            if (text.Length > MaxSpeechInputLength)
                throw new ArgumentException(nameof(text), "Text length is over the maximum speech input length.");

            try
            {
                using (await _mutex.LockAsync())
                {
                    //await semaphore.WaitAsync(cancelToken ?? CancellationToken.None);
                    this.text = text;
                    this.language = crossLocale;
                    this.pitch = pitch ?? 1.0f;
                    this.speakRate = speakRate ?? 1.0f;
                    this.volume = volume ?? 1.0f;

                    // TODO: need to wait lock so not to break people using queuing mechanism
                    // Dealt with by using LockAsync() ???
                    //await Init();
                    await Speak(cancelToken);
                }
            }
            finally
            {
                //semaphore.Release();
            }
        }


        private void SetDefaultLanguage()
        {
            SetDefaultLanguageNonLollipop();

        }

        private void SetDefaultLanguageNonLollipop()
        {
            //disable warning because we are checking ahead of time.
#pragma warning disable 0618
            var sdk = (int)global::Android.OS.Build.VERSION.SdkInt;
            if (sdk >= 18)
            {

                try
                {

#if __ANDROID_18__
                    if (textToSpeech.DefaultLanguage == null && textToSpeech.Language != null)
                        textToSpeech.SetLanguage(textToSpeech.Language);
                    else if (textToSpeech.DefaultLanguage != null)
                        textToSpeech.SetLanguage(textToSpeech.DefaultLanguage);
#endif
                }
                catch
                {

                    if (textToSpeech.Language != null)
                        textToSpeech.SetLanguage(textToSpeech.Language);
                }
            }
            else
            {
                if (textToSpeech.Language != null)
                    textToSpeech.SetLanguage(textToSpeech.Language);
            }
#pragma warning restore 0618
        }

        async Task Speak(CancellationToken? cancelToken)
        {
            if (string.IsNullOrWhiteSpace(text))
                //return Task.CompletedTask;
                return;

            if (language.HasValue && !string.IsNullOrWhiteSpace(language.Value.Language))
            {
                Locale locale = null;
                if (!string.IsNullOrWhiteSpace(language.Value.Country))
                    locale = new Locale(language.Value.Language, language.Value.Country);
                else
                    locale = new Locale(language.Value.Language);

                var result = textToSpeech.IsLanguageAvailable(locale);
                if (result == LanguageAvailableResult.CountryAvailable)
                {
                    textToSpeech.SetLanguage(locale);
                }
                else
                {
                    Console.WriteLine("Locale: " + locale + " was not valid, setting to default.");
                    SetDefaultLanguage();
                }
            }
            else
            {
                SetDefaultLanguage();
            }

            var tcs = new TaskCompletionSource();
            var flag = new AsyncManualResetEvent();

            var utteranceID = Guid.NewGuid().ToString();
            var doneTask = listener.ListenFor(utteranceID);

            cancelToken?.Register(() =>
            {
                textToSpeech.Stop();
                tcs?.TrySetCanceled();
                listener?.completionSources?[utteranceID]?.TrySetCanceled();
            });

            var utteranceProgressListenerDictionary = new Dictionary<string, string>();
            utteranceProgressListenerDictionary.Add(Android.Speech.Tts.TextToSpeech.Engine.KeyParamUtteranceId, utteranceID);
            utteranceProgressListenerDictionary.Add(Android.Speech.Tts.TextToSpeech.Engine.KeyParamVolume, volume.ToString());

            textToSpeech.SetPitch(pitch);
            textToSpeech.SetSpeechRate(speakRate);

            //textToSpeech.SetOnUtteranceProgressListener(new TtsProgressListener(flag));

            //var bundle = new Android.OS.Bundle();
            //bundle.PutFloat(Android.Speech.Tts.TextToSpeech.Engine.KeyParamVolume, 1.0f);

#pragma warning disable CS0618 // Type or member is obsolete
            //textToSpeech.Speak(text, QueueMode.Flush, bundle, text);
            textToSpeech.Speak(text, QueueMode.Flush, utteranceProgressListenerDictionary);
#pragma warning restore CS0618 // Type or member is obsolete

            //return tcs.Task;
            //return Task.WhenAny(tcs.Task, flag.WaitAsync());
            //return listener.IsDone();

            //int i = 0;
            //var sw = new System.Diagnostics.Stopwatch();
            //sw.Start();
            //while (doneTask.Status != TaskStatus.RanToCompletion)
            //{
            //    Task.Delay(100).Wait(101);
            //    //await Task.Delay(100, CancellationTokenHelpers.Timeout(105).Token);
            //    Android.Util.Log.Debug("Speech", $"Iteration {i}, {sw.ElapsedMilliseconds} ms - status is {doneTask.Status}.");
            //    i++;
            //}

            //return doneTask;
            await doneTask;
        }

        /// <summary>
        /// Get all installed and valid lanaguages
        /// </summary>
        /// <returns>List of CrossLocales</returns>
        public IEnumerable<CrossLocale> GetInstalledLanguages()
        {
            if (textToSpeech != null && initialized)
            {
                int version = (int)global::Android.OS.Build.VERSION.SdkInt;
                bool isLollipop = version >= 21;
                if (isLollipop)
                {
                    try
                    {
                        //in a different method as it can crash on older target/compile for some reason
                        return GetInstalledLanguagesLollipop();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Something went horribly wrong, defaulting to old implementation to get languages: " + ex);
                    }
                }

                var languages = new List<CrossLocale>();
                var allLocales = Locale.GetAvailableLocales();
                foreach (var locale in allLocales)
                {

                    try
                    {
                        var result = textToSpeech.IsLanguageAvailable(locale);

                        if (result == LanguageAvailableResult.CountryAvailable)
                        {
                            languages.Add(new CrossLocale { Country = locale.Country, Language = locale.Language, DisplayName = locale.DisplayName });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error checking language; " + locale + " " + ex);
                    }
                }

                return languages.GroupBy(c => c.ToString())
                      .Select(g => g.First());
            }
            else
            {
                return Locale.GetAvailableLocales()
                  .Where(a => !string.IsNullOrWhiteSpace(a.Language) && !string.IsNullOrWhiteSpace(a.Country))
                  .Select(a => new CrossLocale { Country = a.Country, Language = a.Language, DisplayName = a.DisplayName })
                  .GroupBy(c => c.ToString())
                  .Select(g => g.First());
            }
        }

        /// <summary>
        /// In a different method as it can crash on older target/compile for some reason
        /// </summary>
        /// <returns></returns>
        private IEnumerable<CrossLocale> GetInstalledLanguagesLollipop()
        {
            var sdk = (int)global::Android.OS.Build.VERSION.SdkInt;
            if (sdk < 21)
                return new List<CrossLocale>();

#if __ANDROID_21__
            return textToSpeech.AvailableLanguages
              .Select(a => new CrossLocale { Country = a.Country, Language = a.Language, DisplayName = a.DisplayName });
#endif
        }

        /// <summary>
        /// Gets the max string length of the speech engine
        /// -1 means no limit
        /// </summary>
        public int MaxSpeechInputLength =>
            Android.Speech.Tts.TextToSpeech.MaxSpeechInputLength;

        void IDisposable.Dispose()
        {
            textToSpeech?.Stop();
            textToSpeech?.Dispose();
            textToSpeech = null;
        }

        private bool _useSpeakerMode = false;
        public bool SpeakerMode
        {
            get { return _useSpeakerMode; }
            set
            {
                if (value == _useSpeakerMode) return;

                AudioAttributes aa;
                //int SessionID;

                _useSpeakerMode = value;
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

                if (textToSpeech.SetAudioAttributes(aa) != OperationResult.Success)
                    Android.Util.Log.Warn("TTS", $"Unable to set AudioAttributes to {_useSpeakerMode}.");
            }
        }
    }
}
