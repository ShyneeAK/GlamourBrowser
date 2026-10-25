namespace GlamourBrowser.Core.Items;

/// <summary>The tile dye: <see cref="First"/> for dye channel 1, <see cref="Second"/> for channel 2 (stain ids, 0 = none).</summary>
public readonly record struct DyePair(byte First, byte Second)
{
    public bool IsNone => First == 0 && Second == 0;
}
