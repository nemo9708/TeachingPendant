using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using TeachingPendant.Logging;
using TeachingPendant.Alarm;

namespace TeachingPendant.UI.Views
{
    /// <summary>
    /// ErrorLogViewer 사용자 컨트롤의 상호작용 로직
    /// 실제 Logger 시스템과 연동하여 로그 파일을 읽고 표시
    /// </summary>
    public partial class ErrorLogViewer : UserControl
    {
        #region Private Fields
        private static readonly string CLASS_NAME = "ErrorLogViewer";

        private ObservableCollection<LogEntry> _logEntries;
        private ObservableCollection<LogEntry> _filteredLogEntries;
        private DispatcherTimer _realTimeUpdateTimer;
        private bool _isRealTimeEnabled = true;
        private string _currentSearchText = "";
        private string _currentLogLevelFilter = "All";
        private string _currentModuleFilter = "All";
        private DateTime _lastLogFileCheck;
        private readonly object _lockObject = new object();
        private string _currentLogFilePath = "";
        #endregion

        #region Constructor
        /// <summary>
        /// ErrorLogViewer 생성자
        /// </summary>
        public ErrorLogViewer()
        {
            InitializeComponent();
            this.Loaded += OnViewerLoaded;
            this.Unloaded += OnViewerUnloaded;
        }
        #endregion

        #region Event Handlers - Lifecycle
        /// <summary>
        /// 뷰어 로드 이벤트
        /// </summary>
        private async void OnViewerLoaded(object sender, RoutedEventArgs e)
        {
            await InitializeLogViewer();
        }

        /// <summary>
        /// 뷰어 언로드 이벤트
        /// </summary>
        private void OnViewerUnloaded(object sender, RoutedEventArgs e)
        {
            CleanupResources();
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 로그 뷰어 초기화
        /// </summary>
        private async Task InitializeLogViewer()
        {
            try
            {
                txtStatusMessage.Text = "로그 시스템 초기화 중...";
                Logger.Info(CLASS_NAME, "InitializeLogViewer", "로그 뷰어 초기화 시작");

                // 컬렉션 초기화
                _logEntries = new ObservableCollection<LogEntry>();
                _filteredLogEntries = new ObservableCollection<LogEntry>();

                // DataGrid 바인딩 설정
                dgLogs.ItemsSource = _filteredLogEntries;

                // 모듈 필터 콤보박스 초기화
                await InitializeModuleFilter();

                // 로그 파일 경로 설정
                _currentLogFilePath = GetCurrentLogFilePath();

                // 초기 로그 로드
                await LoadLogEntries();

                // 실시간 업데이트 타이머 시작
                StartRealTimeUpdate();

                txtStatusMessage.Text = "로그 뷰어 준비 완료";
                Logger.Info(CLASS_NAME, "InitializeLogViewer", "로그 뷰어 초기화 완료");
            }
            catch (Exception ex)
            {
                txtStatusMessage.Text = "초기화 실패";
                Logger.Error(CLASS_NAME, "InitializeLogViewer", "로그 뷰어 초기화 실패", ex);
                AlarmMessageManager.ShowCustomMessage("로그 뷰어 초기화에 실패했습니다", AlarmCategory.Error);
            }
        }

        /// <summary>
        /// 현재 로그 파일 경로 가져오기
        /// FileLogWriter의 실제 로그 파일 경로 사용
        /// </summary>
        private string GetCurrentLogFilePath()
        {
            try
            {
                // Logger가 초기화되어 있다면 실제 파일 경로 사용
                if (Logger.IsInitialized)
                {
                    var logDirectory = Logger.GetLogDirectory();
                    var today = DateTime.Now.ToString("yyyyMMdd");
                    var logFileName = $"TeachingPendant_{today}.log";
                    var fullPath = Path.Combine(logDirectory, logFileName);

                    Logger.LogDebug(CLASS_NAME, "GetCurrentLogFilePath", $"로그 파일 경로: {fullPath}");
                    return fullPath;
                }
                else
                {
                    // Logger가 초기화되지 않은 경우 기본 경로 사용
                    var logDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "TeachingPendantData",
                        "Logs"
                    );

                    var today = DateTime.Now.ToString("yyyyMMdd");
                    var logFileName = $"TeachingPendant_{today}.log";
                    var fullPath = Path.Combine(logDirectory, logFileName);

                    return fullPath;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "GetCurrentLogFilePath", "로그 파일 경로 가져오기 실패", ex);
                return "";
            }
        }

