# SCRIPTABLE_OBJECTS — Tactical Five

> **[F] There are no game-data ScriptableObjects in this project.** All game content (teams, players, league settings, sponsors, records, etc.) is stored in **SQLite** and seeded by C# seeders (`DatabaseManager.Seed*`), not in `.asset` files.

## 1. What `.asset` files actually exist (complete list, excluding Unity/package defaults)

| Asset | Type | Purpose |
|---|---|---|
| `UI/Resources/TacticalFivePanelSettings.asset` | `UnityEngine.UIElements.PanelSettings` | Shared UI Toolkit panel settings (theme, scale mode 1920×1080, match 0.5). Referenced by every `UIDocument` in the scene. |
| `Art/Fonts/FA_BarlowCondensed-{Bold,Medium,Regular,SemiBold} SDF.asset` | TextMesh Pro SDF font | Font assets for UI |
| `Art/Fonts/Saira_Condensed-{Bold,Regular,SemiBold} SDF.asset` | TextMesh Pro SDF font | Font assets for UI |

Everything else that is a `.asset` belongs to the engine/package defaults (`ProjectSettings/*`, TextMesh Pro settings, UI Toolkit default runtime theme `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss`, `InputSystem_Actions.inputactions`, etc.).

## 2. Why this design

- The game is **fully data-driven via SQLite**; the seed data (`SeedTeams`, `SeedPlayers`, `SeedFreeAgents`, `SeedSponsors`, `SeedTvChannels`, seeders for records/palmarés) would be redundant as ScriptableObjects.
- `template.db` doubles as the "master data asset": `DatabaseManager.EnsureTemplateDb()` builds it from the seeders and new save slots are cloned from it (`CloneFromTemplate`).

## 3. Guidance

- **Do not** introduce game-data ScriptableObjects as a source of truth for game content unless you intend to move away from SQLite. If you need design-time data, add it to the seeders in `DatabaseManager`/`*Seeder.cs`.
- **PanelSettings** is the one SO you may safely extend (theme, scale mode) — remember it is shared by all screens.
- **Fonts**: any new UI font must be imported as a TMP SDF asset and referenced from USS via `url("project://database/...")`.

## 4. Open questions

- Whether the absence of SOs is a deliberate choice vs a simplification ([D] deliberate: the project already has a robust seeding pipeline and template DB).
