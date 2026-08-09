using Dalamud.Game.ClientState.Fates;
using Ocelot.Services.Data;
using System.Numerics;
using FateData = Lumina.Excel.Sheets.Fate;

namespace BOCCHI.Common.Data.Fates;

public readonly record struct FateId(ushort Value)
{
    public override string ToString() => Value.ToString();
}

public class Fate(FateId id, IFate context, IDataRepository<FateData> fateDataRepository)
{
    public readonly FateData GameData = fateDataRepository.Get(context.FateId);
    public readonly FateId Id = id;

    public readonly string Name = context.Name.ToString();

    public readonly float Radius = context.Radius;

    public Vector3 Position { get; private set; } = context.Position;

    public FateState State { get; private set; } = context.State;

    public byte Progress { get; private set; } = context.Progress;

    /// <summary>Seconds left on the FATE timer (Dalamud <see cref="IFate.TimeRemaining"/>).</summary>
    public long TimeRemainingSeconds { get; private set; } = context.TimeRemaining;

    /// <summary>Unix epoch seconds of the FATE's real start (Dalamud <see cref="IFate.StartTimeEpoch"/>). 0 until the client publishes one.</summary>
    public int StartTimeEpoch { get; private set; } = context.StartTimeEpoch;

    /// <summary>Full FATE duration in seconds (Dalamud <see cref="IFate.Duration"/>).</summary>
    public short DurationSeconds { get; private set; } = context.Duration;

    /// <summary>The real spawn instant when the client published one, otherwise null.</summary>
    public DateTimeOffset? StartedAt => StartTimeEpoch > 0 ? DateTimeOffset.FromUnixTimeSeconds(StartTimeEpoch) : null;

    public void Update(IFate context)
    {
        Position = context.Position;
        State = context.State;
        Progress = context.Progress;
        TimeRemainingSeconds = context.TimeRemaining;
    }
}
