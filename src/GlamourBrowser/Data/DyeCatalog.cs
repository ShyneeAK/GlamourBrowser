using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using GlamourBrowser.Core.Localization;

namespace GlamourBrowser.Data;

/// <param name="Color">ImGui colour (ABGR), opaque.</param>
/// <param name="Items">The items that give this dye (e.g. "Einfacher Farbstoff"), as the game's plate editor takes them.</param>
public sealed record DyeInfo(byte Id, string Name, uint Color, byte Shade, byte SubOrder, IReadOnlyList<uint> Items);

/// <summary>All dyes from the Stain sheet, by shade and then the game's order.</summary>
public sealed class DyeCatalog
{
    private readonly Dictionary<byte, DyeInfo> byId;

    public DyeCatalog(IDataManager data)
    {
        All = data.GetExcelSheet<Stain>()
            .Where(s => s.RowId is > 0 and <= byte.MaxValue && !s.Name.IsEmpty)
            .Select(s => new DyeInfo((byte)s.RowId, s.Name.ExtractText(), Abgr(s.Color), s.Shade, s.SubOrder,
                s.Item.Select(i => i.RowId).Where(id => id != 0).ToList()))
            .OrderBy(d => d.Shade)
            .ThenBy(d => d.SubOrder)
            .ToList();
        byId = All.ToDictionary(d => d.Id);
    }

    public IReadOnlyList<DyeInfo> All { get; }

    public DyeInfo? Find(byte id) => byId.GetValueOrDefault(id);

    public IReadOnlyList<uint> ItemsOf(byte id) => Find(id)?.Items ?? [];

    public string NameOf(byte id) => id == 0 ? Loc.T("keine Farbe") : Find(id)?.Name ?? Loc.F("Farbe {0}", id);

    /// <summary>The sheet holds RGB; ImGui wants ABGR.</summary>
    private static uint Abgr(uint rgb) => 0xFF000000 | (rgb & 0xFF) << 16 | (rgb & 0xFF00) | (rgb >> 16 & 0xFF);
}
