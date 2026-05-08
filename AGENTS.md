# AGENTS.md - Apex.World AI Development Guide

## Project Overview

**Apex.World** is a non-destructive world generator for the S&box engine (Facepunch's Source 2 modding framework). The project uses an entity-component system with a spawner pattern to manage procedural world generation.

**Key Facts:**
- C# (.NET 10.0), S&box game engine
- Multiplayer game (50 tick rate, 64 max players)
- Architecture: Manager + Spawner pattern
- Organized as a game project with embedded library

## Architecture & Core Patterns

### Memory: Component Hierarchy & Root Access

**The ApexWorldManager is the root authority.** All spawners are children of this manager:

```csharp
// Libraries/ApexWorld/Code/ApexWorldManager.cs
public class ApexWorldManager : Component
{
    [Property] public BaseSpawner[] Spawners { get; set; }
}
```

**Golden rule:** Any spawner accesses the manager via `GetRootComponent<ApexWorldManager>()` to access shared state:

```csharp
// Libraries/ApexWorld/Code/Spawners/BaseSpawner.cs
protected ApexWorldManager Manager => GetRootComponent<ApexWorldManager>();
```

This pattern prevents direct parent references and allows loose coupling. Always use this accessor in spawner implementations.

### Spawner Pattern

The spawner system is the primary extensibility point. To add new world generation behavior:

1. **Inherit** from `BaseSpawner` in `Sandbox.Spawners` namespace
2. **Use [Property]** attributes for editor-configurable fields
3. **Override ToString()** for debug output: `"{GetType().Name} - {SpawnerName}"`
4. **Access manager** via protected property to coordinate generation

**Existing implementations:** `TextureSpawner` (placeholder for texture generation)

## csproj & Build System Details

### S&box-Specific Configuration

- **OutputPath:** Hard-coded to Steam S&box installation directory
- **DefineConstants:** `SANDBOX` symbol is defined (use for S&box-specific code)
- **Analyzers:** Two S&box analyzers run at build time:
  - `Sandbox.CodeUpgrader.dll` - Automatic code updates
  - `Sandbox.Generator.dll` - Entity/component code generation

### Project Structure

```
Libraries/ApexWorld/          ← Reusable library package
  Code/                        ← Runtime code
    ApexWorldManager.cs
    Spawners/
  Editor/                      ← S&box editor tools
  UnitTests/                   ← Test assembly

Code/                          ← Game-specific code
Editor/                        ← Game editor extensions
```

**Important:** The library is type=``"library"`` in apexworld.sbproj; the main project is type=``"game"``.

## Developer Workflows

### Building & Running

For S&box projects, the standard build workflow is:

1. **Automatic:** S&box hot-reloads code changes when you save files
2. **Manual rebuild:** Visual Studio build system output goes to Steam S&box folder
3. **Testing:** Launch S&box and load the startup scene (scenes/minimal.scene)

### Testing & Debugging

- **Unit Tests:** Located in `Libraries/ApexWorld/UnitTests/`
- **Run via:** S&box test runner (integrated in editor)
- **Debug prints:** Use Log or standard C# Debug output visible in S&box console

## Conventions & Coding Standards

### Naming & Namespacing

- **Game code:** `namespace Sandbox` (see Assembly.cs global using)
- **Library-specific:** Use subnamespaces like `Sandbox.Spawners`
- **Properties:** Use [Property] attribute for editor-visible fields
- **UI Display:** Use [Title()] attribute for friendly names in editor

### Global Using Directives

The file `Code/Assembly.cs` defines global usings:

```csharp
global using Sandbox;
global using System.Collections.Generic;
global using System.Linq;
```

These are available in all files without explicit imports.

### Code Style & Infrastructure

- **C# 14** language features enabled
- **Nullable:** Disabled globally (Nullable=disable in csproj)
- **Documentation:** GenerateDocumentationFile=true (XML docs required)
- **No unsafe:** AllowUnsafeBlocks=False

## Cross-Component Communication

### Manager Access Pattern

Spawners coordinate through the manager hierarchy, never peer-to-peer:

```csharp
// Get manager
var mgr = GetRootComponent<ApexWorldManager>();

// Access other spawners if needed
foreach (var spawner in mgr.Spawners)
{
    // Coordinate generation
}
```

### Scene & Entity Structure

- **Startup scene:** `scenes/minimal.scene` (configured in apexworld.sbproj)
- **Assets path:** `Assets/` for scenes, materials, textures
- **Entity hierarchy:** All world generation flows through ApexWorldManager root

## External Dependencies

- **Sandbox.System, Sandbox.Engine, Sandbox.Filesystem** - S&box runtime
- **Microsoft.AspNetCore.Components** - For UI/UI framework (included by S&box)
- **Base Library.csproj** - S&box base game code and utilities

All dependencies are managed by S&box; package management is through Steam installation.

## Quick Reference: Key Files

| File | Purpose |
|------|---------|
| `apexworld.sbproj` | Game manifest (title, startup scene, multiplayer config) |
| `Libraries/ApexWorld/apexworld.sbproj` | Library manifest |
| `Code/apexworld.csproj` | Game runtime compilation config |
| `Libraries/ApexWorld/Code/apexworld.csproj` | Library compilation config |
| `Code/Assembly.cs` | Global using directives |
| `Libraries/ApexWorld/Code/ApexWorldManager.cs` | Root manager, spawner array |
| `Libraries/ApexWorld/Code/Spawners/BaseSpawner.cs` | Spawner base class & pattern |

## Common Tasks for AI Agents

- **Adding a spawner type:** Create class in `Spawners/`, inherit `BaseSpawner`, add [Property] fields
- **Accessing world state:** Use `Manager` property from any spawner
- **Editor integration:** Add tools to `Editor/MyEditorMenu.cs` using S&box editor APIs
- **Testing changes:** Reload game in S&box, verify spawner behavior in-engine
- **Debugging:** Use Manager reference to inspect Spawners array state

