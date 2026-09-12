using System.Collections.Generic;
using System.Linq;

public static class NewsTexts
{
    // ── LIGA ── QuickNews (variantes largas en QuickNewsGenerator)

    // ── PARTIDOS / RESULTADOS ──

    public static string Resultado(string teamA, int scoreA, string teamB, int scoreB,
        string date, string topScorerName, int topScorerPts)
    {
        string ganador = scoreA > scoreB ? teamA : teamB;
        string perdedor = scoreA > scoreB ? teamB : teamA;
        int maxPts = System.Math.Max(scoreA, scoreB);
        int minPts = System.Math.Min(scoreA, scoreB);
        return $"El {ganador} se impone a {perdedor} por {maxPts}–{minPts} en un partido que se definió en los últimos minutos.\n\n" +
            $"{topScorerName} fue la figura con {topScorerPts} puntos, liderando la carga ofensiva de su equipo y manteniendo a raya al rival durante las posesiones más disputadas.\n\n" +
            $"Con este resultado, {ganador} suma una victoria importante en su camino por la clasificación, mientras que {perdedor} deberá reaccionar en la próxima jornada para no perder el ritmo en la tabla.";
    }

    public static string GLeagueChampion(string teamName, string rivalName, int scoreA, int scoreB)
    {
        string ganador = scoreA > scoreB ? teamName : rivalName;
        string perdedor = scoreA > scoreB ? rivalName : teamName;
        return $"¡{ganador} es el campeón de la G-League!\n\n" +
            $"En una Gran Final que se decidió en el tercer y definitivo duelo, {ganador} superó a {perdedor} por {System.Math.Max(scoreA, scoreB)}–{System.Math.Min(scoreA, scoreB)} y se lleva el título de la liga de desarrollo.\n\n" +
            $"Un plantilla que apostó por el desarrollo de sus prospectos y jugadores asignados cosechó los frutos más dulces de la temporada. Este campeón refuerza la inversión en la cantera y envía un mensaje claro a la franquicia sobre el futuro del proyecto.";
    }

    // ── LESIONES ──

    public static string Recuperacion(string playerName, string injuryType, int daysOut)
    {
        return $"{playerName} ha superado la lesión de tipo \"{injuryType}\" que lo mantuvo fuera de las canchas durante {daysOut} días.\n\n" +
            $"El cuerpo médico confirmó que el jugador está en plena condición física y apto para volver a los entrenamientos con normalidad. Su regreso supone un refuerzo importante para la plantilla, que ha debido adaptarse a su ausencia en las últimas semanas.\n\n" +
            $"El entrenador deberá decidir si lo reintegra de forma gradual en la rotación o si le otorga minutos completos desde el primer partido de su regreso. La espera ha valido la pena: el equipo recupera a una pieza clave.";
    }

    public static string Lesion(string playerName, string injuryType, int daysOut, bool isIr)
    {
        string textoIr = isIr
            ? "\n\nDada la gravedad de la lesión, se ha decidido activar la reserva de lesionados. El jugador permanecerá fuera de la convocatoria mientras completa su rehabilitación y no computará en la nómina del tope salarial."
            : "\n\nEl cuerpo médico trabajará para acelerar su recuperación, aunque se espera que pierda al menos los próximos compromisos.";
        return $"Se ha confirmado que {playerName} sufre una lesión de tipo \"{injuryType}\" que lo mantendrá fuera de las canchas durante aproximadamente {daysOut} días.\n\n" +
            $"La lesión se produjo durante el último encuentro y las pruebas médicas realizadas esta mañana han revelado la extensión del daño. El jugador ya ha comenzado el proceso de rehabilitación bajo supervisión del staff médico.{textoIr}" +
            "\n\nLa ausencia de " + playerName + " obliga al cuerpo técnico a reajustar la rotación y buscar alternativas para cubrir su puesto en la pista.";
    }

