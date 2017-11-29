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

namespace com.Atropos.Communications
{
    public class WifiPeer : Java.Lang.Object
    {
        public WifiP2pDevice Device;
        public string Name { get { return Device.DeviceName; } }
        public string MACaddress { get { return Device.DeviceAddress; } }
        public WifiP2pDeviceState Status { get { return Device.Status; } }
        public Stream InputStream;
        public Stream OutputStream;
        public bool IsNotFound = false;

        public static implicit operator WifiPeer(WifiP2pDevice device)
        {
            return new WifiPeer() { Device = device };
        }
        public static implicit operator WifiP2pDevice(WifiPeer peer)
        {
            return peer.Device;
        }
    }

    public partial class WiFiDirectActivity
    {
        private List<WifiPeer> _peers = new List<WifiPeer>();
        private WiFiPeerListAdapter ListAdapter;
        private ProgressDialog _progressDialog;

        public WifiP2pDevice MyDevice { get; private set; }
        public int selectedDeviceIndex = -1;
        private View ListView;
        private ListView list;
        public static WiFiDirectActivity _currentActivity;

        private static Dictionary<string, WifiP2pDeviceState> StatusByMacAddress = new Dictionary<string, WifiP2pDeviceState>();

        private void SetUpListView()
        {
            //_peers.Add(new WifiP2pDevice() { DeviceName = "TestDevice", Status = WifiP2pDeviceState.Available });
            _currentActivity = this;
            ListView = FindViewById(Resource.Id.wifi_frag_list);
            list = FindViewById<ListView>(Resource.Id.wifi_peerslist);
            ListAdapter = new WiFiPeerListAdapter(this, Resource.Layout.WiFi_row_devices, _peers);
            list.Adapter = ListAdapter;
            list.DisableScrolling();
            list.ItemClick += OnListItemClick;
        }

        public static string GetDeviceStatus(WifiP2pDevice device)
        {
            var oldStatus = StatusByMacAddress.GetValueOrDefault(device.DeviceAddress, WifiP2pDeviceState.Unavailable);
            if (!StatusByMacAddress.ContainsKey(device.DeviceAddress))
            {
                Log.Debug(WiFiDirectActivity.Tag, $"Peer '{device.DeviceName}' registered with status {device.Status}.");
                StatusByMacAddress.Add(device.DeviceAddress, device.Status);
            }
            else if (oldStatus != device.Status)
            {
                Log.Debug(WiFiDirectActivity.Tag, $"Peer '{device.DeviceName}' changed status from {oldStatus} to {device.Status}.");
                StatusByMacAddress[device.DeviceAddress] = device.Status;
            }

            //switch (device.Status)
            //{
            //    case WifiP2pDeviceState.Available:
            //        return "Available";
            //    case WifiP2pDeviceState.Invited:
            //        return "Invited";
            //    case WifiP2pDeviceState.Connected:
            //        return "Connected";
            //    case WifiP2pDeviceState.Failed:
            //        return "Failed";
            //    case WifiP2pDeviceState.Unavailable:
            //        return "Unavailable";
            //    default:
            //        return "Unknown";
            //}

            return device.Status.ToString();
        }

        public void OnListItemClick(object sender, Android.Widget.AdapterView.ItemClickEventArgs e)
        {
            var listView = sender as ListView;
            selectedDeviceIndex = e.Position;
            _device = (WifiPeer)ListAdapter.GetItem(e.Position);
            ListAdapter.NotifyDataSetChanged();
            ShowDetails(_device);
        }

        /// <summary>
        /// Update UI for this device.
        /// </summary>
        /// <param name="device">WifiP2pDevice object</param>
        public void UpdateThisDevice(WifiP2pDevice device)
        {
            MyDevice = device;
            var view = FindViewById<TextView>(Resource.Id.my_name);
            view.Text = device.DeviceName;
            view = FindViewById<TextView>(Resource.Id.my_status);
            view.Text = GetDeviceStatus(device);
        }

        public void DismissProgressIndicator()
        {
            if (_progressDialog != null && _progressDialog.IsShowing)
                _progressDialog.Dismiss();
        }

        public void ShowProgressIndicator(string title, string message)
        {
            _progressDialog = ProgressDialog.Show(this, title, message, true, true);
        }

