"""1지역(멈춘 숲) 적·보스·소품 그림(_imports/gpt/area1)을 게임용 프레임으로 정리한다.

실행: python -I Tools/art/process_gpt_area1.py  (저장소 루트에서)
출력: Assets/_Game/Art/Generated/<동작>/<동작>_<i>.png, 소품 PNG

주인공 스크립트(process_gpt_pilot.py)의 도구를 그대로 쓰되, 적은 프레임마다 발밑을 맞추지 않고
"공통 바닥선" 기준으로 높이를 유지한다 (다람쥐 점프처럼 공중 프레임이 공중에 남도록).
"""

import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import process_gpt_pilot as pilot  # noqa: E402  (같은 폴더의 공용 도구)

SRC = os.path.join(pilot.ROOT, "_imports", "gpt", "area1")
OUT = pilot.OUT


def slice_with_baseline(img, expected):
    """프레임을 자르고, 각 프레임이 공통 바닥선에서 얼마나 떠 있는지(px)도 돌려준다."""
    hard = pilot.harden(img)
    alpha = np.array(hard)[..., 3]
    cols = (alpha > 0).any(axis=0)
    runs, start = [], None
    for x, filled in enumerate(cols):
        if filled and start is None:
            start = x
        elif not filled and start is not None:
            runs.append((start, x))
            start = None
    if start is not None:
        runs.append((start, len(cols)))
    runs = [r for r in runs if r[1] - r[0] > 12]
    if len(runs) != expected:
        sys.exit(f"프레임 수가 맞지 않음: {len(runs)} != {expected}")

    frames, bottoms = [], []
    for x0, x1 in runs:
        frame = hard.crop((x0, 0, x1, hard.height))
        box = frame.getbbox()
        frames.append(frame.crop(box))
        bottoms.append(box[3])
    baseline = max(bottoms)
    lifts = [baseline - b for b in bottoms]
    return frames, lifts


def export(frames, lifts, scale, cell, groups, colors):
    """groups: {동작 이름: [프레임 번호...]} → Generated/<동작>/<동작>_<i>.png"""
    cw, ch = cell
    for name, indices in groups.items():
        sheet = Image.new("RGBA", (cw * len(indices), ch), (0, 0, 0, 0))
        for slot, index in enumerate(indices):
            frame = frames[index]
            w = max(1, round(frame.width * scale))
            h = max(1, round(frame.height * scale))
            lift = round(lifts[index] * scale)
            if w > cw or h + lift > ch:
                sys.exit(f"{name}: 칸({cw}x{ch})보다 큰 프레임 {w}x{h}+{lift}")
            small = pilot.harden(frame.resize((w, h), Image.Resampling.LANCZOS))
            sheet.alpha_composite(small, (slot * cw + (cw - w) // 2, ch - h - lift))
        pilot.save_frames(pilot.quantize(sheet, colors), name, cell, len(indices))
        print(name, len(indices), "frames", cell)


def single(img, scale, name, colors=32):
    hard = pilot.harden(img)
    hard = hard.crop(hard.getbbox())
    w, h = max(1, round(hard.width * scale)), max(1, round(hard.height * scale))
    small = pilot.harden(hard.resize((w, h), Image.Resampling.LANCZOS))
    pilot.save(pilot.quantize(small, colors), name + ".png")
    print(name, small.size)


def load(name):
    return Image.open(os.path.join(SRC, name)).convert("RGBA")


def main():
    os.makedirs(OUT, exist_ok=True)

    # 태엽 멧돼지: 몸통 폭 약 88px
    boar, lifts = slice_with_baseline(load("boar_strip.png"), 6)
    scale = 88 / np.mean([f.width for f in boar[:2]])
    export(boar, lifts, scale, (128, 96), {
        "boar_idle": [0, 1], "boar_run": [2, 3], "boar_telegraph": [4], "boar_stunned": [5],
    }, 48)

    # 톱니 다람쥐: 키 약 56px
    squirrel, lifts = slice_with_baseline(load("squirrel_strip.png"), 5)
    scale = 56 / np.mean([f.height for f in squirrel[:2]])
    export(squirrel, lifts, scale, (96, 96), {
        "squirrel_idle": [0, 1], "squirrel_move": [2], "squirrel_windup": [3], "squirrel_throw": [4],
    }, 48)

    # 태엽 사슴왕: 키 약 168px (PPU 48 → 3.5 단위)
    stag, lifts = slice_with_baseline(load("stag_strip.png"), 6)
    scale = 168 / np.mean([f.height for f in stag[:2]])
    export(stag, lifts, scale, (256, 208), {
        "stag_idle": [0, 1], "stag_charge": [2], "stag_sweep": [3], "stag_rear": [4], "stag_enraged": [5],
    }, 64)

    # 소품: 톱니 투사체, 낙엽, 닫힌 문, 열린 문
    props, _ = slice_with_baseline(load("props_strip.png"), 4)
    single(props[0], 20 / props[0].width, "gear_projectile")
    single(props[1], 22 / props[1].width, "leaf_projectile")
    gate_scale = 192 / props[2].height
    single(props[2], gate_scale, "gate_closed", 40)
    single(props[3], gate_scale, "gate_open", 40)


if __name__ == "__main__":
    main()
