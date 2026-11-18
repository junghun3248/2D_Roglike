"""GPT 생성 그림(_imports/gpt/pilot)을 게임용 스프라이트 시트로 정리한다.

실행: python -I Tools/art/process_gpt_pilot.py  (저장소 루트에서)
출력: Assets/_Game/Art/Generated/*.png, sheets.json

하는 일
1. 투명 열 기준으로 프레임을 잘라낸다 (프레임 수가 다르면 실패).
2. 반투명 가장자리를 단단하게(알파 0/255) 만들어 흐린 테두리·옅은 배경 안개를 없앤다.
3. 모든 프레임을 같은 크기 칸에 발밑(가장 아래 불투명 줄) 기준 가운데 정렬한다.
4. 게임 해상도로 줄이고 색 수를 줄여 픽셀아트처럼 보이게 한다.
5. 배경 3겹, 바닥·발판 타일을 만든다.
"""

import json
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "_imports", "gpt", "pilot")
OUT = os.path.join(ROOT, "Assets", "_Game", "Art", "Generated")

ALPHA_CUT = 128          # 이 값 이상만 불투명으로 남긴다
SLICE_ALPHA = 40         # 프레임 분리용 투명 판정
HERO_HEIGHT = 96         # 게임 속 주인공 키(px)
HERO_CELL = (128, 128)
SLIME_WIDTH = 56
SLIME_CELL = (64, 64)
SHEET_COLORS = 48
BG_HEIGHT = 540
TILE_SCALE = 0.22        # 바닥·발판 원본 축소 비율


def load(name):
    return Image.open(os.path.join(SRC, name)).convert("RGBA")


def harden(img):
    a = np.array(img)
    a[..., 3] = np.where(a[..., 3] >= ALPHA_CUT, 255, 0)
    a[a[..., 3] == 0, :3] = 0
    return Image.fromarray(a, "RGBA")


def slice_frames(img, expected):
    alpha = np.array(img)[..., 3]
    cols = (alpha > SLICE_ALPHA).any(axis=0)
    runs, start = [], None
    for x, filled in enumerate(cols):
        if filled and start is None:
            start = x
        elif not filled and start is not None:
            runs.append((start, x))
            start = None
    if start is not None:
        runs.append((start, len(cols)))
    # 아주 작은 조각(머리카락 끝, 검 광택)은 가장 가까운 프레임에 붙인다.
    big = [r for r in runs if r[1] - r[0] > 12]
    for r in runs:
        if r in big:
            continue
        nearest = min(range(len(big)), key=lambda i: min(abs(big[i][0] - r[1]), abs(r[0] - big[i][1])))
        big[nearest] = (min(big[nearest][0], r[0]), max(big[nearest][1], r[1]))
    if len(big) != expected:
        sys.exit(f"프레임 수가 맞지 않음: {len(big)} != {expected}")
    frames = []
    for x0, x1 in big:
        # 옅은 배경 안개(반투명)를 먼저 없애야 실제 몸 크기로 잘린다.
        frame = harden(img.crop((x0, 0, x1, img.height)))
        frames.append(frame.crop(frame.getbbox()))
    return frames


def quantize(img, colors):
    alpha = img.getchannel("A")
    rgb = img.convert("RGB").quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
    rgb.putalpha(alpha)
    return rgb


def body_anchor_x(img):
    """몸통 기준 가로 위치. 청록색(검·검기·스카프)은 프레임마다 크게 달라서 빼고 무게중심을 잡는다."""
    a = np.array(img).astype(np.int32)
    opaque = a[..., 3] > 0
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    cyan = (g > r + 40) & (b > r + 40)
    body = opaque & ~cyan
    mask = body if body.sum() > 50 else opaque
    xs = np.nonzero(mask)[1]
    return float(xs.mean()) if xs.size else img.width / 2


