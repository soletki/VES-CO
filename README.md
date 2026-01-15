Here's the improved `README.md` file, incorporating the new content while maintaining the existing structure and information:

# VESCO

VESCO is a WPF-based timeline video editor and compositor built on .NET 8 and C# 12. It provides frame-accurate playback, multi-track video and audio composition, real-time preview, and export capabilities (H.264 + AAC by default). VESCO is designed for extensibility and experimentation with media pipelines in a desktop environment.

---

## Table of Contents

- [Project Summary](#project-summary)
- [Features](#features)
- [Architecture Overview](#architecture-overview)
- [Key Components & Files](#key-components--files)
- [Getting Started](#getting-started)
- [Build and Run](#build-and-run)
- [Dependencies](#dependencies)
- [Project Structure](#project-structure)
- [Common Workflows](#common-workflows)
- [Export & Encoding](#export--encoding)
- [Limitations & Known Issues](#limitations--known-issues)
- [Recommended Enhancements](#recommended-enhancements)
- [Contributing](#contributing)
- [Troubleshooting](#troubleshooting)
- [License](#license)

---

## Project Summary

VESCO provides a timeline-based interface for arranging video and audio clips across multiple tracks, applying transforms (position, scale, opacity), previewing compositions in real time, and exporting the final project to a single video file. The project targets Windows using WPF and .NET 8.

## Features

- Multi-track video and audio timeline
- Frame-accurate playhead and stepping
- Clip operations: add, move, split, and property editing (position, scale, opacity)
- Real-time downscaled preview for responsive UI
- Per-track mute and volume controls
- Export pipeline to encode timeline into a single video file
- Basic keyboard shortcuts for timeline navigation and editing

## Architecture Overview

The codebase follows a layered approach:

- **UI Layer**: WPF windows and controls (`MainWindow`, `ExportWindow`) for user interaction.
- **Control Layer**: Controllers managing playback, timeline coordinate mapping, and clip interactions (`PlayheadController`, `TimelineController`, `ClipManager`).
- **Data Layer**: Timeline model containing tracks and clips (`Timeline`, `VideoTrack`, `AudioTrack`, `VideoClip`, `AudioClip`).
- **Processing Layer**: Media I/O and export (`VideoSource` using OpenCV, `AudioSource` using NAudio, `VideoRenderer` for export).

The timeline is the canonical model: playback and preview read data from `Timeline`, which composes frames and mixes audio per requested frame number.

## Key Components & Files

- `MainWindow.xaml` / `MainWindow.xaml.cs` — Main UI, preview host, keyboard shortcuts.
- `ClipManager.cs` — Clip selection, dragging, splitting, canvas rendering.
- `PlayheadController.cs` — Playback (play/pause/step), timing, preview updates.
- `Timeline/Timeline.cs` — Core model and composition orchestration.
- `Timeline/VideoSource.cs` — OpenCV wrapper to read video frames and metadata.
- `Timeline/AudioSource.cs` — NAudio wrapper for audio reading.
- `Timeline/VideoClip.cs` / `Timeline/AudioClip.cs` — Clip metadata and per-frame/data retrieval.
- `Timeline/VideoTrack.cs` / `Timeline/AudioTrack.cs` — Track-level composition and mixing.
- `VideoRenderer.cs` / `ExportWindow.xaml.cs` — Export orchestration and settings UI.

## Getting Started

### Prerequisites

- Windows 10 or later
- Visual Studio 2022 (or later) with the WPF/.NET desktop workload
- .NET 8 SDK
- Native OpenCV binaries for your platform (x64/x86) if using OpenCV bindings

### Steps to Get Started

1. Clone the repo:
   git clone https://github.com/soletki/VES-CO.git
2. Open the solution in Visual Studio.
3. Restore NuGet packages: __Project > Restore NuGet Packages__ (or `dotnet restore`).
4. Ensure native OpenCV and encoder binaries (if needed) are available in the output folder or PATH.
5. Build and run the project.

## Build and Run

- In Visual Studio: Open the solution, then __Build > Build Solution__ and __Debug > Start Debugging__.
- Command line: `dotnet build` (requires .NET 8 SDK present).

**Note**: If the project relies on native OpenCV DLLs or FFmpeg binaries, copy the appropriate native files into the project's `bin/Debug/net8.0` or set the PATH to include those binaries.

## Dependencies

- .NET 8
- WPF
- OpenCvSharp (or equivalent OpenCV .NET binding) — for `VideoSource` and frame access
- NAudio — for audio file reading and sample access
- An encoder backend (FFmpeg or Media Foundation) used by `VideoRenderer` for final encoding (verify which one is present and bundle accordingly)

## Project Structure

Top-level folders and responsibilities:

- `Timeline/` — Timeline model, track & clip classes, sources
- Root — UI windows and controllers (`MainWindow`, `ExportWindow`, `ClipManager`, `PlayheadController`, `VideoRenderer`)

Pick any file and search for class names in the project to see usage; most runtime behavior revolves around `Timeline`.

## Common Workflows

- **Adding media**: Drag media into the media bin (UI) then onto the timeline; clips store timeline start, source offsets, and length.
- **Seeking**: Click or drag the playhead or use keyboard shortcuts (space to play/pause, `.` and `,` to step frames).
- **Editing**: Select a clip to adjust X/Y/scale/opacity via property inputs; use split/cut to divide clips at the playhead.
- **Export**: Open the Export dialog, set codec/quality/resolution, and start export. Exports run as a background task (verify cancellation token and progress reporting in `VideoRenderer`).

## Export & Encoding

The export pipeline composes each frame with `Timeline.CompositeFrames()` and mixes audio with `Timeline.MixAudio()`. Final frames and audio are encoded into an output container. Default settings target H.264 video and AAC audio; confirm encoder availability on the host system.

### Performance Considerations

- Export is CPU and I/O intensive. Use a dedicated background thread and report progress to the UI.
- Consider configuring a frame cache or using FFmpeg for faster and more reliable seeking/encoding.

## Limitations & Known Issues

- Seeking with OpenCV `VideoCapture` may be slow and imprecise for some formats.
- Audio mixing is basic additive mixing and may require normalization to avoid clipping.
- Preview rendering on the UI thread can cause UI freezes; prefer a downscaled buffer and background decoding.
- WPF + native dependencies restrict cross-platform portability.

## Recommended Enhancements

- Add frame caching (LRU) for source frames to reduce repeated decoding.
- Move heavy decode and export work to background services with `CancellationToken` and progress reporting.
- Use FFmpeg for deterministic seeking and encoding if encoder quality/performance is critical.
- Add unit tests for `Timeline` frame mapping and `MixAudio()` correctness.
- Add a `.editorconfig` and `CONTRIBUTING.md` to standardize contributions.

## Contributing

Contributions are welcome. Suggested process:

1. Fork the repository
2. Create a feature branch
3. Add code/tests and run locally
4. Open a pull request with a clear description

Please follow consistent code style. Add unit tests for behavioral changes and document breaking changes in PRs.

## Troubleshooting

- **Black preview or no frames**: Verify native OpenCV DLLs are present and `VideoSource` can open files.
- **No audio**: Ensure NAudio can open your audio format and sample rate; check `AudioSource` exceptions in debug output.
- **Export failures**: Check encoder availability (FFmpeg or Media Foundation) and ensure output path is writable.

## License

Specify your project license here (e.g., MIT). If not set, add a `LICENSE` file to the repo.

---

If you want, I can:
- Commit this README.md into your repository.
- Create `CONTRIBUTING.md` and a full `.editorconfig` consistent with your project.
- Add a short 'How to build' CI workflow for GitHub Actions.

This revised README maintains the original structure while enhancing clarity and organization, ensuring that users can easily navigate and understand the project.