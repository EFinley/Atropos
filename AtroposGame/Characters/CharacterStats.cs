using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using PerpetualEngine.Storage;

namespace Atropos.Characters
{
    public enum Role
    {
        None,
        Hitter,
        Hacker,
        Sorceror,
        Spy,
        Any,
        All,
        Self,
        NPC,
        GM
    }

    [Serializable]
    public class CharacterStats
    {
        public static CharacterStats Current { get; set; }

        public string CharacterName { get; set; }

        public List<Role> Roles { get; set; } = new List<Role>();
        public Role Role { get { return (Roles.Count > 0) ? Roles[0] : Role.None; } }

        public Template Template { get; set; }
        public List<Perk> Perks { get { return Template.Perks; } }

        private const string STORED_CHARACTERS_KEY = "__StoredCharactersKey__";

        public static List<CharacterStats> StoredCharacters
        {
            get
            {
                var storedChars = Res.Storage.Get<List<CharacterStats>>(STORED_CHARACTERS_KEY);
                return storedChars ?? new List<CharacterStats>();
            }
        }

        public static void StoreCharacter(CharacterStats character)
        {
            var storedChars = StoredCharacters.ToArray().ToList(); // Guarantees a copy rather than a reference
            storedChars.Add(character);
            Res.Storage.Put(STORED_CHARACTERS_KEY, storedChars);
        }

        public static void RemoveCharacter(CharacterStats character)
        {
            var storedChars = StoredCharacters.ToArray().ToList(); // Guarantees a copy rather than a reference
            storedChars.Remove(character);
            Res.Storage.Put(STORED_CHARACTERS_KEY, storedChars);
        }

        public static void RemoveCharacter(string characterName)
        {
            RemoveCharacter(StoredCharacters.First(c => c.CharacterName == characterName));
        }

        public static IEnumerable<CharacterStats> GetStoredByRole(Role role)
        {
            return StoredCharacters.Where(c => c.Roles.Contains(role));
        }
    }

    [Serializable]
    public class Template
    {
        public string Label;
        public Role Role;
        public List<Perk> Perks = new List<Perk>();

        public Template(string label, Role role, params Perk[] perks)
        {
            Label = label;
            Role = role;
            Perks = perks.ToList();
        }
        public Template(string label, Role role, params string[] perkNames) 
            : this(label, role, perkNames.Select(pN => Perk.ByName[pN]).ToArray()) { }

        public static List<Template> BaseTemplates = new List<Template>()
        {
            new Template("Street Samurai", Role.Hitter, "Whipcrack", "Red X"),
            new Template("Mercenary", Role.Hitter, "Augmented", "Well Balanced"),
            new Template("Combat Mage", Role.Sorceror, "Well of Power", "Dauntless"),
            new Template("Street Shaman", Role.Sorceror, "Totemic Sympathy", "Connected"),
            new Template("Whiz Decker", Role.Hacker, "Ghost", "Ace of Aces"),
            new Template("Pragmatist", Role.Hacker, "Lifehacker", "Always Prepared"),
            new Template("Thief", Role.Spy, "Infiltrator", "Sneakerware"),
            new Template("Trickster", Role.Spy, "Gifted", "Charmed Life")
        };

        public static List<Template> AllTemplates
        {
            get
            {
                var _addedTemplates = Res.Storage.Get<List<Template>>(TEMPLATES_KEY);
                if (_addedTemplates != null) return BaseTemplates.Concat(_addedTemplates).ToList();
                else return BaseTemplates;
            }
        }
        public void Save(Template template)
        {
            var _addedTemplates = Res.Storage.Get<List<Template>>(TEMPLATES_KEY);
            _addedTemplates.Add(template);
            Res.Storage.Put(TEMPLATES_KEY, _addedTemplates);
        }

        private const string TEMPLATES_KEY = "__TemplatesKey__";
    }

    [Serializable]
    public class Perk
    {
        public enum PerkType
        {
            Universal,
            CrossTraining,
            RoleSpecific
        }

        public string Name;
        public PerkType Type;
        public Role Role;
        public string ShortDesc;
        public string LongDesc;
        public Action OnApply;
        public bool IsImplemented = false;

        public bool Selected; // Used in the template customization screen

        public Perk(string name, PerkType type, Role role, string shortDesc, string longDesc = null, Action onApply = null )
        {
            Name = name;
            ShortDesc = shortDesc;
            LongDesc = longDesc ?? "(Long description here.)";
            OnApply = onApply ?? (() => { });
        }

