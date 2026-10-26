using System.Text.Json;

namespace GlamourBrowser.Core.Market;

/// <summary>The cheapest listing of an item on a world or data center, as Universalis knows it.</summary>
/// <param name="World">The listing's world; null when Universalis does not name it (a single world's answer).</param>
/// <param name="Updated">When the market data was last uploaded.</param>
public sealed record MarketPrice(uint PricePerUnit, bool Hq, string? World, DateTimeOffset Updated);

/// <summary>Reads Universalis' current market data ("/api/v2/{world or data center}/{item}?listings=1").</summary>
public static class UniversalisPrices
{
    public const string Api = "https://universalis.app/api/v2";

    /// <summary>The request for the cheapest listing only, without the sale history.</summary>
    public static string Url(string worldOrDataCenter, uint itemId)
        => $"{Api}/{Uri.EscapeDataString(worldOrDataCenter)}/{itemId}?listings=1&entries=0";

    /// <summary>The cheapest listing; null when nothing is listed.</summary>
    /// <exception cref="JsonException">The answer is not market data.</exception>
    public static MarketPrice? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var updated = root.TryGetProperty("lastUploadTime", out var upload) && upload.TryGetInt64(out var ms)
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
            : DateTimeOffset.MinValue;
        if (!root.TryGetProperty("listings", out var listings) || listings.ValueKind != JsonValueKind.Array || listings.GetArrayLength() == 0)
            return null;
        var cheapest = listings.EnumerateArray()
            .Where(l => l.TryGetProperty("pricePerUnit", out _))
            .MinBy(l => l.GetProperty("pricePerUnit").GetUInt32());
        if (cheapest.ValueKind != JsonValueKind.Object)
            return null;
        var hq = cheapest.TryGetProperty("hq", out var h) && h.ValueKind == JsonValueKind.True;
        var world = cheapest.TryGetProperty("worldName", out var w) && w.ValueKind == JsonValueKind.String ? w.GetString() : null;
        return new MarketPrice(cheapest.GetProperty("pricePerUnit").GetUInt32(), hq, world, updated);
    }
}
