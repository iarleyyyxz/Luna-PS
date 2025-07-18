using System;
using System.IO;



public class CPU
{
    // Registradores
    public uint[] Registers = new uint[32]; // $0 - $31
    public uint PC; // Endereço inicial do BIOS no PS1
    public uint HI = 0, LO = 0; // Registradores especiais

    // Memória (por simplicidade, 2MB como no PS1)
    public byte[] RAM = new byte[2 * 1024 * 1024];
    public byte[] BIOS = new byte[512 * 1024]; // 512 KB de ROM BIOS

    // GTE: COP2
    private uint[] GTE_Data = new uint[32];      // Dados (ex: VXY0, VZ0, IR1, etc.)
    private uint[] GTE_Control = new uint[32];   // Controle (ex: MAC0-3, FLAG, etc.)

    private uint nextPC;
    private bool inDelaySlot = false;


    private uint[] COP0Registers = new uint[32];

    const uint ExceptionHandlerAddress = 0x80000080;

    private Memory memory;

    private uint[] Cop0 = new uint[32];

    private const int COP0_INDEX = 0;
    private const int COP0_RANDOM = 1;
    private const int COP0_ENTRYLO = 2;
    private const int COP0_BADVADDR = 8;
    private const int COP0_STATUS = 12;
    private const int COP0_CAUSE = 13;
    private const int COP0_EPC = 14;
    private const int COP0_PRID = 15;

    public Timer[] Timers = new Timer[3]
    {
        new Timer(), // Timer 0 → IRQ 3
        new Timer(), // Timer 1 → IRQ 4
        new Timer()  // Timer 2 → IRQ 5
    };


    // STATUS
    // Bit 0 = IE (interrupt enable)
    // Bit 1 = EXL (exception level)
    // Bits 8..15 = IM0..IM7 (interrupt mask)

    // CAUSE
    // Bits 8..15 = IP0..IP7 (interrupt pending)


    public uint ReadCop0(int reg) => Cop0[reg];


    public CPU(Memory memory)
    {
        this.memory = memory;
        PC = 0xBFC00000; // endereço inicial da BIOS no PS1
    }

    public void Step()
    {
        uint instruction = FetchInstruction();

        if (inDelaySlot)
        {
            inDelaySlot = false;
            PC = nextPC;
        }
        else
        {
            PC += 4;
        }

        ExecuteInstruction(instruction);
        Registers[0] = 0;

        CheckInterrupts(); // <- aqui!

        for (int i = 0; i < 3; i++)
        {
            Timers[i].Tick();

            if (Timers[i].IRQPending)
            {
                SetIRQ(i + 3, true); // IRQ3, IRQ4, IRQ5
                Timers[i].IRQPending = false;
            }
        }

    }



    public void Reset()
    {
        PC = 0xBFC00000; // Endereço inicial do reset na BIOS (kseg1)

        // Zera registradores gerais
        for (int i = 0; i < Registers.Length; i++)
            Registers[i] = 0;

        // Zera registradores COP0 e inicializa Status
        for (int i = 0; i < COP0Registers.Length; i++)
            COP0Registers[i] = 0;

        // Inicializa Status com bit EXL = 0 e IE = 1 (habilita interrupções)
        COP0Registers[12] = 0x00000000;

        // Opcional: definir SP (stack pointer) em $29 para padrão da BIOS
        Registers[29] = 0x801FFF00; // Exemplo de SP inicial típico PS1
    }

    public uint FetchInstruction()
    {
        return memory.ReadWord(PC);
    }

    private void CheckInterrupts()
    {
        uint status = Cop0[COP0_STATUS];
        uint cause = Cop0[COP0_CAUSE];

        bool IE = (status & 0x1) != 0;       // bit 0
        bool EXL = (status & 0x2) != 0;       // bit 1

        if (!IE || EXL)
            return;

        // Bits 8..15 são IM e IP
        uint enabledIRQs = (status >> 8) & 0xFF;
        uint pendingIRQs = (cause >> 8) & 0xFF;

        uint activeIRQs = enabledIRQs & pendingIRQs;

        if (activeIRQs != 0)
        {
            RaiseException(0); // Código 0 = Interrupt
        }
    }



