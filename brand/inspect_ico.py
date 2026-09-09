from PIL import Image

im = Image.open("out/fragua.ico")
print("sizes reported:", im.info.get("sizes"))
im2 = Image.open("out/icon_256.png")
print("icon_256 size/mode:", im2.size, im2.mode)
