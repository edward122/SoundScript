# 🎤 SoundScript - AI-Powered Voice Dictation

Transform your voice into perfectly formatted text with AI-powered transcription and polishing.

## ✨ Features

- **🎯 Global Hotkey**: Press `Ctrl+Win` to start/stop recording from anywhere
- **🤖 AI Transcription**: Powered by OpenAI Whisper for accurate speech-to-text
- **✨ Text Polishing**: Google Gemini AI improves grammar and formatting
- **📋 Auto-Paste**: Automatically paste results where you need them
- **🎵 Smart Audio**: Mutes background apps during recording
- **📊 Analytics**: Track your dictation stats and word usage
- **💾 Audio Download**: Save your recordings as WAV files
- **🌙 Dark Theme**: Beautiful dark interface
- **🔄 Auto-Updates**: Stay up-to-date with the latest features

## 🚀 Quick Start

### Option 1: Download Executable (Recommended)
1. Download `SoundScript.exe` from the [latest release](https://github.com/YOUR_USERNAME/SoundScript/releases)
2. Run the executable - no installation required!
3. Enter your API keys in Settings
4. Start dictating with `Ctrl+Win`

### Option 2: Build from Source
```bash
git clone https://github.com/YOUR_USERNAME/SoundScript.git
cd SoundScript
dotnet restore
dotnet build
```

## 🔑 API Keys Required

You'll need API keys from:

### OpenAI (for Whisper transcription)
1. Go to [OpenAI API Keys](https://platform.openai.com/api-keys)
2. Create a new API key
3. Copy and paste into SoundScript Settings

### Google AI Studio (for text polishing)
1. Go to [Google AI Studio](https://aistudio.google.com/app/apikey)
2. Create a new API key
3. Copy and paste into SoundScript Settings

## 🎮 How to Use

1. **Setup**: Enter your API keys in Settings (⚙️ button)
2. **Record**: Press and hold `Ctrl+Win` anywhere on your computer
3. **Speak**: Talk naturally while holding the hotkey
4. **Release**: Let go of `Ctrl+Win` to stop recording
5. **Magic**: Your text appears and gets automatically pasted!

## 📁 File Locations

- **Settings**: `%AppData%\SoundScript\settings.json`
- **Database**: `%AppData%\SoundScript\history.db`
- **Audio Files**: `%AppData%\SoundScript\soundstart.mp3`, `soundend.mp3`

## 🔧 Customization

### Audio Feedback
Place custom audio files in `%AppData%\SoundScript\`:
- `soundstart.mp3` - Plays when recording starts
- `soundend.mp3` - Plays when recording ends

### Settings Options
- **Auto-paste**: Automatically paste results
- **Skip polishing**: Use raw transcription for speed
- **Model selection**: Choose different AI models
- **Max retries**: Configure retry attempts

## 🆘 Troubleshooting

### Common Issues

**"No audio recorded"**
- Check microphone permissions
- Ensure microphone is not muted
- Try running as administrator

**"API key invalid"**
- Verify API keys are correct
- Check API key permissions
- Ensure sufficient API credits

**"Hotkey not working"**
- Try running as administrator
- Check for conflicting hotkeys
- Restart the application

**"Audio not muting"**
- Run as administrator for better audio control
- Check Windows audio permissions

### Getting Help
1. Check the [Issues](https://github.com/YOUR_USERNAME/SoundScript/issues) page
2. Create a new issue with details about your problem
3. Include your Windows version and error messages

## 🔄 Updates

SoundScript automatically checks for updates daily. You can also manually check:
1. Open Settings (⚙️)
2. Click "🔄 Check for Updates"
3. Download and replace the executable if an update is available

## 🏗️ For Developers

### Building
```bash
# Debug build
dotnet build

# Release build (single file)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Or use the build script
build-release.bat
```

### Project Structure
```
SoundScript/
├── Services/           # Core services (audio, AI, etc.)
├── ViewModels/         # MVVM view models
├── Views/              # WPF windows and dialogs
├── Models/             # Data models
├── Utils/              # Utility classes
└── Resources/          # Icons and assets
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **OpenAI** for Whisper API
- **Google** for Gemini AI
- **NAudio** for audio processing
- **SQLite** for data storage

## 🌟 Support

If you find SoundScript useful, please:
- ⭐ Star this repository
- 🐛 Report bugs
- 💡 Suggest features
- 🔄 Share with others

---

**Made with ❤️ for productivity enthusiasts** 