using DataDiode;
using DataDiode.Authentication;
using DataDiode.Authorization.Middleware;
using DataDiode.Models.Token;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

var storageRoot = Path.GetFullPath(cfg["Storage:Root"] ?? "UserFiles", builder.Environment.ContentRootPath);
Directory.CreateDirectory(storageRoot);
var store = new FileStore(storageRoot);

builder.Services.AddSingleton(store);
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDb>(o => o.UseSqlServer(cfg.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default missing")));

// ---- External auth database (the dashboard user table) - optional until its connection string is set ----
var externalAuth = cfg.GetConnectionString("ExternalAuth");
if (!string.IsNullOrWhiteSpace(externalAuth))
    builder.Services.AddDbContext<ExternalAuthDbContext>(o => o.UseSqlServer(externalAuth));

builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// ---- Auth: DataDiode's own login + the dashboard's cookie login (see Authentication/DashboardAuthExtensions.cs) ----
// and the permission based authorization ([HasPermission("users.manage")], roles from the Roles / UserRoles tables).
builder.Services.AddDataDiodeAuth(cfg);

// Allowed browser origins: the configured list plus this machine's own name / FQDN on the frontend port.
// Credentials are allowed: the dashboard sign-in is a cookie.
var corsOrigins = (cfg.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"]).ToList();
var feScheme = cfg["Cors:FrontendScheme"] ?? "http";
var fePort = cfg["Cors:FrontendPort"] ?? "4200";
corsOrigins.Add($"{feScheme}://{Environment.MachineName}:{fePort}");
try { corsOrigins.Add($"{feScheme}://{System.Net.Dns.GetHostEntry("").HostName}:{fePort}"); } catch { }
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(corsOrigins.Select(x => x.TrimEnd('/')).Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = long.MaxValue);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = null);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "DataDiode API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Paste the token returned by /api/auth/login.",
    });
    o.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

// ---------- db init: create if missing, add the auth tables / columns / rows, seed the first admin ----------
DbInitializer.Initialize(app.Services, cfg, app.Logger);

// BusinessException -> clean JSON { message } with the right status code
app.UseMiddleware<BusinessExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// The built Angular app (wwwroot) can be served by the API too (one address, so the dashboard cookie is never cross-site).
var hasSpa = File.Exists(Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html"));
var staticOptions = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // index.html is never cached, so a new build (new hashed script / style names) is picked up on the next refresh
        if (ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    }
};
if (hasSpa)
{
    app.UseDefaultFiles();
    app.UseStaticFiles(staticOptions);
}

app.UseCors();
app.UseMiddleware<ForbiddenResponseMiddleware>();
app.UseMiddleware<UnauthorizedAccessAuditMiddleware>();
app.UseAuthentication();
app.UseCookieRequestGuard();
app.UseAuthorization();

app.MapControllers();
// any other address (not /api, not /swagger, and without a file extension) is an Angular page: send index.html
if (hasSpa) app.MapFallbackToFile("{*path:regex(^(?!api/|swagger)[^.]*$)}", "index.html", staticOptions);

app.Run();
