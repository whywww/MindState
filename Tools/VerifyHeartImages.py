from pathlib import Path
import cv2
import numpy as np
import json

root = Path(__file__).resolve().parents[1] / "Evidence/FlowerHeart"
out = root / "Masks"
out.mkdir(exist_ok=True)
rows = []
for path in sorted(root.glob("State*.png")):
    im = cv2.imread(str(path))
    background = (np.max(np.abs(im.astype(np.int16) - im[0, 0].astype(np.int16)), axis=2) <= 3).astype(np.uint8)
    count, labels, stats, centers = cv2.connectedComponentsWithStats(background, 8)
    holes = np.zeros(background.shape, np.uint8)
    for i in range(1, count):
        x, y, w, h, area = stats[i]
        if x > 0 and y > 0 and x+w < im.shape[1] and y+h < im.shape[0] and area >= 4:
            holes[labels == i] = 255
    cv2.imwrite(str(out / (path.stem + "_Silhouette.png")), (1-background)*255)
    cv2.imwrite(str(out / (path.stem + "_EnclosedBackground.png")), holes)
    rows.append(dict(file=path.name, pixels=int(np.count_nonzero(holes))))
(root / "MaskChecks.json").write_text(json.dumps(dict(method="Corner RGB background tolerance 3; enclosed components >=4 pixels. Does not detect intersections or gaps connected to the exterior.", frames=rows), indent=2), encoding="utf-8")
print(dict(frames=len(rows), withHoles=sum(r["pixels"] > 0 for r in rows), failures=[r for r in rows if r["pixels"]]))
