using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;

namespace GlamourBrowser;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public int Columns { get; set; } = 6;
    public int Rows { get; set; } = 3;
    public TileMode TileMode { get; set; } = TileMode.CurrentOutfit;
    public int ParallelViews { get; set; } = 3;
    public bool DimMissing { get; set; } = true;

    public float RotateSensitivity { get; set; } = 0.5f;
    public float PanSensitivity { get; set; } = 0.5f;
    public float ZoomSensitivity { get; set; } = 3f;

    /// <summary>Camera calibration per character (ContentId) and slot.</summary>
    public Dictionary<ulong, Dictionary<EquipSlot, CameraPreset>> CameraCalibrations { get; set; } = new();

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