    private uint ReadWord(uint address)
    {
        // Região de RAM (espelhada em várias áreas)
        if ((address & 0x1FFF_FFFF) < RAM.Length)
        {
            int index = (int)(address & 0x1FFFFF); // RAM é 2MB
            return (uint)(
                (RAM[index + 0] << 24) |
                (RAM[index + 1] << 16) |
                (RAM[index + 2] << 8) |
                (RAM[index + 3])
            );
        }

        // Região da BIOS (somente leitura, 512 KB, começa em 0x1FC00000)
        if (address >= 0x1FC00000 && address <= 0x1FC80000)
        {
            int index = (int)(address - 0x1FC00000);
            return (uint)(
                (BIOS[index + 0] << 24) |
                (BIOS[index + 1] << 16) |
                (BIOS[index + 2] << 8) |
                (BIOS[index + 3])
            );
        }

        // Acesso aos registradores dos Timers (0x1F801100 - 0x1F80112F)
        if (address >= 0x1F801100 && address <= 0x1F80112F)
        {
            int timerIndex = (int)((address - 0x1F801100) / 0x10);
            int offset = (int)((address - 0x1F801100) % 0x10);

            switch (offset)
            {
                case 0x0: return Timers[timerIndex].Counter;
                case 0x4: return Timers[timerIndex].Mode;
                case 0x8: return Timers[timerIndex].Target;
                default: return 0;
            }
        }


        Console.WriteLine($"Leitura inválida de endereço: 0x{address:X8}");
        return 0;
    }

    private void WriteWord(uint address, uint value)
{
    // Região de RAM (espelhada em várias áreas)
    if ((address & 0x1FFF_FFFF) < RAM.Length)
    {
        int index = (int)(address & 0x1FFFFF); // 2MB
        RAM[index + 0] = (byte)((value >> 24) & 0xFF);
        RAM[index + 1] = (byte)((value >> 16) & 0xFF);
        RAM[index + 2] = (byte)((value >> 8) & 0xFF);
        RAM[index + 3] = (byte)(value & 0xFF);
        return;
    }

    // Região dos Timers (0x1F801100 - 0x1F80112F)
    if (address >= 0x1F801100 && address <= 0x1F80112F)
    {
        int timerIndex = (int)((address - 0x1F801100) / 0x10);
        int offset = (int)((address - 0x1F801100) % 0x10);

        switch (offset)
        {
            case 0x0:
                Timers[timerIndex].Counter = (ushort)(value & 0xFFFF);
                break;
            case 0x4:
                Timers[timerIndex].Mode = (ushort)(value & 0x3FFF); // 14 bits válidos
                break;
            case 0x8:
                Timers[timerIndex].Target = (ushort)(value & 0xFFFF);
                break;
        }
        return;
    }

    Console.WriteLine($"Escrita inválida de endereço: 0x{address:X8}, valor: 0x{value:X8}");
}



    public void SetIRQ(int line, bool state)
    {
        if (line < 0 || line > 7) return;

        if (state)
            Cop0[COP0_CAUSE] |= (1u << (8 + line));  // IPx = 1
        else
            Cop0[COP0_CAUSE] &= ~(1u << (8 + line)); // IPx = 0
    }

