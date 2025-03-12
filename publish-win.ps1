dotnet publish GUI/GUI.csproj `
    --configuration "Release Windows" `
    --framework net9.0 `
    --runtime win-x64 `
    --self-contained true `
    --output publish/x64 `
    -p:Platform=x64
dotnet publish GUI/GUI.csproj `
    --configuration "Release Windows" `
    --framework net9.0 `
    --runtime win-arm64 `
    --self-contained true `
    --output publish/arm64 `
    -p:Platform=arm64