    public static string ReservaIR(string playerName, string injuryType, int daysOut)
    {
        return $"Se ha activado la reserva de lesionados para {playerName}, quien sufre una lesión de tipo \"{injuryType}\" con un tiempo estimado de recuperación de {daysOut} días.\n\n" +
            $"El jugador quedará fuera de la convocatoria activa y no computará en el tope salarial mientras completa su rehabilitación. Su ausencia deja un hueco importante en la plantilla que deberá ser cubierto por los jugadores disponibles.\n\n" +
            $"El cuerpo médico y el entrenador trabajarán conjuntamente para diseñar un plan de recuperación que permita al jugador volver en las mejores condiciones posibles.";
    }

    // ── TRASPASOS ──

    public static string Intercambio(string teamA, string playersA, string teamB, string playersB,
        string picksDesc)
    {
        return $"Se ha concretado un intercambio entre {teamA} y {teamB} que remueve el mapa de la liga.\n\n" +
            $"{teamA} envía: {playersA}\n" +
            $"{teamB} envía: {playersB}\n" +
            (string.IsNullOrEmpty(picksDesc) ? "" : $"Incluidos en la operación: {picksDesc}\n") +
            $"\nUna operación que busca reequilibrar ambas plantillas. {teamA} apostará por la pieza recibida para cubrir una necesidad inmediata, mientras que {teamB} opta por la construcción a mediano plazo o por refrescar su dinámica de vestuario.";
    }

    public static string Traspaso(string teamA, string playerA, string teamB, string playerB,
        string picksDesc)
    {
        return $"Traspaso confirmado: {teamA} y {teamB} han cerrado una operación que intercambia talento entre ambas franquicias.\n\n" +
            $"{teamA} envía a {playerA} a {teamB}, que a su vez envía a {playerB}.\n" +
            (string.IsNullOrEmpty(picksDesc) ? "" : $"La operación también incluye: {picksDesc}\n") +
            $"\nUna movida que refleja las prioridades de ambos equipos. {teamA} decide dar un giro a su plantilla, mientras que {teamB} suma una pieza que encaja en su proyecto.";
    }

    public static string SignAndTrade(string playerName, string fromTeam, string toTeam, int salary)
    {
        return $"Se ha completado un Sign & Trade de {playerName} entre {fromTeam} y {toTeam}.\n\n" +
            $"{playerName} firma un nuevo contrato con {toTeam} por ${salary:N0} anuales y es traspasado de inmediato. {fromTeam} recibe a cambio una compensación que podría incluir jugadores o picks futuros.\n\n" +
            $"Esta operación permite a {toTeam} incorporar una estrella sin sacrificar su plantilla, mientras que {fromTeam} obtiene un retorno por un jugador que podría haber perdido gratis en el mercado de agentes libres.";
    }

    public static string HardCap(string playerName, string teamName)
    {
        return $"El {teamName} ha activado el hard cap tras la incorporación de {playerName}.\n\n" +
            $"A partir de este momento, el equipo no podrá superar el umbral del primer apron salarial en ninguna operación. Esto limita severamente la capacidad del equipo para realizar traspasos, firmar agentes libres o utilizar excepciones del tope.\n\n" +
            $"Una decisión que implica un compromiso total con la plantilla actual. El equipo asume las restricciones a cambio de haber conseguido una pieza que considera clave para sus aspiraciones.";
    }

    public static string DeadlineSemana()
    {
        return $"La fecha límite de traspasos se acerca y el mercado está en ebullición.\n\n" +
            $"Los equipos tienen solo unos días para cerrar las operaciones que definirán sus aspiraciones de postemporada. Las negociaciones se intensifican, las llamadas entre gerentes generales no cesan y los rumores se multiplican.\n\n" +
            $"Los contendores buscan ese último refuerzo que les dé la diferencia, mientras que los equipos en reconstrucción aprovechan para deshacerse de veteranos a cambio de picks y jóvenes prospectos. ¡No te pierdas lo que viene!";
    }

    public static string Buyout(string playerName, string teamName)
    {
        return $"El {teamName} ha completado la rescisión del contrato de {playerName} mediante un buyout.\n\n" +
            $"El jugador recibirá una parte de su salario garantizado y quedará libre para firmar con cualquier equipo. Una operación de beneficio mutuo: {teamName} libera espacio en su plantilla y el jugador busca un nuevo destino donde pueda aportar.\n\n" +
            $"Se espera que varios equipos interesados se muevan en las próximas horas para hacerse con los servicios del veterano. Su experiencia y capacidad de aportar en minutos limitados lo convierten en un candidato atractivo para los contendores.";
    }

