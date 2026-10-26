using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;

public class PlayerSeasonStatsRow
{
    public int player_id { get; set; }
    public string first_name { get; set; }
    public string last_name { get; set; }
    public string position { get; set; }
    public int games { get; set; }
    public int total_points { get; set; }
    public int total_rebounds { get; set; }
    public int total_assists { get; set; }
    public int total_steals { get; set; }
    public int total_blocks { get; set; }
    public int total_rating { get; set; }
}

public class SeasonStatsAggregate
{
    public int player_id { get; set; }
    public int gp { get; set; }
    public int total_points { get; set; }
    public int total_rebounds { get; set; }
    public int total_assists { get; set; }
    public int total_steals { get; set; }
    public int total_blocks { get; set; }
    public int total_fgm { get; set; }
    public int total_fga { get; set; }
    public int total_fg3m { get; set; }
    public int total_fg3a { get; set; }
    public int total_ftm { get; set; }
    public int total_fta { get; set; }
    public int total_turnovers { get; set; }
    public double total_minutes { get; set; }
    public int total_rating { get; set; }
    public int total_dd { get; set; }
    public int total_td { get; set; }
}

public class PlayerStatTotalRow
{
    public int player_id { get; set; }
    public double total { get; set; }
}

public class PlayerAwardQueryRow
{
    public int id { get; set; }
    public string photo { get; set; }
    public string first_name { get; set; }
    public string last_name { get; set; }
    public string position { get; set; }
    public string secondary_position { get; set; }
    public string team_name { get; set; }
    public string team_logo { get; set; }
    public int games { get; set; }
    public double avg_pts { get; set; }
    public double avg_reb { get; set; }
    public double avg_ast { get; set; }
    public double avg_rating { get; set; }
}

public class HistoricalStatsAggregateRow
{
    public int player_id { get; set; }
    public int games { get; set; }
    public int total_points { get; set; }
    public int total_rebounds { get; set; }
    public int total_assists { get; set; }
    public int total_steals { get; set; }
    public int total_blocks { get; set; }
    public int total_turnovers { get; set; }
    public int total_fgm { get; set; }
    public int total_fga { get; set; }
    public int total_fg3m { get; set; }
    public int total_fg3a { get; set; }
    public int total_ftm { get; set; }
    public int total_fta { get; set; }
    public int total_oreb { get; set; }
    public int total_dreb { get; set; }
    public int total_double_doubles { get; set; }
    public int total_triple_doubles { get; set; }
    public int total_minutes { get; set; }
    public int total_rating { get; set; }
}

public class LeagueSeasonAggregateRow
{
    public int season_id { get; set; }
    public int year_start { get; set; }
    public int year_end { get; set; }
    public int games { get; set; }
    public double total_minutes { get; set; }
    public int total_points { get; set; }
    public int total_rebounds { get; set; }
    public int total_assists { get; set; }
    public int total_steals { get; set; }
    public int total_blocks { get; set; }
    public int total_fgm { get; set; }
    public int total_fga { get; set; }
    public int total_fg3m { get; set; }
    public int total_fg3a { get; set; }
    public int total_ftm { get; set; }
    public int total_fta { get; set; }
    public int total_turnovers { get; set; }
    public int total_oreb { get; set; }
    public int total_dreb { get; set; }
}

public class PlayerCareerSeasonRow
{
    public int season_id { get; set; }
    public int year_start { get; set; }
    public int year_end { get; set; }
    public int team_id { get; set; }
    public string team_abbreviation { get; set; }
    public string team_name { get; set; }
    public int games { get; set; }
    public double total_minutes { get; set; }
    public int total_points { get; set; }
    public int total_rebounds { get; set; }
    public int total_assists { get; set; }
    public int total_steals { get; set; }
    public int total_blocks { get; set; }
    public int total_rating { get; set; }
    // F6-01: splits de tiro, pérdidas y rebotes ofensivos/defensivos.
    public int total_fgm { get; set; }
    public int total_fga { get; set; }
    public int total_fg3m { get; set; }
    public int total_fg3a { get; set; }
    public int total_ftm { get; set; }
    public int total_fta { get; set; }
    public int total_turnovers { get; set; }
    public int total_oreb { get; set; }
    public int total_dreb { get; set; }
}

[Table("player_season_stats")]
public class PlayerSeasonStatRow
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    [Indexed]
    public int player_id { get; set; }
    [Indexed]
    public int season_id { get; set; }
    public int year_start { get; set; }
    public int year_end { get; set; }
    public int team_id { get; set; }
    public string team_abbreviation { get; set; }
    public string team_name { get; set; }
    public int games { get; set; }
    public double total_minutes { get; set; }
    public int total_points { get; set; }
    public int total_rebounds { get; set; }
    public int total_assists { get; set; }
    public int total_steals { get; set; }
    public int total_blocks { get; set; }
    public int total_rating { get; set; }
    // F6-01: splits de tiro, pérdidas y rebotes ofensivos/defensivos.
    public int total_fgm { get; set; }
    public int total_fga { get; set; }
    public int total_fg3m { get; set; }
    public int total_fg3a { get; set; }
    public int total_ftm { get; set; }
    public int total_fta { get; set; }
    public int total_turnovers { get; set; }
    public int total_oreb { get; set; }
    public int total_dreb { get; set; }
}

