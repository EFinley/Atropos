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

namespace Atropos
{
    public interface IActivator
    {
        void Activate(CancellationToken? externalStopToken = null);
        void Deactivate();
        CancellationToken StopToken { get; }
        bool IsActive { get; }
        void DependsOn(CancellationToken token, Activator.Options options = Activator.Options.Default);
    }

    public interface IResumer : IActivator
    {
        void Suspend();
        void Resume();
        bool IsSuspended { get; } 
    }

    public interface IActivatable
    {
        IActivator Activator { get; }
        void OnActivate();
        void OnDeactivate();
    }

    public interface IResumable : IActivatable
    {
        new IResumer Activator { get; }
        void OnSuspend();
        void OnResume();
    }

    public static class Activator
    {
        public static void CreateDependency(IActivator A, IActivator B)
        {
            A.DependsOn(B.StopToken);
        }
        public static void CreateDependency(IActivatable A, IActivator B)
        {
            A.Activator.DependsOn(B.StopToken);
        }
        public static void CreateDependency(IActivator A, IActivatable B)
        {
            A.DependsOn(B.Activator.StopToken);
        }
        public static void CreateDependency(IActivatable A, IActivatable B)
        {
            A.Activator.DependsOn(B.Activator.StopToken);
        }

        public enum Options
        {
            Default,
            RemoveDependency
        }
    }

    public abstract class ActivatorBase : IActivator
    {
        protected CancellationTokenSource cts;
        private HashSet<CancellationToken> externalTokens = new HashSet<CancellationToken>();
        private Dictionary<CancellationToken, IDisposable> tokenRegistrations = new Dictionary<CancellationToken, IDisposable>();
        public CancellationToken StopToken
        {
            get
            {
                return cts?.Token ?? CancellationToken.None;
            }
            set
            {
                DependsOn(value);
            }
        }

        //protected abstract void OnActivation();
        public virtual void Activate(CancellationToken? externalStopToken = null)
        {
            DependsOn(externalStopToken ?? CancellationToken.None);
            if (IsActive) return;

            CreateCTS();
            //OnActivation();
        }

        ~ActivatorBase()
        {
            Deactivate();
        }

        //protected abstract void OnDeactivation();
        public virtual void Deactivate()
        {
            //OnDeactivation();
            cts?.Cancel();
            externalTokens.Clear();
            foreach (var registration in tokenRegistrations.Values) registration.Dispose();
            tokenRegistrations.Clear();
            cts = null;
        }

        public void DependsOn(CancellationToken dependency)
        {
            DependsOn(dependency, Activator.Options.Default);
        }

        public void DependsOn(CancellationToken dependency, Activator.Options options)
        {
            if (options == Activator.Options.Default && !externalTokens.Contains(dependency))
            {
                externalTokens.Add(dependency);
                if (IsActive) tokenRegistrations.Add(dependency, dependency.Register(Deactivate)); // If already running, don't re-create the CTS, just add this dependency as a stand-alone registration.
            }
            else if (options == Activator.Options.RemoveDependency && externalTokens.Contains(dependency))
            {
                externalTokens.Remove(dependency);
                if (tokenRegistrations.ContainsKey(dependency)) tokenRegistrations[dependency].Dispose(); // Causes it to become unregistered once more.
                CreateCTS();
                //cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationTokenHelpers.Normalize(externalTokens).Token);
            }

            //var currentToken = cts?.Token ?? CancellationToken.None;
            //cts = CancellationTokenSource.CreateLinkedTokenSource(token, currentToken);
            //cts.Token.Register(Deactivate);
        }

        private void CreateCTS()
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationTokenHelpers.Normalize(externalTokens).Token);
            tokenRegistrations.Clear();
            tokenRegistrations.Add(cts.Token, cts.Token.Register(Deactivate));
        }

        public bool IsActive { get { return (cts != null && !cts.IsCancellationRequested); } }
    }

    public abstract class ResumableBase : ActivatorBase, IResumer
    {
        public bool IsSuspended { get; private set; }
        private AsyncManualResetEvent signalFlag = new AsyncManualResetEvent();

        protected abstract void OnSuspend();
        public void Suspend()
        {
            IsSuspended = true;
            OnSuspend();
        }

        protected abstract void OnResume();
        public void Resume()
        {
            if (!IsActive) Activate();
            OnResume();
            IsSuspended = false;
        }
    }
}