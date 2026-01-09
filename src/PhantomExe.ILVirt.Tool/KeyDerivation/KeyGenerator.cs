// src/PhantomExe.ILVirt.Tool/KeyDerivation/KeyGenerator.cs
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AsmResolver.DotNet;

namespace PhantomExe.ILVirt.Tool.KeyDerivation
{
    public static class KeyGenerator
    {
        public static byte[] GenerateKey(MethodDefinition method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            var entropy = new byte[16];
            RandomNumberGenerator.Fill(entropy);

            var tokenBytes = BitConverter.GetBytes(method.MetadataToken.ToInt32());
            var typeHash = method.DeclaringType?.FullName?.GetHashCode() ?? 0;
            var typeHashBytes = BitConverter.GetBytes(typeHash);

            for (int i = 0; i < 4; i++)
            {
                entropy[i] ^= tokenBytes[i % tokenBytes.Length];
                entropy[4 + i] ^= typeHashBytes[i % typeHashBytes.Length];
            }

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(entropy);

            return new byte[]
            {
                (byte)(hash[0] ^ hash[8] ^ 0x5A),
                (byte)(hash[4] ^ hash[12] ^ 0xC3),
                (byte)(hash[16] ^ (byte)method.Name!.Length),
                (byte)(hash[24] ^ (byte)method.Parameters.Count)
            };
        }

        internal static byte DeriveMetadataByte(ModuleDefinition module)
        {
            try
            {
                // Simplemente usa dnlib directamente con el archivo
                var modulePath = module.FilePath;
                if (!string.IsNullOrEmpty(modulePath) && File.Exists(modulePath))
                {
                    var dnModule = dnlib.DotNet.ModuleDefMD.Load(modulePath);
                    var mvidGuid = dnModule.Mvid;
                    
                    if (mvidGuid.HasValue)
                    {
                        var mvid = mvidGuid.Value.ToByteArray();
                        byte result = 0;
                        foreach (var b in mvid.Take(4)) result ^= b;
                        return (byte)(result ^ 0x9E);
                    }
                }
                
                return 0x9E;
            }
            catch
            {
                return 0x9E;
            }
        }
    }
}