# Agent Development Notes

This repository contains a Windows Forms project written for .NET 8.0 as well as a few Python utilities.
It is assumed contributors are developing on Windows with the .NET 8.0 SDK installed.

## Building the game

Run the following commands from the repository root:

```bash
# restore NuGet packages and compile
 dotnet restore
 dotnet build "economy sim.sln" -v minimal
```

Use `dotnet run --project "economy sim.csproj"` to launch the game after building.

## Python scripts

Python utilities (e.g. `generate_world.py`, `fetch_etopo1.py`, `create_country_mask.py`) expect Python 3.10+
and optional packages such as `fiona`, `rasterio` and `shapely` for GIS functions.
Run them with `python3 <script>` from the repository root. These scripts download or generate large data files;
avoid committing any data or log output under `data/` or `logs/`.

## Code style

* C# code follows standard .NET naming conventions: PascalCase for public members and camelCase for locals.
* Python code should comply with PEP 8 and be formatted with 4‑space indents.

## Validation before committing

1. If C# files were changed, run `dotnet build "economy sim.sln"` to ensure the project compiles.
2. If Python files were changed, run `python -m py_compile <file>` for each modified script.
3. Do not commit `world_setup.json`, generated files under `data/`, or contents of the `logs/` directory.