    // ── FICHAJES / CONTRATOS ──

    public static string Fichaje(string playerName, string teamName, int salary, int years)
    {
        return $"El {teamName} ha fichado a {playerName} por {years} temporada{(years > 1 ? "s" : "")} a un ritmo de ${salary:N0} anuales.\n\n" +
            $"{playerName} se une a la plantilla con la esperanza de ser un factor diferencial. Su llegada refuerza una posición que el equipo buscaba cubrir desde hace semanas y aporta experiencia y calidad a una rotación que promete ser competitiva.\n\n" +
            $"Para la franquicia, es una inversión que equilibra el presente con el futuro. El contrato permite proyectar la estructura salarial a medio plazo sin comprometer la flexibilidad necesaria para futuras movidas.";
    }

    public static string FichajeRechazado(string playerName, string teamName, string reason)
    {
        return $"{playerName} ha rechazado la oferta del {teamName}.\n\n" +
            $"El jugador decidió no aceptar las condiciones propuestas por la franquicia, alegando {reason}. La negociación se prolongó durante varios días, pero finalmente las partes no llegaron a un acuerdo.\n\n" +
            $"El {teamName} deberá buscar alternativas en el mercado de agentes libres o considerar un traspaso para cubrir la plaza que quedó vacía. El jugador, por su parte, continúa evaluando las opciones que tiene sobre la mesa.";
    }

    public static string FichajeCancelado(string playerName, string teamName)
    {
        return $"La operación de fichaje de {playerName} por el {teamName} ha sido cancelada.\n\n" +
            $"A pesar de las negociaciones avanzadas, la operación no se concretó debido a desacuerdos en los términos del contrato. Ambas partes decidieron no forzar una situación que no terminaba de cuajar.\n\n" +
            $"El equipo continúa buscando opciones para reforzar su plantilla, mientras que el jugador deberá encontrar un nuevo destino. El mercado sigue abierto y ambas opciones siguen sobre la mesa.";
    }

    public static string Renovacion(string playerName, string teamName, int salary, int years)
    {
        return $"{playerName} ha renovado su contrato con el {teamName} por {years} temporada{(years > 1 ? "s" : "")} más.\n\n" +
            $"El acuerdo asegura que el jugador seguirá vistiendo la camiseta del equipo en los próximos años a un ritmo de ${salary:N0} anuales. Una decisión que pone fin a las especulaciones sobre su futuro y que confirma el compromiso mutuo entre jugador y franquicia.\n\n" +
            $"Para el {teamName}, retener a {playerName} era una prioridad. Su presencia en la plantilla garantiza continuidad y estabilidad en un proyecto que apunta a ser competitivo en las próximas temporadas.";
    }

    public static string OfertaRechazada(string playerName, string teamName)
    {
        return $"El {teamName} ha rechazado la oferta de renovación presentada por {playerName}.\n\n" +
            $"Las partes no han logrado llegar a un acuerdo sobre los términos del nuevo contrato. El equipo consideró que las condiciones exigidas por el jugador no se ajustaban a la estructura salarial del proyecto, y decidió no hacer una contraoferta.\n\n" +
            $"{playerName} quedará como agente libre al finalizar la temporada actual y deberá buscar un nuevo equipo. Se espera que varios interesados se presente en las próximas semanas.";
    }

    public static string RefirmaCancelada(string playerName, string teamName)
    {
        return $"La re-firma de {playerName} con el {teamName} ha sido cancelada.\n\n" +
            $"A pesar de las intenciones iniciales de ambas partes, la negociación se rompió sin llegar a un acuerdo. El jugador no aceptó las condiciones propuestas y la franquicia decidió no modificar su oferta.\n\n" +
            $"Con esta cancelación, {playerName} se incorpora al grupo de agentes libres disponibles cuando finalice la temporada. El mercado de verano promete ser movido.";
    }

