using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Convierte un evento del play-by-play (un ataque) en un guion de presentación
/// para la vista Visual 2D: posiciones de los 10 jugadores, recorrido del balón,
/// líneas del ticker, marcador por pasos y tiempos de la jugada.
///
/// Es una capa PURA y determinista: no toca el resultado del partido ni usa el
/// generador aleatorio del motor. La única "aleatoriedad" es un hash estable de
/// (partido, evento, jugador) para alternar lados y evitar que todas las jugadas
/// salgan idénticas. El renderer se limita a reproducir el guion.
/// </summary>
public static class Sim2DChoreographer
{
    public class PlayerInfo
    {
        public int Id;
        public int TeamId;
        public string Position;
        public string Name;
        public int Overall;
    }

    public class Point
    {
        public float Time;
        public float X;
        public float Y;
        public bool IsShot;
    }

    public class PlayerPath
    {
        public int PlayerId;
        public List<Point> Points = new();
    }

    public enum CueKind
    {
        BuildUp,
        Shot,
        Outcome,
        FreeThrow,
        Rebound,
        Turnover
    }

    public class Cue
    {
        public float Time;
        public string Text;
        public int Priority;
        public CueKind Kind;
    }

    public class ScoreStep
    {
        public float Time;
        public int Home;
        public int Away;
    }

    public class Script
    {
        public List<PlayerPath> PlayerPaths = new();
        public List<Point> BallPath = new();
        public List<Cue> Cues = new();
        public List<ScoreStep> ScoreSteps = new();
        public int BallHandlerId;
        public bool BallFollowsHandler;
        public List<int> ActionIds = new();
        public int PressureId;
        public bool IsFreeThrow;
        public float DeltasTime = 0.96f;
    }

    public enum InboundKind
    {
        None,
        TipOff,
        Baseline,
        Sideline,
        DeadBall,
        Outlet
    }

    /// <summary>
    /// Construye el guion del evento. Devuelve null si el evento no tiene deltas
    /// (inicio de cuarto, expulsión…) y debe usarse el layout de respaldo.
    /// </summary>
    public static Script Build(
        GameSimulator.PlayByPlayEvent ev,
        GameSimulator.PlayByPlayEvent previous,
        IReadOnlyList<PlayerInfo> offense,
        IReadOnlyList<PlayerInfo> defense,
        bool attackRight,
        InboundKind inbound,
        float inboundY,
        int eventIndex,
        int gameId,
        int previousHome,
        int previousAway,
        bool offenseIsHome)
    {
        if (ev?.deltas == null || ev.deltas.Count == 0) return null;
        if (offense == null || offense.Count == 0) return null;
        defense ??= new List<PlayerInfo>();

        int fga = StatSum(ev, "fga");
        int fg3a = StatSum(ev, "fg3a");
        int fgm = StatSum(ev, "fgm");
        int fta = StatSum(ev, "fta");
        int ftm = StatSum(ev, "ftm");
        int shooterId = StatPlayer(ev, "fga");
        if (shooterId == 0) shooterId = StatPlayer(ev, "fgm");
        int ftShooterId = ev.freeThrowShooterId > 0 ? ev.freeThrowShooterId : StatPlayer(ev, "fta");
        int rebounderId = StatPlayer(ev, "oreb");
        bool offensiveRebound = rebounderId > 0;
        if (rebounderId == 0) rebounderId = StatPlayer(ev, "dreb");
        int stealerId = StatPlayer(ev, "stl");
        int blockerId = StatPlayer(ev, "blk");
        int offenderId = StatPlayer(ev, "to");
        int assisterId = StatPlayer(ev, "ast");

        bool isFt = fta > 0 || (ev.freeThrowResults != null && ev.freeThrowResults.Count > 0);
        bool isShot = fga > 0;
        bool made = fgm > 0;
        bool three = fg3a > 0;
        bool isTurnover = offenderId > 0 || stealerId > 0;
        bool isBlock = blockerId > 0;
        bool andOne = made && isFt;
        bool putback = inbound == InboundKind.None && previous != null && StatSum(previous, "oreb") > 0;
        bool fastBreak = inbound == InboundKind.None && previous != null && StatSum(previous, "stl") > 0;

        var shooter = Find(offense, shooterId);
        var ftShooter = Find(offense, ftShooterId) ?? shooter;
        var rebounder = Find(offense, rebounderId) ?? Find(defense, rebounderId);
        var stealer = Find(defense, stealerId);
        var blocker = Find(defense, blockerId);
        var offender = Find(offense, offenderId);
        var handler = Find(offense, fastBreak ? stealerId : putback ? rebounderId : 0)
            ?? offense.FirstOrDefault(p => p.Position == "PG")
            ?? offense[0];

        var script = new Script
        {
            BallHandlerId = handler.Id,
            IsFreeThrow = isFt && !andOne
        };

        if (isFt && !andOne)
        {
            var results = ev.freeThrowResults != null && ev.freeThrowResults.Count > 0
                ? new List<bool>(ev.freeThrowResults)
                : SynthesizeFreeThrows(ftm, fta);
            BuildFreeThrowStandalone(script, ev, offense, defense, attackRight,
                previousHome, previousAway, offenseIsHome, ftShooter, results, rebounder);
            SortCues(script);
            return script;
        }

        BuildFieldGoal(script, ev, offense, defense, attackRight, inbound, inboundY,
            eventIndex, gameId, previousHome, previousAway, offenseIsHome,
            shooter, ftShooter, rebounder, offensiveRebound, stealer, blocker, offender,
            handler, assisterId, made, three, isShot, isTurnover, isBlock, andOne,
            putback, fastBreak);

        SortCues(script);
        return script;
    }

