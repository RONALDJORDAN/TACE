$Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$x = Get-Content (Join-Path $Root 'logs\live_now.xml') -Raw
$matches = [regex]::Matches($x, '<node [^>]*clickable="true"[^>]*>')
foreach ($m in $matches) {
    Write-Host $m.Value -ForegroundColor Yellow
}