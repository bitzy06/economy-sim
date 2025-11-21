# UI Structure

This document describes the user interface organization and navigation in Economy Sim.

## Table of Contents
1. [UI Overview](#ui-overview)
2. [Window Hierarchy](#window-hierarchy)
3. [Main Menu](#main-menu)
4. [Game View](#game-view)
5. [Feature Windows](#feature-windows)
6. [Navigation Flow](#navigation-flow)
7. [UI Components](#ui-components)

---

## UI Overview

### Technology
Economy Sim uses **Avalonia UI**, a cross-platform XAML-based UI framework similar to WPF.

### Architecture Pattern
- **MVVM** (Model-View-ViewModel) - Partially implemented
- **Code-Behind** - Event handlers in `.axaml.cs` files
- **Direct Binding** - Some UI elements directly access game data

### Window Types
- **Main Menu** - Entry point, game launch
- **Game View** - Primary gameplay window
- **Dialog Windows** - Feature-specific interfaces
- **Loading Window** - Progress indicator

---

## Window Hierarchy

### Application Flow

```
Application Start
       ↓
┌─────────────┐
│ MainWindow  │ (Main Menu)
└──────┬──────┘
       │
       ├─→ New Game ──→ GameView (Main Game)
       │                    │
       │                    ├─→ Construction Window
       │                    ├─→ Factory Stats Window
       │                    ├─→ Policy Manager Window
       │                    ├─→ Trade Proposal Window
       │                    ├─→ Diplomatic Relations Window
       │                    ├─→ Performance Stats Window
       │                    ├─→ Population Stats Window
       │                    └─→ Map Editor Window
       │
       ├─→ Load Game ──→ (Not yet implemented)
       │
       ├─→ Options ──→ OptionsWindow
       │
       └─→ Exit ──→ Close Application
```

---

## Main Menu

### MainWindow

**File**: `Views/MainWindow.axaml[.cs]`

**Purpose**: Initial screen, game launcher

#### UI Elements

```
┌─────────────────────────────────────┐
│         ECONOMY SIM                 │
│                                     │
│   ┌─────────────────────────┐      │
│   │      New Game           │      │
│   └─────────────────────────┘      │
│                                     │
│   ┌─────────────────────────┐      │
│   │      Load Game          │      │
│   └─────────────────────────┘      │
│                                     │
│   ┌─────────────────────────┐      │
│   │      Options            │      │
│   └─────────────────────────┘      │
│                                     │
│   ┌─────────────────────────┐      │
│   │      Exit               │      │
│   └─────────────────────────┘      │
└─────────────────────────────────────┘
```

#### Button Actions

| Button | Action | Implementation |
|--------|--------|----------------|
| New Game | Open GameView, close MainWindow | `NewGameButton_Click()` |
| Load Game | Placeholder (console log) | `LoadGameButton_Click()` |
| Options | Open OptionsWindow | `OptionsButton_Click()` |
| Exit | Close application | `ExitButton_Click()` |

---

## Game View

### GameView

**File**: `Views/GameView.axaml[.cs]`

**Purpose**: Main gameplay interface

**Size**: Large, complex (99KB XAML, 202KB code-behind)

#### Layout Overview

```
┌────────────────────────────────────────────────────────────┐
│  Menu Bar: [Game] [View] [Economy] [Politics] [Help]      │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  ┌──────────────────────┐  ┌──────────────────────────┐  │
│  │   Control Panel      │  │                          │  │
│  │                      │  │                          │  │
│  │  - Turn Info         │  │      Map Display         │  │
│  │  - Selected Entity   │  │                          │  │
│  │  - Quick Stats       │  │    (Geographic View)     │  │
│  │  - Action Buttons    │  │                          │  │
│  │                      │  │                          │  │
│  └──────────────────────┘  └──────────────────────────┘  │
│                                                            │
├────────────────────────────────────────────────────────────┤
│  Status Bar: Budget | Turn | Population | GDP             │
└────────────────────────────────────────────────────────────┘
```

#### Key UI Sections

**1. Menu Bar**
- Game menu: Save, Load, Settings, Exit
- View menu: Toggle panels, map layers
- Economy menu: Trade, markets, factories
- Politics menu: Policies, diplomacy
- Help menu: Documentation, about

**2. Map Display**
- Geographic map with political borders
- Zoom and pan controls
- Click to select entities (cities, countries)
- Multiple layers (terrain, population, political)

**3. Control Panel**
- Current turn number
- Selected entity information
- Quick action buttons
- Role information (if player has role)

**4. Statistics Panel**
- Economic indicators (GDP, budget)
- Population stats
- Resource availability
- Trade balance

**5. Event Log**
- Recent game events
- Notifications
- System messages

**6. Status Bar**
- Key metrics at a glance
- Current player role
- Game speed control

#### Player Role UI Changes

The UI adapts based on player role:

**Prime Minister Mode**:
```
Visible:
  - National policy buttons
  - Country statistics
  - International trade options
  - Diplomatic relations
  
Hidden:
  - Individual factory controls
  - Corporate budget
```

**Governor Mode**:
```
Visible:
  - State policy buttons
  - State statistics
  - Inter-state trade
  
Hidden:
  - National policies
  - Corporate controls
```

**CEO Mode**:
```
Visible:
  - Corporate budget
  - Factory management
  - Production controls
  - Trade goods buttons
  
Hidden:
  - Government policies
  - National statistics
```

---

## Feature Windows

### Construction Window

**File**: `Views/ConstructionWindow.axaml[.cs]`

**Purpose**: Build new factories

**Trigger**: Click "Build Factory" button (CEO role)

#### Layout

```
┌───────────────────────────────────┐
│  Build New Factory                │
├───────────────────────────────────┤
│  Factory Type: [Dropdown]         │
│                                   │
│  Location: [City Dropdown]        │
│                                   │
│  Capacity: [Input]                │
│                                   │
│  Cost: $XXXXX                     │
│                                   │
│  Duration: X turns                │
│                                   │
│  [Build]  [Cancel]                │
└───────────────────────────────────┘
```

---

### Factory Stats Window

**File**: `Views/FactoryStatsWindow.axaml[.cs]`

**Purpose**: View factory details and production

**Trigger**: Click on a factory

#### Layout

```
┌───────────────────────────────────┐
│  Factory: Steel Mill              │
├───────────────────────────────────┤
│  Owner: Acme Corp                 │
│  Location: New York               │
│                                   │
│  Production:                      │
│    Input: Iron (10), Coal (5)     │
│    Output: Steel (15)             │
│                                   │
│  Capacity: 100 units/turn         │
│  Workers: 80 / 100                │
│                                   │
│  Efficiency: 80%                  │
│                                   │
│  [Close]                          │
└───────────────────────────────────┘
```

---

### Policy Manager Window

**File**: `Views/PolicyManagerWindow.axaml[.cs]`

**Purpose**: Manage government policies

**Trigger**: Politics menu → Manage Policies (PM role)

#### Layout

```
┌───────────────────────────────────┐
│  Policy Manager                   │
├───────────────────────────────────┤
│  Active Policies:                 │
│    ☑ Free Trade                   │
│    ☑ Income Tax (15%)             │
│    ☐ Protectionism                │
│                                   │
│  Available Policies:              │
│    [ Add New Policy ]             │
│                                   │
│  Policy Details:                  │
│    (shows when policy selected)   │
│                                   │
│  [Apply]  [Cancel]                │
└───────────────────────────────────┘
```

---

### Trade Proposal Window

**File**: `Views/TradeProposalWindow.axaml[.cs]`

**Purpose**: Create trade agreements

**Trigger**: Economy menu → Trade

#### Layout

```
┌───────────────────────────────────┐
│  Trade Proposal                   │
├───────────────────────────────────┤
│  Offer:                           │
│    Good: [Dropdown]               │
│    Quantity: [Input]              │
│    Price: [Input]                 │
│                                   │
│  Request:                         │
│    Good: [Dropdown]               │
│    Quantity: [Input]              │
│    Price: [Input]                 │
│                                   │
│  Trading With: [Country/Corp]     │
│                                   │
│  [Propose]  [Cancel]              │
└───────────────────────────────────┘
```

---

### Diplomatic Relations Window

**File**: `Views/DiplomaticRelationsWindow.axaml[.cs]`

**Purpose**: Manage international relations

**Trigger**: Politics menu → Diplomacy

#### Layout

```
┌───────────────────────────────────┐
│  Diplomatic Relations             │
├───────────────────────────────────┤
│  Country Relations:               │
│                                   │
│  ┌─────────────────────────────┐ │
│  │ Country A  [Friendly] 85%   │ │
│  │ Country B  [Neutral]  50%   │ │
│  │ Country C  [Hostile]  20%   │ │
│  └─────────────────────────────┘ │
│                                   │
│  Actions:                         │
│    [Improve Relations]            │
│    [Trade Agreement]              │
│    [Declare War]                  │
│                                   │
│  [Close]                          │
└───────────────────────────────────┘
```

---

### Performance Stats Window

**File**: `Views/PerformanceStatsWindow.axaml[.cs]`

**Purpose**: Monitor game performance

**Trigger**: Developer/debug menu

#### Layout

```
┌───────────────────────────────────┐
│  Performance Statistics           │
├───────────────────────────────────┤
│  FPS: 60                          │
│  Frame Time: 16.6ms               │
│  Memory: 512MB                    │
│                                   │
│  Turn Processing Time: 250ms     │
│    - Production: 120ms            │
│    - Trade: 80ms                  │
│    - Government: 50ms             │
│                                   │
│  [Close]                          │
└───────────────────────────────────┘
```

---

### Map Editor Window

**File**: `Views/MapEditorWindow.axaml[.cs]`

**Purpose**: Edit map data (developer tool)

**Trigger**: View menu → Map Editor

#### Features
- Draw political borders
- Edit country boundaries
- Add/remove cities
- Modify terrain

---

### Options Window

**File**: `Views/OptionsWindow.axaml[.cs]`

**Purpose**: Configure game settings

**Trigger**: Main Menu → Options

#### Layout

```
┌───────────────────────────────────┐
│  Options                          │
├───────────────────────────────────┤
│  Graphics:                        │
│    Resolution: [Dropdown]         │
│    Fullscreen: [Checkbox]         │
│                                   │
│  Audio:                           │
│    Music Volume: [Slider]         │
│    SFX Volume: [Slider]           │
│                                   │
│  Gameplay:                        │
│    Auto-save: [Checkbox]          │
│    Turn Speed: [Slider]           │
│                                   │
│  [Apply]  [Back to Menu]          │
└───────────────────────────────────┘
```

---

### Loading Window

**File**: `Views/LoadingWindow.axaml[.cs]`

**Purpose**: Show progress during world generation

**Trigger**: Automatically during world load

#### Layout

```
┌───────────────────────────────────┐
│  Loading...                       │
├───────────────────────────────────┤
│                                   │
│  Generating World...              │
│                                   │
│  [████████░░] 80%                 │
│                                   │
│  Current: Creating factories      │
│                                   │
└───────────────────────────────────┘
```

---

## Navigation Flow

### Common User Journeys

#### 1. Start New Game
```
Launch App
  ↓
Main Menu
  ↓
Click "New Game"
  ↓
Loading Window (world generation)
  ↓
Game View (ready to play)
```

#### 2. Build a Factory (as CEO)
```
Game View
  ↓
Assume CEO Role (if not already)
  ↓
Click "Build Factory" button
  ↓
Construction Window opens
  ↓
Select factory type, location
  ↓
Click "Build"
  ↓
Construction Window closes
  ↓
Game View (factory in construction queue)
```

#### 3. Manage Policies (as Prime Minister)
```
Game View
  ↓
Assume Prime Minister Role
  ↓
Menu Bar → Politics → Manage Policies
  ↓
Policy Manager Window opens
  ↓
Toggle policies, adjust settings
  ↓
Click "Apply"
  ↓
Policy Manager Window closes
  ↓
Game View (policies updated)
```

#### 4. View Statistics
```
Game View
  ↓
Click on entity (city, factory, country)
  ↓
Relevant stats window opens
  (Factory Stats, City Stats, etc.)
  ↓
View information
  ↓
Click "Close"
  ↓
Back to Game View
```

---

## UI Components

### Common Controls

#### Buttons
- Standard Avalonia buttons
- Style: Fluent theme
- Hover effects enabled

#### Text Inputs
- TextBox controls for numeric/text input
- Validation (when implemented)

#### Dropdowns
- ComboBox controls
- Populated from game data

#### Lists
- ListBox for displaying collections
- ItemsControl for custom layouts

#### Sliders
- For continuous values (volume, speed)
- Snap to grid when appropriate

### Data Display

#### Statistics Panels
- Read-only text displays
- Update in real-time or per-turn
- Color coding for positive/negative values

#### Maps
- Custom rendered using SkiaSharp
- Displayed in Avalonia Image control
- Interactive (click, pan, zoom)

#### Charts/Graphs
- (Future feature - not yet implemented)

---

## Styling

### Theme
- **Avalonia Fluent Theme** - Modern, clean design
- Light theme (dark theme not yet implemented)

### Colors
- Standard Fluent color palette
- Semantic colors:
  - Green for positive (profit, growth)
  - Red for negative (debt, decline)
  - Blue for neutral info
  - Yellow for warnings

### Fonts
- **Inter** font family (included via Avalonia.Fonts.Inter)
- Sizes vary by element type

---

## Accessibility

### Current State
- Basic keyboard navigation
- No screen reader support yet
- No high contrast mode

### Future Improvements
- Full keyboard shortcuts
- Screen reader compatibility
- Accessibility settings

---

## Responsive Design

### Window Resizing
- Most windows have fixed size
- GameView is resizable
- Map scales with window size

### Resolution Support
- Designed for 1920x1080 and higher
- Minimum tested: 1280x720
- May have issues on smaller screens

---

## UI Best Practices

When adding new UI:

1. **Follow Existing Patterns**: Match the style of existing windows
2. **Use MVVM**: Prefer data binding over direct manipulation
3. **Validate Input**: Check user input before processing
4. **Provide Feedback**: Show confirmation/error messages
5. **Handle Errors Gracefully**: Don't crash on bad input
6. **Test Interaction**: Click all buttons, try edge cases

### Example Window Creation

```csharp
// XAML (NewWindow.axaml)
<Window xmlns="https://github.com/avaloniaui"
        Title="New Window" Width="400" Height="300">
    <StackPanel Margin="10">
        <TextBlock Text="Window Content" />
        <Button Content="Action" Click="ActionButton_Click" />
    </StackPanel>
</Window>

// Code-Behind (NewWindow.axaml.cs)
public partial class NewWindow : Window
{
    public NewWindow()
    {
        InitializeComponent();
    }
    
    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        // Handle action
        this.Close();
    }
}
```

---

**Related Documentation**:
- [Architecture Overview](ARCHITECTURE.md) - System design
- [Code Organization](CODE_ORGANIZATION.md) - File locations
- [Getting Started](GETTING_STARTED.md) - Setup guide
- [Game Systems Guide](GAME_SYSTEMS.md) - Feature details
