using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
//using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using Atropos.Machine_Learning;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using MiscUtil;
using Atropos.Communications;
using Atropos.Communications.Bluetooth;
using static Atropos.Communications.Bluetooth.BluetoothCore;
using Android.Util;

namespace Atropos.Melee
{
    [Serializable]
    public class ChoreographyCue
    {
        //public Classifier Classifier;
        public string ClassifierKey;
        //public GestureClass GestureClass;
        public int GestureClassIndex;
        public DateTime CueTime;
        public double Score;
        public TimeSpan Delay;
        public Guid ExchangeID;
        public bool IsResponse { get => Score != 0.0 && Delay != TimeSpan.Zero; }

        public override string ToString()
        {
            return $"{ClassifierKey}{NEXT}{GestureClassIndex}{NEXT}{CueTime}{NEXT}{Score:f4}{NEXT}{Delay}{NEXT}{ExchangeID}";
        }

        public static ChoreographyCue Parse(string input)
        {
            var substrings = input.Split(onNEXT);
            if (substrings.Length != 6) throw new ArgumentException($"Invalid choreography-key parse string: {input}");
            return new ChoreographyCue()
            {
                ClassifierKey = substrings[0],
                GestureClassIndex = int.Parse(substrings[1]),
                CueTime = DateTime.Parse(substrings[2]),
                Score = Double.Parse(substrings[3]),
                Delay = TimeSpan.Parse(substrings[4]),
                ExchangeID = Guid.Parse(substrings[5])
            };
        }
    }

    public class ExchangeOfBlows
    {
        public Guid ExchangeID;
        public DateTime CueTime;
        public string MyClassifierKey;
        public int MyGestureIndex;
        public string OppClassifierKey;
        public int OppGestureIndex;

        public ChoreographyCue MyCue
        {
            get
            {
                return new ChoreographyCue()
                {
                    ClassifierKey = MyClassifierKey,
                    GestureClassIndex = MyGestureIndex,
                    CueTime = this.CueTime,
                    ExchangeID = this.ExchangeID
                };
            }
        }

        public ChoreographyCue OpponentCue
        {
            get
            {
                return new ChoreographyCue()
                {
                    ClassifierKey = OppClassifierKey,
                    GestureClassIndex = OppGestureIndex,
                    CueTime = this.CueTime,
                    ExchangeID = this.ExchangeID
                };
            }
        }
    }

    public interface IChoreographer : IActivator
    {
        event EventHandler<EventArgs<ChoreographyCue>> OnPromptCue;
        void SubmitResult(ChoreographyCue cue);
    }

    public class SimpleChoreographer : ActivatorBase, IChoreographer
    {
        private IChoreographyGenerator Generator;
        protected Dictionary<string, Classifier> Classifiers;
        //public Classifier ClassifierA { get; set; }
        //public Classifier ClassifierB { get; set; }
        //public IDataset DatasetA { get { return ClassifierA.Dataset; } }
        //public IDataset DatasetB { get { return ClassifierB.Dataset; } }
        //protected int GapMean { get; set; }
        //protected int GapSigma { get; set; }
        //public SimpleChoreographer(Classifier classifierA, Classifier classifierB = null, int millisecondsGapMean = 1000, int millisecondsGapSigma = 500)
        //{
        //    ClassifierA = classifierA;
        //    ClassifierB = classifierB;
        //    GapMean = millisecondsGapMean;
        //    GapSigma = millisecondsGapSigma;
        //}
        public SimpleChoreographer(Dictionary<string, Classifier> classifiers, int millisecondsGapMean = 1000, int millisecondsGapSigma = 500)
        {
            Generator = new SimpleChoreographyGenerator(classifiers, millisecondsGapMean, millisecondsGapSigma);
            Classifiers = classifiers;
            //GapMean = millisecondsGapMean;
            //GapSigma = millisecondsGapSigma;
        }

        public event EventHandler<EventArgs<ChoreographyCue>> OnPromptCue;

        public void SubmitResult(ChoreographyCue cue)
        {
            if (double.IsNaN(cue.Score))
            {
                Speech.Say("What the heck was that?");
            }
            else if (cue.Score < 0) // Encodes "score but for the wrong gesture" in our Cue.Score scheme.
            {
                var recognizedAsName = Classifiers[cue.ClassifierKey].MatchingDatasetClasses[cue.GestureClassIndex].className;
                Speech.Say($"Looked more like {recognizedAsName}, with {(-1 * cue.Score):f1} points.", SoundOptions.AtSpeed(2.0));
            }
            else
            {
                Speech.Say($"Score {cue.Score:f1}, {cue.Delay.TotalSeconds:f1} seconds", SoundOptions.AtSpeed(2.0));
            }
            Generator.SubmitResults(cue, default(ChoreographyCue));
        }

