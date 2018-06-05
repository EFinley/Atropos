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

namespace Atropos.Characters
{
    public enum DamageType
    {
        Bullet,
        Piercing,
        Slashing,
        Impact,
        Fire,
        Lightning,
        Ice
    }

    // Represents a single instance of damage
    [Serializable]
    public struct Damage
    {
        // Intrinsic characteristics
        public const string PREFIX = "Damage";
        public DamageType Type;
        public double Magnitude;
        public double? ToBarriers;
        public bool? ShieldPiercing;
        public string AdditionalEffects;

        public override string ToString()
        {
            return $"{PREFIX}*{(int)Type}*{Magnitude:f2}*{ToBarriers ?? 1.0:f2}*{ShieldPiercing ?? false}*{AdditionalEffects}";
        }

        public static Damage FromString(string inputStr)
        {
            var subfields = inputStr.Split(new char[] { '*' }, 6);
            if (subfields[0] != PREFIX) throw new ArgumentException();
            return new Damage()
            {
                Type = (DamageType)(int.Parse(subfields[1])),
                Magnitude = double.Parse(subfields[2]),
                ToBarriers = double.Parse(subfields[3]),
                ShieldPiercing = bool.Parse(subfields[4]),
                AdditionalEffects = subfields[5]
            };
        }
    }

    // Represents a single wound
    [Serializable]
    public struct Impact
    {
        //public const double SeverityBounced = -2;
        //public const double SeverityAbsorbed = -1;
        public const string PREFIX = "Impact";

        // Intrinsic characteristics
        public double Severity;
        public string Location;
        public string Description;
        public Damage IncurringHit;

        // Derived characteristics
        public double ProportionAbsorbed;
        public string Details;
        public string FullDescription { get => $"{Description.TrimEnd('.')}{Details}"; }

        public override string ToString()
        {
            return $"{PREFIX}*{Severity:f3}*{Location}*{Description}*{IncurringHit}";
        }

        public static Impact FromString(string inputStr)
        {
            var subfields = inputStr.Split(new char[] { '*' }, 5);
            if (subfields[0] != PREFIX) throw new ArgumentException();
            return new Impact()
            {
                Severity = double.Parse(subfields[1]),
                Location = subfields[2],
                Description = subfields[3],
                IncurringHit = Damage.FromString(subfields[4])
            };
        }
    }

    public class Damageable
    {
        public static Damageable Me = new Damageable();

        public List<Impact> DamageSuffered = new List<Impact>();

        public double Toughness { get; set; } = 35; // One-sigma reduction in effectiveness per this many points of effective severity.
        public double Barrier { get; set; } = 25; // A free initial (but small) barrier seems like a good gameplay addition.
        public bool Shielded { get; set; } = false;

        public event EventHandler<EventArgs<Impact>> OnAnyImpact;
        public event EventHandler<EventArgs<Impact>> OnShieldStruck;
        public event EventHandler<EventArgs<Impact>> OnBarrierStruck;
        public event EventHandler<EventArgs<Impact>> OnFleshStruck;

        public static void SetUpStandardHitReactions()
        {
            Damageable.Me.OnShieldStruck += (o, e) => { Speech.Say($"Shield struck by {e.Value.IncurringHit.Type}."); };
            Damageable.Me.OnFleshStruck += (o, e) => { Speech.Say($"Took a {e.Value.FullDescription}"); };
            Damageable.Me.OnBarrierStruck += (o, e) =>
            {
                if (Damageable.Me.Barrier > 0) Speech.Say("Barrier decreased.");
                else Speech.Say("Barrier down.");
            };
        }

        public double TotalSeverity { get => Math.Sqrt(DamageSuffered.Select(d => d.Severity * d.Severity).Sum()); }
        public double EffectivenessMultiplierAfterWounds { get => Math.Exp(-Math.Pow(TotalSeverity / Toughness, 2)); }

        private string GetRandomLocation(Damage damage)
        {
            // Placeholder code awaiting more sophisticated model
            var shortlist = new string[] { "shoulder", "arm", "thigh", "belly", "chest" };
            var side = new string[] { "left", "right" };
            return $"{side.GetRandom()} {shortlist.GetRandom()}";
        }

