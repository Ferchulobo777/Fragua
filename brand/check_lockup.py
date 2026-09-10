from PIL import Image

im = Image.open(r"C:\Users\Notebook\Downloads\Fragua\brand\real-logo\full-lockup.png")
bg = Image.new("RGBA", im.size, (20, 20, 20, 255))
bg.alpha_composite(im)
bg.convert("RGB").save(r"C:\Users\Notebook\Downloads\Fragua\brand\real-logo\lockup-on-dark-check.png")
print(im.size)
