using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortYard.Domain.Entities;

namespace PortYard.Api.Data.Configurations;

public class YardSlotConfiguration : IEntityTypeConfiguration<YardSlot>
{
    public void Configure(EntityTypeBuilder<YardSlot> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Block)
            .IsRequired()
            .HasMaxLength(10);

        builder.HasIndex(s => new { s.Block, s.Row, s.Tier }).IsUnique();

        // Application-managed concurrency token rather than IsRowVersion(): SQLite has no
        // server-generated rowversion type the way SQL Server does, and this needs to keep
        // working unchanged once the database provider changes. LastModifiedAt is set
        // explicitly by the domain (YardSlot.AddContainer/RemoveContainer) every time
        // occupancy changes, and EF compares the originally-read value against what's
        // currently in the database on SaveChanges, regardless of provider.
        builder.Property(s => s.LastModifiedAt).IsConcurrencyToken();

        builder.Ignore(s => s.Code);
        builder.Ignore(s => s.UsedTeu);
        builder.Ignore(s => s.RemainingTeu);

        builder.Navigation(s => s.Containers).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
