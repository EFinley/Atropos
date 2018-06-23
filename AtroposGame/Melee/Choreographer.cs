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
            else if (cue.Score < 0)
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

            var cuestring = message.Content.Split(onNEXT, 2)[1];
            var cue = ChoreographyCue.Parse(cuestring);

            opponentSubmittedCue = cue;
            opponentSubmissionSignal.Set();
        }

        public void HandlePrompts(object sender, EventArgs<ExchangeOfBlows> e)
        {
            var msg = new Message(MsgType.Notify, $"{PROMPT}{NEXT}{e.Value.OpponentCue}");
            Opponent.SendMessage(msg);
            OnPromptCue.Raise(e.Value.MyCue);
        }

        public async void SubmitResult(ChoreographyCue cue)
        {
            await opponentSubmissionSignal.WaitAsync();
            Generator.SubmitResults(cue, opponentSubmittedCue);
        }
    }

    public class ReceivingChoreographer : CommsChoreographer, IChoreographer
    {
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
}