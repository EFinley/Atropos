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
using Android.Graphics;

namespace Atropos.Characters
{
    [Activity]
    class CharacterSheetActivity : SelectorActivity
    {
        public override int layoutID { get; set; } = Resource.Layout.CharacterSheet;
        protected Role targetRole;
        protected Template chosenTemplate;

        private string[] randomNames = new string[]
        {
            "Fletch", "Argent", "Rip", "Char", "Ember", "Slide", "Patch", "Dart", "Smythe", "Walker", "Jaunt", "Topaz", "Shatter",
            "Vaunt", "Nails", "Leopard", "Acre", "Bury", "Delta", "Brigand", "Snitch", "Elmwood", "Shriek", "Veil", "Nuke", "Phoenix",
            "Centaur", "Demise", "Shank", "Pivot", "Windy", "Hitch", "Hatch", "Spite", "Gleam", "Glimmer", "Gloaming", "Hazard",
            "Scratch", "Jive", "Jinx", "Sparrow", "Thrush", "Wren", "Peregrine", "Grin", "Ferret", "Phiz", "Marque", "Andra", "Opus",
            "Feist", "Fetch", "Flitch", "Flinders", "Tweak", "Tweety", "Twist", "Twine", "Twirl", "Swish", "Boots", "Ratch", "Barracuda",
            "Pike", "Gutsy", "Handful", "Wink", "Gem", "Tudor", "Nightly", "Errant", "Arrant", "Knave", "Craft", "Coins", "Surf", "Damsel",
            "Candle", "Shirk", "Blue", "Red", "Dr. Green", "Jape", "Carver", "Quant", "Wine", "Tokay", "Shiraz", "Ruddy", "Pale",
            "Hoppy", "Graze", "Maze", "Gnash", "Tiny", "Fingers", "Dash", "Rope", "Alfred", "March", "October", "Rye", "Blight", "Twins",
            "Rack", "Wrack", "Spiff", "Will", "Clash", "Trial", "Lawless", "Vandal", "Tagger", "Spray", "Skinny", "Rash", "Brash",
            "Mouser", "Chaser", "Shay", "Zephyr", "Link", "Clink", "Clinch", "Cinch", "Cinder", "Dee-dee", "Zap", "Zoom", "Zero", "Marquis",
            "Frenchy", "Teuton", "Tighten", "Runout", "Warrow", "Arrow", "Spindrel", "Ounce", "Gouge", "Scars", "Ink", "Drive", "Poker",
            "Blackjack", "Redjack", "Bluejack", "Greyjay", "Redeye", "Silvereyes", "Mirrors", "Silverjack", "Tineye", "Coppersmith",
            "Bronze", "Brassy", "Indium", "Titan", "Arges", "Minotaur", "Goblin", "Brutus", "Titus", "Hal", "Flambeau", "Troy", "Ithaca",
            "Memphis", "Bangkok", "Baghdad", "Ali", "Aimee", "Aimery", "Blackfoot", "Fiddle", "Fiddler", "Nevermore", "Rattlefoot",
            "Mistwright", "Draughter", "Draugur", "Grue", "Lemure", "Yale", "Gale", "Gallows", "Tyburn", "Tybalt", "Rapier", "Raptor"
        };

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            var startButton = FindViewById<Button>(Resource.Id.charsheet_startBtn);

            // Got here using "Create" next to a Role button on main screen
            if (SelectorActivity.extraData is Role)
            {
                targetRole = (Role)(SelectorActivity.extraData);
                CharacterStats.Current = new CharacterStats();
            }
            // Got here using ????
            else if (SelectorActivity.extraData is CharacterStats)
            {
                CharacterStats.Current = (SelectorActivity.extraData as CharacterStats);
                targetRole = CharacterStats.Current.Role;
                chosenTemplate = CharacterStats.Current.Template; // Could be null
            }
            // Got here from the action menu in some screen later on (e.g. during play)
            else if (SelectorActivity.extraData == null && CharacterStats.Current != null)
            {
                targetRole = CharacterStats.Current.Role;
                chosenTemplate = CharacterStats.Current.Template;
            }
            SelectorActivity.extraData = null;

