using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

public class PremiosController : MonoBehaviour
{
    VisualElement _root;
    VisualElement _body;
    ManagerData _manager;
    SeasonData _season;
    TeamData _myTeam;

    void OnEnable()
    {
        _root = GetComponent<UIDocument>()?.rootVisualElement;
        if (_root == null) return;

        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        FixMonthNames();

        _manager = DatabaseManager.Instance?.GetActiveManager();
        _season = DatabaseManager.Instance?.GetActiveSeason(_manager?.id ?? 0);
        _myTeam = DatabaseManager.Instance?.GetTeamById(_manager?.team_id ?? 0);

        _body = _root.Q<VisualElement>("PremiosBody");

        RegisterNavButtons();
        Refresh();
    }

    void FixMonthNames()
    {
        if (!DatabaseManager.Instance.EnsureDb()) return;
        DatabaseManager.Instance.Db.Execute(@"
            UPDATE monthly_awards
            SET month_name =
              CASE month_name
                WHEN 'diciembre' THEN 'noviembre'
                WHEN 'enero'     THEN 'diciembre'
                WHEN 'febrero'   THEN 'enero'
                WHEN 'marzo'     THEN 'febrero'
                WHEN 'abril'     THEN 'marzo'
                WHEN 'mayo'      THEN 'abril'
                ELSE month_name
              END
        ");
    }

    void RegisterNavButtons()
    {
        SidebarController.Attach(_root, GameScreen.Premios);
        HeaderController.Attach(_root);

        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Roster); });
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Calendar); });
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Palmares); });
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Results); });
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Playoffs); });
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Stats); });
        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Market); });
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Finances); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavManager")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Manager); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
    }

    void PlayClick()
    {
        var audio = GetComponent<AudioSource>();
        if (audio != null) audio.Play();
    }

    void Refresh()
    {
        if (_season == null || _body == null) return;

        var title = _root.Q<Label>("PremiosTitle");
        if (title != null)
            title.text = $"PREMIOS MENSUALES {_season.year_start}-{_season.year_end}";

        _body.Clear();

        var awards = DatabaseManager.Instance.GetMonthlyAwardsForSeason(_season.id);
        var grouped = awards.GroupBy(a => a.month_name);

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

        BuildCard(row, "MANAGER DEL MES",
            managerWinner != null ? managerWinner.team_name : null,
            managerWinner != null ? managerWinner.team_name : null,
            managerWinner != null ? (managerWinner.value * 100).ToString("F1", CultureInfo.InvariantCulture) + "%" : null);

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
