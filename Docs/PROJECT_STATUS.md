# Estado actual del proyecto

`Tools/project_status.py` es la fuente canónica de los datos que cambian con el código o
con Git: versión visible del juego, versión de Unity, rama, HEAD, casos EditMode y número
de pantallas conectadas.

Ejecutar desde cualquier directorio:

```bash
python3 Tools/project_status.py
```

El comando deriva rama y HEAD de Git; cuenta `[Test]`, `[TestCase]` y `[UnityTest]` en
`Assets/_TacticalFive/Tests/Editor/`; y obtiene las pantallas de `GameScreen`, comprobando
que coincidan con los `UIDocument` serializados y los `case` de `ScreenManager.GoTo`.
Si aparece un origen dinámico de casos (`[TestCaseSource]`) o se desincroniza el registro
de pantallas, el comando falla en vez de publicar un conteo incompleto.

**No copiar los valores calculados a otros documentos.** `AGENTS.md`,
`Docs/PROJECT_OVERVIEW.md`, `Docs/AUDITORIA_FINAL.md` y `.agent/MEMORY.md` enlazan aquí.
Las cifras dentro de auditorías fechadas o bitácoras describen el snapshot histórico
indicado y no representan necesariamente el checkout actual.
