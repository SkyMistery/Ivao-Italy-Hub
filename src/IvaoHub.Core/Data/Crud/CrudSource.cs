using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Data.Crud;

/// <summary>
/// The one way to read a table as the back office reads it: with the visibility filter switched
/// off, because the staff has to see drafts and rows of other departments in order to be told they
/// may not touch them. What actually decides is the department filter and the policies, right
/// after (design M0 section 3.5).
/// <para>It exists as a named thing so that a service which legitimately needs a draft — the
/// publication service is the first — asks for it here instead of writing
/// <c>IgnoreQueryFilters</c> of its own. The architecture test allows that call in this folder
/// only, and this is how the folder stays the only place.</para>
/// </summary>
public static class CrudSource
{
    /// <summary>Every row of the set, whoever is logged in.</summary>
    public static IQueryable<TEntity> BackOffice<TEntity>(DbContext database)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(database);
        return database.Set<TEntity>().IgnoreQueryFilters();
    }
}
