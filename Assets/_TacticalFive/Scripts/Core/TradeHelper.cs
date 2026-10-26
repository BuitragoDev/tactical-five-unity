using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class TradeHelper
{
    public const long SALARY_CAP = 174_647_000;
    public const long LUXURY_TAX = 220_428_000;
    public const long FIRST_APRON = 229_015_000;
    public const long SECOND_APRON = 241_686_000;
    public const long NT_MLE = 14_100_000;
    public const long T_MLE = 5_700_000;
    public const long MIN_SALARY = 2_000_000;
    public const int MAX_STANDARD_ROSTER = 15;
    public const long TWO_WAY_SALARY = 578_000;
    public const int MAX_TWO_WAY = 2;
    // Límite combinado de referencia; las validaciones usan cada cupo por separado.
    public const int MAX_ROSTER = MAX_STANDARD_ROSTER + MAX_TWO_WAY;
    public const int MAX_TWO_WAY_AGE = 23;
    public const int IR_MIN_DAYS = 90;

    // Salary matching (ECO-02): una sola fuente para los tramos del CBA.
    public const int FIRST_APRON_MATCHING_PERCENT = 110;
    public const long SALARY_MATCHING_BUFFER = 250_000;
    public const long SALARY_MATCHING_LOW_BRACKET = 7_500_000;
    public const long SALARY_MATCHING_MIDDLE_BRACKET = 29_000_000;
    public const long SALARY_MATCHING_MIDDLE_ALLOWANCE = 7_500_000;

    public static bool IsEligibleForTwoWay(PlayerData p)
    {
        return p != null && IsEligibleForTwoWayAge(p.age);
    }

    /// <summary>Edad máxima para un contrato two-way (misma fuente para FA y draft).</summary>
    public static bool IsEligibleForTwoWayAge(int age) => age <= MAX_TWO_WAY_AGE;

    /// <summary>
    /// Ajusta una oferta al mínimo legal y al máximo permitido. Devuelve false si no hay
    /// rango legal (el máximo está por debajo del mínimo). ECO-13: antes el suelo de 1M se
    /// aplicaba con `else if`, así que una oferta podía quedar por encima del máximo.
    /// </summary>
    public static bool TryClampOffer(long offer, long floor, long max, out long clamped)
    {
        clamped = offer;
        if (max < floor) return false;
        if (clamped < floor) clamped = floor;
        if (clamped > max) clamped = max;
        return true;
    }

    public static bool IsEligibleForIR(PlayerData p)
    {
        return p != null && p.injury_days >= IR_MIN_DAYS;
    }

    static void ValidateTradeSide(
        long salaryOut, long salaryIn, int playersOutCount, int playersInCount,
        long newPayroll, string teamName, bool hardCappedToFirstApron,
        List<string> errors, long firstApron = FIRST_APRON, long secondApron = SECOND_APRON)
    {
        bool aboveSecondApron = newPayroll > secondApron;
        bool hardCapExceeded = hardCappedToFirstApron && newPayroll > firstApron;

        // Son restricciones independientes: el hard cap limita la nómina proyectada;
        // el segundo apron prohíbe agregar salarios de varios jugadores salientes.
        if (hardCapExceeded)
            errors.Add($"{teamName} está sujeto al hard cap del primer apron y la nómina proyectada sería ${newPayroll:N0} (máximo ${firstApron:N0}).");

        if (aboveSecondApron && playersOutCount > 1)
        {
            errors.Add($"{teamName} está por encima del segundo apron y no puede agregar salarios de {playersOutCount} jugadores salientes para recibir {playersInCount}.");
        }

        long maxReceive = GetMaxIncomingSalary(salaryOut, newPayroll, hardCappedToFirstApron, firstApron, secondApron);
        if (salaryIn > maxReceive)
        {
            if (aboveSecondApron)
                errors.Add($"{teamName} está por encima del segundo apron. Solo puede recibir hasta ${maxReceive:N0}.");
            else if (hardCappedToFirstApron)
                errors.Add($"{teamName} está sujeto al hard cap del primer apron. Solo puede recibir hasta ${maxReceive:N0}.");
            else if (newPayroll > firstApron)
                errors.Add($"{teamName} está en el primer apron. Solo puede recibir hasta ${maxReceive:N0}.");
            else
                errors.Add($"{teamName} no puede recibir más de ${maxReceive:N0}.");
        }
    }

    /// <summary>
    /// Límite exacto del salario entrante según la nómina proyectada. Por encima del
    /// segundo apron (o sobre el primero con hard cap) no puede aumentar salario; entre
    /// aprons se aplica el porcentaje definido arriba; por debajo del primero se aplica
    /// el matching por tramos. El mismo valor se usa para validar y redactar el error.
    /// </summary>
    public static long GetMaxIncomingSalary(long salaryOut, long projectedPayroll,
        bool hardCappedToFirstApron = false,
        long firstApron = FIRST_APRON,
        long secondApron = SECOND_APRON)
    {
        if (projectedPayroll > secondApron || (hardCappedToFirstApron && projectedPayroll > firstApron))
            return salaryOut;

        if (projectedPayroll > firstApron)
            return salaryOut * FIRST_APRON_MATCHING_PERCENT / 100;

        if (salaryOut < SALARY_MATCHING_LOW_BRACKET)
            return salaryOut * 2 + SALARY_MATCHING_BUFFER;

        if (salaryOut < SALARY_MATCHING_MIDDLE_BRACKET)
            return salaryOut + SALARY_MATCHING_MIDDLE_ALLOWANCE;

        return salaryOut * 125 / 100 + SALARY_MATCHING_BUFFER;
    }

    public static int PickBonus(DraftPickData pk, int currentDraftYear = 0)
    {
        if (pk == null) return 0;

        // Descuento por año: una ronda de un draft futuro vale menos.
        int yearOffset = (currentDraftYear > 0 && pk.draft_year > 0)
            ? Mathf.Max(0, pk.draft_year - currentDraftYear)
            : 0;

        int bonus;
        if (pk.round == 1)
        {
            // Años futuros sin posición (pick_number = 0): valor plano de R1.
            int slot = pk.pick_number > 0 ? Mathf.Clamp(pk.pick_number, 1, 30) : 16;
            bonus = Mathf.Max(1, 10 + (30 - slot) / 3 - yearOffset * 2);
        }
        else if (pk.round == 2)
        {
            int slot = pk.pick_number > 0 ? Mathf.Clamp(pk.pick_number - 30, 1, 30) : 16;
            bonus = Mathf.Max(1, 5 + (30 - slot) / 5 - yearOffset);
        }
        else
        {
            bonus = Mathf.Max(1, 3 - yearOffset);
        }

        // Un pick protegido top-N pierde cuota de valor según la profundidad
        // del rango protegido; un top-1 colapsa a un mínimo de 1.
        if (pk.protected_from > 0)
        {
            int prot = Mathf.Clamp(pk.protected_from, 1, 30);
            bonus = Mathf.Max(1, bonus - prot * 2);
        }
        return bonus;
    }

    // Resuelve el salario efectivo de un jugador en un traspaso:
    // si está en `signSalaries` (player.id -> nuevo salario), se usa ese (S&T de FA
    // propio firmado con Bird rights); si no, el salario actual del contrato.
    static long SalaryOf(PlayerData p, Dictionary<int, long> signSalaries)
    {
        if (signSalaries != null && signSalaries.TryGetValue(p.id, out var newSal))
            return newSal;
        return p.salary;
    }

    public static List<string> ValidateTrade(
        List<PlayerData> teamASelected,
        List<PlayerData> teamBSelected,
        int teamATotalRoster,
        int teamBTotalRoster,
        string teamBName,
        long teamBCurrentPayroll,
        string teamAName = null,
        long teamACurrentPayroll = 0,
        bool teamAHardCapped = false,
        bool teamBHardCapped = false,
        long firstApron = FIRST_APRON,
        long secondApron = SECOND_APRON,
        long luxuryTax = LUXURY_TAX,
        Dictionary<int, long> teamASignSalaries = null,
        Dictionary<int, long> teamBSignSalaries = null,
        int teamATwoWayCount = 0,
        int teamBTwoWayCount = 0,
        int teamATotalActiveRoster = -1,
        int teamBTotalActiveRoster = -1)
    {
        var errors = new List<string>();

        var aSalaryOut = teamASelected.Sum(p => SalaryOf(p, teamASignSalaries));
        var bSalaryOut = teamBSelected.Sum(p => SalaryOf(p, teamBSignSalaries));

        // En un S&T el FA firmado (presente en *SignSalaries) nunca estuvo en la
        // plantilla del equipo que firma: no descuenta roster ni nómina de ese lado,
        // pero sí cuenta como salario entrante para el otro lado (el nuevo contrato).
        bool aSignedFA(PlayerData p) => teamASignSalaries != null && teamASignSalaries.ContainsKey(p.id);
        bool bSignedFA(PlayerData p) => teamBSignSalaries != null && teamBSignSalaries.ContainsKey(p.id);
        int aOutCount = teamASelected.Count(p => !aSignedFA(p));
        int bOutCount = teamBSelected.Count(p => !bSignedFA(p));
        int aStandardOut = teamASelected.Count(p => !aSignedFA(p) && p.is_on_ir == 0 && p.is_two_way != 1);
        int bStandardOut = teamBSelected.Count(p => !bSignedFA(p) && p.is_on_ir == 0 && p.is_two_way != 1);
        int aTwoWayOut = teamASelected.Count(p => !aSignedFA(p) && p.is_two_way == 1);
        int bTwoWayOut = teamBSelected.Count(p => !bSignedFA(p) && p.is_two_way == 1);
        int aStandardIn = teamBSelected.Count(p => bSignedFA(p) || p.is_two_way != 1);
        int bStandardIn = teamASelected.Count(p => aSignedFA(p) || p.is_two_way != 1);
        int aTwoWayIn = teamBSelected.Count(p => !bSignedFA(p) && p.is_two_way == 1);
        int bTwoWayIn = teamASelected.Count(p => !aSignedFA(p) && p.is_two_way == 1);
        long aRosterOut = teamASelected.Where(p => teamASignSalaries == null || !teamASignSalaries.ContainsKey(p.id)).Sum(p => p.salary);
        long bRosterOut = teamBSelected.Where(p => teamBSignSalaries == null || !teamBSignSalaries.ContainsKey(p.id)).Sum(p => p.salary);

        int aAfter = teamATotalRoster - aStandardOut + aStandardIn;
        int bAfter = teamBTotalRoster - bStandardOut + bStandardIn;
        int aTwoWayAfter = teamATwoWayCount - aTwoWayOut + aTwoWayIn;
        int bTwoWayAfter = teamBTwoWayCount - bTwoWayOut + bTwoWayIn;
        int aActiveAfter = (teamATotalActiveRoster >= 0 ? teamATotalActiveRoster : teamATotalRoster + teamATwoWayCount)
            - teamASelected.Count(p => !aSignedFA(p) && p.is_on_ir == 0) + teamBSelected.Count;
        int bActiveAfter = (teamBTotalActiveRoster >= 0 ? teamBTotalActiveRoster : teamBTotalRoster + teamBTwoWayCount)
            - teamBSelected.Count(p => !bSignedFA(p) && p.is_on_ir == 0) + teamASelected.Count;

        if (aActiveAfter < 10) errors.Add($"{(teamAName ?? "Tu equipo")} tendría solo {aActiveAfter} jugadores (mínimo 10)");
        if (aAfter > MAX_STANDARD_ROSTER) errors.Add($"{(teamAName ?? "Tu equipo")} tendría {aAfter} plazas estándar (máximo {MAX_STANDARD_ROSTER})");
        if (aTwoWayAfter > MAX_TWO_WAY) errors.Add($"{(teamAName ?? "Tu equipo")} tendría {aTwoWayAfter} contratos two-way (máximo {MAX_TWO_WAY})");
        if (bActiveAfter < 10) errors.Add($"{teamBName} tendría solo {bActiveAfter} jugadores (mínimo 10)");
        if (bAfter > MAX_STANDARD_ROSTER) errors.Add($"{teamBName} tendría {bAfter} plazas estándar (máximo {MAX_STANDARD_ROSTER})");
        if (bTwoWayAfter > MAX_TWO_WAY) errors.Add($"{teamBName} tendría {bTwoWayAfter} contratos two-way (máximo {MAX_TWO_WAY})");

        if (!string.IsNullOrEmpty(teamAName))
        {
            var aPayroll = teamACurrentPayroll - aRosterOut + bSalaryOut;
            ValidateTradeSide(aSalaryOut, bSalaryOut, aOutCount, teamBSelected.Count,
                aPayroll, teamAName, teamAHardCapped, errors, firstApron, secondApron);
        }

        var bPayroll = teamBCurrentPayroll - bRosterOut + aSalaryOut;
        ValidateTradeSide(bSalaryOut, aSalaryOut, bOutCount, teamASelected.Count,
            bPayroll, teamBName, teamBHardCapped, errors, firstApron, secondApron);

        return errors;
    }

    static readonly (long bracket, double rate)[] TaxBrackets = new (long, double)[]
    {
        (5_000_000, 1.5),
        (5_000_000, 1.75),
        (5_000_000, 2.5),
        (5_000_000, 3.25),
        (long.MaxValue, 3.75),
    };

    /// <summary>
    /// ¿Puede un equipo IA fichar a un agente libre EXTERNO (sin Bird rights) por
    /// `newSalary`, dado su payroll actual? Modelo simplificado de excepciones, el
    /// mismo que sufre el jugador: espacio de cap → MLE (NT por debajo del 1er
    /// apron, T por debajo del 2º) → salario mínimo. Nunca deja el payroll por
    /// encima del 2º apron salvo con un contrato mínimo.
    /// </summary>
    /// <summary>
    /// ¿La renovación debe ir a valor de mercado en vez de al tope de derechos Bird?
    /// Caso típico: un jugador que firmó por el mínimo tras una rescisión (su valor de
    /// mercado es 3× o más que su salario actual). Para una estrella, eso es su máximo
    /// por experiencia; el resto de jugadores mantiene sus topes Bird.
    /// </summary>
    public static bool ShouldRenewAtMarket(long marketValue, long currentSalary)
        => currentSalary > 0 && marketValue >= currentSalary * 3;

    /// <summary>
    /// Valor de mercado por OVR como fracción del tope (85+ → 35 %, 80+ → 30 %, 75+ → 25 %).
    /// 0 si el jugador está por debajo de esos umbrales (su precio lo marca su salario).
    /// Fuente única para el precio de un agente libre al expirar y para las renovaciones IA.
    /// </summary>
    public static long MarketValueByOvr(int overall, long cap)
    {
        if (cap <= 0) return 0;
        if (overall >= 85) return (long)(cap * 0.35);
        if (overall >= 80) return (long)(cap * 0.30);
        if (overall >= 75) return (long)(cap * 0.25);
        return 0;
    }

    public static bool CanAISignExternal(long payroll, long newSalary, LeagueSettingsData s)
    {
        if (s == null) return true;
        long cap = s.salary_cap > 0 ? s.salary_cap : SALARY_CAP;
        long firstApron = s.apron > 0 ? s.apron : FIRST_APRON;
        long secondApron = s.repeater_apron > 0 ? s.repeater_apron : SECOND_APRON;
        long ntMle = s.mid_level > 0 ? s.mid_level : NT_MLE;
        long tMle = s.taxpayer_mid_level > 0 ? s.taxpayer_mid_level : T_MLE;
        long min = s.minimum_salary > 0 ? s.minimum_salary : MIN_SALARY;

        if (payroll + newSalary <= cap) return true;                          // espacio de cap
        if (payroll + newSalary > secondApron && newSalary > min) return false; // tope duro del 2º apron
        if (payroll <= firstApron) return newSalary <= ntMle;
        if (payroll <= secondApron) return newSalary <= tMle;
        return newSalary <= min;
    }

    /// <summary>
    /// ¿Puede un equipo IA RENOVAR a un jugador propio (Bird rights) por `newSalary`?
    /// La re-firma propia puede superar el cap, pero se limita al 2º apron para que
    /// las nóminas IA no se disparen a lo largo de muchas temporadas.
    /// </summary>
    public static bool CanAIRenew(long projectedPayroll, LeagueSettingsData s)
    {
        if (s == null) return true;
        long secondApron = s.repeater_apron > 0 ? s.repeater_apron : SECOND_APRON;
        return projectedPayroll <= secondApron;
    }

    public static long CalculateLuxuryTax(long payroll, long taxThreshold = LUXURY_TAX)
    {
        if (payroll <= taxThreshold) return 0;
        long excess = payroll - taxThreshold;
        long tax = 0;
        long remaining = excess;
        foreach (var (bracket, rate) in TaxBrackets)
        {
            long chunk = remaining > bracket ? bracket : remaining;
            tax += (long)(chunk * rate);
            remaining -= chunk;
            if (remaining <= 0) break;
        }
        return tax;
    }

}
