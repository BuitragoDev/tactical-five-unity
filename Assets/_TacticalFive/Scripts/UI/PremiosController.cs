using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

public class PremiosController : UIScreenController
{
    VisualElement _body;

    protected override GameScreen ScreenId => GameScreen.Premios;

    protected override void CacheReferences()
    {
        _body = _root.Q<VisualElement>("PremiosBody");
    }

    protected override void Refresh()
    {
        if (_season == null || _body == null) return;

        var title = _root.Q<Label>("PremiosTitle");
        if (title != null)
            title.text = $"PREMIOS MENSUALES {_season.year_start}-{_season.year_end}";

        _body.Clear();

        var awards = DatabaseManager.Instance.GetMonthlyAwardsForSeason(_season.id);

        var corrupted = awards.GroupBy(a => a.month_name).Where(g => g.Count() > 9).ToList();
        if (corrupted.Any())
        {
            var db = DatabaseManager.Instance.Db;
            foreach (var group in corrupted)
            {
                var ordered = group.OrderBy(a => a.id).ToList();
                var extraIds = ordered.Skip(9).Select(a => a.id).ToList();
                Debug.LogWarning($"[Premios] Mes '{group.Key}' tiene {group.Count()} entradas, limpiando {extraIds.Count} duplicados");
                db.Execute($"DELETE FROM monthly_awards WHERE id IN ({string.Join(",", extraIds)})");
            }
            awards = DatabaseManager.Instance.GetMonthlyAwardsForSeason(_season.id);
        }

        string[] monthOrder = { "Noviembre", "Diciembre", "Enero", "Febrero", "Marzo", "Abril" };

        var awardsList = awards.ToList();

        for (int i = 0; i < monthOrder.Length; i += 2)
        {
            var row = new VisualElement();
            row.AddToClassList("premios-months-row");

            BuildMonthColumn(row, monthOrder[i], awardsList);
            if (i + 1 < monthOrder.Length)
                BuildMonthColumn(row, monthOrder[i + 1], awardsList);

            _body.Add(row);
        }
    }

    void BuildMonthColumn(VisualElement row, string month, List<MonthlyAwardData> awards)
    {
        var section = new VisualElement();
        section.AddToClassList("premios-month-section");

        var monthLabel = new Label(month);
        monthLabel.AddToClassList("premios-month-title");
        section.Add(monthLabel);

        var monthAwards = awards.Where(a =>
            string.Equals(a.month_name, month, System.StringComparison.OrdinalIgnoreCase)).ToList();

        if (monthAwards.Count == 0)
        {
            var pending = new Label("Pendiente");
            pending.AddToClassList("premios-pending");
            section.Add(pending);
        }
        else
        {
            BuildWinnersRow(section, monthAwards);
        }

        row.Add(section);
    }

    void BuildWinnersRow(VisualElement section, IEnumerable<MonthlyAwardData> monthAwards)
    {
        var managerWinner = monthAwards.FirstOrDefault(a => a.award_type == "manager" && a.rank == 1);
        var playerWinner = monthAwards.FirstOrDefault(a => a.award_type == "player" && a.rank == 1);
        var rookieWinner = monthAwards.FirstOrDefault(a => a.award_type == "rookie" && a.rank == 1);

        var row = new VisualElement();
        row.AddToClassList("premios-winners-row");

        string managerName = null;
        if (managerWinner != null && managerWinner.team_id.HasValue)
        {
            managerName = DatabaseManager.Instance.GetManagerNameByTeamId(managerWinner.team_id.Value);
            if (string.IsNullOrEmpty(managerName))
                managerName = managerWinner.team_name;
        }

        BuildCard(row, "MANAGER DEL MES",
            managerName,
            managerWinner != null ? managerWinner.team_name : null,
            managerWinner != null ? (managerWinner.value * 100).ToString("F1", CultureInfo.InvariantCulture) + "% VIC" : null);

        BuildCard(row, "JUGADOR DEL MES",
            playerWinner != null ? playerWinner.player_name : null,
            playerWinner != null ? playerWinner.team_name : null,
            playerWinner != null ? playerWinner.value.ToString("F1", CultureInfo.InvariantCulture) + " VAL" : null);

        BuildCard(row, "ROOKIE DEL MES",
            rookieWinner != null ? rookieWinner.player_name : null,
            rookieWinner != null ? rookieWinner.team_name : null,
            rookieWinner != null ? rookieWinner.value.ToString("F1", CultureInfo.InvariantCulture) + " VAL" : null);

        section.Add(row);
    }

    void BuildCard(VisualElement row, string label, string name, string team, string stat)
    {
        var card = new VisualElement();
        card.AddToClassList("premios-card");

        var catLabel = new Label(label);
        catLabel.AddToClassList("premios-card-label");
        card.Add(catLabel);

        if (name != null)
        {
            var nameLabel = new Label(name);
            nameLabel.AddToClassList("premios-card-name");
            card.Add(nameLabel);

            var teamLabel = new Label(team);
            teamLabel.AddToClassList("premios-card-team");
            card.Add(teamLabel);

            var statLabel = new Label(stat);
            statLabel.AddToClassList("premios-card-stat");
            card.Add(statLabel);
        }
        else
        {
            var emptyLabel = new Label("—");
            emptyLabel.AddToClassList("premios-card-empty");
            card.Add(emptyLabel);
        }

        row.Add(card);
    }
}
