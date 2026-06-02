For decal graphs, use `FireAlt/Create VFX Decal Definitions from VFX Assets`. That creates `VFXDecalDefinition` assets for the decal projector workflow.

## Decal Projector VFX

`VFXDecalProjector` is a hybrid authoring component for sprite-backed decals. It bakes sprite, projection, opacity, fade, normal blend, and draw-distance data into ECS components.

The decal workflow uses:

- `VFXDecalDefinition`: persistent graph template for decals.
- `DecalProjectorData`: per-decal projected sprite data.
- `RuntimeDecalLookup`: lookup key built from decal definition and sprite.
- `DecalProjectorVFX`: tracked runtime VFX handle.
- `InitializeVFXDecalsSystem`: creates and cleans backing `HybridVisualEffect` instances per decal lookup.
- `UpdateVFXDecalsSystem`: updates draw distance, enabled state, transform tracking, and per-decal payload data.

The default decal definition is configured in `Preferences/FireAlt/VFX Forge`.

Because decal runtime definitions are keyed by unique lookup data, many unique decal textures can consume many `VFXKey` IDs. Prefer sprite atlases when many decals can share the same texture source.


## Editor Preview and Preferences

`VFXSettings.DefaultDecalVFX` is stored in EditorPrefs through the Preferences provider at `Preferences/FireAlt/VFX Forge`.