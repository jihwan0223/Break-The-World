"""
오브젝트의 체력 단계별 이미지(1,2,3,4단계)를 같은 캔버스 크기로 통일해서
부서지는 동안 위치/크기가 안 튀게 만드는 스크립트.

크기(배율)는 건드리지 않는다. 단계가 진행될수록 파편이 사방으로 튀어서 알파 bbox가 커지는데,
bbox 기준으로 배율을 맞추면 파편이 많은 단계일수록 본체가 작아져 버리기 때문.
그림을 그릴 때 이미 같은 비율로 그린다는 전제로, 캔버스만 바닥 기준으로 맞춰서 키운다.

사용법:
    python align_object_sprites.py

아래 OBJECT_SETS에 파일 목록만 추가하면 여러 오브젝트를 한 번에 처리할 수 있음.
(Pillow 필요: pip install pillow)
"""

from PIL import Image

# 정렬할 오브젝트들. 각 리스트의 첫 번째 파일이 가로 정렬 기준이 됨
OBJECT_SETS = [
    ["Object4.png", "Object4-1.png", "Object4-2.png", "Object4-3.png"],
]

ALPHA_THRESHOLD = 16  # 이 값 이하 알파는 "빈 곳"으로 봄 (배경 제거 때 남는 거의 투명한 잔상 무시용)


def content_bbox(im):
    return im.getchannel("A").point(lambda a: 255 if a > ALPHA_THRESHOLD else 0).getbbox()


def align_set(files):
    imgs = [Image.open(f).convert("RGBA") for f in files]
    bboxes = [content_bbox(im) for im in imgs]  # 알파 기준 실제 내용물 영역

    canvas_w = max(im.width for im in imgs)
    canvas_h = max(im.height for im in imgs)

    # 1단계 그림의 내용물 가로 중심 - 나머지 단계도 이 위치에 중심을 맞춤
    base_center_x = (bboxes[0][0] + bboxes[0][2]) / 2 + (canvas_w - imgs[0].width) // 2

    print(files[0], "canvas:", canvas_w, canvas_h)

    results = []  # (파일명, 새 캔버스 크기) - .meta 수정할 때 사용
    for f, im, bbox in zip(files, imgs, bboxes):
        center_x = (bbox[0] + bbox[2]) / 2
        x = int(round(base_center_x - center_x))
        y = canvas_h - im.height  # 바닥선에 맞춰 붙임

        canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
        canvas.paste(im, (x, y))  # 마스크 없이 붙여야 알파가 제곱으로 깎이지 않음

        canvas.save(f)  # 원본 덮어쓰기
        results.append((f, canvas_w, canvas_h))
        print("saved", f, "->", (canvas_w, canvas_h), "offset", (x, y))

    return results


if __name__ == "__main__":
    for file_set in OBJECT_SETS:
        align_set(file_set)
