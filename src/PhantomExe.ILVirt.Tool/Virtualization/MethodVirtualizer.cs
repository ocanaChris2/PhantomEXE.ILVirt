using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;

using AsmResolverManifestResourceAttributes = 
    AsmResolver.PE.DotNet.Metadata.Tables.Rows.ManifestResourceAttributes;

namespace PhantomExe.ILVirt.Tool.Virtualization
{
    public static class MethodVirtualizer
    {
        public static void Virtualize(
            MethodDefinition method,
            AssemblyDefinition assembly,
            string rootNamespace)
        {
            var bytecode = IlToBytecode(method);
            var key = KeyDerivation.KeyGenerator.GenerateKey(method);

            // [DEBUG] Key and bytecode dump
            Console.WriteLine($"[DEBUG] Encryption key: {BitConverter.ToString(key)}");
            Console.WriteLine($"[DEBUG] Bytecode before encryption: {BitConverter.ToString(bytecode)}");
            
            var encrypted = XorEncrypt(bytecode, key);

            Console.WriteLine($"[DEBUG] Bytecode after encryption: {BitConverter.ToString(encrypted)}");

            var resourceName = $"PhantomExe.BC.{method.MetadataToken.ToInt32():X8}";
            
            if (assembly.ManifestModule == null)
                throw new InvalidOperationException("Assembly has no manifest module.");
            
            var resource = new ManifestResource(
                resourceName,
                AsmResolverManifestResourceAttributes.Public,
                new AsmResolver.DataSegment(encrypted));
            
            assembly.ManifestModule.Resources.Add(resource);

            KeyDerivation.KeyConstantsEmitter.Emit(key, method.DeclaringType!.Module!);
            KeyDerivation.ResourceKeyEncoder.Encode(key[3], assembly);

            ReplaceMethodBody(method, resourceName, rootNamespace);
        }

        private static byte[] IlToBytecode(MethodDefinition method)
        {
            if (method.CilMethodBody == null)
                throw new InvalidOperationException($"Method {method.Name} has no CIL body.");
            
            Console.WriteLine($"[DEBUG] Translating {method.Name}:");
            
            using var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);

            // First pass: build offset map for branch targets
            var offsetMap = new Dictionary<int, int>();
            var currentOffset = 0;
            
            foreach (var instr in method.CilMethodBody.Instructions)
            {
                offsetMap[instr.Offset] = currentOffset;
                currentOffset += GetBytecodeSize(instr);
            }

