using System;
using System.IO;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Tests
{
    internal static class TestWorldMemoryPersistence
    {
        private static int _failures;

        private static void Check(string name, bool condition)
        {
            Console.WriteLine((condition ? "[PASS] " : "[FAIL] ") + name);
            if (!condition) _failures++;
        }

        private static int Main()
        {
            string directory = Path.Combine(Path.GetTempPath(), "DWE_Persistence_" + Guid.NewGuid().ToString("N"));
            string primary = Path.Combine(directory, "world_memory.json");
            string backup = primary + ".bak";

            try
            {
                var store = new WorldMemoryStore(directory);
                store.CreateNpc("First NPC", "Civilian", "None", "a_m_y_hipster_01");
                store.Save(true);
                store.CreateNpc("Second NPC", "Civilian", "None", "a_m_y_hipster_02");
                store.Save(true);

                Check("Primary database exists after save", File.Exists(primary));
                Check("Backup exists after replacement", File.Exists(backup));

                File.WriteAllText(primary, "{ this is not valid json");
                var recovered = new WorldMemoryStore(directory);
                Check("Valid backup is recovered after primary corruption",
                    recovered.GetNpcByName("First NPC") != null);
                Check("Recovery rolls back to the previous committed generation",
                    recovered.GetNpcByName("Second NPC") == null);

                File.WriteAllText(backup, "{ backup is also corrupt");
                bool rejectedCorruptState = false;
                try
                {
                    new WorldMemoryStore(directory);
                }
                catch (InvalidDataException)
                {
                    rejectedCorruptState = true;
                }

                Check("Corrupt primary and backup fail closed", rejectedCorruptState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FAIL] Unexpected exception: " + ex);
                _failures++;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[WARN] Could not clean test directory: " + ex.Message);
                }
            }

            Console.WriteLine();
            Console.WriteLine(_failures == 0 ? "ALL TESTS PASSED" : _failures + " TEST(S) FAILED");
            return _failures == 0 ? 0 : 1;
        }
    }
}