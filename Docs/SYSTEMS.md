# SYSTEMS — Tactical Five Core Systems

> Each system: responsibility, files, key methods, data used, dependencies, lifecycle, risks. **[F]** = fact, **[D]** = deduction, **[H]** = hypothesis. State: HEAD `81d9e4f` (2026-08-16).

---

## S1. Database system (SQLite)

- **Responsibility:** single gateway to all persisted game data.
- **Files:** `DatabaseManager` split into 9 partial classes: `DatabaseManager.cs` (968 ln: singleton, connection, ambient/async, save-slot init, template build/clone, `CreateTables`, `RunMigrations`, seed orchestration, HOF/retired-number seeders, `GetHoFMembers`), `DatabaseManager.Teams.cs` (221), `.Players.cs` (148), `.Staff.cs` (110), `.Manager.cs` (123), `.Games.cs` (290), `.Seeding.cs` (1354), `.Records.cs` (3586), `.Achievements.cs` (39). Plus `SQLite.cs` (sqlite-net wrapper, 5445 ln), `GameSaveManager.cs` (static), `LeagueSettings.cs`.
- **Key API (selected):** `InitSaveSlot(int)`, `CreateTables()`, `RunMigrations()`, `SeedStaticDataIfNeeded()`, `StartNewSeason(...)` (`Records.cs:2568`), `RunInBackground`/`RunInBackgroundAsync`, `GetActiveManager()`, `GetAllTeams()`, `GetPlayersByTeam()`, `GetFreeAgents()`, `GetGamesByGameDay()`, `GetStandingsGames()`, `CheckAndUpdateRecords()`, `SaveSeasonEndRecords()`, `UpdateHistoricalPlayerStatsFromSeason()`, `CalculateTeamChemistry()`, `CompleteTrainingAndApply()`, `EvaluateMonthlyAwards()`, `SignSponsor/SignTVChannel`, `AddOffer/GetMaturedUnprocessedOffers`, `SeedDraftPicks`, `TransferDraftPicks`, `AutoSeedLineup`, `WouldInduct`/`TryInductIntoHallOfFame`, `ShouldRetireNumber`/`TryRetireNumber`, `DecideTeamOption`/`DecidePlayerOption`/`EstimateMarketSalary`, ~150 public methods total.
- **Async:** `_ambientDb` AsyncLocal connection + WAL + `Task.Run` (see `ARCHITECTURE.md §7`). Thread-static `System.Random` (`Rng`) for background work.
- **Lifecycle:** `Awake` = singleton + `DontDestroyOnLoad`; `OnDestroy` closes connection. No DB work until a slot is initialized.
- **Schema versioning [F]:** `schema_migrations` table + `PRAGMA user_version = 2` (`DatabaseManager.cs:286`); migrations run via `IsMigrationApplied`/`MarkMigrationApplied` plus `PRAGMA table_info` column checks.
- **Risks:** `Records.cs` (3586 ln) is still the biggest partial; heavy UI code does multiple sequential queries per refresh (GC + main-thread blocking in places). See `TODO_TECHNICAL_DEBT.md`.

## S2. Save system

- **Files:** `GameSaveManager.cs` (static), `SaveSlotInfo.cs`, `DatabaseManager.InitSaveSlot`.
- **Behavior:** one `save_{n}.db` per slot + `saves.json` metadata + `template.db` seed. `InitSaveSlot` opens with WAL, creates tables, runs migrations, then clones `template.db` (15 static tables) or seeds fresh.
- **Detail:** full doc in `SAVE_SYSTEM.md`.

## S3. Match simulation engine

