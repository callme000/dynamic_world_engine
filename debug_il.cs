using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;

class CilInstruction
{
    public int Offset { get; set; }
    public OpCode OpCode { get; set; }
    public object Operand { get; set; }
    public int Size { get; set; }

    public override string ToString()
    {
        return string.Format("[{0}] {1} {2}", Offset, OpCode.Name, Operand != null ? Operand.ToString() : "");
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
        string dllPath = @"C:\Program Files\Epic Games\GTAVEnhanced\scripts\EnableAllinteriors.dll";
        AppDomain.CurrentDomain.AssemblyResolve += (s, r) => {
            string n = new AssemblyName(r.Name).Name + ".dll";
            string[] paths = { Path.Combine(@"C:\Users\melod\Documents\gtavmods\lib", n), Path.Combine(@"C:\Program Files\Epic Games\GTAVEnhanced\scripts", n) };
            foreach (var p in paths) if (File.Exists(p)) return Assembly.LoadFrom(p);
            return null;
        };
        Assembly asm = Assembly.Load(File.ReadAllBytes(dllPath));
        Type c1 = asm.GetType("EnableAllinteriors.Class1");
        MethodBase cctor = c1.TypeInitializer;

        var instructions = CilDecoder.Decode(cctor);

        // Print instructions around bytes 12050 to 12300
        foreach (var inst in instructions)
        {
            if (inst.Offset >= 12050 && inst.Offset <= 12250)
            {
                Console.WriteLine(inst);
            }
        }
    }
}
