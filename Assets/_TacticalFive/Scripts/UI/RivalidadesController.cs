using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pantalla RIVALIDADES (submenú COMPETICIONES): dinastías de la liga y rivalidades de
/// playoffs de mi equipo (movida desde la pestaña RIVALIDADES de Palmarés).
/// </summary>
public class RivalidadesController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Rivalidades;

    private VisualElement _dinastiasBody, _rivalidadesBody;
    private CustomScrollbar.Handle _dinastiasBar, _rivalidadesBar;

    private List<TeamData> _allTeams;
    private Dictionary<string, Sprite> _logoSprites64 = new();

    protected override void CacheReferences()
    {
        _dinastiasBody = _root.Q<VisualElement>("DinastiasBody");
        _rivalidadesBody = _root.Q<VisualElement>("RivalidadesBody");
    }

    protected override void LoadData()
    {
        base.LoadData();

        var icon = _root.Q<VisualElement>("RivalidadesScreenIcon");
        if (icon != null)
        {
            var tex = Resources.Load<Texture2D>("Icons/competiciones");
            if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
        }

        _logoSprites64.Clear();
        foreach (var s in TeamImages.Logos(64)) _logoSprites64[s.name] = s;
        _allTeams = DatabaseManager.Instance.GetAllTeams();
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Rivalidades] RefreshHeader error: {ex.Message}"); }
        BuildDinastiasList();
        BuildRivalidadesList();

        _dinastiasBar = CustomScrollbar.Bind(
            _root.Q<VisualElement>("DinastiasViewport"), _dinastiasBody,
            _root.Q<VisualElement>("DinastiasTrack"), _root.Q<VisualElement>("DinastiasThumb"));
        _rivalidadesBar = CustomScrollbar.Bind(
            _root.Q<VisualElement>("RivalidadesViewport"), _rivalidadesBody,
            _root.Q<VisualElement>("RivalidadesTrack"), _root.Q<VisualElement>("RivalidadesThumb"));

        _root.Q<VisualElement>("CompetitionsSubmenu")?.AddToClassList("nav-submenu--visible");
        _root.Q<Button>("SubmenuRivalidades")?.AddToClassList("nav-submenu-item--active");
    }

    /// <summary>Ranking de prestigio de la liga (títulos/finales/conferencias por equipo).</summary>
    void BuildDinastiasList()
    {
        if (_dinastiasBody == null || _manager == null || _myTeam == null) return;
        _dinastiasBody.Clear();

        var ranking = DynastyHelper.LeagueRanking(DatabaseManager.Instance.GetAllSeasonRecords(), 12);
        if (ranking.Count == 0)
        {
            _dinastiasBody.Add(EmptyRivalLabel("Todavía no hay títulos registrados."));
            return;
        }

        foreach (var p in ranking)
        {
            var team = _allTeams?.FirstOrDefault(t => t.id == p.teamId);

            var row = new VisualElement();
            row.AddToClassList("rival-row");

            var logo = new VisualElement();
            logo.AddToClassList("rival-logo");
            if (team != null && _logoSprites64.TryGetValue(team.logo, out var sp))
                logo.style.backgroundImage = new StyleBackground(sp);
            row.Add(logo);

            var col = new VisualElement();
            col.AddToClassList("rival-col");

            var name = new Label(team?.name ?? $"Equipo {p.teamId}");
            name.AddToClassList("rival-title");
            col.Add(name);

            var stats = new VisualElement();
            stats.AddToClassList("rival-stats");
            stats.Add(MakeStatBadge($"{p.titles} TÍTULOS", "rival-stat--titles"));
            stats.Add(MakeStatBadge($"{p.finals} FINALES", "rival-stat--finals"));
            col.Add(stats);

            row.Add(col);

            var badge = new Label(p.isDynasty ? "DINASTÍA" : $"PRESTIGIO {p.prestige}");
            badge.AddToClassList("rival-badge");
            if (p.isDynasty) badge.AddToClassList("rival-badge--hot");
            row.Add(badge);

            _dinastiasBody.Add(row);
        }
    }

    static Label MakeStatBadge(string text, string colorClass)
    {
        var lbl = new Label(text);
        lbl.AddToClassList("rival-stat-badge");
        lbl.AddToClassList(colorClass);
        return lbl;
    }

    /// <summary>Rivales de mi equipo en playoffs (balance de series y último cruce).</summary>
    void BuildRivalidadesList()
    {
        if (_rivalidadesBody == null || _manager == null || _myTeam == null) return;
        _rivalidadesBody.Clear();

        var series = DatabaseManager.Instance.GetPlayoffSeriesOfTeam(_manager.id, _myTeam.id);
        var rivals = RivalryHelper.RivalsOf(series, _myTeam.id, _season?.id ?? 0);

        if (rivals.Count == 0)
        {
            _rivalidadesBody.Add(EmptyRivalLabel("Sin cruces de playoffs todavía: las rivalidades aparecerán aquí."));
            return;
        }

        var seasons = DatabaseManager.Instance.GetAllSeasons();

        foreach (var r in rivals)
        {
            var team = _allTeams?.FirstOrDefault(t => t.id == r.opponentId);

            var row = new VisualElement();
            row.AddToClassList("rival-row");

            var logo = new VisualElement();
            logo.AddToClassList("rival-logo");
            if (team != null && _logoSprites64.TryGetValue(team.logo, out var sp))
                logo.style.backgroundImage = new StyleBackground(sp);
            row.Add(logo);

            var col = new VisualElement();
            col.AddToClassList("rival-col");

            var name = new Label(team?.name ?? $"Equipo {r.opponentId}");
            name.AddToClassList("rival-title");
            col.Add(name);

            string lastYear = "";
            var lastSeason = seasons?.FirstOrDefault(x => x.id == r.lastSeasonId);
            if (lastSeason != null)
                lastYear = $" {lastSeason.year_start}-{lastSeason.year_end % 100:D2}";

            var meta = new Label(
                $"{r.series} series · {r.wins}-{r.losses}"
                + (string.IsNullOrEmpty(r.lastStage) ? "" : $" · último: {StageLabel(r.lastStage)}{lastYear} ({RivalryResultLabel(r.lastWon)})"));
            meta.AddToClassList("rival-meta");
            col.Add(meta);
            row.Add(col);

            var badge = new Label(IntensityLabel(r.intensity));
            badge.AddToClassList("rival-badge");
            if (r.intensity >= 30) badge.AddToClassList("rival-badge--hot");
            row.Add(badge);

            _rivalidadesBody.Add(row);
        }
    }

    static string StageLabel(string stage) => stage switch
    {
        "r1" => "1ª ronda",
        "r2" => "2ª ronda",
        "r3" => "final de conferencia",
        "r4" => "FINALES",
        _ => stage ?? ""
    };

    static string RivalryResultLabel(bool won) => won ? "ganamos" : "eliminados";

    static string IntensityLabel(int intensity)
        => intensity >= 30 ? "RIVALIDAD FUERTE" : intensity >= 15 ? "RIVALIDAD" : "CRUCE";

    static Label EmptyRivalLabel(string text)
    {
        var lbl = new Label(text);
        lbl.AddToClassList("rival-empty");
        return lbl;
    }

    protected override void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites64.TryGetValue(_myTeam.logo, out var sprite))
            _root.Q<VisualElement>("HeaderTeamLogo").style.backgroundImage = new StyleBackground(sprite);

        _root.Q<Label>("HeaderTeamName").text = _myTeam.name.ToUpper();
        _root.Q<Label>("HeaderManagerName").text = LocalizationService.T("ui.common.manager", _manager.name);
        var budgetLabel = _root.Q<Label>("HeaderBudget");
        budgetLabel.text = $"${_myTeam.budget / 1_000_000}M";
        budgetLabel.style.color = _myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));

        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        long totalPayroll = players.Sum(p => p.salary);
        _root.Q<Label>("HeaderPayroll").text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - totalPayroll;
        var marginLbl = _root.Q<Label>("HeaderMargin");
        marginLbl.text = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        marginLbl.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) marginLbl.AddToClassList("header-stat-value--negative");

        var chemLabel = _root.Q<Label>("HeaderChemistry");
        if (chemLabel != null)
        {
            int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
            chemLabel.text = $"{chemistry}%";
            chemLabel.RemoveFromClassList("header-stat-value--gold");
            chemLabel.RemoveFromClassList("header-stat-value--negative");
            if (chemistry < 40) chemLabel.AddToClassList("header-stat-value--negative");
            else if (chemistry < 70) chemLabel.AddToClassList("header-stat-value--gold");
        }

        if (_season != null)
        {
            _root.Q<Label>("HeaderSeason").text = LocalizationService.T("ui.common.season", _season.year_start, _season.year_end);
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = LocalizationService.T("ui.code.rivalidades_controller.menu_principal");
    }
}
