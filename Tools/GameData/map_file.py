"""Reads the source game's .map files (client data, maps/*.map).

Layout (little endian):
    int32 size                  the map is size x size cells
    int32 textureCount, then textureCount zero-terminated texture names
    int32 cellCount, then cellCount cells:
        int32 index             y * size + x
        uint8 flags             0: walkable; 0x20: blocked (walls, pillars); 0x60: blocked, outside the playable area
        3 x layer: uint8 present, then (int32 texture, int32 extra) when present
    (a short block of objects follows; not needed here)
Cells missing from the list are outside the map.
"""
import struct

WALKABLE = 0


def read(path):
    data = open(path, 'rb').read()
    pos = 0

    def i32():
        nonlocal pos
        value = struct.unpack_from('<i', data, pos)[0]
        pos += 4
        return value

    def u8():
        nonlocal pos
        value = data[pos]
        pos += 1
        return value

    def cstr():
        nonlocal pos
        end = data.index(b'\0', pos)
        text = data[pos:end].decode('latin1')
        pos = end + 1
        return text

    size = i32()
    textures = [cstr() for _ in range(i32())]
    flags = {}
    for _ in range(i32()):
        index = i32()
        flag = u8()
        for _layer in range(3):
            if u8():
                i32()
                i32()
        flags[(index % size, index // size)] = flag
    return size, textures, flags


def walkable(flags, x, y):
    return flags.get((x, y)) == WALKABLE
