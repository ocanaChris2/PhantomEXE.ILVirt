using System;
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;

// Disambiguate ManifestResourceAttributes
using AsmResolverManifestResourceAttributes = 
    AsmResolver.PE.DotNet.Metadata.Tables.Rows.ManifestResourceAttributes;

namespace PhantomExe.ILVirt.Tool.KeyDerivation
{
    public static class ResourceKeyEncoder
    {
        public static void Encode(byte k2, byte k3, AssemblyDefinition assembly)
        {
            // Store both k2 and k3 in the resource name
            var obfuscatedChar2 = (char)(k2 ^ 0x9E);
            var obfuscatedChar3 = (char)(k3 ^ 0xD1);
            var resourceName = $"PhantomExe.Decoder.{obfuscatedChar2}{obfuscatedChar3}";
            
            // ✅ AsmResolver 5.5.0: ManifestResource constructor with DataSegment
            var resource = new ManifestResource(
                resourceName,
                AsmResolverManifestResourceAttributes.Public,
                new AsmResolver.DataSegment(Array.Empty<byte>()));
            
            assembly?.ManifestModule?.Resources.Add(resource);
        }
    }
}