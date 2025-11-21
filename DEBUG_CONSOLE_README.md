# Debug Console Documentation

## Overview
The debug console is a developer tool that allows you to open any menu/window in the game and automatically capture screenshots. This is useful for testing, documentation, and debugging UI elements.

## Activation
Press the **backtick key (`)** to toggle the debug console on/off.

## Available Commands

### help
Shows all available commands and their usage.
```
> help
```

### openmenu <name>
Opens a specific menu/window by name. Does not capture a screenshot.
```
> openmenu options
> openmenu factory
> openmenu diplomatic
```

### screenshot <name>
Opens a specific menu/window and automatically captures a screenshot. The screenshot is saved to the `screenshots/` folder with a timestamp.
```
> screenshot options
> screenshot factory
> screenshot diplomatic
```

### screenshot
Captures a screenshot of the currently active window without opening a new one.
```
> screenshot
```

### list
Lists all available menus that can be opened.
```
> list
```

## Available Menu Names
- `main` - Main menu window
- `options` - Options/settings window
- `loading` - Loading screen window
- `mapeditor` - Map editor window
- `construction` - Construction management window
- `diplomatic` / `diplomacy` - Diplomatic relations window
- `factory` / `factorystats` - Factory statistics window
- `performance` / `performancestats` - Performance stats window
- `policy` / `policymanager` - Policy manager window
- `popstats` / `population` - Population statistics window
- `trade` / `trademanagement` - Trade management window
- `tradeproposal` - Trade proposal window

## Screenshots
All screenshots are automatically saved to the `screenshots/` folder in the game directory. The filename format is:
```
<menuname>_<timestamp>.png
```

For example:
- `options_20231121_143052.png`
- `factory_20231121_143125.png`
- `current_20231121_143140.png`

## Keyboard Shortcuts
- **Backtick (`)** - Toggle console visibility
- **Enter** - Execute command in console
- **Escape** - Close console

## Notes
- The console is only visible when toggled on
- Command history is displayed in the console output area
- The screenshots folder is automatically created if it doesn't exist
- The screenshots folder is excluded from git commits (via .gitignore)

## Examples

### Example 1: Taking screenshots of all menus
```
> screenshot main
> screenshot options
> screenshot construction
> screenshot diplomatic
> screenshot factory
```

### Example 2: Opening menus for testing
```
> openmenu options
(test the options menu)
> openmenu factory
(test the factory menu)
```

### Example 3: Getting help
```
> help
(displays all available commands)
> list
(displays all available menus)
```

## Troubleshooting

**Console won't open:**
- Make sure you're pressing the backtick key (`) which is usually located above the Tab key
- The game window must have focus

**Screenshot not saving:**
- Check that you have write permissions in the game directory
- The screenshots folder should be created automatically
- Check the console output for any error messages

**Menu won't open:**
- Use the `list` command to see all available menu names
- Menu names are case-insensitive
- Make sure you're using the correct menu name (use aliases if available)
