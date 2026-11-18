# 그림 정리 도구

GPT가 만든 그림(`_imports/gpt/pilot/`)을 게임용 스프라이트로 바꾼다.

```bash
python -I Tools/art/process_gpt_pilot.py
```

- 출력: `Assets/_Game/Art/Generated/` — 애니메이션은 프레임별 PNG(`hero_idle/hero_idle_0.png` …), 배경 3겹, 바닥·발판 타일, `sheets.json`.
- 하는 일: 투명 열로 프레임 자르기 → 반투명 테두리·배경 안개 제거 → 몸통 기준 발밑 정렬 → 주인공 96px·슬라임 56px로 축소 → 색 수 줄이기.
- 다시 실행해도 결과가 같다. 그림을 새로 뽑으면 같은 파일 이름으로 `_imports/gpt/pilot/`에 넣고 다시 실행한 뒤,
  Unity 메뉴 **Roglike > Build Prototype Scene / Build Run Scene**으로 씬을 다시 만든다.
- Unity 가져오기 설정(PPU 48, Point 필터, 발밑 피벗)은 `Assets/_Game/Scripts/Editor/GeneratedArtImporter.cs`가 자동으로 건다.
- 필요: Python 3.12 + Pillow + numpy.
