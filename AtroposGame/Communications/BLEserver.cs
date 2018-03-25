//using System;
//using System.Diagnostics;
//using System.Linq;

//using Android.Bluetooth;
//using Android.Bluetooth.LE;
//using Android.Content;
//using Android.Util;
//using UUID = Java.Util.UUID;
//using Random = System.Random;
//using System.Collections.Generic;

//namespace Atropos.Communications
//{
//    public partial class BleServer
//    {
//        private const string _tag = "BLE_Server";

//        private static string UIDstring = "ffe0ecd2-3d16-4f8d-90de-e89e7fc396a5";
//        public static readonly UUID ServiceUUID = UUID.FromString(UIDstring);
//        public static readonly Guid ServiceGUID = Guid.Parse(UIDstring);

//        private static string MessagesUIDstring = "56B134A1-5EBA-42BD-B55D-F6B3EA29D5BA";
//        public static readonly UUID MessagesUUID = UUID.FromString(MessagesUIDstring);
//        public static readonly Guid MessagesGUID = Guid.Parse(MessagesUIDstring);

//        // This descriptor is mandatory - I think - on all characteristics.
//        private static string MandatoryClientConfigDescriptorIDString = "00002902-0000-1000-8000-00805f9b34fb";
//        public static UUID MandatoryClientConfigDescriptorUUID = UUID.FromString(MandatoryClientConfigDescriptorIDString);

//        public HashSet<BluetoothDevice> Subscribers = new HashSet<BluetoothDevice>();

//        private readonly BluetoothManager _bluetoothManager;
//        private BluetoothAdapter _bluetoothAdapter;
//        private BleGattServerCallback _bluetoothServerCallback;
//        BluetoothLeAdvertiser myBluetoothLeAdvertiser;
//        private BluetoothGattServer _bluetoothServer;
//        private BluetoothGattCharacteristic _characteristic;

//        public BleServer(Context ctx)
//        {
//            _bluetoothManager = (BluetoothManager)ctx.GetSystemService(Context.BluetoothService);
//            _bluetoothAdapter = _bluetoothManager.Adapter;

//            _bluetoothServerCallback = new BleGattServerCallback(this);
//            _bluetoothServer = _bluetoothManager.OpenGattServer(ctx, _bluetoothServerCallback);

//            var service = new BluetoothGattService(ServiceUUID, GattServiceType.Primary);

//            //foreach (var characteristic in CommsContact.Me.Characteristics.Values)
//            //{
//            //    var ctric = new BluetoothGattCharacteristic(UUID.FromString(characteristic.GUID.ToString()),
//            //        GattProperty.Read | GattProperty.Notify, GattPermission.Read);
//            //    ctric.SetValue(characteristic.StringValue);
//            //    characteristic.OnValueChanged += (o, e) =>
//            //    {
//            //        ctric.SetValue(characteristic.StringValue);
//            //        foreach (var subscriber in subscribers)
//            //        {
//            //            _bluetoothServer.NotifyCharacteristicChanged(subscriber, ctric, false);
//            //        }
//            //    };
//            //    _bluetoothServerCallback.CharacteristicReadRequest += (o, e) =>
//            //    {
//            //        if (e.Characteristic != ctric) return;

//            //        _bluetoothServer.SendResponse(e.Device, e.RequestId, GattStatus.Success, e.Offset,
//            //            e.Characteristic.GetValue());
//            //    };
//            //    service.AddCharacteristic(ctric);
//            //    Log.Debug(_tag, $"Added characteristic {ctric.Uuid} to service.");
//            //}

//            //var messagePipeCharacteristic = new BluetoothGattCharacteristic(MessagesUUID, GattProperty.Write, GattPermission.Write);
//            //_bluetoothServerCallback.CharacteristicWriteRequest += (o, e) =>
//            //{
//            //    if (e.Characteristic != messagePipeCharacteristic)
//            //    {
//            //        _bluetoothServer.SendResponse(e.Device, e.RequestId, GattStatus.WriteNotPermitted, e.Offset, e.Characteristic.GetValue());
//            //        return;
//            //    }

//            //    messagePipeCharacteristic.SetValue(e.Value);
//            //    Log.Debug(_tag, $"Received message pipe message: {e.Characteristic.GetStringValue(e.Offset)}");
//            //    _bluetoothServer.SendResponse(e.Device, e.RequestId, GattStatus.Success, e.Offset, e.Characteristic.GetValue());
//            //};
//            //service.AddCharacteristic(messagePipeCharacteristic);
//            //Log.Debug(_tag, $"Added characteristic {messagePipeCharacteristic.Uuid} to service.");

