namespace IvaoHub.Modules.Training;

/// <summary>
/// The kinds of notification of the training (design M3 §5.2), declared to the core through <c>IModule.NotificationTypes</c>.
/// The words are in the module's language file: the mail under <c>mail.{type}</c>, the label of the profile under
/// <c>notifications.{name}</c> of the <c>training</c> namespace. The others of §5.2 arrive with the phases that send them.
/// </summary>
public static class TrainingNotifications
{
    /// <summary>A request of theirs was received (§2.2): not a refusal of the hub, which the screen says instead. Its audience is the trainee.</summary>
    public const string RequestReceived = "training.requestReceived";

    public static readonly IReadOnlyList<string> All = [RequestReceived];
}
