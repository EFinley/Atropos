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

using MiscUtil;
using Atropos.Characters.TraitInteraction;

namespace Atropos.Characters
{
    // Marker class, and placeholder classes for commonly desired queries.  Makes me think that possibly this should be being done with a db and Linq - ah, well, for now it'll do.
    namespace TraitInteraction
    {
        public abstract class Base
        {
        }

        public class Base<T> : Base
        {
            public Base(Func<Trait, T> effectFunc)
            {
                Effect = effectFunc;
            }
            public Func<Trait, T> Effect { get; set; }
        }

        public class PresentOnlyIf : Base<bool>
        {
            public PresentOnlyIf(Func<Trait, bool> dependencyPredicate) : base(dependencyPredicate)
            {
                IsPresent = dependencyPredicate;
            }
            public Func<Trait, bool> IsPresent { get; set; }
        }

        public class PresentIf : Base<bool>
        {
            public PresentIf(Func<Trait, bool> dependencyPredicate) : base(dependencyPredicate)
            {
                IsPresent = dependencyPredicate;
            }
            public Func<Trait, bool> IsPresent { get; set; }
        }

        public class SuppressedIf : Base<bool>
        {
            public SuppressedIf(Func<Trait, bool> suppressionPredicate) : base(suppressionPredicate)
            {
                IsSuppressed = suppressionPredicate;
            }
            public Func<Trait, bool> IsSuppressed { get; set; }
        }

        public interface AdditiveModifier { }

        public class IsIncreasedBy<T> : Base<T>, AdditiveModifier
        {
            public IsIncreasedBy(Func<Trait, T> getModifierFunction) : base(getModifierFunction)
            {
                GetModifier = getModifierFunction;
            }
            public Func<Trait, T> GetModifier { get; set; }
        }

        public class IsReducedBy<T> : Base<T>, AdditiveModifier
        {
            public IsReducedBy(Func<Trait, T> getPenaltyFunction) : base(getPenaltyFunction)
            {
                GetPenalty = getPenaltyFunction;
            }
            public Func<Trait, T> GetPenalty { get; set; }
        }

        public class IsMultipliedBy : Base<double>
        {
            public IsMultipliedBy(Func<Trait, double> getMultiplierFunction) : base(getMultiplierFunction)
            {
                GetMultiplier = getMultiplierFunction;
            }
            public Func<Trait, double> GetMultiplier { get; set; }
        }

        public class InteractionDefinition<Ttrait, Tinteraction>
        {
            public Trait<Ttrait> InteractedUpon { get; set; }
            public Trait InteractedBy { get; set; }
            public Type InteractionType { get; set; }

            public void ExistsOn(Char character)
            {

            }

            public void Exceeds<Tvalue>(Char onWhom, Tvalue minValue)
            {
                if (!typeof(Tinteraction).IsAssignableFrom(typeof(Base<bool>)))
                    throw new InvalidCastException($"{typeof(Tinteraction).Name} not assignable from TraitInteraction.Base<bool>, yet is being asked a yes/no.");
                InteractedUpon.InteractsWith.Add(InteractedBy,
                    (Base)System.Activator.CreateInstance(InteractionType, (Func<Trait, bool>)(tr =>
                    {
                        return Operator.GreaterThan(onWhom.EffectiveScore<Tvalue>(InteractedBy as Trait<Tvalue>), minValue);
                    })));
            }

            public void By<Tvalue>(Func<Trait, Tvalue> accordingToFunction)
            {
                if (!typeof(Tinteraction).IsAssignableFrom(typeof(Base<Tvalue>)))
                    throw new InvalidCastException($"{typeof(Tinteraction).Name} not assignable from TraitInteraction.Base<{typeof(Tvalue).Name}>, yet is being handed a function which returns one.");
                InteractedUpon.InteractsWith.Add(InteractedBy,
                    (Base)System.Activator.CreateInstance(InteractionType, accordingToFunction));
            }
        }
    }

    public static class TraitExtensions
    {
        
    }
}