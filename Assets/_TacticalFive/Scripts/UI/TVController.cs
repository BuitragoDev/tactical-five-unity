using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class TVController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private VisualElement _tvDealsBody;
    private VisualElement _tvScheduleBody;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TvChannelData> _tvChannels;

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
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _tvDealsBody = _root.Q<VisualElement>("TVDealsBody");
        _tvScheduleBody = _root.Q<VisualElement>("TVScheduleBody");
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _tvChannels = DatabaseManager.Instance.GetTVChannels();
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ => ScreenManager.Instance.GoTo(GameScreen.Dashboard));
        _root.Q<Button>("BtnReset")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.MainMenu));
    }

    void RegisterNavButtons()
    {
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Dashboard));
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Roster));
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Calendar));
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Standings));
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Palmares));
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Results));
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Playoffs));
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Stats));
        _root.Q<Button>("NavRecords")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Records));
        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Market));
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Finances));
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Sponsors));
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Arena));
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Messages));
    }

    void Refresh()
    {
        RefreshHeader();
        BuildTVDeals();
        BuildTVSchedule();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        var logoDict = new Dictionary<string, Sprite>();
        foreach (var s in logos) logoDict[s.name] = s;

        if (logoDict.TryGetValue(_myTeam.logo, out var sprite))
            _root.Q<VisualElement>("HeaderTeamLogo").style.backgroundImage = new StyleBackground(sprite);

        _root.Q<Label>("HeaderTeamName").text = _myTeam.name.ToUpper();
        _root.Q<Label>("HeaderManagerName").text = $"Manager: {_manager.name}";
        _root.Q<Label>("HeaderBudget").text = $"${_myTeam.budget / 1_000_000}M";

        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        long totalPayroll = players.Sum(p => p.salary);
        _root.Q<Label>("HeaderPayroll").text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? 155_000_000;
        long margin = salaryCap - totalPayroll;
        var marginLbl = _root.Q<Label>("HeaderMargin");
        marginLbl.text = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        marginLbl.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) marginLbl.AddToClassList("header-stat-value--negative");

        if (_season != null)
        {
            _root.Q<Label>("HeaderSeason").text = $"Temporada {_season.year_start}-{_season.year_end}";
            var nextGame = DatabaseManager.Instance.GetNextGame(_manager.id, _myTeam.id);
            _root.Q<Label>("HeaderDate").text = nextGame != null
                ? System.DateTime.Parse(nextGame.game_date).ToString("dd/MM/yyyy") : "";
        }

        _btnAction.text = "← DASHBOARD";
    }

    void BuildTVDeals()
    {
        _tvDealsBody.Clear();

        foreach (var channel in _tvChannels)
        {
            var item = new VisualElement();
            item.AddToClassList("tv-deal-item");

            var nameLbl = new Label();
            nameLbl.AddToClassList("tv-channel-name");
            nameLbl.text = channel.name.ToUpper();

            var typeLbl = new Label();
            typeLbl.AddToClassList("tv-deal-type");
            typeLbl.text = GetChannelTypeName(channel.channel_type);

            var valueLbl = new Label();
            valueLbl.AddToClassList("tv-deal-value");
            valueLbl.text = $"${channel.value:N0} / temporada";

            item.Add(nameLbl);
            item.Add(typeLbl);
            item.Add(valueLbl);

            _tvDealsBody.Add(item);
        }
    }

    void BuildTVSchedule()
    {
        _tvScheduleBody.Clear();

        var upcomingGames = DatabaseManager.Instance.GetUpcomingGames(_manager.id, _season.current_game_day);
        var tvGames = upcomingGames.Where(g => g.tv_channel_id > 0).Take(10).ToList();

        if (tvGames.Count == 0)
        {
            var noGames = new Label();
            noGames.AddToClassList("no-data-text");
            noGames.text = "NO HAY PARTIDOS PROGRAMADOS EN TV";
            _tvScheduleBody.Add(noGames);
            return;
        }

        foreach (var game in tvGames)
        {
            var item = new VisualElement();
            item.AddToClassList("tv-schedule-item");

            var dateLbl = new Label();
            dateLbl.AddToClassList("tv-schedule-date");
            dateLbl.text = System.DateTime.Parse(game.game_date).ToString("dd/MM");

            var homeTeam = DatabaseManager.Instance.GetTeamById(game.home_team_id);
            var awayTeam = DatabaseManager.Instance.GetTeamById(game.away_team_id);
            var matchupLbl = new Label();
            matchupLbl.AddToClassList("tv-schedule-matchup");
            matchupLbl.text = $"{awayTeam?.abbreviation.ToUpper()} @ {homeTeam?.abbreviation.ToUpper()}";

            var channel = _tvChannels.FirstOrDefault(c => c.id == game.tv_channel_id);
            var channelLbl = new Label();
            channelLbl.AddToClassList("tv-schedule-channel");
            channelLbl.text = channel?.name.ToUpper() ?? "TBD";

            item.Add(dateLbl);
            item.Add(matchupLbl);
            item.Add(channelLbl);

            _tvScheduleBody.Add(item);
        }
    }

    string GetChannelTypeName(int type)
    {
        return type switch
        {
            1 => "NACIONAL",
            2 => "REGIONAL",
            3 => "INTERNACIONAL",
            4 => "STREAMING",
            _ => "OTRO"
        };
    }
}
