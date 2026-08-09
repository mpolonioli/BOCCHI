using System.Numerics;

namespace BOCCHI.Common.Data.Zones;

/// <summary>
///     The height rule shared by every scrape of the game's <c>Treasure</c> layout instances, so a zone with a new
///     sub-area cannot be visible to one scrape and not the others.
///     <para>
///         Which SGB ids count as a coffer is <em>not</em> here: that is
///         <c>TreasureCoffer.IsBronzeOrSilverSgb</c>, and a second copy of the same two row ids is exactly the drift
///         this file exists to prevent.
///     </para>
/// </summary>
public static class TreasureLayout
{
    /// <summary>
    ///     SGB RowId of the coffer tier found only in the Forked Tower's layout. Not tracked — the raid is out of scope
    ///     — but named so a scrape that turns it up is recognised rather than filed as an unknown, and because
    ///     <see cref="DepthFloor" /> is chosen to exclude precisely the rows carrying it.
    /// </summary>
    public const uint SgbGold = 1598;

    /// <summary>
    ///     Layout instances below this height are not standing on ground the player can reach in the open zone.
    ///     <para>
    ///         The value used to be -10, which was wrong in a way that hid a whole area. North Horn's Dark Territory has
    ///         two underground sub-maps — North Basin around -20y and the Subterrane down to -162y — and they are real
    ///         ground with real coffers on it. That floor silently dropped all nine of their spawn points, which is why
    ///         the zone first surveyed as 53 bronze / 6 silver when it actually holds 60 and 8, the same as South Horn.
    ///     </para>
    ///     <para>
    ///         What genuinely does sit below the map is the Forked Tower's own layout: rows 1983–2004 at -672y to -980y,
    ///         carrying <see cref="SgbGold" /> and the same position repeated across layer sets. -400y is the middle of
    ///         the gap between the two — deep enough to keep every walkable sub-map, shallow enough to leave the raid out.
    ///     </para>
    ///     <para>
    ///         Both shipped zones now author a position for every coffer, which bypasses this filter entirely. It still
    ///         guards the first scrape of an unsurveyed zone, which is the case that got North Horn wrong.
    ///     </para>
    /// </summary>
    public const float DepthFloor = -400f;

    /// <summary>
    ///     True when a coffer spawn point is part of the open zone rather than the raid layout parked beneath it.
    /// </summary>
    public static bool IsInPlayableZone(Vector3 position)
    {
        return position.Y > DepthFloor;
    }
}