//            _characteristic = new BluetoothGattCharacteristic(UUID.FromString("A7FEF02F-EB79-492B-A8D2-948B6880EF4A"), GattProperty.Read | GattProperty.Write | GattProperty.Notify, GattPermission.Read | GattPermission.Write);
//            _characteristic.AddDescriptor(new BluetoothGattDescriptor(MandatoryClientConfigDescriptorUUID,
//                    GattDescriptorPermission.Read | GattDescriptorPermission.Write));
//            service.AddCharacteristic(_characteristic);

//            Log.Debug(_tag, $"Existing services [{_bluetoothServer.Services.Count}]: {_bluetoothServer.Services.Select(s => s.Uuid).Join()}");
            
//            _bluetoothServerCallback.CharacteristicReadRequest += _bluetoothServerCallback_CharacteristicReadRequest;
//            //_bluetoothServerCallback.NotificationSent += _bluetoothServerCallback_NotificationSent;
//            _bluetoothServerCallback.DeviceConnected += (o, e) => { };
//            _bluetoothServerCallback.DeviceDisconnected += (o, e) => { };

//            // Check if a service by that label is already running...
//            if (_bluetoothServer.Services.Any(s => s.Uuid == ServiceUUID))
//                // ... and stop it if it is.
//                _bluetoothServer.RemoveService(_bluetoothServer.Services.First(s => s.Uuid == ServiceUUID));
//            // Then add our own service.
//            _bluetoothServer.AddService(service);
            
//            Log.Debug(_tag, $"Server created ({service.Uuid})!");
//            System.Threading.Tasks.Task.Delay(500).Wait();
//            Log.Debug(_tag, $"Existing services [{_bluetoothServer.Services.Count}]: {_bluetoothServer.Services.Select(s => s.Uuid).Join()}");

//            myBluetoothLeAdvertiser = _bluetoothAdapter.BluetoothLeAdvertiser;

//            var builder = new AdvertiseSettings.Builder();
//            builder.SetAdvertiseMode(AdvertiseMode.LowLatency);
//            builder.SetConnectable(true);
//            builder.SetTimeout(0);
//            builder.SetTxPowerLevel(AdvertiseTx.PowerHigh);
//            AdvertiseData.Builder dataBuilder = new AdvertiseData.Builder();
//            dataBuilder.AddServiceUuid(Android.OS.ParcelUuid.FromString(ServiceUUID.ToString()));
//            dataBuilder.SetIncludeDeviceName(true);
//            dataBuilder.SetIncludeTxPowerLevel(false);

//            myBluetoothLeAdvertiser.StartAdvertising(builder.Build(), dataBuilder.Build(), new BleAdvertiseCallback());
            
//        }

//        private int _count = 0;
//        private Stopwatch _sw = new Stopwatch();

//        void _bluetoothServerCallback_NotificationSent(object sender, BleEventArgs e)
//        {
//            if (_count == 0)
//            {
//                _sw = new Stopwatch();
//                _sw.Start();
//            }

//            if (_count < 1000)
//            {
//                var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
//                var random = new Random();
//                var result = new string(
//                    Enumerable.Repeat(chars, 20)
//                        .Select(s => s[random.Next(s.Length)])
//                        .ToArray());
//                _characteristic.SetValue(result);

//                _bluetoothServer.NotifyCharacteristicChanged(e.Device, _characteristic, false);

//                _count++;

//            }
//            else
//            {
//                _sw.Stop();
//                Log.Info(_tag, $"Sent # {_count} notifcations. Total kb:{_count * 20 / 1000}. Time {_sw.Elapsed.TotalSeconds}(s). Throughput {_count * 20.0f / _sw.Elapsed.TotalSeconds} bytes/s");
//            }
//        }

//        private bool _notificationsStarted = false;

//        private int _readRequestCount = 0;
//        void _bluetoothServerCallback_CharacteristicReadRequest(object sender, BleEventArgs e)
//        { 
//            if (_readRequestCount % 5 == 0)
//            {
//                _notificationsStarted = !_notificationsStarted;
//                //_readRequestCount = 0;
//            }
//            else
//            {
//                _readRequestCount++;
//                Log.Info(_tag, $"Read req {_readRequestCount}");
//                e.Characteristic.SetValue($"Right on {_readRequestCount}!");
//                _bluetoothServer.SendResponse(e.Device, e.RequestId, GattStatus.Success, e.Offset,
//                    e.Characteristic.GetValue());
//                return;
//            }

//            if (_notificationsStarted)
//            {

//                Console.WriteLine("Started notifications!");

