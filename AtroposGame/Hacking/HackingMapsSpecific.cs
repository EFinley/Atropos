using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace Atropos.Hacking
{
    public partial class HackingMap
    {
        protected bool Exists(string name) => Nodes.ContainsKey(name);

        protected Effect dataEntryEffect = new Effect("Hacking|PWclick", Resource.Raw._288949_click);

        public void SetUpDefaults()
        {
            // Format: 
            var AssessICE       = Nodes["AssessICE"]        = new HackingMapNode("Assess ICE");
            var FalseTrail      = Nodes["FalseTrail"]       = new HackingMapNode("False Trail", "Lay False Trail");
            var PasswordPrompt  = Nodes["PasswordPrompt"]   = new HackingMapNode("Password", "Password Prompt") { IsBlocking = true };
            var ShellAccess     = Nodes["ShellAccess"]      = new HackingMapNode("Shell", "Shell Access");
            var Paydata         = Nodes["Paydata"]          = new HackingMapNode("Paydata", "Seek Paydata");
            var NetworkScan     = Nodes["NetworkScan"]      = new HackingMapNode("Netscan", "Network Scan");

            // Assess ICE - investigate and un-hide pieces of ICE before they trip you up.
            AssessICE.IsAbove(StartNode);
            AssessICE.OnEntrySay = "Analyzing network";
            AssessICE.Data["Detectables"] = new Dictionary<int, string>
            {
                { 3, "PasswordPrompt" },
                { 5, "Firewall" },
                { 8, "ProxyServer" },
                { 12, "Biometrics" },
                { 15, "Voiceprint" },
                { 19, "TwoFactor" },
                { 23, "Further countermeasures" }
            };
            AssessICE.AddAction("Probe", HackGesture.Typing, (action, node) => 
            {
                var detectables = AssessICE.Data["Detectables"] as Dictionary<int, string>; // Lookup from Data[], rather than direct local variable, because this way it can be altered between creation & use.
                node.Counter_Persistent++;
                action.Name = "Probe More";
                if (detectables.TryGetValue(node.Counter_Persistent, out string nodeName)) // Their tally of digging-clicks corresponds to a valid list entry (whether it's present or not).
                {
                    if (Exists(nodeName) && Nodes[nodeName] != null)
                    {
                        Nodes[nodeName].IsHidden = false;
                        Speech.Say($"{Nodes[nodeName].Longname} detected", Interrupt);
                    }
                    else Speech.Say($"{nodeName} not found.", Interrupt);
                }
            });

            // False Trail - preventative counter to trace attempts which might happen later.
            FalseTrail.IsBelow(StartNode, PasswordPrompt);
            FalseTrail.AddAction("Add Bounce", HackGesture.Typing, (action, node) => 
            {
                var citynames = new string[] { "London", "New York", "Sydney", "Moscow", "Paris", "Bogota", "Rekyavick", "Berlin", "Nairobi", "Sao Paolo", "Leema", "Perth",
                    "Singapore", "Hong Kong", "Vancouver", "Toronto", "Istanbul", "Delhi", "Los Angeles", "Miami", "Prague", "Malta", "The Caymans", "Brunei", "Ell five" };
                if (Res.Random < 0.25)
                {
                    Speech.Say($"Routing through {citynames.GetRandom()}", new SoundOptions() { Speed = 2.0, Interrupt = true });
                    node.Counter_Persistent++;
                }
            });

            PasswordPrompt.IsRightOf(StartNode);
            PasswordPrompt.OnEntrySay = "Running Pineapple-hack 1.1                                 "; // Making it "longer" makes it be said faster [see Enter( )]
            PasswordPrompt.Data["targetClicks"] = MiscUtil.StaticRandom.Next(6, 11);
            PasswordPrompt.AddAction("Crack", HackGesture.Typing, (action, node) =>
            {
                if (!node.IsBlocking) return;
                dataEntryEffect.Play();
                node.Counter_ThisAccess++;
                if (node.Counter_ThisAccess > (int)node.Data["targetClicks"])
                {
                    node.IsBlocking = false;
                    Speech.Say("Cracked it.");
                }
            });
            
            ShellAccess.IsRightOf(PasswordPrompt);
            Paydata.IsBelow(ShellAccess);
            NetworkScan.IsAbove(ShellAccess);

            Paydata.AddAction("Assess", HackGesture.Typing, (action, node) =>
            {
                Speech.Say("No valuable side data found on this server.");
                Paydata.Actions.Clear();
            });

            NetworkScan.AddAction("Trace Status", HackGesture.Left, async (action, node) =>
            {
                await Speech.SayAllOf("Checking...", new SoundOptions() { Speed = 2.0, Interrupt = true });
                await Task.Delay(750);
                Speech.Say("No active trace in progress.", SoundOptions.AtSpeed(1.5));
            });
            NetworkScan.AddAction("Delete Logs", HackGesture.Typing, (action, node) =>
            {
                ExecutableActionsCounter--; // Doesn't count toward the actions needing deletion.
                node.Counter_Persistent++;
                if (0.25 * node.Counter_Persistent * node.Counter_Persistent >= ExecutableActionsCounter)
                {
                    Speech.Say("Logs cleared. Passive trace; should; be impossible.", new SoundOptions() { Speed = 1.8, Interrupt = true });
                }
            });
            NetworkScan.AddAction("Detect Sysop", HackGesture.Right, async (action, node) =>
            {
                await Speech.SayAllOf("Scanning...", new SoundOptions() { Speed = 2.0, Interrupt = true });
                await Task.Delay(1000);
                Speech.Say("Sysop does not appear to be logged on.", SoundOptions.AtSpeed(1.5));
            });
        }

        public void SetupObjectiveChain(params string[] ObjectiveStageNames)
        {
            var lastStage = Nodes["ShellAccess"];
            foreach (var objStageName in ObjectiveStageNames)
            {
                Nodes[objStageName] = new HackingMapNode(objStageName) { IsBlocking = true };
                Nodes[objStageName].IsRightOf(lastStage);
                lastStage = Nodes[objStageName];
                lastStage.AddAction("(Hack)", HackGesture.Typing, (action, node) =>
                {
                    dataEntryEffect.Play();
                    node.IsBlocking = false;
                    OnObjectiveCompleted.Raise(node);
                });
            }

            OnObjectiveCompleted += (o, e) =>
            {
                if (e.Value.Shortname == ObjectiveStageNames.Last())
                {
                    new Effect("Shatter", Resource.Raw._349905_slowGlassShatter).Play();
                    Speech.Say("Objective complete.");
                    HackingActivity.Current.Finish();
                }
            };

            var allObjectiveStages = new List<HackingMapNode>() { Nodes["ShellAccess"] }.Concat(ObjectiveStageNames.Select(name => Nodes[name])).ToArray();
            Nodes["Paydata"].IsBelow(allObjectiveStages);
            Nodes["NetworkScan"].IsAbove(allObjectiveStages);


        }

        public void SetupFirewall()
        {
            var PasswordPrompt = Nodes["PasswordPrompt"];
            var Firewall = Nodes["Firewall"] = new HackingMapNode("Firewall") { IsBlocking = true, IsHidden = true };
            var Sleaze = new HackingICEBreakerNode("Sleaze", "Sleaze v2.11");
            Nodes["Sleaze"] = Sleaze;
            //var Banzai = new HackingICEBreakerNode("Banzai", "Banzai PRO");
            //Nodes["Banzai"] = Banzai;

            Firewall.IsRightOf(StartNode);
            PasswordPrompt.IsRightOf(Firewall);
            Sleaze.IsAbove(Firewall);
            //Banzai.IsBelow(Firewall);

            Sleaze.Unlocks = Firewall;
            //Banzai.Unlocks = Firewall;

            // Password prompt should fail if firewall is present and still blocking - exposing it if hidden (with an ominous warning if so).
            PasswordPrompt.ValidateGesture = (g) =>
            {
                if (g != HackGesture.Typing) return true;
                if (PasswordPrompt.Counter_ThisAccess < (int)PasswordPrompt.Data["targetClicks"]) return true;
                else return !Firewall.IsBlocking;
            };
            PasswordPrompt.OnValidationFailed = (g) =>
            {
                dataEntryEffect.Play();
                Firewall.IsHidden = false;
                Speech.Say("Crack blocked by firewall.  Alert level raised slightly.", Interrupt);
                HackingActivity.Current.TransitionNode(HackingMapNode.Dir.Left).Wait();
            };

            // Sleaze is a pretty straightforward "guess how much effort to put into improving my odds vs. into trying the shot" icebreaker.
            Sleaze.Data["ChanceOfSucc"] = 0.0;
            Sleaze.AddAction("Generate", HackGesture.Typing, (action, node) =>
            {
                dataEntryEffect.Play();
                if (node.Counter_ThisAccess == 0)
                {
                    Speech.Say("Falsifying certificate", Interrupt);
                    action.Name = "Tweak";                    
                }
                node.Counter_ThisAccess++;
                var chanceOfSucc = 1.0 - Math.Exp(-0.3 * node.Counter_ThisAccess);
                node.Data["ChanceOfSucc"] = chanceOfSucc;
                node.Actions.ElementAt(1).Name = $"Submit (~{(chanceOfSucc * 100):f0}%)";
            });
            Sleaze.AddAction("Submit", HackGesture.Right, (action, node) =>
            {
                var chanceOfSucc = (double)node.Data["ChanceOfSucc"];
                if (Res.Random < chanceOfSucc)
                {
                    new Effect("shatter", Resource.Raw._349905_slowGlassShatter).Play();
                    (node as HackingICEBreakerNode).DoUnlocking();
                }
                else
                {
                    new Effect("blatt", Resource.Raw.kblaa_magic).Play();
                    node.Counter_ThisAccess = 0;
                    node.Data["ChanceOfSucc"] = 0.0;
                    action.Name = "Submit (0%)";
                    node.Actions.ElementAt(0).Name = "(Re)Generate";
                    Speech.Say("Certificate not accepted.", Interrupt);
                }
            });

            // Banzai will be a more complex "follow instructions promptly" icebreaker, but I'm moving on to other things for awhile.
        }

        public void SetupProxy(string blockedStageName)
        {
            var ProxyServer = Nodes["ProxyServer"] = new HackingMapNode("Proxy", "Proxy Server") { IsBlocking = true, IsHidden = true };
            var Spoof = Nodes["Spoof"] = new HackingICEBreakerNode("Spoof", "Spoof Proxy");
            var Disrupt = Nodes["Disrupt"] = new HackingICEBreakerNode("Disrupt", "Disrupt Proxy Server");

            //ProxyServer.IsRightOf(PasswordPrompt);
            Spoof.IsAbove(ProxyServer);
            Disrupt.IsBelow(ProxyServer);

        }
    }
}