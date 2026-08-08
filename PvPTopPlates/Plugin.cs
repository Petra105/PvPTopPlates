using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PvPTopPlates;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/ptop";

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static IClientState ClientState { get; private set; } = null!;

    [PluginService]
    internal static IObjectTable ObjectTable { get; private set; } = null!;

    [PluginService]
    internal static IGameGui GameGui { get; private set; } = null!;

    [PluginService]
    internal static ITargetManager TargetManager { get; private set; } = null!;

    [PluginService]
    internal static INamePlateGui NamePlateGui { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;

    [PluginService]
    internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("PvPTopPlates");
    private readonly ConfigWindow configWindow;
    private readonly NativeNameplateTracker nameplateTracker;
    private readonly GuardActionTracker guardActionTracker;
    private readonly OverlayRenderer overlayRenderer;

    internal Configuration Configuration { get; }

    public Plugin()
    {
        Configuration =
            PluginInterface.GetPluginConfig() as Configuration ??
            new Configuration();

        configWindow = new ConfigWindow(Configuration);
        nameplateTracker = new NativeNameplateTracker();
        guardActionTracker = new GuardActionTracker(GameInteropProvider, Log);
        overlayRenderer = new OverlayRenderer(
            Configuration,
            nameplateTracker,
            guardActionTracker);

        windowSystem.AddWindow(configWindow);

        CommandManager.AddHandler(
            CommandName,
            new CommandInfo(OnCommand)
            {
                HelpMessage =
                    "Open settings. Arguments: on, off, or toggle.",
            });

        NamePlateGui.OnPostDataUpdate += OnNamePlateDataUpdate;
        PluginInterface.UiBuilder.Draw += Draw;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += OpenConfigUi;

        Log.Information("PvP TopPlates loaded.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= OpenConfigUi;
        NamePlateGui.OnPostDataUpdate -= OnNamePlateDataUpdate;
        CommandManager.RemoveHandler(CommandName);

        windowSystem.RemoveAllWindows();
        configWindow.Dispose();
        guardActionTracker.Dispose();
    }

    private void Draw()
    {
        windowSystem.Draw();
        overlayRenderer.Draw();
    }

    private void OpenConfigUi()
    {
        configWindow.IsOpen = true;
    }

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "on":
                Configuration.Enabled = true;
                Configuration.Save();
                break;
            case "off":
                Configuration.Enabled = false;
                Configuration.Save();
                break;
            case "toggle":
                Configuration.Enabled = !Configuration.Enabled;
                Configuration.Save();
                break;
            default:
                configWindow.Toggle();
                break;
        }
    }

    private void OnNamePlateDataUpdate(
        INamePlateUpdateContext context,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        nameplateTracker.Update(context, handlers);
    }
}
