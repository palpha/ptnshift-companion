dotnet publish GUI/GUI.csproj `
    --configuration "Debug Windows" `
    --framework net9.0 `
    --runtime win-x64 `
    --self-contained true `
    --output publish/ `
    -p:Platform=x64