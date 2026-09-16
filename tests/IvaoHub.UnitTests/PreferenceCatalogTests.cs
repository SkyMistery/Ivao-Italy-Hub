using System.Text.Json;
using IvaoHub.Core.Preferences;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The catalogue of preferences (M2, T4b) refuses at start up what would otherwise be a quiet collision
/// between two modules, and a closed set of words accepts those words and nothing else.
/// </summary>
public sealed class PreferenceCatalogTests
{
    [Fact]
    public void AKeyIsNamedAfterItsModule()
    {
        var catalog = new PreferenceCatalog([("tours", [PreferenceDescriptor.OneOf("tours.order", "date")])]);

        Assert.True(catalog.TryGet("tours.order", out _));
        Assert.False(catalog.TryGet("tours.other", out _));

        Assert.Throws<InvalidOperationException>(() =>
            new PreferenceCatalog([("tours", [PreferenceDescriptor.OneOf("events.order", "date")])]));
        Assert.Throws<InvalidOperationException>(() =>
            new PreferenceCatalog([("tours", [PreferenceDescriptor.OneOf("tours.", "date")])]));
        Assert.Throws<InvalidOperationException>(() =>
            new PreferenceCatalog([("tours", [PreferenceDescriptor.OneOf("tours." + new string('x', 64), "date")])]));
    }

    [Fact]
    public void AKeyIsDeclaredOnce()
    {
        Assert.Throws<InvalidOperationException>(() => new PreferenceCatalog(
        [
            ("tours", [PreferenceDescriptor.OneOf("tours.order", "date"), PreferenceDescriptor.OneOf("tours.order", "tour")]),
        ]));
    }

    [Theory]
    [InlineData("\"date\"", true)]
    [InlineData("\"tour\"", true)]
    [InlineData("\"Date\"", false)]
    [InlineData("1", false)]
    [InlineData("null", false)]
    [InlineData("[\"date\"]", false)]
    public void AClosedSetAcceptsItsWordsOnly(string json, bool accepted)
    {
        var order = PreferenceDescriptor.OneOf("tours.order", "date", "tour");

        using var document = JsonDocument.Parse(json);
        Assert.Equal(accepted, order.Accepts(document.RootElement));
    }
}
