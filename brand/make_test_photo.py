from PIL import Image
import random

random.seed(7)
w, h = 1600, 1200
im = Image.new("RGB", (w, h))
px = im.load()
for y in range(h):
    for x in range(0, w, 4):
        c = (random.randint(0, 255), random.randint(0, 255), random.randint(0, 255))
        for dx in range(4):
            if x + dx < w:
                px[x + dx, y] = c

out = r"C:\Users\Notebook\Downloads\Fragua\brand\out\test-photo.png"
im.save(out)
print(out)
