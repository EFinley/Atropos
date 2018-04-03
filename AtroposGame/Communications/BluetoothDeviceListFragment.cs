using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Net.Wifi.P2p;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using System.Linq;
using System.IO;
using Android.Bluetooth;

namespace Atropos.Communications.Bluetooth
{
    public class BTPeer : Java.Lang.Object
    {
        public BluetoothDevice Device;
        public string Name;
        public string MACaddressOrRole;
        public Stream InputStream;
        public Stream OutputStream;
        public bool IsNotFound = false;

        public static implicit operator BTPeer(BluetoothDevice device)
        {
            return new BTPeer() { Device = device, Name = device.Name, MACaddressOrRole = device.Address };
        }
        public static implicit operator BluetoothDevice(BTPeer peer)
        {
            return peer.Device;
        }
    }

    public partial class BTDirectActivity
    {
        private List<BTPeer> _peers = new List<BTPeer>();
        private BluetoothPeerListAdapter ListAdapter;
        private AlertDialog _progressDialog;

        public BluetoothDevice MyDevice { get; private set; }
        public int selectedDeviceIndex = -1;
        private ListView list;
        public static BTDirectActivity _currentActivity;

        private static Dictionary<string, WifiP2pDeviceState> StatusByMacAddress = new Dictionary<string, WifiP2pDeviceState>();

        private void SetUpListView()
        {
            //_peers.Add(new WifiP2pDevice() { DeviceName = "TestDevice", Status = WifiP2pDeviceState.Available });
            _currentActivity = this;
            list = FindViewById<ListView>(Resource.Id.bluetooth_peerslist);
            ListAdapter = new BluetoothPeerListAdapter(this, Resource.Layout.WiFi_row_devices, _peers);
            list.Adapter = ListAdapter;
            //list.DisableScrolling();
            list.ItemClick += OnListItemClick;
        }

        //public static string GetDeviceStatus(WifiP2pDevice device)
        //{
        //    var oldStatus = StatusByMacAddress.GetValueOrDefault(device.DeviceAddress, WifiP2pDeviceState.Unavailable);
        //    if (!StatusByMacAddress.ContainsKey(device.DeviceAddress))
        //    {
        //        Log.Debug(WiFiDirectActivity.Tag, $"Peer '{device.DeviceName}' registered with status {device.Status}.");
        //        StatusByMacAddress.Add(device.DeviceAddress, device.Status);
        //    }
        //    else if (oldStatus != device.Status)
        //    {
        //        Log.Debug(WiFiDirectActivity.Tag, $"Peer '{device.DeviceName}' changed status from {oldStatus} to {device.Status}.");
        //        StatusByMacAddress[device.DeviceAddress] = device.Status;
        //    }

        //    string result = device.Status.ToString();
        //    if (device.Status == WifiP2pDeviceState.Connected)
        //    {
        //        var i = AddressBook.Names.IndexOf(device.DeviceName);
        //        if (i >= 0)
        //        {
        //            result = $"Connected as {AddressBook.IPaddresses[i]}";
        //        }
        //    }

        //    return result;
        //}

        public void OnListItemClick(object sender, Android.Widget.AdapterView.ItemClickEventArgs e)
        {
            // Abort scanning if currently doing so
            SetScanListenState(true, false);
            bluetoothAdapter.CancelDiscovery();

            // Set the currently selected device index, reorder the list, and then update things accordingly.
            selectedDeviceIndex = e.Position;
            _device = _peers[selectedDeviceIndex];
            ReorderList();
            RefreshDetails();
        }

        public void ReorderList()
        {
            var connectedPeers = _peers.Where(p => Clients.ContainsKey(p.Device)).ToList();
            var selectedPeers = new List<BTPeer>();
            if (_device != null && !Clients.ContainsKey(_device.Device) && _peers.Any(p => p.Device.Address == _device.Device.Address))
            {
                selectedPeers.Add(_peers.First(p => p.Device.Address == _device.Device.Address));
            }
            var remainingPeers = _peers.Where(p => !connectedPeers.Contains(p) && !selectedPeers.Contains(p));
            _peers = connectedPeers.Concat(selectedPeers).Concat(remainingPeers).ToList();
            selectedDeviceIndex = _peers.FindIndex(p => p.MACaddressOrRole == _device?.MACaddressOrRole);

            foreach (var connectedPeer in connectedPeers)
            {
                var itemIndex = AddressBook.IPaddresses.IndexOf(connectedPeer.Device.Address);
                if (itemIndex >= 0)
                {
                    var contact = (CommsContact)AddressBook.Targets[itemIndex];
                    connectedPeer.Name = contact.Name;
                    var relevantRoles = contact.Roles.Where(r => !r.IsOneOf(Characters.Role.All, Characters.Role.Any, Characters.Role.Self));
                    if (relevantRoles.Count() > 0)
                    {
                        connectedPeer.MACaddressOrRole = relevantRoles.Join(", ", "");
                    }
                    else connectedPeer.MACaddressOrRole = $"Connected ({connectedPeer.Device.Address})";
                }
            }
        }

