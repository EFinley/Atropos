using System;
using System.Text;

using Android;
using Android.App;
using Android.Nfc;
using Android.OS;
using Android.Widget;
using Android.Content;
using Android.Util;

namespace Atropos
{
    /// <summary>
    /// This activity will be used to launch the appropriate module based on the detected tag.  Note that this activity itself lasts only a very brief time!
    /// </summary>
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait),
        IntentFilter(new[] { "android.nfc.action.NDEF_DISCOVERED" },
        DataMimeType = Res.AtroposMimeType,
        Categories = new[] { "android.intent.category.DEFAULT" })]
    // NOTE - Enable/disable (how?) if the NFC box in Settings is checked.
    // ALSO NOTE - We may want two MimeTypes, one for "tag which should launch the app" and one for "tag which is only meaningful inside an activity".
    // While we're at it, things like the Security Panel should have *both* a data field for "open me with the Bypass activity" and one for "I am node X."
    public class ActOnFoundTagActivity : Activity
    {
        private TextView nfcDetailDisplay;

        protected override void OnResume()
        {
            base.OnResume();
        }

        protected override void OnPause()
        {
            base.OnPause();
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            if (!Res.AllowNewActivities) return;
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.DisplayFoundTag);
            if (Intent == null)
            {
                return;
            }
            MainActivity.InitializeAll(); // Basically a no-op IF everything's already initialized; otherwise this takes care of setting up various global required objects etc.
            ActOnIntent(Intent);
        }

        protected void ActOnIntent(Intent intent)
        { 
            // Housework and setup
            nfcDetailDisplay = FindViewById<TextView>(Resource.Id.tag_details);
            var button = FindViewById<Button>(Resource.Id.cancel_btn);
            button.Click += (sender, args) => Finish();

            // Process the actual tag contents...

            // Verify it to be one of ours[which it bloody ought to be, given the Intent's mimetype constraint]
            //if (intent.Type != Res.AtroposMimeType) return;

            // Parse everything found
            InteractionLibrary.CurrentTagHandle = intent.GetParcelableExtra(NfcAdapter.ExtraTag) as Tag;
            var rawMessages = intent.GetParcelableArrayExtra(NfcAdapter.ExtraNdefMessages);
            var msg = (NdefMessage)rawMessages[0];
            var nfcRecordBody = msg.GetRecords();
            string _nfcTagID = InteractionLibrary.CurrentTagHandle.tagID();
            string _nfcTagInteractionMode = Encoding.ASCII.GetString(nfcRecordBody[0].GetPayload());
            string feedbackText = string.Format("Tag ({0}):\n{1}\nLaunch <{2}>", Res.AtroposMimeType, _nfcTagID, _nfcTagInteractionMode);
            nfcDetailDisplay.Text = feedbackText;


            // Debugging - prevent the tag from triggering the activity (for now).
            Finish();
            return; // Not sure this is necessary but just in case.

            // Check to see if we have a matching InteractionMode in our resources library
            if (Res.InteractionModes.ContainsKey(_nfcTagInteractionMode))
            {
                Log.Info("TagFound", feedbackText);
                if (InteractionLibrary.Current?.Name == _nfcTagInteractionMode)
                {
                    Log.Debug("ActOnFoundTag", $"Discovered a tag for {_nfcTagInteractionMode} but we're already in that mode; ignoring.");
                    return;
                }

                Res.InteractionMode t = Res.InteractionModes[_nfcTagInteractionMode];

                LaunchActivity(this, t, _nfcTagID);
            }
            else
            {
                Log.Info("TagFound", $"Looked for but did not find the following:\n {feedbackText}");
                Log.Info("TagFound", $"In the tag dictionary, which contains [{Res.InteractionModes.Keys.Join()}]");
            }

            

            Finish();
        }

        public static void LaunchActivity(Context ctx, Res.InteractionMode interactionMode, string tagID, string ExtraDirective = "")
        {
            Bundle b = new Bundle();
            b.PutString(Res.bundleKey_tagID, tagID);
            b.PutString(Res.bundleKey_Directive, interactionMode.Directive + ExtraDirective); // Used to communicate any special needs or requests to the launched Activity.
            // Note that bundle-passing is currently busted.  Instead, use the following:

            InteractionLibrary.CurrentSpecificTag = tagID;
            InteractionLibrary.Current = interactionMode;

            var intent = new Intent(ctx, interactionMode.Launches.GetType());
            intent.AddFlags(interactionMode.Flags);
            try
            {
                ctx.StartActivity(intent, b);
            }
            catch
            {
                intent.AddFlags(ActivityFlags.NewTask);
                ctx.StartActivity(intent, b);
            }
        }
        
    }
}