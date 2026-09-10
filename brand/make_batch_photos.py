from PIL import Image
import random, os

out_dir = r"C:\Users\Notebook\Downloads\Fragua\brand\out\batch-test"
os.makedirs(out_dir, exist_ok=True)

random.seed(3)
for i in range(5):
    w, h = 600, 400
    im = Image.new("RGB", (w, h))
    px = im.load()
    for y in range(h):
        for x in range(0, w, 6):
            c = (random.randint(0, 255), random.randint(0, 255), random.randint(0, 255))
            for dx in range(6):
                if x + dx < w:
                    px[x + dx, y] = c
    im.save(os.path.join(out_dir, f"foto_{i+1}.png"))

print(out_dir)