    // ── Jugada de campo ──────────────────────────────────────────────────────

    static void BuildFieldGoal(
        Script script,
        GameSimulator.PlayByPlayEvent ev,
        IReadOnlyList<PlayerInfo> offense,
        IReadOnlyList<PlayerInfo> defense,
        bool attackRight,
        InboundKind inbound,
        float inboundY,
        int eventIndex,
        int gameId,
        int previousHome,
        int previousAway,
        bool offenseIsHome,
        PlayerInfo shooter,
        PlayerInfo ftShooter,
        PlayerInfo rebounder,
        bool offensiveRebound,
        PlayerInfo stealer,
        PlayerInfo blocker,
        PlayerInfo offender,
        PlayerInfo handler,
        int assisterId,
        bool made,
        bool three,
        bool isShot,
        bool isTurnover,
        bool isBlock,
        bool andOne,
        bool putback,
        bool fastBreak)
    {
        int side = eventIndex % 2;
        var spots = AssignSpots(offense, shooter, three, attackRight, side);
        Vector2 handlerSpot = spots[handler.Id];

        float setupTime = 0.50f;
        float receiveTime = 0.16f;
        float bringTime = 0.46f;
        float shotTime = 0.84f;
        float reboundTime = 0.96f;
        float pass1Time = 0.56f;
        float pass2Time = 0.70f;
        if (putback)
        {
            setupTime = 0.16f;
            receiveTime = 0.06f;
            bringTime = 0.14f;
            pass1Time = 0.24f;
            pass2Time = 0.30f;
            shotTime = 0.34f;
            reboundTime = 0.48f;
        }
        else if (fastBreak)
        {
            setupTime = 0.30f;
            receiveTime = 0.10f;
            bringTime = 0.26f;
            pass1Time = 0.34f;
            pass2Time = 0.42f;
            shotTime = 0.48f;
            reboundTime = 0.62f;
        }

        Vector2 inboundPoint = Sim2DCourt.SpotPosition(Sim2DCourt.Spot.InboundBaseline, attackRight);
        Vector2 receivePoint = Sim2DCourt.SpotPosition(Sim2DCourt.Spot.Backcourt, attackRight);
        int inbounderId = 0;
        int outletPasserId = 0;
        switch (inbound)
        {
            case InboundKind.Baseline:
            case InboundKind.DeadBall:
            {
                var inbounder = PickInbounder(offense, handler, false);
                inbounderId = inbounder?.Id ?? 0;
                inboundPoint = inbound == InboundKind.Baseline
                    ? Sim2DCourt.SpotPosition(Sim2DCourt.Spot.InboundBaseline, attackRight)
                    : new Vector2(0.5f, Mathf.Clamp(inboundY, 0.06f, 0.94f));
                receivePoint = new Vector2(Sim2DCourt.BackcourtX(attackRight), 0.5f);
                break;
            }
            case InboundKind.Sideline:
            {
                var inbounder = PickInbounder(offense, handler, true);
                inbounderId = inbounder?.Id ?? 0;
                inboundPoint = new Vector2(0.5f, Mathf.Clamp(inboundY, 0.06f, 0.94f));
                receivePoint = new Vector2(Sim2DCourt.BackcourtX(attackRight),
                    Mathf.Clamp(inboundY, 0.12f, 0.88f));
                break;
            }
            case InboundKind.Outlet:
            {
                var outletPasser = Find(offense, rebounder?.Id ?? 0) ?? PickOutletPasser(offense, handler);
                outletPasserId = outletPasser?.Id ?? 0;
                inboundPoint = new Vector2(
                    Sim2DCourt.HoopX(!attackRight) + Sim2DCourt.TowardBasket(attackRight) * 0.03f,
                    0.5f);
                receivePoint = new Vector2(Sim2DCourt.BackcourtX(attackRight), 0.5f);
                break;
            }
            case InboundKind.TipOff:
                inboundPoint = new Vector2(0.5f, 0.5f);
                receivePoint = new Vector2(Sim2DCourt.BackcourtX(attackRight), 0.5f);
                break;
        }

        // ── Recorridos de ataque ──
        foreach (var p in offense)
        {
            Vector2 spot = spots[p.Id];
            if (p.Id == inbounderId)
            {
                AddPlayerPoint(script, p.Id, 0.08f, inboundPoint);
                AddPlayerPoint(script, p.Id, setupTime, spot);
            }
            else if (p.Id == outletPasserId)
            {
                AddPlayerPoint(script, p.Id, 0.06f, inboundPoint);
                AddPlayerPoint(script, p.Id, setupTime, spot);
            }
            else if (p.Id == handler.Id && inbound != InboundKind.None)
            {
                AddPlayerPoint(script, p.Id, receiveTime, receivePoint);
                AddPlayerPoint(script, p.Id, bringTime, spot);
            }
            else if (p.Id == handler.Id)
            {
                AddPlayerPoint(script, p.Id, bringTime, spot);
            }
            else
            {
                AddPlayerPoint(script, p.Id, setupTime, spot);
            }

            if (shooter != null && p.Id == shooter.Id)
            {
                AddPlayerPoint(script, p.Id, shotTime, spots[shooter.Id]);
                if (andOne)
                    AddPlayerPoint(script, p.Id, shotTime + 0.06f,
                        Sim2DCourt.FreeThrowShooter(attackRight));
            }

            if (rebounder != null && p.Id == rebounder.Id && !made && offensiveRebound)
                AddPlayerPoint(script, p.Id, reboundTime, ReboundSpot(attackRight));
        }

        // ── Recorridos de defensa (emparejamiento por posición) ──
        var matchups = AssignMatchups(offense, defense);
        int pressureId = 0;
        foreach (var d in defense)
        {
            if (!matchups.TryGetValue(d.Id, out var man)) continue;
            Vector2 manSpot = spots[man.Id];
            bool onBall = man.Id == handler.Id;
            Vector2 target = Sim2DCourt.DefensiveSpot(manSpot, handlerSpot, attackRight,
                d.Position == "C", onBall);
            AddPlayerPoint(script, d.Id, setupTime, target);

            if (onBall)
            {
                pressureId = d.Id;
                if (isShot && shooter != null)
                {
                    Vector2 closeout = Sim2DCourt.DefensiveSpot(spots[shooter.Id], handlerSpot,
                        attackRight, d.Position == "C", true);
                    AddPlayerPoint(script, d.Id, shotTime - 0.02f, closeout);
                }
            }

            if (isBlock && blocker != null && d.Id == blocker.Id && shooter != null)
                AddPlayerPoint(script, d.Id, shotTime, spots[shooter.Id]);

            if (!made && rebounder != null && offensiveRebound && man.Id == rebounder.Id)
                AddPlayerPoint(script, d.Id, reboundTime,
                    Sim2DCourt.BoxOutSpot(ReboundSpot(attackRight), attackRight));

            if (!made && rebounder != null && !offensiveRebound && d.Id == rebounder.Id)
                AddPlayerPoint(script, d.Id, reboundTime, ReboundSpot(attackRight));
        }
        script.PressureId = pressureId;

        // ── Balón ──
        if (inbound == InboundKind.TipOff)
        {
            AddBall(script, 0.06f, 0.5f, 0.5f);
            AddBall(script, receiveTime + 0.04f, receivePoint);
        }
        else if (inbound == InboundKind.Outlet)
        {
            AddBall(script, 0.04f, inboundPoint);
            AddBall(script, 0.14f, receivePoint);
        }
        else if (inbound != InboundKind.None)
        {
            AddBall(script, 0.10f, inboundPoint);
            AddBall(script, receiveTime + 0.04f, receivePoint);
        }

        if (inbound != InboundKind.None)
        {
            AddBallFollow(script, Mathf.Max(0.18f, receiveTime + 0.06f), bringTime,
                receivePoint, handlerSpot);
        }

        // Pases: el asistente real si el motor lo dio; si no, un ala.
        var passer = Find(offense, assisterId) ?? handler;
        if (shooter != null && passer.Id == shooter.Id) passer = handler;
        PlayerInfo passReceiver = null;
        if (passer.Id != handler.Id)
            passReceiver = passer;
        else if (shooter != null && shooter.Id != handler.Id)
            passReceiver = shooter;
        else
            passReceiver = PickWing(offense, handler, shooter);

        if (passReceiver != null && passReceiver.Id != handler.Id && pass1Time < shotTime)
        {
            AddBall(script, pass1Time, spots[passReceiver.Id]);
            AddCue(script, pass1Time, Sim2DCommentary.Pass(handler.Name, passReceiver.Name,
                ZoneName(spots[passReceiver.Id])), 1, CueKind.BuildUp);
        }
        if (shooter != null && shooter.Id != handler.Id && passReceiver != null
            && shooter.Id != passReceiver.Id && pass2Time < shotTime)
        {
            AddBall(script, pass2Time, spots[shooter.Id]);
            AddCue(script, pass2Time, Sim2DCommentary.Pass(passReceiver.Name, shooter.Name,
                ZoneName(spots[shooter.Id])), 1, CueKind.BuildUp);
        }
        else if (shooter != null && shooter.Id == handler.Id && passReceiver != null
                 && pass2Time < shotTime)
        {
            AddBall(script, pass2Time, handlerSpot);
            AddCue(script, pass2Time, Sim2DCommentary.Pass(passReceiver.Name, handler.Name, ""),
                1, CueKind.BuildUp);
        }

        // Tiro / pérdida / tapón.
        if (isShot && shooter != null)
        {
            AddBall(script, shotTime - 0.04f, spots[shooter.Id]);
            AddBall(script, shotTime + 0.02f, Sim2DCourt.Hoop(attackRight), true);
            if (!made && rebounder != null)
                AddBall(script, reboundTime, ReboundSpot(attackRight));
            else if (made && andOne)
                AddBall(script, shotTime + 0.06f, Sim2DCourt.FreeThrowShooter(attackRight));
        }
        else if (isTurnover && offender != null)
        {
            AddBall(script, 0.52f, spots[offender.Id]);
            bool outOfBounds = ev.turnoverType == "out_of_bounds";
            bool sideline = ev.turnoverType == "sideline";
            if (outOfBounds || sideline)
            {
                AddBall(script, 0.62f,
                    OutOfBoundsPoint(spots[offender.Id], attackRight, sideline));
                AddCue(script, 0.62f, Sim2DCommentary.OutOfBounds(offender.Name), 0, CueKind.Turnover);
            }
            else if (stealer != null)
            {
                AddBall(script, 0.58f, spots[offender.Id]);
                AddCue(script, 0.58f, Sim2DCommentary.Steal(stealer.Name), 0, CueKind.Turnover);
                script.BallHandlerId = stealer.Id;
                script.BallFollowsHandler = true;
            }
            else
            {
                AddCue(script, 0.58f, $"{offender.Name} pierde el balón.", 0, CueKind.Turnover);
            }
        }
        else if (isBlock && shooter != null)
        {
            AddBall(script, shotTime - 0.04f, spots[shooter.Id]);
            AddBall(script, shotTime + 0.02f, Sim2DCourt.Hoop(attackRight), true);
            if (blocker != null)
                AddBall(script, shotTime + 0.10f, spots[shooter.Id]);
            AddCue(script, shotTime + 0.06f,
                $"{blocker?.Name ?? "La defensa"} tapona.", 0, CueKind.Outcome);
        }

        // ── Ticker de construcción ──
        if (inbounderId > 0)
        {
            var inbounder = Find(offense, inbounderId);
            AddCue(script, 0.08f, Sim2DCommentary.Inbound(inbounder?.Name ?? ""), 0, CueKind.BuildUp);
        }
        else if (outletPasserId > 0)
        {
            var passer2 = Find(offense, outletPasserId);
            AddCue(script, 0.06f, Sim2DCommentary.Transition(passer2?.Name ?? ""), 1, CueKind.BuildUp);
        }
        else if (fastBreak)
        {
            AddCue(script, 0.04f, Sim2DCommentary.Transition(handler.Name), 0, CueKind.BuildUp);
        }
        else if (putback)
        {
            AddCue(script, 0.04f, Sim2DCommentary.SecondChance(handler.Name), 1, CueKind.BuildUp);
        }

        AddCue(script, receiveTime + 0.06f, Sim2DCommentary.BringUp(handler.Name), 1, CueKind.BuildUp);
        AddCue(script, Mathf.Max(0.30f, bringTime - 0.02f), Sim2DCommentary.Organize(handler.Name),
            2, CueKind.BuildUp);

        if (isShot && shooter != null)
        {
            AddCue(script, shotTime, Sim2DCommentary.Shot(shooter.Name, three), 0, CueKind.Shot);
            AddCue(script, shotTime + 0.05f, null, 0, CueKind.Outcome);
        }

        // ── Marcador ──
        int fgPoints = made ? (three ? 3 : 2) : 0;
        if (made)
        {
            int home = previousHome + (offenseIsHome ? fgPoints : 0);
            int away = previousAway + (offenseIsHome ? 0 : fgPoints);
            AddScore(script, shotTime + 0.02f, home, away);
        }

        if (andOne)
        {
            var results = ev.freeThrowResults != null && ev.freeThrowResults.Count > 0
                ? new List<bool>(ev.freeThrowResults)
                : new List<bool> { false };
            // El tiro libre del 2+1 parte del marcador ya actualizado por la canasta;
            // si no, el paso de marcador del libre retrocedería el resultado.
            int ftBaseHome = previousHome + (offenseIsHome ? fgPoints : 0);
            int ftBaseAway = previousAway + (offenseIsHome ? 0 : fgPoints);
            AppendFreeThrowPhase(script, offense, defense, attackRight, ftBaseHome, ftBaseAway,
                offenseIsHome, ftShooter ?? shooter, results, rebounder,
                shotTime + 0.06f, 0.98f);
        }

        if (!made && rebounder != null)
        {
            AddCue(script, reboundTime, Sim2DCommentary.Rebound(rebounder.Name, offensiveRebound),
                1, CueKind.Rebound);
        }

        AddAction(script, shooter?.Id ?? 0);
        AddAction(script, rebounder?.Id ?? 0);
        AddAction(script, blocker?.Id ?? 0);
        AddAction(script, stealer?.Id ?? 0);
        AddAction(script, offender?.Id ?? 0);
        script.DeltasTime = LastScoreTime(script, shotTime + 0.10f);
    }

