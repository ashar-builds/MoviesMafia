using MoviesMafia.Domain.Enums;

namespace MoviesMafia.Domain.Entities;

/// <summary>A user's request for a movie or series to be added to the library.</summary>
public class MediaRequest : BaseEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public MediaType Type { get; set; }
}
