from PIL import Image
import os

SRC = r"C:\Users\Notebook\Downloads\Fragua\brand\real-logo\symbol-square.png"
OUT = r"C:\Users\Notebook\Downloads\Fragua\brand\real-logo\out"
os.makedirs(OUT, exist_ok=True)

im = Image.open(SRC).convert("RGBA")
SIZES = [16, 20, 24, 32, 48, 64, 128, 256, 512]

for size in SIZES:
    resized = im.resize((size, size), Image.LANCZOS)
    resized.save(os.path.join(OUT, f"icon_{size}.png"))

# .ico multi-resolucion para el icono de ventana/taskbar de Windows.
ico_sizes = [16, 24, 32, 48, 64, 128, 256]
ico_images = [im.resize((s, s), Image.LANCZOS) for s in ico_sizes]
ico_images[0].save(
    os.path.join(OUT, "fragua.ico"),
    format="ICO",
    sizes=[(s, s) for s in ico_sizes],
    append_images=ico_images[1:],
)

print("listo:", OUT)
