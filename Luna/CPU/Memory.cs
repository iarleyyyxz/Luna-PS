public class Memory
{
    private const int RAM_SIZE = 2 * 1024 * 1024;      // 2 MB
    private const int BIOS_SIZE = 512 * 1024;          // 512 KB

    private readonly byte[] RAM = new byte[RAM_SIZE];
    private readonly byte[] BIOS = new byte[BIOS_SIZE];

    public void LoadBIOS(byte[] biosData)
    {
        if (biosData.Length != BIOS_SIZE)
            throw new ArgumentException($"BIOS inválida: tamanho deve ser {BIOS_SIZE} bytes");
        Array.Copy(biosData, BIOS, BIOS_SIZE);
    }

    public uint ReadWord(uint address)
    {
        address &= 0xFFFFFFFF;

        // RAM: 0x00000000, 0x80000000, 0xA0000000 espelham os mesmos 2MB
        if ((address & 0x1FFF_FFFF) < RAM_SIZE)
        {
            int index = (int)(address & 0x1FFFFF);
            return (uint)(
                (RAM[index + 0] << 24) |
                (RAM[index + 1] << 16) |
                (RAM[index + 2] << 8) |
                (RAM[index + 3])
            );
        }

        // BIOS: 0x1FC00000 - 0x1FC80000
        if (address >= 0x1FC00000 && address < 0x1FC80000)
        {
            int index = (int)(address - 0x1FC00000);
            return (uint)(
                (BIOS[index + 0] << 24) |
                (BIOS[index + 1] << 16) |
                (BIOS[index + 2] << 8) |
                (BIOS[index + 3])
            );
        }

        Console.WriteLine($"[Memory] Leitura inválida de endereço: 0x{address:X8}");
        return 0;
    }

    public void WriteWord(uint address, uint value)
    {
        address &= 0xFFFFFFFF;

        // RAM escrita
        if ((address & 0x1FFF_FFFF) < RAM_SIZE)
        {
            int index = (int)(address & 0x1FFFFF);
            RAM[index + 0] = (byte)((value >> 24) & 0xFF);
            RAM[index + 1] = (byte)((value >> 16) & 0xFF);
            RAM[index + 2] = (byte)((value >> 8) & 0xFF);
            RAM[index + 3] = (byte)(value & 0xFF);
            return;
        }

        Console.WriteLine($"[Memory] Escrita inválida em endereço: 0x{address:X8}");
    }
}
