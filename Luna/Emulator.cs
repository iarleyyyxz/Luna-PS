using System;
using System.IO;


public class Emulator
{
    private CPU cpu;
    private Memory memory;

    public Emulator()
    {
        memory = new Memory();
        cpu = new CPU(memory);
    }

    public void LoadBIOS(string path)
    {
        var memory = new Memory();

        // Carregar BIOS (arquivo externo)
        var biosBytes = File.ReadAllBytes("scph1001.bin"); // ou outra BIOS válida

        memory.LoadBIOS(biosBytes);
        // Inicializar CPU com essa memória
        var cpu = new CPU(memory);

    }

    public void Run()
    {
        cpu.Reset();
        while (true)
        {
            cpu.Step();
            // Atualiza periféricos, timers, etc
        }
    }
}