using System;
using System.Numerics;
using Dalamud.Configuration;

namespace PvPTopPlates;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; }

    public bool Enabled { get; set; } = true;
    public bool DrawOutsidePvPForPositioning { get; set; }
    public bool HideWhenGameUiIsHidden { get; set; } = true;
    public bool RequireNativeNameplatePresence { get; set; } = true;

    public bool ShowEnemies { get; set; } = true;
    public bool ShowPartyMembers { get; set; } = true;
    public bool ShowAllianceMembers { get; set; } = true;
    public bool ShowLocalPlayer { get; set; }
    public bool ShowOtherFriendlies { get; set; }

    public bool ShowNames { get; set; }
    public bool ShowHpPercent { get; set; }
    public bool ShowMpBar { get; set; } = true;
    public bool ShowGuardStateSymbol { get; set; } = true;
    public bool ShowShields { get; set; } = true;
    public bool HighlightCurrentTarget { get; set; } = true;
    public bool StabilizePositions { get; set; } = true;

    public float MaximumDistance { get; set; } = 50f;
    public float WorldHeight { get; set; } = 2.15f;
    public float ScreenOffsetY { get; set; }
    public float BarWidth { get; set; } = 105f;
    public float BarHeight { get; set; } = 11f;
    public float MpBarHeight { get; set; } = 4f;
    public float MpBarSpacing { get; set; } = 2f;
    public float GuardSymbolSize { get; set; } = 16f;
    public float GuardSymbolSpacing { get; set; } = 5f;
    public float BorderThickness { get; set; } = 2f;
    public float CornerRounding { get; set; } = 2f;
    public float StabilizationDeadZone { get; set; } = 0.60f;
    public float StabilizationResponse { get; set; } = 18f;
    public float StabilizationSnapDistance { get; set; } = 32f;

    public Vector4 EnemyColor { get; set; } = new(0.93f, 0.20f, 0.25f, 0.96f);
    public Vector4 PartyColor { get; set; } = new(0.30f, 0.84f, 0.52f, 0.96f);
    public Vector4 AllianceColor { get; set; } = new(0.28f, 0.66f, 0.96f, 0.96f);
    public Vector4 FriendlyColor { get; set; } = new(0.42f, 0.78f, 0.91f, 0.96f);
    public Vector4 LocalPlayerColor { get; set; } = new(0.96f, 0.72f, 0.30f, 0.96f);
    public Vector4 EmptyHealthColor { get; set; } = new(0.035f, 0.035f, 0.045f, 0.88f);
    public Vector4 MpColor { get; set; } = new(0.30f, 0.46f, 0.98f, 0.96f);
    public Vector4 EmptyMpColor { get; set; } = new(0.025f, 0.035f, 0.09f, 0.88f);
    public Vector4 GuardReadyColor { get; set; } = new(0.28f, 0.90f, 0.62f, 1f);
    public Vector4 GuardActiveColor { get; set; } = new(1f, 0.78f, 0.20f, 1f);
    public Vector4 GuardCooldownColor { get; set; } = new(0.42f, 0.44f, 0.50f, 0.92f);
    public Vector4 BorderColor { get; set; } = new(0.01f, 0.01f, 0.015f, 0.98f);
    public Vector4 ShieldColor { get; set; } = new(0.86f, 0.94f, 1f, 0.48f);
    public Vector4 TextColor { get; set; } = new(1f, 1f, 1f, 1f);
    public Vector4 TargetOutlineColor { get; set; } = new(1f, 0.82f, 0.24f, 1f);

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    public void ResetToDefaults()
    {
        var defaults = new Configuration();

        Enabled = defaults.Enabled;
        DrawOutsidePvPForPositioning = defaults.DrawOutsidePvPForPositioning;
        HideWhenGameUiIsHidden = defaults.HideWhenGameUiIsHidden;
        RequireNativeNameplatePresence = defaults.RequireNativeNameplatePresence;

        ShowEnemies = defaults.ShowEnemies;
        ShowPartyMembers = defaults.ShowPartyMembers;
        ShowAllianceMembers = defaults.ShowAllianceMembers;
        ShowLocalPlayer = defaults.ShowLocalPlayer;
        ShowOtherFriendlies = defaults.ShowOtherFriendlies;

        ShowNames = defaults.ShowNames;
        ShowHpPercent = defaults.ShowHpPercent;
        ShowMpBar = defaults.ShowMpBar;
        ShowGuardStateSymbol = defaults.ShowGuardStateSymbol;
        ShowShields = defaults.ShowShields;
        HighlightCurrentTarget = defaults.HighlightCurrentTarget;
        StabilizePositions = defaults.StabilizePositions;

        MaximumDistance = defaults.MaximumDistance;
        WorldHeight = defaults.WorldHeight;
        ScreenOffsetY = defaults.ScreenOffsetY;
        BarWidth = defaults.BarWidth;
        BarHeight = defaults.BarHeight;
        MpBarHeight = defaults.MpBarHeight;
        MpBarSpacing = defaults.MpBarSpacing;
        GuardSymbolSize = defaults.GuardSymbolSize;
        GuardSymbolSpacing = defaults.GuardSymbolSpacing;
        BorderThickness = defaults.BorderThickness;
        CornerRounding = defaults.CornerRounding;
        StabilizationDeadZone = defaults.StabilizationDeadZone;
        StabilizationResponse = defaults.StabilizationResponse;
        StabilizationSnapDistance = defaults.StabilizationSnapDistance;

        EnemyColor = defaults.EnemyColor;
        PartyColor = defaults.PartyColor;
        AllianceColor = defaults.AllianceColor;
        FriendlyColor = defaults.FriendlyColor;
        LocalPlayerColor = defaults.LocalPlayerColor;
        EmptyHealthColor = defaults.EmptyHealthColor;
        MpColor = defaults.MpColor;
        EmptyMpColor = defaults.EmptyMpColor;
        GuardReadyColor = defaults.GuardReadyColor;
        GuardActiveColor = defaults.GuardActiveColor;
        GuardCooldownColor = defaults.GuardCooldownColor;
        BorderColor = defaults.BorderColor;
        ShieldColor = defaults.ShieldColor;
        TextColor = defaults.TextColor;
        TargetOutlineColor = defaults.TargetOutlineColor;
    }
}
