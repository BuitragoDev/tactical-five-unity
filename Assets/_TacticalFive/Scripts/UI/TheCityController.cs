using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// THE CITY (F7-05): centro de mando de la franquicia. El mapa deja de ser una
/// imagen con botones: cada edificio abre un inspector con estado real y acciones,
/// y la capa de proyectos/eventos aporta decisiones persistentes y acotadas.
/// </summary>
public class TheCityController : UIScreenController
{
    class BuildingInfo
    {
        public string key;
        public string buttonName;
        public string statusLabelName;
        public string title;
        public string subtitle;
        public string description;
        public GameScreen screen;
    }

    static readonly BuildingInfo[] Buildings =
    {
        new BuildingInfo
        {
            key = "hospital", buttonName = "CityHospital", statusLabelName = "CityHospitalStatus",
            title = "CENTRO MÉDICO", subtitle = "Parte médico y recuperación",
            description = "Controla las lesiones, los plazos de recuperación y el estado físico de la plantilla.",
            screen = GameScreen.Injured
        },
        new BuildingInfo
        {
            key = "arena", buttonName = "CityArena", statusLabelName = "CityArenaStatus",
            title = "PABELLÓN", subtitle = "Aforo, entradas y experiencia de partido",
            description = "El corazón de la ciudad deportiva. La asistencia depende del mercado, los resultados y el precio de la entrada.",
            screen = GameScreen.Arena
        },
        new BuildingInfo
        {
            key = "training", buttonName = "CityTraining", statusLabelName = "CityTrainingStatus",
            title = "ENTRENAMIENTO", subtitle = "Instalaciones y plan de trabajo",
            description = "Las instalaciones y el plan de entrenamiento condicionan la progresión y el desgaste de la plantilla.",
            screen = GameScreen.Training
        },
        new BuildingInfo
        {
            key = "league", buttonName = "CityLeague", statusLabelName = "CityLeagueStatus",
            title = "OFICINA DE LA LIGA", subtitle = "Clasificación y objetivo de temporada",
            description = "Consulta la posición en la conferencia, el objetivo del consejo y la marcha de la temporada.",
            screen = GameScreen.Standings
        },
        new BuildingInfo
        {
            key = "personnel", buttonName = "CityPersonnel", statusLabelName = "CityPersonnelStatus",
            title = "OFICINA DE PERSONAL", subtitle = "Cuerpo técnico y empleados",
            description = "Gestiona entrenadores, ojeadores, médico y demás personal del club.",
            screen = GameScreen.Employees
        },
        new BuildingInfo
        {
            key = "bank", buttonName = "CityBank", statusLabelName = "CityBankStatus",
            title = "BANCO", subtitle = "Presupuesto, nómina y préstamos",
            description = "El presupuesto del club y su margen salarial. Las decisiones financieras se toman en Finanzas.",
            screen = GameScreen.Finances
        },
        new BuildingInfo
        {
            key = "newspaper", buttonName = "CityNewspaper", statusLabelName = "CityNewspaperStatus",
            title = "NOTICIAS", subtitle = "Buzón y actualidad del club",
            description = "Toda la actualidad del equipo y de la ciudad, con los mensajes pendientes de leer.",
            screen = GameScreen.Messages
        },
        new BuildingInfo
        {
            key = "statistics", buttonName = "CityStatistics", statusLabelName = "CityStatisticsStatus",
            title = "ESTADÍSTICAS", subtitle = "Rendimiento y líderes del equipo",
            description = "Los números del equipo y de la plantilla durante la temporada.",
            screen = GameScreen.Stats
        },
        new BuildingInfo
        {
            key = "museum", buttonName = "CityMuseum", statusLabelName = "CityMuseumStatus",
            title = "MUSEO", subtitle = "Palmarés, Hall of Fame y dorsales",
            description = "La historia de la franquicia: títulos, leyendas y camisetas retiradas.",
            screen = GameScreen.Palmares
        }
    };