        public static List<Perk> AllPerks = new List<Perk>()
        {
            new Perk("Innate Talent", PerkType.Universal, Role.Any, "Gestures are easier, power is reduced."),
            new Perk("Ace of Aces", PerkType.Universal, Role.Any, "Gestures are trickier, power is increased."),
            new Perk("Well Balanced", PerkType.Universal, Role.Any, "Gestures a bit easier, power a bit better."),
            new Perk("Dauntless", PerkType.Universal, Role.Any, "Pain and injury make you stronger, not weaker."),
            new Perk("Charmed Life", PerkType.Universal, Role.Any, "Tiny chance to turn failure into success."),

            new Perk("Dual Specialty", PerkType.CrossTraining, Role.Any, "Full benefits of two roles."),
            new Perk("Blade-Bearer", PerkType.CrossTraining, Role.Hitter, "Able to use melee combat."),
            new Perk("Deadeye", PerkType.CrossTraining, Role.Hitter, "Gunfight as well as a Hitter."),
            new Perk("Hard Boiled", PerkType.CrossTraining, Role.Hitter, "Tough as a Hitter, feel no pain."),
            new Perk("Gifted", PerkType.CrossTraining, Role.Sorceror, "Can cast a single simple spell."),
            new Perk("Iron Soul", PerkType.CrossTraining, Role.Sorceror, "Resist magic as well as a mage."),
            new Perk("Sensitive", PerkType.CrossTraining, Role.Sorceror, "Sense magic and magic use nearby."),
            new Perk("Desktop Ace", PerkType.CrossTraining, Role.Hacker, "Break into systems, only from home."),
            new Perk("Sneakerware", PerkType.CrossTraining, Role.Hacker, "Handle codes & puzzles like a Hacker."),
            new Perk("Connected", PerkType.CrossTraining, Role.Hacker, "Sometimes a friend is as good as a password."),
            new Perk("Always Prepared", PerkType.CrossTraining, Role.Spy, "Use one or two tools from the Spy's list."),
            new Perk("Trustworthy", PerkType.CrossTraining, Role.Spy, "Schmooze with the best of them."),
            new Perk("Situational Awareness", PerkType.CrossTraining, Role.Spy, "A sense for danger and hidden clues."),

            new Perk("Whipcrack", PerkType.RoleSpecific, Role.Hitter, "Your responses are treated as if they happened faster."),
            new Perk("Achilles", PerkType.RoleSpecific, Role.Hitter, "You are ridiculously hard to kill."),
            new Perk("Red X", PerkType.RoleSpecific, Role.Hitter, "You just never miss with a firearm."),
            new Perk("Augmented", PerkType.RoleSpecific, Role.Hitter, "You're a cyborg - stronger, faster, extra SFX."),
            new Perk("Totemic Sympathy", PerkType.RoleSpecific, Role.Sorceror, "Non-magic get bonuses (and SFX) from magic used."),
            new Perk("Polymagic", PerkType.RoleSpecific, Role.Sorceror, "You practice a wide variety of spells."),
            new Perk("Well of Power", PerkType.RoleSpecific, Role.Sorceror, "You can cast over and over without drain."),
            new Perk("Subtle Arts", PerkType.RoleSpecific, Role.Sorceror, "Your magic is hard for senses & spells to detect."),
            new Perk("Lifehacker", PerkType.RoleSpecific, Role.Hacker, "Your hacking can help with tasks in the Real World(TM)."),
            new Perk("Ghost", PerkType.RoleSpecific, Role.Hacker, "Nearly impossible to trace, sometimes silence alarms."),
            new Perk("Guru", PerkType.RoleSpecific, Role.Hacker, "Write your own code to get perfect results, eventually."),
            new Perk("Infiltrator", PerkType.RoleSpecific, Role.Spy, "Hard to detect, free distractions for guards."),
            new Perk("Grifter", PerkType.RoleSpecific, Role.Spy, "Disguises work better, see through lies."),
            new Perk("Steady Hands", PerkType.RoleSpecific, Role.Spy, "No catastrophic consequences when working on puzzles.")
        };

        public static Dictionary<string, Perk> ByName { get { return AllPerks.ToDictionary((perk) => { return perk.Name; }); } }
    }
}