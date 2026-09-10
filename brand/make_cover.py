from PIL import Image

src = Image.open(r"C:\Users\Notebook\Downloads\Fragua\docs\screenshots\convertir.png").convert("RGB")
target_w, target_h = 1296, 729

# Escala manteniendo proporcion hasta cubrir el ancho objetivo, despues
# recorta el alto centrado (mismo criterio que el cover de ForgeMD: full
# bleed de la ventana real, sin marco agregado).
scale = target_w / src.width
new_h = round(src.height * scale)
resized = src.resize((target_w, new_h), Image.LANCZOS)

if new_h >= target_h:
    # Recorta desde arriba, no centrado: la barra de titulo del SO no aporta
    # nada de marca, pero el logo "Fragua" de la barra lateral si.
    top = min(60, new_h - target_h)
    cropped = resized.crop((0, top, target_w, top + target_h))
else:
    # La fuente es mas baja que el objetivo: centra sobre un fondo del
    # mismo canvas oscuro de la app en vez de estirar.
    canvas = Image.new("RGB", (target_w, target_h), (20, 20, 20))
    top = (target_h - new_h) // 2
    canvas.paste(resized, (0, top))
    cropped = canvas

out = r"C:\Users\Notebook\Downloads\portfolio 2026\public\side-projects\fragua-cover.png"
cropped.save(out)
print(out, cropped.size)
