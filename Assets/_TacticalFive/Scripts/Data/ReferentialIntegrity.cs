using System.Collections.Generic;
using System.Linq;
using SQLite;

/// <summary>
/// Integridad referencial equivalente para el esquema SQLite (F8-08).
/// SQLite-net no crea FOREIGN KEY en las tablas existentes, así que las relaciones
/// se declaran aquí y se validan con LEFT JOIN; las tablas nuevas deben añadirse
/// a esta lista.
/// </summary>
public static class ReferentialIntegrity
{
    public sealed class Rule
    {
        public string childTable;
        public string childColumn;
        public string parentTable;
        public string parentColumn;
        public bool allowZero;
        public bool allowNegative;
        public string childWhere;
    }

    public sealed class Orphan
    {
        public string childTable;
        public string childColumn;
        public string parentTable;
        public int count;

        public override string ToString()
            => $"{childTable}.{childColumn} → {parentTable}: {count} huérfano(s)";
    }

    public static readonly Rule[] Rules =
    {
        R("players", "team_id", "teams", "id", allowZero: true),
        R("players", "last_team_id", "teams", "id", allowZero: true),
        R("games", "season_id", "seasons", "id"),
        R("games", "manager_id", "managers", "id"),
        R("games", "home_team_id", "teams", "id", childWhere: "c.game_type NOT IN ('gleague','gleague_playoff','allstar')"),
        R("games", "away_team_id", "teams", "id", childWhere: "c.game_type NOT IN ('gleague','gleague_playoff','allstar')"),
        R("player_game_stats", "game_id", "games", "id"),
        R("player_game_stats", "player_id", "players", "id"),
        R("player_game_stats", "team_id", "teams", "id", allowZero: true),
        R("player_season_stats", "player_id", "players", "id"),
        R("player_season_stats", "season_id", "seasons", "id"),
        R("player_season_stats", "team_id", "teams", "id", allowZero: true),
        R("historical_player_stats", "player_id", "players", "id", allowNegative: true, allowZero: true),
        R("finance_records", "team_id", "teams", "id"),
        R("finance_records", "season_id", "seasons", "id"),
        R("offers", "manager_id", "managers", "id"),
        R("offers", "player_id", "players", "id"),
        R("trade_offers", "manager_id", "managers", "id"),
        R("trade_offers", "team_id_from", "teams", "id"),
        R("trades", "team_id_from", "teams", "id", allowZero: true),
        R("trades", "team_id_to", "teams", "id", allowZero: true),
        R("trades", "player_id", "players", "id", allowZero: true),
        R("draft_picks", "current_team_id", "teams", "id", allowZero: true),
        R("draft_picks", "original_team_id", "teams", "id", allowZero: true),
        R("draft_picks", "swap_original_team_id", "teams", "id", allowZero: true),
        R("team_lineup", "team_id", "teams", "id"),
        R("team_lineup", "player_id", "players", "id"),
        R("team_cap_holds", "team_id", "teams", "id"),
        R("team_cap_holds", "player_id", "players", "id"),
        R("team_cap_holds", "season_id", "seasons", "id"),
        R("messages", "manager_id", "managers", "id"),
        R("season_records", "season_id", "seasons", "id"),
        R("monthly_awards", "season_id", "seasons", "id"),
        R("player_milestones", "player_id", "players", "id"),
        R("injury_history", "player_id", "players", "id"),
        R("injury_history", "team_id", "teams", "id"),
        R("all_star_roster", "player_id", "players", "id"),
        R("scouted_players", "team_id", "teams", "id"),
        R("scouted_players", "player_id", "players", "id"),
        R("city_projects", "manager_id", "managers", "id"),
        R("city_projects", "season_id", "seasons", "id"),
        R("city_projects", "team_id", "teams", "id"),
        R("city_events", "manager_id", "managers", "id"),
        R("city_events", "season_id", "seasons", "id"),
        R("tournament_editions", "manager_id", "managers", "id"),
        R("tournament_editions", "season_id", "seasons", "id"),
        R("tournament_groups", "edition_id", "tournament_editions", "id"),
        R("tournament_standings", "edition_id", "tournament_editions", "id"),
        R("gleague_players", "gleague_team_id", "gleague_teams", "id")
    };

    static Rule R(string childTable, string childColumn, string parentTable, string parentColumn,
        bool allowZero = false, bool allowNegative = false, string childWhere = null)
        => new Rule
        {
            childTable = childTable,
            childColumn = childColumn,
            parentTable = parentTable,
            parentColumn = parentColumn,
            allowZero = allowZero,
            allowNegative = allowNegative,
            childWhere = childWhere
        };

    public static List<Orphan> Validate(SQLiteConnection db)
    {
        var result = new List<Orphan>();
        if (db == null) return result;

        var tables = new HashSet<string>(db.Query<TableNameRow>(
            "SELECT name FROM sqlite_master WHERE type = 'table'").Select(t => t.name));

        foreach (var rule in Rules)
        {
            if (!tables.Contains(rule.childTable) || !tables.Contains(rule.parentTable))
                continue;

            var allowed = new List<string>();
            if (rule.allowZero) allowed.Add("0");
            if (rule.allowNegative) allowed.Add("-1");

            string sql = $@"SELECT COUNT(*)
                FROM ""{rule.childTable}"" c
                LEFT JOIN ""{rule.parentTable}"" p
                  ON c.""{rule.childColumn}"" = p.""{rule.parentColumn}""
                WHERE c.""{rule.childColumn}"" IS NOT NULL
                  AND p.""{rule.parentColumn}"" IS NULL";
            if (allowed.Count > 0)
                sql += $@" AND c.""{rule.childColumn}"" NOT IN ({string.Join(",", allowed)})";
            if (!string.IsNullOrEmpty(rule.childWhere))
                sql += $" AND ({rule.childWhere})";

            int count = db.ExecuteScalar<int>(sql);
            if (count > 0)
                result.Add(new Orphan
                {
                    childTable = rule.childTable,
                    childColumn = rule.childColumn,
                    parentTable = rule.parentTable,
                    count = count
                });
        }

        return result;
    }

    class TableNameRow
    {
        public string name { get; set; }
    }
}
