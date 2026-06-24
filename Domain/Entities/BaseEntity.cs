namespace MoviesMafia.Domain.Entities;

/// <summary>Audit fields stamped automatically by <c>AppDbContext.SaveChangesAsync</c>.</summary>
public interface IBaseEntity
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
    string CreatedBy { get; set; }
    string UpdatedBy { get; set; }
}

public abstract class BaseEntity : IBaseEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public string UpdatedBy { get; set; } = "system";
}
