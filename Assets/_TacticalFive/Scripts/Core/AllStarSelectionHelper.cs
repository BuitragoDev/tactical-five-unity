using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Un candidato al All-Star con su puntuación de selección.</summary>
public class AllStarCandidate
{
    public int player_id;
    public string player_name;
    public string position;
    public int team_id;
    public string team_logo;
    public int overall;
    public float avg_rating;   // rating medio por partido (regular)
    public int games;
    public float score;        // AllStarScore
}

/// <summary>
/// Selección del All-Star (E5). Puro: recibe jugadores y estadísticas, no toca DB.
/// Formato real: 12 por conferencia = 5 titulares + 7 suplentes, respetando posición
/// (2 bases, 2 escoltas, 2 aleros, 2 ala-pívots, 2 pívots titulares+suplentes y 2 wildcards).
/// </summary>
public static class AllStarSelectionHelper
{
    public const int ROSTER_SIZE = 12;
    public const int STARTERS = 5;
    public const int RESERVES = 7;
    public const int MIN_GAMES = 20;   // partidos mínimos para contar el rating con peso pleno

    /// <summary>Puntuación de selección: rating medio ponderado + media + volumen de partidos.</summary>
    public static float AllStarScore(double avgRating, int games, int overall)
    {
        // Un jugador sin partidos suficientes ve reducida su puntuación (no se premia el ruido).
        float volume = Math.Min(1f, games / (float)MIN_GAMES);
        return (float)(avgRating * 0.70) + overall * 0.30f * volume + volume * 8f;
    }

    /// <summary>
    /// Ordena candidatos de una conferencia por puntuación descendente. `statsByPlayer` da
    /// (rating medio, partidos) por jugador; los que no aparecen usan solo el overall.
    /// </summary>
    public static List<AllStarCandidate> Rank(IEnumerable<PlayerData> conferencePlayers,
                                              Func<PlayerData, (double avgRating, int games)> stats)
    {
        var list = new List<AllStarCandidate>();
        foreach (var p in conferencePlayers)
        {
            if (p == null || p.team_id <= 0) continue;
            var (avgRating, games) = stats != null ? stats(p) : (0, 0);
            list.Add(new AllStarCandidate
            {
                player_id = p.id,
                player_name = $"{p.first_name} {p.last_name}",
                position = p.position,
                team_id = p.team_id,
                overall = p.overall,
                avg_rating = (float)avgRating,
                games = games,
                score = AllStarScore(avgRating, games, p.overall)
            });
        }
        return list.OrderByDescending(c => c.score).ThenByDescending(c => c.overall).ToList();
    }

    /// <summary>
    /// Compone la convocatoria de una conferencia: 2 por posición (los 5 primeros por
    /// posición son titulares de su puesto) y 2 wildcards. El orden de salida es:
    /// 5 titulares reales (1 por puesto) + 7 suplentes.
    /// </summary>
    public static List<AllStarCandidate> Select(List<AllStarCandidate> ranked)
    {
        if (ranked == null || ranked.Count == 0) return new List<AllStarCandidate>();

        var pool = ranked.OrderByDescending(c => c.score).ThenByDescending(c => c.overall).ToList();
        var picked = new List<AllStarCandidate>();
        var used = new HashSet<int>();

        // 2 por posición (el 1º de cada puesto será titular)
        foreach (var pos in PositionCodes.Order)
        {
            var atPos = pool.Where(c => c.position == pos && !used.Contains(c.player_id)).Take(2).ToList();
            foreach (var c in atPos) { picked.Add(c); used.Add(c.player_id); }
        }

        // Wildcards hasta 12: primero completan las posiciones con menos de 2, y solo si
        // aún faltan plazas repiten posición (respetando el reparto 2 por puesto siempre que
        // haya candidatos suficientes).
        var capacity = new Dictionary<string, int>();
        foreach (var pos in PositionCodes.Order)
            capacity[pos] = picked.Count(c => c.position == pos);

        // Pase 1: rellenar posiciones por debajo de 2.
        foreach (var c in pool)
        {
            if (picked.Count >= ROSTER_SIZE) break;
            if (used.Contains(c.player_id)) continue;
            if (capacity.TryGetValue(c.position, out var n) && n >= 2) continue;
            picked.Add(c); used.Add(c.player_id);
            if (capacity.ContainsKey(c.position)) capacity[c.position]++;
        }

        // Pase 2: si aún faltan plazas, los mejores disponibles (pueden repetir posición).
        foreach (var c in pool)
        {
            if (picked.Count >= ROSTER_SIZE) break;
            if (used.Contains(c.player_id)) continue;
            picked.Add(c); used.Add(c.player_id);
        }

        // 5 titulares: el mejor de cada posición
        var starters = new List<AllStarCandidate>();
        var reserves = new List<AllStarCandidate>();
        var starterIds = new HashSet<int>();
        foreach (var pos in PositionCodes.Order)
        {
            var best = picked.Where(c => c.position == pos && !starterIds.Contains(c.player_id))
                             .OrderByDescending(c => c.score).FirstOrDefault();
            if (best != null) { starters.Add(best); starterIds.Add(best.player_id); }
        }
        if (starters.Count < STARTERS)
        {
            foreach (var c in picked)
            {
                if (starters.Count >= STARTERS) break;
                if (starterIds.Contains(c.player_id)) continue;
                starters.Add(c); starterIds.Add(c.player_id);
            }
        }

        foreach (var c in picked)
            if (!starterIds.Contains(c.player_id)) reserves.Add(c);

        return starters.Concat(reserves).ToList();
    }

    /// <summary>Participantes (8) para un concurso, ordenados por el atributo indicado.</summary>
    public static List<AllStarCandidate> ContestField(IEnumerable<AllStarCandidate> candidates,
                                                      Func<AllStarCandidate, int> attribute, int count = 8)
    {
        return candidates
            .OrderByDescending(c => attribute(c))
            .ThenByDescending(c => c.overall)
            .Take(count)
            .ToList();
    }
}