    public void ExecuteInstruction(uint instruction)
    {

        // Se PC estiver no handler e a instrução for especial, trate o handler
        if (PC == ExceptionHandlerAddress)
        {
            HandleExceptionHandler(instruction);
            return;
        }

        uint opcode = instruction >> 26;

        switch (opcode)
        {
            case 0x00: HandleSPECIAL(instruction); break; // R-Type
            case 0x02: J(instruction); break;
            case 0x03: JAL(instruction); break;
            case 0x04: BEQ(instruction); break;
            case 0x05: BNE(instruction); break;
            case 0x08: ADDI(instruction); break;
            case 0x0C: ANDI(instruction); break;
            case 0x0D: ORI(instruction); break;
            case 0x0F: LUI(instruction); break;
            case 0x23: LW(instruction); break;
            case 0x2B: SW(instruction); break;
            case 0x0A: SLTI(instruction); break;
            case 0x20: LB(instruction); break;   // opcode 0x20
            case 0x24: LBU(instruction); break;  // opcode 0x24
            case 0x28: SB(instruction); break;   // opcode 0x28
            case 0x21: LH(instruction); break;   // LH: opcode 0x21
            case 0x25: LHU(instruction); break;  // LHU: opcode 0x25
            case 0x29: SH(instruction); break;   // SH: opcode 0x29
            case 0x22: LWL(instruction); break;
            case 0x26: LWR(instruction); break;
            case 0x2A: SWL(instruction); break;
            case 0x2E: SWR(instruction); break;
            case 0x10: HandleCOP0(instruction); break;
            case 0x12: HandleCOP2(instruction); break;

            default:
                Console.WriteLine($"Opcode desconhecido: 0x{opcode:X2} @PC=0x{PC - 4:X8}");
                RaiseException(10); // Reserved Instruction
                break;
        }
    }


    private void HandleSPECIAL(uint instruction)
    {
        uint funct = instruction & 0x3F;
        uint rs = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        uint rd = (instruction >> 11) & 0x1F;
        uint shamt = (instruction >> 6) & 0x1F;

        switch (funct)
        {
            case 0x00: // SLL
                Registers[rd] = Registers[rt] << (int)shamt;
                break;
            case 0x02: // SRL
                Registers[rd] = Registers[rt] >> (int)shamt;
                break;
            case 0x24: // AND
                Registers[rd] = Registers[rs] & Registers[rt];
                break;
            case 0x25: // OR
                Registers[rd] = Registers[rs] | Registers[rt];
                break;
            case 0x2A: // SLT
                Registers[rd] = (int)Registers[rs] < (int)Registers[rt] ? 1u : 0u;
                break;
            case 0x08: // JR
                nextPC = Registers[rs];
                inDelaySlot = true;
                break;
            case 0x26: // XOR
                Registers[rd] = Registers[rs] ^ Registers[rt];
                break;
            case 0x27: // NOR
                Registers[rd] = ~(Registers[rs] | Registers[rt]);
                break;
            case 0x20: // ADD (com detecção de overflow)
                {
                    int a = (int)Registers[rs];
                    int b = (int)Registers[rt];
                    int result = a + b;

                    // Detecta overflow com base em sinal
                    if (((a ^ b) >= 0) && ((a ^ result) < 0))
                    {
                        RaiseException(12); // 12 = Integer Overflow
                        return;
                    }

                    Registers[rd] = (uint)result;
                    break;
                }

            case 0x18: // MULT
                {
                    int s = (int)Registers[rs];
                    int t = (int)Registers[rt];
                    long result = (long)s * (long)t;
                    LO = (uint)(result & 0xFFFFFFFF);
                    HI = (uint)((result >> 32) & 0xFFFFFFFF);
                    break;
                }
            case 0x1A: // DIV
                {
                    int s = (int)Registers[rs];
                    int t = (int)Registers[rt];
                    if (t == 0)
                    {
                        // Resultado indefinido no MIPS. O PS1 geralmente deixa LO e HI inalterados ou zera.
                        Console.WriteLine($"DIV por zero @PC=0x{PC - 4:X8}");
                        break;
                    }
                    LO = (uint)(s / t);
                    HI = (uint)(s % t);
                    break;
                }

            case 0x10: // MFHI
                Registers[rd] = HI;
                break;
            case 0x12: // MFLO
                Registers[rd] = LO;
                break;
            case 0x0C: // SYSCALL
                HandleSyscall();
                break;

            case 0x0D: // BREAK
                HandleBreak();
                break;
            case 0x03: // SRA
                Registers[rd] = (uint)((int)Registers[rt] >> (int)shamt);
                break;
            case 0x04: // SLLV
                Registers[rd] = Registers[rt] << (int)(Registers[rs] & 0x1F);
                break;
            case 0x06: // SRLV
                Registers[rd] = Registers[rt] >> (int)(Registers[rs] & 0x1F);
                break;
            case 0x07: // SRAV
                Registers[rd] = (uint)((int)Registers[rt] >> (int)(Registers[rs] & 0x1F));
                break;
            case 0x22: // SUB com detecção de overflow
                {
                    int a = (int)Registers[rs];
                    int b = (int)Registers[rt];
                    int result = a - b;

                    // Detecta overflow de subtração
                    if (((a ^ b) < 0) && ((a ^ result) < 0))
                    {
                        RaiseException(12); // Integer Overflow
                        return;
                    }

                    Registers[rd] = (uint)result;
                    break;
                }

            case 0x21: // ADDU
                Registers[rd] = Registers[rs] + Registers[rt];
                break;

            case 0x23: // SUBU
                Registers[rd] = Registers[rs] - Registers[rt];
                break;
            default:
                Console.WriteLine($"Funct desconhecido: 0x{funct:X2} @PC=0x{PC - 4:X8}");
                break;
        }
    }