        /// <summary>
        /// 모듈 필터 초기화
        /// </summary>
        private async Task InitializeModuleFilter()
        {
            try
            {
                var modules = new List<string> { "All" };

                // 로그 파일에서 사용된 모듈 목록 추출
                var logModules = await GetAvailableLogModules();
                modules.AddRange(logModules.OrderBy(m => m));

                // UI 스레드에서 콤보박스 업데이트
                cmbModule.ItemsSource = modules;
                cmbModule.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "InitializeModuleFilter", "모듈 필터 초기화 실패", ex);
            }
        }

        /// <summary>
        /// 사용 가능한 로그 모듈 목록 가져오기
        /// </summary>
        private async Task<List<string>> GetAvailableLogModules()
        {
            var modules = new HashSet<string>();

            try
            {
                if (File.Exists(_currentLogFilePath))
                {
                    var lines = await ReadLogFileAsync(_currentLogFilePath, 1000); // 최근 1000줄에서 모듈 추출
                    foreach (var line in lines)
                    {
                        var logEntry = ParseLogLine(line);
                        if (logEntry != null && !string.IsNullOrEmpty(logEntry.Module))
                        {
                            modules.Add(logEntry.Module);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "GetAvailableLogModules", "모듈 목록 가져오기 실패", ex);
            }

            return modules.ToList();
        }
        #endregion

        #region Log Loading and Processing
        /// <summary>
        /// 로그 파일 비동기 읽기
        /// </summary>
        private async Task<List<string>> ReadLogFileAsync(string filePath, int maxLines = -1)
        {
            var lines = new List<string>();

            try
            {
                if (!File.Exists(filePath))
                    return lines;

                // C# 6.0 호환 파일 읽기
                using (var reader = new StreamReader(filePath))
                {
                    var allLines = new List<string>();
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        allLines.Add(line);
                    }

                    if (maxLines > 0 && allLines.Count > maxLines)
                    {
                        // 최근 줄들만 가져오기
                        lines.AddRange(allLines.Skip(allLines.Count - maxLines));
                    }
                    else
                    {
                        lines.AddRange(allLines);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ReadLogFileAsync", "로그 파일 읽기 실패", ex);
            }

            return lines;
        }

        /// <summary>
        /// 로그 엔트리 로드 (성능 최적화)
        /// </summary>
        private async Task LoadLogEntries()
        {
            try
            {
                txtLogStatus.Text = "로그 파일 읽는 중...";

                lock (_lockObject)
                {
                    _logEntries.Clear();
                }

                if (!File.Exists(_currentLogFilePath))
                {
                    txtLogStatus.Text = "로그 파일을 찾을 수 없습니다";
                    UpdateUI();
                    return;
                }

                // 파일 크기 체크 및 성능 최적화
                var fileInfo = new FileInfo(_currentLogFilePath);
                var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);

                int maxLines;
                if (fileSizeMB > 100) // 100MB 이상
                {
                    maxLines = 5000; // 최근 5000줄만
                    txtLogStatus.Text = $"대용량 파일 ({fileSizeMB:F1}MB) - 최근 {maxLines}줄만 로드";
                }
                else if (fileSizeMB > 50) // 50MB 이상
                {
                    maxLines = 10000; // 최근 10000줄만
                    txtLogStatus.Text = $"큰 파일 ({fileSizeMB:F1}MB) - 최근 {maxLines}줄만 로드";
                }
                else
                {
                    maxLines = -1; // 전체 로드
                    txtLogStatus.Text = $"파일 크기 {fileSizeMB:F1}MB - 전체 로드 중...";
                }

                var lines = await ReadLogFileAsync(_currentLogFilePath, maxLines);
                var logEntries = new List<LogEntry>();

                // 청크 단위로 파싱 (메모리 효율성)
                const int chunkSize = 1000;
                for (int i = 0; i < lines.Count; i += chunkSize)
                {
                    var chunk = lines.Skip(i).Take(chunkSize);
                    foreach (var line in chunk)
                    {
                        var entry = ParseLogLine(line);
                        if (entry != null)
                        {
                            logEntries.Add(entry);
                        }
                    }

                    // UI 반응성을 위한 양보
                    if (i % (chunkSize * 5) == 0) // 5000줄마다
                    {
                        await Task.Delay(1); // UI 스레드에 양보
                        txtLogStatus.Text = $"파싱 진행 중... ({i + chunkSize}/{lines.Count})";
                    }
                }

                // UI 스레드에서 컬렉션 업데이트
                await Dispatcher.InvokeAsync(() =>
                {
                    lock (_lockObject)
                    {
                        _logEntries.Clear();
                        // 시간 역순 정렬 (최신이 위로)
                        foreach (var entry in logEntries.OrderByDescending(e => e.TimeStamp))
                        {
                            _logEntries.Add(entry);
                        }
                    }

                    ApplyFilters();
                    UpdateUI();
                });

                _lastLogFileCheck = DateTime.Now;
                txtLogStatus.Text = $"로드 완료 ({logEntries.Count}개) - 파일크기: {fileSizeMB:F1}MB";
            }
            catch (Exception ex)
            {
                txtLogStatus.Text = "로그 로드 실패";
                Logger.Error(CLASS_NAME, "LoadLogEntries", "로그 엔트리 로드 실패", ex);
            }
        }

        /// <summary>
        /// 로그 라인 파싱
        /// 실제 Logger 시스템의 로그 형식에 맞춰 파싱
        /// 형식: [2025-06-19 16:30:45.123] [INFO] [Teaching] [SaveCurrentData] Position saved: Group1-Cassette1 Slot=15
        /// </summary>
        private LogEntry ParseLogLine(string logLine)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(logLine)) return null;

                // 실제 로그 형식: [timestamp] [level] [module] [method] message
                var parts = logLine.Split(new[] { "] [" }, StringSplitOptions.None);
                if (parts.Length < 4) return null;

                // 시간 파싱 ([2025-06-19 16:30:45.123] 형태)
                var timeStr = parts[0].TrimStart('[');
                DateTime timeStamp;
                if (!DateTime.TryParse(timeStr, out timeStamp))
                {
                    timeStamp = DateTime.Now;
                }

                // 레벨 파싱
                var level = parts[1].Trim();

                // 모듈 파싱 (패딩 제거)
                var module = parts[2].Trim();

                // 메서드와 메시지 분리
                var methodAndMessage = parts[3];
                var methodEnd = methodAndMessage.IndexOf("] ");

                string method = "";
                string message = "";
                string exception = "";

                if (methodEnd > 0)
                {
                    // 메서드명 추출 (패딩 제거)
                    method = methodAndMessage.Substring(0, methodEnd).Trim();
                    message = methodAndMessage.Substring(methodEnd + 2);

                    // 예외 정보 분리 (예외가 있는 경우)
                    if (message.Contains("Exception:"))
                    {
                        var exceptionIndex = message.IndexOf("Exception:");
                        exception = message.Substring(exceptionIndex);
                        message = message.Substring(0, exceptionIndex).Trim();
                    }
                }
                else
                {
                    // 메서드 끝 구분자가 없는 경우 전체를 메시지로 처리
                    message = methodAndMessage.TrimEnd(']');
                }

                return new LogEntry(timeStamp, level, module, method, message, exception);
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ParseLogLine", $"로그 라인 파싱 실패: {logLine}", ex);
                return null;
            }
        }
        #endregion

        #region Real-Time Updates
        /// <summary>
        /// 실시간 업데이트 시작
        /// </summary>
        private void StartRealTimeUpdate()
        {
            if (_realTimeUpdateTimer == null)
            {
                _realTimeUpdateTimer = new DispatcherTimer();
                _realTimeUpdateTimer.Interval = TimeSpan.FromSeconds(2); // 2초마다 체크
                _realTimeUpdateTimer.Tick += OnRealTimeUpdate;
            }

            _realTimeUpdateTimer.Start();
        }

        /// <summary>
        /// 실시간 업데이트 이벤트
        /// </summary>
        private async void OnRealTimeUpdate(object sender, EventArgs e)
        {
            if (!_isRealTimeEnabled) return;

            try
            {
                // 파일 변경 시간 체크
                if (File.Exists(_currentLogFilePath))
                {
                    var lastWriteTime = File.GetLastWriteTime(_currentLogFilePath);
                    if (lastWriteTime > _lastLogFileCheck)
                    {
                        await LoadNewLogEntries();
                        _lastLogFileCheck = lastWriteTime;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "OnRealTimeUpdate", "실시간 업데이트 실패", ex);
            }
        }

        /// <summary>
        /// 새 로그 엔트리만 로드
        /// </summary>
        private async Task LoadNewLogEntries()
        {
            try
            {
                var lines = await ReadLogFileAsync(_currentLogFilePath);
                var newEntries = new List<LogEntry>();

                // 마지막 체크 이후의 로그만 파싱
                foreach (var line in lines)
                {
                    var entry = ParseLogLine(line);
                    if (entry != null && entry.TimeStamp > _lastLogFileCheck)
                    {
                        newEntries.Add(entry);
                    }
                }

                if (newEntries.Count > 0)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        lock (_lockObject)
                        {
                            foreach (var entry in newEntries.OrderByDescending(e => e.TimeStamp))
                            {
                                _logEntries.Insert(0, entry);
                            }

                            // 최대 로그 수 제한 (메모리 관리)
                            while (_logEntries.Count > 50000)
                            {
                                _logEntries.RemoveAt(_logEntries.Count - 1);
                            }
                        }

                        ApplyFilters();
                        UpdateUI();
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "LoadNewLogEntries", "새 로그 엔트리 로드 실패", ex);
            }
        }
        #endregion

        #region Filtering and Search
        /// <summary>
        /// 필터 적용
        /// </summary>
        private void ApplyFilters()
        {
            try
            {
                lock (_lockObject)
                {
                    _filteredLogEntries.Clear();

                    var filtered = _logEntries.AsEnumerable();

                    // 레벨 필터
                    if (_currentLogLevelFilter != "All")
                    {
                        filtered = filtered.Where(e => string.Equals(e.Level, _currentLogLevelFilter, StringComparison.OrdinalIgnoreCase));
                    }

                    // 모듈 필터
                    if (_currentModuleFilter != "All")
                    {
                        filtered = filtered.Where(e => string.Equals(e.Module, _currentModuleFilter, StringComparison.OrdinalIgnoreCase));
                    }

                    // 검색 필터
                    if (!string.IsNullOrWhiteSpace(_currentSearchText))
                    {
                        var searchLower = _currentSearchText.ToLowerInvariant();
                        filtered = filtered.Where(e =>
                            (e.Message != null && e.Message.ToLowerInvariant().Contains(searchLower)) ||
                            (e.Method != null && e.Method.ToLowerInvariant().Contains(searchLower)) ||
                            (e.Exception != null && e.Exception.ToLowerInvariant().Contains(searchLower)));
                    }

                    // 날짜 필터 (오늘만 보기)
                    if (chkOnlyToday?.IsChecked == true)
                    {
                        var today = DateTime.Now.Date;
                        filtered = filtered.Where(e => e.TimeStamp.Date == today);
                    }

                    foreach (var entry in filtered)
                    {
                        _filteredLogEntries.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ApplyFilters", "필터 적용 실패", ex);
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 새로고침 버튼 클릭
        /// </summary>
        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadLogEntries();
                AlarmMessageManager.ShowCustomMessage("로그가 새로고침되었습니다", AlarmCategory.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnRefresh_Click", "새로고침 실패", ex);
            }
        }

        /// <summary>
        /// 로그 레벨 필터 변경
        /// </summary>
        private void cmbLogLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbLogLevel.SelectedItem != null)
            {
                var selectedItem = cmbLogLevel.SelectedItem as ComboBoxItem;
                _currentLogLevelFilter = selectedItem?.Content?.ToString() ?? "All";
                ApplyFilters();
            }
        }

        /// <summary>
        /// 모듈 필터 변경
        /// </summary>
        private void cmbModule_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbModule.SelectedItem != null)
            {
                _currentModuleFilter = cmbModule.SelectedItem.ToString();
                ApplyFilters();
            }
        }

        /// <summary>
        /// 검색 텍스트 변경
        /// </summary>
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _currentSearchText = txtSearch?.Text ?? "";
            ApplyFilters();
        }

        /// <summary>
        /// 오늘만 보기 체크박스 변경
        /// </summary>
        private void chkOnlyToday_CheckedChanged(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        /// <summary>
        /// 실시간 업데이트 체크박스 변경
        /// </summary>
        private void chkRealTime_CheckedChanged(object sender, RoutedEventArgs e)
        {
            _isRealTimeEnabled = chkRealTime?.IsChecked ?? true;
            if (_isRealTimeEnabled)
            {
                StartRealTimeUpdate();
            }
            else
            {
                _realTimeUpdateTimer?.Stop();
            }
        }

        /// <summary>
        /// 로그 선택 변경
        /// </summary>
        private void dgLogs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgLogs.SelectedItem is LogEntry selectedLog)
            {
                ShowLogDetails(selectedLog);
            }
            else
            {
                ClearLogDetails();
            }
        }

        /// <summary>
        /// 모든 로그 삭제
        /// </summary>
        private void btnClearLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "모든 로그를 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.",
                    "로그 삭제 확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    lock (_lockObject)
                    {
                        _logEntries.Clear();
                        _filteredLogEntries.Clear();
                    }

                    // 로그 파일도 비우기
                    if (File.Exists(_currentLogFilePath))
                    {
                        File.WriteAllText(_currentLogFilePath, "");
                    }

                    UpdateUI();
                    AlarmMessageManager.ShowCustomMessage("모든 로그가 삭제되었습니다", AlarmCategory.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnClearLogs_Click", "로그 삭제 실패", ex);
                AlarmMessageManager.ShowCustomMessage("로그 삭제 중 오류가 발생했습니다", AlarmCategory.Error);
            }
        }

        /// <summary>
        /// 로그 내보내기
        /// </summary>
        private void btnExportLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Title = "로그 내보내기",
                    Filter = "텍스트 파일 (*.txt)|*.txt|CSV 파일 (*.csv)|*.csv",
                    DefaultExt = "txt",
                    FileName = $"ExportedLogs_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    ExportLogsToFile(saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnExportLogs_Click", "로그 내보내기 실패", ex);
            }
        }
        #endregion

        #region UI Updates
        /// <summary>
        /// UI 상태 업데이트
        /// </summary>
        private void UpdateUI()
        {
            try
            {
                txtLogCount.Text = $"로그 수: {_filteredLogEntries.Count}";
                txtLastUpdate.Text = $"마지막 업데이트: {DateTime.Now:HH:mm:ss}";
                UpdateLogStatistics();
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "UpdateUI", "UI 업데이트 실패", ex);
            }
        }

        /// <summary>
        /// 로그 상세 정보 표시
        /// </summary>
        private void ShowLogDetails(LogEntry logEntry)
        {
            try
            {
                txtDetailTime.Text = $"시간: {logEntry.TimeStamp:yyyy-MM-dd HH:mm:ss.fff}";
                txtDetailLevel.Text = $"레벨: {logEntry.Level}";
                txtDetailModule.Text = $"모듈: {logEntry.Module}";
                txtDetailMethod.Text = $"메서드: {logEntry.Method}";
                txtDetailMessage.Text = $"메시지: {logEntry.Message}";
                txtDetailException.Text = string.IsNullOrEmpty(logEntry.Exception)
                    ? ""
                    : $"예외 정보:\n{logEntry.Exception}";
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ShowLogDetails", "로그 상세 정보 표시 실패", ex);
            }
        }

        /// <summary>
        /// 로그 상세 정보 초기화
        /// </summary>
        private void ClearLogDetails()
        {
            try
            {
                txtDetailTime.Text = "";
                txtDetailLevel.Text = "";
                txtDetailModule.Text = "";
                txtDetailMethod.Text = "";
                txtDetailMessage.Text = "";
                txtDetailException.Text = "";
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ClearLogDetails", "로그 상세 정보 초기화 실패", ex);
            }
        }

        /// <summary>
        /// 로그 통계 업데이트
        /// </summary>
        private void UpdateLogStatistics()
        {
            try
            {
                if (_filteredLogEntries.Count == 0)
                {
                    txtStats.Text = "통계: 로그 없음";
                    return;
                }

                var errorCount = _filteredLogEntries.Count(e => string.Equals(e.Level, "ERROR", StringComparison.OrdinalIgnoreCase));
                var warningCount = _filteredLogEntries.Count(e => string.Equals(e.Level, "WARNING", StringComparison.OrdinalIgnoreCase));
                var infoCount = _filteredLogEntries.Count(e => string.Equals(e.Level, "INFO", StringComparison.OrdinalIgnoreCase));
                var debugCount = _filteredLogEntries.Count(e => string.Equals(e.Level, "DEBUG", StringComparison.OrdinalIgnoreCase));

                txtStats.Text = $"통계: Error({errorCount}) Warning({warningCount}) Info({infoCount}) Debug({debugCount})";
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "UpdateLogStatistics", "통계 업데이트 실패", ex);
            }
        }

        /// <summary>
        /// 로그 파일로 내보내기
        /// </summary>
        private void ExportLogsToFile(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var logData = new List<string>();

                if (extension == ".csv")
                {
                    // CSV 형식
                    logData.Add("시간,레벨,모듈,메서드,메시지,예외");
                    foreach (var entry in _filteredLogEntries)
                    {
                        var csvLine = $"\"{entry.TimeStamp:yyyy-MM-dd HH:mm:ss.fff}\",\"{entry.Level}\",\"{entry.Module}\",\"{entry.Method}\",\"{entry.Message}\",\"{entry.Exception}\"";
                        logData.Add(csvLine);
                    }
                }
                else
                {
                    // 텍스트 형식
                    logData.Add($"로그 내보내기 - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    logData.Add($"총 {_filteredLogEntries.Count}개 항목");
                    logData.Add(new string('=', 80));
                    logData.Add("");

                    foreach (var entry in _filteredLogEntries)
                    {
                        logData.Add($"[{entry.TimeStamp:yyyy-MM-dd HH:mm:ss.fff}] [{entry.Level}] [{entry.Module}] [{entry.Method}]");
                        logData.Add($"메시지: {entry.Message}");
                        if (!string.IsNullOrEmpty(entry.Exception))
                        {
                            logData.Add($"예외: {entry.Exception}");
                        }
                        logData.Add("");
                    }
                }

                File.WriteAllLines(filePath, logData);
                AlarmMessageManager.ShowCustomMessage($"로그가 내보내기되었습니다: {filePath}", AlarmCategory.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ExportLogsToFile", "로그 내보내기 실패", ex);
                AlarmMessageManager.ShowCustomMessage("로그 내보내기 중 오류가 발생했습니다", AlarmCategory.Error);
            }
        }
        #endregion

        #region Cleanup
        /// <summary>
        /// 리소스 정리
        /// </summary>
        private void CleanupResources()
        {
            try
            {
                if (_realTimeUpdateTimer != null)
                {
                    _realTimeUpdateTimer.Stop();
                    _realTimeUpdateTimer = null;
                }

                Logger.Info(CLASS_NAME, "CleanupResources", "ErrorLogViewer 리소스 정리 완료");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "CleanupResources", "리소스 정리 실패", ex);
            }
        }
        #endregion
    }
}