# 검증 시스템 문서

최종 수정: 2026-11-16
근거: `설계/아키텍처.md` 11절

---

## 1. 개요

| 항목 | 내용 |
|---|---|
| 하는 일 | AI 가 쓴 코드와 데이터가 정해진 검사를 통과하는지 자동으로 확인한다 |
| 왜 | AI 출력은 매번 다를 수 있다. 통과와 실패가 분명한 검사를 거친 것만 완료로 친다 |

---

## 2. 규칙

`Tools/verify.sh <모드>`

| 모드 | 무엇 | 걸리는 시간 | 언제 |
|---|---|---|---|
| `core` | 게임 규칙과 데이터 테스트를 Unity 없이 (`dotnet test`) | 몇 초 | Core, Data, CSV 를 바꿀 때마다. 에디터가 열려 있어도 된다 |
| `unity` | Unity 를 화면 없이 열어 컴파일 에러 확인 | 30초~1분 | View, App 을 바꿨을 때 |
| `play` | 씬을 화면 없이 실제로 돌리는 테스트 | 1~2분 | View, App 을 바꿨을 때 |
| `all` | 위 셋을 차례로 | 2~3분 | 기능을 끝냈을 때 |

종료 코드: 0 통과, 1 실패, 2 사용법 오류, 3 Unity 에디터가 프로젝트를 열고 있음.

테스트 소스는 한 벌이다. `Assets/Tests/EditMode/` 의 테스트는 Unity 의 Test Runner 창과 `dotnet test` 양쪽에서 돈다.
`Tools/CoreTests/` 의 프로젝트 파일이 `Assets/Game/Core`, `Assets/Game/Data` 소스를 그대로 가져다 컴파일한다.

---

## 3. 조정 가능한 수치

| 수치 | 현재 값 | 의미 | 바꾸는 곳 |
|---|---|---|---|
| UNITY_TIMEOUT | 420 | Unity 실행을 강제로 끝내는 시간 (초) | `Tools/verify.sh` |
| VERIFY_OUT | 임시 폴더 | 로그와 결과 파일 위치 | 환경변수 |

---

## 4. 핵심 코드

| 파일 | 역할 |
|---|---|
| `Tools/verify.sh` | 검증 스크립트 |
| `Tools/CoreTests/Game.Core/Game.Core.csproj` | Core, Data 를 netstandard2.1 과 C# 9 로 컴파일 (Unity 와 같은 수준 강제) |
| `Tools/CoreTests/Game.Tests/Game.Tests.csproj` | EditMode 테스트를 dotnet 으로 실행 |
| `Assets/Tests/EditMode/` | 규칙과 데이터 테스트 |
| `Assets/Tests/PlayMode/` | 씬을 띄우는 테스트 |
| `.claude/scripts/changelog_append.sh` | 응답이 끝날 때 바뀐 `.cs`, `.csv` 를 `CHANGELOG.md` 에 한 줄 기록 (Stop 훅) |

---

## 5. 변경 이력

| 날짜 | 변경 내용 | 관련 파일 |
|---|---|---|
| 2026-11-16 | 첫 구현 | verify.sh, CoreTests |

---

## 6. 주의사항

- **Unity 에디터가 프로젝트를 열고 있으면 `unity`, `play` 는 돌지 않는다** (종료 코드 3). 에디터를 닫거나 에디터의 Test Runner 창에서 돌린다. `core` 는 상관없다
- **Unity 실행 옵션을 바꾸지 않는다.** `-nographics` 를 넣었다 뺐다 하면 전체 재임포트로 10분 넘게 걸린 적이 있다
- **화면 없는 실행에서 `WaitForEndOfFrame` 은 오지 않는다.** 테스트가 끝없이 멈춘다. 쓰지 않는다
- PlayMode 실행이 중간에 끊기면 `Assets/InitTestScene*.unity` 가 남는다. 스크립트가 지운다
- 강제 종료 뒤 `Temp/UnityLockfile` 이 남을 수 있다. 스크립트는 잠금 파일과 실행 중인 Unity 가 둘 다 있을 때만 막는다
- 테스트용 asmdef 는 `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]` 형식을 쓴다. `optionalUnityReferences` 를 같이 쓰면 중복 참조로 컴파일이 깨진다
- Core 와 Data 는 C# 9, .NET Standard 2.1 까지만 쓴다. NUnit 은 3.x 공통 기능만 (Unity 의 NUnit 이 오래된 버전이다)
- 화면 글자 (IMGUI) 는 스크린샷에 찍히지 않는다. 글자 깨짐은 사람이 봐야 한다
