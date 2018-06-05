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
            SetupButton(Resource.Id.btn_launch_experimental_mode, typeof(Atropos.Machine_Learning.MachineLearningActivity));
            SetupButton(Resource.Id.btn_dev_hotbutton, typeof(FunctionalityTestActivity));
            SetupButton(Resource.Id.btn_train_spells, typeof(SpellTrainingActivity));
            SetupButton(Resource.Id.btn_train_locks, () => { }, null, false);

            CheckBox solipsismMode = FindViewById<CheckBox>(Resource.Id.chbox_solipsism_mode);
            solipsismMode.CheckedChange += (o, e) => { Res.SolipsismMode = solipsismMode.Checked; };

            CheckBox lefthandedMode = FindViewById<CheckBox>(Resource.Id.chbox_lefthanded_mode);
            lefthandedMode.CheckedChange += (o, e) => { Res.LefthandedMode = lefthandedMode.Checked; Handedness.Update(); };

            CheckBox screenFlipMode = FindViewById<CheckBox>(Resource.Id.chbox_flipscreen_mode);
            screenFlipMode.CheckedChange += (o, e) => { Res.ScreenFlipMode = screenFlipMode.Checked; Handedness.Update(); };

            CheckBox allowSpeakers = FindViewById<CheckBox>(Resource.Id.chbox_allow_speakers);
            allowSpeakers.Click += (o, e) => { Res.AllowSpeakerSounds = allowSpeakers.Checked; };

            CheckBox allowNfc = FindViewById<CheckBox>(Resource.Id.chbox_use_nfc);
            allowNfc.Click += (o, e) => { Res.AllowNfc = allowNfc.Checked; };

            SetupButton(Resource.Id.btn_export_run_data, () => { }, null, false);
            SetupButton(Resource.Id.btn_import_run_data, () => { }, null, false);

            // DELETE USER DATA BUTTON DOES NOT SEEM TO WORK
            // Note that SetupButton is not used here because we want to be able to read our button's text.  There's probably a better way to handle it based on reading the Sender, but meh.
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
        
        protected override void OnResume()
        {
            base.OnResume();
            FindViewById<CheckBox>(Resource.Id.chbox_solipsism_mode).Checked = Res.SolipsismMode;
            FindViewById<CheckBox>(Resource.Id.chbox_lefthanded_mode).Checked = Res.LefthandedMode;
            FindViewById<CheckBox>(Resource.Id.chbox_allow_speakers).Checked = Res.AllowSpeakerSounds;
            FindViewById<CheckBox>(Resource.Id.chbox_use_nfc).Checked = Res.AllowNfc;
        }
    }

    public class SelectorActivity : Activity, IRelayToasts
    {
        public virtual int layoutID { get; set; }
        private static SelectorActivity _currentActivity;
        public static SelectorActivity CurrentActivity
        {
            get { return _currentActivity; }
            set { _currentActivity = value; BaseActivity.CurrentToaster = value; }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(layoutID);
            CurrentActivity = this;
        }

        public void RelayToast(string message, ToastLength length = ToastLength.Short)
        {
            RunOnUiThread(() => { Toast.MakeText(this, message, length).Show(); });
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            var inflater = MenuInflater;
            inflater.Inflate(Resource.Menu.action_items, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Resource.Id.menuaction_character:
                    Toast.MakeText(this, Resource.String.popup_placeholder_character, ToastLength.Short).Show();
                    return true;
                //case Resource.Id.menuaction_nfc:
                //    Toast.MakeText(this, Resource.String.popup_placeholder_nfc, ToastLength.Short).Show();
                //    return true;
                case Resource.Id.menuaction_wifi:
                    //LaunchDirectly(typeof(Communications.WiFiDirectActivity));
                    //LaunchDirectly(typeof(Communications.BLECommsActivity));
                    LaunchDirectly(typeof(Communications.Bluetooth.BTDirectActivity));
                    return true;
                case Resource.Id.menuaction_settings:
                    LaunchDirectly(typeof(SettingsActivity));
                    return true;
                default:
                    return base.OnOptionsItemSelected(item);
            }
        }

        protected void SetupButton(int resId, Action action, object extradata = null, bool isImplemented = true)
        {
            View v = FindViewById(resId);
            SetupButton(v, action, extradata, isImplemented);
        }
        protected void SetupButton(View sourceView, Action action, object extradata = null, bool isImplemented = true)
        {
            SetTypeface(sourceView, "FTLTLT.TTF");
            if (isImplemented)
            {
                sourceView.Click += (o, e) =>
                {
                    extraData = extradata;
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

        //protected void SetupButton(int resId, Res.InteractionMode mode, object extradata = null, bool isImplemented = true)
        //{
        //    View v = FindViewById(resId);
        //    SetupButton(v, mode, extradata, isImplemented);
        //}
        //protected void SetupButton(View sourceView, Res.InteractionMode mode, object extradata = null, bool isImplemented = true)
        //{
        //    SetupButton(sourceView, 
        //        () => 
        //        {
        //            //LaunchDirectly(this, new EventArgs<Res.InteractionMode>(mode));
        //            ActOnFoundTagActivity.LaunchActivity(this, mode, mode.Name + "0000", mode.Directive);
        //        }, 
        //        isImplemented);
        //    //SetTypeface(sourceView, "FTLTLT.TTF");
        //    //if (isImplemented)
        //    //{
        //    //    sourceView.Click += (o, e) =>
        //    //    {
        //    //        LaunchDirectly(this, new EventArgs<Res.InteractionMode>(mode));
        //    //    };
        //    //}
        //    //else
        //    //{
        //    //    sourceView.Click += (o, e) =>
        //    //    {
        //    //        Toast.MakeText(this, Resource.String.popup_option_not_available, ToastLength.Short).Show();
        //    //    };
        //    //}
        //}
        protected void SetupButton(int resId, Type activity, object extradata = null, bool isImplemented = true)
        {
            View v = FindViewById(resId);
            SetupButton(v, activity, extradata, isImplemented);
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
        protected void SetupButton(View sourceView, Type activity, object extradata = null, bool isImplemented = true)
        {
            SetupButton(sourceView,
                () =>
                {
                    LaunchDirectly(activity, extradata);
                },
                extradata,
                isImplemented);
        }

        public static object extraData = null;
        protected void LaunchDirectly(Type activity, object extraData = null)
        {
            SelectorActivity.extraData = extraData;
            var intent = new Intent(Application.Context, activity);
            intent.AddFlags(ActivityFlags.SingleTop);
            intent.AddFlags(ActivityFlags.NewTask);
            Application.Context.StartActivity(intent);
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
    }
}