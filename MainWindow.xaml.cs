using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace MediaMTX_GUI
{
    public partial class MainWindow : Window
    {
        private string? selectedVideoFile;
        private Process? mediaMtxProcess;
        private readonly string mediaMtxPath = "mediamtx.exe";
        private readonly string configPath = "mediamtx.yml";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Video files (*.mp4;*.avi;*.mkv;*.mov)|*.mp4;*.avi;*.mkv;*.mov|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                selectedVideoFile = dialog.FileName;
                SelectedFileText.Text = $"선택된 파일: {Path.GetFileName(selectedVideoFile)}";
                StartButton.IsEnabled = true;
            }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedVideoFile)) return;

            try
            {
                CreateMediaMtxConfig();
                StartMediaMtx();
                
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                StatusText.Text = "스트리밍 중...";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
                
                AppendLogWithLimit("RTSP 스트리밍 시작됨\n");
                AppendLogWithLimit($"RTSP URL: rtsp://localhost:8554/stream\n");
            }
            catch (Exception ex)
            {
                AppendLogWithLimit($"오류: {ex.Message}\n");
                StatusText.Text = "오류 발생";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void StopMediaMtxProcess()
        {
            if (mediaMtxProcess != null && !mediaMtxProcess.HasExited)
            {
                mediaMtxProcess.Kill();
                mediaMtxProcess.Dispose();
                mediaMtxProcess = null;
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StopMediaMtxProcess();

                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                StatusText.Text = "대기 중";
                StatusText.Foreground = System.Windows.Media.Brushes.Gray;

                AppendLogWithLimit("RTSP 스트리밍 중지됨\n");
            }
            catch (Exception ex)
            {
                AppendLogWithLimit($"중지 오류: {ex.Message}\n");
            }
        }

        private void CopyUrl_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(RtspUrlTextBox.Text);
            AppendLogWithLimit("RTSP URL이 클립보드에 복사됨\n");
        }

        private void CreateMediaMtxConfig()
        {
            // Get absolute path for ffmpeg.exe in same directory as application
            var appDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var ffmpegPath = Path.Combine(appDirectory, "ffmpeg.exe").Replace("\\", "/").Replace(" ", "\\ ");
            var videoPath = selectedVideoFile.Replace("\\", "/").Replace(" ", "\\ ");
            
            // Build FFmpeg command with streaming settings
            var ffmpegCmd = $"{ffmpegPath} -re -stream_loop -1 -i {videoPath}";
            
            // Add video filters if not original settings
            var videoFilters = new List<string>();
            
            // Resolution setting
            var resolution = ((System.Windows.Controls.ComboBoxItem)ResolutionComboBox.SelectedItem).Content.ToString();
            if (resolution != "원본")
            {
                videoFilters.Add($"scale={resolution}");
            }
            
            // FPS setting
            var fps = ((System.Windows.Controls.ComboBoxItem)FpsComboBox.SelectedItem).Content.ToString();
            if (fps != "원본")
            {
                videoFilters.Add($"fps={fps}");
            }

            // Apply video filters and encoding if any filters are used
            if (videoFilters.Count > 0)
            {
                //ffmpegCmd += $" -vf \"{string.Join(",", videoFilters)}\" -c:v libx264 -c:a aac";
                ffmpegCmd += $" -vf {string.Join(",", videoFilters)} -c:v libx264 -c:a aac";
            }
            else
            {
                ffmpegCmd += " -c copy";
            }
            
            ffmpegCmd += " -f rtsp rtsp://localhost:8554/stream";
            AppendLogWithLimit($"FFmpeg 명령어: {ffmpegCmd}\n");
            
            var config = $@"logLevel: info
logDestinations: [stdout]

api: yes
apiAddress: 127.0.0.1:9997

rtsp: yes
rtspAddress: :8554

paths:
  stream:
    source: publisher
    runOnInit: {ffmpegCmd}
    runOnInitRestart: yes
";
            File.WriteAllText(configPath, config);
        }

        private void StartMediaMtx()
        {
            mediaMtxProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = mediaMtxPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            mediaMtxProcess.OutputDataReceived += (s, e) => 
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Dispatcher.Invoke(() => {
                        AppendLogWithLimit($"{e.Data}\n");
                    });
            };

            mediaMtxProcess.ErrorDataReceived += (s, e) => 
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Dispatcher.Invoke(() => {
                        AppendLogWithLimit($"ERROR: {e.Data}\n");
                    });
            };

            mediaMtxProcess.Start();
            mediaMtxProcess.BeginOutputReadLine();
            mediaMtxProcess.BeginErrorReadLine();
        }

        private void AppendLogWithLimit(string message)
        {
            LogTextBox.AppendText(message);
            
            // Limit buffer to 8000 lines
            var lines = LogTextBox.Text.Split('\n');
            if (lines.Length > 8000)
            {
                var newText = string.Join("\n", lines.Skip(lines.Length - 8000));
                LogTextBox.Text = newText;
            }
            
            // Auto-scroll to bottom
            LogTextBox.ScrollToEnd();
        }

        protected override void OnClosed(EventArgs e)
        {
            StopMediaMtxProcess();
            base.OnClosed(e);
        }
    }
}
