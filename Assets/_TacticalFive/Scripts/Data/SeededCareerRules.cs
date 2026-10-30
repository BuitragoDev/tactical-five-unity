using System;
using System.Collections.Generic;

/// <summary>
/// Reglas puras para unificar la carrera de un jugador del seed con su fila histórica.
///
/// El seeder histórico contiene la carrera real (incluidos jugadores que siguen activos).
/// Hasta ahora esa fila quedaba con `player_id = -1` y la partida creaba además una carrera
/// sintética para el jugador real. Eso producía dos carreras distintas (una con los totales
/// reales y otra con el tope sintético) y el Salón de la Fama leía la sintética.
///
/// Estas reglas identifican una carrera sintética sin depender de nombres ni de ids:
/// `ApplyInitialCareer` fija `total_minutes = games * 36`, mientras que las carreras reales
/// del seeder usan 30 minutos por partido. Así se puede separar la base sintética de las
/// temporadas realmente jugadas y sustituirla por la carrera del seeder.
/// </summary>
public static class SeededCareerRules
{
    public const int SyntheticPointsCap = 15535;
    public const int SyntheticGamesCap = 1600;
    public const int SyntheticMinutesPerGame = 36;

    /// <summary>Clave de nombre normalizada para emparejar el seed con el histórico.</summary>
    public static string NameKey(string firstName, string lastName)
        => $"{Normalize(firstName)}|{Normalize(lastName)}";

    static string Normalize(string value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// ¿La fila contiene una base sintética + las estadísticas indicadas?
    /// Una carrera generada por `ApplyInitialCareer` tiene partidos y minutos
    /// exactamente a 36 por partido y puntos por debajo del tope histórico.
    /// Un jugador generado por el draft no tiene base previa (`baseGames == 0`), así que
    /// nunca se confunde con una carrera sembrada.
    /// </summary>
    public static bool LooksSyntheticBaseline(HistoricalPlayerStatsData owned,
                                              HistoricalPlayerStatsData games)
    {
        if (owned == null) return false;

        var played = games ?? new HistoricalPlayerStatsData();
        int baseGames = owned.games - played.games;
        int basePoints = owned.total_points - played.total_points;
        int baseMinutes = owned.total_minutes - played.total_minutes;

        return baseGames > 0
            && baseGames <= SyntheticGamesCap
            && basePoints >= 0
            && basePoints <= SyntheticPointsCap
            // Tolerancia mínima por el redondeo de minutos al archivar por equipo.
            && Math.Abs(baseMinutes - baseGames * SyntheticMinutesPerGame) <= 5;
    }

    /// <summary>
    /// Suma una carrera base (seeder) y las temporadas jugadas. No modifica ninguna
    /// de las dos filas originales.
    /// </summary>
    public static HistoricalPlayerStatsData Merge(HistoricalPlayerStatsData baseline,
                                                  HistoricalPlayerStatsData games)
    {
        var result = Copy(baseline);
        Add(result, games);
        return result;
    }

    /// <summary>Copia todos los campos de una fila histórica.</summary>
    public static HistoricalPlayerStatsData Copy(HistoricalPlayerStatsData source)
    {
        if (source == null) return new HistoricalPlayerStatsData();

        return new HistoricalPlayerStatsData
        {
            id = source.id,
            player_id = source.player_id,
            first_name = source.first_name,
            last_name = source.last_name,
            position = source.position,
            overall = source.overall,
            team_name = source.team_name,
            team_abbreviation = source.team_abbreviation,
            team_logo = source.team_logo,
            games = source.games,
            total_points = source.total_points,
            total_rebounds = source.total_rebounds,
            total_assists = source.total_assists,
            total_steals = source.total_steals,
            total_blocks = source.total_blocks,
            total_turnovers = source.total_turnovers,
            total_fgm = source.total_fgm,
            total_fga = source.total_fga,
            total_fg3m = source.total_fg3m,
            total_fg3a = source.total_fg3a,
            total_ftm = source.total_ftm,
            total_fta = source.total_fta,
            total_oreb = source.total_oreb,
            total_dreb = source.total_dreb,
            total_double_doubles = source.total_double_doubles,
            total_triple_doubles = source.total_triple_doubles,
            total_minutes = source.total_minutes,
            total_rating = source.total_rating
        };
    }

    /// <summary>Suma una fila de estadísticas sobre otra (in-place).</summary>
    public static void Add(HistoricalPlayerStatsData target, HistoricalPlayerStatsData add)
    {
        if (target == null || add == null) return;

        target.games += add.games;
        target.total_points += add.total_points;
        target.total_rebounds += add.total_rebounds;
        target.total_assists += add.total_assists;
        target.total_steals += add.total_steals;
        target.total_blocks += add.total_blocks;
        target.total_turnovers += add.total_turnovers;
        target.total_fgm += add.total_fgm;
        target.total_fga += add.total_fga;
        target.total_fg3m += add.total_fg3m;
        target.total_fg3a += add.total_fg3a;
        target.total_ftm += add.total_ftm;
        target.total_fta += add.total_fta;
        target.total_oreb += add.total_oreb;
        target.total_dreb += add.total_dreb;
        target.total_double_doubles += add.total_double_doubles;
        target.total_triple_doubles += add.total_triple_doubles;
        target.total_minutes += add.total_minutes;
        target.total_rating += add.total_rating;
    }

    /// <summary>
    /// Devuelve la única fila sin dueño con ese nombre, o null si no hay ninguna o hay
    /// varias. Se usa solo durante el seed/migración para asociar la carrera real; nunca
    /// para resolver carreras de jugadores generados en tiempo de ejecución.
    /// </summary>
    public static HistoricalPlayerStatsData FindUniqueSeedRow(
        IReadOnlyList<HistoricalPlayerStatsData> rows, string firstName, string lastName)
    {
        if (rows == null) return null;

        string key = NameKey(firstName, lastName);
        HistoricalPlayerStatsData found = null;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null || row.player_id > 0) continue;
            if (NameKey(row.first_name, row.last_name) != key) continue;
            if (found != null) return null;
            found = row;
        }
        return found;
    }
}
