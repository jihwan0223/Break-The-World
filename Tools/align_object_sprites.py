"""
오브젝트의 체력 단계별 이미지(1,2,3,4단계)를 같은 월드 크기의 캔버스에 올려서
부서지는 동안 위치/크기가 안 튀게 만드는 스크립트.

이미지 픽셀은 절대 리사이즈하지 않는다. 단계가 진행될수록 파편이 사방으로 튀어서 알파 bbox가 커지는데,
bbox 기준으로 배율을 맞추면 파편이 많은 단계일수록 본체가 작아져 버리기 때문.
대신 크기 차이는 Pixels Per Unit(PPU)로 맞춘다. (단계마다 그림 크기가 다르게 나와도 화면에선 같은 크기로 보이게)

  - 세로: 본체 맨 아래(바닥선)를 모든 단계에서 같은 높이에 맞춤. 파편은 바닥선 계산에서 제외됨
  - 가로: 본체 맨 아래 받침의 가운데를 캔버스 가운데에 맞춤
  - 캔버스 크기: frame_from 단계의 (캔버스 크기 / PPU)를 월드 크기 기준으로 삼고, 단계마다 PPU를 곱해서 픽셀 크기를 정함

사용법:
    1. 아래 OBJECT_SETS에 오브젝트를 추가 (files: 1단계~마지막 단계 순서, ppus: 단계별 PPU)
    2. python align_object_sprites.py
    3. 출력되는 캔버스 크기와 PPU를 update_sprite_meta_rect.py의 TARGETS에 넣고 실행

(Pillow, numpy 필요: pip install pillow numpy)
"""

import numpy as np
from PIL import Image

# 정렬할 오브젝트들.
#   files: 체력 100% -> 부서진 순서의 이미지 파일
#   ppus: 단계별 PPU. 그림에서 본체가 작게 나온 단계는 PPU를 낮춰서 화면에선 같은 크기로 보이게 함 (생략하면 전부 100)
#   frame_from: 월드 크기와 바닥 여백의 기준이 될 단계 번호 (0부터). 보통 본체가 온전하고 캔버스가 넉넉한 단계
OBJECT_SETS = [
    {
        "files": ["Object6.png", "Object6-1.png", "Object6-2.png", "Object6-3.png"],
        "ppus": [81.2, 100, 97.5, 100],
        "frame_from": 1,
    },
]

ALPHA_THRESHOLD = 16  # 이 값 이하 알파는 "빈 곳"으로 봄 (배경 제거 때 남는 거의 투명한 잔상 무시용)
BODY_ROW_RATIO = 0.3  # 가장 넓은 줄의 이 비율 이상 채워진 줄만 본체로 봄 (바닥에 튄 작은 파편 무시용)


def body_baseline(alpha):
    """본체 맨 아래 줄의 아래쪽 경계(y)와, 그 받침의 가로 가운데(x)를 돌려줌"""
    row_counts = alpha.sum(axis=1)
    body_rows = np.where(row_counts >= BODY_ROW_RATIO * row_counts.max())[0]
    bottom_edge = body_rows[-1] + 1

    # 받침 안쪽(바닥에서 10~30줄 위)에서 가장 긴 가로 덩어리의 가운데를 잡음 (옆에 붙은 파편은 다른 덩어리라 무시됨)
    centers = []
    for y in range(bottom_edge - 30, bottom_edge - 10):
        xs = np.where(alpha[y])[0]
        if len(xs) == 0:
            continue
        runs = np.split(xs, np.where(np.diff(xs) > 1)[0] + 1)
        longest = max(runs, key=len)
        centers.append((longest[0] + longest[-1] + 1) / 2)
    return bottom_edge, float(np.median(centers))


def align_set(files, ppus, frame_from):
    imgs = [Image.open(f).convert("RGBA") for f in files]
    alphas = [np.array(im.getchannel("A")) > ALPHA_THRESHOLD for im in imgs]
    baselines = [body_baseline(a) for a in alphas]

    # 월드 크기(유닛)와 바닥 여백(유닛)의 기준
    ref = imgs[frame_from]
    frame_w = ref.width / ppus[frame_from]
    frame_h = ref.height / ppus[frame_from]
    margin = (ref.height - baselines[frame_from][0]) / ppus[frame_from]
    print(f"월드 크기 {frame_w:.3f} x {frame_h:.3f} 유닛, 바닥 여백 {margin:.3f} 유닛")

    results = []  # (파일명, 캔버스 가로, 세로, PPU) - .meta 수정할 때 사용
    for f, im, ppu, (bottom_edge, base_cx) in zip(files, imgs, ppus, baselines):
        cw = int(round(frame_w * ppu))
        ch = int(round(frame_h * ppu))
        x = int(round(cw / 2 - base_cx))
        y = int(round(ch - margin * ppu)) - bottom_edge

        # 투명한 여백은 잘려도 되지만 내용물이 캔버스 밖으로 나가면 안 됨
        left, top, right, bottom = im.getchannel("A").point(lambda a: 255 if a > ALPHA_THRESHOLD else 0).getbbox()
        assert left + x >= 0 and top + y >= 0 and right + x <= cw and bottom + y <= ch, (f, (x, y), (cw, ch))

        canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        canvas.paste(im, (x, y))  # 마스크 없이 붙여야 알파가 제곱으로 깎이지 않음
        canvas.save(f)  # 원본 덮어쓰기
        results.append((f, cw, ch, ppu))
        print(f"saved {f}: canvas {cw}x{ch}, PPU {ppu}, offset ({x}, {y}), 바닥선 {bottom_edge}, 받침 중심 {base_cx:.1f}")

    return results


if __name__ == "__main__":
    for s in OBJECT_SETS:
        align_set(s["files"], s.get("ppus", [100] * len(s["files"])), s.get("frame_from", 0))
