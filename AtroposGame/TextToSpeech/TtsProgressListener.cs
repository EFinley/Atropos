using System;
using System.Threading.Tasks;
using Android.Speech.Tts;
using Nito.AsyncEx;
using System.Collections.Generic;

namespace com.Atropos.TextToSpeech
{
	/// <summary>
	/// Tts progress listener.
	/// </summary>
    public class TtsProgressListener : UtteranceProgressListener
    {
        //public readonly TaskCompletionSource completionSource;
        readonly AsyncManualResetEvent completionFlag;
        public Dictionary<string, TaskCompletionSource> completionSources;
        public TaskCompletionSource startTCS;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Plugin.TextToSpeech.TtsProgressListener"/> class.
		/// </summary>
		/// <param name="tcs">Tcs.</param>
        public TtsProgressListener(ref TaskCompletionSource tcs)
        {
            //completionSource = tcs;
        }

        public TtsProgressListener()
        {
            //completionSource = new TaskCompletionSource();
            completionSources = new Dictionary<string, TaskCompletionSource>();
        }

        public TtsProgressListener(AsyncManualResetEvent flag)
        {
            completionFlag = flag;
        }

        public Task IsDone()
        {
            //return completionSource.Task;
            return TaskConstants.Never;
        }

        public Task ListenFor(string utteranceID)
        {
            var tcs = new TaskCompletionSource();
            completionSources.Add(utteranceID, tcs);
            return tcs.Task;
        }

        public Task ListenForStart()
        {
            startTCS = new TaskCompletionSource();
            return startTCS.Task;
        }

        /// <summary>
        /// Callback function which is called upon utterance being done (member of UtteranceProgressListener base abstract class)
        /// </summary>
        /// <param name="utteranceId">Utterance identifier.</param>
        public override void OnDone(string utteranceId)
        {
            if (completionSources.ContainsKey(utteranceId))
            {
                completionSources[utteranceId].TrySetResult();
                //completionSources.Remove(utteranceId);
            }
            //completionSource?.TrySetResult();
            //completionFlag?.Set();
        }

        /// <summary>
        /// Callback function which is called upon utterance throwing an error (member of UtteranceProgressListener base abstract class)
        /// </summary>
        /// <param name="utteranceId">Utterance identifier.</param>
        public override void OnError(string utteranceId)
        {
            if (completionSources.ContainsKey(utteranceId))
            {
                completionSources[utteranceId].TrySetException(new ArgumentException("Error with TTS engine on progress listener"));
                completionSources.Remove(utteranceId);
            }
            //completionSource.TrySetException(new ArgumentException("Error with TTS engine on progress listener"));
        }

        /// <summary>
        /// Callback function which is called upon utterance starting (member of UtteranceProgressListener base abstract class)
        /// </summary>
        /// <param name="utteranceId">Utterance identifier.</param>
        public override void OnStart(string utteranceId)
        {
            startTCS?.TrySetResult();
            startTCS = null;
        }
    }
}