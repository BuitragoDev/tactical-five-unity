using System;
using System.Collections.Generic;

/// <summary>
/// Distancias, giras y descanso del calendario NBA (F5-03).
///
/// Las coordenadas son aproximadas por ciudad y se usan solo para ordenar el
/// calendario y estimar el desgaste de viaje; no son datos de scouting ni de
/// simulación de partido. Un back-to-back se considera imposible si cruza más de
/// <see cref="MaxBackToBackMiles"/> millas sin un día de descanso.
/// </summary>
public static class TravelHelper
{
    public const int MaxBackToBackMiles = 1500;
    public const int ShortTripMiles = 250;
    public const int MediumTripMiles = 750;
    public const int LongTripMiles = 1500;

    // Ciudad / pabellón aproximado de cada franquicia (latitud, longitud).
    static readonly Dictionary<string, (double lat, double lon)> Coordinates =
        new Dictionary<string, (double lat, double lon)>(StringComparer.OrdinalIgnoreCase)
        {
            ["ATL"] = (33.7490, -84.3880),
            ["BKN"] = (40.6826, -73.9754),
            ["BOS"] = (42.3601, -71.0589),
            ["CHA"] = (35.2271, -80.8431),
            ["CHI"] = (41.8807, -87.6742),
            ["CLE"] = (41.4993, -81.6944),
            ["DAL"] = (32.7767, -96.7970),
            ["DEN"] = (39.7392, -104.9903),
            ["DET"] = (42.3314, -83.0458),
            ["GSW"] = (37.7680, -122.3877),
            ["HOU"] = (29.7604, -95.3698),
            ["IND"] = (39.7684, -86.1581),
            ["LAC"] = (34.0430, -118.2673),
            ["LAL"] = (34.0430, -118.2673),
            ["MEM"] = (35.1495, -90.0490),
            ["MIA"] = (25.7617, -80.1918),
            ["MIL"] = (43.0389, -87.9065),
            ["MIN"] = (44.9778, -93.2650),
            ["NOP"] = (29.9511, -90.0715),
            ["NYK"] = (40.7505, -73.9934),
            ["OKC"] = (35.4676, -97.5164),
            ["ORL"] = (28.5383, -81.3792),
            ["PHI"] = (39.9526, -75.1652),
            ["PHX"] = (33.4484, -112.0740),
            ["PRT"] = (45.5152, -122.6784),
            ["SAC"] = (38.5802, -121.4994),
            ["SAS"] = (29.4241, -98.4936),
            ["TOR"] = (43.6532, -79.3832),
            ["UTA"] = (40.7608, -111.8910),
            ["WAS"] = (38.9072, -77.0369),
        };

    public static bool TryCoordinates(TeamData team, out double lat, out double lon)
    {
        lat = 0;
        lon = 0;
        if (team == null || string.IsNullOrEmpty(team.abbreviation)) return false;
        if (!Coordinates.TryGetValue(team.abbreviation, out var value)) return false;
        lat = value.lat;
        lon = value.lon;
        return true;
    }

    /// <summary>Distancia en millas entre las sedes de dos equipos (0 si falta el dato).</summary>
    public static int DistanceMiles(TeamData a, TeamData b)
    {
        if (a == null || b == null || a.id == b.id) return 0;
        if (!TryCoordinates(a, out double lat1, out double lon1)
            || !TryCoordinates(b, out double lat2, out double lon2))
            return 0;

        const double earthRadiusMiles = 3958.8;
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);
        double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
        return (int)Math.Round(earthRadiusMiles * c);
    }

    public static int DistanceMiles(int teamIdA, int teamIdB,
        IReadOnlyDictionary<int, TeamData> teamsById)
    {
        if (teamsById == null || !teamsById.TryGetValue(teamIdA, out var a)
            || !teamsById.TryGetValue(teamIdB, out var b))
            return 0;
        return DistanceMiles(a, b);
    }

    /// <summary>Días completos de descanso entre dos jornadas consecutivas.</summary>
    public static int RestDays(int previousGameDay, int nextGameDay)
        => Math.Max(0, nextGameDay - previousGameDay - 1);

    /// <summary>
    /// Un back-to-back es imposible si no hay día de descanso y el desplazamiento
    /// supera el umbral. El scheduler evita programarlo.
    /// </summary>
    public static bool IsImpossibleBackToBack(int previousGameDay, int nextGameDay, int miles)
        => previousGameDay >= 0
        && nextGameDay == previousGameDay + 1
        && miles > MaxBackToBackMiles;

    /// <summary>
    /// Desgaste de viaje (0-3 puntos de físico). Los tramos cortos no penalizan;
    /// cada día de descanso reduce un punto y dos días lo absorben casi por completo.
    /// </summary>
    public static int FatigueCost(int miles, int restDays)
    {
        int tier = miles >= LongTripMiles ? 3
                 : miles >= MediumTripMiles ? 2
                 : miles >= ShortTripMiles ? 1
                 : 0;
        if (tier == 0) return 0;
        int rest = Math.Max(0, Math.Min(3, restDays));
        return Math.Max(0, tier - rest);
    }

    /// <summary>Texto corto para la UI (distancia y descanso).</summary>
    public static string Describe(int miles, int restDays)
    {
        string rest = restDays == 1 ? "1 día de descanso" : $"{restDays} días de descanso";
        return miles > 0 ? $"{miles:N0} mi · {rest}" : rest;
    }

    /// <summary>Distancia del equipo a la sede del partido (0 si juega en casa o no hay sede).</summary>
    public static int GameTravelMiles(int teamId, int homeTeamId,
        IReadOnlyDictionary<int, TeamData> teamsById)
    {
        if (teamId == homeTeamId) return 0;
        return DistanceMiles(teamId, homeTeamId, teamsById);
    }

    static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
