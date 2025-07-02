public class Timer
{
    public ushort Counter = 0;
    public ushort Target = 0;
    public ushort Mode = 0;

    public bool IRQPending = false;
    public bool IRQEnabled => (Mode & (1 << 10)) != 0;
    public bool ResetOnTarget => (Mode & (1 << 4)) != 0;

    public void Tick()
    {
        Counter++;

        if (Counter == Target)
        {
            if (IRQEnabled)
                IRQPending = true;

            if (ResetOnTarget)
                Counter = 0;
        }
    }

    public void Reset()
    {
        Counter = 0;
        Mode = 0;
        Target = 0;
        IRQPending = false;
    }
}
