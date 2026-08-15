using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortYard.Domain.Entities;

namespace PortYard.Api.Data.Configurations;

public class ContainerConfiguration : IEntityTypeConfiguration<Container>
{
    public void Configure(EntityTypeBuilder<Container> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ContainerNumber)
            .IsRequired()
            .HasMaxLength(11);

        builder.HasIndex(c => c.ContainerNumber).IsUnique();

        builder.Property(c => c.Size).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(c => c.ShippingLine).IsRequired().HasMaxLength(100);

        builder.Ignore(c => c.Teu);

        builder.HasOne(c => c.CurrentSlot)
            .WithMany(s => s.Containers)
            .HasForeignKey(c => c.CurrentSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(c => c.CurrentSlot).AutoInclude(false);

        builder.HasOne(c => c.InboundVessel)
            .WithMany(v => v.InboundContainers)
            .HasForeignKey(c => c.InboundVesselId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Movements)
            .WithOne(m => m.Container)
            .HasForeignKey(m => m.ContainerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(c => c.Movements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(c => c.CustomsHolds)
            .WithOne(h => h.Container)
            .HasForeignKey(h => h.ContainerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(c => c.CustomsHolds).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
