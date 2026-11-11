using System;
using System.Threading;
using DynamicWorldEngine.Utils;

namespace DynamicWorldEngine.Runtime
{
    /// <summary>
    /// Authoritative gate for GTA V SHVDN script thread execution.
    /// Enforces Invariants #1 & #2:
    /// 1. GTA native operations must execute exclusively through the correct SHVDN script-thread context.
    /// 2. An uninitialized or unknown thread identity must deny native execution, never grant permission.
    /// </summary>
    public static class GameThreadGate
    {
        private static int _mainThreadId = -1;
        private static bool _isInitialized = false;

        public static bool IsInitialized
        {
            get { return _isInitialized && _mainThreadId != -1; }
        }

        public static int MainThreadId
        {
            get { return _mainThreadId; }
        }

        public static void Initialize(int managedThreadId)
        {
            if (managedThreadId <= 0)
            {
                throw new ArgumentException("ManagedThreadId must be a positive thread identifier.", "managedThreadId");
            }
            _mainThreadId = managedThreadId;
            _isInitialized = true;
        }

        public static void Reset()
        {
            _mainThreadId = -1;
            _isInitialized = false;
        }

        /// <summary>
        /// Returns true ONLY if executing on the verified SHVDN script main thread.
        /// Returns false on all background/threadpool threads and in uninitialized / headless test harnesses.
        /// An uninitialized or unknown thread identity always denies native execution.
        /// </summary>
        public static bool IsMainThread
        {
            get
            {
                return _isInitialized && _mainThreadId != -1 && Thread.CurrentThread.ManagedThreadId == _mainThreadId;
            }
        }

        /// <summary>
        /// Validates that the current thread is the main game thread.
        /// Returns false and logs a warning if called from an unauthorized or uninitialized thread.
        /// </summary>
        public static bool EnforceMainThread(string operationName)
        {
            if (!IsMainThread)
            {
                Log.Warn("GameThreadGate", "Blocked native GTA engine call in '{0}' on ManagedThreadId={1} (AuthorizedMainThreadId={2}, Initialized={3})",
                    operationName, Thread.CurrentThread.ManagedThreadId, _mainThreadId, _isInitialized);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Strictly requires that the current thread is the main game thread.
        /// Throws InvalidOperationException if called off-thread or uninitialized.
        /// </summary>
        public static void RequireMainThread(string operationName)
        {
            if (!IsMainThread)
            {
                string msg = string.Format("Off-thread native GTA access violation: '{0}' attempted on ManagedThreadId={1} (AuthorizedMainThreadId={2}, Initialized={3}). Native operations must execute exclusively on the SHVDN script thread.",
                    operationName, Thread.CurrentThread.ManagedThreadId, _mainThreadId, _isInitialized);
                Log.Error("GameThreadGate", null, msg);
                throw new InvalidOperationException(msg);
            }
        }

        /// <summary>
        /// Executes a native function safely with main-thread gate enforcement.
        /// If off-thread, returns fallback.
        /// </summary>
        public static T Execute<T>(string operationName, Func<T> nativeOperation, T fallback = default(T))
        {
            if (!EnforceMainThread(operationName))
            {
                return fallback;
            }
            return nativeOperation();
        }

        /// <summary>
        /// Executes a native action safely with main-thread gate enforcement.
        /// </summary>
        public static bool Execute(string operationName, Action nativeOperation)
        {
            if (!EnforceMainThread(operationName))
            {
                return false;
            }
            nativeOperation();
            return true;
        }
    }
}
