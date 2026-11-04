"""Contact sheet of a zone's individual files over a mid-grey checker (debug aid)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image
from pa_core import OUT, PREVIEW_DIR

def sheet(paths, name, cell=(512, 300), cols=3, bg=(60, 60, 70)):
    rows = (len(paths) + cols - 1) // cols
    out = Image.new("RGB", (cell[0] * cols, cell[1] * rows), bg)
    for i, p in enumerate(paths):
        im = Image.open(os.path.join(OUT, p)).convert("RGBA")
        im.thumbnail(cell)
        chk = Image.new("RGBA", im.size, bg + (255,))
        chk.alpha_composite(im)
        out.paste(chk, ((i % cols) * cell[0], (i // cols) * cell[1]))
    path = os.path.join(PREVIEW_DIR, name)
    out.save(path)
    print(path)

if __name__ == "__main__":
    z = sys.argv[1]
    d = {"z1": "Z1_WakingHollow", "z2": "Z2_Orchard"}[z]
    files = []
    for sub in ("Backdrop", "Foreground", "Props", "Ground"):
        for f in sorted(os.listdir(os.path.join(OUT, d, sub))):
            if f.endswith(".png"):
                files.append(f"{d}/{sub}/{f}")
    sheet(files, f"{z}_contact.png", cols=4)
