
# Full Economy Sim World Design with Natural Earth

## Overview

This guide outlines how to build a rich, layered economy simulation using only **Natural Earth** datasets — with structured strategies for world rendering, simulation zones, town integration, and visual composition. No OpenStreetMap or complex GIS parsing is required.

---

## 1. Simulation Layers

| Layer | Source | Content | Update Frequency |
|-------|--------|---------|------------------|
| Terrain | Your generator | Elevation, water, land | Never |
| Admin Borders | NE admin levels | Countries, provinces | Static |
| Human Geography | NE populated places | Towns, cities | Infrequent |
| Infrastructure | Procedural | Roads, zones, overlays | Semi-realtime |
| Dynamic Entities | ECS/Sprites | Player, vehicles, etc. | Realtime (60 FPS) |

---

## 2. Provinces as Simulation Zones

**Dataset:** `ne_10m_admin_1_states_provinces.shp`

- Use each polygon to define a simulation region.
- Assign cities/towns to regions via metadata (`ADM1NAME`).
- Attach simulation logic like:
  - Resource output
  - Population stats
  - Regional effects

---

## 3. Town and City Placement

**Dataset:** `ne_10m_populated_places.shp`

- Use point data (`LAT`, `LON`) to position towns.
- Filter by `SCALERANK` to control density.
- Use `POP_MAX` for procedural detail scaling.

**Example Use:**
- SCALERANK 0–2: Full detail cities
- SCALERANK 3–6: Mid-detail towns (icon or stub visuals)
- SCALERANK >6: Only shown on zoomed-out maps

---

## 4. Visual Rendering Architecture

### Layered System (Composited in Viewport)

| Layer | Description |
|-------|-------------|
| 0. Terrain | Generated ground texture |
| 1. Static Overlay | Admin borders, province fills |
| 2. Town Visuals | Procedural icons or buildings |
| 3. Dynamic Overlay | Roads, factories, game assets |
| 4. Entities | Player, vehicles, planes |

Use the same tile grid for each: `(cellSize, tileX, tileY)`

---

## 5. Tile Strategy

- Match town/city positions to the same coordinate projection used for terrain.
- Overlays (roads, towns) are generated in-memory using the same tile keys.
- Dynamic overlays are not stored — just re-generated when tiles are dirty.

---

## 6. Procedural Town Visuals

At appropriate zoom:
- Generate 1–3 building sprites per town using a seeded hash of name/population.
- Align to terrain and province tile.
- Label via point metadata.

Example logic:
```text
if POP_MAX > 500000 → skyline sprite
if POP_MAX > 10000 → clustered houses
else → single pixel icon
```

---

## 7. Simulation Flow

```
Sim Tick (e.g. every 200ms)
 ├ Update provinces (resources, population)
 ├ Process town-level trade/events
 └ Flag tiles as dirty if infrastructure changed

Render Loop (60 FPS)
 ├ Check for dirty overlays
 │   └ Re-generate if needed
 ├ Compose base terrain + overlays
 └ Draw all dynamic sprites
```

Each layer is independent; base terrain is never altered.

---

## 8. Performance Strategy

- Use LRU caches for tile overlays
- Regenerate visuals asynchronously when needed
- Rasterize static overlays (borders, towns) only once
- Use procedural rendering for towns, not stored geometry
- Compress final tile composites as needed

---

## 9. Summary

Using only Natural Earth, you can:

- Build a high-performance global simulation
- Accurately place towns and regions
- Structure visual and simulation layers cleanly
- Avoid complex parsing or external APIs

All without touching OSM or heavyweight GIS infrastructure.

