# Procedural SFX

Python 3.12 + numpy가 설치된 환경에서 프로젝트 루트에서 실행:

```powershell
python Tools/audio/make_sfx.py
```

외부 음원 없이 고정 seed로 합성한다. 출력은 `Assets/_Game/Audio/Sfx/`의
44,100 Hz / mono / signed 16-bit PCM WAV 8개다. 재실행해도 동일한 결과다.
스윙은 필터 노이즈, 타격은 저음 사인 sweep과 밝은 노이즈 클릭,
치명타는 금속성 비정수 배음, 처치는 저음과 노이즈 꼬리를 사용한다.
양 끝 fade와 0.92 peak 정규화로 클리핑을 막는다.

`PrototypeSceneBuilder.BuildFromCommandLine`이 SfxLibrary를 만들고 두 씬에
12 voice SfxPlayer를 연결한다. 기존 library 항목과 볼륨은 보존한다.
새 음원은 library에 id / clip / volume 항목으로 추가할 수 있다.
기본 pitch 편차 ±6%, Overdrive 중 0.85배이며 Time.timeScale에는 연동하지 않는다.
타격음은 동일 swing의 첫 유효 명중에서 한 번 선택한다(처치 > 치명타 > 일반).
뒤늦게 다른 적을 맞혀도 중복 재생하지 않는다.
