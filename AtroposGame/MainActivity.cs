using System;
using Android;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
//using DeviceMotion;
//
//
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
using Atropos.Characters;
using System.Threading.Tasks;

namespace Atropos
{
    [Activity(Label = "Atropos", Icon = "@drawable/atropos_sigil", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class MainActivity : SelectorActivity
    {
        public override int layoutID { get; set; } = Resource.Layout.Main;
        public static event EventHandler<EventArgs> OnFullStop;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            //try
            //{
            //    //throw new Java.Lang.OutOfMemoryError();
            //    FindViewById<ImageView>(Resource.Id.role_samurai).SetImageResource(Resource.Drawable.hitter_logo);
            //    Task.Delay(100).Wait();
            //    FindViewById<ImageView>(Resource.Id.role_decker).SetImageResource(Resource.Drawable.hacker_logo);
            //    Task.Delay(100).Wait();
            //    FindViewById<ImageView>(Resource.Id.role_mage).SetImageResource(Resource.Drawable.sorcerer_logo);
            //    Task.Delay(100).Wait();
            //    FindViewById<ImageView>(Resource.Id.role_operative).SetImageResource(Resource.Drawable.spy_logo);
            //}
            //catch (Java.Lang.OutOfMemoryError)
            //{
            //    FindViewById<ImageView>(Resource.Id.role_samurai).SetImageResource(Resource.Drawable.hitter_logo_small);
            //    FindViewById<ImageView>(Resource.Id.role_decker).SetImageResource(Resource.Drawable.hacker_logo_small);
            //    FindViewById<ImageView>(Resource.Id.role_mage).SetImageResource(Resource.Drawable.sorcerer_logo_small);
            //    Task.Delay(100).Wait();
            //    FindViewById<ImageView>(Resource.Id.role_operative).SetImageResource(Resource.Drawable.spy_logo_small);
            //}
            //FindViewById<ImageView>(Resource.Id.role_samurai).SetImageResource(Resource.Drawable.hitter_logo);
            //Task.Delay(1000)
            //    .ContinueWith(_ => FindViewById<ImageView>(Resource.Id.role_decker).SetImageResource(Resource.Drawable.hacker_logo))
            //    .ContinueWith(_ => Task.Delay(1000).Wait())
            //    .ContinueWith(_ => FindViewById<ImageView>(Resource.Id.role_mage).SetImageResource(Resource.Drawable.sorcerer_logo))
            //    .ContinueWith(_ => Task.Delay(1000).Wait())
            //    .ContinueWith(_ => FindViewById<ImageView>(Resource.Id.role_operative).SetImageResource(Resource.Drawable.spy_logo))
            //    .ContinueWith(_ => Task.Delay(1000).Wait())
            //    .ContinueWith(_ => FindViewById<ImageView>(Resource.Id.role_mage).SetImageResource(Resource.Drawable.sorcerer_logo_small))
            //    .ContinueWith(_ => Task.Delay(1000).Wait())
            //    .ContinueWith(_ => FindViewById<ImageView>(Resource.Id.role_operative).SetImageResource(Resource.Drawable.spy_logo_small));
            //InitImageButton(Resource.Id.role_samurai, Resource.Drawable.hitter_logo, Resource.Drawable.hitter_logo_small);
            //InitImageButton(Resource.Id.role_decker, Resource.Drawable.hacker_logo, Resource.Drawable.hacker_logo_small);
            //InitImageButton(Resource.Id.role_mage, Resource.Drawable.sorcerer_logo, Resource.Drawable.sorcerer_logo_small);
            //InitImageButton(Resource.Id.role_operative, Resource.Drawable.spy_logo, Resource.Drawable.spy_logo_small);


            InitializeAll();

            //// TODO!  This all fails because for some reason the main panel isn't displaying the "Create" button.  Can't figure out why, so I'm punting on it for now.
            //foreach (var roleInfo in RoleInfoList)
            //{
            //    this.RotateText(roleInfo.createButtonID);
            //    SetTypeface(roleInfo.createButtonID, "FTLTLT.TTF");

            //    var namesField = FindViewById<TextView>(roleInfo.namesFieldID);

            //    var storedChars = CharacterStats.GetStoredByRole(roleInfo.role);
            //    if (storedChars.Count() == 0)
            //        SetupButton(roleInfo.launchButtonID, roleInfo.activityType);
            //    else if (storedChars.Count() == 1)
            //    {
            //        namesField.Text = storedChars.First().CharacterName;
            //        SetupButton(roleInfo.launchButtonID, roleInfo.activityType, storedChars.First());
            //    }
            //    else
            //    {
            //        namesField.Text = $"{storedChars.Count()} Saved";
            //        SetupButton(roleInfo.launchButtonID, roleInfo.activityType, storedChars.First());
            //    }
            //}

            //this.RotateText(Resource.Id.role_samurai_createBtn);
            //SetTypeface(Resource.Id.role_samurai_createBtn, "FTLTLT.TTF");
            //var storedHitters = CharacterStats.GetStoredByRole(Role.Hitter);
            //FindViewById<TextView>(Resource.Id.role_samurai_storedNames).Text
            //    = (storedHitters.Count() > 1) ? $"{storedHitters.Count()} Saved"
            //    : (storedHitters.Count() == 1) ? storedHitters.First().CharacterName
            //    : "";

            SetupButton(Resource.Id.role_samurai, typeof(SamuraiActivity));
            SetupButton(Resource.Id.role_decker, typeof(DeckerActivity));
            SetupButton(Resource.Id.role_mage, typeof(MageActivity));
            SetupButton(Resource.Id.role_operative, typeof(OperativeActivity));

            //SetupButton(Resource.Id.role_samurai_createBtn, typeof(CharacterSheetActivity), Role.Hitter);
            //SetupButton(Resource.Id.role_decker_createBtn, () => LaunchDirectly(typeof(CharacterSheetActivity), Role.Hacker));
            //SetupButton(Resource.Id.role_mage_createBtn, () => LaunchDirectly(typeof(CharacterSheetActivity), Role.Sorceror));
            //SetupButton(Resource.Id.role_operative_createBtn, () => LaunchDirectly(typeof(CharacterSheetActivity), Role.Spy));

        }

        protected void DoOnResume()
        {
            base.OnResume();
            //Res.SFX.ResumeAll();

            //FindViewById<ImageView>(Resource.Id.role_samurai).SetImageResource(Resource.Drawable.hitter_logo);
            //Task.Delay(1000).Wait();
            //FindViewById<ImageView>(Resource.Id.role_decker).SetImageResource(Resource.Drawable.hacker_logo);
            //Task.Delay(1000).Wait();
            //FindViewById<ImageView>(Resource.Id.role_mage).SetImageResource(Resource.Drawable.sorcerer_logo);
            //Task.Delay(1000).Wait();
            //FindViewById<ImageView>(Resource.Id.role_operative).SetImageResource(Resource.Drawable.spy_logo);
            InitImageButton(Resource.Id.role_samurai, Resource.Drawable.hitter_logo, Resource.Drawable.hitter_logo_small);
            InitImageButton(Resource.Id.role_decker, Resource.Drawable.hacker_logo, Resource.Drawable.hacker_logo_small);
            InitImageButton(Resource.Id.role_mage, Resource.Drawable.sorcerer_logo, Resource.Drawable.sorcerer_logo_small);
            InitImageButton(Resource.Id.role_operative, Resource.Drawable.spy_logo, Resource.Drawable.spy_logo_small);
            FindViewById<LinearLayout>(Resource.Id.linearLayout1).RequestLayout();
        }

        protected override void OnPause()
        {
            base.OnPause();
            Res.SFX.StopAll();
        }

        protected override void OnStop()
        {
            OnFullStop.Raise();
            base.OnStop();
        }

        private void InitImageButton(int buttonID, int primaryImageID, int secondaryImageID)
        {
            bool Success = false;
            Action<int> TryToAssign = (imgID) =>
                Task.Run(() =>
                {
                    FindViewById<ImageView>(buttonID).SetImageResource(imgID);
                    Success = true;
                });
            TryToAssign(primaryImageID);
            if (Success) return;
            TryToAssign(primaryImageID); // Sometimes some memory has cleared up by this point.
            if (Success) return;
            TryToAssign(secondaryImageID);
            if (Success) return;
            while (!Success)
            {
                Task.Delay(500)
                    .ContinueWith(_ => TryToAssign(secondaryImageID));
            }
        }

        public static void InitializeAll()
        {
            SimpleStorage.SetContext(Application.Context);
            if ((Res.InteractionModes?.Count ?? 0) > 0) return; // Already done this; okay, cool.  // TODO: Use a better indicator!!!
            InteractionLibrary.InitializeAll();
            MasterSpellLibrary.LoadAll();
            MasterFechtbuch.LoadAll();

            Encounters.Scenario.Current = Encounters.Scenario.Postcard;

            //Damageable.SetUpStandardHitReactions();
        }

        //private struct RoleInformation
        //{
        //    public Role role;
        //    public Type activityType;
        //    public int launchButtonID;
        //    public int createButtonID;
        //    public int namesFieldID;
        //}

        //private static RoleInformation[] RoleInfoList = new RoleInformation[]
        //{
        //    new RoleInformation()
        //    {
        //        role = Role.Hitter,
        //        activityType = typeof(SamuraiActivity),
        //        launchButtonID = Resource.Id.role_samurai,
        //        createButtonID = Resource.Id.role_samurai_createBtn,
        //        namesFieldID = Resource.Id.role_samurai_storedNames
        //    }
        //};
    }
}

