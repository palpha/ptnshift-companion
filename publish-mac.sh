#!/bin/bash

mkdir -p publish

x64_output="publish/osx-x64"
arm64_output="publish/osx-arm64"

dotnet publish GUI/GUI.csproj -f net9.0 -r osx-x64 -c "Release MacOS" -o $x64_output -p:UseAppHost=true
dotnet publish GUI/GUI.csproj -f net9.0 -r osx-arm64 -c "Release MacOS" -o $arm64_output -p:UseAppHost=true

x64_app="$x64_output/LiveshiftCompanion"
arm64_app="$arm64_output/LiveshiftCompanion"

if [[ ! -f "$x64_app" || ! -f "$arm64_app" ]]; then
  echo "Error: One or both builds failed!"
  exit 1
fi

# Create app bundle
app_bundle="publish/LiveshiftCompanion.app"
rm -rf "$app_bundle"
mkdir -p "$app_bundle/Contents/MacOS"
mkdir -p "$app_bundle/Contents/Resources"

# Copy the contents of the app bundle from x64
cp -a "$x64_output/." "$app_bundle/Contents/MacOS"

# Create universal binary
universal_app="$app_bundle/Contents/MacOS/LiveshiftCompanion"
lipo -create -output "$universal_app" "$x64_app" "$arm64_app"

# Inject Info.plist
cp Info.plist "$app_bundle/Contents/Info.plist"

# Inject icon
cp GUI/Assets/appicon.icns "$app_bundle/Contents/Resources/appicon.icns"

echo "Universal binary created at: $universal_app"
lipo -info "$universal_app"

plutil -p "$app_bundle/Contents/Info.plist"