    // ── Tiros libres ─────────────────────────────────────────────────────────

    static void BuildFreeThrowStandalone(
        Script script,
        GameSimulator.PlayByPlayEvent ev,
        IReadOnlyList<PlayerInfo> offense,
        IReadOnlyList<PlayerInfo> defense,
        bool attackRight,
        int previousHome,
        int previousAway,
        bool offenseIsHome,
        PlayerInfo shooter,
        List<bool> results,
        PlayerInfo rebounder)
    {
        if (shooter == null || results == null || results.Count == 0) return;
        AppendFreeThrowPhase(script, offense, defense, attackRight, previousHome, previousAway,
            offenseIsHome, shooter, results, rebounder, 0.06f, 0.96f);
    }

    static void AppendFreeThrowPhase(
        Script script,
        IReadOnlyList<PlayerInfo> offense,
        IReadOnlyList<PlayerInfo> defense,
        bool attackRight,
        int previousHome,
        int previousAway,
        bool offenseIsHome,
        PlayerInfo shooter,
        List<bool> results,
        PlayerInfo rebounder,
        float start,
        float end)
    {
        if (shooter == null || results == null || results.Count == 0) return;
        int total = results.Count;
        float span = Mathf.Max(0.01f, (end - start) / total);
        Vector2 ftSpot = Sim2DCourt.FreeThrowShooter(attackRight);

        AddPlayerPoint(script, shooter.Id, start, ftSpot);

        var others = offense.Where(p => p.Id != shooter.Id).ToList();
        int laneOffense = 0, laneDefense = 0, outsideOffense = 0, outsideDefense = 0;
        foreach (var p in others)
        {
            Vector2 spot = laneOffense < 2
                ? Sim2DCourt.FreeThrowLane(laneOffense++, false, attackRight)
                : Sim2DCourt.FreeThrowOutside(outsideOffense++, false, attackRight);
            AddPlayerPoint(script, p.Id, start, spot);
        }
        foreach (var d in defense)
        {
            Vector2 spot = laneDefense < 2
                ? Sim2DCourt.FreeThrowLane(laneDefense++, true, attackRight)
                : Sim2DCourt.FreeThrowOutside(outsideDefense++, true, attackRight);
            AddPlayerPoint(script, d.Id, start, spot);
        }

        int madeSoFar = 0;
        for (int i = 0; i < total; i++)
        {
            float attemptStart = start + i * span;
            float contact = attemptStart + span * 0.45f;
            float result = attemptStart + span * 0.62f;
            AddCue(script, attemptStart,
                Sim2DCommentary.FreeThrow(i + 1, total, shooter.Name), 0, CueKind.FreeThrow);
            AddBall(script, attemptStart + span * 0.20f, ftSpot);
            AddBall(script, contact, Sim2DCourt.Hoop(attackRight), true);

            if (results[i])
            {
                madeSoFar++;
                AddCue(script, result, Sim2DCommentary.FreeThrowMade(), 0, CueKind.FreeThrow);
                int home = previousHome + (offenseIsHome ? madeSoFar : 0);
                int away = previousAway + (offenseIsHome ? 0 : madeSoFar);
                AddScore(script, contact, home, away);
                AddBall(script, result + span * 0.08f, ftSpot);
            }
            else
            {
                AddCue(script, result, Sim2DCommentary.FreeThrowMiss(), 0, CueKind.FreeThrow);
                if (i == total - 1)
                {
                    Vector2 reboundSpot = ReboundSpot(attackRight);
                    AddBall(script, result + span * 0.15f, reboundSpot);
                    AddCue(script, result + span * 0.15f,
                        Sim2DCommentary.FreeThrowRebound(rebounder?.Name ?? "", false),
                        1, CueKind.Rebound);
                    if (rebounder != null)
                        AddPlayerPoint(script, rebounder.Id, result + span * 0.10f, reboundSpot);
                    else
                        foreach (var p in others.Take(2))
                            AddPlayerPoint(script, p.Id, result + span * 0.10f, reboundSpot);
                }
                else
                {
                    AddBall(script, result + span * 0.08f, ftSpot);
                }
            }
        }

        script.IsFreeThrow = true;
        script.BallHandlerId = shooter.Id;
        script.BallFollowsHandler = false;
        script.DeltasTime = end - 0.02f;
        AddAction(script, shooter.Id);
    }