            // Second pass: emit bytecode
            foreach (var instr in method.CilMethodBody.Instructions)
            {
                Console.WriteLine($"  IL_{instr.Offset:X4}: {instr.OpCode.Code}");
                
                switch (instr.OpCode.Code)
                {
                    // Load constants
                    case CilCode.Ldc_I4:
                    case CilCode.Ldc_I4_S:
                    case CilCode.Ldc_I4_0:
                    case CilCode.Ldc_I4_1:
                    case CilCode.Ldc_I4_2:
                    case CilCode.Ldc_I4_3:
                    case CilCode.Ldc_I4_4:
                    case CilCode.Ldc_I4_5:
                    case CilCode.Ldc_I4_6:
                    case CilCode.Ldc_I4_7:
                    case CilCode.Ldc_I4_8:
                    case CilCode.Ldc_I4_M1:
                        writer.Write(BytecodeFormat.OP_LDC_I4);
                        int intValue = instr.GetLdcI4Constant();
                        writer.Write(intValue);
                        Console.WriteLine($"    -> OP_LDC_I4({intValue})");
                        break;

                    case CilCode.Ldstr:
                        if (instr.Operand is string strValue)
                        {
                            var bytes = Encoding.UTF8.GetBytes(strValue);
                            writer.Write(BytecodeFormat.OP_LDC_STR);
                            writer.Write((ushort)bytes.Length);
                            writer.Write(bytes);
                            Console.WriteLine($"    -> OP_LDC_STR(\"{strValue}\")");
                        }
                        break;

                    // Load arguments - Handle both short and long forms
                    case CilCode.Ldarg_0:
                        writer.Write(BytecodeFormat.OP_LDARG);
                        writer.Write((byte)0);
                        Console.WriteLine($"    -> OP_LDARG(0)");
                        break;

                    case CilCode.Ldarg_1:
                        writer.Write(BytecodeFormat.OP_LDARG);
                        writer.Write((byte)1);
                        Console.WriteLine($"    -> OP_LDARG(1)");
                        break;

                    case CilCode.Ldarg_2:
                        writer.Write(BytecodeFormat.OP_LDARG);
                        writer.Write((byte)2);
                        Console.WriteLine($"    -> OP_LDARG(2)");
                        break;

                    case CilCode.Ldarg_3:
                        writer.Write(BytecodeFormat.OP_LDARG);
                        writer.Write((byte)3);
                        Console.WriteLine($"    -> OP_LDARG(3)");
                        break;

                    case CilCode.Ldarg_S:
                    case CilCode.Ldarg:
                        writer.Write(BytecodeFormat.OP_LDARG);
                        byte argIndex = GetArgumentIndex(instr);
                        writer.Write(argIndex);
                        Console.WriteLine($"    -> OP_LDARG({argIndex})");
                        break;

                    // Arithmetic operations
                    case CilCode.Add:
                        writer.Write(BytecodeFormat.OP_ADD);
                        Console.WriteLine($"    -> OP_ADD");
                        break;

                    case CilCode.Sub:
                        writer.Write(BytecodeFormat.OP_SUB);
                        Console.WriteLine($"    -> OP_SUB");
                        break;

                    case CilCode.Mul:
                        writer.Write(BytecodeFormat.OP_MUL);
                        Console.WriteLine($"    -> OP_MUL");
                        break;

                    case CilCode.Div:
                        writer.Write(BytecodeFormat.OP_DIV);
                        Console.WriteLine($"    -> OP_DIV");
                        break;

                    case CilCode.Rem:
                        writer.Write(BytecodeFormat.OP_REM);
                        Console.WriteLine($"    -> OP_REM");
                        break;

                    // Comparison operations
                    case CilCode.Ceq:
                        writer.Write(BytecodeFormat.OP_CEQ);
                        Console.WriteLine($"    -> OP_CEQ");
                        break;

                    case CilCode.Cgt:
                    case CilCode.Cgt_Un:
                        writer.Write(BytecodeFormat.OP_CGT);
                        Console.WriteLine($"    -> OP_CGT");
                        break;

                    case CilCode.Clt:
                    case CilCode.Clt_Un:
                        writer.Write(BytecodeFormat.OP_CLT);
                        Console.WriteLine($"    -> OP_CLT");
                        break;

                    // Branching
                    case CilCode.Br:
                    case CilCode.Br_S:
                        writer.Write(BytecodeFormat.OP_BR);
                        WriteBranchTarget(writer, instr, offsetMap);
                        Console.WriteLine($"    -> OP_BR");
                        break;

                    case CilCode.Brtrue:
                    case CilCode.Brtrue_S:
                        writer.Write(BytecodeFormat.OP_BRTRUE);
                        WriteBranchTarget(writer, instr, offsetMap);
                        Console.WriteLine($"    -> OP_BRTRUE");
                        break;

                    case CilCode.Brfalse:
                    case CilCode.Brfalse_S:
                        writer.Write(BytecodeFormat.OP_BRFALSE);
                        WriteBranchTarget(writer, instr, offsetMap);
                        Console.WriteLine($"    -> OP_BRFALSE");
                        break;

                    case CilCode.Beq:
                    case CilCode.Beq_S:
                        writer.Write(BytecodeFormat.OP_BEQ);
                        WriteBranchTarget(writer, instr, offsetMap);
                        Console.WriteLine($"    -> OP_BEQ");
                        break;

                    case CilCode.Bne_Un:
                    case CilCode.Bne_Un_S:
                        writer.Write(BytecodeFormat.OP_BNE);
                        WriteBranchTarget(writer, instr, offsetMap);
                        Console.WriteLine($"    -> OP_BNE");
                        break;

                    case CilCode.Bgt:
                    case CilCode.Bgt_S:
                    case CilCode.Bgt_Un:
                    case CilCode.Bgt_Un_S:
                        writer.Write(BytecodeFormat.OP_BGT);
                        WriteBranchTarget(writer, instr, offsetMap);
                        Console.WriteLine($"    -> OP_BGT");
                        break;

                    case CilCode.Bge:
                    case CilCode.Bge_S:
                    case CilCode.Bge_Un:
                    case CilCode.Bge_Un_S:
                        writer.Write(BytecodeFormat.OP_BGE);
                        WriteBranchTarget(writer, instr, offsetMap);
                        Console.WriteLine($"    -> OP_BGE");
                        break;

                    case CilCode.Blt:
                    case CilCode.Blt_S:
                    case CilCode.Blt_Un:
                    case CilCode.Blt_Un_S:
                        writer.Write(BytecodeFormat.OP_BLT);
                        WriteBranchTarget(writer, instr, offsetMap);
                        Console.WriteLine($"    -> OP_BLT");
                        break;

                    case CilCode.Ble:
                    case CilCode.Ble_S:
                    case CilCode.Ble_Un:
                    case CilCode.Ble_Un_S:
                        writer.Write(BytecodeFormat.OP_BLE);
                        WriteBranchTarget(writer, instr, offsetMap);
                        Console.WriteLine($"    -> OP_BLE");
                        break;

                    // Stack operations
                    case CilCode.Dup:
                        writer.Write(BytecodeFormat.OP_DUP);
                        Console.WriteLine($"    -> OP_DUP");
                        break;

                    case CilCode.Pop:
                        writer.Write(BytecodeFormat.OP_POP);
                        Console.WriteLine($"    -> OP_POP");
                        break;

                    // Store/Load locals
                    case CilCode.Stloc_0:
                        writer.Write(BytecodeFormat.OP_STLOC);
                        writer.Write((byte)0);
                        Console.WriteLine($"    -> OP_STLOC(0)");
                        break;

                    case CilCode.Stloc_1:
                        writer.Write(BytecodeFormat.OP_STLOC);
                        writer.Write((byte)1);
                        Console.WriteLine($"    -> OP_STLOC(1)");
                        break;

                    case CilCode.Stloc_2:
                        writer.Write(BytecodeFormat.OP_STLOC);
                        writer.Write((byte)2);
                        Console.WriteLine($"    -> OP_STLOC(2)");
                        break;

                    case CilCode.Stloc_3:
                        writer.Write(BytecodeFormat.OP_STLOC);
                        writer.Write((byte)3);
                        Console.WriteLine($"    -> OP_STLOC(3)");
                        break;

                    case CilCode.Stloc_S:
                    case CilCode.Stloc:
                        writer.Write(BytecodeFormat.OP_STLOC);
                        byte stlocIndex = GetLocalIndex(instr);
                        writer.Write(stlocIndex);
                        Console.WriteLine($"    -> OP_STLOC({stlocIndex})");
                        break;

                    case CilCode.Ldloc_0:
                        writer.Write(BytecodeFormat.OP_LDLOC);
                        writer.Write((byte)0);
                        Console.WriteLine($"    -> OP_LDLOC(0)");
                        break;

                    case CilCode.Ldloc_1:
                        writer.Write(BytecodeFormat.OP_LDLOC);
                        writer.Write((byte)1);
                        Console.WriteLine($"    -> OP_LDLOC(1)");
                        break;

                    case CilCode.Ldloc_2:
                        writer.Write(BytecodeFormat.OP_LDLOC);
                        writer.Write((byte)2);
                        Console.WriteLine($"    -> OP_LDLOC(2)");
                        break;

                    case CilCode.Ldloc_3:
                        writer.Write(BytecodeFormat.OP_LDLOC);
                        writer.Write((byte)3);
                        Console.WriteLine($"    -> OP_LDLOC(3)");
                        break;

                    case CilCode.Ldloc_S:
                    case CilCode.Ldloc:
                        writer.Write(BytecodeFormat.OP_LDLOC);
                        byte ldlocIndex = GetLocalIndex(instr);
                        writer.Write(ldlocIndex);
                        Console.WriteLine($"    -> OP_LDLOC({ldlocIndex})");
                        break;

                    case CilCode.Ret:
                        writer.Write(BytecodeFormat.OP_RET);
                        Console.WriteLine($"    -> OP_RET");
                        break;

                    case CilCode.Nop:
                        writer.Write(BytecodeFormat.OP_NOP);
                        Console.WriteLine($"    -> OP_NOP");
                        break;

                    default:
                        throw new NotSupportedException(
                            $"Opcode {instr.OpCode.Code} is not yet supported in virtualization. " +
                            $"Method: {method.FullName}, Offset: {instr.Offset}");
                }
            }

