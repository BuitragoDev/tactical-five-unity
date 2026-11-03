/// <summary>
/// Textos de la narración de la vista Visual 2D. Es contenido editorial (como
/// <c>NewsTexts</c>), no UI estática: se construye en runtime con los nombres de
/// los jugadores y no pasa por la tabla de localización. Si algún día se traduce
/// la narración, estas plantillas se migran a claves.
///
/// Las variantes (<c>variant</c>) se eligen de forma determinista desde el
/// coreógrafo con un hash estable del evento; nunca usan el RNG del partido.
/// </summary>
public static class Sim2DCommentary
{
    static int Pick(int variant, int count) =>
        count <= 1 ? 0 : ((variant % count) + count) % count;

    public static string TipOff(string center) => $"Salto inicial: {center}.";

    public static string Inbound(string name) => $"Saca {name}.";

    public static string BringUp(string name, int variant = 0) =>
        Pick(variant, 3) switch
        {
            1 => $"{name} cruza el medio campo.",
            2 => $"{name} avanza con el balón.",
            _ => $"{name} sube el balón."
        };

    public static string Organize(string name, int variant = 0) =>
        Pick(variant, 3) switch
        {
            1 => $"{name} dirige la posesión.",
            2 => $"{name} mueve al equipo.",
            _ => $"{name} organiza el ataque."
        };

    public static string Pass(string from, string to, string zone) =>
        string.IsNullOrEmpty(zone) ? $"{from} pasa a {to}." : $"{from} pasa a {to} ({zone}).";

    public static string Screen(string name) => $"Bloqueo de {name}.";

    public static string ScreenRoll(string name) => $"{name} continúa al aro.";

    public static string Cut(string name) => $"{name} corta al aro.";

    public static string PostUp(string name) => $"{name} juega en el poste.";

    public static string Shot(string name, bool three, int variant = 0) =>
        three
            ? Pick(variant, 3) switch
            {
                1 => $"¡{name} para tres!",
                2 => $"¡Tira desde el triple {name}!",
                _ => $"¡Triple de {name}!"
            }
            : Pick(variant, 3) switch
            {
                1 => $"¡{name} se levanta!",
                2 => $"¡Intenta {name}!",
                _ => $"¡Tiro de {name}!"
            };

    public static string Made(string name, int points, int variant = 0) =>
        points == 3
            ? Pick(variant, 3) switch
            {
                1 => $"¡Triple de {name}!",
                2 => $"{name} acierta el triple.",
                _ => $"{name} anota el triple."
            }
            : Pick(variant, 3) switch
            {
                1 => $"¡Canasta de {name}!",
                2 => $"{name} suma dos.",
                _ => $"{name} anota."
            };

    public static string Miss(string name, int variant = 0) =>
        Pick(variant, 3) switch
        {
            1 => $"El tiro de {name} no entra.",
            2 => $"{name} no acierta.",
            _ => $"{name} falla."
        };

    public static string Rebound(string name, bool offensive, int variant = 0) =>
        offensive
            ? Pick(variant, 2) switch
            {
                1 => $"{name} se queda el rebote ofensivo.",
                _ => $"{name} coge el rebote ofensivo."
            }
            : Pick(variant, 2) switch
            {
                1 => $"{name} asegura el rebote.",
                _ => $"{name} coge el rebote."
            };

    public static string Steal(string name) => $"{name} roba el balón.";

    public static string Block(string name, int variant = 0) =>
        Pick(variant, 3) switch
        {
            1 => $"¡{name} lo rechaza!",
            2 => $"{name} pone el tapón.",
            _ => $"{name} tapona."
        };

    public static string Turnover(string name, int variant = 0) =>
        Pick(variant, 2) switch
        {
            1 => $"{name} entrega el balón.",
            _ => $"{name} pierde el balón."
        };

    public static string OutOfBounds(string name) =>
        string.IsNullOrEmpty(name) ? "El balón se marcha fuera." : $"{name} pierde el balón fuera.";

    /// <summary>Falta: el tipo decide si el ataque sigue, si hay tiros libres o
    /// si es una falta y uno. La sexta falta se narra como expulsión.</summary>
    public static string Foul(string name, string foulType, bool bonus, int variant = 0)
    {
        string who = string.IsNullOrEmpty(name) ? "La defensa" : name;
        switch (foulType)
        {
            case "and_one":
                return Pick(variant, 2) switch
                {
                    1 => $"¡Canasta y falta de {who}!",
                    _ => $"¡Y falta de {who}!"
                };
            case "ejection":
                return $"{who} comete su sexta falta y deja la pista.";
            case "reach_in" when !bonus:
                return Pick(variant, 2) switch
                {
                    1 => $"Falta de {who}. Sigue el ataque.",
                    _ => $"Falta de {who}; el ataque continúa."
                };
            default:
                return Pick(variant, 2) switch
                {
                    1 => $"Falta de {who}. Tiros libres.",
                    _ => $"Falta de {who}: va a la línea."
                };
        }
    }

    public static string FreeThrow(int attempt, int total, string name) =>
        total == 1 ? $"Libre de {name}..." : $"{attempt}º libre de {name}...";

    public static string FreeThrowMade() => "Dentro.";

    public static string FreeThrowMiss() => "Falla.";

    public static string FreeThrowRebound(string name, bool offensive) =>
        offensive ? $"{name} coge el rebote ofensivo del libre." : $"{name} coge el rebote del libre.";

    public static string Transition(string name) => $"{name} sale al contragolpe.";

    public static string SecondChance(string name) => $"{name} pelea la segunda oportunidad.";

    /// <summary>Cambio desde la banda (la animación la hace el renderer).</summary>
    public static string Substitution(string enters, string leaves) =>
        string.IsNullOrEmpty(leaves)
            ? $"Entra {enters}."
            : $"Entra {enters} por {leaves}.";
}
