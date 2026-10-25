# CI de Tactical Five (F3-07)

El workflow `.github/workflows/unity-ci.yml` corre en un runner self-hosted dedicado
(`self-hosted`, `linux`, `unity`). `actions/checkout` limpia la copia de trabajo en cada job
y el grupo de concurrencia serializa el uso del Editor Unity del runner. El pipeline:

1. Ejecuta `python3 Tools/project_status.py`: además de imprimir rama/versiones/conteos,
   falla si `GameScreen`, UIDocuments y `ScreenManager.GoTo` están desincronizados.
2. Compila un player **Standalone Linux x86_64** desde las escenas activas en
   `EditorBuildSettings.asset` (`ContinuousIntegrationBuild.BuildLinuxPlayer`).
3. Ejecuta toda la suite **EditMode**, incluyendo auditoría del seed, fingerprint,
   invariantes del calendario y gates Monte Carlo F3-04/05/06.
4. Valida el XML de NUnit: resultado `Passed`, conteo igual al inventario estático actual,
   cero fallos/skips y presencia de los checks obligatorios. Build, logs y XML se publican
   como artifact aunque un paso falle.

## Configuración del runner

- Linux con Unity **6000.3.15f1** licenciado y módulo **Linux Build Support** instalado.
- Runner de GitHub Actions registrado con las etiquetas `self-hosted`, `linux` y `unity`;
  el proceso del runner debe heredar `UNITY_EDITOR_PATH` apuntando al
  ejecutable Unity (no a un wrapper de la GUI).
- Python 3, Bash y `timeout` no son dependencias; el job se serializa para evitar competir
  por `Temp/UnityLockfile`.
- Configurar la protección de la rama para exigir el check
  **Build Linux + EditMode (Unity 6000.3.15f1)** antes de integrar. GitHub no permite
  versionar esa regla de protección desde este repositorio.

## Ejecución local

```bash
UNITY_EDITOR_PATH="/ruta/Unity/6000.3.15f1/Editor/Unity" bash Tools/ci_unity.sh
```

Los artifacts se escriben fuera del repo en `${RUNNER_TEMP}` o `/tmp/opencode` y se puede
redirigir con `TACTICALFIVE_CI_ARTIFACTS=/ruta/absoluta`. `python3 Tools/verify_editmode_results.py
<editmode.xml>` valida el resultado sin volver a iniciar Unity.

## Limitación de assemblies de tests

Los tests actuales viven bajo `Assets/_TacticalFive/Tests/Editor/` **sin `.asmdef`** y
compilan en la assembly predefinida `Assembly-CSharp-Editor`. Mantenerlos así evita que
un assembly de tests separado intente referenciar `Assembly-CSharp` (Unity no lo permite
como referencia de un asmdef). El gate usa el Test Framework instalado en el proyecto y
valida el XML completo; no requiere dividir assemblies ni cambiar el modelo de compilación.
