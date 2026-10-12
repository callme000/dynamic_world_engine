using System;

namespace DynamicWorldEngine.Runtime
{
    /// <summary>
    /// Implements the Stage-Before-Commit Ingress Invariant for physical GTA entity realization.
    /// Provides RAII / IDisposable transactional semantics: if any step during physical manifestation,
    /// dynamic state reconciliation, or durable disk persistence throws or fails to complete,
    /// exiting the scope without Commit() automatically triggers an immediate rollback deallocation
    /// via RuntimeExecutor.CleanUpEnvironment, guaranteeing zero orphaned ghost entities in GTA V.
    /// </summary>
    public class RealizationScope : IDisposable
    {
        private readonly RuntimeExecutor _runtime;
        private readonly string _environmentId;
        private bool _isCommitted;
        private bool _disposed;

        public RealizationScope(RuntimeExecutor runtime, string environmentId)
        {
            _runtime = runtime;
            _environmentId = environmentId;
            _isCommitted = false;
            _disposed = false;
        }

        public string EnvironmentId { get { return _environmentId; } }
        public bool IsCommitted { get { return _isCommitted; } }

        /// <summary>
        /// Marks the realization scope as successfully manifested and persisted.
        /// Once committed, disposing the scope will NOT clean up or evict the staged entities.
        /// </summary>
        public void Commit()
        {
            _isCommitted = true;
        }

        /// <summary>
        /// Explicitly rolls back staged entities if the scope has not been committed.
        /// </summary>
        public void Rollback()
        {
            if (!_isCommitted && _runtime != null && !string.IsNullOrEmpty(_environmentId))
            {
                try
                {
                    Console.WriteLine(string.Format("[RealizationScope] Realization aborted/rolled back for '{0}'. Evicting staged entities...", _environmentId));
                    _runtime.CleanUpEnvironment(_environmentId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("[RealizationScope] Error during rollback of '{0}': {1}", _environmentId, ex.Message));
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (!_isCommitted)
            {
                Rollback();
            }
        }
    }
}
