using Dalamud.Plugin.Services;

namespace BOCCHI.Treasure.Guided;

/// <summary>One guided hunt, seen from the outside — which is all the coordinator and the UI need of it.</summary>
public interface IGuidedHunt
{
    /// <summary>How the hunt names itself in chat and in the coordinator's hand-off message.</summary>
    string Label { get; }

    bool IsGuiding { get; }

    /// <summary>
    ///     True when the hunt moves the flag on its own as spots resolve. A hunt that only flags when the player presses
    ///     a button takes the flag from nobody, so an outside claimant has no reason to stop it.
    /// </summary>
    bool MovesFlagAutomatically { get; }

    void Start();

    void Stop();

    void Toggle();
}

/// <summary>
///     Keeps exactly one hunt guiding at a time.
///     <para>
///         The game holds a single map flag, and both hunts want to own it and move it as they go. Two hunts flagging
///         at once is not a cosmetic problem: the flag is the thing the player is running at, so a flag that alternates
///         between a coffer and a carrot is worse than no flag. Starting one hunt therefore stops the other, and says
///         so, rather than leaving the player to work out why their marker keeps jumping.
///     </para>
///     <para>Only automatic steering is exclusive — the Flag buttons in the window keep working for either hunt.</para>
///     <para>
///         The guided hunts are not the only things that flag. Upstream's Completionist survey points flag on click,
///         and a guided hunt that keeps auto-flagging afterwards drags the marker off the point the player just asked
///         for. Anything outside this file that claims the flag goes through <see cref="ClaimForExternal" />.
///     </para>
/// </summary>
public class GuidedHuntCoordinator(IChatGui chat)
{
    private readonly List<IGuidedHunt> hunts = [];

    public void Register(IGuidedHunt hunt)
    {
        if (!hunts.Contains(hunt))
        {
            hunts.Add(hunt);
        }
    }

    /// <summary>Hands this hunt the flag, stopping whichever other hunt was holding it.</summary>
    public void Claim(IGuidedHunt claimant)
    {
        // Unconditional between hunts: two hunts guiding at once also both announce their next spot, so the flag is
        // only half of why that is worth preventing.
        StopFlagHolders(hunt => hunt != claimant, $"the {claimant.Label}");
    }

    /// <summary>
    ///     Hands the flag to something that is not a guided hunt — the Completionist's survey points. Only hunts that
    ///     would move the flag on their own are stopped; one that flags only on a button press is left running.
    /// </summary>
    /// <param name="claimantLabel">How the claimant names itself in the hand-off message, e.g. "the survey point".</param>
    public void ClaimForExternal(string claimantLabel)
    {
        StopFlagHolders(hunt => hunt.MovesFlagAutomatically, claimantLabel);
    }

    private void StopFlagHolders(Func<IGuidedHunt, bool> shouldStop, string claimantLabel)
    {
        foreach(IGuidedHunt hunt in hunts)
        {
            if (!hunt.IsGuiding || !shouldStop(hunt))
            {
                continue;
            }

            hunt.Stop();
            chat.Print($"[BOCCHI] Guided {hunt.Label} stopped — {claimantLabel} has the map flag.");
        }
    }
}