    // ── Zonas y emparejamientos ──────────────────────────────────────────────

    static Dictionary<int, Vector2> AssignSpots(IReadOnlyList<PlayerInfo> offense,
        PlayerInfo shooter, bool three, bool attackRight, int side)
    {
        var result = new Dictionary<int, Vector2>();
        var used = new HashSet<Sim2DCourt.Spot>();
        foreach (var p in offense)
        {
            var spot = PreferredSpot(p.Position, side);
            if (used.Contains(spot)) spot = NextFreeSpot(used);
            used.Add(spot);
            result[p.Id] = Sim2DCourt.SpotPosition(spot, attackRight);
        }
        if (shooter != null && result.ContainsKey(shooter.Id))
        {
            result[shooter.Id] = Sim2DCourt.ShotSpot(shooter.Position, three, side, attackRight);
            AvoidShotSpotCollision(offense, result, shooter, used, attackRight);
        }
        return result;
    }

    /// <summary>
    /// Si el tirador se mueve a una zona que ya ocupa un compañero (p. ej. un
    /// ala-pívot que tira desde el poste donde está el pívot), reubica al
    /// compañero en la zona libre más cercana para no amontonar dos jugadores.
    /// </summary>
    static void AvoidShotSpotCollision(IReadOnlyList<PlayerInfo> offense,
        Dictionary<int, Vector2> result, PlayerInfo shooter, HashSet<Sim2DCourt.Spot> used,
        bool attackRight)
    {
        if (!result.TryGetValue(shooter.Id, out var shotSpot)) return;
        var occupied = new List<Vector2>(result.Values);
        foreach (var p in offense)
        {
            if (p.Id == shooter.Id) continue;
            if (Vector2.Distance(result[p.Id], shotSpot) > 0.02f) continue;

            Vector2 original = result[p.Id];
            Sim2DCourt.Spot best = Sim2DCourt.Spot.WingLeft;
            float bestDistance = float.MaxValue;
            foreach (var candidate in SpotOrder)
            {
                if (used.Contains(candidate)) continue;
                Vector2 position = Sim2DCourt.SpotPosition(candidate, attackRight);
                bool free = true;
                foreach (var other in occupied)
                {
                    if (Vector2.Distance(other, position) <= 0.02f) { free = false; break; }
                }
                if (!free) continue;
                float distance = Vector2.Distance(original, position);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = candidate;
            }
            if (bestDistance == float.MaxValue) continue;
            used.Add(best);
            result[p.Id] = Sim2DCourt.SpotPosition(best, attackRight);
            occupied.Add(result[p.Id]);
        }
    }

