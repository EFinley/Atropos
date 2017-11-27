using Java.Util;
using Android.Hardware;
using System.Collections.Generic;
using Android.Runtime;

using System;
using System.Numerics;
using Android.App;
using Android.Content;
using System.Linq;
using System.Threading;
using Nito.AsyncEx;
using MiscUtil;
using System.Threading.Tasks;
using Android.Util;
using System.Runtime.CompilerServices;

namespace com.Atropos
{

    public interface IProvider : IActivator
    {
        Task WhenDataReady();
        void Proceed();
        DateTime Timestamp { get; }
        TimeSpan Interval { get; }
        TimeSpan RunTime { get; }
        SensorDelay Delay { get; set; }
    }

    public interface IProvider<T> : IProvider
    {
        T Data { get; }
    }

    public interface IVector3Provider : IProvider
    {
        Vector3 Vector { get; }
    }
    public interface IOrientationProvider : IProvider
    {
        Quaternion Quaternion { get; }
    }

    //public abstract class SensorProvider : Java.Lang.Object, IProvider, ISensorEventListener
    public abstract class SensorProvider : ActivatorBase, IProvider
    {
        //public static List<string> SensorRegistrationsList = new List<string>();
        //private string myRegistrationString;

        protected object synchronizationToken = new object();

        private class SensorListener : Java.Lang.Object, ISensorEventListener
        {
            private static SensorManager sensorManager;
            static SensorListener() { sensorManager = (SensorManager)Application.Context.GetSystemService(Context.SensorService); }
            public SensorListener(SensorType sensortype)
            {
                sensorType = sensortype;
                sensor = sensorManager.GetDefaultSensor(sensorType);
            }

            public SensorDelay sensorDelay = SensorDelay.Game;

            public SensorType sensorType;
            private Sensor sensor;
            // Not doing anything; required by interface, but irrelevant to us here.
            public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy) { }

            public delegate void JavaStyleEventHandler<T>(T e); // Java lacks the "object sender" convention, apparently.
            public event JavaStyleEventHandler<SensorEvent> SensorChanged;

            public virtual void OnSensorChanged(SensorEvent e)
            {
                if (e.Sensor.Type == sensor.Type)
                {
                    SensorChanged?.Invoke(e);
                }
            }

            public void StartListening()
            {
                sensorManager.RegisterListener(this, sensor, sensorDelay, maxReportLatencyUs: 50000);
                // The maxReportLatency allows it to batch some reads.  Should help responsiveness (I think).
                Res.NumSensors++;
            }

            public void StopListening()
            {
                sensorManager.UnregisterListener(this);
                Res.NumSensors--;
            }
        }

        //protected Sensor sensor;
        //protected static SensorManager sensorManager;
        //protected static SensorDelay chosenDelay = SensorDelay.Game;
        private SensorListener sensorListener;

        protected bool hasTakenData = false;
        protected DateTime startTime = DateTime.Now;
        public long nanosecondsElapsed;
        protected long startNanoseconds;
        protected long previousNanoseconds = (long)-5e7; // Fifty milliseconds "ago"

        //private CancellationTokenSource cts;
        //public CancellationToken StopToken { get { return cts?.Token ?? CancellationToken.None; } }
        //public void DependsOn(CancellationToken dependency, Activator.Options options = Activator.Options.Default)
        //{
        //    cts = CancellationTokenSource.CreateLinkedTokenSource(StopToken, dependency);
        //    cts.Token.Register(Deactivate);
        //}

        //public bool IsActive { get { return (cts != null); } }

        protected AsyncManualResetEvent dataReadyEvent = new AsyncManualResetEvent(true);

        /// <summary>
        /// [Caution] As noted in the AsyncEx documentation, this is not thread-safe and causes an inherent (if small) associated delay in execution.  Use sparingly!
        /// </summary>
        public bool IsDataReady { get { return dataReadyEvent.IsSet(); } }

        public virtual Task WhenDataReady() { return dataReadyEvent.WaitAsync(); }
        public virtual void Proceed() {
            dataReadyEvent.Reset();
        }

        public DateTime Timestamp
        {
            get
            {
                if (!hasTakenData) return DateTime.Now;
                return startTime + RunTime;
            }
        }
        public TimeSpan Interval
        {
            get
            {
                if (!hasTakenData) return TimeSpan.FromMilliseconds(20); // Default value - SensorDelay.Game - in real terms.
                return TimeSpan.FromMilliseconds((nanosecondsElapsed - previousNanoseconds) / 1e6);
            }
        }
        public TimeSpan RunTime
        {
            get
            {
                if (!hasTakenData) return TimeSpan.Zero;
                return TimeSpan.FromMilliseconds((nanosecondsElapsed - startNanoseconds) / 1e6);
            }
        }

        static SensorProvider()
        {
            //sensorManager = (SensorManager)Application.Context.GetSystemService(Context.SensorService);
        }

        private string callerName;
        public SensorProvider(SensorType sensorType, CancellationToken? externalStopToken = null, [CallerMemberName] string callerName = "")
        {
            //sensor = sensorManager.GetDefaultSensor(sensorType);
            sensorListener = new SensorListener(sensorType);
            //Activate(externalStopToken);
            if (externalStopToken != null) DependsOn(externalStopToken.Value);
            this.callerName = callerName;
            //Log.Debug("SensorProvider|Ctor", $"Creating a {this.GetType().Name} (for {Enum.GetName(typeof(SensorType), sensorType)}), from {callerName}, as sensor listener #{Res.NumSensors + 1}.");
        }

