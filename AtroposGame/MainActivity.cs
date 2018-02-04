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
using Atropos.Characters;

namespace Atropos
{
    [Activity(Label = "Atropos", Icon = "@drawable/atropos_sigil", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class MainActivity : SelectorActivity
    {
        public override int layoutID { get; set; } = Resource.Layout.Main;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            InitializeAll();

            // TODO!  This all fails because for some reason the main panel isn't displaying the "Create" button.  Can't figure out why, so I'm punting on it for now.
            foreach (var roleInfo in RoleInfoList)
            {
                this.RotateText(roleInfo.createButtonID);
                SetTypeface(roleInfo.createButtonID, "FTLTLT.TTF");

                var namesField = FindViewById<TextView>(roleInfo.namesFieldID);

                var storedChars = CharacterStats.GetStoredByRole(roleInfo.role);
                if (storedChars.Count() == 0)
                    SetupButton(roleInfo.launchButtonID, roleInfo.activityType);
                else if (storedChars.Count() == 1)
                {
                    namesField.Text = storedChars.First().CharacterName;
                    SetupButton(roleInfo.launchButtonID, roleInfo.activityType, storedChars.First());
                }
                else
                {
                    namesField.Text = $"{storedChars.Count()} Saved";
                    SetupButton(roleInfo.launchButtonID, roleInfo.activityType, storedChars.First());
                }
            }

            //this.RotateText(Resource.Id.role_samurai_createBtn);
            //SetTypeface(Resource.Id.role_samurai_createBtn, "FTLTLT.TTF");
            //var storedHitters = CharacterStats.GetStoredByRole(Role.Hitter);
            //FindViewById<TextView>(Resource.Id.role_samurai_storedNames).Text
            //    = (storedHitters.Count() > 1) ? $"{storedHitters.Count()} Saved"
            //    : (storedHitters.Count() == 1) ? storedHitters.First().CharacterName
            //    : "";

            //SetupButton(Resource.Id.role_samurai, () => LaunchDirectly(typeof(SamuraiActivity), storedHitters));
            SetupButton(Resource.Id.role_decker, typeof(DeckerActivity));
            SetupButton(Resource.Id.role_mage, typeof(MageActivity));
            SetupButton(Resource.Id.role_operative, typeof(OperativeActivity));

            SetupButton(Resource.Id.role_samurai_createBtn, typeof(CharacterSheetActivity), Role.Hitter);
            //SetupButton(Resource.Id.role_decker_createBtn, () => LaunchDirectly(typeof(CharacterSheetActivity), Role.Hacker));
            //SetupButton(Resource.Id.role_mage_createBtn, () => LaunchDirectly(typeof(CharacterSheetActivity), Role.Sorceror));
            //SetupButton(Resource.Id.role_operative_createBtn, () => LaunchDirectly(typeof(CharacterSheetActivity), Role.Spy));

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

            Encounters.Scenario.Current = Encounters.Scenario.Postcard;
        }

        private struct RoleInformation
        {
            public Role role;
            public Type activityType;
            public int launchButtonID;
            public int createButtonID;
            public int namesFieldID;
        }

        private static RoleInformation[] RoleInfoList = new RoleInformation[]
        {
            new RoleInformation()
            {
                role = Role.Hitter,
                activityType = typeof(SamuraiActivity),
                launchButtonID = Resource.Id.role_samurai,
                createButtonID = Resource.Id.role_samurai_createBtn,
                namesFieldID = Resource.Id.role_samurai_storedNames
            }
        };
    }
}

