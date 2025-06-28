
# Using Natural Earth for Town and City Placement in Simulation Games

## Overview

This guide explains how to use Natural Earth’s built-in datasets to represent towns and cities in your simulation game without relying on OpenStreetMap. Natural Earth provides global coverage with manageable file sizes and enough metadata for economic, geographic, and visual logic.

---

## Recommended Dataset

### `ne_10m_populated_places`

This shapefile provides a list of cities and towns globally with the following useful fields:

| Field | Description |
|-------|-------------|
| `NAME` | Name of the city/town |
| `LAT`, `LON` | Location coordinates |
| `ADM0NAME` | Country |
| `ADM1NAME` | Province/State |
| `FEATURECLA` | Type (capital, populated place, etc.) |
| `POP_MAX` | Estimated maximum population |
| `SCALERANK` | Ranking for importance/zoom-level use |

---

## What You Can Do With It

### 1. World Placement of Towns & Cities
- Directly use lat/lon coordinates to place towns in your simulation.
- Filter by `SCALERANK` or `POP_MAX` to limit how many towns you use:
  - `SCALERANK` 0–2: Major cities only
  - `SCALERANK` 0–5: Includes regional towns
  - Full set: Thousands of points globally

### 2. Region Mapping
- Use `ADM1NAME` and `ADM0NAME` to assign each town to a province or country.
- Link with Natural Earth’s `states/provinces` polygons for administrative structure.

### 3. Population-Scaled Simulation
- Use `POP_MAX` to:
  - Scale economic demand or workforce size
  - Drive procedural visuals (small sprite vs. skyline)
  - Prioritize which towns get simulation logic

### 4. Procedural Visuals
- Place 1–3 buildings based on population
- Use fixed hashes of town names to seed layout patterns
- Display nameplates or icons at appropriate zoom levels

---

## Advantages Over Other Sources

| Feature | Natural Earth |
|--------|----------------|
| Global coverage | ✅ |
| Real-world locations | ✅ |
| Administrative mapping | ✅ |
| Scalable detail levels | ✅ |
| Small file size (~1 MB) | ✅ |
| Easy to parse | ✅ |
| Boundaries (polygons) | ❌ (point data only) |
| Minor villages | ❌ (filtered out for relevance) |

---

## Summary

Natural Earth’s `populated_places` dataset is:
- Lightweight
- Global
- Easy to filter
- Great for towns, cities, and mid-scale simulation

It provides all the essential information to drive settlement-based simulation mechanics — without the complexity of parsing or filtering massive datasets.

Use it in combination with Natural Earth’s `states/provinces` for a complete region-based world simulation.

