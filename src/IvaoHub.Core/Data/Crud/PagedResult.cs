using Microsoft.AspNetCore.Mvc;

namespace IvaoHub.Core.Data.Crud;

/// <summary>
/// One page of a list, in the shape every list of the hub answers with. Paging is decided in the
/// CRUD engine and nowhere else, so a screen never invents its own envelope (design M0 section 3.9).
/// </summary>
/// <param name="Items">The rows of this page, already mapped to their list shape.</param>
/// <param name="Page">One based page number.</param>
/// <param name="PageSize">How many rows a page holds.</param>
/// <param name="Total">How many rows the whole filtered set holds.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

/// <summary>The page a request asks for, read the same way by every list of the core.</summary>
public static class Paging
{
    /// <summary>
    /// Page 1 for anything below it, the default size for none, the size capped at <paramref name="maxSize"/>; and a page no
    /// further than the rows an <see cref="int"/> can skip, so that <c>(page - 1) * size</c> never overflows into a 500 (issue
    /// #319, item 9). A page that far is empty either way.
    /// </summary>
    public static (int Page, int PageSize) Read(int? page, int? pageSize, int defaultSize, int maxSize)
    {
        var size = pageSize is > 0 ? Math.Min(pageSize.Value, maxSize) : defaultSize;
        var number = page is > 0 ? Math.Min(page.Value, int.MaxValue / size) : 1;
        return (number, size);
    }

    /// <summary>How many rows come before the page.</summary>
    public static int Skip(int page, int pageSize) => (page - 1) * pageSize;
}

/// <summary>
/// What every list of the hub accepts. Declared as a type rather than read out of the raw query
/// string so that it reaches the OpenAPI document, and from there the generated client and the
/// typed search parameters of the router (design M0 sections 3.9 and 7.3).
/// <para><c>filter[name]=value</c> is deliberately not here: its names are the properties of the
/// entity, so it cannot be one type, and the engine reads it from the query string against the
/// allow list of the resource.</para>
/// </summary>
public sealed record CrudListRequest
{
    /// <summary>One based; anything below 1 means the first page.</summary>
    [FromQuery(Name = "page")]
    public int? Page { get; init; }

    /// <summary>Capped by the engine, so a caller cannot ask for the whole table.</summary>
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }

    /// <summary>A property name the resource declared sortable.</summary>
    [FromQuery(Name = "sort")]
    public string? Sort { get; init; }

    /// <summary><c>asc</c> (the default) or <c>desc</c>.</summary>
    [FromQuery(Name = "dir")]
    public string? Dir { get; init; }

    /// <summary>Free text, searched in the columns the resource declared.</summary>
    [FromQuery(Name = "q")]
    public string? Q { get; init; }
}
