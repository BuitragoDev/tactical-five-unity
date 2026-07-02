using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class TradeHelper
{
    public const long SALARY_CAP = 164_961_000;
    public const long LUXURY_TAX = 200_428_000;
    public const long FIRST_APRON = 209_015_000;
    public const long SECOND_APRON = 221_686_000;
    public const int MAX_ROSTER = 18;

    public static List<string> ValidateTrade(
        List<PlayerData> teamASelected,
        List<PlayerData> teamBSelected,
        int teamATotalRoster,
        int teamBTotalRoster,
        string teamBName,
        long teamBCurrentPayroll)
    {
        var errors = new List<string>();

        var aSalaryOut = teamASelected.Sum(p => p.salary);
        var bSalaryOut = teamBSelected.Sum(p => p.salary);

        var aAfter = teamATotalRoster - teamASelected.Count + teamBSelected.Count;
        var bAfter = teamBTotalRoster - teamBSelected.Count + teamASelected.Count;

        if (aAfter < 10) errors.Add($"El equipo tendría solo {aAfter} jugadores (mínimo 10)");
        if (aAfter > MAX_ROSTER) errors.Add($"El equipo tendría {aAfter} jugadores (máximo {MAX_ROSTER})");
        if (bAfter < 10) errors.Add($"{teamBName} tendría solo {bAfter} jugadores (mínimo 10)");
        if (bAfter > MAX_ROSTER) errors.Add($"{teamBName} tendría {bAfter} jugadores (máximo {MAX_ROSTER})");

        var bPayroll = teamBCurrentPayroll - bSalaryOut + aSalaryOut;

        if (bPayroll > SECOND_APRON)
        {
            if (teamASelected.Count > 1)
                errors.Add($"{teamBName} está en el segundo apron. No pueden agregar salarios de múltiples jugadores.");
            if (bSalaryOut < aSalaryOut)
                errors.Add($"{teamBName} está en el segundo apron. Solo pueden recibir salario igual o menor al que envían.");
        }
        else if (bPayroll > FIRST_APRON)
        {
            var maxReceive = bSalaryOut * 1.10;
            if (aSalaryOut > maxReceive + 250_000)
                errors.Add($"{teamBName} está en el primer apron. Solo pueden recibir hasta el 110% del salario enviado.");
        }
        else
        {
            long maxReceive;
            if (bSalaryOut < 7_500_000)
                maxReceive = bSalaryOut * 2 + 250_000;
            else if (bSalaryOut < 29_000_000)
                maxReceive = bSalaryOut + 7_500_000;
            else
                maxReceive = (long)(bSalaryOut * 1.25 + 250_000);

            if (aSalaryOut > maxReceive + 250_000)
                errors.Add($"{teamBName} no puede recibir más de ${maxReceive:N0}.");
        }

        return errors;
    }

    public static TradeResult EvaluateTrade(
        List<PlayerData> teamASelected,
        List<PlayerData> teamBSelected,
        string teamBName,
        int teamBTotalRoster,
        long teamBCurrentPayroll)
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
