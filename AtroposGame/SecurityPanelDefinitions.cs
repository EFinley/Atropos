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
using System.Numerics;
using PerpetualEngine.Storage;
using System.Threading.Tasks;
using Atropos.Encounters;
using static Atropos.Encounters.Scenario;

namespace Atropos
{
    public struct DetailResult
    {
        public int index;
        public string result;
    }

    public class DetailList : List<DetailResult>
    {
        public string At(int seekIndex)
        {
            try
            {
                return this.OrderBy(r => r.index).Last(r => r.index <= seekIndex).result;
            }
            catch (System.InvalidOperationException)
            {
                return String.Empty;
            }   
        }

        public void Add(int index, string result)
        {
            this.Add(new DetailResult() { index = index, result = result });
        }

        new public string this[int i]
        {
            get { return At(i); }
            set
            {
                if (this.Any(res => res.index == i))
                {
                    var r = this.First(res => res.index == i);
                    r.result = value;
                }
                else { Add(i, value); }
            }
        }
    }

    public class SecurityPanelNode
    { 
        public string Name;
        public string Code;
        public string ShortExamineResult { get { return Results.At(0); } }
        public string LongExamineResult { get { return Results.At(5); } }
        public DetailList Results;
        public int OverlayResourceId;
        public int NumberOfTimesExamined = 0;
        public int NumberOfExaminesBeforeLongResult = 2;
        public Dictionary<SecurityPanelNode, SecurityPanelNodeLink> LinksOut = new Dictionary<SecurityPanelNode, SecurityPanelNodeLink>();
        public Dictionary<SecurityPanelNode, SecurityPanelNodeLink> LinksIn = new Dictionary<SecurityPanelNode, SecurityPanelNodeLink>();
        public DateTime ShortExamineGivenAt;
        public DateTime LongExamineGivenAt;
        public int ExamineMillisecondsBase = 500;
        public int ExamineMillisecondsIncrement = 750;
        public string OutgoingConnectionClarification;

        public SecurityPanelNode(string name, string code, int overlayID)
        {
            Name = name;
            Code = code;
            if (SecurityPanel.CurrentPanel == null)
                SecurityPanel.CurrentPanel?.Nodes?.Add(this);
            OverlayResourceId = overlayID;
            Results = new DetailList();
        }

        public static SecurityPanelNode Unknown = new SecurityPanelNode("...", null, -1);

        public static bool operator==(SecurityPanelNode node1, SecurityPanelNode node2) { return node1.Code == node2.Code; }
        public static bool operator!=(SecurityPanelNode node1, SecurityPanelNode node2) { return node1.Code != node2.Code; }

        public override bool Equals(object obj)
        {
            if (obj is SecurityPanelNode objNode) return objNode == this;
            else return object.ReferenceEquals(obj, this);
        }

        public override int GetHashCode()
        {
            return Code.GetHashCode();
        }
    }

    public class SecurityPanelNodeLink
    {
        public string Name;
        public string Code;
        public SecurityPanelNode StartNode;
        public SecurityPanelNode EndNode;
        public string ShortMultimeterResult { get { return MeasureResults.At(0); } }
        public string LongMultimeterResult { get { return MeasureResults.At(5); } }
        public DetailList MeasureResults;
        public int NumberOfTimesMultimetered = 0;
        public int NumberOfMultimeteringsBeforeLongResult = 1;
        public Action WirecutterResult { get; set; }
        public Action SolderingResult { get; set; }
        public bool IsLinked;
        public DateTime ShortMultimeterGivenAt;
        public DateTime LongMultimeterGivenAt;
        public int MultimeterMillisecondsBase = 1000;
        public int MultimeterMillisecondsIncrement = 1000;
        public int WirecutterMilliseconds = 500;
        public int SolderMilliseconds = 2000;

        public SecurityPanelNodeLink(SecurityPanelNode start, SecurityPanelNode end, bool isLinked = false)
        {
            Name = start.Name + " to " + end.Name;
            Code = start.Code + "to" + end.Code;
            StartNode = start;
            EndNode = end;
            IsLinked = isLinked;
            StartNode.LinksOut.Add(EndNode, this);
            EndNode.LinksIn.Add(StartNode, this);
            SecurityPanel.CurrentPanel.Links.Add(this);
            SecurityPanel.CurrentPanel.Linkages.Add(StartNode, EndNode, this);
            MeasureResults = new DetailList();
        }
    }

    public class SecurityPanel
    {
        public int PictureResourceID = Resource.Drawable.securitypanel_gray;
        public List<SecurityPanelNode> Nodes = new List<SecurityPanelNode>();
        public List<SecurityPanelNodeLink> Links = new List<SecurityPanelNodeLink>();
        public DoubleDictionary<SecurityPanelNode, SecurityPanelNode, SecurityPanelNodeLink> Linkages
            = new DoubleDictionary<SecurityPanelNode, SecurityPanelNode, SecurityPanelNodeLink>();
        public Dictionary<string, SecurityPanelNode> NodeForTag = new Dictionary<string, SecurityPanelNode>();

