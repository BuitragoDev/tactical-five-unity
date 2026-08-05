# GLOSSARY — Tactical Five

> Project glossary: game terms (Spanish as used in-game), acronyms, systems, key classes, and internal concepts. Spanish terms keep the in-game form; explanations in English.

## Game terms (Spanish → meaning)

| Term | Meaning | Refers to |
|---|---|---|
| **Plantilla** | Roster | `TeamData` players + `LineupData` |
| **Quinteto** | Starting five | `LineupData` slot 0 |
| **Masa salarial / Salary cap** | Payroll / cap | `TeamData` salaries vs `league_settings` |
| **MLE / Mid-Level Exception** | Exception to sign FAs over cap | `TradeHelper.NT_MLE/T_MLE` |
| **Bird Rights** | Right to exceed cap to re-sign own players | `RosterController.GetMaxOfferBreakdown` |
| **Apron** | 1st/2nd apron thresholds | `TradeHelper.FIRST_APRON/SECOND_APRON` |
| **Hard cap** | Team cannot exceed 1st apron after using NT-MLE | `teams.first_apron_hard_capped` |
| **Team Option (TO)** | Team can void the last contract year | `players/offers.has_team_option` |
| **Player Option (PO)** | Player can void the last contract year | `players/offers.has_player_option` |
| **Re-firma / Bird rights** | Re-signing an own recent FA above cap using retained rights | `players.last_team_id`, `RosterController.GetMaxOfferBreakdown(isFromSameTeam:true)` |
| **Sign-and-Trade (S&T)** | Firmar a un FA propio (Bird rights) y traspasarlo de inmediato | `MarketController.ProcessSATrade`, `trade_type="sign_and_trade"` |
| **Luxury tax** | Progressive tax on payroll over threshold | `TradeHelper.CalculateLuxuryTax` |
| **Buyout / Rescisión** | Contract release with stretch payments | `RosterController.ConfirmBuyout` |
| **Renovación** | Contract renewal | `OfferData.offer_type=0` |
| **Fichaje** | Free-agent signing | `OfferData.offer_type=1` |
| **Cartera** | "Wallet" screen (player/contracts) | `CarteraController` |
| **Traspaso** | Trade | `TradeData`/`MarketController` |
| **Palmarés / Palmares** | Honors history | `PalmaresController` |
| **Premios** | Awards (MVP, ROY, …) | `PremiosController`, `awards_records` |
| **Récords** | Records | `RecordsController` |
| **Pabellón** | Arena | `ArenaController`; also staff position `"PABELLON"` |
| **Abonos** | Season tickets/subscriptions | `ProcessSubscriptionRevenue` |
| **Patrocinadores** | Sponsors | `SponsorData` |
| **Televisiones** | TV channels | `TvChannelData` |
| **Préstamos** | Loans | `LoanData` |
| **Ojeadores** | Scouts | `ScoutData` |
| **Empleados** | Staff (coaches, PABELLON, psychologist) | `EmployeeData` |
| **Lesiones** | Injuries | `GameSimulator.INJURY_TYPES`, `injury_days` |
| **Físico** | Fatigue (0–99) | `players.fisico` |
| **Moral / Morale** | Player morale (0–100) | `players.morale` |
| **Química** | Team chemistry | `teams.team_chemistry` |
| **Confianza de la afición** | Fan confidence (0–100) | `managers.fan_confidence` |
| **Draft** | Rookie draft (lottery + 60 picks) | `DraftGenerator` |
| **Play-In** | Play-in tournament | `PlayoffsGenerator` |
| **Playoffs** | 4×best-of-7 | `PlayoffsGenerator` |
| **All-Star** | All-Star game | `GameData.game_type="allstar"`, `all_star_records` |
| **Noticias** | Inbox / news | `MessageData`, `MessagesController` |

## Roles

| Term | Meaning |
|---|---|
| **Estrella / Titular / Banquillo / UltimoRecurso** | `PlayerRole` values (0–3) |
| **PG/SG/SF/PF/C** | Positions: Base, Escolta, Alero, Ala-Pívot, Pívot |
| **B/E/A/AP/P** | Short codes for positions (`PositionCodes.Short`) |

## Acronyms

| Acronym | Meaning |
|---|---|
| **OVR** | Overall rating (= mean of 11 attributes) |
| **REB/ASI/ROB/TAP/VAL** | Rebounds/Assists/Steals/Blocks/Value (dashboard stat cards) |
| **FGM/FGA/FG3M/FG3A/FTM/FTA** | Field goal / three / free throw made & attempted |
| **TD / DD** | Triple-double / double-double |
| **MLE** | Mid-Level Exception |
| **NT-MLE / T-MLE** | Non-Taxpayer / Taxpayer MLE |
| **FA** | Free Agent |
| **ROY** | Rookie of the Year |
| **MVP** | Most Valuable Player |
| **P&L** | Profit & Loss (Finances screen) |
| **UIDocument / UXML / USS / TSS** | UI Toolkit: document, markup, style sheet, theme style sheet |

## Key classes (with file)

| Class | File | Role |
|---|---|---|
| `ScreenManager` | `Scripts/Core/ScreenManager.cs` | Navigation singleton |
| `DatabaseManager` | `Scripts/Data/DatabaseManager.cs` | DB singleton (facade + seed + migrations) |
| `GameSaveManager` | `Scripts/Data/GameSaveManager.cs` | Save slots & template |
| `GameSimulator` | `GameSimulator.cs` | Match engine |
| `DraftGenerator` | `Scripts/Core/DraftGenerator.cs` | Draft lottery + class |
| `ScheduleGenerator` | `Scripts/Core/ScheduleGenerator.cs` | 82-game schedule + All-Star |
| `PlayoffsGenerator` | `Scripts/Core/PlayoffsGenerator.cs` | Play-In/Playoffs |
| `TradeHelper` | `Scripts/Core/TradeHelper.cs` | Salary cap rules, taxes, AI evaluation |
| `QuickNewsGenerator` | `QuickNewsGenerator.cs` | News |
| `PlayerPhotoHelper` | `PlayerPhotoHelper.cs` | Photo cascade |
| `GameResultCache` | `GameResultCache.cs` | Day results hand-off |
| `AudioManager` / `CursorManager` | `Scripts/Core/*` | Audio/cursor singletons |
| `HeaderController` / `SidebarController` | `Scripts/UI/*` | Injected UI chrome |
| `CustomSlider` | `Scripts/UI/CustomSlider.cs` | Volume slider control |
| `GameEnums` | `Scripts/Core/GameEnums.cs` | `GameScreen`, `GameMode` |
| `Constants` | `Scripts/Data/Constants.cs` | `PlayerRole`, `PositionCodes`, `CountryCodes`, `NCAATeams` |

## Internal concepts

| Concept | Meaning |
|---|---|
| **Slot** | One save file `save_{n}.db` |
| **Template DB** | `template.db`, master seed cloned into empty slots |
| **Phase** | `seasons.phase`: preseason/regular/playin/playoff/finished |
| **Game day** | Integer index of a day in the season (1..~180) |
| **Game type** | preseason/regular/playin/playoff/allstar |
| **Series label** | Playoff series id (`playoff-r1-east-1v8`, …) |
| **Sender type** | `MessageData.sender_type`: 0 system, 1 player, 2 news |
| **Transfer window** | Sep 1 → Feb 8 (AI trades active) |
| **Fisico penalty** | Performance multiplier when fatigue < 30 |
| **Hard cap flag** | `teams.first_apron_hard_capped` |
