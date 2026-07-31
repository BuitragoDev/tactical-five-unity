using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class InjuredController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Injured;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Label _headerSeason;
    private Label _headerDate;

    // Medical staff
    private VisualElement _medStaffBody;
    private VisualElement _medStaffCard;
    private VisualElement _medStaffEmpty;

    // Injured table
    private VisualElement _injuredTable;

    // League injured modal
    private VisualElement _leagueInjuredOverlay;
    private ScrollView _leagueInjuredScroll;
    private Button _btnLeagueInjured;
    private Button _btnLeagueInjuredClose;

    // Treatment result modal
    private VisualElement _treatmentResultOverlay;
    private Label _treatmentResultTitle;
    private Label _treatmentResultText;
    private Button _btnTreatmentOk;

    // Data
    private List<PlayerData> _allPlayers;
    private List<PlayerData> _injuredPlayers;
    private EmployeeData _medico;
    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _logoSprites32 = new();
    private Texture2D _starTex;
    private StyleBackground _starBg;
    private StyleBackground _empleadoBg;
    protected override void CacheReferences()
    {
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");

        _medStaffBody = _root.Q<VisualElement>("MedStaffBody");
        _medStaffCard = _root.Q<VisualElement>("MedStaffCard");
        _medStaffEmpty = _root.Q<VisualElement>("MedStaffEmpty");
        _injuredTable = _root.Q<VisualElement>("InjuredTable");

        _leagueInjuredOverlay = _root.Q<VisualElement>("LeagueInjuredOverlay");
        _leagueInjuredScroll = _root.Q<ScrollView>("LeagueInjuredScroll");
        _btnLeagueInjured = _root.Q<Button>("BtnLeagueInjured");
        _btnLeagueInjuredClose = _root.Q<Button>("BtnLeagueInjuredClose");

        _treatmentResultOverlay = _root.Q<VisualElement>("TreatmentResultOverlay");
        _treatmentResultTitle = _root.Q<Label>("TreatmentResultTitle");
        _treatmentResultText = _root.Q<Label>("TreatmentResultText");
        _btnTreatmentOk = _root.Q<Button>("BtnTreatmentOk");
    }
    protected override void LoadData()
    {
        base.LoadData();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos32 = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        foreach (var s in logos32) _logoSprites32[s.name] = s;

        _starTex = Resources.Load<Texture2D>("Icons/star_24px");
        if (_starTex != null)
            _starBg = new StyleBackground(_starTex);
        _empleadoBg = new StyleBackground(Resources.Load<Texture2D>("Icons/empleado"));

        
        
        
        ReloadData();
    }

    void ReloadData()
    {
        _allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        _injuredPlayers = _allPlayers.Where(p => p.injury_days > 0).ToList();

        var staff = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        _medico = staff.FirstOrDefault(e => e.position == "MEDICO");
    }
    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _btnTreatmentOk?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseTreatmentResult(); });
        _btnLeagueInjured?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenLeagueInjuredModal(); });
        _btnLeagueInjuredClose?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseLeagueInjuredModal(); });
        _leagueInjuredOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _leagueInjuredOverlay)
            { PlayClick(); CloseLeagueInjuredModal(); }
        });
        if (CursorManager.Instance == null) return;
        var cursor = CursorManager.Instance;
        cursor.RegisterHandCursor(_btnTreatmentOk);
        cursor.RegisterHandCursor(_btnLeagueInjured);
        cursor.RegisterHandCursor(_btnLeagueInjuredClose);
    }
    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Injured] RefreshHeader error: {ex.Message}"); }
        BuildMedicalStaff();
        BuildInjuredTable();
        _root.Q<VisualElement>("RosterSubmenu")?.AddToClassList("nav-submenu--visible");
        _root.Q<Button>("SubmenuLesionados")?.AddToClassList("nav-submenu-item--active");
        _treatmentResultOverlay.style.display = DisplayStyle.None;
    }
    protected override void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;
        if (_headerTeamName == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";

        _headerBudget.text = $"${_myTeam.budget / 1_000_000}M";
        _headerBudget.style.color = _myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));

        var teamEmployees = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        long totalPayroll = _allPlayers.Sum(p => p.salary);
        _headerPayroll.text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - _allPlayers.Sum(p => p.salary);

        string marginText = margin >= 0
            ? $"+${margin / 1_000_000}M"
            : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        _headerMargin.text = marginText;
        _headerMargin.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) _headerMargin.AddToClassList("header-stat-value--negative");

        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        var chemLabel = _root.Q<Label>("HeaderChemistry");
        if (chemLabel != null)
        {
            chemLabel.text = $"{chemistry}%";
            chemLabel.RemoveFromClassList("header-stat-value--gold");
            chemLabel.RemoveFromClassList("header-stat-value--negative");
            if (chemistry < 40)
                chemLabel.AddToClassList("header-stat-value--negative");
            else if (chemistry < 70)
                chemLabel.AddToClassList("header-stat-value--gold");
        }

        if (_season != null)
        {
            _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            _headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "MENÚ PRINCIPAL";
    }

    void BuildMedicalStaff()
    {
        if (_medico != null)
        {
            _medStaffCard.style.display = DisplayStyle.Flex;
            _medStaffCard.Clear();
            _medStaffCard.style.minHeight = 100;
            _medStaffEmpty.style.display = DisplayStyle.None;

            var icon = new VisualElement();
            icon.AddToClassList("med-staff-icon");
            icon.style.backgroundImage = _empleadoBg;
            _medStaffCard.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("med-staff-info");

            var nameLbl = new Label();
            nameLbl.AddToClassList("med-staff-name");
            nameLbl.text = $"{_medico.first_name} {_medico.last_name}".ToUpper();
            info.Add(nameLbl);

            var starRow = new VisualElement();
            starRow.style.flexDirection = FlexDirection.Row;
            starRow.style.marginTop = 4;
            for (int i = 0; i < 5; i++)
            {
                var star = new VisualElement();
                star.AddToClassList("med-staff-star");
                if (i >= _medico.reputation)
                    star.AddToClassList("med-staff-star--empty");
                if (_starTex != null)
                    star.style.backgroundImage = _starBg;
                starRow.Add(star);
            }
            info.Add(starRow);

            var recoveryText = _medico.reputation switch
            {
                5 => "RECUPERA: 25%-40%",
                4 => "RECUPERA: 20%-32%",
                3 => "RECUPERA: 15%-25%",
                2 => "RECUPERA: 10%-18%",
                _ => "RECUPERA: 5%-12%"
            };
            info.Add(new Label(recoveryText));

            _medStaffCard.Add(info);
        }
        else
        {
            _medStaffCard.style.display = DisplayStyle.None;
            _medStaffEmpty.style.display = DisplayStyle.Flex;
            _medStaffEmpty.Clear();

            var emptyLbl = new Label();
            emptyLbl.AddToClassList("med-staff-empty-text");
            emptyLbl.text = "A\u00fan no se ha contratado ning\u00fan jefe de servicios m\u00e9dicos.";
            _medStaffEmpty.Add(emptyLbl);

            var hireBtn = new Button();
            hireBtn.AddToClassList("btn-hire");
            hireBtn.text = "IR A EMPLEADOS";
            hireBtn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                ScreenManager.Instance.GoTo(GameScreen.Employees);
            });
            if (CursorManager.Instance != null)
            {
                hireBtn.RegisterCallback<MouseEnterEvent>(_ =>
                    CursorManager.Instance.SetHandCursor());
                hireBtn.RegisterCallback<MouseLeaveEvent>(_ =>
                    CursorManager.Instance.SetDefaultCursor());
            }
            _medStaffEmpty.Add(hireBtn);
        }
    }

    void BuildInjuredTable()
    {
        _injuredTable.Clear();

        // Header row
        var headerRow = new VisualElement();
        headerRow.AddToClassList("injured-header-row");

        var hNum = new Label(); hNum.AddToClassList("injured-col-num"); hNum.text = "#"; headerRow.Add(hNum);
        var hName = new Label(); hName.AddToClassList("injured-col-name"); hName.text = "JUGADOR"; headerRow.Add(hName);
        var hPos = new Label(); hPos.AddToClassList("injured-col-pos"); hPos.text = "POS"; headerRow.Add(hPos);
        var hInjury = new Label(); hInjury.AddToClassList("injured-col-injury"); hInjury.text = "LESI\u00d3N"; headerRow.Add(hInjury);
        var hDays = new Label(); hDays.AddToClassList("injured-col-days"); hDays.text = "D\u00cdAS"; headerRow.Add(hDays);
        var hAct = new Label(); hAct.AddToClassList("injured-col-action"); hAct.text = "TRATAR"; headerRow.Add(hAct);
        _injuredTable.Add(headerRow);

        if (_injuredPlayers.Count == 0)
        {
            var emptyLbl = new Label();
            emptyLbl.AddToClassList("injured-empty");
            emptyLbl.text = "No hay jugadores lesionados.";
            _injuredTable.Add(emptyLbl);
            return;
        }

        bool hasMedico = _medico != null;

        for (int i = 0; i < _injuredPlayers.Count; i++)
        {
            var player = _injuredPlayers[i];
            var row = new VisualElement();
            row.AddToClassList("injured-row");

            var numLbl = new Label();
            numLbl.AddToClassList("injured-row-num");
            numLbl.text = (i + 1).ToString("D2");
            row.Add(numLbl);

            var nameLbl = new Label();
            nameLbl.AddToClassList("injured-row-name");
            nameLbl.text = $"{player.first_name} {player.last_name}".ToUpper();
            row.Add(nameLbl);

            var posLbl = new Label();
            posLbl.AddToClassList("injured-row-pos");
            posLbl.text = PositionCodes.GetShort(player.position);
            row.Add(posLbl);

            var injuryLbl = new Label();
            injuryLbl.AddToClassList("injured-row-injury");
            injuryLbl.text = string.IsNullOrEmpty(player.injury_type) ? "LESI\u00d3N" : player.injury_type;
            row.Add(injuryLbl);

            var daysLbl = new Label();
            daysLbl.AddToClassList("injured-row-days");
            daysLbl.text = $"{player.injury_days} d\u00eda{(player.injury_days != 1 ? "s" : "")}";
            row.Add(daysLbl);

            bool alreadyTreated = player.treated == 1;
            var treatBtn = new Button();
            treatBtn.AddToClassList("btn-treat");
            treatBtn.text = alreadyTreated ? "TRATADO" : "TRATAR";
            if (!hasMedico || alreadyTreated)
            {
                treatBtn.AddToClassList("btn-treat--disabled");
                treatBtn.SetEnabled(false);
            }
            else
            {
                treatBtn.userData = player;
                treatBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnTreat(player); });
                if (CursorManager.Instance != null)
                {
                    treatBtn.RegisterCallback<MouseEnterEvent>(_ =>
                        CursorManager.Instance.SetHandCursor());
                    treatBtn.RegisterCallback<MouseLeaveEvent>(_ =>
                        CursorManager.Instance.SetDefaultCursor());
                }
            }
            row.Add(treatBtn);

            _injuredTable.Add(row);
        }
    }

    // ── TREATMENT ──

    void OnTreat(PlayerData player)
    {
        if (_medico == null) return;

        float pct = _medico.reputation switch
        {
            5 => Random.Range(0.25f, 0.40f),
            4 => Random.Range(0.20f, 0.32f),
            3 => Random.Range(0.15f, 0.25f),
            2 => Random.Range(0.10f, 0.18f),
            _ => Random.Range(0.05f, 0.12f),
        };

        int oldDays = player.injury_days;
        int newDays = Mathf.CeilToInt(player.injury_days * (1f - pct));
        player.injury_days = Mathf.Clamp(newDays, 1, player.injury_days);
        player.treated = 1;
        DatabaseManager.Instance.UpdatePlayer(player);

        string playerName = $"{player.first_name} {player.last_name}";
        string reductionText = $"{playerName} ha recibido tratamiento m\u00e9dico.\nSus d\u00edas de baja se reducen de {oldDays} a {player.injury_days} días.";

        ReloadData();
        Refresh();

        _treatmentResultTitle.text = "TRATAMIENTO COMPLETADO";
        _treatmentResultText.text = reductionText;
        _treatmentResultOverlay.style.display = DisplayStyle.Flex;
    }

    void CloseTreatmentResult()
    {
        _treatmentResultOverlay.style.display = DisplayStyle.None;
    }
    // ── LEAGUE INJURED ──

    void OpenLeagueInjuredModal()
    {
        BuildLeagueInjuredList();
        var scrollWrapper = _root.Q<VisualElement>("LeagueInjuredScrollWrapper");
        if (scrollWrapper != null)
        {
            scrollWrapper.style.height = new StyleLength(new Length(380, LengthUnit.Pixel));
            scrollWrapper.style.maxHeight = new StyleLength(new Length(380, LengthUnit.Pixel));
        }
        _leagueInjuredScroll.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
        _leagueInjuredOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _leagueInjuredOverlay.AddToClassList("modal-overlay--visible");
        _leagueInjuredOverlay.Q<VisualElement>(null, "modal-box")?.AddToClassList("modal-box--visible");
    }

    void CloseLeagueInjuredModal()
    {
        _leagueInjuredOverlay.RemoveFromClassList("modal-overlay--visible");
        _leagueInjuredOverlay.Q<VisualElement>(null, "modal-box")?.RemoveFromClassList("modal-box--visible");
    }

    void BuildLeagueInjuredList()
    {
        _leagueInjuredScroll.Clear();

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var myTeamId = _myTeam.id;
        var injuredList = new List<(TeamData team, PlayerData player)>();

        foreach (var team in allTeams)
        {
            if (team.id == myTeamId) continue;
            var players = DatabaseManager.Instance.GetPlayersByTeam(team.id);
            foreach (var p in players)
            {
                if (p.injury_days > 0)
                    injuredList.Add((team, p));
            }
        }

        if (injuredList.Count == 0)
        {
            var emptyLbl = new Label();
            emptyLbl.AddToClassList("league-injured-empty");
            emptyLbl.text = "No hay jugadores lesionados en la liga.";
            _leagueInjuredScroll.Add(emptyLbl);
            return;
        }

        foreach (var (team, player) in injuredList)
        {
            var row = new VisualElement();
            row.AddToClassList("league-injured-row");
            row.style.height = new StyleLength(new Length(38, LengthUnit.Pixel));
            row.style.minHeight = new StyleLength(new Length(38, LengthUnit.Pixel));

            var logoLbl = new VisualElement();
            logoLbl.AddToClassList("league-injured-row-logo");
            if (_logoSprites32.TryGetValue(team.logo, out var sprite))
                logoLbl.style.backgroundImage = new StyleBackground(sprite);
            row.Add(logoLbl);

            var teamLbl = new Label();
            teamLbl.AddToClassList("league-injured-row-team");
            teamLbl.text = team.name;
            row.Add(teamLbl);

            var nameLbl = new Label();
            nameLbl.AddToClassList("league-injured-row-name");
            nameLbl.text = $"{player.first_name} {player.last_name}";
            row.Add(nameLbl);

            var injuryLbl = new Label();
            injuryLbl.AddToClassList("league-injured-row-injury");
            injuryLbl.text = string.IsNullOrEmpty(player.injury_type) ? "LESI\u00d3N" : player.injury_type;
            row.Add(injuryLbl);

            var daysLbl = new Label();
            daysLbl.AddToClassList("league-injured-row-days");
            daysLbl.text = player.injury_days.ToString();
            row.Add(daysLbl);

            _leagueInjuredScroll.Add(row);
        }
    }
}
