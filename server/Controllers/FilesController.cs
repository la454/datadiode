using DataDiode.Authorization;
using DataDiode.Authorization.Constants;
using DataDiode.Authorization.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace DataDiode.Controllers;

/// <summary>The signed-in user's files (the folder assigned by the administrator).</summary>
[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController(AppDb db, FileStore store, IAuditService audit, IIdentityService identity) : AppControllerBase
{
    [HasPermission(Permissions.FilesView)]
    [HttpGet]
    public async Task<IActionResult> Files()
    {
        if (await CurrentUserAsync(db) is not { } u) return Unauthorized();
        var files = store.VisibleFiles(u)
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f => new FileDto(f.Name, f.Extension.TrimStart('.').ToLowerInvariant(), f.Length, f.CreationTimeUtc));
        return Ok(files);
    }

    [HasPermission(Permissions.FilesDownload)]
    [HttpGet("{name}")]
    public async Task<IActionResult> Download(string name)
    {
        if (await CurrentUserAsync(db) is not { } u) return Unauthorized();
        var path = store.SafeFilePath(u, name);
        if (path is null || !store.AllowedExt(u, name) || !System.IO.File.Exists(path)) return NotFound();
        await AuditAsync(audit, identity, AuditActions.FileDownloaded, "File", name, "Downloaded " + name + " (" + new FileInfo(path).Length + " bytes).");
        new FileExtensionContentTypeProvider().TryGetContentType(name, out var ct);
        return PhysicalFile(path, ct ?? "application/octet-stream", name);
    }

    [HasPermission(Permissions.FilesDelete)]
    [HttpDelete("{name}")]
    public async Task<IActionResult> DeleteFile(string name)
    {
        if (await CurrentUserAsync(db) is not { } u) return Unauthorized();
        var path = store.SafeFilePath(u, name);
        if (path is null || !store.AllowedExt(u, name) || !System.IO.File.Exists(path)) return NotFound();
        System.IO.File.Delete(path);
        await AuditAsync(audit, identity, AuditActions.FileDeleted, "File", name, "Deleted " + name + ".");
        return NoContent();
    }
}
