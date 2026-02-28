﻿using System;
using System.ComponentModel;
using System.IO;

namespace BatchRecoder.Models
{
    public enum VideoStatus
    {
        Pending, // 未处理
        Processed, // 已处理
        Processing, // 处理中
        Failed, // 失败
        Queued // 排队中
    }

    public class VideoFileInfo : INotifyPropertyChanged
    {
        public const string ProcessedSuffix = ".recoded";
        public const string TemporarySuffix = ".recoded.tmp";
        private double _currentBitrate;
        private string _errorMessage;
        private string _eta;
        private double _progress;

        private VideoStatus _status;
        private string _customOutputDirectory;

        public string FilePath { get; set; }
        public string FileName => Path.GetFileName(FilePath);
        public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(FilePath);
        public string Directory => Path.GetDirectoryName(FilePath);
        public long FileSizeBytes { get; set; }
        public string FileSizeDisplay => FormatFileSize(FileSizeBytes);

        // 自定义输出目录（可选，若为空则使用原目录）
        public string CustomOutputDirectory
        {
            get => _customOutputDirectory;
            set
            {
                _customOutputDirectory = value;
                OnPropertyChanged(nameof(CustomOutputDirectory));
                OnPropertyChanged(nameof(ProcessedFilePath));
            }
        }

        // 媒体信息
        public int Width { get; set; }
        public int Height { get; set; }
        public double FrameRate { get; set; }
        public double VideoBitrate { get; set; } // kbps
        public double AudioBitrate { get; set; } // kbps
        public TimeSpan Duration { get; set; }
        public string VideoCodec { get; set; }
        public string AudioCodec { get; set; }
        public string ResolutionDisplay => Width > 0 ? $"{Width}x{Height}" : "-";
        public string FrameRateDisplay => FrameRate > 0 ? $"{FrameRate:F2} fps" : "-";
        public string VideoBitrateDisplay => VideoBitrate > 0 ? $"{VideoBitrate:F0} kbps" : "-";

        public string DurationDisplay => Duration.TotalSeconds > 0
            ? $"{(int)Duration.TotalHours:D2}:{Duration.Minutes:D2}:{Duration.Seconds:D2}"
            : "-";

        public bool MediaInfoLoaded { get; set; }

        // 对应的已处理文件路径
        public string ProcessedFilePath => GetProcessedFilePath(FilePath, CustomOutputDirectory);

        public VideoStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusDisplay));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        public double Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

        public string Eta
        {
            get => _eta;
            set
            {
                _eta = value;
                OnPropertyChanged(nameof(Eta));
            }
        }

        public double CurrentBitrate
        {
            get => _currentBitrate;
            set
            {
                _currentBitrate = value;
                OnPropertyChanged(nameof(CurrentBitrate));
                OnPropertyChanged(nameof(CurrentBitrateDisplay));
            }
        }

        public string CurrentBitrateDisplay => CurrentBitrate > 0 ? $"{CurrentBitrate:F0} kbps" : "";

        public string StatusDisplay
        {
            get
            {
                switch (Status)
                {
                    case VideoStatus.Pending: return "待处理";
                    case VideoStatus.Processed: return "已处理";
                    case VideoStatus.Processing: return "处理中";
                    case VideoStatus.Failed: return "失败";
                    case VideoStatus.Queued: return "队列中";
                    default: return "";
                }
            }
        }

        public string StatusColor
        {
            get
            {
                switch (Status)
                {
                    case VideoStatus.Pending: return "#FFA500";
                    case VideoStatus.Processed: return "#4CAF50";
                    case VideoStatus.Processing: return "#2196F3";
                    case VideoStatus.Failed: return "#F44336";
                    case VideoStatus.Queued: return "#9C27B0";
                    default: return "#888888";
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static string GetProcessedFilePath(string inputPath, string customOutputDirectory = null)
        {
            var dir = string.IsNullOrWhiteSpace(customOutputDirectory) 
                ? Path.GetDirectoryName(inputPath) 
                : customOutputDirectory;
            var name = Path.GetFileNameWithoutExtension(inputPath);
            var ext = Path.GetExtension(inputPath);
            return Path.Combine(dir, $"{name}{ProcessedSuffix}{ext}");
        }

        public static string GetTemporaryFilePath(string inputPath, string customOutputDirectory = null)
        {
            var dir = string.IsNullOrWhiteSpace(customOutputDirectory) 
                ? Path.GetDirectoryName(inputPath) 
                : customOutputDirectory;
            var name = Path.GetFileNameWithoutExtension(inputPath);
            var ext = Path.GetExtension(inputPath);
            return Path.Combine(dir, $"{name}{TemporarySuffix}{ext}");
        }

        public static bool IsProcessedFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            // 1. Exclude .recoded.tmp (Temporary) anywhere in name?
            // User convention: filename.recoded.tmp.mp4
            var name = Path.GetFileNameWithoutExtension(path); 
            // If path is video.recoded.tmp.mp4, name is video.recoded.tmp
            // If path is video.recoded.mp4, name is video.recoded
            
            // Check for temporary marker
            if (name.EndsWith(TemporarySuffix, StringComparison.OrdinalIgnoreCase) || 
                path.Contains(TemporarySuffix)) // safer check
                return false;

            // 2. Check for Processed Suffix
            // e.g. video.recoded.mp4 (name=video.recoded)
            // or video.recoded (name=video) if extension replaced? No, new rule preserves extension.
            
            return name.EndsWith(ProcessedSuffix, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes <= 0) return "-";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}