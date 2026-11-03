using UnityEngine;

/// <summary>
/// Geometría de la cancha de la vista Visual 2D y reglas de colocación.
/// Es una capa pura (sin estado ni azar) que usan <see cref="Sim2DChoreographer"/>
/// y los tests. Las coordenadas están normalizadas 0..1 sobre la pista: x = 0 es
/// la línea de fondo izquierda, x = 1 la derecha; y = 0 una banda, y = 1 la otra.
/// Cuando el ataque va a la derecha (x = 1) se usan las coordenadas tal cual;
/// cuando va a la izquierda se reflejan con <see cref="Mirror"/>.
/// </summary>
public static class Sim2DCourt
{
    public const float HoopLeftX = 0.069f;
    public const float HoopRightX = 0.931f;
    public const float HoopY = 0.5f;
    public const float FtLeftX = 0.215f;
    public const float FtRightX = 0.785f;

    /// <summary>Zonas de media cancha. Los nombres son del ataque a la derecha;
    /// el reflejo los lleva al ataque a la izquierda.</summary>
    public enum Spot
    {
        Top,
        CornerLeft,
        CornerRight,
        WingLeft,
        WingRight,
        SlotLeft,
        SlotRight,
        ElbowLeft,
        ElbowRight,
        PostLeft,
        PostRight,
        DunkerLeft,
        DunkerRight,
        Rim,
        Backcourt
    }

    public static float TowardBasket(bool attackRight) => attackRight ? 1f : -1f;

    public static float HoopX(bool attackRight) => attackRight ? HoopRightX : HoopLeftX;

    public static float FtX(bool attackRight) => attackRight ? FtRightX : FtLeftX;

    /// <summary>Línea de fondo del aro que se ataca.</summary>
    public static float BaselineX(bool attackRight) => attackRight ? 0.965f : 0.035f;

    /// <summary>Campo propio del equipo que ataca (donde recibe el saque).</summary>
    public static float BackcourtX(bool attackRight) => attackRight ? 0.30f : 0.70f;

    /// <summary>
    /// Punto de saque en la línea de fondo **propia** del equipo que ataca: es la
    /// línea donde se anotó la canasta (o donde salió el balón), no la del aro
    /// que se ataca. Tras canasta el rival saca desde ahí y sube el balón.
    /// </summary>
    public static Vector2 OwnBaselineInbound(bool attackRight) =>
        new Vector2(BaselineX(!attackRight), 0.5f);

    /// <summary>Primera línea del campo rival (donde se instala el ataque).</summary>
    public static float FrontcourtX(bool attackRight) => attackRight ? 0.70f : 0.30f;

    public static Vector2 Mirror(Vector2 point, bool attackRight)
    {
        return attackRight ? point : new Vector2(1f - point.x, point.y);
    }

    public static Vector2 Hoop(bool attackRight) => new Vector2(HoopX(attackRight), HoopY);

    public static Vector2 SpotPosition(Spot spot, bool attackRight)
    {
        Vector2 point = spot switch
        {
            Spot.Top => new Vector2(0.66f, 0.50f),
            Spot.CornerLeft => new Vector2(0.90f, 0.045f),
            Spot.CornerRight => new Vector2(0.90f, 0.955f),
            Spot.WingLeft => new Vector2(0.72f, 0.18f),
            Spot.WingRight => new Vector2(0.72f, 0.82f),
            Spot.SlotLeft => new Vector2(0.70f, 0.30f),
            Spot.SlotRight => new Vector2(0.70f, 0.70f),
            Spot.ElbowLeft => new Vector2(0.80f, 0.36f),
            Spot.ElbowRight => new Vector2(0.80f, 0.64f),
            Spot.PostLeft => new Vector2(0.88f, 0.40f),
            Spot.PostRight => new Vector2(0.88f, 0.60f),
            Spot.DunkerLeft => new Vector2(0.92f, 0.46f),
            Spot.DunkerRight => new Vector2(0.92f, 0.54f),
            Spot.Rim => new Vector2(0.931f, 0.50f),
            Spot.Backcourt => new Vector2(0.30f, 0.50f),
            _ => new Vector2(0.50f, 0.50f)
        };
        return Mirror(point, attackRight);
    }

    public static bool IsThreePointSpot(Spot spot)
    {
        return spot is Spot.Top or Spot.CornerLeft or Spot.CornerRight
            or Spot.WingLeft or Spot.WingRight;
    }

