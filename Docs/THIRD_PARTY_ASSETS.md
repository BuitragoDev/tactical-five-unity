# Inventario de activos y dependencias de terceros

> **Corte del inventario:** 2026-10-23. Objetivo: trazabilidad de origen, licencia y
> atribución; **no es asesoramiento legal ni certifica derechos de distribución**.
> `Verificado` significa que hay una licencia/aviso local o una fuente primaria
> identificable; `Pendiente` significa que falta evidencia suficiente. No asumir que un
> archivo es original, gratuito o de dominio público porque no tenga metadatos.

## Resumen de estado

- **Verificado en el repositorio:** `StandaloneFileBrowser` (MIT), wrapper `sqlite-net`
  (MIT en el encabezado), SQLite nativo (dominio público según el upstream), Liberation
  Sans (OFL 1.1) y los avisos de TextMesh Pro/Unity (Unity Companion License).
- **Verificado contra fuente primaria externa:** Barlow Condensed y Saira Condensed
  (OFL 1.1), además del texto de licencia de TextMesh Pro.
- **Pendiente antes de distribuir:** el binario `Ookii.Dialogs.dll`, EmojiOne, Kindergarten,
  las fotos de jugadores, logos/escudos, pabellones, equipaciones, audio y demás arte bajo
  `Art/Resources`. Su procedencia/licencia no queda acreditada por este inventario.

## Código, paquetes y fuentes

