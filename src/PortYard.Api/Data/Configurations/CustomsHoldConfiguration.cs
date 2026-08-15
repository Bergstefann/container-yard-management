using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortYard.Domain.Entities;

namespace PortYard.Api.Data.Configurations;

public class CustomsHoldConfiguration : IEntityTypeConfiguration<CustomsHold>
{
    public void Configure(EntityTypeBuilder<CustomsHold> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Reason).IsRequired().HasMaxLength(500);
        builder.Ignore(h => h.IsActive);
    }
}