    /// <summary>Punto desde el que tira el jugador según su posición y el tipo de tiro.</summary>
    public static Vector2 ShotSpot(string position, bool three, int side, bool attackRight)
    {
        bool left = side % 2 == 0;
        Spot spot = three
            ? position switch
            {
                "PG" => Spot.Top,
                "SG" => left ? Spot.CornerLeft : Spot.WingRight,
                "SF" => left ? Spot.WingLeft : Spot.CornerRight,
                "PF" => left ? Spot.WingRight : Spot.WingLeft,
                "C" => Spot.Top,
                _ => Spot.WingLeft
            }
            : position switch
            {
                "PG" => left ? Spot.ElbowLeft : Spot.ElbowRight,
                "SG" => left ? Spot.ElbowRight : Spot.ElbowLeft,
                "SF" => left ? Spot.WingLeft : Spot.WingRight,
                "PF" => left ? Spot.PostLeft : Spot.PostRight,
                "C" => left ? Spot.Rim : Spot.DunkerRight,
                _ => Spot.ElbowLeft
            };
        return SpotPosition(spot, attackRight);
    }

    /// <summary>
    /// Colocación defensiva individual. El defensor on-ball se mete entre su par y
    /// el aro; a un pase niega la línea; a dos pases ayuda hacia la pintura. El
    /// pívot protege el aro cuando su par sale a la línea de tres.
    /// </summary>
    public static Vector2 DefensiveSpot(Vector2 man, Vector2 ball, bool attackRight,
        bool isCenter, bool onBall)
    {
        float toward = TowardBasket(attackRight);
        if (onBall)
        {
            return new Vector2(
                Mathf.Clamp(man.x + toward * 0.035f, 0.02f, 0.98f),
                Mathf.Clamp(Mathf.Lerp(man.y, 0.5f, 0.10f), 0.02f, 0.98f));
        }

        float ballDistance = Vector2.Distance(man, ball);
        if (ballDistance < 0.22f)
        {
            // Deny: en la línea de pase, ligeramente del lado del aro.
            return new Vector2(
                Mathf.Clamp(Mathf.Lerp(man.x, ball.x, 0.28f) + toward * 0.01f, 0.02f, 0.98f),
                Mathf.Clamp(Mathf.Lerp(man.y, ball.y, 0.28f), 0.02f, 0.98f));
        }

        if (isCenter)
        {
            // Protección del aro si su par está fuera.
            return new Vector2(
                Mathf.Clamp(HoopX(attackRight) - toward * 0.055f, 0.02f, 0.98f),
                0.5f);
        }

        // Help: se hunde hacia el balón y la pintura.
        return new Vector2(
            Mathf.Clamp(Mathf.Lerp(man.x, ball.x, 0.18f) - toward * 0.02f, 0.02f, 0.98f),
            Mathf.Clamp(Mathf.Lerp(man.y, 0.5f, 0.45f), 0.02f, 0.98f));
    }

    /// <summary>Posición de caja del defensor que boxea al reboteador.</summary>
    public static Vector2 BoxOutSpot(Vector2 rebounder, bool attackRight)
    {
        float toward = TowardBasket(attackRight);
        return new Vector2(
            Mathf.Clamp(rebounder.x + toward * 0.025f, 0.02f, 0.98f),
            Mathf.Clamp(rebounder.y, 0.04f, 0.96f));
    }

    public static Vector2 FreeThrowShooter(bool attackRight)
    {
        return new Vector2(FtX(attackRight), 0.5f);
    }

    /// <summary>Colocación en la botella durante un tiro libre (2 por equipo).</summary>
    public static Vector2 FreeThrowLane(int index, bool defense, bool attackRight)
    {
        float toward = TowardBasket(attackRight);
        float y = index == 0 ? 0.39f : 0.61f;
        float offset = defense ? 0.045f : 0.10f;
        return new Vector2(HoopX(attackRight) - toward * offset, y);
    }

    /// <summary>Jugadores fuera de la botella durante un tiro libre.</summary>
    public static Vector2 FreeThrowOutside(int index, bool defense, bool attackRight)
    {
        float toward = TowardBasket(attackRight);
        float y = index switch
        {
            0 => 0.25f,
            1 => 0.75f,
            _ => 0.50f
        };
        float offset = defense ? 0.16f : 0.09f;
        return new Vector2(FtX(attackRight) - toward * offset, y);
    }
}
