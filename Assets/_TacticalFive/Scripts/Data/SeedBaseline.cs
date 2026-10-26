using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SQLite;

/// <summary>
/// Huella de las tablas estáticas editables del template. Sirve para detectar que el
/// seed cambió desde la última revisión: si el template guardado no lleva esta huella
/// se reconstruye, y el test de EditMode exige actualizar la constante tras revisar el
/// seed. No es un gate de validez (eso es `SeedAudit`): una huella sin actualizar avisa
/// y reconstruye, nunca impide crear la partida.
/// </summary>
public static class SeedBaseline
{
    // Actualizar únicamente después de revisar intencionadamente un cambio del seed.
    public const string Fingerprint = "767f0dada4a2aea44955b5edbcd538b192f24ff3c7566f0d62c301136970bada";

    public static string Compute(
        IEnumerable<TeamData> teams,
        IEnumerable<PlayerData> players,
        IEnumerable<LeagueSettingsData> settings)
    {
        var text = new StringBuilder(128 * 1024);
        AppendRows(text, "teams", teams);
        AppendRows(text, "players", players);
        AppendRows(text, "league_settings", settings);

        using (var sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
            return string.Concat(hash.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }

    public static string Compute(SQLiteConnection database) => Compute(
        database.Table<TeamData>().ToList(),
        database.Table<PlayerData>().ToList(),
        database.Table<LeagueSettingsData>().ToList());

    private static void AppendRows<T>(StringBuilder text, string table, IEnumerable<T> source)
    {
        var rows = (source ?? Enumerable.Empty<T>()).ToList();
        var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0
                        && !Attribute.IsDefined(p, typeof(IgnoreAttribute))
                        // Campos de carrera/agencia libre: no forman parte del seed
                        // estático, y los saves viejos los estiman al inicializarse.
                        && !(table == "players" && (p.Name == nameof(PlayerData.years_of_service)
                            || p.Name == nameof(PlayerData.free_agent_type)
                            || p.Name == nameof(PlayerData.qualifying_offer_salary))))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray();

        AppendToken(text, table);
        AppendToken(text, rows.Count.ToString(CultureInfo.InvariantCulture));

        foreach (var row in rows.OrderBy(GetStableId))
        {
            foreach (var property in properties)
            {
                AppendToken(text, property.Name);
                object value = property.GetValue(row, null);
                AppendToken(text, value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture));
            }
        }
    }

    private static int GetStableId<T>(T row)
    {
        var property = typeof(T).GetProperty("id", BindingFlags.Instance | BindingFlags.Public);
        if (property == null) return 0;
        object value = property.GetValue(row, null);
        return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static void AppendToken(StringBuilder text, string value)
    {
        if (value == null)
        {
            text.Append("-1:");
            return;
        }

        text.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        text.Append(':');
        text.Append(value);
    }
}
