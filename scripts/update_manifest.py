#!/usr/bin/env python3
import hashlib
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

if len(sys.argv) != 4:
    raise SystemExit("usage: update_manifest.py VERSION ZIP_PATH REPOSITORY")

version, zip_path, repository = sys.argv[1:]
version = version.lstrip('v')
parts = version.split('.')
while len(parts) < 4:
    parts.append('0')
plugin_version = '.'.join(parts[:4])

zip_bytes = Path(zip_path).read_bytes()
checksum = hashlib.md5(zip_bytes).hexdigest()  # Jellyfin repository manifests use MD5 here.
release_tag = 'v' + version
source_url = f"https://github.com/{repository}/releases/download/{release_tag}/manual-subtitle-extract-v{version}.zip"

manifest_path = Path('manifest.json')
manifest = json.loads(manifest_path.read_text())
entry = manifest[0]
entry['owner'] = "Basim Alasmari (oob-1)"
versions = entry.setdefault('versions', [])
versions = [v for v in versions if v.get('version') != plugin_version]
versions.insert(0, {
    'version': plugin_version,
    'changelog': f'Release {release_tag}',
    'targetAbi': '10.11.0.0',
    'sourceUrl': source_url,
    'checksum': checksum,
    'timestamp': datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')
})
entry['versions'] = versions
manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')