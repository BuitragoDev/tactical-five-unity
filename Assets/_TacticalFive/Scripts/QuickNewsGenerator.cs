using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public static class QuickNewsGenerator
{
    // ── Variantes de texto largo (5 por evento) ─────────────

    static readonly string[] HitoMidVariants =
    {
        "¡Mitad de temporada superada! {0} llega al ecuador del calendario regular con {1} victorias y {2} derrotas.\n\n" +
        "El equipo ha mostrado una cara y otra durante las primeras 41 jornadas. Con altibajos en el rendimiento, la plantilla ha demostrado que tiene potencial, pero también que necesita encontrar una regularidad que le permita escalar posiciones.\n\n" +
        "La segunda mitad del calendario será decisiva. Cada partido pesa el doble ahora, y el equipo debe afinar los detalles si quiere lograr un puesto favorable en la fase final.",

        "Se cumplen 41 jornadas. {0} afronta la segunda mitad del calendario con {1} triunfos en su casillero.\n\n" +
        "Los números son discretos, pero el vestuario mantiene la confianza en un proyecto que apuesta por el crecimiento colectivo. Las derrotas han servido para aprender, y las victorias para confirmar que el camino es el correcto.\n\n" +
        "El entrenador ya trabaja en las ajustes tácticos que necesitará el equipo para encarar la recta final. Los próximos compromisos exigirán lo mejor de cada jugador.",

        "El ecuador de la temporada deja a {0} con un balance de {1}–{2}. La segunda vuelta decidirá su destino.\n\n" +
        "Lo que ha mostrado el equipo hasta ahora es un grupo en construcción, con momentos de brillantez que dejan entrever un potencial aún por explotar. La consistencia será la clave en las próximas semanas.\n\n" +
        "Los rivales ya estudian sus tendencias, y el cuerpo técnico deberá innovar para mantener la sorpresa. El calendario no perdona y cada error se paga caro en esta liga.",

        "¡41 partidos jugados! {0} atraviesa el ecuador de la fase regular con {1} victorias y mucho por decidir.\n\n" +
        "La primera mitad ha sido una montaña rusa de emociones. Desde victorias contundentes hasta derrota amargas, el equipo ha vivido de todo. Ahora toca mirar hacia adelante con la misma energía que los ha traído hasta aquí.\n\n" +
        "El descanso del All-Star llega en un momento crucial. La plantilla necesitará recargar energías para afrontar la segunda mitad con la máxima intensidad.",

        "Media temporada completada. {0} cierra la primera vuelta con {1} triunfos y la mirada puesta en la recta final.\n\n" +
        "El balance dejado por las primeras 41 jornadas refleja un equipo que está encontrando su identidad. Las piezas encajan cada vez mejor, y el equipo empieza a dar señales de que puede ser un contendiente serio.\n\n" +
        "La segunda mitad del calendario traerá enfrentamientos directos que podrían definir la temporada. El equipo debe mantener la calma y ejecutar con precisión."
    };

    static readonly string[] HitoEndVariants =
    {
        "La fase regular llega a su fin. {0} cierra con {1} victorias y {2} derrotas en la última jornada.\n\n" +
        "Una temporada de altibajos que ha puesto a prueba la resistencia física y mental de la plantilla. Las lesiones, los malos momentos y las victorias inesperadas han formado parte de un camino que ahora llega a su fin.\n\n" +
        "El equipo mira hacia la postemporada con la esperanza de que el trabajo de toda la temporada se traduzca en resultados en los playoffs. Los aficionados esperan con ilusión lo que viene.",

        "Se acabó el calendario regular. {0} finaliza la campaña con un balance de {1}–{2} y la vista puesta en la postemporada.\n\n" +
        "82 partidos de emoción, esfuerzo y sacrificio han llevado al equipo hasta aquí. Ahora todo se resume en una clasificación que abrirá las puertas de la siguiente fase.\n\n" +
        "El cuerpo técnico ya trabaja en la estrategia para los playoffs, mientras que los jugadores aprovechan los últimos días para afinar su estado físico y mental.",

        "Última jornada completada. {0} dice adiós a la fase regular con {1} triunfos y las sensaciones justas de cara a los playoffs.\n\n" +
        "El equipo cierra una etapa que ha dejado buenos momentos, pero también la sensación de que se pudiera haber sacado más provecho. Las estadísticas no mienten, y el balance final refleja un equipo que debe mejorar en detalles clave.\n\n" +
        "Sin embargo, en los playoffs todo cambia. El equipo sabrá elevar su nivel cuando más importa. La afición está convencida de que pueden lograr algo grande.",

        "¡82 partidos! {0} pone el broche a la temporada regular con {1} victorias en el casillero.\n\n" +
        "Una campaña que arrancó con grandes expectativas y que ha cumplido en líneas generales. El equipo ha demostrado que tiene calidad suficiente para competir con los mejores, y eso se verá reflejado en la fase de eliminatorias.\n\n" +
        "Ahora comienza lo mejor: los playoffs. Todo se resetea, y el equipo partirá con la confianza de saber que puede ganar a cualquiera.",

        "Termina la fase regular. Con {1} victorias, {0} se prepara para lo que viene: todo se decide en la postemporada.\n\n" +
        "Un recorrido largo que ha servido para forjar el carácter de un equipo que no se rinde fácilmente. Las derrotas han enseñado, las victorias han unido, y ahora llega el momento de la verdad.\n\n" +
        "El primer rival en los playoffs será un rival duro, pero el equipo está preparado. La defensa, el rebote y la ejecución en los momentos clave serán los pilares del éxito."
    };

    static readonly string[] WinStreakVariants =
    {
        "{0} no conoce la derrota: {1} victorias consecutivas tras vencer a {2} por {3}.\n\n" +
        "El equipo ha encontrado un ritmo imparable que ha dejado a todos sus rivales en-promedio-abajo. La confianza del vestuario se ha disparado, y cada jugador aporta su granito de arena en un esfuerzo colectivo que es envidiable.\n\n" +
        "La racha no es casualidad: es el resultado de un trabajo diario en los entrenamientos y de una química que ha explotado en el momento justo. El entrenador ha sabido gestionar los minutos y mantener frescos a sus titulares.",

        "{0} sigue imparable: su racha alcanza las {1} victorias seguidas después de superar a {2}.\n\n" +
        "Lo que parecía un momento puntual se ha convertido en una tendencia imparable. El equipo rinde por encima de sus expectativas y se ha colocado en una posición privilegiada en la tabla.\n\n" +
        "El vestuario está unido como nunca, y la confianza en el sistema de juego es total. Cada partido es una oportunidad para seguir sumando y consolidar una posición que sueñan.",

        "¡En racha! {0} encadena {1} triunfos consecutivos y ya es uno de los equipos más en forma de la liga.\n\n" +
        "Los números hablan por sí solos: {1} victorias que han dejado a los rivales pensando en cómo frenar a un equipo que parece imparable. La ofensiva ha sido letal y la defensa ha respondido cuando más ha hecho falta.\n\n" +
        "La afición se ha volcado con el equipo, y el pabellón se ha convertido en una fortaleza. La próxima cita será un test para ver si la racha continúa.",

        "El vestuario de {0} está eufórico: {1} victorias consecutivas y una dinámica que asusta a sus rivales.\n\n" +
        "Cada victoria refuerza la creencia de que este equipo tiene lo necesario para lograr algo grande. La cohesión grupal, la intensidad defensiva y la calidad individual se combinan para crear un producto que enamora a los seguidores.\n\n" +
        "El cuerpo técnico mantiene los pies en el suelo y advierte que la racha no garantiza nada en la postemporada, pero reconoce que el momento es excelente para afianzar la confianza.",

        "{0} ha convertido la victoria en un hábito: {1} triunfos seguidos tras batir a {2} por {3}.\n\n" +
        "Un equipo que ha aprendido a ganar de diferentes maneras. Con comeback, con dominio total, con defensa o con ofensiva, {0} ha demostrado que tiene herramientas para superar cualquier obstáculo.\n\n" +
        "La racha ha colocado al equipo en una posición privilegiada en la tabla de clasificación. Los rivales ya no dan por sentado que les ganarán, y eso supone una ventaja psicológica enorme."
    };

    static readonly string[] LoseStreakVariants =
    {
        "{0} atraviesa su peor momento: {1} derrotas consecutivas y las alarmas encendidas.\n\n" +
        "Lo que empezó como un tropiezo aislado se ha convertido en una espiral de resultados negativos que amenaza con arruinar la temporada. El vestuario empieza a mostrar signos de nerviosismo, y la paciencia de la afición se agota.\n\n" +
        "El entrenador deberá tomar decisiones difíciles en los próximos días. Los cambios tácticos y de actitud son urgentes si el equipo quiere salir de este agujero antes de que sea demasiado tarde.",

        "La crisis se instala en {0}: ya son {1} derrotas seguidas y el vestuario empieza a resquebrajarse.\n\n" +
        "Las palabras del capitán en rueda de prensa lo dicen todo: 'No podemos seguir así'. El equipo ha perdido la confianza en sí mismo, y eso se traduce en decisiones erróneas en los momentos clave.\n\n" +
        "La dirección del club ha convocado una reunión de urgencia para analizar la situación. Se esperan cambios en el quinteto titular y posibles ajustes en el cuerpo técnico.",

        "Mala racha en {0}: {1} tropiezos consecutivos que le cuestan posiciones en la clasificación.\n\n" +
        "Cada derrota pesa el doble cuando se acumulan. El equipo ha perdido terreno en la tabla, y lo que parecía una clasificación cómoda se ha convertido en una lucha por mantener las opciones.\n\n" +
        "El calendario no se detiene, y los próximos rivales aprovecharán la debilidad del equipo para sacar ventaja. La reacción debe ser inmediata.",

        "{0} no levanta cabeza. Con {1} derrotas seguidas, la paciencia de la afición se agota.\n\n" +
        "El pabellón, antes un refugio, se ha convertido en un lugar de silencio y frustración. Los aficionados exigen respuestas, y el equipo las debe en la cancha.\n\n" +
        "La situación es delicada, pero no irrecuperable. La temporada aún está a tiempo de enderezarse, pero cada partido que pierda complicará más las cosas.",

        "Los números no engañan: {0} suma {1} derrotas consecutivas y necesita una reacción inmediata.\n\n" +
        "El balance de las últimas semanas es devastador. El equipo ha perdido su identidad de juego y ha dejado de competir con la intensidad que lo caracterizaba. Las figuras del equipo están bajo un nivel que no se esperaba.\n\n" +
        "El próximo partido es una final. El equipo debe encontrar la chispa que lo encendió al inicio de temporada y demostrar que esta racha es solo un mal momento."
    };

    static readonly string[] UpsetVariants =
    {
        "¡Campanada en la liga! {0} ({3} de valoración) sorprende a {1} ({4}) por {2}.\n\n" +
        "Nadie esperaba este resultado, pero {0} ha demostrado que en el baloncesto todo es posible. Con una actuación colectiva brillante, el equipo ha desarmado a un rival que llegaba como claro favorito.\n\n" +
        "La defensa ha sido la clave: contener la ofensiva del rival y castigar cada error con un contraataque letal. Una victoria que da moral para los próximos compromisos.",

        "Nadie daba un duro por {0}, pero se lleva el triunfo ante {1} por {2}. ¡Qué golpe sobre la mesa!\n\n" +
        "El equipo ha superado todas las expectativas con una actuación que recordará durante mucho tiempo. La intensidad defensiva y la eficiencia en ataque han sido los pilares de una victoria que genera optimismo.\n\n" +
        "Este tipo de resultados construyen character en el vestuario. {0} ha demostrado que puede competir con cualquiera cuando pone todo de su parte.",

        "El favoritismo no sirvió de nada: {1} ({4}) cae ante {0} ({3}) por {2}.\n\n" +
        "Un partido que parecía sentenciado por las casillas de apuestas ha dado un giro inesperado. {0} ha jugado sin presión y con una libertad que ha dejado a {1} sin respuestas.\n\n" +
        "La victoria tiene un sabor especial cuando se logra contra todo pronóstico. El equipo acumula un triunfo que puede ser el punto de inflexión de su temporada.",

        "¡Sorpresa mayúscula! {0}, sin nada que perder, desarbola a {1} por {2} y firma la campanada de la jornada.\n\n" +
        "El equipo ha tirado de orgullo y ha demostrado que la clasificación no lo es todo en la cancha. Con una energía contagiosa y una ejecución casi perfecta, {0} ha dejado a todos con la boca abierta.\n\n" +
        "Los aficionados del equipo ganador celebran un triunfo que habrá que recordar. A veces, los equipos más débiles son los más peligrosos.",

        "Los pronósticos saltaron por los aires: {0} ({3}) supera al favorito {1} ({4}) por {2}.\n\n" +
        "Una noche mágica para {0} que demostró que la preparación y la actitud pueden superar cualquier diferencia en el papel. El equipo rival, por su parte, deberá analizar qué falló para no repetir errores.\n\n" +
        "Esta victoria no solo da puntos: da esperanza, da confianza y da razones para creer en un equipo que ha dado lo mejor de sí cuando más lo necesitaba."
    };

    static readonly string[] TripleDoubleVariants =
    {
        "{0} firma un triple-doble de ensueño ({1}) en el duelo ante {2}.\n\n" +
        "Una actuación que quedará en los libros de historia del equipo. {0} ha dominado todos los aspectos del juego: anotación, rebote, asistencias y una presencia en la pintura que ha sido imparable para el rival.\n\n" +
        "El jugador ha estado en las nubes durante todo el partido, tomando decisiones acertadas en cada posesión y elevando a sus compañeros con pases de otro nivel. Una noche para el recuerdo.",

        "Noche histórica para {0}: triple-doble ({1}) contra {2} para el recuerdo.\n\n" +
        "Desde el primer cuarto se notó que era una noche especial. {0} imprimía un ritmo frenético que el rival no podía seguir, y cada acción de su juego parecía sacada de un manual de perfección.\n\n" +
        "El triple-doble se ha completado con una eficiencia notable. Los aficionados presentes en el pabellón han sido testigos de algo excepcional que no olvidarán fácilmente.",

        "{0} domina todas las facetas del juego: triple-doble ({1}) ante {2}.\n\n" +
        "Anotar, rebotear, asistir, robar… {0} ha hecho de todo en una actuación que deja sin palabras. Su impacto en el partido ha sido total, y su equipo se ha beneficiado de cada una de sus acciones.\n\n" +
        "El cuerpo técnico no ha podido contener la sonrisa al ver la actuación de su jugador franquicia. Este tipo de noches justifican todo el esfuerzo de la temporada.",

        "Solo hay una palabra para la actuación de {0}: total. Triple-doble ({1}) frente a {2}.\n\n" +
        "Un dominio absoluto que ha dejado al rival completamente superado. {0} ha liderado a su equipo con una autoridad que impone respeto, tomando el control del partido desde el primer minuto.\n\n" +
        "Los números son impresionantes, pero lo que no miden las estadísticas es la influencia psicológica que tiene un jugador en este estado sobre el rival y sobre sus propios compañeros.",

        "Hacer de todo y hacerlo bien. {0} logra un triple-doble ({1}) en el duelo contra {2}.\n\n" +
        "La consistencia de {0} ha sido la clave de una victoria que parece sencilla en el marcador, pero que en la cancha fue una batalla constante. El triple-doble es la mejor carta de presentación de un jugador que está en su mejor momento.\n\n" +
        "El equipo fluye cuando {0} está en este estado. La conexión con los compañeros es telepática y la ejecución ofensiva alcanza cotas de excelencia que asombran a propios y extraños."
    };

    static readonly string[] ExplosionVariants =
    {
        "{0} se sale: {1} puntos ante {2} en una exhibición ofensiva de otro nivel.\n\n" +
        "Una noche en la que todo lo que lanzaba entraba. {0} ha encontrado su ritmo desde el primer tiro y no ha parado de anotar en todo el partido. La defensa rival ha sufrido cada una de sus acciones con impotencia.\n\n" +
        "Los {1} puntos son el colofón de una actuación dominante que ha dejado a los aficionados de pie en el pabellón. Este es el tipo de partido que define una temporada.",

        "La noche es de {0}: {1} puntos ante {2} para enmarcar.\n\n" +
        "Desde el primer cuarto se intuía que iba a ser especial, y así ha sido. {0} ha anotado de todas las maneras posibles: triples, penetraciones, tiros medios, tiros libres… una exhibición que ha dejado al rival sin respuestas.\n\n" +
        "El equipo se ha alimentado de la energía de su estrella y ha jugado con una confianza contagiosa. Una victoria que tiene el sello indeleble de {0}.",

        "Anotar hasta la extenuación: {0} firma {1} puntos contra {2}.\n\n" +
        "Un partido que pasará a la historia por la capacidad anotadora de {0}. El jugador ha superado todas las barreras defensivas con una determinación feroz, y cada canasta ha levantado más la moral del equipo.\n\n" +
        "El rival ha intentado todo: cambios defensivos, dobles marcajías, ayudas… nada ha servido para frenar a {0}. Una noche que recordará durante mucho tiempo.",

        "{0} pone la liga en alerta con {1} puntos ante {2}.\n\n" +
        "Un aviso a todos los equipos de la competición: {0} está en un nivel que pocos pueden alcanzar. La capacidad de anotación mostrada en este partido demuestra que es uno de los jugadores más letales de la liga.\n\n" +
        "El equipo rival ha sufrido en carne propia la辣idad ofensiva de {0}. Los próximos rivales tendrán que idear un plan especial para intentar contenerlo.",

        "Exhibición de {0}: {1} puntos frente a {2} y el marcador temblando en cada posesión.\n\n" +
        "Un festival anotador que ha dejado a todos sin aliento. {0} ha jugado con una libertad absoluta, tomando tiros desde cualquier posición y convirtiéndolos con una facilidad que asombra.\n\n" +
        "El pabellón ha vibrado con cada canasta, y los aficionados han vivido una noche que será recordada durante mucho tiempo. Este es el tipo de partidos que hacen que el baloncesto sea el deporte más emocionante del mundo."
    };

    static string PickVariant(string[] variants, params object[] args)
    {
        return string.Format(variants[UnityEngine.Random.Range(0, variants.Length)], args);
    }

    public static void Generate(ManagerData manager, TeamData myTeam, SeasonData season, List<GameData> gamesToday, int gameDay, string gameDate)
    {
        if (season == null || gamesToday == null || gamesToday.Count == 0) return;
        if (season.phase == "finished" || season.phase == "preseason") return;

        var allStandingsGames = DatabaseManager.Instance.GetStandingsGames(manager.id);
        var allPlayedGames = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == manager.id
                   && g.is_played == 1
                   && (g.game_type == "regular" || g.game_type == "playin" || g.game_type == "playoff"))
            .OrderBy(g => g.game_day)
            .ThenBy(g => g.id)
            .ToList();
        var allTeams = DatabaseManager.Instance.GetAllTeams().ToDictionary(t => t.id);
        var teamRealAvg = allTeams.Values.ToDictionary(
            t => t.id,
            t => {
                var players = DatabaseManager.Instance.GetPlayersByTeam(t.id);
                return players.Count > 0 ? (int)Math.Round(players.Average(p => p.GetCalculatedAverage())) : 50;
            });
        int newsCount = 0;

        int myTeamGames = allStandingsGames.Count(g =>
            (g.home_team_id == myTeam.id || g.away_team_id == myTeam.id) && g.is_played == 1);
        if ((myTeamGames == 41 || myTeamGames == 82) && season.phase == "regular")
        {
            int wins = allStandingsGames.Count(g =>
                (g.home_team_id == myTeam.id || g.away_team_id == myTeam.id) && g.is_played == 1
                && ((g.home_team_id == myTeam.id && g.home_score > g.away_score)
                    || (g.away_team_id == myTeam.id && g.away_score > g.home_score)));
            int losses = myTeamGames - wins;
            string body = myTeamGames == 41
                ? PickVariant(HitoMidVariants, myTeam.name, wins, losses)
                : PickVariant(HitoEndVariants, myTeam.name, wins, losses);
            string title = myTeamGames == 41 ? "Hito: Mitad de temporada" : "Hito: Fin de fase regular";
            SaveNews(manager, gameDay, gameDate, title, body, NewsType.HITO, NewsCategory.LIGA);
            newsCount++;
        }

        foreach (var game in gamesToday.OrderBy(g => g.id))
        {
            if (newsCount >= 2) break;

            if (!allTeams.TryGetValue(game.home_team_id, out var homeTeam)) continue;
            if (!allTeams.TryGetValue(game.away_team_id, out var awayTeam)) continue;

            int homeStreak = ComputeStreak(allPlayedGames, game.home_team_id);
            int awayStreak = ComputeStreak(allPlayedGames, game.away_team_id);

            if (newsCount < 2 && homeStreak >= 5 && homeTeam.id != myTeam.id)
            {
                string score = $"{game.home_score}-{game.away_score}";
                if (SaveNews(manager, gameDay, gameDate, $"Racha de {homeTeam.name}: {homeStreak} victorias",
                    PickVariant(WinStreakVariants, homeTeam.name, homeStreak, awayTeam.name, score),
                    NewsType.RACHA_VICTORIAS, NewsCategory.LIGA))
                    { newsCount++; continue; }
            }
            if (newsCount < 2 && awayStreak >= 5 && awayTeam.id != myTeam.id)
            {
                string score = $"{game.home_score}-{game.away_score}";
                if (SaveNews(manager, gameDay, gameDate, $"Racha de {awayTeam.name}: {awayStreak} victorias",
                    PickVariant(WinStreakVariants, awayTeam.name, awayStreak, homeTeam.name, score),
                    NewsType.RACHA_VICTORIAS, NewsCategory.LIGA))
                    { newsCount++; continue; }
            }

            if (newsCount < 2 && homeStreak <= -5 && homeTeam.id != myTeam.id)
            {
                if (SaveNews(manager, gameDay, gameDate, $"Mala racha de {homeTeam.name}: {-homeStreak} derrotas",
                    PickVariant(LoseStreakVariants, homeTeam.name, -homeStreak),
                    NewsType.MALA_RACHA, NewsCategory.LIGA))
                    { newsCount++; continue; }
            }
            if (newsCount < 2 && awayStreak <= -5 && awayTeam.id != myTeam.id)
            {
                if (SaveNews(manager, gameDay, gameDate, $"Mala racha de {awayTeam.name}: {-awayStreak} derrotas",
                    PickVariant(LoseStreakVariants, awayTeam.name, -awayStreak),
                    NewsType.MALA_RACHA, NewsCategory.LIGA))
                    { newsCount++; continue; }
            }

            int homeAvg = teamRealAvg[game.home_team_id];
            int awayAvg = teamRealAvg[game.away_team_id];
            int medDiff = Mathf.Abs(homeAvg - awayAvg);
            bool homeFav = homeAvg > awayAvg;
            bool homeWon = game.home_score > game.away_score;
            if (newsCount < 2 && medDiff >= 15 && ((homeFav && !homeWon) || (!homeFav && homeWon)))
            {
                var winner = homeWon ? homeTeam : awayTeam;
                var loser = homeWon ? awayTeam : homeTeam;
                int winAvg = homeWon ? homeAvg : awayAvg;
                int loseAvg = homeWon ? awayAvg : homeAvg;
                string score = $"{game.home_score}-{game.away_score}";
                if (SaveNews(manager, gameDay, gameDate, $"Campanada: {winner.name} vence a {loser.name}",
                    PickVariant(UpsetVariants, winner.name, loser.name, score, winAvg, loseAvg),
                    NewsType.CAMPANADA, NewsCategory.LIGA))
                    { newsCount++; continue; }
            }

            if (newsCount >= 2) continue;

            var gameStats = DatabaseManager.Instance.GetGamePlayerStats(game.id);
            if (gameStats == null) continue;

            foreach (var ps in gameStats)
            {
                if (newsCount >= 2) break;

                if (ps.triple_double == 1)
                {
                    var player = DatabaseManager.Instance.GetPlayerById(ps.player_id);
                    if (player == null) continue;
                    var opponent = game.home_team_id == player.team_id ? awayTeam : homeTeam;

                    var tdParts = new List<string>();
                    if (ps.points >= 10) tdParts.Add($"{ps.points} pts");
                    if (ps.rebounds >= 10) tdParts.Add($"{ps.rebounds} reb");
                    if (ps.assists >= 10) tdParts.Add($"{ps.assists} ast");
                    if (ps.steals >= 10) tdParts.Add($"{ps.steals} rob");
                    if (ps.blocks >= 10) tdParts.Add($"{ps.blocks} tap");

                    string playerName = $"{player.first_name} {player.last_name}";
                    if (SaveNews(manager, gameDay, gameDate, $"Triple-Doble: {playerName}",
                        PickVariant(TripleDoubleVariants, playerName, string.Join(" + ", tdParts.Take(3)), opponent.name),
                        NewsType.TRIPLE_DOBLE, NewsCategory.LIGA))
                        { newsCount++; continue; }
                }

                if (ps.points >= 40)
                {
                    var player = DatabaseManager.Instance.GetPlayerById(ps.player_id);
                    if (player == null) continue;
                    var opponent = game.home_team_id == player.team_id ? awayTeam : homeTeam;
                    string playerName = $"{player.first_name} {player.last_name}";
                    if (SaveNews(manager, gameDay, gameDate, $"Explosión: {playerName} ({ps.points} pts)",
                        PickVariant(ExplosionVariants, playerName, ps.points, opponent.name),
                        NewsType.EXPLOSION, NewsCategory.LIGA))
                        { newsCount++; continue; }
                }
            }
        }
    }

    static int ComputeStreak(List<GameData> allGames, int teamId)
    {
        var teamGames = allGames
            .Where(g => g.home_team_id == teamId || g.away_team_id == teamId)
            .OrderByDescending(g => g.game_day)
            .ThenByDescending(g => g.id)
            .ToList();

        if (teamGames.Count == 0) return 0;

        int streak = 0;
        bool firstWon = false;
        bool firstSet = false;

        foreach (var g in teamGames)
        {
            bool isHome = g.home_team_id == teamId;
            int teamScore = isHome ? g.home_score : g.away_score;
            int oppScore = isHome ? g.away_score : g.home_score;
            bool won = teamScore > oppScore;

            if (!firstSet)
            {
                firstWon = won;
                firstSet = true;
                streak = 1;
                continue;
            }

            if (won == firstWon)
                streak++;
            else
                break;
        }

        return firstWon ? streak : -streak;
    }

    static bool SaveNews(ManagerData manager, int gameDay, string gameDate,
        string title, string body, string newsType, string category)
    {
        var existing = DatabaseManager.Instance.Db.Table<MessageData>()
            .FirstOrDefault(m => m.title == title && m.body == body && m.game_day == gameDay);
        if (existing != null) return false;

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = manager.id,
            sender_type = 2,
            sender_id = 0,
            title = title,
            body = body,
            game_day = gameDay,
            game_date = gameDate,
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0,
            category = category,
            news_type = newsType
        });
        return true;
    }
}
