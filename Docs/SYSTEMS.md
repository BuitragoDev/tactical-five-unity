# SYSTEMS — Tactical Five Core Systems

> Each system: responsibility, files, key methods, data used, dependencies, lifecycle, risks. **[F]** = fact, **[D]** = deduction, **[H]** = hypothesis.

---

## S1. Database system (SQLite)

- **Responsibility:** single gateway to all persisted game data.
- **Files:** `Scripts/Data/DatabaseManager.cs` (5609 ln), `Scripts/Data/SQLite.cs` (sqlite-net wrapper), `Scripts/Data/SQLiteAsync.cs` (**unused**, legacy), `Assets/Plugins/SQLite/*` (native binaries).
- **Key API (selected):** `InitSaveSlot(int)`, `CreateTables()`, `RunMigrations()`, `SeedStaticDataIfNeeded()`, `StartNewSeason(...)`, `GetActiveManager()`, `GetAllTeams()`, `GetPlayersByTeam()`, `GetFreeAgents()`, `GetGamesByGameDay()`, `GetStandingsGames()`, `SaveGameAttendance()`, `CheckAndUpdateRecords()`, `SaveSeasonEndRecords()`, `UpdateHistoricalPlayerStatsFromSeason()`, `CalculateTeamChemistry()`, `CompleteTrainingAndApply()`, `EvaluateMonthlyAwards()`, `SignSponsor/SignTVChannel`, `AddOffer/GetMaturedUnprocessedOffers`, `GetMatured...`, `SeedDraftPicks`, `TransferDraftPicks`, `AutoSeedLineup`, ~100 public methods total.
- **Lifecycle:** `Awake` = singleton + `DontDestroyOnLoad`; `OnDestroy` closes connection. No DB work until a slot is initialized.
- **Risks:** 5600-line monolith; heavy UI code often does multiple sequential queries per refresh (GC + main-thread blocking). See `TODO_TECHNICAL_DEBT.md`.

## S2. Save system

- **Files:** `GameSaveManager.cs` (static), `SaveSlotInfo.cs`, `DatabaseManager.InitSaveSlot`.
- **Behavior:** one `save_{n}.db` per slot + `saves.json` metadata + `template.db` seed. `InitSaveSlot` creates tables, runs migrations, then either clones `template.db` (13 static tables) or seeds fresh.
- **Detail:** full doc in `SAVE_SYSTEM.md`.

## S3. Match simulation engine

- **Responsibility:** possession-by-possession simulation producing per-player stats, injuries, fatigue, record checks.
- **Files:** `GameSimulator.cs` (732 ln). DTOs: `PlayerStatSnapshot`, `TeamStats`, `GameResult` (nested).
- **Entry point:** `SimulateGame(GameData, homePlayers, awayPlayers, homeChemistry, awayChemistry, isHome)`.
- **Pipeline:** filter injured → team ratings (mean `overall + (morale-50)*0.1`, home chemistry bonus ×0.15, away ×0.10, home court +1.5) → `pace = Clamp(101 + (hR+aR-140)*0.06 + rand(-2,2), 95, 107)` → 4 quarters (`SimQuarter`) + up to 5 OTs (`SimOvertime`, 24 possessions) → elite floors (passing≥95 min assists, rebounding≥95 min boards, steals≥90/blocks≥95) if `minutes≥20` → persist stats (`DeletePlayerGameStatsForGame` + `SavePlayerGameStats`) → `CheckAndUpdateRecords` (except allstar) → fatigue `fisico -= minutes*0.25` (×1.5 on real back-to-back) → `CheckInjuries` (base 0.008, up to ×5.5 if `fisico<30`).
- **Simulation internals:** `RunPossession` (turnover prob `0.11 + (defR-offR)*0.0003`; weighted shooter `(overall/100)^2.2 * FisicoPenalty`; shot-type split by position with `three_point` adjustment; defense = best `defense` on floor, factor `(def-70)*0.005`; FG% clamped (3pt 0.28–0.51, 2pt 0.40–0.67); and-one 6%; fouls on miss 18%/14% (3pt/2pt); rebounds `DoReb`; turnovers `DoTO`; fouls `DoFoul` prefers C/PF). Rotations via `SubSchedule(q)` (75% skill / 25% random).
- **Data used:** `GameData`, `PlayerData`, `PlayerGameStats`, `INJURY_TYPES` (27 weighted types, from "Sobrecarga muscular" w60/1–3d to "Rotura ligamento cruzado anterior" w1/180–300d).
- **Dependencies:** `DatabaseManager.Instance`, records system.
- **Risks:** non-deterministic (no seed); uses `UnityEngine.Random`; single-threaded (blocks main thread for a few ms per game — acceptable for 15 games/day).

