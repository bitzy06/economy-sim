
# Economy Sim – Visual & Simulation Layer Design

## Overview

This document outlines a high-level design strategy for building an economy simulation game with rich visuals, semi-real-time updates, and historically accurate human geography—all without modifying the underlying terrain tiles.

---

## 1. Layered Rendering System

| Layer | Purpose | Source | Update Frequency |
|-------|---------|--------|------------------|
| **0 – Terrain** | Elevation + water | Pre-generated PNG tiles | Never |
| **1 – Static Human Geography** | Historical cities, old roads/rails | Rasterized from GIS | Rarely (patch-level) |
| **2 – Evolving Infrastructure** | Roads, rails, buildings | Game simulation | Semi-realtime |
| **3 – Dynamic Entities** | Cars, trains, planes, player | ECS or sprite-based | Every frame |

Each layer is composited during draw without altering the base terrain.

---

## 2. Tile Grid Consistency

All overlays share the same tile key:

```
(cellSize, tileX, tileY)
```

Overlays load dynamically based on terrain tiles and can be asynchronously generated.

---

## 3. Populating Historical Data

### Data Sources

- Natural Earth (cities)
- OSM Full History / RailDataHub (rails)
- 1950s roads (or era-specific snapshots)

### Implementation

- Convert coordinates using your map projection
- Bake vector data into overlay tiles (e.g. PNGs)
- Stored in `tile_cache/human_static/{cellSize}/{x}_{y}.png`

---

## 4. City-Level Detail

| Zoom | Detail Level | Technique |
|------|--------------|-----------|
| Province | Dot + label | Single sprite |
| City | Skyline, roads | Static overlay |
| Street | Buildings, cars | Procedural overlay |
| Character | Playable sprite | ECS / GameObjects |

Only detailed views require runtime updates.

---

## 5. Evolving Infrastructure (Semi-Realtime)

### Block Structure

```text
struct Block {
    int tileX, tileY;
    byte roadMask;
    byte railMask;
    byte zoneType;
}
```

- Dirty blocks when modified
- Re-rasterize overlay tile when flushed
- RAM-only; evict on zoom-out or inactivity

---

## 6. Dynamic Entities

### Vehicles

- Use world-space polylines
- Lerp sprites between segment endpoints

### Planes

- Great-circle interpolation
- Drawn as floating entities

### Player

- Nav mesh for city block
- No tiles touched; dynamic spawn/despawn

---

## 7. Data Flow

```
Sim Tick
 ├ Update economy/infrastructure
 ├ Mark dirty overlays
 └ Push events

Render Loop
 ├ Rebuild overlays if dirty
 ├ Compose Terrain + Overlays → Frame
 └ Draw Dynamic Entities
```

No unnecessary writes; clean separation of simulation and rendering.

---

## 8. Optimization Tips

- LRU cache overlays
- Async PNG compression
- Sprite batching per tile row

---

## 9. Future Experiments

- GPU instancing for vehicles
- Procedural zoning & evolution
- Mod support via shapefile/GeoJSON

---

## Summary

Use a strict separation of terrain, static overlays, dynamic overlays, and real-time entities to maintain performance and modularity. This approach provides a solid base for simulation accuracy, interactive detail, and visual fidelity—all without corrupting terrain tiles.
