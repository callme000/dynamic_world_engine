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
                if (opcode.Size == 1)
                    OneByteOpCodes[opcode.Value] = opcode;
                else
                    TwoByteOpCodes[opcode.Value] = opcode;
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
                if (!TwoByteOpCodes.TryGetValue(val, out opcode))
                    continue;
            }
            else
            {
                if (!OneByteOpCodes.TryGetValue(b, out opcode))
                    continue;
            }

            object operand = null;
            switch (opcode.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineI:
                    operand = unchecked((sbyte)il[offset++]);
                    break;
                case OperandType.ShortInlineVar:
                    operand = il[offset++];
                    break;
                case OperandType.ShortInlineR:
                    operand = BitConverter.ToSingle(il, offset);
                    offset += 4;
                    break;
                case OperandType.InlineI:
                    operand = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    break;
                case OperandType.InlineI8:
                    operand = BitConverter.ToInt64(il, offset);
                    offset += 8;
                    break;
                case OperandType.InlineR:
                    operand = BitConverter.ToDouble(il, offset);
                    offset += 8;
                    break;
                case OperandType.InlineString:
                    int strToken = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    try { operand = module.ResolveString(strToken); } catch { operand = null; }
                    break;
                case OperandType.InlineMethod:
                    int methodToken = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    try { operand = module.ResolveMethod(methodToken); } catch { operand = null; }
                    break;
                case OperandType.InlineField:
                    int fieldToken = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    try { operand = module.ResolveField(fieldToken); } catch { operand = null; }
                    break;
                case OperandType.InlineType:
                    int typeToken = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    try { operand = module.ResolveType(typeToken); } catch { operand = null; }
                    break;
                case OperandType.InlineTok:
                    int memberToken = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    try { operand = module.ResolveMember(memberToken); } catch { operand = null; }
                    break;
                case OperandType.InlineBrTarget:
                    operand = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    break;
                case OperandType.ShortInlineBrTarget:
                    operand = unchecked((sbyte)il[offset++]);
                    break;
                case OperandType.InlineSwitch:
                    int count = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    int[] targets = new int[count];
                    for (int t = 0; t < count; t++)
                    {
                        targets[t] = BitConverter.ToInt32(il, offset);
                        offset += 4;
                    }
                    operand = targets;
                    break;
                case OperandType.InlineVar:
                    operand = BitConverter.ToUInt16(il, offset);
                    offset += 2;
                    break;
                case OperandType.InlineSig:
                    operand = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    break;
                default:
                    break;
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

        int index = 0;
        for (int i = 0; i < instructions.Count; i++)
        {
            var inst = instructions[i];
            if (inst.OpCode == OpCodes.Newobj)
            {
                var mb = inst.Operand as MethodBase;
                if (mb != null && mb.DeclaringType != null && mb.DeclaringType.Name.Contains("ActiveInterior"))
                {
                    int dupIndex = -1;
                    for (int k = i - 1; k >= Math.Max(0, i - 35); k--)
                    {
                        if (instructions[k].OpCode == OpCodes.Dup)
                        {
                            dupIndex = k;
                            break;
                        }
                    }

                    int startSlice = dupIndex >= 0 ? dupIndex + 1 : Math.Max(0, i - 25);
                    var slice = instructions.GetRange(startSlice, i - startSlice + 1);

                    Stack<object> stack = new Stack<object>();
                    foreach (var sInst in slice)
                    {
                        OpCode op = sInst.OpCode;
                        if (op == OpCodes.Nop) continue;
                        if (op == OpCodes.Ldstr) stack.Push(sInst.Operand as string ?? "");
                        else if (op == OpCodes.Ldc_R4) stack.Push(sInst.Operand != null ? (float)sInst.Operand : 0f);
                        else if (op == OpCodes.Ldc_I4) stack.Push(sInst.Operand != null ? (int)sInst.Operand : 0);
                        else if (op == OpCodes.Ldc_I4_S) stack.Push(sInst.Operand != null ? Convert.ToInt32(sInst.Operand) : 0);
                        else if (op == OpCodes.Ldc_I4_0) stack.Push(0);
                        else if (op == OpCodes.Ldc_I4_1) stack.Push(1);
                        else if (op == OpCodes.Ldc_I4_2) stack.Push(2);
                        else if (op == OpCodes.Ldc_I4_3) stack.Push(3);
                        else if (op == OpCodes.Ldc_I4_4) stack.Push(4);
                        else if (op == OpCodes.Ldc_I4_5) stack.Push(5);
                        else if (op == OpCodes.Ldc_I4_6) stack.Push(6);
                        else if (op == OpCodes.Ldc_I4_7) stack.Push(7);
                        else if (op == OpCodes.Ldc_I4_8) stack.Push(8);
                        else if (op == OpCodes.Ldc_I4_M1) stack.Push(-1);
                        else if (op == OpCodes.Newobj)
                        {
                            var sMb = sInst.Operand as MethodBase;
                            if (sMb != null && sMb.DeclaringType != null)
                            {
                                if (sMb.DeclaringType.Name == "Vector3" && stack.Count >= 3)
                                {
                                    float z = Convert.ToSingle(stack.Pop());
                                    float y = Convert.ToSingle(stack.Pop());
                                    float x = Convert.ToSingle(stack.Pop());
                                    stack.Push(new GTA.Math.Vector3(x, y, z));
                                }
                                else if (sMb.DeclaringType.Name.Contains("ActiveInterior"))
                                {
                                    index++;
                                    var pars = sMb.GetParameters();
                                    if (pars.Length == 3 && stack.Count >= 3)
                                    {
                                        string name = stack.Pop() as string;
                                        int blip = Convert.ToInt32(stack.Pop());
                                        object posObj = stack.Pop();
                                        GTA.Math.Vector3 pos = posObj is GTA.Math.Vector3 ? (GTA.Math.Vector3)posObj : GTA.Math.Vector3.Zero;
                                        Console.WriteLine(string.Format("#{0} [3-param] Name='{1}', Pos=({2:F2}, {3:F2}, {4:F2})",
                                            index, name, pos.X, pos.Y, pos.Z));
                                    }
                                    else if (pars.Length == 5 && stack.Count >= 5)
                                    {
                                        object exitObj = stack.Pop();
                                        object enterObj = stack.Pop();
                                        string name = stack.Pop() as string;
                                        int blip = Convert.ToInt32(stack.Pop());
                                        object posObj = stack.Pop();

                                        GTA.Math.Vector3 exit = exitObj is GTA.Math.Vector3 ? (GTA.Math.Vector3)exitObj : GTA.Math.Vector3.Zero;
                                        GTA.Math.Vector3 enter = enterObj is GTA.Math.Vector3 ? (GTA.Math.Vector3)enterObj : GTA.Math.Vector3.Zero;
                                        GTA.Math.Vector3 pos = posObj is GTA.Math.Vector3 ? (GTA.Math.Vector3)posObj : GTA.Math.Vector3.Zero;

                                        Console.WriteLine(string.Format("#{0} [5-param] Name='{1}', Pos=({2:F2}, {3:F2}, {4:F2}), Enter=({5:F2}, {6:F2}, {7:F2}), Exit=({8:F2}, {9:F2}, {10:F2})",
                                            index, name, pos.X, pos.Y, pos.Z, enter.X, enter.Y, enter.Z, exit.X, exit.Y, exit.Z));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
