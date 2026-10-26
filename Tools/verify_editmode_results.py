#!/usr/bin/env python3
"""Valida el XML de Unity EditMode y exige los gates seed/calendario de F3-07."""

from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "Tools"))
from project_status import count_tests  # noqa: E402


REQUIRED_CASES = (
    "SeedAuditorTests.Auditor_NoPasaEnVacio",
    "SaveSlotInitTests.SeedBaseline_Fingerprint_CoincideConElSeedReciénGenerado",
    "ScheduleGeneratorDiagnosticTests.GenerateSchedule_MeetsNbaBalanceInvariants",
    "CalendarHelperTests.DateToGameDay_AndGameDayToDate_AreInverse",
    "MonteCarloRunnerTests.Run_TemporadaCompletaDe30Equipos_CuadraElCalendario",
    "MonteCarloRunnerTests.Run_CienTemporadas_CalibraVictoriasParidadOVRYProrrogas",
)


def main():
    if len(sys.argv) != 2:
        print("Uso: verify_editmode_results.py <results.xml>", file=sys.stderr)
        return 2

    result_path = Path(sys.argv[1])
    if not result_path.is_file():
        print(f"No existe el XML de Unity: {result_path}", file=sys.stderr)
        return 1

    try:
        root = ET.parse(result_path).getroot()
        plain, parameterized, unity_tests = count_tests()
    except (ET.ParseError, OSError, RuntimeError) as error:
        print(f"No se pudieron validar resultados EditMode: {error}", file=sys.stderr)
        return 1

    expected = plain + parameterized + unity_tests
    total = int(root.get("total", "-1"))
    failed = int(root.get("failed", "-1"))
    skipped = int(root.get("skipped", "-1"))
    result = root.get("result", "Unknown")
    if result != "Passed" or failed != 0 or skipped != 0 or total != expected:
        print(
            f"EditMode no pasa: result={result}, total={total}/{expected}, "
            f"failed={failed}, skipped={skipped}",
            file=sys.stderr,
        )
        return 1

    cases = {
        case.get("fullname", "")
        for case in root.iter("test-case")
        if case.get("result") == "Passed"
    }
    missing = [name for name in REQUIRED_CASES if name not in cases]
    if missing:
        print("Faltan checks obligatorios en el XML: " + ", ".join(missing), file=sys.stderr)
        return 1

    print(f"EditMode OK: {total}/{expected}; auditoría seed, calendario y calibración presentes.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