## S4. Schedule generator

- **Files:** `ScheduleGenerator.cs` (220 ln).
- **Behavior:** `GenerateSchedule(season, teams)` builds 82 games/team (intra-division ×4, cross-division ×3/×4, inter-conference ×2), assigns days (max 15 games/day, avoids back-to-backs, max 5 games/team/week, All-Star window Feb 8–14), creates the All-Star game (`home_team_id=-1`, `away_team_id=-2`, `game_type="allstar"`), saves via `SaveRegularSeasonGames` (transaction).
- **Called by:** `PreseasonController` when `season.generated == 0` (then `phase="regular"`).

## S5. Playoffs generator

- **Files:** `PlayoffsGenerator.cs` (558 ln).
- **Behavior:** idempotent, DB-driven state machine. `GeneratePlayIn` (2 games/conf: 7v8, 9v10 on last regular day +7) → `CreatePlayInEliminator` (loser 7v8 vs winner 9v10, +2 days) → `GeneratePlayoffs` (8 seeds/conf, 4 R1 series, 2-2-1-1-1) → `AdvancePlayoffSeries` (series win at 4; deletes unplayed games in finished series; creates next round) → Finals.
- **Series labels:** `playin-7-8-{east|west}`, `playin-9-10-*`, `playin-elim-{conf}`, `playoff-r1-{conf}-{1v8|4v5|2v7|3v6}`, `playoff-r2-{conf}-s{1|2}`, `playoff-r3-{conf}-s1`, `playoff-r4-finals`.
- **Orchestrated by:** `DashboardController.ProcessGameDayRoutine` phase transitions.

## S6. Draft

- **Files:** `DraftGenerator.cs` (431 ln), `DraftPickData.cs`.
- **Behavior:** `GenerateDraft(season, managerId)`: reset `is_rookie`; standings-based order; lottery odds NBA 2024+ (`{0.140×3, 0.125, 0.105, 0.090, 0.075, 0.060, 0.045, 0.030, 0.020, 0.015, 0.010, 0.005}` for 14 teams); 60 picks (2 rounds); class quality roll (`<0.15 weak -3, <0.70 normal, <0.90 strong +2, else historic +4`); generational talents; procedural players (names, positions, heights/weights, 11 attrs, overall = mean, potential, nationality 90% USA else one of 54 ISO codes, college from `NCAATeams`, salary tiers by pick, `contract_years=4`, `is_rookie=1`); resolves traded picks via `current_team_id`; `PlayerPhotoHelper.CreateRookiePhoto`.
- **UI:** `EndSeasonController` (`_btnDraft`).
- **Draft picks model:** `SeedDraftPicks` (2×30), `GetDraftPicksForSeason`, `TransferDraftPicks`, `UpdateDraftPickOwner`.

## S7. Trades & salary cap

