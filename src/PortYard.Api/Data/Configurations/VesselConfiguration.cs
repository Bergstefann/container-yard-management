using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortYard.Domain.Entities;

namespace PortYard.Api.Data.Configurations;

public class VesselConfiguration : IEntityTypeConfiguration<Vessel>
{
    public void Configure(EntityTypeBuilder<Vessel> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Name).IsRequired().HasMaxLength(200);
        builder.Property(v => v.Imo).IsRequired().HasMaxLength(7);

        builder.HasIndex(v => v.Imo).IsUnique();

        builder.Navigation(v => v.InboundContainers).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
