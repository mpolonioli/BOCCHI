using System.Globalization;
using System.Text.RegularExpressions;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.Data;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Ocelot.Extensions;
using Ocelot.Lifecycle;
using Ocelot.Services.PlayerState;

namespace BOCCHI.Treasure.Services;

public class TreasureTracker : ITreasureTracker, IOnUpdate, IDisposable
{
    /// <summary>WideText / chat: “You sense the presence of X silver … and Y bronze …”.</summary>
    private const uint ActiveChestLogMessageId = 10965;

    /// <summary>
    ///     Ceiling on a believable coffer count. Nothing in the game puts hundreds of coffers in a zone, so a reading
    ///     above this is a line that slipped through rather than a count — and the numbers feed what the guided hunt
    ///     deduces about how many coffers are still hidden.
    /// </summary>
    private const int MaxPlausibleCount = 500;

    private readonly IAddonLifecycle addonLifecycle;
    private readonly IChatGui chat;
    private readonly IDataManager data;
    private readonly IObjectTable objects;
    private readonly TimeSpan parseWideTextCooldown = TimeSpan.FromSeconds(5);
    private readonly IPlayer player;
    private readonly IZoneProvider zones;

    private DateTime lastParseWideText = DateTime.MinValue;
    private List<TreasureCoffer> treasures = [];

    public TreasureTracker(
        IObjectTable objects,
        IAddonLifecycle addonLifecycle,
        IChatGui chat,
        IDataManager data,
        IZoneProvider zones,
        IPlayer player
    )
    {
        this.objects = objects;
        this.addonLifecycle = addonLifecycle;
        this.chat = chat;
        this.data = data;
        this.zones = zones;
        this.player = player;
        addonLifecycle.RegisterListener(AddonEvent.PostDraw, "_WideText", OnWideTextPostDraw);
        // Chat is more reliable than scraping _WideText (empty first frames / cooldown misses).
        chat.LogMessage += OnChatLogMessage;
    }

    public void Dispose()
    {
        addonLifecycle.UnregisterListener(AddonEvent.PostDraw, "_WideText", OnWideTextPostDraw);
        chat.LogMessage -= OnChatLogMessage;
    }

    public void Update()
    {
        // Occult Crescent only.
        if (!zones.GetZone().IsOccultCrescentZone())
        {
            if (treasures.Count > 0)
            {
                treasures.Clear();
            }

            return;
        }

        // Key by GameObjectId — BaseId is shared by every bronze/silver of that type,
        // so a dictionary on BaseId kept only one coffer and dropped the rest (no radar line).
        Dictionary<ulong, IGameObject> worldTreasures = objects
            .Where(o => o is { ObjectKind: ObjectKind.Treasure, IsDead: false } && o.IsValid())
            .GroupBy(o => o.GameObjectId)
            .ToDictionary(g => g.Key, g => g.First());

        HashSet<ulong> knownIds = treasures.Select(t => t.GameObjectId).ToHashSet();

        CountOpened(worldTreasures);

        for (int i = treasures.Count - 1; i >= 0; i--)
        {
            TreasureCoffer treasure = treasures[i];
            if (!worldTreasures.ContainsKey(treasure.GameObjectId) || !treasure.IsValid())
            {
                treasures.RemoveAt(i);
            }
        }

        foreach ((ulong objectId, IGameObject obj) in worldTreasures)
        {
            if (knownIds.Contains(objectId))
            {
                continue;
            }

            TreasureCoffer treasure = new(obj, data);
            if (treasure.IsValid())
            {
                treasures.Add(treasure);
            }
        }

        treasures = treasures.OrderBy(t => player.Position.Distance(t.GetPosition())).ToList();
    }

