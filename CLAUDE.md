# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**LD58 (Fly Collector)** is a Unity3D game jam project where the player collects bugs within a time limit. The game uses a focus/inspect camera system with nested interaction levels.

- **Unity Version**: Uses Unity 3D template with URP (Universal Render Pipeline)
- **Scripting Backend**: IL2CPP on Android, Mono elsewhere
- **Key Features**: Camera focus system, inventory management, bug AI, timer-based gameplay

## Build & Run Commands

This is a Unity project. Common operations:

- **Open Project**: Open in Unity Editor (no CLI build commands configured in repository)
- **Build**: Use Unity Editor → File → Build Settings
- **Run**: Press Play in Unity Editor, or run built executable from `Build/` directory

## High-Level Architecture

### Core Gameplay Loop

1. **Timer starts** on first player interaction (focus or inspect)
2. **Player explores** the room using focus/inspect camera modes
3. **Bugs roam** via NavMesh AI (`BugAI.cs`)
4. **Player catches bugs** using jar traps and adds them to inventory
5. **Victory conditions**: Collect target bugs before timer expires; exit room when quota met

### Camera System (Multi-Modal Interaction)

The camera system is the architectural centerpiece. It has three distinct modes managed by `CameraController.cs`:

#### 1. **Normal Mode** (Free Look)
- Raycasts to detect objects implementing `IFocusable`, `IInspectable`, `IInteractable`
- Shows outlines on hover
- Click to enter Focus or Inspect mode

#### 2. **Focus Mode** (Camera Moves to Object)
- Camera flies to a predetermined position/rotation around an object
- Managed by `FocusSession` class (stack-based; supports nesting)
- Integrates with **nest level system** (`FocusLevelManager`)
- Objects can specify required nest levels (e.g., can only interact when zoomed in)
- Exit with ESC or RMB

#### 3. **Inspect Mode** (Object Moves to Camera)
- Object flies to `holdPoint` in front of camera for close examination
- Managed by `InspectSession` class
- Used primarily for catching bugs (brings them close to camera)
- Exit with ESC or RMB

**Key Files**:
- `Assets/Scripts/Camera/CameraController.cs` - Main camera controller orchestrating all modes
- `Assets/Scripts/Camera/FocusSession.cs` - Focus mode implementation with animation
- `Assets/Scripts/Camera/InspectSession.cs` - Inspect mode implementation
- `Assets/Scripts/Camera/FocusLevelManager.cs` - Singleton managing nested focus levels
- `Assets/Scripts/Camera/InteractionFreeze.cs` - Prevents interactions during camera animations

### Interaction System (Condition-Action Pattern)

`InteractableObject.cs` implements a flexible interaction system:

- **Conditions** (`InteractionConditionBase`): Check requirements before executing (e.g., `HasItemCondition`, `FocusLevelCondition`, `LightStateCondition`)
- **Actions** (`InteractionActionBase`): Execute sequentially as coroutines (e.g., `PickupToInventoryAction`, `PlayAnimationAction`, `UnityEventAction`)
- **Context Passing**: `InteractionContext` struct provides action scripts with camera, inventory, animator references

**Key Interfaces**:
- `IFocusable` (Assets/Scripts/Interfaces/IFocusable.cs) - Objects that camera can zoom to
- `IInspectable` (Assets/Scripts/Interfaces/IInspectable.cs) - Objects that can be brought to camera
- `IInteractable` (Assets/Scripts/Interfaces/IInteractable.cs) - Objects that execute condition-action chains

**Pattern**: Conditions and Actions are `MonoBehaviour`s attached to GameObjects, evaluated/executed by `InteractableObject`.

### Nest Level System

The **nest level system** enables progressive disclosure: objects become interactable only when the player has focused to the correct depth.

- **Level 0**: Normal room view
- **Level 1+**: Focused on objects (can focus deeper into sub-objects)

Each `IFocusable` specifies:
- `GetRequiredNestLevel()`: Minimum level required to interact
- `GetTargetNestLevel()`: Level to set when focused
- `IsAvailableAtNestLevel(int)`: Availability check

**Example Flow**:
1. Player at level 0
2. Clicks ventilation grate → camera focuses (level 1)
3. Now can interact with screws (require level 1)
4. Remove screws → can open grate → see bugs inside

### Bug System

- **BugAI** (Assets/Scripts/Bugs/BugAI.cs): NavMesh-based roaming AI with configurable radius
  - Manual disable: `DisableAI(bool)` stops pathfinding (used when bug is caught)
  - Animator integration via `speedParam` float