    private void J(uint instruction)
    {
        uint target = instruction & 0x03FFFFFF;
        nextPC = (PC & 0xF0000000) | (target << 2);
        inDelaySlot = true;
    }


    private void JAL(uint instruction)
    {
        Registers[31] = PC + 4;
        J(instruction); // já trata delay slot
    }


    private void ADDI(uint instruction)
    {
        uint rs = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short imm = (short)(instruction & 0xFFFF); // sinalizado

        int a = (int)Registers[rs];
        int b = (int)imm;
        int result = a + b;

        // Detecta overflow com base nos sinais de entrada e saída
        if (((a ^ b) >= 0) && ((a ^ result) < 0))
        {
            RaiseException(12); // Integer Overflow
            return;
        }

        Registers[rt] = (uint)result;
    }



    private void ANDI(uint instruction)
    {
        uint rs = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        ushort imm = (ushort)(instruction & 0xFFFF);
        Registers[rt] = Registers[rs] & imm;
    }

    private void ORI(uint instruction)
    {
        uint rs = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        ushort imm = (ushort)(instruction & 0xFFFF);
        Registers[rt] = Registers[rs] | imm;
    }

    private void LUI(uint instruction)
    {
        uint rt = (instruction >> 16) & 0x1F;
        ushort imm = (ushort)(instruction & 0xFFFF);
        Registers[rt] = (uint)(imm << 16);
    }

    private void BEQ(uint instruction)
    {
        uint rs = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short offset = (short)(instruction & 0xFFFF);

        if (Registers[rs] == Registers[rt])
        {
            nextPC = PC + ((uint)offset << 2);
            inDelaySlot = true;
        }
    }


    private void BNE(uint instruction)
    {
        uint rs = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short offset = (short)(instruction & 0xFFFF);
        if (Registers[rs] != Registers[rt])
            PC += (uint)(offset << 2);
    }

    private void LW(uint instruction)
    {
        uint baseReg = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short offset = (short)(instruction & 0xFFFF);
        uint addr = Registers[baseReg] + (uint)offset;

        // Verificação de desalinhamento e limites de RAM
        if ((addr & 0b11) != 0 || addr + 3 >= RAM.Length)
        {
            RaiseException(4); // Load Address Error
            return;
        }

        int index = (int)(addr & 0x1FFFFF);
        uint value = (uint)(
            (RAM[index] << 24) |
            (RAM[index + 1] << 16) |
            (RAM[index + 2] << 8) |
            (RAM[index + 3])
        );

        Registers[rt] = value;
    }



