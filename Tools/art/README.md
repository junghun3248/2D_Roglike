# 그림 정리 도구

GPT가 만든 그림(`_imports/gpt/pilot/`)을 게임용 스프라이트로 바꾼다.

```bash
python -I Tools/art/process_gpt_pilot.py
```

주인공만 갱신할 때는 `python -I Tools/art/process_gpt_pilot.py --hero-only`를 사용한다.
이 옵션은 기존 hero 프레임 PNG만 덮어쓰며 다른 그림과 `sheets.json`은 건드리지 않는다.
몸체(청록색 검·스카프 제외)의 높이를 동작별 중앙값으로 ±8% 안에서 보정하고,
발바닥을 고정한다. 달리기는 두 걸음에 맞춰 0–3px 높이 변화를 남긴다.

- 출력: `Assets/_Game/Art/Generated/` — 애니메이션은 프레임별 PNG(`hero_idle/hero_idle_0.png` …), 배경 3겹, 바닥·발판 타일, `sheets.json`.
- 하는 일: 투명 열로 프레임 자르기 → 반투명 테두리·배경 안개 제거 → 몸통 기준 발밑 정렬 → 주인공 96px·슬라임 56px로 축소 → 색 수 줄이기.
- 다시 실행해도 결과가 같다. 그림을 새로 뽑으면 같은 파일 이름으로 `_imports/gpt/pilot/`에 넣고 다시 실행한 뒤,
  Unity 메뉴 **Roglike > Build Prototype Scene / Build Run Scene**으로 씬을 다시 만든다.
- Unity 가져오기 설정(PPU 48, Point 필터, 발밑 피벗)은 `Assets/_Game/Scripts/Editor/GeneratedArtImporter.cs`가 자동으로 건다.
- 필요: Python 3.12 + Pillow + numpy.

## 1지역 적·보스·소품

```bash
python -I Tools/art/process_gpt_area1.py
```

- 입력: `_imports/gpt/area1/` (멧돼지·다람쥐·사슴왕 동작 그림, 소품 4종).
- 출력: 동작별 폴더(`boar_idle`, `boar_telegraph`, `squirrel_throw`, `stag_rear` …)와 `gear_projectile`·`leaf_projectile`·`gate_closed`·`gate_open`.
- 적은 프레임마다 발밑을 맞추지 않고 **공통 바닥선** 기준으로 둔다 (다람쥐 점프처럼 공중 동작이 공중에 남도록).
- 크기: 멧돼지 몸통 폭 88px, 다람쥐 키 56px, 사슴왕 키 168px(약 3.5 단위), 문 키 192px(4 단위).
- 게임 연결: `EnemyPrefabBuilder`가 프리팹에 상태별 그림(`EnemySpriteAnimations` + AI의 `AnimationState`)을 입힌다.