        ~SensorProvider()
        {
            //Log.Debug("SensorProvider|Dtor", $"Destroying a {this.GetType().Name}, from {callerName}.");
            Deactivate();
        }

        public SensorDelay Delay
        {
            get { return sensorListener?.sensorDelay ?? SensorDelay.Game; }
            set
            {
                if (sensorListener == null) return;
                if (IsActive)
                {
                    sensorListener.StopListening();
                    sensorListener.sensorDelay = value;
                    sensorListener.StartListening();
                }
                else sensorListener.sensorDelay = value;
            }
        }

        public override void Activate(CancellationToken? externalStopToken = null)
        {
            //DependsOn(externalStopToken ?? CancellationToken.None);
            if (IsActive)
            {
                if (externalStopToken != null)
                    Log.Warn("SensorProvider|Activate", "Caution - attempted activation with new external cancellation token, but we're already active.");
                return;
            }
            base.Activate(externalStopToken);

            sensorListener.SensorChanged += OnSensorChanged;
            sensorListener.StartListening();

            //sensorManager.RegisterListener(this, sensor, chosenDelay, maxReportLatencyUs: 50000);
            //// The maxReportLatency allows it to batch some reads.  Should help responsiveness (I think).
            //Res.NumSensors++;

            //SensorRegistrationsList.Add($"({this.GetType()}):{sensor.Type.ToString()}");
        }

        public override void Deactivate()
        {
            if (cts == null) return; // ALMOST the same thing as !IsActive, except that it'll allow entrance if cts.CancellationRequested is true (since cts being cancelled will typically result in Deactivate() being called!)
            base.Deactivate();
            sensorListener.StopListening();
            sensorListener.SensorChanged -= OnSensorChanged;
            //sensorManager.UnregisterListener(this);
            //Res.NumSensors--;
            //cts?.Cancel();
            //cts = null;
        }

        protected virtual void TakeTimestamps(SensorEvent e)
        {
            if (!hasTakenData)
            {
                startTime = DateTime.Now;
                startNanoseconds = e.Timestamp;
                hasTakenData = true;
            }
            previousNanoseconds = nanosecondsElapsed;
            nanosecondsElapsed = e.Timestamp;
        }

        //// Not doing anything; required by interface, but irrelevant to us here.
        //public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy) { }

        public virtual void OnSensorChanged(SensorEvent e)
        {
            //if (e.Sensor.Type == sensor.Type)
            //{
            //    TakeTimestamps(e);
            //    SensorChanged(e);
            //    dataReadyEvent.Set();
            //}
            TakeTimestamps(e);
            SensorChanged(e);
            dataReadyEvent.Set();
        }