    private void SW(uint instruction)
    {
        uint baseReg = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short offset = (short)(instruction & 0xFFFF);
        uint addr = Registers[baseReg] + (uint)offset;

        if ((addr & 0b11) != 0 || addr + 3 >= RAM.Length)
        {
            RaiseException(5); // Store Address Error
            return;
        }

        int index = (int)(addr & 0x1FFFFF);
        uint value = Registers[rt];

        RAM[index + 0] = (byte)((value >> 24) & 0xFF);
        RAM[index + 1] = (byte)((value >> 16) & 0xFF);
        RAM[index + 2] = (byte)((value >> 8) & 0xFF);
        RAM[index + 3] = (byte)(value & 0xFF);
    }


    private void SLTI(uint instruction)
    {
        uint rs = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short imm = (short)(instruction & 0xFFFF);
        Registers[rt] = ((int)Registers[rs] < imm) ? 1u : 0u;
    }

    private void LB(uint instruction)
    {
        uint baseReg = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short offset = (short)(instruction & 0xFFFF);
        uint addr = Registers[baseReg] + (uint)offset;
        int index = (int)(addr % RAM.Length);
        sbyte value = (sbyte)RAM[index]; // leitura com sinal
        Registers[rt] = (uint)value;     // extensao de sinal para 32 bits
    }

    private void LBU(uint instruction)
    {
        uint baseReg = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short offset = (short)(instruction & 0xFFFF);
        uint addr = Registers[baseReg] + (uint)offset;
        int index = (int)(addr % RAM.Length);
        byte value = RAM[index];
        Registers[rt] = value; // zero-extended para 32 bits
    }

    private void SB(uint instruction)
    {
        uint baseReg = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short offset = (short)(instruction & 0xFFFF);
        uint addr = Registers[baseReg] + (uint)offset;
        int index = (int)(addr % RAM.Length);
        RAM[index] = (byte)(Registers[rt] & 0xFF);
    }

    private void LH(uint instruction)
    {
        uint baseReg = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short offset = (short)(instruction & 0xFFFF);
        uint addr = Registers[baseReg] + (uint)offset;

        if ((addr & 0b1) != 0 || addr + 1 >= RAM.Length)
        {
            RaiseException(4); // Load Address Error
            return;
        }

        int index = (int)(addr & 0x1FFFFF);
        short value = (short)((RAM[index] << 8) | RAM[index + 1]);

        Registers[rt] = (uint)value;
    }



    private void LHU(uint instruction)
    {
        uint baseReg = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short offset = (short)(instruction & 0xFFFF);
        uint addr = Registers[baseReg] + (uint)offset;

        if ((addr & 0b1) != 0 || addr + 1 >= RAM.Length)
        {
            RaiseException(4); // Load Address Error
            return;
        }

        int index = (int)(addr & 0x1FFFFF);
        ushort value = (ushort)((RAM[index] << 8) | RAM[index + 1]);

        Registers[rt] = value;
    }



    private void SH(uint instruction)
    {
        uint baseReg = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short offset = (short)(instruction & 0xFFFF);
        uint addr = Registers[baseReg] + (uint)offset;

        if ((addr & 0b1) != 0 || addr + 1 >= RAM.Length)
        {
            RaiseException(5); // Store Address Error
            return;
        }

        int index = (int)(addr & 0x1FFFFF);
        ushort value = (ushort)(Registers[rt] & 0xFFFF);

        RAM[index] = (byte)((value >> 8) & 0xFF);
        RAM[index + 1] = (byte)(value & 0xFF);
    }



    private void LWL(uint instruction)
    {
        uint baseReg = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short offset = (short)(instruction & 0xFFFF);
        uint addr = Registers[baseReg] + (uint)offset;
        uint alignedAddr = addr & ~3u;
        int shift = (int)((3 - (addr & 3)) * 8);
        uint mem = ReadWord(alignedAddr);
        uint mask = 0xFFFFFFFF >> shift;
        Registers[rt] = (Registers[rt] & mask) | (mem << shift);
    }

