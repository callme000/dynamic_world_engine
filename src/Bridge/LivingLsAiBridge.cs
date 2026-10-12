using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Bridge
{
    /// <summary>
    /// Transport & Bridge Layer (Layer 1):
    /// Ingests live world state and NPC facts into Living LS AIs (LLAI v4.5) via its custom rules protocol.
    /// Strictly maintains downward-only dependencies (depends solely on Database layer models/store).
    /// </summary>
    public class LivingLsAiBridge
    {
        private const int MaxProtocolCharacterCap = 8000;
        private const int SafeCharacterBudget = 7500;
        private const int MinAlphanumericRequirement = 6;
        private const string RulesFileName = "LLAI CUSTOM RULES.txt";

        private readonly WorldMemoryStore _store;
        private readonly object _syncLock = new object();
        private string _resolvedRulesFilePath;
        private DateTime _lastSyncTime = DateTime.MinValue;
        private readonly TimeSpan _minSyncInterval = TimeSpan.FromMilliseconds(1000);
        private bool _isDirty = false;
        private HashSet<int> _pendingActiveIds = null;

        public LivingLsAiBridge(WorldMemoryStore store, string livingLsAisFolderPath = null)
        {
            _store = store ?? new WorldMemoryStore();
            ResolveCustomRulesPath(livingLsAisFolderPath);
        }

        /// <summary>
        /// Resolves the exact location of LLAI CUSTOM RULES.txt deterministically without heuristic guesswork.
        /// Conforms to LivingLSAIs.LivingLSAIs.ResolveCustomRulesPath reverse-engineered search hierarchy.
        /// </summary>
        private void ResolveCustomRulesPath(string customHintPath)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Scenario 1: User/Test supplied an explicit custom hint path
            if (!string.IsNullOrEmpty(customHintPath))
            {
                // Direct file path specified
                if (customHintPath.EndsWith(RulesFileName, StringComparison.OrdinalIgnoreCase) ||
                    customHintPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    _resolvedRulesFilePath = Path.GetFullPath(customHintPath);
                    return;
                }

                // Directory specified: check standard LLAI sub-paths in priority order
                string dirHint = Path.GetFullPath(customHintPath);
                string candidateBridge = Path.Combine(dirHint, "LLAI SERVER", "LLAIBridge", RulesFileName);
                if (File.Exists(candidateBridge))
                {
                    _resolvedRulesFilePath = candidateBridge;
                    return;
                }

                string candidateBridgeDirect = Path.Combine(dirHint, "LLAIBridge", RulesFileName);
                if (File.Exists(candidateBridgeDirect))
                {
                    _resolvedRulesFilePath = candidateBridgeDirect;
                    return;
                }

                string candidateRoot = Path.Combine(dirHint, RulesFileName);
                if (File.Exists(candidateRoot))
                {
                    _resolvedRulesFilePath = candidateRoot;
                    return;
                }

                // If none exist on disk yet, anchor deterministically inside the provided hint path
                if (dirHint.EndsWith("LLAIBridge", StringComparison.OrdinalIgnoreCase))
                {
                    _resolvedRulesFilePath = Path.Combine(dirHint, RulesFileName);
                }
                else if (dirHint.EndsWith("LLAI SERVER", StringComparison.OrdinalIgnoreCase))
                {
                    _resolvedRulesFilePath = Path.Combine(dirHint, "LLAIBridge", RulesFileName);
                }
                else
                {
                    _resolvedRulesFilePath = candidateBridge;
                }
                return;
            }

            // Scenario 2: Standard GTA V / SHVDN search hierarchy
            string[] standardCandidates = new string[]
            {
                Path.Combine(baseDirectory, "scripts", "LLAI SERVER", "LLAIBridge", RulesFileName),
                Path.Combine(baseDirectory, "LLAI SERVER", "LLAIBridge", RulesFileName),
                Path.Combine(baseDirectory, "2234fb-LivingLSAIs4.5", "LLAI SERVER", "LLAIBridge", RulesFileName),
                Path.Combine(baseDirectory, "scripts", "LLAIBridge", RulesFileName),
                Path.Combine(baseDirectory, "scripts", RulesFileName),
                Path.Combine(baseDirectory, RulesFileName)
            };

            foreach (var cand in standardCandidates)
            {
                if (File.Exists(cand))
                {
                    _resolvedRulesFilePath = cand;
                    return;
                }
            }

            // Scenario 3: Canonical default when no rules file exists yet
            string scriptsDir = Path.Combine(baseDirectory, "scripts");
            if (Directory.Exists(scriptsDir))
            {
                _resolvedRulesFilePath = Path.Combine(scriptsDir, "LLAI SERVER", "LLAIBridge", RulesFileName);
            }
            else
            {
                _resolvedRulesFilePath = Path.Combine(baseDirectory, "LLAI SERVER", "LLAIBridge", RulesFileName);
            }
        }

        /// <summary>
        /// Schedules or immediately synchronizes NPC facts to the LLAI rules file with rate-limiting.
        /// </summary>
        public bool SyncAllNpcFactsToCustomRules(IEnumerable<int> activeNpcIds = null, bool force = false)
        {
            lock (_syncLock)
            {
                if (activeNpcIds != null)
                {
                    _pendingActiveIds = new HashSet<int>(activeNpcIds);
                }

                DateTime now = DateTime.UtcNow;
                if (!force && (now - _lastSyncTime) < _minSyncInterval)
                {
                    _isDirty = true;
                    return true; // Throttled: marked dirty, will be flushed on next scheduled tick
                }

                _isDirty = false;
                _lastSyncTime = now;
                return SyncToDiskFile(_pendingActiveIds);
            }
        }

        /// <summary>
        /// Flushes any pending dirty syncs once the minimum sync interval has elapsed.
        /// </summary>
        public void FlushIfDirty()
        {
            lock (_syncLock)
            {
                if (_isDirty && (DateTime.UtcNow - _lastSyncTime) >= _minSyncInterval)
                {
                    _isDirty = false;
                    _lastSyncTime = DateTime.UtcNow;
                    SyncToDiskFile(_pendingActiveIds);
                }
            }
        }

        /// <summary>
        /// Generates perfectly compliant LLAI v4.5 custom rules and writes them to disk atomically.
        /// Adheres strictly to the reverse-engineered protocol specifications:
        /// - Comment prefix '#' for non-rule metadata
        /// - Safe character cap under 8,000 characters
        /// - Guaranteed > 5 alphanumeric characters to prevent deactivation
        /// - Atomic file write via temporary swap to prevent game-tick read tearing
        /// </summary>
        public bool SyncToDiskFile(IEnumerable<int> activeNpcIds = null)
        {
            lock (_syncLock)
            {
                try
                {
                    var allNpcs = _store.GetAllNpcs();
                    List<NpcRecord> npcsToSync;

                    if (activeNpcIds != null && activeNpcIds.Any())
                    {
                        HashSet<int> filter = (activeNpcIds as HashSet<int>) ?? new HashSet<int>(activeNpcIds);
                        npcsToSync = allNpcs.Where(n => filter.Contains(n.NpcId)).ToList();
                    }
                    else
                    {
                        // Fallback to active/alive recent NPCs (up to 10)
                        npcsToSync = allNpcs.Where(n => n.IsAlive).OrderByDescending(n => n.CreatedAt).Take(10).ToList();
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine("# ══════════════════════════════════════════════════════════════════════");
                    sb.AppendLine("#  DYNAMIC WORLD ENGINE - LIVE WORLD FACTS (AUTO-INJECTED)");
                    sb.AppendLine("#  Mod Target: Living LS AIs v4.5 | Protocol: Strict Line Feed");
                    sb.AppendLine("# ══════════════════════════════════════════════════════════════════════");
                    sb.AppendLine();

                    int injectedCount = 0;
                    foreach (var npc in npcsToSync)
                    {
                        string fact = _store.FormatNpcContextForLivingLSAIs(npc.NpcId);
                        if (!string.IsNullOrEmpty(fact))
                        {
                            // Enforce safe character cap (under LLAI 8,000 char threshold)
                            if (sb.Length + fact.Length + 4 > SafeCharacterBudget)
                            {
                                Console.WriteLine("[LivingLsAiBridge] Character budget limit reached ({0}/{1} chars). Truncating further NPC entries.",
                                    sb.Length, MaxProtocolCharacterCap);
                                break;
                            }

                            sb.AppendLine(fact);
                            sb.AppendLine();
                            injectedCount++;
                        }
                    }

                    // Protocol validation: LLAI requires > 5 alphanumeric characters to activate.
                    // If no NPC rules exist, provide a clean fallback world-state rule.
                    string generatedText = sb.ToString();
                    int alphaNumericCount = 0;
                    for (int i = 0; i < generatedText.Length; i++)
                    {
                        char c = generatedText[i];
                        if (char.IsLetterOrDigit(c))
                        {
                            alphaNumericCount++;
                            if (alphaNumericCount >= MinAlphanumericRequirement) break;
                        }
                    }

                    if (alphaNumericCount < MinAlphanumericRequirement)
                    {
                        sb.AppendLine("[WORLD] Dynamic World Engine active. Real-time world state synchronized.");
                        sb.AppendLine();
                    }

                    string content = sb.ToString();

                    // Ensure target directory exists
                    string dir = Path.GetDirectoryName(_resolvedRulesFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    // Atomic file write using temporary file swap to eliminate read tearing during game ticks
                    bool writeSuccess = PerformAtomicWrite(_resolvedRulesFilePath, content);
                    if (writeSuccess)
                    {
                        Console.WriteLine("[LivingLsAiBridge] Successfully synchronized {0} active NPC facts ({1} chars) to: {2}",
                            injectedCount, content.Length, _resolvedRulesFilePath);
                        return true;
                    }
                    else
                    {
                        _isDirty = true;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _isDirty = true;
                    Console.WriteLine("[LivingLsAiBridge] Error writing custom rules file ({0}): {1}", _resolvedRulesFilePath, ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// Writes content atomically to targetPath using a temporary file and atomic swap with retry backoff.
        /// </summary>
        private bool PerformAtomicWrite(string targetPath, string content)
        {
            string dir = Path.GetDirectoryName(targetPath);
            string tempFile = Path.Combine(dir, Path.GetFileName(targetPath) + ".tmp." + Guid.NewGuid().ToString("N"));

            try
            {
                // Write out temporary file with UTF-8 encoding (without BOM)
                File.WriteAllText(tempFile, content, new UTF8Encoding(false));

                // Atomic swap with retry backoff for transient file locks
                const int maxRetries = 3;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        if (File.Exists(targetPath))
                        {
                            try
                            {
                                File.Replace(tempFile, targetPath, null, true);
                                return true;
                            }
                            catch (PlatformNotSupportedException)
                            {
                                File.Copy(tempFile, targetPath, true);
                                File.Delete(tempFile);
                                return true;
                            }
                        }
                        else
                        {
                            File.Move(tempFile, targetPath);
                            return true;
                        }
                    }
                    catch (IOException)
                    {
                        if (attempt == maxRetries)
                        {
                            // Last attempt fallback: direct copy overwrite
                            File.Copy(tempFile, targetPath, true);
                            return true;
                        }
                        System.Threading.Thread.Sleep(25);
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[LivingLsAiBridge] Atomic write error for '{0}': {1}", targetPath, ex.Message);
                return false;
            }
            finally
            {
                // Guarantee cleanup of scratch file
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        public string GetCustomRulesFilePath()
        {
            return _resolvedRulesFilePath;
        }
    }
}
