// src/PhantomExe.ILVirt.Tool/Program.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static PhantomExe.ILVirt.Tool.AssemblyLoader.Loader;
using PhantomExe.ILVirt.Tool.Virtualization;
using PhantomExe.ILVirt.Tool.VmRuntime;
using AsmResolver.DotNet;

namespace PhantomExe.ILVirt.Tool
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: PhantomExe.ILVirt.Tool <input.dll> <output.dll> [--auto]");
                Console.WriteLine("  --auto: Automatically virtualize all eligible methods without prompts");
                Environment.Exit(1);
            }

            var inputPath = args[0];
            var outputPath = args[1];
            var autoMode = args.Length > 2 && args[2] == "--auto";

            try
            {
                Console.Clear();
                PrintBanner();
                
                Console.WriteLine($"[+] Loading assembly: {Path.GetFileName(inputPath)}");
                
                var assembly = Load(inputPath);
                var rootNs = GetRootNamespace(assembly);

                Console.WriteLine($"[+] Root namespace: {rootNs}");
                
                if (!autoMode)
                {
                    Console.WriteLine("\n" + new string('=', 60));
                    Console.WriteLine("Available Types:");
                    Console.WriteLine(new string('=', 60));
                }
                
                // Get all types except <Module>
                var availableTypes = assembly?.ManifestModule?.TopLevelTypes
                    .Where(t => t.Name != "<Module>")
                    .ToList() ?? new List<TypeDefinition>();

                if (!availableTypes.Any())
                {
                    Console.WriteLine("[!] No types found in assembly.");
                    Environment.Exit(1);
                }

                // Display types and let user select
                TypeDefinition? selectedType;
                if (autoMode)
                {
                    selectedType = availableTypes.First();
                    Console.WriteLine($"[+] Auto-mode: Using type '{selectedType.FullName}'");
                }
                else
                {
                    selectedType = SelectType(availableTypes);
                }

                if (selectedType == null)
                {
                    Console.WriteLine("\n[!] No type selected. Exiting.");
                    Environment.Exit(0);
                }

                // Get all eligible methods
                var eligibleMethods = selectedType.Methods
                    .Where(m => 
                        !m.IsConstructor && 
                        !(m.IsStatic && m.Name == ".cctor") &&
                        m.CilMethodBody != null)
                    .ToList();

                if (!eligibleMethods.Any())
                {
                    Console.WriteLine($"\n[!] No eligible methods found in '{selectedType.FullName}'");
                    Console.WriteLine("    Eligible methods must have a body and not be constructors.");
                    Environment.Exit(1);
                }

                // Let user select methods
                List<MethodDefinition> selectedMethods;
                if (autoMode)
                {
                    selectedMethods = eligibleMethods;
                    Console.WriteLine($"[+] Auto-mode: Virtualizing all {selectedMethods.Count} methods");
                }
                else
                {
                    selectedMethods = SelectMethods(eligibleMethods, selectedType.Name);
                }

                if (!selectedMethods.Any())
                {
                    Console.WriteLine("\n[!] No methods selected. Exiting.");
                    Environment.Exit(0);
                }

                Console.WriteLine("\n" + new string('=', 60));
                Console.WriteLine($"[+] Injecting VM runtime...");
                
                var injector = new VmRuntimeInjector(assembly, rootNs);
                injector.Inject();

                Console.WriteLine($"[+] Virtualizing {selectedMethods.Count} method(s)...\n");
                
                int count = 0;
                foreach (var method in selectedMethods)
                {
                    count++;
                    Console.Write($"  [{count}/{selectedMethods.Count}] Virtualizing {method.Name}... ");
                    
                    try
                    {
                        MethodVirtualizer.Virtualize(method, assembly!, rootNs);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("✓");
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"✗ ({ex.Message})");
                        Console.ResetColor();
                    }
                }

                Console.WriteLine($"\n[+] Saving protected assembly to: {Path.GetFileName(outputPath)}");
                assembly?.Write(outputPath);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n[✓] Protection completed successfully!");
                Console.ResetColor();
                Console.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[!] Error: {ex.Message}");
                Console.WriteLine($"    Type: {ex.GetType().Name}");
                Console.ResetColor();
                if (ex.StackTrace != null)
                {
                    Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
                }
                Environment.Exit(1);
            }
        }

        private static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════╗
