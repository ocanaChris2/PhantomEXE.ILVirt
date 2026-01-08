// src/PhantomExe.ILVirt.Tool/AssemblyLoader.cs
using AsmResolver.DotNet;

namespace PhantomExe.ILVirt.Tool.AssemblyLoader
{
    /// <summary>
    /// Provides assembly loading functionality with AsmResolver 5.5.0 compatibility.
    /// Class renamed to 'Loader' to avoid namespace/type name conflict.
    /// </summary>
    public static class Loader
    {
        /// <summary>
        /// Loads an assembly from the specified file path.
        /// </summary>
        /// <param name="path">The path to the assembly file.</param>
        /// <returns>A loaded AssemblyDefinition instance.</returns>
        public static AssemblyDefinition Load(string path)
        {
            return AssemblyDefinition.FromFile(path);
        }

        /// <summary>
        /// Gets the root namespace from the assembly's top-level types.
        /// </summary>
        /// <param name="assembly">The assembly to inspect.</param>
        /// <returns>The root namespace, or "PhantomExe" if none found.</returns>
        public static string GetRootNamespace(AssemblyDefinition assembly)
        {
            return assembly.ManifestModule?.TopLevelTypes
                .FirstOrDefault(t => !string.IsNullOrEmpty(t.Namespace))?.Namespace 
                ?? "PhantomExe";
        }
    }
}