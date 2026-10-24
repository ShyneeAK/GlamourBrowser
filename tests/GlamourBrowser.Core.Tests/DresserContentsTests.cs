using GlamourBrowser.Core.Ownership;

namespace GlamourBrowser.Core.Tests;

public class DresserContentsTests
{
    [Fact]
    public void Resolve_HqAndOutfits()
    {
        var outfits = new Dictionary<uint, IReadOnlyList<uint>> { [900] = [10, 11] };
        var result = DresserContents.Resolve([0, 1_000_123, 456, 900], outfits);
        Assert.Equal(new uint[] { 10, 11, 123, 456, 900 }, result.Order().ToArray());
    }
}