    private void LWR(uint instruction)
    {
        uint baseReg = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short offset = (short)(instruction & 0xFFFF);
        uint addr = Registers[baseReg] + (uint)offset;
        uint alignedAddr = addr & ~3u;
        int shift = (int)((addr & 3) * 8);
        uint mem = ReadWord(alignedAddr);
        uint mask = 0xFFFFFFFF << (24 - shift);
        Registers[rt] = (Registers[rt] & mask) | (mem >> shift);
    }

    private void SWL(uint instruction)
    {
        uint baseReg = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short offset = (short)(instruction & 0xFFFF);
        uint addr = Registers[baseReg] + (uint)offset;
        uint alignedAddr = addr & ~3u;
        int shift = (int)((3 - (addr & 3)) * 8);
        uint data = Registers[rt] >> shift;

        uint orig = ReadWord(alignedAddr);
        uint mask = 0xFFFFFFFF << shift;
        uint result = (orig & ~mask) | (data & mask);
        WriteWord(alignedAddr, result);
    }

    private void SWR(uint instruction)
    {
        uint baseReg = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        short offset = (short)(instruction & 0xFFFF);
        uint addr = Registers[baseReg] + (uint)offset;
        uint alignedAddr = addr & ~3u;
        int shift = (int)((addr & 3) * 8);
        uint data = Registers[rt] << shift;

        uint orig = ReadWord(alignedAddr);
        uint mask = 0xFFFFFFFF >> (24 - shift);
        uint result = (orig & ~mask) | (data & mask);
        WriteWord(alignedAddr, result);
    }

    private void HandleCOP2(uint instruction)
    {
        uint funct = instruction & 0x3F;
        uint rs = (instruction >> 21) & 0x1F;

        if ((instruction & 0x03E00000) == 0) // MFC2
        {
            uint rt = (instruction >> 16) & 0x1F;
            uint rd = (instruction >> 11) & 0x1F;
            Registers[rt] = GTE_MFC2(rd);
        }
        else if ((instruction & 0x03E00000) == 0x00400000) // CFC2
        {
            // similar, lê control
        }
        else if ((instruction & 0x03E00000) == 0x00800000) // MTC2
        {
            uint rt = (instruction >> 16) & 0x1F;
            uint rd = (instruction >> 11) & 0x1F;
            GTE_MTC2(rd, Registers[rt]);
        }
        else if ((instruction & 0x03E00000) == 0x00C00000) // CTC2
        {
            // similar
        }
        else
        {
            // Executa instrução GTE
            ExecuteGTEInstruction(instruction);
        }
    }



    private void HandleSyscall()
    {
        RaiseException(8); // Exceção tipo 8 = SYSCALL
    }

    private void HandleBreak()
    {
        RaiseException(9); // Exceção tipo 9 = BREAK
    }


    private void HandleCOP0(uint instruction)
    {
        uint rs = (instruction >> 21) & 0x1F;
        uint rt = (instruction >> 16) & 0x1F;
        uint rd = (instruction >> 11) & 0x1F;
        uint funct = instruction & 0x3F;

        switch (rs)
        {
            case 0x00: // MFC0
                Registers[rt] = Cop0[rd];
                break;

            case 0x04: // MTC0
                Cop0[rd] = Registers[rt];
                break;

            case 0x10: // ERET
                if (funct == 0x18)
                {
                    PC = Cop0[COP0_EPC];
                    Cop0[COP0_STATUS] &= ~(1u << 1); // limpa EXL
                }
                else
                {
                    RaiseException(10); // Reserved Instruction
                }
                break;

            default:
                RaiseException(10); // Reserved Instruction
                break;
        }
    }


    private void HandleERET()
    {
        // Sai do modo de exceção: limpa bit EXL
        COP0Registers[12] &= ~(uint)0x2;

        // Retorna para a instrução que causou exceção
        PC = COP0Registers[14];
    }


