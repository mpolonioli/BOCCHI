using BOCCHI.Common.Config;
using Dalamud.Bindings.ImGui;
using Ocelot.Config;
using System.Numerics;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     A hunt's working surface: every spawn point it knows about, what is known about each, and a flag button per row.
///     Both hunts get the same table because the questions a player asks of it are the same — where is it, how far, is
///     it safe, and have I been there — even though what fills it in is completely different.
/// </summary>
public abstract class GuidedHuntTab<TSpot>(GuidedHuntService<TSpot> hunt, IGuidedHuntConfig config, IConfigSaver saver)
    where TSpot : GuidedSpot
{
    private enum SortMode
    {
        Plan,
        Distance,
        Status,

        /// <summary>Only offered by hunts whose spots come in kinds worth grouping — coffer tiers, and nothing else so far.</summary>
        Kind
    }

    private SortMode sort = SortMode.Plan;

    /// <summary>Name of this hunt's tab, and the suffix that keeps its ImGui ids apart from the other tab's.</summary>
    protected abstract string Id { get; }

    /// <summary>Shown in place of the table before the hunt has any spawn points.</summary>
    protected abstract string EmptyMessage { get; }

    protected GuidedHuntService<TSpot> Hunt => hunt;

    public void Draw()
    {
        if (!config.Enabled)
        {
            ImGui.TextUnformatted($"The guided {hunt.Label} is turned off in the config.");
            return;
        }

        if (hunt.Tracker.Spots.Count == 0)
        {
            DrawControlButtons();
            ImGui.TextUnformatted(EmptyMessage);
            return;
        }

        DrawHeader();
        ImGui.Separator();
        DrawTarget();
        ImGui.Separator();
        DrawFilters();
        DrawTable();
    }

    #region Header

    private void DrawHeader()
    {
        DrawControlButtons();
        DrawSession();
        DrawSummary();
    }

    private void DrawControlButtons()
    {
        if (ImGui.Button(hunt.IsGuiding ? $"Stop##{Id}Tab" : $"Start##{Id}Tab"))
        {
            hunt.Toggle();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Only one guided hunt steers the map flag at a time — starting this one stops the other.");
        }

        ImGui.SameLine();
        if (ImGui.Button($"Reset checks##{Id}Tab"))
        {
            hunt.ResetChecks();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Put every spot back to unchecked and start the session over.");
        }

        DrawExtraControls();
    }

    /// <summary>Buttons only one hunt has, drawn on the same row as Start and Reset.</summary>
    protected virtual void DrawExtraControls()
    {
    }

    /// <summary>The session line: how much of the zone has been covered, and what it has yielded.</summary>
    protected abstract void DrawSession();

    /// <summary>What the numbers add up to — the part that tells the player what to do next.</summary>
    protected abstract void DrawSummary();

    #endregion

    #region Target

    /// <summary>What to run at next, in the terms a player running it needs: distance, which way, and how safe.</summary>
    private void DrawTarget()
    {
        ImGui.TextUnformatted("Next:");

        TSpot? target = hunt.Target;
        if (target == null)
        {
            ImGui.TextColored(
                GuidedSummary.Warning,
                hunt.IsGuiding ? "Nothing left worth walking to." : "Not guiding — press Start to pick a target.");

            return;
        }

        if (ImGui.Button($"Flag##{Id}TabTarget"))
        {
            hunt.PlaceFlag(target);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Put the map flag on this spot.");
        }

        Vector3 from = hunt.PlayerPosition;
        bool elsewhere = hunt.IsElsewhere(target);

        ImGui.SameLine();
        ImGui.TextColored(target.GetColor(), target.Label);
        ImGui.SameLine();

        // The straight-line distance to a spot on another map runs through the world, so quoting it would send the
        // player at a wall. What they need instead is the name of the place they have to get to.
        if (elsewhere)
        {
            ImGui.TextColored(GuidedSummary.Warning, $"— in {target.Area ?? "another part of the zone"}");
        }
        else
        {
            ImGui.TextUnformatted($"— {target.DistanceTo(from):f0}y {target.BearingFrom(from)}");
        }

        if (target.Level > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(LevelColor(target), $"(lv{target.Level})");
        }

        DrawApproachHint(target, from, elsewhere);

        if (!elsewhere)
        {
            DrawObservationProgress(target, from);
        }
    }

    /// <summary>Anything else worth saying about getting to the target, such as a shard that lands closer than you are.</summary>
    protected virtual void DrawApproachHint(TSpot target, Vector3 from, bool elsewhere)
    {
    }

    /// <summary>
    ///     Closes the loop on the approach: the bar fills as the player walks in, and the spot marks itself the instant
    ///     it is full, which is what lets them turn away early instead of running the last few yards to be sure.
    /// </summary>
    private void DrawObservationProgress(TSpot target, Vector3 from)
    {
        float range = config.ObservationRange;
        float distance = target.DistanceTo(from);
        if (distance >= range * 4f)
        {
            return;
        }

        float fraction = Math.Clamp(1f - (distance - range) / (range * 3f), 0f, 1f);
        ImGui.ProgressBar(fraction, new Vector2(-1f, 0f));

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                $"Fills as you approach. The spot marks itself empty once you are within {range:f0}y — or sooner, if something there comes into view.");
        }
    }

    #endregion

    #region Table

    private void DrawFilters()
    {
        bool dirty = false;

        ImGui.SetNextItemWidth(120f);
        if (ImGui.BeginCombo($"Sort##{Id}", SortLabel(sort)))
        {
            foreach(SortMode mode in Enum.GetValues<SortMode>())
            {
                if (mode == SortMode.Kind && KindLabel == null)
                {
                    continue;
                }

                if (ImGui.Selectable(SortLabel(mode), mode == sort))
                {
                    sort = mode;
                }
            }

            ImGui.EndCombo();
        }

        bool hideResolved = config.HideResolvedSpots;
        if (ImGui.Checkbox($"Hide finished##{Id}", ref hideResolved))
        {
            config.HideResolvedSpots = hideResolved;
            dirty = true;
        }

        ImGui.SameLine();
        bool hideAboveLevel = config.HideSpotsAboveMyLevel;
        if (ImGui.Checkbox($"Hide above my level##{Id}", ref hideAboveLevel))
        {
            config.HideSpotsAboveMyLevel = hideAboveLevel;
            dirty = true;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"Drops spots ringed by mobs at or above your Knowledge Level ({hunt.KnowledgeLevel()}).");
        }

        bool autoFlag = config.AutoFlagNextTarget;
        if (ImGui.Checkbox($"Move flag automatically##{Id}", ref autoFlag))
        {
            config.AutoFlagNextTarget = autoFlag;
            dirty = true;
        }

        if (dirty)
        {
            saver.Save();
        }
    }

    private void DrawTable()
    {
        List<TSpot> rows = Rows();
        if (rows.Count == 0)
        {
            ImGui.TextUnformatted("Nothing to show with the current filters.");
            return;
        }

        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg
                                      | ImGuiTableFlags.BordersInnerH
                                      | ImGuiTableFlags.ScrollY
                                      | ImGuiTableFlags.SizingFixedFit;

        if (!ImGui.BeginTable($"GuidedSpots{Id}##BOCCHI", 6, flags, new(0, ImGui.GetContentRegionAvail().Y)))
        {
            return;
        }

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 32f);
        ImGui.TableSetupColumn("Spot", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 76f);
        ImGui.TableSetupColumn("Distance", ImGuiTableColumnFlags.WidthFixed, 86f);
        ImGui.TableSetupColumn("Lv", ImGuiTableColumnFlags.WidthFixed, 40f);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableHeadersRow();

        Vector3 from = hunt.PlayerPosition;
        foreach(TSpot spot in rows)
        {
            DrawRow(spot, from);
        }

        ImGui.EndTable();
    }

    private void DrawRow(TSpot spot, Vector3 from)
    {
        bool isTarget = ReferenceEquals(spot, hunt.Target);
        bool skipped = hunt.IsSkipped(spot);

        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        int? position = hunt.PlanPosition(spot);
        ImGui.TextColored(GuidedSummary.Muted, position?.ToString() ?? "—");

        ImGui.TableNextColumn();
        if (isTarget)
        {
            ImGui.TextColored(GuidedSummary.Warning, $"> {spot.Label}");
        }
        else if (skipped)
        {
            ImGui.TextColored(GuidedSummary.Muted, $"({spot.Label})");
        }
        else if (spot.IsResolved)
        {
            // Kept in the table but dimmed: seeing a spot resolve is how you know the automatic check is working.
            ImGui.TextColored(GuidedSummary.Muted, spot.Label);
        }
        else
        {
            ImGui.TextColored(spot.GetColor(), spot.Label);
        }

        if (ImGui.IsItemHovered())
        {
            string where = spot.Area == null ? string.Empty : $"\n{spot.Area}";
            ImGui.SetTooltip($"{spot.Position.X:f1}, {spot.Position.Y:f1}, {spot.Position.Z:f1}\n{spot.BearingFrom(from)}{where}");
        }

        ImGui.TableNextColumn();
        (string statusLabel, Vector4 statusColor) = Status(spot);
        ImGui.TextColored(statusColor, statusLabel);

        if (spot.ObservedAt != null && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"Checked {GuidedSummary.Age(spot.ObservedAt)} ago.");
        }

        ImGui.TableNextColumn();
        if (hunt.IsElsewhere(spot))
        {
            ImGui.TextColored(GuidedSummary.Muted, spot.Area ?? "elsewhere");
        }
        else
        {
            float distance = spot.DistanceTo(from);
            bool inRange = distance <= config.ObservationRange;
            ImGui.TextColored(inRange ? GuidedSummary.Good : GuidedSummary.Muted, $"{distance:f0}y {spot.BearingFrom(from)}");
        }

        ImGui.TableNextColumn();
        if (spot.Level > 0)
        {
            ImGui.TextColored(LevelColor(spot), spot.Level.ToString());
        }
        else
        {
            ImGui.TextColored(GuidedSummary.Muted, "—");
        }

        ImGui.TableNextColumn();
        if (ImGui.SmallButton($"Flag##{Id}{spot.Id}"))
        {
            hunt.SetTarget(spot);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton($"{(skipped ? "Keep" : "Skip")}##{Id}Skip{spot.Id}"))
        {
            hunt.ToggleSkip(spot);
        }
    }

    /// <summary>What a status reads as in the table. The words differ per hunt — an empty coffer spot and an empty carrot spot mean different things.</summary>
    protected abstract (string Label, Vector4 Color) Status(TSpot spot);

    private Vector4 LevelColor(TSpot spot)
    {
        uint level = hunt.KnowledgeLevel();
        if (level == 0 || spot.Level == 0)
        {
            return GuidedSummary.Muted;
        }

        return spot.Level > level ? new Vector4(0.9f, 0.35f, 0.35f, 1f) : GuidedSummary.Muted;
    }

    private List<TSpot> Rows()
    {
        Vector3 from = hunt.PlayerPosition;

        IEnumerable<TSpot> rows = hunt.Tracker.Spots.Where(spot =>
        {
            if (config.HideResolvedSpots && spot.IsResolved)
            {
                return false;
            }

            return !hunt.IsAboveMyLevel(spot);
        });

        // Spots on another map sort below the ones on this one wherever distance is the tie-breaker, because their
        // distance is a straight line through the world rather than anything you could walk.
        int Elsewhere(TSpot spot) => hunt.IsElsewhere(spot) ? 1 : 0;

        return sort switch
        {
            SortMode.Distance => rows.OrderBy(Elsewhere).ThenBy(s => s.DistanceTo(from)).ToList(),
            SortMode.Status => rows.OrderBy(s => s.Status).ThenBy(Elsewhere).ThenBy(s => s.DistanceTo(from)).ToList(),
            SortMode.Kind => rows.OrderBy(KindOf).ThenBy(Elsewhere).ThenBy(s => s.DistanceTo(from)).ToList(),
            // Spots outside the plan — resolved or skipped ones the filters are letting through — sort to the bottom.
            var _ => rows.OrderBy(s => hunt.PlanPosition(s) ?? int.MaxValue).ThenBy(s => s.DistanceTo(from)).ToList()
        };
    }

    /// <summary>Name of the kind this hunt's spots come in, or null when they are all alike and the sort is not offered.</summary>
    protected virtual string? KindLabel => null;

    /// <summary>Sort key behind <see cref="KindLabel" />.</summary>
    protected virtual int KindOf(TSpot spot) => 0;

    private string SortLabel(SortMode mode) => mode == SortMode.Kind ? KindLabel ?? "Kind" : mode.ToString();

    #endregion
}
