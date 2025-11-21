# Getting Started

This guide will help you set up your development environment and start working on Economy Sim.

## Table of Contents
1. [Prerequisites](#prerequisites)
2. [Initial Setup](#initial-setup)
3. [Building the Project](#building-the-project)
4. [Running the Game](#running-the-game)
5. [Development Workflow](#development-workflow)
6. [Common Tasks](#common-tasks)
7. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Software

#### .NET 8.0 SDK
Economy Sim targets .NET 8.0. You must have the SDK installed.

**Download**: [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0)

**Verify Installation**:
```bash
dotnet --version
# Should show 8.0.x or higher
```

#### IDE / Text Editor

**Recommended Options**:
- **Visual Studio 2022** (Windows/Mac) - Full IDE with excellent C# support
- **JetBrains Rider** (Cross-platform) - Premium IDE with great features
- **Visual Studio Code** (Cross-platform) - Lightweight with C# extension

**VS Code Extensions**:
- C# (by Microsoft)
- C# Dev Kit
- Avalonia for VSCode

### Optional Tools

#### Python 3.10+ (for data generation scripts)
```bash
python3 --version
# Should show 3.10.x or higher
```

**Python Packages** (optional, for GIS scripts):
```bash
pip install fiona rasterio shapely
```

#### Git
For version control. Most systems have this pre-installed.
```bash
git --version
```

---

## Initial Setup

### 1. Clone the Repository

```bash
git clone https://github.com/bitzy06/economy-sim.git
cd economy-sim
```

### 2. Restore Dependencies

Restore NuGet packages:
```bash
dotnet restore
```

This downloads all required libraries:
- Avalonia UI framework
- GDAL for geospatial processing
- SkiaSharp for graphics
- NetTopologySuite for geometry
- And more...

Expected output:
```
Determining projects to restore...
Restored /path/to/Economy sim.csproj (in X.XX sec).
```

### 3. Verify Project Structure

Your directory should look like:
```
economy-sim/
├── Assets/              # Images, resources
├── Views/               # Avalonia XAML views
├── docs/                # Documentation (you are here!)
├── *.cs                 # C# source files
├── Economy sim.csproj   # Project file
├── Economy sim.sln      # Solution file
├── AGENTS.md            # Agent instructions
└── world_setup.json     # World configuration (if exists)
```

---

## Building the Project

### Standard Build

```bash
dotnet build -v minimal
```

**Build Output**: `bin/Debug/net8.0/`

**Common Warnings**: The project has some nullable reference warnings. These are non-critical and can be ignored during development.

### Release Build

```bash
dotnet build -c Release -v minimal
```

**Build Output**: `bin/Release/net8.0/`

### Clean Build

If you encounter issues:
```bash
dotnet clean
dotnet restore
dotnet build -v minimal
```

---

## Running the Game

### Run from Command Line

```bash
dotnet run --project "Economy sim.csproj"
```

Or simply:
```bash
dotnet run
```

### Run from IDE

**Visual Studio**:
1. Open `Economy sim.sln`
2. Press F5 (or click "Start")

**Visual Studio Code**:
1. Open the folder
2. Press F5
3. Select ".NET Core Launch" if prompted

**Rider**:
1. Open `Economy sim.sln`
2. Click the run button or press Shift+F10

### Expected Behavior

1. **GDAL Initialization** - Console shows GDAL library loading
2. **Main Menu** - Window appears with buttons:
   - New Game
   - Load Game (not yet implemented)
   - Options
   - Exit
3. **Click "New Game"** - Loads the game world (may take 30-60 seconds)
4. **Game View** - Main game interface appears

---

## Development Workflow

### Typical Development Cycle

1. **Make Changes** - Edit C# files
2. **Build** - `dotnet build -v minimal`
3. **Fix Errors** - Address compilation errors
4. **Run** - `dotnet run` to test
5. **Test** - Verify functionality
6. **Commit** - `git commit` when satisfied

### Hot Reload (Avalonia XAML)

For XAML changes only:
1. Run the app
2. Edit XAML files
3. Save - Changes may appear without restart (limited support)

For C# changes: Full rebuild required.

### File Watcher (Optional)

Use `dotnet watch` for automatic rebuild on file changes:
```bash
dotnet watch run
```

This will:
- Watch for file changes
- Automatically rebuild
- Restart the application

---

## Common Tasks

### Adding a New Class

1. **Create File**: `NewClassName.cs`
2. **Add Namespace**:
```csharp
namespace Economy_sim
{
    public class NewClassName
    {
        // Implementation
    }
}
```
3. **Build**: `dotnet build -v minimal`
4. **Use**: Reference from other files

### Adding a New Window

1. **Create XAML**: `Views/NewWindow.axaml`
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="Economy_sim.NewWindow"
        Title="New Window">
    <!-- Content here -->
</Window>
```

2. **Create Code-Behind**: `Views/NewWindow.axaml.cs`
```csharp
using Avalonia.Controls;

namespace Economy_sim
{
    public partial class NewWindow : Window
    {
        public NewWindow()
        {
            InitializeComponent();
        }
    }
}
```

3. **Build and Use**:
```csharp
var window = new NewWindow();
window.Show();
```

### Modifying Existing Systems

**Before modifying**:
1. Read relevant documentation in `docs/`
2. Understand the system architecture
3. Check for existing tests or examples

**Making changes**:
1. Locate the relevant `.cs` file
2. Make minimal, focused changes
3. Build to check for errors
4. Test the affected functionality

### Working with the Map System

**Map data location**: `data/` directory (not committed to git)

**To generate test data**:
```bash
python3 generate_world.py
```

**Map configuration**: Check `MultiResolutionMapManager.cs`, `HybridMapManager.cs`

### Debugging

**Enable Debug Output**:
- Avalonia uses Trace logging
- Check console output when running

**Debug in Visual Studio**:
1. Set breakpoints (click left margin)
2. Press F5
3. Execution pauses at breakpoints

**Debug in VS Code**:
1. Set breakpoints
2. Press F5
3. Use Debug panel to inspect variables

---

## Troubleshooting

### Build Errors

#### "The type or namespace name 'X' could not be found"

**Solution**:
```bash
dotnet restore
dotnet clean
dotnet build -v minimal
```

#### "Project file is incomplete or corrupted"

**Solution**:
- Close IDE
- Delete `bin/` and `obj/` directories
- Run `dotnet restore`
- Reopen IDE

### Runtime Errors

#### "Could not load file or assembly 'gdal_csharp'"

**Issue**: GDAL native libraries not found

**Solution**:
- Ensure `MaxRev.Gdal.WindowsRuntime.Minimal` package is installed
- Try clean build: `dotnet clean && dotnet build`

#### "Window does not appear"

**Solution**:
- Check console for errors
- Verify Avalonia version in `.csproj` matches references
- Try updating packages: `dotnet restore`

#### "Map fails to load"

**Issue**: Missing map data files

**Solution**:
- Check `data/` directory exists
- Run Python scripts to generate data (if needed)
- Or configure app to skip map loading (modify `GameView.axaml.cs`)

### Python Script Issues

#### "ModuleNotFoundError: No module named 'fiona'"

**Solution**:
```bash
pip install fiona rasterio shapely
```

Or use conda:
```bash
conda install -c conda-forge fiona rasterio shapely
```

#### Scripts fail to download data

**Issue**: Network or permissions

**Solution**:
- Check internet connection
- Ensure write permissions in `data/` directory
- Check firewall settings

### Performance Issues

#### Slow compilation

**Solution**:
- Close unnecessary applications
- Use `dotnet build -v minimal` instead of `-v detailed`
- Build specific projects only

#### Slow execution

**Solution**:
- Use Release build: `dotnet build -c Release`
- Check if debug logging is enabled
- Reduce world generation parameters (smaller world)

---

## Next Steps

Now that you have the project set up:

1. **Explore the Code**: Browse the source files to get familiar
2. **Read Documentation**: Check out:
   - [Architecture Overview](ARCHITECTURE.md)
   - [Game Systems Guide](GAME_SYSTEMS.md)
   - [Code Organization](CODE_ORGANIZATION.md)
3. **Run the Game**: Play with it to understand functionality
4. **Make a Small Change**: Try modifying a value or string
5. **Experiment**: Learn by doing!

### Recommended Reading Order

For new developers:
1. [Code Organization](CODE_ORGANIZATION.md) - Understand file structure
2. [Architecture Overview](ARCHITECTURE.md) - Learn system design
3. [Data Model](DATA_MODEL.md) - Understand entities
4. [Game Systems Guide](GAME_SYSTEMS.md) - Learn mechanics
5. [Flow Charts](FLOWCHARTS.md) - Visual understanding

### Getting Help

- **Documentation**: Check `docs/` folder
- **Code Comments**: Many files have inline documentation
- **Existing Code**: Look for similar examples in the codebase
- **Git History**: `git log` to see past changes

---

## Code Style Guidelines

Follow these conventions (from `AGENTS.md`):

**C# Code**:
- PascalCase for public members
- camelCase for locals
- Use standard .NET naming conventions

**Python Code**:
- Follow PEP 8
- 4-space indents

**Before Committing**:
1. Build succeeds: `dotnet build -v minimal`
2. No new warnings introduced
3. Test changes work as expected
4. Don't commit `world_setup.json`, `data/`, or `logs/`

---

**Related Documentation**:
- [Code Organization](CODE_ORGANIZATION.md) - File structure
- [Architecture Overview](ARCHITECTURE.md) - System design
- [Game Systems Guide](GAME_SYSTEMS.md) - Feature documentation
