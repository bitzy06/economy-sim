# Economy Sim

A complex economic simulation game built with Avalonia and .NET 8.0.

## 📖 Documentation

Comprehensive documentation is available in the **[`docs/`](docs/)** directory.

**Start here**: [Documentation Index](docs/README.md)

### Quick Links

- **New to the project?** → [Getting Started Guide](docs/GETTING_STARTED.md)
- **Want to understand the code?** → [Code Organization](docs/CODE_ORGANIZATION.md)
- **Need system architecture?** → [Architecture Overview](docs/ARCHITECTURE.md)
- **Looking for flow charts?** → [Flow Charts & Diagrams](docs/FLOWCHARTS.md)
- **Want to understand game mechanics?** → [Game Systems Guide](docs/GAME_SYSTEMS.md)

## 🚀 Quick Start

### Prerequisites
- .NET 8.0 SDK
- (Optional) Python 3.10+ for data generation scripts

### Build and Run

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build -v minimal

# Run the game
dotnet run --project "Economy sim.csproj"
```

See the [Getting Started Guide](docs/GETTING_STARTED.md) for detailed setup instructions.

## 🎮 What is Economy Sim?

Economy Sim is a complex economic simulation that models:

- **Multi-level political entities** (Countries → States → Cities)
- **Production chains** with factories converting inputs to outputs
- **Dynamic markets** with supply/demand pricing
- **International trade** between nations and corporations
- **Government systems** with policies, taxes, and budgets
- **Real-world geography** integration with map rendering
- **AI corporations** managing factories and production

## 📂 Project Structure

```
economy-sim/
├── docs/           # Comprehensive documentation
├── Views/          # Avalonia UI windows and views
├── Assets/         # Images and resources
├── *.cs            # C# source files (core systems)
├── *.py            # Python utility scripts
└── Economy sim.csproj  # Project file
```

See [Code Organization](docs/CODE_ORGANIZATION.md) for detailed file structure.

## 🛠️ Technology Stack

- **.NET 8.0** - Runtime framework
- **C# 12** - Programming language  
- **Avalonia 11.3.2** - Cross-platform UI framework
- **GDAL** - Geospatial data processing
- **SkiaSharp** - 2D graphics rendering
- **NetTopologySuite** - Geometric operations

## 📚 Documentation Contents

The [`docs/`](docs/) folder contains:

1. **[README.md](docs/README.md)** - Documentation index and overview
2. **[GETTING_STARTED.md](docs/GETTING_STARTED.md)** - Setup and development guide
3. **[ARCHITECTURE.md](docs/ARCHITECTURE.md)** - System architecture and design
4. **[FLOWCHARTS.md](docs/FLOWCHARTS.md)** - Visual process diagrams
5. **[GAME_SYSTEMS.md](docs/GAME_SYSTEMS.md)** - Detailed game mechanics
6. **[DATA_MODEL.md](docs/DATA_MODEL.md)** - Entity relationships and data structures
7. **[CODE_ORGANIZATION.md](docs/CODE_ORGANIZATION.md)** - File structure guide
8. **[UI_STRUCTURE.md](docs/UI_STRUCTURE.md)** - User interface documentation

## 🤝 Contributing

Before contributing:
1. Read the [Getting Started Guide](docs/GETTING_STARTED.md)
2. Review the [Architecture Overview](docs/ARCHITECTURE.md)
3. Check the [Code Organization](docs/CODE_ORGANIZATION.md)
4. Follow code style guidelines in `AGENTS.md`

## 📄 License

See repository license file for details.

---

**For detailed documentation, visit the [`docs/`](docs/) directory.**
