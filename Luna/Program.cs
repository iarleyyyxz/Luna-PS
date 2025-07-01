using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;

public class Program
{
    public static void Main()
    {
        var nativeWindowSettings = new NativeWindowSettings()
        {
            Size = new OpenTK.Mathematics.Vector2i(640, 480),
            Title = "PS1 GPU Emulador - Textura Demo",
            Flags = ContextFlags.ForwardCompatible
        };

        using var window = new EmuWindow(GameWindowSettings.Default, nativeWindowSettings);
        window.Run();
    }
}
