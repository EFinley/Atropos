using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using Atropos.Machine_Learning;
using Android.Graphics;
using MiscUtil;
using System.Threading;
using Nito.AsyncEx;
using Android.Hardware;
using ZXing.Mobile;
using ZXing;
using Android.Nfc;

namespace Atropos
{
    [Activity(Label = "Atropos :: Functionality Testing")]
    public class FunctionalityTestActivity : Activity
    {
        protected List<string> CategoryNames = new List<string>();
        protected List<List<string>> ItemNames = new List<List<string>>();
        protected List<List<Func<Task>>> ItemActions = new List<List<Func<Task>>>();

        protected Spinner spinner;
        protected ListView listView;
        protected ISpinnerAdapter categoryAdapter;
        protected IListAdapter itemsAdapter;
        protected TextView resultView;
        protected Button stopButton, problemButton;

        protected int CurrentCategory = 0;
        protected int CurrentItem = -1;

        protected CancellationTokenSource cts;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.TestSuite);

            spinner = FindViewById<Spinner>(Resource.Id.spinner);
            listView = FindViewById<ListView>(Resource.Id.list);
            resultView = FindViewById<TextView>(Resource.Id.info);
            stopButton = FindViewById<Button>(Resource.Id.stop_btn);
            problemButton = FindViewById<Button>(Resource.Id.problem_btn);

            CreateSFXTests();
            CreateSpeechTests();
            CreateSensorTests();
            CreateMiscTests();

            problemButton.Click += ProblemButton_Click;

            categoryAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, CategoryNames);
            spinner.ItemSelected += Spinner_ItemSelected;
            spinner.Adapter = categoryAdapter;
            spinner.SetSelection(0);
            listView.ItemClick += ListView_ItemClick;
        }

        private async void ListView_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            try
            {
                CurrentItem = e.Position;
                var task = ItemActions[CurrentCategory][CurrentItem];
                if (stopButton.Enabled) stopButton.CallOnClick();
                await task?.Invoke();
            }
            catch (Exception ex)
            {
                UpdateInfo($"Exception: {ex.ToString()}");
                problemButton.CallOnClick();
            }
        }

        private void Spinner_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
        {
            CurrentCategory = e.Position;
            itemsAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleListItem1, ItemNames.ElementAt(CurrentCategory));
            listView.Adapter = itemsAdapter;
        }

        private void ProblemButton_Click(object sender, EventArgs e)
        {
            var ClipboardMgr = (ClipboardManager)GetSystemService(Service.ClipboardService);
            var result = $"Functionality Test result ({CategoryNames[CurrentCategory]}|{ItemNames[CurrentCategory].ElementAtOrDefault(CurrentItem)}):\n{resultView.Text}";
            var myClip = ClipData.NewPlainText("AtroposSavedDataset", result);
            ClipboardMgr.PrimaryClip = myClip;
            Toast.MakeText(this, "Info window contents saved to clipboard.", ToastLength.Short).Show();
        }

        protected void CreateTest(string testName, Func<Task> testAction)
        {
            CurrentCategory = CategoryNames.Count - 1;
            CreateTest(CurrentCategory, testName, testAction);
        }
        protected void CreateTest(int categoryIndex, string testName, Func<Task> testAction)
        {
            if (ItemNames.ElementAtOrDefault(categoryIndex) == null)
            {
                ItemNames.Add(new List<string>());
                ItemActions.Add(new List<Func<Task>>());
            }
            ItemNames[categoryIndex].Add(testName);
            ItemActions[categoryIndex].Add(testAction);
        }

        protected void UpdateInfo(string info)
        {
            RunOnUiThread(() => { resultView.Text = info; });
        }

        protected void CreateSFXTests()
        {
            CategoryNames.Add("Sound FX");
            CreateTest("Simple", () => PlaySound(Resource.Raw._175949_clash_t, SoundOptions.Default));
            CreateTest("OnHeadphones", () => PlaySound(Resource.Raw._175949_clash_t, SoundOptions.OnHeadphones));
            CreateTest("OnSpeaker", () => PlaySound(Resource.Raw._175949_clash_t, SoundOptions.OnSpeakers));
            CreateTest("Softly", () => PlaySound(Resource.Raw._175949_clash_t, new SoundOptions() { Volume = 0.15 }));
            CreateTest("Looping (10s)", async () =>
            {
                UpdateInfo("Testing...");
                var eff = new Effect("Looping", Resource.Raw._315850_femaleCSharpLoop);
                eff.Play(playLooping: true);
                await Task.Delay(10000).ContinueWith(_ => eff.Stop());
                UpdateInfo("Test complete.");
            });
            CreateTest("Overlapping", async () =>
            {
                var eff1 = new Effect("CSharp", Resource.Raw._315850_femaleCSharpLoop) { Volume = 0.3 };
                var eff2 = new Effect("Clash", Resource.Raw._175949_clash_t);

                UpdateInfo("Testing...");
                await Task.WhenAll(eff1.PlayToCompletion(), Task.Delay(250).ContinueWith(_ => eff2.PlayToCompletion()));
                UpdateInfo("Test complete.");
            });
            CreateTest("Volume Shifting", async () =>
            {
                var eff = new Effect("Aura", Resource.Raw.aura_magic) { Looping = true };
                eff.Play();
                var Intervals = new int[] { 500, 400, 250, 150, 100, 80, 70, 60, 50, 40, 35, 30, 25, 20, 15, 10 };
                //var Volumes = new double[] { 1.0, 0.9, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3, 0.2, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9 };
                foreach (var Interval in Intervals)
                {
                    UpdateInfo($"Changing volume subtly every {Interval} milliseconds.");
                    int numReps = (int)(Math.Round(1000.0 / Interval));
                    for (int i = 0; i < numReps; i++)
                    {
                        eff.Volume = 0.75 - (((double)i / (numReps - 1)) - 0.5)*(((double)i / (numReps - 1)) - 0.5);
                        await Task.Delay(Interval);
                    }
                }
                eff.Stop();
                UpdateInfo("Test complete, 500ms through 10ms.");
            });
            CreateTest("Pitch Increase*", () => PlaySound(Resource.Raw._175949_clash_t, new SoundOptions() { Pitch = 4.0 }));
            CreateTest("Pitch Decrease*", () => PlaySound(Resource.Raw._175949_clash_t, new SoundOptions() { Pitch = 0.25 }));
            CreateTest("Rate Increase*", () => PlaySound(Resource.Raw._175949_clash_t, new SoundOptions() { Speed = 2.0 }));
            CreateTest("Rate Decrease*", () => PlaySound(Resource.Raw._175949_clash_t, new SoundOptions() { Speed = 0.5 }));
            CreateTest("Interrupt (Timed)", async () =>
            {
                var eff = new Effect("strongerThanTheDark", Resource.Raw._316626_strongerThanTheDark_needsTrim);
                cts = new CancellationTokenSource();
                eff.Play(new SoundOptions() { CancelToken = cts.Token });
                UpdateInfo("Testing...");
                await Task.Delay(1500);
                cts.Cancel();
                UpdateInfo("Interrupt token sent. Sound should stop; test continues.");
                await Task.Delay(6000);
                eff.Stop();
                UpdateInfo("Test complete.");
                cts = null;
            });
            CreateTest("Interrupt (Manual)", async () =>
            {
                var eff = new Effect("strongerThanTheDark", Resource.Raw._316626_strongerThanTheDark_needsTrim) { Looping = true };
                var token = SetupStopButtonOneShot("Interrupt token sent. Sound should stop; test continues.");

                UpdateInfo("Testing...");
                eff.Play(new SoundOptions() { CancelToken = token });
                await Task.Delay(7500);
                eff.Stop();
                UpdateInfo("Test complete.");
            });
        }

        protected async Task PlaySound(int resID, SoundOptions options)
        {
            var eff = new Effect($"Res{resID}", resID);
            UpdateInfo("Testing...");
            await eff.PlayToCompletion(options);
            UpdateInfo("Test complete.");
        }

        protected void CreateSpeechTests()
        {
            CategoryNames.Add("Speech Tests");
            CreateTest("Simple", () => PlaySpeech("Some simple speech.", SoundOptions.Default));
            CreateTest("OnHeadphones", () => PlaySpeech("Some simple speech.", SoundOptions.OnHeadphones));
            CreateTest("OnSpeaker", () => PlaySpeech("Some simple speech.", SoundOptions.OnSpeakers));
            CreateTest("Softer", () => PlaySpeech("Not exactly whispering, but as close as we get.", new SoundOptions() { Volume = 0.2 }));
            CreateTest("Plus SFX", async () =>
            {
                UpdateInfo("Testing...");
                await Task.WhenAll(Speech.SayAllOf("This is a clash; we'll use it in a parry cue in melee."),
                    Task.Delay(500).ContinueWith(_ => { var eff = new Effect("Clash", Resource.Raw._175949_clash_t); return eff.PlayToCompletion(); }));
                UpdateInfo("Test complete.");
            });
            CreateTest("Overlapping*", async () =>
            {
                UpdateInfo("Testing");
                await Task.WhenAll(Speech.SayAllOf("This is stream number one."), Speech.SayAllOf("This is stream number two."));
                UpdateInfo("Testing complete.");
            });
            CreateTest("Pitch increase", () => PlaySpeech("Scaramouche, scaramouche, will you do the fandango?", new SoundOptions() { Pitch = 4.0 }));
            CreateTest("Pitch decrease", () => PlaySpeech("Figaro. Figaro figaro figaro.", new SoundOptions() { Pitch = 0.25 }));
            CreateTest("Rate increase", () => PlaySpeech("How much wood can a woodchuck chuck?", new SoundOptions() { Speed = 2.0 }));
            CreateTest("Rate decrease", () => PlaySpeech("Hobbits can be very hasty. Very hasty indeed.", new SoundOptions() { Speed = 0.35 }));
            CreateTest("Interrupt (Timed)", async () =>
            {
                cts = new CancellationTokenSource();
                UpdateInfo("Testing...");
                var speechTask = Speech.SayAllOf("This sentence should end in silence before it reaches its end.  Don't fret; this test is expected to fail.", new SoundOptions() { CancelToken = cts.Token })
                                       .SwallowCancellations();
                await Task.WhenAny(
                    speechTask,
                    Task.Delay(1500));
                UpdateInfo("Interrupt token sent. Ideally, speech should stop; test continues.");
                cts.Cancel();
                await speechTask;
                await Task.Delay(500);
                UpdateInfo("Test complete.");
                cts = null;
            });
            CreateTest("Interrupt (Manual)", async () =>
            {
                var token = SetupStopButtonOneShot("Interrupt token sent. Sound should stop; test continues.");

                UpdateInfo("Testing...");
                var speechString = "This is a test of the interrupt mechanism.  This is only a test.  If this were a real interrupt, I would have stopped speaking by now, assuming you hit the stop button by this point.  If not, you should, quick, before I'm done. I'm done.";
                await Speech.SayAllOf(speechString, new SoundOptions() { CancelToken = token }).SwallowCancellations();
                UpdateInfo("Test complete.");
            });
        }

        protected CancellationToken SetupStopButtonOneShot(string infoOnButtonPress)
        {
            cts = new CancellationTokenSource();
            EventHandler stopEventHandler = null;
            stopEventHandler = (o, e) =>
            {
                if (cts == null) return;
                cts.Cancel();
                stopButton.Click -= stopEventHandler;
                //RunOnUiThread(() =>
                //{
                stopButton.Enabled = false;
                UpdateInfo(infoOnButtonPress);
                //});
                cts = null;
            };
            stopButton.Click += stopEventHandler;
            //RunOnUiThread(() => { stopButton.Enabled = true; });
            stopButton.Enabled = true;

            return cts.Token;
        }

        protected async Task PlaySpeech(string content, SoundOptions options)
        {
            UpdateInfo("Testing...");
            await Speech.SayAllOf(content, options);
            UpdateInfo("Test complete.");
        }

        protected void CreateSensorTests()
        {
            CategoryNames.Add("Sensor Tests");
            CreateTest("Accelerometer", () => RunSensor(SensorType.Accelerometer));
            CreateTest("Linear Accel", () => RunSensor(SensorType.LinearAcceleration));
            CreateTest("Gravity", () => RunSensor(SensorType.Gravity));
            CreateTest("Gyroscope", () => RunSensor(SensorType.Gyroscope));
            CreateTest("Compass", () => RunSensor(SensorType.MagneticField));
            CreateTest("Rotation Vector", () => RunSensor(SensorType.RotationVector));
            CreateTest("Game Rot. Vector", () => RunSensor(SensorType.GameRotationVector));
            CreateTest("Orientation (Grav)", () => RunSensor(new GravityOrientationProvider(), SetupStopButtonOneShot("Sensor stopped."), "Grav Orientation"));
        }

        #region Lists of which types of sensors ought to provide which data types (used for RunSensor)
        private SensorType[] OrientationSensorTypes = new SensorType[]
        {
            SensorType.GameRotationVector, SensorType.GeomagneticRotationVector, SensorType.RotationVector
        };
        private SensorType[] VectorSensorTypes = new SensorType[]
        {
            SensorType.Accelerometer, SensorType.Gravity, SensorType.LinearAcceleration,
            SensorType.Gyroscope, SensorType.GyroscopeUncalibrated,
            SensorType.MagneticField, SensorType.MagneticFieldUncalibrated
        };
        #endregion
        protected async Task RunSensor(SensorType sensorType)
        {
            var sensorName = Enum.GetName(typeof(SensorType), sensorType);
            UpdateInfo($"Testing sensor {sensorName}...");

            var token = SetupStopButtonOneShot("Sensor stopped.");
            IProvider provider = GetProvider(sensorType);
            await RunSensor(provider, token, sensorName);
        }
        protected async Task RunSensor(IProvider provider, CancellationToken token, string sensorName)
        { 
            provider.Activate(token);

            while (!token.IsCancellationRequested)
            {
                await Task.WhenAll(provider.WhenDataReady(), Task.Delay(250));
                var d = provider.GetType().GetProperty("Data").GetValue(provider);
                var l = d.GetType().GetMethod("Length")?.Invoke(d, new object[0]);
                UpdateInfo($"{sensorName} value: {d:f3} (magnitude {l:f3})");
            }
        }
        
        protected IProvider GetProvider(SensorType sensorType)
        {
            if (sensorType.IsOneOf(VectorSensorTypes)) return (IProvider)new Vector3Provider(sensorType);
            else if (sensorType.IsOneOf(OrientationSensorTypes)) return (IProvider)new OrientationSensorProvider(sensorType);
            else throw new ArgumentException($"Sensor type {Enum.GetName(typeof(SensorType), sensorType)} not recognized as either Vector or Quaternion type.");
        }

        protected void CreateMiscTests()
        {
            CategoryNames.Add("Miscellaneous Tests");
            CreateTest("QR scanner", async () =>
            {
                UpdateInfo("Testing...");
                MobileBarcodeScanner.Initialize(Application);
                var scanner = new MobileBarcodeScanner() { UseCustomOverlay = true };

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

                string msg = "";

                if (result != null && !string.IsNullOrEmpty(result.Text))
                    msg = "Found QR code: " + result.Text;
                else
                    msg = "Scanning Canceled!";

                this.RunOnUiThread(() => Toast.MakeText(this, msg, ToastLength.Short).Show());
                UpdateInfo($"Test complete. QR code: {result.Text ?? "(Scanning canceled by user)"}");
            });
            CreateTest("NFC Capability", async () =>
            {
                UpdateInfo("Testing...");
                var _nfcAdapter = NfcAdapter.GetDefaultAdapter(this);
                if (_nfcAdapter == null)
                {
                    UpdateInfo("Test failed: this device doesn't seem to have NFC capability.");
                    return;
                }

                signalFlag = new AsyncManualResetEvent();

                // Create an intent filter for when an NFC tag is discovered.
                var tagDetected = new IntentFilter(NfcAdapter.ActionTagDiscovered);
                var filters = new[] { tagDetected };

                // When an NFC tag is detected, Android will use the PendingIntent to come back to this activity.
                // The OnNewIntent method will be invoked by Android.
                var intent = new Intent(this, this.GetType()).AddFlags(ActivityFlags.SingleTop);
                var pendingIntent = PendingIntent.GetActivity(this, 0, intent, 0);

                Res.AllowNewActivities = false;
                _nfcAdapter.EnableForegroundDispatch(this, pendingIntent, filters, null);

                var token = SetupStopButtonOneShot("Test ended.");
                await Task.WhenAny(token.AsTask(), signalFlag.WaitAsync());
                if (!token.IsCancellationRequested)
                {
                    var text = resultView.Text;
                    stopButton.CallOnClick();
                    while (cts != null) await Task.Delay(25);
                    UpdateInfo(text); 
                }
            });
            CreateTest("Vibrate", async () =>
            {
                UpdateInfo("Testing...");
                Plugin.Vibrate.CrossVibrate.Current.Vibration(250);
                await Task.Delay(300);
                UpdateInfo("Test complete.");
            });
        }

        /// <summary>
        /// This method will be called when an NFC tag is discovered by the application,
        /// as long as we've enabled 'foreground dispatch' - send it to us, don't go looking
        /// for another program to respond to the tag.
        /// </summary>
        /// <param name="intent">The Intent representing the occurrence of "hey, we spotted an NFC!"</param>
        protected override void OnNewIntent(Intent intent)
        {
            var nfcTag = intent.GetParcelableExtra(NfcAdapter.ExtraTag) as Tag;
            if (nfcTag == null) return; // Must have been some other Intent chancing along at the wrong time?
            var nfcID = nfcTag.tagID();

            var rawMessages = intent.GetParcelableArrayExtra(NfcAdapter.ExtraNdefMessages);
            var msg = (NdefMessage)rawMessages[0];
            var nfcRecordBody = msg.GetRecords();
            string nfcContents = Encoding.ASCII.GetString(nfcRecordBody[0].GetPayload());

            var resultString = $"NFC tag found, MIME type {intent.Type}, tag ID {nfcID}, contents {nfcContents}.";
            RunOnUiThread(() => UpdateInfo(resultString));
            Toast.MakeText(this, resultString, ToastLength.Short).Show();

            signalFlag?.Set();
        }
        protected AsyncManualResetEvent signalFlag;
    }
}