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
        // TestOverflowExceptions();
        // TestSUBOverflowExceptions();
        // TestAdduSubu_NoOverflow();
        // TestReservedInstruction();
        //TestInterrupt();
        // TestTimerIRQ();
        TestMIPSInstruct();
    
    }

    static void TestMIPSInstruct()
    {

        CPU cpu = new CPU(new Memory());
        cpu.ResetCPU();


        // SW $2, 0x1108($0) → escrever Timer0.Target
        cpu.Registers[2] = 0x0040;
        uint sw = ((uint)0x2B << 26) | (0 << 21) | (2 << 16) | 0x1108;
        cpu.ExecuteInstruction(sw);

        // LW $3, 0x1108($0) → ler de volta
        uint lw = ((uint)0x23 << 26) | (0 << 21) | (3 << 16) | 0x1108;
        cpu.ExecuteInstruction(lw);
        Console.WriteLine($"Timer0.Target = {cpu.Registers[3]:X8}");

    }

    static void TestTimerIRQ()
    {
        Console.WriteLine("🚦 Testando Timer gerando IRQ...");

        var cpu = new CPU(new Memory());
        cpu.ResetCPU();

        // Habilita IRQ 3 (IM3), e global IE
        cpu.MTC0(12, 0x00000801); // Status: IM3 = 1, IE = 1

        // Configura Timer 0
        cpu.Timers[0].Target = 5;
        cpu.Timers[0].Mode = (1 << 10) | (1 << 4); // Enable IRQ, Reset on Target

        // Roda 10 instruções
        try
        {
            for (int i = 0; i < 10; i++)
                cpu.Step();
        }
        catch (CpuException ex)
        {
            if (ex.Code == 0)
                Console.WriteLine("✅ Timer gerou interrupção corretamente");
            else
                Console.WriteLine($"❌ Código de exceção inesperado: {ex.Code}");
        }
    }



    static void TestInterrupt()
    {
        Console.WriteLine("🚦 Testando interrupção (IRQ)...");

        var cpu = new CPU(new Memory());
        cpu.ResetCPU();

        // Ativa IM0 (bit 8) e IE (bit 0)
        cpu.MTC0(12, (uint)0x00000101); // Status = IE | IM0

        // Gera IRQ0 (seta IP0 = 1)
        cpu.SetIRQ(0, true);

        try
        {
            cpu.Step(); // Não importa a instrução
        }
        catch (CpuException ex)
        {
            if (ex.Code == 0)
                Console.WriteLine("✅ IRQ detectada corretamente");
            else
                Console.WriteLine($"❌ IRQ lançada com código errado: {ex.Code}");
        }
    }
    


}