- **Files:** `TradeHelper.cs` (251 ln), `TradeData.cs`, `TradeOfferData.cs`, `MarketController.cs`, `DashboardController.cs`.
- **Constants (2025-26):** `SALARY_CAP=174_647_000`, `LUXURY_TAX=220_428_000`, `FIRST_APRON=229_015_000`, `SECOND_APRON=241_686_000`, `NT_MLE=14_100_000`, `T_MLE=5_700_000`, `MIN_SALARY=2_000_000`, `MAX_ROSTER=17`.
- **`ValidateTrade(...)`:** roster limits (min 10 / max 17), apron-tier salary matching (2nd apron/hard-capped: no aggregation, incoming ≤ outgoing; 1st apron: ≤110% outgoing; else `out*2+250K` <7.5M / `+7.5M` <29M / `125%+250K`).
- **`CalculateLuxuryTax(payroll)`:** brackets `(5M,1.5),(5M,1.75),(5M,2.5),(5M,3.25),(∞,3.75)` above `LUXURY_TAX`.
- **`EvaluateTrade(...)`:** AI accept decision → `acceptScore` 0–100 (picks sweetener `PickBonus`, overall comparison with steps 90/85/80, total OVR diff clamped ±20, financial situation, roster need +15 if ≤12 players, age factor, `Random.Range(-5,6)`), threshold 50 (40 if >2nd apron, 45 if >1st apron).
- **User-side:** `MarketController` builds trade/offer screens; **AI offers to the player** are generated in `DashboardController.GenerateAITradeOffersForPlayer` and answered via `ShowNextPendingTradeOffer`; `ProcessAITransfers` runs every ≥15 game days inside the transfer window (Sep 1 → Feb 8): AI teams with <12 players sign FAs directly, otherwise try AI trades (max 2), plus `ProcessStarFreeAgentSignings`.
- **`TradeData.trade_type`:** `"trade"`, `"free_agent"`, `"pick_trade"`, `"sign_and_trade"` (latter unused — see `TODO`).

## S8. Economy & finances

- **Files:** `FinanceRecord.cs`, `TeamSettingsData.cs`, `SponsorData.cs`, `TvChannelData.cs`, `LoanData.cs`, logic in `DashboardController`, `FinancesController`, `CarteraController`, `ArenaController`, `SponsorsController`, `TVController`, `LoansController`.
- **Revenue types (`FinanceRecord`):** `1=Taquilla`, `2=Abonos`, `3=Patrocinios`, `4=Televisión`, `5=Remodelación`, `6=Despido`, `7=Sueldos jugadores`, `8=Sueldos empleados`, `9=Préstamo`, `10=Luxury tax`, `11=Buyout`.
- **Attendance formula** (`DashboardController.CalculateAttendance`): `capacity * (base factors) * randomFactor(0.92–1.08) * priceFactor * objectiveFactor`, clamped to capacity. Home game base: `0.30 + fanConfidence/100*0.35 + winPct*0.15 + rivalRep/5*0.08`. Away: `0.55 + winPct*0.30 + myRep/5*0.06`. Others: `0.55 + winPct*0.40`. `priceFactor = Clamp(Exp(-(ticketPrice-30)/150), 0.20, 1.0)`. `objectiveFactor` = `Clamp(1 - posGap*0.06, 0.30, 1)` when not meeting the team objective.
- **Ticket revenue** = `attendance * ticket_price * arenaMultiplier` (arenaMultiplier by `PABELLON` staff reputation: 5→1.20, 4→1.15, 3→1.10, 2→1.06, 1→1.03). Persisted in `game_attendance`.
- **Monthly:** `ProcessMonthlyPayroll` (salaries + employees), `ProcessSubscriptionRevenue` (subscriptions).
- **Sponsors/TV:** `SignSponsor`/`FireSponsor`, `SignTVChannel`/`FireTVChannel` (max 3 TV), with `initial_income` and `home_game_income` per game; contracts in years.
- **Loans:** `LoanData` (slot, amount, total_debt, remaining_months, interest_rate, monthly_payment).
- **Arena:** renovations `general_seats (+3000, $10M, 3wk)`, `tribune (+2000, $20M, 5wk)`, `vip_seats (+1000, $35M, 8wk)`; cost discounted by `PABELLON` reputation (5→0.80 … 1→0.97); max capacity 50,000; tickets/subscriptions in `ArenaController`.
- **Budget:** `TeamData.budget` updated in place; expenses recorded. `CheckBudgetAfterGame` / `budget_red_warnings` (≥3 → fired modal, `CheckBudgetWarning`).

