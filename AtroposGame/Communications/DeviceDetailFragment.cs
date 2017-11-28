using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Net.Wifi;
using Android.Net.Wifi.P2p;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Net;
using Environment = Android.OS.Environment;
using File = Java.IO.File;
using IOException = Java.IO.IOException;
using System.Threading;
using System.Collections.Generic;
using MiscUtil;
using Nito.AsyncEx;

namespace com.Atropos.Communications
{

    public partial class WiFiDirectActivity
    {
        protected static int ChooseFileResultCode = 20;
        private WifiPeer _device { get; set; }
        private WifiP2pInfo _info;
        private View DetailView;
        private CancellationTokenSource _cts;

        public void SetUpDetailView()
        {
            DetailView = FindViewById(Resource.Id.wifi_frag_detail);
            FindViewById<Button>(Resource.Id.btn_connect).Click += (sender, args) =>
                {
                    var config = new WifiP2pConfig
                        {
                            DeviceAddress = _device.MACaddress, 
                            Wps =
                                {
                                    Setup = WpsInfo.Pbc
                                }
                        };
                    if (_progressDialog != null && _progressDialog.IsShowing)
                        _progressDialog.Dismiss();

                    _progressDialog = ProgressDialog.Show(this, $"Connecting to {_device.Name} ({_device.MACaddress})", "Press back to cancel",
                                                          true, true);

                    Connect(config);
                };

            FindViewById<Button>(Resource.Id.btn_disconnect).Click += (sender, args) => 
                Disconnect();

            FindViewById<Button>(Resource.Id.btn_start_client).Click += (sender, args) =>
                {
                    //var intent = new Intent(Intent.ActionGetContent);
                    //intent.SetType("image/*");
                    //StartActivityForResult(intent, ChooseFileResultCode);

                    //// In original code, when we return from selecting an image in the above, we then start a service for the transfer of the selected file...
                    //var uri = data.Data;
                    //var statusText = _contentView.FindViewById<TextView>(Resource.Id.status_text);
                    //statusText.Text = "Sending: " + uri;
                    //Log.Debug(Tag, "Intent---------- " + uri);
                    //var serviceIntent = new Intent(Activity, typeof(FileTransferService));
                    //serviceIntent.SetAction(FileTransferService.ActionSendFile);
                    //serviceIntent.PutExtra(FileTransferService.ExtrasFilePath, uri.ToString());
                    //serviceIntent.PutExtra(FileTransferService.ExtrasGroupOwnerAddress,
                    //                       _info.GroupOwnerAddress.HostAddress);
                    //serviceIntent.PutExtra(FileTransferService.ExtrasGroupOwnerPort, 8988);
                    //Activity.StartService(serviceIntent);

                    //// THEN, in original code, that Activity that just got Started runs the below...
                    //var fileUri = intent.GetStringExtra(ExtrasFilePath);
                    //var host = intent.GetStringExtra(ExtrasGroupOwnerAddress);
                    //var port = intent.GetIntExtra(ExtrasGroupOwnerPort, 8988);

                    //dataBufferToSend = $"Test string {CumulativeInt++}";
                    //sendDataSignal.Set();
                    //TransmitString(dataBufferToSend);
                    Client.SendMessage($"Test string {CumulativeInt++}");
                };
        }
        private static int CumulativeInt = 0;

