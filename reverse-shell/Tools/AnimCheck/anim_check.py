"""
Animation check: plays every party sprite clip in a headless browser, the way Unity slices it, and checks each frame.

    py -3.9 Tools/AnimCheck/anim_check.py [--out DIR] [--chrome PATH] [--keep] [--names A,B,...]

--names checks those Anim_<name> sets instead of the party (e.g. the machines and NPCs: RustSentry,Alt_RustSentry,...).

For every Anim_<name>.asset of A3's model (A3, Ashe, Erin, Lotus, Zuko) and every clip in it, this writes a page
(DIR/anim_check.html, the sheets embedded) that slices the sheet into frameSize cells like SpriteAnimationSet.Frames
(4 facing rows, row 0 = up at the top), then, per clip and facing (left and down; right and up are their mirrors):
  size      the sheet fits its .meta maxTextureSize (Unity shrinks bigger ones and the frames slice wrong)   FAIL
  empty     a frame with no pixels                                                                         FAIL
  clipped   a frame's pixels touching the cell's edge (cut off)                                            FAIL
  despawn   pixels that vanish from one frame to the next with nothing left near them (they should move,
            not disappear): over the clip's allowance                                                       FAIL
  count     a frame's pixel count jumping against the clip's median (parts dropping out)                    FAIL
  body      the portrait (menu idle): the body below the shoulders identical in every frame                FAIL
Headless Chrome runs the page's checks and plays every clip; the results are read back from the page (--dump-dom), and
screenshots of the page at a few moments are saved next to it. Exit code 1 if anything failed.
"""
import base64
import glob
import json
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
ASSETS = os.path.join(ROOT, 'Assets', '_Project')
ALLIES = ['A3', 'Ashe', 'Erin', 'Lotus', 'Zuko']

# How far (px) a pixel may move between frames and still count as "moved", by clip (legs swing far in walks/battle).
REACH = {'portrait': 2, 'idle': 3, 'battle_idle': 3, 'battle_idle2': 3, 'battle_idle3': 3, 'walk': 5, 'walk_s1': 5, 'walk_s2': 5, 'walk_s3': 5,
         'walk_sword': 5, 'idle_sword': 3, 'slash': 0, 'spellcast': 0, 'hurt': 0}   # 0 = poses, not checked
COUNT_TOLERANCE = {'portrait': 0.06, 'idle': 0.12, 'battle_idle': 0.12, 'battle_idle2': 0.12, 'battle_idle3': 0.12, 'walk': 0.2, 'walk_s1': 0.2, 'walk_s2': 0.2,
                   'walk_s3': 0.2, 'walk_sword': 0.25, 'idle_sword': 0.15}


# The portrait's body below the waist (the shrug lifts what's above it): the study's waist row + its offset in the cell.
BODY_FROM = 43 + 42 + 1


def guid_paths():
    out = {}
    for meta in glob.glob(os.path.join(ASSETS, 'Art', 'Characters', '*', '*.png.meta')):
        m = re.search(r'^guid: ([0-9a-f]+)', open(meta, encoding='utf-8').read(), re.M)
        if m:
            out[m.group(1)] = meta[:-5]
    return out


def hair_colours():
    """Each character's hair colours (hex) as drawn on the study: A3's own greys, the others' palette hair ramps."""
    sys.path.insert(0, os.path.join(ROOT, 'Tools', 'ChibiForge'))
    import a3_portrait
    import palette_data
    chars = palette_data.load()['characters']
    out = {}
    for name in ALLIES:
        own = ['%02x%02x%02x' % c for c in a3_portrait.STUDY_HAIR]
        out[name] = list(chars.get(name, {}).get('hair', [])) + own   # A3 too: her hair is the palette's greys now
    return out


def clips():
    paths = guid_paths()
    hair = hair_colours()
    rows = []
    for name in ALLIES:
        text = open(os.path.join(ASSETS, 'Content', 'Animations', 'Anim_%s.asset' % name), encoding='utf-8').read()
        frame = int(re.search(r'^  frameSize: (\d+)', text, re.M).group(1))
        for m in re.finditer(r'  - name: (\S+)\n    sheet: \{fileID: \d+, guid: ([0-9a-f]+), type: 3\}\n    fps: ([0-9.]+)', text):
            clip, guid, fps = m.group(1), m.group(2), float(m.group(3))
            path = paths.get(guid)
            if not path:
                rows.append({'who': name, 'clip': clip, 'error': 'sheet not found (guid %s)' % guid})
                continue
            meta = open(path + '.meta', encoding='utf-8').read()
            mx = int(re.search(r'maxTextureSize: (\d+)', meta).group(1))
            rows.append({'who': name, 'clip': clip, 'fps': fps, 'frame': frame, 'max': mx,
                         'file': os.path.relpath(path, ROOT).replace('\\', '/'),
                         'data': 'data:image/png;base64,' + base64.b64encode(open(path, 'rb').read()).decode('ascii'),
                         'reach': REACH.get(clip, 0), 'tolerance': COUNT_TOLERANCE.get(clip, 0), 'hair': hair[name],
                         'bodyFrom': BODY_FROM})
    return rows


