using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;

namespace DweTools
{
    class CilInstruction
    {
        public int Offset { get; set; }
        public OpCode OpCode { get; set; }
        public object Operand { get; set; }
        public int Size { get; set; }

        public override string ToString()
        {
            string opStr = "";
            if (Operand != null)
            {
                string s = Operand as string;
                MethodBase mb = Operand as MethodBase;
                FieldInfo fi = Operand as FieldInfo;
                Type t = Operand as Type;

                if (s != null) opStr = "\"" + s.Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
                else if (mb != null) opStr = (mb.DeclaringType != null ? mb.DeclaringType.Name + "." : "") + mb.Name;
                else if (fi != null) opStr = (fi.DeclaringType != null ? fi.DeclaringType.Name + "." : "") + fi.Name;
                else if (t != null) opStr = t.FullName;
                else opStr = Operand.ToString();
            }
            return string.Format("IL_{0:X4}: {1,-12} {2}", Offset, OpCode.Name, opStr);
        }
    }

    static class CilDecoder
    {
        private static readonly Dictionary<short, OpCode> OneByteOpCodes = new Dictionary<short, OpCode>();
        private static readonly Dictionary<short, OpCode> TwoByteOpCodes = new Dictionary<short, OpCode>();

        static CilDecoder()
        {
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                OpCode opcode = (OpCode)field.GetValue(null);
                if (opcode.OpCodeType != OpCodeType.Nternal)
                {
                    if (opcode.Size == 1) OneByteOpCodes[opcode.Value] = opcode;
                    else TwoByteOpCodes[opcode.Value] = opcode;
                }
            }
        }

