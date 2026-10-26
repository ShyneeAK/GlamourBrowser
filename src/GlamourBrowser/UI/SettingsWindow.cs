using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using GlamourBrowser.Browser;
using System;
using System.Collections.Generic;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Core.Ui;
using GlamourBrowser.Core.Localization;
using GlamourBrowser.Rendering;

namespace GlamourBrowser.UI;

public sealed class SettingsWindow : Window
{
    private readonly Configuration config;
    private readonly BrowserState state;
    private readonly RenderPool pool;
    private readonly CalibrationService calibration;
    private readonly Theme.Theme theme;
    private readonly Action showChangelog;
    private IDisposable? themeScope;
    private IDisposable? fontScope;
    private int? scaleDraft;
    private int? fontDraft;
    // A new UI scale or text size: the fonts are rebuilt before the next draw.
    private bool fontsChanged;

    // "" follows the game client; the names stay in their own language, so each can be found whatever is shown.
    private static readonly string[] LanguageCodes = ["", "de", "en", "fr", "ja"];
    private static readonly string[] LanguageNames = ["", "Deutsch", "English", "Français", "日本語"];
    private bool languageChanged;

    private static readonly string[] AccentNames = [Loc.T("Blau"), Loc.T("Violett"), Loc.T("Türkis"), Loc.T("Rosé"), Loc.T("Bernstein")];

