using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Datos estáticos de las 30 filiales G-League (una por franquicia NBA) y
/// generación de plantillas de prospectos jóvenes para cada filial.
/// </summary>
public static class GLeagueSeeder
{
    // El seeding puede ocurrir en el hilo principal (InitSaveSlot) o en un hilo
    // de fondo (BuildTemplateDatabaseInBackground), así que se usa System.Random
    // thread-static (nunca UnityEngine.Random, solo main-thread).
    [ThreadStatic] private static System.Random _rng;
    internal static System.Random Rng => _rng ??= new System.Random();

    // (nombre filial, abreviatura NBA matriz, conferencia, clave de logo)
    public static readonly (string name, string nbaAbbr, string conference, string logo)[] Teams =
    {
        // ── ESTE ──
        ("Maine Celtics",          "BOS", "East", "celtics_gleague"),
        ("Long Island Nets",       "BKN", "East", "nets_gleague"),
        ("Westchester Knicks",     "NYK", "East", "knicks_gleague"),
        ("Delaware Blue Coats",    "PHI", "East", "sixers_gleague"),
        ("Raptors 905",            "TOR", "East", "raptors_gleague"),
        ("Windy City Bulls",       "CHI", "East", "bulls_gleague"),
        ("Cleveland Charge",       "CLE", "East", "cavaliers_gleague"),
        ("Motor City Cruise",      "DET", "East", "pistons_gleague"),
        ("Noblesville Boom",       "IND", "East", "pacers_gleague"),
        ("Wisconsin Herd",         "MIL", "East", "bucks_gleague"),
        ("College Park Skyhawks",  "ATL", "East", "hawks_gleague"),
        ("Greensboro Swarm",       "CHA", "East", "hornets_gleague"),
        ("Sioux Falls Skyforce",   "MIA", "East", "heat_gleague"),
        ("Osceola Magic",          "ORL", "East", "magic_gleague"),
        ("Capital City Go-Go",     "WAS", "East", "wizards_gleague"),

        // ── OESTE ──
        ("Grand Rapids Gold",      "DEN", "West", "nuggets_gleague"),
        ("Iowa Wolves",            "MIN", "West", "wolves_gleague"),
        ("Oklahoma City Blue",     "OKC", "West", "thunder_gleague"),
        ("Rip City Remix",         "PRT", "West", "blazers_gleague"),
        ("Salt Lake City Stars",   "UTA", "West", "jazz_gleague"),
        ("Santa Cruz Warriors",    "GSW", "West", "warriors_gleague"),
        ("San Diego Clippers",     "LAC", "West", "clippers_gleague"),
        ("South Bay Lakers",       "LAL", "West", "lakers_gleague"),
        ("Valley Suns",            "PHX", "West", "suns_gleague"),
        ("Stockton Kings",         "SAC", "West", "kings_gleague"),
        ("Texas Legends",          "DAL", "West", "mavericks_gleague"),
        ("Rio Grande Valley Vipers","HOU", "West", "rockets_gleague"),
        ("Memphis Hustle",         "MEM", "West", "grizzlies_gleague"),
        ("Birmingham Squadron",    "NOP", "West", "pelicans_gleague"),
        ("Austin Spurs",           "SAS", "West", "spurs_gleague"),
    };

    /// <summary>Jugadores prospecto por filial.</summary>
    public const int PLAYERS_PER_TEAM = 12;

    static readonly string[] Positions = { "PG", "SG", "SF", "PF", "C" };

    /// <summary>
    /// Genera la plantilla de prospectos de una filial. Usa System.Random
    /// thread-static: es seguro tanto en el hilo principal (InitSaveSlot) como
    /// en un hilo de fondo (BuildTemplateDatabaseInBackground).
    /// </summary>
    public static List<GLeaguePlayerData> GenerateProspects(int gleagueTeamId)
    {
        var result = new List<GLeaguePlayerData>();

        for (int i = 0; i < PLAYERS_PER_TEAM; i++)
        {
            // Reparto de posiciones: 2 por posición + 1 aleatorio
            string position = i < 10 ? Positions[i / 2] : Positions[Rng.Next(0, 5)];

            int baseAvg = Rng.Next(45, 63);
            int potential = Mathf.Min(85, baseAvg + Rng.Next(10, 24));
            int age = Rng.Next(19, 24);

            var attrs = new Dictionary<string, int>();
            foreach (var attr in new[] { "speed", "shooting", "three_point", "passing", "dribbling",
                                         "defense", "rebounding", "athleticism", "iq", "steals", "blocks" })
            {
                int posMod = attr switch
                {
                    "passing" or "dribbling" => position == "PG" ? 4 : position == "SG" ? 1 : -3,
                    "three_point" or "shooting" => position == "SG" || position == "SF" ? 3 : -2,
                    "blocks" or "rebounding" => position == "C" ? 6 : position == "PF" ? 3 : -5,
                    "athleticism" => 0,
                    _ => 0
                };
                int value = Mathf.Clamp(baseAvg + posMod + Rng.Next(-7, 8), 30, potential);
                attrs[attr] = value;
            }

            int overall = Mathf.Min(potential, attrs["speed"] + attrs["shooting"] + attrs["three_point"]
                + attrs["passing"] + attrs["dribbling"] + attrs["defense"] + attrs["rebounding"]
                + attrs["athleticism"] + attrs["iq"] + attrs["steals"] + attrs["blocks"]) / 11;

            result.Add(new GLeaguePlayerData
            {
                gleague_team_id = gleagueTeamId,
                first_name = DraftGenerator.FirstNames[Rng.Next(0, DraftGenerator.FirstNames.Length)],
                last_name = DraftGenerator.LastNames[Rng.Next(0, DraftGenerator.LastNames.Length)],
                position = position,
                age = age,
                overall = overall,
                potential = potential,
                speed = attrs["speed"],
                shooting = attrs["shooting"],
                three_point = attrs["three_point"],
                passing = attrs["passing"],
                dribbling = attrs["dribbling"],
                defense = attrs["defense"],
                rebounding = attrs["rebounding"],
                athleticism = attrs["athleticism"],
                iq = attrs["iq"],
                steals = attrs["steals"],
                blocks = attrs["blocks"],
                photo = $"Default/default{Rng.Next(1, 101)}",
            });
        }

        return result;
    }
}
