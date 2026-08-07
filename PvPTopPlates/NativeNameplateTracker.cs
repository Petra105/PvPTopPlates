using System;
using System.Collections.Generic;
using Dalamud.Game.Gui.NamePlate;

namespace PvPTopPlates;

internal sealed class NativeNameplateTracker
{
    private const long SnapshotLifetimeMilliseconds = 1_000;
    private readonly HashSet<ulong> activeGameObjectIds = new();

    public IReadOnlySet<ulong> ActiveGameObjectIds => activeGameObjectIds;

    public long LastUpdateTick { get; private set; }

    public bool HasCurrentSnapshot =>
        LastUpdateTick != 0 &&
        Environment.TickCount64 - LastUpdateTick <= SnapshotLifetimeMilliseconds;

    public void Update(
        INamePlateUpdateContext context,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        activeGameObjectIds.Clear();

        foreach (var handler in handlers)
        {
            if (handler.GameObjectId == 0 || handler.BattleChara is null)
                continue;

            activeGameObjectIds.Add(handler.GameObjectId);
        }

        LastUpdateTick = Environment.TickCount64;
    }
}
