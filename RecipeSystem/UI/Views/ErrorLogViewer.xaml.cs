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
        private DateTime _lastLogFileCheck;
        #endregion

        #region Constructor
        /// <summary>
        /// ErrorLogViewer 생성자
        /// </summary>
        public ErrorLogViewer()
        {
            InitializeComponent();
            InitializeLogViewer();
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 로그 뷰어 초기화
        /// </summary>
        private async void InitializeLogViewer()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그 뷰어 초기화 시작");

                // 컬렉션 초기화
                _logEntries = new ObservableCollection<LogEntry>();
                _filteredLogEntries = new ObservableCollection<LogEntry>();
                dgLogEntries.ItemsSource = _filteredLogEntries;

                // 실시간 업데이트 타이머 설정 (2초 간격)
                _realTimeUpdateTimer = new DispatcherTimer();
                _realTimeUpdateTimer.Interval = TimeSpan.FromSeconds(2);
                _realTimeUpdateTimer.Tick += OnRealTimeUpdate;

                // 초기 로그 로드
                await LoadLogEntriesAsync();

                // 실시간 업데이트 시작
                if (_isRealTimeEnabled)
                {
                    _realTimeUpdateTimer.Start();
                }

                UpdateStatusMessage("로그 뷰어 준비 완료");
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그 뷰어 초기화 완료");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "InitializeLogViewer", "로그 뷰어 초기화 실패", ex);
                UpdateStatusMessage("로그 뷰어 초기화 실패");
            }
        }
        #endregion

        #region Log Loading and Processing
        /// <summary>
        /// 로그 엔트리를 비동기로 로드
        /// </summary>
        private async Task LoadLogEntriesAsync()
        {
            try
            {
                UpdateStatusMessage("로그 파일 로드 중...");
                txtLogStatus.Text = "로그 로드 중...";

                await Task.Run(() =>
                {
                    // UI 스레드에서 컬렉션 초기화
                    Dispatcher.Invoke(() =>
                    {
                        _logEntries.Clear();
                    });

                    // 로그 파일 목록 가져오기
                    var logFiles = LogManager.GetLogFiles();

                    // 최신 파일부터 처리 (최대 5개 파일)
                    var recentFiles = logFiles.OrderByDescending(f => f.LastWriteTime).Take(5);

                    foreach (var logFile in recentFiles)
                    {
                        try
                        {
                            ProcessLogFile(logFile.FullPath);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"로그 파일 처리 오류: {logFile.FileName} - {ex.Message}");
                        }
                    }
                });

                // 로그 정렬 및 필터 적용
                ApplyFiltersAndSort();
                UpdateLogStatistics();

                _lastLogFileCheck = DateTime.Now;
                UpdateStatusMessage($"로그 로드 완료 - {_logEntries.Count}개 항목");
                txtLogStatus.Text = $"{_logEntries.Count}개 로그 항목 로드됨";
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "LoadLogEntriesAsync", "로그 로드 실패", ex);
                UpdateStatusMessage("로그 로드 실패");
                txtLogStatus.Text = "로그 로드 실패";
            }
        }

        /// <summary>
        /// 개별 로그 파일 처리
        /// </summary>
        /// <param name="filePath">로그 파일 경로</param>
        private void ProcessLogFile(string filePath)
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
                        // UI 스레드에서 컬렉션에 추가
                        Dispatcher.Invoke(() =>
                        {
                            _logEntries.Add(logEntry);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"로그 파일 처리 실패: {filePath} - {ex.Message}");
            }
        }

        /// <summary>
        /// 로그 라인을 LogEntry 객체로 파싱
        /// </summary>
        /// <param name="logLine">로그 라인</param>
        /// <returns>파싱된 LogEntry 또는 null</returns>
        private LogEntry ParseLogLine(string logLine)
        {
            try
            {
                // 로그 포맷: [2024-08-26 15:30:45.123] [INFO] [Module] [Method] Message
                if (string.IsNullOrWhiteSpace(logLine) || !logLine.StartsWith("["))
                    return null;

                var parts = logLine.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) return null;

                // 시간 파싱
                if (!DateTime.TryParse(parts[0].Trim(), out DateTime timestamp))
                    timestamp = DateTime.Now;

                var level = parts[1].Trim();
                var module = parts[2].Trim();
                var method = parts[3].Trim();

                // 메시지는 나머지 부분
                var messageIndex = logLine.IndexOf("]", logLine.IndexOf(method)) + 1;
                var message = messageIndex < logLine.Length ? logLine.Substring(messageIndex).Trim() : "";

                return new LogEntry
                {
                    TimeStamp = timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    Level = level,
                    Module = module,
                    Method = method,
                    Message = message,
                    FullText = logLine
                };
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region Filtering and Searching
        /// <summary>
        /// 필터 및 정렬 적용
        /// </summary>
        private void ApplyFiltersAndSort()
        {
            try
            {
                var filtered = _logEntries.AsEnumerable();

                // 로그 레벨 필터
                if (_currentLogLevelFilter != "All")
                {
                    filtered = filtered.Where(entry => entry.Level.Equals(_currentLogLevelFilter, StringComparison.OrdinalIgnoreCase));
                }

                // 검색 텍스트 필터
                if (!string.IsNullOrEmpty(_currentSearchText))
                {
                    var searchLower = _currentSearchText.ToLower();
                    filtered = filtered.Where(entry =>
                        entry.Module.ToLower().Contains(searchLower) ||
                        entry.Method.ToLower().Contains(searchLower) ||
                        entry.Message.ToLower().Contains(searchLower));
                }

                // 시간 순 정렬 (최신 항목이 위에)
                var sortedList = filtered.OrderByDescending(entry => entry.TimeStamp).ToList();

                // UI 업데이트
                _filteredLogEntries.Clear();
                foreach (var entry in sortedList)
                {
                    _filteredLogEntries.Add(entry);
                }

                UpdateLogCount();
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ApplyFiltersAndSort", "필터 적용 실패", ex);
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
        #endregion

        #region UI Event Handlers
        /// <summary>
        /// 로그 레벨 필터 변경 이벤트
        /// </summary>
        private void OnLogLevelFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbLogLevel.SelectedItem is ComboBoxItem selectedItem)
            {
                _currentLogLevelFilter = selectedItem.Content.ToString();
                ApplyFiltersAndSort();
            }
        }

        /// <summary>
        /// 검색 텍스트 변경 이벤트
        /// </summary>
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _currentSearchText = txtSearch.Text;
            ApplyFiltersAndSort();
        }

        /// <summary>
        /// 새로고침 버튼 클릭
        /// </summary>
        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await LoadLogEntriesAsync();
        }

        /// <summary>
        /// 내보내기 버튼 클릭
        /// </summary>
        private void OnExportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Title = "로그 내보내기",
                    Filter = "텍스트 파일 (*.txt)|*.txt|CSV 파일 (*.csv)|*.csv",
                    FileName = $"LogExport_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    ExportLogs(saveDialog.FileName, saveDialog.FilterIndex == 2);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "OnExportClick", "내보내기 실패", ex);
                MessageBox.Show("로그 내보내기에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 로그 삭제 버튼 클릭
        /// </summary>
        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "모든 로그 파일을 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.",
                "로그 삭제 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ClearAllLogs();
            }
        }

        /// <summary>
        /// 실시간 업데이트 토글 이벤트
        /// </summary>
        private void OnRealTimeToggled(object sender, RoutedEventArgs e)
        {
            _isRealTimeEnabled = chkRealTime.IsChecked == true;

            if (_isRealTimeEnabled)
            {
                _realTimeUpdateTimer.Start();
                UpdateStatusMessage("실시간 업데이트 활성화");
            }
            else
            {
                _realTimeUpdateTimer.Stop();
                UpdateStatusMessage("실시간 업데이트 비활성화");
            }
        }

        /// <summary>
        /// 로그 엔트리 선택 이벤트
        /// </summary>
        private void OnLogEntrySelected(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (dgLogEntries.SelectedItem is LogEntry selectedEntry)
                {
                    ShowLogDetails(selectedEntry);
                }
                else
                {
                    ClearLogDetails();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "OnLogEntrySelected", "로그 선택 처리 실패", ex);
            }
        }

        /// <summary>
        /// 실시간 업데이트 타이머 이벤트
        /// </summary>
        private async void OnRealTimeUpdate(object sender, EventArgs e)
        {
            try
            {
                // 로그 파일 변경 확인 (파일 수정 시간 체크)
                var logFiles = LogManager.GetLogFiles();
                if (logFiles.Any(f => f.LastWriteTime > _lastLogFileCheck))
                {
                    await LoadLogEntriesAsync();
                }

                // 마지막 업데이트 시간 표시
                txtLastUpdate.Text = $"마지막 업데이트: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "OnRealTimeUpdate", "실시간 업데이트 실패", ex);
            }
        }
        #endregion

        #region Log Details Display
        /// <summary>
        /// 선택된 로그의 상세 정보 표시
        /// </summary>
        /// <param name="logEntry">표시할 로그 엔트리</param>
        private void ShowLogDetails(LogEntry logEntry)
        {
            try
            {
                txtDetailTime.Text = $"시간: {logEntry.TimeStamp}";
                txtDetailLevel.Text = $"레벨: {logEntry.Level}";
                txtDetailModule.Text = $"모듈: {logEntry.Module} → {logEntry.Method}";
                txtDetailMessage.Text = logEntry.Message;

                // 예외 정보가 포함된 경우 추가 표시
                if (logEntry.Message.Contains("Exception") || logEntry.Message.Contains("Error"))
                {
                    txtDetailException.Text = ExtractExceptionDetails(logEntry.FullText);
                    txtDetailException.Visibility = Visibility.Visible;
                }
                else
                {
                    txtDetailException.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ShowLogDetails", "로그 상세 정보 표시 실패", ex);
            }
        }

        /// <summary>
        /// 로그 상세 정보 지우기
        /// </summary>
        private void ClearLogDetails()
        {
            txtDetailTime.Text = "";
            txtDetailLevel.Text = "";
            txtDetailModule.Text = "";
            txtDetailMessage.Text = "";
            txtDetailException.Text = "";
            txtDetailException.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 로그에서 예외 상세 정보 추출
        /// </summary>
        /// <param name="fullLogText">전체 로그 텍스트</param>
        /// <returns>예외 상세 정보</returns>
        private string ExtractExceptionDetails(string fullLogText)
        {
            try
            {
                // 스택 트레이스나 예외 정보가 있는지 확인
                var lines = fullLogText.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var exceptionLines = lines.Where(line =>
                    line.Contains("at ") ||
                    line.Contains("Exception") ||
                    line.Contains("StackTrace")).ToList();

                return exceptionLines.Any() ? string.Join(Environment.NewLine, exceptionLines) : "";
            }
            catch
            {
                return "";
            }
        }
        #endregion

        #region Export and Management
        /// <summary>
        /// 로그를 파일로 내보내기
        /// </summary>
        /// <param name="fileName">저장할 파일명</param>
        /// <param name="isCsvFormat">CSV 형식 여부</param>
        private void ExportLogs(string fileName, bool isCsvFormat = false)
        {
            try
            {
                UpdateStatusMessage("로그 내보내기 중...");

                var logsToExport = _filteredLogEntries.ToList();

                using (var writer = new StreamWriter(fileName))
                {
                    if (isCsvFormat)
                    {
                        // CSV 헤더
                        writer.WriteLine("시간,레벨,모듈,메서드,메시지");

                        foreach (var entry in logsToExport)
                        {
                            var message = entry.Message.Replace("\"", "\"\"").Replace(",", ";");
                            writer.WriteLine($"\"{entry.TimeStamp}\",\"{entry.Level}\",\"{entry.Module}\",\"{entry.Method}\",\"{message}\"");
                        }
                    }
                    else
                    {
                        // 텍스트 형식
                        writer.WriteLine($"로그 내보내기 - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        writer.WriteLine($"총 {logsToExport.Count}개 항목");
                        writer.WriteLine("".PadRight(80, '='));
                        writer.WriteLine();

                        foreach (var entry in logsToExport)
                        {
                            writer.WriteLine($"[{entry.TimeStamp}] [{entry.Level}] [{entry.Module}] [{entry.Method}]");
                            writer.WriteLine(entry.Message);
                            writer.WriteLine();
                        }
                    }
                }

                UpdateStatusMessage($"로그 내보내기 완료: {fileName}");
                AlarmMessageManager.ShowCustomMessage($"로그가 성공적으로 내보내졌습니다: {Path.GetFileName(fileName)}", AlarmCategory.Success);
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ExportLogs", "로그 내보내기 실패", ex);
                UpdateStatusMessage("로그 내보내기 실패");
                throw;
            }
        }

        /// <summary>
        /// 모든 로그 파일 삭제
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
                _logEntries.Clear();
                _filteredLogEntries.Clear();

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
                txtStatusMessage.Text = message;
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
        #endregion

        #region Cleanup
        /// <summary>
        /// 리소스 정리 (UserControl Unloaded 시 호출)
        /// </summary>
        private void OnViewerUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _realTimeUpdateTimer?.Stop();
                _realTimeUpdateTimer = null;

                _logEntries?.Clear();
                _filteredLogEntries?.Clear();

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그 뷰어 리소스 정리 완료");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "OnViewerUnloaded", "리소스 정리 실패", ex);
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