//                e.Characteristic.SetValue($"Start {_readRequestCount}!");
//                _bluetoothServer.SendResponse(e.Device, e.RequestId, GattStatus.Success, e.Offset,
//                    e.Characteristic.GetValue());
//                _bluetoothServer.NotifyCharacteristicChanged(e.Device, e.Characteristic, false);
//            }
//            else
//            {
//                Log.Info(_tag, "Stopped notifications!");
//                e.Characteristic.SetValue($"Stop {_readRequestCount}!");
//                _bluetoothServer.SendResponse(e.Device, e.RequestId, GattStatus.Success, e.Offset,
//                    e.Characteristic.GetValue());
//                //_bluetoothServer.NotifyCharacteristicChanged(e.Device, e.Characteristic, false);
//            }
//        }

//        public void Close()
//        {
//            _bluetoothServer.Close();
//            myBluetoothLeAdvertiser.StopAdvertising(new BleAdvertiseCallback());
//        }

//        private void NotifySubscribers(Characteristic characteristic)
//        {
//            if (Subscribers.Count == 0)
//            {
//                Log.Info(_tag, $"Notifying zero subscribers about {characteristic.CharacteristicName}.");
//                return;
//            }

//            Log.Info(_tag, $"Notifying {Subscribers.Count} subscribers about {characteristic.CharacteristicName}.");
//            foreach (var device in Subscribers)
//            {
//                var ctistic = _bluetoothServer.GetService(ServiceUUID).GetCharacteristic(characteristic.UUID);
//                _bluetoothServer.NotifyCharacteristicChanged(device, ctistic, false);
//            }
//        }


//    }

//    public class BleAdvertiseCallback : AdvertiseCallback
//    {
//        public override void OnStartFailure(AdvertiseFailure errorCode)
//        {
//            Console.WriteLine("Advertise start failure {0}", errorCode);
//            base.OnStartFailure(errorCode);
//        }

//        public override void OnStartSuccess(AdvertiseSettings settingsInEffect)
//        {
//            Console.WriteLine("Advertise start success {0}", settingsInEffect.Mode);
//            base.OnStartSuccess(settingsInEffect);
//        }
//    }

//    public class Characteristic
//    {
//        public string CharacteristicName;
//        public Guid GUID;
//        public Plugin.BLE.Abstractions.Contracts.ICharacteristic characteristic;
//        public virtual string StringValue { get { return null; } }
//        public virtual void Parse(string stringValue) { }
//        public virtual event EventHandler<EventArgs> OnValueChanged;
//        public UUID UUID { get => UUID.FromString(GUID.ToString()); }

//        public static Dictionary<string, Guid> GUIDs = new Dictionary<string, Guid>()
//        {
//            {"Name", Guid.Parse("BD482EF5-7C6B-40A3-BD18-7CD885F99F15")},
//            {"Roles", Guid.Parse("25C9AE93-5D4F-4E81-8696-CF006D9D8825")},
//            {"Perks", Guid.Parse("7DF949E8-8328-4A8C-8CD8-DE22D4194941")},
//            {"Status Effects", Guid.Parse("4DD034CB-6B11-44F1-9442-1E8048F17F52")},
//            {"Last Interaction", Guid.Parse("295247F3-FC4F-46BE-98C0-B0A3004598F7")},
//            {"Requested Data", Guid.Parse("86CA453C-2D87-4B60-A372-C7B94F894A72")}
//        };
//    }

//    public class Characteristic<T> : Characteristic
//    {
//        private T _value;
//        public override event EventHandler<EventArgs> OnValueChanged; // Has to be re-declared because we can only FIRE the event inside its actual owning class.
//        public T Value { get => _value; set { _value = value; OnValueChanged?.Invoke(this, EventArgs.Empty); } }
//        public override string StringValue => _value?.ToString() ?? string.Empty;

//        private static Func<string, T> parseFunction;

//        public Characteristic(CommsContact parent, string name)
//        {
//            CharacteristicName = name;
//            GUID = GUIDs.GetValueOrDefault(name);
//            parent.Characteristics.Add(name, this);
//        }

//        public override void Parse(string stringValue) { Value = parseFunction(stringValue); }

//        static Characteristic()
//        {
//            if (typeof(T) == typeof(string))
//                parseFunction = (s) => MiscUtil.Operator.Convert<string, T>(s);
//            else if (typeof(T) == typeof(int))
//                parseFunction = (s) => MiscUtil.Operator.Convert<int, T>(int.Parse(s));
//            else if (typeof(T) == typeof(float))
//                parseFunction = (s) => MiscUtil.Operator.Convert<float, T>(float.Parse(s));
//            else if (typeof(T) == typeof(double))
//                parseFunction = (s) => MiscUtil.Operator.Convert<double, T>(double.Parse(s));
//            else
//                parseFunction = (s) => throw new NotImplementedException();
//        }
//    }
//}