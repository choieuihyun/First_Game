#!/usr/bin/env bash
# CHANGELOG 자동 한 줄 기록 (Stop 훅용)
# 역할: 응답이 끝날 때 바뀐 코드와 데이터 파일 목록을 CHANGELOG.md 에 한 줄 남긴다
#
# 중복 억제가 핵심이다. 기준이 커밋하지 않은 변경 전체라서 커밋하기 전까지는 매번 같은 목록이 나온다
# 그래서 마지막 줄과 같으면 쓰지 않는다

set -u

REPO="${CLAUDE_PROJECT_DIR:-}"
[ -z "$REPO" ] && exit 0
[ -d "$REPO/.git" ] || [ -f "$REPO/.git" ] || exit 0

CHANGELOG="$REPO/CHANGELOG.md"
DATE=$(date +%Y-%m-%d)

# 바뀐 파일과 새 파일 중 코드와 데이터만 (경로 제거, 파일명만)
FILES=$( { git -C "$REPO" diff --name-only HEAD 2>/dev/null; git -C "$REPO" ls-files --others --exclude-standard 2>/dev/null; } \
  | grep -E '\.(cs|csv)$' | sed 's|.*/||' | sort -u)
[ -z "$FILES" ] && exit 0

TOTAL=$(printf '%s\n' "$FILES" | wc -l | tr -d ' ')
SHOWN=$(printf '%s\n' "$FILES" | head -3 | paste -sd ',' -)

# 3개를 넘으면 잘린 사실을 남긴다. 그냥 두면 나중에 3개만 고친 것으로 잘못 읽힌다
if [ "$TOTAL" -gt 3 ]; then
  LINE="[$DATE] [auto] [수정: ${SHOWN} 외 $((TOTAL - 3))개]"
else
  LINE="[$DATE] [auto] [수정: ${SHOWN}]"
fi

[ -f "$CHANGELOG" ] || printf '# CHANGELOG\n\n자동 기록. 응답이 끝날 때 바뀐 코드와 데이터 파일을 한 줄씩 남긴다.\n\n' > "$CHANGELOG"
LAST=$(tail -n 1 "$CHANGELOG")
[ "$LAST" = "$LINE" ] && exit 0
printf '%s\n' "$LINE" >> "$CHANGELOG"
exit 0
