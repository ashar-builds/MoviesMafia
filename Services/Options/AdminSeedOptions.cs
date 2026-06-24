namespace MoviesMafia.Services.Options;

/// <summary>Initial admin account seeded on startup. Bound from the "AdminSeed" configuration section.</summary>
public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Avatar file name within the avatar store (optional).</summary>
    public string ProfilePictureFileName { get; set; } = string.Empty;
}
