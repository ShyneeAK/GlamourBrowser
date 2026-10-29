# Shynees Glamour Dresser

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for Final Fantasy XIV that shows every equippable item as a paged grid of tiles, each one rendered on your own character.

![The item grid: body pieces rendered on the character, with the live try-on, the item card and the filters](docs/screenshots/items.webp)

![My sets: own outfits as tiles, a folder showing its first four sets, and a set's pieces with where they are stored](docs/screenshots/my-sets.webp)

## Features

- **Tile grid** of all equippable items, rendered on your character in your current outfit or in underwear, with adjustable columns and rows, several sort orders and pages rendered ahead in the direction you page.
- **Live try-on** with the round buttons of the game's Try On window: every item or set you click is put on, so an outfit builds up piece by piece (or only the last click, with "Keep tried-on pieces" off), with undo and redo; right-click a worn piece to take it off. Equip or remove everything underneath, close the visor, draw or hide the weapon (hidden on the tiles too). Rings have a slot per hand. Rotate, pan and zoom (the camera stays where you put it), and calibrate a camera position per slot and character for the tiles.
- **Same model**: a button on each tile shows every item that shares its model, in any colour variant. Tiles show at a glance when you own or wish for the same model in another item.
- **Dye preview**: two tile dyes (channel 1 and 2) render every dyeable tile in them and come along when you click a tile; each tile's own dye boxes recolour just that tile, and the item card's dye boxes recolour the worn piece.
- **Filters** by slot, level, ownership, source (shops, quests, duties and more), jobs (gear only certain jobs can wear, several at once or whole groups such as all crafters) and dyeability, kept across restarts; a search can leave words out ("robe -skirt"), and model duplicates can be hidden.
- **Item card** with the game's own item tooltip on its icon, the item's sources and costs, buttons for the map, the duty finder, the recipe and the Mog Station, a chat link, copying the name, and links to Garland Tools, Teamcraft and a web search.
- **Market prices**: hovering the market board icon shows the cheapest listing on your home world and on your data center from [Universalis](https://universalis.app).
- **Ownership** across inventory, retainers, glamour dresser and armoire, including where an item is stored.
- **Automatic camera** that frames every slot to fit your character's race and height (gloves, bracelets and rings from the side), with a zoom per slot.
- **Gear sets** detected from the game data, shown as one tile; the chest in the item card shows a set's pieces as tiles.
- **Wishlist** for items and sets, each wish shown on its own, with the missing pieces grouped by where to get them.
- **Recently used and favorites**: the items you clicked last, and the ones you marked with the heart on their tile, each as their own view with every filter.
- **My sets**: build your own outfits with dyes, like in the glamour dresser, and sort them into folders. Start from scratch, from your current outfit or from a glamour plate, import all glamour plates at once or only the chosen one, and transfer a set into the open glamour plate editor (taking the dresser copy that is already dyed to match). A dye counter shows which dyes a set or outfit needs.

## Installation

1. In the game, open the Dalamud settings with `/xlsettings` and go to **Experimental**.
2. Under **Custom Plugin Repositories**, add `https://raw.githubusercontent.com/ShyneeAK/GlamourBrowser/main/repo.json`, tick **Enabled** and save.
3. Open the plugin installer with `/xlplugins`, search for Shynees Glamour Dresser and install it.

## Usage

`/glamourdresser` (short: `/sgd`) opens or closes the main window.

## Credits

Built on [Dalamud](https://github.com/goatcorp/Dalamud), [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) and [Lumina](https://github.com/NotAdam/Lumina). Additional item source data comes from [LuminaSupplemental](https://github.com/Critical-Impact/LuminaSupplemental).

## AI disclosure

This plugin was developed with AI assistance. Large parts of the code were written by Claude Code. The author decided on the features and the design, reviewed the changes and tested every version in the game.

## License

MIT, see [LICENSE](LICENSE).

FINAL FANTASY is a registered trademark of Square Enix Holdings Co., Ltd. This project is not affiliated with or endorsed by Square Enix.
