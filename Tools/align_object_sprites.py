"""
오브젝트의 체력 단계별 이미지(1,2,3,4단계)들끼리
- 크기를 1단계 기준으로 비율 맞춰 정규화하고
- 같은 캔버스 크기 + 같은 바닥선(baseline)에 배치해서
부서지는 동안 위치/크기가 안 튀게 만드는 스크립트.

사용법:
    python align_object_sprites.py

아래 OBJECT_SETS에 파일 목록만 추가하면 여러 오브젝트를 한 번에 처리할 수 있음.
(Pillow 필요: pip install pillow)
"""

from PIL import Image

# 정규화할 오브젝트들. 각 리스트의 첫 번째 파일이 "기준 크기"가 됨
OBJECT_SETS = [
    ["Object1.png", "Object1-1.png", "Object1-2.png", "Object1-3.png"],
    ["Object2.png", "Object2-1.png", "Object2-2.png", "Object2-3.png"],
    ["Object3.png", "object3-1.png", "object3-2.png", "object3-3.png"],
]

MARGIN = 100  # 캔버스 가장자리 여백 (px)


def align_set(files):
    imgs = [Image.open(f).convert("RGBA") for f in files]
    bboxes = [im.getbbox() for im in imgs]  # 알파 기준 실제 내용물 영역
    heights = [b[3] - b[1] for b in bboxes]
    widths = [b[2] - b[0] for b in bboxes]

    target_height = heights[0]  # 1단계(첫 파일) 기준
    scales = [target_height / h for h in heights]
    new_widths = [int(w * s) for w, s in zip(widths, scales)]
    new_heights = [int(h * s) for h, s in zip(heights, scales)]

    canvas_w = max(new_widths) + MARGIN * 2
    canvas_h = target_height + MARGIN * 2

    print(files[0], "scales:", scales, "canvas:", canvas_w, canvas_h)

    results = []  # (파일명, 새 캔버스 크기) - .meta 수정할 때 사용
    for f, im, bbox, nw, nh in zip(files, imgs, bboxes, new_widths, new_heights):
        content = im.crop(bbox).resize((nw, nh), Image.LANCZOS)

        canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
        x = (canvas_w - nw) // 2
        y = canvas_h - MARGIN - nh  # 바닥선에 맞춰 붙임
        canvas.paste(content, (x, y), content)

        canvas.save(f)  # 원본 덮어쓰기 (필요하면 f.replace(".png", "_aligned.png")로 바꿔서 먼저 확인)
        results.append((f, canvas_w, canvas_h))
        print("saved", f, "->", (canvas_w, canvas_h))

    return results


if __name__ == "__main__":
    for file_set in OBJECT_SETS:
        align_set(file_set)
