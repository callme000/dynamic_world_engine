using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Spatial;
using Newtonsoft.Json;

namespace DynamicWorldEngine.Tools
{
    public class SimpleVec3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public SimpleVec3() { }
        public SimpleVec3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    public class InteriorDto
    {
        public string InteriorId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Privacy { get; set; }
        public string Atmosphere { get; set; }
        public string SecurityLevel { get; set; }
        public int CapacityNpcs { get; set; }
        public List<string> SemanticTags { get; set; }
        public string[] Keywords { get; set; }
        public List<string> IplNames { get; set; }
        public List<string> RemoveIplNames { get; set; }
        public SimpleVec3 EntranceCoords { get; set; }
        public SimpleVec3 InteriorCoords { get; set; }
        public float InteriorHeading { get; set; }
        public List<string> EntitySets { get; set; }
        public bool SupportsVehicles { get; set; }
        public SimpleVec3 WaterArrivalCoords { get; set; }
    }

    public class PropDto
    {
        public string Model { get; set; }
        public string Category { get; set; }
        public SimpleVec3 Dimensions { get; set; }
        public bool Walkable { get; set; }
        public bool BlocksNavMesh { get; set; }
        public bool CanAttachChildren { get; set; }
        public float SurfaceElevation { get; set; }
        public List<string> Tags { get; set; }
    }

    public class CharacterDto
    {
        public string CharacterId { get; set; }
        public string FullName { get; set; }
        public string PedModel { get; set; }
        public string DefaultOccupation { get; set; }
        public string PublicFaction { get; set; }
        public string CanonDescription { get; set; }
        public string DlcOrigin { get; set; }
        public List<string> Roles { get; set; }
        public List<string> AssociatedInteriors { get; set; }
        public List<string> Keywords { get; set; }
        public string DefaultScenario { get; set; }
        public string DefaultInitialTask { get; set; }
    }

    public class ExportCatalogDataToJson
    {
        public static void Main(string[] args)
        {
            string outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DynamicWorldEngineData");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            // 1. Export Interiors
            var rawInteriors = InteriorCatalog.GetAllInteriors();
            var interiorDtos = new List<InteriorDto>();
            foreach (var i in rawInteriors)
            {
                interiorDtos.Add(new InteriorDto
                {
                    InteriorId = i.InteriorId,
                    Name = i.Name,
                    Category = i.Category,
                    Description = i.Description,
                    Privacy = i.Privacy,
                    Atmosphere = i.Atmosphere,
                    SecurityLevel = i.SecurityLevel,
                    CapacityNpcs = i.CapacityNpcs,
                    SemanticTags = i.SemanticTags,
                    Keywords = i.Keywords,
                    IplNames = i.IplNames,
                    RemoveIplNames = i.RemoveIplNames,
                    EntranceCoords = new SimpleVec3(i.EntranceCoords.X, i.EntranceCoords.Y, i.EntranceCoords.Z),
                    InteriorCoords = new SimpleVec3(i.InteriorCoords.X, i.InteriorCoords.Y, i.InteriorCoords.Z),
                    InteriorHeading = i.InteriorHeading,
                    EntitySets = i.EntitySets,
                    SupportsVehicles = i.SupportsVehicles,
                    WaterArrivalCoords = i.WaterArrivalCoords.HasValue ? new SimpleVec3(i.WaterArrivalCoords.Value.X, i.WaterArrivalCoords.Value.Y, i.WaterArrivalCoords.Value.Z) : null
                });
            }
            File.WriteAllText(Path.Combine(outDir, "interiors.json"), JsonConvert.SerializeObject(interiorDtos, Formatting.Indented));
            Console.WriteLine("Exported {0} interiors to {1}", interiorDtos.Count, Path.Combine(outDir, "interiors.json"));

            // 2. Export Props
            var rawProps = ModularAssetCatalog.GetAllAssets();
            var propDtos = new List<PropDto>();
            foreach (var p in rawProps)
            {
                propDtos.Add(new PropDto
                {
                    Model = p.Model,
                    Category = p.Category,
                    Dimensions = new SimpleVec3(p.Dimensions.X, p.Dimensions.Y, p.Dimensions.Z),
                    Walkable = p.Walkable,
                    BlocksNavMesh = p.BlocksNavMesh,
                    CanAttachChildren = p.CanAttachChildren,
                    SurfaceElevation = p.SurfaceElevation,
                    Tags = p.Tags
                });
            }
            File.WriteAllText(Path.Combine(outDir, "props.json"), JsonConvert.SerializeObject(propDtos, Formatting.Indented));
            Console.WriteLine("Exported {0} props to {1}", propDtos.Count, Path.Combine(outDir, "props.json"));

            // 3. Export Characters
            var rawChars = CanonicalCharacterRegistry.GetAllCharacters();
            var charDtos = new List<CharacterDto>();
            foreach (var c in rawChars)
            {
                charDtos.Add(new CharacterDto
                {
                    CharacterId = c.CharacterId,
                    FullName = c.FullName,
                    PedModel = c.PedModel,
                    DefaultOccupation = c.DefaultOccupation,
                    PublicFaction = c.PublicFaction,
                    CanonDescription = c.CanonDescription,
                    DlcOrigin = c.DlcOrigin,
                    Roles = c.Roles,
                    AssociatedInteriors = c.AssociatedInteriors,
                    Keywords = c.Keywords,
                    DefaultScenario = c.DefaultScenario,
                    DefaultInitialTask = c.DefaultInitialTask
                });
            }
            File.WriteAllText(Path.Combine(outDir, "characters.json"), JsonConvert.SerializeObject(charDtos, Formatting.Indented));
            Console.WriteLine("Exported {0} characters to {1}", charDtos.Count, Path.Combine(outDir, "characters.json"));

            Console.WriteLine("All manifests exported successfully!");
        }
    }
}