    public static string RefirmaEnviada(string playerName, string teamName)
    {
        return $"El {teamName} ha enviado una oferta de renovación a {playerName}.\n\n" +
            $"La propuesta busca asegurar la continuidad del jugador en la plantilla por las próximas temporadas. Los derechos de Bird de la franquicia le permiten ofrecer un contrato por encima del tope salarial, una ventaja que otros equipos no pueden igualar.\n\n" +
            $"El jugador tiene 7 días para evaluar la propuesta. Si la acepta, se cerrará una operación que otorga estabilidad a ambos lados. Si la rechaza, se abrirá el mercado de agentes libres para él.";
    }

    public static string AgenteLibre(string playerName, string teamName)
    {
        return $"El {playerName} se convierte oficialmente en agente libre tras no llegar a un acuerdo con el {teamName}.\n\n" +
            $"La franquicia no pudo retener al jugador y ahora deberá afrontar su salida sin obtener compensación a cambio. El agente del jugador ya ha comenzado a sondear el mercado para encontrar la mejor oferta posible.\n\n" +
            $"Se esperan varias ofertas para {playerName} en las próximas horas. Su rendimiento en la temporada actual ha despertado el interés de múltiples equipos que buscan reforzar sus aspiraciones de postemporada.";
    }

    public static string Entrenamiento(string playerName, string improvement, int attribute, int newVal)
    {
        return $"{playerName} ha completado un ciclo de entrenamiento especializado en {improvement}.\n\n" +
            $"El trabajo individualizado con el cuerpo técnico ha dado sus frutos: su capacidad en esta faceta ha mejorado notablemente, alcanzando un nivel de {attribute} puntos en su valoración general.\n\n" +
            $"Este tipo de sesiones son fundamentales para el desarrollo de los jugadores. La inversión en entrenamiento especializado se traduce en mejoras tangibles que impactan directamente en el rendimiento del equipo sobre la pista.";
    }

    public static string Despedido(string playerName, string teamName)
    {
        return $"El {teamName} ha decidido despedir a {playerName}.\n\n" +
            $"La franquicia ha rescindido el contrato del jugador tras una evaluación del rendimiento y la dinámica de vestuario. El equipo consideró que era necesario un cambio para mejorar el equilibrio del plantilla.\n\n" +
            $"El jugador queda libre para firmar con cualquier equipo. Se abre un nuevo capítulo en su carrera, mientras que el {teamName} busca una solución interna o externa para cubrir la plaza que ha quedado vacía.";
    }

    // ── EMPLEADOS ──

    public static string EmpleadoContratado(string employeeName, string position, string teamName)
    {
        return $"El {teamName} ha contratado a {employeeName} para el puesto de {position}.\n\n" +
            $"La incorporación del nuevo miembro del staff refuerza el proyecto deportivo de la franquicia. Su experiencia y conocimientos aportarán un valor añadido a las operaciones diarias del equipo.\n\n" +
            $"Con este movimiento, el {teamName} completa la estructura de su cuerpo técnico y se prepara para afrontar los retos de la temporada con un equipo humano sólido y preparado.";
    }

    public static string EmpleadoDespedido(string employeeName, string position, string teamName)
    {
        return $"El {teamName} ha despedido a {employeeName} del puesto de {position}.\n\n" +
            $"La franquicia ha decidido prescindir de los servicios del empleado tras una evaluación de su rendimiento. El equipo consideró que era necesario un cambio en esta área para mejorar los resultados deportivos.\n\n" +
            $"Se abrirá un proceso de búsqueda para encontrar un sustituto que pueda aportar nuevas ideas y una gestión más eficaz del puesto vacante.";
    }

    // ── FINANZAS ──

    public static string Nomina(string teamName, decimal totalPayroll, decimal luxuryTax)
    {
        string taxText = luxuryTax > 0
            ? $" El equipo también ha abonado ${luxuryTax:N0} en impuesto de lujo, lo que supone un golpe adicional a las arcas del club."
            : "";
        return $"Se han procesado las nóminas mensuales del {teamName} por un total de ${totalPayroll:N0}.{taxText}\n\n" +
            $"El pago cubre los salarios de toda la plantilla y el cuerpo técnico. La gestión financiera del equipo se mantiene estable, aunque la facturación recurrente obliga a mantener un equilibrio constante entre ingresos y gastos.\n\n" +
            $"Con este desembolso, el equipo cumple con sus obligaciones salariales y mantiene la confianza de los jugadores y empleados. Las arcas del club se ajustan al presupuesto planificado para el mes.";
    }

