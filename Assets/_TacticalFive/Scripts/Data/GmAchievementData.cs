using SQLite;

[Table("gm_achievements")]
public class GmAchievementData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    [Indexed(Name = "IX_Achievements_Manager_Type", Order = 1, Unique = true)]
    public int manager_id { get; set; }
    [Indexed(Name = "IX_Achievements_Manager_Type", Order = 2, Unique = true)]
    public string type { get; set; }
    public int? season_id { get; set; }
    public string season_label { get; set; }
    public string unlocked_at { get; set; }
}