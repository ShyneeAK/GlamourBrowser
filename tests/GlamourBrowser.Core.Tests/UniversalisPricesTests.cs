using GlamourBrowser.Core.Market;

namespace GlamourBrowser.Core.Tests;

public class UniversalisPricesTests
{
    [Fact]
    public void Parse_TakesTheCheapestListingWithItsWorld()
    {
        const string json = """
            {"itemID":5,"lastUploadTime":1700000000000,"listings":[
              {"pricePerUnit":1200,"quantity":1,"hq":false,"worldName":"Lich"},
              {"pricePerUnit":900,"quantity":1,"hq":true,"worldName":"Odin"}]}
            """;

        Assert.Equal(new MarketPrice(900, true, "Odin", DateTimeOffset.FromUnixTimeMilliseconds(1700000000000)), UniversalisPrices.Parse(json));
    }

    [Fact]
    public void Parse_AWorldsAnswerHasNoWorldName()
    {
        const string json = """{"lastUploadTime":1700000000000,"listings":[{"pricePerUnit":42,"hq":false}]}""";

        Assert.Equal(new MarketPrice(42, false, null, DateTimeOffset.FromUnixTimeMilliseconds(1700000000000)), UniversalisPrices.Parse(json));
    }

    [Fact]
    public void Parse_NothingListedIsNull()
    {
        Assert.Null(UniversalisPrices.Parse("""{"lastUploadTime":0,"listings":[]}"""));
        Assert.Null(UniversalisPrices.Parse("""{"itemID":5}"""));
    }

    [Fact]
    public void Url_AsksOnlyForTheCheapestListing()
    {
        Assert.Equal("https://universalis.app/api/v2/Light/5?listings=1&entries=0", UniversalisPrices.Url("Light", 5));
    }
}
