import fiona
import rasterio
from rasterio.features import geometry_mask
from shapely.geometry import shape
from shapely.ops import unary_union

# Paths
TIF_PATH = '../data/ETOPO1_Ice_g_geotiff.tif'
SHP_PATH = 'data/ne_10m_admin_0_countries.shp'
OUTPUT_PATH = 'data/ETOPO1_country_mask.tif'

# Read all country polygons
with fiona.open(SHP_PATH) as src:
    geometries = [shape(feat["geometry"]) for feat in src]

# Combine into single geometry (union)
union_geom = unary_union(geometries)

with rasterio.open(TIF_PATH) as src:
    mask = geometry_mask([union_geom.__geo_interface__], transform=src.transform,
                         invert=True, out_shape=(src.height, src.width))
    profile = src.profile
    profile.update(count=1, dtype='uint8', nodata=0, compress='lzw')
    with rasterio.open(OUTPUT_PATH, 'w', **profile) as dst:
        dst.write(mask.astype('uint8'), 1)
print('Saved mask to', OUTPUT_PATH)
