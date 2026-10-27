using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class EditorController : UIScreenController
{
    class CustomDropdown
    {
        public VisualElement Root;
        public Button Trigger;
        public VisualElement List;
        public Label ValueLabel;
        public string Title;    // nombre del filtro (p. ej. "EQUIPO"); si es null no se prefija
        public string RawValue; // valor real seleccionado (el display puede llevar prefijo)
        public string Value => string.IsNullOrEmpty(RawValue) ? (ValueLabel?.text ?? "") : RawValue;
    }
    private Button _btnReset;
    private VisualElement _loadingSpinner;
    private IVisualElementScheduledItem _spinScheduler;
    private bool _isLoading;
    private volatile bool _templateReady;
    private string _loadError;

    // Tabs
    private Button _btnTeams, _btnPlayers;
    private VisualElement _teamPanel, _playerPanel;

    // Team list
    private TextField _teamFilter;
    private VisualElement _teamList;
    private TeamData _selectedTeam;
    private int _selectedTeamId = -1;
    private List<TeamData> _allTeams;
    private readonly Dictionary<int, VisualElement> _teamListLogoElems = new();
    private CustomScrollbar.Handle _teamScroll;

    // Team detail fields
    private VisualElement _teamDetail;
    private VisualElement _teamDetailPanel;
    private TextField _teamNameInput, _teamAbbrInput, _teamCityInput;
    private TextField _teamArenaInput, _teamCapacityInput, _teamOwnerInput;
    private TextField _teamBudgetInput;
    private CustomDropdown _teamConferenceDropdown, _teamDivisionDropdown;
    private CustomDropdown _teamReputationDropdown, _teamFacilitiesDropdown, _teamObjectiveDropdown;

    // Imágenes elegidas pero aún sin guardar (se aplican al pulsar GUARDAR EQUIPO).
    private string _pendingLogoPath;
    private string _pendingArenaPath;

    // Player list
    private CustomDropdown _playerTeamFilter, _playerPosFilter;
    private TextField _playerSearch;
    private VisualElement _playerList;
    private PlayerData _selectedPlayer;
    private int _selectedPlayerId = -1;
    private List<PlayerData> _allPlayers;
    private CustomScrollbar.Handle _playerScroll;

    // Player detail fields
    private VisualElement _playerDetail;
    private VisualElement _playerDetailPanel;
    private TextField _playerFName, _playerLName;
    private CustomDropdown _playerPosDropdown, _playerTeamDropdown, _playerRoleDropdown, _playerRookieDropdown;
    private TextField _playerAge, _playerNat, _playerHt, _playerWt;
    private TextField _playerPot;
    private TextField _playerSpeed, _playerShooting, _player3pt, _playerPassing;
    private TextField _playerDribbling, _playerDefense, _playerRebounding;
    private TextField _playerAthleticism, _playerIq, _playerSteals, _playerBlocks;
    private TextField _playerMorale;
    private TextField _playerSalary, _playerContract, _playerGuaranteed, _playerServiceYears;
    private TextField _playerRings, _playerFinalsMvps, _playerFinalsPlayed, _playerSeasonMvps;
    private bool _playerTeamOptionOn, _playerPlayerOptionOn, _playerTwoWayOn;
    private VisualElement _playerTeamOptionBox, _playerPlayerOptionBox;

    // Foto elegida pero aún sin guardar (se aplica al pulsar GUARDAR JUGADOR).
    private string _pendingPhotoPath;

    // Modal de selección de país.
    private VisualElement _countryOverlay;

    // Image cache (team list logos only)
    private Dictionary<string, Sprite> _logoSprites = new();
    private VisualElement _dropdownOverlay;
    private CustomDropdown _openDropdown;
    private VisualElement _toast;

    private static readonly Dictionary<string, string> _confToDisplay = new()
    {
        { "East", "Este" },
        { "West", "Oeste" },
    };
    private static readonly Dictionary<string, string> _confFromDisplay = new()
    {
        { "Este", "East" },
        { "Oeste", "West" },
    };
    private static readonly Dictionary<string, string> _divToDisplay = new()
    {
        { "Atlantic", "Atlántico" },
        { "Central", "Central" },
        { "Southeast", "Sudeste" },
        { "Northwest", "Noroeste" },
        { "Pacific", "Pacífico" },
        { "Southwest", "Suroeste" },
    };
    private static readonly Dictionary<string, string> _divFromDisplay = new()
    {
        { "Atlántico", "Atlantic" },
        { "Central", "Central" },
        { "Sudeste", "Southeast" },
        { "Noroeste", "Northwest" },
        { "Pacífico", "Pacific" },
        { "Suroeste", "Southwest" },
    };
    private static readonly Dictionary<string, string> _posToDisplay = new()
    {
        { "PG", "Base" },
        { "SG", "Escolta" },
        { "SF", "Alero" },
        { "PF", "Ala-Pívot" },
        { "C", "Pívot" },
    };
    private static readonly Dictionary<string, string> _posFromDisplay = new()
    {
        { "Base", "PG" },
        { "Escolta", "SG" },
        { "Alero", "SF" },
        { "Ala-Pívot", "PF" },
        { "Pívot", "C" },
    };

    void OnDisable()
    {
        CloseCountryModal();
        DatabaseManager.Instance?.CloseTemplateSession();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        CursorManager.Instance?.SetDefaultCursor();

        // Root-level overlay so dropdown lists render above everything
        _dropdownOverlay = new VisualElement();
        _dropdownOverlay.style.position = Position.Absolute;
        _dropdownOverlay.style.left = 0;
        _dropdownOverlay.style.right = 0;
        _dropdownOverlay.style.top = 0;
        _dropdownOverlay.style.bottom = 0;
        _dropdownOverlay.pickingMode = PickingMode.Ignore;
        _root.Add(_dropdownOverlay);

        _toast = new VisualElement();
        _toast.AddToClassList("editor-modal-overlay");
        _root.Add(_toast);

        _loadingSpinner = _root.Q<VisualElement>("LoadingSpinner");
        _loadingSpinner.style.display = DisplayStyle.None;

        if (!string.IsNullOrEmpty(_loadError))
        {
            ShowToast(_loadError, isError: true);
            _loadError = null;
        }
    }

    protected override void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _btnReset = _root.Q<Button>("BtnReset");
        _btnTeams = _root.Q<Button>("BtnTeams");
        _btnPlayers = _root.Q<Button>("BtnPlayers");
        _teamPanel = _root.Q<VisualElement>("EditorTeamPanel");
        _playerPanel = _root.Q<VisualElement>("EditorPlayerPanel");

        _teamFilter = _root.Q<TextField>("TeamFilter");
        _teamList = _root.Q<VisualElement>("TeamList");
        _teamDetail = _root.Q<VisualElement>("TeamDetail");
        _teamDetailPanel = _teamPanel.Q<VisualElement>(className: "editor-detail-panel");

        _playerTeamFilter = WrapFilterDropdown("PlayerTeamFilter", "EQUIPO");
        _playerPosFilter = WrapFilterDropdown("PlayerPosFilter", "POSICIÓN");
        _playerSearch = _root.Q<TextField>("PlayerSearch");
        _playerList = _root.Q<VisualElement>("PlayerList");
        _playerDetail = _root.Q<VisualElement>("PlayerDetail");
        _playerDetailPanel = _playerPanel.Q<VisualElement>(className: "editor-detail-panel");

        // Barras de scroll propias: se re-enlazan en cada visita porque Unity recrea
        // el árbol del UIDocument al reactivar la pantalla (con ??= quedaban muertas).
        _teamScroll = CustomScrollbar.Bind(
            _root.Q<VisualElement>("TeamViewport"), _teamList,
            _root.Q<VisualElement>("TeamScrollTrack"), _root.Q<VisualElement>("TeamScrollThumb"));
        _playerScroll = CustomScrollbar.Bind(
            _root.Q<VisualElement>("PlayerViewport"), _playerList,
            _root.Q<VisualElement>("PlayerScrollTrack"), _root.Q<VisualElement>("PlayerScrollThumb"));
    }

    CustomDropdown WrapFilterDropdown(string name, string title)
    {
        var root = _root.Q<VisualElement>(name);
        var trigger = root.Q<Button>(className: "custom-dropdown__trigger");
        var list = root.Q<VisualElement>(className: "custom-dropdown__list");
        var value = root.Q<Label>(className: "custom-dropdown__value");

        var dd = new CustomDropdown
        {
            Root = root,
            Trigger = trigger,
            List = list,
            ValueLabel = value,
            Title = title
        };

        trigger.clicked += () =>
        {
            if (_openDropdown == dd)
                CloseAllDropdowns();
            else
                OpenDropdown(dd);
        };

        return dd;
    }

    // Fija el valor real del desplegable y su texto visible. Si el desplegable tiene
    // Title (los filtros), el texto se prefija: "EQUIPO (TODOS)", "POSICIÓN (B)"...
    void SetDropdownValue(CustomDropdown dd, string value)
    {
        dd.RawValue = value;
        dd.ValueLabel.text = string.IsNullOrEmpty(dd.Title) ? value : $"{dd.Title} ({value})";
    }

    void SetFilterDropdownItems(CustomDropdown dd, string[] items, int selectedIndex)
    {
        dd.List.Clear();
        for (int i = 0; i < items.Length; i++)
        {
            var text = items[i];
            var item = new Button();
            item.AddToClassList("custom-dropdown__item");
            item.text = text;
            if (i == selectedIndex)
            {
                item.AddToClassList("custom-dropdown__item--selected");
                SetDropdownValue(dd, text);
            }

            var captured = i;
            item.clicked += () =>
            {
                SetDropdownValue(dd, text);
                CloseAllDropdowns();
                foreach (var b in dd.List.Query<Button>(className: "custom-dropdown__item").ToList())
                    b.RemoveFromClassList("custom-dropdown__item--selected");
                item.AddToClassList("custom-dropdown__item--selected");
                FilterPlayerList();
            };

            CursorManager.Instance?.RegisterHandCursor(item);
            dd.List.Add(item);
        }
    }

    void LoadImages()
    {
        foreach (var s in TeamImages.Logos(64))
            _logoSprites[s.name] = s;
    }

    protected override void LoadData()
    {
        try
        {
            // El Editor trabaja SIEMPRE sobre el template (y su ámbito de imágenes),
            // tanto para los datos como para los logos/pabellones. Si había una
            // partida abierta, se cierra su conexión (todo está ya commiteado; para
            // volver a jugar hay que cargar la partida, que reabre su save_{n}).
            DatabaseManager.Instance.EnsureTemplateDb();
            DatabaseManager.Instance.InitTemplateSession();

            // Cargar las imágenes con la sesión/ámbito del template ya fijado.
            LoadImages();

            _allTeams = DatabaseManager.Instance.GetAllTeams() ?? new List<TeamData>();
            _allPlayers = DatabaseManager.Instance.Db.Table<PlayerData>().ToList() ?? new List<PlayerData>();

            var teamChoices = new List<string> { "TODOS" };
            teamChoices.AddRange(_allTeams.Select(t => $"{t.abbreviation} - {t.name}"));
            SetFilterDropdownItems(_playerTeamFilter, teamChoices.ToArray(), 0);

            SetFilterDropdownItems(_playerPosFilter, new[] { "TODOS", "B", "E", "A", "AP", "P" }, 0);
            _loadError = null;
        }
        catch (System.Exception ex)
        {
            _allTeams = new List<TeamData>();
            _allPlayers = new List<PlayerData>();
            _loadError = "No se pudo cargar la base de datos del Editor: " + DescribeSeedError(ex);
            Debug.LogError($"[Editor] Error cargando la base de datos: {ex}");
        }
    }

    /// <summary>
    /// Mensaje corto para los modales del Editor. El detalle completo del auditor se
    /// queda en la consola (un volcado de cientos de líneas no cabe en el modal).
    /// </summary>
    static string DescribeSeedError(System.Exception ex)
    {
        if (ex is SeedAudit.SeedAuditException audit)
        {
            Debug.Log("[Editor] Informe completo del seed:\n" + audit.FullReport);
            return audit.UserSummary;
        }
        return ex.Message;
    }

    protected override void RegisterCallbacks()
    {
        var actionBtn = _root.Q<Button>("BtnAction");
        if (actionBtn != null)
            actionBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.MainMenu); });
        else
            Debug.LogError("[Editor] BtnAction not found in UXML!");

        _btnReset?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ResetToDefaults(); });
        _btnTeams?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SwitchTab("teams"); });
        _btnPlayers?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SwitchTab("players"); });

        _teamFilter?.RegisterValueChangedCallback(_ => FilterTeamList());
        _playerSearch?.RegisterValueChangedCallback(_ => FilterPlayerList());

        // Cursor hand on UXML buttons
        if (CursorManager.Instance != null)
        {
            if (actionBtn != null) CursorManager.Instance.RegisterHandCursor(actionBtn);
            if (_btnReset != null) CursorManager.Instance.RegisterHandCursor(_btnReset);
            if (_btnTeams != null) CursorManager.Instance.RegisterHandCursor(_btnTeams);
            if (_btnPlayers != null) CursorManager.Instance.RegisterHandCursor(_btnPlayers);
        }
    }

    protected override void Refresh()
    {
        SwitchTab("teams");
    }

    void ResetToDefaults()
    {
        if (_isLoading) return;
        // UI-09: acción destructiva (borra el template) → confirmación explícita.
        ShowConfirm("RESETEAR",
            "Se borrarán el template (datos base de nuevas partidas) y todas las imágenes " +
            "personalizadas del editor. Las partidas ya guardadas NO se tocan. ¿Continuar?",
            () => StartCoroutine(ResetToDefaultsCoroutine()));
    }

    IEnumerator ResetToDefaultsCoroutine()
    {
        ShowLoading();
        yield return null;

        // Restaurar imágenes originales: borrar el overlay custom (los logos y
        // pabellones empaquetados en Resources nunca se modifican).
        TeamImages.ClearCustom();

        DatabaseManager.Instance.CloseTemplateSession();

        // Ya no se borra el template antes de construir: el nuevo se genera en `.tmp`,
        // se audita y solo entonces sustituye al anterior (que queda como `.bak`). Si
        // el seed es inválido, el template que había sigue ahí.
        _templateReady = false;
        string errorMsg = null;

        // Application.persistentDataPath solo se puede consultar en el main thread.
        // Resolvemos la ruta aquí y el hilo de fondo solo recibe el path ya calculado.
        string templatePath = DatabaseManager.Instance.TemplateDbPath;

        Task.Run(() =>
        {
            try
            {
                DatabaseManager.Instance.RebuildTemplateDb(templatePath);
            }
            catch (System.Exception ex)
            {
                errorMsg = DescribeSeedError(ex);
                Debug.LogError($"[Editor] Error building template: {ex}");
            }
            finally
            {
                _templateReady = true;
            }
        });

        yield return new WaitWhile(() => !_templateReady);

        if (!string.IsNullOrEmpty(errorMsg))
        {
            HideLoading();
            ShowToast("Error al restablecer la base de datos: " + errorMsg, isError: true);
            yield break;
        }

        try
        {
            DatabaseManager.Instance.InitTemplateSession();
            LoadData();
            if (!string.IsNullOrEmpty(_loadError))
            {
                ShowToast(_loadError, isError: true);
                _loadError = null;
                yield break;
            }
            Refresh();
            ShowToast("Base de datos restablecida correctamente");
        }
        finally
        {
            HideLoading();
        }
    }

    void ShowLoading()
    {
        if (_isLoading) return;
        _isLoading = true;
        if (_btnReset != null) _btnReset.SetEnabled(false);
        _loadingSpinner.style.display = DisplayStyle.Flex;

        _spinScheduler = _root.schedule.Execute(() =>
        {
            if (_loadingSpinner == null) return;
            var current = _loadingSpinner.style.rotate;
            float angle = current.value.angle.value + 15f;
            if (angle >= 360f) angle -= 360f;
            _loadingSpinner.style.rotate = new Rotate(Angle.Degrees(angle));
        }).Every(30);
    }

    void HideLoading()
    {
        _spinScheduler?.Pause();
        _spinScheduler = null;
        _loadingSpinner.style.display = DisplayStyle.None;
        if (_btnReset != null) _btnReset.SetEnabled(true);
        _isLoading = false;
    }

    void SwitchTab(string tab)
    {
        _btnTeams.RemoveFromClassList("editor-tab--active");
        _btnPlayers.RemoveFromClassList("editor-tab--active");

        if (tab == "teams")
        {
            _btnTeams.AddToClassList("editor-tab--active");
            _teamPanel.style.display = DisplayStyle.Flex;
            _playerPanel.style.display = DisplayStyle.None;
            BuildTeamList();
        }
        else
        {
            _btnPlayers.AddToClassList("editor-tab--active");
            _teamPanel.style.display = DisplayStyle.None;
            _playerPanel.style.display = DisplayStyle.Flex;
            BuildPlayerList();
        }
    }

    // ══════════════════════════════════════
    // TEAMS
    // ══════════════════════════════════════

    void BuildTeamList()
    {
        _teamList.Clear();
        _teamListLogoElems.Clear();
        _selectedTeam = null;

        string filter = _teamFilter?.value?.ToLower() ?? "";
        var filtered = string.IsNullOrEmpty(filter)
            ? _allTeams
            : _allTeams.Where(t =>
                t.name.ToLower().Contains(filter) ||
                t.city.ToLower().Contains(filter) ||
                (t.abbreviation ?? "").ToLower().Contains(filter)).ToList();

        var items = new List<VisualElement>();
        for (int i = 0; i < filtered.Count; i++)
        {
            var team = filtered[i];
            var item = new VisualElement();
            item.AddToClassList("editor-list-item");

            var logo = new VisualElement();
            logo.AddToClassList("editor-list-item-logo");
            if (_logoSprites.TryGetValue(team.logo ?? "", out var sprite))
                logo.style.backgroundImage = new StyleBackground(sprite);
            item.Add(logo);
            _teamListLogoElems[team.id] = logo;

            var nameLbl = new Label(team.name.ToUpper());
            nameLbl.AddToClassList("editor-list-item-name");
            var subLbl = new Label(team.abbreviation);
            subLbl.AddToClassList("editor-list-item-sub");
            item.Add(nameLbl);
            item.Add(subLbl);

            var captured = team;
            var capturedItem = item;
            item.RegisterCallback<ClickEvent>(_ => SelectTeam(captured, capturedItem));
            CursorManager.Instance?.RegisterHandCursor(item);
            _teamList.Add(item);
            items.Add(item);
        }

        // Mantener el equipo seleccionado entre visitas al Editor; si ya no está
        // visible (filtro) se selecciona el primero.
        int sel = filtered.FindIndex(t => t.id == _selectedTeamId);
        if (sel < 0 && filtered.Count > 0) sel = 0;
        if (sel >= 0) SelectTeam(filtered[sel], items[sel]);
        CustomScrollbar.Reset(_teamScroll);
    }

    void FilterTeamList()
    {
        BuildTeamList();
    }

    void SelectTeam(TeamData team, VisualElement listItem)
    {
        // Al cambiar de equipo se descarta cualquier imagen en borrador.
        _pendingLogoPath = null;
        _pendingArenaPath = null;

        _selectedTeam = team;
        _selectedTeamId = team.id;
        foreach (var child in _teamList.Children())
            child.RemoveFromClassList("editor-list-item--selected");
        if (listItem != null)
            listItem.AddToClassList("editor-list-item--selected");
        BuildTeamDetail();
    }

    void BuildTeamDetail()
    {
        if (_selectedTeam == null) return;
        _teamDetail.Clear();

        var t = _selectedTeam;
        RecalcTeamRatingsFromPlayers(t);

        var arenaSprite = _pendingArenaPath != null
            ? TeamImages.PreviewSprite(_pendingArenaPath)
            : TeamImages.Arena(t.logo);

        // ── BANNER ──
        var banner = new VisualElement();
        banner.AddToClassList("editor-team-banner");

        var bannerBg = new VisualElement();
        bannerBg.AddToClassList("editor-team-banner-bg");
        if (arenaSprite != null) bannerBg.style.backgroundImage = new StyleBackground(arenaSprite);
        banner.Add(bannerBg);

        var bannerContent = new VisualElement();
        bannerContent.AddToClassList("editor-team-banner-content");

        var bannerLogo = new VisualElement();
        bannerLogo.AddToClassList("editor-team-banner-logo");
        var logoSprite = _pendingLogoPath != null
            ? TeamImages.PreviewSprite(_pendingLogoPath)
            : TeamImages.Logo(120, t.logo);
        if (logoSprite != null) bannerLogo.style.backgroundImage = new StyleBackground(logoSprite);
        bannerContent.Add(bannerLogo);

        var bannerInfo = new VisualElement();
        bannerInfo.AddToClassList("editor-team-banner-info");

        var nameRow = new VisualElement();
        nameRow.AddToClassList("editor-team-banner-name-row");
        var nameLbl = new Label(t.name.ToUpper());
        nameLbl.AddToClassList("editor-team-banner-name");
        nameRow.Add(nameLbl);
        var abbrBadge = new Label(t.abbreviation);
        abbrBadge.AddToClassList("editor-team-banner-abbr");
        nameRow.Add(abbrBadge);
        bannerInfo.Add(nameRow);

        var cityLbl = new Label(t.city);
        cityLbl.AddToClassList("editor-team-banner-city");
        bannerInfo.Add(cityLbl);

        var metaRow = new VisualElement();
        metaRow.AddToClassList("editor-team-banner-meta");
        var confLbl = new Label($"CONFERENCIA  {(_confToDisplay.TryGetValue(t.conference, out var cd) ? cd : t.conference)}");
        confLbl.AddToClassList("editor-team-banner-meta-item");
        metaRow.Add(confLbl);
        var divLbl = new Label($"DIVISIÓN  {(_divToDisplay.TryGetValue(t.division, out var dd) ? dd : t.division)}");
        divLbl.AddToClassList("editor-team-banner-meta-item");
        metaRow.Add(divLbl);
        bannerInfo.Add(metaRow);

        bannerContent.Add(bannerInfo);

        var btnChangeLogo = new Button();
        btnChangeLogo.text = LocalizationService.T("ui.code.editor_controller.cambiar_logo");
        btnChangeLogo.AddToClassList("editor-banner-btn");
        btnChangeLogo.clicked += () => { PlayClick(); ChangeLogo(t); };
        CursorManager.Instance?.RegisterHandCursor(btnChangeLogo);
        bannerContent.Add(btnChangeLogo);

        banner.Add(bannerContent);
        _teamDetail.Add(banner);

        // ── PANELS ──
        var panelsRow = new VisualElement();
        panelsRow.AddToClassList("editor-team-panels-row");

        // INFORMACIÓN GENERAL
        var infoPanel = new VisualElement();
        infoPanel.AddToClassList("editor-section-panel");
        infoPanel.AddToClassList("editor-section-panel--info");
        AddPanelHeader(infoPanel, "INFORMACIÓN GENERAL");

        var infoFields = new VisualElement();
        infoFields.AddToClassList("editor-panel-fields");
        var infoLeft = new VisualElement();
        infoLeft.AddToClassList("editor-panel-col");
        _teamNameInput = AddInput(infoLeft, "Nombre del equipo", t.name);
        SetLettersOnly(_teamNameInput, 30);
        _teamCityInput = AddInput(infoLeft, "Ciudad", t.city);
        SetLettersOnly(_teamCityInput, 30);
        _teamAbbrInput = AddInput(infoLeft, "Abreviatura", t.abbreviation);
        SetAbbreviationInput(_teamAbbrInput);
        _teamConferenceDropdown = AddDropdown(infoLeft, "Conferencia", new[] { "Este", "Oeste" }, _confToDisplay.TryGetValue(t.conference, out var confD) ? confD : t.conference);
        _teamDivisionDropdown = AddDropdown(infoLeft, "División", new[] { "Atlántico", "Central", "Sudeste", "Noroeste", "Pacífico", "Suroeste" }, _divToDisplay.TryGetValue(t.division, out var divD) ? divD : t.division);

        var infoRight = new VisualElement();
        infoRight.AddToClassList("editor-panel-col");
        var coachInput = AddInput(infoRight, "Entrenador", DatabaseManager.Instance.GetManagerNameByTeamId(t.id) ?? "—");
        coachInput.isReadOnly = true;
        _teamOwnerInput = AddInput(infoRight, "Propietario", t.owner);
        SetLettersOnly(_teamOwnerInput, 30);
        _teamBudgetInput = AddInput(infoRight, "Presupuesto salarial", t.budget.ToString());
        SetDigitsOnly(_teamBudgetInput);
        _teamCapacityInput = AddInput(infoRight, "Aforo del pabellón", t.capacity.ToString());
        SetDigitsOnly(_teamCapacityInput);
        _teamReputationDropdown = AddDropdown(infoRight, "Reputación", new[] { "1", "2", "3", "4", "5" }, t.reputation.ToString());
        _teamFacilitiesDropdown = AddDropdown(infoRight, "Instalaciones", new[] { "1", "2", "3", "4", "5" }, t.facilities.ToString());
        _teamObjectiveDropdown = AddDropdown(infoRight, "Objetivo", new[] { "Campeonato", "Playoffs", "Play-In", "Zona tranquila" }, t.objective);

        infoFields.Add(infoLeft);
        infoFields.Add(infoRight);
        infoPanel.Add(infoFields);
        panelsRow.Add(infoPanel);

        // ESTADIO
        var stadiumPanel = new VisualElement();
        stadiumPanel.AddToClassList("editor-section-panel");
        stadiumPanel.AddToClassList("editor-section-panel--stadium");
        AddPanelHeader(stadiumPanel, "ESTADIO");

        var arenaImg = new VisualElement();
        arenaImg.AddToClassList("editor-stadium-image");
        if (arenaSprite != null) arenaImg.style.backgroundImage = new StyleBackground(arenaSprite);
        stadiumPanel.Add(arenaImg);

        _teamArenaInput = AddInput(stadiumPanel, "Nombre del pabellón", t.arena);
        SetLettersOnly(_teamArenaInput, 30);
        _teamArenaInput.parent.AddToClassList("editor-stadium-field");

        var statsRow = new VisualElement();
        statsRow.AddToClassList("editor-stadium-stats");
        statsRow.Add(BuildStadiumStat("CAPACIDAD", t.capacity.ToString("N0")));
        var instStat = BuildStadiumStat("INSTALACIONES", t.facilities.ToString());
        instStat.AddToClassList("editor-stadium-stat--gap");
        statsRow.Add(instStat);
        stadiumPanel.Add(statsRow);

        var btnChangeArena = new Button();
        btnChangeArena.text = LocalizationService.T("ui.code.editor_controller.cambiar_imagen");
        btnChangeArena.AddToClassList("editor-stadium-btn");
        btnChangeArena.clicked += () => { PlayClick(); ChangeArena(t); };
        CursorManager.Instance?.RegisterHandCursor(btnChangeArena);
        stadiumPanel.Add(btnChangeArena);

        panelsRow.Add(stadiumPanel);
        _teamDetail.Add(panelsRow);

        // Save button outside scroll view, fixed at bottom of panel
        var oldBtn = _teamDetailPanel.Q<Button>("SaveBtn");
        oldBtn?.RemoveFromHierarchy();
        AddSaveBtn(_teamDetailPanel, "GUARDAR EQUIPO", SaveTeam, "SaveBtn");
    }

    void AddPanelHeader(VisualElement parent, string text)
    {
        var header = new VisualElement();
        header.AddToClassList("editor-panel-header");
        var lbl = new Label(text);
        lbl.AddToClassList("editor-panel-header-title");
        header.Add(lbl);
        parent.Add(header);
    }

    VisualElement BuildStadiumStat(string label, string value)
    {
        var item = new VisualElement();
        item.AddToClassList("editor-stadium-stat");
        var v = new Label(value);
        v.AddToClassList("editor-stadium-stat-value");
        item.Add(v);
        var l = new Label(label);
        l.AddToClassList("editor-stadium-stat-label");
        item.Add(l);
        return item;
    }

    // Abre el diálogo nativo del SO; si no está disponible, cae al navegador in-game.
    void PickImage(string title, System.Action<string> onSelected)
    {
        if (FileDialogHelper.TryOpenImage(title, onSelected)) return;
        OpenFileBrowser(title, onSelected);
    }

    void ChangeLogo(TeamData t)
    {
        PickImage("SELECCIONA UN LOGO PNG (200 × 200)", path =>
        {
            if (!TeamImages.ValidateLogoFile(path, out var error))
            {
                ShowToast(error, isError: true);
                return;
            }
            // Borrador: se aplica al pulsar GUARDAR EQUIPO.
            _pendingLogoPath = path;
            BuildTeamDetail();
        });
    }

    void ChangeArena(TeamData t)
    {
        PickImage("SELECCIONA UNA IMAGEN PNG (1380 × 752)", path =>
        {
            if (!TeamImages.ValidateArenaFile(path, out var error))
            {
                ShowToast(error, isError: true);
                return;
            }
            // Borrador: se aplica al pulsar GUARDAR EQUIPO.
            _pendingArenaPath = path;
            BuildTeamDetail();
        });
    }

    void RecalcTeamRatingsFromPlayers(TeamData team)
    {
        var teamPlayers = _allPlayers.Where(p => p.team_id == team.id).ToList();
        if (teamPlayers.Count == 0)
        {
            team.attack = 0;
            team.defense = 0;
            team.overall = 0;
            return;
        }

        float attackSum = 0, defenseSum = 0;
        foreach (var p in teamPlayers)
        {
            attackSum += p.shooting + p.three_point + p.passing + p.dribbling + p.speed;
            defenseSum += p.defense + p.rebounding + p.steals + p.blocks + p.athleticism;
        }
        int count = teamPlayers.Count;
        team.attack = Mathf.RoundToInt(attackSum / (5f * count));
        team.defense = Mathf.RoundToInt(defenseSum / (5f * count));
        team.overall = (team.attack + team.defense) / 2;
    }

    void SaveTeam()
    {
        if (_selectedTeam == null) return;
        var t = _selectedTeam;
        t.name = _teamNameInput.value;
        t.abbreviation = _teamAbbrInput.value;
        t.city = _teamCityInput.value;
        t.conference = _confFromDisplay.TryGetValue(_teamConferenceDropdown.Value, out var conf) ? conf : _teamConferenceDropdown.Value;
        t.division = _divFromDisplay.TryGetValue(_teamDivisionDropdown.Value, out var div) ? div : _teamDivisionDropdown.Value;
        t.arena = _teamArenaInput.value;
        int.TryParse(_teamCapacityInput.value, out int cap); t.capacity = cap;
        t.owner = _teamOwnerInput.value;
        long.TryParse(_teamBudgetInput.value, out long bud); t.budget = bud;
        int.TryParse(_teamReputationDropdown.Value, out int rep); t.reputation = rep;
        int.TryParse(_teamFacilitiesDropdown.Value, out int fac); t.facilities = fac;
        t.objective = _teamObjectiveDropdown.Value;

        // Aplicar las imágenes en borrador (si las hay) junto con los campos.
        if (_pendingLogoPath != null)
        {
            if (!TeamImages.SetLogo(t.logo, _pendingLogoPath, out var logoError))
            {
                ShowToast(logoError, isError: true);
                return;
            }
            _pendingLogoPath = null;
        }
        if (_pendingArenaPath != null)
        {
            if (!TeamImages.SetArena(t.logo, _pendingArenaPath, out var arenaError))
            {
                ShowToast(arenaError, isError: true);
                return;
            }
            _pendingArenaPath = null;
        }

        DatabaseManager.Instance.UpdateTeam(t);
        _logoSprites.Clear();
        LoadImages();
        // Actualizar el escudo del equipo en la lista de la izquierda.
        if (_teamListLogoElems.TryGetValue(t.id, out var listLogo)
            && _logoSprites.TryGetValue(t.logo ?? "", out var listSprite))
            listLogo.style.backgroundImage = new StyleBackground(listSprite);
        // Refrescar el detalle para que los stats (aforo, instalaciones, etc.)
        // reflejen los valores recién guardados.
        BuildTeamDetail();
        ShowToast("Equipo guardado correctamente");
    }

    // ══════════════════════════════════════
    // PLAYERS
    // ══════════════════════════════════════

    void BuildPlayerList()
    {
        _playerList.Clear();
        _selectedPlayer = null;

        string teamFilter = _playerTeamFilter?.Value ?? "TODOS";
        string posFilter = _playerPosFilter?.Value ?? "TODOS";
        string search = _playerSearch?.value?.ToLower() ?? "";

        int? teamId = null;
        if (teamFilter != "TODOS")
        {
            var parts = teamFilter.Split(" - ");
            var team = _allTeams.FirstOrDefault(t => t.abbreviation == parts[0]);
            if (team != null) teamId = team.id;
        }

        var query = _allPlayers.AsEnumerable();
        if (teamId.HasValue) query = query.Where(p => p.team_id == teamId.Value);
        if (posFilter != "TODOS") query = query.Where(p => PositionCodes.GetShort(p.position) == posFilter);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => $"{p.first_name} {p.last_name}".ToLower().Contains(search));

        var results = query.OrderBy(p => _allTeams.Find(t => t.id == p.team_id)?.abbreviation ?? "ZZZ")
                          .ThenBy(p => p.last_name).ToList();

        var items = new List<VisualElement>();
        for (int i = 0; i < results.Count; i++)
        {
            var player = results[i];
            var item = new VisualElement();
            item.AddToClassList("editor-list-item");

            var team = _allTeams.Find(t => t.id == player.team_id);
            var logo = new VisualElement();
            logo.AddToClassList("editor-list-item-logo");
            if (team != null && _logoSprites.TryGetValue(team.logo ?? "", out var sprite))
                logo.style.backgroundImage = new StyleBackground(sprite);
            item.Add(logo);

            var abbr = team?.abbreviation ?? "FA";
            var nameLbl = new Label($"{player.first_name} {player.last_name}");
            nameLbl.AddToClassList("editor-list-item-name");
            var subLbl = new Label($"{abbr} | {PositionCodes.GetShort(player.position)} | {player.overall}");
            subLbl.AddToClassList("editor-list-item-sub");
            item.Add(nameLbl);
            item.Add(subLbl);

            var captured = player;
            var capturedItem = item;
            item.RegisterCallback<ClickEvent>(_ => SelectPlayer(captured, capturedItem));
            CursorManager.Instance?.RegisterHandCursor(item);
            _playerList.Add(item);
            items.Add(item);
        }

        // Mantener el jugador seleccionado entre visitas; si no está, el primero.
        int sel = results.FindIndex(p => p.id == _selectedPlayerId);
        if (sel < 0 && results.Count > 0) sel = 0;
        if (sel >= 0) SelectPlayer(results[sel], items[sel]);
        CustomScrollbar.Reset(_playerScroll);
    }

    void FilterPlayerList()
    {
        BuildPlayerList();
    }

    void SelectPlayer(PlayerData player, VisualElement listItem)
    {
        _pendingPhotoPath = null;
        _selectedPlayer = player;
        _selectedPlayerId = player.id;
        foreach (var child in _playerList.Children())
            child.RemoveFromClassList("editor-list-item--selected");
        if (listItem != null)
            listItem.AddToClassList("editor-list-item--selected");
        BuildPlayerDetail();
    }

    void BuildPlayerDetail()
    {
        if (_selectedPlayer == null) return;
        _playerDetail.Clear();

        var p = _selectedPlayer;
        var team = _allTeams.Find(t => t.id == p.team_id);

        var row = new VisualElement();
        row.AddToClassList("editor-player-row");

        var leftCol = new VisualElement();
        leftCol.AddToClassList("editor-player-col-left");
        var rightCol = new VisualElement();
        rightCol.AddToClassList("editor-player-col-right");

        // ── BANNER ──
        leftCol.Add(BuildPlayerBanner(p, team));

        // ── FILA: POSICIÓN | EQUIPO | ROOKIE ──
        leftCol.Add(BuildPlayerTopRow(p));

        // ── INFORMACIÓN PERSONAL ──
        var infoPanel = new VisualElement();
        infoPanel.AddToClassList("editor-section-panel");
        infoPanel.AddToClassList("editor-player-panel");
        AddPanelHeader(infoPanel, "INFORMACIÓN PERSONAL");

        var infoFields = new VisualElement();
        infoFields.AddToClassList("editor-panel-fields");

        var infoLeft = new VisualElement();
        infoLeft.AddToClassList("editor-panel-col");
        _playerFName = AddInput(infoLeft, "Nombre", p.first_name);
        SetLettersOnly(_playerFName, 20);
        _playerLName = AddInput(infoLeft, "Apellidos", p.last_name);
        SetLettersOnly(_playerLName, 25);

        // País: caja de solo lectura + botón CAMBIAR (modal con la lista de países).
        var natRow = new VisualElement();
        natRow.AddToClassList("editor-field-row");
        var natLbl = new Label(LocalizationService.T("ui.code.editor_controller.pais"));
        natLbl.AddToClassList("editor-field-label");
        natRow.Add(natLbl);

        _playerNat = new TextField { value = p.nationality };
        _playerNat.AddToClassList("editor-field-input");
        _playerNat.AddToClassList("editor-field-input--narrow");
        _playerNat.isReadOnly = true;
        natRow.Add(_playerNat);

        var natBtn = new Button();
        natBtn.text = LocalizationService.T("ui.code.editor_controller.cambiar");
        natBtn.AddToClassList("editor-field-btn");
        natBtn.clicked += () => { PlayClick(); OpenCountryModal(); };
        CursorManager.Instance?.RegisterHandCursor(natBtn);
        natRow.Add(natBtn);

        infoLeft.Add(natRow);

        var infoRight = new VisualElement();
        infoRight.AddToClassList("editor-panel-col");
        _playerHt = AddInput(infoRight, "Altura (cm)", p.height_cm.ToString());
        SetDigitsOnly(_playerHt, 3);
        _playerWt = AddInput(infoRight, "Peso (kg)", p.weight_kg.ToString());
        SetDigitsOnly(_playerWt, 3);
        _playerAge = AddInput(infoRight, "Edad", p.age.ToString());
        SetDigitsOnly(_playerAge, 2);
        string[] roleChoices = { "Estrella", "Titular", "Banquillo", "Último recurso" };
        _playerRoleDropdown = AddDropdown(infoRight, "Rol", roleChoices, roleChoices[(int)p.role]);

        infoFields.Add(infoLeft);
        infoFields.Add(infoRight);
        infoPanel.Add(infoFields);
        leftCol.Add(infoPanel);

        // ── HISTORIAL ──
        var histPanel = new VisualElement();
        histPanel.AddToClassList("editor-section-panel");
        histPanel.AddToClassList("editor-player-panel");
        AddPanelHeader(histPanel, "HISTORIAL");

        var histFields = new VisualElement();
        histFields.AddToClassList("editor-panel-fields");
        var histLeft = new VisualElement();
        histLeft.AddToClassList("editor-panel-col");
        _playerRings = AddInput(histLeft, "Anillos", p.rings.ToString());
        SetDigitsOnly(_playerRings, 2);
        _playerFinalsMvps = AddInput(histLeft, "MVP de las Finales", p.finals_mvps.ToString());
        SetDigitsOnly(_playerFinalsMvps, 2);
        var histRight = new VisualElement();
        histRight.AddToClassList("editor-panel-col");
        _playerFinalsPlayed = AddInput(histRight, "Finales jugadas", p.finals_played.ToString());
        SetDigitsOnly(_playerFinalsPlayed, 2);
        _playerSeasonMvps = AddInput(histRight, "MVP Temporada", p.season_mvps.ToString());
        SetDigitsOnly(_playerSeasonMvps, 2);
        histFields.Add(histLeft);
        histFields.Add(histRight);
        histPanel.Add(histFields);
        leftCol.Add(histPanel);

        // ── COLUMNA DERECHA ──
        rightCol.Add(BuildPlayerAttributesPanel(p));
        rightCol.Add(BuildPlayerContractPanel(p));

        row.Add(leftCol);
        row.Add(rightCol);
        _playerDetail.Add(row);

        AddSaveBtn(_playerDetailPanel, "GUARDAR JUGADOR", SavePlayer, "SaveBtn");
    }

    VisualElement BuildPlayerBanner(PlayerData p, TeamData team)
    {
        var banner = new VisualElement();
        banner.AddToClassList("editor-player-banner");

        var bg = new VisualElement();
        bg.AddToClassList("editor-player-banner-bg");
        if (team != null)
        {
            var arena = TeamImages.Arena(team.logo);
            if (arena != null) bg.style.backgroundImage = new StyleBackground(arena);
        }
        banner.Add(bg);

        var content = new VisualElement();
        content.AddToClassList("editor-player-banner-content");

        var photoCol = new VisualElement();
        photoCol.AddToClassList("editor-player-banner-photo-col");

        var photo = new VisualElement();
        photo.AddToClassList("editor-player-banner-photo");
        SetPlayerPhoto(photo, p);
        photoCol.Add(photo);

        var btnPhoto = new Button();
        btnPhoto.text = LocalizationService.T("ui.code.editor_controller.cambiar_imagen");
        btnPhoto.AddToClassList("editor-player-banner-photo-btn");
        btnPhoto.clicked += () => { PlayClick(); ChangePlayerPhoto(p); };
        CursorManager.Instance?.RegisterHandCursor(btnPhoto);
        photoCol.Add(btnPhoto);

        content.Add(photoCol);

        var info = new VisualElement();
        info.AddToClassList("editor-player-banner-info");

        var nameRow = new VisualElement();
        nameRow.AddToClassList("editor-player-banner-name-row");
        var name = new Label($"{p.first_name} {p.last_name}".ToUpper());
        name.AddToClassList("editor-player-banner-name");
        nameRow.Add(name);
        var badge = new Label($"{PositionCodes.GetShort(p.position)} | {p.overall}");
        badge.AddToClassList("editor-player-banner-badge");
        nameRow.Add(badge);
        info.Add(nameRow);

        var teamRow = new VisualElement();
        teamRow.AddToClassList("editor-player-banner-team");
        var teamLogo = new VisualElement();
        teamLogo.AddToClassList("editor-player-banner-team-logo");
        if (team != null)
        {
            var logo = TeamImages.Logo(64, team.logo);
            if (logo != null) teamLogo.style.backgroundImage = new StyleBackground(logo);
        }
        teamRow.Add(teamLogo);
        var teamName = new Label(team != null ? team.name : "AGENTE LIBRE");
        teamName.AddToClassList("editor-player-banner-team-name");
        teamRow.Add(teamName);
        info.Add(teamRow);

        content.Add(info);
        banner.Add(content);
        return banner;
    }

    void SetPlayerPhoto(VisualElement elem, PlayerData p)
    {
        if (_pendingPhotoPath != null)
        {
            var sp = TeamImages.PreviewSprite(_pendingPhotoPath);
            if (sp != null) { elem.style.backgroundImage = new StyleBackground(sp); return; }
        }
        var tex = PlayerPhotoHelper.Load(p.id, p.photo);
        if (tex != null) elem.style.backgroundImage = new StyleBackground(tex);
    }

    VisualElement BuildPlayerTopRow(PlayerData p)
    {
        var row = new VisualElement();
        row.AddToClassList("editor-topinfo-row");

        var posBox = MakeTopInfoBox("POSICIÓN");
        _playerPosDropdown = AddDropdownBox(posBox, new[] { "Base", "Escolta", "Alero", "Ala-Pívot", "Pívot" }, _posToDisplay.TryGetValue(p.position, out var posD) ? posD : p.position);
        row.Add(posBox);

        var teamBox = MakeTopInfoBox("EQUIPO");
        var teamChoices = new List<string> { "0 - AGENTE LIBRE" };
        teamChoices.AddRange(_allTeams.Select(t => $"{t.id} - {t.name}"));
        var selTeam = _allTeams.Find(t => t.id == p.team_id);
        var teamDefault = p.team_id == 0 ? "0 - AGENTE LIBRE" : $"{p.team_id} - {selTeam?.name ?? ""}";
        _playerTeamDropdown = AddDropdownBox(teamBox, teamChoices.ToArray(), teamDefault);
        row.Add(teamBox);

        var rookieBox = MakeTopInfoBox("ROOKIE");
        _playerRookieDropdown = AddDropdownBox(rookieBox, new[] { "No", "Sí" }, p.is_rookie == 1 ? "Sí" : "No");
        row.Add(rookieBox);

        return row;
    }

    VisualElement MakeTopInfoBox(string title)
    {
        var box = new VisualElement();
        box.AddToClassList("editor-topinfo-box");
        var lbl = new Label(title);
        lbl.AddToClassList("editor-topinfo-title");
        box.Add(lbl);
        return box;
    }

    CustomDropdown AddDropdownBox(VisualElement box, string[] choices, string selected)
    {
        var dd = BuildCustomDropdown(choices, selected);
        dd.Root.AddToClassList("editor-topinfo-dropdown");
        box.Add(dd.Root);
        return dd;
    }

    VisualElement BuildPlayerAttributesPanel(PlayerData p)
    {
        var panel = new VisualElement();
        panel.AddToClassList("editor-section-panel");
        panel.AddToClassList("editor-player-panel");
        panel.AddToClassList("editor-player-panel--side");
        AddPanelHeader(panel, "ATRIBUTOS");

        var grid = new VisualElement();
        grid.AddToClassList("editor-attr-grid");

        var col1 = new VisualElement();
        col1.AddToClassList("editor-attr-col");
        _playerPot = AddInput(col1, "Potencial", p.potential.ToString());
        SetDigitsOnly(_playerPot, 2);
        _playerSpeed = AddInput(col1, "Velocidad", p.speed.ToString());
        SetDigitsOnly(_playerSpeed, 2);
        _playerShooting = AddInput(col1, "Tiro", p.shooting.ToString());
        SetDigitsOnly(_playerShooting, 2);
        _player3pt = AddInput(col1, "Triple", p.three_point.ToString());
        SetDigitsOnly(_player3pt, 2);
        _playerPassing = AddInput(col1, "Pase", p.passing.ToString());
        SetDigitsOnly(_playerPassing, 2);
        _playerDribbling = AddInput(col1, "Regate", p.dribbling.ToString());
        SetDigitsOnly(_playerDribbling, 2);
        _playerDefense = AddInput(col1, "Defensa", p.defense.ToString());
        SetDigitsOnly(_playerDefense, 2);

        var col2 = new VisualElement();
        col2.AddToClassList("editor-attr-col");
        _playerRebounding = AddInput(col2, "Rebote", p.rebounding.ToString());
        SetDigitsOnly(_playerRebounding, 2);
        _playerAthleticism = AddInput(col2, "Atletismo", p.athleticism.ToString());
        SetDigitsOnly(_playerAthleticism, 2);
        _playerIq = AddInput(col2, "IQ", p.iq.ToString());
        SetDigitsOnly(_playerIq, 2);
        _playerSteals = AddInput(col2, "Robos", p.steals.ToString());
        SetDigitsOnly(_playerSteals, 2);
        _playerBlocks = AddInput(col2, "Tapones", p.blocks.ToString());
        SetDigitsOnly(_playerBlocks, 2);
        _playerMorale = AddInput(col2, "Moral", p.morale.ToString());
        SetDigitsOnly(_playerMorale, 2);

        grid.Add(col1);
        grid.Add(col2);
        panel.Add(grid);
        return panel;
    }

    VisualElement BuildPlayerContractPanel(PlayerData p)
    {
        var panel = new VisualElement();
        panel.AddToClassList("editor-section-panel");
        panel.AddToClassList("editor-player-panel");
        panel.AddToClassList("editor-player-panel--side");
        AddPanelHeader(panel, "CONTRATO");

        var fields = new VisualElement();
        fields.AddToClassList("editor-contract-fields");

        _playerSalary = AddInput(fields, "Salario", p.salary.ToString());
        SetDigitsOnly(_playerSalary);
        _playerContract = AddInput(fields, "Años", p.contract_years.ToString());
        SetDigitsOnly(_playerContract, 1);
        _playerGuaranteed = AddInput(fields, "Años garantizados", p.guaranteed_years.ToString());
        SetDigitsOnly(_playerGuaranteed, 1);
        _playerServiceYears = AddInput(fields, "Años de servicio", p.years_of_service.ToString());
        SetDigitsOnly(_playerServiceYears, 0);
        panel.Add(fields);

        _playerTeamOptionOn = p.has_team_option == 1;
        _playerPlayerOptionOn = p.has_player_option == 1;
        _playerTwoWayOn = p.is_two_way == 1;

        var options = new VisualElement();
        options.AddToClassList("editor-check-row");

        _playerTeamOptionBox = AddCheck(options, "Opción de equipo", _playerTeamOptionOn, on =>
        {
            _playerTeamOptionOn = on;
            if (on && _playerPlayerOptionOn)
            {
                _playerPlayerOptionOn = false;
                _playerPlayerOptionBox?.EnableInClassList("editor-check--on", false);
            }
        });
        _playerPlayerOptionBox = AddCheck(options, "Opción de jugador", _playerPlayerOptionOn, on =>
        {
            _playerPlayerOptionOn = on;
            if (on && _playerTeamOptionOn)
            {
                _playerTeamOptionOn = false;
                _playerTeamOptionBox?.EnableInClassList("editor-check--on", false);
            }
        });
        AddCheck(options, "Two-way", _playerTwoWayOn, on => _playerTwoWayOn = on);

        panel.Add(options);
        return panel;
    }

    VisualElement AddCheck(VisualElement parent, string label, bool initial, System.Action<bool> onToggle)
    {
        var row = new VisualElement();
        row.AddToClassList("editor-check");
        if (initial) row.AddToClassList("editor-check--on");

        var box = new Label("✓");
        box.AddToClassList("editor-check-box");
        row.Add(box);

        var lbl = new Label(label);
        lbl.AddToClassList("editor-check-label");
        row.Add(lbl);

        row.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            // Leer el estado real de la clase (puede haberlo cambiado otro checkbox,
            // p. ej. la exclusividad TO/PO).
            bool on = !row.ClassListContains("editor-check--on");
            row.EnableInClassList("editor-check--on", on);
            onToggle?.Invoke(on);
        });
        CursorManager.Instance?.RegisterHandCursor(row);
        parent.Add(row);
        return row;
    }

    void ChangePlayerPhoto(PlayerData p)
    {
        PickImage("SELECCIONA UNA FOTO PNG (256 × 256)", path =>
        {
            if (!PlayerPhotoHelper.ValidatePhotoFile(path, out var error))
            {
                ShowToast(error, isError: true);
                return;
            }
            _pendingPhotoPath = path;
            BuildPlayerDetail();
        });
    }

    // ── MODAL DE PAÍS ─────────────────────────────────────────────────
    void OpenCountryModal()
    {
        CloseCountryModal();

        _countryOverlay = new VisualElement();
        _countryOverlay.AddToClassList("editor-browser-overlay");

        var box = new VisualElement();
        box.AddToClassList("editor-country-box");
        _countryOverlay.Add(box);

        var title = new Label(LocalizationService.T("ui.code.editor_controller.selecciona_un_pais"));
        title.AddToClassList("editor-browser-title");
        box.Add(title);

        var scrollRow = new VisualElement();
        scrollRow.AddToClassList("editor-country-scroll-row");

        var viewport = new VisualElement();
        viewport.AddToClassList("editor-country-viewport");
        var content = new VisualElement();
        content.AddToClassList("editor-country-content");
        viewport.Add(content);
        scrollRow.Add(viewport);

        var track = new VisualElement();
        track.AddToClassList("editor-list-scrollbar");
        var thumb = new VisualElement();
        thumb.AddToClassList("editor-list-scrollbar-thumb");
        track.Add(thumb);
        scrollRow.Add(track);

        box.Add(scrollRow);

        var actions = new VisualElement();
        actions.AddToClassList("editor-browser-actions");
        var cancel = new Button();
        cancel.text = LocalizationService.T("ui.code.editor_controller.cancelar");
        cancel.AddToClassList("editor-country-cancel");
        cancel.clicked += () => { PlayClick(); CloseCountryModal(); };
        CursorManager.Instance?.RegisterHandCursor(cancel);
        actions.Add(cancel);
        box.Add(actions);

        // Lista en varias columnas, ordenada por nombre.
        var countries = CountryCodes.Names.OrderBy(kv => kv.Value).ToList();
        const int columns = 4;
        for (int i = 0; i < countries.Count; i += columns)
        {
            var row = new VisualElement();
            row.AddToClassList("editor-country-row");
            for (int c = 0; c < columns; c++)
            {
                int idx = i + c;
                if (idx < countries.Count)
                {
                    var kv = countries[idx];
                    string code = kv.Key;
                    var btn = new Button();
                    btn.text = $"{kv.Key}  {kv.Value}";
                    btn.AddToClassList("editor-country-btn");
                    btn.clicked += () =>
                    {
                        PlayClick();
                        _playerNat.value = code;
                        CloseCountryModal();
                    };
                    CursorManager.Instance?.RegisterHandCursor(btn);
                    row.Add(btn);
                }
                else
                {
                    var filler = new VisualElement();
                    filler.AddToClassList("editor-country-btn");
                    filler.AddToClassList("editor-country-btn--empty");
                    row.Add(filler);
                }
            }
            content.Add(row);
        }

        _root.Add(_countryOverlay);
        _countryOverlay.BringToFront();

        CustomScrollbar.Bind(viewport, content, track, thumb);
    }

    void CloseCountryModal()
    {
        if (_countryOverlay != null)
        {
            _countryOverlay.RemoveFromHierarchy();
            _countryOverlay = null;
        }
    }

    void SavePlayer()
    {
        if (_selectedPlayer == null) return;
        var p = _selectedPlayer;

        p.first_name = _playerFName.value;
        p.last_name = _playerLName.value;
        p.position = _posFromDisplay.TryGetValue(_playerPosDropdown.Value, out var pos) ? pos : _playerPosDropdown.Value;
        int.TryParse(_playerAge.value, out int age); p.age = age;
        p.nationality = _playerNat.value;
        int.TryParse(_playerHt.value, out int ht); p.height_cm = ht;
        int.TryParse(_playerWt.value, out int wt); p.weight_kg = wt;

        int.TryParse(_playerTeamDropdown.Value.Split(" - ")[0], out int tid);
        p.team_id = tid;

        p.is_rookie = _playerRookieDropdown.Value == "Sí" ? 1 : 0;

        int.TryParse(_playerRings.value, out int rings); p.rings = rings;
        int.TryParse(_playerFinalsMvps.value, out int fMvps); p.finals_mvps = fMvps;
        int.TryParse(_playerFinalsPlayed.value, out int fPlayed); p.finals_played = fPlayed;
        int.TryParse(_playerSeasonMvps.value, out int sMvps); p.season_mvps = sMvps;

        int.TryParse(_playerPot.value, out int pot); p.potential = pot;

        int.TryParse(_playerSpeed.value, out int spd); p.speed = PlayerAttributeScale.ClampValid(spd);
        int.TryParse(_playerShooting.value, out int sho); p.shooting = PlayerAttributeScale.ClampValid(sho);
        int.TryParse(_player3pt.value, out int thr); p.three_point = PlayerAttributeScale.ClampValid(thr);
        int.TryParse(_playerPassing.value, out int pas); p.passing = PlayerAttributeScale.ClampValid(pas);
        int.TryParse(_playerDribbling.value, out int dri); p.dribbling = PlayerAttributeScale.ClampValid(dri);
        int.TryParse(_playerDefense.value, out int def); p.defense = PlayerAttributeScale.ClampValid(def);
        int.TryParse(_playerRebounding.value, out int reb); p.rebounding = PlayerAttributeScale.ClampValid(reb);
        int.TryParse(_playerAthleticism.value, out int ath); p.athleticism = PlayerAttributeScale.ClampValid(ath);
        int.TryParse(_playerIq.value, out int iq); p.iq = PlayerAttributeScale.ClampValid(iq);
        int.TryParse(_playerSteals.value, out int stl); p.steals = PlayerAttributeScale.ClampValid(stl);
        int.TryParse(_playerBlocks.value, out int blk); p.blocks = PlayerAttributeScale.ClampValid(blk);
        int.TryParse(_playerMorale.value, out int mor); p.morale = Mathf.Clamp(mor, 0, 99);

        string[] roleChoices = { "Estrella", "Titular", "Banquillo", "Último recurso" };
        int roleIdx = System.Array.IndexOf(roleChoices, _playerRoleDropdown.Value);
        p.role = (PlayerRole)(roleIdx >= 0 ? roleIdx : 3);

        p.RecalculateOverall();

        // Contrato
        long.TryParse(_playerSalary.value, out long sal); p.salary = sal;
        int.TryParse(_playerContract.value, out int yrs); p.contract_years = yrs;
        int.TryParse(_playerGuaranteed.value, out int gyr);
        if (gyr < 0) gyr = 0;
        if (gyr > yrs) gyr = yrs;
        p.guaranteed_years = gyr;
        int.TryParse(_playerServiceYears.value, out int serviceYears);
        p.years_of_service = Mathf.Max(0, serviceYears);
        p.has_team_option = _playerTeamOptionOn ? 1 : 0;
        p.has_player_option = _playerPlayerOptionOn ? 1 : 0;
        p.is_two_way = _playerTwoWayOn ? 1 : 0;

        // Foto en borrador (se aplica junto con el resto).
        if (_pendingPhotoPath != null)
        {
            if (!PlayerPhotoHelper.SetPhoto(p.id, _pendingPhotoPath, out var photoError))
            {
                ShowToast(photoError, isError: true);
                return;
            }
            _pendingPhotoPath = null;
        }

        DatabaseManager.Instance.UpdatePlayer(p);
        ShowToast("Jugador guardado correctamente");
        BuildPlayerList();
    }

    // ══════════════════════════════════════
    // UI HELPERS
    // ══════════════════════════════════════

    TextField AddInput(VisualElement parent, string labelText, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("editor-field-row");

        var lbl = new Label(labelText);
        lbl.AddToClassList("editor-field-label");

        var field = new TextField { value = value };
        field.AddToClassList("editor-field-input");

        row.Add(lbl);
        row.Add(field);
        parent.Add(row);
        return field;
    }

    void SetLettersOnly(TextField field, int maxLength = 0)
    {
        field.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.character == '\0' || char.IsControl(evt.character)) return;
            if (!char.IsLetter(evt.character) && evt.character != ' ' && evt.character != '-')
            {
                evt.StopPropagation();
            }
        }, TrickleDown.TrickleDown);
        field.RegisterValueChangedCallback(evt =>
        {
            var filtered = new string(evt.newValue.Where(c => char.IsLetter(c) || c == ' ' || c == '-').ToArray());
            if (maxLength > 0 && filtered.Length > maxLength)
                filtered = filtered.Substring(0, maxLength);
            if (filtered != evt.newValue)
            {
                int cursor = field.cursorIndex;
                field.value = filtered.Length > 0 ? filtered : evt.previousValue;
                field.cursorIndex = cursor < field.text.Length ? cursor : field.text.Length;
            }
        });
    }

    void SetDigitsOnly(TextField field, int maxLength = 0)
    {
        field.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.character == '\0' || char.IsControl(evt.character)) return;
            if (!char.IsDigit(evt.character))
            {
                evt.StopPropagation();
            }
        }, TrickleDown.TrickleDown);
        field.RegisterValueChangedCallback(evt =>
        {
            var filtered = new string(evt.newValue.Where(char.IsDigit).ToArray());
            if (maxLength > 0 && filtered.Length > maxLength)
                filtered = filtered.Substring(0, maxLength);
            if (filtered != evt.newValue)
            {
                int cursor = field.cursorIndex;
                field.value = filtered.Length > 0 ? filtered : evt.previousValue;
                field.cursorIndex = cursor < field.text.Length ? cursor : field.text.Length;
            }
        });
    }

    void SetAbbreviationInput(TextField field)
    {
        field.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.character == '\0' || char.IsControl(evt.character)) return;
            if (!char.IsLetter(evt.character))
            {
                evt.StopPropagation();
            }
        }, TrickleDown.TrickleDown);
        field.RegisterValueChangedCallback(evt =>
        {
            var upper = evt.newValue.ToUpper();
            var filtered = new string(upper.Where(char.IsLetter).ToArray());
            if (filtered.Length > 3) filtered = filtered.Substring(0, 3);
            if (filtered != evt.newValue)
            {
                int cursor = field.cursorIndex;
                field.value = filtered.Length > 0 ? filtered : evt.previousValue;
                field.cursorIndex = cursor < field.text.Length ? cursor : field.text.Length;
            }
        });
    }

    CustomDropdown AddDropdown(VisualElement parent, string labelText, string[] choices, string selected)
    {
        var row = new VisualElement();
        row.AddToClassList("editor-field-row");

        var lbl = new Label(labelText);
        lbl.AddToClassList("editor-field-label");
        row.Add(lbl);

        var dd = BuildCustomDropdown(choices, selected);
        row.Add(dd.Root);
        parent.Add(row);
        return dd;
    }

    CustomDropdown BuildCustomDropdown(string[] choices, string selected)
    {
        int idx = System.Array.IndexOf(choices, selected);
        if (idx < 0) idx = 0;

        var root = new VisualElement();
        root.AddToClassList("custom-dropdown");

        var trigger = new Button();
        trigger.AddToClassList("custom-dropdown__trigger");

        var valueLabel = new Label(choices[idx]);
        valueLabel.AddToClassList("custom-dropdown__value");
        trigger.Add(valueLabel);

        var arrow = new Label("▾");
        arrow.AddToClassList("custom-dropdown__arrow");
        trigger.Add(arrow);

        var list = new VisualElement();
        list.AddToClassList("custom-dropdown__list");
        list.style.display = DisplayStyle.None;

        var dd = new CustomDropdown
        {
            Root = root,
            Trigger = trigger,
            List = list,
            ValueLabel = valueLabel,
            RawValue = choices[idx]
        };

        foreach (var c in choices)
            AddDropdownItem(dd, c);

        trigger.clicked += () =>
        {
            if (_openDropdown == dd)
                CloseAllDropdowns();
            else
                OpenDropdown(dd);
        };

        root.Add(trigger);
        root.Add(list);

        CursorManager.Instance?.RegisterHandCursor(trigger);

        return dd;
    }

    void AddDropdownItem(CustomDropdown dd, string text)
    {
        var item = new Button();
        item.AddToClassList("custom-dropdown__item");
        item.text = text;
        if (text == dd.RawValue)
            item.AddToClassList("custom-dropdown__item--selected");

        item.clicked += () =>
        {
            SetDropdownValue(dd, text);
            CloseAllDropdowns();
            foreach (var b in dd.List.Query<Button>(className: "custom-dropdown__item").ToList())
                b.RemoveFromClassList("custom-dropdown__item--selected");
            item.AddToClassList("custom-dropdown__item--selected");
            dd.Root.Focus();
        };

        CursorManager.Instance?.RegisterHandCursor(item);

        dd.List.Add(item);
    }

    void OpenDropdown(CustomDropdown dd)
    {
        CloseAllDropdowns();
        if (dd == null) return;

        _openDropdown = dd;
        var list = dd.List;

        list.RemoveFromHierarchy();
        _dropdownOverlay.Add(list);

        var triggerBounds = dd.Trigger.worldBound;
        list.style.position = Position.Absolute;

        // Temporarily show the list off-screen to let layout compute its size
        list.style.left = -9999;
        list.style.top = -9999;
        list.style.display = DisplayStyle.Flex;

        _root.schedule.Execute(() =>
        {
            float listHeight = list.resolvedStyle.height;
            float top = (triggerBounds.yMin + triggerBounds.yMax) / 2f - listHeight / 2f;
            if (top < 0) top = 0;
            if (top + listHeight > _root.worldBound.height)
                top = _root.worldBound.height - listHeight;
            float left = triggerBounds.xMax + 4;
            if (left + list.resolvedStyle.width > _root.worldBound.width)
                left = triggerBounds.xMin - list.resolvedStyle.width - 4;
            if (left < 0) left = 0;
            list.style.left = left;
            list.style.top = top;
        }).ExecuteLater(30);

        _root.RegisterCallbackOnce<PointerDownEvent>(OnPointerDownAnywhere);
    }

    void CloseAllDropdowns()
    {
        if (_openDropdown != null)
        {
            var list = _openDropdown.List;
            list.RemoveFromHierarchy();
            list.style.position = Position.Relative;
            list.style.left = StyleKeyword.Null;
            list.style.top = StyleKeyword.Null;
            list.style.width = StyleKeyword.Null;
            list.style.display = DisplayStyle.None;
            _openDropdown.Root.Add(list);
            _openDropdown = null;
        }
    }

    void OnPointerDownAnywhere(PointerDownEvent evt)
    {
        if (_openDropdown == null) return;

        var target = evt.target as VisualElement;
        if (target != null && IsChildOf(target, _openDropdown.List))
            return;
        if (target != null && target == _openDropdown.Trigger)
            return;
        if (target != null && IsChildOf(target, _openDropdown.Trigger))
            return;

        CloseAllDropdowns();
    }

    static bool IsChildOf(VisualElement child, VisualElement parent)
    {
        var current = child;
        while (current != null)
        {
            if (current == parent) return true;
            current = current.parent;
        }
        return false;
    }

    void AddSaveBtn(VisualElement parent, string text, System.Action action, string name = "")
    {
        if (!string.IsNullOrEmpty(name))
        {
            var old = parent.Q<Button>(name);
            old?.RemoveFromHierarchy();
        }
        var btn = new Button();
        btn.AddToClassList("editor-save-btn");
        btn.text = text;
        if (!string.IsNullOrEmpty(name)) btn.name = name;
        btn.RegisterCallback<ClickEvent>(_ => { PlayClick(); action(); });
        CursorManager.Instance?.RegisterHandCursor(btn);
        parent.Add(btn);
    }

    void ShowToast(string message, bool isError = false)
    {
        _toast.Clear();
        _toast.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("editor-modal-box");
        if (isError) box.AddToClassList("editor-modal-box--error");
        _toast.Add(box);

        var header = new VisualElement();
        header.AddToClassList("editor-modal-header");
        if (isError) header.AddToClassList("editor-modal-header--error");
        box.Add(header);

        var titleText = isError ? "ERROR" : "GUARDADO";
        var title = new Label(titleText);
        title.AddToClassList("editor-modal-title");
        if (isError) title.AddToClassList("editor-modal-title--error");
        header.Add(title);

        var body = new VisualElement();
        body.AddToClassList("editor-modal-body");
        box.Add(body);

        var text = new Label(message);
        text.AddToClassList("editor-modal-text");
        body.Add(text);

        var actions = new VisualElement();
        actions.AddToClassList("editor-modal-actions");
        box.Add(actions);

        var closeBtn = new Button();
        closeBtn.clicked += () => { PlayClick(); CloseToast(); };
        closeBtn.AddToClassList("editor-modal-btn");
        if (isError) closeBtn.AddToClassList("editor-modal-btn--danger");
        closeBtn.text = LocalizationService.T("ui.code.editor_controller.cerrar");
        CursorManager.Instance?.RegisterHandCursor(closeBtn);
        actions.Add(closeBtn);
    }

    void CloseToast()
    {
        _toast.style.display = DisplayStyle.None;
        _toast.Clear();
    }

    /// <summary>Modal de confirmación para acciones destructivas (UI-09).</summary>
    void ShowConfirm(string titleText, string message, System.Action onConfirm)
    {
        _toast.Clear();
        _toast.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("editor-modal-box");
        box.AddToClassList("editor-modal-box--error");
        _toast.Add(box);

        var header = new VisualElement();
        header.AddToClassList("editor-modal-header");
        header.AddToClassList("editor-modal-header--error");
        box.Add(header);

        var title = new Label(titleText);
        title.AddToClassList("editor-modal-title");
        title.AddToClassList("editor-modal-title--error");
        header.Add(title);

        var body = new VisualElement();
        body.AddToClassList("editor-modal-body");
        box.Add(body);

        var text = new Label(message);
        text.AddToClassList("editor-modal-text");
        body.Add(text);

        var actions = new VisualElement();
        actions.AddToClassList("editor-modal-actions");
        box.Add(actions);

        var cancelBtn = new Button { text = LocalizationService.T("ui.code.editor_controller.cancelar") };
        cancelBtn.clicked += () => { PlayClick(); CloseToast(); };
        cancelBtn.AddToClassList("editor-modal-btn");
        cancelBtn.AddToClassList("editor-modal-btn--secondary");
        CursorManager.Instance?.RegisterHandCursor(cancelBtn);
        actions.Add(cancelBtn);

        var confirmBtn = new Button { text = LocalizationService.T("ui.code.editor_controller.resetear") };
        confirmBtn.clicked += () => { PlayClick(); CloseToast(); onConfirm(); };
        confirmBtn.AddToClassList("editor-modal-btn");
        confirmBtn.AddToClassList("editor-modal-btn--danger");
        confirmBtn.style.marginLeft = 12;
        CursorManager.Instance?.RegisterHandCursor(confirmBtn);
        actions.Add(confirmBtn);
    }

    // ── EXPLORADOR DE ARCHIVOS (in-game, sin plugins) ─────────────────
    VisualElement _browserOverlay;

    void OpenFileBrowser(string title, System.Action<string> onSelected)
    {
        CloseFileBrowser();

        _browserOverlay = new VisualElement();
        _browserOverlay.AddToClassList("editor-browser-overlay");

        var box = new VisualElement();
        box.AddToClassList("editor-browser-box");
        _browserOverlay.Add(box);

        var titleLbl = new Label(title);
        titleLbl.AddToClassList("editor-browser-title");
        box.Add(titleLbl);

        var pathBar = new Label();
        pathBar.AddToClassList("editor-browser-path");
        box.Add(pathBar);

        var quick = new VisualElement();
        quick.AddToClassList("editor-browser-quick");
        box.Add(quick);

        var scroll = new ScrollView();
        scroll.AddToClassList("editor-browser-scroll");
        var list = new VisualElement();
        list.AddToClassList("editor-browser-list");
        scroll.Add(list);
        box.Add(scroll);

        var actions = new VisualElement();
        actions.AddToClassList("editor-browser-actions");
        var cancel = new Button();
        cancel.text = LocalizationService.T("ui.code.editor_controller.cancelar");
        cancel.AddToClassList("editor-browser-btn");
        cancel.AddToClassList("editor-browser-btn--cancel");
        cancel.clicked += () => { PlayClick(); CloseFileBrowser(); };
        CursorManager.Instance?.RegisterHandCursor(cancel);
        actions.Add(cancel);
        box.Add(actions);

        void Navigate(string dir)
        {
            try
            {
                if (!System.IO.Directory.Exists(dir)) return;
                pathBar.text = dir;
                list.Clear();

                var parent = System.IO.Directory.GetParent(dir);
                if (parent != null)
                    list.Add(BrowserEntry("..", true, () => Navigate(parent.FullName)));

                foreach (var d in System.IO.Directory.GetDirectories(dir).OrderBy(x => x))
                    list.Add(BrowserEntry(System.IO.Path.GetFileName(d), true, () => Navigate(d)));

                foreach (var f in System.IO.Directory.GetFiles(dir, "*.png").OrderBy(x => x))
                    list.Add(BrowserEntry(System.IO.Path.GetFileName(f), false, () =>
                    {
                        CloseFileBrowser();
                        onSelected?.Invoke(f);
                    }));
            }
            catch (System.Exception ex)
            {
                pathBar.text = LocalizationService.T("ui.code.editor_controller.no_se_puede_abrir_la_carpeta") + ex.Message;
            }
        }

        AddQuickButton(quick, "INICIO", System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), Navigate);
        AddQuickButton(quick, "ESCRITORIO", System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory), Navigate);
        AddQuickButton(quick, "IMÁGENES", System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures), Navigate);
        try
        {
            foreach (var drv in System.IO.DriveInfo.GetDrives())
                AddQuickButton(quick, drv.Name.TrimEnd('\\', '/'), drv.RootDirectory.FullName, Navigate);
        }
        catch { }

        Navigate(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile));

        _root.Add(_browserOverlay);
        _browserOverlay.BringToFront();
    }

    void AddQuickButton(VisualElement parent, string label, string dir, System.Action<string> navigate)
    {
        if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir)) return;
        var b = new Button();
        b.text = label;
        b.AddToClassList("editor-browser-quick-btn");
        b.clicked += () => { PlayClick(); navigate(dir); };
        CursorManager.Instance?.RegisterHandCursor(b);
        parent.Add(b);
    }

    VisualElement BrowserEntry(string name, bool isFolder, System.Action onClick)
    {
        var row = new VisualElement();
        row.AddToClassList("editor-browser-entry");
        if (isFolder) row.AddToClassList("editor-browser-entry--folder");

        var lbl = new Label(isFolder ? "▸ " + name : name);
        lbl.AddToClassList("editor-browser-entry-name");
        row.Add(lbl);

        row.RegisterCallback<ClickEvent>(_ => { PlayClick(); onClick(); });
        CursorManager.Instance?.RegisterHandCursor(row);
        return row;
    }

    void CloseFileBrowser()
    {
        if (_browserOverlay != null)
        {
            _browserOverlay.RemoveFromHierarchy();
            _browserOverlay = null;
        }
    }
}
