//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;

//using Android.App;
//using Android.Content;
//using Android.OS;
//using Android.Runtime;
//using Android.Views;
//using Android.Widget;
//using Android.Util;

//using Plugin.BLE;
//using Plugin.BLE.Android;
//using Plugin.BLE.Abstractions;
//using BLE = Plugin.BLE.Abstractions.Contracts;
//using Android.Bluetooth;
//using System.Threading;
//using System.Threading.Tasks;
//using Nito.AsyncEx;

//using ZXing;
//using ZXing.Mobile;
//using Java.Net;

//namespace Atropos.Communications
//{
    
//    public class BLEPeer : Java.Lang.Object
//    {
//        public Guid Guid;
//        public BLE.IDevice Device;
//        public BLE.ICharacteristic MessagePipe;
//        public string BTstatus { get => Device?.State.ToString(); }
//    }

//    [Activity]
//    public class BLECommsActivity : Activity
//    {
//        public static BLECommsActivity Current { get; set; }
//        private const string _tag = "BLE_Comms";
//        private const string _prefix = "Atropos|BLE|";
//        private const string _GuidStorageKey = "MyBluetoothGUID";

//        private BLE.IBluetoothLE BLEradio { get; set; }
//        private BLE.IAdapter BLEadapter { get; set; }
//        private BleServer BLEserver { get; set; }
//        private string MyGuid { get; set; }

//        public List<CommsContact> PeersList { get; set; } = new List<CommsContact>();
//        public BTPeerListAdapter PeersListAdapter;
//        public CommsContact PeerByGuid(Guid guid) { return PeersList.FirstOrDefault(p => p.BtPeer.Guid == guid); }

//        #region View-Model saved fields
//        protected ImageView qrImage;
//        protected ListView teamList;
//        protected View scanButton, promptRegion;
//        protected EditText macInputField;
//        protected TextView macPromptField;

//        private void FindAllFields()
//        {
//            qrImage = FindViewById<ImageView>(Resource.Id.bluetooth_QRimage);
//            teamList = FindViewById<ListView>(Resource.Id.list);
//            scanButton = FindViewById(Resource.Id.bluetooth_scanBtn);
//            promptRegion = FindViewById(Resource.Id.bluetooth_promptRegion);
//            macInputField = FindViewById<EditText>(Resource.Id.bluetooth_macEntryField);
//            macPromptField = FindViewById<TextView>(Resource.Id.bluetooth_promptInstructions);
//        }
//        #endregion

//        protected override void OnCreate(Bundle savedInstanceState)
//        {
//            base.OnCreate(savedInstanceState);
//            Current = this;
//            SetContentView(Resource.Layout.TeamFormation);
//            MobileBarcodeScanner.Initialize(Application);

//            BLEradio = CrossBluetoothLE.Current;
//            BLEadapter = BLEradio.Adapter;

//            var state = BLEradio.State;
//            Log.Debug(_tag, $"Current BLE radio status is {state}.");

//            FindAllFields();
//            SetUpButtons();
//            RefreshList();

//            DisplayOrPromptForGUID();

//            // Set up server
//            BLEserver = new BleServer(this);

//        }

//        protected override void OnResume()
//        {
//            base.OnResume();
//            this.HideKeyboard();
//        }

//        protected override void OnPause()
//        {
//            base.OnPause();
//            BLEserver.Close();
//        }

//        private void SetUpButtons()
//        {
//            scanButton.Click += async (o, e) =>
//            {
//                var scanner = new MobileBarcodeScanner() { UseCustomOverlay = true };
//                var customOverlay = LayoutInflater.FromContext(this).Inflate(Resource.Layout.QRoverlay, null);

//                customOverlay.FindViewById<ImageButton>(Resource.Id.qr_flashlight_button).Click += (ob, ev) =>
//                {
//                    scanner.ToggleTorch();
//                };
//                customOverlay.FindViewById<ImageButton>(Resource.Id.qr_cancel_button).Click += (ob, ev) =>
//                {
//                    scanner.Cancel();
//                };

//                scanner.CustomOverlay = customOverlay;

//                var opts = new MobileBarcodeScanningOptions()
//                { AutoRotate = true, PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE } };

