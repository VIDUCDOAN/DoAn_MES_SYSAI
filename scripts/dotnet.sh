#!/usr/bin/env bash
set -euo pipefail
# Máy cá nhân có SDK dùng lệnh dotnet bình thường. Cloud dùng SDK đã cài dưới /workspace.
if command -v dotnet >/dev/null 2>&1; then
    exec dotnet "$@"
fi
sdk_dir="${MINIMES_DOTNET_DIR:-/workspace/.dotnet}"
if [ -x "$sdk_dir/dotnet" ]; then
    export DOTNET_ROOT="$sdk_dir"
    export DOTNET_CLI_HOME=/workspace/.dotnet-cli
    export NUGET_PACKAGES=/workspace/.nuget/packages
    export DOTNET_CLI_TELEMETRY_OPTOUT=1
    exec "$sdk_dir/dotnet" "$@"
fi
echo 'Cần cài .NET SDK theo global.json.' >&2
exit 1
