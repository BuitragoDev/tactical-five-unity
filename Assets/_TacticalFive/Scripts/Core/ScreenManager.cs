using UnityEngine;
using UnityEngine.UIElements;

public class ScreenManager : MonoBehaviour
{
    public static ScreenManager Instance { get; private set; }

    [SerializeField] private UIDocument mainMenuDocument;
    [SerializeField] private UIDocument selectTeamDocument;
    [SerializeField] private UIDocument legalNoticeDocument;
    [SerializeField] private UIDocument preseasonDocument;
    [SerializeField] private UIDocument dashboardDocument;
    [SerializeField] private UIDocument rosterDocument;
    [SerializeField] private UIDocument vestuarioDocument;
    [SerializeField] private UIDocument quintetoDocument;
    [SerializeField] private UIDocument trainingDocument;
    [SerializeField] private UIDocument calendarDocument;
    [SerializeField] private UIDocument standingsDocument;
    [SerializeField] private UIDocument palmaresDocument;
    [SerializeField] private UIDocument resultsDocument;
    [SerializeField] private UIDocument playoffsDocument;
    [SerializeField] private UIDocument statsDocument;
    [SerializeField] private UIDocument recordsDocument;
    [SerializeField] private UIDocument marketDocument;
    [SerializeField] private UIDocument financesDocument;
    [SerializeField] private UIDocument loansDocument;
    [SerializeField] private UIDocument sponsorsDocument;
    [SerializeField] private UIDocument tvDocument;
    [SerializeField] private UIDocument arenaDocument;
    [SerializeField] private UIDocument messagesDocument;
    [SerializeField] private UIDocument matchDayDocument;
    [SerializeField] private UIDocument gameResultsDocument;
    [SerializeField] private UIDocument loadGameDocument;
    [SerializeField] private UIDocument settingsDocument;
    [SerializeField] private UIDocument employeesDocument;
    [SerializeField] private UIDocument injuredDocument;
    [SerializeField] private UIDocument carteraDocument;
    [SerializeField] private UIDocument seasonSummaryDocument;
    [SerializeField] private UIDocument playerAwardsDocument;
    [SerializeField] private UIDocument endSeasonDocument;
    [SerializeField] private UIDocument newSeasonDocument;
    [SerializeField] private UIDocument editorDocument;
    [SerializeField] private UIDocument historialDocument;