//                var result = await scanner.Scan(opts);

//                if (result == null || string.IsNullOrEmpty(result.Text)) return;

//                var msg = result.Text;

//                if (!msg.StartsWith(_prefix) || !Guid.TryParse(msg.Substring(_prefix.Length), out Guid targetGuid))
//                {
//                    Toast.MakeText(this, $"Scan the QR code showing on your friend's screen.", ToastLength.Short).Show();
//                    return;
//                }

//                if (PeersList.Any(p => p.BtPeer.Guid == targetGuid))
//                {
//                    Toast.MakeText(this, $"Device {targetGuid} is already on list.", ToastLength.Short).Show();
//                    return;
//                }

//                var peer = new CommsContact { Name = targetGuid.ToString().Substring(ZeroFillPrefix.Length), BtPeer = new BLEPeer() { Guid = targetGuid } };
//                PeersList.Add(peer);
//                RefreshList();

//                await AttemptConnection(peer);
//                if (peer.BtPeer.Device?.State == DeviceState.Connected)
//                {
//                    Log.Debug(_tag, $"Successful connection.  Sending connect-back string.");
//                    peer.SendMessage($"ConnectMe|{MyGuid}");
//                }
//            };

//            qrImage.LongClick += (o, e) =>
//            {
//                Res.Storage.Delete(_GuidStorageKey);
//                DisplayOrPromptForGUID();
//            };
//        }

//        private string ZeroFillPrefix = "00000000-0000-0000-0000-";

//        private void DisplayOrPromptForGUID()
//        {
//            string StupidDefaultMACaddress = ZeroFillPrefix + "020000000000";
//            MyGuid = ZeroFillPrefix + BluetoothAdapter.DefaultAdapter.Address.Replace(":","");
//            Log.Debug(_tag, $"Retrieving DefaultAdapter.Address gets us {MyGuid}");

//            if (MyGuid.EndsWith("00") || MyGuid == StupidDefaultMACaddress) // TODO: Replace with matching the 02:00:00 constant (or at least the part traiilng the two)
//                MyGuid = Res.Storage.Get<string>(_GuidStorageKey) ?? StupidDefaultMACaddress;

//            var barcodeWriter = new ZXing.Mobile.BarcodeWriter
//            {
//                Format = ZXing.BarcodeFormat.QR_CODE,
//                Options = new ZXing.Common.EncodingOptions { Width = 300, Height = 300 }
//            };
//            var barcode = barcodeWriter.Write(_prefix + MyGuid.ToString());
//            qrImage.SetImageBitmap(barcode);

//            if (MyGuid != StupidDefaultMACaddress)
//            {
//                qrImage.Alpha = 1.0f;
//                promptRegion.Visibility = ViewStates.Gone;
//            }
//            else
//            {
//                qrImage.Alpha = 0.1f;
//                promptRegion.Visibility = ViewStates.Visible;
//                macPromptField.Text = "For privacy reasons, apps cannot find their own MAC address under current OS versions. " +
//                        "To enable scan-to-connect, you will need to either (a) scan to connect to someone else running Atropos, or " +
//                        "(b) manually enter your Bluetooth adapter's MAC address here.  It can be found under Settings -> Connected Devices -> Bluetooth under 8.1 and above.";
//                macInputField.TextChanged += (o, e) =>
//                {
//                    var justText = e.Text.ToString().Replace(":", "").Replace("-", "").Replace(" ", "").ToLower();
//                    if (justText.Length != 12) return;
//                    Res.Storage.Put<string>(_GuidStorageKey, ZeroFillPrefix + justText);
//                    RunOnUiThread(DisplayOrPromptForGUID);
//                };
//            }
//        }

//        private async Task AttemptConnection(CommsContact peer)
//        {
//            await Task.Run(async () =>
//            {
//                BLEadapter.ScanTimeout = 10000; // Both of these are probably not getting used at all, but they don't hurt.
//                BLEadapter.ScanTimeoutElapsed += (o, e) => { Log.Debug(_tag, "Scan timeout elapsed."); };

