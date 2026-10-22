# Transmutation and the action bar

## Transmutation (the source game's way)

The source game has no crafting of gear: gear drops finished from monsters and chests and is sold by the merchants. What
it has is **transmutation** at **Mortimer** ("Transmutation", in St. Aurelis where the source game stands him):

| Part | What it does |
|---|---|
| The grid | 4 × 3 slots, like the source game's TRANSMUTE window. Drag items from the bag onto it, or click them in the bag while it is open; click a slot to take it out |
| The line under it | "Makes: Lesser Life Potion" when the items match a recipe, "These items make nothing together" otherwise |
| Confirm | Makes it: one batch is used up (the rest of a bigger stack stays), the result goes into the bag |
| Recipes | The list of what Mortimer knows (the source game's `crafting_recipes` whose items exist here) |

- A recipe matches when the grid holds exactly its kinds of items, at least as many of each (5 Minor Life Potions →
  Lesser Life Potion; 5 Rough Rubies → Ruby; 5 Rubies + Vault Coins → Refined Ruby; 5 Major orbs + Vault Gold → a
  Divine orb; Magic Dust + Dormant Netherplane Key + Major Vault Gold → Netherplane Key...).
  Recipes: `Tools/GameData/recipes.py` → `Scripts/Editor/RecipeData.cs`; Mortimer: `Tools/GameData/world.py`.
- The server checks everything: standing at Mortimer, the items really in those bag slots, a recipe, room in the bag
  (`Inventory.ServerTransmute`). No fee.
- The old crafting materials (ingots, logs, hides, cloth) no longer drop and only sell to vendors.

## Action bar

Like the source game's bar: **12 slots** on keys **1–9, 0, - and =**. A slot holds **Attack**, a **spell** or a **usable
item** (potions show how many are left; when a potion runs out, the best one of the same kind is used).

- A new character: Attack on 1, the first spells on 2, 3, 4, the healing and mana potions on 5 and 6.
- **Drag** a spell from the spellbook (P) or a usable item from the bag (I) onto a slot.
- **Drag** a slot onto another slot to swap them; drag it **off the bar** to empty it.
- Clicking a slot does the same as its key. (Picking a spell in the spellbook and clicking a slot still works.)
- **F** still attacks the selected target wherever Attack sits.
- The bar is saved with the character; bars saved before (7 spell slots) keep their spells on the same keys.

Code: `Progression/Spellbook.cs` (bar entries), `UI/AbilityBar.cs`, `UI/DragDrop.cs`, `UI/TransmuteWindow.cs`, `Items/Recipe.cs`.
Tests: `SpellbookTests` (bar), `VendorTests` (transmutation).