            var bytecode = ms.ToArray();
            Console.WriteLine($"[DEBUG] Generated {bytecode.Length} bytes");
            Console.WriteLine($"[DEBUG] Bytecode hex: {BitConverter.ToString(bytecode)}");
            
            return bytecode;
        }

        private static byte GetArgumentIndex(CilInstruction instr)
        {
            if (instr.Operand is byte b)
                return b;
            if (instr.Operand is int i)
                return (byte)i;
            if (instr.Operand is short s)
                return (byte)s;
            
            throw new InvalidOperationException($"Cannot extract argument index from operand type {instr.Operand?.GetType()}");
        }

        private static byte GetLocalIndex(CilInstruction instr)
        {
            if (instr.Operand is byte b)
                return b;
            if (instr.Operand is int i)
                return (byte)i;
            if (instr.Operand is short s)
                return (byte)s;
            if (instr.Operand is CilLocalVariable local)
                return (byte)local.Index;
            
            throw new InvalidOperationException($"Cannot extract local index from operand type {instr.Operand?.GetType()}");
        }

        private static void WriteBranchTarget(BinaryWriter writer, CilInstruction instr, Dictionary<int, int> offsetMap)
        {
            if (instr.Operand is ICilLabel label)
            {
                var targetInstr = label is CilInstructionLabel instrLabel 
                    ? instrLabel.Instruction 
                    : null;
                
                if (targetInstr != null && offsetMap.TryGetValue(targetInstr.Offset, out int targetOffset))
                {
                    writer.Write(targetOffset);
                }
                else
                {
                    writer.Write(0);
                }
            }
            else
            {
                writer.Write(0);
            }
        }

