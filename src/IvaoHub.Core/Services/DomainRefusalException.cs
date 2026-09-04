namespace IvaoHub.Core.Services;

/// <summary>
/// "This cannot be done, and here is why" — a rule of the domain refusing a request, carrying the
/// i18n key of the reason rather than a sentence. The sibling of
/// <c>ForbiddenDomainException</c>, which answers a different question: that one is "you may not",
/// this one is "nobody may, not like this".
/// <para>It exists so that a service can state a rule once and every caller answer with the same
/// shape a form already knows how to read — <c>errors[field] = ["some.i18n.key"]</c> — instead of
/// each endpoint deciding what an <see cref="InvalidOperationException"/> message means
/// (design M0 sections 3.9 and 7.5).</para>
/// </summary>
/// <param name="field">
/// The field of the payload the refusal is about, as the API spells it. Empty when it is about the
/// request as a whole.
/// </param>
/// <param name="messageKey">The i18n key of the reason, for example <c>errors.superadmin.lastOne</c>.</param>
public sealed class DomainRefusalException(string field, string messageKey)
    : InvalidOperationException($"{field}: {messageKey}")
{
    public string Field { get; } = field;

    public string MessageKey { get; } = messageKey;
}
