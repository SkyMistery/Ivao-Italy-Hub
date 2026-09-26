using IvaoHub.Core.Ivao;

namespace IvaoHub.Modules.Training.Requests;

/// <summary>
/// Whether a member passed the theory exam of a rating, which comes before its training (design M3 §2.2; note
/// 2026-09-25-il-teorico-lo-dichiara-il-trainee). The network does not say it, so today the trainee does: the request asks them,
/// and whoever approves checks it where the exam is taken (A7). The day the network says it, another source answers here, the
/// question goes away, and nothing else of the request changes.
/// <para>It lives in the module (the note, §2 point 3): reading the outcomes from the network would be the core's, which would
/// hand them to the module with a note of that phase.</para>
/// </summary>
public interface ITheoryExamSource
{
    /// <summary>Whether the request has to ask the trainee: true for as long as the source cannot know by itself.</summary>
    bool AsksTheTrainee { get; }

    /// <summary>
    /// Whether <paramref name="vid"/> passed the theory exam of <paramref name="rating"/>. <paramref name="declared"/> is the
    /// trainee's own answer, which a source that knows does not need; null, when the source asks and the trainee gave none.
    /// </summary>
    Task<bool?> HasPassedAsync(int vid, Rating rating, bool? declared, CancellationToken cancellationToken = default);
}

/// <summary>The source of today: the trainee's word, checked by whoever approves (§12 n.15).</summary>
internal sealed class TraineeDeclaration : ITheoryExamSource
{
    public bool AsksTheTrainee => true;

    public Task<bool?> HasPassedAsync(int vid, Rating rating, bool? declared, CancellationToken cancellationToken = default) =>
        Task.FromResult(declared);
}
