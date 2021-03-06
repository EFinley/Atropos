//using System;
//using Android.App;
//using Android.Content;
//using Android.Net.Wifi.P2p;
//using Android.OS;
//using Android.Provider;
//using Android.Util;
//using Android.Views;
//using Android.Widget;
//using System.Collections.Generic;
//using System.Threading.Tasks;

//using Android.Bluetooth;

//namespace Atropos.Communications
//{
//    [Activity(Label = "Atropos WiFi Peer-to-Peer", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
//    public partial class WiFiDirectActivity 
//        : Activity, 
//          WifiP2pManager.IChannelListener, 
//          IDeviceActionListener,
//          WifiP2pManager.IPeerListListener,
//          WifiP2pManager.IConnectionInfoListener
//    {
//        public static WiFiDirectActivity Current { get; set; }

//        public const string Tag = "Atropos_Wifidirect";
//        private WifiP2pManager _manager;
//        private bool _retryChannel;

//        private IntentFilter _intentFilter = new IntentFilter();
//        private WifiP2pManager.Channel _channel;
//        private BroadcastReceiver _receiver;

//        public bool IsWifiP2PEnabled { get; set; }

//        protected override void OnCreate(Bundle bundle)
//        {
//            base.OnCreate(bundle);

//            SetContentView(Resource.Layout.WiFi_main);
//            Current = this;

//            _intentFilter.AddAction(WifiP2pManager.WifiP2pStateChangedAction);
//            _intentFilter.AddAction(WifiP2pManager.WifiP2pPeersChangedAction);
//            _intentFilter.AddAction(WifiP2pManager.WifiP2pConnectionChangedAction);
//            _intentFilter.AddAction(WifiP2pManager.WifiP2pThisDeviceChangedAction);

//            _manager = (WifiP2pManager) GetSystemService(WifiP2pService);
//            _channel = _manager.Initialize(this, MainLooper, null);            

//            SetUpDetailView();
//            SetUpListView();

//            //task.delay(500)
//            //    .continuewith(_ =>  // same as inside onoptionsitemselected, for the discover peers options item, but there's no easy way to serve up that menu item as a constant.
//            //    {
//            //        oninitiatediscovery();
//            //        _manager.discoverpeers(_channel, new myactionlistener(this, "discovery", () => { listadapter.notifydatasetchanged(); }));
//            //    })
//            //    .configureawait(false);
//        }

//        protected override void OnResume()
//        {
//            base.OnResume();
//            _receiver = new WiFiDirectBroadcastReceiver(_manager, _channel, this);
//            RegisterReceiver(_receiver, _intentFilter);
//        }

//        protected override void OnPause()
//        {
//            base.OnPause();
//            UnregisterReceiver(_receiver);
//        }

//        /// <summary>
//        /// Remove all peers and clear all fields. This is called on
//        /// BroadcastReceiver receiving a state change event.
//        /// </summary>
//        public void ResetData()
//        {
//            //var fragmentList = FragmentManager.FindFragmentById<DeviceListFragment>(Resource.Id.frag_list);
//            //var fragmentDetails = FragmentManager.FindFragmentById<DeviceDetailFragment>(Resource.Id.frag_detail);
//            //if (fragmentList != null)
//            //    fragmentList.ClearPeers();
//            //if (fragmentDetails != null)
//            //    fragmentDetails.ResetViews();
//            ClearPeers();
//            ResetViews();
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
//                    if (_manager != null && _channel != null)
//                    {
//                        // Since this is the system wireless settings activity, it's
//                        // not going to send us a result. We will be notified by
//                        // WiFiDeviceBroadcastReceiver instead.

//                        Toast.MakeText(this, "Sending you to WiFi Settings.  WiFi Direct is probably under Advanced.", ToastLength.Long).Show();
//                        StartActivity(new Intent(Settings.ActionWifiIpSettings));
//                    }
//                    else
//                    {
//                        Log.Error(Tag, "Channel or manager is null");
//                    }
//                    return true;
//                case Resource.Id.atn_direct_discover:
//                    if (!IsWifiP2PEnabled)
//                    {
//                        Toast.MakeText(this, Resource.String.p2p_off_warning, ToastLength.Short).Show();
//                        return true;
//                    }

//                    OnInitiateDiscovery();
//                    // WIFIP2P VERSION 
//                    _manager.DiscoverPeers(_channel, new MyActionListener(this, "Discovery", () => { ListAdapter.NotifyDataSetChanged(); }));
                    
//                    return true;
//                default:
//                    return base.OnOptionsItemSelected(item);
//            }
//        }

//        private class MyActionListener : Java.Lang.Object, WifiP2pManager.IActionListener
//        {
//            private readonly Context _context;
//            private readonly string _failure;
//            private readonly Action _action;

//            public MyActionListener(Context context, string failure, Action onSuccessAction)
//            {
//                _context = context;
//                _failure = failure;
//                _action = onSuccessAction;
//            }

//            public void OnFailure(WifiP2pFailureReason reason)
//            {
//                Toast.MakeText(_context, _failure + " Failed : " + reason,
//                                ToastLength.Short).Show();
//            }

//            public void OnSuccess()
//            {
//                Toast.MakeText(_context, _failure + " Initiated",
//                                ToastLength.Short).Show();
//                _action.Invoke();
//            }
//        }

//        public void OnChannelDisconnected()
//        {
//            // we will try once more
//            if (_manager != null && !_retryChannel)
//            {
//                Toast.MakeText(this, "Channel lost. Trying again", ToastLength.Long).Show();
//                ResetData();
//                _retryChannel = true;
//                _manager.Initialize(this, MainLooper, this);
//            }
//            else
//            {
//                Toast.MakeText(this, "Severe! Channel is probably lost permanently. Try Disable/Re-Enable P2P.",
//                               ToastLength.Long).Show();
//            }
//        }

//        public void CancelDisconnect()
//        {
//            /*
//             * A cancel abort request by user. Disconnect i.e. removeGroup if
//             * already connected. Else, request WifiP2pManager to abort the ongoing
//             * request
//             */
//            if (_manager != null)
//            {
//                //var fragment = FragmentManager.FindFragmentById<DeviceListFragment>(Resource.Id.frag_list);
//                //if (fragment.Device == null || fragment.Device.Status == WifiP2pDeviceState.Connected)
//                if (MyDevice == null || MyDevice.Status == WifiP2pDeviceState.Connected)
//                    Disconnect();
//                //else if (fragment.Device.Status == WifiP2pDeviceState.Available ||
//                //         fragment.Device.Status == WifiP2pDeviceState.Invited)
//                else if (MyDevice.Status == WifiP2pDeviceState.Available ||
//                         MyDevice.Status == WifiP2pDeviceState.Invited)
//                {
//                    _manager.CancelConnect(_channel, new MyActionListener(this, "", () => { }));
//                }
//            }
//        }

//        public void Connect(WifiP2pConfig config)
//        {
//            _manager.Connect(_channel, config, new MyActionListener(this, "Connect", () => { }));
//        }

//        public void Disconnect()
//        {
//            ResetViews();
//            _cts?.Cancel(); // Closes off our sockets of all sorts - see DeviceDetailFragment.cs, WifiServer.cs, and WifiClient.cs.
//            //DisconnectSocketStreams(); // Again see DeviceDetailFragment.cs.
//            _manager.RemoveGroup(_channel, new MyActionListener(this, "Disconnect", () => { DetailView.Visibility = ViewStates.Gone; }));
//        }
//    }
//}