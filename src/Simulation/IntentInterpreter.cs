using System;
using System.Collections.Generic;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Simulation
{
    public enum SemanticIntentType
    {
        Friendly,
        Inquiry,
        Bribe,
        Threat,
        Accusation,
        Insult,
        SecretExposition,
        Unknown
    }

    public class SemanticIntent
    {
        public SemanticIntentType Type { get; set; }
        public float Confidence { get; set; }
        public float EmotionalImpact { get; set; }
        public string ExtractedTopic { get; set; }
        public string SecretMatched { get; set; }
        public string RawUtterance { get; set; }

        public SemanticIntent()
        {
            Type = SemanticIntentType.Unknown;
            Confidence = 0.5f;
            EmotionalImpact = 0.5f;
            ExtractedTopic = string.Empty;
            SecretMatched = string.Empty;
            RawUtterance = string.Empty;
        }
    }

    public static class IntentInterpreter
    {
        public static SemanticIntent Interpret(string utterance, NpcRecord targetNpc = null)
        {
            if (string.IsNullOrWhiteSpace(utterance))
            {
                return new SemanticIntent { Type = SemanticIntentType.Unknown, Confidence = 0f, EmotionalImpact = 0f };
            }

            string text = utterance.Trim().ToLowerInvariant();
            SemanticIntent result = new SemanticIntent { RawUtterance = utterance };

            // 1. Check if utterance touches any of the NPC's concealed secrets!
            if (targetNpc != null && targetNpc.Secrets != null)
            {
                foreach (var secret in targetNpc.Secrets)
                {
                    if (string.IsNullOrEmpty(secret)) continue;
                    string sLower = secret.ToLowerInvariant();

                    bool match = false;
                    if (sLower.Contains("informant") && (text.Contains("informant") || text.Contains("cop") || text.Contains("police") || text.Contains("snitch") || text.Contains("fed") || text.Contains("rat")))
                    {
                        match = true;
                    }
                    else if (sLower.Contains("money laundering") && (text.Contains("laundering") || text.Contains("dirty money") || text.Contains("wash")))
                    {
                        match = true;
                    }
                    else if (sLower.Contains("stash") && (text.Contains("stash") || text.Contains("hidden cache") || text.Contains("secret box")))
                    {
                        match = true;
                    }

                    if (match)
                    {
                        result.Type = SemanticIntentType.SecretExposition;
                        result.Confidence = 0.92f;
                        result.EmotionalImpact = 0.85f;
                        result.ExtractedTopic = "Secret Exposed: " + secret;
                        result.SecretMatched = secret;
                        return result;
                    }
                }
            }

            // 2. Semantic Accusations ("I know you...", "You are working with...")
            if (text.StartsWith("i know") || text.StartsWith("you are") || text.Contains("you're a") || text.Contains("you work for") || text.Contains("snitch") || text.Contains("traitor") || text.Contains("liar"))
            {
                result.Type = SemanticIntentType.Accusation;
                result.Confidence = 0.88f;
                result.EmotionalImpact = 0.75f;
                result.ExtractedTopic = "Accusation";
                return result;
            }

            // 3. Direct Threats ("Give me your...", "Or I'll shoot", "Hands up")
            if ((text.Contains("give me") && (text.Contains("money") || text.Contains("cash") || text.Contains("keys"))) ||
                text.Contains("hands up") || text.Contains("i'll shoot") || text.Contains("kill you") || text.Contains("die") || text.Contains("blow your head"))
            {
                result.Type = SemanticIntentType.Threat;
                result.Confidence = 0.95f;
                result.EmotionalImpact = 0.90f;
                result.ExtractedTopic = "Physical Lethal Threat";
                return result;
            }

            // 4. Bribes and Financial Proposals ("Here's $500", "Take this money", "I'll pay you")
            if (text.Contains("bribe") || text.Contains("cut of the") || text.Contains("take this cash") || text.Contains("i'll pay you") || text.Contains("partner up") || text.Contains("for $") || text.Contains("dollars for"))
            {
                result.Type = SemanticIntentType.Bribe;
                result.Confidence = 0.90f;
                result.EmotionalImpact = 0.70f;
                result.ExtractedTopic = "Financial Proposal";
                return result;
            }

            // 5. Inquiries ("Where is...", "Who is...", "Tell me about...")
            if (text.StartsWith("where") || text.StartsWith("who") || text.StartsWith("what") || text.StartsWith("how") || text.Contains("tell me") || text.Contains("looking for"))
            {
                result.Type = SemanticIntentType.Inquiry;
                result.Confidence = 0.85f;
                result.EmotionalImpact = 0.20f;
                result.ExtractedTopic = "Information Request";
                return result;
            }

            // 6. Insults
            if (text.Contains("idiot") || text.Contains("bitch") || text.Contains("loser") || text.Contains("clown") || text.Contains("fuck off") || text.Contains("shut up"))
            {
                result.Type = SemanticIntentType.Insult;
                result.Confidence = 0.85f;
                result.EmotionalImpact = 0.50f;
                result.ExtractedTopic = "Verbal Hostility";
                return result;
            }

            // 7. Friendly greetings
            if (text.Contains("hello") || text.Contains("hey") || text.Contains("good to see") || text.Contains("need help") || text.Contains("friend"))
            {
                result.Type = SemanticIntentType.Friendly;
                result.Confidence = 0.80f;
                result.EmotionalImpact = 0.30f;
                result.ExtractedTopic = "Friendly Greeting";
                return result;
            }

            return result;
        }
    }
}
