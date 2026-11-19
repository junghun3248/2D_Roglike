# 그림 정리 도구

## 검격·명중·착지 VFX

`python -I Tools/art/process_gpt_vfx.py` (Python 3.12 + Pillow + numpy).
원본은 `_imports/gpt/vfx/`의 `slash_arc.png`, `impact_burst.png`, `slam_wave.png`이다.
각각 동일 셀 크기의 가로 4프레임이며, 알파 정리 후 완전 투명 열로 분리한다.
흩어진 스파크 사이의 짧은 빈 열은 합친다(셀 너비 대비 명중 20%, 나머지 10% 미만).
이렇게 얻은 그룹 수가 4개가 아니면 모든 파일 저장 전에 실패한다.
알파 128 미만을 제거하고 최종 알파는 0/255로 고정한다.
프레임별 효과 중심을 공통 캔버스 중심에 맞추고 전체 프레임에 같은 축척을 적용하여
BOX로 96×64 / 48×48 / 128×48 픽셀로 만든다. 원본 베기는 볼록한 날이 오른쪽을 향하도록 좌우 반전한다.
출력은 `Assets/_Game/Art/Generated/vfx/<name>_<0..3>.png`이며 재실행 결과는 동일하다.
Unity 설정은 Sprite / Point / 무압축 / 중심 피벗 / PPU 48이다.
`PrototypeSceneBuilder.BuildFromCommandLine`이 `VfxAnimationBuilder`를 통해
`Data/Vfx/`의 `SpriteAnimationData` 3개를 만들고 두 씬의 `VfxSprites`에 연결한다.
프레임 시간은 기존 데이터의 fps로 조절한다(기본 베기 0.028초, 명중 0.025초, 착지 0.04초).
프레임이 할당되면 비감속 시간으로 재생하며, 없으면 기존 사각형 VFX로 돌아간다.

## UI 아이콘·초상화

`python -I Tools/art/process_gpt_ui.py` (Python 3.12 + Pillow + numpy).
`_imports/gpt/ui/`의 skill_icons.png(5개), synergy_icons.png(6개), hero_portrait.png를 읽는다.
알파 128 미만의 가장자리를 제거하고 투명 열로 분리하며, 개수가 다르거나 투명 알파가 없으면 출력 전에 실패한다.
아이콘은 잘라낸 뒤 정사각형 여백 6%를 주고 BOX로 스킬 64×64, 시너지 32×32로 축소한다.
초상화는 `PORTRAIT_CROP`의 정규화 좌표로 얼굴 중심을 잘라 96×96로 만든다.
최종 알파는 0/255이며 재실행 결과는 동일하다. 출력은 `Assets/_Game/Art/Generated/ui/`이다.
Unity 가져오기는 Sprite / Point / 무압축 / 중심 피벗이며, 씬 빌더를 실행하면 기존 데이터에도 아이콘이 연결된다.
원본은 저장소의 기존 `_imports/` 제외 규칙에 따라 Git에서 제외된다.

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
