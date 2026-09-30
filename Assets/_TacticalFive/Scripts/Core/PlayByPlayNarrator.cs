using System;
using System.Collections.Generic;

/// <summary>
/// Narrador del play-by-play (E4). Convierte las descripciones crudas del simulador
/// ("X encesta un triple", "Y tapona…") en comentario con personalidad, teniendo en
/// cuenta el marcador, el cuarto y las rachas. Es puro y determinista salvo por el azar
/// que se le inyecta (`roll`), de modo que es testeable.
/// </summary>
public static class PlayByPlayNarrator
{
    /// <summary>
    /// Reescribe una jugada. `desc` es el texto crudo; `home/awayScore` y `quarter` el
    /// contexto; `roll` ∈ [0,1) elige la variante. Nunca devuelve vacío.
    /// </summary>
    public static string Narrate(string desc, int homeScore, int awayScore, int quarter, double roll)
    {
        if (string.IsNullOrWhiteSpace(desc)) return "";

        int diff = Math.Abs(homeScore - awayScore);
        bool clutch = quarter >= 4 && diff <= 5;
        bool blowout = diff >= 20;
        bool overtime = quarter > 4;

        var (tag, body) = Classify(desc);
        string prefix = RunPrefix(clutch, blowout, overtime, roll);
        string suffix = RunSuffix(tag, clutch, blowout, roll);

        string core = body;
        // A la canasta se le añade salsa extra.
        if (tag == "three" || tag == "two" || tag == "andone" || tag == "dunk")
            core = EmbellishScore(body, tag, roll);

        string text = string.IsNullOrEmpty(prefix) ? core : $"{prefix} {core}";
        if (!string.IsNullOrEmpty(suffix)) text += $" {suffix}";
        return text;
    }

    // ── CLASIFICACIÓN ────────────────────────────────────

    /// <summary>Devuelve una etiqueta de tipo de jugada y el texto base (sin prefijo/sufijo).</summary>
    static (string tag, string body) Classify(string desc)
    {
        if (desc.Contains("triple")) return ("three", desc);
        if (desc.Contains("tapona")) return ("block", desc);
        if (desc.Contains("pierde el balón")) return ("turnover", desc);
        if (desc.Contains("Pérdida")) return ("turnover", desc);
        if (desc.Contains("tiros libres")) return ("ft", desc);
        if (desc.Contains("rebote") || desc.Contains("Rebote")) return ("rebound", desc);
        if (desc.Contains("anota y añade")) return ("andone", desc);
        if (desc.Contains("anota de dos")) return ("two", desc);
        if (desc.Contains("encesta")) return ("score", desc);
        return ("other", desc);
    }

    // ── PREFIJOS (momento del partido) ────────────────────

    static string RunPrefix(bool clutch, bool blowout, bool overtime, double roll)
    {
        if (overtime)
        {
            return Mix(roll, "¡PRÓRROGA!", "¡Tiempo extra!", "¡Que no se acabe esto!", "¡Aquí no se rinde nadie!");
        }
        if (clutch)
        {
            return Mix(roll, "¡Momento caliente!", "¡Esto se decide ahora!", "¡Partidazo!", "¡Nervios de acero!", "¡Qué final!");
        }
        if (blowout)
        {
            return Mix(roll, "", "", "Sin piedad…", "Puro trámite…", "Y sigue la paliza…");
        }
        return Mix(roll, "", "", "", "¡Buen movimiento!", "¡Vaya acción!", "¡Qué jugada!");
    }

    // ── SUFIJOS (salsa por tipo de jugada) ────────────────

    static string RunSuffix(string tag, bool clutch, bool blowout, double roll)
    {
        switch (tag)
        {
            case "three":
                return Mix(roll, "¡Desde más allá del arco!", "¡Lluvia de tres!", "¡Y la muñeca, caliente!",
                                 "¡Le sobra tiempo!", "¡Bomba desde el perímetro!");
            case "two":
                return Mix(roll, "", "", "¡Con autoridad!", "¡Qué elegancia!", "¡Se la puso donde quiso!");
            case "andone":
                return Mix(roll, "¡Y hay falta!", "¡Canasta y libre!", "¡Jugada de tres puntos!");
            case "block":
                return Mix(roll, "¡A volar!", "¡Eso no pasa!", "¡Manos de hierro!", "¡Se lo comió!");
            case "turnover":
                return Mix(roll, "¡Balón para el rival!", "¡Regalo!", "¡Descuido imperdonable!", "¡A perderla se ha dicho!");
            case "ft":
                return Mix(roll, "", "", "La mecánica manda.", "Sin pestañear desde la línea.");
            case "rebound":
                return Mix(roll, "", "", "¡El tablero es suyo!", "¡Se pelean cada balón!");
            default:
                return clutch && roll < 0.4 ? "¡Qué momento!" : "";
        }
    }

    // ── EMBELLECIMIENTO DE CANASTAS ───────────────────────

    /// <summary>
    /// Añade adjetivo al anotador según el azar. Mantiene el nombre original intacto
    /// (no lo altera) para no confundir con la identidad del jugador.
    /// </summary>
    static string EmbellishScore(string body, string tag, double roll)
    {
        string flourish = tag switch
        {
            "three" => Mix(roll, "¡Sin piedad!", "¡Cañonazo!", "¡Diana!", "¡Solo redes!"),
            _ => Mix(roll, "", "", "¡Bien trabajado!", "¡Ahí le duele!")
        };
        return string.IsNullOrEmpty(flourish) ? body : $"{body}. {flourish}";
    }

    // ── UTILIDAD ──────────────────────────────────────────

    static string Mix(double roll, params string[] options)
    {
        if (options == null || options.Length == 0) return "";
        int idx = (int)(roll * options.Length);
        if (idx < 0) idx = 0;
        if (idx >= options.Length) idx = options.Length - 1;
        return options[idx];
    }
}