        // REQUIRED to be implemented by derived classes (since otherwise what's the point?).
        protected abstract void SensorChanged(SensorEvent e);

    }

    public class NullProvider : IProvider
    {
        public SensorDelay Delay { get { return SensorDelay.Game; } set { } }
        public TimeSpan Interval { get { return TimeSpan.FromMilliseconds(20); } }
        public bool IsActive { get { return false; } }
        public TimeSpan RunTime { get { return TimeSpan.Zero; } }
        public CancellationToken StopToken { get { return CancellationToken.None; } }
        public DateTime Timestamp { get { return DateTime.Now; } }
        public void Activate(CancellationToken? externalStopToken = default(CancellationToken?)) { }
        public void Deactivate() { }
        public void DependsOn(CancellationToken token, Activator.Options options = Activator.Options.Default) { }
        public void Proceed() { }
        public Task WhenDataReady() { return Task.CompletedTask; }
    }

    public class NullProvider<T> : NullProvider, IProvider<T>
    {
        public T Data { get { return default(T); } }
    }

    //public static class SingletonProviders
    //{
    //    private class SingleUserProvider : ActivatorBase, IProvider
    //    {
    //        public SensorProvider InnerProvider;
    //        public AsyncManualResetEvent ProceedSignal = new AsyncManualResetEvent();

    //        public override void Activate(CancellationToken? externalStopToken = default(CancellationToken?))
    //        {
    //            // Register with the library, let it activate if necessary
    //            ((IProvider)InnerProvider).Activate(externalStopToken);
    //        }

    //        public override void Deactivate()
    //        {
    //            // Deregister with the library and let it perform deactivation
    //            ((IProvider)InnerProvider).Deactivate();
    //        }

    //        public void Proceed()
    //        {
    //            ProceedSignal.Set();
    //            // Reset happens when ALL of the subscribers' signals are set.
    //        }

    //        #region Implemented through InnerProvider
    //        public TimeSpan Interval
    //        {
    //            get
    //            {
    //                return ((IProvider)InnerProvider).Interval;
    //            }
    //        }


    //        public TimeSpan RunTime
    //        {
    //            get
    //            {
    //                return ((IProvider)InnerProvider).RunTime;
    //            }
    //        }

    //        public DateTime Timestamp
    //        {
    //            get
    //            {
    //                return ((IProvider)InnerProvider).Timestamp;
    //            }
    //        }

    //        public Task WhenDataReady()
    //        {
    //            return ((IProvider)InnerProvider).WhenDataReady();
    //        }
    //        #endregion
    //    }
    //    private static Dictionary<SensorType, List<SingleUserProvider>> AllProviders;

    //    public static IProvider Obtain(SensorType sensorType)
    //    {

    //    }
    //}

    public abstract class SensorProvider<T> : SensorProvider, IProvider<T> where T:struct
    {
        // Constructor inheritance must be explicit
        public SensorProvider(SensorType sensorType, CancellationToken? externalStopToken = null, [CallerMemberName] string callerName = "") : base(sensorType, externalStopToken, callerName) { }

        // Implementation of implicit typing (so a Vector3Provider can be accepted as a Vector3)
        protected virtual T toImplicitType()
        {
            throw new NotImplementedException("Must override this method in a derived class to use implicit conversion on it.");
        }
        public static implicit operator T(SensorProvider<T> source)
        {
            return source.toImplicitType();
        }
        public T Data { get { return toImplicitType(); } }
    }

    public abstract class MultiSensorProvider : ActivatorBase, IProvider
    {
        protected List<IProvider> providers = new List<IProvider>();
        protected IProvider keyProvider;
        protected object synchronizationToken = new object();

        //private CancellationTokenSource cts;
        //private HashSet<CancellationToken> externalTokens = new HashSet<CancellationToken>();
        //public CancellationToken StopToken { get { return cts?.Token ?? CancellationToken.None; } }
        //public void DependsOn(CancellationToken dependency, Activator.Options options = Activator.Options.Default)
        //{
        //    if (options == Activator.Options.Default)
        //    {
        //        externalTokens.Add(dependency);
        //        if (IsActive) dependency.Register(Deactivate);
        //    }
        //    else if (options == Activator.Options.RemoveDependency && externalTokens.Contains(dependency))
        //    {
        //        externalTokens.Remove(dependency);
        //        cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationTokenHelpers.Normalize(externalTokens).Token);
        //    }
        //    //cts = CancellationTokenSource.CreateLinkedTokenSource(StopToken, dependency);
        //    //cts.Token.Register(Deactivate);
        //}

        public TimeSpan Interval { get { return keyProvider?.Interval ?? TimeSpan.FromMilliseconds(20); } }
        //public bool IsActive { get { return (cts != null); } }
        public TimeSpan RunTime { get { return keyProvider?.RunTime ?? TimeSpan.Zero; } }
        public DateTime Timestamp { get { return keyProvider?.Timestamp ?? DateTime.Now; } }

        protected AsyncManualResetEvent dataReadyEvent = new AsyncManualResetEvent(true);

        /// <summary>
        /// [Caution] As noted in the AsyncEx documentation, this is not thread-safe and causes an inherent (if small) associated delay in execution.  Use sparingly!
        /// </summary>
        public bool IsDataReady { get { return dataReadyEvent.IsSet(); } }

        public virtual Task WhenDataReady() { return dataReadyEvent.WaitAsync(); }

        public virtual void Proceed()
        {
            dataReadyEvent.Reset();
            //foreach (var p in providers) p.Proceed(); // Arguable whether this should be signaled here, or in the loop; see CombinerLoop below.
        }
        
        public void AddProvider(IProvider newProvider)
        {
            providers.Add(newProvider);
            if (providers.Count == 1) keyProvider = providers[0];
            newProvider.DependsOn(StopToken);
        }

        public void RemoveProvider(IProvider tgtProvider)
        {
            providers.Remove(tgtProvider);
            if (ReferenceEquals(tgtProvider, keyProvider))
            {
                if (providers.Count == 0) keyProvider = null;
                else keyProvider = providers[0];
            }
        }

        //protected virtual Task DoWhenAllDataIsReadyAsync()
        //{
        //    //DoWhenAllDataIsReady();
        //    return Task.CompletedTask;
        //} // Override in a derived class to insert behaviour here.

        protected virtual void DoWhenAllDataIsReady() { }

        private async Task CombinerLoop()
        {
            while (IsActive && !StopToken.IsCancellationRequested)
            {
                Task allDataReady = Task.WhenAll(providers.Select(p => p.WhenDataReady()));
                //if (!await allDataReady.Before(StopToken)) break;

                //using (await new AsyncLock().LockAsync(StopToken))
                //{
                    await Task.WhenAny(allDataReady, StopToken.AsTask());

                    DoWhenAllDataIsReady();
                    //await DoWhenAllDataIsReadyAsync();

                    // Let our sources know they can send in their okays whenever.
                    foreach (var p in providers) p.Proceed();
                    // Now raise our OWN signal for anyone who might be listening.
                    dataReadyEvent.Set(); 
                //}
            }
        }

        public MultiSensorProvider(CancellationToken externalToken, params IProvider[] providerArgs)
        {
            if (providerArgs == null || providerArgs.Length == 0) return;
            foreach (var p in providerArgs) AddProvider(p);
            if (providers.Count > 0) keyProvider = providers[0];
            //Activate(externalToken);
        }
        public MultiSensorProvider(params IProvider[] providerArgs) : this(CancellationToken.None, providerArgs) { }
        // Overloads which allow the usual idiom of putting the CancellationToken optional-and-last; since params[] have to come after all other args, we need these to have explicit lists of args before.
        public MultiSensorProvider(IProvider provider1, CancellationToken externalToken) : this(externalToken, provider1) { }
        public MultiSensorProvider(IProvider provider1, IProvider provider2, CancellationToken externalToken) : this(externalToken, provider1, provider2) { }
        public MultiSensorProvider(IProvider provider1, IProvider provider2, IProvider provider3, CancellationToken externalToken) : this(externalToken, provider1, provider2, provider3) { }
        

        public override void Activate(CancellationToken? externalStopToken = null)
        {
            base.Activate(externalStopToken);

            foreach (var p in providers) p.Activate(StopToken);
            CombinerLoop().LaunchAsOrphan(StopToken, $"CombinerLoop({this.GetType().Name})");
        }

        public SensorDelay Delay
        {
            get { return keyProvider.Delay; }
            set
            {
                foreach (var provider in providers) provider.Delay = value;
            }
        }
    }

    public class MultiSensorProvider<T> : MultiSensorProvider, IProvider<T> where T : struct
    {
        public MultiSensorProvider(CancellationToken externalToken, params IProvider[] providerArgs) : base(externalToken, providerArgs) { }
        public MultiSensorProvider(params IProvider[] providerArgs) : base(providerArgs) { }

        protected virtual T toImplicitType()
        {
            throw new NotImplementedException("Must override this method in a derived class to use implicit conversion on it.");
        }
        public static implicit operator T(MultiSensorProvider<T> source)
        {
            return source.toImplicitType();
        }
        public T Data { get { return toImplicitType(); } }
    }

    public class OrientationSensorProvider : SensorProvider<Quaternion>, IOrientationProvider
    {
        /**
	     * The quaternion that holds the current rotation.
	     */
        //protected Orientation currentOrientation;
        protected Quaternion currentOrientation;
        protected float[] valuesArray = new float[4];

        public OrientationSensorProvider(SensorType sensType, CancellationToken? token = null, [CallerMemberName] string callerName = "") : base(sensType, token, callerName)
        {
            // Initialise with identity
            //currentOrientation = new Orientation { Frame = ReferenceFrame.WorldSpace };
            currentOrientation = Quaternion.Identity;
        }

        public virtual Quaternion Quaternion
        {
            get
            {
                if (currentOrientation.LengthSquared() == 0) throw new Exception($"Zero Quaternion value detected - no sensor would be so loony!");
                return currentOrientation;
            }
            protected set { currentOrientation = value; }
        }

        protected override Quaternion toImplicitType()
        {
            return Quaternion;
        }

        public virtual Matrix4x4 RotationMatrix
        {
            get { return Matrix4x4.CreateFromQuaternion(currentOrientation); }
            protected set { currentOrientation = Quaternion.CreateFromRotationMatrix(value); }
        }

        public virtual Vector3 EulerAngles
        {
            get { return currentOrientation.ToEulerAngles(); }
            protected set { currentOrientation = Quaternion.CreateFromYawPitchRoll(value.X, value.Y, value.Z); }
        }

        protected override void SensorChanged(SensorEvent e)
        {
            SensorManager.GetQuaternionFromVector(valuesArray, e.Values.ToArray());
            currentOrientation.W = valuesArray[0];
            currentOrientation.X = valuesArray[1];
            currentOrientation.Y = valuesArray[2];
            currentOrientation.Z = valuesArray[3];
        }

        // Also, for those cases where you just need one read off a sensor that's not necessarily running...
        public static async Task<Quaternion> ReadSingleValue(SensorType sensType)
        {
            var localProvider = new OrientationSensorProvider(sensType);
            await localProvider.WhenDataReady(); // First read is sometimes an issue...
            localProvider.Proceed();
            await localProvider.WhenDataReady();
            var result = localProvider.Quaternion;
            localProvider.Deactivate();
            return result;
        }
    }

    public class Vector3Provider : SensorProvider<Vector3>, IVector3Provider
    {
        /**
	     * The vector that holds the most recent sensor value
	     */
        protected Vector3 _currentVector;
        protected Vector3 currentVector
        {
            get
            {
                return _currentVector;
            }
            set
            {
                _currentVector = value;
            }
        }

        public Vector3Provider(SensorType sensType, CancellationToken? token = null, [CallerMemberName] string callerName = "") : base(sensType, token, callerName)
        {
            // Initialise with identifiable default
            currentVector = Vector3.UnitX * 0.01f;
        }

        public virtual Vector3 Vector
        {
            get
            {
                if (currentVector.LengthSquared() == 0) throw new Exception($"Zero Vector value detected - no sensor would be so loony!");
                return currentVector;
            }
            protected set {
                currentVector = value;
            }
        }

        protected override Vector3 toImplicitType()
        {
            return Vector;
        }

        protected override void SensorChanged(SensorEvent e)
        {
            _currentVector.X = e.Values[0];
            _currentVector.Y = e.Values[1];
            _currentVector.Z = e.Values[2];
        }

        // Also, for those cases where you just need one read off a sensor that's not necessarily running...
        public static async Task<Vector3> ReadSingleValue(SensorType sensType)
        {
            var localProvider = new Vector3Provider(sensType);
            await localProvider.WhenDataReady(); // First read is sometimes an issue...
            localProvider.Proceed();
            await localProvider.WhenDataReady();
            var result = localProvider.Vector;
            localProvider.Deactivate();
            return result;
        }
    }

    public interface IFrameShiftedProvider
    {
        Quaternion FrameShift { get; set; }
        Task SetFrameShiftFromCurrent();
        bool IsFrameShiftSet { get; }
    }

    // Joined marker interfaces... because you can't make a list of thing-that-X-and-Y, but you can make a list of thing-that-Z.
    public interface IFrameShiftedOrientationProvider : IFrameShiftedProvider, IOrientationProvider { }
    public interface IFrameShiftedVector3Provider : IFrameShiftedProvider, IVector3Provider { }
    public interface IFrameShiftedProvider<T> : IFrameShiftedProvider, IProvider<T> { }

    public static class FrameShiftFunctions
    {
        public static async Task<Quaternion> OrientationWhenReady(IOrientationProvider orientator)
        {
            var wasStarted = orientator.IsActive;
            if (!wasStarted) orientator.Activate();
            var stopW = new System.Diagnostics.Stopwatch();
            stopW.Start();
            while (orientator.Quaternion.IsIdentity)
            {
                await orientator.WhenDataReady();
                var quatStatus = (orientator.Quaternion.IsIdentity) ? "identity" : $"not identity: {orientator.Quaternion.ToEulerAngles():f1}.";
                Android.Util.Log.Debug("Frame shift setup", $"After {stopW.ElapsedMilliseconds:f1}ms, orientation is {quatStatus}.");
                orientator.Proceed();
            }
            var result = orientator.Quaternion;
            if (!wasStarted) orientator.Deactivate();
            return result;
        }

        public static bool CheckIsReady(IProvider subject)
        {
            return (subject as IFrameShiftedProvider)?.IsFrameShiftSet ?? true;
        }
    }

    public class FrameShiftedVector3Provider : Vector3Provider, IFrameShiftedVector3Provider
    {
        private Quaternion frameShift;
        public Quaternion FrameShift
        {
            get { return frameShift; }
            set { frameShift = value; }
        }

        public bool IsFrameShiftSet { get { return (!frameShift.IsIdentity && !frameShift.IsZero()); } }

        public FrameShiftedVector3Provider(SensorType sensType, Quaternion? frameSh = null, CancellationToken? token = null, [CallerMemberName] string callerName = "") : base(sensType, token, callerName)
        {
            frameShift = frameSh ?? Quaternion.Identity;
            if (frameShift.IsZero()) frameShift = Quaternion.Identity;
        }

        public async Task SetFrameShiftFromCurrent()
        {
            FrameShift = (await FrameShiftFunctions.OrientationWhenReady(new OrientationSensorProvider(SensorType.RotationVector))).Inverse();
        }

        public override Vector3 Vector
        {
            get
            {
                return (frameShift == Quaternion.Identity) ? currentVector // Fast-track to avoid unnecessary calculation (which would come out to currentVector anyway)
                    : Vector3.Transform(currentVector, frameShift);
            }
        }
    }
   
    public class FrameShiftedOrientationProvider : OrientationSensorProvider, IFrameShiftedOrientationProvider
    {
        private Quaternion frameShift;
        public virtual Quaternion FrameShift
        {
            get { return frameShift; }
            set { frameShift = value; }
        }

        public virtual bool IsFrameShiftSet { get { return !frameShift.IsIdentity; } }

        public FrameShiftedOrientationProvider(SensorType sensType, Quaternion? frameSh = null, CancellationToken? token = null, [CallerMemberName] string callerName = "") : base(sensType, token, callerName)
        {
            frameShift = frameSh ?? Quaternion.Identity;
        }

        public override Quaternion Quaternion
        {
            //get { return currentOrientation.Rotation * frameShift; }
            get { return currentOrientation * frameShift; }
        }

        public virtual async Task SetFrameShiftFromCurrent()
        {
            FrameShift = (await FrameShiftFunctions.OrientationWhenReady(this)).Inverse();
        }
    }

    public class FrameShiftedMultiSensorProvider<T> : MultiSensorProvider<T>, IFrameShiftedProvider where T : struct
    {
        protected IFrameShiftedOrientationProvider OProvider { get { return keyProvider as IFrameShiftedOrientationProvider; } }
        #region Delegating all of the IFrameShifted interface elements to the orientation provider
        public virtual Quaternion FrameShift
        {
            get { return OProvider.FrameShift; }
            set { OProvider.FrameShift = value; }
        }
        public virtual bool IsFrameShiftSet { get { return !OProvider.FrameShift.IsIdentity; } }
        public virtual Quaternion Quaternion
        {
            get { return OProvider.Quaternion; }
        }

        public virtual Task SetFrameShiftFromCurrent()
        {
            return OProvider.SetFrameShiftFromCurrent();
        }
        #endregion
        
        // Sigh.  Can't combine params with optional arguments - which is fair, since the syntax wouldn't really make sense at that point - but it means I need to manually create these overrides.
        // You can supply both a frame shift and a cancellation token...
        public FrameShiftedMultiSensorProvider(Quaternion frameShift, CancellationToken externalToken, IFrameShiftedOrientationProvider keyProv, params IProvider[] providerArgs)
            : base(externalToken, providerArgs.Prepend(keyProv).ToArray())
        {
            OProvider.FrameShift = frameShift * OProvider.FrameShift;
        }
        // Or just the frame shift...
        public FrameShiftedMultiSensorProvider(Quaternion frameShift, IFrameShiftedOrientationProvider keyProv, params IProvider[] providerArgs)
            : this(frameShift, CancellationToken.None, keyProv, providerArgs) { }
        // Or just the token...
        public FrameShiftedMultiSensorProvider(CancellationToken externalToken, IFrameShiftedOrientationProvider keyProv, params IProvider[] providerArgs) 
            : this(Quaternion.Identity, externalToken, keyProv, providerArgs) { }
        // ... or neither.
        public FrameShiftedMultiSensorProvider(IFrameShiftedOrientationProvider keyProv, params IProvider[] providerArgs) 
            : this(Quaternion.Identity, CancellationToken.None, keyProv, providerArgs) { }
    }

    public class GravityOrientationProvider : FrameShiftedOrientationProvider, IVector3Provider
    {
        public GravityOrientationProvider(Quaternion? frameShift = null, CancellationToken? externalStopToken = null, [CallerMemberName] string callerName = "") 
            : base(SensorType.Gravity, frameShift, externalStopToken, callerName)
        {
            normalizedGravityVector = Vector3.UnitZ;
            averageGravityVector = new AdvancedRollingAverageVector3(timeFrameInPeriods: 5, initialAverage: Vector3.UnitZ);
        }

        protected AdvancedRollingAverageVector3 averageGravityVector;
        private Vector3 normalizedGravityVector;
        protected override void SensorChanged(SensorEvent e)
        {
            normalizedGravityVector.X = e.Values[0];
            normalizedGravityVector.Y = e.Values[1];
            normalizedGravityVector.Z = e.Values[2];
            normalizedGravityVector = normalizedGravityVector.Normalize();
            averageGravityVector.Update(normalizedGravityVector);
        }

        private class CorrectionParameter { public Vector3 axis; public Quaternion correction; public Func<Vector3, float> discriminant; }
        private static List<CorrectionParameter> CorrectionParameters;
        static GravityOrientationProvider()
        {
            CorrectionParameters = new List<CorrectionParameter>()
            {
                //new CorrectionParameter() {axis =  Vector3.UnitX, discriminant = (v) =>   v.X  },
                //new CorrectionParameter() {axis = -Vector3.UnitX, discriminant = (v) => -(v.X) },
                //new CorrectionParameter() {axis =  Vector3.UnitY, discriminant = (v) =>   v.Y  },
                //new CorrectionParameter() {axis = -Vector3.UnitY, discriminant = (v) => -(v.Y) },
                new CorrectionParameter() {axis =  Vector3.UnitZ, discriminant = (v) =>   v.Z  } //,
                //new CorrectionParameter() {axis = -Vector3.UnitZ, discriminant = (v) => -(v.Z) }
            };
            foreach (var cp in CorrectionParameters)
            {
                cp.correction = cp.axis.QuaternionToGetTo(Vector3.UnitZ);
            }
        }

        public override async Task SetFrameShiftFromCurrent()
        {
            await base.SetFrameShiftFromCurrent();
            foreach (var cp in CorrectionParameters)
            {
                cp.correction *= FrameShift;
            }
            //averageGravityVector = new AdvancedRollingAverageVector(timeFrameInPeriods: 5, initialAverage: normalizedGravityVector);
        }

        public override Quaternion Quaternion
        {
            get
            {
                return RawQuaternion * FrameShift;
            }
        }

        protected Quaternion RawQuaternion
        {
            get
            {
                //CorrectionParameter CP = CorrectionParameters.OrderByDescending(cp => cp.discriminant(averageGravityVector)).FirstOrDefault();
                //return CP.axis.QuaternionToGetTo(averageGravityVector) * CP.correction;
                //return CP.correction * CP.axis.QuaternionToGetTo(averageGravityVector);
                return Vector3.UnitZ.QuaternionToGetTo(averageGravityVector);
            }
        }

        public Vector3 Vector { get { return averageGravityVector.Average.Normalize(); } }
    }

    public class ConsistentAxisAngleProvider : GravityOrientationProvider
    {
        private AdvancedRollingAverage<Vector3> AverageAxis;
        protected AdvancedRollingAverage<float> AngleSmoother;

        private int AcceptanceCount = 50;
        private float AcceptanceSigma;
        //public float ZeroedAngle { get; set; } = 0.0f;
        public Vector3 Axis { get; private set; }
        public bool AxisIsSet { get; private set; }

        public ConsistentAxisAngleProvider(float acceptanceSigma = 0.2f, Quaternion? frameShift = null, CancellationToken? externalStopToken = null) 
            : base(frameShift, externalStopToken)
        {
            AcceptanceSigma = acceptanceSigma;
            AverageAxis = AdvancedRollingAverage<Vector3>.Create<Vector3>(timeFrameInPeriods: 50, initialAverage: Vector3.UnitY, initialRelativeStdDev: 3.0f);
            AngleSmoother = AdvancedRollingAverage<float>.Create<float>(timeFrameInPeriods: 5, initialAverage: 0f);
            AxisIsSet = false;
            Log.Debug("Orientation Provider|ConsistentAngleAxis", "Creating new AngleAxis.");
        }

        private DateTime nextDisplayUpdateAt = DateTime.Now;
        private TimeSpan displayUpdateInterval = TimeSpan.FromMilliseconds(100);
        protected override void SensorChanged(SensorEvent e)
        {
            base.SensorChanged(e);
            if (RawQuaternion.IsIdentity || !IsFrameShiftSet) return;

            AverageAxis.Update(Oriented(CurrentAxis));
            AngleSmoother.Update((float)AngleDeg);

            if (!AxisIsSet && AverageAxis.StdDev < AcceptanceSigma && AverageAxis.NumPoints > AcceptanceCount)
            {
                Axis = AverageAxis.Average.Normalize();
                AxisIsSet = true;
                Log.Debug("Consistent axis provider", $"Arrived at consensus on axis, it's {Axis:f3}.");
            }

            //else if (!AxisIsSet && DateTime.Now > nextDisplayUpdateAt)
            //{
            //    Log.Debug("Consistent axis provider", $"AverageAxis estimate is ({AverageAxis.Average:f2}), with sigma {AverageAxis.StdDev:f3}.");
            //    nextDisplayUpdateAt += displayUpdateInterval;
            //}
        }

        public Vector3 CurrentAxis { get { return RawQuaternion.XYZ().Normalize(); } }

        public Vector3 Oriented(Vector3 BaseVector)
        {
            return BaseVector * DirectionSign(BaseVector);
        }

        public int DirectionSign(Vector3 BaseVector)
        {
            //return -Math.Sign(Vector3.UnitY.Dot(BaseVector));
            if (Quaternion.AsAngle() < 30.0) return 1;
            // Note above - not RawQuaternion because in this case I actually want to know the frame-shifted version.
            // Basically we end up with pretty bizarre stuff when it's near zero stance (the "direction" vector flies all over).
            else if (AxisIsSet) return Math.Sign(BaseVector.Dot(Axis));
            else if (AverageAxis.StdDev < 3.0 * AcceptanceSigma) return Math.Sign(BaseVector.Dot(AverageAxis));
            else return 1;
        }

        public float DotAxis
        {
            get
            {
                if (!AxisIsSet) return 1.0f;
                return Oriented(CurrentAxis).Dot(Axis);
            }
        }

        public float DotGravity
        {
            get
            {
                if (!AxisIsSet) return 0.0f;
                return Oriented(CurrentAxis).Dot(averageGravityVector.Average.Normalize());
            }
        }

        public double Angle
        {
            get
            {
                //if (!AxisIsSet) return RawQuaternion.AsAngle();
                ////var parallelAxis = (Axis.Dot(AverageAxis) > 0) ? AverageAxis.Average : -AverageAxis.Average;
                ////var correctionQuat = AverageAxis.Average.QuaternionToGetTo(Axis);
                ////var correctionQuat = Oriented(AverageAxis).QuaternionToGetTo(Axis);
                //var correctionQuat = AverageAxis.Average.QuaternionToGetTo(Axis);
                //var correctedQuat = RawQuaternion * correctionQuat;
                //return (correctedQuat.AsAngle() * DirectionSign(CurrentAxis)); // Returns between -pi and +pi (since a rotation by more than pi radians shows up as one by less than pi, in the other direction).
                return RawQuaternion.AsAngle() * DirectionSign(CurrentAxis);
            }
        }

        public double AngleDeg
        {
            get { return Angle * QuaternionExtensions.radToDeg; } // Returns between -180 and 180
        }

        public double Angle360
        {
            get { return Angle + 180.0; }
        }

        public double AngleSmoothed
        {
            get
            {
                return AngleSmoother.Average;
            }
        }

        public void SetNewAxis(Vector3 newAxis)
        {
            Axis = newAxis;
        }
    }

    public class CorrectedAccelerometerProvider : FrameShiftedVector3Provider
    {
        protected Vector3 previousAccelValue;

        public CorrectedAccelerometerProvider(SensorType sensType, Quaternion? frameSh = null, [CallerMemberName] string callerName = "") : base(sensType, frameSh, CancellationToken.None, callerName)
        {
            previousAccelValue = Vector3.Zero;
        }

        protected override void SensorChanged(SensorEvent e)
        {
            // Capture the vector, as normal.
            base.SensorChanged(e);

            // In the above we capture and set currentVector; here we're going to smooth it in the region near zero acceleration by blending with
            // the differences between successive measurements.
            //
            // Derivation useful to include here... because experimentally I find that at low accels the sensors' systematic error begins to dominate.
            // I wanted to use delta-A whenever that becomes true, but A whenever it's not, in a way which would not jump suddenly at
            // any point.  So eventually I came up with the idea of using two coefficients, P and Q, such that P + Q = 1, and where
            // A_net = (P * A) + (Q * delta-A).  For a suitable choice of Q [actually Q(A, delta-A)], I came up with Q = exp(- A^2 * delta-A^2),
            // where those are lengths-squared (easy to calculate).  So if, say, A and delta-A are both 0.5 (a fairly normal scale, given my testing),
            // then Q is around 90%, while if A is closer to 1.5 and delta-A is around one (also fairly typical for moderate motion), then Q is around 10%.
            //
            // Simplifying (1-Q) * A + Q * (A - A_previous) gets us A_effective = A - Q * A_previous.
            //
            // Arguably, since for a shorter timescale the delta will be comparatively smaller, the exponential term should also be scaled by
            // the ratio of the actual timestamp difference to the expected one (20ms).  But this works.
            //
            // Experimentally, the accel values returned by this variant go down as far as 0.25m/s2, while on my Nexus 5X the /systematic/ bias in the y-axis
            // accel value can be as high as 0.65m/s2 (!!!) when the device is being held vertical.

            float RawAccelVsAccelDifferenceSlidingCoefficient 
                = (float)Math.Exp((-currentVector.LengthSquared() * (currentVector - previousAccelValue).LengthSquared()) * (Interval.TotalMilliseconds / 20.0));
            var effectiveAccel = currentVector - RawAccelVsAccelDifferenceSlidingCoefficient * previousAccelValue;
            previousAccelValue = currentVector;
            _currentVector = effectiveAccel;
        }

        // Also, for those cases where you just need one read off a sensor that's not necessarily running...
        public static new async Task<Vector3> ReadSingleValue(SensorType sensType)
        {
            var localProvider = new Vector3Provider(sensType);
            await localProvider.WhenDataReady(); // First read is sometimes an issue...
            localProvider.Proceed();
            await localProvider.WhenDataReady();
            var result = localProvider.Vector;
            localProvider.Deactivate();
            return result;
        }
    }

    public class GravGyroOrientationProvider : FrameShiftedMultiSensorProvider<Quaternion>
    {
        protected GravityOrientationProvider GravProvider;
        protected Vector3Provider GyroProvider;
        public GravGyroOrientationProvider(Quaternion frameShift, CancellationToken externalToken)
        : base(frameShift, externalToken, 
              new GravityOrientationProvider(frameShift, externalToken),
              new Vector3Provider(SensorType.GyroscopeUncalibrated))
        {
            GravProvider = providers[0] as GravityOrientationProvider;
            GyroProvider = providers[1] as Vector3Provider;
        }
        public GravGyroOrientationProvider(Quaternion frameShift) : this(frameShift, CancellationToken.None) { }
        public GravGyroOrientationProvider(CancellationToken externalToken) : this(Quaternion.Identity, externalToken) { }
        public GravGyroOrientationProvider() : this(Quaternion.Identity, CancellationToken.None) { }

        public override Quaternion Quaternion
        {
            get
            {
                var rotationQuat = Quaternion.CreateFromAxisAngle(gravityAxis, totalAngle);
                return rotationQuat * GravProvider;
            }
        }

        #region Tracking the angular delta about the gravity axis...
        protected float totalAngle = 0f;
        protected float angleStep(Vector3 gravityVector, Vector3 gyroscopeVector) // Note - assumes a normalized gravityVector.
        {
            double dT = Interval.TotalSeconds;

            // Our compared-to-this-axis rotation velocity is the dot product of the two
            var gyroscopeRotationVelocity = Vector3.Dot(gravityVector, gyroscopeVector);

            // Integrate around this axis with the angular speed by the timestep
            // in order to get a delta rotation from this sample over the timestep
            return gyroscopeRotationVelocity * (float)dT / 2.0f; // From double thetaOverTwo (per Android source docs / Sensor Fusion demo)
        }
        private Vector3 gravityAxis { get { return GravProvider.Vector.Normalize(); } }
        #endregion

        protected override void DoWhenAllDataIsReady()
        {
            totalAngle += angleStep(gravityAxis, GyroProvider.Vector);
        }
    }

    // From the original Sensor Fusion code - TODO - provide proper credit.
    public class _CalibratedGyroscopeProvider : FrameShiftedOrientationProvider
    {
	    /**
	     * The quaternion that stores the difference that is obtained by the gyroscope.
	     * Basically it contains a rotational difference encoded into a quaternion.
	     * 
	     * To obtain the absolute orientation one must add this into an initial position by
	     * multiplying it with another quaternion
	     */
	    private Quaternion deltaQuaternion = new Quaternion();
	
	    /**
	     * This is a filter-threshold for discarding Gyroscope measurements that are below a certain level and
	     * potentially are only noise and not real motion. Values from the gyroscope are usually between 0 (stop) and
	     * 10 (rapid rotation), so 0.1 seems to be a reasonable threshold to filter noise (usually smaller than 0.1) and
	     * real motion (usually > 0.1). Note that there is a chance of missing real motion, if the use is turning the
	     * device really slowly, so this value has to find a balance between accepting noise (threshold = 0) and missing
	     * slow user-action (threshold > 0.5). 0.1 seems to work fine for most applications.
	     * 
	     */
	    private static double EPSILON = 0.25f;
	
	    /**
	     * Value giving the total velocity of the gyroscope (will be high, when the device is moving fast and low when
	     * the device is standing still). This is usually a value between 0 and 10 for normal motion. Heavy shaking can
	     * increase it to about 25. Keep in mind, that these values are time-depended, so changing the sampling rate of
	     * the sensor will affect this value!
	     */
	    private double gyroscopeRotationVelocity = 0;
	
	    /**
	     * Initialises a new CalibratedGyroscopeProvider
	     * 
	     * @param sensorManager The android sensor manager
	     */
	    public _CalibratedGyroscopeProvider(Quaternion? frameShift = null, CancellationToken? token = null) 
            : base(SensorType.Gyroscope, frameShift, token)
        {

	    }
	
	    protected override void SensorChanged(SensorEvent e) 
        {
	        if (Interval > TimeSpan.Zero)
            {
                double dT = Interval.TotalSeconds;
                // Axis of the rotation sample, not normalized yet.
                Vector3 rotationVector = new Vector3(e.Values[0], e.Values[1], e.Values[2]);

                // Calculate the angular speed of the sample
                gyroscopeRotationVelocity = rotationVector.Length();

                // Normalize the rotation vector if it's big enough to get the axis
                if (gyroscopeRotationVelocity > EPSILON)
                    { rotationVector = rotationVector.Normalize(); }
	
	            // Integrate around this axis with the angular speed by the timestep
	            // in order to get a delta rotation from this sample over the timestep
	            // We will convert this axis-angle representation of the delta rotation
	            // into a quaternion before turning it into the rotation matrix.
	            double thetaOverTwo = gyroscopeRotationVelocity * dT / 2.0f;
	            //double sinThetaOverTwo = Math.Sin(thetaOverTwo);
	            //double cosThetaOverTwo = Math.Cos(thetaOverTwo);
                deltaQuaternion = Quaternion.CreateFromAxisAngle(rotationVector, (float)thetaOverTwo);
	
	            // Move current calculated orientation
                this.Quaternion = deltaQuaternion * this.Quaternion;
	        }
	    }
	}
}