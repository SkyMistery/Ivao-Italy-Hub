using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Modules.FlightOps.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.Threads;

/// <summary>
/// The block <c>flightops.openIssues</c> (design M2 §8.2; note 2026-09-23-contestazioni-chiarimenti-segnalazioni §3.4): what is
/// waiting for the tours' staff besides the queue — the issues on the legs still open, and the disputes and the clarifications
/// nobody has answered yet (<c>New</c> or <c>Read</c>). Three numbers, each a link: the list of the issues, the queue of the
/// disputes, the contacts of the department.
/// <para>Always live and answered for the person asking: an issue counts when the one handler lets them read it (<c>Tours.View</c>
/// on its tour), a thread when they read their department's contacts — the question the core's own dashboard asks.</para>
/// </summary>
public sealed class OpenIssuesProvider(
    FlightOpsDbContext database,
    HubDbContext hub,
    IAuthorizationService authorization,
    IHttpContextAccessor http,
    ICurrentUser currentUser) : IDataBlockProvider
{
    public const string BlockType = "flightops.openIssues";

    /// <summary>How many rows are read before the permissions narrow them: a dashboard counts, it does not list.</summary>
    private const int Window = 500;

    public string Key => BlockType;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken)
    {
        var result = new JsonObject { ["legIssues"] = 0, ["disputes"] = 0, ["clarifications"] = 0, ["department"] = null };
        if (!currentUser.IsAuthenticated || http.HttpContext is not { } request)
        {
            return result;
        }

        if (currentUser.HasAny(TourPermissions.View))
        {
            var open = await CrudSource.BackOffice<LegIssue>(database).AsNoTracking()
                .Where(issue => issue.Status == LegIssueStatus.Open)
                .OrderBy(issue => issue.CreatedAt)
                .Take(Window)
                .ToListAsync(cancellationToken);

            var readable = 0;
            foreach (var issue in open)
            {
                if ((await authorization.AuthorizeAsync(request.User, issue, TourPermissions.View)).Succeeded)
                {
                    readable++;
                }
            }

            result["legIssues"] = readable;
        }

        if (currentUser.HasAny(CorePermissions.ContactsView))
        {
            // A dispute is opened by a report of the tours; a clarification cites one of their objects — both carry the module.
            var cited = hub.ContactReferences.Where(reference => reference.SourceModule == FlightOpsModule.ModuleKey).Select(reference => reference.MessageId);
            var waiting = await CrudSource.BackOffice<ContactMessage>(hub).AsNoTracking()
                .Where(message => (message.Status == ContactStatus.New || message.Status == ContactStatus.Read)
                    && ((message.Kind == ContactKinds.Dispute && message.SourceModule == FlightOpsModule.ModuleKey)
                        || (message.Kind == ContactKinds.Clarification && cited.Contains(message.Id))))
                .OrderBy(message => message.CreatedAt)
                .Take(Window)
                .Select(message => new { message.Kind, message.OwnerDepartment })
                .ToListAsync(cancellationToken);

            var theirs = waiting.Where(message => currentUser.Has(CorePermissions.ContactsView, message.OwnerDepartment)).ToList();
            result["disputes"] = theirs.Count(message => message.Kind == ContactKinds.Dispute);
            result["clarifications"] = theirs.Count(message => message.Kind == ContactKinds.Clarification);

            // The contacts are a queue per department: the link goes to the one most of these are in.
            result["department"] = theirs
                .GroupBy(message => message.OwnerDepartment)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key.ToString().ToLowerInvariant())
                .FirstOrDefault();
        }

        return result;
    }
}