            // Label the templates according to their associated role
            FindViewById<TextView>(Resource.Id.charsheet_roleLabel)
                .Text = $"{targetRole} Templates";

            startButton.Click += (o, e) =>
            {
                if (FindViewById<TextView>(Resource.Id.charsheet_charname).Text == "")
                    FindViewById<Button>(Resource.Id.charsheet_randomNameBtn).CallOnClick();

                CharacterStats.Current.CharacterName = FindViewById<TextView>(Resource.Id.charsheet_charname).Text;
                CharacterStats.Current.Roles = CharacterStats.Current.Roles ?? new List<Role>() { targetRole };
                CharacterStats.Current.Template = chosenTemplate;

                if (targetRole == Role.Hitter) LaunchDirectly(typeof(SamuraiActivity));
                if (targetRole == Role.Hacker) LaunchDirectly(typeof(DeckerActivity));
                if (targetRole == Role.Sorceror) LaunchDirectly(typeof(MageActivity));
                if (targetRole == Role.Spy) LaunchDirectly(typeof(OperativeActivity));
            };

            FindViewById<Button>(Resource.Id.charsheet_randomNameBtn).Click += (o, e) =>
            {
                BaseActivity.CurrentToaster.RelayToast("Selecting random runner name.");
                FindViewById<TextView>(Resource.Id.charsheet_charname).Text = randomNames.GetRandom();
                this.HideKeyboard();
            };

            // Populate the list of templates
            var list = FindViewById<ListView>(Resource.Id.list);
            var adapter = new TemplateAdapter(this);
            list.ItemClick += (o, e) => { chosenTemplate = adapter._items[e.Position]; };
            chosenTemplate = adapter._items[0];
            list.Adapter = adapter;

            // If there's at least one stored PC, open up the hidden "Load region" so they can be selected from the spinner there.
            var storedChars = CharacterStats.GetStoredByRole(targetRole);
            if (storedChars.Count() >= 1)
            {
                //FindViewById(Resource.Id.charsheet_loadCharsRegion).Visibility = ViewStates.Visible;
            }

            this.HideKeyboard();
        }

        protected void OnLoadSpinnerChanged(object sender, AdapterView.ItemSelectedEventArgs e)
        {
            // Store the current character (without applying changes)
            if (CharacterStats.Current != null) CharacterStats.StoreCharacter(CharacterStats.Current);

            // Retrieve and load the 
        }

        public class TemplateAdapter : BaseAdapter<Template>
        {
            private readonly Activity _context;
            public readonly List<Template> _items;

            public TemplateAdapter(CharacterSheetActivity context)
                : base()
            {
                _context = context;
                _items = Template.AllTemplates.Where(t => t.Role == context.targetRole).ToList();
            }

            public override long GetItemId(int position)
            {
                return position;
            }
            public override Template this[int position]
            {
                get { return _items[position]; }
            }
            public override int Count
            {
                get { return _items.Count; }
            }

            public override View GetView(int position, View convertView, ViewGroup parent)
            {
                var v = convertView;

                v = v ?? _context.LayoutInflater.Inflate(Resource.Layout.CharacterSheet_templateRepresentation, null);

                var NameField = v.FindViewById<TextView>(Resource.Id.charsheet_template_nameField);
                var PerkField1 = v.FindViewById<TextView>(Resource.Id.charsheet_template_perkField1);
                var PerkField2 = v.FindViewById<TextView>(Resource.Id.charsheet_template_perkField2);

                Template template = _items[position];
                if (template == null) return v;

                NameField.Text = template.Label;
                PerkField1.Text = template.Perks?[0]?.Name;
                PerkField2.Text = template.Perks?[1]?.Name;
                if (!template.Perks?[0]?.IsImplemented ?? false)
                    PerkField1.SetTextColor(Color.Red);
                else
                    PerkField1.SetTextColor(Color.Blue);
                if (!template.Perks?[1]?.IsImplemented ?? false)
                    PerkField2.SetTextColor(Color.Red);
                else
                    PerkField2.SetTextColor(Color.Blue);

                return v;
            }
        }
    }
}