    // ── Referencias UXML ──────────────────────────────────
    Label _pulseBudget;
    Label _pulseFan;
    Label _pulseObjective;
    Label _pulseNextGame;
    Label _pulseNotice;
    Label _pulseTeam;
    Label _inspectorTitle;
    Label _inspectorSubtitle;
    VisualElement _inspectorBody;
    Label _notice;
    Button _tabBuilding;
    Button _tabProjects;
    Button _tabEvents;

    // ── Estado de pantalla ────────────────────────────────
    string _selectedKey = "arena";
    string _tab = "building";
    List<CityProjectData> _projects = new List<CityProjectData>();
    CityEventData _pendingEvent;

    protected override GameScreen ScreenId => GameScreen.TheCity;

    protected override void CacheReferences()
    {
        _pulseBudget = _root.Q<Label>("CityPulseBudget");
        _pulseFan = _root.Q<Label>("CityPulseFan");
        _pulseObjective = _root.Q<Label>("CityPulseObjective");
        _pulseNextGame = _root.Q<Label>("CityPulseNextGame");
        _pulseNotice = _root.Q<Label>("CityPulseNotice");
        _pulseTeam = _root.Q<Label>("CityPulseTeam");
        _inspectorTitle = _root.Q<Label>("CityInspectorTitle");
        _inspectorSubtitle = _root.Q<Label>("CityInspectorSubtitle");
        _inspectorBody = _root.Q<VisualElement>("CityInspectorBody");
        _notice = _root.Q<Label>("CityNotice");
        _tabBuilding = _root.Q<Button>("CityTabBuilding");
        _tabProjects = _root.Q<Button>("CityTabProjects");
        _tabEvents = _root.Q<Button>("CityTabEvents");
    }

