# Runs the PrivacyMask project after killing any stale process of the same name
$exeName = "PrivacyMask"
# Kill running instances if any
Get-Process -Name $exeName -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force }

# Run from project folder
dotnet run
