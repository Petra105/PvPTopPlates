using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility;

namespace PvPTopPlates;

internal sealed class OverlayRenderer
{
    private const uint GuardStatusId = 3054;
    private const float GuardDurationSeconds = 4f;
    private const float GuardRecastSeconds = 30f;

    private readonly Configuration configuration;
    private readonly NativeNameplateTracker nameplateTracker;
    private readonly List<BarCandidate> candidates = new(64);
    private readonly Dictionary<ulong, PositionState> positionStates = new(64);
    private readonly Dictionary<ulong, GuardTrackingState> guardTrackingStates = new(64);
    private readonly List<ulong> stalePositionIds = new(64);
    private readonly List<ulong> staleGuardIds = new(64);
    private uint frameNumber;

    public OverlayRenderer(
        Configuration configuration,
        NativeNameplateTracker nameplateTracker)
    {
        this.configuration = configuration;
        this.nameplateTracker = nameplateTracker;
    }

    public void Draw()
    {
        if (!ShouldDraw())
        {
            positionStates.Clear();
            if (!Plugin.ClientState.IsPvP)
                guardTrackingStates.Clear();

            return;
        }

        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer is null || !localPlayer.IsValid())
        {
            positionStates.Clear();
            return;
        }

        candidates.Clear();
        AdvanceFrame();

        var uiScale = Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        var displaySize = ImGui.GetIO().DisplaySize;
        var deltaTime = GetFrameDeltaTime();
        var currentTick = Environment.TickCount64;
        var currentTargetId = Plugin.TargetManager.Target?.GameObjectId ?? 0;
        var requireNativePlate =
            configuration.RequireNativeNameplatePresence &&
            nameplateTracker.HasCurrentSnapshot;

        foreach (var actor in Plugin.ObjectTable.PlayerObjects)
        {
            if (!TryCreateCandidate(
                    actor,
                    localPlayer,
                    currentTargetId,
                    requireNativePlate,
                    uiScale,
                    displaySize,
                    deltaTime,
                    currentTick,
                    out var candidate))
            {
                continue;
            }

            candidates.Add(candidate);
        }

        PruneTrackingStates();

        candidates.Sort(static (left, right) =>
        {
            if (left.IsCurrentTarget != right.IsCurrentTarget)
                return left.IsCurrentTarget ? 1 : -1;

            return right.Distance.CompareTo(left.Distance);
        });

