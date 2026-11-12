"""Floor instruction sprites (white on transparent) using the project's Pixelmax font.
Usage: python -I instructions.py <project Assets dir> <out dir> [preview.png]
"""
import sys, os
from PIL import Image, ImageDraw, ImageFont

ASSETS, OUT = sys.argv[1], sys.argv[2]
PREVIEW = sys.argv[3] if len(sys.argv) > 3 else None
FONT = os.path.join(ASSETS, 'Art/Font/Pixelmax-Regular.otf')
TITLE = ImageFont.truetype(FONT, 11)
SMALL = ImageFont.truetype(FONT, 8)
ON = 255

# Pixelmax has no digits -> hand-drawn 3x5 glyphs
DIGITS = {'1': ['.#.', '##.', '.#.', '.#.', '###'], '3': ['##.', '..#', '.#.', '..#', '##.']}


class Canvas:
    def __init__(self, w, h):
        self.im = Image.new('L', (w, h), 0)
        self.d = ImageDraw.Draw(self.im)
        self.d.fontmode = '1'

    def px(self, x, y, v=ON):
        if 0 <= x < self.im.width and 0 <= y < self.im.height:
            self.im.putpixel((x, y), v)

    def rect(self, x0, y0, x1, y1, fill=False):
        if fill:
            self.d.rectangle((x0, y0, x1, y1), fill=ON)
        else:
            self.d.rectangle((x0, y0, x1, y1), outline=ON)

    def text_size(self, s, font):
        b = font.getbbox(s)
        return b[2] - b[0], b[3] - b[1], b

    def text(self, x, y, s, font, v=ON):
        """Draw text with its ink top-left at (x, y)."""
        w, h, b = self.text_size(s, font)
        self.d.text((x - b[0], y - b[1]), s, font=font, fill=v)
        return w, h

    def digit(self, x, y, ch, v=ON):
        for j, row in enumerate(DIGITS[ch]):
            for i, c in enumerate(row):
                if c == '#':
                    self.px(x + i, y + j, v)


MOUSE = ['..#######..', '.#...#...#.', '#....#....#', '#....#....#', '#....#....#', '#....#....#',
         '###########', '#.........#', '#.........#', '#.........#', '#.........#', '#.........#',
         '#.........#', '.#.......#.', '..#.....#..', '...#####...']


def round_box(c, x0, y0, x1, y1):
    """1-px outline with the 4 corner pixels cut."""
    c.d.rectangle((x0, y0, x1, y1), outline=ON)
    for px, py in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)):
        c.px(px, py, 0)


# ---------- widgets: each returns (w, h) and draws at (x, y) when c is given ----------

def keycap(c, x, y, label, min_w=13):
    lw, lh, _ = Canvas.text_size(None, label, SMALL)
    w = max(min_w, lw + 6)
    h = 13  # 1 border + 2 pad + 6 letter + 2 pad + 2 bottom "depth"
    if c:
        # rounded outline, thicker bottom edge for a 3D key
        c.d.line((x + 1, y, x + w - 2, y), fill=ON)
        c.d.line((x, y + 1, x, y + h - 2), fill=ON)
        c.d.line((x + w - 1, y + 1, x + w - 1, y + h - 2), fill=ON)
        c.d.line((x + 1, y + h - 1, x + w - 2, y + h - 1), fill=ON)
        c.d.line((x + 1, y + h - 3, x + w - 2, y + h - 3), fill=ON)
        c.text(x + (w - lw + 1) // 2, y + 3, label, SMALL)
    return w, h


def mouse(c, x, y, button):
    """11x16 mouse, the pressed button ('L' or 'R') filled."""
    w, h = 11, 16
    if c:
        for j, row in enumerate(MOUSE):
            for i, ch in enumerate(row):
                if ch == '#':
                    c.px(x + i, y + j)
        seed = (x + 2, y + 3) if button == 'L' else (x + 8, y + 3)
        ImageDraw.floodfill(c.im, seed, ON)
    return w, h


def circle(c, cx, cy, r, fill=False):
    c.d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=ON, fill=ON if fill else None)


def arrow(c, x, y, direction):
    """3-px-wide arrow head pointing in direction; (x, y) is the tip."""
    dx, dy = {'u': (0, 1), 'd': (0, -1), 'l': (1, 0), 'r': (-1, 0)}[direction]
    c.px(x, y)
    for k in (-1, 0, 1):
        c.px(x + dx + (k if dx == 0 else 0), y + dy + (k if dy == 0 else 0))