//                BLE.IDevice device;
//                try
//                {
//                    var timeout = Task.Delay(30000);
//                    device = await BLEadapter
//                        .ConnectToKnownDeviceAsync(peer.BtPeer.Guid,
//                                new ConnectParameters(true),
//                                cancellationToken: CancellationTokenHelpers.FromTask(timeout).Token)
//                        .SwallowCancellations();
//                    if (timeout.Status == TaskStatus.RanToCompletion)
//                    {
//                        Log.Debug(_tag, "Connection attempt timed out.");
//                        return;
//                    }
//                    Log.Debug(_tag, $"Connection attempt to {device.Id} complete - status is {device.State}");
//                }
//                catch (Exception ex)
//                {
//                    Log.Debug(_tag, $"Error connecting to known device: {ex}");
//                    return;
//                }

//                //var peer = PeerByGuid(target);
//                peer.Name = device.Name; // Temporary, until we retrieve the Name characteristic.
//                peer.BtPeer.Device = device;

//                try
//                {
//                    Log.Debug(_tag, $"Found a BLE peer, {device.Name} (as {device.Id})");
//                    var service = await device.GetServiceAsync(BleServer.ServiceGUID);
//                    Log.Debug(_tag, $"It shows our service ({BleServer.ServiceUUID.ToString()})");

//                    // Retrieve all of our various read-only (well, read-and-notify-when-updated) characteristics for this peer.
//                    foreach (var ctristic in peer.Characteristics.Values)
//                    {
//                        var ctric = await service.GetCharacteristicAsync(ctristic.GUID);
//                        ctristic.characteristic = ctric;
//                        await ctric.ReadAsync();
//                        ctristic.Parse(ctric.StringValue);
//                        Log.Debug(_tag, $"Found characteristic {ctristic.CharacteristicName}, with value {ctristic.StringValue}.");

//                        ctric.ValueUpdated += (o, e) =>
//                        {
//                            ctristic.Parse(e.Characteristic.StringValue);
//                        };
//                        await ctric.StartUpdatesAsync();
//                    }

//                    // And lastly the one write-only characteristic, kept separate because it really belongs to the server side of things...
//                    peer.BtPeer.MessagePipe = await service.GetCharacteristicAsync(BleServer.MessagesGUID);

                    
//                }
//                catch (Plugin.BLE.Abstractions.Exceptions.DeviceConnectionException ex)
//                {
//                    Log.Debug(_tag, $"DeviceConnectionException when fetching services/characteristics: {ex.DeviceName}/{ex.DeviceId} : {ex.Message}");
//                    //HandleFailedConnection(peer.Device.Id);
//                }
//                catch (Exception ex)
//                {
//                    Log.Debug(_tag, $"Error retrieving services/characteristics from device: {ex}");
//                    //HandleFailedConnection(peer.Device.Id);
//                }
//            });
//        }

//        private void RefreshList()
//        {
//            PeersListAdapter = new BTPeerListAdapter(this, Resource.Layout.TeamFormationTeammateItem, PeersList);
//            teamList.Adapter = PeersListAdapter;
//        }

//        public override bool OnCreateOptionsMenu(IMenu menu)
//        {
//            var inflater = MenuInflater;
//            inflater.Inflate(Resource.Menu.WiFi_action_items, menu);
//            return true;
//        }

//        public override bool OnOptionsItemSelected(IMenuItem item)
//        {
//            switch (item.ItemId)
//            {
//                case Resource.Id.atn_direct_enable:
//                    if (BLEradio != null)
//                    {
//                        var bondedDevices = BLEadapter.GetSystemConnectedOrPairedDevices();
//                        Task.Run(async () =>
//                        {
//                            Log.Debug(_tag, $"Found {bondedDevices.Count} bonded or paired devices.");
//                            foreach (var device in bondedDevices)
//                            {
//                                Log.Debug(_tag, $"Bonded device found {device.Name}/{device.Id}.");
//                                await BLEadapter.ConnectToDeviceAsync(device);
//                                Log.Debug(_tag, $"Connection attempted.  Status: {device.State}");
//                                var serv = await device.GetServicesAsync();
//                                Log.Debug(_tag, $"It has {serv.Count} services.  Ours included ({serv.Any(s => s.Id == BleServer.ServiceGUID)}).");
//                            }
//                        });
                            

