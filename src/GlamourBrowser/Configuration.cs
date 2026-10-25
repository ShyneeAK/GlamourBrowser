using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Newtonsoft.Json;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Sources;
using GlamourBrowser.Core.Ui;

namespace GlamourBrowser;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    /// <summary>2: the page cache default went from 3 to 8 pages; 3: to 10 (see Plugin, which raises older files).</summary>
    public int Version { get; set; } = 3;

    public int Columns { get; set; } = 3;
    public int Rows { get; set; } = 2;
    public TileMode TileMode { get; set; } = TileMode.CurrentOutfit;
    public int ParallelViews { get; set; } = 3;
    /// <summary>How many recently shown pages stay rendered besides the current one and the ones ahead, 0–100.</summary>
    public int CachedPages { get; set; } = 10;
    /// <summary>How many pages after the current one are rendered ahead, 0–10.</summary>
    public int LookaheadPages { get; set; } = 4;
    /// <summary>The memory line (tile cache, VRAM, RAM) under the tile grid.</summary>
    public bool ShowMemoryStats { get; set; }

    public AccentColor Accent { get; set; } = AccentColor.Blue;
    public SortOrder Sort { get; set; } = SortOrder.Level;
    public bool UseIdlePose { get; set; } = true;
    /// <summary>Weapon tiles with the weapon drawn (slower: every tile plays the draw); off = on the back.</summary>
    public bool TileWeaponsDrawn { get; set; }
    public bool DimMissingTiles { get; set; } = true;
    /// <summary>The wishlist mode leaves out done wishes ("Erledigte ausblenden").</summary>
    public bool HideDoneWishes { get; set; }
    /// <summary>The wishlist mode shows the missing pieces grouped by source instead of tiles.</summary>
    public bool WishlistBySource { get; set; }
    /// <summary>The plugin's own UI scale on top of Dalamud's, 0.5–3.</summary>
    public float UiScale { get; set; } = 1f;
    public bool FilterColumnVisible { get; set; } = true;
    /// <summary>Replace, not merge, on load: otherwise a closed default section would reopen after a restart.</summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HashSet<string> OpenSections { get; set; } = ["level", "ownership"];

    public float RotateSensitivity { get; set; } = 0.5f;
    public float PanSensitivity { get; set; } = 0.5f;
    public float ZoomSensitivity { get; set; } = 3f;

    /// <summary>Only read once to migrate old data; calibrations now live in per-character files (CalibrationService).</summary>
    public Dictionary<ulong, Dictionary<EquipSlot, CameraPreset>> CameraCalibrations { get; set; } = new();

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
