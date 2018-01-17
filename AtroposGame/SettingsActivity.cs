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

using Atropos.Machine_Learning;
using Android.Graphics;
using MiscUtil;

namespace Atropos
{
    [Activity(Label = "Atropos :: Settings ::")]
    public class SettingsActivity : SelectorActivity
    {
        public override int layoutID { get; set; } = Resource.Layout.SettingsPage;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Link the checkboxes and buttons and such
            SetupButton(Resource.Id.btn_dev_hotbutton, typeof(QRMainActivity));
            SetupButton(Resource.Id.btn_train_spells, InteractionLibrary.SpellTeaching);
            SetupButton(Resource.Id.btn_train_locks, InteractionLibrary.LockTraining, false);

            CheckBox allowSpeakers = FindViewById<CheckBox>(Resource.Id.chbox_allow_speakers);
            allowSpeakers.Checked = Res.AllowSpeakerSounds;
            allowSpeakers.Click += (o, e) => { Res.AllowSpeakerSounds = allowSpeakers.Checked; };

            SetupButton(Resource.Id.btn_launch_experimental_mode, typeof(Atropos.Machine_Learning.MachineLearningActivity));
            SetupButton(Resource.Id.btn_export_run_data, InteractionLibrary.Current, false);
            SetupButton(Resource.Id.btn_import_run_data, InteractionLibrary.Current, false);

            Button deleteData = FindViewById<Button>(Resource.Id.btn_delete_user_data);
            SetTypeface(deleteData, "FTLTLT.TTF");
            deleteData.Click += (o, e) =>
            {
                string confirmationMessage = "CONFIRM - DELETE ALL SPELLS ETC?";

                if (deleteData.Text != confirmationMessage)
                {
                    deleteData.Text = confirmationMessage;
                    System.Threading.Tasks.Task.Delay(1000)
                        .ContinueWith(_ => { deleteData.Text = "Delete Stored Data"; });
                    return;
                }

                RunOnUiThread(() =>
                {
                    Android.Preferences.PreferenceManager.GetDefaultSharedPreferences(Application.Context).Edit().Clear().Apply();
                    MasterSpellLibrary.LoadAll();
                    MasterFechtbuch.LoadAll();
                    //Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
                });
            };
        }
    }

    public class SelectorActivity : Activity
    {
        public virtual int layoutID { get; set; }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(layoutID);
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

        protected void SetupButton(int resId, Action action, bool isImplemented = true)
        {
            View v = FindViewById(resId);
            SetupButton(v, action, isImplemented);
        }
        protected void SetupButton(View sourceView, Action action, bool isImplemented = true)
        {
            SetTypeface(sourceView, "FTLTLT.TTF");
            if (isImplemented)
            {
                sourceView.Click += (o, e) =>
                {
                    action?.Invoke();
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

        protected void SetupButton(int resId, Res.InteractionMode mode, bool isImplemented = true)
        {
            View v = FindViewById(resId);
            SetupButton(v, mode, isImplemented);
        }
        protected void SetupButton(View sourceView, Res.InteractionMode mode, bool isImplemented = true)
        {
            SetupButton(sourceView, 
                () => 
                {
                    //LaunchDirectly(this, new EventArgs<Res.InteractionMode>(mode));
                    ActOnFoundTagActivity.LaunchActivity(this, mode, mode.Name + "0000", mode.Directive);
                }, 
                isImplemented);
            //SetTypeface(sourceView, "FTLTLT.TTF");
            //if (isImplemented)
            //{
            //    sourceView.Click += (o, e) =>
            //    {
            //        LaunchDirectly(this, new EventArgs<Res.InteractionMode>(mode));
            //    };
            //}
            //else
            //{
            //    sourceView.Click += (o, e) =>
            //    {
            //        Toast.MakeText(this, Resource.String.popup_option_not_available, ToastLength.Short).Show();
            //    };
            //}
        }
        protected void SetupButton(int resId, Type activity, bool isImplemented = true)
        {
            View v = FindViewById(resId);
            SetupButton(v, activity, isImplemented);
            //SetTypeface(v, "FTLTLT.TTF");
            //if (isImplemented)
            //{
            //    v.Click += (o, e) =>
            //    {
            //        var intent = new Intent(Application.Context, activity);
            //        intent.AddFlags(ActivityFlags.SingleTop);
            //        intent.AddFlags(ActivityFlags.NewTask);
            //        Application.Context.StartActivity(intent);
            //    };
            //}
            //else
            //{
            //    v.Click += (o, e) =>
            //    {
            //        Toast.MakeText(this, Resource.String.popup_option_not_available, ToastLength.Short).Show();
            //    };
            //}
        }
        protected void SetupButton(View sourceView, Type activity, bool isImplemented = true)
        {
            SetupButton(sourceView,
                () =>
                {
                    var intent = new Intent(Application.Context, activity);
                    intent.AddFlags(ActivityFlags.SingleTop);
                    intent.AddFlags(ActivityFlags.NewTask);
                    Application.Context.StartActivity(intent);
                },
                isImplemented);
        }

        //protected void LaunchDirectly(object sender, EventArgs<Res.InteractionMode> args)
        //{
        //    var selectedMode = args.Value;
        //    ActOnFoundTagActivity.LaunchActivity(this, selectedMode, selectedMode.Name + "0000", selectedMode.Directive);
        //}

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
    }
}