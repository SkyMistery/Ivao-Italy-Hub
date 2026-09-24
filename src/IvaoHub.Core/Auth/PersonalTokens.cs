using System.Security.Cryptography;
using System.Text;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Auth;

/// <summary>
/// What a personal token is for (M2, T19a, note 2026-09-15-token-personali-e-agente-del-validatore §3.1): a module declares
/// it, named after itself (<c>flightops.agent</c>), with the permission a member must hold somewhere to create one. The
/// words are in the module's language file, under <c>tokenAudiences.{name}</c>.
/// </summary>
public sealed record TokenAudienceDescriptor(string Key, string RequiredPermission);

/// <summary>Every audience of the installed modules. The core declares none of its own.</summary>
public sealed class TokenAudienceCatalog
{
    /// <summary>The width of <c>hub_personal_tokens.audience</c>.</summary>
    public const int MaxKeyLength = 64;

    private readonly Dictionary<string, TokenAudienceDescriptor> _byKey = new(StringComparer.Ordinal);

    /// <param name="declared">Each module's key and the audiences it declares.</param>
    public TokenAudienceCatalog(IEnumerable<(string Module, IReadOnlyList<TokenAudienceDescriptor> Audiences)> declared)
    {
        ArgumentNullException.ThrowIfNull(declared);

        foreach (var (module, audiences) in declared)
        {
            foreach (var audience in audiences)
            {
                if (!audience.Key.StartsWith(module + ".", StringComparison.Ordinal)
                    || audience.Key.Length == module.Length + 1
                    || audience.Key.Length > MaxKeyLength)
                {
                    throw new InvalidOperationException(
                        $"The token audience '{audience.Key}' of the module '{module}' has to be named "
                        + $"'{module}.<name>' and fit in {MaxKeyLength} characters.");
                }

                if (!_byKey.TryAdd(audience.Key, audience))
                {
                    throw new InvalidOperationException($"The token audience '{audience.Key}' is declared twice.");
                }
            }
        }
    }

    public IReadOnlyCollection<TokenAudienceDescriptor> All => _byKey.Values;

    public TokenAudienceDescriptor? Find(string? key) =>
        key is not null && _byKey.TryGetValue(key, out var found) ? found : null;

    /// <summary>The audiences this member may create a token for: those whose permission they hold somewhere.</summary>
    public IReadOnlyList<string> AvailableTo(ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.IsAuthenticated
            ? [.. _byKey.Values.Where(audience => user.HasAny(audience.RequiredPermission)).Select(audience => audience.Key).Order(StringComparer.Ordinal)]
            : [];
    }
}

/// <summary>
/// A token a member created for a program of theirs, <c>hub_personal_tokens</c>. Only its hash is kept: the token itself is
/// shown once, when it is created. It carries no permission — every request rebuilds the member's identity as a login
/// does — and it is accepted only by the endpoints of its <see cref="Audience"/>.
/// </summary>
[Audited]
public sealed class PersonalToken : IAuditable
{
    public long Id { get; set; }

    public int Vid { get; set; }

