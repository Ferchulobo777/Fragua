with open(r"C:\Users\Notebook\Downloads\Fragua\src\Fragua.App\Assets\Icons\fragua.ico", "rb") as f:
    data = f.read()
count = int.from_bytes(data[4:6], "little")
print("Entradas de icono:", count)
for i in range(count):
    off = 6 + i * 16
    w = data[off]
    h = data[off + 1]
    print(f"  {w or 256}x{h or 256}")
