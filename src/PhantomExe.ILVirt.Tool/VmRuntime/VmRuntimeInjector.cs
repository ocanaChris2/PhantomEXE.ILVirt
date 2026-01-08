using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PhantomExe.ILVirt.Tool.VmRuntime
{
    public class VmRuntimeInjector
    {
        private readonly AssemblyDefinition _assembly;
        private readonly string _rootNamespace;

        public VmRuntimeInjector(AssemblyDefinition assembly, string rootNamespace)
        {
            _assembly = assembly;
            _rootNamespace = rootNamespace;
        }

        public void Inject()
        {
            EnsureVmKeyHelperExists();
            InjectVmRuntime();
        }

      private void EnsureVmKeyHelperExists()
{
    var module = _assembly.ManifestModule;
    var ns = _rootNamespace;
    
    Console.WriteLine($"[EnsureVmKeyHelper] Namespace: {ns}");
    
    var existingHelper = module?.TopLevelTypes
        .FirstOrDefault(t => t.Name == "VmKeyHelper" && t.Namespace == ns);
    
    if (existingHelper != null)
    {
        Console.WriteLine($"[EnsureVmKeyHelper] VmKeyHelper already exists, skipping creation");
        return;
    }
    
    Console.WriteLine($"[EnsureVmKeyHelper] Creating VmKeyHelper as top-level type");
    
    var helperType = new TypeDefinition(
        ns,
        "VmKeyHelper",
        TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit);

    // ✅ CRITICAL: Set the base type to System.Object
    helperType.BaseType = module!.CorLibTypeFactory.Object.Type;

    var fieldSig = new FieldSignature(module.CorLibTypeFactory.Byte);
    
    // ✅ Create static fields (not const)
    var fieldA = new FieldDefinition(
        "PartA",
        FieldAttributes.Public | FieldAttributes.Static,
        fieldSig);
    
    var fieldB = new FieldDefinition(
        "PartB",
        FieldAttributes.Public | FieldAttributes.Static,
        fieldSig);
    
    helperType.Fields.Add(fieldA);
    helperType.Fields.Add(fieldB);

    // ✅ Add a static constructor to initialize the values
    var cctor = new MethodDefinition(
        ".cctor",
        MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
        MethodSignature.CreateStatic(module.CorLibTypeFactory.Void));

    var cctorBody = new CilMethodBody(cctor);
    
    // Initialize PartA = 0
    cctorBody.Instructions.Add(new CilInstruction(CilOpCodes.Ldc_I4_0));
    cctorBody.Instructions.Add(new CilInstruction(CilOpCodes.Stsfld, fieldA));
    
    // Initialize PartB = 0
    cctorBody.Instructions.Add(new CilInstruction(CilOpCodes.Ldc_I4_0));
    cctorBody.Instructions.Add(new CilInstruction(CilOpCodes.Stsfld, fieldB));
    
    cctorBody.Instructions.Add(new CilInstruction(CilOpCodes.Ret));
    
    cctor.CilMethodBody = cctorBody;
    helperType.Methods.Add(cctor);

    // ✅ Add to TOP LEVEL types (not nested)
    module.TopLevelTypes.Add(helperType);
    
    Console.WriteLine($"[EnsureVmKeyHelper] VmKeyHelper created successfully");
    Console.WriteLine($"[EnsureVmKeyHelper] Type full name: {helperType.FullName}");
    Console.WriteLine($"[EnsureVmKeyHelper] Is nested: {helperType.IsNested}");
}
    

            private void InjectVmRuntime()
                {
            var source = VmRuntimeSource.Generate(_rootNamespace);
            var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

            var compilation = CSharpCompilation.Create("VmRuntime")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Collections.Generic.Stack<>).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Text.Encoding).Assembly.Location),
                    MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Runtime.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.IO.dll")),
                    MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Reflection.dll"))
                )
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);
            
            if (!result.Success)
            {
                var errors = string.Join("\n", result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => $"{d.Id}: {d.GetMessage()}"));
                throw new InvalidOperationException($"Roslyn compilation failed:\n{errors}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            var tempModule = ModuleDefinition.FromBytes(ms.ToArray());

            var importer = new ReferenceImporter(_assembly.ManifestModule!);

            foreach (var sourceType in tempModule.TopLevelTypes.ToList())
            {
                if (sourceType.Name == "<Module>") 
                    continue;

                var targetType = new TypeDefinition(
                    _rootNamespace,
                    sourceType.Name,
                    sourceType.Attributes,
                    sourceType.BaseType != null ? importer.ImportType(sourceType.BaseType) : null
                );

                // Clone interfaces
                foreach (var iface in sourceType.Interfaces)
                {
                    targetType.Interfaces.Add(new InterfaceImplementation(
                        importer.ImportType(iface.Interface!)));
                }

                // Clone fields
                foreach (var sourceField in sourceType.Fields)
                {
                    var targetField = new FieldDefinition(
                        sourceField.Name,
                        sourceField.Attributes,
                        importer.ImportFieldSignature(sourceField.Signature!));
                    
                    if (sourceField.Constant != null)
                    {
                        targetField.Constant = sourceField.Constant;
                    }
                    
                    targetType.Fields.Add(targetField);
                }

                // Clone methods
                foreach (var sourceMethod in sourceType.Methods)
                {
                    var targetMethod = new MethodDefinition(
                        sourceMethod.Name,
                        sourceMethod.Attributes,
                        importer.ImportMethodSignature(sourceMethod.Signature!));

                    // Clone parameters
                    foreach (var param in sourceMethod.ParameterDefinitions)
                    {
                        targetMethod.ParameterDefinitions.Add(new ParameterDefinition(
                            param.Sequence,
                            param.Name,
                            param.Attributes));
                    }

                    // Clone method body
                    if (sourceMethod.CilMethodBody != null)
                    {
                        var sourceBody = sourceMethod.CilMethodBody;
                        var targetBody = new CilMethodBody(targetMethod);

                        // Clone local variables
                        foreach (var local in sourceBody.LocalVariables)
                        {
                            targetBody.LocalVariables.Add(new CilLocalVariable(
                                importer.ImportTypeSignature(local.VariableType)));
                        }

                        // Clone instructions - first pass
                        var instrMap = new Dictionary<CilInstruction, CilInstruction>();
                        foreach (var sourceInstr in sourceBody.Instructions)
                        {
                            var targetInstr = new CilInstruction(sourceInstr.OpCode);
                            
                            if (sourceInstr.Operand != null)
                            {
                                targetInstr.Operand = sourceInstr.Operand switch
                                {
                                    ITypeDefOrRef type => importer.ImportType(type),
                                    IMethodDefOrRef method => ImportMethodSafe(method, importer),
                                    IFieldDescriptor field => importer.ImportField(field),
                                    CilLocalVariable local => targetBody.LocalVariables[sourceBody.LocalVariables.IndexOf(local)],
                                    ICilLabel => sourceInstr.Operand,
                                    IList<ICilLabel> => sourceInstr.Operand,
                                    byte => sourceInstr.Operand,
                                    ushort => sourceInstr.Operand,
                                    sbyte => sourceInstr.Operand,
                                    short => sourceInstr.Operand,
                                    int => sourceInstr.Operand,
                                    long => sourceInstr.Operand,
                                    float => sourceInstr.Operand,
                                    double => sourceInstr.Operand,
                                    string => sourceInstr.Operand,
                                    _ => sourceInstr.Operand
                                };
                            }
                            
                            instrMap[sourceInstr] = targetInstr;
                            targetBody.Instructions.Add(targetInstr);
                        }

                        // Second pass - fix branch targets
                        for (int i = 0; i < sourceBody.Instructions.Count; i++)
                        {
                            var sourceInstr = sourceBody.Instructions[i];
                            var targetInstr = targetBody.Instructions[i];

                            if (sourceInstr.Operand is ICilLabel label)
                            {
                                var labelInstr = GetInstructionFromLabel(label, sourceBody.Instructions);
                                if (labelInstr != null && instrMap.ContainsKey(labelInstr))
                                {
                                    targetInstr.Operand = new CilInstructionLabel(instrMap[labelInstr]);
                                }
                            }
                            else if (sourceInstr.Operand is IList<ICilLabel> switchLabels)
                            {
                                var targetLabels = new List<ICilLabel>();
                                foreach (var switchLabel in switchLabels)
                                {
                                    var labelInstr = GetInstructionFromLabel(switchLabel, sourceBody.Instructions);
                                    if (labelInstr != null && instrMap.ContainsKey(labelInstr))
                                    {
                                        targetLabels.Add(new CilInstructionLabel(instrMap[labelInstr]));
                                    }
                                }
                                targetInstr.Operand = targetLabels;
                            }
                        }

                        // Clone exception handlers
                        foreach (var sourceHandler in sourceBody.ExceptionHandlers)
                        {
                            var targetHandler = new CilExceptionHandler
                            {
                                HandlerType = sourceHandler.HandlerType
                            };

                            if (sourceHandler.TryStart != null)
                            {
                                var instr = GetInstructionFromLabel(sourceHandler.TryStart, sourceBody.Instructions);
                                if (instr != null && instrMap.ContainsKey(instr))
                                    targetHandler.TryStart = new CilInstructionLabel(instrMap[instr]);
                            }
                            
                            if (sourceHandler.TryEnd != null)
                            {
                                var instr = GetInstructionFromLabel(sourceHandler.TryEnd, sourceBody.Instructions);
                                if (instr != null && instrMap.ContainsKey(instr))
                                    targetHandler.TryEnd = new CilInstructionLabel(instrMap[instr]);
                            }
                            
                            if (sourceHandler.HandlerStart != null)
                            {
                                var instr = GetInstructionFromLabel(sourceHandler.HandlerStart, sourceBody.Instructions);
                                if (instr != null && instrMap.ContainsKey(instr))
                                    targetHandler.HandlerStart = new CilInstructionLabel(instrMap[instr]);
                            }
                            
                            if (sourceHandler.HandlerEnd != null)
                            {
                                var instr = GetInstructionFromLabel(sourceHandler.HandlerEnd, sourceBody.Instructions);
                                if (instr != null && instrMap.ContainsKey(instr))
                                    targetHandler.HandlerEnd = new CilInstructionLabel(instrMap[instr]);
                            }

                            if (sourceHandler.ExceptionType != null)
                                targetHandler.ExceptionType = importer.ImportType(sourceHandler.ExceptionType);

                            if (sourceHandler.FilterStart != null)
                            {
                                var instr = GetInstructionFromLabel(sourceHandler.FilterStart, sourceBody.Instructions);
                                if (instr != null && instrMap.ContainsKey(instr))
                                    targetHandler.FilterStart = new CilInstructionLabel(instrMap[instr]);
                            }

                            targetBody.ExceptionHandlers.Add(targetHandler);
                        }

                        targetMethod.CilMethodBody = targetBody;
                    }

                    targetType.Methods.Add(targetMethod);
                }

                _assembly.ManifestModule!.TopLevelTypes.Add(targetType);
            }

    // ✅ CRITICAL: Fix all type references and remove VmRuntime assembly reference
    FixTypeReferences(_assembly.ManifestModule!);
    RemoveVmRuntimeAssemblyReference(_assembly.ManifestModule!);
}

