/// <summary>Per-user folder helpers shared by Program.cs and AppController.</summary>
public class FileStore(string storageRoot)
{
    public static string[] ParseExts(string? s) =>
        (s ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(e => e.TrimStart('.').ToLowerInvariant()).Distinct().ToArray();

    public string UserDir(AppUser u) =>
        string.IsNullOrWhiteSpace(u.FolderPath) ? Path.Combine(storageRoot, u.FolderName) : u.FolderPath;

    public bool AllowedExt(AppUser u, string name)
    {
        var allowed = ParseExts(u.AllowedExtensions);
        return allowed.Length == 0 || allowed.Contains(Path.GetExtension(name).TrimStart('.').ToLowerInvariant());
    }

    public IEnumerable<FileInfo> VisibleFiles(AppUser u)
    {
        var dir = UserDir(u);
        return Directory.Exists(dir)
            ? new DirectoryInfo(dir).EnumerateFiles().Where(f => AllowedExt(u, f.Name))
            : [];
    }

    public string? SafeFilePath(AppUser u, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name)) return null;
        var dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(UserDir(u)));
        var full = Path.GetFullPath(Path.Combine(dir + Path.DirectorySeparatorChar, name));
        var parent = Path.GetDirectoryName(full);
        return parent is not null && string.Equals(Path.TrimEndingDirectorySeparator(parent), dir, StringComparison.OrdinalIgnoreCase)
            ? full : null;
    }

    public long UsedBytes(AppUser u) => VisibleFiles(u).Sum(f => f.Length);
}
