using Android.Net.Wifi.P2p;

namespace Atropos.Communications
{
    public interface IDeviceActionListener
    {
        void ShowDetails(WifiP2pDevice device);
        void CancelDisconnect();
        void Connect(WifiP2pConfig config);
        void Disconnect();
    }
}