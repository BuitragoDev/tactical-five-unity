using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class ResultsController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Results;
    private Button _btnPrevDate;
    private Button _btnNextDate;
    private Label _currentDateLabel;
    private VisualElement _resultsBody;
    private List<TeamData> _allTeams;
    private List<GameData> _allGames;
    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _logoSprites64 = new();
    private List<string> _gameDates = new();
    private int _currentDateIndex = 0;
    protected override void CacheReferences()
    {
        _btnPrevDate = _root.Q<Button>("BtnPrevDate");
        _btnNextDate = _root.Q<Button>("BtnNextDate");
        _currentDateLabel = _root.Q<Label>("CurrentDateLabel");
        _resultsBody = _root.Q<VisualElement>("ResultsBody");
    }
    protected override void LoadData()
    {
        base.LoadData();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        
        

        
        if (_myTeam == null) return;
        
        _allTeams = DatabaseManager.Instance.GetAllTeams();

        _allGames = DatabaseManager.Instance.GetAllGames(_manager.id);
        if (_allGames == null) _allGames = new List<GameData>();

        _gameDates = _allGames
            .Select(g => g.game_date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var myPlayedGames = _allGames
            .Where(g => (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id) && g.is_played == 1)
            .OrderByDescending(g => g.game_date)
            .ToList();

        if (myPlayedGames.Count > 0)
        {
            _currentDateIndex = _gameDates.IndexOf(myPlayedGames[0].game_date);
            if (_currentDateIndex < 0) _currentDateIndex = 0;
        }
        else if (_gameDates.Count > 0)
        {
            _currentDateIndex = 0;
        }
    }
    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _btnPrevDate?.RegisterCallback<ClickEvent>(_ => { PlayClick(); NavigateDate(-1); });
        _btnNextDate?.RegisterCallback<ClickEvent>(_ => { PlayClick(); NavigateDate(1); });
    }
    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Results] RefreshHeader error: {ex.Message}"); }
        UpdateDateLabel();
        ShowResults();
    }
    protected override void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;
        if (_root.Q<Label>("HeaderTeamName") == null) return;

        if (_logoSprites64.TryGetValue(_myTeam.logo, out var sprite))
            _root.Q<VisualElement>("HeaderTeamLogo").style.backgroundImage = new StyleBackground(sprite);

        _root.Q<Label>("HeaderTeamName").text = _myTeam.name.ToUpper();
        _root.Q<Label>("HeaderManagerName").text = $"Manager: {_manager.name}";
        var budgetLabel = _root.Q<Label>("HeaderBudget");
        budgetLabel.text = $"${_myTeam.budget / 1_000_000}M";
        budgetLabel.style.color = _myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));

        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        long totalPayroll = players.Sum(p => p.salary);
        _root.Q<Label>("HeaderPayroll").text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - totalPayroll;
        var marginLbl = _root.Q<Label>("HeaderMargin");
        string marginText = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        marginLbl.text = marginText;
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
        marginLbl.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) marginLbl.AddToClassList("header-stat-value--negative");

        if (_season != null)
        {
            _root.Q<Label>("HeaderSeason").text = $"Temporada {_season.year_start}-{_season.year_end}";
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "MENÚ PRINCIPAL";
    }

    void NavigateDate(int delta)
    {
        _currentDateIndex = Mathf.Clamp(_currentDateIndex + delta, 0, _gameDates.Count - 1);
        UpdateDateLabel();
        ShowResults();
    }

    void UpdateDateLabel()
    {
        if (_currentDateLabel == null) return;

        if (_gameDates.Count == 0)
        {
            _currentDateLabel.text = "SIN PARTIDOS";
            return;
        }

        try
        {
            var dt = System.DateTime.Parse(_gameDates[_currentDateIndex]);
            _currentDateLabel.text = $"{dt.Day} {GetMonthName(dt.Month).ToUpper()} {dt.Year}";
        }
        catch
        {
            _currentDateLabel.text = _gameDates[_currentDateIndex];
        }
    }

    void ShowResults()
    {
        _resultsBody.Clear();

        if (_gameDates.Count == 0)
        {
            var noResults = new VisualElement();
            noResults.AddToClassList("no-results");
            var noLbl = new Label();
            noLbl.AddToClassList("no-results-text");
            noLbl.text = "NO HAY PARTIDOS PROGRAMADOS";
            noResults.Add(noLbl);
            _resultsBody.Add(noResults);
            return;
        }

        var currentDate = _gameDates[_currentDateIndex];
        var dayGames = _allGames.Where(g => g.game_date == currentDate).ToList();

        if (dayGames.Count == 0)
        {
            var noResults = new VisualElement();
            noResults.AddToClassList("no-results");
            var noLbl = new Label();
            noLbl.AddToClassList("no-results-text");
            noLbl.text = "NO HAY PARTIDOS EN ESTA FECHA";
            noResults.Add(noLbl);
            _resultsBody.Add(noResults);
            return;
        }

        foreach (var g in dayGames)
        {
            var card = CreateGameCard(g);
            _resultsBody.Add(card);
        }
    }

    VisualElement CreateGameCard(GameData game)
    {
        var card = new VisualElement();
        card.style.flexDirection = FlexDirection.Row;
        card.style.alignItems = Align.Center;
        card.style.flexShrink = 0;
        card.style.backgroundColor = new Color(28f / 255f, 33f / 255f, 48f / 255f);
        card.style.borderTopLeftRadius = 8;
        card.style.borderTopRightRadius = 8;
        card.style.borderBottomLeftRadius = 8;
        card.style.borderBottomRightRadius = 8;
        card.style.borderTopWidth = 1;
        card.style.borderRightWidth = 1;
        card.style.borderBottomWidth = 1;
        card.style.borderLeftWidth = 1;
        card.style.borderTopColor = new Color(42f / 255f, 51f / 255f, 71f / 255f);
        card.style.paddingLeft = 24;
        card.style.paddingRight = 24;
        card.style.paddingTop = 6;
        card.style.paddingBottom = 6;
        card.style.marginBottom = 16;
        card.style.width = 1000;
        card.style.alignSelf = Align.Center;

        bool isMyGame = game.home_team_id == _myTeam.id || game.away_team_id == _myTeam.id;
        if (isMyGame)
        {
            card.style.borderTopColor = new Color(212f / 255f, 160f / 255f, 23f / 255f);
            card.style.borderRightColor = new Color(212f / 255f, 160f / 255f, 23f / 255f);
            card.style.borderBottomColor = new Color(212f / 255f, 160f / 255f, 23f / 255f);
            card.style.borderLeftColor = new Color(212f / 255f, 160f / 255f, 23f / 255f);
            card.style.backgroundColor = new Color(212f / 255f, 160f / 255f, 23f / 255f, 0.04f);
        }
        if (game.is_played != 1)
        {
            card.style.opacity = 0.7f;
        }

        var home = _allTeams.Find(t => t.id == game.home_team_id);
        var away = _allTeams.Find(t => t.id == game.away_team_id);

        // Home team side (left)
        var homeSide = new VisualElement();
        homeSide.style.flexDirection = FlexDirection.Row;
        homeSide.style.alignItems = Align.Center;
        homeSide.style.justifyContent = Justify.FlexEnd;
        homeSide.style.flexGrow = 1;
        homeSide.style.flexBasis = 0;
        homeSide.style.paddingRight = 10;

        var homeName = new Label();
        homeName.AddToClassList("results-team-name");
        homeName.text = home?.name ?? "???";
        if (isMyGame && game.home_team_id == _myTeam.id)
            homeName.style.color = new Color(212f / 255f, 160f / 255f, 23f / 255f);
        homeSide.Add(homeName);

        var homeLogo = new VisualElement();
        homeLogo.style.width = 32;
        homeLogo.style.height = 32;
        homeLogo.style.flexShrink = 0;
        homeLogo.style.marginLeft = 10;
        if (home != null && _logoSprites.TryGetValue(home.logo, out var hSprite))
            homeLogo.style.backgroundImage = new StyleBackground(hSprite);
        homeSide.Add(homeLogo);

        card.Add(homeSide);

        // Score box
        var scoreBox = new VisualElement();
        scoreBox.style.flexDirection = FlexDirection.Row;
        scoreBox.style.alignItems = Align.Center;
        scoreBox.style.justifyContent = Justify.Center;
        scoreBox.style.backgroundColor = new Color(0, 0, 0, 0.25f);
        scoreBox.style.borderTopLeftRadius = 4;
        scoreBox.style.borderTopRightRadius = 4;
        scoreBox.style.borderBottomLeftRadius = 4;
        scoreBox.style.borderBottomRightRadius = 4;
        scoreBox.style.paddingLeft = 16;
        scoreBox.style.paddingRight = 16;
        scoreBox.style.paddingTop = 6;
        scoreBox.style.paddingBottom = 6;
        scoreBox.style.minWidth = 100;
        scoreBox.style.marginLeft = 16;
        scoreBox.style.marginRight = 16;

        if (game.is_played == 1)
        {
            var hs = new Label();
            hs.AddToClassList("results-score-label");
            if (game.home_score > game.away_score)
                hs.AddToClassList("results-score-label--winner");
            hs.style.marginRight = 8;
            hs.text = game.home_score.ToString();
            scoreBox.Add(hs);

            var sep = new Label();
            sep.AddToClassList("results-score-sep");
            sep.style.marginRight = 8;
            sep.text = "-";
            scoreBox.Add(sep);

            var as2 = new Label();
            as2.AddToClassList("results-score-label");
            if (game.away_score > game.home_score)
                as2.AddToClassList("results-score-label--winner");
            as2.text = game.away_score.ToString();
            scoreBox.Add(as2);
        }
        else
        {
            var e1 = new Label();
            e1.AddToClassList("results-score-sep");
            e1.style.marginRight = 8;
            e1.text = "-";
            scoreBox.Add(e1);

            var sep = new Label();
            sep.AddToClassList("results-score-sep");
            sep.style.marginRight = 8;
            sep.text = "-";
            scoreBox.Add(sep);

            var e2 = new Label();
            e2.AddToClassList("results-score-sep");
            e2.text = "-";
            scoreBox.Add(e2);
        }

        card.Add(scoreBox);

        // Away team side (right)
        var awaySide = new VisualElement();
        awaySide.style.flexDirection = FlexDirection.Row;
        awaySide.style.alignItems = Align.Center;
        awaySide.style.justifyContent = Justify.FlexStart;
        awaySide.style.flexGrow = 1;
        awaySide.style.flexBasis = 0;
        awaySide.style.paddingLeft = 10;

        var awayLogo = new VisualElement();
        awayLogo.style.width = 32;
        awayLogo.style.height = 32;
        awayLogo.style.flexShrink = 0;
        awayLogo.style.marginRight = 10;
        if (away != null && _logoSprites.TryGetValue(away.logo, out var aSprite))
            awayLogo.style.backgroundImage = new StyleBackground(aSprite);
        awaySide.Add(awayLogo);

        var awayName = new Label();
        awayName.AddToClassList("results-team-name");
        awayName.text = away?.name ?? "???";
        if (isMyGame && game.away_team_id == _myTeam.id)
            awayName.style.color = new Color(212f / 255f, 160f / 255f, 23f / 255f);
        awaySide.Add(awayName);

        card.Add(awaySide);

        return card;
    }

    string GetMonthName(int month)
    {
        var names = new[] { "", "Ene", "Feb", "Mar", "Abr", "May", "Jun",
                           "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
        return names[month];
    }
}
