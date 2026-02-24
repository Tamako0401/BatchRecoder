# BatchRecoder

BatchRecoder is a small WPF-based tool that batches FFmpeg jobs, keeps a queue with pause/resume/stop controls, and automatically handles extracting media information from scanned video files.

## Requirements
- Windows 10/11 with .NET Framework 4.7.2 (or newer) installed.
- A directory that contains the videos you want to process. Supported extensions include `.mp4`, `.flv`, `.mkv`, `.avi`, `.mov`, `.wmv`, `.ts`, `.webm`, `.m4v`, plus any file ending with `.recoded.*` (e.g., `movie.recoded.mp4`).

## Building & Running
1. Open `BatchRecoder.sln` in Visual Studio or Rider, then build and run the `BatchRecoder` project.
2. Alternatively, from the repository root run:
   ```bash
   dotnet run --project BatchRecoder/BatchRecoder.csproj
   ```

The UI will automatically download the latest FFmpeg build into the executable directory the first time it runs (you will see download progress logged at the bottom). If FFmpeg is already present, the app reads its version and logs it during startup.

## Key Features
- **Auto FFmpeg Setup**: Missing `ffmpeg.exe` / `ffprobe.exe` triggers a download from gyan.dev; the progress is logged, and the files are saved next to the executable.
- **Queue Management with Pause/Resume**: The queue distinguishes between Pending, Queued, Processing, Processed, and Failed states. `Pause` suspends the current FFmpeg process via NT-level calls, while `Stop` cancels the token, clears the queue, and resets statuses back to Pending.
- **Robust Scan Logic**: Every video file in the target directory (including `*.recoded.*` outputs) appears in the list. Files whose name includes `.recoded` are immediately marked as Processed, and any `.recoded.tmp` leftovers are deleted before scanning so the source file restarts processing from scratch.
- **Safe File Naming**: Processed outputs are named `source.recoded.ext` and temporary outputs `source.recoded.tmp.ext`, preserving the original extension so FFmpeg knows the proper container. The scanner identifies processed files by checking for the `.recoded` pattern in the base name.
- **Encoding Parameters**:
  - Choose among `libx264`, `libx265`, NVENC/QSV/AMF variants, control `CRF`, `preset`, `profile`, `tune`, audio encoder/bitrate, and extra FFmpeg args.
  - Select whitespace-friendly target resolutions such as 720p/1080p/2k/4k plus 16:9/16:10 aspect ratio scaling or provide a custom width/height.
  - The UI exposes drop-downs backed by `EncoderSettings` lists to avoid null bindings in the designer.
- **UI Polish**: The file DataGrid uses lighter grid lines and auto column widths for readability, and the log area auto-truncates past entries to stay responsive.

## File Life Cycle
- `source.recoded.tmp.ext` – created while FFmpeg is running. Cancelling or failure removes this file and resets the status so the full job restarts.
- `source.recoded.ext` – created when FFmpeg completes successfully; any existing file is overwritten. Once the scanner sees this file, the matching source is marked as Processed and skipped.

## Troubleshooting & Tips
- If FFmpeg exits with `-22`, inspect the generated arguments in the log (the last logged FFmpeg command shows the exact flags passed). Ensure your target resolution makes sense and that the source file path contains no illegal characters.
- Need to re-run a single file? Select it in the list, wait till it becomes `Failed`, then click **Retry**.
- Want more control over FFmpeg parameters? Use the **Extra Args** box but keep values space-separated and avoid quotes unless necessary.

## Extending the Tool
- Add more video file extensions to `MainViewModel.ScanDirectoryAsync` if your workflow uses other containers.
- Extend `EncoderSettings.TargetResolutions` and update the scaling switch to support new presets.

Happy encoding!

