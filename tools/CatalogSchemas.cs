using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

namespace DynamicWorldEngine.Tools
{
    class CatalogSchemas
    {
        static void Main(string[] args)
        {
            string dllPath = @"C:\Program Files\Epic Games\GTAVEnhanced\scripts\EnableAllinteriors.dll";
            if (args.Length > 0 && File.Exists(args[0])) dllPath = args[0];

            AppDomain.CurrentDomain.AssemblyResolve += (s, r) =>
            {
                string n = new AssemblyName(r.Name).Name + ".dll";
                string[] paths = {
                    Path.Combine(@"c:\Users\melod\Documents\gtavmods\lib", n),
                    Path.Combine(@"C:\Program Files\Epic Games\GTAVEnhanced\scripts", n),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, n),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", n)
                };
                foreach (var p in paths) if (File.Exists(p)) return Assembly.LoadFrom(p);
                return null;
            };

            Assembly asm = Assembly.Load(File.ReadAllBytes(dllPath));
            Console.WriteLine("Assembly: " + asm.FullName);

            Type c1 = asm.GetType("EnableAllinteriors.Class1");
            Type actType = asm.GetType("EnableAllinteriors.Class1+ActiveInterior");

            Console.WriteLine("\n================ ACTIVEINTERIOR CONSTRUCTORS ================");
            foreach (var ctor in actType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var pars = ctor.GetParameters();
                List<string> pStrs = new List<string>();
                foreach (var p in pars) pStrs.Add(p.ParameterType.Name + " " + p.Name);
                Console.WriteLine("ActiveInterior(" + string.Join(", ", pStrs) + ")");
            }

            Console.WriteLine("\n================ ACTIVEINTERIOR FIELDS ================");
            foreach (var f in actType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                Console.WriteLine(string.Format("{0} {1} (Access: {2}, Static: {3})", f.FieldType.Name, f.Name, f.IsPublic ? "public" : "non-public", f.IsStatic));
            }

            Console.WriteLine("\n================ ACTIVEINTERIOR PROPERTIES ================");
            foreach (var p in actType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                Console.WriteLine(string.Format("{0} {1}", p.PropertyType.Name, p.Name));
            }

            FieldInfo actIntListField = c1.GetField("ActiveInteriors", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Console.WriteLine("ActiveInteriors field token: 0x" + actIntListField.MetadataToken.ToString("X8"));

            try
            {
                Console.WriteLine("Attempting to read Class1.ActiveInteriors via reflection...");
                var list = actIntListField.GetValue(null);
                Console.WriteLine("Class1.ActiveInteriors read result: " + (list != null ? list.ToString() : "null"));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to read Class1.ActiveInteriors directly: " + ex.GetType().Name + ": " + ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("InnerException: " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message);
                }
            }

            Console.WriteLine("\n================ CLASS1 FIELDS REFERENCING ACTIVEINTERIOR / LISTS ================");
            foreach (var f in c1.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                string typeName = f.FieldType.Name;
                if (f.FieldType.IsGenericType)
                {
                    var genArgs = f.FieldType.GetGenericArguments();
                    List<string> gStrs = new List<string>();
                    foreach (var g in genArgs) gStrs.Add(g.Name);
                    typeName = string.Format("{0}<{1}>", f.FieldType.Name.Split('`')[0], string.Join(", ", gStrs));
                }

                if (typeName.Contains("ActiveInterior") || typeName.Contains("List") || f.Name.ToLower().Contains("interior"))
                {
                    Console.WriteLine(string.Format("{0} {1} (Access: {2}, Static: {3})", typeName, f.Name, f.IsPublic ? "public" : "non-public", f.IsStatic));
                }
            }

            Console.WriteLine("\n================ CLASS1 METHODS ================");
            foreach (var m in c1.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (m.Name.Contains("Interior") || m.Name.Contains("Mansion") || m.Name.Contains("Penthouse") || m.Name.Contains("Nightclub") || m.Name.Contains("Kortz"))
                {
                    Console.WriteLine(string.Format("{0} {1}() (Static: {2})", m.ReturnType.Name, m.Name, m.IsStatic));
                }
            }
        }
    }
}
