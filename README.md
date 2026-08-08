# PvP TopPlates

PvP TopPlates is a Dalamud plugin that redraws player HP, shield, and MP bars
in ImGui's foreground layer. The bars are projected from each player
character's world position, stabilized in screen space, and rendered as
top-level UI with no world depth test. Terrain, walls, ramps, and other 3D
geometry therefore cannot draw over them.

This is a Dalamud plugin, not a Penumbra or TexTools asset mod.

## Install

1. Open Dalamud Settings with `/xlsettings`.
2. Open the `Experimental` tab.
3. Add this URL under Custom Plugin Repositories:

   ```text
   https://raw.githubusercontent.com/Petra105/PvPTopPlates/main/repo.json
   ```

4. Save, then open `/xlplugins` and install **PvP TopPlates**.

## Behavior

- Runs only in PvP by default.
- Draws only targetable, living player characters with valid HP data.
- Respects currently active native nameplates by default.
- Separately filters enemies, party members, alliance members, the local
  player, and other friendly players.
- Smooths small frame-to-frame projection changes with a configurable dead
  zone, frame-rate-independent response, and large-movement snap threshold.
- Supports configurable range, HP and MP bar size, world height, screen
  offset, colors, shield overlay, names, HP percentages, and target
  highlighting.
- Hides with the game UI by default.
- Never modifies game packets, HP values, targeting, or the native renderer.

The native bars are not removed. Configure the world height and screen offset
so the foreground bar covers or sits immediately beside the native bar.

## Command

`/ptop` opens the configuration window.

`/ptop on`, `/ptop off`, and `/ptop toggle` change the master enable state.

## Build

Requirements:

- Windows 10 or later
- Visual Studio 2022 or the .NET 10 SDK
- XIVLauncher and Dalamud installed and launched at least once

From the repository root:

```powershell
dotnet restore --locked-mode
dotnet build PvPTopPlates.sln -c Release --no-restore
```

The development DLL is written beneath:

```text
PvPTopPlates\bin\x64\Release\PvPTopPlates.dll
```

The Dalamud SDK also creates a release package beneath the Release output
directory. If Dalamud is installed in a non-default location, set
`DALAMUD_HOME` to the appropriate Dalamud development directory before
building.

## Load as a development plugin

1. In game, run `/xlsettings`.
2. Open `Experimental`.
3. Add the full path to `PvPTopPlates.dll` under Dev Plugin Locations.
4. Run `/xlplugins`, open `Dev Tools`, and enable PvP TopPlates.
5. Run `/ptop` to adjust placement before entering a match. Enable the
   outside-PvP positioning option temporarily if needed.

## Visibility notes

The `Require an active native nameplate` option is enabled by default. It
limits the overlay to actors for which the game is currently maintaining a
nameplate. Disable it only if a specific PvP mode fails to report expected
players.

Enemy actors must remain targetable. This prevents the overlay from exposing
players while the game marks them hidden, untargetable, or unavailable.

## Compatibility

The project targets Dalamud API 15 through `Dalamud.NET.Sdk/15.0.0`, current
on July 26, 2026. Major FFXIV or Dalamud API updates may require a rebuild or
small source update.

Dalamud and other third-party FFXIV tools are not endorsed by Square Enix and
may violate the FFXIV User Agreement. Use at your own risk.
