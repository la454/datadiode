using Microsoft.EntityFrameworkCore;

public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    /// <summary>Legacy flag, kept in step with the roles (an Admin role = true). The roles (UserRoles) are the source of truth.</summary>
    public bool IsAdmin { get; set; }
    /// <summary>Name of the predefined folder under the storage root (a GUID, never user-supplied).</summary>
    public string FolderName { get; set; } = "";
    /// <summary>Absolute path of the folder whose files this user sees. Empty = legacy default under the storage root.</summary>
    public string FolderPath { get; set; } = "";
    /// <summary>Comma separated list of extensions without dot, e.g. "pdf,png". Empty = all allowed.</summary>
    public string AllowedExtensions { get; set; } = "";
    public long QuotaBytes { get; set; } = 100L * 1024 * 1024;
    public int? GroupId { get; set; }
    public Group? Group { get; set; }

    public string? Email { get; set; }
    public string? FullName { get; set; }
    /// <summary>A disabled user cannot sign in (own login) and gets no identity from the dashboard.</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>The dashboard user's GUID (the dashboard identifies people by GUID, DataDiode by Id). Null = a user created here.</summary>
    public Guid? ExternalId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<UserRole> UserRoles { get; set; } = [];

    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Username : FullName!;
}

public class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<AppUser> Users { get; set; } = [];
}

public class Role
{
    public int RoleId { get; set; }
    public string Name { get; set; } = "";
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    /// <summary>System roles (Admin, User) cannot be deleted or renamed.</summary>
    public bool IsSystem { get; set; }
    public List<UserRole> UserRoles { get; set; } = [];
    public List<RolePermission> RolePermissions { get; set; } = [];
}

public class Permission
{
    public int PermissionId { get; set; }
    /// <summary>Stable code used by the code and the menu, e.g. "users.manage".</summary>
    public string Code { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string GroupName { get; set; } = "";
    public List<RolePermission> RolePermissions { get; set; } = [];
}

public class RolePermission
{
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}

public class UserRole
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public AppUser User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}

/// <summary>One audited event (a refused call, a sign-in, a role change...). Table: AuditLogs.</summary>
public class AuditLog
{
    public long AuditLogId { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string Action { get; set; } = "";
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().Property(u => u.Username).HasMaxLength(256);
        b.Entity<AppUser>().Property(u => u.Email).HasMaxLength(256);
        b.Entity<AppUser>().Property(u => u.FullName).HasMaxLength(256);
        b.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        b.Entity<AppUser>().HasIndex(u => u.ExternalId).IsUnique().HasDatabaseName("UX_Users_ExternalId");
        b.Entity<AppUser>().HasIndex(u => u.Email).IsUnique().HasDatabaseName("UX_Users_Email");
        b.Entity<Group>().ToTable("Groups");
        b.Entity<Group>().Property(g => g.Name).HasMaxLength(100);
        b.Entity<Group>().HasIndex(g => g.Name).IsUnique();
        b.Entity<AppUser>().HasOne(u => u.Group).WithMany(g => g.Users).HasForeignKey(u => u.GroupId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(r => r.RoleId);
            e.Property(r => r.Name).HasMaxLength(80);
            e.Property(r => r.NameAr).HasMaxLength(100);
            e.Property(r => r.Description).HasMaxLength(300);
            e.Property(r => r.IsSystem).ValueGeneratedNever();
            e.HasIndex(r => r.Name).IsUnique().HasDatabaseName("UX_Roles_Name");
        });
        b.Entity<Permission>(e =>
        {
            e.ToTable("Permissions");
            e.HasKey(p => p.PermissionId);
            e.Property(p => p.Code).HasMaxLength(80);
            e.Property(p => p.NameAr).HasMaxLength(150);
            e.Property(p => p.NameEn).HasMaxLength(150);
            e.Property(p => p.GroupName).HasMaxLength(60);
            e.HasIndex(p => p.Code).IsUnique().HasDatabaseName("UX_Permissions_Code");
        });
        b.Entity<RolePermission>(e =>
        {
            e.ToTable("RolePermissions");
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Permission).WithMany(p => p.RolePermissions).HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<UserRole>(e =>
        {
            e.ToTable("UserRoles");
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(a => a.AuditLogId);
            e.Property(a => a.Username).HasMaxLength(200);
            e.Property(a => a.Action).HasMaxLength(100);
            e.Property(a => a.EntityType).HasMaxLength(100);
            e.Property(a => a.EntityId).HasMaxLength(200);
            e.Property(a => a.Details).HasMaxLength(2000);
            e.Property(a => a.IpAddress).HasMaxLength(45);
            e.Property(a => a.UserAgent).HasMaxLength(300);
            e.Property(a => a.ErrorMessage).HasMaxLength(500);
            e.Property(a => a.Success).ValueGeneratedNever();
            e.HasIndex(a => a.CreatedAt).HasDatabaseName("IX_AuditLogs_CreatedAt").IsDescending();
            e.HasIndex(a => a.UserId).HasDatabaseName("IX_AuditLogs_UserId");
        });
    }
}

public record LoginRequest(string Username, string Password);
public record CreateUserRequest(string Username, string Password, bool IsAdmin, string? AllowedExtensions, long? QuotaMb, string? FolderPath, int? GroupId = null,
                                string? Email = null, string? FullName = null);
public record UpdateSettingsRequest(string? AllowedExtensions, long QuotaMb, string? FolderPath, int? GroupId = null,
                                    bool? IsActive = null, string? Email = null, string? FullName = null);
public record GroupRequest(string Name);
public record ResetPasswordRequest(string Password);
public record FileDto(string Name, string Extension, long Size, DateTime Created);
