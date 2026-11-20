using System.Text;
using System.Text.Json;

namespace DataDiode.Authentication.Models;

/// <summary>The dashboard user stored inside the dashboard's token cookie.</summary>
public class DashboardUser
{
    public Guid? Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }

    public string FullName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(DisplayName)) return DisplayName.Trim();
            var n = ((FirstName ?? "") + " " + (LastName ?? "")).Trim();
            return n.Length > 0 ? n : (UserName ?? Email ?? "");
        }
    }
}

/// <summary>
/// The dashboard's cookie is a JSON object: { AccessToken, accessTokenExpiresAt, RefreshToken, User: { Id, FirstName, LastName, UserName, Email, displayName } }.
/// It may arrive as plain JSON, URL-encoded JSON or Base64 JSON; all three are accepted. Property names are case-insensitive.
/// Only the access token is used here: refreshing it is the dashboard's job.
/// </summary>
public class DashboardTokenCookie
{
    public string? AccessToken { get; set; }
    public DashboardUser? User { get; set; }

    public static bool TryRead(string? raw, out DashboardTokenCookie? cookie)
    {
        cookie = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        foreach (var candidate in Candidates(raw))
        {
            var text = candidate.Trim().Trim('"');
            if (text.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) text = text.Substring(7).Trim();
            // a bare JWT (three dot-separated parts) is accepted as the access token itself
            if (!text.StartsWith('{') && text.Split('.').Length == 3 && !text.Contains(' '))
            {
                cookie = new DashboardTokenCookie { AccessToken = text };
                return true;
            }
            var first = candidate.TrimStart();
            if (first.Length == 0 || (first[0] != '{' && first[0] != '"')) continue;     // not JSON: do not even try (no exception)
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                // a JSON string that wraps the real value ("{\"AccessToken\":...}" or "eyJ...")
                if (doc.RootElement.ValueKind == JsonValueKind.String)
                {
                    var inner = doc.RootElement.GetString();
                    if (!string.IsNullOrWhiteSpace(inner) && inner != raw && TryRead(inner, out cookie)) return true;
                    continue;
                }
                if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;
                var c = new DashboardTokenCookie();
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    if (string.Equals(p.Name, "accessToken", StringComparison.OrdinalIgnoreCase))
                        c.AccessToken = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
                    else if (string.Equals(p.Name, "user", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.Object)
                        c.User = ReadUser(p.Value);
                }
                if (string.IsNullOrWhiteSpace(c.AccessToken)) continue;
                cookie = c;
                return true;
            }
            catch (JsonException) { /* try the next form */ }
        }
        return false;
    }

    private static IEnumerable<string> Candidates(string raw)
    {
        yield return raw;
        string? decoded = null;
        try { decoded = Uri.UnescapeDataString(raw); } catch (UriFormatException) { }
        if (decoded != null && decoded != raw) yield return decoded;
        string? b64 = null;
        var t = raw.Trim().Replace('-', '+').Replace('_', '/');
        t = t.PadRight(t.Length + (4 - t.Length % 4) % 4, '=');
        var buffer = new byte[t.Length];
        if (Convert.TryFromBase64String(t, buffer, out var written)) b64 = Encoding.UTF8.GetString(buffer, 0, written);
        if (b64 != null) yield return b64;
    }

    private static DashboardUser ReadUser(JsonElement e)
    {
        var u = new DashboardUser();
        foreach (var p in e.EnumerateObject())
        {
            var name = p.Name.ToLowerInvariant();
            var str = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
            switch (name)
            {
                case "id": u.Id = Guid.TryParse(str, out var g) ? g : null; break;
                case "firstname": u.FirstName = str; break;
                case "lastname": u.LastName = str; break;
                case "username": u.UserName = str; break;
                case "email": u.Email = str; break;
                case "displayname": u.DisplayName = str; break;
            }
        }
        return u;
    }
}
