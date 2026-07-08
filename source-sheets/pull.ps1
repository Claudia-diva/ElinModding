# Refresh the Elin official source sheets from the public Google Drive folder
# and regenerate the greppable CSVs. Needs: pip install gdown openpyxl
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$folder = "https://drive.google.com/drive/folders/1czOUsixQyAIqMNCyjOpsDZmqVxo_FUFg"

python -m gdown --folder $folder -O "$root\raw"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python "$root\convert.py"
