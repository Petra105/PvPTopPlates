using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace PvPTopPlates;

internal sealed class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Configuration configuration)
        : base(
            "PvP TopPlates Settings###PvPTopPlatesSettings",
            ImGuiWindowFlags.NoCollapse)
    {
        this.configuration = configuration;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(440f, 520f),
            MaximumSize = new Vector2(900f, 1_200f),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var changed = false;

        var statusText = Plugin.ClientState.IsPvP
            ? "PvP detected; overlay can render."
            : configuration.DrawOutsidePvPForPositioning
                ? "Positioning mode is active outside PvP."
                : "Waiting for PvP; enable positioning mode to test outside a match.";
        var statusColor = Plugin.ClientState.IsPvP
            ? new Vector4(0.36f, 0.90f, 0.56f, 1f)
            : new Vector4(0.95f, 0.75f, 0.32f, 1f);

        ImGui.TextColored(statusColor, statusText);
        ImGui.TextWrapped(
            "Bars are drawn through Dalamud's foreground UI layer. World geometry cannot cover them.");

        DrawSectionHeader("General");
        changed |= DrawCheckbox(
            "Enabled",
            configuration.Enabled,
            value => configuration.Enabled = value);
        changed |= DrawCheckbox(
            "Allow drawing outside PvP for positioning",
            configuration.DrawOutsidePvPForPositioning,
            value => configuration.DrawOutsidePvPForPositioning = value);
        HelpMarker(
            "Temporarily allows nearby player bars outside PvP so placement can be calibrated.");

        changed |= DrawCheckbox(
            "Hide when the game UI is hidden",
            configuration.HideWhenGameUiIsHidden,
            value => configuration.HideWhenGameUiIsHidden = value);
        changed |= DrawCheckbox(
            "Require an active native nameplate",
            configuration.RequireNativeNameplatePresence,
            value => configuration.RequireNativeNameplatePresence = value);
        HelpMarker(
            "Recommended. Limits the overlay to actors for which the game currently maintains a nameplate.");

        DrawSectionHeader("Players");
        changed |= DrawCheckbox(
            "Enemies",
            configuration.ShowEnemies,
            value => configuration.ShowEnemies = value);
        changed |= DrawCheckbox(
            "Party members",
            configuration.ShowPartyMembers,
            value => configuration.ShowPartyMembers = value);
        changed |= DrawCheckbox(
            "Alliance members",
            configuration.ShowAllianceMembers,
            value => configuration.ShowAllianceMembers = value);
        changed |= DrawCheckbox(
            "Local player",
            configuration.ShowLocalPlayer,
            value => configuration.ShowLocalPlayer = value);
        changed |= DrawCheckbox(
            "Other friendly players",
            configuration.ShowOtherFriendlies,
            value => configuration.ShowOtherFriendlies = value);

        DrawSectionHeader("Contents");
        changed |= DrawCheckbox(
            "Names",
            configuration.ShowNames,
            value => configuration.ShowNames = value);
        changed |= DrawCheckbox(
            "HP percentage",
            configuration.ShowHpPercent,
            value => configuration.ShowHpPercent = value);
        changed |= DrawCheckbox(
            "MP bar",
            configuration.ShowMpBar,
            value => configuration.ShowMpBar = value);
        changed |= DrawCheckbox(
            "Guard state symbol",
            configuration.ShowGuardStateSymbol,
            value => configuration.ShowGuardStateSymbol = value);
        HelpMarker(
            "Works for allies and enemies. Outlined shield: ready. Solid shield: active. Slashed shield: cooldown after an observed Guard activation.");
        changed |= DrawCheckbox(
            "Shield overlay",
            configuration.ShowShields,
            value => configuration.ShowShields = value);
        changed |= DrawCheckbox(
            "Highlight current target",
            configuration.HighlightCurrentTarget,
            value => configuration.HighlightCurrentTarget = value);

        DrawSectionHeader("Placement and size");
        changed |= DrawSliderFloat(
            "Maximum distance",
            configuration.MaximumDistance,
            10f,
            100f,
            "%.0f yalms",
            value => configuration.MaximumDistance = value);
        changed |= DrawSliderFloat(
            "World height",
            configuration.WorldHeight,
            0.5f,
            4.5f,
            "%.2f yalms",
            value => configuration.WorldHeight = value);
        HelpMarker(
            "Moves the projected anchor vertically in the 3D world. Adjust this for the desired head or nameplate position.");

        changed |= DrawSliderFloat(
            "Screen Y offset",
            configuration.ScreenOffsetY,
            -100f,
            100f,
            "%.0f px",
            value => configuration.ScreenOffsetY = value);
        changed |= DrawSliderFloat(
            "Bar width",
            configuration.BarWidth,
            40f,
            240f,
            "%.0f px",
            value => configuration.BarWidth = value);
        changed |= DrawSliderFloat(
            "Bar height",
            configuration.BarHeight,
            4f,
            40f,
            "%.0f px",
            value => configuration.BarHeight = value);
        changed |= DrawSliderFloat(
            "MP bar height",
            configuration.MpBarHeight,
            2f,
            20f,
            "%.0f px",
            value => configuration.MpBarHeight = value);
        changed |= DrawSliderFloat(
            "MP bar spacing",
            configuration.MpBarSpacing,
            0f,
            12f,
            "%.0f px",
            value => configuration.MpBarSpacing = value);
        changed |= DrawSliderFloat(
            "Guard symbol size",
            configuration.GuardSymbolSize,
            8f,
            36f,
            "%.0f px",
            value => configuration.GuardSymbolSize = value);
        changed |= DrawSliderFloat(
            "Guard symbol spacing",
            configuration.GuardSymbolSpacing,
            0f,
            20f,
            "%.0f px",
            value => configuration.GuardSymbolSpacing = value);
        changed |= DrawSliderFloat(
            "Border thickness",
            configuration.BorderThickness,
            0f,
            6f,
            "%.1f px",
            value => configuration.BorderThickness = value);
        changed |= DrawSliderFloat(
            "Corner rounding",
            configuration.CornerRounding,
            0f,
            12f,
            "%.1f px",
            value => configuration.CornerRounding = value);

        DrawSectionHeader("Stabilization");
        changed |= DrawCheckbox(
            "Stabilize plate movement",
            configuration.StabilizePositions,
            value => configuration.StabilizePositions = value);
        HelpMarker(
            "Smooths small frame-to-frame projection changes in screen space. Large movements snap immediately so camera turns remain responsive.");
        changed |= DrawSliderFloat(
            "Dead zone",
            configuration.StabilizationDeadZone,
            0f,
            3f,
            "%.2f px",
            value => configuration.StabilizationDeadZone = value);
        HelpMarker(
            "Movement below this distance is ignored to prevent sub-pixel shimmer.");
        changed |= DrawSliderFloat(
            "Response",
            configuration.StabilizationResponse,
            1f,
            40f,
            "%.1f",
            value => configuration.StabilizationResponse = value);
        HelpMarker(
            "Higher values follow movement more quickly. The response is independent of frame rate.");
        changed |= DrawSliderFloat(
            "Snap distance",
            configuration.StabilizationSnapDistance,
            8f,
            160f,
            "%.0f px",
            value => configuration.StabilizationSnapDistance = value);
        HelpMarker(
            "Movement above this distance bypasses smoothing to avoid visible lag.");

        DrawSectionHeader("Colors");
        changed |= DrawColorEditor(
            "Enemy",
            configuration.EnemyColor,
            value => configuration.EnemyColor = value);
        changed |= DrawColorEditor(
            "Party",
            configuration.PartyColor,
            value => configuration.PartyColor = value);
        changed |= DrawColorEditor(
            "Alliance",
            configuration.AllianceColor,
            value => configuration.AllianceColor = value);
        changed |= DrawColorEditor(
            "Other friendly",
            configuration.FriendlyColor,
            value => configuration.FriendlyColor = value);
        changed |= DrawColorEditor(
            "Local player",
            configuration.LocalPlayerColor,
            value => configuration.LocalPlayerColor = value);
        changed |= DrawColorEditor(
            "Empty health",
            configuration.EmptyHealthColor,
            value => configuration.EmptyHealthColor = value);
        changed |= DrawColorEditor(
            "MP",
            configuration.MpColor,
            value => configuration.MpColor = value);
        changed |= DrawColorEditor(
            "Empty MP",
            configuration.EmptyMpColor,
            value => configuration.EmptyMpColor = value);
        changed |= DrawColorEditor(
            "Guard ready",
            configuration.GuardReadyColor,
            value => configuration.GuardReadyColor = value);
        changed |= DrawColorEditor(
            "Guard active",
            configuration.GuardActiveColor,
            value => configuration.GuardActiveColor = value);
        changed |= DrawColorEditor(
            "Guard cooldown",
            configuration.GuardCooldownColor,
            value => configuration.GuardCooldownColor = value);
        changed |= DrawColorEditor(
            "Border",
            configuration.BorderColor,
            value => configuration.BorderColor = value);
        changed |= DrawColorEditor(
            "Shield",
            configuration.ShieldColor,
            value => configuration.ShieldColor = value);
        changed |= DrawColorEditor(
            "Text",
            configuration.TextColor,
            value => configuration.TextColor = value);
        changed |= DrawColorEditor(
            "Target outline",
            configuration.TargetOutlineColor,
            value => configuration.TargetOutlineColor = value);

        ImGui.Spacing();
        if (ImGui.Button("Reset all settings to defaults"))
        {
            configuration.ResetToDefaults();
            changed = true;
        }

        if (changed)
            configuration.Save();
    }

    private static bool DrawCheckbox(
        string label,
        bool value,
        Action<bool> apply)
    {
        if (!ImGui.Checkbox(label, ref value))
            return false;

        apply(value);
        return true;
    }

    private static void DrawSectionHeader(string label)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted(label);
    }

    private static bool DrawSliderFloat(
        string label,
        float value,
        float minimum,
        float maximum,
        string format,
        Action<float> apply)
    {
        if (!ImGui.SliderFloat(label, ref value, minimum, maximum, format))
            return false;

        apply(value);
        return true;
    }

    private static bool DrawColorEditor(
        string label,
        Vector4 color,
        Action<Vector4> apply)
    {
        if (!ImGui.ColorEdit4(
                label,
                ref color,
                ImGuiColorEditFlags.AlphaBar |
                ImGuiColorEditFlags.AlphaPreviewHalf |
                ImGuiColorEditFlags.NoInputs))
        {
            return false;
        }

        apply(color);
        return true;
    }

    private static void HelpMarker(string text)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(text);
    }
}
