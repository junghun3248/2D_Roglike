# 2D_Roglike — Agent Instructions

Claude, Codex 등 이 저장소에서 작업하는 모든 AI 에이전트가 따르는 규칙이다.
게임 방향과 우선순위는 [기획서](./docs/GDD.md)가 기준이다. 작업 전에 관련 절을 읽는다.
기능 기획·연출·발표 자료를 고민할 때는 [2026 졸업작품 전시 참고 노트](./docs/graduation-showcase-2026.md)도 참고한다.

## 1. 프로젝트

- 2D 횡스크롤 액션 로그라이트. 핵심 재미: **타격 쾌감 · 딜 성장 체감 · 빌드의 재미**.
- Unity `6000.3.10f1` (LTS), 2D URP, Input System, Cinemachine 3.
- 1인 개발 + AI. 사람은 에디터 조립·손맛 튜닝·최종 판단, AI는 코드·데이터 구조를 주로 맡는다.

## 2. 폴더 구조

게임 자산은 모두 `Assets/_Game/` 아래에 둔다 (외부 에셋과 섞이지 않게).

```text
Assets/_Game/
  Scripts/
    Core/        게임 루프, 시간 제어(히트스톱·슬로모), 공용 유틸
    Combat/      공격, 피격, 데미지 계산, 타격 연출
    Player/      이동, 입력, 상태
    Enemies/     적 AI, 스포너
    Progression/ 능력치(PlayerStats), 런 내 성장(3택1), 영구 성장
    Run/         한 판의 흐름 (구역 → 강화 → 보스 → 결과)
    UI/          HUD, 데미지 숫자, 메뉴
    Data/        ScriptableObject 정의 (스킬·적·아이템 데이터 타입)
  Data/          ScriptableObject 에셋 (실제 수치)
  Prefabs/
  Scenes/
  Art/  Audio/  VFX/
Assets/ThirdParty/   외부 에셋 (수정하지 않는다)
```

- 폴더는 실제로 필요해질 때 만든다. 빈 자리표시 폴더를 미리 만들지 않는다.
- 각 `Scripts/` 하위 폴더는 같은 이름의 Assembly Definition을 가질 수 있다. 도입할 때는 의존 방향(`Core` ← 나머지)을 지킨다.

## 3. C# 코드 규칙

- 네임스페이스: `Roglike.<폴더명>` (예: `Roglike.Combat`).
- 이름: 클래스·메서드·프로퍼티 `PascalCase`, private 필드 `_camelCase`, 지역 변수·매개변수 `camelCase`.
- 인스펙터 노출은 `public` 필드 대신 `[SerializeField] private`.
- 한 파일 = 한 주요 타입. 파일명은 타입명과 같다.
- **수치는 코드에 박지 않는다.** 데미지·쿨다운·속도 등 조정 대상 값은 ScriptableObject나 `[SerializeField]`로 뺀다.
- **콘텐츠 추가 = 데이터 추가**가 되도록 설계한다. 새 스킬·적·아이템이 기존 코드 수정 없이 데이터로 늘어나야 한다.
- `Update`에서 매 프레임 `GetComponent`, `Find`, LINQ, 문자열 조합으로 할당하지 않는다. 캐시한다.
- 시간 감속(오버드라이브)을 고려한다: 게임플레이는 `Time.deltaTime`, 감속 영향을 받지 않아야 하는 UI·연출은 `Time.unscaledDeltaTime`.
- 큰 데미지 수는 `long` 또는 `double`로 다룬다 (후반 억 단위).
- 주석은 "왜"가 필요할 때만, 한국어 가능.
- 새 패키지·외부 라이브러리는 필요성을 설명하고 사람의 확인을 받은 뒤 추가한다.

## 4. Unity 작업 규칙

- `.meta` 파일은 항상 원본과 함께 커밋한다. 에셋을 옮길 때는 meta도 같이 옮긴다.
- 씬·프리팹 YAML을 손으로 크게 편집하지 않는다. 필요하면 에디터 스크립트나 사람에게 맡긴다.
- 에디터에서 해야 하는 작업(씬 배치, 컴포넌트 연결, 수치 조정)은 끝에 **사람이 할 일 목록**으로 명확히 적는다.
- 코드를 바꾼 뒤에는 컴파일 오류가 없는지 확인한다:
  `Unity.exe -batchmode -quit -projectPath <repo> -logFile <log>` 후 로그에서 `error CS` 검색.
  (에디터가 이 프로젝트를 열고 있으면 batchmode가 실패하므로 그 경우 사람에게 확인을 요청한다.)

## 5. Git 규칙

- 커밋 메시지: `<type>(<scope>): <한국어 설명>` — 본문은 선택.
  - type: `feat` `fix` `refactor` `perf` `docs` `style` `test` `build` `ci` `chore` `revert`
  - scope: 소문자·숫자·하이픈 (예: `combat`, `player`, `ui`)
  - 제목 72자 이내, 끝에 마침표 없음. 예: `feat(combat): 히트스톱 추가`
- 한 커밋 = 한 기능 단위. 무관한 변경을 섞지 않는다.
- 작업은 브랜치에서 하고 `main`에 직접 커밋하지 않는다.
- 커밋·푸시는 사람이 요청할 때만 한다.
- 이미지·사운드·폰트 등 바이너리는 Git LFS로 관리된다 (`.gitattributes` 참고).

## 6. 작업 방식

1. 요청과 관련된 기획서 절, 기존 코드를 먼저 읽는다.
2. 작은 단위로 구현한다. 기획서에 없는 큰 기능은 먼저 제안하고 확인받는다.
3. 컴파일을 확인한다.
4. 바꾼 것, 확인하지 못한 것, 사람이 에디터에서 해야 할 일을 보고한다.
5. 기획이 바뀌었으면 [docs/GDD.md](./docs/GDD.md)도 함께 갱신한다.
