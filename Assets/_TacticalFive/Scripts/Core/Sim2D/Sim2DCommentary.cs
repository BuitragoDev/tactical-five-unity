/// <summary>
/// Textos de la narración de la vista Visual 2D. Es contenido editorial (como
/// <c>NewsTexts</c>), no UI estática: se construye en runtime con los nombres de
/// los jugadores y no pasa por la tabla de localización. Si algún día se traduce
/// la narración, estas plantillas se migran a claves.
/// </summary>
public static class Sim2DCommentary
{
    public static string TipOff(string center) => $"Salto inicial: {center}.";

    public static string Inbound(string name) => $"Saca {name}.";

    public static string BringUp(string name) => $"{name} sube el balón.";

    public static string Organize(string name) => $"{name} organiza el ataque.";

    public static string Pass(string from, string to, string zone) =>
        string.IsNullOrEmpty(zone) ? $"{from} pasa a {to}." : $"{from} pasa a {to} ({zone}).";

    public static string Screen(string name) => $"Bloqueo de {name}.";

    public static string ScreenRoll(string name) => $"{name} continúa al aro.";

    public static string Cut(string name) => $"{name} corta al aro.";

    public static string PostUp(string name) => $"{name} juega en el poste.";

    public static string Shot(string name, bool three) =>
        three ? $"¡Triple de {name}!" : $"¡Tiro de {name}!";

    public static string Made(string name, int points) =>
        points == 3 ? $"{name} anota el triple." : $"{name} anota.";

    public static string Miss(string name) => $"{name} falla.";

    public static string Rebound(string name, bool offensive) =>
        offensive ? $"{name} coge el rebote ofensivo." : $"{name} coge el rebote.";

    public static string Steal(string name) => $"{name} roba el balón.";

    public static string OutOfBounds(string name) =>
        string.IsNullOrEmpty(name) ? "El balón se marcha fuera." : $"{name} pierde el balón fuera.";

    public static string FreeThrow(int attempt, int total, string name) =>
        total == 1 ? $"Libre de {name}..." : $"{attempt}º libre de {name}...";

    public static string FreeThrowMade() => "Dentro.";

    public static string FreeThrowMiss() => "Falla.";

    public static string FreeThrowRebound(string name, bool offensive) =>
        offensive ? $"{name} coge el rebote ofensivo del libre." : $"{name} coge el rebote del libre.";

    public static string Transition(string name) => $"{name} sale al contragolpe.";

    public static string SecondChance(string name) => $"{name} pelea la segunda oportunidad.";
}
