"""
align_object_sprites.py로 이미지 캔버스 크기를 바꾼 뒤,
Unity의 .meta 파일에 저장된 스프라이트 잘라내기 좌표(rect)를 새 크기에 맞게 고쳐주는 스크립트.

이걸 안 하면 이미지는 커졌는데 스프라이트는 예전 좌표로 잘려서 이상하게 보임.

사용법:
    1. 아래 TARGETS에 (메타파일 경로, 그 안에서 실제 쓰이는 스프라이트의 internalID, 새 가로, 새 세로)를 채움
       - internalID는 Unity 씬(.unity) 파일에서 해당 이미지 guid를 검색하면
         "{fileID: <이 값>, guid: <이미지 guid>, type: 3}" 형태로 찾을 수 있음
    2. python update_sprite_meta_rect.py 실행
"""

import re

# (메타 파일 경로, internalID, 새 가로, 새 세로)
TARGETS = [
    ("Assets/3_Image/ObjectImage/Object1.png.meta", 1275030799, 1394, 1323),
    ("Assets/3_Image/ObjectImage/Object1-1.png.meta", -5934715673664778258, 1394, 1323),
    ("Assets/3_Image/ObjectImage/Object1-2.png.meta", -9220788892964207167, 1394, 1323),
    ("Assets/3_Image/ObjectImage/Object1-3.png.meta", 1379209726663985032, 1394, 1323),
    ("Assets/3_Image/ObjectImage/Object2.png.meta", -2340292433013277512, 703, 1025),
    ("Assets/3_Image/ObjectImage/Object2-1.png.meta", 6578731144817686623, 703, 1025),
    ("Assets/3_Image/ObjectImage/Object2-2.png.meta", -1385604352584601288, 703, 1025),
    ("Assets/3_Image/ObjectImage/Object2-3.png.meta", -8288779678485480098, 703, 1025),
    ("Assets/3_Image/ObjectImage/Object3.png.meta", 5378765685844372841, 961, 1027),
    ("Assets/3_Image/ObjectImage/object3-1.png.meta", -7309518164613636011, 961, 1027),
    ("Assets/3_Image/ObjectImage/object3-2.png.meta", -1730377823473803685, 961, 1027),
    ("Assets/3_Image/ObjectImage/object3-3.png.meta", -4773684954617920154, 961, 1027),
]


def update_meta(path, target_id, new_w, new_h):
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    # 스프라이트 항목 하나하나로 쪼갬
    blocks = re.split(r"(?=    - serializedVersion: 2\n      name:)", content)

    target_str = str(target_id)
    found = False
    for i, block in enumerate(blocks):
        if f"internalID: {target_str}\n" in block:
            found = True
            new_block = re.sub(
                r"(rect:\n        serializedVersion: 2\n        x: )\d+\n        y: \d+\n        width: \d+\n        height: \d+",
                rf"\g<1>0\n        y: 0\n        width: {new_w}\n        height: {new_h}",
                block,
            )
            if new_block == block:
                print("WARNING: rect를 못 찾음 ->", path)
            blocks[i] = new_block
            break

    if not found:
        print("ERROR: internalID를 못 찾음 ->", path)
        return

    with open(path, "w", encoding="utf-8") as f:
        f.write("".join(blocks))
    print("updated", path)


if __name__ == "__main__":
    for path, target_id, w, h in TARGETS:
        update_meta(path, target_id, w, h)
