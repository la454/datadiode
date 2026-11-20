namespace DataDiode.Authorization.Dtos;

public class AccessInfoDto
{
    public int UserId { get; set; }
    public string? Role { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsUser { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
}

public class RoleCheckResultDto
{
    public string Role { get; set; } = "";
    public bool Allowed { get; set; }
}