        public static List<CilInstruction> Decode(MethodBase method)
        {
            var instructions = new List<CilInstruction>();
            if (method == null) return instructions;

            MethodBody body = null;
            try { body = method.GetMethodBody(); } catch { }
            if (body == null) return instructions;

            byte[] il = body.GetILAsByteArray();
            if (il == null || il.Length == 0) return instructions;

            Module module = method.Module;
            int offset = 0;

            while (offset < il.Length)
            {
                int startOffset = offset;
                byte b = il[offset++];
                OpCode opcode;

                if (b == 0xFE)
                {
                    if (offset >= il.Length) break;
                    byte b2 = il[offset++];
                    short val = unchecked((short)((0xFE << 8) | b2));
                    if (!TwoByteOpCodes.TryGetValue(val, out opcode)) continue;
                }
                else
                {
                    if (!OneByteOpCodes.TryGetValue(b, out opcode)) continue;
                }

                object operand = null;
                switch (opcode.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineI: operand = unchecked((sbyte)il[offset++]); break;
                    case OperandType.ShortInlineVar: operand = il[offset++]; break;
                    case OperandType.ShortInlineR: operand = BitConverter.ToSingle(il, offset); offset += 4; break;
                    case OperandType.InlineI: operand = BitConverter.ToInt32(il, offset); offset += 4; break;
                    case OperandType.InlineI8: operand = BitConverter.ToInt64(il, offset); offset += 8; break;
                    case OperandType.InlineR: operand = BitConverter.ToDouble(il, offset); offset += 8; break;
                    case OperandType.InlineString:
                        int strToken = BitConverter.ToInt32(il, offset); offset += 4;
                        try { operand = module.ResolveString(strToken); } catch { }
                        break;
                    case OperandType.InlineMethod:
                        int methodToken = BitConverter.ToInt32(il, offset); offset += 4;
                        try { operand = module.ResolveMethod(methodToken); } catch { }
                        break;
                    case OperandType.InlineField:
                        int fieldToken = BitConverter.ToInt32(il, offset); offset += 4;
                        try { operand = module.ResolveField(fieldToken); } catch { }
                        break;
                    case OperandType.InlineType:
                        int typeToken = BitConverter.ToInt32(il, offset); offset += 4;
                        try { operand = module.ResolveType(typeToken); } catch { }
                        break;
                    case OperandType.InlineTok:
                        int memberToken = BitConverter.ToInt32(il, offset); offset += 4;
                        try { operand = module.ResolveMember(memberToken); } catch { }
                        break;
                    case OperandType.InlineBrTarget: operand = BitConverter.ToInt32(il, offset); offset += 4; break;
                    case OperandType.ShortInlineBrTarget: operand = unchecked((sbyte)il[offset++]); break;
                    case OperandType.InlineSwitch:
                        int count = BitConverter.ToInt32(il, offset); offset += 4;
                        int[] targets = new int[count];
                        for (int t = 0; t < count; t++) { targets[t] = BitConverter.ToInt32(il, offset); offset += 4; }
                        operand = targets;
                        break;
                    case OperandType.InlineVar: operand = BitConverter.ToUInt16(il, offset); offset += 2; break;
                    case OperandType.InlineSig: operand = BitConverter.ToInt32(il, offset); offset += 4; break;
                    default: break;
                }

                instructions.Add(new CilInstruction
                {
                    Offset = startOffset,
                    OpCode = opcode,
                    Operand = operand,
                    Size = offset - startOffset
                });
            }

            return instructions;
        }
    }

    class Program
    {
        static void DumpMethod(MethodBase mb)
        {
            if (mb == null) return;
            Console.WriteLine("================================================================================");
            Console.WriteLine("METHOD: " + mb.DeclaringType.FullName + "." + mb.Name);
            Console.WriteLine("Parameters: " + string.Join(", ", Array.ConvertAll(mb.GetParameters(), p => p.ParameterType.Name + " " + p.Name)));
            MethodInfo mi = mb as MethodInfo;
            if (mi != null) Console.WriteLine("Return: " + mi.ReturnType.FullName);
            Console.WriteLine("--------------------------------------------------------------------------------");
            var instructions = CilDecoder.Decode(mb);
            foreach (var inst in instructions)
            {
                Console.WriteLine(inst.ToString());
            }
            Console.WriteLine();
        }

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

            string llaiPath = Path.Combine(modDir, "LivingLSAIs.dll");
            var asm = Assembly.Load(File.ReadAllBytes(llaiPath));

            Type livingLsAisType = asm.GetType("LivingLSAIs.LivingLSAIs");
            Type npcProfileType = asm.GetType("LivingLSAIs.NpcProfile");

            // Dump fields of LivingLSAIs related to rules/paths
            Console.WriteLine("=== LivingLSAIs Fields ===");
            foreach (var f in livingLsAisType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (f.Name.IndexOf("Rule", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.Name.IndexOf("Path", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.Name.IndexOf("Bridge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.Name.IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine("  " + (f.IsStatic ? "static " : "") + f.FieldType.Name + " " + f.Name);
                }
            }

            // Dump fields of NpcProfile related to rules
            Console.WriteLine("\n=== NpcProfile Fields ===");
            foreach (var f in npcProfileType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (f.Name.IndexOf("Rule", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.Name.IndexOf("Custom", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.Name.IndexOf("Prompt", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine("  " + (f.IsStatic ? "static " : "") + f.FieldType.Name + " " + f.Name);
                }
            }

            Console.WriteLine("\n=== TARGET METHODS DISASSEMBLY ===");
            string[] methodNamesLiving = {
                "ResolvePaths",
                "ResolveCustomRulesPath",
                "EnsureCustomRulesFile",
                "ReloadCustomRules",
                "PushCustomRulesToBridge"
            };

            foreach (var mname in methodNamesLiving)
            {
                var m = livingLsAisType.GetMethod(mname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                DumpMethod(m);
            }

            string[] methodNamesNpc = {
                "LoadCustomRules",
                "BuildCustomRulesBlock",
                "ToSystemPrompt"
            };

            foreach (var mname in methodNamesNpc)
            {
                var m = npcProfileType.GetMethod(mname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                DumpMethod(m);
            }
        }
    }
}
