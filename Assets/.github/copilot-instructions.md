# Bumper Beasts: rules for every change

- Unity 6 2D game in C#. Do not change gameplay behavior, physics values, or existing public field names unless asked.
- Do not edit scenes or prefabs. Prefer adding serialized public fields I can assign in the Inspector.
- Anything the player can see should also be visible or placeable in Edit Mode when practical: use objects I can place in the scene, [ExecuteAlways] or OnValidate for previews, OnDrawGizmos for helper shapes, and Editor menu items for adding things. Say so when something can only exist at runtime.
- Null-check references. No new packages. No Debug.Log spam and no allocations in Update.
- Code must compile cleanly. When finished, list the files changed and any Inspector steps I need to do.