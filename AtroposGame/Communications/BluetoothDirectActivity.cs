using System;
using Android.App;
using Android.Content;
using Android.Net.Wifi.P2p;
using Android.OS;
using Android.Provider;
using Android.Util;
using Android.Views;
using Android.Widget;
using System.Collections.Generic;
using System.Threading.Tasks;

using Android.Bluetooth;
using Android.Runtime;
using System.Threading;
using System.Linq;

namespace Atropos.Communications.Bluetooth
{
    [Activity(Label = "Atropos Bluetooth Comms", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public partial class BTDirectActivity : Activity
    {
        public static BTDirectActivity Current { get; set; }
        private IMenu Menu;

        public const string Tag = "Atropos|BluetoothCommsActivity";

        private IntentFilter _intentFilter = new IntentFilter();
        private BroadcastReceiver _receiver;
        private BTPeer _device { get; set; }

        protected static BluetoothServer Server;
        public static Dictionary<BluetoothDevice, BluetoothClient> Clients = new Dictionary<BluetoothDevice, BluetoothClient>();

        private BluetoothAdapter bluetoothAdapter;
        private CancellationTokenSource GlobalBluetoothDeactivator;

        private int REQUEST_ENABLE = 13;
        private int REQUEST_DISCOVERABLE = 14;
        private int DISCOVERABLE_DURATION = 300;

        private const string KnownMACaddressesKey = "KnownMACaddressesForBluetooth";
        private HashSet<string> KnownMACaddresses = new HashSet<string>();

        private bool listening = true;
        private bool scanning = false;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            SetContentView(Resource.Layout.WiFi_main);
            Current = this;

            // BLUETOOTH VERSION
            bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
            if (bluetoothAdapter == null || !bluetoothAdapter.IsEnabled)
            {
                Intent enableBluetoothIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
                StartActivityForResult(enableBluetoothIntent, REQUEST_ENABLE);
            }

            //SetUpDetailView();
            SetUpListView();
            SetUpDeviceNameOperations();
            SetScanListenState(listening: false, scanning: false, firstTimeSetup: true);

            GlobalBluetoothDeactivator = new CancellationTokenSource();

            // Rather than wait for the OnConnectionInfoAvailable, the way WiFiP2P does it, here we just set up the server and keep it running.
            Server = BluetoothMessageCenter.Server = new BluetoothServer(GlobalBluetoothDeactivator.Token);
            Server.Listen();

            KnownMACaddresses = Res.Storage.Get<HashSet<string>>(KnownMACaddressesKey) ?? new HashSet<string>();
            foreach (var macAddress in KnownMACaddresses)
            {
                var device = bluetoothAdapter.GetRemoteDevice(macAddress); // Returns a device with name = null if device not found in range
                OnPeerAvailable(device); // Ignores all devices with null or empty name fields
            }
        }

        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
        {
            if (requestCode == REQUEST_ENABLE)
            {
                if (resultCode == Result.Ok)
                {
                    Toast.MakeText(this, "Thank you! Bluetooth now enabled.", ToastLength.Short).Show();
                }
                else
                {
                    Toast.MakeText(this, "Without Bluetooth enabled, you may as well press Back to return to Atropos. Alone.", ToastLength.Short).Show();
                }
            }
            else if (requestCode == REQUEST_DISCOVERABLE)
            {
                if ((int)resultCode == DISCOVERABLE_DURATION)
                {
                    Toast.MakeText(this, "Device now discoverable by other devices for the next five minutes.", ToastLength.Short).Show();
                }
                else
                {
                    Toast.MakeText(this, "Discoverability not allowed (or bugged).  Not gonna work.", ToastLength.Short).Show();
                }
            }
        }

        protected override void OnResume()
        {
            base.OnResume();
            // WIFIP2P VERSION
            //_receiver = new WiFiDirectBroadcastReceiver(_manager, _channel, this);
            // BLUETOOTH VERSION
            _receiver = new BluetoothBroadcastReceiver();
            _intentFilter = new IntentFilter();
            _intentFilter.AddAction(BluetoothDevice.ActionFound);
            _intentFilter.AddAction(BluetoothDevice.ActionAclDisconnected);

            RegisterReceiver(_receiver, _intentFilter);
        }

        protected override void OnPause()
        {
            base.OnPause();
            UnregisterReceiver(_receiver);

            // Debugging!
            DisconnectAll();
        }

        //public override bool OnCreateOptionsMenu(IMenu menu)
        //{
        //    Menu = menu;
        //    var inflater = MenuInflater;
        //    inflater.Inflate(Resource.Menu.WiFi_action_items, menu);
        //    // Tint the lightbulb icon grey
        //    var item = menu.GetItem(0);
        //    item.tintMenuIcon(this, Android.Graphics.Color.Gray);
        //    return true;
        //}

        //public override bool OnOptionsItemSelected(IMenuItem item)
        //{
        //    switch (item.ItemId)
        //    {
        //        case Resource.Id.atn_direct_enable:
        //            Intent discoverableIntent = new Intent(BluetoothAdapter.ActionRequestDiscoverable);
        //            discoverableIntent.PutExtra(BluetoothAdapter.ExtraDiscoverableDuration, DISCOVERABLE_DURATION);
        //            StartActivityForResult(discoverableIntent, REQUEST_DISCOVERABLE);
        //            return true;
        //        case Resource.Id.atn_direct_discover:

                                      

        //            //var fragment = FragmentManager.FindFragmentById<DeviceListFragment>(Resource.Id.frag_list);
        //            //fragment.OnInitiateDiscovery();
        //            OnInitiateDiscovery();
        //            // WIFIP2P VERSION 
        //            // _manager.DiscoverPeers(_channel, new MyActionListener(this, "Discovery", () => { ListAdapter.NotifyDataSetChanged(); }));
        //            // BLUETOOTH VERSION
        //            // Note that we've already registered a broadcast receiver to handle when devices show up - that's in OnResume above.
        //            bluetoothAdapter.StartDiscovery();

        //            return true;
        //        default:
        //            return base.OnOptionsItemSelected(item);
        //    }
        //}

        private class BluetoothBroadcastReceiver : BroadcastReceiver
        {
            public override void OnReceive(Context ctx, Intent intent)
            {
                string action = intent.Action;
                BluetoothDevice device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);

                if (action == BluetoothDevice.ActionFound)
                {
                    BTDirectActivity.Current.OnPeerAvailable(device);
                }
                if (action == BluetoothDevice.ActionAclDisconnected)
                {
                    BTDirectActivity.Current.Disconnect(device);
                }
            }
        }

        private void SetUpDeviceNameOperations()
        {
            var myNameField = FindViewById<TextView>(Resource.Id.my_name);
            var myNameChangeBtn = FindViewById<Button>(Resource.Id.my_name_changeBtn);
            var myNameEditField = FindViewById<EditText>(Resource.Id.my_name_editbox);
            var myMACaddressField = FindViewById<TextView>(Resource.Id.my_status);

            myNameField.Text = myNameEditField.Text = bluetoothAdapter.Name;
            myMACaddressField.Text = "(MAC address hidden by Android)";
            myNameChangeBtn.Click += (o, e) =>
            {
                myNameField.Visibility = ViewStates.Gone;
                myNameChangeBtn.Visibility = ViewStates.Gone;
                myNameEditField.Visibility = ViewStates.Visible;
            };
            myNameEditField.EditorAction += (o, e) =>
            {
                if (e.ActionId == Android.Views.InputMethods.ImeAction.Done)
                {
                    bluetoothAdapter.SetName(myNameEditField.Text);
                    myNameField.Text = myNameEditField.Text;

                    myNameField.Visibility = ViewStates.Visible;
                    myNameChangeBtn.Visibility = ViewStates.Visible;
                    myNameEditField.Visibility = ViewStates.Gone;
                }
            };
        }

        public void SetScanListenState(bool listening, bool scanning, bool firstTimeSetup = false)
        {
            Button listenButton = FindViewById<Button>(Resource.Id.bluetooth_listenBtn);
            Button scanButton = FindViewById<Button>(Resource.Id.bluetooth_scanBtn);
            TextView listeningStatus = FindViewById<TextView>(Resource.Id.bluetooth_listeningStatusText);
            TextView scanningStatus = FindViewById<TextView>(Resource.Id.bluetooth_scanningStatusText);

            listenButton.Visibility = (listening) ? ViewStates.Gone : ViewStates.Visible;
            scanButton.Visibility = (scanning) ? ViewStates.Gone : ViewStates.Visible;
            listeningStatus.Visibility = (listening) ? ViewStates.Visible : ViewStates.Gone;
            scanningStatus.Visibility = (scanning) ? ViewStates.Visible : ViewStates.Gone;

            if (firstTimeSetup)
            {
                listenButton.Click += (o, e) =>
                {
                    SetScanListenState(true, false);
                    bluetoothAdapter.CancelDiscovery();

                    if (bluetoothAdapter.ScanMode != ScanMode.ConnectableDiscoverable)
                    {
                        Intent discoverableIntent = new Intent(BluetoothAdapter.ActionRequestDiscoverable);
                        discoverableIntent.PutExtra(BluetoothAdapter.ExtraDiscoverableDuration, DISCOVERABLE_DURATION);
                        StartActivityForResult(discoverableIntent, REQUEST_DISCOVERABLE);
                    }
                };

                scanButton.Click += (o, e) =>
                {
                    SetScanListenState(false, true);
                    bluetoothAdapter.StartDiscovery();
                };

                scanButton.LongClick += (o, e) =>
                {
                    KnownMACaddresses.Clear();
                    _peers.Clear();
                    Res.Storage.Put(KnownMACaddressesKey, KnownMACaddresses);
                    RefreshDetails();
                };
            }
            else
            {
                this.listening = listening;
                this.scanning = scanning;
            }
        }

        public void Connect(BTPeer peer)
        {
            // Make this a no-op if it's already connected, just in case.
            if (Clients.ContainsKey(peer.Device)) return;
            // Maybe that's not working?
            if (Clients.Keys.Any(p => p.Address == peer.Device.Address)) return;

            if (_progressDialog != null && _progressDialog.IsShowing)
                _progressDialog.Dismiss();
            bluetoothAdapter.CancelDiscovery();

            //_progressDialog = ProgressDialog.Show(this, $"Connecting to {_device.Name} ({_device.MACaddressOrRole})", "Press back to cancel", true, true);

            var newTeamMate = new CommsContact();
            newTeamMate.BtPeer = peer;
            newTeamMate.Name = peer.Device.Name;
            newTeamMate.IPaddress = peer.Device.Address;
            var client = new BluetoothClient(GlobalBluetoothDeactivator.Token);

            // Arrange for appropriate outcomes to success or failure of our connection attempt...
            client.OnConnectionSuccess += (o, e) =>
            {
                //newTeamMate.Client = client;
                //AddressBook.Add(newTeamMate);
                Clients.Add(_device, client);
                KnownMACaddresses.Add(_device.MACaddressOrRole);
                Res.Storage.Put(KnownMACaddressesKey, KnownMACaddresses);
                _progressDialog?.Dismiss();
                RefreshDetails(true);
            };
            client.OnConnectionFailure += (o, e) =>
            {
                _progressDialog?.Dismiss();
                KnownMACaddresses.Remove(_device.MACaddressOrRole);
                Res.Storage.Put(KnownMACaddressesKey, KnownMACaddresses);
                client = null;
                RefreshDetails();
            };
            client.OnDisconnection += (o, e) =>
            {
                Clients.Remove(_device);
                RefreshDetails();
            };
            client.OnMessageReceived += (o, e) =>
            {
                RelayToast($"Client received ({e.Value.Type}) {e.Value.Content}");
            };

            //... then make the attempt itself.
            client?.Connect(newTeamMate.BtPeer);
        }
        public void Connect(BluetoothDevice device)
        {
            OnPeerAvailable(device);
            var peer = _peers.First(p => p.Device.Address == device.Address);
            Connect(peer);
        }
        public void ConnectByMACaddress(string address)
        {
            var device = bluetoothAdapter.GetRemoteDevice(address); // Returns a device with name = null if device not found in range
            if (string.IsNullOrEmpty(device.Name)) return;

            Connect(device);
        }
        public void ConnectByTag(object sender, EventArgs args)
        {
            var button = sender as Button;
            if (button == null) return;
            var label = (BTPeer)button.Tag;
            Connect(label);
        }

        public void Disconnect(BTPeer peer)
        {
            if (!Clients.ContainsKey(peer)) return;

            Clients[peer].Disconnect();
            //Clients.Remove(peer);
            //RefreshDetails();
        }
        public void Disconnect(BluetoothDevice device)
        {
            var peer = _peers.First(p => p.Device.Address == device.Address);
            Disconnect(peer);
        }
        public void DisconnectByTag(object sender, EventArgs args)
        {
            var button = sender as Button;
            if (button == null) return;
            var label = (BTPeer)button.Tag;
            Disconnect(label);
        }

        public void SendTestString(BTPeer peer)
        {
            if (!Clients.ContainsKey(peer)) { Log.Warn(Tag, $"Attempting to send test string to disconnected peer {peer.Device.Address}."); return; }

            var r = new Random();
            var message = new Message(MsgType.Notify, $"Test string {r.Next(100)}");
            RelayToast($"Sending test string '{message.Content}'.");
            Clients[peer].SendMessage(message);
        }
        public void SendTestStringByTag(object sender, EventArgs args)
        {
            var button = sender as Button;
            if (button == null) return;
            var label = (BTPeer)button.Tag;
            SendTestString(label);
        }

        /// <summary>
        /// Remove all peers and clear all fields. This is called on
        /// BroadcastReceiver receiving a state change event.
        /// </summary>
        public void ResetData()
        {
            //var fragmentList = FragmentManager.FindFragmentById<DeviceListFragment>(Resource.Id.frag_list);
            //var fragmentDetails = FragmentManager.FindFragmentById<DeviceDetailFragment>(Resource.Id.frag_detail);
            //if (fragmentList != null)
            //    fragmentList.ClearPeers();
            //if (fragmentDetails != null)
            //    fragmentDetails.ResetViews();
            ClearPeers();
            //ResetViews();
        }

        public void RelayToast(string message)
        {
            RunOnUiThread(() =>
            {
                Toast.MakeText(this, message, ToastLength.Long).Show();
                FindViewById<TextView>(Resource.Id.status_text).Text = message;
            });
            
        }

        //public void OnChannelDisconnected()
        //{
        //    // we will try once more
        //    if (_manager != null && !_retryChannel)
        //    {
        //        Toast.MakeText(this, "Channel lost. Trying again", ToastLength.Long).Show();
        //        ResetData();
        //        _retryChannel = true;
        //        _manager.Initialize(this, MainLooper, this);
        //    }
        //    else
        //    {
        //        Toast.MakeText(this, "Severe! Channel is probably lost permanently. Try Disable/Re-Enable P2P.",
        //                       ToastLength.Long).Show();
        //    }
        //}

        //public void CancelDisconnect()
        //{
        //    /*
        //     * A cancel abort request by user. Disconnect i.e. removeGroup if
        //     * already connected. Else, request WifiP2pManager to abort the ongoing
        //     * request
        //     */
        //    if (_manager != null)
        //    {
        //        //var fragment = FragmentManager.FindFragmentById<DeviceListFragment>(Resource.Id.frag_list);
        //        //if (fragment.Device == null || fragment.Device.Status == WifiP2pDeviceState.Connected)
        //        if (MyDevice == null || MyDevice.Status == WifiP2pDeviceState.Connected)
        //            Disconnect();
        //        //else if (fragment.Device.Status == WifiP2pDeviceState.Available ||
        //        //         fragment.Device.Status == WifiP2pDeviceState.Invited)
        //        else if (MyDevice.Status == WifiP2pDeviceState.Available ||
        //                 MyDevice.Status == WifiP2pDeviceState.Invited)
        //        {
        //            _manager.CancelConnect(_channel, new MyActionListener(this, "", () => { }));
        //        }
        //    }
        //}

        //public void Connect(WifiP2pConfig config)
        //{
        //    _manager.Connect(_channel, config, new MyActionListener(this, "Connect", () => { }));
        //}

        public void DisconnectAll()
        {
            //ResetViews();
            RefreshDetails();
            GlobalBluetoothDeactivator?.Cancel(); // Closes off our sockets of all sorts - see DeviceDetailFragment.cs, BluetoothServer.cs, and WifiClient.cs.
            //DisconnectSocketStreams(); // Again see DeviceDetailFragment.cs.
        }
    }
}