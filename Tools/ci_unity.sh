#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR_PATH:-}"
if [[ -z "$UNITY_EDITOR" || ! -x "$UNITY_EDITOR" ]]; then
    echo "Define UNITY_EDITOR_PATH con el ejecutable Unity licenciado de este runner." >&2
    exit 2
fi

PROJECT_UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
EDITOR_UNITY_VERSION="$("$UNITY_EDITOR" -version | tail -n 1 | tr -d '\r')"
if [[ "$EDITOR_UNITY_VERSION" != "$PROJECT_UNITY_VERSION" ]]; then
    echo "Versión Unity incorrecta: proyecto=$PROJECT_UNITY_VERSION, runner=$EDITOR_UNITY_VERSION" >&2
    exit 2
fi

RUN_ID="${GITHUB_RUN_ID:-local-$(date +%Y%m%d%H%M%S)}"
RUN_ATTEMPT="${GITHUB_RUN_ATTEMPT:-1}"
ARTIFACTS="${TACTICALFIVE_CI_ARTIFACTS:-${RUNNER_TEMP:-/tmp/opencode}/tacticalfive-ci-${RUN_ID}-${RUN_ATTEMPT}}"
mkdir -p "$ARTIFACTS"
BUILD_PATH="$ARTIFACTS/TacticalFive.x86_64"
BUILD_LOG="$ARTIFACTS/build.log"
TEST_LOG="$ARTIFACTS/editmode.log"
TEST_XML="$ARTIFACTS/editmode.xml"

cd "$ROOT"
python3 Tools/project_status.py

echo "[CI] Build Linux → $BUILD_PATH"
if ! TACTICALFIVE_BUILD_PATH="$BUILD_PATH" "$UNITY_EDITOR" \
    -batchmode -nographics -projectPath "$ROOT" \
    -executeMethod ContinuousIntegrationBuild.BuildLinuxPlayer \
    -quit -logFile "$BUILD_LOG"; then
    tail -n 100 "$BUILD_LOG" || true
    exit 1
fi
if [[ ! -x "$BUILD_PATH" ]]; then
    echo "El build terminó sin generar un ejecutable Linux: $BUILD_PATH" >&2
    tail -n 100 "$BUILD_LOG" || true
    exit 1
fi

echo "[CI] EditMode completo → $TEST_XML"
if ! "$UNITY_EDITOR" -batchmode -nographics -projectPath "$ROOT" \
    -runTests -testPlatform EditMode -testResults "$TEST_XML" \
    -logFile "$TEST_LOG"; then
    tail -n 120 "$TEST_LOG" || true
    exit 1
fi

python3 Tools/verify_editmode_results.py "$TEST_XML"
echo "[CI] Correcto. Logs, XML y build quedan en: $ARTIFACTS"
