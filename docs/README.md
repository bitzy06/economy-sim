# Economy Sim Documentation

Welcome to the Economy Sim documentation! This directory contains comprehensive guides to help you understand and work with the project.

## 📚 Documentation Index

### Getting Started
- **[Getting Started Guide](GETTING_STARTED.md)** - Developer onboarding, setup, and first steps
- **[Code Organization](CODE_ORGANIZATION.md)** - Project structure and file organization

### Architecture & Design
- **[Architecture Overview](ARCHITECTURE.md)** - System architecture and component relationships
- **[Flow Charts](FLOWCHARTS.md)** - Visual diagrams of key processes and workflows
- **[Data Model](DATA_MODEL.md)** - Data structures and entity relationships

### Game Systems
- **[Game Systems Guide](GAME_SYSTEMS.md)** - Detailed explanation of all game systems
- **[UI Structure](UI_STRUCTURE.md)** - User interface organization and navigation

## 🎮 What is Economy Sim?

Economy Sim is a complex economic simulation game built with Avalonia (cross-platform .NET UI framework) and .NET 8.0. The game simulates a world economy with:

- **Countries, States, and Cities** - Hierarchical political and economic entities
- **Factories and Production** - Complex production chains with input/output goods
- **Markets and Trade** - Local and international trade systems
- **Corporations** - AI-controlled economic actors that own and operate factories
- **Government Systems** - Political parties, policies, and financial systems
- **Population Dynamics** - Different social classes with varied consumption patterns
- **Construction Projects** - Dynamic building and infrastructure development
- **Map Integration** - Real-world geographical data with multi-resolution rendering

## 🏗️ Technology Stack

- **Framework**: .NET 8.0
- **UI**: Avalonia 11.3.2 (cross-platform XAML-based UI)
- **GIS Libraries**: GDAL, NetTopologySuite for geospatial data
- **Graphics**: SkiaSharp, ImageSharp for rendering
- **Architecture**: MVVM pattern with event-driven components

## 🗂️ Quick Navigation

| What You Want to Do | Where to Go |
|---------------------|-------------|
| Set up development environment | [Getting Started Guide](GETTING_STARTED.md) |
| Understand overall architecture | [Architecture Overview](ARCHITECTURE.md) |
| Learn about a specific game system | [Game Systems Guide](GAME_SYSTEMS.md) |
| Understand data structures | [Data Model](DATA_MODEL.md) |
| Find where code is located | [Code Organization](CODE_ORGANIZATION.md) |
| See visual process flows | [Flow Charts](FLOWCHARTS.md) |
| Work with the UI | [UI Structure](UI_STRUCTURE.md) |

## 📖 Additional Resources

In the root directory, you'll also find:
- `AGENTS.md` - Instructions for AI agents working on the codebase
- Multiple `*_README.md` files - Specific feature documentation
- `*_SUMMARY.md` files - Implementation summaries for major features

## 🤝 Contributing

Before making changes:
1. Read the [Getting Started Guide](GETTING_STARTED.md)
2. Review the [Architecture Overview](ARCHITECTURE.md)
3. Check the relevant system documentation in [Game Systems Guide](GAME_SYSTEMS.md)
4. Follow the code style guidelines in `AGENTS.md`

## 📝 Documentation Organization

Each documentation file follows a consistent structure:
- **Overview** - High-level summary
- **Detailed Sections** - In-depth explanations
- **Code Examples** - When applicable
- **Diagrams** - Visual representations
- **Related Links** - Cross-references to other docs

---

**Last Updated**: 2025-11-21
**Version**: 1.0
