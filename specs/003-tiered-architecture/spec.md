---
title: Tiered Plugin Architecture
status: draft
created: 2025-10-29
updated: 2025-10-29
author: Claude
tags: [architecture, plugins, design]
related: []
---

# Tiered Plugin Architecture

## Overview

This document describes the tiered architecture for organizing plugins with explicit priority ranges and dependency rules.

## Problem Statement

Previously, the plugin system lacked a clear organizational structure:

- **No load order guarantees**: Essential plugins could load after game-specific ones
- **Dependency confusion**: Game-specific plugins could depend on other game-specific plugins without clear boundaries
- **Priority field unused**: `PluginManifest.Priority` existed but wasn't used in loading
- **Maintenance challenges**: Difficult to distinguish reusable systems from game-specific code

## Solution: Three-Tier Architecture

### Tier 1: Essential Systems (Priority 1-9999)

**Purpose**: Foundation that everything depends on

**Location**: `dotnet/framework/`

**Load Order**: First (lowest priority numbers)

#### Core (1-4999)
- Contracts and abstractions
- No implementations or business logic

**Assemblies**:
- `LablabBean.Core` (100) - Domain models, interfaces
- `LablabBean.Infrastructure` (200) - DI, logging, config
- `LablabBean.Plugins.Contracts` (300) - Plugin system contracts
- All `LablabBean.Contracts.*` - Service contracts (Config, Resource, Scene, Input, etc.)
- `LablabBean.SourceGenerators.*` - Build-time code generation

#### Plugin (5000-9999)
- Core system implementations
- Plugin infrastructure

**Assemblies**:
- `LablabBean.Plugins.Core` (5000) - Plugin loader/registry
- `LablabBean.Plugins.Management` (5100) - Tier management

**Dependency Rules**:
- ✅ Can depend on: Other Essential Systems only
- ❌ Cannot depend on: Game General or Game Specific systems

### Tier 2: Game General Systems (Priority 10000-19999)

**Purpose**: Reusable game systems that any game could use

**Location**: `dotnet/plugins/` (publish as NuGet packages)

**Load Order**: After Essential, before Game Specific

#### Core (10000-14999)
- Reusable game contracts
- Genre-agnostic abstractions

**Proposed Assemblies** (to be created):
- `LablabBean.Contracts.Character` (10100) - Character system contracts
- `LablabBean.Contracts.Avatar` (10200) - Avatar system contracts
- `LablabBean.Contracts.Stats` (10300) - Stats system contracts
- `LablabBean.Contracts.Ability` (10400) - Ability system contracts
- `LablabBean.Contracts.Environment` (10500) - Environment system contracts
- `LablabBean.Contracts.Quest` (10600) - Quest system contracts (generic)
- `LablabBean.Contracts.Progression` (10700) - Progression system contracts
- `LablabBean.Contracts.GameAI` (10800) - Game AI contracts
- `LablabBean.Contracts.Dialogue` (10900) - Dialogue system contracts
- `LablabBean.Contracts.Particle` (11000) - Particle system contracts

#### Plugin (15000-19999)
- Reusable game implementations
- Infrastructure plugins

**Existing Assemblies**:
- `LablabBean.Plugins.Character` (15100) - Character system
- `LablabBean.Plugins.Avatar` (15200) - Avatar system
- `LablabBean.Plugins.Stats` (15300) - Stats system
- `LablabBean.Plugins.Inventory` (15400) - Generic inventory
- `LablabBean.Plugins.StatusEffects` (15500) - Generic status effects
- `LablabBean.Plugins.Quest` (15600) - Quest system
- `LablabBean.Plugins.Progression` (15700) - Progression system
- `LablabBean.Plugins.GameAI` (15800) - Game AI
- `LablabBean.Plugins.Dialogue` (15900) - Dialogue system
- `LablabBean.Plugins.Particle` (16000) - Particle system
- `LablabBean.Plugins.Serialization.Json` (16100)
- `LablabBean.Plugins.PersistentStorage.Json` (16200)
- `LablabBean.Plugins.ObjectPool.Standard` (16300)
- `LablabBean.Plugins.Scheduler.Standard` (16400)
- `LablabBean.Plugins.Localization.Json` (16500)
- `LablabBean.Plugins.Analytics` (16600)
- `LablabBean.Plugins.ConfigManager` (16700)
- `LablabBean.Plugins.ResourceLoader` (16800)
- `LablabBean.Plugins.SceneLoader` (16900)
- `LablabBean.Plugins.InputHandler` (17000)
- `LablabBean.Plugins.InputActionMap` (17100)

**Dependency Rules**:
- ✅ Can depend on: Essential Systems, other Game General Systems
- ❌ Cannot depend on: Game Specific systems

### Tier 3: Game Specific Systems (Priority 20000-29999)

**Purpose**: Dungeon crawler-specific implementations

