using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
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

    public static void Attach(VisualElement root, bool registerBtnAction = true)
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

            // If header already exists, just repopulate it (idempotent)
            var existingHeader = container.Q<VisualElement>("TopHeader");
            if (existingHeader != null)
            {
                Populate(existingHeader);
                if (registerBtnAction)
                {
                    var existingBtn = existingHeader.Q<Button>("BtnAction");
                    if (existingBtn != null)
                    {
                        existingBtn.ClearBindings();
                        existingBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
                    }
                }
                LoadTeamLogo(existingHeader);
                return;
            }

            // After SidebarController inserted the sidebar at index 0, layout is:
            //   container (flex-direction: row originally)
            //     [0] sidebar
            //     [1] main content
            //     [2+] modals (position: absolute)
            // We restructure to:
            //   container (flex-direction: column)
            //     [0] header (full width)
            //     [1] body-row (flex row: sidebar + main content)
            //     [2+] modals (position: absolute)

            var children = new List<VisualElement>();
            while (container.childCount > 0)
            {
                children.Add(container[0]);
                container.RemoveAt(0);
            }

            var header = template.CloneTree();
            container.Add(header);

            var bodyRow = new VisualElement();
            bodyRow.style.flexDirection = FlexDirection.Row;
            bodyRow.style.flexGrow = 1;
            bodyRow.style.minHeight = 0;

            // Sidebar is children[0], main content is children[1]
            if (children.Count >= 2)
            {
                bodyRow.Add(children[0]); // sidebar
                bodyRow.Add(children[1]); // main content
            }
            container.Add(bodyRow);

            // Remaining children (modals) go back as direct children of container
            for (int i = 2; i < children.Count; i++)
                container.Add(children[i]);

            container.style.flexDirection = FlexDirection.Column;

            Populate(header);

            var btnAction = header.Q<Button>("BtnAction");
            if (registerBtnAction && btnAction != null)
                btnAction.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });

            var configIcon = header.Q<VisualElement>("ConfigIcon");

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
