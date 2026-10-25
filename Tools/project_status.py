#!/usr/bin/env python3
"""Imprime el estado versionado/dinámico actual de Tactical Five.

Fuente canónica para rama, HEAD, versiones, casos EditMode y pantallas. Los documentos
de onboarding/auditoría deben enlazar aquí en lugar de copiar esos valores, que envejecen
en cada cambio de código o commit.
"""

from pathlib import Path
import re
import subprocess
import sys


ROOT = Path(__file__).resolve().parent.parent
TESTS_DIR = ROOT / "Assets/_TacticalFive/Tests/Editor"
ENUMS_FILE = ROOT / "Assets/_TacticalFive/Scripts/Core/GameEnums.cs"
SCREEN_MANAGER_FILE = ROOT / "Assets/_TacticalFive/Scripts/Core/ScreenManager.cs"


def git(*args):
    return subprocess.check_output(
        ["git", "-C", str(ROOT), *args], text=True, stderr=subprocess.PIPE
    ).strip()


def strip_csharp_comments(source):
    # Los atributos de NUnit se escriben en líneas propias en esta suite. Quitamos
    # comentarios para no contar ejemplos comentados como tests ejecutables.
    source = re.sub(r"/\*.*?\*/", "", source, flags=re.DOTALL)
    return re.sub(r"(?m)^\s*//.*$", "", source)


def count_tests():
    plain_tests = 0
    parameterized_tests = 0
    unity_tests = 0
    dynamic_sources = []

    for path in sorted(TESTS_DIR.rglob("*.cs")):
        source = strip_csharp_comments(path.read_text(encoding="utf-8"))
        plain_tests += len(re.findall(r"(?m)^\s*\[Test\s*(?:\]|,)", source))
        parameterized_tests += len(re.findall(r"(?m)^\s*\[TestCase\s*\(", source))
        unity_tests += len(re.findall(r"(?m)^\s*\[UnityTest\s*(?:\]|,)", source))
        if re.search(r"(?m)^\s*\[TestCaseSource\b", source):
            dynamic_sources.append(str(path.relative_to(ROOT)))

    if dynamic_sources:
        raise RuntimeError(
            "Hay [TestCaseSource] y el conteo estático no puede expandir sus casos: "
            + ", ".join(dynamic_sources)
            + ". Actualiza este comando o usa el total del Unity Test Runner."
        )

    return plain_tests, parameterized_tests, unity_tests


def screen_counts():
    source = strip_csharp_comments(ENUMS_FILE.read_text(encoding="utf-8"))
    match = re.search(r"\benum\s+GameScreen\s*\{(.*?)\}", source, flags=re.DOTALL)
    if not match:
        raise RuntimeError("No se pudo localizar GameScreen en GameEnums.cs")
    enum_values = [
        value.split("=", 1)[0].strip()
        for value in match.group(1).split(",")
        if value.strip()
    ]

    manager = strip_csharp_comments(SCREEN_MANAGER_FILE.read_text(encoding="utf-8"))
    documents = len(re.findall(r"\[SerializeField\]\s+private\s+UIDocument\s+\w+\s*;", manager))
    case_values = re.findall(r"\bcase\s+GameScreen\.(\w+)\s*:", manager)
    cases = len(case_values)
    if (len(enum_values) != documents or len(enum_values) != cases
            or set(enum_values) != set(case_values)):
        raise RuntimeError(
            f"Registro de pantallas desincronizado: GameScreen={len(enum_values)}, "
            f"UIDocument={documents}, cases GoTo={cases}; "
            f"sin case={sorted(set(enum_values) - set(case_values))}, "
            f"case sin enum={sorted(set(case_values) - set(enum_values))}."
        )
    return len(enum_values), documents, cases


def main():
    try:
        branch = git("branch", "--show-current") or "(detached HEAD)"
        head = git("rev-parse", "--short=8", "HEAD")
        unity_version = re.search(
            r"(?m)^m_EditorVersion:\s*(.+)$",
            (ROOT / "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8"),
        ).group(1).strip()
        main_menu = (ROOT / "Assets/_TacticalFive/UI/Screens/MainMenu/MainMenu.uxml").read_text(
            encoding="utf-8"
        )
        product_match = re.search(r'<ui:Label\s+text="([^"]+)"\s+class="version-label"', main_menu)
        if not product_match:
            raise RuntimeError("No se encontró la etiqueta de versión en MainMenu.uxml")

        plain, parameterized, unity_tests = count_tests()
        screens, documents, cases = screen_counts()
    except (OSError, subprocess.CalledProcessError, AttributeError, RuntimeError) as error:
        print(f"No se pudo calcular el estado: {error}", file=sys.stderr)
        return 1

    print("# Estado actual de Tactical Five")
    print()
    print("| Dato | Valor | Fuente |")
    print("|---|---|---|")
    print(f"| Versión del juego | `{product_match.group(1)}` | `MainMenu.uxml` |")
    print(f"| Unity | `{unity_version}` | `ProjectSettings/ProjectVersion.txt` |")
    print(f"| Rama | `{branch}` | Git |")
    print(f"| HEAD | `{head}` | Git |")
    print(
        f"| Casos EditMode | **{plain + parameterized + unity_tests}** "
        f"({plain} `[Test]` + {parameterized} `[TestCase]` + {unity_tests} `[UnityTest]`) "
        f"en {len(list(TESTS_DIR.rglob('*.cs')))} archivos | `Tests/Editor/*.cs` |"
    )
    print(
        f"| Pantallas | **{screens}** (`GameScreen`, {documents} `UIDocument`, "
        f"{cases} casos `ScreenManager.GoTo`) | `GameEnums.cs`, `ScreenManager.cs` |"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
