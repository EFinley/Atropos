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
using Android.Graphics;
using Atropos.Communications;
using Atropos.Encounters;

namespace Atropos
{
    [Activity]
    public class SelectorActivity : Activity
    {
        public virtual int layoutID { get; set; }
        private static int[] EncounterPromptIDs
            = new int[] { Resource.Id.choice_encounter_1, Resource.Id.choice_encounter_2, Resource.Id.choice_encounter_3 };
        private static int[] EncounterPromptTextfieldIDs
            = new int[] { Resource.Id.choice_enounter_1_text, Resource.Id.choice_enounter_2_text, Resource.Id.choice_enounter_3_text }; // Oops - "enounter".  Oh, well.
        private List<View> EncounterPrompts = new List<View>();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the layout resource associated with this activity
            SetContentView(layoutID);

            // Set up the Encounter Prompts - the buttons which simulate location awareness
            foreach (var PromptID in EncounterPromptIDs)
            {
                var Prompt = FindViewById<RelativeLayout>(PromptID);
                if (Prompt == null) continue;
                SetTypeface(PromptID, "PRISTINA.TTF");
                EncounterPrompts.Add(Prompt);
                Prompt.Click += async (o, e) =>
                {
                    int localIndex = EncounterPrompts.IndexOf(Prompt);
                    var EncElement = Elements.ElementAtOrDefault(localIndex);
                    if (EncElement == default(EncounterElement)) { RemoveElement(localIndex); return; }

                    await EncElement.DoElement();
                    EncElement.Complete();

                    if (EncElement.nextElements.Count == 0) RemoveElement(localIndex);
                    else
                    {
                        RemoveElement();
                        foreach (EncounterElement nextElem in EncElement.nextElements)
                        {
                            DisplayElement(nextElem);
                        }
                    }
                };
            }

            // Default to Postcard From The Shadows encounter sequence (during testing, given that there are no others defined)
            if (EncounterElement.CurrentElement == null && EncounterPrompts.Count > 0) DisplayElement(EncounterElement.SetUpPostcard());

            // Feedback on whether our networking info is carrying through changeover...
            if (WiFiMessageCenter.Client != null)
            {
                Toast.MakeText(this, $"Found WifiClient, IsConnected {WiFiMessageCenter.Client.IsConnected}, with {AddressBook.Names.Join()} in the address book.", ToastLength.Long).Show();
            }
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            var inflater = MenuInflater;
            inflater.Inflate(Resource.Menu.action_items, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            Intent intent;
            switch (item.ItemId)
            {
                case Resource.Id.menuaction_character:
                    Toast.MakeText(this, Resource.String.popup_placeholder_character, ToastLength.Short).Show();
                    return true;
                //case Resource.Id.menuaction_nfc:
                //    Toast.MakeText(this, Resource.String.popup_placeholder_nfc, ToastLength.Short).Show();
                //    return true;
                case Resource.Id.menuaction_wifi:
                    intent = new Intent(this, typeof(Communications.WiFiDirectActivity));
                    intent.AddFlags(ActivityFlags.SingleTop);
                    intent.AddFlags(ActivityFlags.NewTask);
                    StartActivity(intent);
                    return true;
                case Resource.Id.menuaction_settings:
                    intent = new Intent(this, typeof(SettingsActivity));
                    intent.AddFlags(ActivityFlags.NewTask);
                    intent.AddFlags(ActivityFlags.SingleTop);
                    StartActivity(intent);
                    return true;
                default:
                    return base.OnOptionsItemSelected(item);
            }
        }

        protected void SetupButton(int resId, Res.InteractionMode mode, bool currentlyEnabled = true)
        {
            View v = FindViewById(resId);
            SetupButton(v, mode, currentlyEnabled);
        }
        protected void SetupButton(View sourceView, Res.InteractionMode mode, bool currentlyEnabled = true)
        {
            SetTypeface(sourceView, "FTLTLT.TTF");
            if (currentlyEnabled)
            {
                sourceView.Click += (o, e) =>
                    {
                        LaunchDirectly(this, new EventArgs<Res.InteractionMode>(mode));
                    }; 
            }
            else
            {
                sourceView.Click += (o, e) =>
                {
                    Toast.MakeText(this, Resource.String.popup_option_not_available, ToastLength.Short).Show();
                };
            }
        }
        protected void SetupButton(int resId, Type activity, bool isImplemented = true)
        {
            View v = FindViewById(resId);
            SetTypeface(v, "FTLTLT.TTF");
            if (isImplemented)
            {
                v.Click += (o, e) =>
                    {
                        var intent = new Intent(Application.Context, activity);
                        intent.AddFlags(ActivityFlags.SingleTop);
                        intent.AddFlags(ActivityFlags.NewTask);
                        Application.Context.StartActivity(intent);
                    };
            }
            else
            {
                v.Click += (o, e) =>
                {
                    Toast.MakeText(this, Resource.String.popup_option_not_available, ToastLength.Short).Show();
                };
            }
        }

