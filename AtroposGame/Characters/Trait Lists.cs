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

using Atropos.Characters.TraitInteraction;

namespace Atropos.Characters
{
    public class SpecificTraitLists
    {
        // TODO - search-and-replace "Trait<" with "public Trait<" in this document only. 
        public class CharacterTraits : TraitList
        {
            Trait<double> GestureEaseOfUse = Create(1.0);
            Trait<double> EffectMagnitudeMultiplier = Create(1.0);

            Trait<bool> IsWounded = Create(false);
            Trait<double> WoundSeverity = Create(0.0);

            

            public CharacterTraits()
            {
                // Here we define all of the interrelationships between traits - a couple of examples for now.  Basically each represents a single rule in the RPG.
                IsWounded
                    .Interaction<PresentIf>(WoundSeverity)
                    .Exceeds(Char.PC, 1.0);

                EffectMagnitudeMultiplier
                    .Interaction<IsMultipliedBy>(WoundSeverity)
                    .By(tr => 1.0f - 0.1f * Char.PC.EffectiveScore(WoundSeverity));
            }
        }

        public class ScenarioTraits : TraitList
        {
            Trait<bool> SilentAlarmTriggered = Create<bool>();
            Trait<bool> AudibleAlarmTriggered = Create<bool>();
        }

        public class MechanicsTraits : TraitList
        {

        }
    }
}