using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SQLite;

/// <summary>
/// F8-05: los índices compuestos deben cubrir las consultas del día y de
/// standings sobre una base con forma de 10 temporadas.
/// </summary>
public class DatabaseIndexPlanTests
{
    string _path;
    SQLiteConnection _db;

    class PlanRow
    {
        public int id { get; set; }
        public int parent { get; set; }
        public int notused { get; set; }
        public string detail { get; set; }
    }

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), "tf_indexes_" + System.Guid.NewGuid().ToString("N") + ".db");
        _db = new SQLiteConnection(_path);

        _db.CreateTable<GameData>();
        _db.CreateTable<PlayerGameStats>();
        _db.CreateTable<PlayerSeasonStatRow>();
        _db.CreateTable<FinanceRecord>();
        _db.CreateTable<OfferData>();
        _db.CreateTable<TradeOfferData>();
        _db.CreateTable<DraftPickData>();
        _db.CreateTable<LineupData>();
        _db.CreateTable<SeasonRecord>();
        _db.CreateTable<MonthlyAwardData>();
        _db.CreateTable<TeamCapHoldData>();

        foreach (string sql in DatabaseManager.QueryIndexDefinitions)
            _db.Execute(sql);

        SeedTenSeasonShape();
    }

    [TearDown]
    public void TearDown()
    {
        try { _db?.Close(); } catch { }
        _db = null;
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }

    [Test]
    public void StandingsAndDayQueries_UseCompositeIndexes()
    {
        AssertPlanUsesIndex(
            "SELECT * FROM games WHERE manager_id = 1 AND season_id = 3 " +
            "AND game_type = 'regular' AND is_played = 1 ORDER BY game_day, id",
            "IX_Games_Manager_Season_Type_Played_Day");

        AssertPlanUsesIndex(
            "SELECT * FROM games WHERE manager_id = 1 AND game_day = 25 AND is_played = 0",
            "IX_Games_Manager_Day_Played");
    }

    [Test]
    public void PlayerAggregatesAndTeamStats_UseCompositeIndexes()
    {
        AssertPlanUsesIndex(
            "SELECT ps.player_id, COUNT(*) FROM player_game_stats ps " +
            "JOIN games g ON ps.game_id = g.id WHERE g.manager_id = 1 AND g.season_id = 3 " +
            "AND g.game_type = 'regular' AND g.is_played = 1 GROUP BY ps.player_id",
            "IX_Games_Manager_Season_Type_Played_Day");

        AssertPlanUsesIndex(
            "SELECT * FROM player_game_stats WHERE team_id = 4 AND game_id = 10",
            "IX_PlayerGameStats_Team_Game");

        AssertPlanUsesIndex(
            "SELECT * FROM player_season_stats WHERE season_id = 3",
            "IX_PlayerSeasonStats_Season_Team");
    }

    [Test]
    public void FinanceOffersAndRolloverQueries_UseIndexes()
    {
        AssertPlanUsesIndex(
            "SELECT * FROM finance_records WHERE team_id = 4 AND season_id = 3 " +
            "AND record_type = 7 AND game_day = 31",
            "IX_FinanceRecords_Team_Season_Type_Day");

        AssertPlanUsesIndex(
            "SELECT * FROM offers WHERE manager_id = 1 AND processed = 0",
            "IX_Offers_Manager_Processed_Day");

        AssertPlanUsesIndex(
            "SELECT * FROM trade_offers WHERE manager_id = 1 AND processed = 0",
            "IX_TradeOffers_Manager_Processed");

        AssertPlanUsesIndex(
            "SELECT * FROM draft_picks WHERE current_team_id = 4 ORDER BY draft_year",
            "IX_DraftPicks_CurrentTeam_Year");
    }

    bool AssertPlanUsesIndex(string sql, string indexName)
    {
        var plan = _db.Query<PlanRow>("EXPLAIN QUERY PLAN " + sql);
        Assert.That(plan.Any(p => !string.IsNullOrEmpty(p.detail) && p.detail.Contains(indexName)), Is.True,
            $"La consulta no usa {indexName}:\n" + string.Join("\n", plan.Select(p => p.detail)));
        return true;
    }

    void SeedTenSeasonShape()
    {
        var games = new List<GameData>();
        var stats = new List<PlayerGameStats>();
        var seasonStats = new List<PlayerSeasonStatRow>();
        var finances = new List<FinanceRecord>();

        int gameId = 0;
        for (int season = 1; season <= 10; season++)
        {
            for (int day = 1; day <= 123; day++)
            {
                for (int gameOfDay = 0; gameOfDay < 10; gameOfDay++)
                {
                    gameId++;
                    games.Add(new GameData
                    {
                        manager_id = 1,
                        season_id = season,
                        game_day = day,
                        home_team_id = (gameOfDay % 15) + 1,
                        away_team_id = 15 + ((gameOfDay + day) % 15) + 1,
                        game_type = "regular",
                        is_played = 1
                    });

                    stats.Add(new PlayerGameStats
                    {
                        game_id = gameId,
                        player_id = (gameOfDay % 15) + 1,
                        team_id = (gameOfDay % 30) + 1,
                        minutes = 30,
                        points = 20,
                        rebounds = 8,
                        assists = 5,
                        rating = 20
                    });
                }
            }

            for (int team = 1; team <= 30; team++)
            {
                for (int player = 1; player <= 15; player++)
                {
                    seasonStats.Add(new PlayerSeasonStatRow
                    {
                        player_id = (team - 1) * 15 + player,
                        season_id = season,
                        team_id = team,
                        games = 82,
                        total_points = 1_500,
                        total_rebounds = 500,
                        total_assists = 300
                    });
                }

                for (int pay = 1; pay <= 12; pay++)
                {
                    finances.Add(new FinanceRecord
                    {
                        team_id = team,
                        season_id = season,
                        record_type = FinanceRecord.TYPE_SALARIES,
                        game_day = pay * 15,
                        amount = 1_000_000
                    });
                }
            }
        }

        _db.InsertAll(games);
        _db.InsertAll(stats);
        _db.InsertAll(seasonStats);
        _db.InsertAll(finances);

        _db.InsertAll(new List<OfferData>
        {
            new OfferData { manager_id = 1, player_id = 1, processed = 0, day_sent = 1 },
            new OfferData { manager_id = 1, player_id = 2, processed = 1, day_sent = 1 }
        });
        _db.InsertAll(new List<TradeOfferData>
        {
            new TradeOfferData { manager_id = 1, processed = 0, team_id_from = 2 },
            new TradeOfferData { manager_id = 1, processed = 1, team_id_from = 3 }
        });
        _db.InsertAll(new List<DraftPickData>
        {
            new DraftPickData { current_team_id = 4, draft_year = 2027, round = 1, pick_number = 1 },
            new DraftPickData { current_team_id = 4, draft_year = 2028, round = 2, pick_number = 45 }
        });
    }
}
