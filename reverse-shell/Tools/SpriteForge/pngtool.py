"""Minimal PNG read/write in pure Python (no dependencies) for the SpriteForge tools.

Reads non-interlaced PNGs: grayscale, RGB, palette, gray+alpha and RGBA at 1/2/4/8-bit depth (16-bit
is downsampled to 8). Writes 8-bit RGBA.
"""
import struct
import sys
import zlib

CHANNELS = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}


def _unfilter(raw, h, stride, fbpp):
    rows, prev, i = [], bytearray(stride), 0
    for _ in range(h):
        f = raw[i]
        line = bytearray(raw[i + 1:i + 1 + stride])
        i += 1 + stride
        for x in range(stride):
            a = line[x - fbpp] if x >= fbpp else 0
            b = prev[x]
            c = prev[x - fbpp] if x >= fbpp else 0
            if f == 1:
                line[x] = (line[x] + a) & 255
            elif f == 2:
                line[x] = (line[x] + b) & 255
            elif f == 3:
                line[x] = (line[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(line)
        prev = line
    return rows


def read_png(path):
    data = open(path, 'rb').read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n', path
    pos, idat, plte, trns = 8, b'', None, None
    while pos < len(data):
        n, = struct.unpack('>I', data[pos:pos + 4])
        t = data[pos + 4:pos + 8]
        c = data[pos + 8:pos + 8 + n]
        pos += 12 + n
        if t == b'IHDR':
            w, h, depth, ctype, _, _, inter = struct.unpack('>IIBBBBB', c)
        elif t == b'PLTE':
            plte = c
        elif t == b'tRNS':
            trns = c
        elif t == b'IDAT':
            idat += c
    assert inter == 0, 'interlaced PNG not supported: ' + path
    ch = CHANNELS[ctype]
    bits = depth * ch
    stride = (w * bits + 7) // 8
    rows = _unfilter(zlib.decompress(idat), h, stride, max(1, bits // 8))

    out = []
    for line in rows:
        if depth == 16:
            vals = [line[i] for i in range(0, len(line), 2)]  # keep the high byte
        elif depth == 8:
            vals = list(line)
        else:
            per = 8 // depth
            mask = (1 << depth) - 1
            vals = []
            for byte in line:
                for k in range(per):
                    vals.append((byte >> (8 - depth * (k + 1))) & mask)
        row = []
        for x in range(w):
            px = vals[x * ch:(x + 1) * ch]
            if ctype == 6:
                row.append(tuple(px))
            elif ctype == 2:
                row.append(tuple(px) + (255,))
            elif ctype == 3:
                k = px[0]
                al = trns[k] if trns and k < len(trns) else 255
                row.append(tuple(plte[k * 3:k * 3 + 3]) + (al,))
            elif ctype == 0:
                v = px[0] * 255 // ((1 << depth) - 1) if depth < 8 else px[0]
                row.append((v, v, v, 255))
            else:
                row.append((px[0],) * 3 + (px[1],))
        out.append(row)
    return w, h, out


def write_png(path, w, h, px):
    raw = b''.join(b'\x00' + bytes(v for p in row for v in p) for row in px)

    def chunk(t, c):
        return struct.pack('>I', len(c)) + t + c + struct.pack('>I', zlib.crc32(t + c) & 0xffffffff)

    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
                           + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


if __name__ == '__main__':
    # usage: pngtool.py grid in.png out.png cell scale  -> enlarged copy with a cell grid, for inspecting sheets
    _, cmd, src, dst, cell, scale = sys.argv
    cell, scale = int(cell), int(scale)
    w, h, px = read_png(src)
    W, H = w * scale, h * scale
    out = [[px[y // scale][x // scale] for x in range(W)] for y in range(H)]
    for y in range(H):
        for x in range(W):
            if (x // scale) % cell == 0 and x % scale == 0 or (y // scale) % cell == 0 and y % scale == 0:
                out[y][x] = (255, 255, 0, 255)
    write_png(dst, W, H, out)
    print(w, h)