PAGE = r"""<!doctype html><html><head><meta charset="utf-8"><title>Animation check</title>
<style>body{background:#1b1a18;color:#ece5d3;font:14px monospace;margin:12px}canvas{image-rendering:pixelated;background:#3c3c46;margin:2px}
.clip{display:inline-block;margin:6px;padding:6px;border:1px solid #3a362f;vertical-align:top}.fail{color:#e0675b}.ok{color:#8fd18f}</style></head>
<body><h2>Animation check</h2><div id="summary">running...</div><div id="grid"></div><pre id="results"></pre>
<script>
const CLIPS = __CLIPS__;
const ROWS = { left: 1, down: 2 };
function loadImg(src) { return new Promise(r => { const i = new Image(); i.onload = () => r(i); i.src = src; }); }
function framesOf(img, size, row) {
  const n = Math.floor(img.width / size), out = [];
  for (let f = 0; f < n; f++) {
    const c = document.createElement('canvas'); c.width = size; c.height = size;
    const x = c.getContext('2d', { willReadFrequently: true });
    x.drawImage(img, f * size, row * size, size, size, 0, 0, size, size);   // Unity: rect y = height - (row+1)*size from the bottom
    out.push(x.getImageData(0, 0, size, size));
  }
  return out;
}
function solid(d, x, y) { return d.data[(y * d.width + x) * 4 + 3] > 100; }
function check(c, img) {
  const problems = [];
  if (img.width > c.max || img.height > c.max) problems.push(`size ${img.width}x${img.height} over the ${c.max} px import limit`);
  const rows = img.height >= 4 * c.frame ? ROWS : { only: 0 };
  for (const [facing, row] of Object.entries(rows)) {
    const fr = framesOf(img, c.frame, row), size = c.frame;
    const counts = fr.map(d => { let n = 0; for (let i = 3; i < d.data.length; i += 4) if (d.data[i] > 100) n++; return n; });
    fr.forEach((d, i) => {
      if (counts[i] === 0) { problems.push(`${facing} frame ${i}: empty`); return; }
      for (let k = 0; k < size; k++)
        if (solid(d, k, 0) || solid(d, k, size - 1) || solid(d, 0, k) || solid(d, size - 1, k)) { problems.push(`${facing} frame ${i}: touches the cell edge (clipped)`); break; }
    });
    if (c.tolerance > 0) {
      const med = counts.slice().sort((a, b) => a - b)[Math.floor(counts.length / 2)];
      counts.forEach((n, i) => { if (Math.abs(n - med) / med > c.tolerance) problems.push(`${facing} frame ${i}: ${n} px vs median ${med} (parts dropping out?)`); });
    }
    if (c.reach > 0 && fr.length > 1) {
      for (let i = 0; i < fr.length; i++) {
        const a = fr[i], b = fr[(i + 1) % fr.length];
        let lost = 0;
        for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
          if (!solid(a, x, y)) continue;
          let near = false;
          for (let dy = -c.reach; dy <= c.reach && !near; dy++) for (let dx = -c.reach; dx <= c.reach && !near; dx++) {
            const X = x + dx, Y = y + dy; if (X >= 0 && Y >= 0 && X < size && Y < size && solid(b, X, Y)) near = true;
          }
          if (!near) lost++;
        }
        if (lost > 0) problems.push(`${facing} frame ${i}->${(i + 1) % fr.length}: ${lost} px vanished (no pixel within ${c.reach} px)`);
      }
    }
    if (c.clip === 'portrait') {   // the body below the shoulders never changes (only hair, eyes and the shrug move)
      const f0 = fr[0], hair = new Set(c.hair);
      const hex = (d, p) => [0, 1, 2].map(k => d.data[p + k].toString(16).padStart(2, '0')).join('');
      const isHair = (d, p) => d.data[p + 3] > 100 && hair.has(hex(d, p));
      fr.forEach((d, i) => {
        let diff = 0;
        for (let y = c.bodyFrom; y < size; y++) for (let x = 0; x < size; x++) {
          const p = (y * size + x) * 4;
          if (isHair(d, p) || isHair(f0, p)) continue;   // hair moving over / off the body is the wind
          for (let k = 0; k < 4; k++) if (d.data[p + k] !== f0.data[p + k]) { diff++; break; }
        }
        if (diff > 0) problems.push(`${facing} frame ${i}: ${diff} body pixels changed below the waist`);
      });
    }
  }
  return problems;
}
(async () => {
  const grid = document.getElementById('grid'), out = [], players = [];
  let fails = 0;
  for (const c of CLIPS) {
    if (c.error) { out.push(`FAIL ${c.who} ${c.clip}: ${c.error}`); fails++; continue; }
    const img = await loadImg(c.data);
    const problems = check(c, img);
    if (problems.length) fails++;
    out.push(`${problems.length ? 'FAIL' : 'ok  '} ${c.who.padEnd(6)} ${c.clip.padEnd(12)} ${c.fps} fps  ${img.width}x${img.height}` + (problems.length ? '\n      ' + problems.slice(0, 6).join('\n      ') + (problems.length > 6 ? `\n      (+${problems.length - 6} more)` : '') : ''));
    // play it (facing down), 3x, at its own fps
    const div = document.createElement('div'); div.className = 'clip';
    div.innerHTML = `<div class="${problems.length ? 'fail' : 'ok'}">${c.who} ${c.clip}</div>`;
    const cv = document.createElement('canvas'); cv.width = 96 * 3; cv.height = 96 * 3; div.append(cv); grid.append(div);
    const fr = framesOf(img, c.frame, img.height >= 4 * c.frame ? 2 : 0);
    const ctx = cv.getContext('2d'); ctx.imageSmoothingEnabled = false;
    const tmp = document.createElement('canvas'); tmp.width = c.frame; tmp.height = c.frame;
    players.push({ fr, ctx, tmp, fps: c.fps, size: c.frame });
  }
  const t0 = performance.now();
  (function tick() {
    const t = (performance.now() - t0) / 1000;
    for (const p of players) {
      const i = Math.floor(t * p.fps) % p.fr.length;
      p.tmp.getContext('2d').putImageData(p.fr[i], 0, 0);
      p.ctx.clearRect(0, 0, 288, 288);
      p.ctx.drawImage(p.tmp, (p.size - 96) / 2, p.size - 96 - 4, 96, 96, 0, 0, 288, 288);
    }
    requestAnimationFrame(tick);
  })();
  document.getElementById('summary').innerHTML = fails ? `<span class="fail">${fails} clip(s) FAILED</span>` : `<span class="ok">all ${CLIPS.length} clips passed</span>`;
  document.getElementById('results').textContent = out.join('\n');
  document.body.dataset.done = '1';
})();
</script></body></html>"""


