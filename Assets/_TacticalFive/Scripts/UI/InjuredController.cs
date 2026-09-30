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
    private VisualElement _injuredViewport;
    private VisualElement _injuredScrollTrack;
    private VisualElement _injuredScrollThumb;
    private CustomScrollbar.Handle _injuredScrollBar;

    // League injured modal
    private VisualElement _leagueInjuredOverlay;
    private VisualElement _leagueInjuredList;
    private Button _btnLeagueInjured;
    private Button _btnLeagueInjuredClose;

    // Force return modal (E2)
    private VisualElement _forceReturnOverlay;
    private Label _forceReturnWho;
    private Label _forceReturnDiag;
    private Label _forceReturnRisk;
    private Button _btnForceReturnCancel;
    private Button _btnForceReturnConfirm;
    private PlayerData _forceReturnPlayer;

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

    // League injured modal data
    private List<(TeamData team, PlayerData player)> _leagueInjuredPlayers = new();

    // League injured scrollbar
    private ScrollBar _leagueBar;

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
        _injuredViewport = _root.Q<VisualElement>("InjuredViewport");
        _injuredScrollTrack = _root.Q<VisualElement>("InjuredScrollTrack");
        _injuredScrollThumb = _root.Q<VisualElement>("InjuredScrollThumb");

        // Barra de scroll propia para la lista de lesionados (se re-enlaza en cada visita).
        _injuredScrollBar = CustomScrollbar.Bind(_injuredViewport, _injuredCards,
                                                 _injuredScrollTrack, _injuredScrollThumb);

        _leagueInjuredOverlay = _root.Q<VisualElement>("LeagueInjuredOverlay");
        _leagueInjuredList = _root.Q<VisualElement>("LeagueInjuredList");
        _btnLeagueInjured = _root.Q<Button>("BtnLeagueInjured");
        _btnLeagueInjuredClose = _root.Q<Button>("BtnLeagueInjuredClose");

        _forceReturnOverlay = _root.Q<VisualElement>("ForceReturnOverlay");
        _forceReturnWho = _root.Q<Label>("ForceReturnWho");
        _forceReturnDiag = _root.Q<Label>("ForceReturnDiag");
        _forceReturnRisk = _root.Q<Label>("ForceReturnRisk");
        _btnForceReturnCancel = _root.Q<Button>("BtnForceReturnCancel");
        _btnForceReturnConfirm = _root.Q<Button>("BtnForceReturnConfirm");

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

        var logos = TeamImages.Logos(64);
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos32 = TeamImages.Logos(32);
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
        // El parte médico incluye a los lesionados y a los que volvieron antes de tiempo
        // (sin baja pero con riesgo de recaída pendiente).
        _injuredPlayers = _allPlayers.Where(p => p.injury_days > 0 || p.recovery_risk > 0).ToList();

        var staff = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        _medico = staff.FirstOrDefault(e => e.position == "MEDICO");
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _btnTreatmentOk?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseTreatmentResult(); });
        _btnForceReturnCancel?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseForceReturnModal(); });
        _btnForceReturnConfirm?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ConfirmForceReturn(); });
        _forceReturnOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _forceReturnOverlay)
            { PlayClick(); CloseForceReturnModal(); }
        });
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
        cursor.RegisterHandCursor(_btnForceReturnCancel);
        cursor.RegisterHandCursor(_btnForceReturnConfirm);
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
        // El modal de alta forzada se controla por clases del tema (modal-overlay--visible
        // + modal-box--visible); al refrescar se limpian ambas.
        _forceReturnOverlay.RemoveFromClassList("modal-overlay--visible");
        _forceReturnOverlay.Q<VisualElement>(null, "modal-box")?.RemoveFromClassList("modal-box--visible");
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
            CustomScrollbar.Reset(_injuredScrollBar);
            return;
        }

        bool hasMedico = _medico != null;

        // Se agrupan las cards en filas de 3 (rejilla). El espaciado entre filas vive en la
        // fila (clase injury-row), no en la card, para que la última fila no deje hueco.
        const int CARDS_PER_ROW = 3;
        VisualElement row = null;

        for (int i = 0; i < total; i++)
        {
            var player = _injuredPlayers[i];
            bool isRecovered = player.injury_days == 0;
            bool atRisk = isRecovered && player.recovery_risk > 0;
            bool alreadyTreated = player.treated == 1;
            bool onIR = player.is_on_ir == 1;

            var def = InjuryCatalog.ByName(player.injury_type);
            var sev = def != null
                ? def.severity
                : InjuryCatalog.SeverityOf(player.injury_total_days > 0 ? player.injury_total_days : player.injury_days);
            int totalDays = player.injury_total_days > 0 ? player.injury_total_days : player.injury_days;
            int progress = InjuryHelper.RecoveryProgress(player.injury_days, totalDays);

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
            if (atRisk)
            {
                statusLbl.AddToClassList("card-status--recovered");
                statusLbl.text = "! ALTA FORZADA";
            }
            else if (isRecovered)
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
            injuryName.text = atRisk
                ? "En vigilancia m\u00e9dica"
                : (isRecovered
                    ? "Fase final de recuperaci\u00f3n"
                    : (string.IsNullOrEmpty(player.injury_type) ? "Lesi\u00f3n" : player.injury_type));
            injuryInfo.Add(injuryName);

            var subtitleLbl = new Label();
            subtitleLbl.AddToClassList("card-injury-subtitle");
            subtitleLbl.text = atRisk ? "Alta forzada" : "Duraci\u00f3n estimada";
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
            daysLbl.text = atRisk
                ? "Sin baja"
                : $"{player.injury_days} d\u00eda{(player.injury_days != 1 ? "s" : "")}";
            daysRow.Add(daysLbl);

            injuryInfo.Add(daysRow);

            // Pronóstico del parte médico (rango total de la lesión).
            var pronLbl = new Label(atRisk
                ? $"Riesgo de reca\u00edda: {player.recovery_risk}%"
                : $"Pron\u00f3stico: {(def != null ? InjuryCatalog.Pronostico(def) : $"{totalDays} d\u00edas")}");
            pronLbl.AddToClassList("card-pronostico");
            injuryInfo.Add(pronLbl);

            // Progreso de recuperación (E2).
            if (!atRisk)
            {
                var progressRow = new VisualElement();
                progressRow.AddToClassList("card-progress-row");

                var track = new VisualElement();
                track.AddToClassList("card-progress-track");
                var fill = new VisualElement();
                fill.AddToClassList("card-progress-fill");
                if (progress < 40) fill.AddToClassList("card-progress-fill--low");
                else if (progress < 75) fill.AddToClassList("card-progress-fill--mid");
                fill.style.width = new StyleLength(new Length(progress, LengthUnit.Percent));
                track.Add(fill);
                progressRow.Add(track);

                var progLbl = new Label($"Recuperaci\u00f3n {progress}%");
                progLbl.AddToClassList("card-progress-text");
                progressRow.Add(progLbl);

                injuryInfo.Add(progressRow);
            }

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
            sevLabel.text = atRisk ? "VIGILANCIA" : InjuryCatalog.SeverityLabel(sev);
            if (atRisk)
                sevLabel.AddToClassList("card-severity-label--safe");
            else if (sev <= InjurySeverity.Moderada)
                sevLabel.AddToClassList(sev == InjurySeverity.Leve ? "card-severity-label--leve" : "card-severity-label--moderada");
            else
                sevLabel.AddToClassList("card-severity-label--severa");
            severity.Add(sevLabel);

            // Jugador que volvió antes de tiempo: se muestra su riesgo de recaída.
            if (atRisk)
            {
                var riskLbl = new Label($"RIESGO {player.recovery_risk}%");
                riskLbl.AddToClassList("card-risk-badge");
                severity.Add(riskLbl);
            }

            footer.Add(severity);

            // ── BUTTONS ──
            var buttons = new VisualElement();
            buttons.AddToClassList("card-buttons");

            if (atRisk)
            {
                // En vigilancia médica: no hay acción posible, solo esperar/descansar.
            }
            else if (isRecovered)
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

                // E2: alta forzada (volver antes de tiempo). Solo se permite entre 2 y 7 días
                // restantes (regla InjuryHelper.CanForceReturn); fuera de ese rango "SIN FORZAR".
                // Desde la IR además debe caber en la plantilla.
                var forceBtn = new Button();
                forceBtn.AddToClassList("btn-card-force");
                bool forceBlocked = onIR && DatabaseManager.Instance.GetRosterCount(_myTeam.id) >= TradeHelper.MAX_ROSTER;
                bool canForce = InjuryHelper.CanForceReturn(player.injury_days);
                if (forceBlocked)
                {
                    forceBtn.text = "PLANTILLA LLENA";
                    forceBtn.AddToClassList("btn-card-no-apt--disabled");
                    forceBtn.SetEnabled(false);
                }
                else if (!canForce)
                {
                    forceBtn.text = "SIN FORZAR";
                    forceBtn.AddToClassList("btn-card-no-apt--disabled");
                    forceBtn.SetEnabled(false);
                }
                else
                {
                    forceBtn.text = "FORZAR ALTA";
                    forceBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenForceReturnModal(player); });
                    if (CursorManager.Instance != null)
                    {
                        forceBtn.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
                        forceBtn.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
                    }
                }
                buttons.Add(forceBtn);
            }

            footer.Add(buttons);
            card.Add(footer);

            cardWrapper.Add(card);

            if (row == null || row.childCount >= CARDS_PER_ROW)
            {
                row = new VisualElement();
                row.AddToClassList("injury-row");
                _injuredCards.Add(row);
            }
            row.Add(cardWrapper);
        }

        // La última fila no lleva el margen grande entre filas (solo un pequeño colchón
        // para que el scroll no corte las cards).
        if (row != null)
            row.AddToClassList("injury-row--last");

        // Vuelve al inicio y recalcula el thumb con la lista reconstruida.
        CustomScrollbar.Reset(_injuredScrollBar);
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

    // ── ALTA FORZADA (E2) ──

    void OpenForceReturnModal(PlayerData player)
    {
        if (player == null || player.injury_days <= 0) return;

        // Solo forzable entre 2 y 7 días restantes (regla pura).
        if (!InjuryHelper.CanForceReturn(player.injury_days)) return;

        _forceReturnPlayer = player;

        var def = InjuryCatalog.ByName(player.injury_type);
        var sev = def != null
            ? def.severity
            : InjuryCatalog.SeverityOf(player.injury_total_days > 0 ? player.injury_total_days : player.injury_days);
        int risk = InjuryHelper.ForceReturnRisk(player.injury_days, sev);
        int perGame = InjuryHelper.RelapseChancePercent(risk, player.fisico);

        _forceReturnWho.text = $"{player.first_name} {player.last_name}";
        _forceReturnDiag.text = $"Diagn\u00f3stico: {player.injury_type} ({InjuryCatalog.SeverityLabel(sev)}) \u2014 {player.injury_days} d\u00edas restantes";
        // B1: además del riesgo nominal, se muestra la probabilidad REAL por partido.
        _forceReturnRisk.text =
            $"Riesgo asumido: {risk}% nominal \u00b7 {perGame}% de reca\u00edda por partido jugado";

        // Advertencia visual según el riesgo: verde (<25), dorado (25-49), rojo (≥50).
        _forceReturnRisk.RemoveFromClassList("modal-paragraph--highlight");
        _forceReturnRisk.RemoveFromClassList("force-return-risk--low");
        _forceReturnRisk.RemoveFromClassList("force-return-risk--mid");
        _forceReturnRisk.RemoveFromClassList("force-return-risk--high");
        if (risk >= 50)
            _forceReturnRisk.AddToClassList("force-return-risk--high");
        else if (risk >= 25)
            _forceReturnRisk.AddToClassList("force-return-risk--mid");
        else
            _forceReturnRisk.AddToClassList("force-return-risk--low");

        _forceReturnOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _forceReturnOverlay.AddToClassList("modal-overlay--visible");
        _forceReturnOverlay.Q<VisualElement>(null, "modal-box")?.AddToClassList("modal-box--visible");
    }

    void CloseForceReturnModal()
    {
        _forceReturnOverlay.RemoveFromClassList("modal-overlay--visible");
        _forceReturnOverlay.Q<VisualElement>(null, "modal-box")?.RemoveFromClassList("modal-box--visible");
        _forceReturnPlayer = null;
    }

    void ConfirmForceReturn()
    {
        var player = _forceReturnPlayer;
        if (player == null) { CloseForceReturnModal(); return; }

        string name = $"{player.first_name} {player.last_name}";
        string diag = player.injury_type;
        int risk = 0;

        DatabaseManager.Instance.ForceReturnFromInjury(
            player, _season?.id ?? 0, _season?.current_game_day ?? 0, _season?.current_date);
        risk = player.recovery_risk;

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = $"Alta forzada: {name}",
            body = $"El jugador vuelve antes de tiempo de su {diag.ToLowerInvariant()}. El cuerpo m\u00e9dico estima un {risk}% de riesgo de reca\u00edda hasta que se recupere del todo: descansar lo reduce, jugar lo mantiene.",
            game_day = _season?.current_game_day ?? 0,
            game_date = _season?.current_date ?? "",
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0,
            category = NewsCategory.LESIONES,
            news_type = NewsType.LESION
        });

        CloseForceReturnModal();
        ReloadData();
        Refresh();
    }

    // ── LEAGUE INJURED ──

    void OpenLeagueInjuredModal()
    {
        BuildLeagueInjuredList();
        var viewport = _root.Q<VisualElement>("LeagueInjuredScrollWrapper");
        var list = _root.Q<VisualElement>("LeagueInjuredList");
        var track = _root.Q<VisualElement>("LeagueInjuredScrollTrack");
        var thumb = _root.Q<VisualElement>("LeagueInjuredScrollThumb");
        _leagueBar = BindScrollBar(viewport, list, track, thumb);
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

        _leagueInjuredPlayers = _leagueInjuredPlayers.OrderBy(x => x.player.injury_days).ToList();

        if (_leagueInjuredPlayers.Count == 0)
        {
            var emptyLbl = new Label();
            emptyLbl.AddToClassList("league-injured-empty");
            emptyLbl.text = "No hay jugadores lesionados en la liga.";
            _leagueInjuredList.Add(emptyLbl);
            return;
        }

        for (int i = 0; i < _leagueInjuredPlayers.Count; i++)
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
    }

    // ── Scrollbar helpers ──

    class ScrollBar : IScrollBarHandle
    {
        public VisualElement Viewport;
        public VisualElement Content;
        public VisualElement Track;
        public VisualElement Thumb;
        public float ScrollY;
        public bool Dragging;
        public float StartPointerY;
        public float StartScrollY;
    }

    ScrollBar BindScrollBar(VisualElement viewport, VisualElement content,
                            VisualElement track, VisualElement thumb)
    {
        if (viewport == null || content == null || track == null || thumb == null) return null;

        // Ya enlazada en esta visita (el viewport persiste): no registrar los
        // handlers otra vez, que se acumulaban al reconstruir la lista.
        if (ScrollBarBinder.TryGet<ScrollBar>(viewport, out var bound)) return bound;


        viewport.style.overflow = Overflow.Hidden;
        viewport.style.flexDirection = FlexDirection.Column;

        var bar = new ScrollBar
        {
            Viewport = viewport,
            Content = content,
            Track = track,
            Thumb = thumb
        };

        viewport.RegisterCallback<WheelEvent>(evt =>
        {
            float max = GetScrollMax(bar);
            if (max <= 0f) return;
            bar.ScrollY = Mathf.Clamp(bar.ScrollY + evt.delta.y * 30f, 0f, max);
            ApplyScroll(bar);
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerDownEvent>(evt =>
        {
            bar.Dragging = true;
            bar.StartPointerY = evt.position.y;
            bar.StartScrollY = bar.ScrollY;
            thumb.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!bar.Dragging) return;
            float max = GetScrollMax(bar);
            float travel = track.layout.height - thumb.layout.height;
            if (max <= 0f || travel <= 0f) return;
            float delta = evt.position.y - bar.StartPointerY;
            bar.ScrollY = Mathf.Clamp(bar.StartScrollY + delta / travel * max, 0f, max);
            ApplyScroll(bar);
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (bar.Dragging)
            {
                bar.Dragging = false;
                thumb.ReleasePointer(evt.pointerId);
            }
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerCaptureOutEvent>(_ => bar.Dragging = false);

        track.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (bar.Dragging) return;
            float max = GetScrollMax(bar);
            if (max <= 0f) return;
            float page = viewport.layout.height;
            float y = evt.localPosition.y;
            float thumbTop = thumb.layout.y;
            float thumbBottom = thumbTop + thumb.layout.height;
            float target = y < thumbTop ? bar.ScrollY - page
                         : (y > thumbBottom ? bar.ScrollY + page : bar.ScrollY);
            bar.ScrollY = Mathf.Clamp(target, 0f, max);
            ApplyScroll(bar);
            evt.StopPropagation();
        });

        CursorManager.Instance?.RegisterHandCursor(thumb);
        CursorManager.Instance?.RegisterHandCursor(track);

        content.RegisterCallback<GeometryChangedEvent>(_ => ClampAndApply(bar));
        viewport.RegisterCallback<GeometryChangedEvent>(_ => ClampAndApply(bar));

        ApplyScroll(bar);
        ScrollBarBinder.Remember(viewport, bar);
        return bar;
    }

    static float GetScrollMax(ScrollBar bar)
    {
        if (bar == null || bar.Content == null || bar.Viewport == null) return 0f;
        float contentHeight = bar.Content.layout.height;
        float viewportHeight = bar.Viewport.layout.height;
        return Mathf.Max(0f, contentHeight - viewportHeight);
    }

    void ClampAndApply(ScrollBar bar)
    {
        if (bar == null) return;
        bar.ScrollY = Mathf.Clamp(bar.ScrollY, 0f, GetScrollMax(bar));
        ApplyScroll(bar);
    }

    void ApplyScroll(ScrollBar bar)
    {
        if (bar == null || bar.Content == null) return;

        bar.Content.style.top = -bar.ScrollY;

        float trackHeight = bar.Track.layout.height;
        float contentHeight = bar.Content.layout.height;
        float viewportHeight = bar.Viewport.layout.height;
        if (trackHeight <= 0f || contentHeight <= 0f) return;

        float max = Mathf.Max(0f, contentHeight - viewportHeight);
        float thumbHeight = max <= 0f ? trackHeight : Mathf.Max(36f, trackHeight * viewportHeight / contentHeight);
        thumbHeight = Mathf.Min(thumbHeight, trackHeight);

        float travel = trackHeight - thumbHeight;
        float t = max <= 0f ? 0f : Mathf.Clamp01(bar.ScrollY / max);

        bar.Thumb.style.height = thumbHeight;
        bar.Thumb.style.top = travel * t;
    }
}
