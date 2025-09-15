# Publishes the app as a single-file executable (x64)
$rid = "win-x64"
$cfg = "Release"

Write-Host "Publishing ($cfg) for $rid..."
Write-Host "Publishing without trimming (recommended for WinForms)..."
# Windows Forms apps should not be trimmed; disable trimming to avoid NETSDK1175
dotnet publish -c $cfg -r $rid -p:PublishSingleFile=true -p:PublishTrimmed=false

Write-Host "Published to: bin\$cfg\net8.0-windows\$rid\publish\"