def find_chrome(argv):
    if '--chrome' in argv:
        return argv[argv.index('--chrome') + 1]
    for p in (r'C:\Program Files\Google\Chrome\Application\chrome.exe', r'C:\Program Files (x86)\Google\Chrome\Application\chrome.exe',
              r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe', '/usr/bin/google-chrome', '/usr/bin/chromium'):
        if os.path.exists(p):
            return p
    return None


def main(argv):
    global ALLIES
    if '--names' in argv:
        ALLIES = [n for n in argv[argv.index('--names') + 1].split(',') if n]
        # The pack sprites (npc_packs.py) are 3x nearest upscales: one step of their art is 3 px, so a two-pixel foot
        # step moves 6.
        REACH['walk'] = 12   # and Tech Dungeon's gunner hops 2-3 art px per stride (its run has a shadow under it)
    out_dir = argv[argv.index('--out') + 1] if '--out' in argv else os.path.join(ROOT, 'Temp', 'anim_check')
    os.makedirs(out_dir, exist_ok=True)
    page = os.path.join(out_dir, 'anim_check.html')
    open(page, 'w', encoding='utf-8').write(PAGE.replace('__CLIPS__', json.dumps(clips())))
    chrome = find_chrome(argv)
    if not chrome:
        print('no Chrome/Edge found; open', page, 'yourself')
        return 1
    url = 'file:///' + page.replace('\\', '/')
    common = [chrome, '--headless=new', '--disable-gpu', '--allow-file-access-from-files', '--window-size=1600,2400']
    dom = subprocess.run(common + ['--virtual-time-budget=20000', '--dump-dom', url], capture_output=True, text=True,
                         encoding='utf-8', errors='replace').stdout
    for i, budget in enumerate((20500, 20750, 21000)):   # the clips mid-play at a few moments
        subprocess.run(common + ['--virtual-time-budget=%d' % budget, '--screenshot=' + os.path.join(out_dir, 'play_%d.png' % i), url],
                       capture_output=True)
    m = re.search(r'<pre id="results">(.*?)</pre>', dom, re.S)
    s = re.search(r'<div id="summary">(.*?)</div>', dom, re.S)
    if not m or 'data-done="1"' not in dom:
        print('the page did not finish in the browser')
        return 1
    import html
    print(html.unescape(re.sub('<[^>]+>', '', s.group(1))))
    print(html.unescape(m.group(1)))
    print('page:', page, '| screenshots:', os.path.join(out_dir, 'play_*.png'))
    return 1 if 'FAIL' in m.group(1) else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
