# Debug Console - Quick Start Guide

## What is this?

The Debug Console is a developer tool that lets you:
1. **Open any menu/window** in the game instantly
2. **Take screenshots** of menus automatically
3. **Test UI elements** without clicking through the game

## How to Use

### Step 1: Open the Console
**Press the backtick key (`)** - it's usually above the Tab key

```
┌─────────────────────────────────────────────────────────────┐
│ Economy Sim - Game View                                      │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│                  [Game Map Display]                           │
│                                                               │
├─────────────────────────────────────────────────────────────┤
│ Debug Console (Press ` to close, type 'help' for commands)   │
│                                                               │
│                                                               │
│ > _                                                           │
└─────────────────────────────────────────────────────────────┘
```

### Step 2: Type a Command
Type your command and press **Enter**

```
> help
```

### Step 3: See the Results
The console shows command output and status

```
> help
Debug Console Commands:
  help                    - Show this help text
  openmenu <name>         - Open a menu/window by name
  screenshot <name>       - Open menu and capture screenshot
  screenshot              - Capture screenshot of current window
  list                    - List all available menus
```

## Common Commands

### Get Help
```
> help
```
Shows all available commands

### List All Menus
```
> list
```
Shows: main, options, construction, diplomatic, etc.

### Open a Menu (No Screenshot)
```
> openmenu options
```
Opens the Options window

### Open Menu + Screenshot
```
> screenshot factory
```
Opens Factory Stats window AND saves a screenshot to `screenshots/factory_20231121_143052.png`

### Screenshot Current Window
```
> screenshot
```
Saves screenshot of whatever window is currently active

## Real Examples

### Example 1: Take Screenshots of All Menus
Perfect for documentation or bug reports!

```
> screenshot main
Opened MainWindow
Screenshot saved to: screenshots/main_20231121_140001.png

> screenshot options
Opened OptionsWindow
Screenshot saved to: screenshots/options_20231121_140015.png

> screenshot factory
Opened FactoryStatsWindow
Screenshot saved to: screenshots/factory_20231121_140032.png
```

Result: You now have 3 PNG files in the `screenshots/` folder!

### Example 2: Test Menu Opening
Quickly test if all menus work:

```
> openmenu construction
Opened ConstructionWindow

> openmenu diplomatic
Opened DiplomaticRelationsWindow

> openmenu trade
Opened TradeManagementWindow
```

### Example 3: Document a Bug
You found a visual bug in the policy menu:

```
> screenshot policy
Opened PolicyManagerWindow
Screenshot saved to: screenshots/policy_20231121_151234.png
```

Now you can attach `screenshots/policy_20231121_151234.png` to your bug report!

## Menu Names Reference

### Quick Reference Table
| Menu Name          | Aliases               | What it Opens           |
|-------------------|-----------------------|-------------------------|
| main              | -                     | Main Menu               |
| options           | -                     | Options/Settings        |
| loading           | -                     | Loading Screen          |
| mapeditor         | -                     | Map Editor              |
| construction      | -                     | Construction Manager    |
| diplomatic        | diplomacy             | Diplomatic Relations    |
| factory           | factorystats          | Factory Statistics      |
| performance       | performancestats      | Performance Stats       |
| policy            | policymanager         | Policy Manager          |
| popstats          | population            | Population Statistics   |
| trade             | trademanagement       | Trade Management        |
| tradeproposal     | -                     | Trade Proposal          |

### Aliases
Some menus have shortcuts:
```
> screenshot diplomatic    ✓ Works
> screenshot diplomacy     ✓ Also works (same menu)

> screenshot factory       ✓ Works
> screenshot factorystats  ✓ Also works (same menu)
```

## Keyboard Shortcuts

| Key         | Action                    |
|-------------|---------------------------|
| **`**       | Toggle console on/off     |
| **Enter**   | Execute typed command     |
| **Escape**  | Close console             |

## Tips & Tricks

### Tip 1: Screenshot Timestamps
Every screenshot has a unique timestamp, so you can take multiple screenshots of the same menu:

```
> screenshot options
Screenshot saved to: screenshots/options_20231121_140001.png

(change something in options)

> screenshot options
Screenshot saved to: screenshots/options_20231121_140045.png
```

You get two different files!

### Tip 2: Testing Without Clicking
No need to navigate through the game:

```
Instead of: Main Menu → New Game → Factory Stats
Just type:  screenshot factory
```

### Tip 3: Use Short Commands
```
> list          (instead of typing the full help text)
> screenshot    (captures current window fast)
```

### Tip 4: Case Doesn't Matter
```
> openmenu OPTIONS    ✓ Works
> openmenu options    ✓ Works
> openmenu OpTiOnS    ✓ Works
```

## Where Are My Screenshots?

Screenshots are saved in the `screenshots/` folder in the game directory:

```
economy-sim/
├── screenshots/           ← Your screenshots are here!
│   ├── main_20231121_140001.png
│   ├── options_20231121_140015.png
│   └── factory_20231121_140032.png
├── Views/
├── Assets/
└── ...
```

## Troubleshooting

### Console Won't Open
**Problem**: Pressing ` doesn't do anything  
**Solution**: Make sure the game window has focus (click on it first)

### Can't Find Backtick Key
**Problem**: Where is the ` key?  
**Solution**: It's usually above Tab, left of the 1 key. On some keyboards, it might be elsewhere.

### "Unknown menu" Error
**Problem**: `Unknown menu: facotry`  
**Solution**: Check spelling! Use the `list` command to see correct names.

```
> list
Available menus:
  construction
  diplomatic
  diplomacy
  factory        ← It's "factory" not "facotry"
  ...
```

### Screenshots Folder Missing
**Problem**: Where did the screenshots folder go?  
**Solution**: It's created automatically! Just run a screenshot command:

```
> screenshot main
```

The folder will be created if it doesn't exist.

## Summary

1. **Press `** to open console
2. **Type command** (like `screenshot factory`)
3. **Press Enter** to execute
4. **Find screenshots** in `screenshots/` folder
5. **Press `** to close console

That's it! Simple and powerful. Happy debugging! 🎮📸

---

For more detailed information, see:
- `DEBUG_CONSOLE_README.md` - Full documentation
- `DEBUG_CONSOLE_IMPLEMENTATION_SUMMARY.md` - Technical details
