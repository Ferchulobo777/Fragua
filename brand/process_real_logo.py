from PIL import Image
import os
import numpy as np

SRC = r"C:\Users\Notebook\Downloads\Fragua\b40c3020-477b-4ead-b95b-fe1c10cfb7e0.png"
OUT = r"C:\Users\Notebook\Downloads\Fragua\brand\real-logo"
os.makedirs(OUT, exist_ok=True)

im = Image.open(SRC).convert("RGBA")
gray = im.convert("L")
arr_gray = np.array(gray).astype(np.float32)

# Umbral con rampa: por debajo de LOW, transparente; por encima de HIGH,
# opaco; en el medio, interpolado. Deja el fondo negro real transparente sin
# comerse el resplandor tenue de la llama, que es mas brillante que el
# fondo pero mas oscuro que el nucleo del fuego.
LOW, HIGH = 10, 34
alpha = np.clip((arr_gray - LOW) / (HIGH - LOW), 0, 1) * 255
alpha = alpha.astype(np.uint8)

arr_rgba = np.array(im)
arr_rgba[:, :, 3] = np.minimum(arr_rgba[:, :, 3], alpha)
keyed = Image.fromarray(arr_rgba, "RGBA")

mask = gray.point(lambda p: 255 if p > 18 else 0)
bbox = mask.getbbox()
left, top, right, bottom = bbox

search_top = top + int((bottom - top) * 0.55)
row_sums = np.array(gray)[search_top:bottom, left:right].sum(axis=1)
split_row = search_top + int(row_sums.argmin())

# --- Simbolo solo, transparente, recorte cuadrado con margen ---
symbol = keyed.crop((left, top, right, split_row))
sw, sh = symbol.size
side = max(sw, sh)
pad = int(side * 0.12)
side_padded = side + pad * 2
square = Image.new("RGBA", (side_padded, side_padded), (0, 0, 0, 0))
square.paste(symbol, ((side_padded - sw) // 2, (side_padded - sh) // 2), symbol)
square.save(os.path.join(OUT, "symbol-square.png"))
print("symbol-square:", square.size)

# --- Simbolo + palabra, transparente, para un uso hero mas grande ---
full = keyed.crop((left, top, right, bottom))
fw, fh = full.size
pad_f = int(fw * 0.06)
full_padded = Image.new("RGBA", (fw + pad_f * 2, fh + pad_f * 2), (0, 0, 0, 0))
full_padded.paste(full, (pad_f, pad_f), full)
full_padded.save(os.path.join(OUT, "full-lockup.png"))
print("full-lockup:", full_padded.size)
