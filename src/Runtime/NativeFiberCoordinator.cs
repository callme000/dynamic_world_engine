using System;
using System.Threading;
using DynamicWorldEngine.Utils;

namespace DynamicWorldEngine.Runtime
{
    /// <summary>
    /// Enforces Inviolable Rule #2: Zero Native GTA Calls off Main Thread.
    /// Provides authoritative fiber confinement and thread isolation between
    /// background worker threads and GTA V's native C++ ScriptHookVDotNet fiber.
    /// </summary>
    public static class NativeFiberCoordinator
    {
        private static int _mainThreadId = -1;

        public static int MainThreadId
        {
            get { return _mainThreadId; }
            set { _mainThreadId = value; }
        }

        public static void Initialize(int managedThreadId)
        {
            _mainThreadId = managedThreadId;
        }

        public static void Reset()
        {
            _mainThreadId = -1;
        }

        /// <summary>
        /// Returns true ONLY if executing on the verified SHVDN script main thread.
        /// Returns false on all background/threadpool threads and in headless test harnesses.
        /// </summary>
        public static bool IsMainThread
        {
            get
            {
                return _mainThreadId != -1 && Thread.CurrentThread.ManagedThreadId == _mainThreadId;
            }
        }

        /// <summary>
        /// Validates that the current thread is the main game thread.
        /// Returns false and logs a warning if called from a background thread.
        /// </summary>
        public static bool EnforceMainThread(string operationName)
        {
            if (!IsMainThread)
            {
                Log.Warn("FiberConfinement", "Blocked off-thread native GTA engine call in '{0}' on ManagedThreadId={1} (MainThreadId={2})",
                    operationName, Thread.CurrentThread.ManagedThreadId, _mainThreadId);
                return false;
            }
            return true;
        }
    }
}
