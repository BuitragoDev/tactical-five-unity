using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class RosterController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Roster;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Label _headerSeason;
    private Label _headerDate;

    // Summary
    private Label _summaryPlayers;
    private Label _summaryOverall;
    private Label _summaryBudget;

    // Roster list
    private VisualElement _rosterBody;

    // Detail
    private VisualElement _detailEmpty;
    private ScrollView _detailScroll;
    private VisualElement _detailContent;
    private VisualElement _detailPhoto;
    private Label _detailPlayerName;
    private Label _detailPlayerPos;
    private Label _detailPlayerMeta;
    private Button _detailLinkTrajectory;
        private Button _detailLinkProfile;
    private Label _detailOvr;
    private Label _detailHealth;
    private VisualElement _detailAttrs;
    private VisualElement _detailSeasonStats;
    private Label _statPts;
    private Label _statReb;
    private Label _statAst;
    private Label _statStl;
    private Label _statBlk;
    private Label _detailContract;
    private Label _detailPotential;
    private VisualElement _detailGLeague;
    private Label _gleagueStatsText;
    private Button _btnBuyout;

    // Modal buyout
    private VisualElement _buyoutOverlay;
    private VisualElement _buyoutBox;
    private Label _buyoutText1;
    private Label _buyoutText2;
    private Label _buyoutInfo;
    private Button _btnBuyoutStretch;
    private Button _btnBuyoutCancel;

    // Renovar contrato
    private Button _btnRenew;

    // Modal renovación (oferta)
    private VisualElement _renewOverlay;
    private VisualElement _renewBox;
    private VisualElement _renewIcon;
    private Label _renewTitle;
    private Label _renewText1;
    private Label _renewText2;
    private Button _btnRenewCancel;
    private Button _btnRenewConfirm;
    private Label _renewSalaryValue;
    private Label _renewSalaryDec;
    private Label _renewSalaryInc;
    private Label _renewYearsValue;
    private Label _renewYearsDec;
    private Label _renewYearsInc;
    private Label _renewPendingText;
    private VisualElement _renewFormRowSalary;
    private VisualElement _renewFormRowYears;
    private VisualElement _renewFormRowOptions;
    private VisualElement _renewFormRowTwoWay;
    private Label _renewWarningText;
    private Label _renewMaxInfo;
    private long _renewMaxSalary;
    private long _renewSalary;
    private int _renewYears;
    private bool _offerSent;

    // Modal renovación bloqueada (≥3 años)
    private VisualElement _renewBlockOverlay;
    private VisualElement _renewBlockBox;
    private VisualElement _renewBlockIcon;
    private Label _renewBlockTitle;
    private Label _renewBlockText;
    private Button _btnRenewBlockOk;

    // Modal cooldown renovación
    private VisualElement _renewCooldownOverlay;
    private VisualElement _renewCooldownBox;
    private VisualElement _renewCooldownIcon;
    private Label _renewCooldownTitle;
    private Label _renewCooldownText;
    private Button _btnRenewCooldownOk;

    // Modal resultado renovación
    private VisualElement _renewResultOverlay;
    private VisualElement _renewResultBox;
    private VisualElement _renewResultIcon;
    private Label _renewResultTitle;
    private Label _renewResultText1;
    private Label _renewResultText2;

    // Datos
    private int _renewOfferYears;
    private long _renewOfferSalary;
    private Button _renewTeamOption;
    private Button _renewPlayerOption;
    private Button _renewTwoWayToggle;
    private bool _teamOptionActive;
    private bool _playerOptionActive;
    private bool _renewTwoWayActive;
    private List<PlayerData> _players;
    private PlayerData _selectedPlayer;
    private Dictionary<string, Sprite> _logoSprites = new();

    static string GetPositionDisplay(string pos, string secondary)
    {
        var main = PositionCodes.GetName(pos);
        if (string.IsNullOrEmpty(secondary)) return main;
        var sec = PositionCodes.GetName(secondary);
        return $"{main} / {sec}";
    }
    static string FormatMoney(long amount)
    {
        return System.Math.Abs(amount).ToString("N0").Replace(',', '.') + " $";
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        SetupScrollViews();
    }
    protected override void CacheReferences()
    {
        // Header
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");

        // Summary
        _summaryPlayers = _root.Q<Label>("SummaryPlayers");
        _summaryOverall = _root.Q<Label>("SummaryOverall");
        _summaryBudget = _root.Q<Label>("SummaryBudget");

        // Roster
        _rosterBody = _root.Q<VisualElement>("RosterBody");

        // Detail
        _detailEmpty = _root.Q<VisualElement>("DetailEmpty");
        _detailScroll = _root.Q<ScrollView>("DetailScroll");
        _detailContent = _root.Q<VisualElement>("DetailContent");
        _detailPhoto = _root.Q<VisualElement>("DetailPhoto");
        _detailPlayerName = _root.Q<Label>("DetailPlayerName");
        _detailPlayerPos = _root.Q<Label>("DetailPlayerPos");
        _detailPlayerMeta = _root.Q<Label>("DetailPlayerMeta");
        _detailLinkTrajectory = _root.Q<Button>("DetailLinkTrajectory");
        _detailLinkProfile = _root.Q<Button>("DetailLinkProfile");
        _detailOvr = _root.Q<Label>("DetailOvr");
        _detailHealth = _root.Q<Label>("DetailHealth");
        _detailAttrs = _root.Q<VisualElement>("DetailAttrs");
        _detailSeasonStats = _root.Q<VisualElement>("DetailSeasonStats");
        _statPts = _root.Q<Label>("StatPts");
        _statReb = _root.Q<Label>("StatReb");
        _statAst = _root.Q<Label>("StatAst");
        _statStl = _root.Q<Label>("StatStl");
        _statBlk = _root.Q<Label>("StatBlk");
        _detailContract = _root.Q<Label>("DetailContract");
        _detailContract.enableRichText = true;
        _detailPotential = _root.Q<Label>("DetailPotential");
        _detailGLeague = _root.Q<VisualElement>("GLeagueStats");
        _gleagueStatsText = _root.Q<Label>("GLeagueStatsText");
        _btnBuyout = _root.Q<Button>("BtnBuyout");

        // Modal buyout
        _buyoutOverlay = _root.Q<VisualElement>("BuyoutOverlay");
        _buyoutBox = _root.Q<VisualElement>("BuyoutBox");
        _buyoutText1 = _root.Q<Label>("BuyoutText1");
        _buyoutText2 = _root.Q<Label>("BuyoutText2");
        _buyoutInfo = _root.Q<Label>("BuyoutInfo");
        _btnBuyoutStretch = _root.Q<Button>("BtnBuyoutStretch");
        _btnBuyoutCancel = _root.Q<Button>("BtnBuyoutCancel");

        // Renovar contrato
        _btnRenew = _root.Q<Button>("BtnRenew");

        // Modal renovación
        _renewOverlay = _root.Q<VisualElement>("RenewOverlay");
        _renewBox = _root.Q<VisualElement>("RenewBox");
        _renewIcon = _root.Q<VisualElement>("RenewIcon");
        _renewTitle = _root.Q<Label>("RenewTitle");
        _renewText1 = _root.Q<Label>("RenewText1");
        _renewText2 = _root.Q<Label>("RenewText2");
        _btnRenewCancel = _root.Q<Button>("BtnRenewCancel");
        _btnRenewConfirm = _root.Q<Button>("BtnRenewConfirm");
        _renewSalaryValue = _root.Q<Label>("RenewSalaryValue");
        _renewSalaryDec = _root.Q<Label>("RenewSalaryDec");
        _renewSalaryInc = _root.Q<Label>("RenewSalaryInc");
        _renewYearsValue = _root.Q<Label>("RenewYearsValue");
        _renewYearsDec = _root.Q<Label>("RenewYearsDec");
        _renewYearsInc = _root.Q<Label>("RenewYearsInc");
        _renewTeamOption = _root.Q<Button>("RenewTeamOption");
        _renewPlayerOption = _root.Q<Button>("RenewPlayerOption");
        _renewTwoWayToggle = _root.Q<Button>("RenewTwoWayToggle");
        if (_renewPlayerOption != null) _renewPlayerOption.style.marginLeft = 12;
        _renewPendingText = _root.Q<Label>("RenewPendingText");
        _renewFormRowSalary = _root.Q<VisualElement>("RenewFormRowSalary");
        _renewFormRowYears = _root.Q<VisualElement>("RenewFormRowYears");
        _renewFormRowOptions = _root.Q<VisualElement>("RenewFormRowOptions");
        _renewFormRowTwoWay = _root.Q<VisualElement>("RenewFormRowTwoWay");
        _renewWarningText = _root.Q<Label>("RenewWarningText");
        _renewMaxInfo = _root.Q<Label>("RenewMaxInfo");

        // Modal renovación bloqueada
        _renewBlockOverlay = _root.Q<VisualElement>("RenewBlockOverlay");
        _renewBlockBox = _root.Q<VisualElement>("RenewBlockBox");
        _renewBlockIcon = _root.Q<VisualElement>("RenewBlockIcon");
        _renewBlockTitle = _root.Q<Label>("RenewBlockTitle");
        _renewBlockText = _root.Q<Label>("RenewBlockText");
        _btnRenewBlockOk = _root.Q<Button>("BtnRenewBlockOk");

        // Modal cooldown renovación
        _renewCooldownOverlay = _root.Q<VisualElement>("RenewCooldownOverlay");
        _renewCooldownBox = _root.Q<VisualElement>("RenewCooldownBox");
        _renewCooldownIcon = _root.Q<VisualElement>("RenewCooldownIcon");
        _renewCooldownTitle = _root.Q<Label>("RenewCooldownTitle");
        _renewCooldownText = _root.Q<Label>("RenewCooldownText");
        _btnRenewCooldownOk = _root.Q<Button>("BtnRenewCooldownOk");

        // Modal resultado renovación
        _renewResultOverlay = _root.Q<VisualElement>("RenewResultOverlay");
        _renewResultBox = _root.Q<VisualElement>("RenewResultBox");
        _renewResultIcon = _root.Q<VisualElement>("RenewResultIcon");
        _renewResultTitle = _root.Q<Label>("RenewResultTitle");
        _renewResultText1 = _root.Q<Label>("RenewResultText1");
        _renewResultText2 = _root.Q<Label>("RenewResultText2");
    }

    void SetupScrollViews()
    {
        var rosterScroll = _root.Q<ScrollView>("RosterScroll");
        if (rosterScroll != null)
            rosterScroll.contentContainer.style.flexDirection = FlexDirection.Column;

        if (_detailScroll != null)
            _detailScroll.contentContainer.style.flexDirection = FlexDirection.Column;
    }
    protected override void LoadData()
    {
        base.LoadData();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos) _logoSprites[s.name] = s;

        // Icono contrato en modal de renovación
        if (_renewIcon != null)
        {
            var contractTex = Resources.Load<Texture2D>("Icons/contrato");
            if (contractTex != null)
                _renewIcon.style.backgroundImage = new StyleBackground(contractTex);
        }

        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
    }
    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _btnBuyout?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenBuyoutModal(); });
        _btnBuyoutStretch?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ConfirmBuyout(); });
        _btnBuyoutCancel?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseBuyoutModal(); });
        _buyoutOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _buyoutOverlay)
            { PlayClick(); CloseBuyoutModal(); }
        });
        _btnRenew?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnRenewClicked(); });
        _btnRenewCancel?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseRenewModal(); });
        _btnRenewConfirm?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SendOffer(); });
        _btnRenewBlockOk?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseRenewBlockModal(); });
        _btnRenewCooldownOk?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseRenewCooldownModal(); });
        SetupRenewLongPress(_renewSalaryDec, () => StepRenewSalary(-1));
        SetupRenewLongPress(_renewSalaryInc, () => StepRenewSalary(1));
        SetupRenewLongPress(_renewYearsDec, () => StepRenewYears(-1));
        SetupRenewLongPress(_renewYearsInc, () => StepRenewYears(1));
        _renewTeamOption?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ToggleRenewOption("team"); });
        _renewPlayerOption?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ToggleRenewOption("player"); });
        _renewTwoWayToggle?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ToggleRenewTwoWay(); });
        _detailLinkTrajectory?.RegisterCallback<ClickEvent>(_ =>
        {
            if (_selectedPlayer == null) return;
            PlayClick();
            ScreenManager.SelectedPlayerId = _selectedPlayer.id;
            ScreenManager.Instance.GoTo(GameScreen.Trajectory);
        });
        if (CursorManager.Instance != null && _detailLinkTrajectory != null)
            CursorManager.Instance.RegisterHandCursor(_detailLinkTrajectory);

        _detailLinkProfile?.RegisterCallback<ClickEvent>(_ =>
        {
            if (_selectedPlayer == null) return;
            PlayClick();
            ScreenManager.SelectedPlayerId = _selectedPlayer.id;
            ScreenManager.Instance.GoTo(GameScreen.PlayerProfile);
        });
        if (CursorManager.Instance != null && _detailLinkProfile != null)
            CursorManager.Instance.RegisterHandCursor(_detailLinkProfile);
    }
    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Roster] RefreshHeader error: {ex.Message}"); }
        RefreshSummary();
        BuildRosterList();
        _root.Q<Button>("SubmenuJugadores")?.AddToClassList("nav-submenu-item--active");

        _detailEmpty.style.display = DisplayStyle.Flex;
        _detailScroll.style.display = DisplayStyle.None;
        CloseRenewModal();
        CloseRenewBlockModal();
        CloseRenewCooldownModal();
        CloseRenewResultModal();
    }

    // ── HEADER ───────────────────────────────────────────

    protected override void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        var logo = _root.Q<VisualElement>("HeaderTeamLogo");
        var teamName = _root.Q<Label>("HeaderTeamName");
        var managerName = _root.Q<Label>("HeaderManagerName");
        var budget = _root.Q<Label>("HeaderBudget");
        var payroll = _root.Q<Label>("HeaderPayroll");
        var margin = _root.Q<Label>("HeaderMargin");
        var season = _root.Q<Label>("HeaderSeason");
        var date = _root.Q<Label>("HeaderDate");
        if (teamName == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            logo.style.backgroundImage = new StyleBackground(sprite);

        teamName.text = _myTeam.name.ToUpper();
        managerName.text = $"Manager: {_manager.name}";
        budget.text = $"${_myTeam.budget / 1_000_000}M";
        budget.style.color = _myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));

        long totalPayroll = _players.Sum(p => p.salary);
        payroll.text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long marginVal = salaryCap - _players.Sum(p => p.salary);

        string marginText = marginVal >= 0
            ? $"+${marginVal / 1_000_000}M"
            : $"-${Mathf.Abs((int)(marginVal / 1_000_000))}M";
        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        margin.text = marginText;
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

        margin.RemoveFromClassList("header-stat-value--negative");
        if (marginVal < 0) margin.AddToClassList("header-stat-value--negative");

        if (_season != null)
        {
            season.text = $"Temporada {_season.year_start}-{_season.year_end}";
            date.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "MENÚ PRINCIPAL";
    }

    // ── SUMMARY ──────────────────────────────────────────

    void RefreshSummary()
    {
        int rosterCount = DatabaseManager.Instance.GetRosterCount(_myTeam.id);
        _summaryPlayers.text = $"{rosterCount}/{TradeHelper.MAX_ROSTER}";
        int avgOverall = _players.Count > 0
            ? (int)_players.Average(p => p.GetCalculatedAverage()) : 0;
        _summaryOverall.text = avgOverall.ToString();
        long totalPayroll = _players.Sum(p => p.salary);
        _summaryBudget.text = $"${totalPayroll / 1_000_000}M";

        // Contador two-way bajo el resumen
        int twCount = DatabaseManager.Instance.GetTwoWayCount(_myTeam.id);
        var twLabel = _root.Q<Label>("SummaryTwoWay");
        if (twLabel != null)
            twLabel.text = $"Two-way: {twCount}/{TradeHelper.MAX_TWO_WAY}";
    }

    // ── ROSTER LIST ──────────────────────────────────────

    void ReloadRosterList()
    {
        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        BuildRosterList();
        RefreshSummary();
        if (_selectedPlayer != null)
        {
            var still = _players.FirstOrDefault(p => p.id == _selectedPlayer.id);
            if (still != null) ShowPlayerDetail(still);
        }
    }

    void BuildRosterList()
    {
        _rosterBody.Clear();

        foreach (var pos in PositionCodes.Order)
        {
            var posPlayers = _players
                .Where(p => p.position == pos)
                .OrderByDescending(p => p.GetCalculatedAverage())
                .ToList();

            if (posPlayers.Count == 0) continue;

            // Cabecera posición
            var posHeader = new VisualElement();
            posHeader.AddToClassList("pos-header");

            var badge = new Label();
            badge.AddToClassList("pos-badge");
            badge.text = PositionCodes.GetShort(pos);

            var label = new Label();
            label.AddToClassList("pos-label");
            label.text = PositionCodes.GetName(pos);

            posHeader.Add(badge);
            posHeader.Add(label);

            var section = new VisualElement();
            section.AddToClassList("pos-section");
            section.Add(posHeader);

            // Filas de jugadores
            for (int i = 0; i < posPlayers.Count; i++)
            {
                var player = posPlayers[i];
                var row = CreatePlayerRow(i + 1, player);
                section.Add(row);
            }

            _rosterBody.Add(section);
        }
    }

    VisualElement CreatePlayerRow(int num, PlayerData player)
    {
        var row = new VisualElement();
        row.AddToClassList("player-row");
        if (player.injury_days > 0)
            row.AddToClassList("player-row--injured");
        if (player.on_trade_block == 1)
            row.AddToClassList("player-row--on-block");

        var nameLbl = new Label();
        nameLbl.AddToClassList("player-name");
        if (player.injury_days > 0)
            nameLbl.AddToClassList("player-name--injured");
        nameLbl.text = player.is_rookie == 1
            ? $"{player.first_name} {player.last_name} (R)"
            : $"{player.first_name} {player.last_name}";

        var ovrLbl = new Label();
        ovrLbl.AddToClassList("player-ovr");
        int ovrVal = player.GetCalculatedAverage();
        ovrLbl.text = ovrVal.ToString();
        if (ovrVal > 84)
            ovrLbl.AddToClassList("player-ovr--high");
        else if (ovrVal >= 70)
            ovrLbl.AddToClassList("player-ovr--mid");
        else
            ovrLbl.AddToClassList("player-ovr--low");

        var metaLbl = new Label();
        metaLbl.AddToClassList("player-meta");
        metaLbl.text = $"{player.age} años · {player.height_cm / 100f:F2}m";

        var salaryLbl = new Label();
        salaryLbl.AddToClassList("player-salary");
        salaryLbl.text = $"{player.salary.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-ES"))} $";

        var contractLbl = new Label();
        contractLbl.AddToClassList("player-contract");
        contractLbl.text = $"{player.contract_years} año{(player.contract_years != 1 ? "s" : "")}";
        if (player.contract_years <= 1)
            contractLbl.AddToClassList("player-contract--expiring");

        // Morale dot
        var moraleDot = new VisualElement();
        moraleDot.AddToClassList("morale-dot");
        Color dotColor;
        if (player.morale >= 70)
            dotColor = new Color32(39, 174, 96, 255);
        else if (player.morale >= 40)
            dotColor = new Color32(212, 160, 23, 255);
        else
            dotColor = new Color32(192, 57, 43, 255);
        moraleDot.style.backgroundColor = new StyleColor(dotColor);

        var moLabel = new Label("MO");
        moLabel.AddToClassList("dot-label");

        // Fisico dot
        var fisicoDot = new VisualElement();
        fisicoDot.AddToClassList("fisico-dot");
        Color fisicoColor;
        if (player.fisico >= 60)
            fisicoColor = new Color32(39, 174, 96, 255);
        else if (player.fisico >= 30)
            fisicoColor = new Color32(212, 160, 23, 255);
        else
            fisicoColor = new Color32(192, 57, 43, 255);
        fisicoDot.style.backgroundColor = new StyleColor(fisicoColor);

        var fiLabel = new Label("FI");
        fiLabel.AddToClassList("dot-label");

        var posLbl = new Label();
        posLbl.AddToClassList("player-pos");
        posLbl.text = PositionCodes.GetName(player.secondary_position);

        // Role icon
        var roleIcon = new VisualElement();
        roleIcon.AddToClassList("player-row-role-icon");
        UpdateRoleIcon(roleIcon, player.role);

        row.Add(nameLbl);
        if (player.injury_days > 0)
        {
            var lesBadge = new Label("LES");
            lesBadge.AddToClassList("player-badge");
            lesBadge.AddToClassList("player-badge--les");
            row.Add(lesBadge);
        }
        if (player.is_two_way == 1)
        {
            var twBadge = new Label("TW");
            twBadge.AddToClassList("player-badge");
            twBadge.AddToClassList("player-badge--tw");
            row.Add(twBadge);
        }
        if (player.g_league_assigned == 1)
        {
            var glBadge = new Label("GL");
            glBadge.AddToClassList("player-badge");
            glBadge.AddToClassList("player-badge--gl");
            row.Add(glBadge);
        }
        if (player.is_on_ir == 1)
        {
            var irBadge = new Label("IR");
            irBadge.AddToClassList("player-badge");
            irBadge.AddToClassList("player-badge--ir");
            row.Add(irBadge);
        }
        row.Add(ovrLbl);
        row.Add(moraleDot);
        row.Add(moLabel);
        row.Add(fisicoDot);
        row.Add(fiLabel);
        row.Add(roleIcon);
        row.Add(posLbl);
        row.Add(metaLbl);
        row.Add(salaryLbl);
        row.Add(contractLbl);

        // Trade block label
        var tradeBlockLbl = new Label();
        tradeBlockLbl.AddToClassList("trade-block-label");
        if (player.on_trade_block == 1)
            tradeBlockLbl.AddToClassList("trade-block-label--active");
        tradeBlockLbl.text = player.on_trade_block == 1 ? "TRANSFERIBLE" : "BLOQUEADO";
        tradeBlockLbl.RegisterCallback<ClickEvent>(evt =>
        {
            evt.StopPropagation();
            PlayClick();
            player.on_trade_block = player.on_trade_block == 1 ? 0 : 1;
            DatabaseManager.Instance.UpdatePlayer(player);
            row.EnableInClassList("player-row--on-block", player.on_trade_block == 1);
            tradeBlockLbl.text = player.on_trade_block == 1 ? "TRANSFERIBLE" : "BLOQUEADO";
            tradeBlockLbl.EnableInClassList("trade-block-label--active", player.on_trade_block == 1);
        });
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(tradeBlockLbl);
        row.Add(tradeBlockLbl);

        // Columna G-League: ASIGNAR / RECUPERAR
        var glLbl = new Label();
        glLbl.AddToClassList("gleague-label");
        bool canAssign = player.g_league_assigned == 0
            && player.injury_days == 0
            && player.is_on_ir == 0
            && GLeagueHelper.HasEnoughActive(_players);
        if (player.g_league_assigned == 1)
        {
            glLbl.text = "G-LEAGUE";
            glLbl.AddToClassList("gleague-label--on");
            glLbl.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                DatabaseManager.Instance.SetGLeagueAssignment(player, false);
                ReloadRosterList();
            });
        }
        else if (canAssign)
        {
            glLbl.text = "ASIGNAR G";
            glLbl.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                DatabaseManager.Instance.SetGLeagueAssignment(player, true);
                ReloadRosterList();
            });
        }
        else
        {
            glLbl.text = "—";
            glLbl.AddToClassList("gleague-label--disabled");
        }
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(glLbl);
        row.Add(glLbl);

        row.userData = player;

        // Click
        row.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnPlayerSelected(player, row); });
        if (CursorManager.Instance != null)
        {
            row.RegisterCallback<MouseEnterEvent>(_ =>
                CursorManager.Instance.SetHandCursor());
            row.RegisterCallback<MouseLeaveEvent>(_ =>
                CursorManager.Instance.SetDefaultCursor());
        }

        return row;
    }

    // ── DETALLE JUGADOR ──────────────────────────────────

    void OnPlayerSelected(PlayerData player, VisualElement row)
    {
        _selectedPlayer = player;

        // Quitar selección anterior
        _rosterBody.Query<VisualElement>(className: "player-row--selected")
                   .ForEach(e => e.RemoveFromClassList("player-row--selected"));
        row.AddToClassList("player-row--selected");

        ShowPlayerDetail(player);
    }

    void ShowPlayerDetail(PlayerData p)
    {
        _detailEmpty.style.display = DisplayStyle.None;
        _detailScroll.style.display = DisplayStyle.Flex;

        // Cabecera
        _detailPlayerName.text = $"{p.first_name} {p.last_name}".ToUpper();
        _detailPlayerPos.text = GetPositionDisplay(p.position, p.secondary_position);
        _detailPlayerMeta.text = $"{p.age} años · {CountryCodes.GetName(p.nationality)} · {p.height_cm / 100f:F2}m · {p.weight_kg}kg{(p.is_rookie == 1 ? " · Rookie" : "")}";
        int detailOvrVal = p.GetCalculatedAverage();
        _detailOvr.text = detailOvrVal.ToString();
        _detailOvr.RemoveFromClassList("detail-ovr--high");
        _detailOvr.RemoveFromClassList("detail-ovr--mid");
        _detailOvr.RemoveFromClassList("detail-ovr--low");
        if (detailOvrVal > 84)
            _detailOvr.AddToClassList("detail-ovr--high");
        else if (detailOvrVal >= 70)
            _detailOvr.AddToClassList("detail-ovr--mid");
        else
            _detailOvr.AddToClassList("detail-ovr--low");

        // Foto
        if (_detailPhoto != null)
        {
            Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
            if (tex != null)
                _detailPhoto.style.backgroundImage = new StyleBackground(tex);
            else
                _detailPhoto.style.backgroundImage = StyleKeyword.None;
        }

        // Salud
        if (p.injury_days > 0)
        {
            _detailHealth.text = $"🏥   {p.injury_type} — {p.injury_days} días de baja";
            _detailHealth.RemoveFromClassList("detail-health-ok");
            _detailHealth.AddToClassList("detail-health-injured");
        }
        else
        {
            _detailHealth.text = "✓  DISPONIBLE";
            _detailHealth.RemoveFromClassList("detail-health-injured");
            _detailHealth.AddToClassList("detail-health-ok");
        }

        // Atributos
        BuildAttrBars(p);

        // Stats temporada
        var s = DatabaseManager.Instance.GetPlayerSeasonStats(p.id, _manager.id);
        _statPts.text = s.avgPts.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        _statReb.text = s.avgReb.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        _statAst.text = s.avgAst.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        _statStl.text = s.avgStl.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        _statBlk.text = s.avgBlk.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        // Stats G-League (si tiene minutos en la liga de desarrollo esta temporada)
        if (_detailGLeague != null && _gleagueStatsText != null)
        {
            var glStats = _season != null
                ? DatabaseManager.Instance.GetGLeagueStats(p.id, _season.id)
                : null;
            if (glStats != null && glStats.games > 0)
            {
                var dec = System.Globalization.CultureInfo.InvariantCulture;
                _detailGLeague.style.display = DisplayStyle.Flex;
                _gleagueStatsText.text =
                    $"{glStats.games} partidos · {(glStats.points / (float)glStats.games).ToString("F1", dec)} pts · " +
                    $"{(glStats.rebounds / (float)glStats.games).ToString("F1", dec)} reb · {(glStats.assists / (float)glStats.games).ToString("F1", dec)} ast";
            }
            else
            {
                _detailGLeague.style.display = DisplayStyle.None;
            }
        }

        // Contrato y potencial
        int guaranteed = p.guaranteed_years;
        int total = p.contract_years;
        bool hasOpt = p.has_team_option == 1 || p.has_player_option == 1;
        string optLabel = p.has_team_option == 1 ? "Team Option" : "Player Option";
        string optColor = p.has_team_option == 1 ? "#27AE60" : "#F8C440";

        string contractText = $"${p.salary / 1_000_000}M/año";
        if (hasOpt && guaranteed == 0)
            contractText += $" · {total} año{(total != 1 ? "s" : "")} (<color={optColor}>{optLabel}</color>)";
        else if (hasOpt)
            contractText += $" · {guaranteed} año{(guaranteed != 1 ? "s" : "")} + <color={optColor}>{optLabel}</color>";
        else
            contractText += $" · {total} año{(total != 1 ? "s" : "")}";
        _detailContract.text = contractText;
        _detailPotential.text = p.potential.ToString();

        // Rol - icono clickeable que rota
        var roleIcon = _root.Q<VisualElement>("DetailRoleIcon");
        if (roleIcon != null)
        {
            UpdateRoleIcon(roleIcon, p.role);
            roleIcon.userData = p;
            roleIcon.UnregisterCallback<ClickEvent>(CycleRole);
            roleIcon.RegisterCallback<ClickEvent>(CycleRole);
            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(roleIcon);
        }
        var roleNameLbl = _root.Q<Label>("DetailRoleName");
        if (roleNameLbl != null)
            roleNameLbl.text = GetRoleName(p.role);

        // Botón renovar siempre visible
        if (_btnRenew != null)
            _btnRenew.style.display = DisplayStyle.Flex;
    }

    void BuildAttrBars(PlayerData p)
    {
        _detailAttrs.Clear();

        var attrs = new[]
        {
            ("TIRO",      p.shooting),
            ("TRIPLE",    p.three_point),
            ("PASE",      p.passing),
            ("BOTE",      p.dribbling),
            ("DEFENSA",   p.defense),
            ("REBOTE",    p.rebounding),
            ("VELOCIDAD", p.speed),
            ("ATLETISMO", p.athleticism),
            ("IQ",        p.iq),
            ("ROBOS",     p.steals),
            ("TAPONES",   p.blocks),
            ("MORAL",     p.morale),
            ("FÍSICO",    p.fisico),
        };

        foreach (var (label, val) in attrs)
        {
            var badge = new VisualElement();
            badge.AddToClassList("attr-badge");
            if (val < 40) badge.AddToClassList("attr-badge--low");
            else if (val < 70) badge.AddToClassList("attr-badge--mid");

            var lbl = new Label();
            lbl.AddToClassList("attr-badge-label");
            lbl.text = label;

            var valLbl = new Label();
            valLbl.AddToClassList("attr-badge-val");
            valLbl.text = val.ToString();

            badge.Add(lbl);
            badge.Add(valLbl);
            _detailAttrs.Add(badge);
        }
    }

    // ── RENOVAR CONTRATO ──────────────────────────────────

    void ToggleRenewOption(string type)
    {
        if (type == "team")
        {
            _teamOptionActive = !_teamOptionActive;
            if (_teamOptionActive) _playerOptionActive = false;
        }
        else
        {
            _playerOptionActive = !_playerOptionActive;
            if (_playerOptionActive) _teamOptionActive = false;
        }
        if (_teamOptionActive || _playerOptionActive)
            _renewTwoWayActive = false;
        RefreshRenewOptionToggles();
        RefreshRenewSpinners();
    }

    void ToggleRenewTwoWay()
    {
        _renewTwoWayActive = !_renewTwoWayActive;
        if (_renewTwoWayActive)
        {
            _teamOptionActive = false;
            _playerOptionActive = false;
            _renewSalary = TradeHelper.TWO_WAY_SALARY;
            _renewYears = 2;
        }
        else
        {
            long autoSalary = CalculateAutoSalary(_selectedPlayer.salary);
            _renewSalary = autoSalary < _renewMaxSalary ? autoSalary : _renewMaxSalary;
            _renewSalary = (long)(Mathf.Round(_renewSalary / 100_000f) * 100_000);
            _renewYears = CalculateAutoYears(_selectedPlayer.age);
        }
        RefreshRenewOptionToggles();
        RefreshRenewSpinners();
        UpdateCapWarning();
    }

    void RefreshRenewOptionToggles()
    {
        if (_renewTeamOption != null)
        {
            _renewTeamOption.RemoveFromClassList("renew-toggle-btn--team-active");
            if (_teamOptionActive)
                _renewTeamOption.AddToClassList("renew-toggle-btn--team-active");
        }
        if (_renewPlayerOption != null)
        {
            _renewPlayerOption.RemoveFromClassList("renew-toggle-btn--player-active");
            if (_playerOptionActive)
                _renewPlayerOption.AddToClassList("renew-toggle-btn--player-active");
        }
        if (_renewTwoWayToggle != null)
        {
            _renewTwoWayToggle.RemoveFromClassList("renew-toggle-btn--tw-active");
            if (_renewTwoWayActive)
                _renewTwoWayToggle.AddToClassList("renew-toggle-btn--tw-active");
        }
    }

    void OnRenewClicked()
    {
        if (_selectedPlayer == null) return;

        // Verificar cooldown de renovación
        int currentDay = _season?.current_game_day ?? 0;
        if (_selectedPlayer.renewal_cooldown_day > currentDay)
        {
            int daysLeft = _selectedPlayer.renewal_cooldown_day - currentDay;
            OpenRenewCooldownModal(daysLeft);
            return;
        }

        // Si tiene 3+ años restantes: modal de bloqueo
        if (_selectedPlayer.contract_years >= 3)
        {
            OpenRenewBlockModal();
            return;
        }

        // Resetear estado
        _offerSent = false;

        // Calcular límites Bird Rights + cap space
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long totalPayroll = _players != null ? _players.Sum(p => p.salary) : 0;
        _renewMaxSalary = CalculateMaxOfferSalary(_selectedPlayer, leagueSettings, totalPayroll);

        // Mostrar formulario, ocultar pending
        if (_renewFormRowSalary != null) _renewFormRowSalary.style.display = DisplayStyle.Flex;
        if (_renewFormRowYears != null) _renewFormRowYears.style.display = DisplayStyle.Flex;
        if (_renewFormRowOptions != null) _renewFormRowOptions.style.display = DisplayStyle.Flex;
        if (_renewPendingText != null) _renewPendingText.style.display = DisplayStyle.None;
        if (_renewText1 != null) _renewText1.style.display = DisplayStyle.Flex;
        if (_renewText2 != null) _renewText2.style.display = DisplayStyle.Flex;
        if (_btnRenewConfirm != null)
        {
            _btnRenewConfirm.SetEnabled(true);
            _btnRenewConfirm.text = "ENVIAR OFERTA";
        }

        // Poner valores por defecto
        long autoSalary = CalculateAutoSalary(_selectedPlayer.salary);
        _renewSalary = autoSalary < _renewMaxSalary ? autoSalary : _renewMaxSalary;
        _renewSalary = (long)(Mathf.Round(_renewSalary / 100_000f) * 100_000);
        _renewYears = CalculateAutoYears(_selectedPlayer.age);
        _teamOptionActive = false;
        _playerOptionActive = false;
        _renewTwoWayActive = false;

        // Contrato two-way: solo jugadores jóvenes (≤23) y si quedan plazas
        bool renewTwoWayEligible = TradeHelper.IsEligibleForTwoWay(_selectedPlayer)
            && DatabaseManager.Instance.GetTwoWayCount(_myTeam.id) < TradeHelper.MAX_TWO_WAY;
        if (_renewFormRowTwoWay != null)
            _renewFormRowTwoWay.style.display = renewTwoWayEligible ? DisplayStyle.Flex : DisplayStyle.None;
        if (_renewTwoWayToggle != null)
            _renewTwoWayToggle.SetEnabled(renewTwoWayEligible);

        RefreshRenewOptionToggles();
        RefreshRenewSpinners();

        ClearRenewModalColor(_renewBox, _renewTitle);
        string playerName = $"{_selectedPlayer.first_name} {_selectedPlayer.last_name}";
        if (_renewText1 != null)
            _renewText1.text = $"Oferta de renovación para {playerName}";

        // Mostrar advertencia de lujo si aplica
        UpdateCapWarning();

        UpdateRenewMaxInfo();
        UpdateAcceptScoreDisplay();

        if (_renewOverlay != null) _renewOverlay.style.display = DisplayStyle.Flex;
        if (_renewBox != null) _renewBox.style.display = DisplayStyle.Flex;
    }

    int CalculateAutoYears(int age)
    {
        if (age <= 25) return 5;
        if (age <= 28) return 4;
        if (age <= 32) return 3;
        if (age < 40) return 2;
        return 1;
    }

    long CalculateAutoSalary(long currentSalary)
    {
        long newSalary = (long)(currentSalary * 1.05);
        newSalary = (long)(Mathf.Round(newSalary / 100_000f) * 100_000);
        if (newSalary < currentSalary) newSalary = currentSalary;
        return newSalary;
    }
    public static float CalculateAcceptScore(PlayerData player, long offerSalary, int offerYears, int gamesPlayed, int teamChemistry)
    {
        float score = 50f;
        float salaryIncrease = player.salary > 0 ? (float)(offerSalary - player.salary) / player.salary : 0f;

        if (salaryIncrease >= 0.30f) score += 25f;
        else if (salaryIncrease >= 0.10f) score += 15f;
        else if (salaryIncrease >= 0f) score += 5f;
        else score -= Mathf.Abs(salaryIncrease) * 50f;

        if (player.age >= 32) score += 10f;
        else if (player.age >= 28) score += 5f;
        else if (player.age <= 23) score -= 5f;

        if (player.overall >= 85) score -= 5f;
        else if (player.overall < 75) score += 5f;

        if (gamesPlayed >= 50) score += 10f;
        else if (gamesPlayed >= 30) score += 5f;
        else if (gamesPlayed < 10) score -= 10f;

        if (offerYears >= 4) score += 10f;
        else if (offerYears >= 3) score += 5f;
        else if (offerYears < 2) score -= 5f;

        float chemistryMod = (teamChemistry - 50) * 0.3f;
        score += chemistryMod;

        return Mathf.Max(10f, Mathf.Min(95f, score));
    }

    float CalculateAcceptScore(long offerSalary, int offerYears)
    {
        int gamesPlayed = 0;
        if (_season != null)
            gamesPlayed = DatabaseManager.Instance.GetPlayerGamesPlayedInSeason(_selectedPlayer.id, _season.id);
        int teamChem = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        return CalculateAcceptScore(_selectedPlayer, offerSalary, offerYears, gamesPlayed, teamChem);
    }
    public struct MaxOfferBreakdown
    {
        public long finalMax;
    public long maxByExp;
    public long birdMax;
    public long capSpaceMax;
    public long exceptionMax;
    public string birdTierName;
    public string bindingReason;
    public string exceptionName;
    }
    public static MaxOfferBreakdown GetMaxOfferBreakdown(PlayerData player, LeagueSettingsData settings, long totalPayroll, bool isFromSameTeam = true, bool proManagerOnly = false)
    {
        var result = new MaxOfferBreakdown();

        if (settings == null)
        {
            result.finalMax = 60_000_000;
            result.birdTierName = "—";
            result.bindingReason = "Sin configuración de liga";
            return result;
        }

        long firstApron = settings.apron > 0 ? settings.apron : TradeHelper.FIRST_APRON;
        long secondApron = settings.repeater_apron > 0 ? settings.repeater_apron : TradeHelper.SECOND_APRON;
        long ntMle = settings.mid_level > 0 ? settings.mid_level : TradeHelper.NT_MLE;
        long tMle = settings.taxpayer_mid_level > 0 ? settings.taxpayer_mid_level : TradeHelper.T_MLE;
        long minSalary = settings.minimum_salary > 0 ? settings.minimum_salary : TradeHelper.MIN_SALARY;

        int exp = Mathf.Max(0, player.age - 22);
        if (exp <= 6) result.maxByExp = (long)(settings.salary_cap * 0.25);
        else if (exp <= 9) result.maxByExp = (long)(settings.salary_cap * 0.30);
        else result.maxByExp = (long)(settings.salary_cap * 0.35);

        if (isFromSameTeam)
        {
            if (player.seasons_with_team >= 3)
            {
                result.birdTierName = "COMPLETOS";
                result.birdMax = result.maxByExp;
            }
            else if (player.seasons_with_team == 2)
            {
                result.birdTierName = "EARLY";
                result.birdMax = player.salary * 175 / 100;
                long avgPct = (long)(settings.salary_cap * 105 / 1000);
                if (avgPct > result.birdMax) result.birdMax = avgPct;
            }
            else
            {
                result.birdTierName = "SIN BIRD";
                result.birdMax = player.salary * 120 / 100;
            }
        }
        else
        {
            // FA externo: sin Bird Rights
            result.birdMax = 0;
            result.birdTierName = "SIN BIRD (FA)";
        }

        long capSpace = settings.salary_cap - totalPayroll;
        result.capSpaceMax = player.salary + (capSpace > 0 ? capSpace : 0);

        result.exceptionMax = 0;
        result.exceptionName = "";

        long rawMax;
        if (!isFromSameTeam && totalPayroll > settings.salary_cap)
        {
            // FA externo y equipo sobre el cap: solo excepciones.
            // ProManager: sin Non-Taxpayer MLE → la excepción pasa a ser la Taxpayer MLE
            // aunque el equipo esté solo sobre el cap (≤1er apron).
            if (proManagerOnly)
            {
                result.exceptionMax = tMle;
                result.exceptionName = "Mid-Level Exception (Taxpayer)";
                result.capSpaceMax = 0;
            }
            else if (totalPayroll <= firstApron)
            {
                result.exceptionMax = ntMle;
                result.exceptionName = "Mid-Level Exception (No Taxpayer)";
            }
            else if (totalPayroll <= secondApron)
            {
                result.exceptionMax = tMle;
                result.exceptionName = "Mid-Level Exception (Taxpayer)";
                result.capSpaceMax = 0;
            }
            else
            {
                result.exceptionMax = minSalary;
                result.exceptionName = "Mínimo";
                result.capSpaceMax = 0;
            }
            rawMax = result.exceptionMax;
        }
        else
        {
            rawMax = result.birdMax > result.capSpaceMax ? result.birdMax : result.capSpaceMax;
        }

        result.finalMax = result.maxByExp < rawMax ? result.maxByExp : rawMax;

        // Determine binding reason
        if (result.finalMax == result.maxByExp && result.maxByExp <= rawMax)
            result.bindingReason = $"Máx. por experiencia ({FormatMoney(result.maxByExp)})";
        else if (!isFromSameTeam && totalPayroll > settings.salary_cap)
            result.bindingReason = $"Excepción {result.exceptionName} ({FormatMoney(result.exceptionMax)})";
        else if (result.birdMax >= result.capSpaceMax)
            result.bindingReason = $"Bird Rights {result.birdTierName} ({FormatMoney(result.birdMax)})";
        else
            result.bindingReason = $"Espacio salarial ({FormatMoney(result.capSpaceMax)})";

        return result;
    }
    public static long CalculateMaxOfferSalary(PlayerData player, LeagueSettingsData settings, long totalPayroll, bool isFromSameTeam = true, bool proManagerOnly = false)
    {
        return GetMaxOfferBreakdown(player, settings, totalPayroll, isFromSameTeam, proManagerOnly).finalMax;
    }

    string GetBirdRightsTier(PlayerData player)
    {
        if (player.seasons_with_team >= 3) return "COMPLETOS";
        if (player.seasons_with_team == 2) return "EARLY";
        return "SIN BIRD";
    }

    void UpdateCapWarning()
    {
        if (_renewWarningText == null || _selectedPlayer == null || _players == null) return;

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        if (leagueSettings == null) return;

        long totalPayroll = _players.Sum(p => p.salary);
        long newTotal = totalPayroll - _selectedPlayer.salary + _renewSalary;

        if (newTotal > leagueSettings.luxury_tax)
        {
            long overage = newTotal - leagueSettings.luxury_tax;
            _renewWarningText.text = $"AVISO: Salario total (${newTotal / 1_000_000}M) supera el límite de lujo en ${overage / 1_000_000}M";
            _renewWarningText.style.display = DisplayStyle.Flex;
        }
        else
        {
            _renewWarningText.style.display = DisplayStyle.None;
        }
    }

    void UpdateRenewMaxInfo()
    {
        if (_renewMaxInfo == null || _selectedPlayer == null || _players == null) return;

        var settings = DatabaseManager.Instance.GetLeagueSettings();
        if (settings == null) return;

        long totalPayroll = _players.Sum(p => p.salary);
        var breakdown = GetMaxOfferBreakdown(_selectedPlayer, settings, totalPayroll);

        _renewMaxInfo.text = $"Máximo: {FormatMoney(breakdown.finalMax)} — {breakdown.bindingReason}";
        _renewMaxInfo.style.display = DisplayStyle.Flex;
    }

    void SendOffer()
    {
        if (_selectedPlayer == null || _offerSent) return;

        // Asegurar que los valores están dentro de límites
        if (_renewSalary < 1000000) _renewSalary = 1000000;
        if (_renewYears < 1) _renewYears = 1;
        else if (_renewYears > 5) _renewYears = 5;

        // Contrato two-way: salario fijo y duración de 2 años, sin límites de cap
        if (_renewTwoWayActive)
        {
            _renewSalary = TradeHelper.TWO_WAY_SALARY;
            _renewYears = 2;
        }
        else if (_renewSalary > _renewMaxSalary)
        {
            _renewSalary = _renewMaxSalary;
            RefreshRenewSpinners();
            if (_renewWarningText != null)
            {
                _renewWarningText.text = $"Oferta ajustada al máximo legal: {FormatMoney(_renewMaxSalary)}.";
                _renewWarningText.style.display = DisplayStyle.Flex;
            }
        }

        _renewOfferSalary = _renewSalary;
        _renewOfferYears = _renewYears;
        RefreshRenewSpinners();

        _offerSent = true;

        // Guardar oferta en BD
        int currentDay = _season?.current_game_day ?? 0;
        bool hasTeamOpt = _teamOptionActive && !_renewTwoWayActive;
        bool hasPlayerOpt = _playerOptionActive && !_renewTwoWayActive;
        int guarYears = (hasTeamOpt || hasPlayerOpt) ? System.Math.Max(0, _renewOfferYears - 1) : _renewOfferYears;
        var offer = new OfferData
        {
            manager_id = _manager.id,
            player_id = _selectedPlayer.id,
            offer_salary = _renewOfferSalary,
            offer_years = _renewOfferYears,
            guaranteed_years = guarYears,
            has_team_option = hasTeamOpt ? 1 : 0,
            has_player_option = hasPlayerOpt ? 1 : 0,
            is_two_way = _renewTwoWayActive ? 1 : 0,
            day_sent = currentDay,
            status = "pending",
            processed = 0
        };
        DatabaseManager.Instance.AddOffer(offer);
        Debug.Log($"[Roster] Oferta guardada: player={_selectedPlayer.id} salary={_renewOfferSalary} years={_renewOfferYears} day_sent={currentDay}");

        // Ocultar formulario, mostrar pending
        if (_renewFormRowSalary != null) _renewFormRowSalary.style.display = DisplayStyle.None;
        if (_renewFormRowYears != null) _renewFormRowYears.style.display = DisplayStyle.None;
        if (_renewFormRowOptions != null) _renewFormRowOptions.style.display = DisplayStyle.None;
        if (_renewFormRowTwoWay != null) _renewFormRowTwoWay.style.display = DisplayStyle.None;
        if (_renewText1 != null) _renewText1.style.display = DisplayStyle.None;
        if (_renewText2 != null) _renewText2.style.display = DisplayStyle.None;
        if (_renewPendingText != null) _renewPendingText.style.display = DisplayStyle.Flex;

        // Deshabilitar botón enviar oferta
        if (_btnRenewConfirm != null)
        {
            _btnRenewConfirm.SetEnabled(false);
            _btnRenewConfirm.text = "ENVIADA";
        }
    }

    void ShowRenewResultModal(string title, string line1, string line2, bool positive)
    {
        if (_renewResultTitle != null) _renewResultTitle.text = title;
        if (_renewResultText1 != null) _renewResultText1.text = line1;
        if (_renewResultText2 != null) _renewResultText2.text = line2;
        if (_renewResultOverlay != null) _renewResultOverlay.style.display = DisplayStyle.Flex;
        if (_renewResultBox != null) _renewResultBox.style.display = DisplayStyle.Flex;
        SetRenewModalColor(_renewResultBox, _renewResultTitle, positive);
        SetRenewResultIcon(_renewResultIcon, positive);

        StartCoroutine(AutoCloseRenewResult());
    }

    System.Collections.IEnumerator AutoCloseRenewResult()
    {
        yield return new WaitForSeconds(5f);
        CloseRenewResultModal();
    }

    void CloseRenewResultModal()
    {
        if (_renewResultOverlay != null) _renewResultOverlay.style.display = DisplayStyle.None;
        if (_renewResultBox != null) _renewResultBox.style.display = DisplayStyle.None;
        ClearRenewModalColor(_renewResultBox, _renewResultTitle);
        ClearRenewResultIcon(_renewResultIcon);
    }

    void OpenRenewCooldownModal(int daysLeft)
    {
        if (_selectedPlayer == null || _renewCooldownText == null) return;

        string playerName = $"{_selectedPlayer.first_name} {_selectedPlayer.last_name}";
        _renewCooldownText.text = $"{playerName} no puede ser renovado ahora. Debes esperar {daysLeft} día{(daysLeft != 1 ? "s" : "")} para intentarlo de nuevo.";

        _renewCooldownOverlay.style.display = DisplayStyle.Flex;
        _renewCooldownBox.style.display = DisplayStyle.Flex;
        SetRenewModalColor(_renewCooldownBox, _renewCooldownTitle, false);
        SetRenewResultIcon(_renewCooldownIcon, false);
    }

    void CloseRenewCooldownModal()
    {
        _renewCooldownOverlay.style.display = DisplayStyle.None;
        _renewCooldownBox.style.display = DisplayStyle.None;
        ClearRenewModalColor(_renewCooldownBox, _renewCooldownTitle);
        ClearRenewResultIcon(_renewCooldownIcon);
    }

    // ── Spinner helpers ─────────────────────────────────────

    void StepRenewSalary(int dir)
    {
        long val = _renewSalary + 500_000 * dir;
        _renewSalary = val < 1_000_000 ? 1_000_000 : (val > _renewMaxSalary ? _renewMaxSalary : val);
        RefreshRenewSpinners();
        UpdateCapWarning();
        UpdateAcceptScoreDisplay();
    }

    void StepRenewYears(int dir)
    {
        int val = _renewYears + dir;
        _renewYears = val < 1 ? 1 : (val > 5 ? 5 : val);
        RefreshRenewSpinners();
        UpdateAcceptScoreDisplay();
    }

    void RefreshRenewSpinners()
    {
        if (_renewSalaryValue != null)
            _renewSalaryValue.text = FormatMoney(_renewSalary);
        if (_renewYearsValue != null)
            _renewYearsValue.text = $"{_renewYears} año{(_renewYears > 1 ? "s" : "")}";

        ToggleRenewSpinDisabled(_renewSalaryDec, _renewTwoWayActive || _renewSalary <= 1_000_000);
        ToggleRenewSpinDisabled(_renewSalaryInc, _renewTwoWayActive || _renewSalary >= _renewMaxSalary);
        ToggleRenewSpinDisabled(_renewYearsDec, _renewTwoWayActive || _renewYears <= 1);
        ToggleRenewSpinDisabled(_renewYearsInc, _renewTwoWayActive || _renewYears >= 5);
    }

    void ToggleRenewSpinDisabled(Label el, bool disabled)
    {
        if (el == null) return;
        if (disabled)
            el.AddToClassList("btn-spin--disabled");
        else
            el.RemoveFromClassList("btn-spin--disabled");
    }

    void SetupRenewLongPress(VisualElement el, System.Action onStep)
    {
        if (el == null) return;

        IVisualElementScheduledItem scheduled = null;

        el.RegisterCallback<PointerDownEvent>(_ =>
        {
            PlayClick();
            scheduled?.Pause();
            onStep();
            scheduled = el.schedule.Execute(() => onStep()).Every(80).StartingIn(350);
        });

        el.RegisterCallback<PointerUpEvent>(_ =>
        {
            if (scheduled != null)
            {
                scheduled.Pause();
                scheduled = null;
            }
        });

        el.RegisterCallback<PointerCaptureOutEvent>(_ =>
        {
            if (scheduled != null)
            {
                scheduled.Pause();
                scheduled = null;
            }
        });
    }

    void RegisterRenewSpinnerCursor(Label el)
    {
        if (el != null && CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(el);
    }

    void CloseRenewModal()
    {
        if (_renewOverlay != null) _renewOverlay.style.display = DisplayStyle.None;
        if (_renewBox != null) _renewBox.style.display = DisplayStyle.None;
        if (_renewPendingText != null) _renewPendingText.style.display = DisplayStyle.None;
        if (_renewWarningText != null) _renewWarningText.style.display = DisplayStyle.None;
        ClearRenewModalColor(_renewBox, _renewTitle);
        _offerSent = false;
        _renewMaxSalary = 0;
    }

    void UpdateAcceptScoreDisplay()
    {
        if (_selectedPlayer == null || _renewText2 == null) return;
        if (_renewSalary <= 0 || _renewYears < 1)
        {
            _renewText2.text = "";
            return;
        }
        float score = CalculateAcceptScore(_renewSalary, _renewYears);
        _renewText2.text = $"Probabilidad de aceptación: {score:F0}%";
    }

    void OpenRenewBlockModal()
    {
        if (_selectedPlayer == null) return;

        string playerName = $"{_selectedPlayer.first_name} {_selectedPlayer.last_name}";
        _renewBlockText.text = $"{playerName} tiene {_selectedPlayer.contract_years} años de contrato restantes. Solo se pueden renovar contratos con menos de 3 años restantes.";

        _renewBlockOverlay.style.display = DisplayStyle.Flex;
        _renewBlockBox.style.display = DisplayStyle.Flex;
        SetRenewModalColor(_renewBlockBox, _renewBlockTitle, false);
        SetRenewResultIcon(_renewBlockIcon, false);
    }

    void CloseRenewBlockModal()
    {
        _renewBlockOverlay.style.display = DisplayStyle.None;
        _renewBlockBox.style.display = DisplayStyle.None;
        ClearRenewModalColor(_renewBlockBox, _renewBlockTitle);
        ClearRenewResultIcon(_renewBlockIcon);
    }

    void SetRenewModalColor(VisualElement box, Label title, bool positive)
    {
        if (box != null)
        {
            box.RemoveFromClassList("renew-modal-box--positive");
            box.RemoveFromClassList("renew-modal-box--negative");
            box.AddToClassList(positive ? "renew-modal-box--positive" : "renew-modal-box--negative");
        }
        if (title != null)
        {
            title.RemoveFromClassList("renew-modal-title--positive");
            title.RemoveFromClassList("renew-modal-title--negative");
            title.AddToClassList(positive ? "renew-modal-title--positive" : "renew-modal-title--negative");
        }
    }

    void ClearRenewModalColor(VisualElement box, Label title)
    {
        if (box != null)
        {
            box.RemoveFromClassList("renew-modal-box--positive");
            box.RemoveFromClassList("renew-modal-box--negative");
        }
        if (title != null)
        {
            title.RemoveFromClassList("renew-modal-title--positive");
            title.RemoveFromClassList("renew-modal-title--negative");
        }
    }

    void SetRenewResultIcon(VisualElement iconElem, bool positive)
    {
        if (iconElem == null) return;
        string iconName = positive ? "contrato" : "rechazar";
        var tex = Resources.Load<Texture2D>($"Icons/{iconName}");
        if (tex != null)
            iconElem.style.backgroundImage = new StyleBackground(tex);
        else
            iconElem.style.backgroundImage = null;
    }

    void ClearRenewResultIcon(VisualElement iconElem)
    {
        if (iconElem != null)
            iconElem.style.backgroundImage = null;
    }

    // ── RESCISIÓN (BUYOUT) ──────────────────────────────────

    void OpenBuyoutModal()
    {
        if (_selectedPlayer == null) return;

        long remainingSalary = _selectedPlayer.salary * _selectedPlayer.contract_years;
        int stretchYears = _selectedPlayer.contract_years * 2;
        long annualPayment = remainingSalary / stretchYears;
        long lastYear = remainingSalary - annualPayment * (stretchYears - 1);
        string paymentDetail = annualPayment == lastYear
            ? $"${annualPayment:N0}/año x {stretchYears} años"
            : $"${annualPayment:N0}/año x {stretchYears - 1} años + ${lastYear:N0} (último)";

        _buyoutText1.text = $"Rescisión de contrato de {_selectedPlayer.first_name} {_selectedPlayer.last_name}.";
        _buyoutText2.text = $"Salario restante: ${remainingSalary:N0} · {paymentDetail}";

        _buyoutOverlay.style.display = DisplayStyle.Flex;
        _buyoutBox.style.display = DisplayStyle.Flex;
    }

    void CloseBuyoutModal()
    {
        _buyoutOverlay.style.display = DisplayStyle.None;
        _buyoutBox.style.display = DisplayStyle.None;
    }

    void ConfirmBuyout()
    {
        if (_selectedPlayer == null) return;

        long remainingSalary = _selectedPlayer.salary * _selectedPlayer.contract_years;
        int stretchYears = _selectedPlayer.contract_years * 2;
        long annualPayment = remainingSalary / stretchYears;
        int currentDay = _season?.current_game_day ?? 0;
        string playerName = $"{_selectedPlayer.first_name} {_selectedPlayer.last_name}";
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        _selectedPlayer.team_id = 0;
        _selectedPlayer.last_team_id = 0;
        _selectedPlayer.seasons_with_team = 0;
        DatabaseManager.Instance.UpdatePlayer(_selectedPlayer);

        long remainder = remainingSalary;
        for (int y = 0; y < stretchYears; y++)
        {
            long payment = (y == stretchYears - 1) ? remainder : annualPayment;
            remainder -= payment;
            DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
            {
                team_id = _myTeam.id,
                season_id = _season?.id ?? 0,
                record_type = FinanceRecord.TYPE_BUYOUT,
                game_day = currentDay,
                amount = payment,
                created_at = now
            });
        }

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 0,
            sender_id = 0,
            title = "Rescisión de contrato (buyout)",
            body = $"Se ha rescindido el contrato de {playerName} mediante buyout.\n" +
                   $"El jugador queda libre y puede firmar por cualquier equipo.",
            game_day = currentDay,
            game_date = now,
            created_at = now,
            date_sent = now,
            is_read = 0
        });

        Debug.Log($"[Roster] {playerName} buyout. ${remainingSalary} stretched over {stretchYears} years.");

        CloseBuyoutModal();

        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        _myTeam = DatabaseManager.Instance.GetTeamById(_myTeam.id);
        _selectedPlayer = null;
        Refresh();
    }

    void CycleRole(ClickEvent ev)
    {
        var icon = ev.target as VisualElement;
        if (icon?.userData is not PlayerData p) return;
        PlayerRole newRole = (PlayerRole)(((int)p.role + 1) % 4);
        DatabaseManager.Instance.UpdatePlayerRole(p.id, newRole);
        p.role = newRole;
        UpdateRoleIcon(icon, newRole);

        // Update the corresponding row icon in the list
        foreach (var row in _rosterBody.Query<VisualElement>(className: "player-row").Build().ToList())
        {
            if (row.userData is PlayerData rowPlayer && rowPlayer.id == p.id)
            {
                var rowIcon = row.Q<VisualElement>(className: "player-row-role-icon");
                if (rowIcon != null) UpdateRoleIcon(rowIcon, newRole);
                break;
            }
        }

        // Update the detail role name label
        var roleNameLbl = _root.Q<Label>("DetailRoleName");
        if (roleNameLbl != null) roleNameLbl.text = GetRoleName(newRole);

        PlayClick();
    }

    static string GetRoleName(PlayerRole role) => role switch
    {
        PlayerRole.Estrella => "Estrella",
        PlayerRole.Titular => "Titular",
        PlayerRole.Banquillo => "Banquillo",
        _ => "Último recurso"
    };

    void UpdateRoleIcon(VisualElement icon, PlayerRole role)
    {
        string iconName = role switch
        {
            PlayerRole.Estrella => "rol_estrella",
            PlayerRole.Titular => "rol_titular",
            PlayerRole.Banquillo => "rol_banquillo",
            _ => "rol_ultimoRecurso"
        };
        var tex = Resources.Load<Texture2D>($"Icons/{iconName}");
        icon.style.backgroundImage = tex != null ? new StyleBackground(tex) : StyleKeyword.None;
        icon.tooltip = role switch
        {
            PlayerRole.Estrella => "Estrella",
            PlayerRole.Titular => "Titular",
            PlayerRole.Banquillo => "Banquillo",
            _ => "Último recurso"
        };
    }
}