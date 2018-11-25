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

namespace Atropos.Hacking.Wheel
{
    // Hacking classifier is ordered as follows: Left, Right, Up, Down, Click.
    public enum Gest
    {
        Left,
        Right,
        Up,
        Down,
        Click
    }

    public class WheelDir
    {
        public Gest Primary;
        public Gest Secondary;
        public string Descriptor;
        public int Index;

        private double Alpha { get => Math.PI * (0.5 - Index / 6.0); } // Zero is twelve o'clock = pi/2, six is -pi/2, etc.
        public double SuccessCoefficient { get => 0.5 * (Math.Sin(Alpha) + 1); } // Zero to one scale
        public double TimeCoefficient { get => 1.0 - SuccessCoefficient; }
        public double FortuneCoefficient { get => 0.5 * (Math.Cos(Alpha) + 1); }
        public double RiskCoefficient { get => 1.0 - FortuneCoefficient; }
    }

    public class Wheel
    {
        public static WheelDir Intense = new WheelDir() { Descriptor = "Intense", Primary = Gest.Up, Secondary = Gest.Click, Index = 0 };
        public static WheelDir Maestro = new WheelDir() { Descriptor = "Maestro", Primary = Gest.Up, Secondary = Gest.Right, Index = 1 };
        public static WheelDir Panache = new WheelDir() { Descriptor = "Panache", Primary = Gest.Right, Secondary = Gest.Up, Index = 2 };
        public static WheelDir Flashy = new WheelDir() { Descriptor = "Flashy", Primary = Gest.Right, Secondary = Gest.Click, Index = 3 };
        public static WheelDir Showboat = new WheelDir() { Descriptor = "Showboat", Primary = Gest.Right, Secondary = Gest.Down, Index = 4 };
        public static WheelDir WingingIt = new WheelDir() { Descriptor = "Winging It", Primary = Gest.Down, Secondary = Gest.Right, Index = 5 };
        public static WheelDir Casual = new WheelDir() { Descriptor = "Casual", Primary = Gest.Down, Secondary = Gest.Click, Index = 6 };
        public static WheelDir LightStep = new WheelDir() { Descriptor = "Light Step", Primary = Gest.Down, Secondary = Gest.Left, Index = 7 };
        public static WheelDir Tentative = new WheelDir() { Descriptor = "Tentative", Primary = Gest.Left, Secondary = Gest.Down, Index = 8 };
        public static WheelDir Cautious = new WheelDir() { Descriptor = "Cautious", Primary = Gest.Left, Secondary = Gest.Click, Index = 9 };
        public static WheelDir Painstaking = new WheelDir() { Descriptor = "Painstaking", Primary = Gest.Left, Secondary = Gest.Up, Index = 10 };
        public static WheelDir Methodical = new WheelDir() { Descriptor = "Methodical", Primary = Gest.Up, Secondary = Gest.Left, Index = 11 };

        public static List<WheelDir> Directions = new List<WheelDir>()
            {
                Intense, Maestro, Panache, Flashy, Showboat, WingingIt, Casual, LightStep, Tentative, Cautious, Painstaking, Methodical
            };

        public static WheelDir ByName(string name) => Directions.SingleOrDefault(wdir => wdir.Descriptor.ToLower() == name.ToLower());
        public static WheelDir ByIndex(int index) => Directions.SingleOrDefault(wdir => wdir.Index == index);
        public static WheelDir ByGestures(Gest primary, Gest secondary) => Directions.SingleOrDefault(wdir => wdir.Primary == primary && wdir.Secondary == secondary);

        #region Direction-dependent Transition Phrases
        public class TransitionPhrase
        {
            public int keyIndex;
            public string[] phraseList;
        }

        private static TransitionPhrase Tphr(int keyIndex, params string[] phrases) => new TransitionPhrase() { keyIndex = keyIndex, phraseList = phrases };
        public static List<TransitionPhrase> TransitionPhrases = new List<TransitionPhrase>()
        {
            Tphr(0, "You push onward", "You press on", "You advance"),
            Tphr(1, "You glide forward", "Sure-footed, you advance", "The data streams past as you move"),
            Tphr(2, "You stride onward", "Forward you leap", "You bound off a data wall and onward"),
            Tphr(3, "Onward and upward", "Full steam ahead", "Full speed ahead"),
            Tphr(4, "You screen-cap that and glide on", "Your soundtrack rocks as you stroll"),
            Tphr(5, "You note that trick for later and continue", "Whee! Onward", "Your avatar grins as you carry on"),
            Tphr(6, "No problem. Onward", "You stroll forward", "Easy as pie. Now"),
            Tphr(7, "You silently slip onward", "You ghost through", "Onward, with grace,"),
            Tphr(8, "You tread carefully", "Without a sound, you carry on", "You proceed gingerly"),
            Tphr(9, "You creep inward", "You sidle along the bitstream", "Carefully, now, onward"),
            Tphr(10, "You work your way deeper", "You inch onward", "Bit by bit you proceed"),
            Tphr(11, "Step by step, onward", "Next, onward", "Eyes peeled, you pass on")
        };
        public static string GetTransitionPhraseFor(int keyIndex)
        {
            var randomizer = Math.Truncate(Res.RandomZ); // Add a normally distributed variable (sigma = 1) to get a little variance (but 70% will round down to zero change).
            var chosenIndex = (keyIndex + 12 + randomizer) % 12;
            return TransitionPhrases.Single(tph => tph.keyIndex == chosenIndex).phraseList.GetRandom();
        }
        #endregion

