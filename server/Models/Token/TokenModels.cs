using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DataDiode.Models.Token;

/// <summary>One row of the dashboard's AccessTokenSessions table (read only here).</summary>
public class AccessTokenSession
{
    [Key]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    [Required, StringLength(64)]
    public string Jti { get; set; } = "";
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    [StringLength(200)]
    public string? RevokeReason { get; set; }
    [Required, StringLength(45)]
    public string IssuedIp { get; set; } = "";
}

/// <summary>The dashboard's token database (connection string "ConnectionStrings:ExternalToken"): live sessions of the access tokens.</summary>
public class AccessGateSessionsContext : DbContext
{
    public AccessGateSessionsContext(DbContextOptions<AccessGateSessionsContext> options) : base(options) { }

    public DbSet<AccessTokenSession> AccessTokenSessions => Set<AccessTokenSession>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AccessTokenSession>().Property(e => e.Id).ValueGeneratedNever();
    }
}

/// <summary>A user of the company dashboard (table "User" in the dashboard database). Read only: used to get the name and e-mail of a signed-in dashboard user.</summary>
public class DashboardUserRecord
{
    public decimal UserId { get; set; }
    public Guid PublicUserId { get; set; }
    public string? DisplayName { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>The dashboard's user table, read from the dashboard database (connection string "ConnectionStrings:ExternalAuth"). Optional.</summary>
public class ExternalAuthDbContext : DbContext
{
    public ExternalAuthDbContext(DbContextOptions<ExternalAuthDbContext> options) : base(options) { }

    public DbSet<DashboardUserRecord> Users => Set<DashboardUserRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<DashboardUserRecord>(e => { e.ToTable("User"); e.HasKey(x => x.UserId); e.Property(x => x.UserId).ValueGeneratedNever(); });
    }
}