        private bool _mainBoltsAreEngaged = true;
        public bool MainBoltsAreEngaged
        {
            get { return _mainBoltsAreEngaged; }
            set { if (!value && !SecondaryBoltsAreEngaged) Current["securityDoorUnlock"] = State.Success; _mainBoltsAreEngaged = value; }
        }
        private bool _secondaryBoltsAreEngaged = false;
        public bool SecondaryBoltsAreEngaged
        {
            get { return _secondaryBoltsAreEngaged; }
            set { if (value) Current["securityDoorUnlock"] = State.Failed; _secondaryBoltsAreEngaged = value; }
        }
        private bool _alarmHasBeenRaised = false;
        public bool AlarmHasBeenRaised
        {
            get { return _alarmHasBeenRaised; }
            set { _alarmHasBeenRaised = value; if (value) Current["alarmSetOff"] = State.True; }
        }
        private bool _suspiciousInfoHasBeenSent = false;
        public bool SuspiciousInfoHasBeenSent
        {
            get { return _suspiciousInfoHasBeenSent; }
            set { _suspiciousInfoHasBeenSent = value; if (value) Current["sysopSuspects"] = State.True; }
        }

        public IEffect AlarmFX, BoltsClosingFX, BoltsOpeningFX, SparksFX;

        private static SecurityPanel _panel = new SecurityPanel();
        public static SecurityPanel CurrentPanel
        {
            get { if (_panel != null) return _panel; else _panel = new SecurityPanel(); return _panel; }
            set { _panel = value; }
        }
        //static Security() { Initialize(); }

        public static void Initialize(BypassActivity parentActivity)
        {
            if (CurrentPanel == null) CurrentPanel = new SecurityPanel();
            CurrentPanel.CurrentActivity = parentActivity;
            CurrentPanel.SetUpSecurity();

            CurrentPanel.AlarmFX = new Effect("SecurityPanel.Alarm", Resource.Raw._169206_security_voice_activating_alarm);
            CurrentPanel.BoltsClosingFX = new Effect("SecurityPanel.BoltsClosing", Resource.Raw._110538_bolt_closing);
            CurrentPanel.BoltsOpeningFX = new Effect("SecurityPanel.BoltsOpening", Resource.Raw._213996_bolt_opening);
            CurrentPanel.SparksFX = new Effect("SecurityPanel.Sparks", Resource.Raw._277314_electricArc);
        }

        private static Action Say(string phrase)
        {
            return () => { Speech.Say(phrase); };
        }

        private static Action Announce(string phrase)
        {
            return () => {  Speech.Say(phrase, useSpeakerMode: true); };
        }

        private static Action Play(IEffect sfx)
        {
            return () => { sfx.PlayToCompletion(); };
        }

        private static Action Blare(IEffect sfx)
        {
            return () => { sfx.PlayToCompletion(null, true); };
        }

        public SecurityPanel() { }

        protected BypassActivity CurrentActivity { get; set; }
        protected virtual void RelayMessage(string message)
        {
            if (CurrentActivity == null) return;
            (CurrentActivity as IRelayMessages).RelayMessage(message);
        }
        
