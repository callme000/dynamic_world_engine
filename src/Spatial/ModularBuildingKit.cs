using System;
using System.Collections.Generic;

namespace DynamicWorldEngine.Spatial
{
    public class ModularRoomDefinition
    {
        public string RoomName { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float Width { get; set; }
        public float Length { get; set; }
        public float Height { get; set; }

        public ModularRoomDefinition(string name, float cx, float cy, float w, float l, float h = 2.8f)
        {
            RoomName = name;
            CenterX = cx;
            CenterY = cy;
            Width = w;
            Length = l;
            Height = h;
        }
    }

    public static class ModularBuildingKit
    {
        // Verified high-collision structural models
        public const string ModelFloorConcrete = "v_ilev_cd_floor01";
        public const string ModelFloorWood = "prop_wood_plank_01";
        public const string ModelWallReinforced = "prop_sec_barrier_01a";
        public const string ModelWallPlaster = "v_serv_plas_wall01";
        public const string ModelDoorwayOpen = "prop_security_fence_01";
        public const string ModelCeilingSteel = "prop_steel_plate_01";

        public const float StandardWallHeight = 2.8f;
        public const float StandardWallLength = 3.0f;
        public const float TableSurfaceZ = 0.76f;
    }
}