║                                                           ║
║   ██████╗ ██╗  ██╗ █████╗ ███╗   ██╗████████╗ ██████╗   ║
║   ██╔══██╗██║  ██║██╔══██╗████╗  ██║╚══██╔══╝██╔═══██╗  ║
║   ██████╔╝███████║███████║██╔██╗ ██║   ██║   ██║   ██║  ║
║   ██╔═══╝ ██╔══██║██╔══██║██║╚██╗██║   ██║   ██║   ██║  ║
║   ██║     ██║  ██║██║  ██║██║ ╚████║   ██║   ╚██████╔╝  ║
║   ╚═╝     ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝  ╚═══╝   ╚═╝    ╚═════╝   ║
║                                                           ║
║            IL Virtualization Protector v1.0              ║
║                                                           ║
╚═══════════════════════════════════════════════════════════╝
");
            Console.ResetColor();
        }

        private static TypeDefinition? SelectType(List<TypeDefinition> types)
        {
            Console.WriteLine();
            for (int i = 0; i < types.Count; i++)
            {
                var type = types[i];
                var methodCount = type.Methods.Count(m => 
                    !m.IsConstructor && 
                    !(m.IsStatic && m.Name == ".cctor") &&
                    m.CilMethodBody != null);
                
                Console.WriteLine($"  [{i + 1}] {type.FullName} ({methodCount} eligible methods)");
            }
            
            Console.WriteLine("\n" + new string('-', 60));
            Console.Write("Select type number (or 'q' to quit): ");
            
            var input = Console.ReadLine()?.Trim();
            
            if (input?.ToLower() == "q")
                return null;
            
            if (int.TryParse(input, out int selection) && selection > 0 && selection <= types.Count)
            {
                return types[selection - 1];
            }
            
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n[!] Invalid selection. Please try again.\n");
            Console.ResetColor();
            return SelectType(types);
        }

        private static List<MethodDefinition> SelectMethods(List<MethodDefinition> methods, string typeName)
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine($"Available Methods in '{typeName}':");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine();
            
            for (int i = 0; i < methods.Count; i++)
            {
                var method = methods[i];
                var signature = GetMethodSignature(method);
                Console.WriteLine($"  [{i + 1}] {signature}");
            }
            
            Console.WriteLine("\n" + new string('-', 60));
            Console.WriteLine("Selection Options:");
            Console.WriteLine("  • Single method: Enter number (e.g., '1')");
            Console.WriteLine("  • Multiple methods: Enter numbers separated by commas (e.g., '1,3,5')");
            Console.WriteLine("  • Range: Enter start-end (e.g., '1-5')");
            Console.WriteLine("  • All methods: Enter 'all' or '*'");
            Console.WriteLine("  • Quit: Enter 'q'");
            Console.WriteLine(new string('-', 60));
            Console.Write("\nYour selection: ");
            
            var input = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(input) || input.ToLower() == "q")
                return new List<MethodDefinition>();
            
            if (input.ToLower() == "all" || input == "*")
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n[!] Selected all {methods.Count} methods");
                Console.ResetColor();
                return methods;
            }
            
            var selectedMethods = new List<MethodDefinition>();
            
            try
            {
                // Handle ranges (e.g., "1-5")
                if (input.Contains('-'))
                {
                    var parts = input.Split('-');
                    if (parts.Length == 2 && 
                        int.TryParse(parts[0].Trim(), out int start) && 
                        int.TryParse(parts[1].Trim(), out int end))
                    {
                        if (start > 0 && end <= methods.Count && start <= end)
                        {
                            for (int i = start; i <= end; i++)
                            {
                                selectedMethods.Add(methods[i - 1]);
                            }
                        }
                        else
                        {
                            throw new ArgumentException("Invalid range");
                        }
                    }
                }
                // Handle comma-separated numbers (e.g., "1,3,5")
                else if (input.Contains(','))
                {
                    var numbers = input.Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Select(int.Parse)
                        .Distinct()
                        .OrderBy(n => n)
                        .ToList();
                    
                    foreach (var num in numbers)
                    {
                        if (num > 0 && num <= methods.Count)
                        {
                            selectedMethods.Add(methods[num - 1]);
                        }
                        else
                        {
                            throw new ArgumentException($"Invalid method number: {num}");
                        }
                    }
                }
                // Handle single number
                else if (int.TryParse(input, out int single))
                {
                    if (single > 0 && single <= methods.Count)
                    {
                        selectedMethods.Add(methods[single - 1]);
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid method number: {single}");
                    }
                }
                else
                {
                    throw new ArgumentException("Invalid input format");
                }
                
                if (selectedMethods.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n[✓] Selected {selectedMethods.Count} method(s):");
                    Console.ResetColor();
                    foreach (var method in selectedMethods)
                    {
                        Console.WriteLine($"    • {method.Name}");
                    }
                }
                
                return selectedMethods;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[!] Error: {ex.Message}. Please try again.\n");
                Console.ResetColor();
                return SelectMethods(methods, typeName);
            }
        }

        private static string GetMethodSignature(MethodDefinition method)
        {
            var returnType = method.Signature?.ReturnType?.Name ?? "void";
            var parameters = method.Parameters
                .Select(p => $"{p.ParameterType?.Name ?? "?"} {p.Name}")
                .ToList();
            
            var paramStr = parameters.Any() ? string.Join(", ", parameters) : "";
            var modifiers = new List<string>();
            
            if (method.IsPublic) modifiers.Add("public");
            else if (method.IsPrivate) modifiers.Add("private");
            else if (method.IsFamily) modifiers.Add("protected");
            
            if (method.IsStatic) modifiers.Add("static");
            if (method.IsVirtual) modifiers.Add("virtual");
            
            var modStr = modifiers.Any() ? string.Join(" ", modifiers) + " " : "";
            
            return $"{modStr}{returnType} {method.Name}({paramStr})";
        }
    }
}