## S9. Contracts, offers, renewals, FAs

- **Files:** `RosterController.cs` (renewals, dismissals, buyout), `MarketController.cs` (FA offers, trades), `OfferData.cs`, `DashboardController.ProcessMaturedOffers` (result resolution).
- **Offer resolution:** offers mature after 7 days (`day_sent + 7 <= currentDay`). Acceptance uses `RosterController.CalculateAcceptScore(player, salary, years, gamesPlayed, chemistry)` (base 50; salaryIncrease ≥30%→+25, ≥10%→+15, ≥0→+5, else −|inc|×50; age ≥32→+10, ≥28→+5, ≤23→−5; overall ≥85→−5, <75→+5; games ≥50→+10, ≥30→+5, <10→−10; years ≥4→+10, ≥3→+5, <2→−5; chemistryMod=(chem−50)*0.3; clamp 10–95). Roll: `Random(1,101) <= score`.
- **`GetMaxOfferBreakdown`:** max salary by experience (≤6yr: 25% cap; ≤9yr: 30%; else 35%), Bird tiers for own-team renewals (≥3 seasons full, 2 seasons early = max(salary×1.75, cap×10.5%), else non-Bird = salary×1.20), **FA external → no Bird rights** (`birdMax=0`), cap space = `salary + max(0, cap − payroll)`, exceptions if over cap (NT-MLE ≤1st apron, T-MLE ≤2nd, else minimum), final = min(maxByExp, max(bird, capSpace) or exception).
- **Hard cap:** using NT-MLE when over cap sets `first_apron_hard_capped=1` → blocks any payroll > FIRST_APRON in later transactions.
- **Renewal cooldown:** accepted renewal → `renewal_cooldown_day = day + 365`; rejected → +15 (FA rejected → +14).
- **Dismissal/buyout:** `Roster.uxml` has `DESPEDIR`/`RESCINDIR` buttons; buyout with stretch is partly documented in `PLAN.md` but **only a dismissal with severance exists in code** (see `TODO`).

## S10. Training & progression

- **Files:** `TrainingData.cs`, `TrainingController.cs`, `DatabaseManager.CompleteTrainingAndApply`, `StartNewSeason` (aging/progression).
- **Training:** assign a player an attribute to train for `duration_days`; on completion, attribute +2 (via reflection `typeof(PlayerData).GetProperty`), `overall` recalculated and capped by `potential`. Processed in `ProcessTraining()` on game days.
- **Progression (StartNewSeason):** age +1; attribute delta by age band (`≤22:+4, ≤27:+1, ≤30:0, ≤34:−3, else −5`) + position-priority +1 + rand(−1,1); overall recalculated (cap potential). Retirements at `age ≥ 40`.

## S11. Soft stats: morale, relationships, personalities, chemistry

- **Files:** `PlayerData` (morale, fisico), `PlayerPersonalityData.cs`, `PlayerRelationshipData.cs`, `TeamData.team_chemistry`, `ManagerData.fan_confidence`; logic in `DashboardController` (per game), `ManagerController` (psychologist), `DatabaseManager` (seed + update + calculate).
- **Morale after game** (`UpdatePlayersMoraleAfterGame`): role delta (minutes vs expected: Estrella 40', Titular 28', Banquillo 10', Último 3') + form delta (avg last-5 rating: ≥28→+2 … ≤10→−2) + streak (win%≥0.7→+1, ≤0.3→−1) + contract (1yr→−1) + injury (−2), total clamped −3..+3, morale 0..100. Morale < 20 triggers complaint messages (`Queja`/`Preocupación`).
- **Fan confidence after game:** win home +4 / away +2 (+1 if margin ≤5 or ≥20); loss home −3 / away −2 (−1 if margin ≤5 or ≥20); 0..100.
- **Chemistry:** `CalculateTeamChemistry(teamId, gameDay)` + `UpdateTeamChemistry` after each game; seeded personalities/relationships; `UpdateRelationshipsAfterGame` evolves bonds.
- **Psychologist:** `ProcessPsychologistMorale` uses hired staff (`ManagerController`).