- **Responsibility:** possession-by-possession simulation producing per-player stats, injuries, fatigue, record checks.
- **Files:** `GameSimulator.cs` (936 ln). DTOs: `PlayerStatSnapshot`, `TeamStats`, `GameResult`, `PlayByPlayEvent`, `StatDelta`, `PossessionOutcome` (nested).
- **Entry point:** `SimulateGame(GameData, homePlayers, awayPlayers, homeChemistry, awayChemistry, isHome)`.
- **Play-by-play capture:** la crónica se registra sin alterar el resultado (`PlayByPlayEvent`: quarter, texto en español, marcador acumulado, `timeElapsed`, deltas `StatDelta`) vía `CaptureBox`/`DiffBox` + minutos por jugador; se consume en el overlay de `MatchDayController` cuando `GetSimMode()==1`.
  - **Pipeline [F]:** active rotations filter injured players, but team-rating lists currently include them; the `+1.5` home-court bonus is only enabled for the user's home-game flag in the Dashboard call path. Then chemistry → pace → quarters/OT → target-minute rotations → stats → records → fatigue → injuries. See `GAMEPLAY.md §2` for the exact formulas and deviations.
  - **Simulation internals:** `RunPossession` handles shooting, fouls, rebounds and blocks. Rebounds use attribute exponents plus position multipliers; blocks use one collective chance with weighted defender selection and a 20% cap. Assists remain 35% weighted by `passing^3`; turnovers prefer PG/SG/SF handlers; fouls prefer C/PF. Rotations via `SubSchedule(q)` (75% skill / 25% random).
- **Data used:** `GameData`, `PlayerData`, `PlayerGameStats`, `INJURY_TYPES` (27 weighted types, "Sobrecarga muscular" w60/1–3d … "Rotura ligamento cruzado anterior" w1/180–300d).
- **Dependencies:** `DatabaseManager.Instance`, records system.
- **Risks:** uses `UnityEngine.Random`; single-threaded (main thread, measured fast — intentional). Duplicated team-rating formula with `MatchupPreview` must stay in sync.

## S4. Schedule generator

- **Files:** `ScheduleGenerator.cs` (219 ln).
- **Behavior:** `GenerateSchedule(season, teams)` builds 82 games/team: same division 4 games each pair; intra-conference cross-division `count = offset<3 ? 4 : 3`; inter-conference 2 games. Assigns days (max 15 games/day, `FindDay` scoring −100 for back-to-back candidates, −30 if ≥4 games/week, −2×games/day; ≤5 games/week), All-Star window Feb 8–14 excluded, All-Star game Saturday of 2nd week of Feb (`home_team_id=-1`, `away_team_id=-2`, `game_type="allstar"`). Season Oct 22 → Apr 15. Saves via `SaveRegularSeasonGames` (transaction).
- **Called by:** `PreseasonController` when `season.generated == 0` (then `phase="regular"`).

## S5. Playoffs generator

- **Files:** `PlayoffsGenerator.cs` (558 ln).
- **Behavior:** idempotent, DB-driven state machine. `GeneratePlayIn` (2 games/conf: 7v8, 9v10 on last regular day +7) → `CreatePlayInEliminator` (loser 7v8 vs winner 9v10, +2 days) → `GeneratePlayoffs` (8 seeds/conf: top-6 + play-in winners; 4 R1 series `1v8,4v5,2v7,3v6`; best-of-7 2-2-1-1-1 home `{0,1,4,6}`; games every 2 days) → `AdvancePlayoffSeries` (series win at 4; deletes unplayed games in finished series; `CheckAndCreateNextRound` triggers R2 → ConfFinal → Finals).
- **Series labels:** `playin-7-8-{east|west}`, `playin-9-10-*`, `playin-elim-{conf}`, `playoff-r1-{conf}-{1v8|4v5|2v7|3v6}`, `playoff-r2-{conf}-s{1|2}`, `playoff-r3-{conf}-s1`, `playoff-r4-finals`.
- **Orchestrated by:** `DashboardController.ProcessGameDayRoutine` phase transitions.

## S6. Draft