        private static int GetBytecodeSize(CilInstruction instr)
        {
            switch (instr.OpCode.Code)
            {
                case CilCode.Ldc_I4:
                case CilCode.Ldc_I4_S:
                case CilCode.Ldc_I4_0:
                case CilCode.Ldc_I4_1:
                case CilCode.Ldc_I4_2:
                case CilCode.Ldc_I4_3:
                case CilCode.Ldc_I4_4:
                case CilCode.Ldc_I4_5:
                case CilCode.Ldc_I4_6:
                case CilCode.Ldc_I4_7:
                case CilCode.Ldc_I4_8:
                case CilCode.Ldc_I4_M1:
                    return 5; // opcode(1) + int32(4)

                case CilCode.Ldstr:
                    if (instr.Operand is string str)
                    {
                        var bytes = Encoding.UTF8.GetBytes(str);
                        return 1 + 2 + bytes.Length; // opcode + length + data
                    }
                    return 1;

                case CilCode.Ldarg_S:
                case CilCode.Ldarg:
                case CilCode.Stloc_S:
                case CilCode.Stloc:
                case CilCode.Ldloc_S:
                case CilCode.Ldloc:
                case CilCode.Ldarg_0:
                case CilCode.Ldarg_1:
                case CilCode.Ldarg_2:
                case CilCode.Ldarg_3:
                case CilCode.Stloc_0:
                case CilCode.Stloc_1:
                case CilCode.Stloc_2:
                case CilCode.Stloc_3:
                case CilCode.Ldloc_0:
                case CilCode.Ldloc_1:
                case CilCode.Ldloc_2:
                case CilCode.Ldloc_3:
                    return 2; // opcode(1) + index(1)

                case CilCode.Br:
                case CilCode.Br_S:
                case CilCode.Brtrue:
                case CilCode.Brtrue_S:
                case CilCode.Brfalse:
                case CilCode.Brfalse_S:
                case CilCode.Beq:
                case CilCode.Beq_S:
                case CilCode.Bne_Un:
                case CilCode.Bne_Un_S:
                case CilCode.Bgt:
                case CilCode.Bgt_S:
                case CilCode.Bgt_Un:
                case CilCode.Bgt_Un_S:
                case CilCode.Bge:
                case CilCode.Bge_S:
                case CilCode.Bge_Un:
                case CilCode.Bge_Un_S:
                case CilCode.Blt:
                case CilCode.Blt_S:
                case CilCode.Blt_Un:
                case CilCode.Blt_Un_S:
                case CilCode.Ble:
                case CilCode.Ble_S:
                case CilCode.Ble_Un:
                case CilCode.Ble_Un_S:
                    return 5; // opcode(1) + offset(4)

                default:
                    return 1; // Just opcode
            }
        }