def stick(c, x, y, label='L', arrows=True, press=False):
    """Analog stick: 13x13 circle with the letter, optional 4 arrows or a 'press' arrow above."""
    pad = 4 if arrows else 0
    top = 5 if press else 0
    w, h = 13 + pad * 2, 13 + pad * 2 + top
    if c:
        cx, cy = x + w // 2, y + top + pad + 6
        circle(c, cx, cy, 6)
        lw, lh, _ = c.text_size(label, SMALL)
        c.text(cx - lw // 2, cy - 2 - (lh - 6), label, SMALL)
        if arrows:
            arrow(c, cx, cy - 10, 'u'); arrow(c, cx, cy + 10, 'd')
            arrow(c, cx - 10, cy, 'l'); arrow(c, cx + 10, cy, 'r')
        if press:  # downward arrow pressing the stick
            c.d.line((cx, y, cx, y + 2), fill=ON)
            arrow(c, cx, y + 3, 'd')
    return w, h


def face_buttons(c, x, y, pressed):
    """Diamond of 4 buttons (N, S, W, E); the pressed one is filled."""
    w = h = 19
    if c:
        cx, cy = x + 9, y + 9
        pos = {'N': (cx, cy - 6), 'S': (cx, cy + 6), 'W': (cx - 6, cy), 'E': (cx + 6, cy)}
        for k, (bx, by) in pos.items():
            circle(c, bx, by, 3, fill=(k == pressed))
    return w, h


def shoulder(c, x, y, label='R', num='1'):
    lw, lh, _ = Canvas.text_size(None, label, SMALL)
    w, h = lw + 10, 10
    if c:
        round_box(c, x, y, x + w - 1, y + h - 1)
        c.text(x + 3, y + 2, label, SMALL)
        c.digit(x + 3 + lw + 1, y + 3, num)
    return w, h


def slash(c, x, y, h=14):
    if c:
        for i in range(h):
            c.px(x + (h - 1 - i) // 3, y + i)
    return (h + 2) // 3, h


def wasd(c, x, y):
    kw, kh = keycap(None, 0, 0, 'W')
    w, h = kw * 3 + 2, kh * 2 + 1
    if c:
        keycap(c, x + kw + 1, y, 'W')
        for i, k in enumerate('ASD'):
            keycap(c, x + i * (kw + 1), y + kh + 1, k)
    return w, h


# ---------- composition ----------

GAP = 6

def compose(title, keyboard, gamepad, subtitle=None):
    """keyboard / gamepad: lists of widget lambdas (c, x, y) -> (w, h)."""
    def row_size(items):
        sizes = [f(None, 0, 0) for f in items]
        return sum(s[0] for s in sizes) + 3 * (len(sizes) - 1), max(s[1] for s in sizes), sizes

    kw, kh, ks = row_size(keyboard)
    gw, gh, gs = row_size(gamepad)
    sw, sh = slash(None, 0, 0, max(kh, gh))
    body_w = kw + GAP + sw + GAP + gw
    body_h = max(kh, gh)
    tw, th, _ = Canvas.text_size(None, title, TITLE)
    stw, sth = (Canvas.text_size(None, subtitle, SMALL)[:2] if subtitle else (0, 0))
    W = max(body_w, tw, stw) + 2
    H = th + 4 + body_h + (sth + 4 if subtitle else 0) + 2
    W += W % 2; H += H % 2  # even sizes keep a centered pivot on whole pixels
    c = Canvas(W, H)
    c.text((W - tw) // 2, 1, title, TITLE)
    y0 = th + 5
    x = (W - body_w) // 2
    for f, s in zip(keyboard, ks):
        f(c, x, y0 + (body_h - s[1]) // 2); x += s[0] + 3
    x += GAP - 3
    slash(c, x, y0, body_h); x += sw + GAP
    for f, s in zip(gamepad, gs):
        f(c, x, y0 + (body_h - s[1]) // 2); x += s[0] + 3
    if subtitle:
        c.text((W - stw) // 2, y0 + body_h + 4, subtitle, SMALL)
    return c.im


SPRITES = {
    'Instr_Move':   ('MOVERSE', [wasd], [lambda c, x, y: stick(c, x, y, 'L')], None),
    'Instr_Attack': ('ATACAR', [lambda c, x, y: mouse(c, x, y, 'L')], [lambda c, x, y: face_buttons(c, x, y, 'W')], None),
    'Instr_Trail':  ('ESTELA', [lambda c, x, y: keycap(c, x, y, 'ESPACIO')], [lambda c, x, y: face_buttons(c, x, y, 'S')], 'MANTENER'),
    'Instr_Sprint': ('CORRER', [lambda c, x, y: keycap(c, x, y, 'SHIFT')], [lambda c, x, y: stick(c, x, y, 'L', arrows=False, press=True)], 'MANTENER'),
    'Instr_Glow':   ('BRILLAR', [lambda c, x, y: keycap(c, x, y, 'E')], [lambda c, x, y: face_buttons(c, x, y, 'N')], 'MANTENER'),
    'Instr_Throw':  ('LANZAR', [lambda c, x, y: mouse(c, x, y, 'R')], [lambda c, x, y: shoulder(c, x, y)], 'MANTENER Y SOLTAR'),
}

os.makedirs(OUT, exist_ok=True)
outs = []
for name, (title, kb, gp, sub) in SPRITES.items():
    a = compose(title, kb, gp, sub)
    rgba = Image.new('RGBA', a.size, (255, 255, 255, 0))
    rgba.putalpha(a)
    rgba.save(os.path.join(OUT, name + '.png'))
    outs.append((name, rgba))
    print(name, a.size)

if PREVIEW:
    S = 4
    W = max(r.width for _, r in outs) * S + 16
    H = sum(r.height * S + 12 for _, r in outs) + 4
    pv = Image.new('RGBA', (W, H), (52, 44, 40, 255))
    y = 4
    for _, r in outs:
        big = r.resize((r.width * S, r.height * S), Image.NEAREST)
        pv.alpha_composite(big, (8, y)); y += big.height + 12
    pv.save(PREVIEW)