        public void RefreshDetails(bool connected = false)
        {
            if (selectedDeviceIndex >= 0 && _peers.Count > selectedDeviceIndex) _device = _peers[selectedDeviceIndex];
            RunOnUiThread(() =>
            {
                ListAdapter = new BluetoothPeerListAdapter(this, Resource.Layout.WiFi_row_devices, _peers);
                list.Adapter = ListAdapter;
            });

            //RunOnUiThread(() =>
            //{
            //    _device = (BTPeer)ListAdapter.GetItem(selectedDeviceIndex);
            //    ListAdapter.NotifyDataSetChanged();
            //    ShowDetails(_device);
            //    UpdateThisDevice(MyDevice);


            //    if (!connected)
            //    {
            //        FindViewById(Resource.Id.btn_connect).Visibility = ViewStates.Visible;
            //        FindViewById(Resource.Id.btn_disconnect).Visibility = ViewStates.Gone;
            //        FindViewById(Resource.Id.btn_test).Visibility = ViewStates.Gone;
            //    }
            //    else
            //    {
            //        FindViewById(Resource.Id.btn_connect).Visibility = ViewStates.Gone;
            //        FindViewById(Resource.Id.btn_disconnect).Visibility = ViewStates.Visible;
            //        FindViewById(Resource.Id.btn_test).Visibility = ViewStates.Visible;
            //    }
            //});
        }

        /// <summary>
        /// Update UI for this device.
        /// </summary>
        /// <param name="device">WifiP2pDevice object</param>
        public void UpdateThisDevice()
        {
            RunOnUiThread(() =>
            {
                var view = FindViewById<TextView>(Resource.Id.my_name);
                view.Text = bluetoothAdapter.Name;
                view = FindViewById<TextView>(Resource.Id.my_status);
                view.Text = Server.MyMACaddress;
            });
        }

        public void DismissProgressIndicator()
        {
            if (_progressDialog != null && _progressDialog.IsShowing)
                _progressDialog.Dismiss();
        }

        public void ShowProgressIndicator(string title, string message)
        {
            //_progressDialog = ProgressDialog.Show(this, title, message, true, true);
            var builder = new AlertDialog.Builder(this);
            builder.SetTitle(title);
            builder.SetMessage(message);
            _progressDialog = builder.Create();
        }

        public void OnPeerAvailable(BluetoothDevice peer)
        {
            // Ignore all unnamed devices - don't even dismiss the progress indicator for them, they don't exist.
            if (String.IsNullOrEmpty(peer.Name)) return;

            DismissProgressIndicator();

            if (_peers.Any(p => p.Device.Address == peer.Address))
            {
                _peers.First(p => p.Device.Address == peer.Address).Device = peer;
            }
            else _peers.Add(new BTPeer() { Device = peer, Name = peer.Name, MACaddressOrRole = peer.Address });

            //// Two-step removal process... only prune those which "miss" two "roll call" attempts.  Gives some tolerance for flaky connections,
            //// but may be an issue.  If so, just swap the order of the foreach and the where below as a quick fix.
            //_peers = _peers.Where(p => !p.IsNotFound).ToList();
            //foreach (var peer in _peers)
            //{
            //    if (!peerDevices.DeviceList.Contains(peer.Device))
            //        peer.IsNotFound = true;
            //}

            ReorderList();
            RefreshDetails();

            if (_peers.Count == 0)
            {
                FindViewById(Resource.Id.bluetooth_nopeersfound).Visibility = ViewStates.Visible;
                FindViewById(Resource.Id.bluetooth_peerslist).Visibility = ViewStates.Gone;
                Log.Debug(BTDirectActivity._tag, "No devices found");
            }
            else
            {
                //// Selected-item priority in the ensuing remade list... prior selection, then connected, then invited, then available.  If none of the above,
                //// then select nothing.
                //if (selectedDeviceIndex >= 0)
                //{
                //    var currentDeviceAddress = _device.MACaddressOrRole;
                //    selectedDeviceIndex = _peers.FindIndex(p => p.MACaddressOrRole == currentDeviceAddress);
                //}
                ////if (selectedDeviceIndex == -1) selectedDeviceIndex = _peers.FindIndex(p => p.Status == WifiP2pDeviceState.Connected);
                ////if (selectedDeviceIndex == -1) selectedDeviceIndex = _peers.FindIndex(p => p.Status == WifiP2pDeviceState.Invited);
                ////if (selectedDeviceIndex == -1) selectedDeviceIndex = _peers.FindIndex(p => p.Status == WifiP2pDeviceState.Available);
                
                //if (selectedDeviceIndex >= 0)
                //    OnListItemClick(list, new AdapterView.ItemClickEventArgs(list, null, selectedDeviceIndex, 0));

                FindViewById(Resource.Id.bluetooth_nopeersfound).Visibility = ViewStates.Gone;
                FindViewById(Resource.Id.bluetooth_peerslist).Visibility = ViewStates.Visible;
                Log.Debug(BTDirectActivity._tag, $"Peer list: {_peers.Select(p => p.Name).Join()}");
            }
        }

