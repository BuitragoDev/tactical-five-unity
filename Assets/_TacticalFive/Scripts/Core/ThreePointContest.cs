using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Resultado de un participante en el concurso de triples.</summary>
public class ThreePointResult
{
    public int player_id;
    public string player_name;
    public string team_logo;
    public List<int> roundScores = new();   // puntos de cada ronda (máx 33)
    public int bestScore;
    public int rank;
    public bool champion;
}

/// <summary>
/// Concurso de triples (E5). Puro: 5 carros de 5 balones (4 normales ×1 + 1 de dinero ×2)
/// + 2 tiros de media pista de 3 puntos = 33 máximos. Rondas con eliminación y final.
/// La conversión depende del atributo de tiro de tres y el azar.
/// </summary>
public static class ThreePointContest
{
    public const int FIELD_SIZE = 8;
    public const int RACKS = 5;
    public const int BALLS_PER_RACK = 5;      // 4 normales + 1 de dinero (el último, x2)
    public const int MONEY_BALL_INDEX = 4;
    public const int MAX_BASE = RACKS * (4 * 1 + 1 * 2);   // 30
    public const int HALF_COURT_SHOTS = 2;
    public const int HALF_COURT_POINTS = 3;
    public const int MAX_SCORE = MAX_BASE + HALF_COURT_SHOTS * HALF_COURT_POINTS; // 36

    /// <summary>Probabilidad de encestar un tiro según el atributo y la distancia. `roll` ∈ [0,1).</summary>
    public static bool ShotMade(int threePoint, bool halfCourt, double roll)
    {
        float baseChance = 0.35f + Math.Clamp(threePoint, 0, 99) / 100f * 0.55f;  // 0.35 - 0.90
        if (halfCourt) baseChance *= 0.35f;                                       // media pista: muy difícil
        return roll < baseChance;
    }

    /// <summary>Puntos de una ronda completa. El último balón de cada carro vale 2 (money ball).</summary>
    public static int RunRound(int threePoint, Random rng)
    {
        int points = 0;
        for (int rack = 0; rack < RACKS; rack++)
        {
            for (int ball = 0; ball < BALLS_PER_RACK; ball++)
            {
                if (!ShotMade(threePoint, false, rng.NextDouble())) continue;
                points += ball == MONEY_BALL_INDEX ? 2 : 1;
            }
        }
        for (int s = 0; s < HALF_COURT_SHOTS; s++)
            if (ShotMade(threePoint, true, rng.NextDouble())) points += HALF_COURT_POINTS;
        return points;
    }

    /// <summary>
    /// Corre el concurso: ronda clasificatoria con todos y final a 3. El mejor de cada
    /// ronda puntúa su máximo; gana el de mayor puntuación en la final.
    /// </summary>
    public static List<ThreePointResult> Run(IEnumerable<(int player_id, string name, string logo, int threePoint)> field,
                                            Random rng, int qualifiers = 3)
    {
        var participants = field.Select(f => new ThreePointResult
        {
            player_id = f.player_id,
            player_name = f.name,
            team_logo = f.logo
        }).ToList();
        var attributes = field.ToDictionary(f => f.player_id, f => f.threePoint);

        // Ronda clasificatoria
        foreach (var p in participants)
        {
            int score = RunRound(attributes[p.player_id], rng);
            p.roundScores.Add(score);
            p.bestScore = score;
        }

        var finalists = participants.OrderByDescending(p => p.bestScore).Take(qualifiers).ToList();

        // Final
        foreach (var p in finalists)
        {
            int score = RunRound(attributes[p.player_id], rng);
            p.roundScores.Add(score);
            if (score > p.bestScore) p.bestScore = score;
        }

        var ranked = participants.OrderByDescending(p => p.bestScore).ToList();
        for (int i = 0; i < ranked.Count; i++) ranked[i].rank = i + 1;
        if (ranked.Count > 0) ranked[0].champion = true;
        return ranked;
    }
}