def build_sheet(frames, scale, cell, colors, anchor_body=False):
    cw, ch = cell
    sheet = Image.new("RGBA", (cw * len(frames), ch), (0, 0, 0, 0))
    for i, frame in enumerate(frames):
        w = max(1, round(frame.width * scale))
        h = max(1, round(frame.height * scale))
        small = harden(frame.resize((w, h), Image.Resampling.LANCZOS))
        if h > ch:
            sys.exit(f"칸 높이({ch})보다 큰 프레임: {h}")
        # 발밑 정렬 + 가로는 몸통(또는 상자) 가운데를 칸 가운데에 둔다.
        anchor = body_anchor_x(small) if anchor_body else w / 2
        x = i * cw + round(cw / 2 - anchor)
        x = min(max(x, i * cw), i * cw + cw - w)  # 칸 밖으로 나가면 안쪽으로 붙인다
        if w > cw:
            sys.exit(f"칸 폭({cw})보다 큰 프레임: {w}")
        y = ch - h
        sheet.alpha_composite(small, (x, y))
    return quantize(sheet, colors)


def save(img, name):
    img.save(os.path.join(OUT, name), optimize=True)


def save_frames(sheet, name, cell, count):
    """시트를 칸별 PNG로도 저장한다: Generated/<name>/<name>_<i>.png (Unity에서 Single 스프라이트로 가져오기 쉽다)."""
    folder = os.path.join(OUT, name)
    os.makedirs(folder, exist_ok=True)
    for old in os.listdir(folder):
        if old.endswith(".png"):
            os.remove(os.path.join(folder, old))
    for i in range(count):
        sheet.crop((i * cell[0], 0, (i + 1) * cell[0], cell[1])).save(
            os.path.join(folder, f"{name}_{i}.png"), optimize=True)


def make_tileable_x(img, blend):
    """좌우 이음매가 티 나지 않게 양 끝을 섞는다."""
    a = np.array(img).astype(np.float32)
    w = a.shape[1]
    left = a[:, :blend].copy()
    right = a[:, w - blend:].copy()
    t = np.linspace(0.0, 1.0, blend)[None, :, None]
    a[:, :blend] = right * (1 - t) + left * t
    return Image.fromarray(a[:, : w - blend].round().astype(np.uint8), "RGBA")


