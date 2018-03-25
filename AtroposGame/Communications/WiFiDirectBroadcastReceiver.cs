//using Android.Content;
//using Android.Net;
//using Android.Net.Wifi.P2p;
//using Android.Util;

//namespace Atropos.Communications
//{
//    /// <summary>
//    /// A BroadcastReceiver that notifies of important wifi p2p events.
//    /// </summary>
//    public class WiFiDirectBroadcastReceiver : BroadcastReceiver
//    {
//        private readonly WifiP2pManager _manager;
//        private readonly WifiP2pManager.Channel _channel;
//        private readonly WiFiDirectActivity _activity;

//        /// <summary>
//        /// ctor
//        /// </summary>
//        /// <param name="manager">WifiP2pManager system service</param>
//        /// <param name="channel">Wifi p2p channel</param>
//        /// <param name="activity">activity associated with the receiver</param>
//        public WiFiDirectBroadcastReceiver(WifiP2pManager manager, WifiP2pManager.Channel channel,
//                                           WiFiDirectActivity activity)
//        {
//            _manager = manager;
//            _channel = channel;
//            _activity = activity;
//        }

//        public override void OnReceive(Context context, Intent intent)
//        {
//            var action = intent.Action;

//            if (WifiP2pManager.WifiP2pStateChangedAction.Equals(action))
//            {
//                // UI update to indicate wifi p2p status.
//                var state = intent.GetIntExtra(WifiP2pManager.ExtraWifiState, -1);
//                if (state == (int)WifiP2pState.Enabled)
//                {
//                    // Wifi Direct mode is enabled
//                    _activity.IsWifiP2PEnabled = true;
//                    Log.Debug(WiFiDirectActivity.Tag, "P2P enabled.");
//                    _manager.RequestPeers(_channel, _activity);
//                }
//                else
//                {
//                    _activity.IsWifiP2PEnabled = false;
//                    _activity.ResetData();
//                    Log.Debug(WiFiDirectActivity.Tag, "P2P disabled.");
//                }
//            }
//            else if (WifiP2pManager.WifiP2pPeersChangedAction.Equals(action))
//            {
//                // request available peers from the wifi p2p manager. This is an
//                // asynchronous call and the calling activity is notified with a
//                // callback on PeerListListener.onPeersAvailable()
//                _manager?.RequestPeers(_channel, _activity);
//                Log.Debug(WiFiDirectActivity.Tag, "P2P peers changed... requesting details.");
//            }
//            else if (WifiP2pManager.WifiP2pConnectionChangedAction.Equals(action))
//            {
//                if (_manager == null)
//                    return;

//                // Do a peers query here as well, since often this is triggered by returning from the WiFi settings (where we can connect just as easily as here)
//                _manager.RequestPeers(_channel, _activity);

//                var networkInfo = (NetworkInfo) intent.GetParcelableExtra(WifiP2pManager.ExtraNetworkInfo);

//                if (networkInfo.IsConnected)
//                {
//                    // we are connected with the other device, request connection
//                    // info to find group owner IP
//                    _manager.RequestConnectionInfo(_channel, _activity);
//                    Log.Debug(WiFiDirectActivity.Tag, "Connection state changed: connected.");
//                }
//                else
//                {
//                    // It's a disconnect
//                    _activity.ResetData();
//                    Log.Debug(WiFiDirectActivity.Tag, "Connection state changed: disconnected.");
//                }
//            }
//            else if (WifiP2pManager.WifiP2pThisDeviceChangedAction.Equals(action))
//            {
//                _activity?.UpdateThisDevice((WifiP2pDevice)intent?.GetParcelableExtra(WifiP2pManager.ExtraWifiP2pDevice));
//            }
//        }
//    }
//}