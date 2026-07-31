# PREFABS — Tactical Five

> **[F] There are zero `.prefab` assets in the whole project** (verified by a global search of `Assets/`). This is a deliberate architectural consequence of the single-scene + UI-Toolkit design, not an omission.

## 1. Why there are no prefabs

- **UI** is fully declarative in **UXML** (the UI Toolkit equivalent of prefabs for views) + procedural C# (`VisualElement`/`Label` construction). Screens live as scene GameObjects with `UIDocument`; reusable views (Header, Sidebar) are stored as UXML under `Resources/UI/Core/` and injected at runtime via `Resources.Load<VisualTreeAsset>`.
- **Gameplay objects** do not exist — the game has no 3D/2D scene content (no GameObjects for players, balls, arenas). All entity data lives in **SQLite** and is rendered as UI.
- **The only visual representation** of entities are textures loaded via `Resources.Load`:
  - Team logos: `Teams/Logos/{32|64|80|100|120}x{...}/{logo}.png`
  - Jerseys: `Teams/Jerseys/` and `Teams/Jerseys/121x170/`
  - Player photos: `PlayerPhotos/{id}.png`, `PlayerPhotos/Default/default{1..100}.png`
  - Flags, icons, arenas, sponsors, TV channel logos, audio clips.

## 2. Runtime "instantiation" patterns (in lieu of prefabs)

| Need | Mechanism | Example |
|---|---|---|
| Reusable UI views | `Resources.Load<VisualTreeAsset>` + `CloneTree()` | `HeaderController.cs:13`, `SidebarController.cs:50` |
| Lists/rows/tables | C# `new VisualElement()/Label()` appended to a container (`Clear()` + `Add()`) | Every controller's `Build...` methods |
| Static sprites by key | `Resources.LoadAll<Sprite>` → `Dictionary<string,Sprite>` | Logos, `EndSeasonController.RefreshHeader` |
| Player photos | `PlayerPhotoHelper.Load(id, field)` (5-source cascade) | Player detail screens |
| Audio | `Resources.Load<AudioClip>("Audios/{name}")` | `AudioManager` |
| Screen navigation | `SetActive` on scene documents | `ScreenManager.ShowOnly` |

## 3. Consequences & guidance for future work

- To add a new "prefab-like" visual (e.g., a reusable card), create a **UXML + USS** pair (like Header/Sidebar) rather than a `.prefab`.
- To add new entities, model a **SQLite table + controller** (see `CODE_GUIDELINES.md` "how to add a new feature").
- Any asset you want loaded at runtime without a reference must live in a `Resources/` folder (all game art is under `Art/Resources/`, UI under `UI/Resources/`).

## 4. Open questions

- Whether prefabs were ever planned (the `.meta` orphan `Data/Database` and `_Recovery` scenes hint at a reorganization history). [H]
- Addressables are not configured — no Addressables usage anywhere [F].
