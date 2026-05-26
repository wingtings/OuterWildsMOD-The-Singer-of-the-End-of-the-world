import numpy as np
from PIL import Image
import os

OUT = os.path.dirname(os.path.abspath(__file__))
W, H = 1024, 512

def fractal(seed, octaves=6, persistence=0.5):
    """Smooth fractal value-noise, tileable in x (longitude)."""
    rng = np.random.default_rng(seed)
    out = np.zeros((H, W), dtype=np.float64)
    amp, total = 1.0, 0.0
    for o in range(octaves):
        freq = 2 ** o
        gw, gh = max(2, freq * 4), max(2, freq * 2)
        grid = rng.random((gh, gw + 1))
        grid[:, -1] = grid[:, 0]              # wrap longitude seam
        img = Image.fromarray((grid * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)
        out += amp * (np.asarray(img, dtype=np.float64) / 255.0)
        total += amp
        amp *= persistence
    out /= total
    out -= out.min()
    out /= out.max()
    return out

def save_gray(arr, name):
    Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8), "L").save(os.path.join(OUT, name))

def save_rgb(r, g, b, name):
    rgb = np.stack([np.clip(c, 0, 255) for c in (r, g, b)], axis=-1).astype(np.uint8)
    Image.fromarray(rgb, "RGB").save(os.path.join(OUT, name))

# ---- 世末之城: 阴湿青灰色废墟地表 ----
n1 = fractal(seed=11, octaves=6)
n2 = fractal(seed=23, octaves=7)
mottle = 0.6 * n1 + 0.4 * n2
height_s = 0.5 * n1 + 0.5 * fractal(seed=31, octaves=5)
save_gray(height_s, "singer_height.png")
# 冷青灰基调,低处更暗(积水),高处略亮(湿石)
r = 38 + mottle * 46 + n2 * 10
g = 46 + mottle * 48 + n2 * 10
b = 56 + mottle * 52 + n2 * 14
# 随机暗斑(积水/苔痕)
wet = (n2 < 0.32)
r[wet] *= 0.7; g[wet] *= 0.72; b[wet] *= 0.8
save_rgb(r, g, b, "singer_ground.png")

# ---- 神谕之境: 淡紫白结晶地表 ----
m1 = fractal(seed=77, octaves=6)
m2 = fractal(seed=91, octaves=7)
height_g = 0.5 * m1 + 0.5 * fractal(seed=53, octaves=5)
save_gray(height_g, "god_height.png")
r = 198 + m1 * 38 + m2 * 8
g = 190 + m1 * 34 + m2 * 8
b = 224 + m1 * 26 + m2 * 6
# 偏紫的纹路
vein = (m2 > 0.7)
r[vein] = np.clip(r[vein] - 8, 0, 255); b[vein] = np.clip(b[vein] + 12, 0, 255)
save_rgb(r, g, b, "god_ground.png")

print("generated:", sorted(f for f in os.listdir(OUT) if f.endswith(".png")))
