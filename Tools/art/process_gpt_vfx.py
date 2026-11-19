"""Four-frame VFX strips -> centered pixel sprites (Python 3.12, Pillow, numpy)."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "_imports/gpt/vfx"
OUT = ROOT / "Assets/_Game/Art/Generated/vfx"
ALPHA_CUT = 128
FRAME_COUNT = 4
PADDING = 2
SIZES = {"slash_arc": (96, 64), "impact_burst": (48, 48), "slam_wave": (128, 48)}


def harden(image):
    pixels = np.array(image.convert("RGBA"))
    pixels[..., 3] = np.where(pixels[..., 3] >= ALPHA_CUT, 255, 0)
    pixels[pixels[..., 3] == 0, :3] = 0
    return Image.fromarray(pixels)


def split_strip(image, gap_fraction=0.1):
    # Detached sparks belong to the same frame. Merge only short transparent gaps;
    # count the resulting groups independently rather than forcing four crops.
    alpha = np.array(image)[..., 3]
    occupied = (alpha > 0).any(axis=0)
    edges = np.flatnonzero(np.diff(np.r_[False, occupied, False]))
    min_gap = image.width / FRAME_COUNT * gap_fraction
    groups = []
    for left, right in zip(edges[::2], edges[1::2]):
        if groups and left - groups[-1][1] < min_gap:
            groups[-1][1] = int(right)
        else:
            groups.append([int(left), int(right)])
    if len(groups) != FRAME_COUNT:
        raise ValueError(f"Wrong frame count: {len(groups)} != {FRAME_COUNT}; check transparent gutters")
    frames = []
    for left, right in groups:
        frame = image.crop((left, 0, right, image.height))
        bounds = frame.getbbox()
        if bounds is None:
            raise ValueError("Wrong frame count: empty frame")
        frames.append(frame.crop(bounds))
    return frames


def process(name, size):
    with Image.open(SRC / f"{name}.png") as source:
        if "A" not in source.getbands() or source.getchannel("A").getextrema() != (0, 255):
            raise ValueError(f"{name}: real transparent alpha required")
        frames = split_strip(harden(source), 0.2 if name == "impact_burst" else 0.1)
    width = max(frame.width for frame in frames)
    height = max(frame.height for frame in frames)
    scale = min((size[0] - 2 * PADDING) / width, (size[1] - 2 * PADDING) / height)
    outputs = []
    for frame in frames:
        # The generated slash opens right; mirror its convex blade toward the target.
        if name == "slash_arc":
            frame = ImageOps.mirror(frame)
        canvas = Image.new("RGBA", (width, height))
        canvas.alpha_composite(frame, ((width - frame.width) // 2, (height - frame.height) // 2))
        small = harden(canvas.resize((round(width * scale), round(height * scale)), Image.Resampling.BOX))
        output = Image.new("RGBA", size)
        output.alpha_composite(small, ((size[0] - small.width) // 2, (size[1] - small.height) // 2))
        outputs.append(output)
    return outputs


def main():
    outputs = {name: process(name, size) for name, size in SIZES.items()}
    OUT.mkdir(parents=True, exist_ok=True)
    for name, frames in outputs.items():
        for index, frame in enumerate(frames):
            frame.save(OUT / f"{name}_{index}.png", optimize=True)
        print(f"{name}: {len(frames)} frames, {frames[0].size}, alpha 0/255")


if __name__ == "__main__":
    main()
