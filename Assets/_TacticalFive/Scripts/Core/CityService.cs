using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lógica de la capa de ciudad (F7-05): inicia y completa proyectos, genera
/// eventos de ciudad y aplica sus efectos por los canales existentes
/// (presupuesto, confianza, moral/físico y mensajes). Sin economía paralela.
/// </summary>
public static class CityService
{
    // ── PROYECTOS ─────────────────────────────────────────

    public static bool StartProject(ManagerData manager, TeamData team, SeasonData season,
        string projectKey, out string error)
    {
        error = null;
        var db = DatabaseManager.Instance;
        if (manager == null || team == null || season == null || db == null)
        {
            error = "No hay una partida activa.";
            return false;
        }

        var definition = CityCatalog.Project(projectKey);
        if (definition == null)
        {
            error = "Proyecto desconocido.";
            return false;
        }

        if (season.phase == "finished")
        {
            error = "La temporada ya ha terminado.";
            return false;
        }

        var existing = db.GetActiveCityProject(manager.id, season.id, projectKey);
        if (existing != null)
        {
            error = "Ya hay una obra en curso para este proyecto.";
            return false;
        }

        int completedLevel = db.GetCityProjectLevel(manager.id, season.id, projectKey);
        int nextLevel = completedLevel + 1;
        if (nextLevel > definition.maxLevel)
        {
            error = "Este proyecto ya está al máximo.";
            return false;
        }

        long cost = CityCatalog.ProjectCost(definition, nextLevel);
        if (team.budget < cost)
        {
            error = $"Presupuesto insuficiente: faltan {CityCatalog.FormatMoney(cost - team.budget)}.";
            return false;
        }

        team.budget -= cost;
        db.UpdateTeamBudget(team.id, team.budget);

        int day = season.current_game_day;
        db.InsertCityProject(new CityProjectData
        {
            manager_id = manager.id,
            season_id = season.id,
            team_id = team.id,
            project_key = projectKey,
            level = nextLevel,
            status = "active",
            started_day = day,
            completion_day = day + definition.days,
            cost = cost,
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });

        string effect = CityCatalog.ProjectEffectSummary(projectKey, nextLevel);
        AddMessage(manager, season, day,
            "Obra de ciudad iniciada",
            $"{team.name} impulsa la construcción de {definition.name} (nivel {nextLevel}) con una inversión de " +
            $"{CityCatalog.FormatMoney(cost)}. Las obras arrancarán de inmediato y se prolongarán durante " +
            $"{definition.days} días, con seguimiento diario por parte del club.\n\n" +
            $"{definition.description} Cuando concluyan los trabajos, el proyecto aportará {effect} " +
            $"La entidad informará de cualquier avance relevante a través de sus canales oficiales.",
            NewsType.PROYECTO_CIUDAD);

        return true;
    }

    /// <summary>
    /// Avanza la ciudad un día: completa obras y, con probabilidad, genera un
    /// evento pendiente. Devuelve cuántos proyectos se completaron.
    /// </summary>
    public static int Advance(ManagerData manager, TeamData team, SeasonData season, int gameDay)
    {
        var db = DatabaseManager.Instance;
        if (manager == null || team == null || season == null || db == null) return 0;

        int completed = 0;
        var projects = db.GetCityProjects(manager.id, season.id);
        foreach (var project in projects)
        {
            if (project.status != "active" || project.completion_day > gameDay) continue;

            project.status = "completed";
            db.UpdateCityProject(project);

            ApplyProjectCompletion(manager, team, season, project, gameDay);
            completed++;
        }

        MaybeGenerateEvent(manager, season, gameDay);
        return completed;
    }

    static void ApplyProjectCompletion(ManagerData manager, TeamData team, SeasonData season,
        CityProjectData project, int gameDay)
    {
        var definition = CityCatalog.Project(project.project_key);
        if (definition == null) return;

        var db = DatabaseManager.Instance;
        string effect = CityCatalog.ProjectEffectSummary(project.project_key, project.level);

        switch (project.project_key)
        {
            case CityCatalog.ProjectFan:
                manager.fan_confidence = Mathf.Clamp(
                    manager.fan_confidence + 4 + project.level * 2, 0, 100);
                db.SaveManager(manager);
                break;

            case CityCatalog.ProjectMedia:
                manager.fan_confidence = Mathf.Clamp(manager.fan_confidence + 5, 0, 100);
                db.SaveManager(manager);
                ApplyRosterDelta(team.id, 0, 3);
                break;
        }

        AddMessage(manager, season, gameDay,
            $"Proyecto completado: {definition.name}",
            $"La obra de {definition.name} (nivel {project.level}) ha quedado terminada tras " +
            $"{definition.days} días de trabajos. La inversión final ascendió a " +
            $"{CityCatalog.FormatMoney(project.cost)}.\n\n" +
            $"{effect} La ciudad ya disfruta de la mejora y el club estudia el siguiente paso para " +
            $"seguir creciendo. Los aficionados notarán el cambio en las próximas jornadas.",
            NewsType.PROYECTO_CIUDAD);
    }

