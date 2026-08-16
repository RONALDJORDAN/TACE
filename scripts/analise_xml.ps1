$f = "logs\live_3s_swipe.xml"
Write-Host "=== ANALISE LIVE APOS SWIPE DE 3s (236,574 -> 240,578) ===" -ForegroundColor Cyan
if (Test-Path $f) {
    $x = Get-Content $f -Raw
    $pattern = '<node [^>]*>'
    $matches = [regex]::Matches($x, $pattern)
    foreach ($m in $matches) {
        $node = $m.Value
        $txt = ""
        $desc = ""
        if ($node -match 'text="([^"]+)"') { $txt = $Matches[1] }
        if ($node -match 'content-desc="([^"]+)"') { $desc = $Matches[1] }
        
        if ($txt -ne "" -or $desc -ne "") {
            $clk = if ($node -match 'clickable="true"') { "TRUE " } else { "FALSE" }
            if ($node -match 'bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"') {
                $x1 = [int]$Matches[1]
                $y1 = [int]$Matches[2]
                $x2 = [int]$Matches[3]
                $y2 = [int]$Matches[4]
                $cx = [int](($x1 + $x2) / 2)
                $cy = [int](($y1 + $y2) / 2)
                
                $label = if ($txt -ne "") { "text='$txt'" } else { "desc='$desc'" }
                $resId = ""
                if ($node -match 'resource-id="([^"]+)"') { $resId = " id='$($Matches[1])'" }
                
                $color = if ($clk.Trim() -eq "TRUE") { "Green" } else { "Yellow" }
                Write-Host "  [CLK:$clk] cx=$cx cy=$cy | bounds=[$x1,$y1][$x2,$y2] | $label$resId" -ForegroundColor $color
            }
        }
    }
} else {
    Write-Host "Arquivo $f nao encontrado." -ForegroundColor Red
}