    /// <summary>What the member called it: «home PC».</summary>
    public string Name { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>SHA-256 of the token, in lowercase hex. Never the token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>The first characters of the token, to recognise it in the list.</summary>
    public string Prefix { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Bookkeeping, not a change: a request that only moves it leaves no audit row.</summary>
    [NotAudited]
    public DateTime? LastUsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}

/// <summary>Why a token was refused: one word out of a closed set, which picks the sentence the program shows.</summary>
public enum TokenRefusal
{
    /// <summary>Not a token of this hub, or not one it knows.</summary>
    Unknown,
    Revoked,
    Expired,

    /// <summary>The member has not signed in to the hub for too long: their positions may no longer be true.</summary>
    SignInAgain,
}

/// <summary>A token just created: the only moment its text exists outside the program that will hold it.</summary>
public sealed record IssuedToken(PersonalToken Row, string Token);

/// <summary>
/// Creating, revoking and recognising personal tokens (note 2026-09-15 §3.1). The rules are here and nowhere else: the
/// endpoints of <c>/me/tokens</c> and the authentication scheme both call this.
/// </summary>
public sealed class PersonalTokens(HubDbContext database, TokenAudienceCatalog audiences, IClock clock)
{
    /// <summary>What every token starts with, so a leaked one is recognisable by a scanner and by a person.</summary>
    public const string TokenPrefix = "hubpat_";

    /// <summary>The longest a token may live.</summary>
    public const int MaxDays = 90;

    /// <summary>
    /// A token is refused when its member last signed in longer ago than this: positions are refreshed at the login, and a
    /// validator who left the staff and never came back would otherwise keep them (note §3.1).
    /// </summary>
    public const int SignedInWithinDays = 30;

    /// <summary>How many live tokens one member may hold at once. Enough for a few computers, not a leak nobody tracks.</summary>
    public const int MaxActivePerMember = 10;

    public const int MaxNameLength = 64;

    /// <summary>The characters of the token shown in the list: the prefix and four more.</summary>
    private const int ShownLength = 11;

    /// <summary>A last use is written at most this often: a program that reads a hundred reports writes it once.</summary>
    private static readonly TimeSpan LastUseResolution = TimeSpan.FromMinutes(1);

    public IQueryable<PersonalToken> Of(int vid) => database.PersonalTokens.Where(token => token.Vid == vid);

    /// <summary>Creates a token for the member. The problems are keyed by the field of the form, as <c>ProblemDetails</c> are.</summary>
    public async Task<(IssuedToken? Issued, IReadOnlyDictionary<string, string[]>? Problems)> IssueAsync(
        ICurrentUser user,
        string name,
        string audience,
        int days,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var problems = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            problems["name"] = ["errors.required"];
        }
        else if (trimmed.Length > MaxNameLength)
        {
            problems["name"] = ["errors.text.tooLong"];
        }

        if (!audiences.AvailableTo(user).Contains(audience, StringComparer.Ordinal))
        {
            problems["audience"] = ["errors.token.audienceNotAllowed"];
        }

        if (days is < 1 or > MaxDays)
        {
            problems["days"] = ["errors.token.days"];
        }

        var now = clock.UtcNow;
        if (await Of(user.Vid).CountAsync(token => token.RevokedAt == null && token.ExpiresAt > now, cancellationToken) >= MaxActivePerMember)
        {
            problems["name"] = ["errors.token.tooMany"];
        }

        if (problems.Count > 0)
        {
            return (null, problems);
        }

        var token = TokenPrefix + Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var row = new PersonalToken
        {
            Vid = user.Vid,
            Name = trimmed,
            Audience = audience,
            TokenHash = Hash(token),
            Prefix = token[..ShownLength],
            ExpiresAt = now.AddDays(days),
        };

        database.PersonalTokens.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        return (new IssuedToken(row, token), null);
    }

    /// <summary>Revokes one of the member's tokens. False when it is not theirs, or not there: the same answer.</summary>
    public async Task<bool> RevokeAsync(int vid, long id, CancellationToken cancellationToken)
    {
        var row = await Of(vid).FirstOrDefaultAsync(token => token.Id == id, cancellationToken);
        if (row is null)
        {
            return false;
        }

        if (row.RevokedAt is null)
        {
            row.RevokedAt = clock.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// The token behind the text a program sent, and its member, or why not. A use is recorded on the row (at most once a
    /// minute); what the program then does is the endpoint's business.
    /// </summary>
    public async Task<(PersonalToken? Token, TokenRefusal? Refusal)> RecogniseAsync(string presented, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presented);

        if (!presented.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            return (null, TokenRefusal.Unknown);
        }

        var hash = Hash(presented);
        var row = await database.PersonalTokens.FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (row is null)
        {
            return (null, TokenRefusal.Unknown);
        }

        var now = clock.UtcNow;
        if (row.RevokedAt is not null)
        {
            return (null, TokenRefusal.Revoked);
        }

        if (row.ExpiresAt <= now)
        {
            return (null, TokenRefusal.Expired);
        }

        var lastLogin = await database.Users
            .Where(user => user.Vid == row.Vid)
            .Select(user => user.LastLoginAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (lastLogin is null || lastLogin.Value < now.AddDays(-SignedInWithinDays))
        {
            return (null, TokenRefusal.SignInAgain);
        }

        if (row.LastUsedAt is null || now - row.LastUsedAt.Value >= LastUseResolution)
        {
            row.LastUsedAt = now;
            await database.SaveChangesAsync(cancellationToken);
        }

        return (row, null);
    }

    /// <summary>SHA-256 in lowercase hex. A token has 256 random bits: a salt or a slow hash would protect nothing.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
