"""生成两颗星球的地表贴图(等距柱状投影)。
世末之城仿木炉星:北极一块平整空地(放故事道具),四周丘陵山谷;地表颜色随高度变化。
重跑: python gen_textures.py
"""
import numpy as np
from PIL import Image
import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "planets", "textures")
os.makedirs(OUT, exist_ok=True)
W, H = 1024, 512

def fractal(seed, octaves=6, persistence=0.5):
    """平滑分形噪声,经度方向无缝。"""
    rng = np.random.default_rng(seed)
    out = np.zeros((H, W), dtype=np.float64)
    amp, total = 1.0, 0.0
    for o in range(octaves):
        freq = 2 ** o
        gw, gh = max(2, freq * 4), max(2, freq * 2)
        grid = rng.random((gh, gw + 1))
        grid[:, -1] = grid[:, 0]
        img = Image.fromarray((grid * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)
        out += amp * (np.asarray(img, dtype=np.float64) / 255.0)
        total += amp
        amp *= persistence
    out = (out - out.min()) / (out.max() - out.min())
    return out

def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)

def save_gray(arr, name):
    Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8), "L").save(os.path.join(OUT, name))

def save_rgb(r, g, b, name):
    rgb = np.stack([np.clip(c, 0, 255) for c in (r, g, b)], axis=-1).astype(np.uint8)
    Image.fromarray(rgb, "RGB").save(os.path.join(OUT, name))

# 纬度: row0 = 北极(+90, 故事空地所在), rowH-1 = 南极
lat = 90 - 180 * (np.arange(H) / (H - 1))
lat2d = np.repeat(lat[:, None], W, axis=1)

# ===== 世末之城 =====
# flatness: 北极附近(lat>=70)为平地, 70->50 渐变到地形
flatness = smoothstep(50, 70, lat2d)
terrain = fractal(seed=11, octaves=6)
detail = fractal(seed=23, octaves=8)
terrain = 0.7 * terrain + 0.3 * detail
V_FLAT = 0.4545  # min291,max313 -> 291+0.4545*22≈301(故事道具的摆放半径)
height_s = flatness * V_FLAT + (1 - flatness) * terrain
height_s = np.clip(height_s + (detail - 0.5) * 0.02 * flatness, 0, 1)
save_gray(height_s, "singer_height.png")

# 颜色随高度: 低地苔green/积水 -> 中地灰green -> 高地岩gray -> 峰顶浅gray
h = height_s
mottle = fractal(seed=41, octaves=7)
c_low = np.array([38, 52, 46]); c_mid = np.array([58, 66, 64])
c_high = np.array([84, 86, 90]); c_peak = np.array([120, 124, 130])
t1 = smoothstep(0.30, 0.50, h)
t2 = smoothstep(0.55, 0.72, h)
t3 = smoothstep(0.78, 0.92, h)
col = (c_low[None, None, :] * (1 - t1)[..., None]
       + c_mid[None, None, :] * (t1 * (1 - t2))[..., None]
       + c_high[None, None, :] * (t2 * (1 - t3))[..., None]
       + c_peak[None, None, :] * t3[..., None])
col = col + (mottle[..., None] - 0.5) * 16
wet = smoothstep(0.18, 0.0, h)[..., None]
col = col * (1 - 0.25 * wet)
save_rgb(col[..., 0], col[..., 1], col[..., 2], "singer_ground.png")

# ===== 神谕之境(淡紫白结晶,轻微起伏) =====
m1 = fractal(seed=77, octaves=6)
m2 = fractal(seed=91, octaves=7)
height_g = 0.5 * m1 + 0.5 * fractal(seed=53, octaves=5)
save_gray(height_g, "god_height.png")
r = 198 + m1 * 38 + m2 * 8
g = 190 + m1 * 34 + m2 * 8
b = 224 + m1 * 26 + m2 * 6
vein = (m2 > 0.7)
r[vein] = np.clip(r[vein] - 8, 0, 255); b[vein] = np.clip(b[vein] + 12, 0, 255)
save_rgb(r, g, b, "god_ground.png")

print("generated:", sorted(f for f in os.listdir(OUT) if f.endswith(".png")))
