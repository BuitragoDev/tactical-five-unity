using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class TradeHelper
{
    public const long SALARY_CAP = 154_647_000;
    public const long LUXURY_TAX = 200_428_000;
    public const long FIRST_APRON = 209_015_000;
    public const long SECOND_APRON = 221_686_000;
    public const long NT_MLE = 14_100_000;
    public const long T_MLE = 5_700_000;
    public const long MIN_SALARY = 2_000_000;
    public const int MAX_ROSTER = 17;

    static void ValidateTradeSide(
        long salaryOut, long salaryIn, int playersInCount,
        long newPayroll, string teamName, bool hardCappedToFirstApron,
        List<string> errors)
    {
        if (newPayroll > SECOND_APRON || (hardCappedToFirstApron && newPayroll > FIRST_APRON))
        {
            if (playersInCount > 1)
                errors.Add($"{teamName} está en el {(hardCappedToFirstApron ? "hard cap del primer apron" : "segundo apron")}. No puede recibir múltiples jugadores.");
            if (salaryOut < salaryIn)
                errors.Add($"{teamName} está en el {(hardCappedToFirstApron ? "hard cap del primer apron" : "segundo apron")}. Solo puede recibir salario ≤ al que envía.");
        }
        else if (newPayroll > FIRST_APRON)
        {
            var maxReceive = (long)(salaryOut * 1.10);
            if (salaryIn > maxReceive + 250_000)
                errors.Add($"{teamName} está en el primer apron. Solo puede recibir hasta el 110% del salario que envía.");
        }
        else
        {
            long maxReceive;
            if (salaryOut < 7_500_000)
                maxReceive = salaryOut * 2 + 250_000;
            else if (salaryOut < 29_000_000)
                maxReceive = salaryOut + 7_500_000;
            else
                maxReceive = (long)(salaryOut * 1.25 + 250_000);

            if (salaryIn > maxReceive + 250_000)
                errors.Add($"{teamName} no puede recibir más de ${maxReceive:N0}.");
        }
    }

    public static int PickBonus(DraftPickData pk)
    {
        if (pk == null) return 0;
        if (pk.round == 1)
        {
            int slot = Mathf.Clamp(pk.pick_number, 1, 30);
            return 10 + (30 - slot) / 3;
        }
        if (pk.round == 2)
        {
            int slot = Mathf.Clamp(pk.pick_number - 30, 1, 30);
            return 5 + (30 - slot) / 5;
        }
        return 3;
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
        bool teamBHardCapped = false)
    {
        var errors = new List<string>();

        var aSalaryOut = teamASelected.Sum(p => p.salary);
        var bSalaryOut = teamBSelected.Sum(p => p.salary);

        var aAfter = teamATotalRoster - teamASelected.Count + teamBSelected.Count;
        var bAfter = teamBTotalRoster - teamBSelected.Count + teamASelected.Count;

        if (aAfter < 10) errors.Add($"{(teamAName ?? "Tu equipo")} tendría solo {aAfter} jugadores (mínimo 10)");
        if (aAfter > MAX_ROSTER) errors.Add($"{(teamAName ?? "Tu equipo")} tendría {aAfter} jugadores (máximo {MAX_ROSTER})");
        if (bAfter < 10) errors.Add($"{teamBName} tendría solo {bAfter} jugadores (mínimo 10)");
        if (bAfter > MAX_ROSTER) errors.Add($"{teamBName} tendría {bAfter} jugadores (máximo {MAX_ROSTER})");

        if (!string.IsNullOrEmpty(teamAName))
        {
            var aPayroll = teamACurrentPayroll - aSalaryOut + bSalaryOut;
            ValidateTradeSide(aSalaryOut, bSalaryOut, teamBSelected.Count, aPayroll, teamAName, teamAHardCapped, errors);
        }

        var bPayroll = teamBCurrentPayroll - bSalaryOut + aSalaryOut;
        ValidateTradeSide(bSalaryOut, aSalaryOut, teamASelected.Count, bPayroll, teamBName, teamBHardCapped, errors);

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

    public static long CalculateLuxuryTax(long payroll)
    {
        if (payroll <= LUXURY_TAX) return 0;
        long excess = payroll - LUXURY_TAX;
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

    public static TradeResult EvaluateTrade(
        List<PlayerData> teamASelected,
        List<PlayerData> teamBSelected,
        string teamBName,
        int teamBTotalRoster,
        long teamBCurrentPayroll,
        List<DraftPickData> teamASelectedPicks = null,
        List<DraftPickData> teamBSelectedPicks = null)
    {
        var aSalaryOut = teamASelected.Sum(p => p.salary);
        var bSalaryOut = teamBSelected.Sum(p => p.salary);

        var bBestOvr = teamBSelected.Count > 0 ? teamBSelected.Max(p => p.overall) : 0;
        var aBestOvr = teamASelected.Count > 0 ? teamASelected.Max(p => p.overall) : 0;
        var bAvgOvr = teamBSelected.Count > 0 ? teamBSelected.Average(p => p.overall) : 0;
        var aAvgOvr = teamASelected.Count > 0 ? teamASelected.Average(p => p.overall) : 0;
        var aTotalOvr = teamASelected.Sum(p => p.overall);
        var bTotalOvr = teamBSelected.Sum(p => p.overall);

        int acceptScore = 0;

        // Picks as sweetener: B receives A's picks, A receives B's picks
        if (teamASelectedPicks != null)
        {
            foreach (var pk in teamASelectedPicks)
                acceptScore += PickBonus(pk);
        }
        if (teamBSelectedPicks != null)
        {
            foreach (var pk in teamBSelectedPicks)
                acceptScore -= PickBonus(pk);
        }

        // Player quality comparison
        if (bBestOvr >= 90)
        {
            if (aBestOvr >= 90) acceptScore += 40 + (aBestOvr - bBestOvr) * 3;
            else if (aBestOvr >= 85) acceptScore += 15 + (aBestOvr - bBestOvr) * 2;
            else acceptScore -= 50;
        }
        else if (bBestOvr >= 85)
        {
            if (aBestOvr >= 85) acceptScore += 30 + (aBestOvr - bBestOvr) * 2;
            else if (aBestOvr >= 80) acceptScore += 10;
            else acceptScore -= 30;
        }
        else if (bBestOvr >= 80)
        {
            if (aBestOvr >= 80) acceptScore += 20;
            else if (aBestOvr >= 75) acceptScore += 5;
            else acceptScore -= 15;
        }
        else
        {
            if (aAvgOvr >= bAvgOvr) acceptScore += 10;
            else acceptScore -= 10;
        }

        // Total OVR comparison
        acceptScore += Mathf.Clamp(aTotalOvr - bTotalOvr, -20, 20);

        // Financial situation
        if (teamBCurrentPayroll > SECOND_APRON)
        {
            if (aSalaryOut > bSalaryOut) acceptScore += 30;
            else acceptScore -= 20;
        }
        else if (teamBCurrentPayroll > FIRST_APRON)
        {
            if (aSalaryOut > bSalaryOut) acceptScore += 20;
            else acceptScore -= 10;
        }
        else if (teamBCurrentPayroll > LUXURY_TAX)
        {
            if (aSalaryOut > bSalaryOut) acceptScore += 15;
            else if (aSalaryOut < bSalaryOut) acceptScore -= 5;
        }
        else
        {
            if (aSalaryOut > bSalaryOut) acceptScore += 5;
            else if (aSalaryOut < bSalaryOut) acceptScore -= 5;
        }

        // Team needs
        var bAfter = teamBTotalRoster - teamBSelected.Count + teamASelected.Count;
        if (bAfter <= 12) acceptScore += 15;
        else if (bAfter <= 14) acceptScore += 5;

        // Age factor
        if (teamASelected.Count > 0 && teamBSelected.Count > 0)
        {
            var aAvgAge = teamASelected.Average(p => p.age);
            var bAvgAge = teamBSelected.Average(p => p.age);
            if (aAvgAge < bAvgAge - 3) acceptScore += 10;
            else if (aAvgAge > bAvgAge + 3) acceptScore -= 5;
        }

        // Randomness
        acceptScore += Random.Range(-5, 6);

        acceptScore = Mathf.Clamp(acceptScore, 0, 100);

        var threshold = 50;
        if (teamBCurrentPayroll > SECOND_APRON) threshold = 40;
        else if (teamBCurrentPayroll > FIRST_APRON) threshold = 45;

        return new TradeResult
        {
            WouldAccept = acceptScore >= threshold,
            AcceptScore = acceptScore,
            Threshold = threshold,
            TeamASelected = teamASelected,
            TeamBSelected = teamBSelected
        };
    }
}

public class TradeResult
{
    public bool WouldAccept;
    public int AcceptScore;
    public int Threshold;
    public List<PlayerData> TeamASelected;
    public List<PlayerData> TeamBSelected;
}