//                        // Since this is the system bluetooth settings activity, it's
//                        // not going to send us a result. We will be notified by
//                        // WiFiDeviceBroadcastReceiver instead.

//                        Toast.MakeText(this, "Sending you to Bluetooth Settings.", ToastLength.Long).Show();
//                        StartActivityForResult(new Intent(BluetoothAdapter.ActionRequestEnable), 666);
//                    }
//                    else
//                    {
//                        Log.Error(_tag, "BLE radio is null");
//                    }
//                    return true;
//                case Resource.Id.atn_direct_discover:
//                    BLEadapter.DeviceDiscovered += OnDeviceDiscovered;
//                    Task.Run(async () =>
//                    {
//                        BLEadapter.ScanTimeout = 30000;
//                        BLEadapter.ScanTimeoutElapsed += (o, e) => { Log.Debug(_tag, "Scan timeout elapsed."); };
//                        //await BLEadapter.StartScanningForDevicesAsync();
//                        //await BLEadapter.StartScanningForDevicesAsync(new Guid[] { BleServer.ServerGUID });
//                        Plugin.BLE.Abstractions.Contracts.IDevice device = null;
//                        //var signal = new AsyncAutoResetEvent();
//                        try
//                        {
//                            //RunOnUiThread(async () =>
//                            //{
//                                var timeout = Task.Delay(5000);
//                                var tryForNexus5 = BLEadapter.ConnectToKnownDeviceAsync(
//                                    Guid.Parse("00000000-0000-0000-0000-78f88214bec8"),
//                                    new Plugin.BLE.Abstractions.ConnectParameters(false),
//                                    CancellationTokenHelpers.FromTask(timeout).Token);
//                                var tryForNexus4 = BLEadapter.ConnectToKnownDeviceAsync(
//                                    Guid.Parse("00000000-0000-0000-0000-58a2b55f3e5d"),
//                                    new Plugin.BLE.Abstractions.ConnectParameters(false),
//                                    CancellationTokenHelpers.FromTask(timeout).Token);
//                                device = await Task.WhenAny(tryForNexus4, tryForNexus5).Result;
//                                if (timeout.Status == TaskStatus.RanToCompletion)
//                                {
//                                    Log.Debug(_tag, "Connection attempt timed out.");
//                                    return;
//                                }
//                                //signal.Set();
//                                Log.Debug(_tag, $"Connection attempt to {device.Id} complete - status is {device.State}");
//                            //});
//                        }
//                        catch (TaskCanceledException)
//                        {
//                            Log.Debug(_tag, $"Connection attempt timed out.");
//                            return;
//                        }
//                        catch (Exception ex)
//                        {
//                            Log.Debug(_tag, $"Error connecting to known device: {ex}");
//                            return;
//                        }


//                        try
//                        {
//                            //await signal.WaitAsync();
//                            Log.Debug(_tag, $"Found a BLE peer, {device.Name} (as {device.Id})");
//                            var service = await device.GetServiceAsync(BleServer.ServiceGUID);
//                            if (service == null)
//                            {
//                                var services = await device.GetServicesAsync();
//                                Log.Debug(_tag, $"Found {services.Count} services available {services.Select(s => s.Id).Join()}.");
//                                service = services.FirstOrDefault(s => s.Id == BleServer.ServiceGUID);
//                            }
//                            Log.Debug(_tag, (service != null) ? $"It shows our service ({service.Id}), along with {((await device.GetServicesAsync()).Count() - 1)} others." : $"It doesn't show our service ({BleServer.ServiceGUID}), though.");
//                            if (service == null) return;

