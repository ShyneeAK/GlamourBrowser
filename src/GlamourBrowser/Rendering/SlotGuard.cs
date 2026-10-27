using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace GlamourBrowser.Rendering;

/// <summary>Detects game windows that claim CharaView slots: the party portraits use 0–7, the other windows 0–4.</summary>
public static unsafe class SlotGuard
{
    /// <summary>
    /// A game window that renders a character is open (character, inspection, adventurer plate, try-on, dyes, fashion
    /// accessories, gear sets, glamour plates, portraits): slots 0–4 are its, not ours.
    /// </summary>
    public static bool WindowsActive()
        => Active(AgentStatus.Instance()) || Active(AgentInspect.Instance()) || Active(AgentCharaCard.Instance())
           || Active(AgentTryon.Instance()) || Active(AgentColorant.Instance()) || Active(AgentFashion.Instance())
           || Active(AgentGearSet.Instance()) || Active(AgentMiragePrismMiragePlate.Instance()) || Active(AgentBannerEditor.Instance())
           || Active(AgentBannerPreview.Instance());

    private static bool Active<T>(T* agent) where T : unmanaged => agent != null && ((AgentInterface*)agent)->IsAgentActive();

    public static bool ConflictActive()
    {
        var agent = AgentBannerParty.Instance();
        return agent != null && agent->IsAgentActive();
    }
}
