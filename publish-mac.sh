#!/bin/bash
set -e

version=$1

if [ -z "$version" ]; then
  echo "Error: No version specified"
  exit 1
fi

plist_version=$(/usr/libexec/PlistBuddy -c "Print CFBundleShortVersionString" Info.plist)

if [ "$version" != "$plist_version" ]; then
  echo "Error: Version mismatch. Version in Info.plist is $plist_version, but specified version is $version"
  exit 1
fi

# get apple id from keychain
apple_id=$(security find-generic-password -s "APPLE_ID" | grep acct | sed 's/ *"acct"<blob>="//' | sed 's/"//g')

if [ -z "$apple_id" ]; then
  echo "Error: No Apple ID found in keychain"
  exit 1
fi

./build-mac.sh
./sign-mac.sh
./compress-mac.sh $version
./notarize-mac.sh $version $apple_id
./upload-mac.sh $version
