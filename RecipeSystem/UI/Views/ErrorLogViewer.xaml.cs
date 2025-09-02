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
using TeachingPendant.Alarm;

namespace TeachingPendant.UI.Views
{
    /// <summary>
    /// ErrorLogViewer 사용자 컨트롤의 상호작용 로직
    /// 단순화된 버전으로 컴파일 에러 방지에 중점
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

                // 컬렉션 초기화
                _logEntries = new ObservableCollection<LogEntry>();
                _filteredLogEntries = new ObservableCollection<LogEntry>();

                // DataGrid에 바인딩
                dgLogEntries.ItemsSource = _filteredLogEntries;

                // 현재 로그 파일 경로 설정
                SetCurrentLogFilePath();

                // 필터 초기화 (순서 변경: 로그 파일을 먼저 알아야 모듈 필터를 만들 수 있어요)
                await InitializeFilters();

                // 초기 로그 로드
                await LoadLogEntries();

                // 실시간 업데이트 타이머 시작
                StartRealTimeUpdateTimer();

                txtStatusMessage.Text = "로그 뷰어 초기화 완료";
            }
            catch (Exception ex)
            {
                txtStatusMessage.Text = "로그 뷰어 초기화 실패: " + ex.Message;
            }
        }

        /// <summary>
        /// 현재 로그 파일 경로 설정
        /// </summary>
        private void SetCurrentLogFilePath()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var logDirectory = Path.Combine(appDataPath, "TeachingPendantData", "Logs");
                var fileName = $"TeachingPendant_{DateTime.Now:yyyyMMdd}.log";
                _currentLogFilePath = Path.Combine(logDirectory, fileName);

                // 로그 디렉토리가 없으면 생성
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
            }
            catch (Exception ex)
            {
                // 로그 파일 경로 설정 실패시에도 계속 진행
            }
        }

        /// <summary>
        /// 필터 초기화
        /// </summary>
        private async Task InitializeFilters()
        {
            try
            {
                // 요구사항에 따라 '레벨' 필터는 비워둡니다.
                if (cmbLogLevel != null)
                {
                    // ItemsSource를 null로 설정하여 목록을 비웁니다.
                    cmbLogLevel.ItemsSource = null;

                    // 사용자가 헷갈리지 않도록 컨트롤을 비활성화 처리하는 것이 좋아요.
                    cmbLogLevel.IsEnabled = false;
                }

                // '모듈' 필터에만 동적으로 모듈 리스트를 채우는 로직을 실행합니다.
                await InitializeModuleFilter();

                // --- 이하 검색, 체크박스, 버튼 이벤트 연결 코드는 동일 ---

                // 검색 박스 이벤트 연결
                if (txtSearch != null)
                {
                    txtSearch.TextChanged += txtSearch_TextChanged;
                }

                // 체크박스 이벤트 연결
                if (chkOnlyToday != null)
                {
                    chkOnlyToday.Checked += chkOnlyToday_CheckedChanged;
                    chkOnlyToday.Unchecked += chkOnlyToday_CheckedChanged;
                }

                if (chkRealTime != null)
                {
                    chkRealTime.Checked += chkRealTime_CheckedChanged;
                    chkRealTime.Unchecked += chkRealTime_CheckedChanged;
                }

                // 버튼 이벤트 연결
                if (btnRefresh != null)
                {
                    btnRefresh.Click += btnRefresh_Click;
                }

                if (btnExportLogs != null)
                {
                    btnExportLogs.Click += btnExportLogs_Click;
                }

                if (btnClearLogs != null)
                {
                    btnClearLogs.Click += btnClearLogs_Click;
                }

                // DataGrid 이벤트 연결
                if (dgLogEntries != null)
                {
                    dgLogEntries.SelectionChanged += dgLogEntries_SelectionChanged;
                }
            }
            catch (Exception ex)
            {
                // 필터 초기화 실패시에도 계속 진행
            }
        }

        /// <summary>
        /// 모듈 필터 초기화
        /// </summary>
        private async Task InitializeModuleFilter()
        {
            try
            {
                if (cmbModule == null) return;

                var modules = new List<string> { "All" };

                // 로그 파일에서 사용된 모듈 목록 추출
                var logModules = await GetAvailableLogModules();
                modules.AddRange(logModules.OrderBy(m => m));

                // UI 스레드에서 **cmbModule** 콤보박스 업데이트
                cmbModule.ItemsSource = modules;
                cmbModule.SelectedIndex = 0;
                cmbModule.SelectionChanged += cmbModule_SelectionChanged; // 이벤트 핸들러도 확인
            }
            catch (Exception ex)
            {
                // 모듈 필터 초기화 실패시에도 계속 진행
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
                // 모듈 목록 가져오기 실패시에도 계속 진행
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

                    // 최근 라인부터 가져오기 (maxLines가 지정된 경우)
                    if (maxLines > 0 && allLines.Count > maxLines)
                    {
                        lines = allLines.Skip(allLines.Count - maxLines).ToList();
                    }
                    else
                    {
                        lines = allLines;
                    }
                }
            }
            catch (Exception ex)
            {
                // 파일 읽기 실패시에도 계속 진행
            }

            return lines;
        }

        /// <summary>
        /// 로그 항목들 로드
        /// </summary>
        private async Task LoadLogEntries()
        {
            try
            {
                txtStatusMessage.Text = "로그 파일 읽는 중...";

                var lines = await ReadLogFileAsync(_currentLogFilePath);
                var newEntries = new List<LogEntry>();

                foreach (var line in lines)
                {
                    var logEntry = ParseLogLine(line);
                    if (logEntry != null)
                    {
                        newEntries.Add(logEntry);
                    }
                }

                // UI 스레드에서 컬렉션 업데이트
                Application.Current.Dispatcher.Invoke(() =>
                {
                    lock (_lockObject)
                    {
                        _logEntries.Clear();
                        foreach (var entry in newEntries.OrderBy(e => e.TimeStamp))
                        {
                            _logEntries.Add(entry);
                        }
                    }

                    ApplyFilters();
                    UpdateUI();
                });

                txtStatusMessage.Text = $"로그 {newEntries.Count}개 로드 완료";
            }
            catch (Exception ex)
            {
                txtStatusMessage.Text = "로그 로드 실패: " + ex.Message;
            }
        }

        /// <summary>
        /// 로그 라인 파싱
        /// 형식: [2025-06-19 16:30:45.123] [INFO] [Teaching] [SaveCurrentData] Message
        /// </summary>
        private LogEntry ParseLogLine(string logLine)
        {
            if (string.IsNullOrWhiteSpace(logLine))
                return null;

            try
            {
                // 간단한 파싱 로직
                if (logLine.Contains("[") && logLine.Contains("]"))
                {
                    var parts = logLine.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length >= 4)
                    {
                        DateTime timeStamp;
                        if (DateTime.TryParse(parts[0].Trim(), out timeStamp))
                        {
                            var level = parts.Length > 1 ? parts[1].Trim() : "";
                            var module = parts.Length > 2 ? parts[2].Trim() : "";
                            var method = parts.Length > 3 ? parts[3].Trim() : "";
                            var message = parts.Length > 4 ? parts[4].Trim() : "";

                            return new LogEntry
                            {
                                TimeStamp = timeStamp,
                                Level = level,
                                Module = module,
                                Method = method,
                                Message = message
                            };
                        }
                    }
                }
            }
            catch
            {
                // 파싱 실패시 null 반환
            }

            return null;
        }
        #endregion

        #region Filtering
        /// <summary>
        /// 필터 적용
        /// </summary>
        private void ApplyFilters()
        {
            try
            {
                lock (_lockObject)
                {
                    var filteredItems = _logEntries.AsEnumerable();

                    // 로그 레벨 필터
                    if (_currentLogLevelFilter != "All")
                    {
                        filteredItems = filteredItems.Where(item =>
                            string.Equals(item.Level, _currentLogLevelFilter, StringComparison.OrdinalIgnoreCase));
                    }

                    // 모듈 필터
                    if (_currentModuleFilter != "All")
                    {
                        filteredItems = filteredItems.Where(item =>
                            string.Equals(item.Module, _currentModuleFilter, StringComparison.OrdinalIgnoreCase));
                    }

                    // 검색 텍스트 필터
                    if (!string.IsNullOrEmpty(_currentSearchText))
                    {
                        filteredItems = filteredItems.Where(item =>
                            item.Message.IndexOf(_currentSearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            item.Method.IndexOf(_currentSearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            item.Exception.IndexOf(_currentSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
                    }

                    // 오늘만 보기 필터
                    if (chkOnlyToday != null && chkOnlyToday.IsChecked == true)
                    {
                        var today = DateTime.Today;
                        filteredItems = filteredItems.Where(item => item.TimeStamp.Date == today);
                    }

                    _filteredLogEntries.Clear();
                    foreach (var item in filteredItems.OrderByDescending(e => e.TimeStamp))
                    {
                        _filteredLogEntries.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                // 필터 적용 실패시에도 계속 진행
            }
        }
        #endregion

        #region Real-time Update
        /// <summary>
        /// 실시간 업데이트 타이머 시작
        /// </summary>
        private void StartRealTimeUpdateTimer()
        {
            try
            {
                _realTimeUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                _realTimeUpdateTimer.Tick += RealTimeUpdateTimer_Tick;

                if (_isRealTimeEnabled)
                {
                    _realTimeUpdateTimer.Start();
                }
            }
            catch (Exception ex)
            {
                // 타이머 시작 실패시에도 계속 진행
            }
        }

        /// <summary>
        /// 실시간 업데이트 타이머 이벤트
        /// </summary>
        private async void RealTimeUpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!_isRealTimeEnabled)
                    return;

                // 파일 변경 시간 확인
                if (File.Exists(_currentLogFilePath))
                {
                    var lastWriteTime = File.GetLastWriteTime(_currentLogFilePath);
                    if (lastWriteTime > _lastLogFileCheck)
                    {
                        _lastLogFileCheck = lastWriteTime;
                        await LoadLogEntries();
                    }
                }
            }
            catch (Exception ex)
            {
                // 실시간 업데이트 실패시에도 계속 진행
            }
        }
        #endregion

        #region UI Event Handlers
        /// <summary>
        /// 로그 레벨 필터 변경
        /// </summary>
        private void cmbLogLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (cmbLogLevel.SelectedItem != null)
                {
                    _currentLogLevelFilter = cmbLogLevel.SelectedItem.ToString();
                    ApplyFilters();
                    UpdateUI();
                }
            }
            catch (Exception ex)
            {
                // 이벤트 처리 실패시에도 계속 진행
            }
        }

        /// <summary>
        /// 모듈 필터 변경
        /// </summary>
        private void cmbModule_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (cmbModule.SelectedItem != null)
                {
                    _currentModuleFilter = cmbModule.SelectedItem.ToString();
                    ApplyFilters();
                    UpdateUI();
                }
            }
            catch (Exception ex)
            {
                // 이벤트 처리 실패시에도 계속 진행
            }
        }

        /// <summary>
        /// 검색 텍스트 변경
        /// </summary>
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _currentSearchText = txtSearch.Text ?? "";
                ApplyFilters();
                UpdateUI();
            }
            catch (Exception ex)
            {
                // 이벤트 처리 실패시에도 계속 진행
            }
        }

        /// <summary>
        /// 오늘만 보기 체크박스 변경
        /// </summary>
        private void chkOnlyToday_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyFilters();
                UpdateUI();
            }
            catch (Exception ex)
            {
                // 이벤트 처리 실패시에도 계속 진행
            }
        }

        /// <summary>
        /// 실시간 업데이트 체크박스 변경
        /// </summary>
        private void chkRealTime_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _isRealTimeEnabled = chkRealTime?.IsChecked ?? true;

                if (_isRealTimeEnabled && _realTimeUpdateTimer != null)
                {
                    _realTimeUpdateTimer.Start();
                }
                else if (_realTimeUpdateTimer != null)
                {
                    _realTimeUpdateTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                // 이벤트 처리 실패시에도 계속 진행
            }
        }

        /// <summary>
        /// 새로고침 버튼 클릭
        /// </summary>
        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadLogEntries();
            }
            catch (Exception ex)
            {
                txtStatusMessage.Text = "새로고침 실패: " + ex.Message;
            }
        }

        /// <summary>
        /// 로그 내보내기 버튼 클릭
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
                MessageBox.Show("로그 내보내기 실패: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 로그 삭제 버튼 클릭
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
                    MessageBox.Show("모든 로그가 삭제되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("로그 삭제 실패: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 로그 항목 선택 변경
        /// </summary>
        private void dgLogEntries_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (dgLogEntries.SelectedItem is LogEntry selectedItem)
                {
                    ShowLogDetails(selectedItem);
                }
                else
                {
                    ClearLogDetails();
                }
            }
            catch (Exception ex)
            {
                // 선택 변경 실패시에도 계속 진행
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
                if (txtLogCount != null)
                {
                    txtLogCount.Text = $"로그 수: {_filteredLogEntries.Count}";
                }

                if (txtLastUpdate != null)
                {
                    txtLastUpdate.Text = $"마지막 업데이트: {DateTime.Now:HH:mm:ss}";
                }

                UpdateLogStatistics();
            }
            catch (Exception ex)
            {
                // UI 업데이트 실패시에도 계속 진행
            }
        }

        /// <summary>
        /// 로그 상세 정보 표시
        /// </summary>
        private void ShowLogDetails(LogEntry logEntry)
        {
            try
            {
                if (txtDetailTime != null)
                    txtDetailTime.Text = $"시간: {logEntry.TimeStamp:yyyy-MM-dd HH:mm:ss.fff}";

                if (txtDetailLevel != null)
                    txtDetailLevel.Text = $"레벨: {logEntry.Level}";

                if (txtDetailModule != null)
                    txtDetailModule.Text = $"모듈: {logEntry.Module}";

                if (txtDetailMethod != null)
                    txtDetailMethod.Text = $"메서드: {logEntry.Method}";

                if (txtDetailMessage != null)
                    txtDetailMessage.Text = $"메시지: {logEntry.Message}";

                if (txtDetailException != null)
                {
                    txtDetailException.Text = string.IsNullOrEmpty(logEntry.Exception)
                        ? ""
                        : $"예외 정보:\n{logEntry.Exception}";
                }
            }
            catch (Exception ex)
            {
                // 상세 정보 표시 실패시에도 계속 진행
            }
        }

        /// <summary>
        /// 로그 상세 정보 초기화
        /// </summary>
        private void ClearLogDetails()
        {
            try
            {
                if (txtDetailTime != null) txtDetailTime.Text = "";
                if (txtDetailLevel != null) txtDetailLevel.Text = "";
                if (txtDetailModule != null) txtDetailModule.Text = "";
                if (txtDetailMethod != null) txtDetailMethod.Text = "";
                if (txtDetailMessage != null) txtDetailMessage.Text = "";
                if (txtDetailException != null) txtDetailException.Text = "";
            }
            catch (Exception ex)
            {
                // 상세 정보 초기화 실패시에도 계속 진행
            }
        }

        /// <summary>
        /// 로그 통계 업데이트
        /// </summary>
        private void UpdateLogStatistics()
        {
            try
            {
                if (txtStats == null) return;

                if (_filteredLogEntries.Count == 0)
                {
                    txtStats.Text = "통계: 로그 없음";
                    return;
                }

                var errorCount = _filteredLogEntries.Count(e => string.Equals(e.Level, "ERROR", StringComparison.OrdinalIgnoreCase));
                var warningCount = _filteredLogEntries.Count(e => string.Equals(e.Level, "WARNING", StringComparison.OrdinalIgnoreCase));
                var infoCount = _filteredLogEntries.Count(e => string.Equals(e.Level, "INFO", StringComparison.OrdinalIgnoreCase));
                var debugCount = _filteredLogEntries.Count(e => string.Equals(e.Level, "DEBUG", StringComparison.OrdinalIgnoreCase));

                txtStats.Text = $"통계 - ERROR: {errorCount}, WARNING: {warningCount}, INFO: {infoCount}, DEBUG: {debugCount}";
            }
            catch (Exception ex)
            {
                // 통계 업데이트 실패시에도 계속 진행
            }
        }

        /// <summary>
        /// 로그를 파일로 내보내기
        /// </summary>
        private void ExportLogsToFile(string filePath)
        {
            try
            {
                using (var writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("=== TeachingPendant 로그 내보내기 ===");
                    writer.WriteLine($"내보낸 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"총 로그 수: {_filteredLogEntries.Count}");
                    writer.WriteLine();

                    foreach (var entry in _filteredLogEntries)
                    {
                        writer.WriteLine($"[{entry.TimeStamp:yyyy-MM-dd HH:mm:ss.fff}] [{entry.Level}] [{entry.Module}] [{entry.Method}] {entry.Message}");

                        if (!string.IsNullOrEmpty(entry.Exception))
                        {
                            writer.WriteLine($"예외: {entry.Exception}");
                        }

                        writer.WriteLine();
                    }
                }

                MessageBox.Show($"로그가 성공적으로 내보내졌습니다.\n파일: {filePath}", "내보내기 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"로그 내보내기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Resource Management
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
            }
            catch (Exception ex)
            {
                // 리소스 정리 실패시에도 계속 진행
            }
        }
        #endregion
    }
}