    static Sim2DCourt.Spot PreferredSpot(string position, int side)
    {
        bool left = side % 2 == 0;
        return position switch
        {
            "PG" => Sim2DCourt.Spot.Top,
            "SG" => left ? Sim2DCourt.Spot.CornerLeft : Sim2DCourt.Spot.CornerRight,
            "SF" => left ? Sim2DCourt.Spot.WingLeft : Sim2DCourt.Spot.WingRight,
            "PF" => left ? Sim2DCourt.Spot.ElbowLeft : Sim2DCourt.Spot.ElbowRight,
            "C" => left ? Sim2DCourt.Spot.PostLeft : Sim2DCourt.Spot.PostRight,
            _ => Sim2DCourt.Spot.WingLeft
        };
    }

    static readonly Sim2DCourt.Spot[] SpotOrder =
    {
        Sim2DCourt.Spot.Top,
        Sim2DCourt.Spot.CornerLeft,
        Sim2DCourt.Spot.CornerRight,
        Sim2DCourt.Spot.WingLeft,
        Sim2DCourt.Spot.WingRight,
        Sim2DCourt.Spot.SlotLeft,
        Sim2DCourt.Spot.SlotRight,
        Sim2DCourt.Spot.ElbowLeft,
        Sim2DCourt.Spot.ElbowRight,
        Sim2DCourt.Spot.PostLeft,
        Sim2DCourt.Spot.PostRight,
        Sim2DCourt.Spot.DunkerLeft,
        Sim2DCourt.Spot.DunkerRight
    };

