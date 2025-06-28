
# Economy Sim World Design with Natural Earth Data

## Overview

This document outlines how to build a full-featured economy simulation game using **Natural Earth** datasets for terrain structuring, political boundaries, towns, cities, and regional logic — **without relying on OpenStreetMap (OSM)**. It focuses on performance, realism, and gameplay depth while maintaining low overhead.

---

## Why Natural Earth?

Natural Earth offers:
- Free, public domain GIS data
- Global coverage
- Multiple detail levels (1:10m, 1:50m, 1:110m)
- Small file sizes (~1–50 MB per dataset)
- Easy integration with map engines and simulations

---

## Layered Simulation Design

| Layer | Source | Purpose |
|-------|--------|---------|
| Terrain | Your generator | Elevation, water, land types |
| Country Borders | `ne_10m_admin_0_countries` | National divisions |
| Provinces/States | `ne_10m_admin_1_states_provinces` | Economic + simulation regions |
| Cities/Towns | `ne_10m_populated_places` | Population centers and simulation anchors |

---

## 1. Provinces and States

### Dataset: `ne_10m_admin_1_states_provinces.shp`

Use this to break the world into simulation regions.

**Includes:**
- Province/state names
- Country linkage
- Population estimates (approx.)
- Geometric polygons for map overlay or interaction

**Use Cases:**
- Grouping cities
- Assigning simulation logic to areas (taxes, policies, regional production)
- Drawing labeled map regions

---

## 2. Cities and Towns

### Dataset: `ne_10m_populated_places.shp`

Point-based data for towns and cities.

**Key Fields:**
- `NAME`, `LAT`, `LON`
- `POP_MAX`: max estimated population
- `SCALERANK`: importance (lower is more important)
- `ADM0NAME`, `ADM1NAME`: Country and region

**Use Cases:**
- Spawn points for economy entities
- Interaction hubs
- Visual settlement placement
- Procedural scaling based on population

---

## 3. Using Towns Effectively

### Town Tiers

| Type | Filtering | Purpose |
|------|-----------|---------|
| Major cities | `SCALERANK <= 2` | Simulation & visual focal points |
| Towns | `SCALERANK <= 6` | Gameplay density, regional trade |
| Minor places | `SCALERANK > 6` | Optional filler or background |

**Procedural Visuals**:
- Use hash of name/pop to seed house layouts
- No need to store geometry
- Visuals vary by `POP_MAX`

**Grouping**:
- Assign towns to provinces using `ADM1NAME`
- Enables shared simulation clusters

---

## 4. Region-Based Simulation Logic

With states/provinces and towns in place, structure simulation like this:

### Per-Region Objects:
```json
{
  "name": "Andalucía",
  "country": "Spain",
  "population": 8300000,
  "towns": ["Seville", "Granada", "Córdoba"],
  "agriculture_output": 1.2,
  "tax_rate": 0.07
}
```

### Benefits:
- Efficient simulation batching
- Logical trade routes between towns
- Easy event targeting (e.g. "Flood in Punjab")

---

## 5. Visual Layer Composition

| Layer | Data Source | Description |
|-------|-------------|-------------|
| Terrain | Your own | Base land and water |
| Admin Borders | `admin_0` / `admin_1` | Country and state outlines |
| Towns & Cities | `populated_places` | Dots + procedural buildings |
| Labels | Natural Earth names | Province, city labels |

Overlay each layer dynamically based on zoom level.

---

## 6. Performance and Scalability

- File sizes stay <50 MB total
- No need to parse OSM or large vector maps
- Easy to LOD filter towns and provinces by relevance
- Works offline and loads fast

---

## 7. Optional Enhancements

- Add hand-authored data to provinces (custom events, storylines)
- Use `POP_MAX` to distribute economic weight or unlock milestones
- Link cities with abstract trade paths based on proximity and population

---

## Summary

You can build a full-scale, detailed, and historically grounded economy sim world using **only Natural Earth** data. It supports:

- Real city and region names
- Accurate boundaries
- Scalable detail levels
- Simulation grouping

…and it keeps your data pipeline lightweight and manageable.

