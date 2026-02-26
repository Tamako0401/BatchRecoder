using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace BatchRecoder.Models
{
    public class EncoderSettings : INotifyPropertyChanged
    {
        private int _audioBitrate = 128;
        private string _audioEncoder = "aac";
        private bool _copyAudio;
        private int _crf = 23;
        private string _extraArgs = "";
        private string _outputFormat = "mp4";
        private string _preset = "medium";
        private string _videoEncoder = "H.264 (Software - x264)";
        private string _targetResolution = "Original";
        private int _customWidth = 1920;
        private int _customHeight = 1080;
        private string _profile = "high";
        private string _tune = "none";
        private string _targetFrameRate = "Original";

        public string VideoEncoder
        {
            get => _videoEncoder;
            set
            {
                _videoEncoder = value;
                OnPropertyChanged(nameof(VideoEncoder));
            }
        }
        
        public string Profile
        {
            get => _profile;
            set
            {
                _profile = value;
                OnPropertyChanged(nameof(Profile));
            }
        }

        public string Tune
        {
            get => _tune;
            set
            {
                _tune = value;
                OnPropertyChanged(nameof(Tune));
            }
        }

        public int Crf
        {
            get => _crf;
            set
            {
                _crf = Math.Max(0, Math.Min(51, value));
                OnPropertyChanged(nameof(Crf));
            }
        }

        public string Preset
        {
            get => _preset;
            set
            {
                _preset = value;
                OnPropertyChanged(nameof(Preset));
            }
        }

        public string AudioEncoder
        {
            get => _audioEncoder;
            set
            {
                _audioEncoder = value;
                OnPropertyChanged(nameof(AudioEncoder));
            }
        }

        public int AudioBitrate
        {
            get => _audioBitrate;
            set
            {
                _audioBitrate = value;
                OnPropertyChanged(nameof(AudioBitrate));
            }
        }

        public string OutputFormat
        {
            get => _outputFormat;
            set
            {
                _outputFormat = value;
                OnPropertyChanged(nameof(OutputFormat));
            }
        }

        public bool CopyAudio
        {
            get => _copyAudio;
            set
            {
                _copyAudio = value;
                OnPropertyChanged(nameof(CopyAudio));
            }
        }

        public string ExtraArgs
        {
            get => _extraArgs;
            set
            {
                _extraArgs = value;
                OnPropertyChanged(nameof(ExtraArgs));
            }
        }

        public string TargetResolution
        {
            get => _targetResolution;
            set
            {
                _targetResolution = value;
                OnPropertyChanged(nameof(TargetResolution));
                OnPropertyChanged(nameof(IsCustomResolution));
            }
        }

        public int CustomWidth
        {
            get => _customWidth;
            set
            {
                _customWidth = value;
                OnPropertyChanged(nameof(CustomWidth));
            }
        }

        public int CustomHeight
        {
            get => _customHeight;
            set
            {
                _customHeight = value;
                OnPropertyChanged(nameof(CustomHeight));
            }
        }

        public string TargetFrameRate
        {
            get => _targetFrameRate;
            set
            {
                _targetFrameRate = value;
                OnPropertyChanged(nameof(TargetFrameRate));
            }
        }

        public bool IsCustomResolution => TargetResolution == "Custom";

        // 可用选项
        public static List<string> VideoEncoders { get; } = new List<string> 
        { 
            "H.264 (Software - x264)", 
            "H.264 (NVIDIA - NVENC)", 
            "H.264 (Intel - QSV)", 
            "H.264 (AMD - AMF)",
            "H.265 (Software - x265)", 
            "H.265 (NVIDIA - NVENC)", 
            "H.265 (Intel - QSV)", 
            "H.265 (AMD - AMF)" 
        };

        private static readonly Dictionary<string, string> VideoEncoderMap = new Dictionary<string, string>
        {
            { "H.264 (Software - x264)", "libx264" },
            { "H.264 (NVIDIA - NVENC)", "h264_nvenc" },
            { "H.264 (Intel - QSV)", "h264_qsv" },
            { "H.264 (AMD - AMF)", "h264_amf" },
            { "H.265 (Software - x265)", "libx265" },
            { "H.265 (NVIDIA - NVENC)", "hevc_nvenc" },
            { "H.265 (Intel - QSV)", "hevc_qsv" },
            { "H.265 (AMD - AMF)", "hevc_amf" }
        };

        public static List<string> Profiles { get; } = new List<string> { "baseline", "main", "high", "high10", "high422", "high444" };
        public static List<string> Tunes { get; } = new List<string> { "none", "film", "animation", "grain", "stillimage", "psnr", "ssim", "fastdecode", "zerolatency" };
        public static List<string> Presets { get; } = new List<string> { "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow" };
        public static List<string> AudioEncoders { get; } = new List<string> { "aac", "mp3", "opus" };
        public static List<string> TargetResolutions { get; } = new List<string>
        {
            "Original",
            "720p (1280x720)", 
            "1080p (1920x1080)", 
            "2k (2560x1440)", 
            "4k (3840x2160)",
            "16:9 (Scale Height)", 
            "16:10 (Scale Height)",
            "Custom"
        };
        public static List<int> AudioBitrates { get; } = new List<int> { 64, 96, 128, 192, 256, 320 };
        public static List<string> OutputFormats { get; } = new List<string> { "mp4", "mkv", "avi", "mov" };
        public static List<string> TargetFrameRates { get; } = new List<string>
        {
            "Original",
            "23.976",
            "24",
            "29.97",
            "30",
            "50",
            "59.94",
            "60",
            "120",
            "Custom"
        };

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        ///     生成 ffmpeg 参数（不含输入输出）
        /// </summary>
        public string BuildArguments(string inputPath, string outputPath)
        {
            var sb = new StringBuilder();

            sb.Append($"-i \"{inputPath}\" ");
            
            // 获取实际编码器名称
            var encoderName = VideoEncoderMap.ContainsKey(VideoEncoder) ? VideoEncoderMap[VideoEncoder] : "libx264";

            // 视频编码
            sb.Append($"-c:v {encoderName} ");
            
            // CRF (注意：某些硬件编码器不支持 -crf，可能需要 -qp 或 -b:v)
            if (encoderName.Contains("libx"))
            {
                 sb.Append($"-crf {Crf} ");
                 if (Profile != "high") sb.Append($"-profile:v {Profile} ");
                 if (Tune != "none") sb.Append($"-tune {Tune} ");
                 
                 // Apply preset for software encoders
                 if (Preset != "medium") sb.Append($"-preset {Preset} ");
            }
            else if (encoderName.Contains("nvenc"))
            {
                 // NVENC: use -cq for VBR quality, -rc vbr (optional but good for cq)
                 // -cq ranges can be different, but 0-51 is a safe assumption for mapping.
                 // NOTE: -cq only works if -rc is set to vbr or similar on some versions, but 
                 // -cq alone corresponds to -rc vbr_hq or vbr usually. 
                 // Let's use -cq and ensure no conflicting params.
                 
                 // Fix for Error -22: Some ffmpeg versions/drivers require -rc constqp or vbr for -cq to work,
                 // or just use -qp. But h264_nvenc supports -cq.
                 // However, "slow" preset in nvenc might be named differently?
                 // nvenc presets: slow, medium, fast => p1 to p7 in newer.
                 // But "slow" is usually mapped.
                 
                 sb.Append($"-cq {Crf} ");
                 if (Preset != "medium") sb.Append($"-preset {Preset} "); // Only add if not default to minimize issues
                 
                 if (Profile != "high") sb.Append($"-profile:v {Profile} ");
            }
            else if (encoderName.Contains("qsv"))
            {
                 // QSV often uses -global_quality for ICQ
                 sb.Append($"-global_quality {Crf} ");
                  if (Profile != "high") sb.Append($"-profile:v {Profile} ");
            }
            else if (encoderName.Contains("amf"))
            {
                 // AMF
                 sb.Append($"-rc cqp -qp_i {Crf} -qp_p {Crf} -quality quality "); 
                 if (Profile != "high") sb.Append($"-profile:v {Profile} ");
            }
            else 
            {
                 sb.Append($"-crf {Crf} ");
                 sb.Append($"-preset {Preset} ");
            }


            // 分辨率 + 帧率：统一形成一个滤镜链，避免出现多个 -vf/-filter:v
            var filterParts = new List<string>();

            if (TargetResolution != "Original")
            {
                if (TargetResolution == "Custom")
                {
                    filterParts.Add($"scale={CustomWidth}:{CustomHeight}");
                }
                else if (TargetResolution.StartsWith("720p"))
                {
                    filterParts.Add("scale=-2:720");
                }
                else if (TargetResolution.StartsWith("1080p"))
                {
                    filterParts.Add("scale=-2:1080");
                }
                else if (TargetResolution.StartsWith("2k"))
                {
                    filterParts.Add("scale=-2:1440");
                }
                else if (TargetResolution.StartsWith("4k"))
                {
                    filterParts.Add("scale=-2:2160");
                }
                else if (TargetResolution.StartsWith("16:9"))
                {
                    filterParts.Add("scale=iw:trunc(iw/16*9/2)*2");
                }
                else if (TargetResolution.StartsWith("16:10"))
                {
                    filterParts.Add("scale=iw:trunc(iw/16*10/2)*2");
                }
                else
                {
                    var resString = TargetResolution.Split(' ')[0];
                    if (resString.Contains("x"))
                    {
                        var parts = resString.Split('x');
                        if (parts.Length == 2)
                        {
                            filterParts.Add($"scale={parts[0]}:{parts[1]}");
                        }
                    }
                }
            }

            // 帧率处理：使用滤镜 fps（更明确的重采样语义），与 scale 串联
            // 注意：只对非 Original 生效；"Custom" 由用户直接输入框输入。
            var fr = (TargetFrameRate ?? "Original").Trim();
            if (!string.Equals(fr, "Original", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(fr, "Custom", StringComparison.OrdinalIgnoreCase))
                {
                    // UI 选择 Custom 但未填值时，不添加
                }
                else
                {
                    // 允许用户输入 23.976 / 30000/1001 等；这里不做苛刻校验，交给 ffmpeg。
                    filterParts.Add($"fps={fr}");
                }
            }

            if (filterParts.Count > 0)
            {
                sb.Append($"-filter:v \"{string.Join(",", filterParts)}\" ");
            }

            // 音频编码
            if (CopyAudio || AudioEncoder == "copy")
            {
                sb.Append("-c:a copy ");
            }
            else
            {
                sb.Append($"-c:a {AudioEncoder} ");
                sb.Append($"-b:a {AudioBitrate}k ");
            }

            // 额外参数
            if (!string.IsNullOrWhiteSpace(ExtraArgs))
                sb.Append($"{ExtraArgs.Trim()} ");

            // 覆盖 + 输出
            sb.Append($"-y \"{outputPath}\"");

            return sb.ToString();
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

