using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortYard.Domain.Entities;

namespace PortYard.Api.Data.Configurations;

public class MovementConfiguration : IEntityTypeConfiguration<Movement>
{
    public void Configure(EntityTypeBuilder<Movement> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Operator).IsRequired().HasMaxLength(100);
        builder.Property(m => m.OccurredAt).IsRequired();

        // FromSlotId/ToSlotId are computed from the FromSlot/ToSlot navigations (see Movement),
        // so EF maps the relationship through separately named shadow FK columns instead — using
        // "FromSlotId" here too would make EF bind the FK to the ignored, unsettable CLR property.
        builder.Ignore(m => m.FromSlotId);
        builder.Ignore(m => m.ToSlotId);

        builder.HasOne(m => m.FromSlot)
            .WithMany()
            .HasForeignKey("FromSlotRefId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.ToSlot)
            .WithMany()
            .HasForeignKey("ToSlotRefId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
