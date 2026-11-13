# Snake

A classic Snake game built with Unity 6 (6000.0.85f1) and the Universal Render Pipeline. It supports touch swipe controls for mobile and ships with generated visuals, sound effects and post-processing.

## Features

- Classic Snake gameplay: eat food, grow, avoid walls and yourself
- Swipe input for Android, plus keyboard input through the Input System
- Procedurally generated art, themes and visual effects
- Generated sound effects
- In-game UI for score and game state

## Project structure

| Path | Contents |
| --- | --- |
| `Assets/Scripts/Core` | Game logic (`SnakeGame`), bootstrap, swipe input |
| `Assets/Scripts/Visuals` | Arena, snake and food views, themes, effects, post-processing |
| `Assets/Scripts/Audio` | Sound effects (`Sfx`) |
| `Assets/Scripts/UI` | Game UI (`GameUI`) |
| `Assets/Scenes` | `SampleScene`, the main scene |

## Getting started

1. Install [Unity Hub](https://unity.com/download) and Unity Editor **6000.0.85f1**.
2. Clone this repository:
   ```bash
   git clone <repo-url>
   ```
3. In Unity Hub, choose **Add > Add project from disk** and select the cloned folder.
4. Open `Assets/Scenes/SampleScene` and press **Play**.

## Building for Android

Open **File > Build Profiles**, select **Android**, and click **Build**.

## License

No license has been specified yet.
