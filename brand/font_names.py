from fontTools.ttLib import TTFont

paths = [
    r"C:\Users\Notebook\Downloads\Fragua\src\Fragua.App\Assets\Fonts\CommitMono\CommitMono-400-Regular.otf",
    r"C:\Users\Notebook\Downloads\Fragua\src\Fragua.App\Assets\Fonts\MonaSans\MonaSans-Regular.otf",
]

for p in paths:
    f = TTFont(p)
    name = f["name"]
    print(p)
    for rec in name.names:
        if rec.nameID in (1, 4, 6, 16):
            print(" ", rec.nameID, rec.toUnicode())