    static Sim2DCourt.Spot NextFreeSpot(HashSet<Sim2DCourt.Spot> used)
    {
        foreach (var spot in SpotOrder)
            if (!used.Contains(spot)) return spot;
        return Sim2DCourt.Spot.WingLeft;
    }

    public static Dictionary<int, PlayerInfo> AssignMatchups(IReadOnlyList<PlayerInfo> offense,
        IReadOnlyList<PlayerInfo> defense)
    {
        var result = new Dictionary<int, PlayerInfo>();
        var available = new List<PlayerInfo>(offense);
        foreach (var defender in defense.OrderBy(d => PositionOrder(d.Position)))
        {
            var man = available.FirstOrDefault(o => o.Position == defender.Position);
            if (man == null)
            {
                int order = PositionOrder(defender.Position);
                man = available.OrderBy(o => Mathf.Abs(PositionOrder(o.Position) - order)).FirstOrDefault();
            }
            if (man == null) break;
            result[defender.Id] = man;
            available.Remove(man);
        }
        return result;
    }

    static int PositionOrder(string position) => position switch
    {
        "PG" => 0,
        "SG" => 1,
        "SF" => 2,
        "PF" => 3,
        "C" => 4,
        _ => 5
    };

    // ── Utilidades ───────────────────────────────────────────────────────────

