# GlamourBrowser

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for Final Fantasy XIV that shows every equippable item as a paged grid of tiles, each one rendered on your own character.

The user interface is in German. The plugin is not in the official Dalamud repository; build it yourself and load it as a dev plugin.

## Features

- **Tile grid** of all equippable items, rendered on your character in your current outfit or in underwear, with adjustable columns and rows.
- **Live preview** of the selected item or set: rotate, pan and zoom, with a camera position you can calibrate per slot and character.
- **Filters** by slot, level, ownership, source (shops, quests, duties and more), jobs (gear only certain jobs can wear, several at once or whole groups such as all crafters) and dyeability.
- **Ownership** across inventory, retainers, glamour dresser and armoire, including where an item is stored.
- **Gear sets** detected from the game data, shown as one tile.
- **Wishlist** for items and sets, with the missing pieces grouped by where to get them.
- **My sets**: build your own outfits with dyes, like in the glamour dresser. Start from scratch, from your current outfit or from a glamour plate, import all glamour plates at once, and transfer a set into the open glamour plate editor.
- **Render cache** with settings for how many recently shown pages stay rendered and how many are rendered ahead, plus an optional memory line (tile cache, VRAM, RAM).

## Usage

`/gbrowser` opens or closes the main window.

## Building

You need the .NET 10 SDK and an XIVLauncher installation with Dalamud. The build takes Dalamud's assemblies from its dev folder: `%AppData%\XIVLauncher\addon\Hooks\dev\` on Windows, `~/.xlcore/dalamud/Hooks/dev/` on Linux, or the folder in `DALAMUD_HOME`.

```bash
dotnet build GlamourBrowser.slnx -c Debug
dotnet test tests/GlamourBrowser.Core.Tests
```

To load it in the game, add `src/GlamourBrowser/bin/Debug/GlamourBrowser.dll` under Dalamud Settings → Experimental → Dev Plugin Locations and enable it in the plugin installer.

## Project layout

| Path | Contents |
|------|----------|
| `src/GlamourBrowser` | The plugin: ImGui windows, tile rendering through the game's character views, game interop |
| `src/GlamourBrowser.Core` | Game-independent logic (filters, paging, sets, wishlist, custom sets, plate transfer), covered by tests |
| `src/GlamourBrowser.Sources` | Builds the item source index from the game data |
| `tests/GlamourBrowser.Core.Tests` | xUnit tests for the core logic |
| `tools/SourceProbe` | Offline check of the source index against the game's sqpack files |

## Credits

Built on [Dalamud](https://github.com/goatcorp/Dalamud), [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) and [Lumina](https://github.com/NotAdam/Lumina). Additional item source data comes from [LuminaSupplemental](https://github.com/Critical-Impact/LuminaSupplemental).

## License

MIT, see [LICENSE](LICENSE).

FINAL FANTASY is a registered trademark of Square Enix Holdings Co., Ltd. This project is not affiliated with or endorsed by Square Enix.
