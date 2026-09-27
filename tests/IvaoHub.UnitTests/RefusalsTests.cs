using FluentValidation.Results;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The refusals a verb gathers field by field when no validator covers it (note 2026-09-27-i-rifiuti-di-un-form-nel-nucleo).
/// They reach the form in the one shape a validator's do, so these tests pin that shape: the keys of a field each once and
/// in order, the languages next to a translated field, and the same 400 as <see cref="CrudProblems"/> gives a validator.
/// </summary>
public sealed class RefusalsTests
{
    private static LocaleCatalog Catalog() => new(
        HubPaths.Resolve(AppContext.BaseDirectory),
        Options.Create(new DivisionOptions
        {
            Code = "XX",
            CountryId = "XX",
            Domain = "hub.example",
            Timezone = "UTC",
            Locales = ["en"],
            DefaultLocale = "en",
        }));

    [Fact]
    public void NothingRefusedIsEmpty()
    {
        var refusals = new Refusals();

        Assert.True(refusals.IsEmpty);
        Assert.Empty(refusals.Errors);
        Assert.Empty(refusals.MissingLocales);

        refusals.Add("slug", "errors.required");

        Assert.False(refusals.IsEmpty);
    }

    [Fact]
    public void AFieldCarriesEveryKeyItWasRefusedWithEachOnceInOrder()
    {
        var refusals = new Refusals()
            .Add("kind", "module:errors.kindLocked")
            .Add("closeAt", "errors.required")
            .Add("kind", "module:errors.hasChildren")
            .Add("kind", "module:errors.kindLocked");

        Assert.Equal(["kind", "closeAt"], refusals.Errors.Keys);
        Assert.Equal(["module:errors.kindLocked", "module:errors.hasChildren"], refusals.Errors["kind"]);
        Assert.Equal(["errors.required"], refusals.Errors["closeAt"]);

        // A field is named as the API spells it: another spelling is another field.
        refusals.Add("CloseAt", "errors.required");
        Assert.Equal(3, refusals.Errors.Count);
    }

    [Fact]
    public void WhatWasReadDoesNotMoveUnderTheReader()
    {
        var refusals = new Refusals().Add("slug", "errors.required");
        var errors = refusals.Errors;

        refusals.Add("slug", "module:errors.slugTaken").Add("title", "errors.required");

        Assert.Equal(["errors.required"], errors["slug"]);
        Assert.Single(errors);
    }

    [Fact]
    public void AMissingTranslationIsRefusedWithTheValidatorsKeyAndItsLanguages()
    {
        var refusals = new Refusals()
            .Missing("title", ["it"])
            .Missing("title", ["en", "IT"])
            .Missing("briefing.sections[0].blocks[0].props.text", ["en"]);

        Assert.False(refusals.IsEmpty);
        Assert.Equal([LocalizedRules.MissingMessageKey], refusals.Errors["title"]);
        Assert.Equal(["it", "en"], refusals.MissingLocales["title"]);
        Assert.Equal(["en"], refusals.MissingLocales["briefing.sections[0].blocks[0].props.text"]);

        // A field refused for another reason carries no languages.
        refusals.Add("slug", "errors.required");
        Assert.False(refusals.MissingLocales.ContainsKey("slug"));
    }

    [Fact]
    public void TheAnswerIsTheOneAValidatorGets()
    {
        var catalog = Catalog();
        var validator = new ValidationResult(
        [
            new ValidationFailure("Title", LocalizedRules.MissingMessageKey) { CustomState = new LocalizedMissing(["it"]) },
            new ValidationFailure("Slug", "errors.required"),
        ]);
        var refusals = new Refusals().Missing("title", ["it"]).Add("slug", "errors.required");

        var expected = Details(CrudProblems.Validation(validator, catalog, "en"));
        var actual = Details(CrudProblems.Validation(refusals, catalog, "en"));

        Assert.Equal(StatusCodes.Status400BadRequest, actual.Status);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Errors.Keys, actual.Errors.Keys);
        Assert.All(expected.Errors, entry => Assert.Equal(entry.Value, actual.Errors[entry.Key]));

        var expectedLocalized = Localized(expected);
        var actualLocalized = Localized(actual);
        Assert.Equal(expectedLocalized.Keys, actualLocalized.Keys);
        Assert.Equal(expectedLocalized["title"], actualLocalized["title"]);
    }

    [Fact]
    public void RefusalsWithNoLanguageMissingCarryNoLanguages()
    {
        var details = Details(CrudProblems.Validation(new Refusals().Add("slug", "errors.required"), Catalog(), "en"));

        Assert.Equal(["errors.required"], details.Errors["slug"]);
        Assert.False(details.Extensions.ContainsKey(CrudProblems.LocalizedExtension));
    }

    private static HttpValidationProblemDetails Details(IResult result) =>
        Assert.IsType<HttpValidationProblemDetails>(Assert.IsType<ProblemHttpResult>(result).ProblemDetails);

    private static IReadOnlyDictionary<string, string[]> Localized(HttpValidationProblemDetails details) =>
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(details.Extensions[CrudProblems.LocalizedExtension]);
}
