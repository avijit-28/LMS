$roadmapPath = "D:\avi\Project\MVC\LMS\PRODUCTION_ROADMAP.md"
$lines = Get-Content -Path $roadmapPath
$inBlock = $false
$currentLang = ""
$startLine = 0
$title = ""

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -match '^#{1,4}\s+(.*)') {
        $title = $matches[1]
    }
    if ($line -match '^```(\w+)?') {
        if (-not $inBlock) {
            $inBlock = $true
            $currentLang = $matches[1]
            $startLine = $i + 1
        } else {
            $inBlock = $false
            if ($currentLang -in @("python", "sql", "yaml", "csharp")) {
                Write-Output ("[{0,-7}] Lines {1,4}-{2,4} under '{3}'" -f $currentLang, $startLine, ($i+1), $title)
            }
        }
    }
}
