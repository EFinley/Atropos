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

using ZXing;
using ZXing.Mobile;
using System.Threading.Tasks;

namespace Atropos
{
    [Activity]
    public abstract class RoleActivity : Activity
    {
        protected abstract void SetUpButtons();
        //private View ButtonSetupContext;

        private static int[] RoleChoicePromptIDs
            = new int[] { Resource.Id.role_interact, Resource.Id.role_choice1, Resource.Id.role_choice2, Resource.Id.role_choice3 };
        private static int[] RoleChoicePromptTextfieldIDs
            = new int[] { Resource.Id.role_interact_text, Resource.Id.role_choice1_text, Resource.Id.role_choice2_text, Resource.Id.role_choice3_text };
        private static int[] RoleChoicePromptImagefieldIDs
            = new int[] { Resource.Id.role_interact_image, Resource.Id.role_choice1_image, Resource.Id.role_choice2_image, Resource.Id.role_choice3_image };
        private List<View> RoleChoicePrompts = new List<View>();

        public static bool UseQRScanner = true;
        private MobileBarcodeScanner scanner;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the layout resource associated with this activity
            SetContentView(Resource.Layout.RolePage);

            MobileBarcodeScanner.Initialize(Application);

            SetupButton(StartScanning, "Interact", Resource.Drawable.qr_example, Color.DarkCyan);
            SetUpButtons();

            // Feedback on whether our networking info is carrying through changeover...
            if (WiFiMessageCenter.Client != null)
            {
                Toast.MakeText(this, $"Found WifiClient, {(WiFiMessageCenter.Client.IsConnected ? "connected" : "disconnected")}, with {AddressBook.Names.Join()} in the address book.", ToastLength.Long).Show();
            }
        }

        protected override void OnResume()
        {
            base.OnResume();

            if (UseQRScanner)
            {
                if (ZXing.Net.Mobile.Android.PermissionsHandler.NeedsPermissionRequest(this))
                    ZXing.Net.Mobile.Android.PermissionsHandler.RequestPermissionsAsync(this);

                //var opts = new MobileBarcodeScanningOptions()
                //{
                //    DelayBetweenContinuousScans = 2500,
                //    AutoRotate = true,
                //    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE }
                //};

                //scanner.ScanContinuously(opts, HandleScanResult);
            }
        }

