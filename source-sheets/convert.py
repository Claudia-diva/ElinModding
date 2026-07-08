"""Convert the raw xlsx source sheets to greppable CSVs.

Output: csv/<Workbook>.<Sheet>.csv (UTF-8, one line per row).
Run via pull.ps1, or standalone after dropping xlsx files into raw/.
"""
import csv
import re
from pathlib import Path

import openpyxl

ROOT = Path(__file__).parent
RAW = ROOT / "raw"
OUT = ROOT / "csv"


def sanitize(name: str) -> str:
    return re.sub(r'[\\/:*?"<>|]+', "_", name).strip() or "sheet"


def main() -> None:
    OUT.mkdir(exist_ok=True)
    for xlsx in sorted(RAW.glob("*.xlsx")):
        wb = openpyxl.load_workbook(xlsx, read_only=True, data_only=True)
        for ws in wb.worksheets:
            # sheets often declare max-row/col dimensions; keep the used rectangle only
            rows = []
            last_row = 0
            width = 0
            for row in ws.iter_rows(values_only=True):
                vals = ["" if v is None else v for v in row]
                rows.append(vals)
                used = 0
                for i, v in enumerate(vals):
                    if v != "":
                        used = i + 1
                if used:
                    last_row = len(rows)
                    width = max(width, used)
            rows = rows[:last_row]
            out = OUT / f"{xlsx.stem}.{sanitize(ws.title)}.csv"
            with out.open("w", newline="", encoding="utf-8") as f:
                w = csv.writer(f)
                for vals in rows:
                    w.writerow((vals + [""] * width)[:width])
            print(f"{out.name}: {len(rows)} rows x {width} cols")
        wb.close()


if __name__ == "__main__":
    main()
