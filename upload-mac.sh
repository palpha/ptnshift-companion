#!/bin/bash
set -e

version=$1

if [ -z "$version" ]; then
  echo "Error: No version specified"
  exit 1
fi

# see if dmg exists
dmg_path="publish/PtnshiftCompanion_${version}.dmg"

if [ ! -f $dmg_path ]; then
  echo "Error: No DMG found at $dmg_path"
  exit 1
fi

aws s3 cp $dmg_path s3://bergius.org/ptnshift/PtnshiftCompanion_${version}.dmg --profile olaglig

echo "DMG uploaded to S3: https://bergius.org/ptnshift/PtnshiftCompanion_${version}.dmg"