using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace MediaMTX_GUI
{
    public partial class MainWindow : Window
    {
        private List<string> selectedVideoFiles = new List<string>();
        private Process? mediaMtxProcess;
        private readonly string mediaMtxPath = "mediamtx.exe";
        private readonly string configPath = "mediamtx.yml";
        private readonly string settingsPath = "settings.txt";

        public MainWindow()
        {
            InitializeComponent();
            _flushTimer = new Timer(FlushLogs, null, 0, 200); // 0.2초마다 UI 반영
            KillExistingMediaMtxProcesses();
            LoadSettings();
        }

        private void KillExistingMediaMtxProcesses()
        {
            try
            {
                var processes = Process.GetProcessesByName("mediamtx");
                foreach (var process in processes)
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                AppendLogWithLimit($"기존 MediaMTX 프로세스 정리 중 오류: {ex.Message}\n");
            }
        }

        private void SelectFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Video files (*.mp4;*.avi;*.mkv;*.mov)|*.mp4;*.avi;*.mkv;*.mov|All files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!selectedVideoFiles.Contains(file))
                    {
                        selectedVideoFiles.Add(file);
                    }
                }
                UpdateFileList();
                StartButton.IsEnabled = selectedVideoFiles.Count > 0;
                SaveSettings();
            }
        }

        private void ClearFiles_Click(object sender, RoutedEventArgs e)
        {
            selectedVideoFiles.Clear();
            UpdateFileList();
            StartButton.IsEnabled = false;
            RtspUrlListBox.Items.Clear();
            SaveSettings();
        }

        private void UpdateFileList()
        {
            FileListBox.Items.Clear();
            for (int i = 0; i < selectedVideoFiles.Count; i++)
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                
                var textBlock = new TextBlock 
                { 
                    Text = $"Stream {i + 1}: {Path.GetFileName(selectedVideoFiles[i])}", 
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                
                var deleteButton = new Button 
                { 
                    Content = "❌", 
                    Width = 25, 
                    Height = 25,
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = i
                };
                deleteButton.Click += DeleteFile_Click;
                
                panel.Children.Add(textBlock);
                panel.Children.Add(deleteButton);
                FileListBox.Items.Add(panel);
            }
        }

        private void DeleteFile_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var index = (int)button.Tag;
            
            if (index < selectedVideoFiles.Count)
            {
                selectedVideoFiles.RemoveAt(index);
                UpdateFileList();
                StartButton.IsEnabled = selectedVideoFiles.Count > 0;
                RtspUrlListBox.Items.Clear();
                SaveSettings();
            }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (selectedVideoFiles.Count == 0) return;

            try
            {
                CreateMediaMtxConfig();
                StartMediaMtx();
                
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                StatusText.Text = $"{selectedVideoFiles.Count}개 스트림 실행 중...";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
                
                UpdateRtspUrlList();
                AppendLogWithLimit($"{selectedVideoFiles.Count}개 RTSP 스트림 시작됨\n");
            }
            catch (Exception ex)
            {
                AppendLogWithLimit($"오류: {ex.Message}\n");
                StatusText.Text = "오류 발생";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void UpdateRtspUrlList()
        {
            RtspUrlListBox.Items.Clear();
            for (int i = 0; i < selectedVideoFiles.Count; i++)
            {
                var url = $"rtsp://localhost:8554/stream{i + 1}";
                var button = new Button
                {
                    Content = $"📡 {url}",
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 12,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = url
                };
                button.Click += (s, e) => Clipboard.SetText(((Button)s).Tag.ToString());
                RtspUrlListBox.Items.Add(button);
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

        private void CreateMediaMtxConfig()
        {
            var appDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var ffmpegPath = Path.Combine(appDirectory, "ffmpeg.exe").Replace("\\", "/").Replace(" ", "\\ ");
            
            var config = $@"logLevel: info
logDestinations: [stdout]

api: yes
apiAddress: 127.0.0.1:9997

rtsp: yes
rtspAddress: :8554

paths:
";

            for (int i = 0; i < selectedVideoFiles.Count; i++)
            {
                var videoPath = selectedVideoFiles[i].Replace("\\", "/").Replace(" ", "\\ ");
                var streamName = $"stream{i + 1}";
                
                var ffmpegCmd = $"{ffmpegPath} -re -stream_loop -1 -i {videoPath}";
                
                var videoFilters = new List<string>();
                
                var resolution = ((ComboBoxItem)ResolutionComboBox.SelectedItem).Content.ToString();
                if (resolution != "원본")
                {
                    videoFilters.Add($"scale={resolution}");
                }
                
                var fps = ((ComboBoxItem)FpsComboBox.SelectedItem).Content.ToString();
                if (fps != "원본")
                {
                    videoFilters.Add($"fps={fps}");
                }

                if (videoFilters.Count > 0)
                {
                    ffmpegCmd += $" -vf {string.Join(",", videoFilters)} -c:v libx264 -c:a aac";
                }
                else
                {
                    ffmpegCmd += " -c copy";
                }
                
                ffmpegCmd += $" -f rtsp rtsp://localhost:8554/{streamName}";
                
                config += $@"  {streamName}:
    source: publisher
    runOnInit: {ffmpegCmd}
    runOnInitRestart: yes
";
                AppendLogWithLimit($"Stream {i + 1} FFmpeg 명령어: {ffmpegCmd}\n");
            }
            
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

        private const int MaxLines = 500;
        private readonly ConcurrentQueue<string> _logQueue = new();
        private readonly Timer _flushTimer;

        private void AppendLogWithLimit(string message)
        {
            _logQueue.Enqueue(message);
        }

        private void FlushLogs(object? _)
        {
            if (_logQueue.IsEmpty)
                return;

            var sb = new StringBuilder();
            while (_logQueue.TryDequeue(out var line))
                sb.AppendLine(line.TrimEnd());

            Dispatcher.BeginInvoke(() =>
            {
                LogTextBox.AppendText(sb.ToString());
                LogScrollViewer.ScrollToBottom();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveSettings();
            StopMediaMtxProcess();
            base.OnClosed(e);
        }

        private void LoadSettings()
        {
            if (File.Exists(settingsPath))
            {
                var lines = File.ReadAllLines(settingsPath);
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line) && File.Exists(line))
                    {
                        selectedVideoFiles.Add(line);
                    }
                }
                UpdateFileList();
                StartButton.IsEnabled = selectedVideoFiles.Count > 0;
            }
        }

        private void SaveSettings()
        {
            File.WriteAllLines(settingsPath, selectedVideoFiles);
        }
    }
}
