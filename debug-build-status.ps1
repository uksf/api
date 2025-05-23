# Debug script for diagnosing stuck modpack builds
# Run this when a build appears to be frozen to gather diagnostic information

Write-Host "=== Build Process Diagnostic Script ===" -ForegroundColor Cyan
Write-Host "Timestamp: $(Get-Date)" -ForegroundColor Gray

# Check for running git processes
Write-Host "`n=== Git Processes ===" -ForegroundColor Yellow
$gitProcesses = Get-Process -Name "git" -ErrorAction SilentlyContinue
if ($gitProcesses) {
    Write-Host "Found running git processes:" -ForegroundColor Red
    $gitProcesses | Format-Table Id, ProcessName, CPU, StartTime, Path -AutoSize
    
    # Show command lines for git processes
    Write-Host "`nGit process command lines:" -ForegroundColor Yellow
    foreach ($proc in $gitProcesses) {
        try {
            $cmdLine = (Get-CimInstance Win32_Process -Filter "ProcessId = $($proc.Id)").CommandLine
            Write-Host "PID $($proc.Id): $cmdLine" -ForegroundColor White
        } catch {
            Write-Host "PID $($proc.Id): Unable to get command line" -ForegroundColor Gray
        }
    }
} else {
    Write-Host "No git processes found" -ForegroundColor Green
}

# Check for hanging cmd.exe processes (which execute git commands)
Write-Host "`n=== CMD Processes ===" -ForegroundColor Yellow
$cmdProcesses = Get-Process -Name "cmd" -ErrorAction SilentlyContinue
if ($cmdProcesses) {
    Write-Host "Found running cmd processes:" -ForegroundColor Red
    $cmdProcesses | Format-Table Id, ProcessName, CPU, StartTime -AutoSize
} else {
    Write-Host "No cmd processes found" -ForegroundColor Green
}

# Check for the API process
Write-Host "`n=== API Process ===" -ForegroundColor Yellow
$apiProcesses = Get-Process -Name "*Api*" -ErrorAction SilentlyContinue
if ($apiProcesses) {
    Write-Host "Found API processes:" -ForegroundColor Green
    $apiProcesses | Format-Table Id, ProcessName, CPU, StartTime -AutoSize
} else {
    Write-Host "No API processes found" -ForegroundColor Red
}

# Check network connections that might indicate stuck git operations
Write-Host "`n=== Network Connections (Git-related) ===" -ForegroundColor Yellow
$connections = netstat -an | Select-String ":443|:22|:80" | Select-String "ESTABLISHED"
if ($connections) {
    Write-Host "Active network connections (HTTPS/SSH/HTTP):" -ForegroundColor White
    $connections | ForEach-Object { Write-Host $_.Line -ForegroundColor Gray }
} else {
    Write-Host "No relevant network connections found" -ForegroundColor Green
}

# Check for git lock files in common build directories
Write-Host "`n=== Git Lock Files ===" -ForegroundColor Yellow
$possiblePaths = @(
    "P:\sources\*",
    ".\sources\*",
    "C:\build\sources\*"
)

$lockFiles = @()
foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        $locks = Get-ChildItem -Path $path -Recurse -Name ".git\index.lock" -ErrorAction SilentlyContinue
        if ($locks) {
            $lockFiles += $locks
        }
    }
}

if ($lockFiles) {
    Write-Host "Found git lock files (may indicate stuck operations):" -ForegroundColor Red
    $lockFiles | ForEach-Object { Write-Host $_ -ForegroundColor White }
    Write-Host "`nTo resolve, you may need to manually delete these lock files:" -ForegroundColor Yellow
    $lockFiles | ForEach-Object { Write-Host "Remove-Item '$_' -Force" -ForegroundColor Cyan }
} else {
    Write-Host "No git lock files found" -ForegroundColor Green
}

# Show system performance
Write-Host "`n=== System Performance ===" -ForegroundColor Yellow
$cpu = Get-CimInstance -ClassName Win32_Processor | Measure-Object -Property LoadPercentage -Average
$memory = Get-CimInstance -ClassName Win32_OperatingSystem
$memoryUsed = [math]::Round((($memory.TotalVisibleMemorySize - $memory.FreePhysicalMemory) / $memory.TotalVisibleMemorySize) * 100, 2)

Write-Host "CPU Usage: $($cpu.Average)%" -ForegroundColor $(if ($cpu.Average -gt 80) { "Red" } else { "Green" })
Write-Host "Memory Usage: $memoryUsed%" -ForegroundColor $(if ($memoryUsed -gt 80) { "Red" } else { "Green" })

Write-Host "`n=== Recommendations ===" -ForegroundColor Cyan
Write-Host "1. If git processes are stuck for >5 minutes, use the emergency cleanup API" -ForegroundColor White
Write-Host "2. Remove any .git\index.lock files found above" -ForegroundColor White
Write-Host "3. Check network connectivity to git repositories" -ForegroundColor White
Write-Host "4. The build system now tracks only its own processes for safer cleanup" -ForegroundColor White

Write-Host "`n=== Emergency Cleanup ===" -ForegroundColor Yellow
Write-Host "If builds are stuck, use the emergency cleanup API endpoint:" -ForegroundColor White
Write-Host "POST /api/modpack/builds/emergency-cleanup" -ForegroundColor Cyan
Write-Host "This will only kill processes created by the build system, not all git/cmd processes" -ForegroundColor Green

Write-Host "`n=== Script Complete ===" -ForegroundColor Green 