    public static string Abonos(string teamName, int totalSold, decimal revenue)
    {
        return $"La campaña de abonos del {teamName} ha concluido con éxito.\n\n" +
            $"Se han vendido un total de {totalSold} abonos, lo que supone unos ingresos de ${revenue:N0} para la franquicia. Una cifra que refleja la confianza de la afición en el proyecto deportivo del equipo.\n\n" +
            $"Los abonos representan una fuente de ingresos estable y predecible que permite planificar las inversiones deportivas con mayor seguridad. El equipo agradece el apoyo incondicional de sus seguidores.";
    }

    public static string Patrocinio(string sponsorName, string teamName, decimal value)
    {
        return $"El {teamName} ha cerrado un acuerdo de patrocinio con {sponsorName}.\n\n" +
            $"El contrato tiene un valor estimado de ${value:N0} anuales y se extiende por varias temporadas. La marca se asociará con la franquicia en eventos, redes sociales y visibilidad en el pabellón.\n\n" +
            $"Una alianza estratégica que beneficia a ambas partes: {sponsorName} amplía su presencia en el deporte, mientras que el equipo asegura una fuente de ingresos que refuerza su estabilidad financiera.";
    }

    public static string TV(string channelName, string teamName, decimal value)
    {
        return $"El {teamName} ha firmado un acuerdo de derechos televisivos con {channelName}.\n\n" +
            $"El contrato, valorado en ${value:N0} anuales, garantiza la retransmisión de los partidos del equipo en la plataforma del canal. Los seguidores podrán seguir cada encuentro en alta definición desde la comodidad de su hogar.\n\n" +
            $"Esta alianza mediática amplía el alcance del equipo y genera una fuente de ingresos complementaria que contribuye a la sostenibilidad económica de la franquicia.";
    }

    public static string PrestamoConcedido(string teamName, decimal amount, int months)
    {
        return $"El {teamName} ha solicitado y recibido un préstamo bancario por ${amount:N0}.\n\n" +
            $"El préstamo tiene un plazo de devolución de {months} meses y condiciones que permiten al equipo cubrir sus necesidades de liquidez a corto plazo. La operación ha sido aprobada tras una evaluación de la solvencia financiera de la franquicia.\n\n" +
            $"Este recursos permitirá al equipo afrontar los gastos operativos del mes sin comprometer su capacidad de inversión deportiva. La gestión financiera responsable es clave para mantener la salud económica del club.";
    }

    public static string PrestamoSaldado(string teamName, decimal amount)
    {
        return $"El {teamName} ha saldado completamente el préstamo pendiente por ${amount:N0}.\n\n" +
            $"La devolución anticipada o puntual del préstamo libera al equipo de una carga financiera que pesaba sobre sus cuentas. Con esta operación, el club reduce su deuda y mejora su posición ante posibles inversiones futuras.\n\n" +
            $"Un paso importante hacia la estabilidad financiera del equipo. El ahorro en intereses y la reducción de obligaciones permitirán al equipo destinar recursos a otras áreas del proyecto deportivo.";
    }

    public static string RemodelacionIniciada(string teamName, string arenaName, decimal cost)
    {
        return $"El {teamName} ha iniciado las obras de remodelación del {arenaName}.\n\n" +
            $"La inversión estimada es de ${cost:N0} y se espera que las obras se extiendan durante varios meses. La remodelación incluirá mejoras en los vestuarios, la zona VIP, los accesos y la infraestructura técnica del recinto.\n\n" +
            $"Un proyecto ambicioso que busca modernizar las instalaciones y mejorar la experiencia de los aficionados. El equipo invierte en su futuro y en la satisfacción de sus seguidores.";
    }

    public static string RemodelacionCompletada(string teamName, string arenaName)
    {
        return $"El {teamName} ha completado la remodelación del {arenaName}.\n\n" +
            $"Las obras, que se prolongaron durante varios meses, han transformado por completo las instalaciones. Los nuevos vestuarios, la zona de prensa renovada y las mejoras en los accesos son algunos de los cambios más destacados.\n\n" +
            $"El equipo inaugurará las nuevas instalaciones en el próximo partido en casa. Los aficionados podrán disfrutar de una experiencia mejorada que refleja el compromiso de la franquicia con la excelencia en todos los aspectos.";
    }

