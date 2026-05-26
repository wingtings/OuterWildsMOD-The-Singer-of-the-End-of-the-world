"""为飞船日志的 8 个 Entry 生成氛围缩略图,填补 "no photo"。
文件名必须等于 Entry ID。输出到 planets/shiplog/sprites/。
重跑: python gen_shiplog_sprites.py
"""
import os, math, random
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "planets", "shiplog", "sprites")
os.makedirs(OUT, exist_ok=True)
S = 320

def vgrad(top, bot):
    img = Image.new("RGB", (S, S))
    px = img.load()
    for y in range(S):
        t = y / (S - 1)
        px_row = tuple(int(top[i] + (bot[i] - top[i]) * t) for i in range(3))
        for x in range(S):
            px[x, y] = px_row
    return img

def rain(draw, n=70, color=(150, 170, 200), seed=1):
    rng = random.Random(seed)
    for _ in range(n):
        x = rng.randint(-20, S); y = rng.randint(0, S)
        ln = rng.randint(12, 26)
        draw.line([(x, y), (x - 6, y + ln)], fill=color, width=1)

def figure(draw, cx, cy, h, color):
    """简易站立人影。"""
    w = h * 0.28
    draw.ellipse([cx - w * 0.5, cy - h, cx + w * 0.5, cy - h + w], fill=color)        # 头
    draw.polygon([(cx - w * 0.5, cy), (cx + w * 0.5, cy),
                  (cx + w * 0.35, cy - h + w), (cx - w * 0.35, cy - h + w)], fill=color)  # 身

def save(img, name):
    img.filter(ImageFilter.GaussianBlur(0.4)).save(os.path.join(OUT, name + ".png"))

# 1. 世末之城: 雨中天际线
def singer_city():
    img = vgrad((26, 32, 46), (60, 66, 78)); d = ImageDraw.Draw(img)
    d.ellipse([220, 40, 270, 90], fill=(140, 150, 170))  # 月
    sky = [(40, 200, 70, 320, (18, 22, 32)), (110, 150, 60, 320, (12, 16, 24)),
           (175, 185, 80, 320, (20, 24, 34)), (250, 170, 70, 320, (14, 18, 28))]
    for x, top, w, bot, c in sky:
        d.rectangle([x, top, x + w, bot], fill=c)
    rain(d, 90, seed=2)
    return img

# 2. 雨中广场: 孤独人影 + 倒影
def city_square():
    img = vgrad((30, 36, 50), (70, 74, 84)); d = ImageDraw.Draw(img)
    d.ellipse([90, 250, 230, 290], fill=(40, 50, 64))     # 水洼
    figure(d, 160, 250, 95, (16, 20, 30))
    d.polygon([(150, 252), (170, 252), (166, 286), (154, 286)], fill=(60, 70, 90))  # 倒影
    rain(d, 80, seed=3)
    return img

# 3. 废弃音乐厅: 拱门 + 暖光
def city_hall():
    img = vgrad((24, 28, 40), (52, 56, 66)); d = ImageDraw.Draw(img)
    d.rectangle([70, 110, 250, 300], fill=(20, 24, 34))
    d.pieslice([95, 150, 225, 360], 180, 360, fill=(196, 150, 70))  # 拱门暖光
    d.rectangle([150, 250, 170, 300], fill=(20, 24, 34))
    d.polygon([(60, 110), (160, 60), (260, 110)], fill=(30, 34, 46))  # 屋顶
    rain(d, 60, seed=4)
    return img

# 4. 高塔观测台: 高塔伸入云层
def city_tower():
    img = vgrad((28, 30, 44), (66, 70, 82)); d = ImageDraw.Draw(img)
    for cx, cy, r in [(90, 70, 46), (170, 50, 56), (250, 80, 48)]:
        d.ellipse([cx - r, cy - r * 0.6, cx + r, cy + r * 0.6], fill=(80, 84, 96))  # 云
    d.polygon([(140, 300), (180, 300), (170, 90), (150, 90)], fill=(18, 22, 32))   # 塔身
    d.ellipse([150, 70, 170, 95], fill=(120, 200, 210))                            # 塔顶光
    rain(d, 50, seed=5)
    return img

# 5. 神谕之境: 紫白星球 + 结晶
def god_realm():
    img = vgrad((16, 12, 28), (28, 22, 44)); d = ImageDraw.Draw(img)
    rng = random.Random(6)
    for _ in range(60):
        x, y = rng.randint(0, S), rng.randint(0, S)
        d.point((x, y), fill=(180, 180, 210))                       # 星
    d.ellipse([90, 90, 230, 230], fill=(206, 196, 232))             # 星球
    d.ellipse([120, 110, 175, 150], fill=(224, 216, 244))           # 高光
    for bx, by, s in [(60, 240, 30), (250, 200, 26), (150, 270, 22)]:
        d.polygon([(bx, by), (bx - s * 0.4, by - s), (bx, by - s * 1.6),
                   (bx + s * 0.4, by - s)], fill=(180, 160, 220))   # 结晶
    return img

# 6. 赌约石碑: 三块石碑
def bet_stele():
    img = vgrad((40, 34, 56), (150, 140, 170)); d = ImageDraw.Draw(img)
    for x, h, c in [(80, 150, (60, 54, 74)), (160, 200, (50, 46, 66)), (240, 130, (66, 60, 80))]:
        d.rounded_rectangle([x - 26, 300 - h, x + 26, 300], radius=10, fill=c)
        for i in range(3):                                          # 刻痕
            yy = 300 - h + 24 + i * 26
            d.line([(x - 14, yy), (x + 14, yy)], fill=(150, 140, 175), width=2)
    return img

# 7. 神明之声: 发光结晶 + 声波
def god_voice():
    img = vgrad((18, 14, 30), (34, 26, 50)); d = ImageDraw.Draw(img)
    for r in (120, 92, 64):                                         # 声波
        d.arc([160 - r, 160 - r, 160 + r, 160 + r], 200, 340, fill=(150, 130, 200), width=2)
    d.polygon([(160, 90), (130, 160), (160, 240), (190, 160)], fill=(210, 196, 246))  # 结晶
    d.polygon([(160, 120), (145, 160), (160, 210), (175, 160)], fill=(240, 232, 255))
    return img

# 8. 第三者条款: 注视之眼
def third_party():
    img = vgrad((20, 16, 32), (44, 36, 60)); d = ImageDraw.Draw(img)
    d.ellipse([60, 110, 260, 210], fill=(230, 224, 246))           # 眼白
    d.ellipse([135, 120, 185, 200], fill=(90, 70, 140))            # 虹膜
    d.ellipse([150, 145, 170, 175], fill=(20, 16, 30))             # 瞳
    d.ellipse([156, 150, 164, 158], fill=(240, 240, 255))          # 高光
    return img

gens = {
    "SINGER_CITY": singer_city, "CITY_SQUARE": city_square, "CITY_HALL": city_hall,
    "CITY_TOWER": city_tower, "GOD_REALM": god_realm, "BET_STELE": bet_stele,
    "GOD_VOICE": god_voice, "THIRD_PARTY": third_party,
}
for name, fn in gens.items():
    save(fn(), name)
print("generated:", sorted(f for f in os.listdir(OUT) if f.endswith(".png")))
