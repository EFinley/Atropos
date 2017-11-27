using Android.App;
using Android.Widget;
using Android.OS;
using com.Atropos;

namespace Ditto
{
    [Activity(Label = "Ditto", MainLauncher = true)]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            StartActivity(typeof(com.Atropos.MainActivity));
        }
    }
}

