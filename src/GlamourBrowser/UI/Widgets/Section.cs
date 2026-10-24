using Dalamud.Bindings.ImGui;

namespace GlamourBrowser.UI.Widgets;

/// <summary>Collapsible section whose open state is remembered in the configuration.</summary>
public static class Section
{
    public static bool Begin(string key, string title, Configuration config)
    {
        var open = config.OpenSections.Contains(key);
        ImGui.SetNextItemOpen(open, ImGuiCond.Always);
        var nowOpen = ImGui.CollapsingHeader($"{title}##section-{key}");
        if (nowOpen != open)
        {
            if (nowOpen)
                config.OpenSections.Add(key);
            else
                config.OpenSections.Remove(key);
            config.Save();
        }

        if (nowOpen)
            ImGui.Indent(6);
        return nowOpen;
    }

    /// <summary>Call only when <see cref="Begin"/> returned true.</summary>
    public static void End()
    {
        ImGui.Unindent(6);
        ImGui.Spacing();
    }
}
