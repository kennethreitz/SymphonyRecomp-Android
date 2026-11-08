#!/bin/sh
set -eu
cd "$(dirname "$0")/../.."
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
"$DOTNET" build android/mods/AndroidModSmoke -c Release -p:RunAnalyzers=false -v minimal
out=android/mods/dist
mkdir -p "$out/android-mod-smoke" "$out/android-source-smoke"
cp android/mods/AndroidModSmoke/bin/Release/net10.0/AndroidModSmoke.dll "$out/android-mod-smoke/"
cp android/mods/AndroidModSmoke/mod.json "$out/android-mod-smoke/"
cp android/mods/SourceSmoke/mod.json android/mods/SourceSmoke/SourceSmoke.cs "$out/android-source-smoke/"
printf 'Deployable mod folders: %s\n' "$PWD/$out"
