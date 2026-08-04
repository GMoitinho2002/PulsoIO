using Microsoft.EntityFrameworkCore;
using PulsoIO.Modules.Administration.Domain;

namespace PulsoIO.Modules.Administration.Infrastructure;

internal sealed class AdministrationDbContext(DbContextOptions<AdministrationDbContext> options)
    : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();

    public DbSet<ClientEnvironment> Environments => Set<ClientEnvironment>();

    public DbSet<Integration> Integrations => Set<Integration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("administration");

        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("clients");
            entity.HasKey(client => client.Id);
            entity.Property(client => client.Name).HasMaxLength(150).IsRequired();
            entity.Property(client => client.NormalizedName).HasMaxLength(150).IsRequired();
            entity.Property(client => client.IsActive).HasDefaultValue(true).IsRequired();
            entity.Property(client => client.CreatedAtUtc).IsRequired();
            entity.Property(client => client.UpdatedAtUtc).IsRequired();
            entity.Property(client => client.ConcurrencyToken).IsConcurrencyToken().IsRequired();
            entity.HasIndex(client => client.NormalizedName).IsUnique();
            entity.HasMany(client => client.Environments)
                .WithOne(environment => environment.Client)
                .HasForeignKey(environment => environment.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClientEnvironment>(entity =>
        {
            entity.ToTable("environments");
            entity.HasKey(environment => environment.Id);
            entity.Property(environment => environment.Name).HasMaxLength(100).IsRequired();
            entity.Property(environment => environment.NormalizedName).HasMaxLength(100).IsRequired();
            entity.Property(environment => environment.Kind).HasConversion<string>().HasMaxLength(32);
            entity.Property(environment => environment.IsActive).HasDefaultValue(true).IsRequired();
            entity.Property(environment => environment.CreatedAtUtc).IsRequired();
            entity.Property(environment => environment.UpdatedAtUtc).IsRequired();
            entity.Property(environment => environment.ConcurrencyToken).IsConcurrencyToken().IsRequired();
            entity.HasIndex(environment => new { environment.ClientId, environment.NormalizedName })
                .IsUnique();
            entity.HasAlternateKey(environment => new { environment.ClientId, environment.Id });
            entity.HasMany(environment => environment.Integrations)
                .WithOne(integration => integration.Environment)
                .HasForeignKey(integration => new
                {
                    integration.ClientId,
                    integration.EnvironmentId
                })
                .HasPrincipalKey(environment => new
                {
                    environment.ClientId,
                    environment.Id
                })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Integration>(entity =>
        {
            entity.ToTable("integrations");
            entity.HasKey(integration => integration.Id);
            entity.Property(integration => integration.Name).HasMaxLength(150).IsRequired();
            entity.Property(integration => integration.NormalizedName).HasMaxLength(150).IsRequired();
            entity.Property(integration => integration.Direction)
                .HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(integration => integration.SourceSystem).HasMaxLength(150).IsRequired();
            entity.Property(integration => integration.TargetSystem).HasMaxLength(150).IsRequired();
            entity.Property(integration => integration.HttpMethod).HasMaxLength(16);
            entity.Property(integration => integration.EndpointPattern).HasMaxLength(500);
            entity.Property(integration => integration.IsActive).HasDefaultValue(true).IsRequired();
            entity.Property(integration => integration.CreatedAtUtc).IsRequired();
            entity.Property(integration => integration.UpdatedAtUtc).IsRequired();
            entity.Property(integration => integration.ConcurrencyToken).IsConcurrencyToken().IsRequired();
            entity.HasIndex(integration => integration.ClientId);
            entity.HasIndex(integration => new
            {
                integration.EnvironmentId,
                integration.NormalizedName
            }).IsUnique();
        });
    }
}
