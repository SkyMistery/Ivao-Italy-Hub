using System.Globalization;
using System.Linq.Expressions;
using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Core.Data.Crud;

/// <summary>
/// The one CRUD engine of the hub. A resource of the back office is a configuration object, not a
/// controller: list, read, create, update and delete are generated here, with paging, sorting,
/// searching, the department filter, the policies, validation and optimistic concurrency all
/// decided once (plan section 16.6, design M0 section 3.9).
/// <para>Two modes, one body of code with one branch. <b>Departmental</b>, when the entity is
/// <see cref="IOwnedByDepartment"/>: the list is narrowed to the departments of the user and every
/// single row is authorised against its owner. <b>Global</b>, when it is not — user grants, the
/// audit log: a single global policy and no row level check, because there is no department to
/// compare against.</para>
/// <para>This is also the only file allowed to call <c>IgnoreQueryFilters</c>: the visibility
/// filter hides drafts and other departments from everybody, staff included, so the back office
/// has to switch it off and re-filter — and a test of the architecture makes sure nowhere else
/// does.</para>
/// </summary>
public static class MapCrudExtensions
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public static RouteGroupBuilder MapCrud<TEntity, TListDto, TDetailDto, TWriteDto>(
        this IEndpointRouteBuilder app,
        string pattern,
        Action<CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto>> configure)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto>();
        configure(options);
        Verify(options);

        var group = app.MapGroup(pattern).WithTags(options.PermissionArea);

        // The handlers are declared as Delegate so that the route builder binds them as route
        // handlers, with their return value written to the response, rather than as bare request
        // delegates that would discard it.
        Delegate list = (HttpContext http, [AsParameters] CrudListRequest request) =>
            ListAsync(http, request, options);
        Delegate read = (HttpContext http, string id) => GetAsync(http, id, options);

        group.MapGet("/", list)
            .WithName($"{options.PermissionArea}List")
            .Produces<PagedResult<TListDto>>()
            .RequireAuthorization(options.EffectiveReadPolicy);

        group.MapGet("/{id}", read)
            .WithName($"{options.PermissionArea}Get")
            .Produces<TDetailDto>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(options.EffectiveReadPolicy);

        if (options.ReadOnly)
        {
            return group;
        }

        Delegate create = (HttpContext http, TWriteDto body) => CreateAsync(http, body, options);
        Delegate update = (HttpContext http, string id, TWriteDto body) => UpdateAsync(http, id, body, options);
        Delegate remove = (HttpContext http, string id) => DeleteAsync(http, id, options);

        group.MapPost("/", create)
            .WithName($"{options.PermissionArea}Create")
            .Produces<TDetailDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(options.EffectiveWritePolicy);

        group.MapPut("/{id}", update)
            .WithName($"{options.PermissionArea}Update")
            .Produces<TDetailDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(options.EffectiveWritePolicy);

        if (options.AllowDelete)
        {
            group.MapDelete("/{id}", remove)
                .WithName($"{options.PermissionArea}Delete")
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status404NotFound)
                .RequireAuthorization(options.EffectiveWritePolicy);
        }

        return group;
    }

    private static void Verify<TEntity, TListDto, TDetailDto, TWriteDto>(
        CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto> options)
        where TEntity : class
    {
        if (string.IsNullOrWhiteSpace(options.PermissionArea)
            && (options.ReadPolicy is null || options.WritePolicy is null))
        {
            throw new InvalidOperationException(
                $"MapCrud for {typeof(TEntity).Name} needs a PermissionArea, or both ReadPolicy and WritePolicy.");
        }

        if (options.ToList is null || options.ToDetail is null)
        {
            throw new InvalidOperationException($"MapCrud for {typeof(TEntity).Name} needs ToList and ToDetail.");
        }

        if (!options.ReadOnly && options.Apply is null)
        {
            throw new InvalidOperationException($"MapCrud for {typeof(TEntity).Name} needs Apply to accept writes.");
        }

        var undeclared = options.DefaultFilters.Keys
            .Where(name => !options.Filterable.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (undeclared.Length > 0)
        {
            // A default filter a caller cannot override is a list that lies quietly; saying so at
            // start up is cheaper than finding out from an empty table.
            throw new InvalidOperationException(
                $"MapCrud for {typeof(TEntity).Name} defaults filters that are not filterable: "
                + string.Join(", ", undeclared) + ".");
        }
    }

    // ---- the five endpoints -------------------------------------------------------------------

    private static async Task<IResult> ListAsync<TEntity, TListDto, TDetailDto, TWriteDto>(
        HttpContext http,
        CrudListRequest request,
        CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto> options)
        where TEntity : class
    {
        var scope = CrudScope<TEntity>.From(http, options.ContextType);

        var query = Source(scope.Database, options);

        if (!TryNarrowToDepartments(scope.CurrentUser, ref query, out var forbidden))
        {
            return forbidden!;
        }

        if (!TryApplyFilters(http.Request.Query, options, ref query, out var badFilter))
        {
            return badFilter!;
        }

        query = ApplySearch(request.Q, scope.CurrentUser.Locale, options, query);

        var total = await query.CountAsync(http.RequestAborted);

        if (!TryApplyOrder(request, options, ref query, out var badSort))
        {
            return badSort!;
        }

        var (page, pageSize) = ReadPaging(request);

        var rows = await query
            .AsNoTracking()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(http.RequestAborted);

        return Results.Ok(new PagedResult<TListDto>(
            [.. rows.Select(options.ToList!)],
            page,
            pageSize,
            total));
    }

    private static async Task<IResult> GetAsync<TEntity, TListDto, TDetailDto, TWriteDto>(
        HttpContext http,
        string id,
        CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto> options)
        where TEntity : class
    {
        var scope = CrudScope<TEntity>.From(http, options.ContextType);

        var entity = await FindAsync(scope, options, id, tracked: false, http.RequestAborted);
        if (entity is null)
        {
            return NotFound(scope);
        }

        if (await Denies(scope, entity, options.EffectiveReadPolicy))
        {
            // A row of another department is not readable, and saying "forbidden" rather than
            // "missing" is fine: the staff already knows the other departments exist.
            return Forbidden(scope);
        }

        return Results.Ok(options.ToDetail!(entity));
    }

    private static async Task<IResult> CreateAsync<TEntity, TListDto, TDetailDto, TWriteDto>(
        HttpContext http,
        TWriteDto body,
        CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto> options)
        where TEntity : class
    {
        var scope = CrudScope<TEntity>.From(http, options.ContextType);

        if (await ValidateAsync(scope, options, body, http.RequestAborted) is { } invalid)
        {
            return invalid;
        }

        var entity = (TEntity)Activator.CreateInstance(typeof(TEntity), nonPublic: true)!;
        options.Apply!(body, entity);

        if (await DeniesWrite(scope, entity, options))
        {
            return Forbidden(scope);
        }

        scope.Database.Add(entity);
        await scope.Database.SaveChangesAsync(http.RequestAborted);

        var key = scope.Key.PropertyInfo is null
            ? scope.Database.Entry(entity).Property(scope.Key.Name).CurrentValue
            : scope.Key.PropertyInfo.GetValue(entity);

        return Results.Created(
            $"{http.Request.Path}/{Convert.ToString(key, CultureInfo.InvariantCulture)}",
            options.ToDetail!(entity));
    }

    private static async Task<IResult> UpdateAsync<TEntity, TListDto, TDetailDto, TWriteDto>(
        HttpContext http,
        string id,
        TWriteDto body,
        CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto> options)
        where TEntity : class
    {
        var scope = CrudScope<TEntity>.From(http, options.ContextType);

        var entity = await FindAsync(scope, options, id, tracked: true, http.RequestAborted);
        if (entity is null)
        {
            return NotFound(scope);
        }

        // Twice on purpose: once on the row as it is stored, so nobody edits what is not theirs,
        // and once on the row as it would become, so nobody hands a row to another department.
        if (await DeniesWrite(scope, entity, options))
        {
            return Forbidden(scope);
        }

        if (await ValidateAsync(scope, options, body, http.RequestAborted) is { } invalid)
        {
            return invalid;
        }

        options.Apply!(body, entity);

        if (await DeniesWrite(scope, entity, options))
        {
            return Forbidden(scope);
        }

        CarryConcurrencyToken(scope.Database.Entry(entity));

        await scope.Database.SaveChangesAsync(http.RequestAborted);

        return Results.Ok(options.ToDetail!(entity));
    }

    private static async Task<IResult> DeleteAsync<TEntity, TListDto, TDetailDto, TWriteDto>(
        HttpContext http,
        string id,
        CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto> options)
        where TEntity : class
    {
        var scope = CrudScope<TEntity>.From(http, options.ContextType);

        var entity = await FindAsync(scope, options, id, tracked: true, http.RequestAborted);
        if (entity is null)
        {
            return NotFound(scope);
        }

        if (await DeniesWrite(scope, entity, options))
        {
            return Forbidden(scope);
        }

        scope.Database.Remove(entity);
        await scope.Database.SaveChangesAsync(http.RequestAborted);

        return Results.NoContent();
    }

    // ---- the pieces the endpoints share -------------------------------------------------------

    private static IQueryable<TEntity> Source<TEntity, TListDto, TDetailDto, TWriteDto>(
        DbContext database,
        CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto> options)
        where TEntity : class =>
        options.Source is null ? CrudSource.BackOffice<TEntity>(database) : options.Source(database);

    /// <summary>
    /// Departmental mode: the list only ever holds rows of the departments the user belongs to.
    /// Whoever reaches every department sees the lot; a member who somehow holds the permission but
    /// belongs to no department sees nothing, and is told so rather than shown an empty list.
    /// </summary>
    private static bool TryNarrowToDepartments<TEntity>(
        ICurrentUser currentUser,
        ref IQueryable<TEntity> query,
        out IResult? forbidden)
        where TEntity : class
    {
        forbidden = null;

        if (!typeof(IOwnedByDepartment).IsAssignableFrom(typeof(TEntity)) || currentUser.HasAllDepartments)
        {
            return true;
        }

        if (currentUser.Departments.Count == 0)
        {
            forbidden = Results.StatusCode(StatusCodes.Status403Forbidden);
            return false;
        }

        var departments = currentUser.Departments.ToList();
        var entity = Expression.Parameter(typeof(TEntity), "entity");
        var owner = Expression.Property(entity, nameof(IOwnedByDepartment.OwnerDepartment));
        var contains = Expression.Call(
            Expression.Constant(departments),
            typeof(List<Department>).GetMethod(nameof(List<Department>.Contains), [typeof(Department)])!,
            owner);

        query = query.Where(Expression.Lambda<Func<TEntity, bool>>(contains, entity));
        return true;
    }

    private static bool TryApplyFilters<TEntity, TListDto, TDetailDto, TWriteDto>(
        IQueryCollection request,
        CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto> options,
        ref IQueryable<TEntity> query,
        out IResult? badRequest)
        where TEntity : class
    {
        badRequest = null;

        var asked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, values) in request)
        {
            if (!key.StartsWith("filter[", StringComparison.Ordinal) || !key.EndsWith(']'))
            {
                continue;
            }

            var name = key[7..^1];
            asked.Add(name);

            if (!TryApplyFilter(name, values.ToString(), options, ref query, out badRequest))
            {
                return false;
            }
        }

        // What the caller did not mention, the resource decides. `filter[isTemplate]=false` and no
        // filter at all mean the same thing here, and that is deliberate: the caller who wants the
        // templates asks for them.
        foreach (var (name, value) in options.DefaultFilters)
        {
            if (asked.Contains(name))
            {
                continue;
            }

            if (!TryApplyFilter(name, value, options, ref query, out badRequest))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryApplyFilter<TEntity, TListDto, TDetailDto, TWriteDto>(
        string name,
        string raw,
        CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto> options,
        ref IQueryable<TEntity> query,
        out IResult? badRequest)
        where TEntity : class
    {
        badRequest = null;

        var declared = options.Filterable.FirstOrDefault(
            allowed => string.Equals(allowed, name, StringComparison.OrdinalIgnoreCase));

        var property = declared is null ? null : typeof(TEntity).GetProperty(declared);
        if (property is null)
        {
            // An allow list, not a free query language: an unknown filter is a mistake in the
            // caller, and a silent one would quietly return the whole table.
            badRequest = Results.BadRequest(new { filter = name });
            return false;
        }

        if (!TryConvert(raw, property.PropertyType, out var value))
        {
            badRequest = Results.BadRequest(new { filter = name });
            return false;
        }

        var entity = Expression.Parameter(typeof(TEntity), "entity");
        var comparison = Expression.Equal(
            Expression.Property(entity, property),
            Expression.Constant(value, property.PropertyType));

        query = query.Where(Expression.Lambda<Func<TEntity, bool>>(comparison, entity));
        return true;
    }

    /// <summary>
    /// Free text over the declared columns, translated ones read in the language of the reader:
    /// a coordinator searching in Italian must not have to guess the English title.
    /// </summary>
    private static IQueryable<TEntity> ApplySearch<TEntity, TListDto, TDetailDto, TWriteDto>(
        string? term,
        string locale,
        CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto> options,
        IQueryable<TEntity> query)
        where TEntity : class
    {
        var fields = options.SearchFields.ToArray();
        if (string.IsNullOrWhiteSpace(term) || fields.Length == 0)
        {
            return query;
        }

        var pattern = $"%{Escape(term.Trim())}%";
        var path = LocalizedQuery.PathFor(locale);
        var entity = Expression.Parameter(typeof(TEntity), "entity");
        var like = typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like),
            [typeof(DbFunctions), typeof(string), typeof(string)])!;

        Expression? any = null;
        foreach (var field in fields)
        {
            var selector = field.Selector(path);
            var text = new ParameterSwap(selector.Parameters[0], entity).Visit(selector.Body)!;
            var match = Expression.Call(
                like,
                Expression.Constant(EF.Functions),
                text,
                Expression.Constant(pattern, typeof(string)));

            any = any is null ? match : Expression.OrElse(any, match);
        }

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(any!, entity));
    }

    private static bool TryApplyOrder<TEntity, TListDto, TDetailDto, TWriteDto>(
        CrudListRequest request,
        CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto> options,
        ref IQueryable<TEntity> query,
        out IResult? badRequest)
        where TEntity : class
    {
        badRequest = null;

        var descending = string.Equals(request.Dir, "desc", StringComparison.OrdinalIgnoreCase);
        var sort = request.Sort;

        if (string.IsNullOrWhiteSpace(sort))
        {
            if (options.DefaultOrder is not null)
            {
                query = descending ? query.OrderByDescending(options.DefaultOrder) : query.OrderBy(options.DefaultOrder);
            }

            return true;
        }

        var declared = options.Sortable.FirstOrDefault(
            allowed => string.Equals(allowed, sort, StringComparison.OrdinalIgnoreCase));

        var property = declared is null ? null : typeof(TEntity).GetProperty(declared);
        if (property is null)
        {
            badRequest = Results.BadRequest(new { sort });
            return false;
        }

        var entity = Expression.Parameter(typeof(TEntity), "entity");
        var selector = Expression.Lambda(Expression.Property(entity, property), entity);

        var method = typeof(Queryable)
            .GetMethods()
            .First(candidate => candidate.Name == (descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy))
                && candidate.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TEntity), property.PropertyType);

        query = (IQueryable<TEntity>)method.Invoke(null, [query, selector])!;
        return true;
    }

    /// <summary>
    /// A page is capped here rather than trusted: an unbounded page size is the easiest way to
    /// turn a list into a way of dumping a table.
    /// </summary>
    private static (int Page, int PageSize) ReadPaging(CrudListRequest request) => (
        request.Page is > 0 ? request.Page.Value : 1,
        request.PageSize is > 0 ? Math.Min(request.PageSize.Value, MaxPageSize) : DefaultPageSize);

    private static async Task<TEntity?> FindAsync<TEntity, TListDto, TDetailDto, TWriteDto>(
        CrudScope<TEntity> scope,
        CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto> options,
        string id,
        bool tracked,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        if (!TryConvert(id, scope.Key.ClrType, out var key))
        {
            return null;
        }

        var entity = Expression.Parameter(typeof(TEntity), "entity");
        var predicate = Expression.Lambda<Func<TEntity, bool>>(
            Expression.Equal(
                Expression.Property(entity, scope.Key.Name),
                Expression.Constant(key, scope.Key.ClrType)),
            entity);

        var query = Source(scope.Database, options);
        return await (tracked ? query : query.AsNoTracking()).FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <summary>
    /// Validation is the server's job and the client only shows the answer, so an endpoint never
    /// repeats a rule (plan section 16.6).
    /// </summary>
    private static async Task<IResult?> ValidateAsync<TEntity, TListDto, TDetailDto, TWriteDto>(
        CrudScope<TEntity> scope,
        CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto> options,
        TWriteDto body,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var validator = options.Validator ?? scope.Services.GetService<IValidator<TWriteDto>>();
        if (validator is null)
        {
            return null;
        }

        var result = await validator.ValidateAsync(body, cancellationToken);
        return result.IsValid ? null : CrudProblems.Validation(result, scope.Catalog, scope.CurrentUser.Locale);
    }

    /// <summary>
    /// The row level check. In departmental mode the resource is handed to the single authorization
    /// handler, which compares its owner with the user; in global mode there is no resource to
    /// compare and the policy on the endpoint has already had the last word.
    /// </summary>
    private static async Task<bool> Denies<TEntity>(CrudScope<TEntity> scope, TEntity entity, string policy)
        where TEntity : class
    {
        if (entity is not IOwnedByDepartment)
        {
            return false;
        }

        var result = await scope.Authorization.AuthorizeAsync(scope.Principal, entity, policy);
        return !result.Succeeded;
    }

    private static async Task<bool> DeniesWrite<TEntity, TListDto, TDetailDto, TWriteDto>(
        CrudScope<TEntity> scope,
        TEntity entity,
        CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto> options)
        where TEntity : class
    {
        if (await Denies(scope, entity, options.EffectiveWritePolicy))
        {
            return true;
        }

        if (options.ExtraWritePolicy?.Invoke(entity) is not { } extra)
        {
            return false;
        }

        // The extra policy is asked without the resource when the entity has no department, and
        // with it otherwise: it is the same handler answering either way.
        var result = entity is IOwnedByDepartment
            ? await scope.Authorization.AuthorizeAsync(scope.Principal, entity, extra)
            : await scope.Authorization.AuthorizeAsync(scope.Principal, extra);

        return !result.Succeeded;
    }

    /// <summary>
    /// Puts the version the caller edited into the <c>WHERE</c> of the update. A stale one matches
    /// no row, EF Core raises a concurrency failure and the exception handler turns it into 409
    /// (design M0 section 3.9). A payload that carries no version is taken to mean "the row as it
    /// is now", which is what a create-then-update in a script does.
    /// </summary>
    private static void CarryConcurrencyToken(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        foreach (var property in entry.Properties.Where(candidate => candidate.Metadata.IsConcurrencyToken))
        {
            var current = property.CurrentValue;
            if (current is null || Equals(current, Activator.CreateInstance(property.Metadata.ClrType)))
            {
                continue;
            }

            property.OriginalValue = current;
        }
    }

    private static IResult Forbidden<TEntity>(CrudScope<TEntity> scope)
        where TEntity : class =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: scope.Catalog.Resolve(scope.CurrentUser.Locale, CrudProblems.ForbiddenTitleKey));

    private static IResult NotFound<TEntity>(CrudScope<TEntity> scope)
        where TEntity : class =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: scope.Catalog.Resolve(scope.CurrentUser.Locale, CrudProblems.NotFoundTitleKey));

    private static string Escape(string term) => term
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private static bool TryConvert(string raw, Type target, out object? value)
    {
        var type = Nullable.GetUnderlyingType(target) ?? target;

        try
        {
            value = type.IsEnum
                ? Enum.Parse(type, raw, ignoreCase: true)
                : Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or ArgumentException or OverflowException)
        {
            value = null;
            return false;
        }
    }

    /// <summary>Rewrites a lambda body so several selectors can share one parameter.</summary>
    private sealed class ParameterSwap(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }

    /// <summary>Everything one request needs, resolved once instead of five times.</summary>
    private sealed class CrudScope<TEntity>
        where TEntity : class
    {
        private CrudScope(HttpContext http, DbContext database)
        {
            Services = http.RequestServices;
            Principal = http.User;
            Database = database;
            CurrentUser = Services.GetRequiredService<ICurrentUser>();
            Authorization = Services.GetRequiredService<IAuthorizationService>();
            Catalog = Services.GetRequiredService<LocaleCatalog>();

            var key = database.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey()
                ?? throw new InvalidOperationException($"{typeof(TEntity).Name} has no primary key.");

            if (key.Properties.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{typeof(TEntity).Name} has a composite key; the CRUD engine addresses a row by a single id.");
            }

            Key = key.Properties[0];
        }

        public IServiceProvider Services { get; }

        public System.Security.Claims.ClaimsPrincipal Principal { get; }

        public DbContext Database { get; }

        public ICurrentUser CurrentUser { get; }

        public IAuthorizationService Authorization { get; }

        public LocaleCatalog Catalog { get; }

        public IProperty Key { get; }

        public static CrudScope<TEntity> From(HttpContext http, Type contextType) =>
            new(http, (DbContext)http.RequestServices.GetRequiredService(contextType));
    }
}
