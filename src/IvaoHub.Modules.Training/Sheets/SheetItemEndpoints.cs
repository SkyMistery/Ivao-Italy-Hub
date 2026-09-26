using FluentValidation;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Training.Sheets;

/// <summary>An item as the form loads it.</summary>
public sealed record SheetItemDto(
    long Id,
    Department OwnerDepartment,
    RatingKind Kind,
    int Rating,
    SheetSection Section,
    Localized<string> Title,
    int Sort,
    bool IsActive,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>
/// An item as the list shows it, with the short name of its rating, which the core's vocabulary gives: the module writes no
/// name of a rating of its own (design M3 §1.7).
/// </summary>
public sealed record SheetItemListDto(
    long Id,
    RatingKind Kind,
    int Rating,
    string? RatingShortName,
    SheetSection Section,
    Localized<string> Title,
    int Sort,
    bool IsActive,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>What a client may set on an item. Its department is the module's base department, which the payload does not carry.</summary>
public sealed record SheetItemWriteDto(
    RatingKind Kind,
    int Rating,
    SheetSection Section,
    Localized<string> Title,
    int Sort,
    bool IsActive,
    DateTime RowVersion);

/// <summary>
/// The rules of an item, against the vocabulary of ratings the host registers (a test builds its own): a rating the core gives
/// a practical training, on the ladder the item says; a title in every language of the division; a place in the sheet.
/// Messages are i18n keys.
/// </summary>
public sealed class SheetItemWriteDtoValidator : AbstractValidator<SheetItemWriteDto>
{
    /// <summary>A sheet is a page of items; a thousand places are room for any of them.</summary>
    public const int MaxSort = 999;

    public SheetItemWriteDtoValidator(RatingVocabulary vocabulary, IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(division);

        RuleFor(item => item.Kind).IsInEnum().WithMessage("errors.required");
        RuleFor(item => item.Rating)
            .Must((item, rating) => vocabulary.Find(item.Kind, rating) is { HasPracticalTraining: true })
            .WithMessage("training:errors.ratingNotTrained");
        RuleFor(item => item.Section).IsInEnum().WithMessage("errors.required");
        RuleFor(item => item.Title).Required(division.Value);
        RuleFor(item => item.Sort).InclusiveBetween(0, MaxSort).WithMessage("errors.number.range");
    }
}

/// <summary>Items to and from their payloads: by hand, because the list adds what the core's vocabulary says of the rating.</summary>
internal static class SheetItemMapper
{
    public static SheetItemDto ToDto(SheetItem item) => new(
        item.Id,
        item.OwnerDepartment,
        item.Kind,
        item.Rating,
        item.Section,
        item.Title,
        item.Sort,
        item.IsActive,
        item.UpdatedAt,
        item.RowVersion);

    public static SheetItemListDto ToList(SheetItem item, RatingVocabulary vocabulary) => new(
        item.Id,
        item.Kind,
        item.Rating,
        vocabulary.Find(item.Kind, item.Rating)?.ShortName,
        item.Section,
        item.Title,
        item.Sort,
        item.IsActive,
        item.UpdatedAt,
        item.RowVersion);

    /// <summary>Everything the payload says, and the version the caller edited, so a stale form is answered 409.</summary>
    public static void Apply(SheetItemWriteDto payload, SheetItem item)
    {
        item.Kind = payload.Kind;
        item.Rating = payload.Rating;
        item.Section = payload.Section;
        item.Title = payload.Title;
        item.Sort = payload.Sort;
        item.IsActive = payload.IsActive;
        item.RowVersion = payload.RowVersion;
    }
}

/// <summary>
/// The items of the evaluation sheet in the back office (design M3 §1.4, §4.2): a resource of the CRUD engine behind the screen
/// <c>/staff/training/sheets</c>, read and written with <c>Training.ManageSheets</c> on the base department of the module. The
/// write guard asks <c>Training.Edit</c> of every row of the staff as well (§3.1), which the coordinator and the assistant hold
/// with it (§3.2). <c>filter[kind]</c> and <c>filter[rating]</c> narrow the list to the sheet of one rating, in its order.
/// </summary>
public static class SheetItemEndpoints
{
    public const string Pattern = "/api/training/sheet-items";

    public static IEndpointRouteBuilder MapSheetItemEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var vocabulary = app.ServiceProvider.GetRequiredService<RatingVocabulary>();

        app.MapCrud<SheetItem, SheetItemListDto, SheetItemDto, SheetItemWriteDto>(Pattern, options =>
        {
            options.PermissionArea = TrainingPermissions.Area;
            options.Name = "TrainingSheetItems";
            options.ReadPolicy = TrainingPermissions.ManageSheets;
            options.WritePolicy = TrainingPermissions.ManageSheets;
            options.ContextType = typeof(TrainingDbContext);

            options.DefaultOrder = item => item.Sort;
            options.Sortable.Add(nameof(SheetItem.Sort));
            options.Sortable.Add(nameof(SheetItem.Rating));
            options.Sortable.Add(nameof(SheetItem.UpdatedAt));
            options.Filterable.Add(nameof(SheetItem.Kind));
            options.Filterable.Add(nameof(SheetItem.Rating));
            options.SearchFields.Add(item => item.Title);

            options.ToList = item => SheetItemMapper.ToList(item, vocabulary);
            options.ToDetail = SheetItemMapper.ToDto;
            options.Apply = SheetItemMapper.Apply;

            // An item a report marks is switched off, never deleted (§1.4): the report keeps its copy of it either way, and
            // the sheet keeps its history.
            options.Delete = async (item, services, cancellationToken) =>
            {
                if (await services.GetRequiredService<ISheetItemReports>().AnyAsync(item.Id, cancellationToken))
                {
                    throw new DomainRefusalException("id", "training:errors.sheetItemUsed");
                }

                services.GetRequiredService<TrainingDbContext>().SheetItems.Remove(item);
            };
        });

        return app;
    }
}
