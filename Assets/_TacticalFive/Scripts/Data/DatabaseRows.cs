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
}

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
}