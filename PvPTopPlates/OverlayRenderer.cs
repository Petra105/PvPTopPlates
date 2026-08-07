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
    private readonly Configuration configuration;
    private readonly NativeNameplateTracker nameplateTracker;
    private readonly List<BarCandidate> candidates = new(64);

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
            return;

        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer is null || !localPlayer.IsValid())
            return;

        candidates.Clear();

        var uiScale = Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        var displaySize = ImGui.GetIO().DisplaySize;
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
                    out var candidate))
            {
                continue;
            }

            candidates.Add(candidate);
        }

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
        out BarCandidate candidate)
    {
        candidate = default;

        if (!actor.IsValid() || actor.GameObjectId == 0)
            return false;

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

        var halfWidth = configuration.BarWidth * uiScale * 0.5f;
        var height = configuration.BarHeight * uiScale;
        if (screenPosition.X + halfWidth < 0 ||
            screenPosition.X - halfWidth > displaySize.X ||
            screenPosition.Y + height < 0 ||
            screenPosition.Y > displaySize.Y)
        {
            return false;
        }

        candidate = new BarCandidate(
            actor.Name.TextValue,
            actor.CurrentHp,
            actor.MaxHp,
            actor.ShieldPercentage,
            screenPosition,
            distance,
            relation,
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

        drawList.AddRectFilled(
            borderMinimum,
            borderMaximum,
            ToColor(configuration.BorderColor),
            rounding + borderThickness);
        drawList.AddRectFilled(
            barMinimum,
            barMaximum,
            ToColor(configuration.EmptyHealthColor),
            rounding);

        var healthRatio = Math.Clamp(
            (float)candidate.CurrentHp / candidate.MaximumHp,
            0f,
            1f);
        var healthMaximum = new Vector2(
            barMinimum.X + (width * healthRatio),
            barMaximum.Y);

        if (healthMaximum.X > barMinimum.X)
        {
            drawList.AddRectFilled(
                barMinimum,
                healthMaximum,
                ToColor(GetRelationColor(candidate.Relation)),
                rounding);
        }

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

        if (configuration.HighlightCurrentTarget && candidate.IsCurrentTarget)
        {
            var targetOffset = new Vector2(1.5f * uiScale);
            drawList.AddRect(
                borderMinimum - targetOffset,
                borderMaximum + targetOffset,
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

    private readonly record struct BarCandidate(
        string Name,
        uint CurrentHp,
        uint MaximumHp,
        byte ShieldPercentage,
        Vector2 ScreenPosition,
        float Distance,
        PlayerRelation Relation,
        bool IsCurrentTarget);
}

