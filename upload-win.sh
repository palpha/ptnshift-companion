#!/bin/bash
set -e

version=$1

if [ -z "$version" ]; then
  echo "Error: No version specified"
  exit 1
fi

x64_filename="PTNSHIFTCompanion_win-x64_${version}.zip"
arm64_filename="PTNSHIFTCompanion_win-arm64_${version}.zip"

x64_path="publish/x64/$x64_filename"
arm64_path="publish/arm64/$arm64_filename"

if [ ! -f $x64_path ] || [ ! -f $arm64_path ]; then
    echo "Error: Missing ZIP file(s). Ensure both $x64_path and $arm64_path exist."
    exit 1
fi

aws s3 cp $x64_path s3://bergius.org/ptnshift/$x64_filename --profile olaglig
aws s3 cp $x64_path s3://bergius.org/ptnshift/$arm64_filename --profile olaglig

echo "ZIPs uploaded to S3: https://bergius.org/ptnshift/$x64_filename and https://bergius.org/ptnshift/$arm64_filename"
