using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace com.Atropos
{
    public interface ITutorial
    {
        string IntroductoryRemarks { get; }
        string[] ExpandedRemarks { get; }
        int NumberOfSteps { get; }
        List<ITutorialStep> Steps { get; }
    }

    public interface ITutorialStep
    {
        string IntroSpeech { get; }
        string ExpandedSpeech { get; }
        float Sigma { get; }
        int FeedbackLevelGiven { get; set; }
        string GoodFeedback { get; } // Generally triggered at ~0.5 sigma from target
        string ImprovingFeedback { get; } // ~1.5 sigma
        string KeepWorkingFeedback { get; } // Three sigma AND/OR lots of time spend flailing about not finding it.
        IEffect FeedbackSFX { get; }
    }

    public interface ITutorialStep<T> : ITutorialStep
    {
        T Target { get; set; }
        float Assess(T data);
    }

    public class OrientationStep : ITutorialStep<Quaternion>
    {
        public Quaternion Target { get; set; } = Quaternion.Identity;
        public float Sigma { get; set; } = 10f;
        public float Assess(Quaternion data)
        {
            return Target.AngleTo(data) / Sigma;
        }

        public string IntroSpeech { get; set; } = "";
        public string ExpandedSpeech { get; set; } = "Sorry, this gesture has no mnemonic set.  When in doubt, disco in place...";
        public int FeedbackLevelGiven { get; set; } = 0;
        public IEffect FeedbackSFX { get; set; } = MasterSpellLibrary.SpellFeedbackSFX;

        public virtual string GoodFeedback { get; set; } = "That should be good enough. Let's proceed.";
        public virtual string ImprovingFeedback { get; set; } = "Getting closer...";
        public virtual string KeepWorkingFeedback { get { return $"Keep trying. If it helps, the screen of your phone should be vaguely {OrientationCue()}"; } }

        public Quaternion FrameShift { get; set; } = Quaternion.Identity; // Will need to be set if working with relative, rather than absolute, quaternions (like in spells).
        protected virtual string OrientationCue()
        {
            var RotationAxesAndCues = new Dictionary<Vector3, string>()
            {
                { Vector3.UnitY, "vertical, in portrait mode" },
                { -Vector3.UnitY, "vertical, in upside-down portrait mode" },
                { Vector3.UnitX, "vertical, in landscape mode tipped to its left" },
                { -Vector3.UnitX, "vertical, in landscape mode tipped to its right" },
                { Vector3.UnitZ, "horizontal, facing up" },
                { -Vector3.UnitZ, "horizontal, facing down" }
            };

            var gravityVectorAtTargetOrientation = Vector3.UnitZ.RotatedBy(FrameShift).RotatedBy(Target);
            Vector3 bestVector = RotationAxesAndCues.Keys.OrderByDescending(v => Vector3.Dot(v, gravityVectorAtTargetOrientation)).First();

            return RotationAxesAndCues[bestVector];
        }
    }


    public class SpellTutorial : ITutorial
    {
        public Spell taughtSpell { get; set; } = Spell.None;

        public SpellTutorial(Spell spell, params string[] mnemonics)
        {
            taughtSpell = spell;
            OrientationStep newStep;
            var Mnem = mnemonics.ToList();
            // Tutorial step #1 is on getting the Zero Stance right.
            _steps.Add( new OrientationStep()
            {
                Target = spell.ZeroStance,
                IntroSpeech = "First up is the spell's 'zero stance'.  This is sometimes used to identify the spell.",
                ExpandedSpeech = (Mnem.Count > 0) ? $"The creator supplied this mnemonic for the zero stance: {Mnem[0]}" : ""
            });
            for (int i = 1; i <= spell.Glyphs.Count; i++)
            {
                newStep = new OrientationStep();
                newStep.Target = spell.Glyphs[i].Orientation;
                var mnem = Mnem.ElementAtOrDefault(i);
                if (mnem != null && mnem.Length > 0) newStep.ExpandedSpeech
                        = "Try this mnemonic for the gesture: {mnem}.";
                newStep.FrameShift = spell.ZeroStance;
                _steps.Add(newStep);
            }
        }

        public SpellTutorial(Spell spell, ITutorialStep finalStep, params string[] mnemonics)
            : this(spell, mnemonics)
        {
            _steps.Add(finalStep);
        }

        public string IntroductoryRemarks
        {
            get { return $"Let's train you to cast {taughtSpell.SpellName}.  It involves a total of {NumberOfSteps} static positions."; }
        }

        public string[] ExpandedRemarks
        {
            get {
                return new List<string> {
                    "You cast spells in Atropos by moving your device to a series of specific orientations, which we refer to as 'Glyphs'.",
                    "Most spells will need between 3 and 5 of these, and some are trickier or pickier than others.",
                    "As you begin casting a glyph, you'll hear a faint tone.  As you get closer to the target orientation, it will get louder.",
                    "When you've got it, if that wasn't the last glyph then you'll hear a sound to let you know that you're casting the next glyph.",
                    "If it was the last glyph, you'll know, as you hear your spell go off and, y'know, people scream and catch fire or whatever.",
                    "During training, if a glyph is frustrating you, shake your device and we will try to describe the pose you're looking for",
                    "At each pose, you get extra leeway - up to twenty degrees for how steady you hold it, and up to five or ten just for patience",
                    "Pro tip: With practice, you may find you don't need the tone anymore, and muscle memory will take over."
                }.ToArray();
            }
        }

        public int NumberOfSteps { get { return taughtSpell.Glyphs.Count + 1; } }

        private List<ITutorialStep> _steps = new List<ITutorialStep>();
        public List<ITutorialStep> Steps { get { return _steps; } }
    }
}