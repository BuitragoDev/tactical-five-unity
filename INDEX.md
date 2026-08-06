# Tactical Five — Knowledge Base Index

> Entry point for developers and AI agents working on this project.
> **Project:** Tactical Five — NBA-management sim (Unity 6, single scene, UI Toolkit, SQLite). All in-game text is Spanish.
> **Analysis date:** 2026-07-31 · Code base: commit `50b1a86` · Engine `6000.3.15f1`.

## Documentation map

### Foundational
| Doc | Contents |
|---|---|
| [Docs/PROJECT_OVERVIEW.md](Docs/PROJECT_OVERVIEW.md) | What the game is, how it plays, tech stack, core loops, state of development |
| [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md) | Modules, singletons, init lifecycle, navigation, dependency maps (Mermaid) |

### Gameplay & systems
| Doc | Contents |
|---|---|
| [Docs/GAMEPLAY.md](Docs/GAMEPLAY.md) | Every mechanic with exact formulas (simulation, economy, contracts, morale, draft…) |
| [Docs/SYSTEMS.md](Docs/SYSTEMS.md) | The 16 core systems: responsibilities, files, key methods, risks |
| [Docs/EVENTS.md](Docs/EVENTS.md) | All communication channels: static state, DB messages, PlayerPrefs, UI callbacks |

### Data & persistence
| Doc | Contents |
|---|---|
| [Docs/DATA_MODEL.md](Docs/DATA_MODEL.md) | All ~40 SQLite tables field-by-field, relations, seeds, migrations |
| [Docs/SAVE_SYSTEM.md](Docs/SAVE_SYSTEM.md) | Slots, `template.db`, save/load flows, versioning, risks |

### UI
| Doc | Contents |
|---|---|
| [Docs/UI_TOOLKIT.md](Docs/UI_TOOLKIT.md) | PanelSettings/theme, UXML/USS, controller pattern, 39 screens, navigation tree |
| [Docs/SCENES.md](Docs/SCENES.md) | The single scene, its 40 GameObjects, lifecycle |
| [Docs/PREFABS.md](Docs/PREFABS.md) | Why there are zero prefabs; runtime instantiation patterns |
| [Docs/SCRIPTABLE_OBJECTS.md](Docs/SCRIPTABLE_OBJECTS.md) | No game SOs; the PanelSettings + fonts that exist |

### Engineering
| Doc | Contents |
|---|---|
| [Docs/CODE_GUIDELINES.md](Docs/CODE_GUIDELINES.md) | Real conventions, patterns to follow/avoid, how to add screens/mechanics/data |
| [Docs/TODO_TECHNICAL_DEBT.md](Docs/TODO_TECHNICAL_DEBT.md) | Prioritized bugs, refactors, debt, risks, improvements (P0–P3) |
| [Docs/IMPROVEMENT_PROPOSALS.md](Docs/IMPROVEMENT_PROPOSALS.md) | Propuestas de mejora para hacer el juego más potente, priorizadas por impacto/esfuerzo |

### Agent knowledge base
| Doc | Contents |
|---|---|
| [.agent/SKILLS.md](.agent/SKILLS.md) | Onboarding: architecture facts, fast paths, conventions, traps (read first for agents) |
| [.agent/MEMORY.md](.agent/MEMORY.md) | Persistent state, decisions & reasons, confirmed assumptions, "never touch" list |
| [.agent/GLOSSARY.md](.agent/GLOSSARY.md) | Game terms (Spanish), acronyms, systems, key classes |

## Recommended reading order

1. `Docs/PROJECT_OVERVIEW.md`
2. `Docs/ARCHITECTURE.md`
3. `Docs/GAMEPLAY.md`
4. `Docs/UI_TOOLKIT.md`
5. `Docs/SYSTEMS.md` → `Docs/SAVE_SYSTEM.md` → `Docs/DATA_MODEL.md`
6. `Docs/SCENES.md` → `Docs/EVENTS.md`
7. `.agent/SKILLS.md` (agents) → remaining docs on demand

**For agents:** start with `.agent/SKILLS.md`, then `Docs/ARCHITECTURE.md` and `Docs/DATA_MODEL.md`.

## Conventions in the docs

- `**[F]**` = verified fact · `**[D]**` = reasonable deduction · `**[H]**` = hypothesis.
- Every important claim references `file:line` or asset paths.
- Each doc ends with an **Open questions** section.
