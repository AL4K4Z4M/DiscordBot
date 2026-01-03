param (
    [Parameter(Mandatory=$true)]
    [ValidateSet("Daily", "Weekly")]
    [string]$Type
)

$ProjectDir = "C:\DiscordBot\Scrappy"
$DailyBackupDir = "C:\DiscordBot\Scrappy_Daily_Backup"
$WeeklyBackupDir = "C:\DiscordBot\Scrappy_Backup"
$Timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm"

# Create backup directories if they don't exist
if (!(Test-Path $DailyBackupDir)) { New-Item -ItemType Directory -Path $DailyBackupDir }
if (!(Test-Path $WeeklyBackupDir)) { New-Item -ItemType Directory -Path $WeeklyBackupDir }

if ($Type -eq "Daily") {
    # 1. Daily Backup: secrets.json and scrappy.db
    $DestDir = Join-Path $DailyBackupDir "Daily_$Timestamp"
    New-Item -ItemType Directory -Path $DestDir
    
    $FilesToBackup = @("secrets.json", "scrappy.db")
    foreach ($File in $FilesToBackup) {
        $SourceFile = Join-Path $ProjectDir $File
        if (Test-Path $SourceFile) {
            Copy-Item -Path $SourceFile -Destination $DestDir
        }
    }
    
    # 2. Cleanup: Keep only last 7 days of daily backups
    $Limit = (Get-Date).AddDays(-7)
    Get-ChildItem -Path $DailyBackupDir | Where-Object { $_.CreationTime -lt $Limit } | Remove-Item -Recurse -Force
    
    Write-Output "Daily backup completed: $DestDir"

} elseif ($Type -eq "Weekly") {
    # 3. Weekly Backup: Full Directory (excluding build artifacts/git)
    $DestFile = Join-Path $WeeklyBackupDir "Weekly_Full_$Timestamp.zip"
    
    # Use PowerShell's Compress-Archive. We exclude large/useless dirs.
    # Note: We manually filter because Compress-Archive -Exclude is unreliable for deep structures.
    $Items = Get-ChildItem -Path $ProjectDir -Exclude "bin", "obj", ".git", "publish"
    Compress-Archive -Path $Items.FullName -DestinationPath $DestFile
    
    Write-Output "Weekly backup completed: $DestFile"
}
