﻿//using System;

//using Android.Util;
//using Android.Bluetooth;
//using System.Collections.Generic;

//namespace Atropos.Communications
//{
//    public partial class BleServer
//    {
//        public class BleEventArgs : EventArgs
//        {
//            public BluetoothDevice Device { get; set; }
//            public GattStatus GattStatus { get; set; }
//            public BluetoothGattCharacteristic Characteristic { get; set; }
//            public byte[] Value { get; set; }
//            public int RequestId { get; set; }
//            public int Offset { get; set; }
//        }

//        public class BleGattServerCallback : BluetoothGattServerCallback
//        {
//            private string _tag = "BleGattServerCallback";
//            private BleServer _parent;

//            public event EventHandler<BleEventArgs> DeviceConnected;
//            public event EventHandler<BleEventArgs> DeviceDisconnected;
//            public event EventHandler<BleEventArgs> NotificationSent;
//            public event EventHandler<BleEventArgs> CharacteristicReadRequest;
//            public event EventHandler<BleEventArgs> CharacteristicWriteRequest;

//            public BleGattServerCallback(BleServer parent)
//            {
//                _parent = parent;
//            }

//            public override void OnCharacteristicReadRequest(BluetoothDevice device, int requestId, int offset,
//                BluetoothGattCharacteristic characteristic)
//            {
//                base.OnCharacteristicReadRequest(device, requestId, offset, characteristic);

//                Log.Info(_tag, "Read request from {0}", device.Name);

//                if (CharacteristicReadRequest != null)
//                {
//                    CharacteristicReadRequest(this, new BleEventArgs() { Device = device, Characteristic = characteristic, RequestId = requestId, Offset = offset });
//                }
//            }

//            public override void OnCharacteristicWriteRequest(BluetoothDevice device, int requestId, BluetoothGattCharacteristic characteristic,
//                bool preparedWrite, bool responseNeeded, int offset, byte[] value)
//            {
//                base.OnCharacteristicWriteRequest(device, requestId, characteristic, preparedWrite, responseNeeded, offset, value);

//                if (CharacteristicWriteRequest != null)
//                {
//                    CharacteristicWriteRequest(this, new BleEventArgs() { Device = device, Characteristic = characteristic, Value = value, RequestId = requestId, Offset = offset });
//                }
//            }

//            public override void OnConnectionStateChange(BluetoothDevice device, ProfileState status, ProfileState newState)
//            {
//                base.OnConnectionStateChange(device, status, newState);
//                Console.WriteLine("State changed to {0}", newState);

//                var bleArgs = new BleEventArgs() { Device = device };
//                if (newState == ProfileState.Connected) DeviceConnected?.Invoke(this, bleArgs);
//                else if (newState == ProfileState.Disconnected)
//                {
//                    DeviceDisconnected?.Invoke(this, bleArgs);
//                    _parent.Subscribers.Remove(device);
//                }
//            }

//            public override void OnNotificationSent(BluetoothDevice device, GattStatus status)
//            {
//                base.OnNotificationSent(device, status);

//                if (NotificationSent != null)
//                {
//                    NotificationSent(this, new BleEventArgs() { Device = device });
//                }
//            }

//            public override void OnDescriptorReadRequest(BluetoothDevice device, int requestId, int offset, BluetoothGattDescriptor descriptor)
//            {
//                //base.OnDescriptorReadRequest(device, requestId, offset, descriptor);

//                // Is it the mandatory Client Configuration descriptor?
//                if (descriptor.Uuid == MandatoryClientConfigDescriptorUUID)
//                {
//                    Log.Debug(_tag, "Config descriptor read.");
//                    byte[] returnValue;
//                    if (_parent.Subscribers.Contains(device))
//                    {
//                        returnValue = (byte[])BluetoothGattDescriptor.EnableNotificationValue;
//                    }
//                    else
//                    {
//                        returnValue = (byte[])BluetoothGattDescriptor.DisableNotificationValue;
//                    }

//                    _parent._bluetoothServer.SendResponse(device, requestId, GattStatus.Success, 0, returnValue);
//                }
//                else
//                {
//                    Log.Warn(_tag, "Unknown descriptor read request");
//                    _parent._bluetoothServer.SendResponse(device, requestId, GattStatus.Failure, 0, null);
//                }
//            }

//            public override void OnDescriptorWriteRequest(BluetoothDevice device, int requestId, BluetoothGattDescriptor descriptor, bool preparedWrite, bool responseNeeded, int offset, byte[] value)
//            {
//                //base.OnDescriptorWriteRequest(device, requestId, descriptor, preparedWrite, responseNeeded, offset, value);

//                if (descriptor.Uuid == MandatoryClientConfigDescriptorUUID)
//                {
//                    if (value == (byte[])BluetoothGattDescriptor.EnableNotificationValue)
//                    {
//                        Log.Debug(_tag, $"Subscribe device {device.Name} ({device.Address}) to notifications.");
//                        _parent.Subscribers.Add(device);
//                    }
//                    else if (value == (byte[])BluetoothGattDescriptor.DisableNotificationValue)
//                    {
//                        Log.Debug(_tag, $"Unsubscribe device {device.Name} ({device.Address}) from notifications.");
//                        _parent.Subscribers.Remove(device);
//                    }

//                    if (responseNeeded) _parent._bluetoothServer.SendResponse(device, requestId, GattStatus.Success, 0, null);
//                }
//                else
//                {
//                    Log.Warn(_tag, "Unknown desciptor write request");
//                    if (responseNeeded) _parent._bluetoothServer.SendResponse(device, requestId, GattStatus.Failure, 0, null);
//                }
//            }
//        }
//    }
    
//}