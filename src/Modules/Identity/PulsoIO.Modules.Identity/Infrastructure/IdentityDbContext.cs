using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PulsoIO.Modules.Identity.Domain;

namespace PulsoIO.Modules.Identity.Infrastructure;

internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("identity");

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.Property(user => user.Name).HasMaxLength(150).IsRequired();
            entity.Property(user => user.IsActive).HasDefaultValue(true).IsRequired();
            entity.Property(user => user.ClientId);
            entity.Property(user => user.ProfilePhoto).HasColumnType("bytea");
            entity.Property(user => user.ProfilePhotoContentType).HasMaxLength(32);
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.NormalizedEmail).HasMaxLength(320).IsRequired();
            entity.Property(user => user.UserName).HasMaxLength(320).IsRequired();
            entity.Property(user => user.NormalizedUserName).HasMaxLength(320).IsRequired();
            entity.HasIndex(user => user.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .IsUnique();
            entity.HasIndex(user => user.ClientId)
                .HasDatabaseName("IX_users_client_id");
        });

        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(token => token.Id);
            entity.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(token => token.SecurityStamp).HasMaxLength(64).IsRequired();
            entity.Property(token => token.ReplacedByTokenHash).HasMaxLength(64);
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => new { token.UserId, token.FamilyId });
            entity.HasIndex(token => token.ExpiresAtUtc);
            entity.HasOne(token => token.User)
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
