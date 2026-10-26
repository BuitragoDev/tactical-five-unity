using System;

/// <summary>
/// Escala salarial de rookies por puesto de draft y temporada (F4-06/F4-07).
///
/// El salario de un rookie ya no es un <c>Random.Range</c> por tramo: depende solo
/// del número de elección y del año de la temporada en que debuta. La tabla base
/// reproduce la forma de la escala de primera ronda de 2025-26; las temporadas
/// posteriores escalan con el crecimiento del cap de <see cref="CbaTable"/>, la
/// misma referencia que usan los topes y las excepciones. La segunda ronda usa
/// contratos cercanos al mínimo, también decrecientes con el pick.
/// </summary>
public static class RookieScale
{
    /// <summary>Temporada de referencia de la tabla (2025-26).</summary>
    public const int BaseSeasonStartYear = 2025;

    public const int TotalPicks = 60;

    // Escala de primera ronda 2025-26 (picks 1..30). Estrictamente decreciente:
    // el test RookieScaleTests lo comprueba en cada temporada.
    private static readonly long[] Round1Base =
    {
        13_800_000, 12_350_000, 11_100_000, 10_000_000, 9_070_000,
         8_250_000,  7_540_000,  6_910_000,  6_360_000, 6_050_000,
         5_750_000,  5_470_000,  5_200_000,  4_950_000, 4_710_000,
         4_490_000,  4_280_000,  4_090_000,  3_910_000, 3_750_000,
         3_600_000,  3_460_000,  3_330_000,  3_210_000, 3_100_000,
         3_000_000,  2_910_000,  2_830_000,  2_760_000, 2_700_000
    };

    // Segunda ronda: contrato estándar cercano al mínimo, decreciente por pick.
    private const long Round2Start = 2_000_000;
    private const long Round2Step = 20_000;

    /// <summary>Salario de rookie para un pick (1..60) en una temporada concreta.</summary>
    /// <param name="pickNumber">Número de elección 1-based.</param>
    /// <param name="seasonStartYear">Año de inicio de la temporada en la que debuta (p. ej. 2026 para 2026-27).</param>
    /// <param name="isTwoWay">Si firma contrato two-way, el salario es el fijo de la liga.</param>
    public static long SalaryFor(int pickNumber, int seasonStartYear, bool isTwoWay = false)
    {
        int season = seasonStartYear > 0 ? seasonStartYear : BaseSeasonStartYear;
        if (isTwoWay) return CbaTable.For(season).TwoWaySalary;
        if (pickNumber < 1) pickNumber = 1;
        if (pickNumber > TotalPicks) pickNumber = TotalPicks;

        long baseSalary = pickNumber <= 30
            ? Round1Base[pickNumber - 1]
            : Round2Start - (pickNumber - 31) * Round2Step;

        return RoundToTenThousand(ApplySeason(baseSalary, season));
    }

    /// <summary>Importe base de la tabla de primera ronda para un pick (sin crecimiento).</summary>
    public static long Round1BaseSalary(int pickNumber)
    {
        if (pickNumber < 1) pickNumber = 1;
        if (pickNumber > 30) pickNumber = 30;
        return Round1Base[pickNumber - 1];
    }

    /// <summary>Importe base de la tabla de segunda ronda para un pick (sin crecimiento).</summary>
    public static long Round2BaseSalary(int pickNumber)
    {
        if (pickNumber < 31) pickNumber = 31;
        if (pickNumber > TotalPicks) pickNumber = TotalPicks;
        return Round2Start - (pickNumber - 31) * Round2Step;
    }

    private static double ApplySeason(long baseSalary, int seasonStartYear)
    {
        int season = seasonStartYear > 0 ? seasonStartYear : BaseSeasonStartYear;
        if (season <= BaseSeasonStartYear) return baseSalary;
        return baseSalary * CbaTable.CapRatio(BaseSeasonStartYear, season);
    }

    private static long RoundToTenThousand(double amount)
    {
        if (amount <= 0) return 0;
        return (long)Math.Round(amount / 10_000.0, MidpointRounding.AwayFromZero) * 10_000L;
    }
}
