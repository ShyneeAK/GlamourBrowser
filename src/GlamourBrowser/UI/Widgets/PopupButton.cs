using System;
using Dalamud.Bindings.ImGui;

namespace GlamourBrowser.UI.Widgets;

/// <summary>A button that opens a popup with arbitrary content.</summary>
public static class PopupButton
{
    public static void Draw(string id, string label, Action content)
    {
        if (ImGui.Button($"{label}##{id}"))
            ImGui.OpenPopup(id);
        if (!ImGui.BeginPopup(id))
            return;
        content();
        ImGui.EndPopup();
    }
}