## S12. Injuries & fatigue

- **Files:** `GameSimulator.INJURY_TYPES`, `ProcessGameInjuries` (Dashboard), `ProcessInjuries` (daily recovery).
- **Fatigue:** `fisico` (default 99) reduced `minutes*0.25` per game (×1.5 if real back-to-back), recovered daily (`ProcessFisicoRecovery`).
- **Injury risk:** base 0.008/game, multiplier up to ×5.5 when `fisico<30`; 27 weighted types; injured players excluded from sim; recovery messages on return.

## S13. News & messages

- **Files:** `MessageData.cs`, `QuickNewsGenerator.cs`, `MessagesController.cs`.
- **Sources of messages:** match results, offers (accept/reject), renewals, signings, injuries, recoveries, morale complaints, trade window reminder, renovations, budget warnings, monthly awards, star FA signings, etc. — all inserted with `AddMessage` (sender_type: 0 = system, 1 = player, 2 = news).
- **Quick news** (`QuickNewsGenerator.Generate`): season milestones (game 41/82), streaks ≥5, upsets (avg diff ≥15 and favorite loses), triple-doubles, 40+ point explosions; max 2/day, deduped.

## S14. Player photos

- **Files:** `PlayerPhotoHelper.cs`, `Art/Resources/PlayerPhotos/` (602 player photos 256×256 + 100 defaults 200×200).
- **Cascade:** `Resources/PlayerPhotos/{id}` → `Resources/PlayerPhotos/{photoField}` → `persistentDataPath/PlayerPhotos/{slot}/{id}.png` → legacy `persistentDataPath/PlayerPhotos/{id}.png` → `Resources/PlayerPhotos/default`.
- **Rookies:** `CreateRookiePhoto` copies a random default PNG into the slot folder.

## S15. Audio & cursor

- **Files:** `AudioManager.cs`, `CursorManager.cs`, `Art/Resources/Audios/` (7 WAV), `Art/UI/Icons/cursor_{default,hand}.png`.
- **Audio:** `PlayMusic(name)` (loop, no restart if same), `PlaySFX(name)` (one-shot, cached), volumes persisted (`TF_Audio_Master/Music/SFX`), `SetQualityLevel` (`TF_Graphics_Quality`). Menu music `backgroundMenu` on start.
- **Cursor:** `SetDefaultCursor`/`SetHandCursor`, `RegisterHandCursor(element)` (MouseEnter/Leave, TrickleDown).

## S16. Records, awards & history

- **Files:** `TeamRecordData/HistoricalRecordData/HistoricalPlayerStatsData/SeasonRecord/AllStarRecord/AwardsRecord/FinalsRecord/FinalsPlayerStatsData/MonthlyAwardData/QuintetRecord/CoachRankingData` + `HistoricalPlayerStatsSeeder/TeamRecordSeeder/PalmaresSeeder/AllStarAppearanceSeed`.
- **Behavior:** in-game record checks (`CheckAndUpdateRecords` on stats), season-end awards (`SaveSeasonEndRecords`, `EvaluateMonthlyAwards`), All-Star MVP record, career stats archive (`UpdateHistoricalPlayerStatsFromSeason`), coach ranking (score updated on seasons), player awards (`GetPlayerAwards`).

---

## Open questions

- `ProManager` differences (none found in code). 
- Whether buyout (stretch provision) is intended but unfinished (`PLAN.md` §4.2 mentions it; only dismissal exists).
- Whether AI star FA signings block the user's offers correctly when they overlap (`ProcessStarFreeAgentSignings` vs user's pending offers) — see `MEMORY.md`.