        protected Socket CommsSocket;
        protected WifiServer Server;
        protected WifiClient Client;
        public static WifiClient GetWifiClient { get { return _currentActivity?.Client; } }
        public void OnConnectionInfoAvailable(WifiP2pInfo info)
        {
            DismissProgressIndicator();

            _info = info;

            DetailView.Visibility = ViewStates.Visible;

            // The owner IP is now known.
            var view = FindViewById<TextView>(Resource.Id.group_owner);
            view.Text = Resources.GetString(Resource.String.group_owner_text)
                    + ((info.IsGroupOwner) ? Resources.GetString(Resource.String.yes)
                            : Resources.GetString(Resource.String.no));

            // InetAddress from WifiP2pInfo struct.
            view = FindViewById<TextView>(Resource.Id.group_ip);
            view.Text = "Group Owner IP - " + _info.GroupOwnerAddress.HostAddress;
            Log.Debug(Tag, $"Info received: Group formed [{_info.GroupFormed}], Owner address [{_info.GroupOwnerAddress}] <Host {_info.GroupOwnerAddress.HostAddress}>, Am Owner [{_info.IsGroupOwner}].");
            Toast.MakeText(this, $"P2P group formed ({((_info.IsGroupOwner) ? "as server" : "as client")})", ToastLength.Short).Show();

            //**FindViewById<Button>(Resource.Id.btn_start_client).Visibility = ViewStates.Visible;
            FindViewById(Resource.Id.btn_disconnect).Visibility = ViewStates.Visible;
            FindViewById<Button>(Resource.Id.btn_connect).Visibility = ViewStates.Gone;

            _cts = new CancellationTokenSource();

            if (!_info.GroupFormed)
            {
                Log.Error(Tag, $"Wifi P2P group not formed successfully.");
                return;
            }

            // After the group negotiation, we assign the group owner as the message-relay
            // server. The message server is a multiple-threaded, multiple-connection server
            // socket.
            if (_info.IsGroupOwner)
            {
                Server = new WifiServer(_cts.Token);
                Server.Listen();
            }
            #region Previous almost-working code...
            //ServerSocket serverSocket = null;
            //Task.Run(async () =>
            //{
            //    try
            //    {
            //        serverSocket = new ServerSocket(8988);
            //        serverSocket.ReuseAddress = true; // Might help...?  Nope, don't think it did.
            //        Log.Debug(Tag, "Server: Socket opened");
            //        //CommsSocket = await serverSocket.AcceptAsync();
            //        CommsSocket = serverSocket.Accept();
            //        Log.Debug(Tag, $"Server: Connection accepted to {CommsSocket.InetAddress.CanonicalHostName}");
            //        _manager?.RequestPeers(_channel, _currentActivity); // Update list, please! Otherwise "Invited" doesn't go away.

            //        OnDataReceived += MakeToastFromDataReceived;

            //        _device.InputStream = CommsSocket.InputStream;
            //        //**_device.OutputStream = CommsSocket.OutputStream;

            //        //await Task.WhenAll(IncomingDataLoop(CommsSocket.InputStream, _cts.Token),
            //        //                    OutgoingDataLoop(CommsSocket.OutputStream, _cts.Token));
            //        //await IncomingDataLoop(CommsSocket.InputStream, _cts.Token);
            //        //**TransmitString($"ACK {MyDevice.DeviceName} Connected (as Server)");
            //        await Task.Run(() => HandleIncomingData(_cts.Token));
            //        //await _cts.Token.AsTask();


            //        #region Old Code for reference
            //        //var f = new File(Environment.ExternalStorageDirectory + "/"
            //        //                 + Activity.PackageName + "/wifip2pshared-" + DateTime.Now.Ticks + ".jpg");
            //        //var dirs = new File(f.Parent);
            //        //if (!dirs.Exists())
            //        //    dirs.Mkdirs();
            //        //f.CreateNewFile();

            //        //Log.Debug(Tag, "Server: copying files " + f);
            //        //var inputStream = client.InputStream;
            //        //CopyFile(inputStream, new FileStream(f.ToString(), FileMode.OpenOrCreate));
            //        //var inputDatastream = new Java.IO.DataInputStream(CommsSocket.InputStream);
            //        //var outputDatastream = new Java.IO.DataOutputStream(CommsSocket.OutputStream);
            //        ////string result = "";
            //        //////char res;
            //        ////int res;
            //        //////while ((res = inputDatastream.ReadChar()) != '|')
            //        ////while ((res = inputDatastream.ReadChar()) != '|')
            //        ////        result += res;
            //        ////Task.Delay(1500).Wait();
            //        //return f.AbsolutePath;
            //        //return result;
            //        #endregion
            //    }
            //    catch (IOException e)
            //    {
            //        Log.Error(Tag +  "|OnConnectionInfo|AsServer", "IOException: " + e.Message);
            //        //return null;
            //    }
            //    catch (Exception e)
            //    {
            //        Log.Error(Tag +  "|OnConnectionInfo|AsServer", e.Message);
            //    }
            //    finally
            //    {
            //        DisconnectSocketStreams();
            //        serverSocket?.Close();
            //        FindViewById(Resource.Id.btn_disconnect).Visibility = ViewStates.Gone;
            //        FindViewById(Resource.Id.btn_start_client).Visibility = ViewStates.Gone;
            //    }
            //})
            //.ConfigureAwait(false);

            //#region Slightly less old, old code

            ////.ContinueWith(result =>
            ////{
            ////    if (result != null)
            ////    {
            ////        FindViewById<TextView>(Resource.Id.status_text).Text = "String received - " +
            ////                                                                            result.Result;
            ////        Toast.MakeText(_currentActivity, $"String received: {result.Result}", ToastLength.Short).Show();
            ////        //var intent = new Intent();
            ////        //intent.SetAction(Intent.ActionView);
            ////        //intent.SetDataAndType(Android.Net.Uri.Parse("file://" + result.Result), "image/*");
            ////        //Activity.StartActivity(intent);
            ////    }
            ////}); 
            //#endregion

            //}
            //else if (_info.GroupFormed)
            //{
            //    FindViewById<Button>(Resource.Id.btn_start_client).Visibility = ViewStates.Visible; //**
            //    CommsSocket = new Socket();
            //    // Extracting the core bits from the above indirections, then...
            //    var host = _info.GroupOwnerAddress.ToString().TrimStart('/');
            //    var port = 8988;
            //    var SocketTimeout = 5000;

            //    Task.Run(async () =>
            //    {
            //        Java.IO.DataOutputStream outputDatastream = null;
            //        try
            //        {
            //            Log.Debug(Tag, "Client: Opening socket.");
            //            CommsSocket.Bind(null);
            //            await Task
            //                .Delay(250) // To allow the server to be ready to receive *before* we send out our "okay, go" signal
            //                .ContinueWith(async (_) =>
            //                {
            //                    CommsSocket.Connect(new InetSocketAddress(host, port), SocketTimeout);
            //                    if (CommsSocket.IsConnected)
            //                        Log.Debug(Tag, $"Client: Connection accepted to {CommsSocket.InetAddress.CanonicalHostName}.");
            //                    else
            //                    {
            //                        Log.Debug(Tag, "Client: Connection unsuccessful.");
            //                        return;
            //                    }

            //                    OnDataReceived += MakeToastFromDataReceived;

            //                    //await Task.WhenAll(IncomingDataLoop(CommsSocket.InputStream, _cts.Token),
            //                    //                   OutgoingDataLoop(CommsSocket.OutputStream, _cts.Token));
            //                    //await IncomingDataLoop(CommsSocket.InputStream, _cts.Token);
            //                    //**TransmitString($"ACK {MyDevice.DeviceName} Connected (as Client)");
            //                    //**await Task.Run(() => HandleIncomingData(_cts.Token));
            //                    //await _cts.Token.AsTask();

            //                    _device.OutputStream = CommsSocket.OutputStream;
            //                    await OutgoingDataLoop(CommsSocket.OutputStream, _cts.Token);

            //                    //RunOnUiThread(() =>
            //                    //{
            //                    //    //FindViewById(Resource.Id.btn_disconnect).Visibility = ViewStates.Gone;
            //                    //    //FindViewById(Resource.Id.btn_start_client).Visibility = ViewStates.Gone;
            //                    //});

            //                })
            //                .ConfigureAwait(false);
            //        }
            //        catch (IOException e)
            //        {
            //            Log.Error(Tag +  "|TransmitString|AsClient", "IOException: " + e.Message);
            //        }
            //        catch (Exception e)
            //        {
            //            Log.Error(Tag +  "|TransmitString|AsClient", e.Message);
            //        }
            //        finally
            //        {
            //            outputDatastream?.Close();
            //            DisconnectSocketStreams();
            //        }
            //    });
            #endregion

            // Whether or not you're the server, everybody then signs on to that server as a client.
            Client = new WifiClient(_info.GroupOwnerAddress.ToString().TrimStart('/'), _cts.Token);

            var statusText = FindViewById<TextView>(Resource.Id.status_text);
            Client.OnMessageReceived += (o, e) =>
            {
                Log.Debug(Tag, $"Received '{e.Value}'.");
                RunOnUiThread(() =>
                {
                    statusText.Text = "String received - " + e.Value;
                });
            };
            Client.OnMessageSent += (o, e) =>
            {
                Log.Debug(Tag, $"Sent '{e.Value}'.");
                RunOnUiThread(() =>
                {
                    statusText.Text = "String sent - " + e.Value;
                });
            };
            Client.OnConnectionSuccess += (o, e) =>
            {
                Task.Run(() =>
                {
                    for (int i = 0; i < 20; i++)
                    {
                        if (Client.Addresses.ContainsKey(_device.Name)) break;
                        Task.Delay(150).Wait();
                    }
                    RunOnUiThread(() =>
                    {
                        FindViewById(Resource.Id.btn_start_client).Visibility = ViewStates.Visible;
                    });
                });
            };
            //Client.OnTeammateDetected += (o, e) =>
            //{
            //    var detectedTeammate = e.Value;
            //    if (Team.All.TeamMembers.Any(member => member.Name == detectedTeammate.Name))
            //    {
            //        var conflictedTeammate = Team.All.TeamMembers.First(member => member.Name == detectedTeammate.Name);
            //        conflictedTeammate.Roles = detectedTeammate.Roles;
            //        conflictedTeammate.IsMe = detectedTeammate.IsMe;
            //        conflictedTeammate.Client = detectedTeammate.Client;
            //    }
            //    else Team.All.Members.Add(detectedTeammate);
            //};
            Client.Connect(MyDevice.DeviceName);
        }

//        public void DisconnectSocketStreams()
//        {
//            OnDataReceived -= MakeToastFromDataReceived;

//            try
//            {
//                CommsSocket?.InputStream?.Close();
//                CommsSocket?.OutputStream?.Close();
//            }
//            catch (Java.Net.SocketException)
//            {
//                // Swallow these - if socket is already closed, that's fine.  It all depends on the timing of what the stop token triggers when.
//            }
            

//            if (CommsSocket != null)
//            {
//                if (CommsSocket.IsConnected)
//                {
//                    try
//                    {
//                        CommsSocket.Close();
//                    }
//                    catch (IOException e)
//                    {
//                        // Give up
//                        Log.Debug(Tag, "Gave up on closing socket " + e.StackTrace);
//                    }
//                }
//            }
//        }

//        public class DataReceivedEventArgs : EventArgs
//        {
//            public DateTime Timestamp;
//            public string Sender;
//            public string Message;
//        }
//        public event EventHandler<DataReceivedEventArgs> OnDataReceived;
//        public async Task IncomingDataLoop(Stream inputStream, CancellationToken stopToken)
//        {
//            //int resultChar;
//            int chunkSize = 128;
//            byte[] buffer = new byte[0];
//            var resultString = String.Empty;
//            var stopTask = new TaskCompletionSource<int>();
//            var inputDatastream = new Java.IO.DataInputStream(inputStream);
//            const string TerminatorChar = "Z";//'|';
//            byte res;

//            //using (stopToken.Register(() => { stopTask.SetCanceled(); }))
//            //{
//            await Task.Run(async () =>
//            {
//                while (!stopToken.IsCancellationRequested)
//                {
//                    //var result = await Task.WhenAny(inputDatastream.ReadAsync(), stopTask.Task);
//                    //if (result.IsCanceled) break;
//                    //buffer.Add((byte)result.Result);
//                    //var oldBuf = new List<byte>(buffer).ToArray(); // Makes a copy, not a reference
//                    //var newBuf = new byte[chunkSize];
//                    //inputDatastream.Read(newBuf, 0, chunkSize);
//                    //if (newBuf.Length == 0 || newBuf.Count(b => b > 0) == 0) { await Task.Delay(100); continue; }
//                    //buffer = new List<byte>(oldBuf).Concat(newBuf).ToArray();
//                    //resultString = Convert.ToBase64String(newBuf);

//                    res = 255;
//                    while (res > 0) // != TerminatorChar[0])
//                    {
//                        res = (byte)await inputDatastream.ReadAsync();
//                        Log.Debug(Tag, $"Received '{res}'.");
//                        resultString += Convert.ToBase64String(new byte[] { res });
//                    }
//                    Log.Debug(Tag, $"Received '{resultString}'.");
//                    //if (resultString.EndsWith(TerminatorChar))
//                    if (res == 0) // TerminatorChar[0]) // Which in this version should always be true
//                    {
//                        OnDataReceived?.Invoke(this,
//                            new DataReceivedEventArgs()
//                            {
//                                Timestamp = DateTime.Now,
//                                Sender = _device.Name,
//                                Message = resultString //resultString.TrimEnd(TerminatorChar[0])
//                            }
//                        );
//                        resultString = String.Empty;
//                        //buffer.Clear();
//                        //newBuf = new byte[0];
//                    }
//                    else
//                    {
//                        //Log.Debug(Tag, $"Received a non-terminated string, consisting of {resultString} (byte[] {newBuf}).");
//                    }
//                }
//            });
//            inputDatastream.Close();               
//        }

//        public void HandleIncomingData(CancellationToken stopToken)
//        {
//            //int resultChar;
//            //int chunkSize = 128;
//            byte[] buffer = new byte[0];
//            var resultString = String.Empty;
//            //var stopTask = new TaskCompletionSource<int>();
//            var inputDatastream = new Java.IO.DataInputStream(_device.InputStream);
//            //const string TerminatorChar = "Z";//'|';
//            int res;

//            try
//            {
//                while (!stopToken.IsCancellationRequested)
//                {
//                    //var result = await Task.WhenAny(inputDatastream.ReadAsync(), stopTask.Task);
//                    //if (result.IsCanceled) break;
//                    //buffer.Add((byte)result.Result);
//                    //var oldBuf = new List<byte>(buffer).ToArray(); // Makes a copy, not a reference
//                    //var newBuf = new byte[chunkSize];
//                    //inputDatastream.Read(newBuf, 0, chunkSize);
//                    //if (newBuf.Length == 0 || newBuf.Count(b => b > 0) == 0) { await Task.Delay(100); continue; }
//                    //buffer = new List<byte>(oldBuf).Concat(newBuf).ToArray();
//                    //resultString = Convert.ToBase64String(newBuf);

//                    res = 255;
//                    while (res > 0 && !stopToken.IsCancellationRequested) // != TerminatorChar[0])
//                    {
//                        res = inputDatastream.ReadChar();
//                        Log.Debug(Tag, $"Received '{res}' (aka {(char)res}).");
//                        resultString += (char)res;
//                    }
//                    Log.Debug(Tag, $"Received '{resultString}'.");
//                    //if (resultString.EndsWith(TerminatorChar))
//                    if (res == 0) // TerminatorChar[0]) // Which in this version should always be true (unless we've fired the stop token)
//                    {
//                        OnDataReceived?.Invoke(this,
//                            new DataReceivedEventArgs()
//                            {
//                                Timestamp = DateTime.Now,
//                                Sender = _device.Name,
//                                Message = resultString //resultString.TrimEnd(TerminatorChar[0])
//                        }
//                        );
//                        res = 255;
//                        resultString = String.Empty;
//                        //buffer.Clear();
//                        //newBuf = new byte[0];
//                    }
//                    else
//                    {
//                        //Log.Debug(Tag, $"Received a non-terminated string, consisting of {resultString} (byte[] {newBuf}).");
//                    }
//                }
//            }
//            catch (IOException e)
//            {
//                Log.Error(Tag +  "|HandleIncomingData", $"IOException: {e}");
//            }
//            catch (Exception e)
//            {
//                Log.Error(Tag +  "|HandleIncomingData", $"{e}");
//            }
//            finally
//            {
//                inputDatastream.Close();
//                Disconnect();
//            }
//        }

//        //private void ButteredLog(string message)
//        //{
//        //    Log.Debug(Tag, message);
//        //    try
//        //    {
//        //        Toast.MakeText(_currentActivity, message, ToastLength.Short).Show();
//        //    }
//        //    catch { } // Don't care, just don't die either.
//        //}

//        //private void ButteredError(string location, string message)
//        //{
//        //    Log.Error(Tag + location, message);
//        //    try
//        //    {
//        //        Toast.MakeText(_currentActivity, $"!({location})! {message}", ToastLength.Long).Show();
//        //    }
//        //    catch { } // Don't care, just don't die either.
//        //}

//        public void TransmitString(string message)
//        {
//            #region Transplanted code
//            CommsSocket = new Socket();
//            // Extracting the core bits from the above indirections, then...
//            var host = _info.GroupOwnerAddress.ToString().TrimStart('/');
//            //var host = _info.GroupOwnerAddress.HostAddress;
//            var port = 8988;
//            var SocketTimeout = 5000;

//            Task.Run(async () =>
//            {
//                Java.IO.DataOutputStream outputDatastream = null;
//                try
//                {
//                    Log.Debug(Tag, "Client: Opening socket.");
//                    CommsSocket.Bind(null);
//                    await Task
//                        .Delay(10)
//                        .ContinueWith((_) =>
//                        {
//                            CommsSocket.Connect(new InetSocketAddress(host, port), SocketTimeout);
//                            if (CommsSocket.IsConnected)
//                                Log.Debug(Tag, "Client: Connection accepted.");
//                            else
//                            {
//                                Log.Debug(Tag, "Client: Connection unsuccessful.");
//                                return;
//                            }

//                            OnDataReceived += MakeToastFromDataReceived;

//                            //await Task.WhenAll(IncomingDataLoop(CommsSocket.InputStream, _cts.Token),
//                            //                   OutgoingDataLoop(CommsSocket.OutputStream, _cts.Token));
//                            //await IncomingDataLoop(CommsSocket.InputStream, _cts.Token);
//                            //**TransmitString($"ACK {MyDevice.DeviceName} Connected (as Client)");
//                            //**await Task.Run(() => HandleIncomingData(_cts.Token));
//                            //await _cts.Token.AsTask();

//                            Log.Debug(Tag, "Initiating transmission");
//                            _device.OutputStream = CommsSocket.OutputStream;
//                            outputDatastream = new Java.IO.DataOutputStream(_device.OutputStream);
//                            outputDatastream.WriteChars(message);
//                            outputDatastream.WriteChar(0);
//                            Log.Debug(Tag, $"Transmitted {message}");
//                            //RunOnUiThread(() =>
//                            //{
//                            //    //FindViewById(Resource.Id.btn_disconnect).Visibility = ViewStates.Gone;
//                            //    //FindViewById(Resource.Id.btn_start_client).Visibility = ViewStates.Gone;
//                            //});
                            
//                        })
//                        .ConfigureAwait(false);
//                }
//                catch (IOException e)
//                {
//                    Log.Error(Tag +  "|TransmitString|AsClient", "IOException: " + e.Message);
//                }
//                catch (Exception e)
//                {
//                    Log.Error(Tag +  "|TransmitString|AsClient", e.Message);
//                }
//                finally
//                {
//                    outputDatastream?.Close();
//                    DisconnectSocketStreams();
//                }
//            });
//#endregion
//            //Task
//            //.Delay(10)
//            //.ContinueWith((_) =>
//            //{
//            //    Log.Debug(Tag, "Initiating transmission");
//            //    var outputDatastream = new Java.IO.DataOutputStream(_device.OutputStream);
//            //    outputDatastream.WriteChars(message);
//            //    outputDatastream.WriteChar(0);
//            //    Log.Debug(Tag, $"Transmitted {message}");
//            //    outputDatastream.Close();
//            //})
//            //.ConfigureAwait(false);
//        }

//        public void MakeToastFromDataReceived(object sender, DataReceivedEventArgs e)
//        {
//            RunOnUiThread(() =>
//            {
//                Log.Debug(Tag, $"Received '{e.Message}'.");
//                var statusText = WiFiDirectActivity._currentActivity.FindViewById<TextView>(Resource.Id.status_text);
//                statusText.Text = "String received - " + e.Message;
//            });
//            //Toast.MakeText(_currentActivity, e.Message, ToastLength.Long).Show();
//        }

//        private AsyncManualResetEvent sendDataSignal = new AsyncManualResetEvent();
//        private string dataBufferToSend = "";
//        public async Task OutgoingDataLoop(Stream outputStream, CancellationToken stopToken)
//        {
//            const string TerminatorChar = "Z"; //'|';

//            using (var outputDatastream = new Java.IO.DataOutputStream(outputStream))
//            {
//                await Task.Run(() =>
//                {
//                    while (!stopToken.IsCancellationRequested)
//                    {
//                        sendDataSignal.Wait(stopToken);
//                        if (!stopToken.IsCancellationRequested)
//                        //if (await sendDataSignal.WaitAsync().Before(stopToken))
//                        {
//                            //outputDatastream = new Java.IO.DataOutputStream(_device.OutputStream);
//                            Log.Debug(Tag, "Initiating transmission");
//                            outputDatastream.WriteChars(dataBufferToSend);
//                            outputDatastream.WriteChar(0);
//                            Log.Debug(Tag, $"Transmitted {dataBufferToSend}");

//                            //byte[] resultBytes = Convert.FromBase64String(dataBufferToSend); // + TerminatorChar);
//                            //await outputDatastream.WriteAsync(resultBytes);
//                            //await outputDatastream.WriteAsync(0);
//                            //outputDatastream.Write(resultBytes);
//                            //outputDatastream.WriteChars(dataBufferToSend + TerminatorChar);
//                            Task.Delay(100, stopToken).Wait(stopToken); // Do we halt the delay, or our waiting on it?  Doing both is thread-safer.
//                        }
//                        sendDataSignal.Reset();
//                    }
//                }); 
//            }
//        }

//        //// Not currently in use - comes from example program where the "send" op was to send a picture file.
//        //public static bool CopyData(Stream inputStream, Stream outputStream)
//        //{
//        //    var buf = new byte[1024];
//        //    try
//        //    {
//        //        int n;
//        //        while ((n = inputStream.Read(buf, 0, buf.Length)) != 0)
//        //            outputStream.Write(buf, 0, n);
//        //        outputStream.Close();
//        //        inputStream.Close();
//        //    }
//        //    catch (Exception e)
//        //    {
//        //        Log.Debug(Tag, e.ToString());
//        //        return false;
//        //    }
//        //    return true;
//        //}

