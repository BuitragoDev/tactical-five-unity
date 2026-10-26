using System;
using System.Collections.Generic;

/// <summary>
/// Cifras del convenio colectivo (CBA) por temporada (año de inicio). F4-07.
///
/// Fuente de las temporadas publicadas: NBA.com (cap, luxury tax, aprons, MLE,
/// bi-anual y mínimo). Las temporadas futuras que la NBA aún no ha fijado usan las
/// proyecciones publicadas por RealGM y se marcan como no verificadas; a partir de
/// la última fila de la tabla se proyectan con un crecimiento anual explícito del
/// 5 %. La tabla es la fuente única de cap, tax, aprons y excepciones: ya no se
/// aplica un +5 % ciego a los valores de la partida en cada rollover.
///
/// El mínimo se publica por años de servicio (0, 1, 2 y 3+). El repeater tax queda
/// fuera de esta fase: el impuesto se calcula con los tramos estándar de
/// <see cref="TradeHelper.CalculateLuxuryTax"/>.
/// </summary>
public static class CbaTable
{
    /// <summary>Temporada inicial del juego (2026-27).</summary>
    public const int CurrentSeasonStartYear = 2026;

    /// <summary>Primera temporada con cifra publicada en la tabla.</summary>
    public const int FirstSeasonStartYear = 2024;

    /// <summary>Última temporada con fila propia (proyección NBA de RealGM).</summary>
    public const int LastSeasonStartYear = 2034;

    /// <summary>Crecimiento usado solo para temporadas posteriores a la tabla.</summary>
    public const double ProjectionGrowth = 0.05;

    public sealed class Season
    {
        public int StartYear { get; }
        public long SalaryCap { get; }
        public long LuxuryTax { get; }
        public long FirstApron { get; }
        public long SecondApron { get; }
        public long NonTaxpayerMle { get; }
        public long TaxpayerMle { get; }
        public long BiAnnual { get; }
        public long MinimumSalary { get; }
        public long MinimumSalary1 { get; }
        public long MinimumSalary2 { get; }
        public long MinimumSalary3Plus { get; }
        public long TwoWaySalary { get; }
        /// <summary>true si la cifra está publicada por la NBA; false si es proyección.</summary>
        public bool Verified { get; }
        public string Source { get; }

        public Season(int startYear, long salaryCap, long luxuryTax, long firstApron,
            long secondApron, long nonTaxpayerMle, long taxpayerMle, long biAnnual,
            long minimumSalary, long minimumSalary1, long minimumSalary2,
            long minimumSalary3Plus, long twoWaySalary, bool verified, string source)
        {
            StartYear = startYear;
            SalaryCap = salaryCap;
            LuxuryTax = luxuryTax;
            FirstApron = firstApron;
            SecondApron = secondApron;
            NonTaxpayerMle = nonTaxpayerMle;
            TaxpayerMle = taxpayerMle;
            BiAnnual = biAnnual;
            MinimumSalary = minimumSalary;
            MinimumSalary1 = minimumSalary1;
            MinimumSalary2 = minimumSalary2;
            MinimumSalary3Plus = minimumSalary3Plus;
            TwoWaySalary = twoWaySalary;
            Verified = verified;
            Source = source;
        }

        /// <summary>
        /// Mínimo legal para un jugador según sus años de servicio. El juego agrupa
        /// 3 o más años en una sola banda (F4-08); la NBA publica bandas 3–9 y 10+.
        /// </summary>
        public long MinimumFor(int yearsOfService)
        {
            if (yearsOfService <= 0) return MinimumSalary;
            if (yearsOfService == 1) return MinimumSalary1;
            if (yearsOfService == 2) return MinimumSalary2;
            return MinimumSalary3Plus;
        }

        public string Label => $"{StartYear}-{(StartYear + 1) % 100:00}";
    }

    private static readonly Dictionary<int, Season> Table = new Dictionary<int, Season>
    {
        // Temporadas publicadas por la NBA.
        [2024] = new Season(2024,
            140_588_000, 170_814_000, 178_132_000, 188_931_000,
            12_822_000, 5_168_000, 4_668_000,
            1_157_153, 1_862_265, 2_087_519, 2_162_606,
            578_577, true, "NBA.com (cifras oficiales)"),

        [2025] = new Season(2025,
            154_647_000, 187_895_000, 195_945_000, 207_824_000,
            14_104_000, 5_685_000, 5_134_000,
            1_272_870, 2_048_494, 2_296_274, 2_378_870,
            636_435, true, "NBA.com (cifras oficiales)"),

        [2026] = new Season(2026,
            164_961_000, 200_428_000, 209_015_000, 221_686_000,
            15_044_000, 6_064_000, 5_477_000,
            1_357_763, 2_185_116, 2_449_421, 2_537_526,
            678_882, true, "NBA.com (30/06/2026)"),

        // Proyecciones publicadas por RealGM (no son cifras oficiales).
        [2027] = Projected(2027, 176_000_000, 213_000_000, 222_000_000, 236_000_000,
            16_051_000, 6_470_000, 5_843_000),
        [2028] = Projected(2028, 184_800_000, 224_532_000, 234_113_000, 248_308_000,
            16_854_000, 6_793_000, 6_135_000),
        [2029] = Projected(2029, 194_040_000, 235_759_000, 245_819_000, 260_724_000,
            17_696_000, 7_133_000, 6_442_000),
        [2030] = Projected(2030, 203_742_000, 247_547_000, 258_110_000, 273_760_000,
            18_581_000, 7_489_000, 6_764_000),
        [2031] = Projected(2031, 213_929_000, 259_924_000, 271_015_000, 287_447_000,
            19_510_000, 7_864_000, 7_102_000),
        [2032] = Projected(2032, 224_625_000, 272_919_000, 284_565_000, 301_818_000,
            20_486_000, 8_257_000, 7_458_000),
        [2033] = Projected(2033, 235_856_000, 286_565_000, 298_793_000, 316_909_000,
            21_510_000, 8_670_000, 7_830_000),
        [2034] = Projected(2034, 247_649_000, 300_894_000, 313_733_000, 332_756_000,
            22_586_000, 9_103_000, 8_222_000),
    };

