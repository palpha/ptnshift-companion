#!/bin/bash
set -e

version=$1

if [ -z "$version" ]; then
  echo "Error: No version specified"
  exit 1
fi

version_folder="publish/$version"
dmg_path="publish/PTNSHIFTCompanion_${version}.dmg"

if [ ! -f $dmg_path ]; then
  mkdir -p $version_folder
  mv "publish/PTNSHIFT Companion.app" $version_folder
  hdiutil create -srcfolder $version_folder -format UDZO -volname "PTNSHIFT Companion $version" $dmg_path
else
  echo "DMG already exists at: $dmg_path"
fi

# sign dmg
signing_identity="Developer ID Application: Niklas Bergius (C535NAFJUW)"
codesign -s "$signing_identity" $dmg_path

echo "DMG created at: $dmg_path"