    public SettingsWindow(Configuration config, BrowserState state, RenderPool pool, CalibrationService calibration, Theme.Theme theme,
                          Action showChangelog)
        : base(Loc.T("Shynees Glamour Dresser – Einstellungen") + "##ShyneesGlamourDresserSettings")
    {
        this.config = config;
        this.state = state;
        this.pool = pool;
        this.calibration = calibration;
        this.theme = theme;
        this.showChangelog = showChangelog;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(420, 380) };
    }

    public override void PreDraw()
    {
        themeScope = theme.Push();
        // A new scale takes effect here, while none of the plugin's fonts is pushed: Dalamud refuses to dispose
        // a font handle that is still pushed.
        if (fontsChanged)
        {
            UiScale.Set(config.UiScale, config.FontScale);
            fontsChanged = false;
        }
        // Before Begin, so the title bar scales too.
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
        var accent = (int)config.Accent;
        if (ImGui.Combo(Loc.T("Akzentfarbe"), ref accent, AccentNames, AccentNames.Length))
        {
            config.Accent = (AccentColor)accent;
            config.Save();
            theme.Refresh();
        }

        var columns = config.Columns;
        var rows = config.Rows;
        var gridChanged = ImGui.SliderInt(Loc.T("Spalten"), ref columns, 2, 10, "%d", ImGuiSliderFlags.AlwaysClamp);
        gridChanged |= ImGui.SliderInt(Loc.T("Zeilen"), ref rows, 1, 6, "%d", ImGuiSliderFlags.AlwaysClamp);
        if (gridChanged)
            state.SetGrid(columns, rows);

        var mode = (int)config.TileMode;
        if (ImGui.Combo(Loc.T("Kachel-Modus"), ref mode, [Loc.T("Aktuelles Outfit"), Loc.T("Unterwäsche")], 2))
            state.SetTileMode((TileMode)mode);

        var pose = config.UseIdlePose ? 1 : 0;
        if (ImGui.Combo(Loc.T("Pose"), ref pose, [Loc.T("Standard"), Loc.T("Meine Idle-Pose")], 2))
            state.SetUseIdlePose(pose == 1);


        DrawLanguage();
        if (ImGui.Button(Loc.T("Neuigkeiten anzeigen")))
            showChangelog();

        // The value being dragged is kept across frames; fonts are rebuilt only once the slider is released.
        var percent = scaleDraft ?? (int)MathF.Round(config.UiScale * 100);
        if (ImGui.SliderInt(Loc.T("UI-Skalierung"), ref percent, 50, 300, "%d %%", ImGuiSliderFlags.AlwaysClamp))
            scaleDraft = percent;
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            config.UiScale = percent / 100f;
            config.Save();
            fontsChanged = true;
            scaleDraft = null;
        }

        var fontPercent = fontDraft ?? (int)MathF.Round(config.FontScale * 100);
        if (ImGui.SliderInt(Loc.T("Schriftgröße"), ref fontPercent, 80, 200, "%d %%", ImGuiSliderFlags.AlwaysClamp))
            fontDraft = fontPercent;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Nur der Text, zusätzlich zur UI-Skalierung; Icons und die Slot-Leiste bleiben gleich groß."));
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            config.FontScale = fontPercent / 100f;
            config.Save();
            fontsChanged = true;
            fontDraft = null;
        }

        var cachedPages = config.CachedPages;
        if (ImGui.SliderInt(Loc.T("Seiten im Cache"), ref cachedPages, 0, PageHistory.MaxPages, "%d", ImGuiSliderFlags.AlwaysClamp))
            state.SetCachedPages(cachedPages);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Wie viele zuletzt angezeigte Seiten gerendert bleiben, auch beim Wechsel zwischen Slots und Modi.\n"
                                   + "Zurück zu einer solchen Seite zeigt die Bilder sofort. Mehr Seiten brauchen mehr Grafikspeicher."));

        var lookahead = config.LookaheadPages;
        if (ImGui.SliderInt(Loc.T("Seiten vorausrendern"), ref lookahead, 0, Lookahead.MaxPages, "%d", ImGuiSliderFlags.AlwaysClamp))
            state.SetLookaheadPages(lookahead);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Wie viele Seiten in Blätterrichtung schon im Voraus gerendert werden, die nächste zuerst; "
                                   + "rückwärts die vorherigen, auf der letzten Seite die davor.\n"
                                   + "Blättern zeigt die Bilder dann sofort. Mehr Seiten brauchen mehr Grafikspeicher und Renderzeit."));

        ImGui.Separator();
        DrawCameraPresets();

        ImGui.Separator();
        ImGui.TextDisabled(Loc.T("Debug"));
        var memory = config.ShowMemoryStats;
        if (ImGui.Checkbox(Loc.T("Speicheranzeige unter dem Raster"), ref memory))
        {
            config.ShowMemoryStats = memory;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Zeigt unter den Kacheln, wie viel Grafikspeicher der Kachel-Cache belegt, dazu VRAM und RAM des Spiels."));

        var views = config.ParallelViews;
        if (ImGui.SliderInt(Loc.T("Parallele Ansichten"), ref views, 1, 3, "%d", ImGuiSliderFlags.AlwaysClamp))
        {
            config.ParallelViews = views;
            pool.MaxViews = views;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Wie viele Kacheln gleichzeitig gerendert werden (Standard 3)."));
    }

    /// <summary>The plugin's language: the game client's, or a chosen one. Texts already built change after a reload of the plugin.</summary>
    private void DrawLanguage()
    {
        var index = Array.IndexOf(LanguageCodes, config.Language);
        if (index < 0)
            index = 0;
        string[] names = [Loc.F("Wie der Client ({0})", LanguageNames[Array.IndexOf(LanguageCodes, Plugin.LanguageOf(""))]), .. LanguageNames.Skip(1)];
        if (ImGui.Combo(Loc.T("Sprache"), ref index, names, names.Length))
        {
            config.Language = LanguageCodes[index];
            config.Save();
            Loc.Load(Plugin.LanguageOf(config.Language));
            languageChanged = true;
        }
        if (languageChanged)
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), Loc.T("Ganz übernommen wird die Sprache nach einem Neustart des Plugins."));
    }

    /// <summary>
    /// One zoom per slot for the automatic camera (over 100 % closer, below further away, around the same middle);
    /// sets use the body's. Own calibrations stay as they are.
    /// </summary>
    private void DrawAutoZooms()
    {
        ImGui.TextDisabled(Loc.T("Zoom der automatischen Kamera"));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Wie nah die automatische Kamera an den Slot heranzoomt (über 100 % näher, darunter weiter weg); "
                                   + "eigene Kalibrierungen bleiben, wie sie sind."));
        foreach (var (slot, name) in SlotNames.All)
        {
            var zoom = (int)MathF.Round(config.AutoFramingZooms.GetValueOrDefault(slot, 1f) * 100);
            if (!ImGui.SliderInt($"{name}##autozoom{slot}", ref zoom, 50, 300, "%d %%", ImGuiSliderFlags.AlwaysClamp))
                continue;
            if (zoom == 100)
                config.AutoFramingZooms.Remove(slot);
            else
                config.AutoFramingZooms[slot] = zoom / 100f;
            config.Save();
            state.InvalidateTiles();
        }
        ImGui.Spacing();
    }

    private void DrawCameraPresets()
    {
        ImGui.TextDisabled(Loc.T("Kamera-Kalibrierung dieses Charakters"));
        ImGui.TextWrapped(Loc.T("Kalibriert wird in der Live-Ansicht: Item eines Slots auswählen, Ansicht einstellen, „Kamera-Position festlegen“ klicken."));

        DrawAutoZooms();

        var contentId = Plugin.PlayerState.ContentId;
        calibration.Ensure(contentId);
        var calibrated = calibration.Calibrations.For(contentId);
        foreach (var (slot, name) in SlotNames.All)
        {
            if (!calibrated.ContainsKey(slot))
            {
                ImGui.TextDisabled(Loc.F("{0}: Standard", name));
                continue;
            }

            ImGui.TextUnformatted(Loc.F("{0}: kalibriert", name));
            ImGui.SameLine();
            if (ImGui.SmallButton(Loc.T("Zurücksetzen") + $"##{slot}") && calibration.Remove(contentId, slot))
                state.InvalidateTiles();
        }
    }
}