//                            var characteristics = await service.GetCharacteristicsAsync();
//                            Log.Debug(_tag, $"    Service [{service.Name}], with {characteristics.Count} characteristics:");
//                            foreach (var characteristic in characteristics)
//                            {
//                                Log.Debug(_tag, $"         <{characteristic.Name}>, Properties ({characteristic.Properties}).  Reading 20 times:");
//                                foreach (int i in Enumerable.Range(0, 20))
//                                {
//                                    await characteristic.ReadAsync();
//                                    var result = characteristic.StringValue;
//                                    Log.Debug(_tag, $"             => {result}");
//                                }
//                                Log.Debug(_tag, $"             Now listening for update notifications...");
//                                characteristic.ValueUpdated += (o, ev) =>
//                                {
//                                    Log.Debug(_tag, $"       =>> {ev.Characteristic.StringValue}");
//                                };
//                                await characteristic.StartUpdatesAsync();
//                                await Task.Delay(15000);
//                                await characteristic.StopUpdatesAsync();
//                            }
//                        }
//                        catch (Plugin.BLE.Abstractions.Exceptions.DeviceConnectionException ex)
//                        {
//                            Log.Debug(_tag, $"DeviceConnectionException when fetching services/characteristics: {ex.DeviceName}/{ex.DeviceId} : {ex.Message}");
//                            //HandleFailedConnection(peer.Device.Id);
//                        }
//                        catch (Exception ex)
//                        {
//                            Log.Debug(_tag, $"Error retrieving services/characteristics from device: {ex}");
//                            //HandleFailedConnection(peer.Device.Id);
//                        }
//                    });
//                    return true;
//                default:
//                    return base.OnOptionsItemSelected(item);
//            }
//        }

//        private Dictionary<Guid, int> ListOfFailedAttempts = new Dictionary<Guid, int>();
//        private List<Guid> ListOfIgnoredAddresses = new List<Guid>();
//        private int FailedAttemptsBeforeDisqualification = 3;
//        private async void OnDeviceDiscovered(object sender, Plugin.BLE.Abstractions.EventArgs.DeviceEventArgs e)
//        {
//            // Flat-out ignore anything that has made it onto our "Not Atropos" blacklist.
//            if (ListOfIgnoredAddresses.Contains(e.Device.Id)) return;

//            var peer = new BLEPeer() { Device = e.Device };
//            var teammate = new CommsContact { Name = e.Device.Name, BtPeer = peer };
//            PeersList.Add(teammate);
//            RefreshList();
//            //Log.Debug(_tag, $"Found a BLE peer, {peer.Device.Name} (as {peer.Device.Id})");

//            try
//            {
//                var signal = new AsyncAutoResetEvent();
//                RunOnUiThread(async () =>
//                {
//                    try
//                    {
//                        await BLEadapter.ConnectToDeviceAsync(peer.Device);
//                        signal.Set();
//                    }
//                    catch (Plugin.BLE.Abstractions.Exceptions.DeviceConnectionException)
//                    {
//                        //Log.Debug(_tag, $"(Inner) Unable to connect to {peer.Device.Name}/{peer.Device.Id}.");
//                        return;
//                    }
//                });
//                var timeout = CancellationTokenHelpers.Timeout(1500).Token;
//                await signal.WaitAsync(timeout).SwallowCancellations();
//                if (timeout.IsCancellationRequested) throw new Plugin.BLE.Abstractions.Exceptions.DeviceConnectionException(peer.Device.Id, peer.Device.Name, "Timeout on connection attempt (Atropos)");
//                Log.Debug(_tag, $"Success connecting to {peer.Device.Name}/{peer.Device.Id}");
//            }
//            catch (Plugin.BLE.Abstractions.Exceptions.DeviceConnectionException ex)
//            {
//                Log.Debug(_tag, $"Unable to connect to {ex.DeviceName}/{ex.DeviceId} : {ex.Message}");
//                HandleFailedConnection(peer.Device.Id);
//                return;
//            }

//            try
//            {
//                var service = await peer.Device.GetServiceAsync(BleServer.ServiceGUID);
//                Log.Debug(_tag, $"Found a BLE peer, {peer.Device.Name} (as {peer.Device.Id})");
//                Log.Debug(_tag, $"It shows our service ({BleServer.ServiceUUID.ToString()})");
//                await BLEadapter.StopScanningForDevicesAsync();