        protected override void OnPause()
        {
            base.OnPause();
            if (UseQRScanner && scanner != null)
            {
                if (scanner.IsTorchOn) scanner.ToggleTorch();
                //scanner.Cancel();
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

        protected async void StartScanning()
        {
            scanner = new MobileBarcodeScanner() { UseCustomOverlay = true };

            var customOverlay = LayoutInflater.FromContext(this).Inflate(Resource.Layout.QRoverlay, null);

            customOverlay.FindViewById<ImageButton>(Resource.Id.qr_flashlight_button).Click += (ob, ev) =>
            {
                scanner.ToggleTorch();
            };
            customOverlay.FindViewById<ImageButton>(Resource.Id.qr_cancel_button).Click += (ob, ev) =>
            {
                scanner.Cancel();
            };
            
            scanner.CustomOverlay = customOverlay;

            var opts = new MobileBarcodeScanningOptions()
            { AutoRotate = true, PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE } };

            var result = await scanner.Scan(opts);

            HandleScanResult(result);
        }

        protected void HandleScanResult(ZXing.Result result)
        {
            string msg = "";

            if (result != null && !string.IsNullOrEmpty(result.Text))
                msg = "Found Barcode: " + result.Text;
            else
                msg = "Scanning Canceled!";

            this.RunOnUiThread(() => Toast.MakeText(this, msg, ToastLength.Short).Show());
        }

        protected void SetupButton(Type activity, string text, int imageSrc, Color textColour, bool isImplemented = true)
        {
            SetupButton(() =>
            {
                var intent = new Intent(Application.Context, activity);
                intent.AddFlags(ActivityFlags.SingleTop);
                intent.AddFlags(ActivityFlags.NewTask);
                StartActivity(intent);
            }, 
            text, imageSrc, textColour, isImplemented);
        }
        protected void SetupButton(Action action, string text, int imageSrc, Color textColour, bool isImplemented = true)
        {
            //if (ButtonSetupContext == null) throw new Exception("Null context - cannot set up buttons.");

            View mainV = null;
            for (int j = 0; j < RoleChoicePromptIDs.Length; j++)
            {
                if (j >= RoleChoicePrompts.Count)
                {
                    mainV = FindViewById(RoleChoicePromptIDs[j]);
                    RoleChoicePrompts.Add(mainV);
                    break;
                }

                mainV = RoleChoicePrompts[j];
                var tV = mainV.FindViewById<TextView>(RoleChoicePromptTextfieldIDs[j]);
                if (tV.Text == text) break;
            }
            if (mainV == null) throw new Exception("Can neither find nor add choice prompt!");

            //var i = RoleChoicePrompts.Count;
            var i = RoleChoicePrompts.IndexOf(mainV);
            if (i >= RoleChoicePromptIDs.Length) throw new Exception("Unable to add choice prompt - all slots full.");
            
            mainV.Visibility = ViewStates.Visible;

            TextView textV = mainV.FindViewById<TextView>(RoleChoicePromptTextfieldIDs[i]);
            SetTypeface(textV, "FTLTLT.TTF");
            textV.Text = text;
            textV.SetTextColor(textColour);

            ImageView imgV = mainV.FindViewById<ImageView>(RoleChoicePromptImagefieldIDs[i]);
            imgV.SetImageResource(imageSrc);

            if (isImplemented)
            {
                mainV.Click += (o, e) =>
                {
                    action?.Invoke();
                }; 
            }
            else
            {
                mainV.Click += (o, e) =>
                {
                    Toast.MakeText(this, Resource.String.popup_option_not_available, ToastLength.Short).Show();
                };
            }
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

        ////public List<EncounterElement> Elements = new List<EncounterElement>(3);
        //public EncounterElement[] Elements = new EncounterElement[3];
        //public void DisplayElement(EncounterElement element)
        //{
        //    // Clear the last spot on the list if necessary.
        //    if (Elements.Count(e => e != null) >= RoleChoicePrompts.Count) RemoveElement(RoleChoicePrompts.Count - 1);

        //    int newItemIndex = (Elements.Any(e => e == null)) ? Elements.ToList().IndexOf(null) : Elements.Length;
        //    Elements[newItemIndex] = element;
        //    var p = RoleChoicePrompts[newItemIndex];
        //    var text = p.FindViewById<TextView>(RoleChoicePromptTextfieldIDs[newItemIndex]);

        //    text.Text = element.ButtonLabel ?? element.Name;
        //    p.Visibility = ViewStates.Visible;
        //    element.Begin();
        //}
        //public void RemoveElement(int index = -1)
        //{
        //    if (index == -1)
        //    {
        //        int tgtCount = RoleChoicePrompts.Count;
        //        for (int i = 0; i < tgtCount; i++) RemoveElement(i);
        //    }
        //    else
        //    {
        //        Elements[index] = null;
        //        RoleChoicePrompts[index].Visibility = ViewStates.Gone;
        //    }
        //}
    }

    [Activity(Label = "Atropos :: Hitter ::")]
    public class SamuraiActivity : RoleActivity
    {
        //public override int layoutID { get; set; } = Resource.Layout.Samurai;

        protected override void SetUpButtons()
        {
            SetupButton(typeof(GunActivityRevised), "Shoot", Resource.Drawable.assault_shotgun_image, Color.MediumPurple);
            SetupButton(typeof(Machine_Learning.MeleeAlphaActivity), "Melee", Resource.Drawable.katana_image, Color.Red);
        }
    }

    [Activity(Label = "Atropos :: Sorceror ::")]
    public class MageActivity : RoleActivity
    {
        //public override int layoutID { get; set; } = Resource.Layout.Mage;

        protected override void SetUpButtons()
        {
            SetupButton(typeof(SpellCastingActivity), "Cast", Resource.Drawable.spell_casting_image, Color.Blue);
            SetupButton(typeof(GunActivityRevised), "Shoot", Resource.Drawable.handgun_image, Color.MediumPurple);
        }
    }

    [Activity(Label = "Atropos :: Hacker ::")]
    public class DeckerActivity : RoleActivity
    {
        //public override int layoutID { get; set; } = Resource.Layout.Decker;

        protected override void SetUpButtons()
        {
            SetupButton(() => { }, "Hack", Resource.Drawable.command_prompt_image, Color.Green, false);
            SetupButton(typeof(GunActivityRevised), "Shoot", Resource.Drawable.handgun_image, Color.MediumPurple);
        }
    }

    [Activity(Label = "Atropos :: Spy ::")]
    public class OperativeActivity : RoleActivity
    {
        //public override int layoutID { get; set; } = Resource.Layout.Operative;

        protected override void SetUpButtons()
        {
            SetupButton(typeof(ToolkitActivity), "Tools", Resource.Drawable.toolkit_image, Color.Silver);
            SetupButton(typeof(GunActivityRevised), "Shoot", Resource.Drawable.handgun_image, Color.MediumPurple);
        }
    }

    [Activity]
    public class ToolkitActivity : RoleActivity
    {
        //public override int layoutID { get; set; } = Resource.Layout.OperativeToolbox;

        protected override void SetUpButtons()
        {
            SetupButton(typeof(BypassActivity), "Bypass", Resource.Drawable.magnifier_image, Color.MediumSeaGreen);
            SetupButton(typeof(Locks.SafecrackingActivity), "Crack", Resource.Drawable.vault_dial_image, Color.IndianRed);
            SetupButton(typeof(Atropos.Locks.LockPickingActivity), "Pick", Resource.Drawable.lockpicks_image, Color.MediumBlue);
        }
    }

}