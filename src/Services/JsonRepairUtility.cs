using System;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Services
{
    public static class JsonRepairUtility
    {
        public static string RepairAndExtractJson(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return "{}";

            string text = rawText.Trim();

            // 1. Strip markdown code block wrappers
            if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(7);
            }
            if (text.StartsWith("```"))
            {
                text = text.Substring(3);
            }
            if (text.EndsWith("```"))
            {
                text = text.Substring(0, text.Length - 3);
            }
            text = text.Trim();

            // 2. Find start of JSON object or array
            int firstOpenBrace = text.IndexOf('{');
            int firstOpenBracket = text.IndexOf('[');

            int startIdx = -1;
            if (firstOpenBrace >= 0 && firstOpenBracket >= 0)
                startIdx = Math.Min(firstOpenBrace, firstOpenBracket);
            else if (firstOpenBrace >= 0)
                startIdx = firstOpenBrace;
            else if (firstOpenBracket >= 0)
                startIdx = firstOpenBracket;

            if (startIdx >= 0)
            {
                text = text.Substring(startIdx);
            }

            // 2b. Strip non-standard comments outside strings (e.g. # or //)
            StringBuilder cleaned = new StringBuilder();
            bool inStr = false;
            bool isEsc = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (isEsc)
                {
                    isEsc = false;
                    cleaned.Append(c);
                    continue;
                }
                if (c == '\\')
                {
                    isEsc = true;
                    cleaned.Append(c);
                    continue;
                }
                if (c == '"')
                {
                    inStr = !inStr;
                    cleaned.Append(c);
                    continue;
                }
                if (!inStr)
                {
                    if (c == '#' || (c == '/' && i + 1 < text.Length && text[i + 1] == '/'))
                    {
                        while (i < text.Length && text[i] != '\n' && text[i] != '\r')
                        {
                            i++;
                        }
                        if (i < text.Length) cleaned.Append(text[i]);
                        continue;
                    }
                }
                cleaned.Append(c);
            }
            text = cleaned.ToString();

            // 3. Test if already valid JSON
            try
            {
                JToken token = JToken.Parse(text);
                NormalizeSchemaQuirks(token);
                return token.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch
            {
                // Proceed to healing
            }

            // 4. Heal truncated string: check for unclosed double quotes
            bool inString = false;
            bool isEscaped = false;
            var stack = new System.Collections.Generic.Stack<char>();
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (isEscaped)
                {
                    isEscaped = false;
                    sb.Append(c);
                    continue;
                }

                if (c == '\\')
                {
                    isEscaped = true;
                    sb.Append(c);
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    sb.Append(c);
                    continue;
                }

                if (inString)
                {
                    sb.Append(c);
                    continue;
                }

                if (c == '{' || c == '[')
                {
                    stack.Push(c);
                    sb.Append(c);
                }
                else if (c == '}')
                {
                    if (stack.Count > 0 && stack.Peek() == '{')
                        stack.Pop();
                    sb.Append(c);
                }
                else if (c == ']')
                {
                    if (stack.Count > 0 && stack.Peek() == '[')
                        stack.Pop();
                    sb.Append(c);
                }
                else
                {
                    sb.Append(c);
                }
            }

            // If cut off inside a string, close the quote
            if (inString)
            {
                sb.Append('"');
            }

            string healed = sb.ToString().Trim();

            // Strip trailing commas, colons, or incomplete key-values before closing
            healed = Regex.Replace(healed, @",\s*$", "");
            healed = Regex.Replace(healed, @":\s*$", ": null");
            healed = Regex.Replace(healed, @",\s*([}\]])", "$1");

            // Close any remaining open braces / brackets
            while (stack.Count > 0)
            {
                char open = stack.Pop();
                if (open == '{') healed += "}";
                else if (open == '[') healed += "]";
            }

            // Final sanity check
            try
            {
                JToken.Parse(healed);
                return healed;
            }
            catch
            {
                // If healing failed, attempt safe fallback object extraction
                try
                {
                    int lastValid = healed.LastIndexOf('}');
                    if (lastValid > 0)
                    {
                        string sub = healed.Substring(0, lastValid + 1);
                        JToken.Parse(sub);
                        return sub;
                    }
                }
                catch { }

                return healed;
            }
        }

        private static void NormalizeSchemaQuirks(JToken root)
        {
            if (root == null) return;

            try
            {
                // Normalize scenario director beats world_event occupants
                var occupantsTokens = root.SelectTokens("$..world_event.occupants");
                foreach (var occToken in occupantsTokens)
                {
                    JArray arr = occToken as JArray;
                    if (arr != null)
                    {
                        for (int i = 0; i < arr.Count; i++)
                        {
                            if (arr[i].Type == JTokenType.String)
                            {
                                string strVal = arr[i].ToString();
                                JObject obj = new JObject();
                                obj["name"] = strVal;
                                obj["ped_model"] = (strVal.StartsWith("s_") || strVal.StartsWith("u_") || strVal.StartsWith("a_") || strVal.StartsWith("g_")) ? strVal : "s_m_y_cop_01";
                                obj["weapon"] = "WEAPON_PISTOL";
                                obj["role"] = "officer";
                                obj["hostility_to_player"] = "neutral";
                                arr[i] = obj;
                            }
                        }
                    }
                }

                // Normalize initial_events
                var initEventsTokens = root.SelectTokens("$..initial_events");
                foreach (var evToken in initEventsTokens)
                {
                    JArray arr = evToken as JArray;
                    if (arr != null)
                    {
                        for (int i = 0; i < arr.Count; i++)
                        {
                            if (arr[i].Type == JTokenType.String)
                            {
                                string strVal = arr[i].ToString();
                                JObject obj = new JObject();
                                obj["description"] = strVal;
                                obj["sentiment_tag"] = "neutral";
                                arr[i] = obj;
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
