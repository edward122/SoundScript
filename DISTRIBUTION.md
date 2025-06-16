# 📦 SoundScript Distribution Guide

This guide explains how to build, distribute, and set up auto-updates for SoundScript.

## 🚀 Quick Distribution (Recommended)

### For End Users
1. **Download**: Get `SoundScript.exe` from [GitHub Releases](https://github.com/YOUR_USERNAME/SoundScript/releases)
2. **Run**: Double-click the executable - no installation needed!
3. **Setup**: Enter API keys in Settings and start dictating with `Ctrl+Win`

### For Developers/Distributors
1. **Build**: Run `build-release.bat` to create `Release/SoundScript.exe`
2. **Share**: Distribute the single executable file
3. **Updates**: Users get automatic update notifications

## 🏗️ Building from Source

### Prerequisites
- Windows 10/11
- .NET 8 SDK
- Git

### Build Steps
```bash
# Clone repository
git clone https://github.com/YOUR_USERNAME/SoundScript.git
cd SoundScript

# Build release version
build-release.bat

# Or manually:
dotnet publish SoundScript/SoundScript.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o Release
```

## 🔄 Auto-Update System

### How It Works
1. **Daily Checks**: App checks GitHub releases API once per day
2. **Version Compare**: Compares current version with latest release
3. **User Prompt**: Shows update dialog if newer version available
4. **Manual Check**: Users can check via Settings → "Check for Updates"

### Setting Up Auto-Updates

1. **Replace GitHub URL**: Update `YOUR_USERNAME` in:
   - `SoundScript/Services/UpdateService.cs`
   - `.github/workflows/release.yml`
   - `README.md`
   - `install.ps1`

2. **Create GitHub Repository**:
   ```bash
   git remote add origin https://github.com/YOUR_USERNAME/SoundScript.git
   git push -u origin main
   ```

3. **Enable GitHub Actions**: The workflow in `.github/workflows/release.yml` will automatically build and release

4. **Create First Release**:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

## 📋 Distribution Options

### Option 1: Single Executable (Easiest)
- **Pros**: No installation, portable, self-contained
- **Cons**: Larger file size (~50-80MB)
- **Best for**: Most users, quick sharing

### Option 2: PowerShell Installer
```powershell
# Basic install
iex (iwr -useb https://raw.githubusercontent.com/YOUR_USERNAME/SoundScript/main/install.ps1).Content

# With shortcuts and PATH
powershell -c "iex (iwr -useb https://raw.githubusercontent.com/YOUR_USERNAME/SoundScript/main/install.ps1).Content" -CreateShortcuts -AddToPath
```

### Option 3: Manual Installation
1. Download `SoundScript.exe`
2. Create folder: `%LOCALAPPDATA%\SoundScript`
3. Move executable to folder
4. Create shortcuts as needed

## 🔧 Configuration for Distribution

### Update GitHub URLs
Replace `YOUR_USERNAME` in these files:
- `SoundScript/Services/UpdateService.cs` (line 13)
- `.github/workflows/release.yml` (line 25)
- `README.md` (multiple locations)
- `install.ps1` (line 25)

### Version Management
Update version in `SoundScript/SoundScript.csproj`:
```xml
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>
```

### Custom Branding
- Replace `logo.ico` with your icon
- Update company name in project file
- Modify about dialog text

## 📊 Release Process

### Automated (Recommended)
1. **Update Version**: Increment version in `SoundScript.csproj`
2. **Commit Changes**: `git commit -am "Release v1.0.1"`
3. **Create Tag**: `git tag v1.0.1`
4. **Push**: `git push origin v1.0.1`
5. **GitHub Actions**: Automatically builds and creates release

### Manual
1. **Build**: Run `build-release.bat`
2. **Test**: Verify the executable works
3. **Create Release**: Upload to GitHub releases manually
4. **Announce**: Update README, notify users

## 🛡️ Security Considerations

### Code Signing (Optional)
For professional distribution, consider code signing:
```bash
# With certificate
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com Release/SoundScript.exe
```

### Antivirus False Positives
- Single-file executables may trigger false positives
- Consider submitting to antivirus vendors for whitelisting
- Code signing reduces false positives

## 📈 Analytics & Feedback

### Usage Tracking (Optional)
- Add telemetry for usage statistics
- Track feature adoption
- Monitor error rates

### User Feedback
- GitHub Issues for bug reports
- Discussions for feature requests
- Email support for direct contact

## 🎯 Distribution Checklist

- [ ] Replace all `YOUR_USERNAME` placeholders
- [ ] Update version numbers
- [ ] Test build process
- [ ] Verify auto-update works
- [ ] Create GitHub repository
- [ ] Set up GitHub Actions
- [ ] Test installation methods
- [ ] Update documentation
- [ ] Create first release
- [ ] Announce to users

## 🆘 Troubleshooting

### Build Issues
- **Missing .NET**: Install .NET 8 SDK
- **Permission Errors**: Run as administrator
- **Path Issues**: Use full paths in scripts

### Distribution Issues
- **Large File Size**: Normal for self-contained apps
- **Slow Download**: Consider hosting on CDN
- **Antivirus Blocks**: Submit for whitelisting

### Update Issues
- **GitHub API Limits**: Authenticated requests have higher limits
- **Network Errors**: Handle gracefully in UpdateService
- **Version Parsing**: Ensure consistent version format

---

**Ready to distribute? Follow the checklist above and your users will have a smooth experience! 🚀** 