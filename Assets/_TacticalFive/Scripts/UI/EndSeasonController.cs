using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

public class EndSeasonController : UIScreenController
{
    enum DraftState { Idle, UserPicking, AIPicking, Complete }

    // ── Header ──
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerSeason;
    private Label _headerDate;

    // ── Pre-draft ──
    private Label _seasonTag;
    private VisualElement _retiringList;
    private VisualElement _fameList;
    private Button _btnDraft;
    private Button _btnNextSeason;
    private Button _btnLottery;
    private VisualElement _lotteryOverlay;
    private VisualElement _famePanel;
    private VisualElement _preDraftHeader;
    private VisualElement _preDraftContent;
    private Label _draftYearLabel;
    private Label _draftYearTitle;

    // ── Pre-draft pagination ──
    private Button _btnRetiringPrev;
    private Button _btnRetiringNext;
    private Button _btnFamePrev;
    private Button _btnFameNext;
    private int _retiringPage;
    private int _famePage;
    private List<PlayerData> _retiringPlayers = new();
    private List<PlayerData> _famePlayers = new();
    private const int PRE_DRAFT_PAGE_SIZE = 4;

    // ── Draft room ──
    private VisualElement _draftRoom;
    private Label _draftProgressText;
    private VisualElement _draftProgressFill;
    private Label _draftClassLabel;
    private Label _prospectCount;
    private VisualElement _prospectList;
    private Label _draftPickRound;
    private Label _draftPickNumber;
    private VisualElement _draftPickTeamLogo;
    private Label _draftPickTeamName;
    private VisualElement _draftPlayerCard;
    private Label _draftPlayerName;
    private Label _draftPlayerPosition;
    private Label _draftPlayerSecPos;
    private Label _draftPlayerAge;
    private Label _draftPlayerHeight;
    private Label _draftPlayerWeight;
    private Label _draftPlayerNationality;
    private Label _draftPlayerCollege;
    private Label _draftPlayerTrait;
    private Label _draftAttrMed;
    private Label _draftAttrSpeed;
    private Label _draftAttrShooting;
    private Label _draftAttr3pt;
    private Label _draftAttrPassing;
    private Label _draftAttrDribbling;
    private Label _draftAttrDefense;
    private Label _draftAttrRebounding;
    private Label _draftAttrAthleticism;
    private Label _draftAttrIQ;
    private Label _draftAttrSteals;
    private Label _draftAttrBlocks;
    private Button _btnDraftPick;
    private Button _btnAutoPick;
    private VisualElement _draftLog;

    // ── Draft pagination ──
    private Button _btnProspectPrev;
    private Button _btnProspectNext;
    private Label _prospectPageLabel;
    private Button _btnLogPrev;
    private Button _btnLogNext;
    private Label _logPageLabel;
    private int _prospectPage;
    private int _logPage;
    private List<DraftLogEntry> _logEntries = new();
    private const int PAGE_SIZE = 10;

    private class DraftLogEntry
    {
        public int PickNumber;
        public string TeamLogo;
        public string TeamName;
        public string Position;
        public string FirstName;
        public string LastName;
        public int Overall;
        public bool IsUserPick;
    }

    // ── State ──
    private Dictionary<string, Sprite> _logo32;
    private DraftState _draftState;
    private int _currentPickIndex;
    private List<DraftGenerator.DraftPickSlot> _draftOrder;
    private List<DraftProspect> _availableProspects;
    private DraftProspect _selectedProspect;
    private string _classLabel;
    private float _classQuality;
    private bool _draftRunning;

