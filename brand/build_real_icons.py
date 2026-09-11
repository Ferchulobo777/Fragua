from PIL import Image
import os

SRC = r"C:\Users\Notebook\Downloads\Fragua\brand\real-logo\symbol-square.png"
OUT = r"C:\Users\Notebook\Downloads\Fragua\brand\real-logo\out"
os.makedirs(OUT, exist_ok=True)

im = Image.open(SRC).convert("RGBA")
print("source size:", im.size)
SIZES = [16, 20, 24, 32, 48, 64, 128, 256, 512]

for size in SIZES:
    resized = im.resize((size, size), Image.LANCZOS)
    resized.save(os.path.join(OUT, f"icon_{size}.png"))

# .ico multi-resolucion para el icono de ventana/taskbar/escritorio de Windows.
# Importante: se parte siempre de la imagen original de alta resolucion (im),
# no de una version ya reducida, para que cada tamano salga nitido en vez de
# ser una ampliacion borrosa de un frame de 16x16.
ico_sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
im.save(
    os.path.join(OUT, "fragua.ico"),
    format="ICO",
    sizes=ico_sizes,
)

print("listo:", OUT)
