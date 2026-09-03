using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IvaoHub.Core.Auth;

/// <summary>The IVAO tokens of a user, in clear, only ever inside the process.</summary>
public sealed record IvaoUserTokens(string AccessToken, string? RefreshToken, DateTime ExpiresAtUtc, string? Scopes);

/// <summary>
/// Stores the IVAO access and refresh tokens encrypted in <c>hub_user_tokens</c> rather than in the
/// authentication cookie: some modules call the IVAO API on behalf of the user, and a cookie that
/// carries credentials travels with every single request.
/// <para>If the keys in <c>hub-keys/</c> are lost the values cannot be read any more. That is
/// treated as "no token", never as an error: the user simply logs in again (plan section 16.14).</para>
/// </summary>
public sealed class IvaoUserTokenStore(
    HubDbContext database,
    IDataProtectionProvider dataProtection,
    IClock clock,
    ILogger<IvaoUserTokenStore> logger)
{
    /// <summary>Purpose string of the protector. Changing it makes every stored token unreadable.</summary>
    public const string Purpose = "IvaoTokens";

    private readonly IDataProtector _protector = dataProtection.CreateProtector(Purpose);

    public async Task SaveAsync(int vid, IvaoUserTokens tokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var row = await database.UserTokens.FirstOrDefaultAsync(token => token.Vid == vid, cancellationToken);
        if (row is null)
        {
            row = new UserToken { Vid = vid };
            database.UserTokens.Add(row);
        }

        row.AccessTokenEnc = _protector.Protect(tokens.AccessToken);
        row.RefreshTokenEnc = tokens.RefreshToken is null ? null : _protector.Protect(tokens.RefreshToken);
        row.ExpiresAt = tokens.ExpiresAtUtc;
        row.Scopes = tokens.Scopes;
        row.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Null when there is no row, or when the row can no longer be decrypted.</summary>
    public async Task<IvaoUserTokens?> GetAsync(int vid, CancellationToken cancellationToken = default)
    {
        var row = await database.UserTokens.AsNoTracking()
            .FirstOrDefaultAsync(token => token.Vid == vid, cancellationToken);

        if (row is null)
        {
            return null;
        }

        try
        {
            return new IvaoUserTokens(
                _protector.Unprotect(row.AccessTokenEnc),
                row.RefreshTokenEnc is null ? null : _protector.Unprotect(row.RefreshTokenEnc),
                row.ExpiresAt,
                row.Scopes);
        }
        catch (Exception exception)
        {
            // The key ring is gone or was replaced. Nothing to do but ask for a new login.
            logger.LogWarning(
                exception,
                "The stored IVAO tokens of {Vid} cannot be decrypted; treating them as absent. "
                + "The Data Protection keys in hub-keys/ were probably lost or replaced.",
                vid);
            return null;
        }
    }

    /// <summary>
    /// Tracked, not <c>ExecuteDelete</c>. A bulk delete goes straight to the server and never
    /// reaches the save changes interceptor, so it would be a hole in the audit, in the write guard
    /// and in the projections. It costs one extra query on a logout, which is a good price for
    /// having exactly one way into the database.
    /// </summary>
    public async Task DeleteAsync(int vid, CancellationToken cancellationToken = default)
    {
        var rows = await database.UserTokens.Where(token => token.Vid == vid).ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return;
        }

        database.UserTokens.RemoveRange(rows);
        await database.SaveChangesAsync(cancellationToken);
    }
}
