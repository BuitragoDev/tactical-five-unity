using SQLite;

[Table("players")]
public class PlayerData
{
    [PrimaryKey]
    public int id { get; set; }
    public int team_id { get; set; } // 0 = agente libre
    public string first_name { get; set; }
    public string last_name { get; set; }
    public string position { get; set; }
    public string secondary_position { get; set; } = "";
    public int age { get; set; }
    public string nationality { get; set; }
    public int height_cm { get; set; }
    public int weight_kg { get; set; }
    public int overall { get; set; }
    public int potential { get; set; }
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
    public long salary { get; set; }
    public int contract_years { get; set; }
    public int is_rookie { get; set; }
    public int injury_days { get; set; }
    public string injury_type { get; set; }
    public int treated { get; set; }
    public int renewal_cooldown_day { get; set; }
    public int seasons_with_team { get; set; } = 1;
    public int morale { get; set; } = 50;
    public int fisico { get; set; } = 99;
    public string photo { get; set; }

    public int GetCalculatedAverage()
    {
        return (speed + shooting + three_point + passing + dribbling + defense + rebounding + athleticism + iq + steals + blocks) / 11;
    }
}