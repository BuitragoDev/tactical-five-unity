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
    /// <summary>Duración total de la lesión actual (para el progreso del parte médico). 0 = sin lesión.</summary>
    public int injury_total_days { get; set; }
    /// <summary>0-100: riesgo de recaída tras un alta forzada (volver antes de tiempo). 0 = sin riesgo.</summary>
    public int recovery_risk { get; set; }
    public int renewal_cooldown_day { get; set; }
    public int seasons_with_team { get; set; } = 1;
    public int morale { get; set; } = 50;
    public int fisico { get; set; } = 99;
    public int on_trade_block { get; set; }
    /// <summary>Jornada en la que el jugador fue rescindido (buyout) y quedó libre; -1 = no aplica.
    /// Mientras sea ≥ 0 su precio de mercado es el mínimo (mercado de buyout).</summary>
    public int waived_game_day { get; set; } = -1;
    public int rings { get; set; }
    public int finals_mvps { get; set; }
    public int finals_played { get; set; }
    public int season_mvps { get; set; }
    public int is_on_ir { get; set; }            // 1 = en la reserva de lesionados (no cuenta en el tope de plantilla)
    public int is_two_way { get; set; }          // 1 = contrato two-way (salario reducido, máx 2/equipo)
    public int g_league_assigned { get; set; }   // 1 = asignado a la liga de desarrollo (no juega en NBA)
    public PlayerRole role { get; set; } = PlayerRole.UltimoRecurso;
    public string photo { get; set; }
    public int number { get; set; } // dorsal

    // Minutos configurados en la pantalla ROTACIÓN. Transitorio: no se persiste en
    // la tabla `players` (vive en `team_lineup.rotation_minutes`); se rellena al
    // construir la convocatoria para que el simulador los use como objetivo.
    // -1 = automático (heurística del simulador).
    [Ignore]
    public int rotation_minutes { get; set; } = -1;

    /// <summary>
    /// Media de los 11 atributos (redondeada) capada por el potencial. Delega en
    /// <see cref="PlayerRating"/>, que es la fuente única de la fórmula; no la dupliques.
    /// </summary>
    public int GetCalculatedAverage()
    {
        return PlayerRating.FromAttributes(speed, shooting, three_point, passing, dribbling, defense,
                                           rebounding, athleticism, iq, steals, blocks, potential);
    }

    /// <summary>Recalcula y fija `overall` desde los atributos. Llámalo SIEMPRE que
    /// mutes un atributo (training, mentoring, progresión, desarrollo G-League...).</summary>
    public void RecalculateOverall() => overall = GetCalculatedAverage();
}