    private static readonly Dictionary<int, Season> BeyondTable = new Dictionary<int, Season>();

    /// <summary>Fila CBA de una temporada; nunca devuelve null.</summary>
    public static Season For(int seasonStartYear)
    {
        if (seasonStartYear <= 0) seasonStartYear = CurrentSeasonStartYear;
        if (seasonStartYear < FirstSeasonStartYear) seasonStartYear = FirstSeasonStartYear;

        if (Table.TryGetValue(seasonStartYear, out var season))
            return season;

        if (BeyondTable.TryGetValue(seasonStartYear, out var projected))
            return projected;

        var last = Table[LastSeasonStartYear];
        int years = seasonStartYear - LastSeasonStartYear;
        projected = new Season(
            seasonStartYear,
            Grow(last.SalaryCap, years),
            Grow(last.LuxuryTax, years),
            Grow(last.FirstApron, years),
            Grow(last.SecondApron, years),
            Grow(last.NonTaxpayerMle, years),
            Grow(last.TaxpayerMle, years),
            Grow(last.BiAnnual, years),
            Grow(last.MinimumSalary, years),
            Grow(last.MinimumSalary1, years),
            Grow(last.MinimumSalary2, years),
            Grow(last.MinimumSalary3Plus, years),
            Grow(last.TwoWaySalary, years),
            false,
            $"Proyección del juego (+{ProjectionGrowth:P0} anual desde {LastSeasonStartYear}-{(LastSeasonStartYear + 1) % 100:00})");

        BeyondTable[seasonStartYear] = projected;
        return projected;
    }

    /// <summary>
    /// Copia los topes y excepciones de la temporada en settings. <c>minimum_salary</c>
    /// guarda el mínimo de 0 años; las bandas 1, 2 y 3+ se consultan en la fila CBA.
    /// </summary>
    public static void ApplyTo(LeagueSettingsData settings, int seasonStartYear)
    {
        if (settings == null) return;
        var season = For(seasonStartYear);
        settings.salary_cap = season.SalaryCap;
        settings.luxury_tax = season.LuxuryTax;
        settings.apron = season.FirstApron;
        settings.repeater_apron = season.SecondApron;
        settings.mid_level = season.NonTaxpayerMle;
        settings.taxpayer_mid_level = season.TaxpayerMle;
        settings.bi_annual = season.BiAnnual;
        settings.minimum_salary = season.MinimumSalary;
        settings.two_way_salary = season.TwoWaySalary;
        settings.season_start_year = season.StartYear;
        settings.cba_projected = season.Verified ? 0 : 1;
        settings.cba_source = season.Source;
    }

    /// <summary>Factor de crecimiento del cap entre dos temporadas.</summary>
    public static double CapRatio(int baseSeasonStartYear, int targetSeasonStartYear)
    {
        long baseCap = For(baseSeasonStartYear).SalaryCap;
        if (baseCap <= 0) return 1.0;
        return (double)For(targetSeasonStartYear).SalaryCap / baseCap;
    }

    private static Season Projected(int startYear, long cap, long tax, long firstApron,
        long secondApron, long nonTaxpayerMle, long taxpayerMle, long biAnnual)
    {
        long minimum = MinimumFromCap(cap);
        return new Season(startYear, cap, tax, firstApron, secondApron,
            nonTaxpayerMle, taxpayerMle, biAnnual,
            minimum, MinimumTierFromZero(minimum, 1),
            MinimumTierFromZero(minimum, 2), MinimumTierFromZero(minimum, 3),
            TwoWayFromMinimum(minimum), false, "RealGM (proyección NBA)");
    }

    // El mínimo de 0 años y el two-way escalan con el mismo porcentaje que el cap.
    private static long MinimumFromCap(long cap)
        => cap <= 0 ? 0 : (long)Math.Round(cap * 0.008231, MidpointRounding.AwayFromZero);

    // Ratios oficiales de la escala de mínimos (0 años = 1). Se usan solo en las
    // temporadas proyectadas; las oficiales llevan las cifras exactas en la tabla.
    private static long MinimumTierFromZero(long minimum, int yearsOfService)
    {
        if (minimum <= 0) return 0;
        double ratio = yearsOfService <= 1 ? 1.6093504
                     : yearsOfService == 2 ? 1.8040127
                     : 1.8689023;
        return (long)Math.Round(minimum * ratio, MidpointRounding.AwayFromZero);
    }

    private static long TwoWayFromMinimum(long minimum)
        => minimum <= 0 ? 0 : (long)Math.Round(minimum / 2.0, MidpointRounding.AwayFromZero);

    private static long Grow(long value, int years)
        => years <= 0 ? value : (long)Math.Round(value * Math.Pow(1.0 + ProjectionGrowth, years),
            MidpointRounding.AwayFromZero);
}
