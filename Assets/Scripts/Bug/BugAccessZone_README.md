# BugAccessZone - Usage Guide

## Overview
`BugAccessZone` controls bug accessibility based on camera focus level. Bugs inside a zone can only be inspected/caught when the camera reaches the required focus level.

## Setup

### 1. Create Access Zone

1. Create an empty GameObject in your scene (or use existing NavMeshModifier)
2. Add `BugAccessZone` component
3. Configure settings:
   - **Required Focus Level**: Minimum focus level needed (e.g., `1` for first zoom level)
   - **Zone Name**: Descriptive name for debugging
   - **Auto Setup Collider**: Automatically adds BoxCollider if missing

### 2. Configure Zone Trigger

The zone needs a **trigger collider** to detect bugs:
- If using NavMeshModifier: Add a **BoxCollider** and set `Is Trigger = true`
- The collider volume defines where bugs are restricted
- Bugs entering/exiting this volume will be registered automatically

### 3. Setup Bug Inspection Conditions

On your **InspectableObject** (attached to bug prefab), use the existing **FocusLevelCondition** system:

1. Add `InspectableObject` component to bug prefab (if not already present)
2. Add `InteractableObject` component for interaction conditions
3. Add `FocusLevelCondition` component:
   - Set **Required Nest Level** to match the zone's required level
   - This prevents inspection when focus level is too low

## Example Scene Setup

### Ventilation Shaft Example

```
VentilationShaft (focus target)
├── NavMeshModifier (defines bug walking area)
│   └── BugAccessZone
│       - Required Focus Level: 1
│       - Zone Name: "Vent Interior"
│       - BoxCollider (Is Trigger: true)
│
└── Bugs (spawned inside)
    └── BugPrefab
        ├── BugAI (will auto-register with zone)
        ├── InspectableObject
        └── InteractableObject (optional)
            └── FocusLevelCondition
                - Required Nest Level: 1
```

### Underground Tunnel Example

```
UndergroundTunnel
├── AccessZone (empty GameObject)
│   └── BugAccessZone
│       - Required Focus Level: 2
│       - Zone Name: "Deep Tunnel"
│       - BoxCollider (Is Trigger: true, size covers tunnel)
│
└── TunnelBugs
    └── DeepBug
        ├── BugAI
        ├── InspectableObject
        └── InteractableObject
            └── FocusLevelCondition
                - Required Nest Level: 2
```

## How It Works

1. **Bug enters zone** → `BugAccessZone` detects via trigger → Registers bug → Sets accessibility based on current focus level
2. **Focus level changes** → `FocusLevelManager` broadcasts event → `BugAccessZone` updates → All bugs in zone update accessibility
3. **Player hovers bug** → `InspectableObject` checks conditions → `FocusLevelCondition` validates → Only accessible if focus level matches

## API Reference

### BugAccessZone

```csharp
// Properties
public int RequiredFocusLevel { get; }        // Minimum focus level needed
public bool IsAccessible { get; }             // Is zone currently accessible?
public int BugCount { get; }                  // Number of bugs in zone
public string ZoneName { get; }               // Zone identifier

// Methods
public bool ContainsBug(BugAI bug)            // Check if bug is in zone
public IReadOnlyCollection<BugAI> GetBugsInZone() // Get all bugs in zone
public void RefreshBugs()                     // Manually refresh bug list
```

### BugAI

```csharp
// New Methods
public bool IsAccessible()                    // Is bug currently accessible?
public void SetAccessible(bool accessible)    // Set accessibility (called by zone)
public void RegisterAccessZone(BugAccessZone zone)   // Register with zone
public void UnregisterAccessZone(BugAccessZone zone) // Unregister from zone

// Properties
[SerializeField] private bool alwaysAccessible = false; // Bypass zone restrictions
```

## Debug Features

### Visual Gizmos
- **Orange box**: Zone is inactive (focus level too low)
- **Green box**: Zone is active (bugs accessible)
- **Label**: Shows zone name, required level, accessibility, bug count

### Debug Logging
Enable `showDebug` on `BugAccessZone` to log:
- Bugs entering/exiting zone
- Focus level changes
- Accessibility updates per bug

## Common Patterns

### Pattern 1: Simple Room Focus
```
Room → Required Level 1
Players must focus on room to see/catch bugs inside
```

### Pattern 2: Nested Containers
```
Terrarium (Level 1) → Can see bugs
  └── Lid Open (Level 2) → Can catch bugs
```

### Pattern 3: Progressive Discovery
```
Crack in Wall (Level 0) → No bugs accessible
  └── Remove Panel (Level 1) → Reveal bug nest
      └── Open Nest (Level 2) → Bugs become catchable
```

## Troubleshooting

### Bugs are always accessible
- Check `BugAI.alwaysAccessible` is `false`
- Verify bug is actually inside trigger volume
- Enable `showDebug` to see if bug registered with zone

### Bugs never become accessible
- Check `BugAccessZone.RequiredFocusLevel` matches game progression
- Verify `FocusLevelManager.CurrentNestLevel` is updating correctly
- Ensure trigger collider overlaps bug NavMesh area

### Outline doesn't show when hovering
- Bug is not accessible → This is correct behavior
- Use `FocusLevelCondition` on `InspectableObject` to control visibility

## Migration from Old System

If you have existing bugs without zones:
1. Bugs default to `accessible = true` when not in any zone
2. Add `BugAccessZone` only where you need focus-based restrictions
3. Set `alwaysAccessible = true` on bugs that should ignore zones
