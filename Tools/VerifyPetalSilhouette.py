"""Compare fixed-camera Unity captures; no alignment/resizing can hide shape changes."""
import argparse
import csv
from pathlib import Path
import cv2
import numpy as np


def silhouette(path):
    image = cv2.imread(str(path))
    if image is None:
        raise ValueError(f"Cannot read {path}")
    mask = (np.max(image, axis=2) > 10).astype(np.uint8)
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    contours = [c for c in contours if cv2.contourArea(c) > 10]
    filled = np.zeros_like(mask)
    cv2.drawContours(filled, contours, -1, 1, cv2.FILLED)
    boundary = filled - cv2.erode(filled, np.ones((3, 3), np.uint8))
    ys, xs = np.nonzero(filled)
    if not len(xs):
        raise ValueError(f"Empty flower silhouette: {path}")
    return filled, boundary, (xs.min(), ys.min(), xs.max()-xs.min()+1, ys.max()-ys.min()+1)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("baseline", type=Path)
    parser.add_argument("candidate", type=Path)
    args = parser.parse_args()
    rows = []
    for original in sorted(args.baseline.glob("*.png")):
        if not original.stem.startswith(("Bloom_", "Orbit_", "Breath_", "Enter_", "Exit_")) or any(s in original.stem for s in ("_IDs", "_WrapPair")):
            continue
        candidate = args.candidate / original.name
        if not candidate.exists():
            raise ValueError(f"Missing candidate capture: {candidate}")
        a, ca, ba = silhouette(original)
        b, cb, bb = silhouette(candidate)
        dw, dh = abs(bb[2]/ba[2]-1), abs(bb[3]/ba[3]-1)
        da = cv2.distanceTransform(1-ca, cv2.DIST_L2, cv2.DIST_MASK_PRECISE)
        db = cv2.distanceTransform(1-cb, cv2.DIST_L2, cv2.DIST_MASK_PRECISE)
        distance = max(float(da[cb.astype(bool)].max()), float(db[ca.astype(bool)].max()))
        offset = distance / np.hypot(ba[2], ba[3])
        changed = np.logical_xor(a, b).sum() / a.sum()
        rows.append(dict(capture=original.name, widthChange=dw, heightChange=dh, maxContourOffset=offset, silhouetteDifference=changed, status="PASS" if max(dw, dh, offset) <= .02 else "FAIL"))
    if not rows:
        raise ValueError("No captures compared")
    with (args.candidate / "ShapeChecks.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=rows[0].keys())
        writer.writeheader()
        writer.writerows(rows)
    for key in ("widthChange", "heightChange", "maxContourOffset"):
        worst = max(rows, key=lambda r: r[key])
        print(f"{key} max={worst[key]:.4%} ({worst['capture']})")
    failed = [r['capture'] for r in rows if r['status'] == 'FAIL']
    print(f"{len(rows)} captures, {len(failed)} failed")
    if failed:
        print("\n".join(failed))
    return int(bool(failed))


if __name__ == "__main__":
    raise SystemExit(main())
