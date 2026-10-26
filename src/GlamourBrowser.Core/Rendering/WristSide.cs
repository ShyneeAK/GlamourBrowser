using System.Text;

namespace GlamourBrowser.Core.Rendering;

/// <summary>The wrists a bracelet's model is on: many are on one arm only, which the camera has to face.</summary>
public enum WristSide
{
    Both,
    Right,
    Left,
}

public static class WristModels
{
    /// <summary>
    /// The side of a bracelet model (the game's .mdl file): the bones it is skinned to are named in its string block,
    /// and a bracelet on one wrist names only that arm's (hand, forearm). Both when it names both arms or neither.
    /// </summary>
    public static WristSide SideOf(ReadOnlySpan<byte> model)
    {
        var text = Encoding.ASCII.GetString(model);
        bool Names(char side) => text.Contains($"j_te_{side}\0") || text.Contains($"j_ude_b_{side}\0") || text.Contains($"n_hte_{side}\0");
        return (Names('r'), Names('l')) switch
        {
            (true, false) => WristSide.Right,
            (false, true) => WristSide.Left,
            _ => WristSide.Both,
        };
    }
}