- **Files:** `DraftGenerator.cs` (511 ln), `DraftPickData.cs` (now with `protected_from`, `is_swap`, `swap_original_team_id`).
- **Behavior:** `GenerateDraft(season, managerId)`: reset `is_rookie`; standings-based order; lottery odds NBA 2024+ (`{0.140×3, 0.125, 0.105, 0.090, 0.075, 0.060, 0.045, 0.030, 0.020, 0.015, 0.010, 0.005}` for 14 teams); 60 picks (2 rounds; R2 same order); class quality roll (`<0.15 weak -3, <0.70 normal, <0.90 strong +2, else historic +4`); generational talents (historic 1–2; top-5 picks 1–3%); base avg by pick (#1 `83+(-2..3)` … R2 `60 - (pick-30)*10/29`); traits 30% (+8 to 1–2 attrs); potential tiers; procedural players (names, positions, heights/weights, 11 attrs, overall=mean capped by potential, nationality 90% USA else 54 ISO codes, college from `NCAATeams`, salary tiers by pick, `contract_years=4` guaranteed 4, `is_rookie=1`); resolves traded picks via `current_team_id` including **protections** (`protected_from`, reverts to original within range) and **swaps** (`is_swap`/`swap_original_team_id`, holder takes better slot); `AssignJerseyNumber`; `PlayerPhotoHelper.CreateRookiePhoto`.
- **UI:** `EndSeasonController` (`_btnDraft`).
- **Draft picks model:** `SeedDraftPicks` (2×30), `GetDraftPicksForSeason`, `TransferDraftPicks`, `UpdateDraftPickOwner`.

## S7. Trades & salary cap

- **Files:** `TradeHelper.cs` (293 ln), `TradeData.cs`, `TradeOfferData.cs`, `MarketController.cs`, `DashboardController.cs`.
- **Constants (2025-26):** `SALARY_CAP=174_647_000`, `LUXURY_TAX=220_428_000`, `FIRST_APRON=229_015_000`, `SECOND_APRON=241_686_000`, `NT_MLE=14_100_000`, `T_MLE=5_700_000`, `MIN_SALARY=2_000_000`, `MAX_ROSTER=17`. +5%/season in `StartNewSeason`.
- **`ValidateTrade(...)`:** roster limits (min 10 / max 17), apron-tier salary matching (2nd apron/hard-capped: no aggregation, incoming ≤ outgoing; 1st apron: ≤110% outgoing; else `out*2+250K` <7.5M / `+7.5M` <29M / `125%+250K`). S&T players don't count as outgoing roster/payroll on their own side but count as incoming salary for the other side.
- **`PickBonus`:** R1 `10 + (30-slot)/3`; R2 `5 + (30-slot)/5`; else 3; protected top-N subtracts `protected_from*2`, min 1.
- **`CalculateLuxuryTax(payroll)`:** brackets `(5M,1.5),(5M,1.75),(5M,2.5),(5M,3.25),(∞,3.75)` above `LUXURY_TAX`.
- **`EvaluateTrade(...)`:** AI accept decision → `acceptScore` 0–100 (pick sweeteners ±PickBonus, player quality steps 90/85/80/75, `clamp(aTotalOvr-bTotalOvr,-20,20)`, financial branches by B payroll vs aprons ±, roster need +15 if ≤12 / +5 if ≤14, age factor, `Random.Range(-5,6)`), threshold 50 (40 if B>2nd apron, 45 if >1st apron).
- **User-side:** `MarketController` builds trade/offer screens; **AI offers to the player** via `DashboardController.GenerateAITradeOffersForPlayer` and answered through `ShowNextPendingTradeOffer`; `ProcessAITransfers` runs every ≥10 game days inside the transfer window (Sep 1 → Feb 8), reduced to **3-5 game days** during deadline week (`IsDeadlineWeek`). AI teams with <12 players sign FAs directly, otherwise try AI trades (max 3), plus `ProcessStarFreeAgentSignings`.
- **AI strategy per team:** `enum TeamStrategy { Rebuild, Balanced, Contend }` classified by `GetTeamStrategy` (top 4 conference or 2+ stars OVR≥85 → Contend; bottom 4 or young roster without stars → Rebuild), cached in `_teamStrategyCache`. Per-team cooldowns (`_teamTradeCooldown`, cleared on season change in `OnEnable`): Contend 6d (0.45), Rebuild 8d (0.40), Balanced 15d (0.25); 3-5 during deadline week. **Contend** (`TryFindAITrade`): upgrades up to OVR 90, adds future pick when OVR>84, protects young (age<26, OVR≥82). **Rebuild** fire sale (`TrySellVeteran`): veterans ≥30 for young/picks. Offers strategy-aware (`PickTradeTarget`/`BuildOfferPackage`). `ProcessStarFreeAgentSignings` prioritizes Contend > Balanced > Rebuild; Rebuild never signs OVR≥85.
- **Deadline week (Feb 1-8):** cooldown 3-5 days; contenders (`IsTeamContender`) offer extra picks; `[DEADLINE]` prefix on AI offers; golden countdown banner in Market.
- **Deadline Day (Feb 7):** `OnActionClicked` intercepts btnAction (date check `IsFeb7OfYearEnd`); modal DEADLINE DAY (IR AL MERCADO / CERRAR, once per season via `_deadlineDayModalShown`/`_deadlineModalSeasonId`); no day advanced.
- **`TradeData.trade_type`:** `"trade"`, `"free_agent"`, `"pick_trade"`, `"sign_and_trade"`.
- **Sign-and-trade (S&T) de FA propio:** sección "FA RECIENTES (BIRD RIGHTS) — SIGN & TRADE" en el panel de traspaso; `ProcessSATrade` firma a un `IsOwnRecentFA` con su max Bird y lo traspasa de inmediato (dos `TradeData`); receptor bajo hard cap del 1er apron. `ValidateTrade`/`EvaluateTrade` aceptan `teamASignSalaries`/`teamBSignSalaries`. IA propone S&T (`GenerateAITradeOffersForPlayer`) y respeta `pendingSATIds`.
- **Trade block:** `players.on_trade_block` marcado desde `RosterController`; los jugadores TRANSFERIBLE se destacan en Market.

## S8. Economy & finances

- **Files:** `FinanceRecord.cs`, `TeamSettingsData.cs`, `SponsorData.cs`, `TvChannelData.cs`, `LoanData.cs`, logic in `DashboardController`, `FinancesController`, `CarteraController`, `ArenaController`, `SponsorsController`, `TVController`, `LoansController`.
- **Revenue types (`FinanceRecord`):** `1=Taquilla`, `2=Abonos`, `3=Patrocinios`, `4=Televisión`, `5=Remodelación`, `6=Despido`, `7=Sueldos jugadores`, `8=Sueldos empleados`, `9=Préstamo`, `10=Luxury tax`, `11=Buyout`.
- **Attendance formula** (`DashboardController.CalculateAttendance`): `capacity * (base factors) * randomFactor(0.92–1.08) * priceFactor * objectiveFactor`, clamped to capacity. Home game base: `0.30 + fanConfidence/100*0.35 + winPct*0.15 + rivalRep/5*0.08`. Away: `0.55 + winPct*0.30 + myRep/5*0.06`. Others: `0.55 + winPct*0.40`. `priceFactor = Clamp(Exp(-(ticketPrice-30)/150), 0.20, 1.0)`. `objectiveFactor` = `Clamp(1 - posGap*0.06, 0.30, 1)` when not meeting the team objective.
- **Ticket revenue** = `attendance * ticket_price * arenaMultiplier` (by `PABELLON` reputation: 5→1.20 … 1→1.03). Persisted in `game_attendance`.
- **Monthly:** `ProcessMonthlyPayroll` (players `sum/12` type 7 + employees `sum/12` type 8; `payrollDays = {1,31,61,91,121,151,181}`), `ProcessTeamLuxuryTax` (`annual/12`, type 10), `ProcessSubscriptionRevenue` (days 10–12, type 2).
- **Cap sheet panel** (`FinancesController.BuildCapSheet`): summary payroll/cap/apron/space (from `LeagueSettings`, fallback `TradeHelper`); projection current+4 years at +5% (`ProjectedCap`); committed payroll `Σ salary` while `contract_years > yr`; expiring (`contract_years==1` → FA); exceptions NT-MLE/T-MLE/minimum + luxury/aprons.
- **Sponsors/TV:** `SignSponsor`/`FireSponsor`, `SignTVChannel`/`FireTVChannel` (max 3 TV), `initial_income` + `home_game_income`, contracts in years.
- **Loans:** `LoanData` (slot, amount, total_debt, remaining_months, interest_rate, monthly_payment).
- **Arena:** renovations `general_seats (+3000, $10M, 3wk)`, `tribune (+2000, $20M, 5wk)`, `vip_seats (+1000, $35M, 8wk)`; cost discounted by `PABELLON` reputation (5→0.80 … 1→0.97); max 50,000; tickets/subscriptions in `ArenaController`.
- **Budget:** `TeamData.budget` updated in place; `CheckBudgetAfterGame`/`budget_red_warnings` (≥3 → fired modal; ≥2 in ProManager).

## S9. Contracts, offers, renewals, FAs

- **Files:** `RosterController.cs` (renewals, dismissals, buyout, trade block), `MarketController.cs` (FA offers, trades, S&T), `OfferData.cs`, `DashboardController.ProcessMaturedOffers`.
- **Offer resolution:** offers mature after 7 days (`day_sent + 7 <= currentDay`). Acceptance via `RosterController.CalculateAcceptScore(player, salary, years, gamesPlayed, chemistry)` (base 50; salaryIncrease ≥30%→+25, ≥10%→+15, ≥0→+5, else −|inc|×50; age ≥32→+10, ≥28→+5, ≤23→−5; overall ≥85→−5, <75→+5; games ≥50→+10, ≥30→+5, <10→−10; years ≥4→+10, ≥3→+5, <2→−5; chemistryMod=(chem−50)*0.3; clamp 10–95). Roll: `Random(1,101) <= score`.
- **Contract options:** renewal and FA offers expose **TO/PO toggles** (mutually exclusive); if set → `guaranteed_years = max(0, offer_years − 1)`, `has_team_option`/`has_player_option` = 1. `ProcessMaturedOffers` persists them; messages format via `FormatContractYears`.
- **Re-firma de FA propio (Bird rights):** `players.last_team_id` (set at seed/sign; kept on option decline/expiry). `IsOwnRecentFA(p, teamId)` = `team_id==0 && last_team_id==teamId && seasons_with_team>0`. `NewSeasonController` modal de re-firma (oferta diferida, madura 7 días); al madurar reasigna `team_id` o cancela si firmó por otro. `OnSignPlayer` usa `isOwnRecentFA` para conservar Bird rights. IA solo evita `offer_type==1`, así que un re-sign no es "robado".
- **`GetMaxOfferBreakdown`:** max by experience (≤6yr 25% cap, ≤9yr 30%, else 35%); Bird tiers (≥3 seasons full, 2 seasons early = max(salary×1.75, cap×10.5%), else non-Bird = salary×1.20); **FA external → no Bird** (`birdMax=0`); cap space = `salary + max(0, cap − payroll)`; exceptions if over cap (NT-MLE ≤1st apron, T-MLE ≤2nd, else minimum; **ProManager: Taxpayer MLE only, no NT-MLE**); final = min(maxByExp, rawMax).
- **Hard cap:** NT-MLE over cap sets `first_apron_hard_capped=1` → blocks any payroll > FIRST_APRON later.
- **Renewal cooldown:** accepted renewal → `day + 365`; rejected → +15 (FA rejected → +14).
- **Buyout:** `ConfirmBuyout` with stretch `contract_years * 2`; TYPE_BUYOUT (11) per year.
- **Player/team options resolution:** `DecideTeamOption` (team decides at new season), `DecidePlayerOption` (AI: market, happiness, loyalty, role, age, team success). NewSeason modal shows results.

## S10. Training & progression

- **Files:** `TrainingData.cs`, `TrainingController.cs`, `DatabaseManager.CompleteTrainingAndApply`, `StartNewSeason`.
- **Training:** player+attribute, `duration_days`; on completion attribute +2 (reflection `typeof(PlayerData).GetProperty`), `overall` recalculated (cap potential). `ProcessTraining()` on game days.
- **Progression (StartNewSeason):** age +1; attribute delta by age band (`≤22:+4, ≤27:+1, ≤30:0, ≤34:−3, else −5`) + **position-priority +1** + **position-based athletic decline** (athletic attrs decline faster for older players per position) + **mentoring** (veterans boost young) + rand(−1,1); overall recalculated (cap potential). Retirements at `age ≥ 40` (HOF + retired-number capture first).

## S11. Soft stats: morale, relationships, personalities, chemistry

- **Files:** `PlayerData` (morale, fisico), `PlayerPersonalityData.cs`, `PlayerRelationshipData.cs`, `TeamData.team_chemistry`, `ManagerData.fan_confidence`; logic in `DashboardController`, `ManagerController` (psychologist), `DatabaseManager`.
- **Morale after game** (`UpdatePlayersMoraleAfterGame`): role delta (minutes vs expected: Estrella 40', Titular 28', Banquillo 10', Último 3') + form delta (avg last-5 rating: ≥28→+2 … ≤10→−2) + streak (win%≥0.7→+1, ≤0.3→−1) + contract (1yr→−1) + injury (−2), total clamped −3..+3, morale 0..100. Morale <20 → complaints (`Queja`/`Preocupación`); <10 → demands trade.
- **Fan confidence after game:** win home +4 / away +2 (+1 if margin ≤5 or ≥20); loss home −3 / away −2 (−1 if margin ≤5 or ≥20); 0..100.
- **Chemistry:** `CalculateTeamChemistry(teamId, gameDay)` + `UpdateTeamChemistry` after each game; seeded personalities/relationships; `UpdateRelationshipsAfterGame` evolves bonds.
- **Psychologist:** `ProcessPsychologistMorale` uses hired staff (morale-only; injury treatment via `treated` flag is applied in Injured flow, [H] not accelerated).

## S12. Injuries & fatigue

- **Files:** `GameSimulator.INJURY_TYPES` (27 types, weights 60→1, days 1→300), `ProcessGameInjuries` (Dashboard), `ProcessInjuries` (daily recovery batch, background thread).
- **Fatigue:** `fisico` (default 99) reduced `round(minutes*0.30)` per game (×1.5 on real back-to-back), recovered +8/day **only on rest days** (teams without a game that day; background batch reads `game_day == current_game_day`).
- **Injury risk:** base 0.008/game, ×`(1+(30−fisico)*0.15)` when `fisico<30`; 27 weighted types; injured players excluded from sim; recovery messages on return.
- **Load management:** `TF_LoadMgmt_Enabled` + back-to-back detection → modal to rest up to 2 tired players (`DashboardController.cs:899-923`).

## S13. News & messages

- **Files:** `MessageData.cs`, `QuickNewsGenerator.cs`, `MessagesController.cs`.
- **Sources:** match results, offers (accept/reject), renewals, signings, injuries, recoveries, morale complaints, trade window reminder, renovations, budget warnings, monthly awards, star FA signings, achievements, deadline — all via `AddMessage` (`sender_type`: 0 system, 1 player, 2 news).
- **Quick news** (`QuickNewsGenerator.Generate`, max 2/day, dedup by title+body+day, generated en `DashboardController.ProcessGameDayRoutine` tras simular el día): season milestones (41/82), streaks ≥5 (±), upsets (avg diff ≥15 and favorite loses), triple-doubles, 40+ point explosions. **5 variantes de texto por evento elegidas al azar** (`UnityEngine.Random`, main thread) con datos reales (equipos, marcador, racha, valoraciones, jugador, stats).

## S14. Player photos

- **Files:** `PlayerPhotoHelper.cs`, `Art/Resources/PlayerPhotos/` (602 player photos + 100 defaults).
- **Cascade:** `Resources/PlayerPhotos/{id}` → `Resources/PlayerPhotos/{photoField}` → `persistentDataPath/PlayerPhotos/{slot}/{id}.png` → legacy `persistentDataPath/PlayerPhotos/{id}.png` → `Resources/PlayerPhotos/default`.
- **Rookies:** `CreateRookiePhoto` copies a random default PNG into the slot folder.

## S15. Audio & cursor

- **Files:** `AudioManager.cs`, `CursorManager.cs`, `Art/Resources/Audios/` (7 WAV), `Art/UI/Icons/cursor_{default,hand}.png`.
- **Audio:** `PlayMusic(name)` (loop, no restart if same), `PlaySFX(name)` (one-shot, cached), volumes persisted (`TF_Audio_Master/Music/SFX`), `SetQualityLevel` (`TF_Graphics_Quality`). Menu music `backgroundMenu` on start.
- **Cursor:** `SetDefaultCursor`/`SetHandCursor`, `RegisterHandCursor(element)` (MouseEnter/Leave, TrickleDown). Single instance (duplicate removed).

## S16. Records, awards, history & legacy

- **Files:** `TeamRecordData`/`HistoricalRecordData`/`HistoricalPlayerStatsData`/`SeasonRecord`/`AllStarRecord`/`AwardsRecord`/`FinalsRecord`/`FinalsPlayerStatsData`/`MonthlyAwardData`/`QuintetRecord`/`CoachRankingData` + seeders (`HistoricalPlayerStatsSeeder`, `TeamRecordSeeder`, `PalmaresSeeder`, `AllStarAppearanceSeed`).
- **Behavior:** in-game record checks (`CheckAndUpdateRecords`, also triggers `AchievementService.EvaluateRecordBreak`), season-end awards (`SaveSeasonEndRecords` incl. honor counters rings/finals_mvps/finals_played/season_mvps), All-Star MVP record, career archive (`UpdateHistoricalPlayerStatsFromSeason`), coach ranking, player awards (`GetPlayerAwards`), season quintets (`GetAllStarTeam`/`GetAllRookieTeam` via `GetBestPerPosition`).
- **Hall of Fame:** `HallOfFameHelper.ShouldInduct` (1 ring OR 1 FMVP OR 30k pts OR 15k reb OR 10k ast); `WouldInduct` (DB), `GetRetiringHallOfFameMembers` (age≥40), `TryInductIntoHallOfFame` (in `StartNewSeason`); ~100 legends seeded via `HallOfFameSeeder`; shown in Palmares + EndSeason.
- **Retired numbers:** `ShouldRetireNumber`/`TryRetireNumber`; seeded legends (`RetiredNumberSeeder` 53 + `VeteranRetiredNumberSeeder` 17); `AssignJerseyNumber` reserves; Dorsales screen.

## S17. GM achievements (Logros)

- **Files:** `AchievementCatalog.cs`, `AchievementService.cs`, `GmAchievementType.cs`, `DatabaseManager.Achievements.cs`, `GmAchievementData.cs`, `LogrosController.cs`.
- **Catalog:** 28 achievements in 6 categories, defined in `AchievementCatalog.All` (`new GmAchievementDefinition(...)`).
- **Persistence:** `gm_achievements` (`UNIQUE(manager_id, type)`, `INSERT OR IGNORE` idempotent); `_pendingToasts` queue consumed by `DashboardController.Update`.
- **Hooks:** `EvaluateGameDay`, `EvaluateSeasonEnd`, `EvaluateSignStarFA`/`EvaluateSignAndTrade`/`EvaluateTradeStar` (Market), `EvaluateRecordBreak` (inside `CheckAndUpdateRecords`), `BackfillCareer` (silent on Logros open).
- **UI:** Logros screen (tabs + grid + counter); button+counter in Manager; toast on Dashboard; `SubmenuLogros` in sidebar.

## S18. Advanced analytics & fog of war

- **AdvancedStatsHelper:** eFG%, TS%, PER (simple per-48), `CalcEff`. Consumers: `StatsController`, `PlayerProfileController`.
- **FogOfWarHelper:** hides OVR (band), role, and attributes for un-scouted players; scouted ids set by completed scouts (`CarteraController`); consumers: `CarteraController`, `PlayerProfileController`.
- **MatchupPreview:** forecast ratings + win probability + favorites + stars; consumer: `MatchDayController`.

## S19. IR / two-way / G-League (Propuesta D + liga completa)

- **Files:** `GLeagueHelper.cs` (reglas puras), `GLeagueScheduleGenerator.cs`, `GLeaguePostSeason.cs`, `GLeagueStandings.cs` (puros), `GLeagueSeeder.cs`, `TradeHelper.cs` (`TWO_WAY_SALARY`, `MAX_TWO_WAY`, `IR_MIN_DAYS`, `IsEligibleForTwoWay`, `IsEligibleForIR`), `InjuredController.cs` (IR), `MarketController.cs`/`DashboardController.cs` (two-way), `RosterController.cs`/`QuintetoController.cs`/`GLeagueController.cs` (UI), `DatabaseManager.Players.cs` (`GetRosterCount`, `GetTwoWayCount`, `SetOnIR`, `GetGLeagueStats`, `GetTeamGLeagueStats`), `DatabaseManager.GLeague.cs` (CRUD filiales/prospectos/partidos/stats/campeones).
- **IR:** `is_on_ir` (no cuenta en tope); botón en Lesionados; recuperación en pre-lote diario con liberación IA / modal plantilla llena para el usuario; FastSim pausa.
- **Two-way:** sub-tipo de contrato (`is_two_way`), salario fijo, máx 2/equipo, edad ≤23; toggle en oferta FA, firma IA y rookies 2ª ronda; `ProcessMaturedOffers` salta checks de cap.
- **G-League (asignaciones):** `g_league_assigned`; desarrollo +1 atributo/7 días cap `potential` (`ProcessGLeagueDevelopment` en el paso 2 del día); exclusión de NBA en `GetActivePlayers`, `QuintetoController`, `MatchDayController` y All-Star.
- **G-League (liga completa):** 30 filiales (`gleague_teams`) con 11 prospectos cada una (`gleague_players`); calendario de 28 partidos/filial solo en días NBA nov→mar (`GLeagueScheduleGenerator`); partidos simulados diarios vía `ProcessGLeagueGame` con `SimulateGame(persistToDb:false)` — sin player_game_stats/records/fatiga/lesiones; stats acumuladas por partido en `gleague_season_stats`; playoffs eliminatorios QF→SF→CF→Final (`GLeaguePostSeason`) con campeón en `gleague_champions`; UI de 4 pestañas en la pantalla GLeague. Detalle completo y fórmulas: GAMEPLAY §16.
- **Invariante clave:** los GameData GL guardan ids de filial codificados `+GAME_TEAM_ID_OFFSET (1000)`; descodificar con `DecodeGlTeamId`. Los prospectos usan ids de simulación `+PROSPECT_ID_OFFSET (500000)`.
- **Invitante:** `GetRosterCount` (excluye IR) sustituyó a los contajes de `GetPlayersByTeam().Count` en los sitios de tope de plantilla (Market, Dashboard, NewSeason, Records).

---

## Open questions

- `GetBestPerPosition` returns only the FIRST quintet — a second team was intended? [H]
- `MatchupPreview.RecentFormBonus` uses the manager's season context for both teams [D].
- Whether `league_settings.apron/repeater_apron` (still seeded) should be the single source vs `TradeHelper` constants — most UI uses `TradeHelper`. [D]