/// <summary>
/// Removes the VmRuntime assembly reference that was created during Roslyn compilation
/// </summary>
private void RemoveVmRuntimeAssemblyReference(ModuleDefinition module)
{
    // Find and remove VmRuntime assembly reference
    var vmRuntimeRef = module.AssemblyReferences
        .FirstOrDefault(a => a.Name == "VmRuntime");
    
    if (vmRuntimeRef != null)
    {
        module.AssemblyReferences.Remove(vmRuntimeRef);
    }
}

/// <summary>
/// Fixes type references in the module to point to types within the current assembly
/// instead of external assemblies
/// </summary>
private void FixTypeReferences(ModuleDefinition module)
{
    var typeCache = new Dictionary<string, TypeDefinition>();
    var typeLookup = new Dictionary<string, TypeDefinition>();
    
    // Build cache of all types in the current module (including newly added ones)
    foreach (var type in module.TopLevelTypes)
    {
        if (type.Name != "<Module>")
        {
            typeCache[type.FullName] = type;
            // Also cache by simple name for easier lookup
            typeLookup[type.Name!] = type;
        }
    }

    // Fix references in all types
    foreach (var type in module.TopLevelTypes)
    {
        if (type.Name == "<Module>") continue;

        // Fix method references in method bodies
        foreach (var method in type.Methods)
        {
            if (method.CilMethodBody == null) continue;

            foreach (var instr in method.CilMethodBody.Instructions)
            {
                // Fix method calls
                if (instr.Operand is IMethodDefOrRef methodRef)
                {
                    var declaringType = methodRef.DeclaringType;
                    if (declaringType != null)
                    {
                        // Try to find local type by full name or simple name
                        TypeDefinition? localType = null;
                        
                        if (typeCache.TryGetValue(declaringType.FullName, out localType) ||
                            typeLookup.TryGetValue(declaringType.Name!, out localType))
                        {
                            // Find matching method in local type
                            var localMethod = localType.Methods
                                .FirstOrDefault(m => m.Name == methodRef.Name && 
                                                     SignaturesMatch(m.Signature, methodRef.Signature));
                            
                            if (localMethod != null)
                            {
                                instr.Operand = localMethod;
                            }
                        }
                    }
                }
                // Fix field references
                else if (instr.Operand is IFieldDescriptor fieldRef)
                {
                    var declaringType = fieldRef.DeclaringType;
                    if (declaringType != null)
                    {
                        TypeDefinition? localType = null;
                        
                        if (typeCache.TryGetValue(declaringType.FullName, out localType) ||
                            typeLookup.TryGetValue(declaringType.Name!, out localType))
                        {
                            var localField = localType.Fields
                                .FirstOrDefault(f => f.Name == fieldRef.Name);
                            
                            if (localField != null)
                            {
                                instr.Operand = localField;
                            }
                        }
                    }
                }
                // Fix type references (for type tokens)
                else if (instr.Operand is ITypeDefOrRef typeRef)
                {
                    TypeDefinition? localType = null;
                    
                    if (typeCache.TryGetValue(typeRef.FullName, out localType) ||
                        typeLookup.TryGetValue(typeRef.Name!, out localType))
                    {
                        instr.Operand = localType;
                    }
                }
            }
        }
    }
}

