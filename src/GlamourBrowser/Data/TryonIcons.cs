using System.Numerics;
using GlamourBrowser.UI.Widgets;

namespace GlamourBrowser.Data;

/// <summary>
/// The round buttons of the game's try-on and character windows (ui/uld/CircleButtons), so the live view's buttons look
/// like the game's own. Parts as the character window's layout names them, in the texture's standard size (252×160).
/// </summary>
public static class TryonIcons
{
    private const string Texture = "ui/uld/CircleButtons_hr1.tex";

    private static UiIcon Part(float u, float v, float edge)
        => UiIcon.Part(Texture, new Vector2(u / 252f, v / 160f), new Vector2((u + edge) / 252f, (v + edge) / 160f));

    /// <summary>"Waffe ziehen / wegstecken".</summary>
    public static readonly UiIcon DrawWeapon = Part(48, 112, 24);

    /// <summary>"Visier schließen".</summary>
    public static readonly UiIcon Visor = Part(72, 112, 24);

    /// <summary>"Alles anlegen / ablegen".</summary>
    public static readonly UiIcon Outfit = Part(144, 112, 24);

    /// <summary>"Anprobierte Teile behalten" (the profile with the pin).</summary>
    public static readonly UiIcon Keep = Part(72, 136, 24);

    /// <summary>"Weggesteckte Waffen und Werkzeuge anzeigen".</summary>
    public static readonly UiIcon ShowWeapon = Part(24, 136, 24);

    /// <summary>The cog of the settings buttons.</summary>
    public static readonly UiIcon Settings = Part(0, 0, 28);

    /// <summary>The gold ring the game puts around an active round button (28 × 28 around the 24 × 24 buttons).</summary>
    public static readonly UiIcon ActiveRing = Part(84, 84, 28);
}
