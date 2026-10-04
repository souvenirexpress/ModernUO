# World Simulation

## Architecture

The world simulation is an opt-in UOContent system. It does not modify `Server.Item` or require legacy items to carry simulation state.

```text
MaterialDefinition + PhysicalProperties + IWorldObjectState
                         |
                         v
                 AffordanceResolver
                         |
       actor + source + action + target + environment
                         |
                         v
                 InteractionResolver
                         |
                         v
       state changes + effects + messages + object changes
                         |
            +------------+-------------+
            |                          |
            v                          v
  WorldSimulationSystem       VisualStateResolver
```

All implementation code is under `Projects/UOContent/Engines/WorldSimulation/`. Example items are in `Projects/UOContent/Items/World Simulation/`.

## Materials

`MaterialDefinition` contains optional physical, structural, thermal, chemical, and biological properties. Object-specific mass and volume are exposed separately through `PhysicalProperties`.

`MaterialRegistry` loads shared definitions from `Distribution/Data/world-simulation/materials.json`. Items persist only a `MaterialId`; they do not duplicate a material definition in every save record. Missing definitions resolve to the shared `Unknown` fallback.

To add a material:

1. Add its stable ID to `MaterialId`.
2. Add one definition to `materials.json`.
3. Add tests for any new thresholds or unusual optional-property behavior.

## Object State

`IWorldObjectState` owns numeric dynamic state: condition, temperature, moisture, mechanical damage, combustion, contamination, freshness, decay, and fermentation. Normalized values are clamped to `0.0..1.0`; temperature is Celsius.

`IsDry`, `IsWet`, `IsHot`, `IsCold`, `IsBurning`, and `IsCharred` are derived properties. They are not persisted as independent flags. Optional container state is represented by `ContainerObjectState` and `IWorldContainerStateProvider`, so ordinary items pay no container-state cost.

`WorldSimulationItem` is the opt-in persistent item base. Its generated ModernUO serialization stores all dynamic fields and supports ordinary world saves and restarts. Legacy items that do not implement `IWorldSimulatedObject` are returned safely by resolvers without mutation.

## Affordances

`AffordanceResolver.GetAvailableActions` combines source capability values, target material properties, target state, and environment. It never checks concrete pairs such as `Torch + OakLog`.

The complete action vocabulary is prepared in `WorldInteractionAction`. Version 1 resolves Heat, Ignite, Cool, and Extinguish. Other actions are contracts for later vertical slices.

## Interactions

`InteractionResolver` delegates to small `IInteractionRule` implementations. Every rule provides both an explanation and a result. `InteractionResult` can report state changes, generated or destroyed items, effects, and player messages.

To add an interaction rule:

1. Implement `IInteractionRule` beside the related rules.
2. Keep `CanHandle` limited to the action and broad target contract.
3. Put material/state/capability requirements in `Explain`.
4. Apply only the resulting state transition in `Resolve`.
5. Register one rule instance in `InteractionResolver` and add focused tests.

Concrete item-type checks are not allowed for material behavior. Integration checks for existing UO contracts such as `BaseLight`, `BaseBeverage`, and `BaseWaterContainer` belong in `SourceCapabilityResolver`.

## Fire And Water

A lit `SimulationTorch` or existing burning `BaseLight` exposes heat and ignition power. Ignition succeeds when the source is strong enough, the material is flammable, fuel remains, moisture is below the threshold, and transferred heat reaches the material ignition temperature.

Burning items register with the central simulation system. Fuel falls, char and soot rise, moisture falls, and temperature stays elevated. Empty fuel or excess moisture ends combustion.

`SimulationWater`, a water-filled `BaseBeverage`, and a filled `BaseWaterContainer` expose cooling and extinguishing power. Applying them reduces temperature and combustion while increasing moisture. The target item remains the same persisted object.

## Simulation Ticks

`WorldSimulationSystem` owns one recurring ModernUO timer and an active list plus membership set. Static items are never scanned. Items register only while burning, thermally displaced from ambient, or drying. All updates run on ModernUO's single game loop; no background thread touches world state.

The public `Advance` method accepts an explicit elapsed time and `EnvironmentContext`, which keeps rule behavior deterministic in tests and allows future rain, underwater, and wind integrations.

## Visual State

`VisualStateResolver` maps logical state to art and light only for items implementing `IWorldVisualProfile`. Art IDs never determine simulation state. `DryOakLog` demonstrates default, burning, charred, and ash visuals.

## Integrating An Item

For a new opt-in item, derive from `WorldSimulationItem`, add `[SerializationGenerator(0)]`, pass a `MaterialId` to the base constructor, and override object-specific physical properties or composition when needed. Call `OnSimulationStateChanged` after constructor-time state changes that require active ticking.

Existing inheritance-heavy items can implement `IWorldSimulatedObject` directly or receive a narrow adapter in a resolver. Do not move universal state into the engine `Item` class.

## Debugging

`[SimInspect` targets an item and displays material, properties, numeric state, derived state, and currently available actions. `[SimExplain <action>` targets a source and then a target and reports the exact rule decision, including failed moisture or power thresholds.

The generated `[Props` fields on `WorldSimulationItem` also expose persisted state to game masters.

## Performance Principles

- Never iterate `World.Items` or the full map.
- Never create one timer per simulated item.
- Share immutable material definitions through the registry.
- Persist numeric source state, not redundant derived flags.
- Keep interaction resolution event-driven and simulation ticking limited to active items.
- Keep all game-state mutation on the main loop.

## Current Scope

Abadoria v0.10.36 adds Cut/Chop rules with material resistance, tool condition,
sharpness, wear and persistent target condition/cracking. Affordances delegate
to the same rule explanations used by execution; prepared vocabulary-only
actions are no longer offered. Client context menus query server-owned offers
through BF/0081; execution revalidates access and has a 750 ms actor cooldown.
Studio exposes six executable source capabilities. Mechanical remains retain
their serial; no recipes/resource yields or object splitting are implemented.
See the parent repository's `Documentation/WORLD-INTERACTIONS.md` for details.

Version 1 implements the framework, all requested material categories, dynamic state, optional container-state contracts, affordance and interaction resolvers, central active ticking, visual-state separation, Fire/Water interactions, example items, GM diagnostics, and automated coverage.

Remaining work includes liquid mixing, construction physics, food processing,
magic storage, weather discovery from maps/regions, and mass conversion of the
existing item library. Cut/Chop damage is implemented as described above.
