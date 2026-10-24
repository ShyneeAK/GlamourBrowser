using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace GlamourBrowser.Rendering;

/// <summary>Detects game windows that claim CharaView slots 5-7 (party portraits use slots 0-7).</summary>
public static unsafe class SlotGuard
{
    public static bool ConflictActive()
    {
        var agent = AgentBannerParty.Instance();
        return agent != null && agent->IsAgentActive();
    }
}
