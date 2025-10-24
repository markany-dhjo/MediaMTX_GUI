using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MediaMTX_GUI
{
    public partial class MainWindow : Window
    {
        private List<string> selectedVideoFiles = new List<string>();
        private Process? mediaMtxProcess;
        private Dictionary<int, Process> streamProcesses = new Dictionary<int, Process>();
        private Dictionary<int, bool> streamStates = new Dictionary<int, bool>();
        private readonly string mediaMtxPath = "mediamtx.exe";
        private readonly string configPath = "mediamtx.yml";
        private readonly string settingsPath = "settings.txt";
        private readonly string logPath = "log.txt";

        public MainWindow()
        {
            InitializeComponent();
            _flushTimer = new Timer(FlushLogs, null, 0, 500); // 0.5초마다 UI 반영 (부하 감소)
            InitializeLogFile();
            KillExistingMediaMtxProcesses();
            LoadSettings();
        }

        private void InitializeLogFile()
        {
            try
            {
                File.WriteAllText(logPath, $"MediaMTX GUI 로그 시작 - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
            }
            catch (Exception ex)
            {
                // 로그 파일 생성 실패시 무시
            }
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
                StartAllButton.IsEnabled = selectedVideoFiles.Count > 0;
                SaveSettings();
            }
        }

        private void ClearFiles_Click(object sender, RoutedEventArgs e)
        {
            foreach (var kvp in streamProcesses.ToList())
            {
                StopIndividualStream(kvp.Key);
            }
            
            selectedVideoFiles.Clear();
            streamStates.Clear();
            streamProcesses.Clear();
            UpdateFileList();
            StartAllButton.IsEnabled = false;
            StopAllButton.IsEnabled = false;
            RtspUrlListBox.Items.Clear();
            SaveSettings();
        }

        private void UpdateFileList()
        {
            FileListBox.Items.Clear();
            for (int i = 0; i < selectedVideoFiles.Count; i++)
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
                
                var textBlock = new TextBlock 
                { 
                    Text = $"Stream {i + 1}: {Path.GetFileName(selectedVideoFiles[i])}", 
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 15, 0),
                    Width = 400
                };
                
                var startButton = new Button 
                { 
                    Content = "▶", 
                    Width = 35, 
                    Height = 28,
                    Background = System.Windows.Media.Brushes.Green,
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = i,
                    Margin = new Thickness(0, 0, 5, 0),
                    IsEnabled = !streamStates.ContainsKey(i) || !streamStates[i]
                };
                startButton.Click += StartStream_Click;
                
                var stopButton = new Button 
                { 
                    Content = "⏹", 
                    Width = 35, 
                    Height = 28,
                    Background = System.Windows.Media.Brushes.Red,
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = i,
                    Margin = new Thickness(0, 0, 10, 0),
                    IsEnabled = streamStates.ContainsKey(i) && streamStates[i]
                };
                stopButton.Click += StopStream_Click;
                
                var deleteButton = new Button 
                { 
                    Content = "❌", 
                    Width = 28, 
                    Height = 28,
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = i
                };
                deleteButton.Click += DeleteFile_Click;
                
                panel.Children.Add(textBlock);
                panel.Children.Add(startButton);
                panel.Children.Add(stopButton);
                panel.Children.Add(deleteButton);
                FileListBox.Items.Add(panel);
            }
        }

        private void StartStream_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var index = (int)button.Tag;
            
            if (index >= selectedVideoFiles.Count) return;
            
            try
            {
                button.IsEnabled = false;
                AppendLogWithLimit($"Stream {index + 1} 시작 중...\n");
                
                if (mediaMtxProcess == null || mediaMtxProcess.HasExited)
                {
                    StartMediaMtx();
                    Thread.Sleep(2000);
                }
                
                StartIndividualStream(index);
                streamStates[index] = true;
                UpdateFileList();
                UpdateRtspUrlList();
                UpdateButtons();
                AppendLogWithLimit($"Stream {index + 1} 시작됨: {Path.GetFileName(selectedVideoFiles[index])}\n");
            }
            catch (Exception ex)
            {
                AppendLogWithLimit($"Stream {index + 1} 시작 오류: {ex.Message}\n");
                button.IsEnabled = true;
            }
        }

        private void StopStream_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var index = (int)button.Tag;
            
            try
            {
                StopIndividualStream(index);
                streamStates[index] = false;
                UpdateFileList();
                UpdateRtspUrlList();
                UpdateButtons();
                AppendLogWithLimit($"Stream {index + 1} 중지됨: {Path.GetFileName(selectedVideoFiles[index])}\n");
            }
            catch (Exception ex)
            {
                AppendLogWithLimit($"Stream {index + 1} 중지 오류: {ex.Message}\n");
            }
        }

        private void StartIndividualStream(int index)
        {
            var streamName = $"stream{index + 1}";
            var appDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var ffmpegPath = Path.Combine(appDirectory, "ffmpeg.exe");
            
            var ffmpegArgs = new List<string>
            {
                "-re",
                "-stream_loop", "-1",
                "-i", $"\"{selectedVideoFiles[index]}\"",
                "-avoid_negative_ts", "make_zero"
            };
            
            var resolution = ((ComboBoxItem)ResolutionComboBox.SelectedItem).Content.ToString();
            var fps = ((ComboBoxItem)FpsComboBox.SelectedItem).Content.ToString();
            
            if (resolution != "원본" || fps != "원본")
            {
                // 인코딩이 필요한 경우
                var filters = new List<string>();
                if (resolution != "원본") filters.Add($"scale={resolution}");
                if (fps != "원본") filters.Add($"fps={fps}");
                
                if (filters.Count > 0)
                {
                    ffmpegArgs.AddRange(new[] { "-vf", string.Join(",", filters) });
                }
                
                ffmpegArgs.AddRange(new[] { 
                    "-c:v", "libx264", 
                    "-preset", "fast",
                    "-crf", "23",
                    "-maxrate", "2000k",
                    "-bufsize", "4000k",
                    "-c:a", "aac",
                    "-b:a", "128k",
                    "-ar", "44100"
                });
            }
            else
            {
                // 원본 설정인 경우 복사
                ffmpegArgs.AddRange(new[] { "-c", "copy" });
            }
            
            ffmpegArgs.AddRange(new[] { "-f", "rtsp", $"rtsp://localhost:8554/{streamName}" });
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = string.Join(" ", ffmpegArgs),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.OutputDataReceived += (s, e) => 
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // 중요한 로그만 출력
                    if (e.Data.Contains("Input #") || e.Data.Contains("Stream #") || 
                        e.Data.Contains("Output #") || e.Data.Contains("Stream mapping") ||
                        (e.Data.Contains("frame=") && e.Data.Contains("fps=")))
                    {
                        Dispatcher.Invoke(() => AppendLogWithLimit($"Stream {index + 1}: {e.Data}\n"));
                    }
                }
            };

            process.ErrorDataReceived += (s, e) => 
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // 오류는 모두 출력하되 중요하지 않은 것은 제외
                    if (!e.Data.Contains("libav") && !e.Data.Contains("built with") && 
                        !e.Data.Contains("configuration:"))
                    {
                        Dispatcher.Invoke(() => AppendLogWithLimit($"Stream {index + 1}: {e.Data}\n"));
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            streamProcesses[index] = process;
            
            AppendLogWithLimit($"FFmpeg 명령어: {string.Join(" ", ffmpegArgs)}\n");
        }

        private void StopIndividualStream(int index)
        {
            if (streamProcesses.ContainsKey(index))
            {
                var process = streamProcesses[index];
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                process.Dispose();
                streamProcesses.Remove(index);
            }
        }

        private void DeleteFile_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var index = (int)button.Tag;
            
            if (index < selectedVideoFiles.Count)
            {
                if (streamStates.ContainsKey(index) && streamStates[index])
                {
                    StopIndividualStream(index);
                }
                
                selectedVideoFiles.RemoveAt(index);
                
                var newStreamStates = new Dictionary<int, bool>();
                var newStreamProcesses = new Dictionary<int, Process>();
                
                for (int i = 0; i < selectedVideoFiles.Count; i++)
                {
                    if (i < index)
                    {
                        if (streamStates.ContainsKey(i))
                            newStreamStates[i] = streamStates[i];
                        if (streamProcesses.ContainsKey(i))
                            newStreamProcesses[i] = streamProcesses[i];
                    }
                    else
                    {
                        if (streamStates.ContainsKey(i + 1))
                            newStreamStates[i] = streamStates[i + 1];
                        if (streamProcesses.ContainsKey(i + 1))
                            newStreamProcesses[i] = streamProcesses[i + 1];
                    }
                }
                
                streamStates = newStreamStates;
                streamProcesses = newStreamProcesses;
                
                UpdateFileList();
                UpdateRtspUrlList();
                UpdateButtons();
                SaveSettings();
            }
        }

        private void StartAll_Click(object sender, RoutedEventArgs e)
        {
            if (selectedVideoFiles.Count == 0) return;

            try
            {
                StartAllButton.IsEnabled = false;
                AppendLogWithLimit("전체 스트림 시작 중...\n");

                if (mediaMtxProcess == null || mediaMtxProcess.HasExited)
                {
                    StartMediaMtx();
                    Thread.Sleep(2000);
                }
                
                for (int i = 0; i < selectedVideoFiles.Count; i++)
                {
                    if (!streamStates.ContainsKey(i) || !streamStates[i])
                    {
                        StartIndividualStream(i);
                        streamStates[i] = true;
                    }
                }
                
                UpdateFileList();
                UpdateRtspUrlList();
                UpdateButtons();
                AppendLogWithLimit($"{selectedVideoFiles.Count}개 RTSP 스트림 시작됨\n");
            }
            catch (Exception ex)
            {
                AppendLogWithLimit($"전체 시작 오류: {ex.Message}\n");
                StartAllButton.IsEnabled = true;
            }
        }

        private void StopAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                for (int i = 0; i < selectedVideoFiles.Count; i++)
                {
                    if (streamStates.ContainsKey(i) && streamStates[i])
                    {
                        StopIndividualStream(i);
                        streamStates[i] = false;
                    }
                }
                
                StopMediaMtxProcess();
                UpdateFileList();
                UpdateRtspUrlList();
                UpdateButtons();
                AppendLogWithLimit("모든 RTSP 스트리밍 중지됨\n");
            }
            catch (Exception ex)
            {
                AppendLogWithLimit($"전체 중지 오류: {ex.Message}\n");
            }
        }

        private void UpdateButtons()
        {
            var hasRunningStreams = streamStates.Values.Any(s => s);
            StopAllButton.IsEnabled = hasRunningStreams;
            
            var runningCount = streamStates.Values.Count(s => s);
            StatusText.Text = runningCount > 0 ? $"{runningCount}개 스트림 실행 중" : "대기 중";
            StatusText.Foreground = runningCount > 0 ? 
                System.Windows.Media.Brushes.Green : 
                System.Windows.Media.Brushes.Gray;
        }

        private void UpdateRtspUrlList()
        {
            RtspUrlListBox.Items.Clear();
            for (int i = 0; i < selectedVideoFiles.Count; i++)
            {
                if (streamStates.ContainsKey(i) && streamStates[i])
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
        }

        private void StartMediaMtx()
        {
            var config = $@"logLevel: info
logDestinations: [stdout]

api: no

rtsp: yes
rtspAddress: :8554

paths:
  all:
    source: publisher
";
            
            File.WriteAllText(configPath, config);
            
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
                {
                    // 중요한 MediaMTX 로그만 출력
                    if (e.Data.Contains("listener opened") || e.Data.Contains("session") || 
                        e.Data.Contains("ERROR") || e.Data.Contains("WARN"))
                    {
                        Dispatcher.Invoke(() => AppendLogWithLimit($"MediaMTX: {e.Data}\n"));
                    }
                }
            };

            mediaMtxProcess.ErrorDataReceived += (s, e) => 
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Dispatcher.Invoke(() => AppendLogWithLimit($"MediaMTX ERROR: {e.Data}\n"));
            };

            mediaMtxProcess.Start();
            mediaMtxProcess.BeginOutputReadLine();
            mediaMtxProcess.BeginErrorReadLine();
            
            AppendLogWithLimit("MediaMTX 서버 시작됨\n");
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

        private const int MaxLines = 500;
        private readonly ConcurrentQueue<string> _logQueue = new();
        private readonly Timer _flushTimer;

        private void AppendLogWithLimit(string message)
        {
            _logQueue.Enqueue(message);
            
            // 파일에도 기록
            try
            {
                File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss} - {message}");
            }
            catch
            {
                // 파일 쓰기 실패시 무시
            }
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
            foreach (var process in streamProcesses.Values)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.Dispose();
                }
            }
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
                StartAllButton.IsEnabled = selectedVideoFiles.Count > 0;
            }
        }

        private void SaveSettings()
        {
            File.WriteAllLines(settingsPath, selectedVideoFiles);
        }
    }
}
