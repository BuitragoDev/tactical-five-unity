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
        try
        {
            var template = GetTemplate();
            if (template == null)
            {
                Debug.LogWarning("[Header] Header.uxml not found in Resources/UI/Core/Header");
                return;
            }

            var container = root.childCount > 0 ? root[0] : root;
            if (container == null) container = root;

            var contentWrapper = container.childCount > 1 ? container[1] : container;

            var header = template.CloneTree();
            contentWrapper.Insert(0, header);

            Populate(header);

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
        catch (System.Exception ex)
        {
            Debug.LogError($"[Header] Error in Attach: {ex.Message}\n{ex.StackTrace}");
        }
    }

    static void Populate(VisualElement header)
    {
        var manager = DatabaseManager.Instance?.GetActiveManager();
        if (manager == null) return;
        var myTeam = DatabaseManager.Instance?.GetTeamById(manager.team_id);
        if (myTeam == null) return;
        var season = DatabaseManager.Instance?.GetActiveSeason(manager.id);
        if (season == null) return;

        var players = DatabaseManager.Instance.GetPlayersByTeam(myTeam.id);
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();

        SafeSetText(header, "HeaderTeamName", myTeam.name.ToUpper());
        SafeSetText(header, "HeaderManagerName", $"Manager: {manager.name}");

        long totalPayroll = players != null ? players.Sum(p => p.salary) : 0;

        var budget = header.Q<Label>("HeaderBudget");
        if (budget != null)
        {
            budget.text = $"${myTeam.budget / 1_000_000}M";
            budget.style.color = myTeam.budget < 0
                ? new StyleColor(new Color32(192, 57, 43, 255))
                : new StyleColor(new Color32(39, 174, 96, 255));
        }

        SafeSetText(header, "HeaderPayroll", $"${totalPayroll / 1_000_000}M");

        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - totalPayroll;
        var marginLabel = header.Q<Label>("HeaderMargin");
        if (marginLabel != null)
        {
            marginLabel.text = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
            marginLabel.RemoveFromClassList("header-stat-value--negative");
            if (margin < 0) marginLabel.AddToClassList("header-stat-value--negative");
        }

        int chemistry = myTeam.id > 0 ? DatabaseManager.Instance.GetTeamChemistry(myTeam.id) : 50;
        var chemLabel = header.Q<Label>("HeaderChemistry");
        if (chemLabel != null)
        {
            chemLabel.text = $"{chemistry}%";
            chemLabel.RemoveFromClassList("header-stat-value--gold");
            chemLabel.RemoveFromClassList("header-stat-value--negative");
            if (chemistry < 40) chemLabel.AddToClassList("header-stat-value--negative");
            else if (chemistry < 70) chemLabel.AddToClassList("header-stat-value--gold");
        }

        SafeSetText(header, "HeaderSeason", $"Temporada {season.year_start}-{season.year_end}");
        SafeSetText(header, "HeaderDate", DatabaseManager.Instance.GetCurrentDateString(manager.id));
    }

    static void LoadTeamLogo(VisualElement header)
    {
        var manager = DatabaseManager.Instance?.GetActiveManager();
        if (manager == null) return;
        var myTeam = DatabaseManager.Instance?.GetTeamById(manager.team_id);
        if (myTeam == null) return;

        var logo = header.Q<VisualElement>("HeaderTeamLogo");
        if (logo == null) return;
        var tex = Resources.Load<Sprite>($"Teams/Logos/64x64/{myTeam.logo}");
        if (tex != null)
            logo.style.backgroundImage = new StyleBackground(tex);
    }

    static void SafeSetText(VisualElement parent, string elementName, string text)
    {
        var label = parent.Q<Label>(elementName);
        if (label != null)
            label.text = text;
    }

    static void PlayClick()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX("click");
    }
}