    public GameMode CurrentMode { get; private set; } = GameMode.None;
    public GameScreen CurrentScreen { get; private set; } = GameScreen.MainMenu;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        ShowOnly(mainMenuDocument);
    }

    public void GoTo(GameScreen screen, GameMode mode = GameMode.None)
    {
        if (mode != GameMode.None)
            CurrentMode = mode;
        CurrentScreen = screen;
        switch (screen)
        {
            case GameScreen.MainMenu:
                ShowOnly(mainMenuDocument);
                break;
            case GameScreen.SelectTeam:
                ShowOnly(selectTeamDocument);
                break;
            case GameScreen.LegalNotice:
                ShowOnly(legalNoticeDocument);
                break;
            case GameScreen.Preseason:
                ShowOnly(preseasonDocument);
                break;
            case GameScreen.Dashboard:
                ShowOnly(dashboardDocument);
                break;
            case GameScreen.Roster:
                ShowOnly(rosterDocument);
                break;
            case GameScreen.Calendar:
                ShowOnly(calendarDocument);
                break;
            case GameScreen.Standings:
                ShowOnly(standingsDocument);
                break;
            case GameScreen.Palmares:
                ShowOnly(palmaresDocument);
                break;
            case GameScreen.Results:
                ShowOnly(resultsDocument);
                break;
            case GameScreen.Playoffs:
                ShowOnly(playoffsDocument);
                break;
            case GameScreen.Stats:
                ShowOnly(statsDocument);
                break;
            case GameScreen.Records:
                ShowOnly(recordsDocument);
                break;
            case GameScreen.Market:
                ShowOnly(marketDocument);
                break;
            case GameScreen.Finances:
                ShowOnly(financesDocument);
                break;
            case GameScreen.Loans:
                ShowOnly(loansDocument);
                break;
            case GameScreen.Sponsors:
                ShowOnly(sponsorsDocument);
                break;
            case GameScreen.TV:
                ShowOnly(tvDocument);
                break;
            case GameScreen.Arena:
                ShowOnly(arenaDocument);
                break;
            case GameScreen.Messages:
                ShowOnly(messagesDocument);
                break;
            case GameScreen.MatchDay:
                ShowOnly(matchDayDocument);
                break;
            case GameScreen.GameResults:
                ShowOnly(gameResultsDocument);
                break;
            case GameScreen.LoadGame:
                ShowOnly(loadGameDocument);
                break;
            case GameScreen.Settings:
                ShowOnly(settingsDocument);
                break;
            case GameScreen.Employees:
                ShowOnly(employeesDocument);
                break;
            case GameScreen.Injured:
                ShowOnly(injuredDocument);
                break;
            case GameScreen.Cartera:
                ShowOnly(carteraDocument);
                break;
            case GameScreen.SeasonSummary:
                ShowOnly(seasonSummaryDocument);
                break;
            case GameScreen.PlayerAwards:
                ShowOnly(playerAwardsDocument);
                break;
            case GameScreen.EndSeason:
                ShowOnly(endSeasonDocument);
                break;
            case GameScreen.NewSeason:
                ShowOnly(newSeasonDocument);
                break;
            case GameScreen.Editor:
                ShowOnly(editorDocument);
                break;
            case GameScreen.Historial:
                ShowOnly(historialDocument);
                break;
            case GameScreen.Training:
                ShowOnly(trainingDocument);
                break;
            case GameScreen.Vestuario:
                ShowOnly(vestuarioDocument);
                break;
            case GameScreen.Quinteto:
                ShowOnly(quintetoDocument);
                break;
        }
    }

    void ShowOnly(UIDocument target)
    {
        if (mainMenuDocument != null) mainMenuDocument.gameObject.SetActive(false);
        if (selectTeamDocument != null) selectTeamDocument.gameObject.SetActive(false);
        if (legalNoticeDocument != null) legalNoticeDocument.gameObject.SetActive(false);
        if (preseasonDocument != null) preseasonDocument.gameObject.SetActive(false);
        if (dashboardDocument != null) dashboardDocument.gameObject.SetActive(false);
        if (rosterDocument != null) rosterDocument.gameObject.SetActive(false);
        if (calendarDocument != null) calendarDocument.gameObject.SetActive(false);
        if (standingsDocument != null) standingsDocument.gameObject.SetActive(false);
        if (palmaresDocument != null) palmaresDocument.gameObject.SetActive(false);
        if (resultsDocument != null) resultsDocument.gameObject.SetActive(false);
        if (playoffsDocument != null) playoffsDocument.gameObject.SetActive(false);
        if (statsDocument != null) statsDocument.gameObject.SetActive(false);
        if (recordsDocument != null) recordsDocument.gameObject.SetActive(false);
        if (marketDocument != null) marketDocument.gameObject.SetActive(false);
        if (financesDocument != null) financesDocument.gameObject.SetActive(false);
        if (loansDocument != null) loansDocument.gameObject.SetActive(false);
        if (sponsorsDocument != null) sponsorsDocument.gameObject.SetActive(false);
        if (tvDocument != null) tvDocument.gameObject.SetActive(false);
        if (arenaDocument != null) arenaDocument.gameObject.SetActive(false);
        if (messagesDocument != null) messagesDocument.gameObject.SetActive(false);
        if (matchDayDocument != null) matchDayDocument.gameObject.SetActive(false);
        if (gameResultsDocument != null) gameResultsDocument.gameObject.SetActive(false);
        if (loadGameDocument != null) loadGameDocument.gameObject.SetActive(false);
        if (settingsDocument != null) settingsDocument.gameObject.SetActive(false);
        if (employeesDocument != null) employeesDocument.gameObject.SetActive(false);
        if (injuredDocument != null) injuredDocument.gameObject.SetActive(false);
        if (carteraDocument != null) carteraDocument.gameObject.SetActive(false);
        if (seasonSummaryDocument != null) seasonSummaryDocument.gameObject.SetActive(false);
        if (playerAwardsDocument != null) playerAwardsDocument.gameObject.SetActive(false);
        if (endSeasonDocument != null) endSeasonDocument.gameObject.SetActive(false);
        if (newSeasonDocument != null) newSeasonDocument.gameObject.SetActive(false);
        if (editorDocument != null) editorDocument.gameObject.SetActive(false);
        if (historialDocument != null) historialDocument.gameObject.SetActive(false);
        if (trainingDocument != null) trainingDocument.gameObject.SetActive(false);
        if (vestuarioDocument != null) vestuarioDocument.gameObject.SetActive(false);
        if (quintetoDocument != null) quintetoDocument.gameObject.SetActive(false);
        if (target != null) target.gameObject.SetActive(true);
    }
}
