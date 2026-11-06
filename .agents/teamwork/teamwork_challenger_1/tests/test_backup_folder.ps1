# Test LmsBackup.ps1 error handling when BackupFolder cannot be created
$BackupFolder = "Z:\NonExistentDrive\LMS_Backups"
$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$LogFile = Join-Path $BackupFolder "backup_log_$Timestamp.txt"

function Write-Log([string]$Message) {
    $Line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')] $Message"
    Write-Host $Line
    Add-Content -Path $LogFile -Value $Line
}

try {
    if (-not (Test-Path $BackupFolder)) {
        New-Item -ItemType Directory -Path $BackupFolder -Force | Out-Null
    }
}
catch {
    Write-Host "Caught initial error: $_"
    Write-Host "Now executing catch block with Write-Log..."
    try {
        Write-Log "CRITICAL BACKUP FAILURE: $_"
    }
    catch {
        Write-Host "FAILED in catch block! Error: $_"
    }
}
