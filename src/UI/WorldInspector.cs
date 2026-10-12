using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Simulation;

namespace DynamicWorldEngine.UI
{
    public class WorldInspector
    {
        public bool IsVisible { get; set; }

        public WorldInspector()
        {
            IsVisible = false;
        }

        public void Toggle()
        {
            IsVisible = !IsVisible;
            Notification.Show(string.Format("~y~[World Inspector]~w~ {0}", IsVisible ? "~g~ENABLED" : "~r~DISABLED"));
        }

        public void DrawInspectorHud(WorldMemoryStore store, List<ActiveNpcContext> activeContexts)
        {
            if (!IsVisible || store == null) return;

            // Draw Background Box at Top-Right
            float boxX = 0.72f;
            float boxY = 0.04f;
            float boxW = 0.26f;
            float boxH = 0.44f;

            DrawRect(boxX + (boxW / 2f), boxY + (boxH / 2f), boxW, boxH, 0, 0, 0, 195);

            // Title
            DrawText("DYNAMIC WORLD ENGINE v8.0", boxX + 0.01f, boxY + 0.01f, 0.35f, Color.Gold, true);
            DrawText(string.Format("Environments: {0} | NPCs: {1}", store.GetAllEnvironments().Count, store.GetAllNpcs().Count), boxX + 0.01f, boxY + 0.038f, 0.26f, Color.White, false);
            DrawText(string.Format("World Events: {0} | Props: {1}", store.GetRecentWorldEvents(50).Count, store.GetAllProps().Count), boxX + 0.01f, boxY + 0.060f, 0.26f, Color.White, false);
            DrawText("Simulation Agency: AUTONOMOUS", boxX + 0.01f, boxY + 0.082f, 0.25f, Color.LimeGreen, false);

            // Nearest NPC Section
            DrawRect(boxX + (boxW / 2f), boxY + 0.110f, boxW - 0.01f, 0.002f, 255, 255, 255, 100);
            DrawText("SELECTED / NEAREST NPC", boxX + 0.01f, boxY + 0.118f, 0.29f, Color.Orange, true);

            Ped nearestPed = null;
            NpcRecord nearestRecord = null;
            float minDist = 15.0f;
            Vector3 playerPos = Game.Player.Character.Position;

            if (activeContexts != null)
            {
                foreach (var ctx in activeContexts)
                {
                    if (ctx != null && ctx.IsValid)
                    {
                        float d = ctx.PedInstance.Position.DistanceTo(playerPos);
                        if (d < minDist)
                        {
                            minDist = d;
                            nearestPed = ctx.PedInstance;
                            nearestRecord = ctx.Record;
                        }
                    }
                }
            }

            if (nearestRecord != null)
            {
                DrawText(string.Format("Name: {0}", nearestRecord.Name), boxX + 0.01f, boxY + 0.145f, 0.27f, Color.White, false);
                DrawText(string.Format("Role: {0} ({1})", nearestRecord.Occupation, nearestRecord.PublicFaction), boxX + 0.01f, boxY + 0.168f, 0.26f, Color.LightGray, false);
                DrawText(string.Format("State: {0}", nearestRecord.CurrentState ?? "CALM"), boxX + 0.01f, boxY + 0.191f, 0.26f, Color.Cyan, true);
                
                string goalStr = (nearestRecord.ActiveGoal != null) ? nearestRecord.ActiveGoal.GoalType : "IDLE";
                DrawText(string.Format("Goal: {0}", goalStr), boxX + 0.01f, boxY + 0.214f, 0.26f, Color.Chartreuse, true);

                DrawText(string.Format("Fear: {0}% | Trust: {1} | Respect: {2}", nearestRecord.Fear, nearestRecord.Trust, nearestRecord.Respect), boxX + 0.01f, boxY + 0.237f, 0.26f, Color.Yellow, false);
                
                string secret = (nearestRecord.Secrets != null && nearestRecord.Secrets.Count > 0) ? nearestRecord.Secrets[0] : (nearestRecord.HiddenRole ?? "None");
                if (secret.Length > 28) secret = secret.Substring(0, 25) + "...";
                DrawText(string.Format("Secret: {0}", secret), boxX + 0.01f, boxY + 0.260f, 0.24f, Color.Magenta, false);

                string beliefStr = (nearestRecord.Beliefs != null && nearestRecord.Beliefs.Count > 0) 
                    ? string.Format("{0} {1}", nearestRecord.Beliefs[0].Subject, nearestRecord.Beliefs[0].Predicate)
                    : "No active beliefs";
                if (beliefStr.Length > 28) beliefStr = beliefStr.Substring(0, 25) + "...";
                DrawText(string.Format("Belief: {0}", beliefStr), boxX + 0.01f, boxY + 0.283f, 0.24f, Color.SandyBrown, false);

                var recent = store.GetRelevantEventHistorySlice(nearestRecord.NpcId, 1);
                string lastEvent = recent.Count > 0 ? recent[0].Description : "No recent events";
                if (lastEvent.Length > 30) lastEvent = lastEvent.Substring(0, 27) + "...";
                DrawText(string.Format("Last: {0}", lastEvent), boxX + 0.01f, boxY + 0.306f, 0.23f, Color.LightSkyBlue, false);
            }
            else
            {
                DrawText("No persistent NPC within 15 meters.", boxX + 0.01f, boxY + 0.15f, 0.26f, Color.Gray, false);
            }
        }

        private void DrawRect(float x, float y, float w, float h, int r, int g, int b, int a)
        {
            Function.Call(Hash.DRAW_RECT, x, y, w, h, r, g, b, a);
        }

        private void DrawText(string text, float x, float y, float scale, Color color, bool bold)
        {
            Function.Call(Hash.SET_TEXT_FONT, 0);
            Function.Call(Hash.SET_TEXT_SCALE, scale, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, color.A);
            Function.Call(Hash.SET_TEXT_WRAP, 0.0f, 1.0f);
            Function.Call(Hash.SET_TEXT_CENTRE, false);
            Function.Call(Hash.SET_TEXT_DROPSHADOW, 0, 0, 0, 0, 0);
            Function.Call(Hash.SET_TEXT_EDGE, 1, 0, 0, 0, 205);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y);
        }
    }
}