//                var characteristics = await service.GetCharacteristicsAsync();
//                Log.Debug(_tag, $"    Service [{service.Name}], with {characteristics.Count} characteristics:");
//                foreach (var characteristic in characteristics)
//                {
//                    Log.Debug(_tag, $"         <{characteristic.Name}>, Properties ({characteristic.Properties}).  Reading 20 times:");
//                    foreach (int i in Enumerable.Range(0, 20))
//                    {
//                        await Task.Delay(100);
//                        await characteristic.ReadAsync();
//                        var result = characteristic.StringValue;
//                        Log.Debug(_tag, $"             => {result}");
//                    }
//                    Log.Debug(_tag, $"             Now listening for update notifications...");
//                    characteristic.ValueUpdated += (o, ev) =>
//                    {
//                        Log.Debug(_tag, $"       =>> {ev.Characteristic.StringValue}");
//                    };
//                    await characteristic.StartUpdatesAsync();
//                    await Task.Delay(15000);
//                    await characteristic.StopUpdatesAsync();
//                }
//            }
//            catch (Plugin.BLE.Abstractions.Exceptions.DeviceConnectionException ex)
//            {
//                Log.Debug(_tag, $"DeviceConnectionException when fetching services/characteristics: {ex.DeviceName}/{ex.DeviceId} : {ex.Message}");
//                HandleFailedConnection(peer.Device.Id);
//            }
//            catch (Exception ex)
//            {
//                Log.Debug(_tag, $"Error retrieving services/characteristics from device: {ex}");
//                HandleFailedConnection(peer.Device.Id);
//            }
//        }

//        private void HandleFailedConnection(Guid peerID)
//        {
//            if (!ListOfFailedAttempts.ContainsKey(peerID)) ListOfFailedAttempts.Add(peerID, 0);
//            else ListOfFailedAttempts[peerID]++;
//            if (ListOfFailedAttempts[peerID] > FailedAttemptsBeforeDisqualification)
//            {
//                ListOfIgnoredAddresses.Add(peerID);
//            }
//        }

//        private String getBluetoothMacAddress()
//        {
//            BluetoothAdapter bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
//            String bluetoothMacAddress = "";
//            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
//            {
//                try
//                {
//                    var mServiceField = bluetoothAdapter.Class.GetDeclaredField("mService");
//                    mServiceField.Accessible = true;

//                    Java.Lang.Object btManagerService = mServiceField.Get(bluetoothAdapter);

//                    if (btManagerService != null)
//                    {
//                        bluetoothMacAddress = (String)btManagerService.Class.GetMethod("getAddress").Invoke(btManagerService);
//                    }
//                }
//                //catch (NoSuchFieldException e)
//                //{

//                //}
//                //catch (NoSuchMethodException e)
//                //{

//                //}
//                //catch (IllegalAccessException e)
//                //{

//                //}
//                //catch (InvocationTargetException e)
//                //{

//                //}
//                catch { }
//            }
//            else
//            {
//                bluetoothMacAddress = bluetoothAdapter.Address;
//            }
//            return bluetoothMacAddress;
//        }
//    }

//    public class BTPeerListAdapter : BaseAdapter<CommsContact>
//    {
//        private readonly Activity _context;
//        private readonly int _itemLayoutID;
//        private readonly List<CommsContact> _items;

//        public BTPeerListAdapter(Activity context, int itemLayoutID, List<CommsContact> objects)
//            : base()
//        {
//            _context = context;
//            _itemLayoutID = itemLayoutID;
//            _items = objects;
//        }

//        public override long GetItemId(int position)
//        {
//            return position;
//        }

//        public override int Count
//        {
//            get { return _items.Count; }
//        }

//        public override CommsContact this[int position]
//        {
//            get { return _items[position]; }
//        }

//        public override View GetView(int position, View convertView, ViewGroup parent)
//        {
//            var v = convertView;
//            v = v ?? _context.LayoutInflater.Inflate(_itemLayoutID, null);

//            var teammate = _items[position];

//            if (teammate != null)
//            {
//                var top = v.FindViewById<TextView>(Resource.Id.device_name);
//                var bottom = v.FindViewById<TextView>(Resource.Id.device_details);
//                var right = v.FindViewById<TextView>(Resource.Id.device_status);
//                if (top != null)
//                    top.Text = teammate.Name;
//                if (bottom != null)
//                    bottom.Text = teammate.Roles.Join(", ", "");
//                if (right != null)
//                    right.Text = teammate.Status;
//            }

//            return v;
//        }
//    }
//}