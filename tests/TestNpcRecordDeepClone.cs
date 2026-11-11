using System;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Tests
{
    internal static class TestNpcRecordDeepClone
    {
        private static int _failures;

        private static void Check(string name, bool condition)
        {
            Console.WriteLine((condition ? "[PASS] " : "[FAIL] ") + name);
            if (!condition) _failures++;
        }

        private static int Main()
        {
            var source = new NpcRecord
            {
                NpcId = 42,
                History = new System.Collections.Generic.List<EventRecord>
                {
                    new EventRecord
                    {
                        EventId = 7,
                        Description = "Original event",
                        Consequences = new System.Collections.Generic.List<string> { "Original consequence" },
                        Witnesses = new System.Collections.Generic.List<WitnessRecord>
                        {
                            new WitnessRecord(9, PerceptionType.DirectSight, 0.9f, 12f)
                        }
                    }
                },
                Knowledge = new System.Collections.Generic.List<KnowledgeEntry>
                {
                    new KnowledgeEntry("FACT", "Original knowledge", "event-7")
                },
                Beliefs = new System.Collections.Generic.List<BeliefEntry>
                {
                    new BeliefEntry("Player", "IS_DANGEROUS", "TRUE", 0.6f, "Rumor")
                }
            };

            source.ActiveGoal.GoalId = "goal-active";
            source.ActiveGoal.Status = "SUSPENDED";
            source.LongTermGoal.GoalId = "goal-long";
            source.LongTermGoal.Status = "COMPLETED";

            NpcRecord snapshot = source.Clone();

            Check("Goal identity is preserved", snapshot.ActiveGoal.GoalId == "goal-active");
            Check("Goal status is preserved", snapshot.ActiveGoal.Status == "SUSPENDED");
            Check("Long-term goal identity is preserved", snapshot.LongTermGoal.GoalId == "goal-long");
            Check("Long-term goal status is preserved", snapshot.LongTermGoal.Status == "COMPLETED");

            snapshot.History[0].Description = "Mutated snapshot event";
            snapshot.History[0].Consequences[0] = "Mutated snapshot consequence";
            snapshot.History[0].Witnesses[0].Confidence = 0.1f;
            snapshot.Knowledge[0].Description = "Mutated snapshot knowledge";
            snapshot.Beliefs[0].Confidence = 0.99f;
            snapshot.ActiveGoal.Status = "FAILED";

            Check("EventRecord is deeply copied", source.History[0].Description == "Original event");
            Check("Event consequences are deeply copied", source.History[0].Consequences[0] == "Original consequence");
            Check("Witness records are deeply copied", Math.Abs(source.History[0].Witnesses[0].Confidence - 0.9f) < 0.0001f);
            Check("Knowledge entries are deeply copied", source.Knowledge[0].Description == "Original knowledge");
            Check("Belief entries are deeply copied", Math.Abs(source.Beliefs[0].Confidence - 0.6f) < 0.0001f);
            Check("Goal mutations do not leak back", source.ActiveGoal.Status == "SUSPENDED");

            Check("Snapshot lists are distinct", !object.ReferenceEquals(source.History, snapshot.History)
                && !object.ReferenceEquals(source.Knowledge, snapshot.Knowledge)
                && !object.ReferenceEquals(source.Beliefs, snapshot.Beliefs));

            Console.WriteLine();
            Console.WriteLine(_failures == 0 ? "ALL TESTS PASSED" : _failures + " TEST(S) FAILED");
            return _failures == 0 ? 0 : 1;
        }
    }
}
