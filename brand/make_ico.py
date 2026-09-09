from PIL import Image

# Windows .ico solo acepta 16/24/32/48/64/128/256: 20 no es un tamano valido
# de directorio de icono, aunque si sirve como PNG suelto para otros usos.
sizes = [16, 24, 32, 48, 64, 128, 256]
imgs = [Image.open(f"out/icon_{s}.png").convert("RGBA") for s in sizes]

# Pillow arma el .ico a partir de la imagen base: tiene que ser la mas
# grande, si no las demas entradas del directorio quedan mal generadas
# (el bug real detras del icono roto de ForgeMD).
base = imgs[-1]
others = imgs[:-1]
base.save("out/fragua.ico", format="ICO", sizes=[(s, s) for s in sizes], append_images=others)
print("ok")
