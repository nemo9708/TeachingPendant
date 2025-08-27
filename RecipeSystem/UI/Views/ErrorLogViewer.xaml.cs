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
    /// ErrorLogViewer.xaml에 대한 상호작용 로직
    /// 로그 파일 뷰어 및 실시간 로그 모니터링 기능 제공
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
        #endregion

        #region Constructor
        /// <summary>
        /// ErrorLogViewer 생성자
        /// </summary>
        public ErrorLogViewer()
        {
            InitializeComponent();
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
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그 뷰어 초기화 시작");

                // 컬렉션 초기화
                _logEntries = new ObservableCollection<LogEntry>();
                _filteredLogEntries = new ObservableCollection<LogEntry>();

                // DataGrid에 바인딩
                dgLogs.ItemsSource = _filteredLogEntries;

                // 모듈 필터 초기화
                InitializeModuleFilter();

                // 실시간 업데이트 타이머 설정
                SetupRealTimeTimer();

                // 초기 로그 로드
                await LoadLogEntriesAsync();

                UpdateStatusMessage("로그 뷰어 초기화 완료");
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그 뷰어 초기화 완료");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "InitializeLogViewer", "로그 뷰어 초기화 실패", ex);
                UpdateStatusMessage("로그 뷰어 초기화 실패");
            }
        }

        /// <summary>
        /// 모듈 필터 초기화
        /// </summary>
        private void InitializeModuleFilter()
        {
            try
            {
                cmbModule.Items.Clear();
                cmbModule.Items.Add("All");
                cmbModule.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "InitializeModuleFilter", "모듈 필터 초기화 실패", ex);
            }
        }

        /// <summary>
        /// 실시간 업데이트 타이머 설정
        /// </summary>
        private void SetupRealTimeTimer()
        {
            try
            {
                _realTimeUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2) // 2초마다 업데이트
                };
                _realTimeUpdateTimer.Tick += RealTimeUpdateTimer_Tick;

                if (_isRealTimeEnabled)
                {
                    _realTimeUpdateTimer.Start();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "SetupRealTimeTimer", "실시간 타이머 설정 실패", ex);
            }
        }
        #endregion

        #region Event Handlers - Timer
        /// <summary>
        /// 실시간 업데이트 타이머 이벤트
        /// </summary>
        private async void RealTimeUpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_isRealTimeEnabled)
                {
                    await LoadLogEntriesAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실시간 업데이트 오류: {ex.Message}");
            }
        }
        #endregion

        #region Event Handlers - UI Controls
        /// <summary>
        /// 새로고침 버튼 클릭
        /// </summary>
        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatusMessage("로그 새로고침 중...");
                await LoadLogEntriesAsync();
                UpdateStatusMessage("로그 새로고침 완료");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnRefresh_Click", "새로고침 실패", ex);
                UpdateStatusMessage("새로고침 실패");
            }
        }

        /// <summary>
        /// 로그 레벨 필터 변경
        /// </summary>
        private void cmbLogLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (cmbLogLevel.SelectedItem is ComboBoxItem selectedItem)
                {
                    _currentLogLevelFilter = selectedItem.Content.ToString();
                    ApplyFiltersAndSort();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "cmbLogLevel_SelectionChanged", "로그 레벨 필터 변경 실패", ex);
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
                    ApplyFiltersAndSort();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "cmbModule_SelectionChanged", "모듈 필터 변경 실패", ex);
            }
        }

        /// <summary>
        /// 검색 텍스트 변경
        /// </summary>
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _currentSearchText = txtSearch.Text;
                ApplyFiltersAndSort();
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "txtSearch_TextChanged", "검색 필터 적용 실패", ex);
            }
        }

        /// <summary>
        /// 검색 초기화 버튼 클릭
        /// </summary>
        private void btnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtSearch.Text = "";
                _currentSearchText = "";
                ApplyFiltersAndSort();
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnClearSearch_Click", "검색 초기화 실패", ex);
            }
        }

        /// <summary>
        /// 실시간 모니터링 토글
        /// </summary>
        private void chkRealTime_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _isRealTimeEnabled = chkRealTime.IsChecked == true;

                if (_realTimeUpdateTimer != null)
                {
                    if (_isRealTimeEnabled)
                    {
                        _realTimeUpdateTimer.Start();
                        UpdateStatusMessage("실시간 모니터링 활성화");
                    }
                    else
                    {
                        _realTimeUpdateTimer.Stop();
                        UpdateStatusMessage("실시간 모니터링 비활성화");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "chkRealTime_CheckedChanged", "실시간 모니터링 토글 실패", ex);
            }
        }

        /// <summary>
        /// 로그 선택 변경
        /// </summary>
        private void dgLogs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (dgLogs.SelectedItem is LogEntry selectedLog)
                {
                    DisplayLogDetails(selectedLog);
                }
                else
                {
                    ClearLogDetails();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "dgLogs_SelectionChanged", "로그 선택 처리 실패", ex);
            }
        }

        /// <summary>
        /// 로그 내보내기 버튼 클릭
        /// </summary>
        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ExportLogsToFile();
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnExport_Click", "로그 내보내기 실패", ex);
            }
        }

        /// <summary>
        /// 모든 로그 삭제 버튼 클릭
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
                    ClearAllLogs();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnClearLogs_Click", "로그 삭제 버튼 처리 실패", ex);
            }
        }
        #endregion

        #region Core Methods
        /// <summary>
        /// 로그 엔트리 로드 (비동기)
        /// </summary>
        private async Task LoadLogEntriesAsync()
        {
            try
            {
                // 현재 시간 기준으로 중복 로드 방지
                var now = DateTime.Now;
                if ((now - _lastLogFileCheck).TotalSeconds < 1)
                {
                    return;
                }

                await Task.Run(() =>
                {
                    var logFiles = LogManager.GetLogFiles();

                    // 최신 파일부터 처리 (최대 5개 파일)
                    var recentFiles = logFiles.OrderByDescending(f => f.LastWriteTime).Take(5);

                    lock (_lockObject)
                    {
                        var previousCount = _logEntries.Count;
                        var modules = new HashSet<string>();

                        foreach (var logFile in recentFiles)
                        {
                            try
                            {
                                ProcessLogFile(logFile.FullPath, modules);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"로그 파일 처리 오류: {logFile.FileName} - {ex.Message}");
                            }
                        }

                        // 새로운 모듈 업데이트
                        Dispatcher.BeginInvoke(new Action(() => UpdateModuleFilter(modules)));

                        // 새로운 로그가 있을 때만 UI 업데이트
                        if (_logEntries.Count != previousCount)
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                ApplyFiltersAndSort();
                                UpdateLogStatistics();
                                UpdateLastUpdateTime();
                            }));
                        }
                    }
                });

                _lastLogFileCheck = now;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    txtLogStatus.Text = $"{_logEntries.Count}개 로그 항목 로드됨";
                }));
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "LoadLogEntriesAsync", "로그 로드 실패", ex);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateStatusMessage("로그 로드 실패");
                    txtLogStatus.Text = "로그 로드 실패";
                }));
            }
        }

        /// <summary>
        /// 개별 로그 파일 처리
        /// </summary>
        /// <param name="filePath">로그 파일 경로</param>
        /// <param name="modules">발견된 모듈 목록</param>
        private void ProcessLogFile(string filePath, HashSet<string> modules)
        {
            try
            {
                if (!File.Exists(filePath)) return;

                var lines = File.ReadAllLines(filePath);

                foreach (var line in lines)
                {
                    var logEntry = ParseLogLine(line);
                    if (logEntry != null)
                    {
                        // 중복 제거 (시간 + 메시지 기준)
                        var isDuplicate = _logEntries.Any(e =>
                            e.TimeStamp == logEntry.TimeStamp &&
                            e.Message == logEntry.Message);

                        if (!isDuplicate)
                        {
                            _logEntries.Add(logEntry);

                            // 모듈 목록에 추가
                            if (!string.IsNullOrEmpty(logEntry.Module))
                            {
                                modules.Add(logEntry.Module);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"로그 파일 처리 오류: {filePath} - {ex.Message}");
            }
        }

        /// <summary>
        /// 로그 라인 파싱
        /// </summary>
        /// <param name="line">로그 라인</param>
        /// <returns>파싱된 로그 엔트리</returns>
        private LogEntry ParseLogLine(string line)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(line) || line.Length < 20)
                    return null;

                // 로그 형식: [2025-08-27 14:30:15.123] [INFO] [Module] [Method] Message
                var parts = line.Split(new[] { "] [" }, StringSplitOptions.None);

                if (parts.Length < 4)
                    return null;

                var entry = new LogEntry();

                // 시간 추출 (첫 번째 [ 제거)
                entry.TimeStamp = parts[0].Substring(1);

                // 레벨 추출
                entry.Level = parts[1];

                // 모듈 추출
                entry.Module = parts[2];

                // 메서드와 메시지 분리
                var methodAndMessage = parts[3];
                var methodEndIndex = methodAndMessage.IndexOf("] ");

                if (methodEndIndex > 0)
                {
                    entry.Method = methodAndMessage.Substring(0, methodEndIndex);
                    entry.Message = methodAndMessage.Substring(methodEndIndex + 2);
                }
                else
                {
                    entry.Method = "";
                    entry.Message = methodAndMessage.TrimEnd(']');
                }

                // 전체 텍스트 저장
                entry.FullText = line;

                return entry;
            }
            catch
            {
                // 파싱 실패 시 기본 엔트리 반환
                return new LogEntry
                {
                    TimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    Level = "Info",
                    Module = "Unknown",
                    Method = "",
                    Message = line,
                    FullText = line
                };
            }
        }

        /// <summary>
        /// 필터 및 정렬 적용
        /// </summary>
        private void ApplyFiltersAndSort()
        {
            try
            {
                lock (_lockObject)
                {
                    var filteredList = _logEntries.AsEnumerable();

                    // 로그 레벨 필터 적용
                    if (_currentLogLevelFilter != "All")
                    {
                        filteredList = filteredList.Where(e =>
                            e.Level.Equals(_currentLogLevelFilter, StringComparison.OrdinalIgnoreCase));
                    }

                    // 모듈 필터 적용
                    if (_currentModuleFilter != "All")
                    {
                        filteredList = filteredList.Where(e =>
                            e.Module.Equals(_currentModuleFilter, StringComparison.OrdinalIgnoreCase));
                    }

                    // 검색 텍스트 필터 적용
                    if (!string.IsNullOrWhiteSpace(_currentSearchText))
                    {
                        var searchText = _currentSearchText.ToLower();
                        filteredList = filteredList.Where(e =>
                            e.Message.ToLower().Contains(searchText) ||
                            e.Module.ToLower().Contains(searchText) ||
                            e.Method.ToLower().Contains(searchText));
                    }

                    // 시간 기준 내림차순 정렬 (최신 로그가 위로)
                    var sortedList = filteredList.OrderByDescending(e => e.TimeStamp).ToList();

                    // UI 업데이트
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // 1. 현재 선택된 항목을 기억합니다.
                        var previouslySelectedItem = dgLogs.SelectedItem as LogEntry;

                        _filteredLogEntries.Clear();
                        foreach (var entry in sortedList)
                        {
                            _filteredLogEntries.Add(entry);
                        }

                        // 2. 이전에 선택된 항목이 있었다면, 다시 선택해줍니다.
                        if (previouslySelectedItem != null)
                        {
                            // FullText가 고유한 값이라고 가정하고 같은 로그를 찾습니다.
                            var itemToReselect = _filteredLogEntries.FirstOrDefault(item =>
                                item.FullText == previouslySelectedItem.FullText);

                            if (itemToReselect != null)
                            {
                                dgLogs.SelectedItem = itemToReselect;
                            }
                        }

                        UpdateLogCount();
                    }));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ApplyFiltersAndSort", "필터 적용 실패", ex);
            }
        }

        /// <summary>
        /// 모듈 필터 업데이트
        /// </summary>
        /// <param name="modules">발견된 모듈 목록</param>
        private void UpdateModuleFilter(HashSet<string> modules)
        {
            try
            {
                var currentSelection = _currentModuleFilter;

                cmbModule.Items.Clear();
                cmbModule.Items.Add("All");

                foreach (var module in modules.OrderBy(m => m))
                {
                    cmbModule.Items.Add(module);
                }

                // 이전 선택 복원
                var itemToSelect = cmbModule.Items.Cast<string>()
                    .FirstOrDefault(item => item == currentSelection);

                if (itemToSelect != null)
                {
                    cmbModule.SelectedItem = itemToSelect;
                }
                else
                {
                    cmbModule.SelectedIndex = 0; // "All" 선택
                    _currentModuleFilter = "All";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "UpdateModuleFilter", "모듈 필터 업데이트 실패", ex);
            }
        }

        /// <summary>
        /// 로그 상세 정보 표시
        /// </summary>
        /// <param name="logEntry">선택된 로그 엔트리</param>
        private void DisplayLogDetails(LogEntry logEntry)
        {
            try
            {
                txtDetailTime.Text = $"시간: {logEntry.TimeStamp}";
                txtDetailLevel.Text = $"레벨: {logEntry.Level}";
                txtDetailModule.Text = $"모듈: {logEntry.Module}";
                txtDetailMethod.Text = $"메서드: {logEntry.Method}";
                txtDetailMessage.Text = logEntry.Message;

                // 예외 정보가 있으면 표시
                if (logEntry.FullText.Contains("Exception:") || logEntry.FullText.Contains("at "))
                {
                    var exceptionStart = logEntry.FullText.IndexOf("Exception:");
                    if (exceptionStart > 0)
                    {
                        txtDetailException.Text = logEntry.FullText.Substring(exceptionStart);
                    }
                    else
                    {
                        txtDetailException.Text = "";
                    }
                }
                else
                {
                    txtDetailException.Text = "";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "DisplayLogDetails", "로그 상세 정보 표시 실패", ex);
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
                if (_logEntries.Count == 0)
                {
                    txtStats.Text = "통계: 로그 없음";
                    return;
                }

                var errorCount = _logEntries.Count(e => e.Level.Equals("Error", StringComparison.OrdinalIgnoreCase));
                var warningCount = _logEntries.Count(e => e.Level.Equals("Warning", StringComparison.OrdinalIgnoreCase));
                var infoCount = _logEntries.Count(e => e.Level.Equals("Info", StringComparison.OrdinalIgnoreCase));
                var debugCount = _logEntries.Count(e => e.Level.Equals("Debug", StringComparison.OrdinalIgnoreCase));

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
        private void ExportLogsToFile()
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
                    var filePath = saveDialog.FileName;
                    var isCSV = Path.GetExtension(filePath).ToLower() == ".csv";

                    using (var writer = new StreamWriter(filePath))
                    {
                        if (isCSV)
                        {
                            // CSV 헤더
                            writer.WriteLine("시간,레벨,모듈,메서드,메시지");

                            // CSV 데이터
                            foreach (var entry in _filteredLogEntries)
                            {
                                writer.WriteLine($"\"{entry.TimeStamp}\",\"{entry.Level}\",\"{entry.Module}\",\"{entry.Method}\",\"{entry.Message}\"");
                            }
                        }
                        else
                        {
                            // 텍스트 형식
                            writer.WriteLine($"로그 내보내기 - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                            writer.WriteLine($"총 {_filteredLogEntries.Count}개 항목");
                            writer.WriteLine(new string('=', 80));

                            foreach (var entry in _filteredLogEntries)
                            {
                                writer.WriteLine($"[{entry.TimeStamp}] [{entry.Level}] [{entry.Module}] [{entry.Method}] {entry.Message}");
                            }
                        }
                    }

                    UpdateStatusMessage($"로그 내보내기 완료: {_filteredLogEntries.Count}개 항목");
                    AlarmMessageManager.ShowCustomMessage($"로그가 성공적으로 내보내졌습니다: {filePath}", AlarmCategory.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ExportLogsToFile", "로그 내보내기 실패", ex);
                MessageBox.Show("로그 내보내기에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 모든 로그 삭제
        /// </summary>
        private void ClearAllLogs()
        {
            try
            {
                UpdateStatusMessage("로그 삭제 중...");

                var logFiles = LogManager.GetLogFiles();
                var deletedCount = 0;

                foreach (var logFile in logFiles)
                {
                    try
                    {
                        if (LogManager.DeleteLogFile(logFile.FullPath))
                        {
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"로그 파일 삭제 실패: {logFile.FileName} - {ex.Message}");
                    }
                }

                // 메모리의 로그 엔트리도 지우기
                lock (_lockObject)
                {
                    _logEntries.Clear();
                    _filteredLogEntries.Clear();
                }

                UpdateLogStatistics();
                UpdateLogCount();
                ClearLogDetails();

                UpdateStatusMessage($"로그 삭제 완료: {deletedCount}개 파일");
                AlarmMessageManager.ShowCustomMessage($"{deletedCount}개의 로그 파일이 삭제되었습니다", AlarmCategory.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ClearAllLogs", "로그 삭제 실패", ex);
                UpdateStatusMessage("로그 삭제 실패");
                MessageBox.Show("일부 로그 파일 삭제에 실패했습니다.", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// 상태 메시지 업데이트
        /// </summary>
        /// <param name="message">표시할 메시지</param>
        private void UpdateStatusMessage(string message)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    txtStatusMessage.Text = message;
                }));
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Status: {message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 상태 메시지 업데이트 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 로그 카운트 업데이트
        /// </summary>
        private void UpdateLogCount()
        {
            try
            {
                txtLogCount.Text = $"로그 수: {_filteredLogEntries.Count}/{_logEntries.Count}";
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "UpdateLogCount", "로그 카운트 업데이트 실패", ex);
            }
        }

        /// <summary>
        /// 마지막 업데이트 시간 갱신
        /// </summary>
        private void UpdateLastUpdateTime()
        {
            try
            {
                txtLastUpdate.Text = $"마지막 업데이트: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "UpdateLastUpdateTime", "업데이트 시간 갱신 실패", ex);
            }
        }

        /// <summary>
        /// 리소스 정리
        /// </summary>
        private void CleanupResources()
        {
            try
            {
                _realTimeUpdateTimer?.Stop();
                _realTimeUpdateTimer = null;

                lock (_lockObject)
                {
                    _logEntries?.Clear();
                    _filteredLogEntries?.Clear();
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그 뷰어 리소스 정리 완료");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "CleanupResources", "리소스 정리 실패", ex);
            }
        }
        #endregion
    }

    #region Data Model Classes
    /// <summary>
    /// 로그 엔트리 데이터 모델
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// 로그 발생 시간
        /// </summary>
        public string TimeStamp { get; set; }

        /// <summary>
        /// 로그 레벨 (Debug, Info, Warning, Error)
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// 모듈명
        /// </summary>
        public string Module { get; set; }

        /// <summary>
        /// 메서드명
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 로그 메시지
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 전체 로그 텍스트 (상세 정보용)
        /// </summary>
        public string FullText { get; set; }

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public LogEntry()
        {
            TimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Level = "Info";
            Module = "";
            Method = "";
            Message = "";
            FullText = "";
        }
    }
    #endregion
}