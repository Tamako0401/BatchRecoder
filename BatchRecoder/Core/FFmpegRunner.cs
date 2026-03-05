using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BatchRecoder.Models;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace BatchRecoder.Core
{
    public class FFmpegRunner
    {
        private string _ffmpegPath = "ffmpeg";
        private string _ffprobePath = "ffprobe";
        private Process _activeProcess; // Keep track of the active process

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtResumeProcess(IntPtr processHandle);

        public void Pause()
        {
            if (_activeProcess != null && !_activeProcess.HasExited)
            {
                try { NtSuspendProcess(_activeProcess.Handle); } catch { }
            }
        }

        public void Resume()
        {
            if (_activeProcess != null && !_activeProcess.HasExited)
            {
                try { NtResumeProcess(_activeProcess.Handle); } catch { }
            }
        }

        public FFmpegRunner()
        {
            if (File.Exists("ffmpeg.exe")) _ffmpegPath = "ffmpeg.exe";
            if (File.Exists("ffprobe.exe")) _ffprobePath = "ffprobe.exe";
        }

        public async Task<bool> EnsureFFmpegExistsAsync(Action<string> logCallback, CancellationToken token)
        {
            if (File.Exists("ffmpeg.exe") && File.Exists("ffprobe.exe"))
            {
                 _ffmpegPath = "ffmpeg.exe";
                 _ffprobePath = "ffprobe.exe";
                return true;
            }

            logCallback("未检测到 FFmpeg，准备下载...");
            string downloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
            string zipPath = "ffmpeg.zip";
            string extractPath = "ffmpeg_temp";

            try
            {
                using (var client = new HttpClient())
                {
                    logCallback($"正在下载: {downloadUrl}");
                    // 使用 ResponseHeadersRead 以便在下载过程中取消
                    using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, token))
                    {
                        response.EnsureSuccessStatusCode();

                        // 进度下载
                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var canReportProgress = totalBytes != -1;

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            var buffer = new byte[81920];
                            var needsProgressUpdate = true;
                            long totalRead = 0;
                            int read;

                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, read, token);
                                totalRead += read;

                                if (canReportProgress && needsProgressUpdate)
                                {
                                    // 简单的去抖动，避免日志刷新过快，实际开发可以用 IProgress<T>
                                    if (totalRead % (1024 * 1024 * 5) < 81920) // 每 5MB 更新一次
                                    {
                                         var progress = (double)totalRead / totalBytes * 100;
                                         logCallback($"下载进度: {progress:F1}% ({totalRead / 1024 / 1024}MB / {totalBytes / 1024 / 1024}MB)");
                                         needsProgressUpdate = true;
                                    }
                                }
                            }
                        }
                    }
                }

                logCallback("下载完成，正在解压...");
                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);

                ZipFile.ExtractToDirectory(zipPath, extractPath);

                // 查找 ffmpeg.exe 和 ffprobe.exe
                var ffmpegFile = Directory.GetFiles(extractPath, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                var ffprobeFile = Directory.GetFiles(extractPath, "ffprobe.exe", SearchOption.AllDirectories).FirstOrDefault();

                if (ffmpegFile != null)
                {
                    File.Copy(ffmpegFile, "ffmpeg.exe", true);
                    _ffmpegPath = "ffmpeg.exe";
                }

                if (ffprobeFile != null)
                {
                    File.Copy(ffprobeFile, "ffprobe.exe", true);
                    _ffprobePath = "ffprobe.exe";
                }

                logCallback(await GetFFmpegVersionAsync());
                
                // 清理
                try
                {
                    File.Delete(zipPath);
                    Directory.Delete(extractPath, true);
                }
                catch { /* 忽略清理错误 */ }

                return File.Exists(_ffmpegPath);
            }
            catch (Exception ex)
            {
                logCallback($"下载或安装 FFmpeg 失败: {ex.Message}");
                return false;
            }
        }

        public async Task<string> GetFFmpegVersionAsync()
        {
             if (!File.Exists(_ffmpegPath)) return "FFmpeg 未找到";

             try
             {
                 var processInfo = new ProcessStartInfo
                 {
                     FileName = _ffmpegPath,
                     Arguments = "-version",
                     RedirectStandardOutput = true,
                     UseShellExecute = false,
                     CreateNoWindow = true,
                     StandardOutputEncoding = Encoding.UTF8
                 };

                 using (var process = new Process { StartInfo = processInfo })
                 {
                     process.Start();
                     var output = await process.StandardOutput.ReadToEndAsync();
                     await process.WaitForExitAsync();
                     
                     var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                     return lines.Length > 0 ? lines[0] : "无法获取版本信息";
                 }
             }
             catch
             {
                 return "无法获取版本信息";
             }
        }

        // Add to FFmpegRunner class
        public async Task<bool> EncodeAsync(VideoFileInfo video, EncoderSettings settings, Action<string> logCallback,
            CancellationToken token, string customOutputDirectory = null)
        {
            // 确保 Duration 已加载，否则先加载媒体信息
            if (!video.MediaInfoLoaded || video.Duration.TotalSeconds <= 0)
            {
                // 如果 ScanDirectoryAsync 还在运行或者未运行完毕，这里强制再加载一次以确保能获取 Duration
                // 虽然略微影响启动速度，但能保证进度条正确
                await LoadMediaInfoAsync(video);
            }

            var finalOutputPath = VideoFileInfo.GetProcessedFilePath(video.FilePath, customOutputDirectory);
            var tempOutputPath = VideoFileInfo.GetTemporaryFilePath(video.FilePath, customOutputDirectory);
            
            // Clean up existing temp file if any
            if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);

            // Use temp path for ffmpeg output
            var arguments = settings.BuildArguments(video.FilePath, tempOutputPath);

            logCallback($"开始转码: {video.FileName} -> {arguments}");

            var processInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = arguments,
                RedirectStandardError = true, // FFmpeg 输出进度在 stderr
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                using (var process = new Process { StartInfo = processInfo })
                {
                    _activeProcess = process; // Set active process
                    process.EnableRaisingEvents = true;

                    // 进度解析逻辑
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (string.IsNullOrEmpty(e.Data)) return;

                        // 解析时间: time=00:00:05.12 或 time=00:00:05
                        // 使用 Regex 提取更稳健
                        var timeMatch = Regex.Match(e.Data, @"time=\s*(\d{2}:\d{2}:\d{2}(\.\d+)?)");
                        if (timeMatch.Success)
                        {
                            var timeStr = timeMatch.Groups[1].Value;
                            if (TimeSpan.TryParse(timeStr, out var currentTime))
                            {
                                if (video.Duration.TotalSeconds > 0)
                                {
                                    var progress = currentTime.TotalSeconds / video.Duration.TotalSeconds * 100;
                                    video.Progress = Math.Min(99.9, Math.Max(0, progress));
                                }
                            }
                        }

                        // 解析速度: speed=1.2x
                        var speedMatch = Regex.Match(e.Data, @"speed=\s*(\d+(\.\d+)?)x");
                        if (speedMatch.Success)
                        {
                            var speedStr = speedMatch.Groups[1].Value;
                            if (double.TryParse(speedStr, out var speed) && speed > 0 &&
                                video.Duration.TotalSeconds > 0 && video.Progress > 0)
                            {
                                var remainingSeconds = video.Duration.TotalSeconds * (100 - video.Progress) / 100 /
                                                       speed;
                                video.Eta = TimeSpan.FromSeconds(remainingSeconds).ToString(@"hh\:mm\:ss");
                            }
                        }
                    };

                    process.Start();
                    process.BeginErrorReadLine();

                    // 等待退出，支持取消
                    try
                    {
                        await process.WaitForExitAsync(token);
                    }
                    catch (TaskCanceledException)
                    {
                        if (!process.HasExited) process.Kill(); // 强制结束 FFmpeg
                        // 清理可能产生的半成品文件
                        if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);
                        logCallback($"任务已取消: {video.FileName}");
                        return false;
                    }
                    finally
                    {
                         _activeProcess = null; // Clear active process
                    }

                    if (process.ExitCode == 0)
                    {
                        // Rename temp file to final file on success
                        if (File.Exists(tempOutputPath))
                        {
                            if (File.Exists(finalOutputPath)) File.Delete(finalOutputPath);
                            File.Move(tempOutputPath, finalOutputPath);
                        }

                        logCallback($"转码成功: {video.FileName}");
                        return true;
                    }

                    // On failure, clean up temp file
                    if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);

                    logCallback($"FFmpeg 异常退出 代码: {process.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                logCallback($"启动 FFmpeg 失败: {ex.Message}");
                if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);
                return false;
            }
        }

        public async Task LoadMediaInfoAsync(VideoFileInfo video)
        {
            var arguments =
                $"-v error -show_entries format=duration,bit_rate:stream=width,height,avg_frame_rate,codec_name,bit_rate,codec_type -of default=noprint_wrappers=1:nokey=0 \"{video.FilePath}\"";

            var processInfo = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            try
            {
                using (var process = new Process { StartInfo = processInfo })
                {
                    process.Start();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        ParseMediaInfo(video, output);
                        video.MediaInfoLoaded = true;
                    }
                }
            }
            catch (Exception)
            {
                // 忽略错误，可能只是无法解析
            }
        }

        private void ParseMediaInfo(VideoFileInfo video, string output)
        {
            using (var reader = new StringReader(output))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split('=');
                    if (parts.Length != 2) continue;
                    var key = parts[0];
                    var value = parts[1];

                    if (key == "width" && int.TryParse(value, out var w)) video.Width = w;
                    if (key == "height" && int.TryParse(value, out var h)) video.Height = h;
                    if (key == "duration" && double.TryParse(value, out var d))
                        video.Duration = TimeSpan.FromSeconds(d);
                    if (key == "codec_name")
                        if (string.IsNullOrEmpty(video.VideoCodec))
                            video.VideoCodec = value; // Simple assignment, might need refinement
                    if (key == "avg_frame_rate")
                    {
                        var frParts = value.Split('/');
                        if (frParts.Length == 2 && double.TryParse(frParts[0], out var num) &&
                            double.TryParse(frParts[1], out var den) && den > 0)
                            video.FrameRate = num / den;
                        else if (double.TryParse(value, out var fr)) video.FrameRate = fr;
                    }

                    if (key == "bit_rate" && double.TryParse(value, out var br))
                        // FFprobe reports bitrate in bits/s, we want kbps maybe? Model says kbps in comments but double type.
                        // Let's assume bits for now and convert to kbps if needed.
                        // Usually bit_rate from ffprobe is bits/s.
                        if (video.VideoBitrate == 0)
                            video.VideoBitrate = br / 1000.0;
                }
            }
        }
    }

    public static class ProcessExtensions
    {
        public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
        {
            if (process.HasExited) return Task.CompletedTask;

            var tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;

            process.Exited += (sender, args) => tcs.TrySetResult(null);

            if (cancellationToken != default) cancellationToken.Register(() => tcs.TrySetCanceled());

            return process.HasExited ? Task.CompletedTask : tcs.Task;
        }
    }
}
// Other methods remain unchanged

