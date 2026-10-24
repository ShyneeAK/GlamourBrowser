namespace GlamourBrowser.Core.Rendering;

public enum SettleAction
{
    Wait,
    Reapply,
    Copy,
    CopyWithoutWeapons,
    Timeout,
}

/// <summary>
/// Decides, once per frame after a tile's items have loaded, when its image may be copied: the view must
/// be idle and its weapons drawn. Hidden weapons are re-applied a bounded number of times; the timeout
/// counts every frame, so retries cannot keep a worker busy forever.
/// </summary>
public sealed class SettleGate(int settleFrames, int timeoutFrames, int maxRetries)
{
    private int frames;
    private int settled;
    private int retries;

    public int Retries => retries;

    public SettleAction Step(bool idle, bool weaponsShown)
    {
        if (++frames > timeoutFrames)
            return SettleAction.Timeout;

        if (!idle)
        {
            settled = 0;
            return SettleAction.Wait;
        }

        if (!weaponsShown)
        {
            if (retries >= maxRetries)
                return SettleAction.CopyWithoutWeapons;
            retries++;
            settled = 0;
            return SettleAction.Reapply;
        }

        return ++settled >= settleFrames ? SettleAction.Copy : SettleAction.Wait;
    }
}