/// <summary>
/// Checks if two method signatures match
/// </summary>
private bool SignaturesMatch(MethodSignature? sig1, MethodSignature? sig2)
{
    if (sig1 == null || sig2 == null) return false;
    if (sig1.ParameterTypes.Count != sig2.ParameterTypes.Count) return false;
    
    for (int i = 0; i < sig1.ParameterTypes.Count; i++)
    {
        if (sig1.ParameterTypes[i].FullName != sig2.ParameterTypes[i].FullName)
            return false;
    }
    
    return sig1.ReturnType.FullName == sig2.ReturnType.FullName;
}

        /// <summary>
        /// Safely imports a method, handling generic instantiations
        /// </summary>
        private IMethodDefOrRef ImportMethodSafe(IMethodDefOrRef method, ReferenceImporter importer)
        {
            try
            {
                return importer.ImportMethod(method);
            }
            catch
            {
                // If import fails (e.g., generic instantiation issues), try to get the method definition
                if (method is MethodSpecification methodSpec && methodSpec.Method != null)
                {
                    return importer.ImportMethod(methodSpec.Method);
                }
                return method;
            }
        }

        /// <summary>
        /// Safely imports a member reference
        /// </summary>
        private IMemberDescriptor ImportMemberSafe(MemberReference member, ReferenceImporter importer)
        {
            try
            {
                if (member is IMethodDefOrRef method)
                    return ImportMethodSafe(method, importer);
                if (member is IFieldDescriptor field)
                    return importer.ImportField(field);
                if (member is ITypeDefOrRef type)
                    return importer.ImportType(type);
            }
            catch
            {
                // Return original if import fails
            }
            return member;
        }

        /// <summary>
        /// Helper method to get the instruction from an ICilLabel
        /// </summary>
        private CilInstruction? GetInstructionFromLabel(ICilLabel label, CilInstructionCollection instructions)
        {
            if (label is CilInstructionLabel instructionLabel)
            {
                return instructionLabel.Instruction;
            }
            
            if (label is CilOffsetLabel offsetLabel)
            {
                return instructions.GetByOffset(offsetLabel.Offset);
            }
            
            return null;
        }
    }
}