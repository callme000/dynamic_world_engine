using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;

namespace DynamicWorldEngine.Spatial
{
    public class ManagedBlip
    {
        public Blip BlipInstance { get; set; }
        public string EnvironmentId { get; set; }
        public string BlipName { get; set; }
        public Vector3 Position { get; set; }
        public bool IsDiscovered { get; set; }
        public string RequiredSecret { get; set; }

        public ManagedBlip(Blip instance, string envId, string name, Vector3 pos, bool discovered = true, string secret = "")
        {
            BlipInstance = instance;
            EnvironmentId = envId;
            BlipName = name;
            Position = pos;
            IsDiscovered = discovered;
            RequiredSecret = secret;
        }
    }

    public class BlipManager
    {
        private readonly List<ManagedBlip> _managedBlips = new List<ManagedBlip>();

        public Blip CreateNpcBlip(Ped ped, string name, string envId, BlipColor color = BlipColor.Green, BlipSprite sprite = BlipSprite.Standard)
        {
            if (ped == null || !ped.Exists()) return null;

            try
            {
                Blip blip = ped.AddBlip();
                blip.Color = color;
                blip.Sprite = sprite;
                blip.Name = name;
                blip.IsShortRange = true;
                
                _managedBlips.Add(new ManagedBlip(blip, envId, name, ped.Position));
                return blip;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[BlipManager] Error creating NPC blip: " + ex.Message);
                return null;
            }
        }

        public Blip CreateNpcBlip(Ped ped, string name, BlipColor color = BlipColor.Green, BlipSprite sprite = BlipSprite.Standard)
        {
            return CreateNpcBlip(ped, name, "global", color, sprite);
        }

        public Blip CreateCoordBlip(Vector3 position, string label, string envId, BlipColor color = BlipColor.Yellow, BlipSprite sprite = BlipSprite.DollarSign, bool isDiscovered = true, string requiredSecret = "")
        {
            try
            {
                Blip blip = null;
                if (isDiscovered)
                {
                    blip = World.CreateBlip(position);
                    blip.Color = color;
                    blip.Sprite = sprite;
                    blip.Name = label;
                    blip.IsShortRange = false;
                }

                _managedBlips.Add(new ManagedBlip(blip, envId, label, position, isDiscovered, requiredSecret));
                return blip;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[BlipManager] Error creating coord blip: " + ex.Message);
                return null;
            }
        }

        public Blip CreateCoordBlip(Vector3 position, string label, BlipColor color = BlipColor.Yellow, BlipSprite sprite = BlipSprite.DollarSign)
        {
            return CreateCoordBlip(position, label, "global", color, sprite, true, "");
        }

        public void RevealSecretBlip(string envId, string requiredSecret, BlipColor color = BlipColor.Yellow, BlipSprite sprite = BlipSprite.Standard)
        {
            foreach (var mb in _managedBlips)
            {
                if (!mb.IsDiscovered && string.Equals(mb.EnvironmentId, envId, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(mb.RequiredSecret) || string.Equals(mb.RequiredSecret, requiredSecret, StringComparison.OrdinalIgnoreCase))
                    {
                        mb.IsDiscovered = true;
                        if (mb.BlipInstance == null || !mb.BlipInstance.Exists())
                        {
                            mb.BlipInstance = World.CreateBlip(mb.Position);
                            mb.BlipInstance.Color = color;
                            mb.BlipInstance.Sprite = sprite;
                            mb.BlipInstance.Name = mb.BlipName;
                            mb.BlipInstance.IsShortRange = false;
                        }
                    }
                }
            }
        }

        public void RemoveBlipsForEnvironment(string environmentId)
        {
            if (string.IsNullOrEmpty(environmentId)) return;

            for (int i = _managedBlips.Count - 1; i >= 0; i--)
            {
                var mb = _managedBlips[i];
                if (string.Equals(mb.EnvironmentId, environmentId, StringComparison.OrdinalIgnoreCase))
                {
                    if (mb.BlipInstance != null && mb.BlipInstance.Exists())
                    {
                        mb.BlipInstance.Delete();
                    }
                    _managedBlips.RemoveAt(i);
                }
            }
        }

        public void ClearAllBlips()
        {
            foreach (var mb in _managedBlips)
            {
                if (mb.BlipInstance != null && mb.BlipInstance.Exists())
                {
                    mb.BlipInstance.Delete();
                }
            }
            _managedBlips.Clear();
        }
    }
}