**Location**:
- Contracts: `dotnet/framework/` (in this repo)
- Plugins: Platform-specific repos (console/windows/unity)

**Load Order**: Last (highest priority numbers)

#### Core (20000-24999)
- Game-specific contracts
- Dungeon crawler abstractions

**Proposed Assemblies** (to be created):
- `LablabBean.DungeonCrawler.Contracts.NPC` (20100)
- `LablabBean.DungeonCrawler.Contracts.Merchant` (20200)
- `LablabBean.DungeonCrawler.Contracts.Boss` (20300)
- `LablabBean.DungeonCrawler.Contracts.Hazards` (20400)
- `LablabBean.DungeonCrawler.Contracts.Spells` (20500)

#### Plugin (25000-29999)
- Game-specific implementations
- Platform-specific variations

**Existing Assemblies** (need refactoring):
- `LablabBean.Plugins.NPC` (25100) → Split by platform
- `LablabBean.Plugins.Merchant` (25200) → Split by platform
- `LablabBean.Plugins.Boss` (25300) → Split by platform
- `LablabBean.Plugins.Hazards` (25400) → Split by platform
- `LablabBean.Plugins.Spells` (25500) → Split by platform
- `LablabBean.Plugins.MockGame` (25600) - Test harness

**Platform-Specific Structure**:
```
lablab-bean-console/
├── plugins/
│   ├── LablabBean.DungeonCrawler.Console.NPC/
│   ├── LablabBean.DungeonCrawler.Console.Merchant/
│   └── ...

lablab-bean-windows/
├── plugins/
│   ├── LablabBean.DungeonCrawler.Windows.NPC/
│   ├── LablabBean.DungeonCrawler.Windows.Merchant/
│   └── ...

lablab-bean-unity/
├── plugins/
│   ├── LablabBean.DungeonCrawler.Unity.NPC/
│   ├── LablabBean.DungeonCrawler.Unity.Merchant/
│   └── ...
```

**Dependency Rules**:
- ✅ Can depend on: Essential Systems, Game General Systems, other Game Specific systems
- ⚠️ Warn if depending on platform-specific plugins from other platforms

## Implementation Details

### Priority-Based Loading

The `DependencyResolver` now implements priority-based topological sorting:

1. **Resolve dependencies** using Kahn's algorithm
2. **Within each dependency level**, sort by priority (ascending)
3. **Load plugins** in the resulting order

**Example Load Sequence**:
```
1. LablabBean.Core (100)
2. LablabBean.Infrastructure (200)
3. LablabBean.Plugins.Contracts (300)
4. LablabBean.Plugins.Core (5000)
5. LablabBean.Contracts.Character (10100)
6. LablabBean.Plugins.Character (15100)
7. LablabBean.DungeonCrawler.Contracts.NPC (20100)
8. LablabBean.DungeonCrawler.Console.NPC (25100)
```

### Configuration Management

#### Per-Application Configuration

Each application has a `tiers.json` file:

```
lablab-bean-console/
├── dotnet/
│   └── app/
│       └── LablabBean.Console/
│           └── tiers.json
```

#### Configuration Schema

See `LablabBean.Plugins.Management/tiers.example.json` for a complete example.

Key sections:
- **tiers**: Tier definitions with priority ranges
- **categories**: Core vs Plugin categorization within tiers
- **assemblies**: Known plugins with assigned priorities
- **dependencyRules**: Enforcement settings and allowed dependencies

#### Validation

The `TierManager` validates:
1. Priority is within tier/category range
2. Dependencies respect tier hierarchy
3. No circular dependencies
4. No missing hard dependencies

### Plugin Manifest Requirements

Each `plugin.json` must specify priority:

```json
{
  "id": "LablabBean.Plugins.Character",
  "name": "Character System",
  "version": "1.0.0",
  "priority": 15100,
  "dependencies": [
    {
      "id": "LablabBean.Contracts.Character",
      "optional": false
    }
  ]
}
```

## Migration Plan

### Phase 1: Core Infrastructure (Completed)

- ✅ Implement priority-based loading in `DependencyResolver`
- ✅ Create `LablabBean.Plugins.Management` project
- ✅ Design tier configuration schema
- ✅ Document architecture

### Phase 2: Essential Tier Refactoring

1. Audit existing `LablabBean.Contracts.*` assemblies
2. Assign priorities (1-4999 for Core, 5000-9999 for Plugin)
3. Update `plugin.json` files with priorities
4. Create initial `tiers.json` for each application

### Phase 3: Game General Tier Creation

1. Create new contracts assemblies:
   - `LablabBean.Contracts.Character`
   - `LablabBean.Contracts.Avatar`
   - `LablabBean.Contracts.Stats`
   - etc.

2. Move/refactor existing plugins:
   - `LablabBean.Plugins.Quest` → Promote to Game General
   - `LablabBean.Plugins.Progression` → Promote to Game General
   - Update priorities to 10000-19999 range

