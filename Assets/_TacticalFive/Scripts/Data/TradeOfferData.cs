using SQLite;
using System.Collections.Generic;
using System.Linq;

[Table("trade_offers")]
public class TradeOfferData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int manager_id { get; set; }
    public int team_id_from { get; set; }
    public string player_ids_out { get; set; }
    public string player_ids_in { get; set; }
    public int day_sent { get; set; }
    public int processed { get; set; }
    public string pick_ids_out { get; set; } = "";
    public string pick_ids_in { get; set; } = "";

    public List<int> GetWantedPlayerIds()
    {
        if (string.IsNullOrEmpty(player_ids_out)) return new List<int>();
        return player_ids_out.Split(',').Select(int.Parse).ToList();
    }

    public List<int> GetOfferedPlayerIds()
    {
        if (string.IsNullOrEmpty(player_ids_in)) return new List<int>();
        return player_ids_in.Split(',').Select(int.Parse).ToList();
    }

    public List<int> GetWantedPickIds()
    {
        if (string.IsNullOrEmpty(pick_ids_out)) return new List<int>();
        return pick_ids_out.Split(',').Select(int.Parse).ToList();
    }

    public List<int> GetOfferedPickIds()
    {
        if (string.IsNullOrEmpty(pick_ids_in)) return new List<int>();
        return pick_ids_in.Split(',').Select(int.Parse).ToList();
    }

    public static string JoinIds(List<int> ids)
    {
        return string.Join(",", ids);
    }
}
