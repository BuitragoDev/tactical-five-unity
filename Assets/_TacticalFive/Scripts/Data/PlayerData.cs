using SQLite;

[Table("players")]
public class PlayerData
{
    [PrimaryKey]
    public int id { get; set; }
    public int team_id { get; set; } // 0 = agente libre
    public int last_team_id { get; set; } // último equipo para el que jugó (0 si nunca/FA externo); habilita Bird rights al re-firmar
    public string first_name { get; set; }
    public string last_name { get; set; }
    public string position { get; set; }
    public string secondary_position { get; set; } = "";
    public int age { get; set; }
    public string nationality { get; set; }
    public string college { get; set; }
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
    public int contract_years { get; set; }        // total de años restantes del contrato (garantizados + opción)
    public int guaranteed_years { get; set; }     // años totalmente garantizados restantes (default = contract_years)
    public int has_team_option { get; set; }      // 1 si el último año es team option (se resuelve al llegar a él)
    public int has_player_option { get; set; }    // 1 si el último año es player option (no puede coexistir con team)
    public int is_rookie { get; set; }
    public int injury_days { get; set; }
    public string injury_type { get; set; }
    public int treated { get; set; }
    public int renewal_cooldown_day { get; set; }
    public int seasons_with_team { get; set; } = 1;
    public int morale { get; set; } = 50;
    public int fisico { get; set; } = 99;
    public PlayerRole role { get; set; } = PlayerRole.UltimoRecurso;
    public string photo { get; set; }

    public int GetCalculatedAverage()
    {
        return (speed + shooting + three_point + passing + dribbling + defense + rebounding + athleticism + iq + steals + blocks) / 11;
    }
}