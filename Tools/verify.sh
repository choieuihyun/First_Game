#!/usr/bin/env bash
# 검증 스크립트. AI 가 쓴 코드는 이 검사를 통과해야 완료다
# 사용법: Tools/verify.sh core|unity|play|all
#   core  : 게임 규칙 테스트를 Unity 없이 돌린다 (몇 초). 에디터가 열려 있어도 된다
#   unity : Unity 를 화면 없이 열어 컴파일 에러를 확인한다
#   play  : 씬을 화면 없이 실제로 돌려 실행 중 에러를 확인한다
#   all   : core, unity, play 를 차례로. 하나라도 실패하면 멈춘다
# 종료 코드: 0 통과, 1 실패, 2 사용법 오류, 3 Unity 에디터가 프로젝트를 열고 있음
# 출력 폴더: 환경변수 VERIFY_OUT (없으면 임시 폴더)
# 주의: Unity 옵션을 바꾸지 않는다. 옵션이 달라지면 전체 재임포트로 몇 분이 걸린다
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MODE="${1:-}"
OUT="${VERIFY_OUT:-${TMPDIR:-/tmp}/firstGame-verify}"
UNITY_TIMEOUT=420
mkdir -p "$OUT"

unity_path() {
  local version
  version="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
  echo "/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity"
}

# 에디터가 이 프로젝트를 열고 있으면 배치 모드가 실패한다
check_editor_closed() {
  if [ -e "$ROOT/Temp/UnityLockfile" ] && pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null 2>&1; then
    echo "Unity 에디터가 이 프로젝트를 열고 있습니다. 에디터를 닫거나 Test Runner 창에서 실행하세요"
    exit 3
  fi
}

# Unity 를 실행하고 너무 오래 걸리면 강제로 끝낸다
run_unity() {
  local unity
  unity="$(unity_path)"
  if [ ! -x "$unity" ]; then echo "Unity 를 찾지 못했습니다: $unity"; return 1; fi
  "$unity" "$@" &
  local pid=$!
  ( sleep "$UNITY_TIMEOUT"; kill "$pid" 2>/dev/null ) &
  local watchdog=$!
  wait "$pid"
  local code=$?
  kill "$watchdog" 2>/dev/null
  wait "$watchdog" 2>/dev/null
  return $code
}

run_core() {
  dotnet test "$ROOT/Tools/CoreTests/Game.Tests/Game.Tests.csproj" --nologo
}

run_unity_compile() {
  check_editor_closed
  local log="$OUT/compile.log"
  run_unity -batchmode -quit -nographics -projectPath "$ROOT" -logFile "$log"
  local code=$?
  if grep -qE "error CS|Scripts have compiler errors" "$log" 2>/dev/null; then
    grep -E "error CS" "$log" | sort -u | head -30
    echo "Unity 컴파일 실패 (로그: $log)"
    return 1
  fi
  if [ $code -ne 0 ]; then echo "Unity 가 비정상 종료했습니다 (코드 $code, 로그: $log)"; return 1; fi
  echo "Unity 컴파일 통과"
}

run_play() {
  check_editor_closed
  local log="$OUT/play.log" xml="$OUT/play.xml"
  rm -f "$xml"
  run_unity -batchmode -projectPath "$ROOT" -runTests -testPlatform PlayMode -testResults "$xml" -logFile "$log"
  # 실행이 중간에 끊기면 임시 씬이 남는다
  rm -f "$ROOT"/Assets/InitTestScene*.unity "$ROOT"/Assets/InitTestScene*.unity.meta
  if [ ! -f "$xml" ]; then
    grep -E "error CS" "$log" 2>/dev/null | sort -u | head -30
    echo "PlayMode 테스트가 결과를 남기지 못했습니다 (로그: $log)"
    return 1
  fi
  local summary
  summary="$(grep -oE '<test-run[^>]*' "$xml" | grep -oE '(result|total|passed|failed)="[^"]*"' | tr '\n' ' ')"
  echo "PlayMode: $summary"
  if ! grep -oE '<test-run[^>]*' "$xml" | grep -q 'result="Passed"'; then
    grep -A6 "<message>" "$xml" | grep -v "One or more child tests" | head -40
    return 1
  fi
}

case "$MODE" in
  core) run_core ;;
  unity) run_unity_compile ;;
  play) run_play ;;
  all) run_core && run_unity_compile && run_play ;;
  *) echo "사용법: Tools/verify.sh core|unity|play|all"; exit 2 ;;
esac
