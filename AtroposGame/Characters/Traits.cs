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
    public class GameMechanics
    {
        public static Char CurrentActingChar;
        public static Char CurrentTargetChar;
    }

    public class Trait
    {
        public string Name { get; set; }
        public Dictionary<Trait, TraitInteraction.Base> InteractsWith;

        public static List<Trait> AllTraits = new List<Trait>();

        public static SpecificTraitLists.CharacterTraits Char;
        public static SpecificTraitLists.ScenarioTraits Scen;
        public static SpecificTraitLists.MechanicsTraits Mech;

        //private static object _syncLock = new object();
        //private static bool _namesAreSet = false;
        //static Trait()
        //{
        //    Char = new SpecificTraitLists.CharacterTraits();
        //    Mech = new SpecificTraitLists.MechanicsTraits();

        //// Guarantee thread-safety in this process
        //lock (_syncLock)
        //{
        //    if (!_namesAreSet)
        //    {
        //        Char.SetTraitNames();
        //        Mech.SetTraitNames();
        //        _namesAreSet = true;
        //    }
        //}
        //}

        
    }

    public class Trait<T> : Trait
    {
        public T DefaultValue;
        private T _baseValue;
        public T Value { get => _getValue(); set => _setValue(value); }
        private T _getValue() { return _baseValue; }
        private void _setValue(T value) { }

        //public void Interaction<Tmode, Tresult>(Trait otherTrait, Func<Trait, Tresult> effectFunc) where Tmode : TraitInteraction.Base<Tresult>
        public TraitInteraction.InteractionDefinition<T, Tmode> Interaction<Tmode>(Trait otherTrait) where Tmode : TraitInteraction.Base
        {
            //InteractsWith.Add(otherTrait, (TraitInteraction.Base)System.Activator.CreateInstance(typeof(Tmode), effectFunc));
            return new TraitInteraction.InteractionDefinition<T, Tmode>() { InteractedUpon = this, InteractedBy = otherTrait, InteractionType = typeof(Tmode) };
        }
    }

    public class Char
    {
        public static Char PC { get; set; }

        public CharacterStats Stats { get; set; }
        public IEnumerable<Trait> Traits { get; set; }

        public bool Has(Trait trait)
        {
            bool traitIsPresent = this.Traits.Contains(trait);
            foreach (var kvp in trait.InteractsWith.Where(t => t.Value is PresentIf))
            {
                traitIsPresent = traitIsPresent || (kvp.Value as PresentIf).IsPresent(kvp.Key);
            }
            foreach (var kvp in trait.InteractsWith.Where(t => t.Value is PresentOnlyIf))
            {
                traitIsPresent = traitIsPresent && (kvp.Value as PresentOnlyIf).IsPresent(kvp.Key);
            }
            foreach (var kvp in trait.InteractsWith.Where(t => t.Value is SuppressedIf))
            {
                traitIsPresent = traitIsPresent && !(kvp.Value as SuppressedIf).IsSuppressed(kvp.Key);
            }
            return traitIsPresent;
        }

        public T EffectiveScore<T>(Trait<T> trait)
        {
            T Score = trait.Value;
            foreach (var kvp in trait.InteractsWith.Where(t => t.Value is AdditiveModifier))
            {
                if (kvp.Value is IsIncreasedBy<T> bonusInteraction) Score = Operator.Add(Score, (bonusInteraction.GetModifier(kvp.Key)));
                if (kvp.Value is IsReducedBy<T> penaltyInteraction) Score = Operator.Subtract(Score, (penaltyInteraction.GetPenalty(kvp.Key)));
            }
            foreach (var kvp in trait.InteractsWith.Where(t => t.Value is IsMultipliedBy))
            {
                Score = Operator.MultiplyAlternative(Score, (kvp.Value as IsMultipliedBy).GetMultiplier(kvp.Key));
            }
            return Score;
        }
    }

    public class TraitList // Will be incarnate as three subclasses, accessible via Trait.Char, Trait.Scen, etc.
    {
        public TraitList()
        {
            SetTraitNames();
        }

        public Trait this[string name]
            { get => Trait.AllTraits.Find(t => t.Name == name); }

        protected static Trait Create()
        {
            var result = new Trait();
            //All.Add(result);
            Trait.AllTraits.Add(result);
            return result;
        }

        protected static Trait<T> Create<T>(T initialVal = default(T))
        {
            var result = new Trait<T>();
            //All.Add(result);
            Trait.AllTraits.Add(result);
            result.Value = result.DefaultValue = initialVal;
            return result;
        }

        public void SetTraitNames()
        {
            var TraitList = this
                .GetType()
                .GetProperties()
                .Where(p => p.PropertyType == typeof(Trait));
            foreach (var pInfo in TraitList)
            {
                var name = pInfo.Name;
                var trait = (Trait)pInfo.GetValue(null);
                trait.Name = name;
                pInfo.SetValue(null, trait);
                //All.Add(trait);
            }
        }
    }
}