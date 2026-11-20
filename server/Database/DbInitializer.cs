using System.Text.RegularExpressions;
using DataDiode.Authorization.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Start-up database work: create the database if it is missing, add the tables / columns / rows the sign-in and authorization code needs
/// (database/Add_External_Auth.sql, safe to run on every start) and seed the first administrator.
/// </summary>
public static class DbInitializer
{
    public static void Initialize(IServiceProvider services, IConfiguration cfg, ILogger log)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var store = scope.ServiceProvider.GetRequiredService<FileStore>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();

        db.Database.EnsureCreated();
        ApplyScript(db, log);

        if (!db.Users.Any())
        {
            var adminRole = db.Roles.FirstOrDefault(r => r.Name == AuthRoles.Admin)
                ?? throw new InvalidOperationException("The Admin role is missing: database/Add_External_Auth.sql did not run.");
            var admin = new AppUser
            {
                Username = (cfg["SeedAdmin:Username"] ?? "admin").Trim(),
                Email = string.IsNullOrWhiteSpace(cfg["SeedAdmin:Email"]) ? null : cfg["SeedAdmin:Email"]!.Trim().ToLowerInvariant(),
                FullName = cfg["SeedAdmin:FullName"] ?? "Administrator",
                IsAdmin = true,
                IsActive = true,
                FolderName = Guid.NewGuid().ToString("N"),
                QuotaBytes = 1024L * 1024 * 1024,
                CreatedAt = DateTime.UtcNow,
            };
            admin.PasswordHash = hasher.HashPassword(admin, cfg["SeedAdmin:Password"] ?? "Admin@123");
            Directory.CreateDirectory(store.UserDir(admin));
            admin.UserRoles.Add(new UserRole { Role = adminRole });
            db.Users.Add(admin);
            db.SaveChanges();
            log.LogWarning("Seeded default admin account. Change its password immediately.");
        }
    }

    /// <summary>Runs the SQL script batch by batch (GO is a client keyword, not T-SQL). Every step in it checks before it changes anything.</summary>
    private static void ApplyScript(AppDb db, ILogger log)
    {
        var file = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Database", "Add_External_Auth.sql"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "database", "Add_External_Auth.sql")),
        }.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("database/Add_External_Auth.sql was not found next to the server. Run it by hand with sqlcmd, or copy it to <server folder>/Database/.");

        var batches = Regex.Split(File.ReadAllText(file), @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Where(b => !string.IsNullOrWhiteSpace(b)).ToList();

        var conn = db.Database.GetDbConnection();
        var wasClosed = conn.State != System.Data.ConnectionState.Open;
        if (wasClosed) conn.Open();
        try
        {
            foreach (var batch in batches)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = batch;
                cmd.CommandTimeout = 120;
                cmd.ExecuteNonQuery();
            }
            log.LogInformation("Database schema is up to date ({Count} script batches checked).", batches.Count);
        }
        finally
        {
            if (wasClosed) conn.Close();
        }
    }
}
