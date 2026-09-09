from PIL import Image

src = r"C:\Users\Notebook\AppData\Local\Temp\claude\c--Users-Notebook-Downloads-portfolio-2026\408ee991-aac4-418b-89e5-364203636bb7\scratchpad\fragua-dialog.png"
im = Image.open(src)
print(im.size)

# Ventana real segun GetWindowRect de esa sesion: aprox 960x640 en (0,0).
crop = im.crop((0, 0, 960, 640))
out = r"C:\Users\Notebook\Downloads\Fragua\docs\screenshots\convertir.png"
crop.save(out)
print(out, crop.size)