def main():
    os.makedirs(OUT, exist_ok=True)
    sheets = {}

    hero = {
        "hero_idle": (load("hero_idle_strip.png"), 6),
        "hero_attack": (load("hero_attack_strip.png"), 6),
        "hero_run": (load("hero_run_strip.png"), 8),
    }
    hero_frames = {k: slice_frames(img, n) for k, (img, n) in hero.items()}
    # 세 애니메이션 공통 배율: 대기 프레임 평균 키를 HERO_HEIGHT에 맞춘다.
    idle_height = np.mean([f.height for f in hero_frames["hero_idle"]])
    hero_scale = HERO_HEIGHT / idle_height
    # GPT가 공격 포즈를 더 작게 그려서, 공격은 프레임 키 중앙값으로 따로 맞춘다 (자세가 낮아 약간 작게).
    attack_median = float(np.median([f.height for f in hero_frames["hero_attack"]]))
    scales = {
        "hero_idle": hero_scale,
        "hero_run": hero_scale,
        "hero_attack": HERO_HEIGHT * 0.92 / attack_median,
    }
    for name, frames in hero_frames.items():
        scale = scales[name]
        # 칼 궤적 때문에 넓은 프레임이 있으면 칸 폭을 16단위로 늘린다 (발밑 가운데 기준이라 위치는 그대로).
        # 몸통 기준 정렬이라 칼 쪽으로 튀어나온 만큼 여유가 필요: 가장 넓은 프레임의 2배 폭까지 허용.
        widest = max(round(f.width * scale) for f in frames)
        tallest = max(round(f.height * scale) for f in frames)
        cell = (max(HERO_CELL[0], -(-int(widest * 1.6) // 16) * 16), max(HERO_CELL[1], -(-tallest // 16) * 16))
        sheet = build_sheet(frames, scale, cell, SHEET_COLORS, anchor_body=True)
        save_frames(sheet, name, cell, len(frames))
        sheets[name] = {"cell": list(cell), "frames": len(frames), "pivot": [cell[0] / 2, 0]}

    slime = slice_frames(load("slime_strip.png"), 6)
    slime_scale = SLIME_WIDTH / np.mean([f.width for f in slime[:4]])
    save_frames(build_sheet(slime[:4], slime_scale, SLIME_CELL, 32), "slime_idle", SLIME_CELL, 4)
    save_frames(build_sheet(slime[4:], slime_scale, SLIME_CELL, 32), "slime_hurt", SLIME_CELL, 2)
    sheets["slime_idle"] = {"cell": list(SLIME_CELL), "frames": 4, "pivot": [SLIME_CELL[0] / 2, 0]}
    sheets["slime_hurt"] = {"cell": list(SLIME_CELL), "frames": 2, "pivot": [SLIME_CELL[0] / 2, 0]}

    for name, opaque in (("bg_sky", True), ("bg_hills", False), ("bg_forest", False)):
        img = load(name + ".png")
        w = round(img.width * BG_HEIGHT / img.height)
        img = img.resize((w, BG_HEIGHT), Image.Resampling.LANCZOS)
        if not opaque:
            img = harden(img)
        save(img, name + ".png")

    tiles = load("tiles_ground.png")
    alpha = np.array(tiles)[..., 3]
    rows = (alpha > SLICE_ALPHA).sum(axis=1)
    # 위쪽 큰 덩어리(풀+흙): 처음 불투명 줄부터 덩어리가 끊기기 전까지
    top = int(np.argmax(rows > tiles.width * 0.15))          # 풀 끝
    solid = int(np.argmax(rows > tiles.width * 0.9))         # 흙이 꽉 차기 시작
    bottom = solid
    while bottom < tiles.height and rows[bottom] > tiles.width * 0.5:
        bottom += 1
    grass_dirt = tiles.crop((0, top, tiles.width, bottom))
    # 풀 띠 = 풀 끝부터 흙이 찬 뒤 조금 더 (풀 아래 그림자까지)
    dirt_start = int((solid - top) + (bottom - solid) * 0.18)
    small_w = round(tiles.width * TILE_SCALE)

    top_strip = grass_dirt.crop((0, 0, tiles.width, dirt_start))
    top_strip = top_strip.resize((small_w, round(top_strip.height * TILE_SCALE)), Image.Resampling.LANCZOS)
    save(quantize(harden(make_tileable_x(top_strip, 12)), 32), "ground_top.png")

    fill = grass_dirt.crop((0, dirt_start, tiles.width, grass_dirt.height))
    fill = fill.resize((small_w, round(fill.height * TILE_SCALE)), Image.Resampling.LANCZOS)
    fill = make_tileable_x(fill, 12)
    # 세로 이음매도 섞는다 (아래로 반복).
    fill = make_tileable_x(fill.rotate(90, expand=True), 8).rotate(-90, expand=True)
    filled = np.array(fill)
    filled[..., 3] = 255
    save(quantize(Image.fromarray(filled, "RGBA"), 24), "ground_fill.png")

    # 발판: 아래쪽 마지막 큰 줄 덩어리, 가운데 한 마디만 반복 단위로 쓴다.
    plank_rows = [y for y in range(bottom + 40, tiles.height) if rows[y] > tiles.width * 0.6]
    if not plank_rows:
        sys.exit("발판을 찾지 못함")
    plank = tiles.crop((0, plank_rows[0], tiles.width, plank_rows[-1] + 1))
    pw = plank.width
    segment = plank.crop((int(pw * 0.26), 0, int(pw * 0.5), plank.height))
    segment = segment.resize((round(segment.width * TILE_SCALE), round(segment.height * TILE_SCALE)), Image.Resampling.LANCZOS)
    save(quantize(harden(make_tileable_x(segment, 6)), 24), "platform.png")

    with open(os.path.join(OUT, "sheets.json"), "w", encoding="utf-8") as f:
        json.dump(sheets, f, indent=2)
    print("hero scale %.3f, slime scale %.3f" % (hero_scale, slime_scale))
    for name in sorted(os.listdir(OUT)):
        if name.endswith(".png"):
            print(name, Image.open(os.path.join(OUT, name)).size)


if __name__ == "__main__":
    main()