    // ── EVENTOS ───────────────────────────────────────────

    public static CityEventData MaybeGenerateEvent(ManagerData manager, SeasonData season,
        int gameDay, float roll = -1f, int eventIndex = -1)
    {
        var db = DatabaseManager.Instance;
        if (manager == null || season == null || db == null) return null;
        if (season.phase == "finished") return null;
        if (gameDay < 5) return null;
        if (db.GetPendingCityEvent(manager.id, season.id) != null) return null;

        // Una oportunidad cada seis días; el llamador puede inyectar el dado en tests.
        if (gameDay % 6 != 0) return null;
        float value = roll >= 0f ? roll : UnityEngine.Random.value;
        if (value > 0.45f) return null;

        var definition = ChooseEvent(eventIndex);
        if (definition == null) return null;

        var cityEvent = new CityEventData
        {
            manager_id = manager.id,
            season_id = season.id,
            event_key = definition.key,
            status = "pending",
            game_day = gameDay,
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            resolved_day = 0,
            choice_index = -1
        };
        db.InsertCityEvent(cityEvent);

        AddMessage(manager, season, gameDay,
            "Nuevo evento en la ciudad",
            $"{definition.title}. Abre THE CITY para tomar una decisión.",
            NewsType.EVENTO_CIUDAD);

        return cityEvent;
    }

    static CityCatalog.EventDefinition ChooseEvent(int eventIndex)
    {
        var events = CityCatalog.Events;
        if (events.Length == 0) return null;
        int idx = eventIndex >= 0
            ? Mathf.Clamp(eventIndex, 0, events.Length - 1)
            : UnityEngine.Random.Range(0, events.Length);
        return events[idx];
    }

    public static bool ResolveEvent(ManagerData manager, TeamData team, SeasonData season,
        CityEventData cityEvent, int choiceIndex, out string error)
    {
        error = null;
        var db = DatabaseManager.Instance;
        if (manager == null || team == null || season == null || cityEvent == null || db == null)
        {
            error = "No hay una partida activa.";
            return false;
        }
        if (cityEvent.status != "pending")
        {
            error = "Este evento ya está resuelto.";
            return false;
        }

        var definition = CityCatalog.Event(cityEvent.event_key);
        if (definition == null || definition.choices == null
            || choiceIndex < 0 || choiceIndex >= definition.choices.Length)
        {
            error = "Decisión no válida.";
            return false;
        }

        var choice = definition.choices[choiceIndex];
        if (choice.cost > team.budget)
        {
            error = $"Presupuesto insuficiente: faltan {CityCatalog.FormatMoney(choice.cost - team.budget)}.";
            return false;
        }

        if (choice.cost > 0)
        {
            team.budget -= choice.cost;
            db.UpdateTeamBudget(team.id, team.budget);
        }

        if (choice.fanConfidence != 0)
        {
            manager.fan_confidence = Mathf.Clamp(
                manager.fan_confidence + choice.fanConfidence, 0, 100);
            db.SaveManager(manager);
        }

        if (choice.morale != 0 || choice.fisico != 0)
            ApplyRosterDelta(team.id, choice.morale, choice.fisico);

        int day = season.current_game_day;
        cityEvent.status = "resolved";
        cityEvent.resolved_day = day;
        cityEvent.choice_index = choiceIndex;
        db.UpdateCityEvent(cityEvent);

        AddMessage(manager, season, day,
            $"Ciudad: {definition.title}",
            $"Decisión: {choice.label}. {CityCatalog.EventEffectSummary(choice)}",
            NewsType.EVENTO_CIUDAD);

        return true;
    }

    // ── EFECTOS AUXILIARES ────────────────────────────────

    static void ApplyRosterDelta(int teamId, int moraleDelta, int fisicoDelta)
    {
        var players = DatabaseManager.Instance.GetPlayersByTeam(teamId);
        if (players == null || players.Count == 0) return;

        var changed = new List<PlayerData>();
        foreach (var player in players)
        {
            bool touched = false;
            if (moraleDelta != 0)
            {
                player.morale = Mathf.Clamp(player.morale + moraleDelta, 0, 100);
                touched = true;
            }
            if (fisicoDelta != 0)
            {
                player.fisico = Mathf.Clamp(player.fisico + fisicoDelta, 0, 99);
                touched = true;
            }
            if (touched) changed.Add(player);
        }

        if (changed.Count > 0)
            DatabaseManager.Instance.UpdatePlayersBatch(changed);
    }

    static void AddMessage(ManagerData manager, SeasonData season, int gameDay,
        string title, string body, string newsType)
    {
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = manager.id,
            sender_type = 0,
            sender_id = 0,
            title = title,
            body = body,
            game_day = gameDay,
            game_date = season?.current_date ?? "",
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0,
            category = NewsCategory.CLUB,
            news_type = newsType
        });
    }
}
