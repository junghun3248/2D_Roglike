"""Deterministic original game SFX; Python 3.12 and numpy, no sample assets."""
from pathlib import Path
import wave
import numpy as np

RATE = 44100
OUTPUT = Path(__file__).resolve().parents[2] / "Assets/_Game/Audio/Sfx"


def synthesize(name, duration, seed):
    rng = np.random.default_rng(seed)
    t = np.arange(round(RATE * duration)) / RATE
    noise = rng.uniform(-1, 1, len(t))
    bright = noise - np.roll(noise, 1)
    phase = lambda start, end: 2 * np.pi * (start * t + (end - start) * t * t / (2 * duration))
    if name in ("swing", "dash"):
        airy = np.convolve(noise, np.ones(5) / 5, mode="same")
        signal = airy * np.sin(np.pi * t / duration) ** 1.8
        if name == "dash":
            signal += 0.15 * np.sin(phase(240, 65)) * np.exp(-t * 25)
    elif name == "jump":
        signal = np.sin(phase(240, 560)) * np.exp(-t * 26) + bright * np.exp(-t * 100) * 0.12
    else:
        # Band-limited crack leaves room for a low, pitched body.
        frequencies = np.fft.rfftfreq(len(t), 1 / RATE)
        spectrum = np.fft.rfft(noise)
        def band(low, high):
            filtered = np.fft.irfft(spectrum * ((frequencies >= low) & (frequencies <= high)), n=len(t))
            return filtered / max(np.sqrt(np.mean(filtered ** 2)), 1e-9)
        deep = name in ("kill", "land", "player_hurt")
        body_duration = {"kill": 0.25, "land": 0.11, "player_hurt": 0.16}.get(name, 0.085)
        start, end = (120, 45) if deep else (180, 60)
        sweep_time = np.minimum(t, body_duration)
        body_phase = 2 * np.pi * (start * sweep_time + (end - start) * sweep_time ** 2 / (2 * body_duration))
        envelope = np.maximum(1 - t / body_duration, 0) ** 1.2
        body = np.sin(body_phase) * envelope
        crack_duration = 0.07 if name == "kill" else 0.022
        crack_envelope = np.maximum(1 - t / crack_duration, 0) ** 1.5
        crack = band(600 if deep else 1000, 1800 if deep else 3000) * crack_envelope
        signal = 1.3 * body + (0.04 if name == "land" else 1.1) * crack
        if name in ("hit", "hit_crit"):
            signal += 0.04 * band(5000, 10000) * np.exp(-t * 140)
        signal = np.tanh(signal * 1.3)
        if name == "hit_crit":
            ring = sum(weight * np.sin(2 * np.pi * frequency * t)
                       for frequency, weight in ((1550, 1), (2070, 0.5), (2470, 0.3)))
            signal += 0.16 * ring * np.exp(-t / 0.05) * np.maximum(1 - t / 0.15, 0)
    # Fade both boundaries to avoid clicks unrelated to the designed impact.
    signal *= np.minimum(t / 0.001, 1) * np.minimum((duration - t) / 0.012, 1)
    signal *= (0.45 if name == "swing" else 0.4 if name == "land" else 0.92) / max(np.max(np.abs(signal)), 1e-9)
    return np.rint(signal * 32767).astype("<i2").tobytes()


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    sounds = {"swing": 0.09, "hit": 0.15, "hit_crit": 0.22, "kill": 0.28,
              "dash": 0.14, "jump": 0.12, "land": 0.13, "player_hurt": 0.23}
    for index, (name, duration) in enumerate(sounds.items()):
        with wave.open(str(OUTPUT / f"{name}.wav"), "wb") as wav:
            wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
            wav.writeframes(synthesize(name, duration, 20261119 + index))
        with wave.open(str(OUTPUT / f"{name}.wav"), "rb") as wav:
            samples = np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype(float) / 32768
        magnitude = np.abs(np.fft.rfft(samples))
        frequencies = np.fft.rfftfreq(len(samples), 1 / RATE)
        power = magnitude ** 2
        centroid = np.sum(frequencies * magnitude) / np.sum(magnitude)
        low_share = np.sum(power[frequencies < 300]) / np.sum(power)
        rms = np.sqrt(np.mean(samples ** 2))
        print(f"{name}: {duration:.2f}s centroid={centroid:.0f} Hz low(<300 Hz)={low_share:.1%} RMS={rms:.3f}")
        if name == "hit":
            assert 1500 <= centroid <= 3000, "Hit centroid outside the intended impact band"
            assert low_share >= 0.35, "Hit needs more low-frequency body"


if __name__ == "__main__":
    main()