        private Impact GenerateDescription(Impact impact)
        {
            var descs = new Dictionary<DamageType, string>
            {
                { DamageType.Bullet, "bullet wound to" },
                { DamageType.Piercing, "puncture wound to" },
                { DamageType.Slashing, "cut to" },
                { DamageType.Impact, "blunt trauma to" },
                { DamageType.Fire, "burn on" },
                { DamageType.Ice, "frost burn on" },
                { DamageType.Lightning, "electrical burn on" }
            };
            var coreDesc = descs[impact.IncurringHit.Type];

            string prefix, instruction, barrierEffect;

            var OriginalSeverity = impact.Severity / (1.0 - impact.ProportionAbsorbed);
            var DescribedSeverity = (impact.ProportionAbsorbed < 0.99) ? impact.Severity : OriginalSeverity;
            if (DescribedSeverity < 10)
            {
                prefix = "Trivial";
                instruction = "Feel free to ignore.";
            }
            else if (DescribedSeverity < 20)
            {
                prefix = (Res.CoinFlip) ? "Minor" : "Slight";
                instruction = "Wince but carry on.";
            }
            else if (DescribedSeverity < 35)
            {
                prefix = (Res.CoinFlip) ? "Painful" : "Bloody";
                instruction = (Res.CoinFlip) ? "Act wounded. A civilian would probably fold now." : "Act wounded. Wound penalties significant.";
            }
            else if (DescribedSeverity < 50)
            {
                prefix = (Res.CoinFlip) ? "Severe" : "Brutal";
                instruction = "Gonna need a hospital or a healer. Very hard to think about anything but the pain.";
            }
            else
            {
                prefix = (Res.CoinFlip) ? "Massive" : "Crippling";
                instruction = "You're out of the fight for good. First show agony, then go into shock or pass out.";
            }
            if (impact.ProportionAbsorbed > 0.99) instruction = "";

            var absorbed = impact.ProportionAbsorbed;
            if (absorbed < 0.01) barrierEffect = (Res.CoinFlip) ? ", unprotected." : ".";
            else if (absorbed < 0.25) barrierEffect = ", after punching through armour.";
            else if (absorbed < 0.75) barrierEffect = (Res.CoinFlip) ? ", after zeroing out your barrier." : ", knocking out your defenses.";
            else if (absorbed < 0.99) barrierEffect = ", having been mostly absorbed by your barrier.";
            else barrierEffect = ", fully absorbed by your barrier.";

            var additionalEffects = String.Empty;
            if (!String.IsNullOrEmpty(impact.IncurringHit.AdditionalEffects)) additionalEffects = $"Also: {impact.IncurringHit.AdditionalEffects}.";

            var output = impact;
            output.Description = $"{prefix} {coreDesc} the {impact.Location}.";
            output.Details = $"{barrierEffect}{additionalEffects} {instruction}";
            return output;
        }

        public void Suffer(Damage damage, string Location = null, bool raiseEvents = true)
        {
            var location = Location ?? GetRandomLocation(damage);
            var result = new Impact() { Location = location, IncurringHit = damage, Details = ".", ProportionAbsorbed = 0 };

            // Shields are absolute (except where they're not)
            if (Shielded && !(damage.ShieldPiercing ?? false))
            {
                result.Location = "on shield";
                result.Severity = 0;
                result.Description = "Blocked by shield.";
                result.ProportionAbsorbed = 1.0;
                if (raiseEvents) OnShieldStruck.Raise(result);
            }
            else
            {
                // If shields don't come into it, then absorb it on the barrier (if you can)
                var amountAbsorbed = Math.Min(damage.Magnitude, Barrier / Math.Max(0.001, (damage.ToBarriers ?? 1.0))); // Prevent divide-by-zero - 0.1% is the worst you can get.
                result.Severity = damage.Magnitude - amountAbsorbed;
                result.ProportionAbsorbed = amountAbsorbed / damage.Magnitude;
                Barrier -= amountAbsorbed;

                result = GenerateDescription(result);

                if (amountAbsorbed > 0 && raiseEvents) OnBarrierStruck.Raise(result);
                if (result.Severity > 0)
                {
                    if (raiseEvents) OnFleshStruck.Raise(result);
                    DamageSuffered.Add(result);
                }
            }

            // And, of course...
            if (raiseEvents) OnAnyImpact.Raise(result);
        }
    }
}