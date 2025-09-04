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
using System.Windows.Input;

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
        /// 임시 메시지 표시 (txtStatusMessage 대신)
        /// </summary>
        private void ShowTempMessage(string message, int durationMs = 3000)
        {
            // txtStatusMessage가 없으므로 txtStats를 활용
            if (txtStats != null)
            {
                string originalText = txtStats.Text;
                txtStats.Text = message;
                txtStats.FontWeight = System.Windows.FontWeights.Bold;

                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(durationMs);
                timer.Tick += (s, e) =>
                {
                    txtStats.Text = originalText;
                    txtStats.FontWeight = System.Windows.FontWeights.Normal;
                    ((System.Windows.Threading.DispatcherTimer)s).Stop();
                };
                timer.Start();
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
                // 레벨 필터는 비워둠
                if (cmbLogLevel != null)
                {
                    cmbLogLevel.ItemsSource = null;
                    cmbLogLevel.IsEnabled = false;
                }

                // 모듈 필터 초기화
                await InitializeModuleFilter();

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

                // 선택 삭제 버튼 이벤트 연결 (중요!)
                if (btnDeleteSelected != null)
                {
                    btnDeleteSelected.Click += btnDeleteSelected_Click;
                    System.Diagnostics.Debug.WriteLine("btnDeleteSelected 이벤트 연결 완료");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("btnDeleteSelected이 null입니다!");
                }

                // 전체 삭제 버튼 이벤트
                if (btnClearLogs != null)
                {
                    btnClearLogs.Click += btnClearLogs_Click;
                }

                // DataGrid 이벤트 연결
                if (dgLogEntries != null)
                {
                    dgLogEntries.SelectionChanged += dgLogEntries_SelectionChanged;
                    dgLogEntries.PreviewKeyDown += dgLogEntries_PreviewKeyDown;
                    System.Diagnostics.Debug.WriteLine("dgLogEntries PreviewKeyDown 이벤트 연결 완료");
                }

                // UserControl 레벨 키보드 이벤트
                this.PreviewKeyDown += Window_PreviewKeyDown;
                System.Diagnostics.Debug.WriteLine("Window_PreviewKeyDown 이벤트 연결 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("InitializeFilters 에러: " + ex.Message);
            }
        }

        // 컨텍스트 메뉴 초기화 (새로 추가 - 선택사항)
        private void InitializeContextMenu()
        {
            var contextMenu = new System.Windows.Controls.ContextMenu();

            // 선택 삭제 메뉴
            var deleteSelectedMenuItem = new System.Windows.Controls.MenuItem();
            deleteSelectedMenuItem.Header = "선택한 항목 삭제";
            deleteSelectedMenuItem.Click += (s, e) => DeleteSelectedLogs();
            contextMenu.Items.Add(deleteSelectedMenuItem);

            // 구분선
            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            // 전체 선택 메뉴
            var selectAllMenuItem = new System.Windows.Controls.MenuItem();
            selectAllMenuItem.Header = "전체 선택 (Ctrl+A)";
            selectAllMenuItem.Click += (s, e) => SelectAllLogs();
            contextMenu.Items.Add(selectAllMenuItem);

            // 선택 해제 메뉴
            var deselectMenuItem = new System.Windows.Controls.MenuItem();
            deselectMenuItem.Header = "선택 해제 (Esc)";
            deselectMenuItem.Click += (s, e) => DeselectAllLogs();
            contextMenu.Items.Add(deselectMenuItem);

            // 구분선
            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            // 복사 메뉴
            var copyMenuItem = new System.Windows.Controls.MenuItem();
            copyMenuItem.Header = "선택한 로그 복사";
            copyMenuItem.Click += (s, e) => CopySelectedLogs();
            contextMenu.Items.Add(copyMenuItem);

            // DataGrid에 컨텍스트 메뉴 연결
            if (dgLogEntries != null)
            {
                dgLogEntries.ContextMenu = contextMenu;
            }
        }

        // 선택한 로그를 클립보드에 복사하는 메서드 (새로 추가)
        private void CopySelectedLogs()
        {
            try
            {
                if (dgLogEntries.SelectedItems.Count == 0)
                    return;

                var selectedLogs = new System.Text.StringBuilder();
                foreach (var item in dgLogEntries.SelectedItems)
                {
                    var logEntry = item as LogEntry;
                    if (logEntry != null)
                    {
                        selectedLogs.AppendLine(FormatLogEntry(logEntry));
                    }
                }

                if (selectedLogs.Length > 0)
                {
                    System.Windows.Clipboard.SetText(selectedLogs.ToString());

                    int count = dgLogEntries.SelectedItems.Count;
                    string message = count == 1
                        ? "로그 1개가 클립보드에 복사되었습니다."
                        : string.Format("로그 {0}개가 클립보드에 복사되었습니다.", count);

                    ShowTempMessage(message, 3000);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("클립보드 복사 실패: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 상태바에 임시 메시지 표시 (새로 추가)
        private void ShowStatusMessage(string message, int durationMs = 3000)
        {
            if (txtStats != null)
            {
                string originalText = txtStats.Text;
                txtStats.Text = message;
                txtStats.FontWeight = System.Windows.FontWeights.Bold;

                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(durationMs);
                timer.Tick += (s, e) =>
                {
                    txtStats.Text = originalText;
                    txtStats.FontWeight = System.Windows.FontWeights.Normal;
                    timer.Stop();
                };
                timer.Start();
            }
        }

        // 키보드 단축키 초기화 메서드 (새로 추가)
        private void InitializeKeyboardShortcuts()
        {
            // UserControl 레벨에서 키 이벤트 처리
            this.PreviewKeyDown += Window_PreviewKeyDown;

            // F5 키로 새로고침 (기존 btnRefresh_Click 활용)
            this.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.F5)
                {
                    // btnRefresh_Click 이벤트 직접 호출
                    btnRefresh_Click(null, null);
                    e.Handled = true;
                }
            };
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

                            for (int i = 5; i < parts.Length; i++)
                            {
                                if (!string.IsNullOrEmpty(parts[i].Trim()))
                                {
                                    message += " " + parts[i].Trim();
                                }
                            }

                            var extractedMethod = ExtractMethodFromMessage(message);
                            if (!string.IsNullOrEmpty(extractedMethod) && string.IsNullOrEmpty(method))
                            {
                                method = extractedMethod;
                            }

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
                return null;
            }

            return null;
        }

        /// <summary>
        /// 메시지에서 메서드 이름을 추출
        /// </summary>
        /// <param name="message">로그 메시지</param>
        /// <returns>추출된 메서드 이름</returns>
        private string ExtractMethodFromMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "";

            try
            {
                var methodPatterns = new string[]
                {
            " 호출", " 실행", " 완료", " 시작", " 종료",
            " called", " executed", " completed", " started", " finished",
            ":", "() ", "()", " - "
                };

                foreach (var pattern in methodPatterns)
                {
                    int index = message.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                    if (index > 0)
                    {
                        string beforePattern = message.Substring(0, index).Trim();
                        string[] words = beforePattern.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                        if (words.Length > 0)
                        {
                            string lastWord = words[words.Length - 1];
                            if (IsValidMethodName(lastWord))
                            {
                                return lastWord;
                            }
                        }
                    }
                }

                string[] messageWords = message.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (messageWords.Length > 0)
                {
                    string firstWord = messageWords[0];
                    if (IsValidMethodName(firstWord))
                    {
                        return firstWord;
                    }
                }
            }
            catch
            {
                return "";
            }

            return "";
        }

        /// <summary>
        /// 유효한 메서드명 패턴인지 확인
        /// </summary>
        /// <param name="word">확인할 단어</param>
        /// <returns>유효한 메서드명인지 여부</returns>
        private bool IsValidMethodName(string word)
        {
            if (string.IsNullOrEmpty(word) || word.Length < 3)
                return false;

            if (!char.IsLetter(word[0]))
                return false;

            word = word.TrimEnd('(', ')');

            foreach (char c in word)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            if (word.Length > 50)
                return false;

            return true;
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
                // 선택된 항목 개수 업데이트 추가
                UpdateSelectedCount();

                // 기존 코드 유지
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

        // 선택 항목 개수 업데이트 메서드 (새로 추가)
        private void UpdateSelectedCount()
        {
            try
            {
                if (dgLogEntries != null)
                {
                    int selectedCount = dgLogEntries.SelectedItems.Count;

                    // 상태바가 없으므로 툴팁으로만 표시
                    if (btnDeleteSelected != null)
                    {
                        btnDeleteSelected.IsEnabled = selectedCount > 0;

                        if (selectedCount > 0)
                        {
                            btnDeleteSelected.ToolTip = string.Format("선택한 {0}개 로그 삭제 (Delete 키)", selectedCount);
                        }
                        else
                        {
                            btnDeleteSelected.ToolTip = "삭제할 로그를 먼저 선택하세요";
                        }
                    }

                    // 통계 텍스트에 선택 개수 표시 (옵션)
                    if (txtStats != null && selectedCount > 0)
                    {
                        var stats = txtStats.Text;
                        if (!stats.Contains("선택:"))
                        {
                            txtStats.Text = stats + string.Format(" | 선택: {0}개", selectedCount);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 업데이트 실패시 무시
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
                    txtLogCount.Text = string.Format("로그 수: {0}", _filteredLogEntries.Count);
                }

                if (txtLastUpdate != null)
                {
                    txtLastUpdate.Text = string.Format("마지막 업데이트: {0:HH:mm:ss}", DateTime.Now);
                }

                UpdateLogStatistics();
                UpdateSelectedCount(); // 선택 개수 업데이트 추가
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

        #region Delete Selected Logs
        /// <summary>
        /// 선택된 로그 항목만 삭제
        /// </summary>
        private void DeleteSelectedLogs()
        {
            System.Diagnostics.Debug.WriteLine("===== DeleteSelectedLogs 메서드 시작 =====");

            try
            {
                // 선택된 항목이 없는지 확인
                if (dgLogEntries.SelectedItems == null || dgLogEntries.SelectedItems.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("선택된 항목이 없음");
                    MessageBox.Show("삭제할 로그를 선택해주세요.",
                                  "선택 없음",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                    return;
                }

                int selectedCount = dgLogEntries.SelectedItems.Count;
                System.Diagnostics.Debug.WriteLine(string.Format("선택된 항목 수: {0}", selectedCount));

                string message = selectedCount == 1
                    ? "선택한 로그 1개를 삭제하시겠습니까?"
                    : string.Format("선택한 로그 {0}개를 삭제하시겠습니까?", selectedCount);

                var result = MessageBox.Show(
                    message + "\n\n이 작업은 메모리에서만 삭제됩니다.",
                    "로그 삭제 확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Debug.WriteLine("사용자가 Yes를 선택함");

                    // 삭제할 항목들을 리스트로 복사
                    var itemsToDelete = new List<LogEntry>();
                    foreach (var item in dgLogEntries.SelectedItems)
                    {
                        var logEntry = item as LogEntry;
                        if (logEntry != null)
                        {
                            itemsToDelete.Add(logEntry);
                            System.Diagnostics.Debug.WriteLine(string.Format("삭제 대상: {0} - {1}",
                                logEntry.TimeStamp, logEntry.Message));
                        }
                    }

                    System.Diagnostics.Debug.WriteLine(string.Format("삭제 전: _filteredLogEntries={0}, _logEntries={1}",
                        _filteredLogEntries.Count, _logEntries.Count));

                    // UI 스레드에서 실행
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var logEntry in itemsToDelete)
                        {
                            _filteredLogEntries.Remove(logEntry);
                            _logEntries.Remove(logEntry);
                        }

                        UpdateUI();
                        dgLogEntries.Items.Refresh();
                    });

                    System.Diagnostics.Debug.WriteLine(string.Format("삭제 후: _filteredLogEntries={0}, _logEntries={1}",
                        _filteredLogEntries.Count, _logEntries.Count));

                    string completeMessage = selectedCount == 1
                        ? "선택한 로그가 삭제되었습니다."
                        : string.Format("선택한 로그 {0}개가 삭제되었습니다.", selectedCount);

                    MessageBox.Show(completeMessage, "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("사용자가 No를 선택함");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DeleteSelectedLogs 에러: " + ex.ToString());
                MessageBox.Show("선택 로그 삭제 실패: " + ex.Message,
                              "오류",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }

            System.Diagnostics.Debug.WriteLine("===== DeleteSelectedLogs 메서드 종료 =====");
        }

        /// <summary>
        /// 로그 파일을 현재 메모리 상태로 업데이트
        /// </summary>
        private void UpdateLogFile()
        {
            try
            {
                if (!File.Exists(_currentLogFilePath))
                    return;

                // 임시 파일 경로
                string tempFile = _currentLogFilePath + ".tmp";

                // 남은 로그들을 임시 파일에 기록
                using (var writer = new StreamWriter(tempFile, false))
                {
                    lock (_lockObject)
                    {
                        foreach (var logEntry in _logEntries.OrderBy(e => e.TimeStamp))
                        {
                            // 원본 로그 형식으로 재구성
                            string logLine = FormatLogEntry(logEntry);
                            writer.WriteLine(logLine);
                        }
                    }
                }

                // 원본 파일을 임시 파일로 교체
                File.Delete(_currentLogFilePath);
                File.Move(tempFile, _currentLogFilePath);
            }
            catch (Exception ex)
            {
                // 파일 업데이트 실패시 메모리 상태만 유지
                System.Diagnostics.Debug.WriteLine("로그 파일 업데이트 실패: " + ex.Message);
            }
        }

        /// <summary>
        /// LogEntry를 원본 로그 형식 문자열로 변환
        /// </summary>
        private string FormatLogEntry(LogEntry logEntry)
        {
            // 원본 로그 형식: [2025-06-19 16:30:45.123] [INFO] [Teaching] [SaveCurrentData] Message
            string timestamp = logEntry.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formattedLog = string.Format("[{0}] [{1}] [{2}] [{3}] {4}",
                timestamp, logEntry.Level, logEntry.Module, logEntry.Method, logEntry.Message);

            // 예외 정보가 있으면 추가
            if (!string.IsNullOrEmpty(logEntry.Exception))
            {
                formattedLog += "\nException: " + logEntry.Exception;
            }

            return formattedLog;
        }

        /// <summary>
        /// 선택 삭제 버튼 클릭 이벤트 핸들러
        /// </summary>
        private void btnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("btnDeleteSelected_Click 이벤트 발생!");
            DeleteSelectedLogs();
        }


        /// <summary>
        /// Delete 키 눌림 이벤트 핸들러
        /// </summary>
        private void dgLogEntries_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                System.Diagnostics.Debug.WriteLine("Delete 키 눌림!");
                DeleteSelectedLogs();
                e.Handled = true;
            }
        }
        #endregion

        #region Selection Management
        /// <summary>
        /// 전체 선택 기능
        /// </summary>
        private void SelectAllLogs()
        {
            if (dgLogEntries != null)
            {
                dgLogEntries.SelectAll();
                UpdateSelectedCount();
            }
        }


        /// <summary>
        /// 선택 해제 기능
        /// </summary>
        private void DeselectAllLogs()
        {
            if (dgLogEntries != null)
            {
                dgLogEntries.UnselectAll();
                UpdateSelectedCount();
            }
        }

        /// <summary>
        /// Ctrl+A 키 조합 처리
        /// </summary>
        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.A &&
                System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                System.Diagnostics.Debug.WriteLine("Ctrl+A 눌림!");
                SelectAllLogs();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                System.Diagnostics.Debug.WriteLine("ESC 키 눌림!");
                DeselectAllLogs();
                e.Handled = true;
            }
        }
        #endregion
    }
}