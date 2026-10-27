# LOCALIZATION — Tactical Five

> F7-06: los textos visibles de la UI salen de una tabla única. El español es el
> idioma base; añadir otro idioma no requiere tocar controllers.

## 1. Piezas

| Pieza | Ruta | Responsabilidad |
|---|---|---|
| Tabla española | `Assets/_TacticalFive/Resources/Localization/es.json` | `key` → texto español. 2.120 entradas en el snapshot actual. |
| Runtime | `Assets/_TacticalFive/Scripts/Core/LocalizationService.cs` | Carga la tabla del idioma (`TF_Language`), resuelve tokens y formatea plantillas. |
| Tokens UXML | `text="{loc:clave}"`, `placeholder="{loc:clave}"` | Los UXML no contienen texto visible directo. |
| C# estático | `LocalizationService.T("clave")` | Sustituye `text = "..."` y `new Label("...")` estáticos de `Scripts/UI`. |
| C# dinámico | `LocalizationService.T("clave", arg0, arg1)` | Plantillas con `{0}`, `{1}`… para textos que dependen de datos. |
| Extractor | `Tools/localization_extract.py` | Regenera/fusiona `es.json` y migra literales nuevos. `--check` valida. |
| Tests | `Assets/_TacticalFive/Tests/Editor/LocalizationTests.cs` | Tabla, fallback, formato, `Apply`, cobertura de tokens y literales. |

## 2. Flujo en runtime

1. `UIScreenController.OnEnable` termina con `LocalizationService.Apply(_root)`.
2. `Apply` recorre el árbol UI Toolkit:
   - `TextElement.text` (`Label`, `Button`, etc.).
   - `VisualElement.tooltip`.
   - `TextField.textEdition.placeholder`.
3. Cada token `{loc:clave}` se sustituye por `T("clave")`.
4. Los controllers pueden fijar textos dinámicos con `T("clave", args)` durante
   `Refresh`; `Apply` se ejecuta después y también resuelve esos tokens.
5. Si falta una clave, `T` devuelve la propia clave (visible para detectar el
   error) y, si el idioma actual no tiene entrada, cae al español.

## 3. Añadir un idioma

1. Copiar `es.json` a `Assets/_TacticalFive/Resources/Localization/en.json`.
2. Cambiar `"language": "en"` y traducir los `value` (los `key` no cambian).
3. Llamar a `LocalizationService.SetLanguage("en")` desde el control de ajustes
   que se añada en el futuro. `SetLanguage` persiste `TF_Language` en
   `PlayerPrefs`.
4. No hay que tocar UXML ni controllers: las claves son las mismas y el fallback
   cubre las entradas que falten.

## 4. Añadir o cambiar un texto

- **UXML:** usar `text="{loc:clave}"`. El extractor genera la clave y el valor.
- **C# estático:** no escribir `text = "TEXTO"`; usar
  `text = LocalizationService.T("clave")`.
- **C# dinámico:** usar una plantilla con argumentos:
  `text = LocalizationService.T("ui.common.manager", _manager.name)`.
  La tabla contiene `"Manager: {0}"`.
- **Nunca** añadir un literal visible nuevo fuera de la tabla. `LocalizationTests`
  falla si reaparece un `text="..."`/`new Label("...")` estático o un token sin
  entrada.

## 5. Extractor

```bash
python3 Tools/localization_extract.py          # aplica y fusiona es.json
python3 Tools/localization_extract.py --check  # valida sin escribir
```

Convención de claves:

- UXML: `ui.<pantalla>.<slug>` (p. ej. `ui.roster.title`).
- C#: `ui.code.<archivo>.<slug>` (p. ej. `ui.code.roster_controller.ver_perfil`).
- Plantillas compartidas: `ui.common.*`.

El extractor no reescribe claves existentes; fusiona nuevas entradas y conserva
la tabla para no perder traducciones. Si cambia el texto fuente de una clave, se
debe actualizar su `value` a mano.

## 6. Alcance

F7-06 extrae **textos estáticos de UI** y las plantillas dinámicas comunes de
cabecera. Los textos de contenido generado (noticias largas, `NewsTexts`,
`QuickNewsGenerator`, narraciones) siguen en C# como contenido editorial; cuando
se traduzcan, deben pasar a plantillas `T`/tabla sin duplicar lógica. Las fechas
y números siguen usando el formato del juego (`yyyy-MM-dd`, `dd/MM/yyyy`,
`$"{value:N0}"`); una localización completa de formatos requerirá un helper de
cultura adicional.
