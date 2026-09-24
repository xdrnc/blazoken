using Microsoft.EntityFrameworkCore;
using OAuthDemo.ResourceApi.Models;

namespace OAuthDemo.ResourceApi.Data;

public class ResourceDbContext : DbContext
{
    public ResourceDbContext(DbContextOptions<ResourceDbContext> options) : base(options) { }

    public DbSet<ClientData> ClientData { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ClientData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ClientId);
            entity.Property(e => e.ClientId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).IsRequired();
        });
    }
}