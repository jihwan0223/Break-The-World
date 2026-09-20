"""
align_object_sprites.py로 이미지 캔버스 크기를 바꾼 뒤,
Unity의 .meta 파일에 저장된 스프라이트 잘라내기 좌표(rect)와 PPU를 새 값에 맞게 고쳐주는 스크립트.

이걸 안 하면 Unity가 이미지를 내용물에 딱 맞게, 그것도 파편마다 여러 조각으로 잘라서(자동 슬라이스)
캔버스 정렬이 깨지고, 단계별로 맞춰둔 PPU도 반영되지 않음. 스프라이트를 캔버스 전체 하나로 합쳐줌.

사용법:
    1. 먼저 Unity에서 새 이미지를 임포트해서 .meta 파일이 생기게 함 (Assets 새로고침)
    2. 아래 TARGETS에 (메타파일 경로, 새 가로, 새 세로, PPU)를 채움
       - 가로/세로/PPU는 align_object_sprites.py가 저장하면서 출력한 값
    3. python update_sprite_meta_rect.py 실행 (프로젝트 루트에서)
    4. Unity에서 Assets 새로고침
"""

import re

# (메타 파일 경로, 새 가로, 새 세로, PPU)
TARGETS = [
    ("Assets/3_Image/ObjectImage/Object6.png.meta", 831, 1247, 81.2),
    ("Assets/3_Image/ObjectImage/Object6-1.png.meta", 1024, 1536, 100),
    ("Assets/3_Image/ObjectImage/Object6-2.png.meta", 998, 1498, 97.5),
    ("Assets/3_Image/ObjectImage/Object6-3.png.meta", 1024, 1536, 100),
]

NAME_ENTRY = r"  - first:\n      213: -?\d+\n    second: \S+\n"  # internalIDToNameTable 항목 하나


def update_meta(path, new_w, new_h, ppu):
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    stem = path.split("/")[-1][: -len(".png.meta")]  # 예: Object6-1
    name = f"{stem}_0"  # Unity가 자동 슬라이스로 만든 첫 스프라이트 이름 - 이것만 남기고 나머지(파편 조각)는 지움

    # 1) 이름 표: 첫 스프라이트 것만 남김
    content, n = re.subn(
        r"(  internalIDToNameTable:\n)((?:" + NAME_ENTRY + r")+)",
        lambda m: m.group(1) + "".join(e for e in re.findall(NAME_ENTRY, m.group(2)) if e.endswith(f"second: {name}\n")),
        content,
    )
    if n != 1:
        print("ERROR: internalIDToNameTable을 못 찾음 ->", path)
        return

    # 2) 스프라이트 목록: 첫 스프라이트 하나만 남기고 rect를 캔버스 전체로
    head, rest = content.split("    sprites:\n", 1)
    sprites_part, tail = re.split(r"(?m)^(?=    outline: \[\]\n)", rest, maxsplit=1)
    blocks = re.split(r"(?=    - serializedVersion: 2\n      name:)", sprites_part)
    kept = [b for b in blocks if b.startswith(f"    - serializedVersion: 2\n      name: {name}\n")]
    if len(kept) != 1:
        print("ERROR: 스프라이트", name, "를 못 찾음 ->", path)
        return
    block, rect_count = re.subn(
        r"(rect:\n        serializedVersion: 2\n        x: )\d+\n        y: \d+\n        width: \d+\n        height: \d+",
        rf"\g<1>0\n        y: 0\n        width: {new_w}\n        height: {new_h}",
        kept[0],
    )
    if rect_count != 1:
        print("ERROR: rect를 못 찾음 ->", path)
        return
    content = head + "    sprites:\n" + block + tail

    # 3) 이름-ID 표도 첫 스프라이트만
    content, n = re.subn(
        r"(    nameFileIdTable:\n)((?:      .+\n)+)",
        lambda m: m.group(1) + "".join(l for l in m.group(2).splitlines(keepends=True) if l.startswith(f"      {name}:")),
        content,
    )
    if n != 1:
        print("ERROR: nameFileIdTable을 못 찾음 ->", path)
        return

    content, ppu_count = re.subn(r"(\n  spritePixelsToUnits: )[\d.]+", rf"\g<1>{ppu}", content)
    if ppu_count != 1:
        print("ERROR: spritePixelsToUnits를 못 찾음 ->", path)
        return

    with open(path, "w", encoding="utf-8") as f:
        f.write(content)
    print("updated", path, f"({new_w}x{new_h}, PPU {ppu})")


if __name__ == "__main__":
    for path, w, h, ppu in TARGETS:
        update_meta(path, w, h, ppu)
