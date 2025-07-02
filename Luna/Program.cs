using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using System;
using static CPU;

public class Program
{
    public static void Main()
    {
        /* var nativeWindowSettings = new NativeWindowSettings()
         {
             Size = new OpenTK.Mathematics.Vector2i(640, 480),
             Title = "PS1 GPU Emulador - Textura Demo",
             Flags = ContextFlags.ForwardCompatible
         };

         using var window = new EmuWindow(GameWindowSettings.Default, nativeWindowSettings);
         window.Run();*/
        //TestMemoryInstructions();
        TestOverflowExceptions();
    }

    static void TestOverflowExceptions()
    {
        Console.WriteLine("🚦 Testando exceções de overflow...");

        var cpu = new CPU(new Memory());

        // Teste 1: ADD - overflow positivo
        cpu.ResetCPU();
        cpu.Registers[1] = 0x7FFFFFFF; // maior int32 positivo
        cpu.Registers[2] = 1;
        uint instrADD = (0x00u << 26) | (1u << 21) | (2u << 16) | (3u << 11) | (0u << 6) | 0x20u;
        ExpectException(() => cpu.ExecuteRaw(instrADD), 12, "ADD overflow positivo");

        // Teste 2: ADD - overflow negativo
        cpu.ResetCPU();
        cpu.Registers[1] = 0x80000000; // menor int32 negativo
        cpu.Registers[2] = 0xFFFFFFFF; // -1
        instrADD = (0x00u << 26) | (1u << 21) | (2u << 16) | (3u << 11) | (0u << 6) | 0x20u;
        ExpectException(() => cpu.ExecuteRaw(instrADD), 12, "ADD overflow negativo");

        // Teste 3: ADDI - overflow positivo
        cpu.ResetCPU();
        cpu.Registers[1] = 0x7FFFFFFF;
        short imm = 1;
        uint instrADDI = (0x08u << 26) | (1u << 21) | (2u << 16) | ((ushort)imm);
        ExpectException(() => cpu.ExecuteRaw(instrADDI), 12, "ADDI overflow positivo");

        // Teste 4: ADDI - overflow negativo
        cpu.ResetCPU();
        cpu.Registers[1] = 0x80000000;
        imm = -1;
        instrADDI = (0x08u << 26) | (1u << 21) | (2u << 16) | ((ushort)imm);
        ExpectException(() => cpu.ExecuteRaw(instrADDI), 12, "ADDI overflow negativo");

        Console.WriteLine("✅ Todos os testes de overflow passaram.");
    }


static void ExpectException(Action action, byte expectedCode, string label)
{
    try
    {
        action();
        Console.WriteLine($"❌ {label} — esperava exceção {expectedCode}, mas nenhuma foi lançada.");
    }
    catch (CpuException ex)
    {
        if (ex.Code != expectedCode)
        {
            Console.WriteLine($"❌ {label} — código errado: {ex.Code}, esperado {expectedCode}");
        }
        else
        {
            Console.WriteLine($"✅ {label}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ {label} — exceção inesperada: {ex.Message}");
    }
}



}
