using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;

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
            return string.Format("IL_{0:X4}: {1,-10} {2}", Offset, OpCode.Name, Operand != null ? Operand.ToString() : "");
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

            // First catalog LemonUI.SHVDN3.dll
            Console.WriteLine("=== LemonUI.SHVDN3.dll Catalog ===");
            string lemonPath = Path.Combine(modDir, "LemonUI.SHVDN3.dll");
            if (File.Exists(lemonPath))
            {
                var lemonAsm = Assembly.Load(File.ReadAllBytes(lemonPath));
                Console.WriteLine("LemonUI FullName: " + lemonAsm.FullName);
                var lemonTypes = lemonAsm.GetTypes();
                Console.WriteLine("LemonUI Types count: " + lemonTypes.Length);
                foreach (var t in lemonTypes.Take(20))
                {
                    Console.WriteLine("  LemonUI Type: " + t.FullName);
                }
            }

            // Catalog LivingLSAIs.dll
            Console.WriteLine("\n=== LivingLSAIs.dll Catalog ===");
            string llaiPath = Path.Combine(modDir, "LivingLSAIs.dll");
            var llaiAsm = Assembly.Load(File.ReadAllBytes(llaiPath));
            Console.WriteLine("LivingLSAIs FullName: " + llaiAsm.FullName);
            var types = llaiAsm.GetTypes();
            Console.WriteLine("Total Types: " + types.Length);

            var namespaces = types.Select(t => t.Namespace).Distinct().OrderBy(n => n);
            Console.WriteLine("\n--- Namespaces ---");
            foreach (var ns in namespaces)
            {
                Console.WriteLine("  " + (ns ?? "<global>"));
            }

            Console.WriteLine("\n--- Types with interesting names ---");
            foreach (var t in types)
            {
                string fn = t.FullName;
                if (fn.IndexOf("Rule", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fn.IndexOf("Bridge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fn.IndexOf("Config", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fn.IndexOf("Profile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fn.IndexOf("Prompt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fn.IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fn.IndexOf("Custom", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fn.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine("  Type: " + fn);
                }
            }

            // Search for methods that reference "CUSTOM RULES" or "RULES" or "setCustomRules"
            Console.WriteLine("\n--- Searching method IL strings for 'RULES' or 'CUSTOM' or 'setCustomRules' ---");
            foreach (var t in types)
            {
                MethodBase[] methods;
                try
                {
                    methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                }
                catch { continue; }

                foreach (var m in methods)
                {
                    List<CilInstruction> instructions;
                    try { instructions = CilDecoder.Decode(m); } catch { continue; }

                    bool hasTarget = false;
                    foreach (var inst in instructions)
                    {
                        if (inst.OpCode == OpCodes.Ldstr)
                        {
                            string s = inst.Operand as string;
                            if (s != null && (s.IndexOf("CUSTOM RULES", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                s.IndexOf("setCustomRules", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                s.IndexOf("CUSTOM_RULES", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                s.IndexOf("PLAYER RULES", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                hasTarget = true;
                                break;
                            }
                        }
                    }

                    if (hasTarget)
                    {
                        Console.WriteLine("\nFOUND TARGET METHOD: " + t.FullName + "." + m.Name);
                        foreach (var inst in instructions)
                        {
                            Console.WriteLine("    " + inst.ToString());
                        }
                    }
                }
            }
        }
    }
}
