using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

public static class HeaderController
{
    static VisualTreeAsset _headerTemplate;

    static VisualTreeAsset GetTemplate()
    {
        if (_headerTemplate == null)
            _headerTemplate = Resources.Load<VisualTreeAsset>("UI/Core/Header");
        return _headerTemplate;
    }

    public static void Attach(VisualElement root)
    {
        var template = GetTemplate();
        if (template == null)
        {
            Debug.LogError("[Header] Cannot load Header.uxml from Resources/UI/Core/Header");
            return;
        }

        var container = root.childCount > 0 ? root[0] : root;
        if (container == null) container = root;

        var header = template.CloneTree();
        container.Insert(1, header);

        Refresh(header);

        var btnAction = header.Q<Button>("BtnAction");
        if (btnAction != null)
            btnAction.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });

        var configIcon = header.Q<VisualElement>("ConfigIcon");
        if (configIcon != null)
            configIcon.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Settings); });

        LoadTeamLogo(header);

        if (CursorManager.Instance != null)
        {
            if (btnAction != null) CursorManager.Instance.RegisterHandCursor(btnAction);
            if (configIcon != null) CursorManager.Instance.RegisterHandCursor(configIcon);
        }
    }

    static void Refresh(VisualElement header)
    {
        var manager = DatabaseManager.Instance.GetActiveManager();
        if (manager == null) return;
        var myTeam = DatabaseManager.Instance.GetTeamById(manager.team_id);
        if (myTeam == null) return;
        var season = DatabaseManager.Instance.GetActiveSeason(manager.id);
        if (season == null) return;

        var players = DatabaseManager.Instance.GetPlayersByTeam(myTeam.id);
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();

        header.Q<Label>("HeaderTeamName").text = myTeam.name.ToUpper();
        header.Q<Label>("HeaderManagerName").text = $"Manager: {manager.name}";

        long totalPayroll = players.Sum(p => p.salary);

        var budget = header.Q<Label>("HeaderBudget");
        budget.text = $"${myTeam.budget / 1_000_000}M";
        budget.style.color = myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));

        header.Q<Label>("HeaderPayroll").text = $"${totalPayroll / 1_000_000}M";

        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - totalPayroll;
        var marginLabel = header.Q<Label>("HeaderMargin");
        marginLabel.text = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        marginLabel.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) marginLabel.AddToClassList("header-stat-value--negative");

        int chemistry = DatabaseManager.Instance.GetTeamChemistry(myTeam.id);
        var chemLabel = header.Q<Label>("HeaderChemistry");
        chemLabel.text = $"{chemistry}%";
        chemLabel.RemoveFromClassList("header-stat-value--gold");
        chemLabel.RemoveFromClassList("header-stat-value--negative");
        if (chemistry < 40) chemLabel.AddToClassList("header-stat-value--negative");
        else if (chemistry < 70) chemLabel.AddToClassList("header-stat-value--gold");

        header.Q<Label>("HeaderSeason").text = $"Temporada {season.year_start}-{season.year_end}";
        header.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetCurrentDateString(manager.id);
    }

    static void LoadTeamLogo(VisualElement header)
    {
        var manager = DatabaseManager.Instance.GetActiveManager();
        if (manager == null) return;
        var myTeam = DatabaseManager.Instance.GetTeamById(manager.team_id);
        if (myTeam == null) return;

        var logo = header.Q<VisualElement>("HeaderTeamLogo");
        var tex = Resources.Load<Sprite>($"Teams/Logos/64x64/{myTeam.logo}");
        if (tex != null)
            logo.style.backgroundImage = new StyleBackground(tex);
    }

    static void PlayClick()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX("click");
    }
}
