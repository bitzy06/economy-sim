# UI Changes Mockup

## GameView Bottom Control Bar - Before and After

### BEFORE (Original):
```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│ [Diplomacy] [Trade] [Construction] [Set Policy] [Economy] [Statistics]          [Menu] │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

### AFTER (With Political Borders):
```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│ [Diplomacy] [Trade] [Construction] [Set Policy] │Terrain│Political│ [Economy] [Statistics] [Menu] │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

## Button States

### Terrain View Active:
```
│ [Terrain] │Political│  <- Terrain button highlighted (dark blue)
```

### Political View Active:
```
│ Terrain │[Political]│  <- Political button highlighted (dark red)
```

## Map Display Modes

### Terrain Mode:
- Shows standard terrain tiles (elevation, land/water, etc.)
- Regular terrain rendering pipeline

### Political Mode:
- Shows country boundaries with color-coded regions
- Colors assigned from country_colors.json
- Boundaries based on CShapes-2.0.shp data for January 1950
- Memory-efficient separate layer system

## Visual Representation of Political Map Colors

Example color assignments from the generated JSON:
- USA: Blue (#3B82F6)
- Canada: Red (#EF4444) 
- Mexico: Green (#22C55E)
- UK: Orange (#F59E0B)
- France: Purple (#8B5CF6)
- Germany: Pink (#EC4899)
- Russia: Dark Red (#DC2626)
- China: Yellow (#FACC15)

The political view would show these countries as solid colored regions with their borders clearly defined, providing a clear visual distinction between different political entities as they existed in January 1950.