        public override void Activate(CancellationToken? externalStopToken = null)
        {
            base.Activate(externalStopToken);

            Generator.OnExchangeChosen += (o, e) =>
            {
                OnPromptCue.Raise(e.Value.MyCue);
            };
            Generator.Activate(StopToken);
        }
    }

    public abstract class CommsChoreographer : ActivatorBase
    {
        public const string PROMPT = "ChoreoPrompt";
        public const string RESPONSE = "ChoreoResponse";

    }

    public class SendingChoreographer : CommsChoreographer, IChoreographer
    {
        private string _tag = "SendingChoreographer";

        public event EventHandler<EventArgs<ChoreographyCue>> OnPromptCue;
        private CommsContact Opponent;
        protected Dictionary<string, Classifier> Classifiers;
        private IChoreographyGenerator Generator;

        protected ChoreographyCue opponentSubmittedCue;
        protected AsyncAutoResetEvent opponentSubmissionSignal = new AsyncAutoResetEvent();

        public SendingChoreographer(CommsContact opponent, Dictionary<string, Classifier> classifiers, int millisecondsGapMean = 1000, int millisecondsGapSigma = 500)
        {
            Opponent = opponent;
            Classifiers = classifiers;
            Generator = new SimpleChoreographyGenerator(classifiers, millisecondsGapMean, millisecondsGapSigma);
        }

        public override void Activate(CancellationToken? externalStopToken = null)
        {
            base.Activate(externalStopToken);
            BluetoothMessageCenter.OnReceiveMessage += HandleResponse;
            Generator.OnExchangeChosen += HandlePrompts;
            Generator.Activate(StopToken);
        }

        public override void Deactivate()
        {
            base.Deactivate();
            BluetoothMessageCenter.OnReceiveMessage -= HandleResponse;
            Generator.OnExchangeChosen -= HandlePrompts;
        }

        public void HandleResponse(object sender, EventArgs<Message> messageArgs)
        {
            var message = messageArgs.Value;
            if (message.Type != MsgType.Notify || !message.Content.StartsWith(RESPONSE)) return;

            Log.Debug(_tag, "Parsing opponent's response message.");
            var cuestring = message.Content.Split(onNEXT, 2)[1];
            var cue = ChoreographyCue.Parse(cuestring);

            opponentSubmittedCue = cue;
            opponentSubmissionSignal.Set();
        }

        public void HandlePrompts(object sender, EventArgs<ExchangeOfBlows> e)
        {
            Log.Debug(_tag, $"Dispatching prompts - {e.Value.MyClassifierKey}#{e.Value.MyGestureIndex} for me, {e.Value.OppClassifierKey}#{e.Value.OppGestureIndex} for them.");
            var msg = new Message(MsgType.Notify, $"{PROMPT}{NEXT}{e.Value.OpponentCue}");
            Opponent.SendMessage(msg);
            OnPromptCue.Raise(e.Value.MyCue);
        }

        public async void SubmitResult(ChoreographyCue cue)
        {
            Log.Debug(_tag, "Waiting for opponent submission signal.");
            await opponentSubmissionSignal.WaitAsync();
            Log.Debug(_tag, "Opponent submission received.");

            if (double.IsNaN(cue.Score)) { Speech.Say("Gesture unrecognized."); cue.Score = double.NegativeInfinity; }
            else if (double.IsNaN(opponentSubmittedCue.Score)) { Opponent.SendMessage(MsgType.PushSpeech, "Gesture unrecognized."); opponentSubmittedCue.Score = double.NegativeInfinity; }
            else
            {
                var ownNetScore = cue.Score - cue.Delay.TotalSeconds;
                var oppNetScore = opponentSubmittedCue.Score - opponentSubmittedCue.Delay.TotalSeconds;
                if (Math.Abs(ownNetScore - oppNetScore) < 0.35) { Speech.Say("Tie."); Opponent.SendMessage(MsgType.PushSpeech, "Tie."); }
                else if (ownNetScore > oppNetScore) Speech.Say("Point.");
                else Opponent.SendMessage(MsgType.PushSpeech, "Point.");
            }

            Generator.SubmitResults(cue, opponentSubmittedCue);
        }
    }

