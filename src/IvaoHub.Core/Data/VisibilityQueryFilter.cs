using System.Linq.Expressions;
using System.Reflection;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Data;

/// <summary>
/// The visibility rule, written once as a query filter and applied to every entity that is both
/// visible and owned by a department (design M0 section 3.5). A public read therefore cannot
/// return a row it should not, whatever the endpoint forgot: there is no "and remember to filter
/// by visibility" left to forget.
/// <para>The filter is an expression over scalars of the context, because that is what EF Core 9
/// can translate: it cannot call a service, but it can read a property of the context running the
/// query. Those properties read <c>ICurrentUser</c> <b>when the query runs</b>, never when the
/// context is built — a context can well exist before the cookie has been validated, and reading
/// early would freeze an anonymous answer into every query that instance later serves.</para>
/// </summary>
public static class VisibilityQueryFilter
{
    private static readonly MethodInfo ApplyMethod =
        typeof(VisibilityQueryFilter).GetMethod(nameof(Apply), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Adds the filter to every entity of the model that carries both a visibility and an owner
    /// department. An entity with only one of the two is not filtered: a visibility without an
    /// owner has no department to compare against.
    /// </summary>
    public static void ApplyToModel<TContext>(ModelBuilder modelBuilder, TContext context)
        where TContext : DbContext, IVisibilityScope
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToArray())
        {
            var clrType = entityType.ClrType;

            if (entityType.BaseType is not null
                || !typeof(IVisible).IsAssignableFrom(clrType)
                || !typeof(IOwnedByDepartment).IsAssignableFrom(clrType))
            {
                continue;
            }

            ApplyMethod.MakeGenericMethod(clrType).Invoke(null, [modelBuilder, context, context.GetType()]);
        }
    }

    private static void Apply<TEntity>(ModelBuilder modelBuilder, DbContext context, Type contextType)
        where TEntity : class
    {
        var entity = Expression.Parameter(typeof(TEntity), "entity");
        var visibility = Expression.Property(entity, nameof(IVisible.Visibility));

        // The context is a constant here and the current instance at query time: EF Core replaces
        // a reference to the context inside a query filter with the one running the query. Typed as
        // the context itself, the hub's or a module's (M2), for that replacement to recognise it.
        var self = Expression.Constant(context, contextType);
        var all = Expression.Property(self, nameof(IVisibilityScope.SeesEveryDepartment));
        var members = Expression.Property(self, nameof(IVisibilityScope.SeesMemberRows));
        var staff = Expression.Property(self, nameof(IVisibilityScope.SeesStaffRows));
        var ofTheirDepartments = OfTheirDepartments(entity, self);

        var body = Expression.OrElse(
            all,
            Expression.OrElse(
                Is(visibility, Visibility.Public),
                Expression.OrElse(
                    Expression.AndAlso(members, Is(visibility, Visibility.Members)),
                    Expression.OrElse(
                        Expression.AndAlso(staff, Is(visibility, Visibility.Staff)),
                        Expression.AndAlso(Is(visibility, Visibility.Department), ofTheirDepartments)))));

        if (typeof(IPublishable).IsAssignableFrom(typeof(TEntity)))
        {
            // EF Core 9 allows a single filter per entity, so the editorial state joins the same
            // expression. The back office reads with IgnoreQueryFilters, inside MapCrud only.
            body = Expression.AndAlso(
                body,
                Is(Expression.Property(entity, nameof(IPublishable.Status)), PublishStatus.Published));
        }

        modelBuilder.Entity<TEntity>().HasQueryFilter(Expression.Lambda<Func<TEntity, bool>>(body, entity));
    }

    /// <summary>
    /// "One of the departments of this row is one of the reader's." A row of one department asks
    /// whether its department is in the reader's list; a row in the care of several (M2) asks whether
    /// its mask and the reader's have a bit in common.
    /// </summary>
    private static Expression OfTheirDepartments(ParameterExpression entity, Expression scope)
    {
        if (DepartmentMask.IsStoredOn(entity.Type))
        {
            return Expression.NotEqual(
                Expression.And(
                    Expression.Property(entity, DepartmentMask.PropertyName),
                    Expression.Property(scope, nameof(IVisibilityScope.VisibleDepartmentMask))),
                Expression.Constant(0));
        }

        var departments = Expression.Property(scope, nameof(IVisibilityScope.VisibleDepartments));
        return Expression.Call(
            departments,
            ContainsOf(departments.Type),
            Expression.Property(entity, nameof(IOwnedByDepartment.OwnerDepartment)));
    }

    private static BinaryExpression Is(Expression property, object value) =>
        Expression.Equal(property, Expression.Constant(value, property.Type));

    private static MethodInfo ContainsOf(Type listType) =>
        listType.GetMethod(nameof(List<int>.Contains), [listType.GetGenericArguments()[0]])!;
}

/// <summary>
/// What the global query filter reads about the person running a query, as properties of the context
/// running it: EF Core can translate a property of the context, not a call to a service (design M0
/// section 3.5). The hub's context has them, and so does every module's (<see cref="ModuleDbContext"/>).
/// </summary>
public interface IVisibilityScope
{
    /// <summary>Director, web team and super administrators read every row, whoever owns it.</summary>
    bool SeesEveryDepartment { get; }

    /// <summary>Whether rows restricted to members are readable.</summary>
    bool SeesMemberRows { get; }

    /// <summary>Whether rows restricted to the staff are readable.</summary>
    bool SeesStaffRows { get; }

    /// <summary>The departments whose own rows are readable.</summary>
    List<Department> VisibleDepartments { get; }

    /// <summary>The same departments as a <see cref="DepartmentMask"/>, for rows in the care of several.</summary>
    int VisibleDepartmentMask { get; }
}
