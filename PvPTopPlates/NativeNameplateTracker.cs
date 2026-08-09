using System;
using System.Collections.Generic;
using Dalamud.Game.Gui.NamePlate;

namespace PvPTopPlates;

internal sealed class NativeNameplateTracker
{
    private const long SnapshotLifetimeMilliseconds = 1_000;
    private const uint InvalidEntityId = 0xE000_0000;
    private readonly HashSet<ulong> activeGameObjectIds = new();
    private readonly HashSet<uint> activeEntityIds = new();

    public IReadOnlySet<ulong> ActiveGameObjectIds => activeGameObjectIds;

    public long LastUpdateTick { get; private set; }

    public bool HasCurrentSnapshot =>
        LastUpdateTick != 0 &&
        Environment.TickCount64 - LastUpdateTick <= SnapshotLifetimeMilliseconds;

    public bool Contains(ulong gameObjectId, uint entityId)
    {
        return activeGameObjectIds.Contains(gameObjectId) ||
               activeEntityIds.Contains(entityId);
    }

    public void Update(
        INamePlateUpdateContext context,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        activeGameObjectIds.Clear();
        activeEntityIds.Clear();

        foreach (var handler in handlers)
        {
            if (handler.GameObjectId == 0 ||
                handler.GameObjectId == InvalidEntityId)
                continue;

            activeGameObjectIds.Add(handler.GameObjectId);

            var entityId = handler.GameObject?.EntityId ??
                           (uint)(handler.GameObjectId & uint.MaxValue);
            if (entityId != 0 && entityId != InvalidEntityId)
                activeEntityIds.Add(entityId);
        }

        LastUpdateTick = Environment.TickCount64;
    }
}
