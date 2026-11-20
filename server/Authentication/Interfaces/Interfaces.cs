using DataDiode.Models.Token;

namespace DataDiode.Authentication.Interfaces;

public interface ITokenValidatorService
{
    /// <summary>The dashboard's session row for this user and token id (jti), or null when there is none.</summary>
    Task<AccessTokenSession?> GetAccessTokenSessionAsync(string userId, string jti);

    /// <summary>The dashboard's session row that holds this refresh token (the value of the refresh cookie), or null.</summary>
    Task<AccessTokenSession?> GetSessionByRefreshTokenAsync(string refreshToken);
}

public interface IUserResolver
{
    /// <summary>
    /// Finds the DataDiode user of a dashboard user: by the GUID first, then by e-mail (the GUID is then stored).
    /// A dashboard user that DataDiode has never seen gets a DataDiode record with NO role (an admin assigns roles later).
    /// </summary>
    Task<AppUser?> ResolveAsync(Guid? externalId, string? email, string? fullName);
}