    static PlayerInfo Find(IReadOnlyList<PlayerInfo> list, int id)
    {
        if (list == null || id <= 0) return null;
        for (int i = 0; i < list.Count; i++)
            if (list[i].Id == id) return list[i];
        return null;
    }

    static int StatSum(GameSimulator.PlayByPlayEvent ev, string stat)
    {
        if (ev?.deltas == null) return 0;
        int sum = 0;
        foreach (var d in ev.deltas)
            if (d.stat == stat) sum += (int)d.amount;
        return sum;
    }

    static int StatPlayer(GameSimulator.PlayByPlayEvent ev, string stat)
    {
        if (ev?.deltas == null) return 0;
        foreach (var d in ev.deltas)
            if (d.stat == stat && d.amount > 0) return d.player_id;
        return 0;
    }

    static List<bool> SynthesizeFreeThrows(int made, int attempts)
    {
        var results = new List<bool>();
        for (int i = 0; i < attempts; i++) results.Add(i < made);
        return results;
    }

    static PlayerInfo PickInbounder(IReadOnlyList<PlayerInfo> offense, PlayerInfo handler, bool sideline)
    {
        if (sideline)
            return offense.FirstOrDefault(p => p.Position == "SF" && p.Id != handler.Id)
                ?? offense.FirstOrDefault(p => p.Id != handler.Id)
                ?? handler;
        return offense.FirstOrDefault(p => p.Position == "PF" && p.Id != handler.Id)
            ?? offense.FirstOrDefault(p => p.Position == "C" && p.Id != handler.Id)
            ?? offense.FirstOrDefault(p => p.Id != handler.Id)
            ?? handler;
    }

