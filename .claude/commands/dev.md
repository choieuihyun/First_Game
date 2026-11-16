# /dev - 개발 (승인된 기획이나 설계를 코드로 만든다)

승인된 기획서나 기능을 구현하고, 검증을 통과시키고, 문서를 갱신한다.
**기획 내용을 임의로 바꾸지 않는다.** 기획과 코드가 어긋나면 사용자에게 묻는다.

개발 대상: $ARGUMENTS
(비어 있으면 `AIDocument/진행_현황.md` 의 다음 할 일에서 찾고, 그래도 없으면 무엇을 만들지 묻는다)

---

## 1. 시작 전에 읽기

- `AIDocument/진행_현황.md`
- `AIDocument/설계/아키텍처.md` (층 규칙과 금지 사항)
- 대상과 관련된 기획서 (`AIDocument/기획/`) 와 시스템 문서 (`AIDocument/시스템/`)

기획서가 없거나 승인 전이면 멈추고 `/design` 을 먼저 하자고 말한다.

## 2. 만드는 순서

1. **데이터로 되는지 먼저 본다.** 새 물질, 반응, 적, 수치 조정은 `Assets/Resources/Data/*.csv` 만 고치면 되는 경우가 많다.
   방법은 `AIDocument/시스템/반응/SYSTEM.md` 에 있다
2. 코드가 필요하면 **테스트를 먼저 쓴다.** 실패하는 것을 확인한 뒤 구현한다
   - 게임 규칙 (Core, Data): `Assets/Tests/EditMode/`
   - 화면과 시작 흐름 (View, App): `Assets/Tests/PlayMode/`
3. 구현한다. 지킬 것:
   - `Assets/Game/Core`, `Assets/Game/Data` 는 UnityEngine 을 쓰지 않는다
   - 기획 수치를 코드에 쓰지 않는다. CSV 에 둔다
   - Core 의 랜덤은 `Rng` 하나만. 목록은 생성 순서대로 순회한다
   - `RunState` 에 필드를 추가하면 `RunSerializer` 에도 넣고 `Version` 을 올린다
   - 한글 주석 (CLAUDE.md 주석 규칙)

## 3. 검증 (통과해야 완료)

| 바꾼 것 | 돌릴 것 |
|---|---|
| Core, Data, CSV | `Tools/verify.sh core` (몇 초. 에디터가 열려 있어도 된다) |
| View, App, 그리고 기능을 끝냈을 때 | `Tools/verify.sh all` |

- 종료 코드 3 이면 Unity 에디터가 프로젝트를 열고 있는 것이다. 사용자에게 에디터를 닫아 달라고 하거나,
  `core` 만 돌리고 나머지는 에디터의 Test Runner 창에서 돌리도록 안내한다
- 실패하면 원인을 찾아 고치고 다시 돈다. 같은 에러 3회 또는 시도 5회를 넘으면 멈추고 보고한다
- 통과하지 않은 것을 통과했다고 말하지 않는다. 돌리지 못한 검증은 돌리지 못했다고 말한다

## 4. 마무리

1. 바뀐 시스템의 `AIDocument/시스템/<시스템>/SYSTEM.md` 를 갱신한다 (없으면 `_TEMPLATE_SYSTEM.md` 로 만든다)
2. `AIDocument/진행_현황.md` 를 갱신한다. 결정이 있었으면 `AIDocument/결정_기록.md` 에 추가한다
3. 사용자에게 보고한다: 무엇을 만들었나, 검증 결과, Unity 에서 Play 로 직접 볼 것
4. 멈춘다. 재미와 손맛은 사용자가 확인한다

## 5. 금지

- 기획서 내용을 임의로 바꾸기
- 커밋 (사용자가 그 턴에 직접 시켰을 때만. CLAUDE.md 커밋 규칙 참고)
- 패키지 추가 (`Packages/manifest.json`), 사용자 허락 없이
- `Library/`, `Temp/`, `Logs/`, `UserSettings/` 를 읽거나 고치기
- `.meta` 파일을 직접 쓰기 (Unity 가 만든다. 파일을 지우거나 옮길 때는 `.meta` 도 함께)