    public class ReceivingChoreographer : CommsChoreographer, IChoreographer
    {
        private string _tag = "ReceivingChoreographer";
        public event EventHandler<EventArgs<ChoreographyCue>> OnPromptCue;
        private CommsContact Opponent;

        public ReceivingChoreographer(CommsContact opponent)
        {
            Opponent = opponent;
        }

        public override void Activate(CancellationToken? externalStopToken = null)
        {
            base.Activate(externalStopToken);
            BluetoothMessageCenter.OnReceiveMessage += HandleMessage;
        }

        public override void Deactivate()
        {
            base.Deactivate();
            BluetoothMessageCenter.OnReceiveMessage -= HandleMessage;
        }

        public void HandleMessage(object sender, EventArgs<Message> messageArgs)
        {
            var message = messageArgs.Value;
            if (message.Type != MsgType.Notify || !message.Content.StartsWith(PROMPT)) return;

            Log.Debug(_tag, "Parsing cue prompt message.");
            var cuestring = message.Content.Split(onNEXT, 2)[1];
            var cue = ChoreographyCue.Parse(cuestring);

            OnPromptCue.Raise(cue);
        }

        public void SubmitResult(ChoreographyCue cue)
        {
            var result = new Message(MsgType.Notify, $"{RESPONSE}{NEXT}{cue}");
            Opponent.SendMessage(result);
        }
    }

    public class SolipsisticChoreographer : ActivatorBase, IChoreographer
    {
        private IChoreographyGenerator Generator;
        protected Dictionary<string, Classifier> Classifiers;

        private bool IsOnPlayerTwo = false;
        private AsyncAutoResetEvent ReceivedSubmission = new AsyncAutoResetEvent();
        private ChoreographyCue PlayerOneCue;

        public SolipsisticChoreographer(Dictionary<string, Classifier> classifiers, int millisecondsGapMean = 2500, int millisecondsGapSigma = 1000)
        {
            Generator = new SimpleChoreographyGenerator(classifiers, millisecondsGapMean, millisecondsGapSigma);
            Classifiers = classifiers;
        }

        public event EventHandler<EventArgs<ChoreographyCue>> OnPromptCue;

        public void SubmitResult(ChoreographyCue cue)
        {
            //if (double.IsNaN(cue.Score))
            //{
            //    Speech.Say("What the heck was that?");
            //}
            //else if (cue.Score < 0)
            //{
            //    var recognizedAsName = Classifiers[cue.ClassifierKey].MatchingDatasetClasses[cue.GestureClassIndex].className;
            //    Speech.Say($"Looked more like {recognizedAsName}, with {(-1 * cue.Score):f1} points.", SoundOptions.AtSpeed(2.0));
            //}
            //else
            //{
            //    Speech.Say($"Score {cue.Score:f1}, {cue.Delay.TotalSeconds:f1} seconds", SoundOptions.AtSpeed(2.0));
            //}

            if (IsOnPlayerTwo)
            {
                if (double.IsNaN(PlayerOneCue.Score)) Speech.Say("First gesture unrecognized.", SoundOptions.AtSpeed(2.0));
                else if (double.IsNaN(cue.Score)) Speech.Say("Second gesture unrecognized.", SoundOptions.AtSpeed(2.0));
                else
                {
                    var P1netscore = PlayerOneCue.Score - PlayerOneCue.Delay.TotalSeconds;
                    var P2netscore = cue.Score - cue.Delay.TotalSeconds;
                    if (Math.Abs(P1netscore - P2netscore) < 0.35) Speech.Say("Tie.");
                    else if (P1netscore > P2netscore) Speech.Say("Point to first.", SoundOptions.AtSpeed(2.0));
                    else Speech.Say("Point to second.", SoundOptions.AtSpeed(2.0));
                }

                Generator.SubmitResults(PlayerOneCue, cue);
                IsOnPlayerTwo = false;
                PlayerOneCue = null;
            }
            else
            {
                PlayerOneCue = cue;
                IsOnPlayerTwo = true;
                ReceivedSubmission.Set();
            }
        }

        public override void Activate(CancellationToken? externalStopToken = null)
        {
            base.Activate(externalStopToken);

            Generator.OnExchangeChosen += async (o, e) =>
            {
                IsOnPlayerTwo = false;
                OnPromptCue.Raise(e.Value.MyCue);
                await ReceivedSubmission.WaitAsync();
                await Task.Delay(500);
                var cue = e.Value.OpponentCue;
                cue.CueTime = DateTime.Now + TimeSpan.FromMilliseconds(250);
                OnPromptCue.Raise(cue);
            };
            Generator.Activate(StopToken);
        }
    }
}