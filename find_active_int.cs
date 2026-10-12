using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        string dllPath = @"C:\Program Files\Epic Games\GTAVEnhanced\scripts\EnableAllinteriors.dll";
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                string name = new AssemblyName(resolveArgs.Name).Name + ".dll";
                string shvdnPath = Path.Combine(@"C:\Users\melod\Documents\gtavmods\lib", name);
                if (File.Exists(shvdnPath)) return Assembly.LoadFrom(shvdnPath);
                string scriptsPath = Path.Combine(@"C:\Program Files\Epic Games\GTAVEnhanced\scripts", name);
                if (File.Exists(scriptsPath)) return Assembly.LoadFrom(scriptsPath);
                string gtaPath = Path.Combine(@"C:\Program Files\Epic Games\GTAVEnhanced", name);
                if (File.Exists(gtaPath)) return Assembly.LoadFrom(gtaPath);
                return null;
            };

            Assembly asm = Assembly.LoadFrom(dllPath);
            Module mod = asm.ManifestModule;

            Type c1 = asm.GetType("EnableAllinteriors.Class1");
            Type actIntType = asm.GetType("EnableAllinteriors.Class1+ActiveInterior");

            Console.WriteLine("Scanning for methods referencing ActiveInterior...");
            foreach (MethodInfo m in c1.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                MethodBody body = m.GetMethodBody();
                if (body == null) continue;
                byte[] il = body.GetILAsByteArray();
                if (il == null) continue;

                // Check if method calls constructor of ActiveInterior or references it
                bool references = false;
                for (int i = 0; i < il.Length; i++)
                {
                    byte op = il[i];
                    if ((op == 0x28 || op == 0x6f || op == 0x73) && i + 4 < il.Length) // call, callvirt, newobj
                    {
                        int token = BitConverter.ToInt32(il, i + 1);
                        try
                        {
                            MemberInfo member = mod.ResolveMember(token);
                            if (member != null && (member.DeclaringType == actIntType || member.Name.Contains("ActiveInterior")))
                            {
                                references = true;
                                break;
                            }
                        }
                        catch { }
                        i += 4;
                    }
                }

                if (references)
                {
                    Console.WriteLine("Method references ActiveInterior: " + m.Name);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex);
        }
    }
}
