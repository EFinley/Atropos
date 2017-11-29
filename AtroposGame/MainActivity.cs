using System;
using Android;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
//using DeviceMotion;
//using DeviceMotion.Plugin;
//using DeviceMotion.Plugin.Abstractions;
using Android.Nfc;
using Android.Nfc.Tech;
using Java.IO;
using Android.Media;

using Android.Util;
using System.Text;
using System.Collections.Generic;
using System.Resources;
// using Accord.Statistics.Filters;
// using Accord.Math;
using System.Linq;
using PerpetualEngine.Storage;
using Android.Hardware;
using MiscUtil;

namespace com.Atropos
{
    [Activity(Label = "Atropos", Icon = "@drawable/atropos_sigil", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class MainActivity : SelectorActivity
    {
        //static MainActivity() { layoutID = Resource.Layout.Main; }
        public override int layoutID { get; set; } = Resource.Layout.Main;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // This line still included, because looking it up proved a right pain in the butt.  To clear the SimpleStorage contents out entirely, this should do:
            // Android.Preferences.PreferenceManager.GetDefaultSharedPreferences(ApplicationContext).Edit().Clear().Apply();

            InitializeAll();

            SetupButton(Resource.Id.role_samurai, typeof(SamuraiActivity));
            SetupButton(Resource.Id.role_mage, typeof(MageActivity));
            SetupButton(Resource.Id.role_decker, typeof(DeckerActivity));
            SetupButton(Resource.Id.role_operative, typeof(OperativeActivity));
        }
        
        protected void DoOnResume()
        {
            base.OnResume();
            //Res.SFX.ResumeAll();
        }

        protected override void OnPause()
        {
            base.OnPause();
            Res.SFX.StopAll();
        }

        public static void InitializeAll()
        {
            SimpleStorage.SetContext(Application.Context);
            if ((Res.InteractionModes?.Count ?? 0) > 0) return; // Already done this; okay, cool.
            InteractionLibrary.InitializeAll();
            MasterSpellLibrary.LoadAll();
            MasterFechtbuch.LoadAll();
        }
    }
}

