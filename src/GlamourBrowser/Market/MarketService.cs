using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GlamourBrowser.Core.Market;

namespace GlamourBrowser.Market;

/// <summary>
/// The cheapest listing of an item on the home world and on its data center, asked from Universalis when the card's
/// market trait is hovered; kept for a few minutes, failures for less.
/// </summary>
public sealed class MarketService : IDisposable
{
    private static readonly TimeSpan Fresh = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FailedFor = TimeSpan.FromSeconds(30);

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentDictionary<(uint Item, string Place), Entry> entries = new();

    public MarketService()
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd("GlamourBrowser");
    }

    /// <summary>What is known about the item on the world or data center; starts the request when nothing (fresh) is.</summary>
    public MarketAnswer Get(uint itemId, string worldOrDataCenter)
    {
        var key = (itemId, worldOrDataCenter);
        var now = DateTime.UtcNow;
        if (entries.TryGetValue(key, out var entry) && (entry.Answer.State == MarketState.Loading || now < entry.Until))
            return entry.Answer;
        entries[key] = new Entry(MarketAnswer.Loading, DateTime.MaxValue);
        _ = Fetch(key);
        return MarketAnswer.Loading;
    }

    private async Task Fetch((uint Item, string Place) key)
    {
        try
        {
            var json = await http.GetStringAsync(UniversalisPrices.Url(key.Place, key.Item), stop.Token);
            var price = UniversalisPrices.Parse(json);
            entries[key] = new Entry(new MarketAnswer(MarketState.Done, price), DateTime.UtcNow + Fresh);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            Plugin.Log.Debug($"[market] {key.Item} on {key.Place}: {e.Message}");
            entries[key] = new Entry(MarketAnswer.Failed, DateTime.UtcNow + FailedFor);
        }
    }

    public void Dispose()
    {
        stop.Cancel();
        http.Dispose();
        stop.Dispose();
    }

    private sealed record Entry(MarketAnswer Answer, DateTime Until);
}

public enum MarketState
{
    Loading,
    Done,
    Failed,
}

/// <param name="Price">The cheapest listing; null while loading, after a failure or when nothing is listed.</param>
public sealed record MarketAnswer(MarketState State, MarketPrice? Price)
{
    public static readonly MarketAnswer Loading = new(MarketState.Loading, null);
    public static readonly MarketAnswer Failed = new(MarketState.Failed, null);
}
