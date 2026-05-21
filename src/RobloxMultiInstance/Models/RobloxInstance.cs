namespace RobloxMultiInstance.Models;

public class RobloxInstance
{
    public int Pid { get; init; }
    public IntPtr Hwnd { get; set; }
    public string Title { get; set; } = "";
    public DateTime SeenAt { get; init; } = DateTime.UtcNow;
    public Guid? AccountId { get; set; }
    public bool InPlayMode { get; set; }
    public Win32Rect? StorageRect { get; set; }

    public record Win32Rect(int X, int Y, int Width, int Height);
}