    private void RaiseException(byte code)
    {
        Cop0[COP0_EPC] = PC - 4;
        Cop0[COP0_CAUSE] = (uint)(code << 2);  // bits 6..2
        Cop0[COP0_STATUS] |= (1u << 1);        // EXL = 1
        PC = 0x80000080;

        throw new CpuException(code);
    }


    private void HandleExceptionHandler(uint instruction)
    {
        // Para começar, só vamos imprimir o tipo da exceção e pular o handler (exemplo simples)

        uint cause = COP0Registers[13];
        byte exceptionCode = (byte)(cause >> 2);

        Console.WriteLine($"Exceção tratada: código {exceptionCode}, PC={PC:X8}");

        // Simula execução do handler: normalmente aqui a BIOS faz várias operações,
        // mas agora só avançamos o PC para evitar loop infinito

        PC += 4; // avança para próxima instrução

        // Se a instrução for ERET, retorne da exceção
        uint opcode = (instruction >> 26) & 0x3F;
        uint funct = instruction & 0x3F;

        if (opcode == 0x10 && funct == 0x18) // ERET
        {
            HandleERET();
        }
    }

    private uint GTE_MFC2(uint reg)
    {
        return GTE_Data[reg & 0x1F];
    }
    private uint GTE_CFC2(uint reg)
    {
        return GTE_Control[reg & 0x1F];
    }

    public void MTC0(int reg, uint val)
    {
        Cop0[reg] = val;
    }


    private void GTE_MTC2(uint reg, uint value)
    {
        GTE_Data[reg & 0x1F] = value;
    }

    private void GTE_CTC2(uint reg, uint value)
    {
        GTE_Control[reg & 0x1F] = value;
    }

    private void ExecuteGTEInstruction(uint instruction)
    {
        uint funct = instruction & 0x3F;
        uint opcode = (instruction >> 20) & 0x1F;

        switch (funct)
        {
            case 0x01: // RTPS
                GTE_RTPS();
                break;
            case 0x06: // NCLIP
                GTE_NCLIP();
                break;
            default:
                throw new NotImplementedException($"GTE instruction 0x{funct:X2} not implemented");
        }
    }

    private void GTE_RTPS()
    {
        short vx = (short)(GTE_Data[0] & 0xFFFF);
        short vy = (short)(GTE_Data[0] >> 16);
        short vz = (short)(GTE_Data[1] & 0xFFFF);

        // Matriz de rotação
        short RT11 = (short)(GTE_Control[4] >> 16);
        short RT12 = (short)(GTE_Control[4] & 0xFFFF);
        short RT13 = (short)(GTE_Control[5] >> 16);

        short RT21 = (short)(GTE_Control[5] & 0xFFFF);
        short RT22 = (short)(GTE_Control[6] >> 16);
        short RT23 = (short)(GTE_Control[6] & 0xFFFF);

        short RT31 = (short)(GTE_Control[7] >> 16);
        short RT32 = (short)(GTE_Control[7] & 0xFFFF);
        short RT33 = (short)(GTE_Control[8] >> 16);

        short TRX = (short)(GTE_Control[0] >> 0);
        short TRY = (short)(GTE_Control[1] >> 0);
        short TRZ = (short)(GTE_Control[2] >> 0);

        int mac1 = (RT11 * vx + RT12 * vy + RT13 * vz + (TRX << 12));
        int mac2 = (RT21 * vx + RT22 * vy + RT23 * vz + (TRY << 12));
        int mac3 = (RT31 * vx + RT32 * vy + RT33 * vz + (TRZ << 12));

        short ir1 = ClampIR(mac1 >> 12, 0);
        short ir2 = ClampIR(mac2 >> 12, 1);
        short ir3 = ClampIR(mac3 >> 12, 2);

        GTE_Data[9] = (uint)(ushort)ir1;
        GTE_Data[10] = (uint)(ushort)ir2;
        GTE_Data[11] = (uint)(ushort)ir3;

        int sz3 = ir3;
        GTE_Data[20] = (uint)(ushort)sz3;

        // Projeção perspectiva
        int h = (short)GTE_Control[11];
        int ofx = (short)GTE_Control[16];
        int ofy = (short)GTE_Control[17];

        int screenX = ((ir1 * h) / Math.Max(sz3, 1)) + ofx;
        int screenY = ((ir2 * h) / Math.Max(sz3, 1)) + ofy;

        GTE_Data[14] = (uint)(ushort)screenX; // SX2
        GTE_Data[15] = (uint)(ushort)screenY; // SY2
    }

