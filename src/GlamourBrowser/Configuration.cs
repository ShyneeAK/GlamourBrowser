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
    /// <summary>
    /// 2: the page cache default went from 3 to 8 pages; 3: to 10; 4: parallel views back to 3, now a debug setting;
    /// 6: parallel views 2, the most that never touch the game windows' slots (see Plugin, which raises older files).
    /// </summary>
    public int Version { get; set; } = 6;

    public int Columns { get; set; } = 3;
    public int Rows { get; set; } = 2;
    /// <summary>The basis of tiles and try-on ("Alles anlegen / ablegen"); a new installation starts with everything off.</summary>
    public TileMode TileMode { get; set; } = TileMode.Underwear;
    /// <summary>
    /// Tiles rendered at the same time, 1–7; a debug setting. 2 stays on slots 6 and 5 beside the live preview's 7; more use
    /// the game windows' slots while those are closed (<see cref="Core.Rendering.RenderSlots"/>).
    /// </summary>
    public int ParallelViews { get; set; } = 2;
    /// <summary>The plugin version whose news the player has seen; the changelog shows what came after it.</summary>
    public string LastSeenVersion { get; set; } = "";
    /// <summary>The plugin's language: "de", "en", "fr" or "ja"; empty follows the game client.</summary>
    public string Language { get; set; } = "";
    /// <summary>How many recently shown pages stay rendered besides the current one and the ones ahead, 0–100.</summary>
    public int CachedPages { get; set; } = 10;
    /// <summary>How many pages after the current one are rendered ahead, 0–10.</summary>
    public int LookaheadPages { get; set; } = 4;
    /// <summary>The memory line (tile cache, VRAM, RAM) under the tile grid.</summary>
    public bool ShowMemoryStats { get; set; }

    public AccentColor Accent { get; set; } = AccentColor.Blue;
    public SortOrder Sort { get; set; } = SortOrder.Level;
    /// <summary>The order of "Meine Sets", apart from the items' (see <see cref="Core.CustomSets.CustomSetSorting"/>): the last changed first by default.</summary>
    public SortOrder MySetsSort { get; set; } = SortOrder.Newest;
    public bool UseIdlePose { get; set; } = true;
    /// <summary>Weapon tiles with the weapon drawn (slower: every tile plays the draw); off = on the back.</summary>
    public bool TileWeaponsDrawn { get; set; }
    public bool DimMissingTiles { get; set; } = true;
    /// <summary>The hint when a worn piece clicked in the live pane is not among the filtered tiles ("Nicht mehr anzeigen" turns it off).</summary>
    public bool HintPiecesOutsideFilters { get; set; } = true;
    /// <summary>The wishlist mode leaves out done wishes ("Erledigte ausblenden").</summary>
    public bool HideDoneWishes { get; set; }
    /// <summary>The wishlist mode shows the missing pieces grouped by source instead of tiles.</summary>
    public bool WishlistBySource { get; set; }
    /// <summary>"Zuletzt benutzt" shows the missing pieces grouped by source instead of tiles.</summary>
    public bool HistoryBySource { get; set; }
    /// <summary>The favourites show the missing pieces grouped by source instead of tiles.</summary>
    public bool FavoritesBySource { get; set; }
    /// <summary>The wishlist's tiles wear the try-on around the wish ("Outfit auf Kacheln"); off, only what the wish holds.</summary>
    public bool WishlistOutfit { get; set; }
    /// <summary>The tiles of "Zuletzt benutzt" wear the try-on around the item.</summary>
    public bool HistoryOutfit { get; set; } = true;
    /// <summary>The favourites' tiles wear the try-on around the item.</summary>
    public bool FavoritesOutfit { get; set; } = true;
    /// <summary>The plugin's own UI scale on top of Dalamud's, 0.5–3.</summary>
    public float UiScale { get; set; } = 1f;
    /// <summary>The text size on top of the UI scale, 0.8–2; icons and the slot bar keep their size.</summary>
    public float FontScale { get; set; } = 1f;
    public bool FilterColumnVisible { get; set; } = true;
    /// <summary>"Anprobierte Teile behalten": clicks add up in the try-on; off, a click replaces the one before.</summary>
    public bool TryOnKeep { get; set; } = true;
    /// <summary>The browser's view and filters when it was last changed, opened again after a restart; null before the first change.</summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public SavedFilter? Filter { get; set; }
    /// <summary>Replace, not merge, on load: otherwise a closed default section would reopen after a restart.</summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HashSet<string> OpenSections { get; set; } = ["level", "ownership"];

    public float RotateSensitivity { get; set; } = 0.5f;
    public float PanSensitivity { get; set; } = 0.5f;
    public float ZoomSensitivity { get; set; } = 3f;
    /// <summary>Zoom of the automatic camera per slot (1 or missing: as measured, 2: twice as close); own calibrations stay as they are.</summary>
    public Dictionary<EquipSlot, float> AutoFramingZooms { get; set; } = new();

    /// <summary>Only read once to migrate old data; calibrations now live in per-character files (CalibrationService).</summary>
    public Dictionary<ulong, Dictionary<EquipSlot, CameraPreset>> CameraCalibrations { get; set; } = new();

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
