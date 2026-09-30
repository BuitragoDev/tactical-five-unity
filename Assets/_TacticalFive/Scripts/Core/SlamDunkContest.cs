using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Un mate ejecutado en el concurso, con su dificultad y puntuación obtenida.</summary>
public class DunkAttempt
{
    public string name;
    public int baseScore;      // puntos máximos que reparte la mesa si sale perfecto
    public bool made;
    public int points;         // 0-50 según los jueces
}

/// <summary>Resultado de un participante en el concurso de mates.</summary>
public class DunkResult
{
    public int player_id;
    public string player_name;
    public string team_logo;
    public List<int> roundScores = new();   // mejor intento de cada ronda
    public int bestScore;
    public int rank;
}

/// <summary>
/// Concurso de mates (E5). Puro: simulación con puntuación de jueces (0-50). El resultado
/// depende del atletismo/velocidad/altura del jugador, la dificultad del mate y el azar.
/// </summary>
public static class SlamDunkContest
{
    public const int FIELD_SIZE = 8;
    public const int MAX_SCORE = 50;
    public const int ATTEMPTS_PER_ROUND = 3;

    // Elenco de mates: dificultad alta = más puntos base si entra, pero más riesgo de fallar.
    public static readonly (string name, int baseScore, int difficulty)[] Dunks =
    {
        ("Mate sencillo",            30, 1),
        ("Doble bombeo",             36, 2),
        ("Molino (360° parcial)",    40, 3),
        ("Tomahawk",                 42, 3),
        ("Mano cambiada en el aire", 45, 4),
        ("360° completo",            47, 4),
        ("Entre las piernas",        48, 5),
        ("Salto sobre una torre",    50, 5),
        ("Libre de espaldas",        54, 6),   // 54: se recorta a 50 con la mesa
    };

    /// <summary>
    /// Un intento: `skill` 0-1 (atletismo/velocidad/altura) contra la dificultad del mate.
    /// La probabilidad de éxito baja con la dificultad; el fallo da entre 0 y la mitad.
    /// `roll` y `judgeRoll` ∈ [0,1).
    /// </summary>
    public static (bool made, int points) Attempt(int difficulty, int baseScore, double skill,
                                                  double roll, double judgeRoll)
    {
        float chance = Math.Clamp((float)skill + 0.45f - difficulty * 0.09f, 0.12f, 0.95f);
        bool made = roll < chance;

        if (!made)
            return (false, (int)Math.Round(baseScore * (float)judgeRoll * 0.5f));

        // Jueces: base + ajuste por ejecución impecable, capado a 50.
        int points = baseScore - 2 + (int)Math.Round((float)judgeRoll * 12f);
        return (true, Math.Min(MAX_SCORE, Math.Max(0, points)));
    }

    /// <summary>Habilidad del concursante (0-1) a partir de sus atributos.</summary>
    public static float Skill(int athleticism, int speed, int heightCm)
        => Math.Clamp((athleticism * 0.55f + speed * 0.30f + Math.Clamp(heightCm - 190, 0, 25) * 0.60f) / 100f, 0.15f, 1f);

    /// <summary>
    /// Corre el concurso: cada participante hace `rounds` rondas con varios intentos y se
    /// queda con el mejor de cada una. Devuelve el ranking (más puntuación primero).
    /// </summary>
    public static List<DunkResult> Run(IEnumerable<(int player_id, string name, string logo, float skill)> field,
                                       Random rng, int rounds = 2)
    {
        var results = new List<DunkResult>();
        foreach (var (id, name, logo, skill) in field)
        {
            var res = new DunkResult { player_id = id, player_name = name, team_logo = logo };
            for (int r = 0; r < rounds; r++)
            {
                int best = 0;
                for (int a = 0; a < ATTEMPTS_PER_ROUND; a++)
                {
                    // Los mates más difíciles aumentan el techo pero también el riesgo.
                    var dunk = Dunks[rng.Next(Dunks.Length)];
                    var (made, pts) = Attempt(dunk.difficulty, dunk.baseScore, skill, rng.NextDouble(), rng.NextDouble());
                    if (made && pts > best) best = pts;
                }
                res.roundScores.Add(best);
            }
            res.bestScore = res.roundScores.Count > 0 ? res.roundScores.Max() : 0;
            results.Add(res);
        }

        return results
            .OrderByDescending(r => r.bestScore)
            .Select((r, i) => { r.rank = i + 1; return r; })
            .ToList();
    }
}
