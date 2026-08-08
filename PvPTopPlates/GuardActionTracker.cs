using System;
using System.Collections.Concurrent;
using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace PvPTopPlates;

internal sealed class GuardActionTracker : IDisposable
{
    private const uint GuardActionId = 29053;
    private const uint InvalidEntityId = 0xE000_0000;

    private readonly ConcurrentDictionary<uint, GuardUseObservation> observations = new();
    private readonly IPluginLog log;
    private Hook<ReceiveActionEffectDelegate>? receiveActionEffectHook;

    private unsafe delegate void ReceiveActionEffectDelegate(
        uint casterEntityId,
        Character* caster,
        Vector3* targetPosition,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds);

    public GuardActionTracker(
        IGameInteropProvider gameInteropProvider,
        IPluginLog log)
    {
        this.log = log;

        try
        {
            var receiveAddress = ActionEffectHandler.Addresses.Receive.Value;
            if (receiveAddress == nint.Zero)
            {
                log.Warning(
                    "Enemy Guard action tracking address is unavailable; status-based tracking remains available.");
                return;
            }

            receiveActionEffectHook =
                gameInteropProvider.HookFromAddress<ReceiveActionEffectDelegate>(
                    receiveAddress,
                    OnReceiveActionEffect);
            receiveActionEffectHook.Enable();
        }
        catch (Exception exception)
        {
            receiveActionEffectHook?.Dispose();
            receiveActionEffectHook = null;
            log.Warning(
                exception,
                "Enemy Guard action tracking could not be initialized; status-based tracking remains available.");
        }
    }

    public bool TryGetLatestUse(uint entityId, out long observedAtTick)
    {
        if (observations.TryGetValue(entityId, out var observation))
        {
            observedAtTick = observation.ObservedAtTick;
            return true;
        }

        observedAtTick = 0;
        return false;
    }

    public void Clear()
    {
        observations.Clear();
    }

    public void Dispose()
    {
        receiveActionEffectHook?.Disable();
        receiveActionEffectHook?.Dispose();
        receiveActionEffectHook = null;
        observations.Clear();
    }

    private unsafe void OnReceiveActionEffect(
        uint casterEntityId,
        Character* caster,
        Vector3* targetPosition,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        try
        {
            if (header != null &&
                header->ActionId == GuardActionId &&
                casterEntityId != 0 &&
                casterEntityId != InvalidEntityId)
            {
                var observation = new GuardUseObservation(
                    header->GlobalSequence,
                    Environment.TickCount64);
                observations.AddOrUpdate(
                    casterEntityId,
                    observation,
                    (_, previous) =>
                        previous.GlobalSequence == observation.GlobalSequence
                            ? previous
                            : observation);
            }
        }
        catch (Exception exception)
        {
            log.Error(exception, "Failed to observe a Guard action effect.");
        }

        receiveActionEffectHook!.Original(
            casterEntityId,
            caster,
            targetPosition,
            header,
            effects,
            targetEntityIds);
    }

    private readonly record struct GuardUseObservation(
        uint GlobalSequence,
        long ObservedAtTick);
}
