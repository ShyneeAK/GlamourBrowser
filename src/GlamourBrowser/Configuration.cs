using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Newtonsoft.Json;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Ui;

namespace GlamourBrowser;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public int Columns { get; set; } = 6;
    public int Rows { get; set; } = 3;
    public TileMode TileMode { get; set; } = TileMode.CurrentOutfit;
    public int ParallelViews { get; set; } = 3;

    public AccentColor Accent { get; set; } = AccentColor.Blue;
    public bool FilterColumnVisible { get; set; } = true;
    /// <summary>Replace, not merge, on load: otherwise a closed default section would reopen after a restart.</summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HashSet<string> OpenSections { get; set; } = ["level", "dye", "ownership"];

    public float RotateSensitivity { get; set; } = 0.5f;
    public float PanSensitivity { get; set; } = 0.5f;
    public float ZoomSensitivity { get; set; } = 3f;

    /// <summary>Only read once to migrate old data; calibrations now live in per-character files (CalibrationService).</summary>
    public Dictionary<ulong, Dictionary<EquipSlot, CameraPreset>> CameraCalibrations { get; set; } = new();

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