        #region Direction-dependent Icebreaker Names & Styles
        public const string DEFAULT = "(default)";
        private static string[] DEFAULT_INIT = new string[] { "Running breaker.", "Slotting in.", "Activating...", "Icebreaker started.", "Breaking...", "Begin." };
        private static string[] DEFAULT_SUCC = new string[] { "Got it!", "Cracked it.", "Done.", "Unlocked.", "Countermeasure defeated.", "Boo-yeah!", "Poaned it." };
        private static string[] DEFAULT_FAIL = new string[] { "Bounced.", "Rejected.", "Try again.", "Crack failed.", "Vulnerability patched.", "Ice too strong.", "Darn it!", "Rats!" };
        public class IcebreakerDescriptor
        {
            public int keyIndex;
            public string name;
            public string typeString;
            public double typeParam;
            public string speechI; // Initiation phrase
            public string speechS; // Success phrase
            public string speechF; // Fail phrase
            public string speechT; // Transition between attempts - only relevant for Type C icebreakers

            public IcebreakerDescriptor Evaluate()
            {
                var result = this;
                if (this.speechI == DEFAULT) result.speechI = DEFAULT_INIT.GetRandom();
                if (this.speechS == DEFAULT) result.speechS = DEFAULT_SUCC.GetRandom();
                if (this.speechF == DEFAULT) result.speechF = DEFAULT_FAIL.GetRandom();
                return result;
            }
        }

        // Type A: Stated length with ratio of before-fail / before-success times
        private static IcebreakerDescriptor TypeA(int index, string name, string speechI = DEFAULT, string speechS = DEFAULT, string speechF = DEFAULT, double param = 1.0) 
            => new IcebreakerDescriptor() { keyIndex = index, name = name, typeString = "TypeA", speechI = speechI, speechS = speechS, speechF = speechF, typeParam = param };
        // Type B: Unstated length with ratio of before-fail / before-success times
        private static IcebreakerDescriptor TypeB(int index, string name, string speechI = DEFAULT, string speechS = DEFAULT, string speechF = DEFAULT, double param = 1.0)
            => new IcebreakerDescriptor() { keyIndex = index, name = name, typeString = "TypeB", speechI = speechI, speechS = speechS, speechF = speechF, typeParam = param };
        // Type C: Unstated-duration pseudo-repeating with ms duration of a single attempt
        private static IcebreakerDescriptor TypeC(int index, string name, string speechT = "Retrying...", string speechI = DEFAULT, string speechS = DEFAULT, string speechF = DEFAULT, double param = 1500)
            => new IcebreakerDescriptor() { keyIndex = index, name = name, typeString = "TypeC", speechI = speechI, speechS = speechS, speechF = speechF, typeParam = param };

        public static List<IcebreakerDescriptor> IcebreakerDescriptors = new List<IcebreakerDescriptor>()
        {
            TypeB(0, "Jackhammer version 1.2", param: 0.75), // Intense
            TypeB(0, "Hand-coding I.D.E."), // Intense
            TypeA(1, "Synchrony anarch"), // Maestro
            TypeC(1, "Tonal assault version 2"), // Maestro
            TypeC(2, "Rapier 5.2", "Encore!", "En garde!", "Tooshay!", "Ack, I am vanquish-ed!"), // Panache
            TypeA(2, "Master thief 1.8"), // Panache
            TypeA(3, "Shatterglass"), // Flashy
            TypeB(3, "Command prompt", "Coding on the fly"), // Flashy
            TypeC(4, "Antillies", "Stay on target", "The code is strong with this one"), // Showboat
            TypeB(4, "Swindler version one point oh point seven"), // Showboat
            TypeB(5, "Script kiddie's latest", param: 2.0), // Winging It
            TypeC(5, "Something I picked up on the net", "Tweaking a little"), // Winging It
            TypeA(6, "Buffer overrun attack", "Constructing bufstring", param: 1.25), // Casual
            TypeB(6, "Good old blaster", param: 0.65), // Casual
            TypeA(7, "Good old sleaze", param: 0.65), // Light Step
            TypeC(7, "Port scan attack", "Port closed. Scanning...", "Scanning...", "Open port found.", "No vulnerabilities found."), // Light Step
            TypeA(8, "Masquerade 7.7", param: 0.5), // Tentative
            TypeB(8, "Mirror traffic attack"), // Tentative
            TypeA(9, "Probe version 1.4"), // Cautious
            TypeC(9, "Spoof version 2.3", "Tweaking...", "Falsifying server certificate"), // Cautious
            TypeA(10, "Firmware analysis"), // Painstaking
            TypeB(10, "Mimic version two point oh point fourteen"), // Painstaking
            TypeA(11, "Gateway disassembly attack"), // Methodical
            TypeB(11, "Diamond cutter version one point twelve", param: 0.5) // Methodical
        };
        public static IcebreakerDescriptor GetIcebreakerFor(int keyIndex)
        {
            var randomizer = Math.Truncate(Res.RandomZ * 1.4); // Add a normally distributed variable (sigma = 1.4) to get some variance (but 50% will round down to zero change).
            var chosenIndex = (keyIndex + 12 + randomizer) % 12;
            return IcebreakerDescriptors.Where(icebr => icebr.keyIndex == chosenIndex).ToList().GetRandom().Evaluate();
        }
        #endregion
    }

    //public class WheelSegment
    //{
    //    public List<WheelDir> Directions;
    //    public static WheelSegment Between(WheelDir from, WheelDir to)
    //    {
    //        var sign = (from.Index > to.Index) ? +1 : -1;
    //    }
    //}
}