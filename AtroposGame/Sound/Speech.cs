
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

using Atropos.TextToSpeech;
using Atropos.TextToSpeech.Abstractions;
//using Plugin.TextToSpeech;
//using Plugin.TextToSpeech.Abstractions;
using ASpeech = Android.Speech.Tts;
using System.Threading;
using System.Threading.Tasks;

namespace Atropos 
{
    //public class patchedSpeechService : ASpeech.TextToSpeechService
    //{
    //    private ASpeech.TextToSpeechService _service;

    //    public patchedSpeechService(Context ctx, Android.Speech.Tts.TextToSpeech.IOnInitListener listener, string serviceName = null)
    //    {
    //        if (serviceName == null)
    //        {
    //            var testEngine = new Android.Speech.Tts.TextToSpeech(Application.Context, listener);
    //            serviceName = testEngine.DefaultEngine;
    //            testEngine.Shutdown();
    //        }
    //        _service = (ASpeech.TextToSpeechService)ctx.GetSystemService(serviceName);
    //    }
    //    public IBinder onBind(Intent intent)
    //    {
    //        return _service.OnBind(intent);
    //    }
    //    public void onCreate()
    //    {
    //        _service.OnCreate();
    //    }
    //    public void onDestroy()
    //    {
    //        _service.OnDestroy();
    //    }
    //    public string onGetDefaultVoiceNameFor(string lang, string country, string variant)
    //    {
    //        return _service.OnGetDefaultVoiceNameFor(lang, country, variant);
    //    }
    //    public IList<ASpeech.Voice> onGetVoices()
    //    {
    //        try { return _service.OnGetVoices(); }
    //        catch (Exception e)
    //        {
    //            Log.Info("patchedSpeechService", $"Exception thrown in onGetVoices: {e.GetType().Name} ({e.Message}).  Returning empty list.");
    //            return new List<ASpeech.Voice>();
    //        }
    //    }
    //    public ASpeech.OperationResult onIsValidVoiceName(string voiceName)
    //    {
    //        return _service.OnIsValidVoiceName(voiceName);
    //    }
    //    public ASpeech.OperationResult onLoadVoice(string voiceName)
    //    {
    //        return _service.OnLoadVoice(voiceName);
    //    }

    //}

    //public struct CrossLocale { }
    //public class TextToSpeech
    //{
    //    public void Speak(params object[] stuff) { }
    //    public void Init() { }
    //}

    //public class Listener : Java.Lang.Object, Android.Speech.Tts.TextToSpeech.IOnInitListener
    //{
    //    public System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
    //    public void OnInit([GeneratedEnum] ASpeech.OperationResult status)
    //    {
    //        Log.Debug("TTS", $"Successful OnInit in the simple case, at least, with result {status} after {sw.ElapsedMilliseconds} ms.");
    //    }
    //}

    public struct Utterance
    {
        public string content;
        public string utteranceID;
        public DateTime timeOfUttering;
    }

    public class Speech : Atropos.TextToSpeech.TextToSpeech
    {
        public List<Utterance> RecentUtterances = new List<Utterance>();
        public TimeSpan SuppressUtteranceInterval = TimeSpan.FromMinutes(10.0);

        /// <summary>
        /// Inheriting from https://github.com/jamesmontemagno/TextToSpeechPlugin (version 2.0.0)
        /// gets us these baseline methods:
        ///     this.Speak(text, queue, locale, pitch, volume (nonfunctional on Android))
        ///     
        /// </summary>
        public Speech()
        {
            this.Init().ContinueWith(t => { Log.Debug("TTS", "==================TTS engine initialized============="); }) ;

            //listen = new Listener();
            //listen.sw.Start();
            //tts = new Android.Speech.Tts.TextToSpeech(Application.Context, listen);
        }

        private string[] againPrompts = new string[] { "Again", "As before", "Once more", "Same again", "Ditto", "Like before", "Same instructions" };

        public Task SayNow(string content, stringOrID[] parameters = null, bool queue = false, CrossLocale? crossLocale = null, double? pitch = 1.0, double? speakRate = 1.0, double? volume = 1.0, CancellationToken? cancelToken = null)
        {
            RecentUtterances = RecentUtterances.Where(u => u.timeOfUttering > DateTime.Now - SuppressUtteranceInterval).ToList();
            if (RecentUtterances.Count(u => u.content == content) > 0)
                content = againPrompts.GetRandom();

            //return Speak(utteranceContent, crossLocale, (float?)pitch, (float?)speakRate, (float?)volume);
            //var cts = new CancellationTokenSource();
            var ret = Speak(content, crossLocale, (float?)pitch, (float?)speakRate, (float?)volume, cancelToken);
            //return Task.WhenAny(ret, Task.Delay(200 * utteranceContent.Length));
            //return pollForCompletion();
            
            return ret;
        }

        public static void Say(string content, stringOrID[] parameters = null, 
            bool queue = false, CrossLocale? crossLocale = null, double? pitch = 1.0, 
            double? speakRate = 1.0, double? volume = 1.0, 
            Action doOnStart = null, bool? useSpeakerMode = null,
            CancellationToken? cancelToken = null)
        {
            SayAllOf(content, parameters, queue, crossLocale, pitch, speakRate, volume, doOnStart, useSpeakerMode, cancelToken)
                .LaunchAsOrphan($"Speech: {content}");
        }

        public static void Say(string content, SoundOptions options)
        {
            Say(content, null, false, null,
                options.Pitch ?? 1.0,
                options.Speed ?? 1.0,
                options.Volume ?? 1.0,
                null,
                options.UseSpeakers,
                options.CancelToken);
        }

        public static async Task SayAllOf(string content, stringOrID[] parameters = null,
            bool queue = false, CrossLocale? crossLocale = null, double? pitch = 1.0,
            double? speakRate = 1.0, double? volume = 1.0,
            Action doOnStart = null, bool? useSpeakerMode = null,
            CancellationToken? cancelToken = null)
        {
            bool previousSpeakerMode = Res.Speech.SpeakerMode;

            if (Res.AllowSpeakerSounds)
                useSpeakerMode = useSpeakerMode ?? previousSpeakerMode;
            else useSpeakerMode = false;

            if (doOnStart != null) Task.Run(async () =>
            {
                await Res.Speech.listener.ListenForStart();
                doOnStart();
            }).LaunchAsOrphan("DoOnStart listener");
            await Res.Speech.SayNow(content, parameters, queue, crossLocale, pitch, speakRate, volume, cancelToken);
            Res.Speech.SpeakerMode = previousSpeakerMode;
        }

        public static async Task SayAllOf(string content, SoundOptions options)
        {
            await SayAllOf(content, null, false, null,
                options.Pitch ?? 1.0,
                options.Speed ?? 1.0,
                options.Volume ?? 1.0,
                null,
                options.UseSpeakers,
                options.CancelToken);
        }
    }
}