    // ── TEMPORADA / MORAL ──

    public static string Bienvenida(string teamName, string playerName, int ovr)
    {
        return $"¡Bienvenido al {teamName}, {playerName}!\n\n" +
            $"El nuevo fichaje se incorpora a la plantilla con la ilusión de aportar su granito de arena al proyecto deportivo. Con una valoración de {ovr} puntos, llega para cubrir una posición que el equipo necesitaba reforzar.\n\n" +
            $"El jugador ha expresado su entusiasmo por formar parte de la franquicia y su compromiso con los objetivos del equipo. Los aficionados ya sueñan con verlo en acción bajo las luces del pabellón.";
    }

    public static string EliminacionPlayin(string teamName, int rank)
    {
        return $"El {teamName} ha sido eliminado del Play-In y cierra su temporada.\n\n" +
            $"Concluido en la posición {rank} de su conferencia, el equipo no pudo acceder a la fase final de los playoffs. Una temporada de altibajos que deja aprendizajes valiosos de cara al futuro.\n\n" +
            $"El equipo ahora se enfrenta a una offseason crucial. Las decisiones que se tomen en los próximos meses definirán el rumbo del proyecto deportivo para las próximas temporadas. La afición espera un refuerzo que devuelva las esperanzas.";
    }

    public static string EliminacionPlayoffs(string teamName, string rivalName, int round, int losses)
    {
        string roundName = round switch
        {
            1 => "primera ronda",
            2 => "segunda ronda",
            3 => "finales de conferencia",
            _ => "finales"
        };
        return $"El {teamName} ha sido eliminado de los Playoffs en {roundName} por {rivalName}.\n\n" +
            $"Con un marcador de {losses}–0 a favor del rival, el equipo no pudo reponerse al dominio mostrado por {rivalName} en la serie. Una eliminación dolorosa que deja la sensación de que el equipo no estuvo a la altura en los momentos decisivos.\n\n" +
            $"La temporada ha terminado antes de lo esperado. El equipo deberá analizar qué falló y qué se puede mejorar para estar en mejor posición la próxima vez que llegue a esta fase.";
    }

    public static string PremiosDelMes(string teamName, string month, List<string> awards)
    {
        string awardsText = string.Join(", ", awards);
        return $"El {teamName} ha sido galardonado con los premios del mes de {month}.\n\n" +
            $"Los reconocimientos otorgados: {awardsText}. Un mes brillante para la franquicia que ha destacado por encima del resto de equipos en la competición.\n\n" +
            $"Estos premios reflejan el trabajo colectivo de toda la plantilla y el cuerpo técnico. El equipo sigue sumando reconocimientos que alimentan la confianza y la moral de cara a los próximos compromisos.";
    }

    public static string Queja(string teamName, string topic)
    {
        return $"La afición del {teamName} ha expresado su malestar público respecto a {topic}.\n\n" +
            $"Las redes sociales se han llenado de críticas y los seguidores han dejado claras sus preocupaciones sobre la dirección del equipo. La presión sobre la gerencia y el cuerpo técnico crece con cada declaración.\n\n" +
            $"El equipo deberá responder a estas críticas con hechos en la cancha. La paciencia de la afición tiene un límite, y la próxima serie de partidos será decisiva para recuperar la confianza perdida.";
    }

    public static string Preocupacion(string teamName, string topic)
    {
        return $"Se detectan señales de preocupación en el entorno del {teamName} respecto a {topic}.\n\n" +
            $"Las fuentes internas indican que existe malestar dentro del vestuario, y esto podría afectar al rendimiento del equipo en las próximas semanas. La dirección del club mantiene una postura de calma, pero la situación merece atención.\n\n" +
            $"Es fundamental que el equipo encuentre una solución rápida para evitar que esta situación se desborde. El próximo partido será una prueba de fuego para medir la capacidad de reacción del grupo.";
    }
}
