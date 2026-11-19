"""UI 원본을 투명 열로 분리하고 픽셀 스프라이트로 만든다.

실행: python -I Tools/art/process_gpt_ui.py
"""

from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "_imports/gpt/ui"
OUT = ROOT / "Assets/_Game/Art/Generated/ui"
ALPHA_CUT = 128
PADDING = 0.06
SKILLS = ("DashSlash", "Slam", "StasisField", "SwordWave", "Whirlwind")
SYNERGIES = ("Blade", "Critical", "Fury", "Overdrive", "Tempo", "Vitality")
# 머리카락 바깥쪽과 어깨를 덜어 내 얼굴이 원형 마스크 중심에 오게 한다.
PORTRAIT_CROP = (0.18, 0.16, 0.94, 0.92)


def harden(image):
    pixels = np.array(image.convert("RGBA"))
    pixels[..., 3] = np.where(pixels[..., 3] >= ALPHA_CUT, 255, 0)
    pixels[pixels[..., 3] == 0, :3] = 0
    return Image.fromarray(pixels)


def load(name):
    with Image.open(SRC / name) as image:
        if "A" not in image.getbands() or image.getchannel("A").getextrema()[0] != 0:
            raise ValueError(f"{name}: 투명 알파 배경이 필요합니다")
        return harden(image)


def split_strip(image, expected):
    columns = (np.array(image)[..., 3] > 0).any(axis=0)
    edges = np.flatnonzero(np.diff(np.r_[False, columns, False]))
    runs = list(zip(edges[::2], edges[1::2]))
    if len(runs) != expected:
        raise ValueError(f"아이콘 수 불일치: {len(runs)} != {expected}; 투명 열을 확인하세요")
    frames = []
    for left, right in runs:
        frame = image.crop((int(left), 0, int(right), image.height))
        frames.append(frame.crop(frame.getbbox()))
    return frames


def square_icon(image, size):
    side = int(np.ceil(max(image.size) / (1 - 2 * PADDING)))
    square = Image.new("RGBA", (side, side))
    square.alpha_composite(image, ((side - image.width) // 2, (side - image.height) // 2))
    return harden(square.resize((size, size), Image.Resampling.BOX))


def main():
    outputs = {}
    for filename, ids, prefix, size in (
        ("skill_icons.png", SKILLS, "skill", 64),
        ("synergy_icons.png", SYNERGIES, "synergy", 32),
    ):
        frames = split_strip(load(filename), len(ids))
        outputs.update({f"{prefix}_{identifier}.png": square_icon(frame, size)
                        for identifier, frame in zip(ids, frames)})
    portrait = load("hero_portrait.png")
    bounds = tuple(round(value * (portrait.width if i % 2 == 0 else portrait.height))
                   for i, value in enumerate(PORTRAIT_CROP))
    outputs["portrait_hero.png"] = harden(portrait.crop(bounds).resize((96, 96), Image.Resampling.BOX))
    OUT.mkdir(parents=True, exist_ok=True)
    for name, image in outputs.items():
        image.save(OUT / name, optimize=True)
        print(f"{name}: {image.width}x{image.height}")


if __name__ == "__main__":
    main()
