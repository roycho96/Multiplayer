#!/usr/bin/env bash
set -euo pipefail

root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
cd -- "$root"

output=${MP_PACKAGE_DIR:-"$root/Multiplayer"}
product=${MP_MOD_OUTPUT_PATH:-"$root/1.7"}
if [[ -e "$output" || -L "$output" ]]; then
    echo "Package destination already exists: $output" >&2
    exit 1
fi

case "${1:-}" in
    '') dotnet build Source/Multiplayer.sln --configuration Release -p:ModOutputPath="$product" ;;
    --stage-only) ;;
    *) echo "Usage: $0 [--stage-only]" >&2; exit 2 ;;
esac

for dll in MultiplayerLoader Multiplayer MultiplayerCommon 0MultiplayerAPI; do
    if [[ ! -f "$product/Assemblies/$dll.dll" ]]; then
        echo "Missing required assembly: $product/Assemblies/$dll.dll" >&2
        exit 1
    fi
done
for dll in 0Harmony Assembly-CSharp UnityStubs; do
    if [[ -e "$product/Assemblies/$dll.dll" ]]; then
        echo "Unexpected game or Harmony assembly: $product/Assemblies/$dll.dll" >&2
        exit 1
    fi
done
if compgen -G "$product/Assemblies/Authority.*.dll" >/dev/null || [[ ! -d "$root/Languages/English" ]]; then
    echo 'Unexpected authority assemblies or uninitialized Languages submodule.' >&2
    exit 1
fi

base_version=$(sed -nE 's/.*SimpleVersion = "([0-9.]+)".*/\1/p' Source/Common/Version.cs)
revision=$(git rev-parse --short HEAD)
version="$base_version ($revision)"
mkdir -p -- "$(dirname -- "$output")"
mkdir -- "$output"
cp -r -- About Defs Textures LoadFolders.xml "$output/"
mkdir -- "$output/Languages" "$output/1.7"
tar --exclude=.git -C "$root/Languages" -cf - . | tar -C "$output/Languages" -xf -
cp -r -- "$product/Assemblies" "$output/1.7/"
sed -i "s#<modVersion>[^<]*</modVersion>#<modVersion>${version}</modVersion>#" "$output/About/About.xml"
echo "Staged Multiplayer mod: $output"
