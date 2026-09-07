using SQLite;

[Table("season_attendance_summary")]
public class SeasonAttendanceSummaryData
{
    [PrimaryKey]
    public int season_id { get; set; }
    public int manager_id { get; set; }
    public int total_attendance { get; set; }
    public int total_capacity { get; set; }
    public float occupancy_pct { get; set; }
}