    static PlayerInfo PickOutletPasser(IReadOnlyList<PlayerInfo> offense, PlayerInfo handler)
    {
        return offense.FirstOrDefault(p => p.Position == "C" && p.Id != handler.Id)
            ?? offense.FirstOrDefault(p => p.Position == "PF" && p.Id != handler.Id)
            ?? offense.FirstOrDefault(p => p.Id != handler.Id)
            ?? handler;
    }

    static PlayerInfo PickWing(IReadOnlyList<PlayerInfo> offense, PlayerInfo handler, PlayerInfo shooter)
    {
        return offense.FirstOrDefault(p => p.Position == "SG"
                && p.Id != handler.Id && p.Id != shooter?.Id)
            ?? offense.FirstOrDefault(p => p.Position == "SF"
                && p.Id != handler.Id && p.Id != shooter?.Id)
            ?? offense.FirstOrDefault(p => p.Id != handler.Id && p.Id != shooter?.Id);
    }

    static Vector2 ReboundSpot(bool attackRight)
    {
        return new Vector2(
            Sim2DCourt.HoopX(attackRight) - Sim2DCourt.TowardBasket(attackRight) * 0.03f, 0.5f);
    }

    static Vector2 OutOfBoundsPoint(Vector2 offenderSpot, bool attackRight, bool sideline)
    {
        if (sideline)
            return new Vector2(offenderSpot.x, offenderSpot.y < 0.5f ? 0.02f : 0.98f);
        if (offenderSpot.y < 0.2f) return new Vector2(offenderSpot.x, 0.02f);
        if (offenderSpot.y > 0.8f) return new Vector2(offenderSpot.x, 0.98f);
        return new Vector2(Sim2DCourt.BaselineX(attackRight), 0.5f);
    }

    static string ZoneName(Vector2 spot)
    {
        if (spot.x > 0.86f) return "esquina";
        if (spot.y < 0.32f || spot.y > 0.68f) return "ala";
        return "top";
    }

    static void AddPlayerPoint(Script script, int playerId, float time, Vector2 position)
    {
        if (playerId <= 0) return;
        var path = script.PlayerPaths.FirstOrDefault(p => p.PlayerId == playerId);
        if (path == null)
        {
            path = new PlayerPath { PlayerId = playerId };
            script.PlayerPaths.Add(path);
        }
        if (path.Points.Count > 0 && time <= path.Points[path.Points.Count - 1].Time + 0.0001f)
            time = path.Points[path.Points.Count - 1].Time + 0.001f;
        path.Points.Add(new Point
        {
            Time = Mathf.Clamp01(time),
            X = Mathf.Clamp01(position.x),
            Y = Mathf.Clamp01(position.y)
        });
    }

    static void AddBall(Script script, float time, Vector2 position, bool isShot = false)
    {
        if (script.BallPath.Count > 0
            && time <= script.BallPath[script.BallPath.Count - 1].Time + 0.0001f)
            time = script.BallPath[script.BallPath.Count - 1].Time + 0.001f;
        script.BallPath.Add(new Point
        {
            Time = Mathf.Clamp01(time),
            X = Mathf.Clamp01(position.x),
            Y = Mathf.Clamp01(position.y),
            IsShot = isShot
        });
    }

    static void AddBall(Script script, float time, float x, float y, bool isShot = false)
        => AddBall(script, time, new Vector2(x, y), isShot);

    static void AddBallFollow(Script script, float fromTime, float toTime,
        Vector2 fromPosition, Vector2 toPosition, int samples = 3)
    {
        if (toTime <= fromTime) return;
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            AddBall(script, Mathf.Lerp(fromTime, toTime, t),
                Vector2.Lerp(fromPosition, toPosition, t));
        }
    }

    static void AddCue(Script script, float time, string text, int priority, CueKind kind)
    {
        script.Cues.Add(new Cue
        {
            Time = Mathf.Clamp01(time),
            Text = text,
            Priority = priority,
            Kind = kind
        });
    }

    static void AddScore(Script script, float time, int home, int away)
    {
        script.ScoreSteps.Add(new ScoreStep
        {
            Time = Mathf.Clamp01(time),
            Home = home,
            Away = away
        });
    }

    static void AddAction(Script script, int playerId)
    {
        if (playerId > 0 && !script.ActionIds.Contains(playerId))
            script.ActionIds.Add(playerId);
    }

    static float LastScoreTime(Script script, float fallback)
    {
        return script.ScoreSteps.Count > 0
            ? script.ScoreSteps[script.ScoreSteps.Count - 1].Time
            : fallback;
    }

    static void SortCues(Script script)
    {
        script.Cues.Sort((a, b) => a.Time.CompareTo(b.Time));
    }
}