3. Create new plugin implementations where needed

### Phase 4: Game Specific Tier Refactoring

1. Create contracts assemblies:
   - `LablabBean.DungeonCrawler.Contracts.NPC`
   - `LablabBean.DungeonCrawler.Contracts.Merchant`
   - etc.

2. Split platform-specific plugins:
   - Move `LablabBean.Plugins.NPC` to:
     - `lablab-bean-console/plugins/LablabBean.DungeonCrawler.Console.NPC`
     - `lablab-bean-windows/plugins/LablabBean.DungeonCrawler.Windows.NPC`
     - `lablab-bean-unity/plugins/LablabBean.DungeonCrawler.Unity.NPC`

3. Update priorities to 20000-29999 range

### Phase 5: Validation & Testing

1. Enable tier validation in applications
2. Run `TierManager.ValidateTiers()` on startup
3. Fix any tier violations
4. Test load order across all platforms
5. Update documentation

## Benefits

### Clear Separation of Concerns
- **Essential**: Foundation (must never depend on game logic)
- **Game General**: Reusable across games (no game-specific assumptions)
- **Game Specific**: Tied to dungeon crawler (can use anything)

### Deterministic Loading
- Priority + dependencies = predictable order
- No more "plugin X loaded before Y" bugs
- Clear debugging with load order logging

### Reusability
- Game General plugins become NuGet packages
- Easy to share systems between projects
- Clear API contracts via Core assemblies

### Better Testing
- Test each tier independently
- Mock dependencies easily
- Validate tier rules automatically

### Easier Maintenance
- Know where to put new code
- Update framework without touching game code
- Platform-specific code stays in platform repos

## Best Practices

### 1. Explicit Priorities
Always specify priority in `plugin.json`:
```json
{
  "priority": 15100  // Don't rely on defaults
}
```

### 2. Respect Tier Boundaries
- Essential plugins: Keep them minimal and generic
- Game General plugins: No game-specific assumptions
- Game Specific plugins: OK to be specialized

### 3. Use Contracts
Always define contracts before implementations:
1. Create `LablabBean.Contracts.X` (Core tier)
2. Create `LablabBean.Plugins.X` (Plugin tier)
3. Implementations depend on contracts, not vice versa

### 4. Document Dependencies
Add descriptions to assemblies in `tiers.json`:
```json
{
  "id": "LablabBean.Plugins.Character",
  "priority": 15100,
  "description": "Character system implementation with stats and inventory"
}
```

### 5. Validate Early
Enable tier validation in development:
```csharp
var tierManager = new TierManager(logger);
tierManager.LoadConfiguration("tiers.json");

var result = tierManager.ValidateTiers(manifests);
if (!result.IsValid)
{
    // Log errors and fail fast
    throw new InvalidOperationException("Tier validation failed");
}
```

## Future Enhancements

### Auto-Assignment
Generate priorities automatically based on:
- Assembly dependencies
- Naming conventions
- Manifest metadata

### Visual Tools
- Dependency graph visualization
- Tier hierarchy browser
- Load order simulator

### NuGet Integration
- Publish Game General tier as NuGet packages
- Automatic tier inference from package metadata
- Version compatibility checking

### IDE Support
- Visual Studio extension for tier management
- IntelliSense for `tiers.json`
- Refactoring tools for tier migrations

## References

- `LablabBean.Plugins.Management/README.md` - Usage guide
- `LablabBean.Plugins.Management/tiers.example.json` - Configuration example
- `LablabBean.Plugins.Core/DependencyResolver.cs` - Implementation
- `LablabBean.Plugins.Contracts/PluginManifest.cs` - Manifest schema

## Changelog

- **2025-10-29**: Initial architecture design and implementation
- **2025-10-29**: Created `LablabBean.Plugins.Management` project
- **2025-10-29**: Implemented priority-based loading in `DependencyResolver`

## Questions & Answers

### Q: Can I have multiple plugins with the same priority?

**A**: Yes, but they'll be sorted by plugin ID as a tiebreaker. For predictable load order, use unique priorities.

### Q: What happens if my priority is outside the tier range?

**A**: `TierManager.ValidateTiers()` will report an error and the plugin may fail to load (depending on enforcement settings).

### Q: Can Game General plugins depend on Game Specific plugins?

**A**: No. This violates tier dependency rules. Game General must remain reusable across games.

### Q: How do I split a plugin across platforms?

**A**:
1. Create a contracts assembly in Core tier
2. Create platform-specific implementations in each repo
3. Use profile-specific entry points in `plugin.json`

### Q: Should I put UI plugins in Game General or Game Specific?

**A**: If the UI is reusable (e.g., inventory HUD), put it in Game General. If it's game-specific (e.g., dungeon minimap), put it in Game Specific.
