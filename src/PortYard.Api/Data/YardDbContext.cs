using Microsoft.EntityFrameworkCore;
using PortYard.Domain.Entities;

namespace PortYard.Api.Data;

public class YardDbContext(DbContextOptions<YardDbContext> options) : DbContext(options)
{
    public DbSet<Container> Containers => Set<Container>();
    public DbSet<YardSlot> YardSlots => Set<YardSlot>();
    public DbSet<Movement> Movements => Set<Movement>();
    public DbSet<CustomsHold> CustomsHolds => Set<CustomsHold>();
    public DbSet<Vessel> Vessels => Set<Vessel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(YardDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
