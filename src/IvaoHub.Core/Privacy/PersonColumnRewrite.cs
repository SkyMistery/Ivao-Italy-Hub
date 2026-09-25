using System.Linq.Expressions;
using System.Reflection;
using IvaoHub.Core.Data.Crud;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IvaoHub.Core.Privacy;

/// <summary>
/// Turns a VID into the pseudonym in every column of a context that names a person (<see cref="PersonColumns"/>): the core's
/// half of an erasure, run in the context of the core and in every context of every module, so that a module writes none of
/// it. Read from the model of the context, so a table added tomorrow is covered the day it is added.
/// <para>Only the tables the context owns: a module context maps some of the core's (the projections, the threads, the
/// audit log), and those are rewritten once, by the core's context. The audit log is left alone here: it is rewritten by
/// <see cref="AuditRedaction"/>, which knows its JSON. Keys are left alone too: a row whose key is a VID is a row about the
/// person, and the erasure deletes it before it gets here.</para>
/// <para>It changes tracked rows and does not save: the caller saves, in erasure mode, inside its transaction.</para>
/// </summary>
internal static class PersonColumnRewrite
{
    private static readonly MethodInfo RowsMethod = typeof(PersonColumnRewrite)
        .GetMethod(nameof(RowsAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Rewrites the rows, except those a module keeps (<see cref="ErasureRequest.Keep"/>), and returns how many it changed.</summary>
    public static async Task<int> RewriteAsync(DbContext context, ErasureRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var rows = 0;
        foreach (var entity in context.Model.GetEntityTypes().Where(entity => PersonColumns.RewrittenBy(context, entity)))
        {
            var columns = entity.GetProperties()
                .Where(property => PersonColumns.IsVid(property) && !property.IsPrimaryKey())
                .ToArray();

            if (columns.Length == 0)
            {
                continue;
            }

            var task = (Task<int>)RowsMethod.MakeGenericMethod(entity.ClrType)
                .Invoke(null, [context, columns, request, cancellationToken])!;
            rows += await task;
        }

        return rows;
    }

    private static async Task<int> RowsAsync<TEntity>(
        DbContext context,
        IProperty[] columns,
        ErasureRequest request,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        // row => EF.Property<int>(row, "CreatedBy") == vid || EF.Property<int?>(row, "HandledBy") == vid || …
        var row = Expression.Parameter(typeof(TEntity), "row");
        var holds = columns
            .Select(column => (Expression)Expression.Equal(
                Expression.Call(typeof(EF), nameof(EF.Property), [column.ClrType], row, Expression.Constant(column.Name)),
                Expression.Constant(request.Vid, column.ClrType)))
            .Aggregate(Expression.OrElse);

        // Every row, whoever is signed in: the back office's way of reading, and the one the architecture test allows.
        var rows = await CrudSource.BackOffice<TEntity>(context)
            .AsTracking()
            .Where(Expression.Lambda<Func<TEntity, bool>>(holds, row))
            .ToListAsync(cancellationToken);

        var changed = 0;
        foreach (var entry in rows.Where(entity => !request.IsKept(entity)).Select(context.Entry))
        {
            changed++;
            foreach (var column in columns)
            {
                var property = entry.Property(column.Name);
                if (property.CurrentValue is int value && value == request.Vid)
                {
                    property.CurrentValue = request.Pseudonym;
                }
            }
        }

        return changed;
    }
}
