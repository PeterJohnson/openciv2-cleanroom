#!/usr/bin/env python3
"""Produce original synthetic P03 map-facing replacements for the native matrix."""
import hashlib
import json
from pathlib import Path
import sys

from PIL import Image, ImageDraw


def palette_sheet(seed):
    image = Image.new('P', (640, 480), 253)  # Translates to the P03 key at palette index 7.
    colors = [0, 0, 0]
    for index in range(1, 256):
        colors.extend(((seed * 29 + index * 63) % 240 + 15,
                       (seed * 83 + index * 23) % 240 + 15,
                       (seed * 11 + index * 48) % 240 + 15))
    image.putpalette(colors)
    return image, ImageDraw.Draw(image)


def diamond(draw, x, y, fill):
    for row in range(32):
        half_span = 32 - abs(2 * row + 1 - 32)
        draw.line((x + 32 - half_span, y + row, x + 31 + half_span, y + row), fill=fill)


def motif(draw, x, y, width, height, fill, hole=253):
    draw.polygon([(x + width // 2, y + 3), (x + width - 6, y + height // 2),
                  (x + width // 2, y + height - 4), (x + 6, y + height // 2)], fill)
    draw.rectangle((x + width // 2 - 2, y + height // 2 - 2,
                    x + width // 2 + 2, y + height // 2 + 2), hole)


def terrain_one(directory):
    image, draw = palette_sheet(5)
    for terrain in range(11):
        row = 1 + terrain * 33
        diamond(draw, 1, row, 8 + terrain)
        motif(draw, 131, row, 64, 32, 32 + terrain)
        motif(draw, 196, row, 64, 32, 48 + terrain)
    for index in range(6):
        motif(draw, 456, 100 + 33 * index, 64, 32, 61 + index)
    for index in range(9):
        motif(draw, 1 + 65 * index, 364, 64, 32, 71 + index)
        motif(draw, 1 + 65 * index, 397, 64, 32, 81 + index)
    # Palette slot 10 leaves the blend source visible. Key-color holes in
    # the stencil cut the copied image without using the cell's corner color.
    diamond(draw, 1, 447, 0)
    for y in range(449, 478, 3):
        draw.line((1, y, 64, y), fill=255)
    diamond(draw, 131, 447, 69)
    diamond(draw, 196, 447, 86)
    image.save(directory / 'terrain1.BmP')


def terrain_two(directory):
    image, draw = palette_sheet(7)
    for index in range(16):
        x = 1 + 65 * (index % 8)
        y = 33 * (index // 8)
        for group, top in enumerate((67, 133, 199, 265)):
            motif(draw, x, top + y, 64, 32, 23 + 16 * group + index)
    for index in range(4):
        motif(draw, 1 + 65 * index, 331, 64, 32, 84 + index)
    for index in range(8):
        x = 1 + 66 * index
        for q, (dx, y) in enumerate(((0, 429), (33, 463), (0, 446), (0, 463))):
            motif(draw, x + dx, y, 32, 16, 91 + 8 * q + index)
    image.save(directory / 'Terrain2.GIF', optimize=False, interlace=False)


def units(directory):
    image, draw = palette_sheet(9)
    for index in range(63):
        x, y = 1 + 65 * (index % 9), 1 + 49 * (index // 9)
        motif(draw, x, y, 64, 48, 11 + index)
        draw.point((x + 7, y - 1), 250)
        draw.point((x + 17, y - 1), 250)
        draw.point((x - 1, y + 6), 250)
    motif(draw, 586, 1, 12, 20, 18)
    image.save(directory / 'Units.GiF', optimize=False, interlace=False)
    (directory / 'UNITS.BMP').write_bytes(b'BM\x00\x00')


def cities(directory):
    image, draw = palette_sheet(11)
    for style in range(6):
        y = 39 + 49 * style
        for size in range(4):
            for wall in range(2):
                x = (334 if wall else 1) + 65 * size
                motif(draw, x, y, 64, 48, 44 + 8 * style + 2 * size + wall)
                draw.point((x + 16, y - 1), 250)
                draw.point((x - 1, y + 24), 250)
                draw.point((x + 42, y - 1), 249)
                draw.point((x - 1, y + 7), 249)
    for color in range(8):
        x = 1 + 15 * color
        motif(draw, x, 425, 14, 22, 71 + color)
        motif(draw, x, 448, 14, 22, 91 + color)
        draw.point((x, 423), 71 + color)
        draw.point((x + 3, 429), 71 + color)
        draw.point((x + 6, 429), 91 + color)
    for index, x in enumerate((143, 208, 273, 338)):
        motif(draw, x, 423, 64, 48, 96 + index)
    draw.rectangle((355, 434, 360, 441), 252)  # Translates to primary mask index 6.
    draw.rectangle((365, 434, 370, 441), 251)  # Translates to secondary mask index 5.
    image.save(directory / 'cities.gif', optimize=False, interlace=False)


def native(directory):
    images = {}
    for key, box, rgb in (('terrain/2', (64, 32), '#a649a7'),
                          ('unit/32', (64, 48), '#176cff'),
                          ('city/47', (64, 48), '#ec7529')):
        image = Image.new('RGBA', box)
        draw = ImageDraw.Draw(image)
        if box[1] == 32:
            diamond(draw, 0, 0, rgb)
        else:
            motif(draw, 0, 0, *box, rgb, hole=(0, 0, 0, 0))
        path = key.replace('/', '-') + '.png'
        image.save(directory / path)
        item = {'path': path, 'sha256': hashlib.sha256((directory / path).read_bytes()).hexdigest()}
        if box[1] == 48:
            item['marker'] = [22, 6]
        images[key] = item
    (directory / 'map-art.json').write_text(json.dumps({'format': 'openciv2-map-art/1', 'images': images}, indent=2) + '\n')


def main():
    root = Path(sys.argv[1])
    scenario, installed, native_root = (root / name for name in ('scenario', 'installation', 'native'))
    for directory in scenario, installed, native_root:
        directory.mkdir(parents=True, exist_ok=True)
    terrain_one(scenario)
    terrain_two(scenario)
    units(scenario)
    cities(installed)
    native(native_root)


if __name__ == '__main__':
    main()