        protected void LaunchDirectly(object sender, EventArgs<Res.InteractionMode> args)
        {
            var selectedMode = args.Value;
            ActOnFoundTagActivity.LaunchActivity(this, selectedMode, selectedMode.Name + "0000", selectedMode.Directive);
        }

        protected void SetTypeface(int resId, string fontFilename)
        {
            var tgtView = FindViewById(resId);
            SetTypeface(tgtView, fontFilename);
        }

        protected void SetTypeface(View tgtView, string fontFilename)
        {
            if (tgtView == null) return;
            Typeface tf = Typeface.CreateFromAsset(this.Assets, fontFilename);

            var vg = tgtView as ViewGroup;
            if (vg != null)
            {
                foreach (int i in Enumerable.Range(0, vg.ChildCount))
                SetTypeface(vg.GetChildAt(i), fontFilename);
            }

            else (tgtView as TextView)?.SetTypeface(tf, TypefaceStyle.Normal);
        }

        //public List<EncounterElement> Elements = new List<EncounterElement>(3);
        public EncounterElement[] Elements = new EncounterElement[3];
        public void DisplayElement(EncounterElement element)
        {
            // Clear the last spot on the list if necessary.
            if (Elements.Count(e => e != null) >= EncounterPrompts.Count) RemoveElement(EncounterPrompts.Count - 1);

            int newItemIndex = (Elements.Any(e => e == null)) ? Elements.ToList().IndexOf(null) : Elements.Length;
            Elements[newItemIndex] = element;
            var p = EncounterPrompts[newItemIndex];
            var text = p.FindViewById<TextView>(EncounterPromptTextfieldIDs[newItemIndex]);

            text.Text = element.ButtonLabel ?? element.Name;
            p.Visibility = ViewStates.Visible;
            element.Begin();
        }
        public void RemoveElement(int index = -1)
        {
            if (index == -1)
            {
                int tgtCount = EncounterPrompts.Count;
                for (int i = 0; i < tgtCount; i++) RemoveElement(i);
            }
            else
            {
                Elements[index] = null;
                EncounterPrompts[index].Visibility = ViewStates.Gone;
            }
        }
    }

    [Activity(Label = "Atropos :: Hitter ::")]
    public class SamuraiActivity : SelectorActivity
    {
        public override int layoutID { get; set; } = Resource.Layout.Samurai;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetupButton(Resource.Id.choice_samurai_gun, InteractionLibrary.Gunfight);
            SetupButton(Resource.Id.choice_samurai_katana, typeof(Machine_Learning.MeleeAlphaActivity));
        }
    }

    [Activity(Label = "Atropos :: Sorceror ::")]
    public class MageActivity : SelectorActivity
    {
        public override int layoutID { get; set; } = Resource.Layout.Mage;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetupButton(Resource.Id.choice_mage_castspell, InteractionLibrary.SpellCasting);
            SetupButton(Resource.Id.choice_mage_gun, InteractionLibrary.Gunfight);
        }
    }

    [Activity(Label = "Atropos :: Hacker ::")]
    public class DeckerActivity : SelectorActivity
    {
        public override int layoutID { get; set; } = Resource.Layout.Decker;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetupButton(Resource.Id.choice_decker_deck, InteractionLibrary.Decking, false);
            SetupButton(Resource.Id.choice_decker_gun, InteractionLibrary.Gunfight);
        }
    }

    [Activity(Label = "Atropos :: Spy ::")]
    public class OperativeActivity : SelectorActivity
    {
        public override int layoutID { get; set; } = Resource.Layout.Operative;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetupButton(Resource.Id.choice_operative_toolkit, typeof(ToolkitActivity));
            SetupButton(Resource.Id.choice_operative_gun, InteractionLibrary.Gunfight);
        }
    }

    [Activity]
    public class ToolkitActivity : SelectorActivity
    {
        public override int layoutID { get; set; } = Resource.Layout.OperativeToolbox;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetupButton(Resource.Id.choice_toolkit_bypass, typeof(BypassActivity));
            SetupButton(Resource.Id.choice_toolkit_safecracking, typeof(Atropos.Locks.SafecrackingActivity));
            SetupButton(Resource.Id.choice_toolkit_lockpicking, typeof(Atropos.Locks.LockPickingActivity));
        }
    }

}