        var drawList = ImGui.GetForegroundDrawList();
        foreach (var candidate in candidates)
            DrawBar(drawList, candidate, uiScale);
    }

    private bool ShouldDraw()
    {
        if (!configuration.Enabled || !Plugin.ClientState.IsLoggedIn)
            return false;

        if (!Plugin.ClientState.IsPvP && !configuration.DrawOutsidePvPForPositioning)
            return false;

        if (configuration.HideWhenGameUiIsHidden && Plugin.GameGui.GameUiHidden)
            return false;

        return true;
    }

    private bool TryCreateCandidate(
        IBattleChara actor,
        IBattleChara localPlayer,
        ulong currentTargetId,
        bool requireNativePlate,
        float uiScale,
        Vector2 displaySize,
        float deltaTime,
        long currentTick,
        out BarCandidate candidate)
    {
        candidate = default;

        if (!actor.IsValid() || actor.GameObjectId == 0)
            return false;

        var guardState = UpdateGuardState(actor, currentTick);

        var relation = GetRelation(actor, localPlayer.GameObjectId);
        if (!ShouldShowRelation(relation))
            return false;

        if (relation != PlayerRelation.LocalPlayer && !actor.IsTargetable)
            return false;

        if (actor.IsDead || actor.MaxHp == 0 || actor.CurrentHp == 0)
            return false;

        if (requireNativePlate &&
            relation != PlayerRelation.LocalPlayer &&
            !nameplateTracker.ActiveGameObjectIds.Contains(actor.GameObjectId))
        {
            return false;
        }

        var distance = Vector3.Distance(localPlayer.Position, actor.Position);
        if (distance > configuration.MaximumDistance)
            return false;

        var worldAnchor = actor.Position + (Vector3.UnitY * configuration.WorldHeight);
        if (!Plugin.GameGui.WorldToScreen(
                worldAnchor,
                out var screenPosition,
                out var isInViewport) ||
            !isInViewport)
        {
            return false;
        }

        screenPosition.Y += configuration.ScreenOffsetY * uiScale;
        screenPosition = StabilizePosition(
            actor.GameObjectId,
            screenPosition,
            uiScale,
            deltaTime);

        var halfWidth = configuration.BarWidth * uiScale * 0.5f;
        var borderThickness = configuration.BorderThickness * uiScale;
        var rightExtent = halfWidth + borderThickness;
        if (configuration.ShowGuardStateSymbol)
        {
            rightExtent +=
                (configuration.GuardSymbolSpacing * uiScale) +
                (configuration.GuardSymbolSize * uiScale);
        }

        var stackBottom = screenPosition.Y +
                          (configuration.BarHeight * uiScale) +
                          borderThickness;
        if (configuration.ShowMpBar && actor.MaxMp > 0)
        {
            stackBottom +=
                (configuration.MpBarSpacing * uiScale) +
                (borderThickness * 2f) +
                (configuration.MpBarHeight * uiScale);
        }

        if (screenPosition.X + rightExtent < 0 ||
            screenPosition.X - halfWidth > displaySize.X ||
            stackBottom < 0 ||
            screenPosition.Y - borderThickness > displaySize.Y)
        {
            return false;
        }

        candidate = new BarCandidate(
            actor.Name.TextValue,
            actor.CurrentHp,
            actor.MaxHp,
            actor.CurrentMp,
            actor.MaxMp,
            actor.ShieldPercentage,
            screenPosition,
            distance,
            relation,
            guardState,
            actor.GameObjectId == currentTargetId);

        return true;
    }

    private bool ShouldShowRelation(PlayerRelation relation)
    {
        return relation switch
        {
            PlayerRelation.Enemy => configuration.ShowEnemies,
            PlayerRelation.Party => configuration.ShowPartyMembers,
            PlayerRelation.Alliance => configuration.ShowAllianceMembers,
            PlayerRelation.LocalPlayer => configuration.ShowLocalPlayer,
            _ => configuration.ShowOtherFriendlies,
        };
    }

    private static PlayerRelation GetRelation(IBattleChara actor, ulong localPlayerId)
    {
        if (actor.GameObjectId == localPlayerId)
            return PlayerRelation.LocalPlayer;

        if (actor.StatusFlags.HasFlag(StatusFlags.Hostile))
            return PlayerRelation.Enemy;

        if (actor.StatusFlags.HasFlag(StatusFlags.PartyMember))
            return PlayerRelation.Party;

        if (actor.StatusFlags.HasFlag(StatusFlags.AllianceMember))
            return PlayerRelation.Alliance;

        return PlayerRelation.OtherFriendly;
    }

    private void DrawBar(ImDrawListPtr drawList, BarCandidate candidate, float uiScale)
    {
        var width = configuration.BarWidth * uiScale;
        var height = configuration.BarHeight * uiScale;
        var halfWidth = width * 0.5f;
        var borderThickness = configuration.BorderThickness * uiScale;
        var rounding = configuration.CornerRounding * uiScale;

        var barMinimum = new Vector2(
            candidate.ScreenPosition.X - halfWidth,
            candidate.ScreenPosition.Y);
        var barMaximum = barMinimum + new Vector2(width, height);
        var borderOffset = new Vector2(borderThickness);
        var borderMinimum = barMinimum - borderOffset;
        var borderMaximum = barMaximum + borderOffset;

        var healthRatio = Math.Clamp(
            (float)candidate.CurrentHp / candidate.MaximumHp,
            0f,
            1f);
        DrawFilledBar(
            drawList,
            barMinimum,
            barMaximum,
            healthRatio,
            GetRelationColor(candidate.Relation),
            configuration.EmptyHealthColor,
            borderThickness,
            rounding);

        if (configuration.ShowShields && candidate.ShieldPercentage > 0)
        {
            var shieldRatio = Math.Clamp(candidate.ShieldPercentage / 100f, 0f, 1f);
            var shieldMaximum = new Vector2(
                barMinimum.X + (width * shieldRatio),
                barMaximum.Y);

            drawList.AddRectFilled(
                barMinimum,
                shieldMaximum,
                ToColor(configuration.ShieldColor),
                rounding);
        }

        var stackBorderMinimum = borderMinimum;
        var stackBorderMaximum = borderMaximum;
        if (configuration.ShowMpBar && candidate.MaximumMp > 0)
        {
            var mpHeight = configuration.MpBarHeight * uiScale;
            var mpSpacing = configuration.MpBarSpacing * uiScale;
            var mpMinimum = new Vector2(
                barMinimum.X,
                borderMaximum.Y + mpSpacing + borderThickness);
            var mpMaximum = mpMinimum + new Vector2(width, mpHeight);
            var mpRatio = Math.Clamp(
                (float)candidate.CurrentMp / candidate.MaximumMp,
                0f,
                1f);

            DrawFilledBar(
                drawList,
                mpMinimum,
                mpMaximum,
                mpRatio,
                configuration.MpColor,
                configuration.EmptyMpColor,
                borderThickness,
                rounding);

            stackBorderMaximum = mpMaximum + borderOffset;
        }

        if (configuration.ShowGuardStateSymbol)
        {
            var symbolSize = Math.Max(6f, configuration.GuardSymbolSize * uiScale);
            var symbolSpacing = Math.Max(0f, configuration.GuardSymbolSpacing * uiScale);
            var symbolCenter = new Vector2(
                borderMaximum.X + symbolSpacing + (symbolSize * 0.5f),
                barMinimum.Y + (height * 0.5f));
            DrawGuardSymbol(
                drawList,
                symbolCenter,
                symbolSize,
                candidate.GuardState,
                uiScale);
        }

        if (configuration.HighlightCurrentTarget && candidate.IsCurrentTarget)
        {
            var targetOffset = new Vector2(1.5f * uiScale);
            drawList.AddRect(
                stackBorderMinimum - targetOffset,
                stackBorderMaximum + targetOffset,
                ToColor(configuration.TargetOutlineColor),
                rounding + borderThickness + targetOffset.X,
                ImDrawFlags.None,
                2f * uiScale);
        }

        if (configuration.ShowHpPercent)
        {
            var hpText = $"{MathF.Round(healthRatio * 100f):0}%";
            var textSize = ImGui.CalcTextSize(hpText);
            var textPosition = new Vector2(
                candidate.ScreenPosition.X - (textSize.X * 0.5f),
                barMinimum.Y + ((height - textSize.Y) * 0.5f));
            DrawShadowedText(drawList, textPosition, hpText, uiScale);
        }

        if (configuration.ShowNames)
        {
            var textSize = ImGui.CalcTextSize(candidate.Name);
            var textPosition = new Vector2(
                candidate.ScreenPosition.X - (textSize.X * 0.5f),
                borderMinimum.Y - textSize.Y - (2f * uiScale));
            DrawShadowedText(drawList, textPosition, candidate.Name, uiScale);
        }
    }

    private void DrawFilledBar(
        ImDrawListPtr drawList,
        Vector2 minimum,
        Vector2 maximum,
        float fillRatio,
        Vector4 filledColor,
        Vector4 emptyColor,
        float borderThickness,
        float rounding)
    {
        var borderOffset = new Vector2(borderThickness);
        drawList.AddRectFilled(
            minimum - borderOffset,
            maximum + borderOffset,
            ToColor(configuration.BorderColor),
            rounding + borderThickness);
        drawList.AddRectFilled(
            minimum,
            maximum,
            ToColor(emptyColor),
            rounding);

        var fillMaximum = new Vector2(
            minimum.X + ((maximum.X - minimum.X) * fillRatio),
            maximum.Y);
        if (fillMaximum.X > minimum.X)
        {
            drawList.AddRectFilled(
                minimum,
                fillMaximum,
                ToColor(filledColor),
                rounding);
        }
    }

    private void DrawGuardSymbol(
        ImDrawListPtr drawList,
        Vector2 center,
        float size,
        GuardState state,
        float uiScale)
    {
        var halfSize = size * 0.5f;
        var top = center.Y - halfSize;
        var left = center.X - (halfSize * 0.78f);
        var right = center.X + (halfSize * 0.78f);
        var shoulder = top + (size * 0.18f);
        var lowerSide = top + (size * 0.62f);
        var bottom = top + size;

        var topLeft = new Vector2(left, shoulder);
        var topRight = new Vector2(right, shoulder);
        var lowerRight = new Vector2(right - (size * 0.08f), lowerSide);
        var tip = new Vector2(center.X, bottom);
        var lowerLeft = new Vector2(left + (size * 0.08f), lowerSide);

        var stateColor = state switch
        {
            GuardState.Active => configuration.GuardActiveColor,
            GuardState.Cooldown => configuration.GuardCooldownColor,
            _ => configuration.GuardReadyColor,
        };
        var fillColor = state == GuardState.Active
            ? stateColor
            : configuration.EmptyHealthColor;
        var fill = ToColor(fillColor);
        var outline = ToColor(stateColor);

        drawList.AddTriangleFilled(center, topLeft, topRight, fill);
        drawList.AddTriangleFilled(center, topRight, lowerRight, fill);
        drawList.AddTriangleFilled(center, lowerRight, tip, fill);
        drawList.AddTriangleFilled(center, tip, lowerLeft, fill);
        drawList.AddTriangleFilled(center, lowerLeft, topLeft, fill);

        var outlineThickness = Math.Max(1f, 1.5f * uiScale);
        drawList.AddLine(topLeft, topRight, outline, outlineThickness);
        drawList.AddLine(topRight, lowerRight, outline, outlineThickness);
        drawList.AddLine(lowerRight, tip, outline, outlineThickness);
        drawList.AddLine(tip, lowerLeft, outline, outlineThickness);
        drawList.AddLine(lowerLeft, topLeft, outline, outlineThickness);

        if (state == GuardState.Ready)
        {
            var checkStart = new Vector2(
                center.X - (size * 0.23f),
                center.Y + (size * 0.02f));
            var checkMiddle = new Vector2(
                center.X - (size * 0.05f),
                center.Y + (size * 0.20f));
            var checkEnd = new Vector2(
                center.X + (size * 0.26f),
                center.Y - (size * 0.18f));
            drawList.AddLine(checkStart, checkMiddle, outline, outlineThickness);
            drawList.AddLine(checkMiddle, checkEnd, outline, outlineThickness);
        }
        else if (state == GuardState.Active)
        {
            drawList.AddCircleFilled(
                center,
                Math.Max(1.5f, size * 0.11f),
                ToColor(configuration.TextColor));
        }
        else
        {
            var slashStart = new Vector2(
                center.X - (size * 0.25f),
                center.Y + (size * 0.25f));
            var slashEnd = new Vector2(
                center.X + (size * 0.25f),
                center.Y - (size * 0.25f));
            drawList.AddLine(slashStart, slashEnd, outline, outlineThickness);
        }
    }

    private GuardState UpdateGuardState(IBattleChara actor, long currentTick)
    {
        guardTrackingStates.TryGetValue(actor.GameObjectId, out var tracked);

        var isDead = actor.IsDead || actor.CurrentHp == 0;
        if (isDead)
        {
            guardTrackingStates[actor.GameObjectId] = new GuardTrackingState(
                false,
                true,
                0,
                frameNumber);
            return GuardState.Ready;
        }

        var cooldownEndsAt = tracked.WasDead ? 0 : tracked.CooldownEndsAt;
        var isActive = TryGetGuardRemainingTime(actor, out var remainingTime);
        if (isActive && !tracked.WasActive)
        {
            var observedAge = GuardDurationSeconds - Math.Clamp(
                remainingTime,
                0f,
                GuardDurationSeconds);
            var remainingRecast = Math.Max(
                0f,
                GuardRecastSeconds - observedAge);
            cooldownEndsAt = currentTick + (long)MathF.Round(remainingRecast * 1_000f);
        }

        var state = isActive
            ? GuardState.Active
            : currentTick < cooldownEndsAt
                ? GuardState.Cooldown
                : GuardState.Ready;

        guardTrackingStates[actor.GameObjectId] = new GuardTrackingState(
            isActive,
            false,
            cooldownEndsAt,
            frameNumber);
        return state;
    }

    private static bool TryGetGuardRemainingTime(
        IBattleChara actor,
        out float remainingTime)
    {
        foreach (var status in actor.StatusList)
        {
            if (status.StatusId != GuardStatusId)
                continue;

            remainingTime = float.IsFinite(status.RemainingTime)
                ? status.RemainingTime
                : GuardDurationSeconds;
            return true;
        }

        remainingTime = 0f;
        return false;
    }

    private Vector2 StabilizePosition(
        ulong actorId,
        Vector2 rawPosition,
        float uiScale,
        float deltaTime)
    {
        if (!configuration.StabilizePositions)
            return rawPosition;

        var result = rawPosition;
        if (positionStates.TryGetValue(actorId, out var state) &&
            frameNumber - state.LastSeenFrame <= 5)
        {
            var delta = rawPosition - state.Position;
            var distance = delta.Length();
            var deadZone = Math.Max(
                0f,
                configuration.StabilizationDeadZone * uiScale);
            var snapDistance = Math.Max(
                deadZone,
                configuration.StabilizationSnapDistance * uiScale);

            if (float.IsFinite(distance) && distance <= deadZone)
            {
                result = state.Position;
            }
            else if (float.IsFinite(distance) && distance < snapDistance)
            {
                var response = Math.Max(0.01f, configuration.StabilizationResponse);
                var blend = 1f - MathF.Exp(-response * deltaTime);
                result = state.Position + (delta * blend);
            }
        }

        positionStates[actorId] = new PositionState(result, frameNumber);
        return result;
    }

    private void AdvanceFrame()
    {
        frameNumber++;
        if (frameNumber != 0)
            return;

        positionStates.Clear();
        guardTrackingStates.Clear();
        frameNumber = 1;
    }

    private void PruneTrackingStates()
    {
        if (frameNumber % 120 != 0)
            return;

        if (positionStates.Count > 0)
        {
            stalePositionIds.Clear();
            foreach (var (actorId, state) in positionStates)
            {
                if (frameNumber - state.LastSeenFrame > 300)
                    stalePositionIds.Add(actorId);
            }

            foreach (var actorId in stalePositionIds)
                positionStates.Remove(actorId);
        }

        if (guardTrackingStates.Count > 0)
        {
            staleGuardIds.Clear();
            foreach (var (actorId, state) in guardTrackingStates)
            {
                if (frameNumber - state.LastSeenFrame > 300)
                    staleGuardIds.Add(actorId);
            }

            foreach (var actorId in staleGuardIds)
                guardTrackingStates.Remove(actorId);
        }
    }

    private static float GetFrameDeltaTime()
    {
        var deltaTime = ImGui.GetIO().DeltaTime;
        if (!float.IsFinite(deltaTime) || deltaTime <= 0f)
            return 1f / 60f;

        return Math.Min(deltaTime, 0.1f);
    }

    private void DrawShadowedText(
        ImDrawListPtr drawList,
        Vector2 position,
        string text,
        float uiScale)
    {
        drawList.AddText(
            position + new Vector2(uiScale),
            ToColor(new Vector4(0f, 0f, 0f, 0.95f)),
            text);
        drawList.AddText(position, ToColor(configuration.TextColor), text);
    }

    private Vector4 GetRelationColor(PlayerRelation relation)
    {
        return relation switch
        {
            PlayerRelation.Enemy => configuration.EnemyColor,
            PlayerRelation.Party => configuration.PartyColor,
            PlayerRelation.Alliance => configuration.AllianceColor,
            PlayerRelation.LocalPlayer => configuration.LocalPlayerColor,
            _ => configuration.FriendlyColor,
        };
    }

    private static uint ToColor(Vector4 color)
    {
        return ImGui.ColorConvertFloat4ToU32(color);
    }

    private enum PlayerRelation
    {
        Enemy,
        Party,
        Alliance,
        LocalPlayer,
        OtherFriendly,
    }

    private enum GuardState
    {
        Ready,
        Active,
        Cooldown,
    }

    private readonly record struct BarCandidate(
        string Name,
        uint CurrentHp,
        uint MaximumHp,
        uint CurrentMp,
        uint MaximumMp,
        byte ShieldPercentage,
        Vector2 ScreenPosition,
        float Distance,
        PlayerRelation Relation,
        GuardState GuardState,
        bool IsCurrentTarget);

    private readonly record struct PositionState(
        Vector2 Position,
        uint LastSeenFrame);

    private readonly record struct GuardTrackingState(
        bool WasActive,
        bool WasDead,
        long CooldownEndsAt,
        uint LastSeenFrame);
}
