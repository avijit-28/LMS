$roadmapPath = "D:\avi\Project\MVC\LMS\PRODUCTION_ROADMAP.md"
$lines = Get-Content -Path $roadmapPath

$inBlock = $false
$currentLang = ""
$currentBlock = [System.Collections.Generic.List[string]]::new()
$startLine = 0

$blocks = [System.Collections.Generic.List[PSObject]]::new()

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -match '^```(\w+)?') {
        if (-not $inBlock) {
            $inBlock = $true
            $currentLang = $matches[1]
            if ([string]::IsNullOrWhiteSpace($currentLang)) { $currentLang = "text" }
            $currentBlock.Clear()
            $startLine = $i + 1
        } else {
            $inBlock = $false
            $codeText = ($currentBlock -join "`r`n")
            $blocks.Add([PSCustomObject]@{
                Language = $currentLang
                StartLine = $startLine
                EndLine = $i + 1
                LineCount = $currentBlock.Count
                Code = $codeText
            })
            $currentBlock.Clear()
        }
    } elseif ($inBlock) {
        $currentBlock.Add($line)
    }
}

Write-Output "Total code blocks extracted: $($blocks.Count)"
$blocks | Group-Object Language | Select-Object Count, Name | Sort-Object Count -Descending | Format-Table -AutoSize

# Validate PowerShell blocks
Write-Output "`n=== Validating PowerShell Code Blocks ==="
$psBlocks = $blocks | Where-Object { $_.Language -in @("powershell", "ps1") }
foreach ($b in $psBlocks) {
    $tokens = $null
    $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseInput($b.Code, [ref]$tokens, [ref]$errors)
    if ($errors.Count -gt 0) {
        Write-Output "FAIL at Line $($b.StartLine)-$($b.EndLine): $($errors.Count) syntax errors"
        foreach ($err in $errors) {
            Write-Output "  Error: $($err.Message) (Offset: $($err.Extent.StartOffset))"
        }
    } else {
        Write-Output "PASS at Line $($b.StartLine)-$($b.EndLine): Syntax Valid (Lines: $($b.LineCount))"
    }
}

# Validate JSON blocks
Write-Output "`n=== Validating JSON Code Blocks ==="
$jsonBlocks = $blocks | Where-Object { $_.Language -eq "json" }
foreach ($b in $jsonBlocks) {
    try {
        $null = ConvertFrom-Json -InputObject $b.Code -ErrorAction Stop
        Write-Output "PASS at Line $($b.StartLine)-$($b.EndLine): Valid JSON (Lines: $($b.LineCount))"
    } catch {
        Write-Output "FAIL at Line $($b.StartLine)-$($b.EndLine): Invalid JSON ($($_.Exception.Message))"
    }
}

# Validate XML blocks
Write-Output "`n=== Validating XML Code Blocks ==="
$xmlBlocks = $blocks | Where-Object { $_.Language -eq "xml" }
foreach ($b in $xmlBlocks) {
    try {
        [xml]$doc = $b.Code
        Write-Output "PASS at Line $($b.StartLine)-$($b.EndLine): Valid XML (Lines: $($b.LineCount))"
    } catch {
        Write-Output "FAIL at Line $($b.StartLine)-$($b.EndLine): Invalid XML ($($_.Exception.Message))"
    }
}
