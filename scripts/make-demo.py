# Builds the README demo GIFs from sample data. Needs Pillow.
#   bin\ClaudeUsageTaskbar.exe --demo-frames <folder>
#   python scripts/make-demo.py <folder>
import glob
import os
import sys

from PIL import Image

FRAME_MS = 80
HOLD_LAST_MS = 1800
COLORS = 128


def build(folder, target):
    paths = sorted(glob.glob(os.path.join(folder, '*.png')))
    images = [Image.open(path).convert('RGB') for path in paths]
    palette = images[len(images) // 2].quantize(COLORS, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    frames = [image.quantize(palette=palette, dither=Image.Dither.NONE) for image in images]
    durations = [FRAME_MS] * (len(frames) - 1) + [HOLD_LAST_MS]
    frames[0].save(target, save_all=True, append_images=frames[1:], duration=durations, loop=0, optimize=True, disposal=1)


root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
for language in ('en', 'pt-BR'):
    build(os.path.join(sys.argv[1], language), os.path.join(root, 'docs', 'images', 'demo-%s.gif' % language))
