# Builds the README animations from frames. Needs Pillow.
#   bin\ClaudeUsageTaskbar.exe --gif-frames <folder>
#   python scripts/make-gifs.py <folder>
import glob
import os
import sys

from PIL import Image

COLORS = 255

# name -> (milliseconds per frame, milliseconds to hold the last frame, languages)
GIFS = {
    'mascot-moods': (100, 600, False),
    'mascot-working': (110, 600, False),
    'mascot-moves': (60, 700, False),
    'mascot-outfits': (100, 600, False),
    'mascot-reactions': (100, 600, False),
    'mascot-walks': (80, 1200, False),
    'mascot-acts': (80, 1200, False),
    'icon-styles': (70, 1600, False),
    'chart': (90, 900, True),
    'themes': (1300, 1300, True),
    'settings': (1600, 1600, True),
}


def build(folder, target, frame_ms, hold_ms):
    paths = sorted(glob.glob(os.path.join(folder, '*.png')))
    images = [Image.open(path).convert('RGB') for path in paths]
    # One palette from several frames, so colors do not flicker between them.
    picks = images[::max(1, len(images) // 6)]
    width, height = images[0].size
    mosaic = Image.new('RGB', (width * len(picks), height))
    for i, image in enumerate(picks):
        mosaic.paste(image, (i * width, 0))
    palette = mosaic.quantize(COLORS, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    frames = [image.quantize(palette=palette, dither=Image.Dither.NONE) for image in images]
    durations = [frame_ms] * (len(frames) - 1) + [hold_ms]
    frames[0].save(target, save_all=True, append_images=frames[1:], duration=durations, loop=0, optimize=True, disposal=1)
    print('%s %d frames %d KB' % (os.path.basename(target), len(frames), os.path.getsize(target) // 1024))


root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
out = os.path.join(root, 'docs', 'images')
for name, (frame_ms, hold_ms, languages) in GIFS.items():
    if languages:
        for language in ('en', 'pt-BR'):
            build(os.path.join(sys.argv[1], name, language), os.path.join(out, '%s-%s.gif' % (name, language)), frame_ms, hold_ms)
    else:
        build(os.path.join(sys.argv[1], name), os.path.join(out, name + '.gif'), frame_ms, hold_ms)
