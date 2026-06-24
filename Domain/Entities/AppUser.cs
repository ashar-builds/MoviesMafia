using Microsoft.AspNetCore.Identity;

namespace MoviesMafia.Domain.Entities;

/// <summary>Application user. Stores only the avatar file name (not an absolute path).</summary>
public class AppUser : IdentityUser, IBaseEntity
{
    /// <summary>File name of the user's avatar within the configured avatar store (e.g. "alice.png"). Empty when none.</summary>
    public string ProfilePictureFileName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public string UpdatedBy { get; set; } = "system";
}
