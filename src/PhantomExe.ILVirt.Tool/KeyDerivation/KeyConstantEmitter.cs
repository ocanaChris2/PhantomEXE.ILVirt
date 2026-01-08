// src/PhantomExe.ILVirt.Tool/KeyDerivation/KeyConstantsEmitter.cs
using System;
using System.Linq;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;

using TypeAttributes = AsmResolver.PE.DotNet.Metadata.Tables.Rows.TypeAttributes;
using FieldAttributes = AsmResolver.PE.DotNet.Metadata.Tables.Rows.FieldAttributes;
using MethodAttributes = AsmResolver.PE.DotNet.Metadata.Tables.Rows.MethodAttributes;

namespace PhantomExe.ILVirt.Tool.KeyDerivation
{
    public static class KeyConstantsEmitter
    {
        public static void Emit(byte[] key, ModuleDefinition module)
        {
            var k0 = (byte)(key[0] ^ 0x5A);
            var k1 = (byte)(key[1] ^ 0xC3);

            Console.WriteLine($"[KeyEmitter] Emitting key constants: PartA={k0:X2}, PartB={k1:X2}");

            var ns = module.TopLevelTypes.FirstOrDefault(t => t.Name != "<Module>")?.Namespace ?? "PhantomExe";
            
            var helperType = module.TopLevelTypes
                .FirstOrDefault(t => t.Name == "VmKeyHelper" && t.Namespace == ns);

            if (helperType == null)
            {
                throw new InvalidOperationException($"VmKeyHelper not found! Expected in namespace '{ns}'");
            }

            Console.WriteLine($"[KeyEmitter] Found VmKeyHelper: {helperType.FullName}");

            var fieldA = helperType.Fields.FirstOrDefault(f => f.Name == "PartA");
            var fieldB = helperType.Fields.FirstOrDefault(f => f.Name == "PartB");
            
            if (fieldA == null || fieldB == null)
            {
                throw new InvalidOperationException("PartA or PartB fields not found!");
            }

            // ✅ Create or get the static constructor
            var cctor = helperType.Methods.FirstOrDefault(m => m.Name == ".cctor");
            
            if (cctor == null)
            {
                Console.WriteLine($"[KeyEmitter] Creating static constructor");
                
                cctor = new MethodDefinition(
                    ".cctor",
                    MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    MethodSignature.CreateStatic(module.CorLibTypeFactory.Void));
                
                cctor.CilMethodBody = new CilMethodBody(cctor);
                helperType.Methods.Add(cctor);
            }
            else
            {
                Console.WriteLine($"[KeyEmitter] Updating existing static constructor");
            }

            // Update the static constructor body
            var body = cctor.CilMethodBody!;
            body.Instructions.Clear();
            
            // Set PartA = k0
            body.Instructions.Add(new CilInstruction(CilOpCodes.Ldc_I4, (int)k0));
            body.Instructions.Add(new CilInstruction(CilOpCodes.Stsfld, fieldA));
            
            // Set PartB = k1
            body.Instructions.Add(new CilInstruction(CilOpCodes.Ldc_I4, (int)k1));
            body.Instructions.Add(new CilInstruction(CilOpCodes.Stsfld, fieldB));
            
            body.Instructions.Add(new CilInstruction(CilOpCodes.Ret));

            Console.WriteLine($"[KeyEmitter] Static constructor updated with PartA={k0:X2}, PartB={k1:X2}");
        }
    }
}