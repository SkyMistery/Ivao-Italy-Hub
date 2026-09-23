namespace IvaoHub.Modules.FlightOps;

/// <summary>
/// The kinds of notification of the tours (design M2 §3.5, §4.2.2, §9), declared to the core through
/// <c>IModule.NotificationTypes</c>. The words are in the module's language file: the mail under <c>mail.{type}</c>, the label
/// of the profile under <c>notifications.{name}</c> of the <c>flightops</c> namespace.
/// </summary>
public static class FlightOpsNotifications
{
    /// <summary>A report of theirs was accepted. Its audience is the pilot.</summary>
    public const string PirepAccepted = "flightops.pirepAccepted";

    /// <summary>A report of theirs was sent back to be corrected. Its audience is the pilot.</summary>
    public const string PirepToModify = "flightops.pirepToModify";

    /// <summary>A report of theirs was rejected, with the rules broken. Its audience is the pilot.</summary>
    public const string PirepRejected = "flightops.pirepRejected";

    /// <summary>The daily digest of the queue (§4.2.2). Its audience is whoever may validate, each with their own tours.</summary>
    public const string ReviewDigest = "flightops.reviewDigest";

    /// <summary>A pilot reported a problem on a leg (§3.11, T14b). Its audience is the mailbox of the tour's department.</summary>
    public const string LegIssueReported = "flightops.legIssueReported";

    public static readonly IReadOnlyList<string> All = [PirepAccepted, PirepToModify, PirepRejected, ReviewDigest, LegIssueReported];
}