- **BugManager**: Spawns bugs based on `TargetBugsList` ScriptableObject

- **Runtime Tracking**:
  - `TargetBugsRuntime.cs`: Stores which bugs should be caught this session
  - `CaughtBugsRuntime.cs`: Tracks which bugs player has caught

- **BugJarTrap**: Catches bugs on trigger, adds to inventory, disables AI

### Inventory System

`InventoryManager.cs` (singleton, persistent across scenes):
- Slot-based with stacking (`maxStackSize` per `Item`)
- UnityEvents: `OnItemAdded`, `OnItemRemoved`, `OnInventoryChanged`
- Can filter by `ItemType` enum (e.g., Quest items are bugs)

**Item** (ScriptableObject): Defines `itemID`, `itemName`, `itemType`, `maxStackSize`

### Timer & Game Flow

- **GameTimer** (Assets/Scripts/Timer/GameTimer.cs): Countdown timer (singleton)
  - Starts on first interaction via `FocusLevelManager.OnFirstInteraction` event
  - UnityEvents: `onTimerStart`, `onTimerEnd`, `onMinutePassed`

- **GameSceneController** (Assets/Scripts/Scene/GameSceneController.cs): Win/loss logic
  - Checks caught bugs vs target bugs
  - Opens exit room when quota met (`ExitRoom.cs`)
  - Determines outcome: `Victory`, `WrongBugs`, or `Timeout`
  - Passes results to GameOverScene via `GameSceneManager` persistent data

### Scene Management

- **GameSceneManager**: Singleton, persists data between scenes with `SetPersistentData(key, value)` / `GetPersistentData(key)`
- **Scenes**: MainMenu, Game (main gameplay), GameOver, Credits

## Important Patterns & Conventions

### Singleton Pattern
Many managers use singleton pattern with `DontDestroyOnLoad`:
- `InventoryManager.Instance`
- `GameTimer.Instance`
- `FocusLevelManager.Instance`
- `GameSceneManager.Instance`
- `TargetBugsRuntime.Instance`
- `CaughtBugsRuntime.Instance`

### Event-Driven Communication
- UnityEvents for inspector-wired callbacks
- C# events for code-based subscriptions
- FocusLevelManager events: `OnFirstInteraction`, `OnFirstFocusEver`, `OnNestLevelChanged`

### Interaction Freeze System
`InteractionFreeze.cs` uses push/pop stack to freeze/unfreeze interactions during animations. Always pair `Push()` with `Pop()`.

### Gate Pattern
`InteractionGate.cs` prevents timer auto-start when script-initiated interactions occur (e.g., cutscenes).

## Scripting Defines

All platforms use: `DOTWEEN` (DOTween animation library is available)

## Common Development Workflows

### Adding New Interactable Object
1. Add collider to GameObject
2. Add `InteractableObject` component
3. Create condition scripts (inherit `InteractionConditionBase`) as needed
4. Create action scripts (inherit `InteractionActionBase`) as needed
5. Assign conditions/actions in `InteractableObject` inspector
6. Optionally add `Outline` component for hover feedback

### Adding New Bug Type
1. Create `Item` ScriptableObject (set `itemType = Quest`)
2. Create `BugMeta` ScriptableObject (links Item to bug prefab)
3. Add BugMeta to `TargetBugsList` or `BugList`
4. Bug prefab needs: `BugAI`, `NavMeshAgent`, collider, `InspectableObject`

### Adjusting Focus Behavior
- Modify `IFocusable` implementation on object (e.g., `FocusableObject.cs`)
- Set camera position via `GetCameraPosition()` and `GetCameraRotation()`
- Control mouse rotation with `IsCameraPositionLocked()`
- Set nest level requirements via `GetRequiredNestLevel()` and `GetTargetNestLevel()`

### Debug Flags
Most managers have `showDebugInfo` / `showDebug` serialized fields for verbose logging. Enable in inspector during development.

## Code Style Notes

- Comments often in Russian (project appears to be developed by Russian-speaking team)
- Uses `SerializeField` private fields with inspector tooltips
- Prefers composition over inheritance (Component-based Unity patterns)
- Coroutines used for action sequences (`InteractionActionBase.Execute()` returns `IEnumerator`)
- не добавляй Debug.Log в MonoBehavior без причины.
- не пиши Debug.Log