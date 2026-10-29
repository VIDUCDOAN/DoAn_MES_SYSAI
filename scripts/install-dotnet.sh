#!/usr/bin/env bash
set -euo pipefail
sdk_dir="${MINIMES_DOTNET_DIR:-/workspace/.dotnet}"
if [ -x "$sdk_dir/dotnet" ] && [ "$("$sdk_dir/dotnet" --version)" = '10.0.403' ]; then
    echo '.NET SDK 10.0.403 đã được cài.'
    exit 0
fi
archive_file="$(mktemp /tmp/minimes-sdk.XXXXXX.tar.gz)"
trap 'rm -f "$archive_file"' EXIT
curl --fail --show-error --location 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-linux-x64.tar.gz' --output "$archive_file"
python3 - "$archive_file" "$sdk_dir" <<'PY'
import hashlib
import sys
import tarfile
from pathlib import Path

archive, destination = Path(sys.argv[1]), Path(sys.argv[2])
# SHA512 từ release-metadata/10.0/releases.json chính thức của Microsoft.
expected = 'b7b3d8461cf98b07d2552ce9199702fa3ad010ac2671a5f8cd5136f7534e50f6855ba7bbb02b11494563f1d04156276a31cda1103b900e5eecd0d9473dc7b9c5'
with archive.open('rb') as stream:
    digest = hashlib.file_digest(stream, 'sha512').hexdigest()
if digest != expected:
    raise SystemExit('Checksum SDK không khớp. Dừng cài đặt.')
destination.mkdir(parents=True, exist_ok=True)
with tarfile.open(archive) as package:
    package.extractall(destination, filter='data')
print('Đã xác minh SHA512 và cài SDK 10.0.403.')
PY
