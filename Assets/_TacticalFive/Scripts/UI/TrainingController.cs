using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class TrainingController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Training;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Label _headerSeason;
    private Label _headerDate;

    // Left: player list
    private VisualElement _trainingBody;
    private CustomScrollbar.Handle _rosterScrollBar;

    // Right
    private VisualElement _trainingRightBody;
    private VisualElement _asistenteCard;
    private VisualElement _noAsistente;
    private VisualElement _attrPanel;
    private VisualElement _attrBody;
    private Label _trainingEmpty;
    private VisualElement _trainingPlayerInfo;
    private Label _trainingPlayerName;
    private Label _trainingPlayerMeta;

    // Modal + auto
    private VisualElement _noAsistenteOverlay;
    private Button _btnNoAsistenteOk;
    private Button _btnAutoTrain;
    private Button _btnResetTraining;
    private ScrollView _rightScroll;

    // Data
    private List<PlayerData> _players;
    private List<TrainingData> _activeTraining;
    private EmployeeData _asistente;
    private PlayerData _selectedPlayer;
    private int _currentGameDay;
    private Dictionary<string, Sprite> _logoSprites = new();
    private Texture2D _starTex;
    private StyleBackground _starBg;
    private StyleBackground _empleadoBg;
    private static readonly Dictionary<string, string> AttrDisplay = new()
    {
        { "shooting",    "TIRO" },
        { "three_point", "TRIPLE" },
        { "passing",     "PASE" },
        { "dribbling",   "BOTE" },
        { "defense",     "DEFENSA" },
        { "rebounding",  "REBOTE" },
        { "speed",       "VELOCIDAD" },
        { "athleticism", "ATLETISMO" },
        { "steals",      "ROBOS" },
        { "blocks",      "TAPONES" },
    };
    private static readonly Dictionary<string, string> BadgeAbbr = new()
    {
        { "shooting",    "T2" },
        { "three_point", "3P" },
        { "passing",     "PAS" },
        { "dribbling",   "BOT" },
        { "defense",     "DEF" },
        { "rebounding",  "REB" },
        { "speed",       "VEL" },
        { "athleticism", "ATL" },
        { "steals",      "ROB" },
        { "blocks",      "TAP" },
    };
    private static readonly Dictionary<string, Color32> AttrColors = new()
    {
        { "shooting",    new Color32(160, 45,  30,  255) },
        { "three_point", new Color32(25,  95,  155, 255) },
        { "passing",     new Color32(18,  110, 50,  255) },
        { "dribbling",   new Color32(95,  40,  125, 255) },
        { "defense",     new Color32(130, 28,  18,  255) },
        { "rebounding",  new Color32(150, 108, 6,   255) },
        { "speed",       new Color32(12,  120, 100, 255) },
        { "athleticism", new Color32(20,  135, 65,  255) },
        { "steals",      new Color32(160, 35,  20,  255) },
        { "blocks",      new Color32(0,   38,  77,  255) },
    };
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

        _trainingBody = _root.Q<VisualElement>("TrainingBody");
        _trainingRightBody = _root.Q<VisualElement>("TrainingRightBody");

        // Barra de scroll propia para la lista de plantilla (se re-enlaza en cada visita).
        var rosterViewport = _root.Q<VisualElement>("TrainingScrollViewport");
        var rosterContent = _root.Q<VisualElement>("TrainingScrollContent");
        var rosterTrack = _root.Q<VisualElement>("TrainingScrollTrack");
        var rosterThumb = _root.Q<VisualElement>("TrainingScrollThumb");
        _rosterScrollBar = CustomScrollbar.Bind(rosterViewport, rosterContent, rosterTrack, rosterThumb);

        _asistenteCard = _root.Q<VisualElement>("AsistenteCard");
        _noAsistente = _root.Q<VisualElement>("NoAsistente");
        _attrPanel = _root.Q<VisualElement>("TrainingAttrPanel");
        _attrBody = _root.Q<VisualElement>("TrainingAttrBody");
        _trainingEmpty = _root.Q<Label>("TrainingEmpty");

        _noAsistenteOverlay = _root.Q<VisualElement>("NoAsistenteOverlay");
        _btnNoAsistenteOk = _root.Q<Button>("BtnNoAsistenteOk");
        _btnAutoTrain = _root.Q<Button>("BtnAutoTrain");
        _btnResetTraining = _root.Q<Button>("BtnResetTraining");

        _rightScroll = _root.Q<ScrollView>("TrainingRightScroll");

        _trainingPlayerInfo = _root.Q<VisualElement>("TrainingPlayerInfo");
        _trainingPlayerName = _root.Q<Label>("TrainingPlayerName");
        _trainingPlayerMeta = _root.Q<Label>("TrainingPlayerMeta");
    }
    protected override void LoadData()
    {
        base.LoadData();

        var screenIcon = _root.Q<VisualElement>("ScreenIcon");
        if (screenIcon != null)
        {
            var iconTex = Resources.Load<Texture2D>("Icons/entrenamiento_icon");
            if (iconTex != null)
                screenIcon.style.backgroundImage = iconTex;
        }

        var logos = TeamImages.Logos(64);
        foreach (var s in logos) _logoSprites[s.name] = s;

        _starTex = Resources.Load<Texture2D>("Icons/star_24px");
        if (_starTex != null)
            _starBg = new StyleBackground(_starTex);
        _empleadoBg = new StyleBackground(Resources.Load<Texture2D>("Icons/avatar"));

        
        
        
        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        _activeTraining = DatabaseManager.Instance.GetTeamTraining(_myTeam.id);
        _currentGameDay = _season?.current_game_day ?? 0;

        var staff = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        _asistente = staff.FirstOrDefault(e => e.position == "ASISTENTE");
    }
    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _btnNoAsistenteOk?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _noAsistenteOverlay.RemoveFromClassList("training-modal-overlay--visible");
        });
        _btnAutoTrain?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnAutoAssign(); });
        _btnResetTraining?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnResetTraining(); });
    }
    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Training] RefreshHeader error: {ex.Message}"); }
        BuildPlayerList();
        BuildAsistenteCard();
        _selectedPlayer = null;
        ShowEmptyState();
        _root.Q<Button>("SubmenuEntrenamiento")?.AddToClassList("nav-submenu-item--active");
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

        long totalPayroll = _players.Sum(p => p.salary);
        _headerPayroll.text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - _players.Sum(p => p.salary);

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
            chemLabel.text = $"{chemistry.ToString()}%";
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

    void BuildPlayerList()
    {
        _trainingBody.Clear();

        var sorted = _players
            .OrderBy(p => System.Array.IndexOf(PositionCodes.Order, p.position))
            .ThenByDescending(p => p.overall)
            .ToList();

        var grid = new VisualElement();
        grid.AddToClassList("training-cards-grid");

        foreach (var player in sorted)
        {
            var card = CreatePlayerCard(player);
            // Re-aplicar la selección: el árbol se recrea y antes se perdía, lo que
            // hacía fallar el .First() que buscaba la tarjeta seleccionada.
            if (_selectedPlayer != null && player.id == _selectedPlayer.id)
                card.AddToClassList("training-card--selected");
            grid.Add(card);
        }

        _trainingBody.Add(grid);

        // Recalcula la barra con la lista reconstruida.
        CustomScrollbar.Reset(_rosterScrollBar);
    }

    VisualElement CreatePlayerCard(PlayerData player)
    {
        var card = new VisualElement();
        card.AddToClassList("training-card");

        // Top row: photo (left) + info (right)
        var topRow = new VisualElement();
        topRow.AddToClassList("training-card-top-row");

        // Photo
        var photo = new VisualElement();
        photo.AddToClassList("training-card-photo");
        var tex = PlayerPhotoHelper.Load(player.id, player.photo);
        if (tex != null)
            photo.style.backgroundImage = new StyleBackground(tex);
        topRow.Add(photo);

        // Info (name + badges)
        var info = new VisualElement();
        info.AddToClassList("training-card-info");

        // Name
        var nameContainer = new VisualElement();
        nameContainer.AddToClassList("training-card-name");

        var firstNameLbl = new Label();
        firstNameLbl.AddToClassList("training-card-firstname");
        firstNameLbl.text = player.first_name?.ToUpper() ?? "";
        nameContainer.Add(firstNameLbl);

        var lastNameLbl = new Label();
        lastNameLbl.AddToClassList("training-card-lastname");
        lastNameLbl.text = player.last_name?.ToUpper() ?? "";
        nameContainer.Add(lastNameLbl);

        info.Add(nameContainer);

        // Badges row: position + MED
        var badgesRow = new VisualElement();
        badgesRow.AddToClassList("training-card-bottom");

        var posBadge = new VisualElement();
        posBadge.AddToClassList("training-card-pos-badge");
        var posBadgeLbl = new Label();
        posBadgeLbl.AddToClassList("training-card-pos-badge-label");
        string posText = PositionCodes.GetShort(player.position);
        if (!string.IsNullOrEmpty(player.secondary_position))
            posText += $" / {PositionCodes.GetShort(player.secondary_position)}";
        posBadgeLbl.text = posText;
        posBadge.Add(posBadgeLbl);
        badgesRow.Add(posBadge);

        int ovrVal = player.overall;
        var medBadge = new VisualElement();
        medBadge.AddToClassList("training-card-med-badge");
        if (ovrVal > 84) medBadge.AddToClassList("training-card-med-badge--high");
        else if (ovrVal >= 70) medBadge.AddToClassList("training-card-med-badge--mid");
        else medBadge.AddToClassList("training-card-med-badge--low");

        var medLbl = new Label();
        medLbl.AddToClassList("training-card-med-badge-label");
        if (ovrVal > 84) medLbl.AddToClassList("training-card-med-badge-label--high");
        else if (ovrVal >= 70) medLbl.AddToClassList("training-card-med-badge-label--mid");
        else medLbl.AddToClassList("training-card-med-badge-label--low");
        medLbl.text = $"MED {ovrVal}";
        medBadge.Add(medLbl);
        badgesRow.Add(medBadge);

        info.Add(badgesRow);
        topRow.Add(info);
        card.Add(topRow);

        // Bottom: training badge (full width)
        var badgeContainer = new VisualElement();
        badgeContainer.AddToClassList("training-card-badge");

        var active = _activeTraining.FirstOrDefault(t => t.player_id == player.id);
        if (active != null)
        {
            if (AttrColors.TryGetValue(active.attribute, out var badgeColor))
                badgeContainer.style.backgroundColor = new StyleColor(badgeColor);

            var badgeLbl = new Label();
            badgeLbl.AddToClassList("training-card-badge-label");
            badgeLbl.text = AttrDisplay.TryGetValue(active.attribute, out var attrName) ? attrName : active.attribute.ToUpper();
            badgeContainer.Add(badgeLbl);
        }
        else
        {
            badgeContainer.AddToClassList("training-card-badge--none");
            var badgeLbl = new Label();
            badgeLbl.AddToClassList("training-card-badge-label");
            badgeLbl.text = "SIN ENTRENAMIENTO";
            badgeContainer.Add(badgeLbl);
        }

        card.Add(badgeContainer);

        // Click handler
        card.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnPlayerSelected(player, card); });
        if (CursorManager.Instance != null)
        {
            card.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
            card.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
        }

        return card;
    }

    void BuildAsistenteCard()
    {
        _asistenteCard.Clear();
        _noAsistente.Clear();
        _noAsistente.style.display = DisplayStyle.None;
        _asistenteCard.style.display = DisplayStyle.Flex;

        if (_asistente != null)
        {
            var icon = new VisualElement();
            icon.AddToClassList("training-staff-icon");
            icon.style.backgroundImage = _empleadoBg;
            _asistenteCard.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("training-staff-info");

            var nameLbl = new Label();
            nameLbl.AddToClassList("training-staff-name");
            nameLbl.text = $"{_asistente.first_name} {_asistente.last_name}".ToUpper();
            info.Add(nameLbl);

            var starRow = new VisualElement();
            starRow.AddToClassList("training-staff-stars");
            for (int i = 0; i < 5; i++)
            {
                var star = new VisualElement();
                star.AddToClassList("training-staff-star");
                if (i >= _asistente.reputation)
                    star.AddToClassList("training-staff-star--empty");
                if (_starTex != null)
                    star.style.backgroundImage = _starBg;
                starRow.Add(star);
            }
            info.Add(starRow);

            int durationDays = GetTrainingDuration(_asistente.reputation);
            var interestLbl = new Label();
            interestLbl.AddToClassList("training-staff-interest");
            interestLbl.text = $"Duración del entrenamiento: {durationDays} días";
            info.Add(interestLbl);

            _asistenteCard.Add(info);
        }
        else
        {
            _asistenteCard.style.display = DisplayStyle.None;
            _noAsistente.style.display = DisplayStyle.Flex;

            var lbl = new Label();
            lbl.AddToClassList("training-no-staff-label");
            lbl.text = "No hay Asistente contratado.\nVe a Empleados para contratar uno.";
            _noAsistente.Add(lbl);

            var hireBtn = new Button();
            hireBtn.AddToClassList("training-btn-hire");
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
            _noAsistente.Add(hireBtn);
        }
    }

    void OnPlayerSelected(PlayerData player, VisualElement card)
    {
        _selectedPlayer = player;

        _trainingBody.Query<VisualElement>(className: "training-card--selected")
            .ForEach(e => e.RemoveFromClassList("training-card--selected"));
        card.AddToClassList("training-card--selected");

        if (_asistente != null)
            ShowAttrTable(player);
        else
            ShowEmptyState();

        ShowRightPanel();
    }

    void ShowAttrTable(PlayerData player)
    {
        _trainingEmpty.style.display = DisplayStyle.None;
        _attrPanel.style.display = DisplayStyle.Flex;
        _attrBody.Clear();

        _trainingPlayerInfo.AddToClassList("training-player-info--visible");
        _trainingPlayerName.text = $"{player.first_name} {player.last_name}".ToUpper();
        _trainingPlayerMeta.text = $"{PositionCodes.GetName(player.position)} · {player.age} años · {CountryCodes.GetName(player.nationality)} · {player.height_cm / 100f:F2}m · {player.weight_kg}kg{(player.is_rookie == 1 ? " · Rookie" : "")}";

        var activeTraining = _activeTraining.FirstOrDefault(t => t.player_id == player.id);

        var attrs = new (string field, string label)[]
        {
            ("shooting",    "TIRO"),
            ("three_point", "TRIPLE"),
            ("passing",     "PASE"),
            ("dribbling",   "BOTE"),
            ("defense",     "DEFENSA"),
            ("rebounding",  "REBOTE"),
            ("speed",       "VELOCIDAD"),
            ("athleticism", "ATLETISMO"),
            ("steals",      "ROBOS"),
            ("blocks",      "TAPONES"),
        };

        foreach (var (field, label) in attrs)
        {
            var row = new VisualElement();
            row.AddToClassList("training-attr-row");

            var val = (int)typeof(PlayerData).GetProperty(field).GetValue(player);
            bool atMax = val >= 99;
            bool isActiveAttr = activeTraining != null && activeTraining.attribute == field;
            bool disabled = atMax;

            var btn = new Button();
            btn.AddToClassList("training-attr-btn");
            if (AttrColors.TryGetValue(field, out var btnColor) && !isActiveAttr && !disabled)
                btn.style.backgroundColor = new StyleColor(btnColor);
            if (isActiveAttr || disabled)
                btn.AddToClassList("training-attr-btn--disabled");

            if (isActiveAttr)
                btn.text = "ENTRENANDO";
            else
                btn.text = "ENTRENAR";

            if (!isActiveAttr && !disabled)
            {
                btn.userData = field;
                btn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnAssignTraining(player, field); });
                if (CursorManager.Instance != null)
                {
                    btn.RegisterCallback<MouseEnterEvent>(_ =>
                        CursorManager.Instance.SetHandCursor());
                    btn.RegisterCallback<MouseLeaveEvent>(_ =>
                        CursorManager.Instance.SetDefaultCursor());
                }
            }

            var nameLbl = new Label();
            nameLbl.AddToClassList("training-attr-name");
            nameLbl.text = label;

            var valLbl = new Label();
            valLbl.AddToClassList("training-attr-value");
            if (val > 84)
                valLbl.AddToClassList("training-attr-value--high");
            else if (val >= 70)
                valLbl.AddToClassList("training-attr-value--mid");
            else
                valLbl.AddToClassList("training-attr-value--low");
            valLbl.text = val.ToString();

            row.Add(btn);
            row.Add(nameLbl);
            row.Add(valLbl);
            _attrBody.Add(row);
        }
    }

    void ShowEmptyState()
    {
        _trainingEmpty.style.display = DisplayStyle.Flex;
        _rightScroll.style.display = DisplayStyle.None;
        _attrPanel.style.display = DisplayStyle.None;
        _trainingPlayerInfo.RemoveFromClassList("training-player-info--visible");
    }

    void ShowRightPanel()
    {
        _trainingEmpty.style.display = DisplayStyle.None;
        _rightScroll.style.display = DisplayStyle.Flex;
    }

    void OnAssignTraining(PlayerData player, string attribute)
    {
        if (_asistente == null)
        {
            _noAsistenteOverlay.AddToClassList("training-modal-overlay--visible");
            return;
        }

        var existing = _activeTraining.FirstOrDefault(t => t.player_id == player.id);
        int duration = GetTrainingDuration(_asistente.reputation);

        if (existing != null)
        {
            existing.attribute = attribute;
            existing.start_day = _currentGameDay;
            existing.duration = duration;
            DatabaseManager.Instance.Db.Update(existing);
            Debug.Log($"[Training] Entrenamiento cambiado: {player.first_name} {player.last_name} → {attribute} ({duration} días)");
        }
        else
        {
            var training = new TrainingData
            {
                player_id = player.id,
                team_id = _myTeam.id,
                attribute = attribute,
                start_day = _currentGameDay,
                duration = duration,
                completed = 0,
            };
            DatabaseManager.Instance.InsertTraining(training);
            Debug.Log($"[Training] Entrenamiento asignado: {player.first_name} {player.last_name} → {attribute} ({duration} días)");
        }

        _activeTraining = DatabaseManager.Instance.GetTeamTraining(_myTeam.id);
        BuildPlayerList();
        var selCard = _trainingBody.Query<VisualElement>(className: "training-card--selected").First();
        if (selCard != null)
            OnPlayerSelected(player, selCard);
    }
    private static readonly Dictionary<string, string[]> PrimaryAttrs = new()
    {
        { "PG", new[] { "passing", "dribbling", "three_point", "speed", "shooting" } },
        { "SG", new[] { "shooting", "three_point", "speed", "dribbling" } },
        { "SF", new[] { "shooting", "three_point", "defense", "speed", "athleticism" } },
        { "PF", new[] { "defense", "rebounding", "shooting", "blocks", "athleticism" } },
        { "C",  new[] { "rebounding", "blocks", "defense", "shooting", "athleticism" } },
    };
    private static readonly Dictionary<string, string[]> SecondaryAttrs = new()
    {
        { "PG", new[] { "defense", "steals", "athleticism" } },
        { "SG", new[] { "passing", "defense", "steals", "athleticism" } },
        { "SF", new[] { "passing", "dribbling", "rebounding", "steals" } },
        { "PF", new[] { "three_point", "passing", "speed" } },
        { "C",  new[] { "passing", "speed" } },
    };
    private static readonly Dictionary<string, string[]> ExcludedAttrs = new()
    {
        { "PG", new[] { "rebounding", "blocks" } },
        { "SG", new[] { "rebounding", "blocks" } },
        { "SF", new[] { "blocks" } },
        { "PF", new[] { "dribbling", "steals" } },
        { "C",  new[] { "three_point", "dribbling", "steals" } },
    };

    void OnResetTraining()
    {
        DatabaseManager.Instance.DeleteTeamTraining(_myTeam.id);
        _activeTraining = DatabaseManager.Instance.GetTeamTraining(_myTeam.id);
        _selectedPlayer = null;
        ShowEmptyState();
        BuildPlayerList();
    }

    void OnAutoAssign()
    {
        if (_asistente == null)
        {
            _noAsistenteOverlay.AddToClassList("training-modal-overlay--visible");
            return;
        }

        int duration = GetTrainingDuration(_asistente.reputation);
        int count = 0;

        foreach (var player in _players)
        {
            string pos = player.position;
            var allTrainable = new[] { "shooting", "three_point", "passing", "dribbling", "defense",
                                       "rebounding", "speed", "athleticism", "steals", "blocks" };

            // Try primary attributes first
            string attr = PickLowest(player, pos, PrimaryAttrs, null, 0);
            // If all primaries ≥ 80, try secondary
            if (attr == null)
                attr = PickLowest(player, pos, SecondaryAttrs, null, 80);
            // If secondaries also ≥ 80, try everything except excluded
            if (attr == null)
                attr = PickLowest(player, null, null, ExcludedAttrs.TryGetValue(pos, out var exc) ? exc : null, 99);

            if (attr == null) continue;

            var existing = _activeTraining.FirstOrDefault(t => t.player_id == player.id);
            if (existing != null)
            {
                existing.attribute = attr;
                existing.start_day = _currentGameDay;
                existing.duration = duration;
                DatabaseManager.Instance.Db.Update(existing);
            }
            else
            {
                var training = new TrainingData
                {
                    player_id = player.id,
                    team_id = _myTeam.id,
                    attribute = attr,
                    start_day = _currentGameDay,
                    duration = duration,
                    completed = 0,
                };
                DatabaseManager.Instance.InsertTraining(training);
            }
            count++;
        }

        Debug.Log($"[Training] Auto-asignados {count} entrenamientos");

        _activeTraining = DatabaseManager.Instance.GetTeamTraining(_myTeam.id);
        BuildPlayerList();

        if (_selectedPlayer != null)
        {
            var card = _trainingBody.Query<VisualElement>(className: "training-card--selected").First();
            if (card != null)
                OnPlayerSelected(_selectedPlayer, card);
        }
    }

    string PickLowest(PlayerData player, string pos, Dictionary<string, string[]> groupMap, string[] excluded, int fallbackThreshold)
    {
        string[] pool;
        if (groupMap != null && pos != null && groupMap.TryGetValue(pos, out var p))
            pool = p;
        else if (excluded != null)
            pool = new[] { "shooting", "three_point", "passing", "dribbling", "defense",
                           "rebounding", "speed", "athleticism", "steals", "blocks" }
                   .Where(a => !excluded.Contains(a)).ToArray();
        else
            return null;

        string weakest = null;
        int minVal = 100;
        bool allAboveThreshold = true;
        foreach (var attr in pool)
        {
            int val = (int)typeof(PlayerData).GetProperty(attr).GetValue(player);
            if (val < fallbackThreshold)
                allAboveThreshold = false;
            if (val < minVal)
            {
                minVal = val;
                weakest = attr;
            }
        }
        // If all attributes in this pool are ≥ fallbackThreshold, return null to try next pool
        if (allAboveThreshold)
            return null;
        return weakest;
    }

    int GetTrainingDuration(int reputation)
    {
        return reputation switch
        {
            5 => 14,
            4 => 18,
            3 => 22,
            2 => 26,
            _ => 30,
        };
    }
}
