// src/PhantomExe.ILVirt.Tool/VmRuntime/VmRuntimeSource.cs
using System;
using System.Linq;
using System.Text;

namespace PhantomExe.ILVirt.Tool.VmRuntime
{
    internal static class VmRuntimeSource
    {
        public static string Generate(string rootNamespace)
        {
            return $@"
using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace {rootNamespace}
{{
    // ✅ VmKeyHelper is NOT defined here - it will be created separately by VmRuntimeInjector

    internal static partial class VmRuntime
    {{
        internal static object Execute(string resourceName, params object[] args)
        {{
            var encrypted = LoadResource(resourceName);
            var key = DeriveKey();
            
            // DEBUG: Log the key
            var keySb = new System.Text.StringBuilder();
            for (int i = 0; i < key.Length; i++)
            {{
                if (i > 0) keySb.Append(""-"");
                keySb.Append(key[i].ToString(""X2""));
            }}
            Console.WriteLine(""[VM DEBUG] Decryption key: "" + keySb.ToString());
            
            // DEBUG: Log encrypted bytes (first 20)
            var encSb = new System.Text.StringBuilder();
            int encLen = encrypted.Length < 20 ? encrypted.Length : 20;
            for (int i = 0; i < encLen; i++)
            {{
                if (i > 0) encSb.Append(""-"");
                encSb.Append(encrypted[i].ToString(""X2""));
            }}
            Console.WriteLine(""[VM DEBUG] Encrypted bytes (first 20): "" + encSb.ToString());
            
            var bytecode = Decrypt(encrypted, key);
            
            // DEBUG: Log decrypted bytecode (first 20)
            var decSb = new System.Text.StringBuilder();
            int decLen = bytecode.Length < 20 ? bytecode.Length : 20;
            for (int i = 0; i < decLen; i++)
            {{
                if (i > 0) decSb.Append(""-"");
                decSb.Append(bytecode[i].ToString(""X2""));
            }}
            Console.WriteLine(""[VM DEBUG] Decrypted bytes (first 20): "" + decSb.ToString());
            
            return Interpreter.Execute(bytecode, args);
        }}

        private static byte[] LoadResource(string name)
        {{
            var asm = typeof(VmRuntime).Assembly;
            using (var stream = asm.GetManifestResourceStream(name))
            {{
                if (stream == null)
                {{
                    throw new System.IO.FileNotFoundException(""Resource not found: "" + name);
                }}
                var data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                return data;
            }}
        }}

        private static byte[] DeriveKey()
        {{
            // ✅ Access VmKeyHelper via reflection since it's created externally
            var thisType = typeof(VmRuntime);
            var helperTypeName = thisType.Namespace + "".VmKeyHelper"";
            var helperType = thisType.Assembly.GetType(helperTypeName);
            
            if (helperType == null)
            {{
                Console.WriteLine(""[DeriveKey] ERROR: VmKeyHelper type not found in assembly!"");
                Console.WriteLine(""[DeriveKey] Looking for: "" + helperTypeName);
                Console.WriteLine(""[DeriveKey] Available types:"");
                foreach (var t in thisType.Assembly.GetTypes())
                {{
                    Console.WriteLine(""  - "" + t.FullName);
                }}
                throw new InvalidOperationException(""VmKeyHelper not found"");
            }}
            
            Console.WriteLine(""[DeriveKey] Found VmKeyHelper: "" + helperType.FullName);
            
            // ✅ CRITICAL: Force static constructor to run before reading fields
            Console.WriteLine(""[DeriveKey] Triggering static constructor..."");
            RuntimeHelpers.RunClassConstructor(helperType.TypeHandle);
            Console.WriteLine(""[DeriveKey] Static constructor triggered"");
            
            var partAField = helperType.GetField(""PartA"", 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Static);
            var partBField = helperType.GetField(""PartB"", 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Static);
            
            if (partAField == null || partBField == null)
            {{
                Console.WriteLine(""[DeriveKey] ERROR: PartA or PartB field not found!"");
                throw new InvalidOperationException(""VmKeyHelper fields not found"");
            }}
            
            byte partA = (byte)partAField.GetValue(null);
            byte partB = (byte)partBField.GetValue(null);
            
            Console.WriteLine(""[DeriveKey] PartA from VmKeyHelper: "" + partA.ToString(""X2""));
            Console.WriteLine(""[DeriveKey] PartB from VmKeyHelper: "" + partB.ToString(""X2""));
            
            byte k0 = (byte)(partA ^ 0x5A);
            byte k1 = (byte)(partB ^ 0xC3);
            byte k2 = (byte)(ComputeMvidDerivedByte() ^ 0x9E);
            byte k3 = (byte)(ExtractResourceKeyPart() ^ 0xD1);
            
            Console.WriteLine(""[DeriveKey] Final key components: k0="" + k0.ToString(""X2"") + 
                            "", k1="" + k1.ToString(""X2"") + 
                            "", k2="" + k2.ToString(""X2"") + 
                            "", k3="" + k3.ToString(""X2""));
            
            return new byte[] {{ k0, k1, k2, k3 }};
        }}

        private static byte ComputeMvidDerivedByte()
        {{
            var mvid = typeof(VmRuntime).Module.ModuleVersionId;
            var bytes = mvid.ToByteArray();
            return (byte)(bytes[0] + bytes[1] + bytes[2] + bytes[3]);
        }}

        private static byte ExtractResourceKeyPart()
        {{
            var names = typeof(VmRuntime).Assembly.GetManifestResourceNames();
            
            string decoderName = null;
            for (int i = 0; i < names.Length; i++)
            {{
                if (names[i].IndexOf(""PhantomExe.Decoder."") >= 0)
                {{
                    decoderName = names[i];
                    break;
                }}
            }}
            
            if (decoderName == null)
            {{
                return 0x00;
            }}
            
            return (byte)decoderName[decoderName.Length - 2];
        }}

        private static byte[] Decrypt(byte[] data, byte[] key)
        {{
            var result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {{
                result[i] = (byte)(data[i] ^ key[i % key.Length]);
            }}
            return result;
        }}
    }}

    internal static partial class Interpreter
    {{
        public static object Execute(byte[] bytecode, object[] args)
        {{
            var stack = new Stack<object>();
            var locals = new object[16];
            int ip = 0;

            while (ip < bytecode.Length)
            {{
                byte op = bytecode[ip++];
                
                switch (op)
                {{
                    case 0x00: // NOP
                        break;

                    case 0x01: // LDC_I4
                        {{
                            int value = BitConverter.ToInt32(bytecode, ip);
                            ip += 4;
                            stack.Push(value);
                            break;
                        }}

                    case 0x02: // ADD
                        {{
                            var b = stack.Pop();
                            var a = stack.Pop();
                            if (a is int ia && b is int ib)
                                stack.Push(ia + ib);
                            else if (a is long la && b is long lb)
                                stack.Push(la + lb);
                            else if (a is double da && b is double db)
                                stack.Push(da + db);
                            else if (a is float fa && b is float fb)
                                stack.Push(fa + fb);
                            break;
                        }}

                    case 0x03: // SUB
                        {{
                            var b = stack.Pop();
                            var a = stack.Pop();
                            if (a is int ia && b is int ib)
                                stack.Push(ia - ib);
                            else if (a is long la && b is long lb)
                                stack.Push(la - lb);
                            else if (a is double da && b is double db)
                                stack.Push(da - db);
                            else if (a is float fa && b is float fb)
                                stack.Push(fa - fb);
                            break;
                        }}

                    case 0x04: // MUL
                        {{
                            var b = stack.Pop();
                            var a = stack.Pop();
                            if (a is int ia && b is int ib)
                                stack.Push(ia * ib);
                            else if (a is long la && b is long lb)
                                stack.Push(la * lb);
                            else if (a is double da && b is double db)
                                stack.Push(da * db);
                            else if (a is float fa && b is float fb)
                                stack.Push(fa * fb);
                            break;
                        }}

                    case 0x05: // DIV
                        {{
                            var b = stack.Pop();
                            var a = stack.Pop();
                            if (a is int ia && b is int ib)
                                stack.Push(ia / ib);
                            else if (a is long la && b is long lb)
                                stack.Push(la / lb);
                            else if (a is double da && b is double db)
                                stack.Push(da / db);
                            else if (a is float fa && b is float fb)
                                stack.Push(fa / fb);
                            break;
                        }}

                    case 0x06: // REM
                        {{
                            var b = stack.Pop();
                            var a = stack.Pop();
                            if (a is int ia && b is int ib)
                                stack.Push(ia % ib);
                            else if (a is long la && b is long lb)
                                stack.Push(la % lb);
                            break;
                        }}

                    case 0x10: // LDC_STR
                        {{
                            ushort len = BitConverter.ToUInt16(bytecode, ip);
                            ip += 2;
                            string str = Encoding.UTF8.GetString(bytecode, ip, len);
                            ip += len;
                            stack.Push(str);
                            break;
                        }}

                    case 0x20: // LDARG
                        {{
                            byte argIndex = bytecode[ip++];
                            if (argIndex < args.Length)
                                stack.Push(args[argIndex]);
                            else
                                stack.Push(null);
                            break;
                        }}

                    case 0x30: // LDLOC
                        {{
                            byte localIndex = bytecode[ip++];
                            stack.Push(locals[localIndex]);
                            break;
                        }}

                    case 0x31: // STLOC
                        {{
                            byte localIndex = bytecode[ip++];
                            locals[localIndex] = stack.Pop();
                            break;
                        }}

                    case 0x32: // DUP
                        {{
                            var value = stack.Peek();
                            stack.Push(value);
                            break;
                        }}

                    case 0x33: // POP
                        stack.Pop();
                        break;

                    case 0x40: // CEQ
                        {{
                            var b = stack.Pop();
                            var a = stack.Pop();
                            bool result = false;
                            
                            if (a == null && b == null)
                                result = true;
                            else if (a != null && b != null)
                            {{
                                if (a is int ia && b is int ib)
                                    result = ia == ib;
                                else if (a is long la && b is long lb)
                                    result = la == lb;
                                else
                                    result = a.Equals(b);
                            }}
                            
                            stack.Push(result ? 1 : 0);
                            break;
                        }}

                    case 0x41: // CGT
                        {{
                            var b = stack.Pop();
                            var a = stack.Pop();
                            bool result = false;
                            
                            if (a is int ia && b is int ib)
                                result = ia > ib;
                            else if (a is long la && b is long lb)
                                result = la > lb;
                            else if (a is double da && b is double db)
                                result = da > db;
                            else if (a is float fa && b is float fb)
                                result = fa > fb;
                            
                            stack.Push(result ? 1 : 0);
                            break;
                        }}

                    case 0x42: // CLT
                        {{
                            var b = stack.Pop();
                            var a = stack.Pop();
                            bool result = false;
                            
                            if (a is int ia && b is int ib)
                                result = ia < ib;
                            else if (a is long la && b is long lb)
                                result = la < lb;
                            else if (a is double da && b is double db)
                                result = da < db;
                            else if (a is float fa && b is float fb)
                                result = fa < fb;
                            
                            stack.Push(result ? 1 : 0);
                            break;
                        }}

                    case 0x50: // BR
                        {{
                            int targetOffset = BitConverter.ToInt32(bytecode, ip);
                            ip = targetOffset;
                            break;
                        }}

                    case 0x51: // BRTRUE
                        {{
                            int targetOffset = BitConverter.ToInt32(bytecode, ip);
                            ip += 4;
                            var value = stack.Pop();
                            bool condition = false;
                            
                            if (value is bool b)
                                condition = b;
                            else if (value is int i)
                                condition = i != 0;
                            else if (value != null)
                                condition = true;
                            
                            if (condition)
                                ip = targetOffset;
                            break;
                        }}

                    case 0x52: // BRFALSE
                        {{
                            int targetOffset = BitConverter.ToInt32(bytecode, ip);
                            ip += 4;
                            var value = stack.Pop();
                            bool condition = false;
                            
                            if (value is bool b)
                                condition = b;
                            else if (value is int i)
                                condition = i != 0;
                            else if (value != null)
                                condition = true;
                            
                            if (!condition)
                                ip = targetOffset;
                            break;
                        }}

                    case 0x53: // BEQ
                        {{
                            int targetOffset = BitConverter.ToInt32(bytecode, ip);
                            ip += 4;
                            var b = stack.Pop();
                            var a = stack.Pop();
                            bool equal = false;
                            
                            if (a == null && b == null)
                                equal = true;
                            else if (a != null && b != null)
                            {{
                                if (a is int ia && b is int ib)
                                    equal = ia == ib;
                                else if (a is long la && b is long lb)
                                    equal = la == lb;
                                else
                                    equal = a.Equals(b);
                            }}
                            
                            if (equal)
                                ip = targetOffset;
                            break;
                        }}

                    case 0x54: // BNE
                        {{
                            int targetOffset = BitConverter.ToInt32(bytecode, ip);
                            ip += 4;
                            var b = stack.Pop();
                            var a = stack.Pop();
                            bool notEqual = true;
                            
                            if (a == null && b == null)
                                notEqual = false;
                            else if (a != null && b != null)
                            {{
                                if (a is int ia && b is int ib)
                                    notEqual = ia != ib;
                                else if (a is long la && b is long lb)
                                    notEqual = la != lb;
                                else
                                    notEqual = !a.Equals(b);
                            }}
                            
                            if (notEqual)
                                ip = targetOffset;
                            break;
                        }}

                    case 0x55: // BGT
                        {{
                            int targetOffset = BitConverter.ToInt32(bytecode, ip);
                            ip += 4;
                            var b = stack.Pop();
                            var a = stack.Pop();
                            bool greater = false;
                            
                            if (a is int ia && b is int ib)
                                greater = ia > ib;
                            else if (a is long la && b is long lb)
                                greater = la > lb;
                            else if (a is double da && b is double db)
                                greater = da > db;
                            
                            if (greater)
                                ip = targetOffset;
                            break;
                        }}

                    case 0x56: // BGE
                        {{
                            int targetOffset = BitConverter.ToInt32(bytecode, ip);
                            ip += 4;
                            var b = stack.Pop();
                            var a = stack.Pop();
                            bool greaterOrEqual = false;
                            
                            if (a is int ia && b is int ib)
                                greaterOrEqual = ia >= ib;
                            else if (a is long la && b is long lb)
                                greaterOrEqual = la >= lb;
                            else if (a is double da && b is double db)
                                greaterOrEqual = da >= db;
                            
                            if (greaterOrEqual)
                                ip = targetOffset;
                            break;
                        }}

                    case 0x57: // BLT
                        {{
                            int targetOffset = BitConverter.ToInt32(bytecode, ip);
                            ip += 4;
                            var b = stack.Pop();
                            var a = stack.Pop();
                            bool less = false;
                            
                            if (a is int ia && b is int ib)
                                less = ia < ib;
                            else if (a is long la && b is long lb)
                                less = la < lb;
                            else if (a is double da && b is double db)
                                less = da < db;
                            
                            if (less)
                                ip = targetOffset;
                            break;
                        }}

                    case 0x58: // BLE
                        {{
                            int targetOffset = BitConverter.ToInt32(bytecode, ip);
                            ip += 4;
                            var b = stack.Pop();
                            var a = stack.Pop();
                            bool lessOrEqual = false;
                            
                            if (a is int ia && b is int ib)
                                lessOrEqual = ia <= ib;
                            else if (a is long la && b is long lb)
                                lessOrEqual = la <= lb;
                            else if (a is double da && b is double db)
                                lessOrEqual = da <= db;
                            
                            if (lessOrEqual)
                                ip = targetOffset;
                            break;
                        }}

                    case 0xFF: // RET
                        return stack.Count > 0 ? stack.Pop() : null;

                    default:
                        throw new NotSupportedException(""Unknown opcode: 0x"" + op.ToString(""X2""));
                }}
            }}

            throw new InvalidOperationException(""Method did not return"");
        }}
    }}
}}";
        }
    }
}