        public void ClearPeers()
        {
            _peers.Clear();
            _device = null;
            selectedDeviceIndex = -1;
            //ListAdapter = new BTPeerListAdapter(_currentActivity, Resource.Layout.WiFi_row_devices, _peers);
            list.Adapter = ListAdapter;
            list.DisableScrolling();
            FindViewById(Resource.Id.btn_connect).Visibility = ViewStates.Gone;
            FindViewById(Resource.Id.btn_disconnect).Visibility = ViewStates.Gone;

            //((WiFiPeerListAdapter)ListAdapter).NotifyDataSetChanged();
        }

        //public void OnInitiateDiscovery()
        //{
        //    DismissProgressIndicator();
        //    ShowProgressIndicator("Discoverable - Seeking Peers", "Caution - this will prevent others connecting /to/ you successfully for some time.");
        //}
    }

    public class BluetoothPeerListAdapter : BaseAdapter<BTPeer>
    {
        private readonly Activity _context;
        private readonly int _itemLayoutID;
        private readonly List<BTPeer> _items;

        public BluetoothPeerListAdapter(Activity context, int itemLayoutID, List<BTPeer> objects)
            : base()
        {
            _context = context;
            _itemLayoutID = itemLayoutID;
            _items = objects;
        }

        public override long GetItemId(int position)
        {
            return position;
        }

        public override int Count
        {
            get { return _items.Count; }
        }

        public override BTPeer this[int position]
        {
            get { return _items[position]; }
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var v = convertView;
            v = v ?? _context.LayoutInflater.Inflate(_itemLayoutID, null);

            var device = _items[position];

            if (device != null)
            {
                var icon = v.FindViewById<ImageView>(Resource.Id.icon);
                var top = v.FindViewById<TextView>(Resource.Id.device_name);
                var bottom = v.FindViewById<TextView>(Resource.Id.device_details);
                var button = v.FindViewById<Button>(Resource.Id.device_connect_button);
                var button2 = v.FindViewById<Button>(Resource.Id.device_disconnect_button);
                var button3 = v.FindViewById<Button>(Resource.Id.device_test_button);

                bool IsConnected = BTDirectActivity.Clients.ContainsKey(device.Device);

                if (icon != null)
                    if (position == BTDirectActivity.Current.selectedDeviceIndex)
                        icon.SetImageResource(Resource.Drawable.team_search_icon_2color);
                    else if (IsConnected)
                        icon.SetImageResource(Resource.Drawable.team_icon);
                    else icon.SetImageResource(Resource.Drawable.team_search_icon);
                if (top != null)
                    top.Text = device.Name;
                if (bottom != null)
                    bottom.Text = device.MACaddressOrRole;
                if (button != null)
                {
                    if (position == BTDirectActivity.Current.selectedDeviceIndex && !IsConnected)
                    {
                        button.Tag = device;
                        button.Click -= BTDirectActivity.Current.ConnectByTag; // Prevent multiple instances accumulating
                        button.Click += BTDirectActivity.Current.ConnectByTag;
                        button.Visibility = ViewStates.Visible;
                    }
                    else button.Visibility = ViewStates.Gone;
                }
                if (button2 != null)
                {
                    if (IsConnected)
                    {
                        button2.Tag = device;
                        button2.Click -= BTDirectActivity.Current.DisconnectByTag;
                        button2.Click += BTDirectActivity.Current.DisconnectByTag;
                        button2.Visibility = ViewStates.Visible;
                    }
                    else button2.Visibility = ViewStates.Gone;
                }
                if (button3 != null)
                {
                    if (IsConnected)
                    {
                        button3.Tag = device;
                        button3.Click -= BTDirectActivity.Current.SendTestStringByTag;
                        button3.Click += BTDirectActivity.Current.SendTestStringByTag;
                        button3.Visibility = ViewStates.Visible;
                    }
                    else button3.Visibility = ViewStates.Gone;
                }
            }

            return v;
        }
    }
}
