using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IvaoHub.Core.Data;

/// <summary>
/// Optimistic concurrency on MariaDB, written once. MariaDB has no <c>rowversion</c> type, so the
/// column is a <c>timestamp(6)</c> that the server bumps on every update; a stale value makes the
/// update affect no row, which the CRUD engine turns into a 409 (design M0 section 3.9).
/// </summary>
public static class RowVersionExtensions
{
    private const string ServerManaged = "CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)";

    public static EntityTypeBuilder<TEntity> HasRowVersion<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, DateTime>> property)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Property(property)
            .IsRowVersion()
            .HasColumnType("timestamp(6)")
            .HasDefaultValueSql(ServerManaged);

        return builder;
    }
}
