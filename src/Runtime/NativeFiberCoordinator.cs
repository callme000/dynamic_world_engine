using System;
using DynamicWorldEngine.Utils;

namespace DynamicWorldEngine.Runtime
{
    /// <summary>
    /// Enforces Inviolable Rule #2: Zero Native GTA Calls off Main Thread.
    /// Provides authoritative fiber confinement and thread isolation between
    /// background worker threads and GTA V's native C++ ScriptHookVDotNet fiber.
    /// Delegates to the authoritative GameThreadGate.
    /// </summary>
    public static class NativeFiberCoordinator
    {
        public static int MainThreadId
        {
            get { return GameThreadGate.MainThreadId; }
            set
            {
                if (value > 0)
                    GameThreadGate.Initialize(value);
                else
                    GameThreadGate.Reset();
            }
        }

        public static void Initialize(int managedThreadId)
        {
            GameThreadGate.Initialize(managedThreadId);
        }

        public static void Reset()
        {
            GameThreadGate.Reset();
        }

        /// <summary>
        /// Returns true ONLY if executing on the verified SHVDN script main thread.
        /// Returns false on all background/threadpool threads and in headless test harnesses.
        /// </summary>
        public static bool IsMainThread
        {
            get { return GameThreadGate.IsMainThread; }
        }

        /// <summary>
        /// Validates that the current thread is the main game thread.
        /// Returns false and logs a warning if called from a background thread.
        /// </summary>
        public static bool EnforceMainThread(string operationName)
        {
            return GameThreadGate.EnforceMainThread(operationName);
        }

        /// <summary>
        /// Strictly requires that the current thread is the main game thread.
        /// Throws InvalidOperationException if called off-thread or uninitialized.
        /// </summary>
        public static void RequireMainThread(string operationName)
        {
            GameThreadGate.RequireMainThread(operationName);
        }
    }
}
