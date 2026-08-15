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

        builder.Ignore(s => s.Code);
        builder.Ignore(s => s.UsedTeu);
        builder.Ignore(s => s.RemainingTeu);

        builder.Navigation(s => s.Containers).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