        public void OnPeersAvailable(WifiP2pDeviceList peerDevices)
        {
            DismissProgressIndicator();

            // Add or update the devices found on this detect pass.
            foreach (var peerDevice in peerDevices.DeviceList)
            {
                if (_peers.Any(p => p.MACaddress == peerDevice.DeviceAddress))
                {
                    _peers.First(p => p.MACaddress == peerDevice.DeviceAddress).Device = peerDevice;
                }
                else _peers.Add(new WifiPeer() { Device = peerDevice });
            }

            // Two-step removal process... only prune those which "miss" two "roll call" attempts.  Gives some tolerance for flaky connections,
            // but may be an issue.  If so, just swap the order of the foreach and the where below as a quick fix.
            _peers = _peers.Where(p => !p.IsNotFound).ToList();
            foreach (var peer in _peers)
            {
                if (!peerDevices.DeviceList.Contains(peer.Device))
                    peer.IsNotFound = true;
            }

            RunOnUiThread(() =>
            {
                ListAdapter = new WiFiPeerListAdapter(_currentActivity, Resource.Layout.WiFi_row_devices, _peers);
                list.Adapter = ListAdapter;
                list.DisableScrolling();
            });
            if (_peers.Count == 0)
            {
                FindViewById(Resource.Id.wifi_nopeersfound).Visibility = ViewStates.Visible;
                Log.Debug(WiFiDirectActivity.Tag, "No devices found");
            }
            else
            {
                // Selected-item priority in the ensuing remade list... prior selection, then connected, then invited, then available.  If none of the above,
                // then select nothing.
                if (selectedDeviceIndex >= 0)
                {
                    var currentDeviceAddress = _device.MACaddress;
                    selectedDeviceIndex = _peers.FindIndex(p => p.MACaddress == currentDeviceAddress);
                }
                if (selectedDeviceIndex == -1) selectedDeviceIndex = _peers.FindIndex(p => p.Status == WifiP2pDeviceState.Connected);
                if (selectedDeviceIndex == -1) selectedDeviceIndex = _peers.FindIndex(p => p.Status == WifiP2pDeviceState.Invited);
                if (selectedDeviceIndex == -1) selectedDeviceIndex = _peers.FindIndex(p => p.Status == WifiP2pDeviceState.Available);
                
                if (selectedDeviceIndex >= 0)
                    OnListItemClick(list, new AdapterView.ItemClickEventArgs(list, null, selectedDeviceIndex, 0));

                FindViewById(Resource.Id.wifi_nopeersfound).Visibility = ViewStates.Gone;
                Log.Debug(WiFiDirectActivity.Tag, $"Peer list: {_peers.Select(p => p.Name).Join()}");
            }
        }

        public void ClearPeers()
        {
            _peers.Clear();
            _device = null;
            selectedDeviceIndex = -1;
            ListAdapter = new WiFiPeerListAdapter(_currentActivity, Resource.Layout.WiFi_row_devices, _peers);
            list.Adapter = ListAdapter;
            list.DisableScrolling();
            FindViewById(Resource.Id.btn_connect).Visibility = ViewStates.Gone;
            FindViewById(Resource.Id.btn_disconnect).Visibility = ViewStates.Gone;

            //((WiFiPeerListAdapter)ListAdapter).NotifyDataSetChanged();
        }

        public void OnInitiateDiscovery()
        {
            DismissProgressIndicator();
            ShowProgressIndicator("Discoverable - Seeking Peers", "Press back to cancel");
        }
    }

    public class WiFiPeerListAdapter : BaseAdapter<WifiPeer>
    {
        private readonly Activity _context;
        private readonly int _itemLayoutID;
        private readonly List<WifiPeer> _items;

        public WiFiPeerListAdapter(Activity context, int itemLayoutID, List<WifiPeer> objects)
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

        public override WifiPeer this[int position]
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
                if (icon != null)
                    if (position == WiFiDirectActivity._currentActivity.selectedDeviceIndex)
                        icon.SetImageResource(Resource.Drawable.team_search_icon_2color);
                    else icon.SetImageResource(Resource.Drawable.team_icon);
                if (top != null)
                    top.Text = device.Name;
                if (bottom != null)
                {
                    bottom.Text = WiFiDirectActivity.GetDeviceStatus(device);
                }
            }

            return v;
        }
    }
}
