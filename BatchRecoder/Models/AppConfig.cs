using System;
using System.IO;
using Newtonsoft.Json;

namespace BatchRecoder.Models
{
    public class AppConfig
    {
        public string TargetDirectory { get; set; } = "";
        public bool UseCustomOutputDirectory { get; set; }
        public string CustomOutputDirectory { get; set; } = "";
        
        public string VideoEncoder { get; set; } = "H.264 (Software - x264)";
        public string Profile { get; set; } = "high";
        public string Tune { get; set; } = "none";
        public int Crf { get; set; } = 23;
        public string Preset { get; set; } = "medium";
        public string AudioEncoder { get; set; } = "aac";
        public int AudioBitrate { get; set; } = 128;
        public string OutputFormat { get; set; } = "mp4";
        public bool CopyAudio { get; set; }
        public string ExtraArgs { get; set; } = "";
        public string TargetResolution { get; set; } = "Original";
        public int CustomWidth { get; set; } = 1920;
        public int CustomHeight { get; set; } = 1080;
        public string TargetFrameRate { get; set; } = "Original";
    }

    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BatchRecoder",
            "config.json"
        );

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch { }
            return new AppConfig();
        }

        public static void Save(AppConfig config)
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }

        public static bool HasSavedConfig()
        {
            return File.Exists(ConfigPath);
        }

        public static void ApplyToSettings(AppConfig config, EncoderSettings settings)
        {
            settings.VideoEncoder = config.VideoEncoder;
            settings.Profile = config.Profile;
            settings.Tune = config.Tune;
            settings.Crf = config.Crf;
            settings.Preset = config.Preset;
            settings.AudioEncoder = config.AudioEncoder;
            settings.AudioBitrate = config.AudioBitrate;
            settings.OutputFormat = config.OutputFormat;
            settings.CopyAudio = config.CopyAudio;
            settings.ExtraArgs = config.ExtraArgs;
            settings.TargetResolution = config.TargetResolution;
            settings.CustomWidth = config.CustomWidth;
            settings.CustomHeight = config.CustomHeight;
            settings.TargetFrameRate = config.TargetFrameRate;
        }

        public static AppConfig CreateFromSettings(EncoderSettings settings, string targetDir, 
            bool useCustomOutputDir, string customOutputDir)
        {
            return new AppConfig
            {
                TargetDirectory = targetDir,
                UseCustomOutputDirectory = useCustomOutputDir,
                CustomOutputDirectory = customOutputDir,
                VideoEncoder = settings.VideoEncoder,
                Profile = settings.Profile,
                Tune = settings.Tune,
                Crf = settings.Crf,
                Preset = settings.Preset,
                AudioEncoder = settings.AudioEncoder,
                AudioBitrate = settings.AudioBitrate,
                OutputFormat = settings.OutputFormat,
                CopyAudio = settings.CopyAudio,
                ExtraArgs = settings.ExtraArgs,
                TargetResolution = settings.TargetResolution,
                CustomWidth = settings.CustomWidth,
                CustomHeight = settings.CustomHeight,
                TargetFrameRate = settings.TargetFrameRate
            };
        }
    }
}
