# Snake

A classic Snake game built with Unity 6 (6000.0.85f1) and the Universal Render Pipeline. It supports touch swipe controls for mobile and ships with clean, procedurally generated visuals and sound effects.

## Features

- Classic Snake gameplay: eat berries, grow, chain speed combos, level up, and avoid walls and yourself (with a brief grace window to turn away at the last moment)
- Swipe input for Android, plus keyboard and mouse controls through the Input System
- Procedurally generated crisp matte visuals, natural level themes, and particle effects
- Synthesised chiptune-style sound effects and optional mobile haptics
- In-game UI for score, best score, combo bar, pause menu, and game over stats

## Project structure

| Path | Contents |
| --- | --- |
| `Assets/Scripts/Core` | Game logic (`SnakeGame`), bootstrap, swipe input |
| `Assets/Scripts/Visuals` | Arena, snake and berry views, themes, effects |
| `Assets/Scripts/Audio` | Sound effects (`Sfx`) |
| `Assets/Scripts/UI` | Game UI (`GameUI`) |
| `Assets/Editor` | Android app icon setup and APK build script (`SnakeAndroidBuild`) |
| `Assets/Icons` | Generated Android adaptive, legacy, and round app icons |
| `Assets/Scenes` | `SampleScene`, the main scene |
| `Art` | Icon generator script (`make_app_icon.py`) |

## Getting started

1. Install [Unity Hub](https://unity.com/download) and Unity Editor **6000.0.85f1**.
2. Clone this repository:
   ```bash
   git clone <repo-url>
   ```
3. In Unity Hub, choose **Add > Add project from disk** and select the cloned folder.
4. Open `Assets/Scenes/SampleScene` and press **Play**.

## Building for Android

From the Unity Editor, use **Tools > Snake > Build APK** (or **File > Build Profiles > Android**).

From the command line (with Unity closed):

```bash
Unity -batchmode -nographics -projectPath . -executeMethod SnakeAndroidBuild.CommandLine -logFile Logs/android-build.log
```

This applies the icons in `Assets/Icons` and outputs `apks/snake.apk`. To regenerate the icons after editing `Art/make_app_icon.py`, run `python3 Art/make_app_icon.py`.

## License

No license has been specified yet.
