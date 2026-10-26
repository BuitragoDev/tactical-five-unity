using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Camp de desarrollo post-draft (F5-05). Tres partidos de verano para rookies y
/// sophomores; el jugador decide quién va convocado. El efecto es acotado: como
/// máximo +1 en un atributo y nunca supera el potencial.
/// </summary>
public static class SummerLeagueHelper
{
    public const int Games = 3;
    public const int MaxRoster = 12;
    public const int DevelopmentMinutes = 45;

    public static bool IsEligible(PlayerData p)
    {
        if (p == null || p.injury_days > 0) return false;
        return p.is_rookie == 1 || (p.age <= 22 && p.years_of_service <= 2);
    }

    public static List<PlayerData> DefaultSelection(IEnumerable<PlayerData> players)
        => (players ?? Enumerable.Empty<PlayerData>())
            .Where(IsEligible)
            .OrderByDescending(p => p.potential)
            .ThenByDescending(p => p.GetCalculatedAverage())
            .Take(MaxRoster)
            .ToList();

    public static List<SummerLeagueStatData> Simulate(IReadOnlyList<PlayerData> selected, int seed)
    {
        var result = new List<SummerLeagueStatData>();
        if (selected == null || selected.Count == 0) return result;
        var rng = new System.Random(seed);

        foreach (var p in selected)
        {
            int minutes = Games * (20 + rng.Next(0, 8));
            double factor = minutes / 72.0; // 3 partidos × 24 min
            double noise = 0.85 + rng.NextDouble() * 0.30;

            int points = (int)Math.Round(
                ((p.shooting * 0.35 + p.three_point * 0.15 + p.athleticism * 0.10
                  + p.GetCalculatedAverage() * 0.40) / 10.0) * factor * noise * Games);
            int rebounds = (int)Math.Round(
                ((p.rebounding * 0.70 + p.athleticism * 0.30) / 20.0) * factor * noise * Games);
            int assists = (int)Math.Round(
                ((p.passing * 0.70 + p.iq * 0.30) / 20.0) * factor * noise * Games);

            result.Add(new SummerLeagueStatData
            {
                player_id = p.id,
                games = Games,
                minutes = minutes,
                points = Math.Max(0, points),
                rebounds = Math.Max(0, rebounds),
                assists = Math.Max(0, assists),
                development_gain = minutes >= DevelopmentMinutes && p.overall < p.potential ? 1 : 0,
                created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
        return result;
    }

    /// <summary>
    /// Aplica el desarrollo acotado: +1 al atributo más bajo (el que más margen
    /// tiene) y recalcula el overall. Nunca sube por encima de 99 ni del potencial.
    /// </summary>
    public static bool ApplyDevelopment(PlayerData p, int gain)
    {
        if (p == null || gain <= 0 || p.overall >= p.potential) return false;

        var attributes = new (int value, Action<int> set)[]
        {
            (p.speed, v => p.speed = v),
            (p.shooting, v => p.shooting = v),
            (p.three_point, v => p.three_point = v),
            (p.passing, v => p.passing = v),
            (p.dribbling, v => p.dribbling = v),
            (p.defense, v => p.defense = v),
            (p.rebounding, v => p.rebounding = v),
            (p.athleticism, v => p.athleticism = v),
            (p.iq, v => p.iq = v),
            (p.steals, v => p.steals = v),
            (p.blocks, v => p.blocks = v),
        };

        int index = 0;
        for (int i = 1; i < attributes.Length; i++)
            if (attributes[i].value < attributes[index].value) index = i;

        attributes[index].set(Math.Min(99, attributes[index].value + Math.Min(gain, 1)));
        p.RecalculateOverall();
        return true;
    }
}
