using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class InfoLeagueController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.InfoLeague;

    // Tabs
    private Button _btnTabFinanzas;
    private Button _btnTabEquipos;
    private VisualElement _tabFinanzas;
    private VisualElement _tabEquipos;

    // Finanzas
    private VisualElement _finanzasBody;

    // Equipos
    private VisualElement _teamsEast;
    private VisualElement _teamsWest;

    protected override void CacheReferences()
    {
        _btnTabFinanzas = _root.Q<Button>("BtnTabFinanzas");
        _btnTabEquipos = _root.Q<Button>("BtnTabEquipos");
        _tabFinanzas = _root.Q<VisualElement>("TabFinanzas");
        _tabEquipos = _root.Q<VisualElement>("TabEquipos");
        _finanzasBody = _root.Q<VisualElement>("FinanzasBody");
        _teamsEast = _root.Q<VisualElement>("TeamsEast");
        _teamsWest = _root.Q<VisualElement>("TeamsWest");
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _btnTabFinanzas?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectTab(0); });
        _btnTabEquipos?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectTab(1); });
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[InfoLeague] RefreshHeader error: {ex.Message}"); }
        SelectTab(0);
        BuildFinanzas();
        BuildEquipos();
    }

    void SelectTab(int index)
    {
        bool showFinanzas = index == 0;
        _btnTabFinanzas?.EnableInClassList("infoleague-tab--active", showFinanzas);
        _btnTabEquipos?.EnableInClassList("infoleague-tab--active", !showFinanzas);
        if (_tabFinanzas != null) _tabFinanzas.style.display = showFinanzas ? DisplayStyle.Flex : DisplayStyle.None;
        if (_tabEquipos != null) _tabEquipos.style.display = showFinanzas ? DisplayStyle.None : DisplayStyle.Flex;
    }

    /* ═══════════════════════════════════════════
       TAB 1: FINANZAS DE LA LIGA
       ═══════════════════════════════════════════ */

    void BuildFinanzas()
    {
        if (_finanzasBody == null) return;

        _finanzasBody.Clear();

        var settings = DatabaseManager.Instance?.GetLeagueSettings();
        if (settings == null) return;

        var rows = new (string label, long value)[]
        {
            ("LÍMITE SALARIAL", settings.salary_cap),
            ("LUXURY TAX", settings.luxury_tax),
            ("1er APRON", settings.apron),
            ("2º APRON", settings.repeater_apron),
            ("MID-LEVEL EXCEPTION (NO TAXPAYER)", settings.mid_level),
            ("MID-LEVEL EXCEPTION (TAXPAYER)", settings.taxpayer_mid_level),
            ("EXCEPCIÓN BI-ANUAL", settings.bi_annual),
            ("SALARIO MÍNIMO", settings.minimum_salary),
        };

        foreach (var row in rows)
        {
            var item = new VisualElement();
            item.AddToClassList("infoleague-finanza-row");

            var label = new Label(row.label);
            label.AddToClassList("infoleague-finanza-label");

            var value = new Label($"{row.value:N0} $");
            value.AddToClassList("infoleague-finanza-value");

            item.Add(label);
            item.Add(value);
            _finanzasBody.Add(item);
        }
    }

    /* ═══════════════════════════════════════════
       TAB 2: EQUIPOS POR CONFERENCIA
       ═══════════════════════════════════════════ */

    void BuildEquipos()
    {
        if (_teamsEast == null || _teamsWest == null) return;

        _teamsEast.Clear();
        _teamsWest.Clear();

        var settings = DatabaseManager.Instance?.GetLeagueSettings();
        long salaryCap = settings?.salary_cap ?? TradeHelper.SALARY_CAP;

        var payrolls = DatabaseManager.Instance?.GetTeamPayrolls() ?? new Dictionary<int, long>();

        BuildTeamColumn(_teamsEast, "East", salaryCap, payrolls);
        BuildTeamColumn(_teamsWest, "West", salaryCap, payrolls);
    }

    void BuildTeamColumn(VisualElement container, string conference, long salaryCap, Dictionary<int, long> payrolls)
    {
        var teams = DatabaseManager.Instance.GetTeamsByConference(conference);
        foreach (var team in teams)
        {
            var row = new VisualElement();
            row.AddToClassList("infoleague-team-row");
            if (_myTeam != null && team.id == _myTeam.id)
                row.AddToClassList("infoleague-team-row--mine");

            var logo = new VisualElement();
            logo.AddToClassList("infoleague-team-logo");
            var tex = Resources.Load<Sprite>($"Teams/Logos/32x32/{team.logo}");
            if (tex != null)
                logo.style.backgroundImage = new StyleBackground(tex);

            var name = new Label(team.name);
            name.AddToClassList("infoleague-team-name");

            long payroll = payrolls.TryGetValue(team.id, out long p) ? p : 0;
            long margin = salaryCap - payroll;

            var budgetLabel = new Label("PRESUPUESTO");
            budgetLabel.AddToClassList("infoleague-team-stat-label");

            var budget = new Label($"{team.budget:N0} $");
            budget.AddToClassList("infoleague-team-stat");
            budget.AddToClassList("infoleague-team-budget");

            var marginLabelText = new Label("MARGEN SALARIAL");
            marginLabelText.AddToClassList("infoleague-team-stat-label");
            marginLabelText.AddToClassList("infoleague-team-stat-label--spaced");

            var marginLabel = new Label($"{(margin >= 0 ? "+" : "-")}{System.Math.Abs(margin):N0} $");
            marginLabel.AddToClassList("infoleague-team-stat");
            marginLabel.AddToClassList(margin >= 0 ? "infoleague-team-margin--positive" : "infoleague-team-margin--negative");

            row.Add(logo);
            row.Add(name);
            row.Add(budgetLabel);
            row.Add(budget);
            row.Add(marginLabelText);
            row.Add(marginLabel);
            container.Add(row);
        }
    }
}