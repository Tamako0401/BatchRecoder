using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BatchRecoder.Core;
using BatchRecoder.Models;

namespace BatchRecoder.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly FFmpegRunner _ffmpegRunner;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isProcessing;
        private bool _isScanning;
        private string _logText = "";
        private string _targetDirectory;
        private bool _isPaused;
        private bool _useCustomOutputDirectory;
        private string _customOutputDirectory;

        public MainViewModel()
        {
            _ffmpegRunner = new FFmpegRunner();
            ScanCommand = new RelayCommand(async _ => await ScanDirectoryAsync(),
                _ => !IsScanning && !string.IsNullOrWhiteSpace(TargetDirectory) && Directory.Exists(TargetDirectory));
            StartCommand = new RelayCommand(async _ => await StartProcessingAsync(),
                _ => !IsProcessing &&
                     VideoFiles.Any(v => v.Status == VideoStatus.Pending || v.Status == VideoStatus.Failed));
            StopCommand = new RelayCommand(_ => StopProcessing(), _ => IsProcessing);
            RetryCommand = new RelayCommand(async param => await RetryVideoAsync(param as VideoFileInfo),
                param => param is VideoFileInfo v && v.Status == VideoStatus.Failed && !IsProcessing);
            PauseCommand = new RelayCommand(_ => PauseProcessing(), _ => IsProcessing && !IsPaused);
            ResumeCommand = new RelayCommand(_ => ResumeProcessing(), _ => IsProcessing && IsPaused);
            BrowseOutputDirectoryCommand = new RelayCommand(_ => BrowseOutputDirectory(),
                _ => UseCustomOutputDirectory);
            BrowseTargetDirectoryCommand = new RelayCommand(_ => BrowseTargetDirectory());

            // 启动时检查并显示 FFmpeg 版本
            Task.Run(async () =>
            {
                var version = await _ffmpegRunner.GetFFmpegVersionAsync();
                AppendLog($"当前 FFmpeg 版本：{version}");
            });
        }        public void TryLoadSavedConfig()
        {
            if (!BatchRecoder.Models.ConfigManager.HasSavedConfig()) return;
            var result = MessageBox.Show(
                "检测到上次保存的配置，是否加载？",
                "加载配置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                LoadConfig();
                AppendLog("已加载上次的配置");
            }
        }
        private void LoadConfig()
        {
                var config = BatchRecoder.Models.ConfigManager.Load();
            BatchRecoder.Models.ConfigManager.ApplyToSettings(config, Settings);
            TargetDirectory = config.TargetDirectory;
            UseCustomOutputDirectory = config.UseCustomOutputDirectory;
            CustomOutputDirectory = config.CustomOutputDirectory;
        }
        public void SaveConfig()
        {
            var config = BatchRecoder.Models.ConfigManager.CreateFromSettings(
                Settings, 
                TargetDirectory, 
                UseCustomOutputDirectory, 
                CustomOutputDirectory);
            BatchRecoder.Models.ConfigManager.Save(config);
        }

        public ObservableCollection<VideoFileInfo> VideoFiles { get; } = new ObservableCollection<VideoFileInfo>();
        public EncoderSettings Settings { get; } = new EncoderSettings();

        public bool UseCustomOutputDirectory
        {
            get => _useCustomOutputDirectory;
            set
            {
                _useCustomOutputDirectory = value;
                OnPropertyChanged(nameof(UseCustomOutputDirectory));
            }
        }

        public string CustomOutputDirectory
        {
            get => _customOutputDirectory;
            set
            {
                _customOutputDirectory = value;
                OnPropertyChanged(nameof(CustomOutputDirectory));
            }
        }
        
        public System.Collections.Generic.List<string> VideoEncoders => EncoderSettings.VideoEncoders;
        public System.Collections.Generic.List<string> Profiles => EncoderSettings.Profiles;
        public System.Collections.Generic.List<string> Tunes => EncoderSettings.Tunes;
        public System.Collections.Generic.List<string> Presets => EncoderSettings.Presets;
        public System.Collections.Generic.List<string> AudioEncoders => EncoderSettings.AudioEncoders;
        public System.Collections.Generic.List<string> TargetResolutions => EncoderSettings.TargetResolutions;
        public System.Collections.Generic.List<int> AudioBitrates => EncoderSettings.AudioBitrates;
        public System.Collections.Generic.List<string> OutputFormats => EncoderSettings.OutputFormats;
        public System.Collections.Generic.List<string> TargetFrameRates => EncoderSettings.TargetFrameRates;

        public string TargetDirectory
        {
            get => _targetDirectory;
            set
            {
                _targetDirectory = value;
                OnPropertyChanged(nameof(TargetDirectory));
            }
        }

        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                _isScanning = value;
                OnPropertyChanged(nameof(IsScanning));
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                _isProcessing = value;
                OnPropertyChanged(nameof(IsProcessing));
            }
        }

        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                _isPaused = value;
                OnPropertyChanged(nameof(IsPaused));
            }
        }

        public string LogText
        {
            get => _logText;
            set
            {
                _logText = value;
                OnPropertyChanged(nameof(LogText));
            }
        }

        public ICommand ScanCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand RetryCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand ResumeCommand { get; }
        public ICommand BrowseOutputDirectoryCommand { get; }
        public ICommand BrowseTargetDirectoryCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void AppendLog(string message)
        {
            if (Application.Current == null) return;

            Action updateLog = () =>
            {
                LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
                if (LogText.Length > 10000) LogText = LogText.Substring(LogText.Length - 5000);
            };

            if (Application.Current.Dispatcher.CheckAccess())
                updateLog();
            else
                Application.Current.Dispatcher.Invoke(updateLog);
        }

        public async Task ScanDirectoryAsync()
        {
            if (string.IsNullOrWhiteSpace(TargetDirectory) || !Directory.Exists(TargetDirectory))
            {
                AppendLog("无效的目录");
                return;
            }

            IsScanning = true;
            AppendLog($"开始扫描目录: {TargetDirectory}");
            VideoFiles.Clear();

            try
            {
                // 1. Clean up .recoded.tmp files (reset pending status effectively by deleting partials)
                var tmpFiles = Directory.GetFiles(TargetDirectory, "*.recoded.tmp", SearchOption.AllDirectories);
                foreach(var tmp in tmpFiles) 
                {
                    try { File.Delete(tmp); } catch { /* Ignore delete errors */ }
                }

                var files = Directory.GetFiles(TargetDirectory, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".flv", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".mov", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".wmv", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".m4v", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".recoded", StringComparison.OrdinalIgnoreCase)) // Add .recoded files
                    .ToList();

                foreach (var file in files)
                {

                    
                    var fileInfo = new FileInfo(file);
                    var video = new VideoFileInfo
                    {
                        FilePath = file,
                        FileSizeBytes = fileInfo.Length,
                        Status = VideoStatus.Pending
                    };
                    
                    // 检查已处理文件是否存在

                    // 对于普通文件（video.mp4），已处理的文件是 video.recoded。

                    // 对于已处理文件（video.recoded），已处理的文件是 video.recoded。
                    if (File.Exists(video.ProcessedFilePath))
                    {
                        // 如果已处理的文件存在，则标记为已处理（跳过）。
                        video.Status = VideoStatus.Processed;
                        video.Progress = 100;
                    }
                    else if (VideoFileInfo.IsProcessedFile(file))
                    {
                         video.Status = VideoStatus.Processed;
                         video.Progress = 100;
                    }
                    else
                    {
                        // Default to pending.
                        // If .recoded.tmp existed (and was deleted or ignored), we restart here.
                        video.Status = VideoStatus.Pending;
                        video.Progress = 0;
                    }

                    VideoFiles.Add(video);
                }

                AppendLog($"扫描完成，找到 {VideoFiles.Count} 个视频文件");

                _ = Task.Run(async () =>
                {
                    foreach (var video in VideoFiles.ToList())
                        if (!video.MediaInfoLoaded)
                            await _ffmpegRunner.LoadMediaInfoAsync(video);
                });
            }
            catch (Exception ex)
            {
                AppendLog($"扫描出错: {ex.Message}");
            }
            finally
            {
                IsScanning = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public async Task StartProcessingAsync()
        {
            var pendingFiles = VideoFiles.Where(v => v.Status == VideoStatus.Pending || v.Status == VideoStatus.Failed)
                .ToList();
            if (!pendingFiles.Any())
            {
                AppendLog("没有需要处理的文件");
                return;
            }

            IsProcessing = true;
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // 检查 FFmpeg
            if (!await _ffmpegRunner.EnsureFFmpegExistsAsync(AppendLog, token))
            {
                IsProcessing = false;
                AppendLog("FFmpeg 缺失且下载失败，无法继续。");
                return;
            }

            AppendLog($"开始处理队列，共 {pendingFiles.Count} 个文件");

            // Mark all pending as queued visually
            foreach (var video in pendingFiles) video.Status = VideoStatus.Queued;

            try
            {
                foreach (var video in pendingFiles)
                {
                    if (token.IsCancellationRequested) 
                    {
                        video.Status = VideoStatus.Pending; // Revert to pending if cancelled
                        continue;
                    }

                    video.Status = VideoStatus.Processing;
                    video.Progress = 0;
                    video.ErrorMessage = null;
                    video.Eta = null;

                    var success = await _ffmpegRunner.EncodeAsync(video, Settings, AppendLog, token, UseCustomOutputDirectory ? CustomOutputDirectory : null);

                    if (success)
                    {
                        video.Status = VideoStatus.Processed;
                        video.Progress = 100;
                        video.Eta = null;
                        video.CurrentBitrate = 0;
                    }
                    else if (token.IsCancellationRequested)
                    {
                         // 任务被终止（取消），重置为 Pending
                         video.Status = VideoStatus.Pending;
                         video.Progress = 0;
                         video.Eta = null;
                         video.CurrentBitrate = 0;
                    }
                    else
                    {
                        video.Status = VideoStatus.Failed;
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"处理队列时发生异常: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                // Double check to reset any remaining Queued items if stopped abruptly
                foreach (var v in VideoFiles.Where(x => x.Status == VideoStatus.Queued))
                {
                    v.Status = VideoStatus.Pending;
                }

                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                AppendLog("队列处理结束");
                
                SaveConfig();
                
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void PauseProcessing()
        {
            if (IsProcessing && !IsPaused)
            {
                _ffmpegRunner.Pause();
                IsPaused = true;
                AppendLog("任务已暂停");
            }
        }

        private void ResumeProcessing()
        {
            if (IsProcessing && IsPaused)
            {
                _ffmpegRunner.Resume();
                IsPaused = false;
                AppendLog("任务继续");
            }
        }

        public void StopProcessing()
        {
            if (IsProcessing && _cancellationTokenSource != null)
            {
                // Ensure resumed before cancelling if paused
                if (IsPaused) ResumeProcessing();

                AppendLog("正在终止任务...");
                _cancellationTokenSource.Cancel();
                
                // Set status immediately for feedback
                foreach (var video in VideoFiles.Where(v => v.Status == VideoStatus.Queued))
                {
                    video.Status = VideoStatus.Pending;
                }
            }
        }

        public async Task RetryVideoAsync(VideoFileInfo video)
        {
            if (video == null || IsProcessing) return;

            IsProcessing = true;
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            try
            {
                video.Status = VideoStatus.Processing;
                video.Progress = 0;
                video.ErrorMessage = null;
                video.Eta = null;

                var success = await _ffmpegRunner.EncodeAsync(video, Settings, AppendLog, token, UseCustomOutputDirectory ? CustomOutputDirectory : null);

                if (success)
                {
                    video.Status = VideoStatus.Processed;
                    video.Progress = 100;
                    video.Eta = null;
                    video.CurrentBitrate = 0;
                }
                else if (!token.IsCancellationRequested)
                {
                    video.Status = VideoStatus.Failed;
                }
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void BrowseOutputDirectory()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择输出目录",
                SelectedPath = !string.IsNullOrWhiteSpace(CustomOutputDirectory) && Directory.Exists(CustomOutputDirectory) 
                    ? CustomOutputDirectory 
                    : TargetDirectory
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                CustomOutputDirectory = dialog.SelectedPath;
            }
        }

        private void BrowseTargetDirectory()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择目标目录",
                SelectedPath = !string.IsNullOrWhiteSpace(TargetDirectory) && Directory.Exists(TargetDirectory) 
                    ? TargetDirectory 
                    : Environment.CurrentDirectory
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TargetDirectory = dialog.SelectedPath;
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Func<object, bool> _canExecute;
        private readonly Action<object> _execute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}

