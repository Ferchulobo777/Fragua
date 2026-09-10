from PIL import Image

OUT_DIR = r"C:\Users\Notebook\Downloads\Fragua\brand\real-logo\out"
sizes = [16, 24, 32, 48, 64, 128]

sheet_w = sum(sizes) + 20 * len(sizes)
sheet_h = 200
sheet = Image.new("RGB", (sheet_w, sheet_h), (13, 13, 13))

x = 10
for s in sizes:
    icon = Image.open(f"{OUT_DIR}/icon_{s}.png").convert("RGBA")
    bg = Image.new("RGBA", icon.size, (24, 24, 24, 255))
    bg.alpha_composite(icon)
    sheet.paste(bg.convert("RGB"), (x, 140 - s))
    x += s + 20

sheet.save(r"C:\Users\Notebook\Downloads\Fragua\brand\real-logo\contact-sheet.png")
print("ok")
