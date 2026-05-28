using SQLite;

[Table("season_records")]
public class SeasonRecord
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int season_id { get; set; }

    // Champions
    public int champion_id { get; set; }
    public int finalist_id { get; set; }
    public string finals_result { get; set; }
    public int east_champion_id { get; set; }
    public int west_champion_id { get; set; }
    public int div1_champion_id { get; set; }
    public int div2_champion_id { get; set; }
    public int div3_champion_id { get; set; }
    public int div4_champion_id { get; set; }
    public int div5_champion_id { get; set; }
    public int div6_champion_id { get; set; }

    // Individual awards
    public int finals_mvp_id { get; set; }
    public float finals_mvp_rating { get; set; }
    public int season_mvp_id { get; set; }
    public float season_mvp_rating { get; set; }
    public int season_mvp_games { get; set; }
    public int rookie_of_year_id { get; set; }
    public float rookie_rating { get; set; }
    public int rookie_games { get; set; }
    public int best_defender_id { get; set; }
    public int sixth_man_id { get; set; }
    public int most_improved_id { get; set; }

    // All-Star
    public int all_star_pg_id { get; set; }
    public int all_star_sg_id { get; set; }
    public int all_star_sf_id { get; set; }
    public int all_star_pf_id { get; set; }
    public int all_star_c_id { get; set; }

    // First Team
    public int first_team_pg { get; set; }
    public int first_team_sg { get; set; }
    public int first_team_sf { get; set; }
    public int first_team_pf { get; set; }
    public int first_team_c { get; set; }

    // Second Team
    public int second_team_pg { get; set; }
    public int second_team_sg { get; set; }
    public int second_team_sf { get; set; }
    public int second_team_pf { get; set; }
    public int second_team_c { get; set; }
}
