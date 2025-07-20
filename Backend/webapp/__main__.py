###
### webapp/__main__.py
###

import uvicorn
from .ASGI import application, app
from webapp.tools.config import recommend_settings
import os
import shutil
from pathlib import Path

def copy_map_json_to_unity():
    src_dir = Path(__file__).parent / 'AUGV' / 'maps_json'
    unity_maps_dir = Path(__file__).parent.parent.parent / 'Assets' / 'Maps'
    if not src_dir.exists():
        print(f"[MapCopy] Source directory does not exist: {src_dir}")
        return
    unity_maps_dir.mkdir(parents=True, exist_ok=True)
    for json_file in src_dir.glob('*.json'):
        dest = unity_maps_dir / json_file.name
        shutil.copy2(json_file, dest)
        print(f"[MapCopy] Copied {json_file} -> {dest}")

# Call this before starting the server
copy_map_json_to_unity()

def main():
    uvicorn.run(application, host="0.0.0.0", port=8080)

if __name__ == "__main__":
    recommend_settings()
    main()