    protected override void LoadData()
    {
        base.LoadData();
        ReloadCityData();
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();

        foreach (var building in Buildings)
        {
            var button = _root.Q<Button>(building.buttonName);
            if (button == null) continue;
            string key = building.key;
            button.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                SelectBuilding(key);
            });
        }

        _tabBuilding?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetTab("building"); });
        _tabProjects?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetTab("projects"); });
        _tabEvents?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetTab("events"); });
    }

    protected override void Refresh()
    {
        base.Refresh();
        if (_manager == null || _myTeam == null || _season == null) return;

        SetNotice("");
        ReloadCityData();
        RefreshPulse();
        RefreshStatuses();
        RefreshTabs();
        BuildInspector();
    }

    void ReloadCityData()
    {
        if (DatabaseManager.Instance == null || _manager == null || _season == null) return;
        _projects = DatabaseManager.Instance.GetCityProjects(_manager.id, _season.id)
            ?? new List<CityProjectData>();
        _pendingEvent = DatabaseManager.Instance.GetPendingCityEvent(_manager.id, _season.id);
    }

    void SelectBuilding(string key)
    {
        _selectedKey = key;
        _tab = "building";
        RefreshTabs();
        BuildInspector();
    }

    void SetTab(string tab)
    {
        _tab = tab;
        RefreshTabs();
        BuildInspector();
    }

    void RefreshTabs()
    {
        _tabBuilding?.EnableInClassList("city-tab--active", _tab == "building");
        _tabProjects?.EnableInClassList("city-tab--active", _tab == "projects");
        _tabEvents?.EnableInClassList("city-tab--active", _tab == "events");
        if (_tabEvents != null)
            _tabEvents.text = _pendingEvent != null ? "EVENTOS · 1" : "EVENTOS";
    }

    void RefreshPulse()
    {
        if (_pulseBudget != null)
            _pulseBudget.text = $"${_myTeam.budget:N0}";

        if (_pulseFan != null)
            _pulseFan.text = $"{_manager.fan_confidence} / 100";

        int rank = GetConferenceRank();
        if (_pulseObjective != null)
        {
            string objective = string.IsNullOrEmpty(_myTeam.objective)
                ? "Sin objetivo"
                : _myTeam.objective;
            _pulseObjective.text = rank > 0
                ? $"{objective} · #{rank} {_myTeam.conference}"
                : objective;
        }

        if (_pulseNextGame != null)
        {
            var next = DatabaseManager.Instance.GetNextGame(_manager.id, _myTeam.id);
            if (next != null)
            {
                int rivalId = next.home_team_id == _myTeam.id
                    ? next.away_team_id
                    : next.home_team_id;
                var rival = DatabaseManager.Instance.GetTeamById(rivalId);
                string venue = next.home_team_id == _myTeam.id ? "vs" : "@";
                _pulseNextGame.text = $"{venue} {rival?.abbreviation ?? "—"} · {next.game_date}";
            }
            else
            {
                _pulseNextGame.text = "Sin partidos";
            }
        }

        if (_pulseNotice != null)
        {
            int active = _projects.Count(p => p.status == "active");
            if (_pendingEvent != null)
                _pulseNotice.text = "Evento pendiente";
            else if (active > 0)
                _pulseNotice.text = $"{active} obra(s) en curso";
            else
                _pulseNotice.text = "Sin avisos";
        }

        if (_pulseTeam != null)
            _pulseTeam.text = _myTeam.name.ToUpper();
    }

    void RefreshStatuses()
    {
        foreach (var building in Buildings)
        {
            var label = _root.Q<Label>(building.statusLabelName);
            if (label != null)
                label.text = GetStatus(building.key);
        }
    }

    string GetStatus(string key)
    {
        var db = DatabaseManager.Instance;
        switch (key)
        {
            case "hospital":
            {
                var players = db.GetPlayersByTeam(_myTeam.id);
                int injured = players.Count(p => p.injury_days > 0);
                return injured == 0 ? "Sin lesionados" : $"{injured} lesionado(s)";
            }
            case "arena":
                return $"{_myTeam.capacity:N0} plazas";
            case "training":
                return $"Instalaciones {_myTeam.facilities}/5";
            case "league":
            {
                int rank = GetConferenceRank();
                return rank > 0 ? $"#{rank} {_myTeam.conference}" : "Sin clasificación";
            }
            case "personnel":
            {
                var staff = db.GetEmployeesByTeam(_myTeam.id);
                return $"{staff.Count} empleado(s)";
            }
            case "bank":
                return $"${_myTeam.budget / 1_000_000}M";
            case "newspaper":
            {
                var messages = db.GetMessages(_manager.id);
                int unread = messages.Count(m => m.is_read == 0);
                return unread == 0 ? "Buzón al día" : $"{unread} sin leer";
            }
            case "statistics":
            {
                var stats = db.GetSeasonPlayerStatsAggregates(_manager.id, _season.id);
                var top = stats.OrderByDescending(s => s.total_points).FirstOrDefault();
                if (top == null) return "Sin datos";
                var player = db.GetPlayerById(top.player_id);
                return player != null ? $"{player.last_name} · {top.total_points} pts" : "Sin datos";
            }
            case "museum":
            {
                int hof = db.GetHoFMembers().Count;
                int retired = db.GetRetiredNumbers(_myTeam.id).Count;
                return $"{hof} HOF · {retired} dorsales";
            }
            default:
                return "";
        }
    }

    int GetConferenceRank()
    {
        if (_manager == null || _myTeam == null) return 0;
        var db = DatabaseManager.Instance;
        var games = db.GetStandingsGames(_manager.id);
        return ObjectiveHelper.GetConferenceRank(
            _myTeam.id, _myTeam.conference, db.GetAllTeams(), games);
    }

    // ── INSPECTOR ─────────────────────────────────────────

    void BuildInspector()
    {
        if (_inspectorBody == null) return;
        _inspectorBody.Clear();

        switch (_tab)
        {
            case "projects":
                BuildProjects();
                break;
            case "events":
                BuildEvents();
                break;
            default:
                BuildBuilding();
                break;
        }
    }

    void BuildBuilding()
    {
        var building = Buildings.FirstOrDefault(b => b.key == _selectedKey) ?? Buildings[0];
        if (_inspectorTitle != null) _inspectorTitle.text = building.title;
        if (_inspectorSubtitle != null) _inspectorSubtitle.text = building.subtitle;

        AddInfo("ESTADO", GetStatus(building.key));
        AddDescription(building.description);

        switch (building.key)
        {
            case "arena":
            {
                int level = GetProjectLevel(CityCatalog.ProjectArena);
                AddInfo("PROYECTO", $"Nivel {level}/2 · {CityCatalog.ProjectEffectSummary(CityCatalog.ProjectArena, level)}");
                AddInfo("PRECIO", $"Entrada media: ${GetTicketPrice():N0}");
                break;
            }
            case "training":
            {
                int level = GetProjectLevel(CityCatalog.ProjectPerformance);
                AddInfo("PROYECTO", $"Nivel {level}/2 · {CityCatalog.ProjectEffectSummary(CityCatalog.ProjectPerformance, level)}");
                break;
            }
            case "hospital":
            {
                var injured = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id)
                    .Where(p => p.injury_days > 0)
                    .OrderByDescending(p => p.injury_days)
                    .Take(4)
                    .ToList();
                if (injured.Count > 0)
                {
                    AddSectionTitle("PARTE MÉDICO");
                    foreach (var p in injured)
                        AddInfo($"{p.first_name} {p.last_name}", p.injury_type);
                }
                break;
            }
            case "bank":
                AddInfo("MARGEN SALARIAL", $"${_myTeam.salary_margin:N0}");
                if (_pendingEvent != null)
                    AddInfo("EVENTO", "Hay una decisión pendiente en la ciudad.");
                break;
            case "newspaper":
                if (_pendingEvent != null)
                    AddInfo("EVENTO", "Hay una decisión pendiente en la ciudad.");
                break;
        }

        AddAction($"ABRIR {building.subtitle.ToUpper()}", () =>
            ScreenManager.Instance.GoTo(building.screen));

        if (building.key == "arena" || building.key == "training")
            AddAction("GESTIONAR PROYECTOS", () => SetTab("projects"), ghost: true);

        AddAction("VER EVENTOS DE LA CIUDAD", () => SetTab("events"), ghost: true);
    }

    void BuildProjects()
    {
        if (_inspectorTitle != null) _inspectorTitle.text = "PROYECTOS DE CIUDAD";
        if (_inspectorSubtitle != null)
            _inspectorSubtitle.text = "Inversión en infraestructura con efectos acotados";

        AddSectionTitle("OBRAS DISPONIBLES");

        foreach (var definition in CityCatalog.Projects)
        {
            var completed = _projects
                .Where(p => p.project_key == definition.key && p.status == "completed")
                .ToList();
            int level = completed.Count;
            var active = _projects.FirstOrDefault(p =>
                p.project_key == definition.key && p.status == "active");

            var card = new VisualElement();
            card.AddToClassList("city-project-card");

            var head = new VisualElement();
            head.AddToClassList("city-project-head");
            var name = new Label(definition.name);
            name.AddToClassList("city-project-name");
            var levelLabel = new Label($"{level}/{definition.maxLevel}");
            levelLabel.AddToClassList("city-project-level");
            head.Add(name);
            head.Add(levelLabel);
            card.Add(head);

            var desc = new Label(definition.description);
            desc.AddToClassList("city-project-desc");
            card.Add(desc);

            var status = new Label();
            status.AddToClassList("city-project-status");
            card.Add(status);

            if (active != null)
            {
                int remaining = Mathf.Max(0, active.completion_day - _season.current_game_day);
                status.text = $"EN CURSO · quedan {remaining} días · {CityCatalog.ProjectEffectSummary(definition.key, active.level)}";
            }
            else if (level >= definition.maxLevel)
            {
                status.text = "COMPLETADO · " + CityCatalog.ProjectEffectSummary(definition.key, level);
            }
            else
            {
                int nextLevel = level + 1;
                long cost = CityCatalog.ProjectCost(definition, nextLevel);
                bool affordable = _myTeam.budget >= cost;
                status.text = $"Nivel {nextLevel} · ${cost:N0} · {definition.days} días";
                var start = new Button(() => StartProject(definition))
                {
                    text = affordable ? "INICIAR PROYECTO" : "SIN PRESUPUESTO"
                };
                start.AddToClassList("city-action");
                if (!affordable)
                {
                    start.SetEnabled(false);
                    start.AddToClassList("city-action--disabled");
                }
                CursorManager.Instance?.RegisterHandCursor(start);
                card.Add(start);
            }

            _inspectorBody.Add(card);
        }
    }

    void BuildEvents()
    {
        if (_inspectorTitle != null) _inspectorTitle.text = "EVENTOS DE CIUDAD";
        if (_inspectorSubtitle != null)
            _inspectorSubtitle.text = "Decisiones puntuales de la franquicia";

        if (_pendingEvent == null)
        {
            AddSectionTitle("SIN EVENTOS PENDIENTES");
            AddDescription("La ciudad está tranquila. Los eventos aparecen cada pocos días y requieren una decisión.");
            AddAction("VER PROYECTOS", () => SetTab("projects"), ghost: true);
            return;
        }

        var definition = CityCatalog.Event(_pendingEvent.event_key);
        if (definition == null)
        {
            AddDescription("El evento pendiente ya no está disponible.");
            return;
        }

        var card = new VisualElement();
        card.AddToClassList("city-event-card");

        var title = new Label(definition.title);
        title.AddToClassList("city-event-title");
        card.Add(title);

        var desc = new Label(definition.description);
        desc.AddToClassList("city-event-desc");
        card.Add(desc);

        for (int i = 0; i < definition.choices.Length; i++)
        {
            int choiceIndex = i;
            var choice = definition.choices[i];
            bool affordable = _myTeam.budget >= choice.cost;

            var button = new Button(() => ResolveEvent(choiceIndex))
            {
                text = choice.label
            };
            button.AddToClassList("city-choice");
            if (!affordable)
            {
                button.SetEnabled(false);
                button.AddToClassList("city-choice--disabled");
            }
            CursorManager.Instance?.RegisterHandCursor(button);
            card.Add(button);

            string effect = CityCatalog.EventEffectSummary(choice);
            if (!string.IsNullOrEmpty(effect))
            {
                var effectLabel = new Label(effect);
                effectLabel.AddToClassList("city-choice-effect");
                card.Add(effectLabel);
            }
        }

        _inspectorBody.Add(card);
    }

    void StartProject(CityCatalog.ProjectDefinition definition)
    {
        if (CityService.StartProject(_manager, _myTeam, _season, definition.key, out string error))
        {
            Refresh();
            SetNotice($"{definition.name}: obra iniciada.");
        }
        else
        {
            SetNotice(error);
        }
    }

    void ResolveEvent(int choiceIndex)
    {
        if (CityService.ResolveEvent(_manager, _myTeam, _season, _pendingEvent, choiceIndex, out string error))
        {
            Refresh();
            SetNotice("Decisión tomada. La ciudad responde.");
        }
        else
        {
            SetNotice(error);
        }
    }

    void SetNotice(string text)
    {
        if (_notice != null) _notice.text = text ?? "";
    }

    int GetProjectLevel(string key)
        => _projects.Count(p => p.project_key == key && p.status == "completed");

    long GetTicketPrice()
    {
        var settings = DatabaseManager.Instance.GetTeamSettings(_myTeam.id);
        return settings != null ? (long)settings.ticket_price : 0;
    }

    // ── Helpers de construcción del inspector ─────────────

    void AddSectionTitle(string text)
    {
        var label = new Label(text);
        label.AddToClassList("city-section-title");
        _inspectorBody.Add(label);
    }

    void AddInfo(string labelText, string valueText)
    {
        var row = new VisualElement();
        row.AddToClassList("city-info-row");
        var label = new Label(labelText);
        label.AddToClassList("city-info-label");
        var value = new Label(valueText ?? "");
        value.AddToClassList("city-info-value");
        row.Add(label);
        row.Add(value);
        _inspectorBody.Add(row);
    }

    void AddDescription(string text)
    {
        var label = new Label(text ?? "");
        label.AddToClassList("city-description");
        _inspectorBody.Add(label);
    }

    void AddAction(string text, Action action, bool ghost = false, bool danger = false)
    {
        var button = new Button(action) { text = text };
        button.AddToClassList("city-action");
        if (ghost) button.AddToClassList("city-action--ghost");
        if (danger) button.AddToClassList("city-action--danger");
        CursorManager.Instance?.RegisterHandCursor(button);
        _inspectorBody.Add(button);
    }
}