        private static byte[] XorEncrypt(byte[] data, byte[] key)
        {
            var result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                result[i] = (byte)(data[i] ^ key[i % key.Length]);
            return result;
        }

        private static void ReplaceMethodBody(
            MethodDefinition method,
            string resourceName,
            string ns)
        {
            var cil = method.CilMethodBody!;
            cil.Instructions.Clear();

            var module = method.DeclaringType!.Module!;
            var vmType = module.TopLevelTypes.FirstOrDefault(t => 
                t.FullName == $"{ns}.VmRuntime");
            
            if (vmType == null)
                throw new InvalidOperationException($"Type '{ns}.VmRuntime' not found");

            var executeMethod = vmType.Methods.FirstOrDefault(m => 
                m.Name == "Execute" && m.Signature?.ParameterTypes.Count == 2);
            
            if (executeMethod == null)
                throw new InvalidOperationException("VmRuntime.Execute method not found");

            // Load resource name
            cil.Instructions.Add(new CilInstruction(CilOpCodes.Ldstr, resourceName));
            
            // Create array for arguments
            var paramCount = method.Parameters.Count;
            cil.Instructions.Add(new CilInstruction(CilOpCodes.Ldc_I4, paramCount));
            cil.Instructions.Add(new CilInstruction(CilOpCodes.Newarr, 
                module.CorLibTypeFactory.Object.Type));
            
            // Fill array with arguments
            for (int i = 0; i < paramCount; i++)
            {
                cil.Instructions.Add(new CilInstruction(CilOpCodes.Dup));
                cil.Instructions.Add(new CilInstruction(CilOpCodes.Ldc_I4, i));
                cil.Instructions.Add(new CilInstruction(CilOpCodes.Ldarg, method.Parameters[i]));
                
                // Box value types
                if (method.Parameters[i].ParameterType.IsValueType)
                {
                    cil.Instructions.Add(new CilInstruction(CilOpCodes.Box, 
                        method.Parameters[i].ParameterType.ToTypeDefOrRef()));
                }
                
                cil.Instructions.Add(new CilInstruction(CilOpCodes.Stelem_Ref));
            }
            
            // Call VmRuntime.Execute(resourceName, args)
            cil.Instructions.Add(new CilInstruction(CilOpCodes.Call, executeMethod));
            
            // Handle return value
            if (method.Signature?.ReturnType.FullName != "System.Void")
            {
                var returnType = method.Signature.ReturnType;
                if (returnType.IsValueType)
                {
                    cil.Instructions.Add(new CilInstruction(CilOpCodes.Unbox_Any, 
                        returnType.ToTypeDefOrRef()));
                }
                else
                {
                    cil.Instructions.Add(new CilInstruction(CilOpCodes.Castclass, 
                        returnType.ToTypeDefOrRef()));
                }
            }
            else
            {
                cil.Instructions.Add(new CilInstruction(CilOpCodes.Pop));
            }
            
            cil.Instructions.Add(new CilInstruction(CilOpCodes.Ret));
        }
    }
}