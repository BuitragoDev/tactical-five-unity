using SQLite;

[Table("historical_player_stats")]
public class HistoricalPlayerStatsData
{
    [PrimaryKey, AutoIncrement]
    public int    id                  { get; set; }
    public string first_name          { get; set; }
    public string last_name           { get; set; }
    public string position            { get; set; }
    public int    overall             { get; set; }
    public string team_name           { get; set; }
    public string team_abbreviation   { get; set; }
    public string team_logo           { get; set; }

    public int    games               { get; set; }
    public int    total_points        { get; set; }
    public int    total_rebounds      { get; set; }
    public int    total_assists       { get; set; }
    public int    total_steals        { get; set; }
    public int    total_blocks        { get; set; }
    public int    total_turnovers     { get; set; }
    public int    total_fgm           { get; set; }
    public int    total_fga           { get; set; }
    public int    total_fg3m          { get; set; }
    public int    total_fg3a          { get; set; }
    public int    total_ftm           { get; set; }
    public int    total_fta           { get; set; }
    public int    total_oreb          { get; set; }
    public int    total_dreb          { get; set; }
    public int    total_double_doubles { get; set; }
    public int    total_triple_doubles { get; set; }
    public int    total_minutes       { get; set; }
    public int    total_rating        { get; set; }

    public string full_name => $"{first_name} {last_name}";
    public float ppg => games > 0 ? (float)total_points / games : 0;
    public float rpg => games > 0 ? (float)total_rebounds / games : 0;
    public float apg => games > 0 ? (float)total_assists / games : 0;
    public float spg => games > 0 ? (float)total_steals / games : 0;
    public float bpg => games > 0 ? (float)total_blocks / games : 0;
    public float fg_pct => total_fga > 0 ? (float)total_fgm / total_fga * 100 : 0;
    public float fg3_pct => total_fg3a > 0 ? (float)total_fg3m / total_fg3a * 100 : 0;
    public float ft_pct => total_fta > 0 ? (float)total_ftm / total_fta * 100 : 0;
}
