using System.Numerics;

namespace GlamourBrowser.UI.Widgets;

/// <summary>An icon to draw: a game icon by id, or a part of a game texture (e.g. a button of a game window).</summary>
/// <param name="Texture">Game texture path; with it, <paramref name="Uv0"/>–<paramref name="Uv1"/> is the part to draw.</param>
public readonly record struct UiIcon(uint IconId, string? Texture = null, Vector2 Uv0 = default, Vector2 Uv1 = default)
{
    public static UiIcon Part(string texture, Vector2 uv0, Vector2 uv1) => new(0, texture, uv0, uv1);

    public static implicit operator UiIcon(uint iconId) => new(iconId);
}
