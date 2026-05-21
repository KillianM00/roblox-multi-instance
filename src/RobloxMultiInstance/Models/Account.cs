namespace RobloxMultiInstance.Models;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Alias { get; set; } = "";
    public string Cookie { get; set; } = "";
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public long? UserId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public string? AvatarUrl { get; set; }
}
