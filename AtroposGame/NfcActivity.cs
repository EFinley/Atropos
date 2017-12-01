
using System;
using Android;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using DeviceMotion;
using DeviceMotion.Plugin;
using DeviceMotion.Plugin.Abstractions;
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

namespace Atropos
{
    [Activity(Label = "Atropos", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class NFCActivity : Activity
    {
        private TextView _nfcText, _nfcStatus;
        private Button _writeTagButton, _selectInteractionModeButton, _launchDirectlyButton;

        private bool _inWriteMode = false;
        private Tag nfcTag;
        private NfcAdapter _nfcAdapter;
        private string _nfcTagID;

        private Res.InteractionMode selectedMode;
        private IEnumerator<Res.InteractionMode> selectedModeEnumerator;
        
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // This line still included, because looking it up proved a right pain in the butt.  To clear the SimpleStorage contents out entirely, this should do:
            // Android.Preferences.PreferenceManager.GetDefaultSharedPreferences(ApplicationContext).Edit().Clear().Apply();

            InitializeAll();

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);
            _nfcText = FindViewById<TextView>(Resource.Id.nfc_text);
            _nfcStatus = FindViewById<TextView>(Resource.Id.nfc_status);

            _nfcAdapter = NfcAdapter.GetDefaultAdapter(this);

            _writeTagButton = FindViewById<Button>(Resource.Id.write_tag_button);
            _writeTagButton.Click += WriteTagButtonOnClick;

            _selectInteractionModeButton = FindViewById<Button>(Resource.Id.select_tag_type_button);
            _selectInteractionModeButton.Click += CycleThroughInteractionModes;

            _launchDirectlyButton = FindViewById<Button>(Resource.Id.launch_directly_button);
            _launchDirectlyButton.Click += LaunchDirectly;

            selectedModeEnumerator = Res.InteractionModes.Values.GetEnumerator();
            selectedModeEnumerator.MoveNext();
            selectedMode = selectedModeEnumerator.Current;
            
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
            // App is paused, so no need to keep an eye out for NFC tags.
            if (_nfcAdapter != null)
                _nfcAdapter.DisableForegroundDispatch(this);
        }

        public static void InitializeAll()
        {
            SimpleStorage.SetContext(Application.Context);
            if ((Res.InteractionModes?.Count ?? 0) > 0) return; // Already done this; okay, cool.
            InteractionLibrary.InitializeAll();
            MasterSpellLibrary.LoadAll();
            MasterFechtbuch.LoadAll();
        }

        public void DisplayTagData()
        {
            var newGUID = Guid.NewGuid().ToString().Split('-')[0]; // We don't need this much randomness (for now), and a full GUID is hard to read.  Just use the first substring.
            _nfcTagID = string.Format("Atropos_{0}", newGUID);
            _nfcText.Text = string.Format("Tag ({0}):\nTag #{1}\nType <{2}>", Res.AtroposMimeType, _nfcTagID, selectedMode.Name);
        }

        /// <summary>
        /// This method will be called when an NFC tag is discovered by the application,
        /// as long as we've enabled 'foreground dispatch' - send it to us, don't go looking
        /// for another program to respond to the tag.  Not actually necessary for launching
        /// the interaction modes themselves; that'll happen even if they're in the background.
        /// Used here to enable the "write to tag" functionality.
        /// </summary>
        /// <param name="intent">The Intent representing the occurrence of "hey, we spotted an NFC!"</param>
        protected override void OnNewIntent(Intent intent)
        {
            if (_inWriteMode)
            {
                _inWriteMode = false;
                nfcTag = intent.GetParcelableExtra(NfcAdapter.ExtraTag) as Tag;

                if (nfcTag == null)
                {
                    return;
                }

                var nfcID = nfcTag.tagID();
                Log.Info("Main", "NFC tag ID is " + nfcID ?? "(none)");
                
                // These next few lines will create a payload (consisting of a string)
                // and a mimetype. NFC records are arrays of bytes.
                var payload = System.Text.Encoding.ASCII.GetBytes(selectedMode.Name);
                var mimeBytes = System.Text.Encoding.ASCII.GetBytes(Res.AtroposMimeType);
                var newRecord = new NdefRecord(NdefRecord.TnfMimeMedia, mimeBytes, new byte[0], payload);
                var ndefMessage = new NdefMessage(new[] { newRecord });

                if (!TryAndWriteToTag(nfcTag, ndefMessage))
                {
                    // Maybe the write couldn't happen because the tag wasn't formatted?
                    TryAndFormatTagWithMessage(nfcTag, ndefMessage);
                }
                //_nfcAdapter.DisableForegroundDispatch(this);
            }
        }

        private void DisplayMessage(string message)
        {
            _nfcText.Text = message;
            Log.Info(Res.AtroposMimeType, message);
        }

        /// <summary>
        /// Identify to Android that this activity wants to be notified when 
        /// an NFC tag is discovered. 
        /// </summary>
        private void EnableWriteMode()
        {
            _inWriteMode = true;

            // Create an intent filter for when an NFC tag is discovered.
            var tagDetected = new IntentFilter(NfcAdapter.ActionTagDiscovered);
            var filters = new[] { tagDetected };

            // When an NFC tag is detected, Android will use the PendingIntent to come back to this activity.
            // The OnNewIntent method will be invoked by Android.
            var intent = new Intent(this, this.GetType()).AddFlags(ActivityFlags.SingleTop);
            var pendingIntent = PendingIntent.GetActivity(this, 0, intent, 0);

            if (_nfcAdapter == null)
            {
                var alert = new AlertDialog.Builder(this).Create();
                alert.SetMessage("NFC is not supported on this device.");
                alert.SetTitle("NFC Unavailable");
                alert.SetButton("OK", delegate
                {
                    _writeTagButton.Enabled = false;
                    _nfcText.Text = "NFC is not supported on this device.";
                });
                alert.Show();
            }
            else
            {
                _nfcAdapter.EnableForegroundDispatch(this, pendingIntent, filters, null);
                DisplayTagData();
            }
                
        }

        private void CycleThroughInteractionModes(object sender, EventArgs args)
        {
            if (!selectedModeEnumerator.MoveNext()) { selectedModeEnumerator.Reset(); selectedModeEnumerator.MoveNext(); }
            selectedMode = selectedModeEnumerator.Current;
            FindViewById<TextView>(Resource.Id.current_mode).Text = selectedMode.PromptText;
        }

        private void LaunchDirectly(object sender, EventArgs args)
        {
            ActOnFoundTagActivity.LaunchActivity(this, selectedMode, selectedMode.Name + "0000", selectedMode.Directive);
        }

        private void WriteTagButtonOnClick(object sender, EventArgs eventArgs)
        {
            var view = (View)sender;
            if (view.Id == Resource.Id.write_tag_button)
            {
                _nfcText.Text = string.Format("Hold tag to device's NFC spot...\n Writing (as {0}):\nTag #{1}\nType <{2}>", Res.AtroposMimeType, _nfcTagID, selectedMode.Name);
                EnableWriteMode();
            }
        }

        /// <summary>
        /// This method will try and write the specified message to the provided tag. 
        /// </summary>
        /// <param name="tag">The NFC tag that was detected.</param>
        /// <param name="ndefMessage">An NDEF message to write.</param>
        /// <returns>true if the tag was written to.</returns>
        private bool TryAndWriteToTag(Tag tag, NdefMessage ndefMessage)
        {

            // This object is used to get information about the NFC tag as 
            // well as perform operations on it.
            var ndef = Ndef.Get(tag);
            if (ndef != null)
            {
                ndef.Connect();

                // Once written to, a tag can be marked as read-only - check for this.
                if (!ndef.IsWritable)
                {
                    DisplayMessage("Tag is read-only.");
                    return false;
                }

                // NFC tags can only store a small amount of data, this depends on the type of tag its.
                var size = ndefMessage.ToByteArray().Length;
                if (ndef.MaxSize < size)
                {
                    DisplayMessage(String.Format("Tag doesn't have enough space ({0} > {1})", size, ndef.MaxSize));
                }
                else
                {
                    try
                    {
                        ndef.WriteNdefMessage(ndefMessage);
                        DisplayMessage("Succesfully wrote tag.");
                        //ActOnFoundTagActivity.LaunchActivity(this, InteractionMode.GunCalibration, "0000", InteractionMode.GunCalibration.Directive);
                        return true;
                    }
                    catch (Java.IO.IOException e)
                    {
                        DisplayMessage("Error writing tag... maybe it needed formatting?\n" + e.Message);
                        return false;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to format a new tag with the message.
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="ndefMessage"></param>
        /// <returns></returns>
        private bool TryAndFormatTagWithMessage(Tag tag, NdefMessage ndefMessage)
        {
            var format = NdefFormatable.Get(tag);
            if (format == null)
            {
                if (tag.GetTechList().Any((s) => s.ToLower().Contains("ndef")))
                    DisplayMessage("Tag supports NDEF but is not formattable - not all of them are.\nYou might also retry the write.");
                else DisplayMessage("Tag does not appear to support NDEF format.");
            }
            else
            {
                try
                {
                    format.Connect();
                    format.Format(ndefMessage);
                    DisplayMessage("Tag successfully written.");
                    return true;
                }
                catch (IOException ioex)
                {
                    var msg = "There was an error trying to format the tag.";
                    DisplayMessage(msg);
                    Log.Error(Res.AtroposMimeType, ioex, msg);
                }
            }
            return false;
        }
    }
}

