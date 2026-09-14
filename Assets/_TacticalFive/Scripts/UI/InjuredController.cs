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

    // Injured cards
    private VisualElement _injuredCards;
    private VisualElement _injuredPaginator;

    // League injured modal
    private VisualElement _leagueInjuredOverlay;
    private VisualElement _leagueInjuredList;
    private VisualElement _leagueInjuredPaginator;
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
    private Dictionary<string, Texture2D> _injuryIcons = new();
    private Texture2D _starTex;
    private StyleBackground _starBg;
    private StyleBackground _empleadoBg;
    private Texture2D _calendarTex;
    private Texture2D _personTex;

    // Pagination
    private int _currentPage;
    private const int ITEMS_PER_PAGE = 6;
    private int _leagueInjuredPage;
    private const int LEAGUE_INJURED_PER_PAGE = 10;
    private List<(TeamData team, PlayerData player)> _leagueInjuredPlayers = new();

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
        _injuredCards = _root.Q<VisualElement>("InjuredCards");
        _injuredPaginator = _root.Q<VisualElement>("InjuredPaginator");

        _leagueInjuredOverlay = _root.Q<VisualElement>("LeagueInjuredOverlay");
        _leagueInjuredList = _root.Q<VisualElement>("LeagueInjuredList");
        _leagueInjuredPaginator = _root.Q<VisualElement>("LeagueInjuredPaginator");
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

        var screenIcon = _root.Q<VisualElement>("ScreenIcon");
        if (screenIcon != null)
        {
            var iconTex = Resources.Load<Texture2D>("Icons/lesionados_icon");
            if (iconTex != null)
                screenIcon.style.backgroundImage = iconTex;
        }

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos32 = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        foreach (var s in logos32) _logoSprites32[s.name] = s;

        _starTex = Resources.Load<Texture2D>("Icons/star_24px");
        if (_starTex != null)
            _starBg = new StyleBackground(_starTex);
        _empleadoBg = new StyleBackground(Resources.Load<Texture2D>("Icons/avatar"));

        _calendarTex = Resources.Load<Texture2D>("Icons/calendar");
        _personTex = Resources.Load<Texture2D>("Icons/person");

        _injuryIcons.Clear();
        var icons = Resources.LoadAll<Texture2D>("Icons/");
        foreach (var tex in icons)
        {
            if (tex.name.StartsWith("lesion"))
                _injuryIcons[tex.name] = tex;
        }

        ReloadData();
    }

    void ReloadData()
    {
        _allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        _injuredPlayers = _allPlayers.Where(p => p.injury_days > 0).ToList();
        _currentPage = 0;

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
        BuildInjuredCards();
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

    // ── MEDICAL STAFF ──

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
                5 => "Recupera: 25%-40%",
                4 => "Recupera: 20%-32%",
                3 => "Recupera: 15%-25%",
                2 => "Recupera: 10%-18%",
                _ => "Recupera: 5%-12%"
            };
            var recoveryLbl = new Label(recoveryText);
            recoveryLbl.AddToClassList("med-staff-recovery");
            info.Add(recoveryLbl);

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

    // ── INJURED CARDS ──

    void BuildInjuredCards()
    {
        _injuredCards.Clear();
        _injuredPaginator?.Clear();
        if (_injuredPaginator != null)
            _injuredPaginator.style.display = DisplayStyle.None;

        int total = _injuredPlayers.Count;

        if (total == 0)
        {
            var emptyWrapper = new VisualElement();
            emptyWrapper.AddToClassList("injured-empty-card");

            var emptyInner = new VisualElement();
            emptyInner.AddToClassList("injured-empty-inner");

            if (_personTex != null)
            {
                var emptyIcon = new VisualElement();
                emptyIcon.AddToClassList("injured-empty-icon");
                emptyIcon.style.backgroundImage = new StyleBackground(_personTex);
                emptyInner.Add(emptyIcon);
            }

            var emptyLbl = new Label();
            emptyLbl.AddToClassList("injured-empty-text");
            emptyLbl.text = "No hay m\u00e1s jugadores lesionados\nen la plantilla";
            emptyInner.Add(emptyLbl);

            emptyWrapper.Add(emptyInner);
            _injuredCards.Add(emptyWrapper);
            return;
        }

        int totalPages = Mathf.CeilToInt((float)total / ITEMS_PER_PAGE);
        if (totalPages < 1) totalPages = 1;
        if (_currentPage >= totalPages) _currentPage = totalPages - 1;
        if (_currentPage < 0) _currentPage = 0;

        int startIdx = _currentPage * ITEMS_PER_PAGE;
        int endIdx = Mathf.Min(startIdx + ITEMS_PER_PAGE, total);

        bool hasMedico = _medico != null;

        for (int i = startIdx; i < endIdx; i++)
        {
            var player = _injuredPlayers[i];
            bool isRecovered = player.injury_days == 0;
            bool alreadyTreated = player.treated == 1;
            bool onIR = player.is_on_ir == 1;

            var cardWrapper = new VisualElement();
            cardWrapper.AddToClassList("injury-card");
            cardWrapper.AddToClassList(isRecovered ? "injury-card--recovered" : "injury-card--injured");

            var card = new VisualElement();
            card.AddToClassList("injury-card-inner");

            // ── HEADER ──
            var header = new VisualElement();
            header.AddToClassList("card-header");

            var jerseyLbl = new Label();
            jerseyLbl.AddToClassList("card-jersey");
            jerseyLbl.text = $"#{player.number}";
            header.Add(jerseyLbl);

            var posLbl = new Label();
            posLbl.AddToClassList("card-position");
            posLbl.text = PositionCodes.GetShort(player.position);
            header.Add(posLbl);

            var nameLbl = new Label();
            nameLbl.AddToClassList("card-name");
            nameLbl.text = $"{player.first_name} {player.last_name}".ToUpper();
            header.Add(nameLbl);

            if (!string.IsNullOrEmpty(player.nationality))
            {
                var flagTex = Resources.Load<Texture2D>($"Flags/{player.nationality}");
                if (flagTex != null)
                {
                    var flag = new VisualElement();
                    flag.AddToClassList("card-flag");
                    flag.style.backgroundImage = new StyleBackground(flagTex);
                    header.Add(flag);
                }
            }

            var statusLbl = new Label();
            statusLbl.AddToClassList("card-status");
            if (isRecovered)
            {
                statusLbl.AddToClassList("card-status--recovered");
                statusLbl.text = "\u2713 RECUPERADO";
            }
            else
            {
                statusLbl.AddToClassList("card-status--injured");
                statusLbl.text = "+ LESIONADO";
            }
            header.Add(statusLbl);

            card.Add(header);

            // ── BODY ──
            var body = new VisualElement();
            body.AddToClassList("card-body");

            var photo = new VisualElement();
            photo.AddToClassList("card-photo");
            var photoTex = PlayerPhotoHelper.Load(player.id, player.photo);
            if (photoTex != null)
                photo.style.backgroundImage = new StyleBackground(photoTex);
            body.Add(photo);

            var injuryIconBg = new VisualElement();
            injuryIconBg.AddToClassList("card-injury-icon");
            if (isRecovered)
                injuryIconBg.AddToClassList("card-injury-icon--recovered");

            var injuryIconImg = new VisualElement();
            injuryIconImg.AddToClassList("card-injury-icon-img");
            var iconTex = GetInjuryIcon(player.injury_type, isRecovered);
            if (iconTex != null)
                injuryIconImg.style.backgroundImage = new StyleBackground(iconTex);
            injuryIconBg.Add(injuryIconImg);
            body.Add(injuryIconBg);

            var injuryInfo = new VisualElement();
            injuryInfo.AddToClassList("card-injury-info");

            var injuryName = new Label();
            injuryName.AddToClassList("card-injury-name");
            if (isRecovered)
                injuryName.AddToClassList("card-injury-name--recovered");
            injuryName.text = isRecovered
                ? "Fase final de recuperaci\u00f3n"
                : (string.IsNullOrEmpty(player.injury_type) ? "Lesi\u00f3n" : player.injury_type);
            injuryInfo.Add(injuryName);

            var subtitleLbl = new Label();
            subtitleLbl.AddToClassList("card-injury-subtitle");
            subtitleLbl.text = "Duraci\u00f3n estimada";
            injuryInfo.Add(subtitleLbl);

            var daysRow = new VisualElement();
            daysRow.AddToClassList("card-injury-days");

            if (_calendarTex != null)
            {
                var calIcon = new VisualElement();
                calIcon.AddToClassList("card-injury-days-icon");
                calIcon.style.backgroundImage = new StyleBackground(_calendarTex);
                daysRow.Add(calIcon);
            }

            var daysLbl = new Label();
            daysLbl.AddToClassList("card-injury-days-text");
            if (isRecovered)
                daysLbl.AddToClassList("card-injury-days-text--recovered");
            daysLbl.text = $"{player.injury_days} d\u00eda{(player.injury_days != 1 ? "s" : "")}";
            daysRow.Add(daysLbl);

            injuryInfo.Add(daysRow);
            body.Add(injuryInfo);

            card.Add(body);

            // ── FOOTER ──
            var footer = new VisualElement();
            footer.AddToClassList("card-footer");

            var severity = new VisualElement();
            severity.AddToClassList("card-severity");

            if (_injuryIcons.TryGetValue("lesion_ritmoCardiaco", out var heartTex))
            {
                var heartIcon = new VisualElement();
                heartIcon.style.width = 32;
                heartIcon.style.height = 32;
                heartIcon.style.marginRight = 6;
                heartIcon.style.flexShrink = 0;
                heartIcon.style.backgroundImage = new StyleBackground(heartTex);
                heartIcon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                severity.Add(heartIcon);
            }

            var sevLabel = new Label();
            sevLabel.AddToClassList("card-severity-label");
            string sevText = GetSeverityLabel(player.injury_days);
            sevLabel.text = sevText;
            if (isRecovered || player.injury_days == 0)
                sevLabel.AddToClassList("card-severity-label--safe");
            else if (player.injury_days <= 3)
                sevLabel.AddToClassList("card-severity-label--leve");
            else if (player.injury_days <= 14)
                sevLabel.AddToClassList("card-severity-label--moderada");
            else
                sevLabel.AddToClassList("card-severity-label--severa");
            severity.Add(sevLabel);

            var bar = new VisualElement();
            bar.AddToClassList("card-severity-bar");
            int segments = GetSeveritySegments(player.injury_days);
            string segColor = isRecovered || player.injury_days == 0
                ? "green"
                : (player.injury_days <= 14 ? "orange" : "red");
            for (int s = 0; s < 4; s++)
            {
                var seg = new VisualElement();
                seg.AddToClassList("severity-segment");
                if (s < segments)
                {
                    seg.AddToClassList(segColor == "green" ? "severity-segment--filled-green"
                        : segColor == "orange" ? "severity-segment--filled-orange"
                        : "severity-segment--filled-red");
                }
                bar.Add(seg);
            }
            severity.Add(bar);
            footer.Add(severity);

            // ── BUTTONS ──
            var buttons = new VisualElement();
            buttons.AddToClassList("card-buttons");

            if (isRecovered)
            {
                var aptoBtn = new Button();
                aptoBtn.AddToClassList("btn-card-apt");
                aptoBtn.text = "APTO";
                aptoBtn.SetEnabled(false);
                aptoBtn.AddToClassList("btn-card-no-apt--disabled");
                buttons.Add(aptoBtn);
            }
            else
            {
                var noAptBtn = new Button();
                noAptBtn.AddToClassList("btn-card-no-apt");
                noAptBtn.text = "PONER NO APTO";
                if (onIR)
                {
                    int rosterCount = DatabaseManager.Instance.GetRosterCount(_myTeam.id);
                    if (rosterCount >= TradeHelper.MAX_ROSTER)
                    {
                        noAptBtn.AddToClassList("btn-card-no-apt--disabled");
                        noAptBtn.SetEnabled(false);
                        noAptBtn.text = "LLENA";
                    }
                    else
                    {
                        noAptBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetPlayerIR(player, false); });
                    }
                }
                else if (!TradeHelper.IsEligibleForIR(player))
                {
                    noAptBtn.AddToClassList("btn-card-no-apt--disabled");
                    noAptBtn.SetEnabled(false);
                }
                else
                {
                    noAptBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetPlayerIR(player, true); });
                }
                if (noAptBtn.enabledSelf && CursorManager.Instance != null)
                {
                    noAptBtn.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
                    noAptBtn.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
                }
                if (onIR) noAptBtn.text = "PONER APTO";
                buttons.Add(noAptBtn);

                var treatBtn = new Button();
                treatBtn.AddToClassList("btn-card-treat");
                if (alreadyTreated)
                {
                    treatBtn.AddToClassList("btn-card-treat--done");
                    treatBtn.text = "TRATADO";
                    treatBtn.SetEnabled(false);
                }
                else
                {
                    treatBtn.text = "TRATAR";
                    if (!hasMedico)
                    {
                        treatBtn.AddToClassList("btn-card-treat--disabled");
                        treatBtn.SetEnabled(false);
                    }
                    else
                    {
                        treatBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnTreat(player); });
                        if (CursorManager.Instance != null)
                        {
                            treatBtn.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
                            treatBtn.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
                        }
                    }
                }
                buttons.Add(treatBtn);
            }

            footer.Add(buttons);
            card.Add(footer);

            cardWrapper.Add(card);
            _injuredCards.Add(cardWrapper);
        }

        // ── PAGINATION BAR ──
        if (totalPages > 1 && _injuredPaginator != null)
        {
            _injuredPaginator.style.display = DisplayStyle.Flex;

            var btnPrev = new Button();
            btnPrev.AddToClassList("injured-paginator-btn");
            btnPrev.text = "\u25C0";
            btnPrev.SetEnabled(_currentPage > 0);
            if (_currentPage == 0) btnPrev.AddToClassList("injured-paginator-btn--disabled");
            int prevPage = _currentPage - 1;
            btnPrev.RegisterCallback<ClickEvent>(_ => { PlayClick(); GoToPage(prevPage); });
            if (CursorManager.Instance != null)
            {
                btnPrev.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
                btnPrev.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
            }
            _injuredPaginator.Add(btnPrev);

            var pageLbl = new Label();
            pageLbl.AddToClassList("injured-paginator-label");
            pageLbl.text = $"{_currentPage + 1} / {totalPages}";
            _injuredPaginator.Add(pageLbl);

            var btnNext = new Button();
            btnNext.AddToClassList("injured-paginator-btn");
            btnNext.text = "\u25B6";
            btnNext.SetEnabled(_currentPage < totalPages - 1);
            if (_currentPage >= totalPages - 1) btnNext.AddToClassList("injured-paginator-btn--disabled");
            int nextPage = _currentPage + 1;
            btnNext.RegisterCallback<ClickEvent>(_ => { PlayClick(); GoToPage(nextPage); });
            if (CursorManager.Instance != null)
            {
                btnNext.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
                btnNext.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
            }
            _injuredPaginator.Add(btnNext);
        }
    }

    void GoToPage(int page)
    {
        _currentPage = page;
        BuildInjuredCards();
    }

    // ── HELPERS ──

    Texture2D GetInjuryIcon(string injuryType, bool isRecovered)
    {
        if (isRecovered)
        {
            if (_injuryIcons.TryGetValue("lesion_ritmoCardiaco", out var heartTex))
                return heartTex;
            return _injuryIcons.Values.FirstOrDefault();
        }

        if (string.IsNullOrEmpty(injuryType))
            return _injuryIcons.TryGetValue("lesion", out var defTex) ? defTex : null;

        string lower = injuryType.ToLowerInvariant();

        if (lower.Contains("sobrecarga") || lower.Contains("calambre") || lower.Contains("distensi")
            || lower.Contains("contractura") || lower.Contains("fibrilar"))
        {
            if (_injuryIcons.TryGetValue("lesion_muscular", out var tex))
                return tex;
        }

        if (lower.Contains("esguince"))
        {
            if (_injuryIcons.TryGetValue("lesion_pie", out var tex))
                return tex;
        }

        if (lower.Contains("fractura") || lower.Contains("contusi"))
        {
            if (_injuryIcons.TryGetValue("lesion_fractura", out var tex))
                return tex;
        }

        if (lower.Contains("tendinitis") || lower.Contains("fascitis") || lower.Contains("gemelo")
            || lower.Contains("aquiles"))
        {
            if (_injuryIcons.TryGetValue("lesion_tendon", out var tex))
                return tex;
        }

        if (lower.Contains("lumbalgia"))
        {
            if (_injuryIcons.TryGetValue("lesion_espalda", out var tex))
                return tex;
        }

        if (lower.Contains("menisco") || lower.Contains("ligamento") || lower.Contains("cruzado"))
        {
            if (_injuryIcons.TryGetValue("lesion_rodilla", out var tex))
                return tex;
        }

        if (lower.Contains("luxaci") || lower.Contains("hombro"))
        {
            if (_injuryIcons.TryGetValue("lesion_hombro", out var tex))
                return tex;
        }

        if (lower.Contains("subluxaci") || lower.Contains("dedo"))
        {
            if (_injuryIcons.TryGetValue("lesion_mano", out var tex))
                return tex;
        }

        return _injuryIcons.TryGetValue("lesion", out var fallbackTex) ? fallbackTex : null;
    }

    string GetSeverityLabel(int injuryDays)
    {
        if (injuryDays <= 0) return "SIN RIESGO";
        if (injuryDays <= 3) return "LEVE";
        if (injuryDays <= 14) return "MODERADA";
        return "SEVERA";
    }

    int GetSeveritySegments(int injuryDays)
    {
        if (injuryDays <= 0) return 4;
        if (injuryDays <= 3) return 1;
        if (injuryDays <= 14) return 2;
        return 3;
    }

    // ── IR (RESERVA DE LESIONADOS) ──

    void SetPlayerIR(PlayerData player, bool onIR)
    {
        if (onIR)
        {
            if (!TradeHelper.IsEligibleForIR(player)) return;
            player.g_league_assigned = 0;
        }
        else
        {
            if (DatabaseManager.Instance.GetRosterCount(_myTeam.id) >= TradeHelper.MAX_ROSTER) return;
        }
        DatabaseManager.Instance.SetOnIR(player, onIR);
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = onIR
                ? $"RESERVA DE LESIONADOS: {player.first_name} {player.last_name}"
                : $"REACTIVADO: {player.first_name} {player.last_name}",
            body = onIR
                ? NewsTexts.ReservaIR($"{player.first_name} {player.last_name}", "lesión", 0)
                : NewsTexts.Recuperacion($"{player.first_name} {player.last_name}", "lesión", 0),
            game_day = _season?.current_game_day ?? 0,
            game_date = DatabaseManager.Instance.GetCurrentDateString(_manager.id),
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = DatabaseManager.Instance.GetCurrentDateString(_manager.id),
            is_read = 0,
            category = NewsCategory.LESIONES,
            news_type = onIR ? NewsType.RESERVA_IR : NewsType.RECUPERACION
        });
        ReloadData();
        Refresh();
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

        int newDays = Mathf.CeilToInt(player.injury_days * (1f - pct));
        player.injury_days = Mathf.Clamp(newDays, 1, player.injury_days);
        player.treated = 1;
        DatabaseManager.Instance.UpdatePlayer(player);

        ReloadData();
        Refresh();
    }

    void CloseTreatmentResult()
    {
        _treatmentResultOverlay.style.display = DisplayStyle.None;
    }

    // ── LEAGUE INJURED ──

    void OpenLeagueInjuredModal()
    {
        _leagueInjuredPage = 0;
        BuildLeagueInjuredList();
        var scrollWrapper = _root.Q<VisualElement>("LeagueInjuredScrollWrapper");
        if (scrollWrapper != null)
        {
            scrollWrapper.style.height = new StyleLength(new Length(380, LengthUnit.Pixel));
            scrollWrapper.style.maxHeight = new StyleLength(new Length(380, LengthUnit.Pixel));
        }
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
        _leagueInjuredList.Clear();
        _leagueInjuredPaginator.Clear();
        _leagueInjuredPaginator.style.display = DisplayStyle.None;

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var myTeamId = _myTeam.id;
        _leagueInjuredPlayers.Clear();

        foreach (var team in allTeams)
        {
            if (team.id == myTeamId) continue;
            var players = DatabaseManager.Instance.GetPlayersByTeam(team.id);
            foreach (var p in players)
            {
                if (p.injury_days > 0)
                    _leagueInjuredPlayers.Add((team, p));
            }
        }

        if (_leagueInjuredPlayers.Count == 0)
        {
            var emptyLbl = new Label();
            emptyLbl.AddToClassList("league-injured-empty");
            emptyLbl.text = "No hay jugadores lesionados en la liga.";
            _leagueInjuredList.Add(emptyLbl);
            return;
        }

        int totalPages = Mathf.CeilToInt((float)_leagueInjuredPlayers.Count / LEAGUE_INJURED_PER_PAGE);
        if (_leagueInjuredPage >= totalPages) _leagueInjuredPage = totalPages - 1;
        if (_leagueInjuredPage < 0) _leagueInjuredPage = 0;

        int start = _leagueInjuredPage * LEAGUE_INJURED_PER_PAGE;
        int end = Mathf.Min(start + LEAGUE_INJURED_PER_PAGE, _leagueInjuredPlayers.Count);

        for (int i = start; i < end; i++)
        {
            var (team, player) = _leagueInjuredPlayers[i];
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

            _leagueInjuredList.Add(row);
        }

        if (totalPages > 1)
        {
            _leagueInjuredPaginator.style.display = DisplayStyle.Flex;

            var btnPrev = new Button();
            btnPrev.AddToClassList("injured-paginator-btn");
            btnPrev.text = "\u25C0";
            btnPrev.SetEnabled(_leagueInjuredPage > 0);
            if (_leagueInjuredPage == 0)
                btnPrev.AddToClassList("injured-paginator-btn--disabled");
            int previousPage = _leagueInjuredPage - 1;
            btnPrev.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                _leagueInjuredPage = previousPage;
                BuildLeagueInjuredList();
            });
            CursorManager.Instance?.RegisterHandCursor(btnPrev);
            _leagueInjuredPaginator.Add(btnPrev);

            var pageLabel = new Label($"{_leagueInjuredPage + 1} / {totalPages}");
            pageLabel.AddToClassList("injured-paginator-label");
            _leagueInjuredPaginator.Add(pageLabel);

            var btnNext = new Button();
            btnNext.AddToClassList("injured-paginator-btn");
            btnNext.text = "\u25B6";
            btnNext.SetEnabled(_leagueInjuredPage < totalPages - 1);
            if (_leagueInjuredPage >= totalPages - 1)
                btnNext.AddToClassList("injured-paginator-btn--disabled");
            int nextPage = _leagueInjuredPage + 1;
            btnNext.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                _leagueInjuredPage = nextPage;
                BuildLeagueInjuredList();
            });
            CursorManager.Instance?.RegisterHandCursor(btnNext);
            _leagueInjuredPaginator.Add(btnNext);
        }
    }
}
