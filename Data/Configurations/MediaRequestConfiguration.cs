using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoviesMafia.Domain.Entities;

namespace MoviesMafia.Data.Configurations;

public sealed class MediaRequestConfiguration : IEntityTypeConfiguration<MediaRequest>
{
    public void Configure(EntityTypeBuilder<MediaRequest> builder)
    {
        builder.ToTable("MediaRequests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.Name).IsRequired().HasMaxLength(256);
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.CreatedBy).HasMaxLength(256);
        builder.Property(r => r.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(r => r.UserId);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