    protected override void OnEnable()
    {
        base.OnEnable();

        _lotteryOverlay = new VisualElement();
        _lotteryOverlay.AddToClassList("lottery-overlay");
        _root.Add(_lotteryOverlay);

        CursorManager.Instance?.SetDefaultCursor();

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnDraft);
            CursorManager.Instance.RegisterHandCursor(_btnNextSeason);
            CursorManager.Instance.RegisterHandCursor(_btnLottery);
            CursorManager.Instance.RegisterHandCursor(_btnDraftPick);
            CursorManager.Instance.RegisterHandCursor(_btnAutoPick);
            CursorManager.Instance.RegisterHandCursor(_btnProspectPrev);
            CursorManager.Instance.RegisterHandCursor(_btnProspectNext);
            CursorManager.Instance.RegisterHandCursor(_btnLogPrev);
            CursorManager.Instance.RegisterHandCursor(_btnLogNext);
            CursorManager.Instance.RegisterHandCursor(_btnRetiringPrev);
            CursorManager.Instance.RegisterHandCursor(_btnRetiringNext);
            CursorManager.Instance.RegisterHandCursor(_btnFamePrev);
            CursorManager.Instance.RegisterHandCursor(_btnFameNext);
        }
    }

    protected override void CacheReferences()
    {
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _seasonTag = _root.Q<Label>("SeasonTag");
        _draftYearLabel = _root.Q<Label>("DraftYearLabel");
        _draftYearTitle = _root.Q<Label>("DraftYearTitle");
        _retiringList = _root.Q<VisualElement>("RetiringList");
        _fameList = _root.Q<VisualElement>("FameList");
        _btnDraft = _root.Q<Button>("BtnDraft");
        _btnNextSeason = _root.Q<Button>("BtnNextSeason");
        _btnLottery = _root.Q<Button>("BtnLottery");
        _famePanel = _root.Q<VisualElement>("FamePanel");
        _preDraftHeader = _root.Q<VisualElement>("PreDraftHeader");
        _preDraftContent = _root.Q<VisualElement>("PreDraftContent");

        _btnRetiringPrev = _root.Q<Button>("BtnRetiringPrev");
        _btnRetiringNext = _root.Q<Button>("BtnRetiringNext");
        _btnFamePrev = _root.Q<Button>("BtnFamePrev");
        _btnFameNext = _root.Q<Button>("BtnFameNext");

        _draftRoom = _root.Q<VisualElement>("DraftRoom");
        _draftProgressText = _root.Q<Label>("DraftProgressText");
        _draftProgressFill = _root.Q<VisualElement>("DraftProgressFill");
        _draftClassLabel = _root.Q<Label>("DraftClassLabel");
        _prospectCount = _root.Q<Label>("ProspectCount");
        _prospectList = _root.Q<VisualElement>("ProspectList");
        _draftPickRound = _root.Q<Label>("DraftPickRound");
        _draftPickNumber = _root.Q<Label>("DraftPickNumber");
        _draftPickTeamLogo = _root.Q<VisualElement>("DraftPickTeamLogo");
        _draftPickTeamName = _root.Q<Label>("DraftPickTeamName");
        _draftPlayerCard = _root.Q<VisualElement>("DraftPlayerCard");
        _draftPlayerName = _root.Q<Label>("DraftPlayerName");
        _draftPlayerPosition = _root.Q<Label>("DraftPlayerPosition");
        _draftPlayerSecPos = _root.Q<Label>("DraftPlayerSecPos");
        _draftPlayerAge = _root.Q<Label>("DraftPlayerAge");
        _draftPlayerHeight = _root.Q<Label>("DraftPlayerHeight");
        _draftPlayerWeight = _root.Q<Label>("DraftPlayerWeight");
        _draftPlayerNationality = _root.Q<Label>("DraftPlayerNationality");
        _draftPlayerCollege = _root.Q<Label>("DraftPlayerCollege");
        _draftPlayerTrait = _root.Q<Label>("DraftPlayerTrait");
        _draftAttrMed = _root.Q<Label>("DraftAttrMed");
        _draftAttrSpeed = _root.Q<Label>("DraftAttrSpeed");
        _draftAttrShooting = _root.Q<Label>("DraftAttrShooting");
        _draftAttr3pt = _root.Q<Label>("DraftAttr3pt");
        _draftAttrPassing = _root.Q<Label>("DraftAttrPassing");
        _draftAttrDribbling = _root.Q<Label>("DraftAttrDribbling");
        _draftAttrDefense = _root.Q<Label>("DraftAttrDefense");
        _draftAttrRebounding = _root.Q<Label>("DraftAttrRebounding");
        _draftAttrAthleticism = _root.Q<Label>("DraftAttrAthleticism");
        _draftAttrIQ = _root.Q<Label>("DraftAttrIQ");
        _draftAttrSteals = _root.Q<Label>("DraftAttrSteals");
        _draftAttrBlocks = _root.Q<Label>("DraftAttrBlocks");
        _btnDraftPick = _root.Q<Button>("BtnDraftPick");
        _btnAutoPick = _root.Q<Button>("BtnAutoPick");
        _draftLog = _root.Q<VisualElement>("DraftLog");

        _btnProspectPrev = _root.Q<Button>("BtnProspectPrev");
        _btnProspectNext = _root.Q<Button>("BtnProspectNext");
        _prospectPageLabel = _root.Q<Label>("ProspectPageLabel");
        _btnLogPrev = _root.Q<Button>("BtnLogPrev");
        _btnLogNext = _root.Q<Button>("BtnLogNext");
        _logPageLabel = _root.Q<Label>("LogPageLabel");
    }

    protected override void LoadData()
    {
        base.LoadData();
        if (_season == null) return;

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        _logo32 = new Dictionary<string, Sprite>();
        foreach (var s in logos) _logo32[s.name] = s;

        ProcessAITeamRenewals();
        _btnNextSeason.SetEnabled(false);
    }

    protected override void RegisterCallbacks()
    {
        _btnDraft?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            StartDraft();
        });

        _btnNextSeason?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.NewSeason);
        });

        _btnLottery?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ShowLotteryModal();
        });

        _btnDraftPick?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            if (_selectedProspect != null && _draftState == DraftState.UserPicking)
                ExecutePick(_selectedProspect);
        });

        _btnAutoPick?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            if (_draftState == DraftState.UserPicking)
                AutoPick();
        });

        _btnProspectPrev?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangeProspectPage(-1); });
        _btnProspectNext?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangeProspectPage(1); });
        _btnLogPrev?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangeLogPage(-1); });
        _btnLogNext?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangeLogPage(1); });
        _btnRetiringPrev?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangeRetiringPage(-1); });
        _btnRetiringNext?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangeRetiringPage(1); });
        _btnFamePrev?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangeFamePage(-1); });
        _btnFameNext?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangeFamePage(1); });
    }

    protected override void Refresh()
    {
        if (_season == null) return;
        RefreshHeader();
        RefreshContent();
    }

    protected override void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        var logoDict = new Dictionary<string, Sprite>();
        foreach (var s in logos64) logoDict[s.name] = s;
        if (logoDict.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
        if (!string.IsNullOrEmpty(_season.current_date))
        {
            if (DateTime.TryParse(_season.current_date, out var dt))
                _headerDate.text = dt.ToString("dd/MM/yyyy");
        }
    }

    void RefreshContent()
    {
        string seasonLabel = $"{_season.year_start}-{_season.year_end.ToString().Substring(2)}";
        _seasonTag.text = seasonLabel;
        _draftYearTitle.text = "DRAFT";
        _draftYearLabel.text = _season.year_end.ToString();
        LoadRetiringPlayers();
        LoadFamePlayers();
        _btnDraft.text = $"EMPEZAR DRAFT {_season.year_end}";
    }

    // ════════════════════════════════════════════════════════════════
    //  DRAFT FLOW
    // ════════════════════════════════════════════════════════════════

    void StartDraft()
    {
        _btnDraft.SetEnabled(false);
        _btnDraft.text = "GENERANDO...";

        var (order, classLabel, classQuality) = DraftGenerator.GenerateDraftOrder(_season, _manager.id, _myTeam.id);
        _draftOrder = order;
        _classLabel = classLabel;
        _classQuality = classQuality;
        _availableProspects = DraftGenerator.GenerateProspectPool(classLabel, classQuality, 75);

        _draftState = DraftState.Idle;
        _currentPickIndex = 0;
        _selectedProspect = null;
        _draftRunning = true;
        _prospectPage = 0;
        _logPage = 0;
        _logEntries.Clear();

        // Switch UI to draft room
        _preDraftHeader.style.display = DisplayStyle.None;
        _preDraftContent.style.display = DisplayStyle.None;
        _famePanel.style.display = DisplayStyle.None;
        _draftRoom.style.display = DisplayStyle.Flex;
        _draftLog.Clear();

        string classText = classLabel switch
        {
            "historic" => "Clase: HISTORICA",
            "strong" => "Clase: Fuerte",
            "weak" => "Clase: Debil",
            _ => "Clase: Normal"
        };
        _draftClassLabel.text = classText;

        StartCoroutine(DraftCoroutine());
    }

    IEnumerator DraftCoroutine()
    {
        while (_currentPickIndex < _draftOrder.Count && _draftRunning)
        {
            var slot = _draftOrder[_currentPickIndex];
            _selectedProspect = null;

            UpdatePickUI(slot);
            UpdateProspectList();

            if (slot.IsUserPick)
            {
                _draftState = DraftState.UserPicking;
                _btnDraftPick.style.display = DisplayStyle.Flex;
                _btnAutoPick.style.display = DisplayStyle.Flex;
                _btnDraftPick.SetEnabled(false);
                _draftPlayerCard.style.display = DisplayStyle.None;

                yield return null;
                while (_draftState == DraftState.UserPicking)
                    yield return null;
            }
            else
            {
                _draftState = DraftState.AIPicking;
                _btnDraftPick.style.display = DisplayStyle.None;
                _btnAutoPick.style.display = DisplayStyle.None;
                _draftPlayerCard.style.display = DisplayStyle.None;

                yield return new WaitForSeconds(0.4f);

                int aiIdx = DraftGenerator.AISelectProspect(_availableProspects, slot);
                var prospect = _availableProspects[aiIdx];
                _availableProspects.RemoveAt(aiIdx);
                var player = DraftGenerator.AssignPick(slot, prospect, null);

                AddLogEntry(slot, prospect, player);
                _currentPickIndex++;
            }
        }

        _draftState = DraftState.Complete;
        _draftProgressFill.style.width = Length.Percent(100);
        _draftProgressText.text = $"Pick {_draftOrder.Count} / {_draftOrder.Count}";
        _btnDraftPick.style.display = DisplayStyle.None;
        _btnAutoPick.style.display = DisplayStyle.None;
        _prospectCount.text = "0";
        _draftPlayerCard.style.display = DisplayStyle.None;
        _draftPickTeamName.text = "DRAFT COMPLETADO";
        _btnNextSeason.text = "FINALIZAR DRAFT";
        _btnNextSeason.SetEnabled(true);

        yield return null;
    }

    void UpdatePickUI(DraftGenerator.DraftPickSlot slot)
    {
        bool isRound2 = slot.IsRound2;
        _draftPickRound.text = isRound2 ? "R2" : "R1";
        _draftPickNumber.text = $"#{slot.PickNumber}";

        float pct = (float)_currentPickIndex / _draftOrder.Count;
        _draftProgressFill.style.width = Length.Percent(pct * 100);
        _draftProgressText.text = $"Pick {_currentPickIndex + 1} / {_draftOrder.Count}";

        if (_logo32.TryGetValue(slot.Team.logo, out var logo))
            _draftPickTeamLogo.style.backgroundImage = new StyleBackground(logo);
        _draftPickTeamName.text = slot.Team.name.ToUpper();
    }

    void UpdateProspectList()
    {
        _prospectCount.text = _availableProspects.Count.ToString();

        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)_availableProspects.Count / PAGE_SIZE));
        _prospectPage = Mathf.Clamp(_prospectPage, 0, pageCount - 1);

        var pageItems = _availableProspects
            .Skip(_prospectPage * PAGE_SIZE)
            .Take(PAGE_SIZE)
            .ToList();

        _prospectList.Clear();
        int baseIndex = _prospectPage * PAGE_SIZE;

        for (int i = 0; i < pageItems.Count; i++)
        {
            var p = pageItems[i];
            int globalIndex = baseIndex + i;
            var row = new VisualElement();
            row.AddToClassList("draft-prospect");
            if (globalIndex < 3) row.AddToClassList("draft-prospect--top");

            var pos = new Label();
            pos.AddToClassList("draft-prospect-pos");
            pos.text = PositionCodes.GetShort(p.position);
            row.Add(pos);

            var nameLbl = new Label();
            nameLbl.AddToClassList("draft-prospect-name");
            nameLbl.text = $"{p.first_name} {p.last_name}";
            row.Add(nameLbl);

            var info = new Label();
            info.AddToClassList("draft-prospect-info");
            info.text = $"{p.age}a · {p.height_cm}cm";
            row.Add(info);

            var med = new Label();
            med.AddToClassList("draft-prospect-med");
            med.text = p.overall.ToString();
            if (p.overall > 84) med.AddToClassList("draft-prospect-med--high");
            else if (p.overall >= 70) med.AddToClassList("draft-prospect-med--mid");
            else med.AddToClassList("draft-prospect-med--low");
            row.Add(med);

            int capturedIndex = globalIndex;
            row.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                SelectProspect(capturedIndex);
            });

            if (_draftState == DraftState.UserPicking)
                CursorManager.Instance?.RegisterHandCursor(row);

            _prospectList.Add(row);
        }

        _prospectPageLabel.text = $"{_prospectPage + 1} de {pageCount}";
        _btnProspectPrev.SetEnabled(_prospectPage > 0);
        _btnProspectNext.SetEnabled(_prospectPage < pageCount - 1);
    }

    void ChangeProspectPage(int delta)
    {
        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)_availableProspects.Count / PAGE_SIZE));
        _prospectPage = Mathf.Clamp(_prospectPage + delta, 0, pageCount - 1);
        _selectedProspect = null;
        _btnDraftPick.SetEnabled(false);
        _draftPlayerCard.style.display = DisplayStyle.None;
        UpdateProspectList();
    }

    void ChangeLogPage(int delta)
    {
        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)_logEntries.Count / PAGE_SIZE));
        _logPage = Mathf.Clamp(_logPage + delta, 0, pageCount - 1);
        RenderLogPage();
    }

    void RenderLogPage()
    {
        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)_logEntries.Count / PAGE_SIZE));
        _logPage = Mathf.Clamp(_logPage, 0, pageCount - 1);

        var pageItems = _logEntries
            .Skip(_logPage * PAGE_SIZE)
            .Take(PAGE_SIZE)
            .ToList();

        _draftLog.Clear();
        foreach (var entry in pageItems)
            _draftLog.Add(BuildLogEntryRow(entry));

        _logPageLabel.text = $"{_logPage + 1} de {pageCount}";
        _btnLogPrev.SetEnabled(_logPage > 0);
        _btnLogNext.SetEnabled(_logPage < pageCount - 1);
    }

    void SelectProspect(int index)
    {
        if (index < 0 || index >= _availableProspects.Count) return;
        _selectedProspect = _availableProspects[index];
        _btnDraftPick.SetEnabled(true);

        _draftPlayerCard.style.display = DisplayStyle.Flex;
        var p = _selectedProspect;
        _draftPlayerName.text = $"{p.first_name} {p.last_name}";
        _draftPlayerPosition.text = PositionCodes.GetShort(p.position);
        if (!string.IsNullOrEmpty(p.secondary_position))
        {
            _draftPlayerSecPos.text = PositionCodes.GetShort(p.secondary_position);
            _draftPlayerSecPos.style.display = DisplayStyle.Flex;
        }
        else
            _draftPlayerSecPos.style.display = DisplayStyle.None;

        _draftPlayerAge.text = $"{p.age} años";
        _draftPlayerHeight.text = $"{p.height_cm}cm";
        _draftPlayerWeight.text = $"{p.weight_kg}kg";
        _draftPlayerNationality.text = CountryCodes.GetName(p.nationality);
        _draftPlayerCollege.text = p.college;

        if (!string.IsNullOrEmpty(p.trait))
        {
            _draftPlayerTrait.style.display = DisplayStyle.Flex;
            _draftPlayerTrait.text = $"Trait: {p.trait.Replace("_", " ").ToUpper()}";
        }
        else
            _draftPlayerTrait.style.display = DisplayStyle.None;

        _draftAttrMed.text = p.overall.ToString();
        _draftAttrSpeed.text = p.speed.ToString();
        _draftAttrShooting.text = p.shooting.ToString();
        _draftAttr3pt.text = p.three_point.ToString();
        _draftAttrPassing.text = p.passing.ToString();
        _draftAttrDribbling.text = p.dribbling.ToString();
        _draftAttrDefense.text = p.defense.ToString();
        _draftAttrRebounding.text = p.rebounding.ToString();
        _draftAttrAthleticism.text = p.athleticism.ToString();
        _draftAttrIQ.text = p.iq.ToString();
        _draftAttrSteals.text = p.steals.ToString();
        _draftAttrBlocks.text = p.blocks.ToString();

        // Highlight in list (global index → local page index)
        int localIndex = index - _prospectPage * PAGE_SIZE;
        for (int i = 0; i < _prospectList.childCount; i++)
            _prospectList[i].RemoveFromClassList("draft-prospect--selected");
        if (localIndex >= 0 && localIndex < _prospectList.childCount)
            _prospectList[localIndex].AddToClassList("draft-prospect--selected");
    }

    void ExecutePick(DraftProspect prospect)
    {
        var slot = _draftOrder[_currentPickIndex];
        _availableProspects.Remove(prospect);

        var player = DraftGenerator.AssignPick(slot, prospect, null);
        AddLogEntry(slot, prospect, player);

        _currentPickIndex++;
        _draftState = DraftState.Idle;
    }

    void AutoPick()
    {
        var slot = _draftOrder[_currentPickIndex];
        int idx = DraftGenerator.AISelectProspect(_availableProspects, slot);
        var prospect = _availableProspects[idx];
        _availableProspects.RemoveAt(idx);

        var player = DraftGenerator.AssignPick(slot, prospect, null);
        AddLogEntry(slot, prospect, player);

        _currentPickIndex++;
        _draftState = DraftState.Idle;
    }

    void AddLogEntry(DraftGenerator.DraftPickSlot slot, DraftProspect prospect, PlayerData player)
    {
        _logEntries.Add(new DraftLogEntry
        {
            PickNumber = slot.PickNumber,
            TeamLogo = slot.Team.logo,
            TeamName = slot.Team.name,
            Position = prospect.position,
            FirstName = prospect.first_name,
            LastName = prospect.last_name,
            Overall = prospect.overall,
            IsUserPick = slot.IsUserPick
        });

        // Auto-advance to last page so new entries are visible
        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)_logEntries.Count / PAGE_SIZE));
        _logPage = pageCount - 1;
        RenderLogPage();
    }

    VisualElement BuildLogEntryRow(DraftLogEntry entry)
    {
        var row = new VisualElement();
        row.AddToClassList("draft-log-entry");
        if (entry.IsUserPick) row.AddToClassList("draft-log-entry--user");

        var num = new Label();
        num.AddToClassList("draft-log-num");
        num.text = $"#{entry.PickNumber}";
        row.Add(num);

        var logo = new VisualElement();
        logo.AddToClassList("draft-log-logo");
        if (_logo32.TryGetValue(entry.TeamLogo, out var sprite))
            logo.style.backgroundImage = new StyleBackground(sprite);
        row.Add(logo);

        var info = new VisualElement();
        info.AddToClassList("draft-log-info");

        var teamLbl = new Label();
        teamLbl.AddToClassList("draft-log-team");
        teamLbl.text = $"{entry.TeamName.ToUpper()} — {PositionCodes.GetShort(entry.Position)}";
        info.Add(teamLbl);

        var playerLbl = new Label();
        playerLbl.AddToClassList("draft-log-player");
        playerLbl.text = $"{entry.FirstName} {entry.LastName}  ·  MED: {entry.Overall}";
        info.Add(playerLbl);

        row.Add(info);
        return row;
    }

    // ════════════════════════════════════════════════════════════════
    //  RETIRING / FAME / PAGINATION
    // ════════════════════════════════════════════════════════════════

    void LoadRetiringPlayers()
    {
        _retiringPlayers = DatabaseManager.Instance.GetRetiringPlayers();
        _retiringPage = 0;
        RenderRetiringPage();
    }

    void LoadFamePlayers()
    {
        _famePlayers = DatabaseManager.Instance.GetRetiringHallOfFameMembers();
        _famePage = 0;
        RenderFamePage();
    }

    void ChangeRetiringPage(int delta)
    {
        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)_retiringPlayers.Count / PRE_DRAFT_PAGE_SIZE));
        _retiringPage = Mathf.Clamp(_retiringPage + delta, 0, pageCount - 1);
        RenderRetiringPage();
    }

    void ChangeFamePage(int delta)
    {
        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)_famePlayers.Count / PRE_DRAFT_PAGE_SIZE));
        _famePage = Mathf.Clamp(_famePage + delta, 0, pageCount - 1);
        RenderFamePage();
    }

    void RenderRetiringPage()
    {
        _retiringList.Clear();

        if (_retiringPlayers.Count == 0)
        {
            var empty = new Label();
            empty.AddToClassList("es-empty");
            empty.text = "No hay jugadores que se retiren esta temporada";
            _retiringList.Add(empty);
            _btnRetiringPrev.SetEnabled(false);
            _btnRetiringNext.SetEnabled(false);
            return;
        }

        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)_retiringPlayers.Count / PRE_DRAFT_PAGE_SIZE));
        _retiringPage = Mathf.Clamp(_retiringPage, 0, pageCount - 1);

        var pageItems = _retiringPlayers
            .Skip(_retiringPage * PRE_DRAFT_PAGE_SIZE)
            .Take(PRE_DRAFT_PAGE_SIZE)
            .ToList();

        foreach (var p in pageItems)
            _retiringList.Add(BuildPlayerRow(p));

        _btnRetiringPrev.SetEnabled(_retiringPage > 0);
        _btnRetiringNext.SetEnabled(_retiringPage < pageCount - 1);
    }

    void RenderFamePage()
    {
        _fameList.Clear();

        if (_famePlayers.Count == 0)
        {
            var empty = new Label();
            empty.AddToClassList("es-empty");
            empty.text = "Ningun jugador entra al Salon de la Fama esta temporada";
            _fameList.Add(empty);
            _btnFamePrev.SetEnabled(false);
            _btnFameNext.SetEnabled(false);
            return;
        }

        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)_famePlayers.Count / PRE_DRAFT_PAGE_SIZE));
        _famePage = Mathf.Clamp(_famePage, 0, pageCount - 1);

        var pageItems = _famePlayers
            .Skip(_famePage * PRE_DRAFT_PAGE_SIZE)
            .Take(PRE_DRAFT_PAGE_SIZE)
            .ToList();

        foreach (var p in pageItems)
            _fameList.Add(BuildFameRow(p));

        _btnFamePrev.SetEnabled(_famePage > 0);
        _btnFameNext.SetEnabled(_famePage < pageCount - 1);
    }

    VisualElement BuildPlayerRow(PlayerData p)
    {
        var card = new VisualElement();
        card.AddToClassList("es-player-row");

        // Player photo
        var photo = new VisualElement();
        photo.AddToClassList("es-player-photo");
        var photoTex = PlayerPhotoHelper.Load(p.id, p.photo);
        if (photoTex != null)
            photo.style.backgroundImage = new StyleBackground(photoTex);
        card.Add(photo);

        // Name
        var nameLbl = new Label();
        nameLbl.AddToClassList("es-player-name");
        nameLbl.text = $"{p.first_name} {p.last_name}";
        card.Add(nameLbl);

        // Age
        var ageLbl = new Label();
        ageLbl.AddToClassList("es-player-age");
        ageLbl.text = $"{p.age} años";
        card.Add(ageLbl);

        // Bottom row: position badge + team logo + team abbreviation
        var bottomRow = new VisualElement();
        bottomRow.AddToClassList("es-player-bottom-row");

        var posLbl = new Label();
        posLbl.AddToClassList("es-player-pos");
        posLbl.text = PositionCodes.GetShort(p.position);
        bottomRow.Add(posLbl);

        if (p.team_id != 0)
        {
            var team = DatabaseManager.Instance.GetTeamById(p.team_id);
            if (team != null)
            {
                var logo = new VisualElement();
                logo.AddToClassList("es-player-logo");
                if (_logo32.TryGetValue(team.logo, out var sprite))
                    logo.style.backgroundImage = new StyleBackground(sprite);
                bottomRow.Add(logo);

                var teamAbbr = new Label();
                teamAbbr.AddToClassList("es-player-team-abbr");
                teamAbbr.text = team.abbreviation;
                bottomRow.Add(teamAbbr);
            }
        }

        card.Add(bottomRow);
        return card;
    }

    VisualElement BuildFameRow(PlayerData p)
    {
        var card = new VisualElement();
        card.AddToClassList("es-player-row");
        card.AddToClassList("es-player-fame-row");

        // Player photo
        var photo = new VisualElement();
        photo.AddToClassList("es-player-photo");
        var photoTex = PlayerPhotoHelper.Load(p.id, p.photo);
        if (photoTex != null)
            photo.style.backgroundImage = new StyleBackground(photoTex);
        card.Add(photo);

        // Name
        var nameLbl = new Label();
        nameLbl.AddToClassList("es-player-name");
        nameLbl.text = $"{p.first_name} {p.last_name}";
        card.Add(nameLbl);

        // "Clase del XXXX"
        var classLbl = new Label();
        classLbl.AddToClassList("es-player-age");
        classLbl.text = $"Clase del {_season.year_end}";
        card.Add(classLbl);

        // Team logo + team abbreviation (stacked vertically)
        var teamCol = new VisualElement();
        teamCol.AddToClassList("es-player-team-col");

        if (p.team_id != 0)
        {
            var team = DatabaseManager.Instance.GetTeamById(p.team_id);
            if (team != null)
            {
                var logo = new VisualElement();
                logo.AddToClassList("es-player-logo");
                if (_logo32.TryGetValue(team.logo, out var sprite))
                    logo.style.backgroundImage = new StyleBackground(sprite);
                teamCol.Add(logo);

                var teamAbbr = new Label();
                teamAbbr.AddToClassList("es-player-team-abbr");
                teamAbbr.text = team.abbreviation;
                teamCol.Add(teamAbbr);
            }
        }

        card.Add(teamCol);

        return card;
    }

    void ProcessAITeamRenewals()
    {
        var allTeams = DatabaseManager.Instance.GetAllTeams();
        foreach (var team in allTeams)
        {
            if (team.id == _myTeam.id) continue;
            var roster = DatabaseManager.Instance.GetPlayersByTeam(team.id);
            var candidates = roster
                .Where(p => p.contract_years == 1 && p.age < 40 && p.GetCalculatedAverage() >= 80)
                .ToList();
            foreach (var player in candidates)
            {
                int years = CalcRenewYears(player.age);
                long newSalary = CalcRenewSalary(player.salary, player.age);
                player.contract_years = years;
                player.guaranteed_years = years;
                player.salary = newSalary;
                DatabaseManager.Instance.UpdatePlayer(player);
            }
        }
    }

    int CalcRenewYears(int age)
    {
        if (age <= 25) return 5;
        if (age <= 28) return 4;
        if (age <= 32) return 3;
        if (age < 40) return 2;
        return 1;
    }

    long CalcRenewSalary(long currentSalary, int age)
    {
        double multiplier;
        if (age <= 25) multiplier = 1.20;
        else if (age <= 30) multiplier = 1.10;
        else multiplier = 1.05;
        long newSalary = (long)(currentSalary * multiplier);
        newSalary = (long)(Math.Round(newSalary / 100000.0) * 100000);
        if (newSalary < currentSalary) newSalary = currentSalary;
        return newSalary;
    }

    // ════════════════════════════════════════════════════════════════
    //  LOTTERY MODALS
    // ════════════════════════════════════════════════════════════════

    void ShowLotteryModal()
    {
        _lotteryOverlay.Clear();
        _lotteryOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("lottery-box");
        _lotteryOverlay.Add(box);

        var title = new Label("LOTERIA DEL DRAFT");
        title.AddToClassList("lottery-title");
        box.Add(title);

        var subtitle = new Label($"Probabilidades para el Draft {_season.year_end}");
        subtitle.AddToClassList("lottery-subtitle");
        box.Add(subtitle);

        var teams = BuildLotteryOrder();
        var grid = new VisualElement();
        grid.AddToClassList("lottery-grid");
        box.Add(grid);

        var col1 = new VisualElement();
        col1.AddToClassList("lottery-column");
        var col2 = new VisualElement();
        col2.AddToClassList("lottery-column");
        grid.Add(col1);
        grid.Add(col2);

        for (int i = 0; i < teams.Count; i++)
        {
            var target = i < 7 ? col1 : col2;
            target.Add(BuildLotteryRow(i + 1, teams[i].team, teams[i].pct));
        }

        var closeBtn = new Button();
        closeBtn.text = "CERRAR";
        closeBtn.AddToClassList("lottery-close-btn");
        closeBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _lotteryOverlay.style.display = DisplayStyle.None;
        });
        box.Add(closeBtn);
        CursorManager.Instance?.RegisterHandCursor(closeBtn);
    }

    VisualElement BuildLotteryRow(int rank, TeamData team, string pct)
    {
        var row = new VisualElement();
        row.AddToClassList("lottery-row");

        var rankLbl = new Label($"#{rank}");
        rankLbl.AddToClassList("lottery-rank");
        row.Add(rankLbl);

        var logo = new VisualElement();
        logo.AddToClassList("lottery-logo");
        if (_logo32.TryGetValue(team.logo, out var sprite))
            logo.style.backgroundImage = new StyleBackground(sprite);
        row.Add(logo);

        var nameLbl = new Label(team.name);
        nameLbl.AddToClassList("lottery-name");
        row.Add(nameLbl);

        var pctLbl = new Label(pct);
        pctLbl.AddToClassList("lottery-pct");
        row.Add(pctLbl);

        return row;
    }

    List<(TeamData team, string pct)> BuildLotteryOrder()
    {
        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);

        var standings = allTeams.Select(t =>
        {
            var teamGames = allGames.Where(g =>
                (g.home_team_id == t.id || g.away_team_id == t.id) && g.is_played == 1).ToList();
            int wins = teamGames.Count(g =>
                (g.home_team_id == t.id && g.home_score > g.away_score) ||
                (g.away_team_id == t.id && g.away_score > g.home_score));
            int total = teamGames.Count;
            float winPct = total > 0 ? (float)wins / total : 0f;
            return new { Team = t, Wins = wins, Total = total, WinPct = winPct };
        })
        .OrderBy(s => s.WinPct)
        .ThenBy(s => s.Wins)
        .Take(14)
        .ToList();

        double[] odds = {
            0.140, 0.140, 0.140, 0.125, 0.105,
            0.090, 0.075, 0.060, 0.045, 0.030,
            0.020, 0.015, 0.010, 0.005
        };

        var result = new List<(TeamData, string)>();
        for (int i = 0; i < standings.Count && i < odds.Length; i++)
            result.Add((standings[i].Team, $"{odds[i] * 100:F1}%"));
        return result;
    }
}
