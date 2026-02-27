# Man in the Year – AAA Android Game

> Unity 2022.3 LTS · Universal Render Pipeline (URP) · Target: Android (Snapdragon 700/800)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│  INPUT LAYER              TouchInputHandler.cs                  │
│  (Unity Input System)     – Single & multi-touch, pinch-zoom   │
│                           – One physics raycast per tap         │
└───────────────┬─────────────────────────┬───────────────────────┘
                │ tap on node              │ tap on terrain
                ▼                          ▼
┌───────────────────────┐   ┌─────────────────────────────────────┐
│  WORLD INTERACTION    │   │  BUILDING SYSTEM   BuildingSystem.cs│
│  ResourceNode.cs      │   │  – 2D bool grid (O(1) lookup)       │
│  – cooldown timer     │   │  – Ghost placement with tint        │
│  – max-uses tracking  │   │  – Production tick (timer, not Upd.)│
└───────────┬───────────┘   └──────────────────┬──────────────────┘
            │ TryAdd / TrySpendAll              │
            └───────────────┬───────────────────┘
                            ▼
              ┌─────────────────────────┐
              │  RESOURCE SYSTEM        │
              │  ResourceSystem.cs      │
              │  – Dictionary inventory │
              │  – C# events (no poll)  │
              └──────────┬──────────────┘
                         │ OnResourceChanged event
                         ▼
              ┌─────────────────────────┐
              │  UI LAYER               │
              │  ResourceUI.cs          │
              │  – event-driven refresh │
              │  – icon + counter text  │
              └─────────────────────────┘
```

---

## File Structure

```
Assets/
└── Scripts/
    ├── Data/
    │   ├── ResourceData.cs      ← ScriptableObject – resource type definition
    │   └── BuildingData.cs      ← ScriptableObject – building definition + costs
    ├── Systems/
    │   ├── ResourceSystem.cs    ← Runtime inventory manager (singleton)
    │   ├── ResourceNode.cs      ← Per-node gathering logic (MonoBehaviour)
    │   └── BuildingSystem.cs    ← Placement + grid + production
    ├── Input/
    │   └── TouchInputHandler.cs ← Low-latency Android touch input
    ├── Pooling/
    │   └── ObjectPool.cs        ← Generic prefab pool (zero Instantiate in gameplay)
    └── UI/
        └── ResourceUI.cs        ← HUD widget bound to a single ResourceData asset
```

---

## Design Principles

| Principle | Implementation |
|-----------|---------------|
| **Data / Presentation separation** | `ResourceData` & `BuildingData` are pure `ScriptableObject` assets; no runtime state lives in them. `ResourceSystem` holds runtime state. `ResourceUI` handles display only. |
| **Open / Closed** | New resource types and building types are created by adding new `.asset` files – no C# changes required. |
| **Event-driven UI** | `ResourceSystem.OnResourceChanged` fires only when inventory actually changes. Zero per-frame polling. |
| **No per-frame allocations** | Plain `for` loops instead of LINQ. `Dictionary` and `List` pre-allocated. `ObjectPool` eliminates `Instantiate`/`Destroy` during gameplay. |

---

## Mobile Optimisation Guide

### CPU Bottlenecks
- **Input**: `TouchInputHandler` casts one physics ray per *tap* (not per frame).
  - Set `interactableLayer` mask in the Inspector to restrict broadphase tests.
- **Building grid**: O(1) bool array lookup; avoids `Physics.OverlapBox` every frame.
- **Production ticks**: Timer-gated loop, not `Update()`. Only iterates placed buildings.

### GPU / Draw Calls
- All building prefabs must have **GPU Instancing** enabled on their materials.
- Use **Static Batching** (mark completed buildings as `Static` after placement).
- `MaterialPropertyBlock` is used for ghost tinting → does **not** break instancing batches.
- VFX Particle Systems should share a single URP-compatible material.

### RAM
- `ObjectPool` pre-warmed in the loading screen; no `Instantiate` during gameplay.
- `ResourceSystem` dictionary pre-allocated with capacity 16.
- `ResourceNode` uses no heap allocations in `Update()`.

---

## Avoiding Input Lag (Android)

1. Use **Unity Input System** package (`com.unity.inputsystem`): device state read at
   frame start → max 1 frame latency.
2. **Player Settings → Android → Optimized Frame Pacing**: ON.
3. Set `Application.targetFrameRate = 60` in your bootstrap script.
4. Keep the main thread free: move expensive work (pathfinding, save I/O) to
   `Task.Run()` / `Job System`.

---

## Level Designer Setup

1. **Create resource types**: Right-click in Project → *Man In The Year → Resource Data*.
   Fill in `resourceId`, `displayName`, icon sprite, yield amount, cooldown.
2. **Create building types**: Right-click → *Man In The Year → Building Data*.
   Assign prefab, ghost prefab, footprint size, build costs, production outputs.
3. **Scene setup**:
   - Add an empty `_Systems` GameObject; attach `ResourceSystem`, `BuildingSystem`,
     `ObjectPool`, `TouchInputHandler`.
   - Set `gridWidth`, `gridHeight`, `cellSize` on `BuildingSystem`.
   - Drag all `BuildingData` assets into the **Building Palette** list.
4. **Resource nodes**: Add `ResourceNode` component to any world mesh.
   Assign `ResourceData` asset and set `maxUses` / `overrideYield` per node.
5. **HUD**: For each resource type, add a `ResourceUI` component to a UI panel,
   assign the matching `ResourceData` asset, wire up `resourceIcon` and `amountText`.