        // This is currently a bit of a hack, because we only support a single security panel.  It's virtual, though, so... you do the math.
        public virtual void SetUpSecurity()
        {
            // Create all of the instances.  Their ctors will take care of inserting them in the dictionaries.
            var A = new SecurityPanelNode("Upper Solenoid", "A", Resource.Drawable.securitypanel_overlay_topleft); // The one kept hot-meaning-unlocked so that the doors will auto-seal if power dies.
            var B = new SecurityPanelNode("Lower Solenoid", "B", Resource.Drawable.securitypanel_overlay_bottomleft); // The one kept cool-meaning-locked which is used to open them normally.
            var C = new SecurityPanelNode("Communications", "C", Resource.Drawable.securitypanel_overlay_bottom);
            var D = new SecurityPanelNode("Processor", "D", Resource.Drawable.securitypanel_overlay_center);
            Nodes.Add(A);
            Nodes.Add(B);
            Nodes.Add(C);
            Nodes.Add(D);

            var AtoB = new SecurityPanelNodeLink(A, B);
            var AtoC = new SecurityPanelNodeLink(A, C, true);
            var AtoD = new SecurityPanelNodeLink(A, D, true);
            var BtoA = new SecurityPanelNodeLink(B, A);
            var BtoC = new SecurityPanelNodeLink(B, C, true);
            var BtoD = new SecurityPanelNodeLink(B, D, true);
            var CtoA = new SecurityPanelNodeLink(C, A);
            var CtoB = new SecurityPanelNodeLink(C, B);
            var CtoD = new SecurityPanelNodeLink(C, D, true);
            var DtoA = new SecurityPanelNodeLink(D, A, true);
            var DtoB = new SecurityPanelNodeLink(D, B, true);
            var DtoC = new SecurityPanelNodeLink(D, C, true);

            // Define Examine strings
            A.Results[0] = "Clearly a solenoid used to shift the door's locking bolts.  No way to tell if 'powered on' means the door is locked or unlocked, though.";
            A.Results[2] = "This one is hot to the touch - must be live and at fairly high voltage.";
            B.Results[0] = A.Results[0];
            B.Results[2] = "This one is cool to the touch.  Probably off at present.";
            C.Results[0] = "Fiber-optic communications line to elsewhere in the building.";
            C.Results[2] = "Not a lot of data flowing out right now, mostly just telemetry from the processor over on the right.";
            D.Results[0] = "The central processor for this panel.  Not very smart, but smart enough.";
            D.Results[2] = "";

            // Define Measure linkages
            AtoB.MeasureResults[0] = "There's no direct connection between the two solenoids.";
            AtoC.MeasureResults[0] = "Looks like there's a single status line running from the solenoid to the comm relay.";
            AtoC.MeasureResults[1] = "This one is showing 3.3V, steady.  A pretty standard signal for 'on' or some other binary 'one'.";
            AtoD.MeasureResults[0] = "Looks like there's just a simple status line running back to the processor.";
            AtoD.MeasureResults[1] = AtoC.MeasureResults[2];

            BtoA.MeasureResults[0] = AtoB.MeasureResults[0];
            BtoC.MeasureResults[0] = AtoC.MeasureResults[0];
            BtoC.MeasureResults[1] = "This line is rated for the usual few volts, but currently it just reads zero.";
            BtoD.MeasureResults[0] = AtoD.MeasureResults[0];
            BtoD.MeasureResults[1] = BtoC.MeasureResults[1];

            CtoA.MeasureResults[0] = "There's no sign of any data in this direction.";
            CtoB.MeasureResults[0] = CtoA.MeasureResults[0];
            CtoD.MeasureResults[0] = "There's certainly some data flowing along this route, on an intermittent basis.";
            CtoD.MeasureResults[2] = "You do identify one channel here: an encrypyted data stream, probably from a card reader or some other access device.  If we had implemented it, your decker might be able to hack this and fake a valid code, but that's not coded yet.";

            DtoA.MeasureResults[0] = "A 3.3 volt control signal is stepped up to plus sixty volts and a pretty high current in the solenoid.";
            DtoA.MeasureResults[2] = "The way they've engineered this link, it's pretty clear that it's intended to stay hot much if not most of the time.";
            DtoB.MeasureResults[0] = "The step-up transformers along this path seem to be sitting idle; the high voltage lines currently inactive.";
            DtoC.MeasureResults[0] = "A data bus carries several signals out to the comms relay from the processor.";
            DtoC.MeasureResults[2] = "You'd have to have access to the source code, or several hours to play with this, before you could make any sense of what's in this data stream.";

            // Wire Cutter results (where nontrivial).  
            // Note that .IsLinked = False is automatically a result; this is apart from that,
            // and a check for .IsLinked that will send up a "nothing to cut" if relevant is likewise built in.
            AtoC.WirecutterResult = () => { SuspiciousInfoHasBeenSent = true; }; // Looks to the control panel like the backup locks just locked themselves.
            AtoD.WirecutterResult = () => { AlarmHasBeenRaised = true; Blare(AlarmFX); }; // The door knows IT didn't tell the backups to go dead, so someone's tampering.
            DtoC.WirecutterResult = () => { AlarmHasBeenRaised = true; Blare(AlarmFX); }; // Control just lost all communications with this door.
            DtoA.WirecutterResult = () => { SecondaryBoltsAreEngaged = true; Blare(BoltsClosingFX); }; // Congrats, you just screwed up and locked yourself out.
            DtoB.WirecutterResult = () => { DtoB.SolderingResult = AtoB.SolderingResult; };

            // Soldering results - similar IsLinked comments as above.
            AtoB.SolderingResult = async () =>
            {
                if (BtoC.IsLinked) SuspiciousInfoHasBeenSent = true;
                if (BtoD.IsLinked) { AlarmHasBeenRaised = true; Blare(AlarmFX); };
                Blare(BoltsOpeningFX);
                MainBoltsAreEngaged = false;
                await Speech.SayAllOf("Doors are now open; proceed.");
            };
            BtoA.SolderingResult = async () =>
            {
                await BoltsClosingFX.PlayToCompletion(10.0, true);
                SecondaryBoltsAreEngaged = true;
                await Speech.SayAllOf("Uh-oh.  That was the secondary bolts engaging - and you're pretty sure the solenoid won't be capable of opening them again.  Looks like Paper Crane is going to be making a service call... or two.");
            };
            AtoC.SolderingResult = AtoD.SolderingResult = async () => 
            {
                AlarmHasBeenRaised = true;
                await SparksFX.PlayToCompletion(1.0, true);
                await Speech.SayAllOf("You have managed to put about six amps through a delicate piece of circuitry.  Congratulations - you just 'bricked' this door.");
                AlarmFX.Play(1.0, false, true);
            };
        }
    }
}