    /// <summary>
    ///     Counts down the tier totals as coffers are opened, so the reading stays true between Treasure Sight casts.
    ///     <para>
    ///         Runs before the list is pruned, and that ordering is the whole point: <see cref="TreasureCoffer.IsValid" />
    ///         rejects an opened coffer, so pruning first drops it on the very tick it flips and the transition is never
    ///         seen. Only coffers the client still has loaded are checked — <see cref="TreasureCoffer.CheckOpened" />
    ///         dereferences the object, and one that has despawned is gone rather than opened.
    ///     </para>
    /// </summary>
    private void CountOpened(Dictionary<ulong, IGameObject> worldTreasures)
    {
        foreach (TreasureCoffer treasure in treasures)
        {
            if (!worldTreasures.ContainsKey(treasure.GameObjectId) || !treasure.CheckOpened())
            {
                continue;
            }

            if (treasure.GetCofferType() == CofferType.Bronze)
            {
                BronzeChests = Math.Max(0, BronzeChests - 1);
            }
            else if (treasure.GetCofferType() == CofferType.Silver)
            {
                SilverChests = Math.Max(0, SilverChests - 1);
            }
        }
    }

    public IReadOnlyList<TreasureCoffer> Treasures => treasures;

    public bool CountInitialised { get; private set; }

    public DateTime LastCountUpdateUtc { get; private set; } = DateTime.MinValue;

    public int BronzeChests { get; private set; }

    public int SilverChests { get; private set; }

    /// <summary>Increments on each successful Treasure Sight count parse.</summary>
    public int SurveyRevision { get; private set; }

    private void OnChatLogMessage(ILogMessage message)
    {
        if (message.LogMessageId != ActiveChestLogMessageId
            || !zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        // Excel order: silver then bronze (matches WideText group 1 / 2).
        if (!message.TryGetIntParameter(0, out int silver)
            || !message.TryGetIntParameter(1, out int bronze))
        {
            return;
        }

        ApplySightCounts(silver, bronze);
    }

    private unsafe void OnWideTextPostDraw(AddonEvent type, AddonArgs args)
    {
        if (!zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        AtkUnitBase* addon = (AtkUnitBase*)args.Addon.Address;
        if (!addon->IsVisible)
        {
            return;
        }

        // Only throttle successful parses — burning CD on empty/wrong banners missed Sight.
        if (DateTime.Now - lastParseWideText < parseWideTextCooldown)
        {
            return;
        }

        // GetNodeById returns null when the banner has not built its nodes yet, and GetAsAtkTextNode would
        // dereference it.
        AtkResNode* node = addon->GetNodeById(3);
        AtkTextNode* textNode = node == null ? null : node->GetAsAtkTextNode();
        if (textNode == null)
        {
            return;
        }

        string text = textNode->NodeText.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string pattern = LogMessageHelper.GetLogMessagePattern(data, ActiveChestLogMessageId);
        Match match = Regex.Match(text, pattern);
        if (!match.Success)
        {
            return;
        }

        if (!TryReadCount(match.Groups[1].Value, out int silver)
            || !TryReadCount(match.Groups[2].Value, out int bronze))
        {
            return;
        }

        lastParseWideText = DateTime.Now;
        ApplySightCounts(silver, bronze);
    }

    /// <summary>
    ///     Reads one scraped count, rejecting an implausible reading rather than letting it through.
    ///     <para>
    ///         <see cref="ApplySightCounts" /> clamps into range, which would quietly turn a misparsed 6000 into a
    ///         confident 30. Refusing the reading keeps the last known good count instead.
    ///     </para>
    /// </summary>
    private static bool TryReadCount(string value, out int count)
    {
        return int.TryParse(value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out count) && count <= MaxPlausibleCount;
    }

    private void ApplySightCounts(int silver, int bronze)
    {
        silver = Math.Clamp(silver, 0, 8);
        bronze = Math.Clamp(bronze, 0, 30);

        // Same banner can hit both chat + WideText — ignore duplicate within a moment.
        if (CountInitialised
            && SilverChests == silver
            && BronzeChests == bronze
            && DateTime.UtcNow - LastCountUpdateUtc < TimeSpan.FromSeconds(2))
        {
            return;
        }

        SilverChests = silver;
        BronzeChests = bronze;
        CountInitialised = true;
        LastCountUpdateUtc = DateTime.UtcNow;
        SurveyRevision++;
    }
}
