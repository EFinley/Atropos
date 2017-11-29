using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Android;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;
using Android.Support.V7.App;

namespace com.Atropos
{
    [Activity(MainLauncher = true, ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait, Theme = "@style/SplashTheme", NoHistory = true)]
    public class SplashActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        protected override void OnResume()
        {
            base.OnResume();

            Task.Run(DoStartupWork);
        }

        public override void OnBackPressed() { }

        private async Task DoStartupWork()
        {
            Log.Debug("SplashscreenActivity", "Doing work in startup activity.");
            await Task.Delay(500);

            StartActivity(new Intent(Application.Context, typeof(MainActivity)));
        }
    }
}