using System;
using System.IO;
using System.Reflection;

class Program
{
    static void Main(string[] args)
    {
        string dllPath = @"C:\Program Files\Epic Games\GTAVEnhanced\scripts\EnableAllinteriors.dll";
        AppDomain.CurrentDomain.AssemblyResolve += (s, r) => {
            string n = new AssemblyName(r.Name).Name + ".dll";
            string[] paths = { Path.Combine(@"C:\Users\melod\Documents\gtavmods\lib", n), Path.Combine(@"C:\Program Files\Epic Games\GTAVEnhanced\scripts", n) };
            foreach (var p in paths) if (File.Exists(p)) return Assembly.LoadFrom(p);
            return null;
        };
        Assembly asm = Assembly.Load(File.ReadAllBytes(dllPath));
        Type actType = asm.GetType("EnableAllinteriors.Class1+ActiveInterior");
        foreach (var ctor in actType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            Console.WriteLine("Ctor:");
            foreach (var p in ctor.GetParameters())
            {
                Console.WriteLine("  Param: " + p.ParameterType.Name + " " + p.Name);
            }
        }
    }
}