        /// <summary>
        /// Updates the UI with device data
        /// </summary>
        /// <param name="device">the device to be displayed</param>
        public void ShowDetails(WifiP2pDevice device)
        {
            _device = device;
            DetailView.Visibility = ViewStates.Visible;
            var view = FindViewById<TextView>(Resource.Id.selected_device_name);
            view.Text = _device.Name;
            view = FindViewById<TextView>(Resource.Id.selected_device_info);
            view.Text = _device.ToString();

            if (_device.Status.IsOneOf(WifiP2pDeviceState.Connected, WifiP2pDeviceState.Invited))
            {
                FindViewById(Resource.Id.btn_connect).Visibility = ViewStates.Gone;
                FindViewById(Resource.Id.btn_disconnect).Visibility = ViewStates.Visible;
            }
            else
            {
                FindViewById(Resource.Id.btn_connect).Visibility = ViewStates.Visible;
                FindViewById(Resource.Id.btn_disconnect).Visibility = ViewStates.Gone;
            }
        }

        /// <summary>
        /// Clears the UI fields after a disconnect or direct mode disable operation.
        /// </summary>
        public void ResetViews()
        {
            //FindViewById<Button>(Resource.Id.btn_connect).Visibility = ViewStates.Visible;

            var view = FindViewById<TextView>(Resource.Id.selected_device_name);
            view.Text = string.Empty;
            view = FindViewById<TextView>(Resource.Id.selected_device_info);
            view.Text = string.Empty;
            view = FindViewById<TextView>(Resource.Id.group_owner);
            view.Text = string.Empty;
            view = FindViewById<TextView>(Resource.Id.status_text);
            view.Text = string.Empty;
            FindViewById<Button>(Resource.Id.btn_start_client).Visibility = ViewStates.Gone;
            DetailView.Visibility = ViewStates.Gone;
        }
    }
}