using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System;

public class SeasonSummaryController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerSeason;
    private Label _headerDate;

    // Summary
    private Label _seasonTag;
    private VisualElement _championLogo;
    private Label _championName;
    private Label _finalsResult;
    private Label _finalsLoser;
    private VisualElement _mvpPhoto;
    private Label _mvpName;
    private Label _mvpTeam;
    private Label _mvpPts;
    private Label _mvpReb;
    private Label _mvpAst;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams = new();
    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _logoSpritesLarge = new();

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        CacheReferences();
        LoadSprites();
        LoadData();

        var btnAwards = _root.Q<Button>("BtnGoToAwards");
        if (btnAwards != null)
            btnAwards.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.PlayerAwards); });
    }

    void CacheReferences()
    {
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _seasonTag = _root.Q<Label>("SeasonTag");
        _championLogo = _root.Q<VisualElement>("ChampionLogo");
        _championName = _root.Q<Label>("ChampionName");
        _finalsResult = _root.Q<Label>("FinalsResult");
        _finalsLoser = _root.Q<Label>("FinalsLoser");
        _mvpPhoto = _root.Q<VisualElement>("MvpPhoto");
        _mvpName = _root.Q<Label>("MvpName");
        _mvpTeam = _root.Q<Label>("MvpTeam");
        _mvpPts = _root.Q<Label>("MvpPts");
        _mvpReb = _root.Q<Label>("MvpReb");
        _mvpAst = _root.Q<Label>("MvpAst");
    }

    void LoadSprites()
    {
        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos64)
            _logoSprites[s.name] = s;

        var logos120 = Resources.LoadAll<Sprite>("Teams/Logos/120x120");
        foreach (var s in logos120)
            _logoSpritesLarge[s.name] = s;
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;
        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        if (_myTeam == null) return;
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        if (_season == null) return;
        _allTeams = DatabaseManager.Instance.GetAllTeams();

        RefreshHeader();
        RefreshSummary();
    }

    void RefreshHeader()
    {
        SetTeamLogo(_headerTeamLogo, _myTeam.logo);
        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
        if (!string.IsNullOrEmpty(_season.current_date))
        {
            if (DateTime.TryParse(_season.current_date, out var dt))
                _headerDate.text = dt.ToString("dd/MM/yyyy");
        }
    }

    void RefreshSummary()
    {
        string seasonLabel = $"{_season.year_start}-{_season.year_end.ToString().Substring(2)}";
        _seasonTag.text = seasonLabel;

        var finalsRecords = DatabaseManager.Instance.GetFinalsRecords();
        var currentFinals = finalsRecords.FirstOrDefault(f => f.season == seasonLabel);
        if (currentFinals == null)
        {
            Debug.LogWarning("[SeasonSummary] No finals record found for " + seasonLabel);
            return;
        }

        // Champion
        var champTeam = FindTeam(currentFinals.champ_keyword);
        if (champTeam != null)
        {
            _championName.text = champTeam.name.ToUpper();
            SetTeamLogoLarge(_championLogo, champTeam.logo);
        }
        else
        {
            _championName.text = currentFinals.champ_name.ToUpper();
        }

        _finalsResult.text = currentFinals.result;

        var finalistTeam = FindTeam(currentFinals.finalist_keyword);
        string finalistName = currentFinals.finalist_name ?? "";
        string finalistDisplay = finalistTeam != null ? finalistTeam.name.ToUpper() : finalistName.ToUpper();
        _finalsLoser.text = $"vs {finalistDisplay}";

        // Finals MVP
        if (!string.IsNullOrEmpty(currentFinals.mvp))
        {
            _mvpName.text = currentFinals.mvp;

            var mvpDetails = DatabaseManager.Instance.GetFinalsMVPDetails(_season.id, _manager.id);
            if (mvpDetails != null)
            {
                Texture2D tex = PlayerPhotoHelper.Load(mvpDetails.PlayerId, mvpDetails.Photo);
                _mvpPhoto.style.backgroundImage = new StyleBackground(tex);
                _mvpTeam.text = mvpDetails.TeamName;
                _mvpPts.text = mvpDetails.AvgPts.ToString("F1");
                _mvpReb.text = mvpDetails.AvgReb.ToString("F1");
                _mvpAst.text = mvpDetails.AvgAst.ToString("F1");
            }
        }
    }

    TeamData FindTeam(string keyword)
    {
        if (string.IsNullOrEmpty(keyword)) return null;
        return _allTeams?.Find(t =>
            t.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(t.logo, keyword, System.StringComparison.OrdinalIgnoreCase));
    }

    void SetTeamLogo(VisualElement elem, string logoName)
    {
        if (elem == null || string.IsNullOrEmpty(logoName)) return;
        if (_logoSprites.TryGetValue(logoName, out var sprite))
            elem.style.backgroundImage = new StyleBackground(sprite);
    }

    void SetTeamLogoLarge(VisualElement elem, string logoName)
    {
        if (elem == null || string.IsNullOrEmpty(logoName)) return;
        if (_logoSpritesLarge.TryGetValue(logoName, out var sprite))
            elem.style.backgroundImage = new StyleBackground(sprite);
        else if (_logoSprites.TryGetValue(logoName, out var fallback))
            elem.style.backgroundImage = new StyleBackground(fallback);
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
