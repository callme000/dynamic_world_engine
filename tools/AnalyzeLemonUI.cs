using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace DweTools
{
    class Program
    {
        static void Main(string[] args)
        {
            string rootDir = @"c:\Users\melod\Documents\gtavmods";
            string modDir = Path.Combine(rootDir, "2234fb-LivingLSAIs4.5");
            string libDir = Path.Combine(rootDir, "lib");

            AppDomain.CurrentDomain.AssemblyResolve += (s, r) =>
            {
                string name = new AssemblyName(r.Name).Name + ".dll";
                string[] searchDirs = new string[] { modDir, libDir, rootDir };
                foreach (var dir in searchDirs)
                {
                    string p = Path.Combine(dir, name);
                    if (File.Exists(p)) return Assembly.Load(File.ReadAllBytes(p));
                }
                return null;
            };

            Console.WriteLine("=== LemonUI Architecture ===");
            var lemonAsm = Assembly.Load(File.ReadAllBytes(Path.Combine(modDir, "LemonUI.SHVDN3.dll")));
            foreach (var t in lemonAsm.GetTypes().Where(t => t.IsPublic))
            {
                Console.WriteLine("LemonUI Class: " + t.FullName);
            }

            Console.WriteLine("\n=== LivingLSAIs LemonUI Integration ===");
            var llaiAsm = Assembly.Load(File.ReadAllBytes(Path.Combine(modDir, "LivingLSAIs.dll")));
            var llaiType = llaiAsm.GetType("LivingLSAIs.LivingLSAIs");

            foreach (var f in llaiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (f.FieldType.Namespace != null && f.FieldType.Namespace.StartsWith("LemonUI"))
                {
                    Console.WriteLine("  LemonUI Field in LivingLSAIs: " + f.FieldType.FullName + " " + f.Name);
                }
            }

            // Also check how _customRulesItem is set up or handled
            foreach (var m in llaiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (m.Name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("CustomRules", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine("  Menu/Rules Method: " + m.Name);
                }
            }
        }
    }
}