    private void GTE_DPCS()
    {
        short r = (short)(GTE_Data[9] & 0x1F);
        short g = (short)(GTE_Data[10] & 0x1F);
        short b = (short)(GTE_Data[11] & 0x1F);

        short rfc = (short)(GTE_Data[6] & 0x1F);
        short gfc = (short)(GTE_Data[7] & 0x1F);
        short bfc = (short)(GTE_Data[8] & 0x1F);

        short ir0 = (short)(GTE_Data[20] & 0xFFFF);

        int mac1 = ((rfc - r) * ir0 + (r << 12));
        int mac2 = ((gfc - g) * ir0 + (g << 12));
        int mac3 = ((bfc - b) * ir0 + (b << 12));

        GTE_Data[25] = (uint)mac1;
        GTE_Data[26] = (uint)mac2;
        GTE_Data[27] = (uint)mac3;

        short ir1 = ClampToIR(mac1 >> 12);
        short ir2 = ClampToIR(mac2 >> 12);
        short ir3 = ClampToIR(mac3 >> 12);

        GTE_Data[21] = (ushort)ir1;
        GTE_Data[22] = (ushort)ir2;
        GTE_Data[23] = (ushort)ir3;

        // Resulting RGB
        GTE_Data[9] = (ushort)ir1;
        GTE_Data[10] = (ushort)ir2;
        GTE_Data[11] = (ushort)ir3;
    }


    private short ClampToIR(int value)
    {
        if (value > 0x7FFF) return 0x7FFF;
        if (value < -0x8000) return -0x8000;
        return (short)value;
    }



    private short ClampIR(int value, int irIndex)
    {
        if (value > 0x7FFF)
        {
            // GTE flag overflow
            GTE_Control[31] |= (1u << (27 - irIndex)); // set IR1/2/3 overflow bit
            return 0x7FFF;
        }
        else if (value < -0x8000)
        {
            GTE_Control[31] |= (1u << (24 - irIndex));
            return unchecked((short)0x8000);
        }
        return (short)value;
    }

    private void GTE_NCLIP()
    {
        short sx0 = (short)(GTE_Data[12] & 0xFFFF);
        short sy0 = (short)(GTE_Data[13] & 0xFFFF);
        short sx1 = (short)(GTE_Data[14] & 0xFFFF);
        short sy1 = (short)(GTE_Data[15] & 0xFFFF);
        short sx2 = (short)(GTE_Data[16] & 0xFFFF);
        short sy2 = (short)(GTE_Data[17] & 0xFFFF);

        int mac0 =
            sx0 * sy1 + sx1 * sy2 + sx2 * sy0
          - sx0 * sy2 - sx1 * sy0 - sx2 * sy1;

        GTE_Data[24] = (uint)mac0;
    }

    // Executa uma instrução manualmente (sem buscar da RAM)
    public void ExecuteRaw(uint instruction)
    {
        ExecuteInstruction(instruction);
    }

    // Reseta a CPU (registradores e RAM)
    public void ResetCPU()
    {
        Array.Clear(Registers, 0, Registers.Length);
        Array.Clear(RAM, 0, RAM.Length);
        PC = 0x0;
    }

    // Exceção personalizada
    public class CpuException : Exception
    {
        public byte Code { get; }

        public CpuException(byte code) : base($"Exceção {code}")
        {
            Code = code;
        }
    }

}



