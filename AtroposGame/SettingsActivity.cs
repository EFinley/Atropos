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

using com.Atropos.Machine_Learning;

namespace com.Atropos
{
    [Activity(Label = "Atropos :: Settings ::")]
    public class SettingsActivity : SelectorActivity
    {
        public override int layoutID { get; set; } = Resource.Layout.SettingsPage;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Link the checkboxes and buttons and such
            SetupButton(Resource.Id.btn_train_spells, InteractionLibrary.SpellTeaching);
            SetupButton(Resource.Id.btn_train_melee, InteractionLibrary.MeleeTeaching, false);
            SetupButton(Resource.Id.btn_train_locks, InteractionLibrary.LockTraining, false);

            CheckBox allowSpeakers = FindViewById<CheckBox>(Resource.Id.chbox_allow_speakers);
            allowSpeakers.Checked = Res.AllowSpeakerSounds;
            allowSpeakers.Click += (o, e) => { Res.AllowSpeakerSounds = allowSpeakers.Checked; };

            SetupButton(Resource.Id.btn_launch_experimental_mode, typeof(Atropos.Machine_Learning.MachineLearningActivity));
            SetupButton(Resource.Id.btn_export_run_data, InteractionLibrary.Current, false);
            SetupButton(Resource.Id.btn_import_run_data, InteractionLibrary.Current, false);
        }
    }
}