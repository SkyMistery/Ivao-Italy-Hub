namespace IvaoHub.Core.Auth;

/// <summary>
/// A write the current user is not allowed to make. Thrown by the save changes interceptor, which
/// is the last net under the policies: an endpoint that forgot to authorize still cannot write
/// into the department of somebody else. The endpoint layer maps it to 403.
/// </summary>
public sealed class ForbiddenDomainException : Exception
{
    public ForbiddenDomainException()
        : this("The current user is not allowed to perform this write.")
    {
    }

    public ForbiddenDomainException(string message)
        : base(message)
    {
    }

    public ForbiddenDomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The permission that was missing, for example <c>Links.Edit</c>.</summary>
    public string? Permission { get; init; }
}
