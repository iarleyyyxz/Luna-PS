using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using System;

public class Program
{
    public static void Main()
    {
      /*  var nativeWindowSettings = new NativeWindowSettings()
        {
            Size = new OpenTK.Mathematics.Vector2i(640, 480),
            Title = "PS1 GPU Emulador - Textura Demo",
            Flags = ContextFlags.ForwardCompatible
        };

        using var window = new EmuWindow(GameWindowSettings.Default, nativeWindowSettings);
        window.Run();*/
        TestMemoryInstructions();
    }

    static void TestMemoryInstructions()
    {
        var cpu = new CPU(new Memory());

        // Teste 1: LW (Load Word)
        cpu.ResetCPU();
        cpu.RAM[0x100] = 0xDE;
        cpu.RAM[0x101] = 0xAD;
        cpu.RAM[0x102] = 0xBE;
        cpu.RAM[0x103] = 0xEF;
        cpu.Registers[1] = 0x100; // base
        uint instrLW = (0x23u << 26) | (1u << 21) | (2u << 16); // LW $2, 0($1)
        cpu.ExecuteRaw(instrLW);
        Console.WriteLine(cpu.Registers[2] == 0xDEADBEEF ? "✅ LW passou" : "❌ LW falhou");

        // Teste 2: SW (Store Word)
        cpu.ResetCPU();
        cpu.Registers[1] = 0x200;
        cpu.Registers[2] = 0x12345678;
        uint instrSW = ((uint)0x2B << 26) | (1 << 21) | (2 << 16); // SW $2, 0($1)
        cpu.ExecuteRaw(instrSW);
        bool swOk = cpu.RAM[0x200] == 0x12 && cpu.RAM[0x201] == 0x34 &&
                    cpu.RAM[0x202] == 0x56 && cpu.RAM[0x203] == 0x78;
        Console.WriteLine(swOk ? "✅ SW passou" : "❌ SW falhou");

        // Teste 3: LH (Load Halfword Signed)
        cpu.ResetCPU();
        cpu.RAM[0x300] = 0xFF; // byte alto
        cpu.RAM[0x301] = 0x80; // byte baixo (valor negativo em short: 0xFF80 == -128)
        cpu.Registers[1] = 0x300;
        uint instrLH = ((uint)0x21 << 26) | (1 << 21) | (2 << 16); // LH $2, 0($1)
        cpu.ExecuteRaw(instrLH);
        Console.WriteLine(cpu.Registers[2] == 0xFFFFFF80 ? "✅ LH passou" : "❌ LH falhou");

        // Teste 4: LHU (Load Halfword Unsigned)
        cpu.ResetCPU();
        cpu.RAM[0x310] = 0xAB;
        cpu.RAM[0x311] = 0xCD;
        cpu.Registers[1] = 0x310;
        uint instrLHU = ((uint)0x25 << 26) | (1 << 21) | (2 << 16); // LHU $2, 0($1)
        cpu.ExecuteRaw(instrLHU);
        Console.WriteLine(cpu.Registers[2] == 0xABCD ? "✅ LHU passou" : "❌ LHU falhou");

        // Teste 5: SH (Store Halfword)
        cpu.ResetCPU();
        cpu.Registers[1] = 0x320;
        cpu.Registers[2] = 0xCAFEBABE;
        uint instrSH = ((uint)0x29 << 26) | (1 << 21) | (2 << 16); // SH $2, 0($1)
        cpu.ExecuteRaw(instrSH);
        bool shOk = cpu.RAM[0x320] == 0xBA && cpu.RAM[0x321] == 0xBE;
        Console.WriteLine(shOk ? "✅ SH passou" : "❌ SH falhou");
    }

}
