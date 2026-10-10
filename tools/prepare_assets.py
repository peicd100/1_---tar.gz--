"""Format approved artwork and keep author font license; never redraw assets."""
from pathlib import Path
import shutil
from PIL import Image
from fontTools.ttLib import TTFont

root = Path(__file__).resolve().parents[1]
assets = root / 'assets'
source = assets / 'archive-box.png'
if not source.exists():
    raise SystemExit('Place the approved generated transparent PNG at assets/archive-box.png first.')
image = Image.open(source).convert('RGBA')
assert image.getextrema()[3][0] == 0, 'Image must retain transparent alpha'
image.save(assets / 'app.ico', sizes=[(n, n) for n in (16, 24, 32, 48, 64, 128, 256)])
for name, size in [('StoreLogo', 50), ('Square44x44Logo', 44), ('Square150x150Logo', 150)]:
    image.resize((size, size), Image.Resampling.LANCZOS).save(assets / (name + '.png'))
font = TTFont(assets / 'JasonHandwriting9.ttf')
names = font['name'].names
license_text = '\n\n'.join(dict.fromkeys(n.toUnicode() for n in names if n.nameID in (0, 8, 9, 13, 14)))
(assets / 'FONT-LICENSE.txt').write_text(license_text + '\n\n' + (assets / 'OFL-1.1.txt').read_text('utf-8'), encoding='utf-8')
print('Font:', next(n.toUnicode() for n in names if n.nameID == 1))
print('PNG:', image.size, 'alpha preserved; ICO:', sorted(Image.open(assets / 'app.ico').ico.sizes()))
