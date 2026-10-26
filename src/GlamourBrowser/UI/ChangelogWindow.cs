using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.UI;

/// <summary>What is new: opens by itself after an update with the versions not seen yet, or from the settings with all of them.</summary>
public sealed class ChangelogWindow : Window
{
    private readonly Configuration config;
    private readonly Theme.Theme theme;
    private readonly Version current;
    private IReadOnlyList<ChangelogEntry> entries = [];
    private IDisposable? themeScope;
    private IDisposable? fontScope;

    public ChangelogWindow(Configuration config, Theme.Theme theme, Version current)
        : base(Loc.T("Shynees Glamour Dresser – Neuigkeiten") + "##ShyneesGlamourDresserChangelog")
    {
        this.config = config;
        this.theme = theme;
        this.current = current;
        Size = new Vector2(520, 420);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    /// <summary>
    /// Opens with the versions after the last one seen, once per update; a fresh install (nothing saved yet) only
    /// remembers the version.
    /// </summary>
    public void ShowAfterUpdate(bool freshInstall)
    {
        Version.TryParse(config.LastSeenVersion, out var lastSeen);
        if (lastSeen == current)
            return;
        if (!freshInstall)
        {
            entries = Changelog.Since(All(), lastSeen, current);
            IsOpen = entries.Count > 0;
        }
        config.LastSeenVersion = current.ToString();
        config.Save();
    }

    public void ShowAll()
    {
        entries = All();
        IsOpen = true;
    }

    private static IReadOnlyList<ChangelogEntry> All() => Changelog.Parse(Changelog.Embedded(Loc.Language));

    public override void PreDraw()
    {
        themeScope = theme.Push();
        fontScope = UiScale.PushText();
    }

    public override void PostDraw()
    {
        fontScope?.Dispose();
        fontScope = null;
        themeScope?.Dispose();
        themeScope = null;
    }

    public override void Draw()
    {
        var footer = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;
        using (var child = Dalamud.Interface.Utility.Raii.ImRaii.Child("##changes", new Vector2(0, -footer)))
        {
            if (child.Success)
            {
                foreach (var entry in entries)
                {
                    ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(theme.Colors.Accent), Loc.F("Version {0}", entry.Version));
                    foreach (var line in entry.Lines)
                    {
                        if (line.StartsWith("- "))
                        {
                            ImGui.Bullet();
                            ImGui.SameLine();
                            ImGui.TextWrapped(line[2..]);
                        }
                        else
                            ImGui.TextWrapped(line);
                    }
                    ImGui.Spacing();
                }
            }
        }
        ImGui.Separator();
        if (ImGui.Button(Loc.T("Schließen")))
            IsOpen = false;
    }
}
