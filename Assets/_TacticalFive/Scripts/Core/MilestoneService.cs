using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Detección diaria de hitos de carrera (E1). La carrera de cada jugador es histórico
/// (`historical_player_stats`, por `player_id`) + temporada en curso + honores, todo desde
/// `DatabaseManager.BuildCareerTotals`.
///
/// La **primera** vez por partida se hace una línea base silenciosa
/// (`EnsureMilestonesBaselined`): los hitos que un jugador ya cumple (p. ej. las leyendas del
/// seed con 40.000 puntos) se registran sin noticia, para no soltar un aluvión el día 1.
/// A partir de ahí, cada hito nuevo se registra una sola vez y genera una noticia agrupada
/// por jugador; el motor de narrativa los lee de <see cref="TodayByPlayer"/> para el hilo de
/// "hito alcanzado" del equipo del usuario.
/// </summary>
public static class MilestoneService
{
    /// <summary>Hitos alcanzados HOY por jugador (jugadores del equipo del usuario y del resto).</summary>
    public static Dictionary<int, List<MilestoneDef>> TodayByPlayer { get; private set; }
        = new Dictionary<int, List<MilestoneDef>>();

    /// <summary>Evalúa los hitos del día. Llamar tras simular los partidos del día.</summary>
    public static void EvaluateDay(ManagerData manager, TeamData myTeam, SeasonData season,
                                   int gameDay, string gameDate)
    {
        TodayByPlayer = new Dictionary<int, List<MilestoneDef>>();
        if (manager == null || season == null) return;

        var db = DatabaseManager.Instance;

        // Primera vez en la partida: registrar lo ya alcanzado sin noticias.
        if (db.EnsureMilestonesBaselined(manager.id, season.id)) return;

        var careers = db.BuildCareerTotals(manager.id, season.id);
        if (careers.Count == 0) return;

        var registered = db.GetMilestoneKeysByPlayer();
        var today = new Dictionary<int, List<MilestoneDef>>();
        var newRows = new List<MilestoneData>();
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        foreach (var kv in careers)
        {
            registered.TryGetValue(kv.Key, out var keys);
            var fresh = MilestoneHelper.Reached(kv.Value, keys);
            if (fresh.Count == 0) continue;

            var player = db.GetPlayerById(kv.Key);
            if (player == null) continue;

            today[kv.Key] = fresh;
            foreach (var d in fresh)
            {
                newRows.Add(new MilestoneData
                {
                    player_id = kv.Key,
                    milestone = d.key,
                    season_id = season.id,
                    game_day = gameDay,
                    team_id = player.team_id,
                    created_at = now
                });
            }
        }

        if (newRows.Count == 0) return;

        db.InsertMilestones(newRows);
        TodayByPlayer = today;

        // Una noticia por jugador (agrupando sus hitos del día).
        foreach (var kv in today)
        {
            var player = db.GetPlayerById(kv.Key);
            if (player == null) continue;

            string name = $"{player.first_name} {player.last_name}";
            bool major = kv.Value.Any(d => d.major);
            db.AddMessage(new MessageData
            {
                manager_id = manager.id,
                sender_type = 2,
                sender_id = 0,
                title = $"Hito histórico: {name}",
                body = NewsTexts.Hito(name, MilestoneHelper.Summarize(kv.Value), major),
                game_day = gameDay,
                game_date = gameDate,
                created_at = now,
                date_sent = now,
                is_read = 0,
                category = NewsCategory.LIGA,
                news_type = NewsType.LEGADO
            });
        }

        Debug.Log($"[Milestones] {today.Count} jugador(es) con hitos nuevos ({newRows.Count} hitos).");
    }
}
