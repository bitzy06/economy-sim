import os, sys
from urllib.request import urlretrieve
from zipfile import ZipFile

URL = "https://www.ngdc.noaa.gov/mgg/global/relief/ETOPO1/data/ice_surface/grid_registered/georeferenced_tiff/ETOPO1_Ice_g_geotiff.zip"
DATA_DIR = os.path.join(os.path.dirname(__file__), "..", "data")
ZIP_PATH  = os.path.join(DATA_DIR, "ETOPO1_Ice_g_geotiff.zip")

os.makedirs(DATA_DIR, exist_ok=True)

if not os.path.exists(ZIP_PATH):
    print(f"Downloading ETOPO1 to {ZIP_PATH}…")
    urlretrieve(URL, ZIP_PATH)
else:
    print("ZIP already exists; skipping download.")

# unzip
with ZipFile(ZIP_PATH, "r") as z:
    print("Extracting…")
    z.extractall(DATA_DIR)

print("Done. Your GeoTIFF is in data/")
