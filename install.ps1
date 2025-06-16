# SoundScript Installer
# Downloads and sets up SoundScript with optional shortcuts

param(
    [switch]$CreateShortcuts = $false,
    [switch]$AddToPath = $false,
    [string]$InstallPath = "$env:LOCALAPPDATA\SoundScript"
)

Write-Host "🎤 SoundScript Installer" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan

# Check if running as admin for better functionality
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-Host "⚠️  Not running as administrator. Some features may be limited." -ForegroundColor Yellow
}

# Create install directory
Write-Host "📁 Creating install directory: $InstallPath"
New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null

# Download latest release
Write-Host "🌐 Fetching latest release information..."
try {
    $latestRelease = Invoke-RestMethod -Uri "https://api.github.com/repos/edward122/SoundScript/releases/latest"
    $downloadUrl = $latestRelease.assets | Where-Object { $_.name -eq "SoundScript.exe" } | Select-Object -ExpandProperty browser_download_url
    
    if (-not $downloadUrl) {
        throw "Could not find SoundScript.exe in latest release"
    }
    
    Write-Host "📥 Downloading SoundScript v$($latestRelease.tag_name)..."
    $exePath = Join-Path $InstallPath "SoundScript.exe"
    Invoke-WebRequest -Uri $downloadUrl -OutFile $exePath -UseBasicParsing
    
    Write-Host "✅ Downloaded successfully!" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to download: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "💡 You can manually download from: https://github.com/edward122/SoundScript/releases" -ForegroundColor Yellow
    exit 1
}

# Create shortcuts if requested
if ($CreateShortcuts) {
    Write-Host "🔗 Creating shortcuts..."
    
    # Desktop shortcut
    $desktopPath = [Environment]::GetFolderPath("Desktop")
    $shortcutPath = Join-Path $desktopPath "SoundScript.lnk"
    
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = $InstallPath
    $shortcut.Description = "AI-Powered Voice Dictation"
    $shortcut.Save()
    
    # Start Menu shortcut
    $startMenuPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
    $startMenuShortcut = Join-Path $startMenuPath "SoundScript.lnk"
    
    $shortcut2 = $shell.CreateShortcut($startMenuShortcut)
    $shortcut2.TargetPath = $exePath
    $shortcut2.WorkingDirectory = $InstallPath
    $shortcut2.Description = "AI-Powered Voice Dictation"
    $shortcut2.Save()
    
    Write-Host "✅ Shortcuts created!" -ForegroundColor Green
}

# Add to PATH if requested
if ($AddToPath) {
    Write-Host "🛤️  Adding to PATH..."
    $currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($currentPath -notlike "*$InstallPath*") {
        [Environment]::SetEnvironmentVariable("PATH", "$currentPath;$InstallPath", "User")
        Write-Host "✅ Added to PATH! (Restart terminal to use 'SoundScript' command)" -ForegroundColor Green
    } else {
        Write-Host "ℹ️  Already in PATH" -ForegroundColor Blue
    }
}

# Set up auto-start (optional)
$autoStart = Read-Host "🚀 Start SoundScript automatically with Windows? (y/N)"
if ($autoStart -eq 'y' -or $autoStart -eq 'Y') {
    $startupPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup"
    $startupShortcut = Join-Path $startupPath "SoundScript.lnk"
    
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($startupShortcut)
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = $InstallPath
    $shortcut.WindowStyle = 7  # Minimized
    $shortcut.Save()
    
    Write-Host "✅ Auto-start enabled!" -ForegroundColor Green
}

Write-Host ""
Write-Host "🎉 Installation Complete!" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host "📍 Installed to: $InstallPath"
Write-Host "🎮 Run SoundScript.exe to get started"
Write-Host ""
Write-Host "🔑 Next Steps:"
Write-Host "1. Get API keys from OpenAI and Google AI Studio"
Write-Host "2. Run SoundScript and enter your keys in Settings"
Write-Host "3. Start dictating with Ctrl+Win!"
Write-Host ""
Write-Host "📚 Documentation: https://github.com/edward122/SoundScript"

# Ask to run now
$runNow = Read-Host "🚀 Run SoundScript now? (Y/n)"
if ($runNow -ne 'n' -and $runNow -ne 'N') {
    Write-Host "🎤 Starting SoundScript..."
    Start-Process -FilePath $exePath -WorkingDirectory $InstallPath
} 