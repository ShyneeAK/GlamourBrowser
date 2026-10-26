using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GlamourBrowser.Core.Rendering;
using Item = Lumina.Excel.Sheets.Item;

namespace GlamourBrowser.Rendering;

/// <summary>
/// The wrist each bracelet is on, read from its model: for the camera (first asked, then kept) and, once every bracelet
/// is read in the background, for the filter by side.
/// </summary>
public sealed class WristSides : IDisposable
{
    private readonly ConcurrentDictionary<uint, WristSide> known = new();
    private readonly CancellationTokenSource stop = new();

    /// <summary>Every bracelet of the catalog is read: the side filter can apply.</summary>
    public bool IsReady { get; private set; }

    /// <summary>On the framework thread, when every bracelet is read.</summary>
    public event Action? Ready;

    /// <summary>Reads the sides of the bracelets in the background.</summary>
    public void LoadAll(IReadOnlyList<uint> bracelets)
    {
        var token = stop.Token;
        Task.Run(() =>
        {
            foreach (var id in bracelets)
            {
                if (token.IsCancellationRequested)
                    return;
                Of(id);
            }
            Plugin.Framework.RunOnFrameworkThread(() =>
            {
                if (token.IsCancellationRequested)
                    return;
                IsReady = true;
                Ready?.Invoke();
            });
        }, token);
    }

    public WristSide Of(uint itemId) => known.GetOrAdd(itemId, Read);

    private static WristSide Read(uint itemId)
    {
        try
        {
            if (Plugin.DataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId) is not { } item)
                return WristSide.Both;
            // The model set; the side is the same for every race, so the first race model found tells it.
            var set = item.ModelMain & 0xFFFF;
            foreach (var race in new[] { "c0101", "c0201", "c1101", "c1201" })
            {
                if (Plugin.DataManager.GetFile($"chara/accessory/a{set:D4}/model/{race}a{set:D4}_wrs.mdl") is { } file)
                    return WristModels.SideOf(file.Data);
            }
        }
        catch (Exception e)
        {
            Plugin.Log.Warning(e, $"Could not read the bracelet model of {itemId}");
        }
        return WristSide.Both;
    }

    public void Dispose()
    {
        stop.Cancel();
        stop.Dispose();
    }
}
