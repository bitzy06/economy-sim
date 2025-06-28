
# Efficient Town Integration in Economy Sim

## Overview

Towns add richness and realism to your simulation world, but simulating or rendering them like cities creates significant overhead. This document outlines a performance-conscious strategy to include towns in a scalable and visually coherent way.

---

## 1. Settlement Tiers

| Tier | Type     | Approximate Count | Detail Level        | Storage Strategy |
|------|----------|-------------------|---------------------|------------------|
| 1    | Cities   | ~2,000            | Full sim + visual   | Overlays + sim   |
| 2    | Towns    | ~20,000–100,000   | Simplified visuals  | Proxy + metadata |
| 3    | Villages | ~500,000+         | Minimal visibility  | Sprite/glyph only|

---

## 2. Towns as Proxy Objects

- Represent each town as a single sprite or 2D decal.
- Avoid generating individual raster tiles for towns.
- Store town metadata (position, population, name) separately:
```json
{
  "name": "Brookfield",
  "lat": 43.12,
  "lon": -88.03,
  "type": "town",
  "population": 3600
}
```

---

## 3. Level-of-Detail (LOD) Rendering

| Zoom Level | Cities                       | Towns                          | Villages    |
|------------|------------------------------|--------------------------------|-------------|
| World      | Dot with label               | Hidden                         | Hidden      |
| Country    | Skyline sprite + name        | Dot with name                  | Hidden      |
| Region     | Procedural city blocks       | Static decal (no sim)          | Glyph only  |
| Street     | Full simulation & navigation | Optional interaction area only | Not shown   |

Only cities reach the street-level simulation depth.

---

## 4. Shared Simulation Regions

- Group towns into "Regional Clusters" (e.g., 10 towns per `SimRegion`).
- Simulation occurs at region level:
```c
townLocalDemand = simRegion.food * town.popWeight;
```
- This avoids per-town economic calculations.

---

## 5. Procedural Town Visuals

- Generate small town visuals procedurally at runtime.
- Use fixed random seed per town to create 1–3 sprite buildings.
- Towns look consistent but require zero disk storage.

---

## 6. Town Data Sources

- Use OSM tags like `place=town` and `place=village`.
- Preprocess into tile-indexed or KD-tree structure for fast lookup.
- Only load towns within current view radius.

---

## 7. Player Interaction with Towns

Towns can support:

- Basic trade or rest stops
- Recruiting or random events
- Navigation and pathfinding connectivity

But **do not require** complex simulation or entity spawning.

---

## Summary

Towns are lightweight, high-impact content. To support them efficiently:

- Use proxy visuals
- Share simulation state in clusters
- Gate depth of simulation by zoom
- Generate visuals procedurally

This keeps the game scalable while delivering immersive, populated worlds.

