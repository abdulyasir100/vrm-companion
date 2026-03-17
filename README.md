# VRM Companion

Unity Android app that displays a VRM avatar as an AI companion. Connects to a backend server via WebSocket to receive chat messages, emotions, and audio — then renders them as a living, talking character.

Built for Galaxy Tab A9+ but works on any Android device.

## Features

- **VRM Avatar Display** — loads `.vrm` files at runtime from StreamingAssets
- **WebSocket Connection** — persistent connection with auto-reconnect (exponential backoff 2-30s)
- **Emotion Expressions** — smooth blendshape transitions for 6 emotions (happy, sad, surprised, angry, thinking, neutral)
- **Audio Lip-Sync** — real-time mouth movement driven by audio amplitude
- **Idle Animations** — autonomous blinking, breathing, and body sway
- **Costume Switching** — swap VRM models on command with walk-off/walk-on animation
- **Sleep Mode** — closed eyes, slow breathing, floating ZZZ overlay
- **Offline Indicator** — visual feedback when disconnected from server
- **Touch Interaction** — tap responses
- **Subtitles** — displays chat text on screen
- **Portrait Lock** — designed for tablet portrait mode

## Requirements

- **Unity 2022.3.62f1** (LTS)
- **UniVRM** package (for VRM loading)
- Android Build Support module

## Setup

1. Open the project in Unity 2022.3.62f1
2. Import UniVRM if not already present (Window > Package Manager)
3. Place your VRM files in `Assets/StreamingAssets/`:
   - Name them to match `Config.cs` entries (default: `SuiseiMaid.vrm`, `SuiseiIdol.vrm`, `SuiseiCasual.vrm`)
   - Or edit `Config.cs` to match your filenames
4. Update `Config.cs` with your server IP:
   ```csharp
   public const string AvatarServerHost = "your-server-ip";
   public const int    AvatarServerPort = 8800;
   ```
5. Build for Android (File > Build Settings > Android > Build)

## Project Structure

```
Assets/
├── Scripts/
│   ├── Config.cs              # All configuration (server URL, timing, animation params)
│   ├── AvatarController.cs    # Root controller — loads VRM, wires sub-controllers
│   ├── WebSocketClient.cs     # Persistent WebSocket with auto-reconnect
│   ├── ExpressionController.cs # Emotion tag → VRM blendshape mapping
│   ├── AnimationController.cs  # Emotion tag → Animator state mapping
│   ├── LipSyncController.cs   # Audio-reactive mouth movement
│   ├── AudioPlayer.cs         # Downloads and plays WAV from server
│   ├── IdleBehavior.cs        # Blinking, breathing, body sway
│   ├── SubtitleController.cs  # On-screen chat text display
│   ├── CostumePicker.cs       # Costume selection UI
│   ├── TouchInteraction.cs    # Tap/touch responses
│   ├── SleepOverlay.cs        # Sleep mode visuals (ZZZ, closed eyes)
│   ├── OfflineMode.cs         # Disconnection indicator
│   └── CameraFraming.cs       # Camera positioning for VRM model
│
├── StreamingAssets/            # VRM files go here (gitignored)
├── Animations/                # Animation clips and controllers
├── Fonts/
└── Textures/
```

## WebSocket Protocol

The app connects to `ws://<server>:<port>/ws` and expects JSON messages:

```json
{"type": "chat", "text": "Hello!", "emotion": "HAPPY", "audio_url": "/audio/abc123.wav"}
{"type": "costume", "costume_id": "casual"}
{"type": "sleep", "sleeping": true}
```

## Configuration

All tuneable values are in `Config.cs`:

| Setting | Default | Description |
|---------|---------|-------------|
| `AvatarServerHost` | `YOUR_SERVER_IP` | Server IP (change to yours) |
| `AvatarServerPort` | `8800` | Server port |
| `ExpressionBlendSpeed` | `5.0` | Emotion transition speed |
| `LipSyncSensitivity` | `30.0` | Mouth movement amplitude |
| `BlinkIntervalMin/Max` | `2.5-6.0s` | Random blink timing |
| `BreathingSpeed` | `1.2` | Breathing animation speed |
| `WalkOffDuration` | `1.5s` | Costume change walk-off time |

## Companion Server

This app is designed to work with [avatar-server](https://github.com/venomaru/avatar-server) but any WebSocket server sending the expected JSON format will work.

## License

MIT