public class SeasonAwardRow
{
    public int season_id { get; set; }
    public int year_start { get; set; }
    public int year_end { get; set; }
    public int season_mvp_id { get; set; }
    public int rookie_of_year_id { get; set; }
    public int finals_mvp_id { get; set; }
    public int best_defender_id { get; set; }
    public int sixth_man_id { get; set; }
    public int most_improved_id { get; set; }
    public int all_star_pg_id { get; set; }
    public int all_star_sg_id { get; set; }
    public int all_star_sf_id { get; set; }
    public int all_star_pf_id { get; set; }
    public int all_star_c_id { get; set; }
    public int first_team_pg { get; set; }
    public int first_team_sg { get; set; }
    public int first_team_sf { get; set; }
    public int first_team_pf { get; set; }
    public int first_team_c { get; set; }
    public int second_team_pg { get; set; }
    public int second_team_sg { get; set; }
    public int second_team_sf { get; set; }
    public int second_team_pf { get; set; }
    public int second_team_c { get; set; }
    public int champion_id { get; set; }
}

public class PlayerAwardEntry
{
    public int season_id { get; set; }
    public int year_start { get; set; }
    public int year_end { get; set; }
    public string award_type { get; set; }
}

public class MonthlyManagerAwardRow
{
    public int team_id { get; set; }
    public string team_name { get; set; }
    public int wins { get; set; }
    public int games { get; set; }
    public int diff { get; set; }
}

public class SeasonPlayerRatingRow
{
    public int player_id { get; set; }
    public double avg_rating { get; set; }
    public int games { get; set; }
}

public class CoachMonthRow
{
    public int team_id { get; set; }
    public int mes_count { get; set; }
}

public class CoachAwardInfo
{
    public string CoachName { get; set; }
    public string TeamName { get; set; }
    public string TeamKeyword { get; set; }
    public string RecordText { get; set; }
}

public class MonthlyPlayerAwardRow
{
    public int player_id { get; set; }
    public string player_name { get; set; }
    public int team_id { get; set; }
    public string team_name { get; set; }
    public double avg_rating { get; set; }
    public int games { get; set; }
}

public class GLSeasonMVPRow
{
    public int player_id { get; set; }
    public string first_name { get; set; }
    public string last_name { get; set; }
    public string position { get; set; }
    public string team_name { get; set; }
    public string team_logo { get; set; }
    public int games { get; set; }
    public double avg_rating { get; set; }
    public double avg_pts { get; set; }
    public double avg_reb { get; set; }
    public double avg_ast { get; set; }
    public double avg_stl { get; set; }
    public double avg_blk { get; set; }
}

public class DraftProspect
{
    public string first_name { get; set; }
    public string last_name { get; set; }
    public string position { get; set; }
    public string secondary_position { get; set; }
    public int age { get; set; }
    public string nationality { get; set; }
    public string college { get; set; }
    public int height_cm { get; set; }
    public int weight_kg { get; set; }
    public int overall { get; set; }
    public int potential { get; set; }
    // Scouting: `scout_error` es la desviación (real - estimación) aplicada a los
    // valores mostrados; `scout_range` es el margen (±) para la UI.
    public int scout_error { get; set; }
    public int scout_range { get; set; }
    public int speed { get; set; }
    public int shooting { get; set; }
    public int three_point { get; set; }
    public int passing { get; set; }
    public int dribbling { get; set; }
    public int defense { get; set; }
    public int rebounding { get; set; }
    public int athleticism { get; set; }
    public int iq { get; set; }
    public int steals { get; set; }
    public int blocks { get; set; }
    public string trait { get; set; }
    public bool is_generational { get; set; }
}
/// <summary>Fila agregada: partidos jugados por jugador (una sola query).</summary>
public class PlayerGamesRow
{
    public int player_id { get; set; }
    public int games { get; set; }
}

/// <summary>Contadores de honores de un jugador (para los hitos de carrera).</summary>
public class PlayerHonorsRow
{
    public int id { get; set; }
    public int team_id { get; set; }
    public int rings { get; set; }
    public int finals_mvps { get; set; }
    public int season_mvps { get; set; }
}

/// <summary>Convocatorias All-Star de un jugador dentro de la partida.</summary>
public class AllStarSeasonCountRow
{
    public int player_id { get; set; }
    public int appearances { get; set; }
}
