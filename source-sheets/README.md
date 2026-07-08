# Elin official source sheets

Mirror of the dev's public Drive folder (linked from the official site):
https://drive.google.com/drive/folders/1czOUsixQyAIqMNCyjOpsDZmqVxo_FUFg

- `csv/` — committed, greppable copies, one file per workbook sheet (`SourceCard.Chara.csv` etc.). Chara/item ids, element ids, tags, text keys all live here.
- `raw/` — the downloaded xlsx, not committed.
- `pull.ps1` — re-download and regenerate the CSVs (needs `pip install gdown openpyxl`).

The game reads the same data from its own compiled sources, so these can lag behind the installed game version; re-pull after game updates when ids seem off.
