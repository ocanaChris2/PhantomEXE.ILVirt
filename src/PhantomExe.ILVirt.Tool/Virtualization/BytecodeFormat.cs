// src/PhantomExe.ILVirt.Tool/Virtualization/BytecodeFormat.cs
namespace PhantomExe.ILVirt.Tool.Virtualization
{
    public static class BytecodeFormat
    {
        // Basic operations
        public const byte OP_NOP = 0x00;
        public const byte OP_LDC_I4 = 0x01;
        public const byte OP_ADD = 0x02;
        public const byte OP_SUB = 0x03;
        public const byte OP_MUL = 0x04;
        public const byte OP_DIV = 0x05;
        public const byte OP_REM = 0x06;
        
        // Load constants
        public const byte OP_LDC_STR = 0x10;
        
        // Arguments and locals
        public const byte OP_LDARG = 0x20;
        public const byte OP_LDLOC = 0x30;
        public const byte OP_STLOC = 0x31;
        public const byte OP_DUP = 0x32;
        public const byte OP_POP = 0x33;
        
        // Comparison
        public const byte OP_CEQ = 0x40;
        public const byte OP_CGT = 0x41;
        public const byte OP_CLT = 0x42;
        
        // Branching
        public const byte OP_BR = 0x50;
        public const byte OP_BRTRUE = 0x51;
        public const byte OP_BRFALSE = 0x52;
        public const byte OP_BEQ = 0x53;
        public const byte OP_BNE = 0x54;
        public const byte OP_BGT = 0x55;
        public const byte OP_BGE = 0x56;
        public const byte OP_BLT = 0x57;
        public const byte OP_BLE = 0x58;
        
        // Control flow
        public const byte OP_RET = 0xFF;
    }
}