| Activo / rutas incluidas | Origen identificado | Licencia / atribución | Estado y acción |
|---|---|---|---|
| `Assets/StandaloneFileBrowser/**`; parche Linux en `Tools/StandaloneFileBrowser/**` | Proyecto [UnityStandaloneFileBrowser](https://github.com/gkngkc/UnityStandaloneFileBrowser). El `LICENSE.txt` local coincide con el upstream. El repo contiene plugins Windows, macOS, WebGL y Linux; `Tools/StandaloneFileBrowser` documenta un parche GTK/X11 local. | MIT. Copyright © 2017 Gökhan Gökçe. Conservar `Assets/StandaloneFileBrowser/LICENSE.txt` y su aviso en redistribuciones del código. | **Verificado parcialmente.** Documentar la procedencia/autoría del delta local Linux y no reemplazar el aviso MIT al regenerar el plugin. |
| `Assets/StandaloneFileBrowser/Plugins/Ookii.Dialogs.dll` | Dependencia incluida por el plugin Windows; el binario no lleva fuente ni procedencia verificable en el repo. | **No determinada.** El MIT de `StandaloneFileBrowser` no se toma como prueba de la licencia independiente de esta DLL. | **Pendiente / bloquear distribución Windows** hasta registrar upstream, versión y licencia/notice del componente. |
| `Assets/_TacticalFive/Scripts/Data/SQLite.cs` | Wrapper `sqlite-net` identificado por el código y su encabezado; commit/versión de upstream no está fijado. | MIT incorporada en el encabezado: Copyright © 2009–2024 Krueger Systems, Inc. Mantener el aviso al copiar/modificar el archivo. Referencia upstream: [praeclarum/sqlite-net](https://github.com/praeclarum/sqlite-net). | **Licencia verificada; versión exacta pendiente** de fijar para un SBOM reproducible. |
| `Assets/Plugins/SQLite/Linux/x86_64/libsqlite3.so`, `Assets/Plugins/SQLite/x86_64/SQLite3.dll` | Bibliotecas nativas SQLite. El repo no registra versión ni comando/artefacto de compilación que produjo estos binarios. | SQLite declara su código entregable en **dominio público**: [SQLite Copyright](https://www.sqlite.org/copyright.html). No se encontró una notice de build adicional en el repo. | **Licencia de upstream verificada; procedencia/versiones de los binarios pendientes.** Registrar versión, arquitectura real, origen y checksum al regenerarlos. |
| `Assets/TextMesh Pro/**` (assets/shaders de TMP) | Assets TextMesh Pro integrados/importados con Unity; el repo no fija el paquete como dependencia en `Packages/manifest.json`. El aviso upstream consultado identifica © 2022 Unity Technologies ApS. | Unity Companion License para proyectos dependientes de Unity: [texto oficial](https://unity.com/legal/licenses/unity-companion-license). Esta licencia no sustituye las licencias de recursos de terceros que el paquete trae aparte. | **Aviso upstream identificado;** conservar la licencia aplicable al paquete/editor utilizado en los materiales de distribución. |
| `Assets/TextMesh Pro/Fonts/LiberationSans.ttf`, sus SDF/atlas y `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt` | Liberation Fonts; el archivo de licencia atribuye datos digitalizados a Google Corporation y la familia Liberation a Red Hat, Inc. | SIL Open Font License 1.1, con texto incluido en `LiberationSans - OFL.txt`. | **Verificado localmente.** Mantener la licencia/atribución junto a la fuente y sus derivados. |
| `Assets/TextMesh Pro/Sprites/EmojiOne.*`, `Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset`, `EmojiOne Attribution.txt` | El aviso local atribuye la muestra a EmojiOne (`https://www.emojione.com/`). | El mismo aviso pide revisar sus términos, pero **no incluye la licencia concreta**. No inferir que hereda Unity Companion License. | **Pendiente / bloquear redistribución de los sprites** hasta identificar versión y licencia que cubre el asset incluido. |
| `Assets/_TacticalFive/Art/Fonts/BarlowCondensed-*.ttf`, `FA_BarlowCondensed-* SDF.asset` | Barlow Condensed; fuente primaria: [Google Fonts — Barlow Condensed](https://github.com/google/fonts/tree/main/ofl/barlowcondensed). Los SDF son derivados de la fuente. | SIL Open Font License 1.1. Atribución: “Copyright 2017 The Barlow Project Authors”. | **Verificado upstream.** Referencia de licencia: [OFL.txt](https://raw.githubusercontent.com/google/fonts/main/ofl/barlowcondensed/OFL.txt). Incluir el notice/licencia al empaquetar/distribuir la fuente. |
| `Assets/_TacticalFive/Art/Fonts/Saira_Condensed-*.ttf`, `Saira_Condensed-* SDF.asset` | Saira Condensed; fuente primaria: [Google Fonts — Saira Condensed](https://github.com/google/fonts/tree/main/ofl/sairacondensed). Los SDF son derivados de la fuente. | SIL Open Font License 1.1. Atribución: “Copyright 2016 The Saira Project Authors”; nombre reservado **Saira**. | **Verificado upstream.** Referencia de licencia: [OFL.txt](https://raw.githubusercontent.com/google/fonts/main/ofl/sairacondensed/OFL.txt). Incluir el notice/licencia al empaquetar/distribuir la fuente y respetar el nombre reservado en derivados. |
| `Assets/_TacticalFive/Art/Fonts/Kindergarten.ttf`, `Kindergarten SDF.asset` | El metadata identifica la familia como “KG Miss Kindergarten”; no se encontró origen/autor verificable en el repo. Se usa en `UI/Screens/Employees/Employees.uss`. | **No determinada.** No hay licencia/notice local; no atribuirla a Barlow/Saira ni asumir uso comercial permitido. | **Pendiente / bloquear distribución** hasta obtener licencia del autor/fuente o sustituir la tipografía. |

## Arte, fotografías y audio

La búsqueda de archivos `LICENSE`, `NOTICE`, `COPYING` y `ATTRIBUTION` no encontró notices
para estas carpetas. Los nombres de archivo y los `.meta` de Unity no demuestran autoría,
origen ni permiso de uso.

| Grupo de activos | Rutas | Origen / licencia / atribución registrada | Estado y acción |
|---|---|---|---|
| Fotos/rostros de jugadores y placeholders | `Assets/_TacticalFive/Art/Resources/PlayerPhotos/**` | No hay fuentes, nombres de fotógrafos, licencias ni releases de imagen en el repo. `PlayerPhotos/Default` no prueba que las demás imágenes sean libres. | **Pendiente de alta prioridad.** Crear procedencia por archivo/ID (URL o proveedor, fotógrafo, licencia, atribución y permiso de likeness); sustituir/retirar los no verificables antes de distribuir. |
| Logos de franquicias NBA/G-League, variantes y marcas | `Art/Resources/Teams/Logos/**`, `Art/Resources/Teams/GLeague/**` | No hay fuente ni autorización/atribución documentada. La temática y los identificadores refieren a equipos/marcas reales; no asumir que el uso del logo queda autorizado por describir equipos en el juego. | **Pendiente de alta prioridad.** Identificar fuente y derechos de uso de cada familia/variante; registrar permiso o reemplazar por arte original. |
| Equipaciones y pabellones | `Art/Resources/Teams/Jerseys/**`, `Art/Resources/Arenas/**` | No se encontró crédito, autor, fuente, licencia ni registro de que el arte sea original. | **Pendiente.** Identificar recursos externos y registrar licencias; para arte propio, añadir creador y fecha/fuente de creación. |
| Banderas, iconos, fondos, ilustraciones/noticias, mapa/ciudad | `Art/Resources/{Flags,Icons,Backgrounds,Noticias,TheCity}/**` y `Art/UI/Icons/**` | No se encontró manifiesto ni metadatos que distingan arte propio de assets descargados. | **Pendiente.** Inventariar por archivo/grupo homogéneo con URL, creador, licencia y atribución; no etiquetar todo el directorio como “propio” sin evidencia. |
| Logos de patrocinadores y televisiones | `Art/Resources/Patrocinadores/**`, `Art/Resources/Televisiones/**` | No hay fuente ni licencia/permiso registrada. Algunos nombres/identidades pueden representar marcas de terceros; verificar cada una. | **Pendiente.** Trazar los archivos y documentar permiso, licencia o sustitución por marcas ficticias originales. |
| Música y efectos de sonido | `Art/Resources/Audios/**` (WAV) | No se encontró autor, biblioteca, URL, licencia, recibo de compra ni atribución. | **Pendiente de alta prioridad.** Identificar por archivo y conservar licencia/recibo; si no se puede probar, sustituir antes de distribuir. |

## Regla de mantenimiento / puerta de distribución

1. Cada recurso nuevo de terceros debe anotarse aquí **antes de integrarse**: ruta, creador,
   URL/paquete y versión, licencia exacta, texto de atribución requerido, modificaciones y
   fecha de verificación. Para archivos individuales, usar además un registro por archivo.
2. Si se sustituye/actualiza un asset, volver a comprobar la licencia de esa versión; una URL
   al proveedor sin identificar el archivo/versión no cierra la trazabilidad.
3. **Pendiente no significa permitido.** Este inventario no confirma que los assets pendientes
   puedan publicarse o usarse comercialmente. Resolver las filas pendientes o reemplazar los
   recursos antes de una distribución pública/comercial; incluir notices/licencias aplicables
   en el paquete final.

## Fuentes primarias verificadas en este pase

- [UnityStandaloneFileBrowser — repositorio](https://github.com/gkngkc/UnityStandaloneFileBrowser) y su `LICENSE.txt` MIT local.
- [SQLite — Copyright](https://www.sqlite.org/copyright.html) (dominio público del código entregable).
- [TextMesh Pro — notice de licencia upstream](https://github.com/needle-mirror/com.unity.textmeshpro/blob/master/LICENSE.md) y [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license).
- [Barlow Condensed — OFL](https://raw.githubusercontent.com/google/fonts/main/ofl/barlowcondensed/OFL.txt).
- [Saira Condensed — OFL](https://raw.githubusercontent.com/google/fonts/main/ofl/sairacondensed/OFL.txt).
