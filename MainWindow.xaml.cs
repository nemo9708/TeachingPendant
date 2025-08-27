using System;
using System.ComponentModel;
using System.IO.Ports; // 시리얼 포트 사용을 위해 추가
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Controls;
using TeachingPendant.Alarm;
using TeachingPendant.HardwareControllers;
using TeachingPendant.Manager;
using TeachingPendant.RecipeSystem.Test;
using TeachingPendant.Safety;
using TeachingPendant.UserManagement.Models;
using TeachingPendant.UserManagement.Services;
using TeachingPendant.VirtualKeyboard;
using TeachingPendant.Logging;


namespace TeachingPendant
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private bool _isClosingInProgress = false;

        // DTP7H 통신 인스턴스
        private DTP7HCommunication _dtp7h = new DTP7HCommunication();

        // ComSettingsWindow에서 설정된 SerialPort 객체를 공유받아 저장하는 변수
        private SerialPort sharedSerialPort;

        public MainWindow()
        {
            InitializeComponent();
            SetupFullScreenWithCloseButton();
            InitializeDTP7HController();
            InitializeTimer();

            // 가상 키보드 매니저 초기화
            VirtualKeyboardManager.Initialize(this);
            InitializeLoginStatus();
            _ = InitializeUserManagerAsync();
            this.KeyDown += MainWindow_KeyDown;
            this.Focusable = true;
            this.Focus();
            _ = LoadApplicationDataAsync();

            if (txtAlarmMessage != null)
            {
                AlarmMessageManager.SetAlarmTextBlock(txtAlarmMessage);
            }
            InitializeSafetySystem();
            UpdateRadioButtonsForCurrentMode();
            GlobalModeManager.ModeChanged += GlobalModeManager_ModeChanged;
            this.Closed += MainWindow_Closed;
        }

        /// <summary>
        /// 전체화면 설정 (X버튼 유지)
        /// </summary>
        private void SetupFullScreenWithCloseButton()
        {
            try
            {
                // 1. 창 상태를 최대화로 설정
                this.WindowState = WindowState.Maximized;

                // 2. 창 크기 조절 방지 (하지만 X버튼은 유지)
                this.ResizeMode = ResizeMode.CanMinimize; // 최소화와 닫기는 가능, 크기조절 불가

                // 3. 작업표시줄 위에 표시 (선택사항)
                this.Topmost = false; // true로 하면 항상 최상위에 표시

                // 4. 창 시작 위치
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                System.Diagnostics.Debug.WriteLine("[MainWindow] 전체화면 모드 설정 완료");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_INITIALIZED, "전체화면 모드로 시작됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] 전체화면 설정 오류: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"전체화면 설정 오류: {ex.Message}");
            }
        }

        #region DTP-7H Hardware Control Fields
        // DTP-7H 하드웨어 제어 인스턴스
        private DTP7HCommunication _dtp7HController;

        private void InitializeDTP7HController()
        {
            try
            {
                _dtp7HController = new DTP7HCommunication();
                System.Diagnostics.Debug.WriteLine("[MainWindow] DTP-7H controller initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] DTP-7H controller initialization failed: {ex.Message}");
            }
        }
        #endregion

        #region Login System
        /// <summary>
        /// Login 버튼 클릭 이벤트
        /// </summary>
        private void Login_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Login button clicked");

                // 이미 로그인된 상태면 로그아웃 처리
                if (UserSession.IsLoggedIn)
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Processing logout");
                    UserSession.Logout("User requested logout");
                    UpdateLoginStatus();
                    AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "You have been logged out.");
                    return;
                }

                // 로그인 창 열기
                var loginWindow = new LoginWindow();
                loginWindow.Owner = this; // 부모 창 설정
                loginWindow.ShowDialog(); // 모달 창으로 표시

                // 로그인 상태 변경에 따른 UI 업데이트
                UpdateLoginStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error opening Login window: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.UI_ERROR, "Cannot open the login window.");
            }
        }

        /// <summary>
        /// 로그인 상태에 따른 UI 업데이트
        /// </summary>
        private void UpdateLoginStatus()
        {
            try
            {
                if (UserSession.IsLoggedIn)
                {
                    // 로그인됨 - 버튼 텍스트 변경
                    btnLogin.Content = $"Logout\n({UserSession.CurrentUser.UserName})";
                    btnLogin.Background = System.Windows.Media.Brushes.LightPink;
                    btnLogin.BorderBrush = System.Windows.Media.Brushes.Red;
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] UI Update: Logged in as - {UserSession.CurrentUser.UserName}");
                }
                else
                {
                    // 로그아웃됨 - 버튼 원래대로
                    btnLogin.Content = "Login";
                    btnLogin.Background = System.Windows.Media.Brushes.LightBlue;
                    btnLogin.BorderBrush = System.Windows.Media.Brushes.Blue;
                    System.Diagnostics.Debug.WriteLine("[MainWindow] UI Update: Logged out");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error updating login status UI: {ex.Message}");
            }
        }

        /// <summary>
        /// 앱 시작 시 로그인 상태 초기화
        /// </summary>
        private void InitializeLoginStatus()
        {
            try
            {
                // 기본적으로 로그아웃 상태로 시작
                UserSession.Logout();
                UpdateLoginStatus();
                System.Diagnostics.Debug.WriteLine("[MainWindow] Login status initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error initializing login status: {ex.Message}");
            }
        }
        #endregion

        /// <summary>
        /// UserManager 초기화
        /// </summary>
        private async Task InitializeUserManagerAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] ==========================================");
                System.Diagnostics.Debug.WriteLine("[MainWindow] UserManager initialization started");
                System.Diagnostics.Debug.WriteLine("[MainWindow] ==========================================");

                // UserManager 초기화 (기본 admin/admin123 계정 자동 생성됨)
                bool initSuccess = await UserManager.InitializeAsync();
                System.Diagnostics.Debug.WriteLine($"[MainWindow] UserManager initialization result: {initSuccess}");

                if (initSuccess)
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] UserManager initialized successfully");
                    var users = UserManager.GetAllUsers();
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Number of registered users: {users.Count}");
                    foreach (var user in users)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] User: {user.UserId} ({user.UserName}) - {user.Role}");
                    }
                    await CreateTestAccountIfNeeded();
                    System.Diagnostics.Debug.WriteLine("[MainWindow] UserManager initialization fully complete!");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] UserManager initialization failed!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] UserManager initialization error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Stack Trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 테스트 계정 생성 (test/1234)
        /// </summary>
        private async Task CreateTestAccountIfNeeded()
        {
            try
            {
                var testUser = UserManager.GetUserById("test");
                if (testUser == null)
                {
                    var result = await UserManager.CreateUserAsync("test", "Test Account", "1234", UserRole.Administrator);
                    if (result.IsSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine("[MainWindow] Test account created successfully: test/1234");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error creating test account: {ex.Message}");
            }
        }

        #region Safety System Integration
        /// <summary>
        /// 안전 시스템 초기화
        /// </summary>
        private void InitializeSafetySystem()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: SafetySystem initialization started");
                var delayTimer = new DispatcherTimer();
                delayTimer.Interval = TimeSpan.FromMilliseconds(500);
                delayTimer.Tick += (sender, e) =>
                {
                    delayTimer.Stop();
                    try
                    {
                        SafetySystem.Initialize();
                        SafetySystem.SafetyStatusChanged += SafetySystem_SafetyStatusChanged;
                        SafetySystem.EmergencyStopTriggered += SafetySystem_EmergencyStopTriggered;
                        SafetySystem.InterlockStatusChanged += SafetySystem_InterlockStatusChanged;
                        System.Diagnostics.Debug.WriteLine("MainWindow: SafetySystem delayed initialization complete");
                        TestInterlockSystemBasic();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("MainWindow: SafetySystem delayed initialization failed: " + ex.Message);
                        AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "Safety system initialization failed");
                    }
                };
                delayTimer.Start();
                System.Diagnostics.Debug.WriteLine("MainWindow: SafetySystem delayed initialization scheduled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: SafetySystem initialization failed: " + ex.Message);
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "Failed to initialize the safety system.");
            }
        }

        /// <summary>
        /// SafetySystem 안전 상태 변경 이벤트 핸들러
        /// </summary>
        private void SafetySystem_SafetyStatusChanged(object sender, SafetyStatusChangedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Safety status changed: " + e.OldStatus + " -> " + e.NewStatus);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    string statusMessage = "";
                    switch (e.NewStatus)
                    {
                        case SafetyStatus.Safe: statusMessage = "Safe State - All systems normal"; AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, statusMessage); break;
                        case SafetyStatus.Warning: statusMessage = "Warning State - Caution required"; AlarmMessageManager.ShowAlarm(Alarms.WARNING, statusMessage); break;
                        case SafetyStatus.Dangerous: statusMessage = "Dangerous State - Immediate check required"; AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, statusMessage); break;
                        case SafetyStatus.EmergencyStop: statusMessage = "Emergency Stop State"; AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, statusMessage); break;
                    }
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Error processing SafetySystem status change event: " + ex.Message);
            }
        }

        /// <summary>
        /// SafetySystem 비상정지 발생 이벤트 핸들러
        /// </summary>
        private void SafetySystem_EmergencyStopTriggered(object sender, EmergencyStopEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Emergency Stop triggered: " + e.Reason);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "Emergency Stop Triggered: " + e.Reason);
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Error processing Emergency Stop event: " + ex.Message);
            }
        }

        /// <summary>
        /// 인터록 상태 변경 이벤트 핸들러
        /// </summary>
        private void SafetySystem_InterlockStatusChanged(object sender, InterlockStatusChangedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Interlock status changed: " + e.DeviceName + " " + e.OldStatus + " -> " + e.NewStatus);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    string message = "";
                    string alarmCode = "";
                    switch (e.NewStatus)
                    {
                        case InterlockStatus.Closed: message = e.DeviceName + " interlock is now in a safe state."; alarmCode = Alarms.STATUS_UPDATE; break;
                        case InterlockStatus.Open: message = e.DeviceName + " interlock is open. Check immediately!"; alarmCode = Alarms.WARNING; break;
                        case InterlockStatus.SensorError: message = e.DeviceName + " interlock sensor has an error."; alarmCode = Alarms.SYSTEM_ERROR; break;
                        case InterlockStatus.Unknown: message = e.DeviceName + " interlock status is unknown."; alarmCode = Alarms.WARNING; break;
                    }
                    AlarmMessageManager.ShowAlarm(alarmCode, message);
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Error processing Interlock event: " + ex.Message);
            }
        }

        /// <summary>
        /// 기본 인터록 시스템 테스트
        /// </summary>
        private void TestInterlockSystemBasic()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Basic interlock system test started");
                string summary = SafetySystem.GetInterlockSystemSummary();
                System.Diagnostics.Debug.WriteLine("[Interlock Test] " + summary);
                SafetySystem.SetAllInterlocksSecure();
                bool chamber1Safe = SafetySystem.IsChamberSecure(1);
                System.Diagnostics.Debug.WriteLine("[Interlock Test] Chamber 1 secure state: " + chamber1Safe);
                bool canOperate = SafetySystem.IsSafeForRobotOperation();
                System.Diagnostics.Debug.WriteLine("[Interlock Test] Robot operation possible: " + canOperate);
                string diagnostics = SafetySystem.GetDiagnosticInfo();
                System.Diagnostics.Debug.WriteLine("[Interlock Diagnostics]\n" + diagnostics);
                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "Basic interlock system test complete");
                System.Diagnostics.Debug.WriteLine("MainWindow: Basic interlock system test complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Interlock test failed: " + ex.Message);
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "Interlock test failed: " + ex.Message);
            }
        }

        /// <summary>
        /// SafetySystem 정리
        /// </summary>
        private void CleanupSafetySystem()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: SafetySystem cleanup started");
                if (SafetySystem.IsInitialized)
                {
                    SafetySystem.SafetyStatusChanged -= SafetySystem_SafetyStatusChanged;
                    SafetySystem.EmergencyStopTriggered -= SafetySystem_EmergencyStopTriggered;
                    SafetySystem.InterlockStatusChanged -= SafetySystem_InterlockStatusChanged;
                    SafetySystem.Shutdown();
                }
                System.Diagnostics.Debug.WriteLine("MainWindow: SafetySystem cleanup complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Error during SafetySystem cleanup: " + ex.Message);
            }
        }
        #endregion

        private void InitializeTimer()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        /// <summary>
        /// 앱 시작 시 모든 데이터 로드
        /// </summary>
        private async Task LoadApplicationDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== MainWindow: Application data loading started ===");
                bool loadSuccess = await PersistentDataManager.LoadAllDataAsync();
                if (loadSuccess)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_INITIALIZED, "Application data loaded successfully");
                    System.Diagnostics.Debug.WriteLine("MainWindow: All data loaded successfully");
                }
                else
                {
                    AlarmMessageManager.ShowAlarm(Alarms.WARNING, "Some data could not be loaded, using defaults");
                    System.Diagnostics.Debug.WriteLine("MainWindow: Some data failed to load, using default values");
                }
                TeachingPendant.TeachingUI.Teaching.ShowPersistentDataStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error loading application data: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "Failed to load application data");
            }
        }

        /// <summary>
        /// 앱 종료 시 모든 데이터 저장
        /// </summary>
        protected override async void OnClosing(CancelEventArgs e)
        {
            if (!_isClosingInProgress)
            {
                e.Cancel = true;
                _isClosingInProgress = true;
                try
                {
                    System.Diagnostics.Debug.WriteLine("=== MainWindow: Application data saving started ===");
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_INITIALIZED, "Saving application data...");
                    bool saveSuccess = await PersistentDataManager.SaveAllDataAsync();
                    if (saveSuccess)
                    {
                        AlarmMessageManager.ShowAlarm(Alarms.POSITION_SAVED, "All data saved successfully");
                        System.Diagnostics.Debug.WriteLine("MainWindow: All data saved successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("MainWindow: Data save failed");
                    }
                    await PersistentDataManager.CreateBackupAsync();
                    if (_timer != null)
                    {
                        _timer.Stop();
                        _timer = null;
                    }
                    GlobalModeManager.ModeChanged -= GlobalModeManager_ModeChanged;
                    System.Diagnostics.Debug.WriteLine("Data saving completed, closing application...");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving application data: {ex.Message}");
                    var result = MessageBox.Show($"Failed to save application data: {ex.Message}\n\nDo you still want to exit?", "Save Error", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.No)
                    {
                        _isClosingInProgress = false;
                        return;
                    }
                }
                System.Diagnostics.Debug.WriteLine("Initiating final application shutdown...");
                await Task.Delay(500);
                CloseAllWindows();
                Application.Current.Shutdown(0);
            }
            base.OnClosing(e);
        }

        /// <summary>
        /// 모든 열린 창들을 강제로 닫기
        /// </summary>
        private void CloseAllWindows()
        {
            try
            {
                var windows = new System.Collections.Generic.List<Window>();
                foreach (Window window in Application.Current.Windows)
                {
                    if (window != this)
                    {
                        windows.Add(window);
                    }
                }
                foreach (var window in windows)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Closing window: {window.GetType().Name}");
                        window.Close();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error closing window {window.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CloseAllWindows: {ex.Message}");
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            txtDateTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        #region Window Event Handlers
        /// <summary>
        /// MainWindow가 로드될 때 이벤트 처리
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Window Loaded event started");

                // DTP-7H 하드웨어 상태 확인
                if (_dtp7HController != null)
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Checking DTP-7H controller status");
                }

                // 안전 시스템 상태 확인
                System.Diagnostics.Debug.WriteLine("[MainWindow] Checking SafetySystem status");

                // 사용자 세션 상태 확인
                if (UserSession.IsLoggedIn)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Current logged-in user: {UserSession.CurrentUser.UserName}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Currently logged out");
                }

                // 가상 키보드 매니저 초기화 (중요!)
                VirtualKeyboardManager.Initialize(this);
                System.Diagnostics.Debug.WriteLine("[MainWindow] VirtualKeyboardManager initialized successfully");

                // 가상 키보드 상태 확인
                // VirtualKeyboardManager.CheckStatus(); // 이 메서드가 없다면 주석 처리

                // 글로벌 모드 상태 확인
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Current global mode: {GlobalModeManager.CurrentMode}");

                // Alarm 메시지 시스템 상태 확인
                if (txtAlarmMessage != null)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_INITIALIZED, "MainWindow loaded successfully");
                }

                System.Diagnostics.Debug.WriteLine("[MainWindow] Window Loaded event complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error during Window Loaded event: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Error during MainWindow load: {ex.Message}");
            }
        }
        #endregion

        #region Navigation Button Clicks
        private void Movement_Click(object sender, RoutedEventArgs e) { OpenCommonFrame("Movement"); }
        private void Monitor_Click(object sender, RoutedEventArgs e) { OpenCommonFrame("Monitor"); }
        private void System_Click(object sender, RoutedEventArgs e) { OpenCommonFrame("System"); }
        private void Teaching_Click(object sender, RoutedEventArgs e) { OpenCommonFrame("Teaching"); }
        private void RecipeRunner_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenCommonFrame("RecipeRunner");
                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Recipe Runner screen has been opened.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Recipe Runner screen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AlarmMessageManager.ShowAlarm(Alarms.UI_ERROR, $"Failed to open Recipe Runner: {ex.Message}");
            }
        }
        private void FileLoad_Click(object sender, RoutedEventArgs e) { OpenCommonFrame("File Load"); }
        private void Setting_Click(object sender, RoutedEventArgs e) { OpenCommonFrame("Setting"); }
        private void ErrorLog_Click(object sender, RoutedEventArgs e) { OpenCommonFrame("Error Log"); }
        private void Help_Click(object sender, RoutedEventArgs e) { OpenCommonFrame("HELP"); }
        private void Option_Click(object sender, RoutedEventArgs e) { AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Option Clicked - To be implemented"); }

        /// <summary>
        /// 새로운 CommonFrame 창을 열고 메인 창을 숨깁니다.
        /// </summary>
        private void OpenCommonFrame(string screenName)
        {
            try
            {
                this.Hide();
                CommonFrame frame = new CommonFrame(screenName);
                frame.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening screen '{screenName}': {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AlarmMessageManager.ShowAlarm(Alarms.UI_ERROR, $"Failed to open {screenName}");
            }
            finally
            {
                this.Show();
                UpdateRadioButtonsForCurrentMode();
            }
        }
        #endregion

        #region Global Mode Management
        private void rbManual_Click(object sender, RoutedEventArgs e) { GlobalModeManager.SetMode(GlobalMode.Manual); }
        private void rbAuto_Click(object sender, RoutedEventArgs e) { GlobalModeManager.SetMode(GlobalMode.Auto); }
        private void rbEmg_Click(object sender, RoutedEventArgs e) { GlobalModeManager.SetMode(GlobalMode.Emergency); }
        private void GlobalModeManager_ModeChanged(object sender, ModeChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(UpdateRadioButtonsForCurrentMode));
        }
        private void UpdateRadioButtonsForCurrentMode()
        {
            switch (GlobalModeManager.CurrentMode)
            {
                case GlobalMode.Manual: rbManual.IsChecked = true; break;
                case GlobalMode.Auto: rbAuto.IsChecked = true; break;
                case GlobalMode.Emergency: rbEmg.IsChecked = true; break;
            }
        }
        #endregion

        /// <summary>
        /// MainWindow가 닫힐 때 이벤트 처리
        /// </summary>
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainWindow_Closed event triggered");
                CleanupSafetySystem();
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer = null;
                }
                GlobalModeManager.ModeChanged -= GlobalModeManager_ModeChanged;
                System.Diagnostics.Debug.WriteLine("MainWindow cleanup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error in MainWindow_Closed: " + ex.Message);
            }
        }

        private void Mapping_Click(object sender, RoutedEventArgs e) { OpenCommonFrame("Mapping"); }

        #region Recipe System Test Key Events
        /// <summary>
        /// 키 다운 이벤트 핸들러 - 레시피 시스템 테스트 키 처리
        /// </summary>
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.F9)
                {
                    System.Diagnostics.Debug.WriteLine("=== F9 key pressed! Simple Recipe Test ===");
                    _ = RunSimpleRecipeTestAsync();
                }
                else if (e.Key == Key.F8)
                {
                    System.Diagnostics.Debug.WriteLine("=== F8 key pressed! Recipe System Test ===");
                    _ = RunRecipeSystemTestAsync();
                }
                else if (e.Key == Key.F12)
                {
                    System.Diagnostics.Debug.WriteLine("=== F12 key pressed! Movement Physics Test ===");
                }
                // Ctrl + L로 로그 뷰어 열기
                else if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    ShowErrorLogViewer();
                    e.Handled = true;
                }
                // 다른 기존 키보드 단축키들도 여기에...
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "MainWindow_KeyDown", "키보드 이벤트 처리 실패", ex);
                System.Diagnostics.Debug.WriteLine($"MainWindow: KeyDown event error: {ex.Message}");
            }
        }

        /// <summary>
        /// 레시피 시스템 전체 테스트 실행 메서드
        /// </summary>
        private async Task RunRecipeSystemTestAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Starting complete recipe system test ===");
                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "Starting complete recipe system test...");
                bool testResult = await RecipeSystemTestHelper.RunCompleteSystemTestAsync();
                if (testResult)
                {
                    System.Diagnostics.Debug.WriteLine("✅ All recipe system tests passed!");
                    AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "Recipe system test complete - All tests passed");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("❌ Some recipe system tests failed");
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "Recipe system test complete - Some tests failed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Recipe system test error: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Recipe system test error: {ex.Message}");
            }
        }

        /// <summary>
        /// 레시피 시스템 간단 테스트 실행 메서드
        /// </summary>
        private async Task RunSimpleRecipeTestAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Starting simple recipe test ===");
                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "Starting simple recipe test...");
                bool testResult = await RecipeSystemTestHelper.RunSimpleRecipeTestAsync();
                if (testResult)
                {
                    System.Diagnostics.Debug.WriteLine("✅ Simple recipe test passed!");
                    AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "Simple recipe test complete - Success");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("❌ Simple recipe test failed");
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "Simple recipe test complete - Failed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Simple recipe test error: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Simple recipe test error: {ex.Message}");
            }
        }
        #endregion

        #region Hardware Control Button Event Handlers

        /// <summary>
        /// LED Blue 버튼 클릭 이벤트 핸들러
        /// </summary>
        private void LEDBlue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] LED Blue button clicked");

                if (_dtp7HController != null)
                {
                    bool success = _dtp7HController.SendLEDCommandDirect(
                        LEDPosition.RightLED1,
                        LEDColor.Blue,
                        1000 // 1초간 켜짐
                    );

                    if (success)
                    {
                        AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "LED Blue command sent successfully");
                    }
                    else
                    {
                        AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "Failed to send LED Blue command");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] DTP-7H controller is not initialized");
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "DTP-7H controller is not initialized");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error on LED Blue button click: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"LED Blue error: {ex.Message}");
            }
        }

        /// <summary>
        /// LED Red 버튼 클릭 이벤트 핸들러
        /// </summary>
        private void LEDRed_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] LED Red button clicked");

                if (_dtp7HController != null)
                {
                    bool success = _dtp7HController.SendLEDCommandDirect(
                        LEDPosition.RightLED1,
                        LEDColor.Red,
                        1000 // 1초간 켜짐
                    );

                    if (success)
                    {
                        AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "LED Red command sent successfully");
                    }
                    else
                    {
                        AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "Failed to send LED Red command");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] DTP-7H controller is not initialized");
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "DTP-7H controller is not initialized");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error on LED Red button click: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"LED Red error: {ex.Message}");
            }
        }

        /// <summary>
        /// LED All 버튼 클릭 이벤트 핸들러
        /// </summary>
        private void LEDAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] LED All button clicked");

                if (_dtp7HController != null)
                {
                    bool success = _dtp7HController.SendLEDCommandDirect(
                        LEDPosition.RightLED1,
                        LEDColor.All,
                        1000 // 1초간 켜짐
                    );

                    if (success)
                    {
                        AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "LED All command sent successfully");
                    }
                    else
                    {
                        AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "Failed to send LED All command");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] DTP-7H controller is not initialized");
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "DTP-7H controller is not initialized");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error on LED All button click: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"LED All error: {ex.Message}");
            }
        }

        /// <summary>
        /// Buzzer 버튼 클릭 이벤트 핸들러
        /// </summary>
        private void Buzzer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Buzzer button clicked");

                if (_dtp7HController != null)
                {
                    bool success = _dtp7HController.SendBuzzerCommandDirect(500); // 0.5초간 부저

                    if (success)
                    {
                        AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Buzzer command sent successfully");
                    }
                    else
                    {
                        AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "Failed to send Buzzer command");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] DTP-7H controller is not initialized");
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "DTP-7H controller is not initialized");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error on Buzzer button click: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Buzzer error: {ex.Message}");
            }
        }

        /// <summary>
        /// ComButton 버튼 클릭 이벤트 핸들러
        /// </summary>
        private void ComButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] ComButton clicked - opening COM settings window");

                if (_dtp7HController != null)
                {
                    // COM 포트 설정 창 열기
                    var settingsWindow = new TeachingPendant.Windows.ComPortSettingsWindow(_dtp7HController);
                    settingsWindow.Owner = this; // 부모 창 설정
                    settingsWindow.ShowDialog();

                    System.Diagnostics.Debug.WriteLine("[MainWindow] COM settings window closed");
                    AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "COM settings window was closed");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] DTP-7H controller is not initialized");
                    MessageBox.Show("DTP-7H controller is not initialized.", "Error",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "DTP-7H controller is not initialized");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error on ComButton click: {ex.Message}");
                MessageBox.Show($"Error opening COM settings window:\n{ex.Message}", "Error",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"COM settings window error: {ex.Message}");
            }
        }

        /// <summary>
        /// 가상 키보드 테스트 버튼 클릭 이벤트 핸들러
        /// </summary>
        private void TestKeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Test virtual keyboard button clicked");
                VirtualKeyboardManager.Toggle();
                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Toggled virtual keyboard");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Test virtual keyboard button error: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Virtual keyboard error: {ex.Message}");
            }
        }

        #endregion

        #region Resource Management
        /// <summary>
        /// MainWindow가 닫힐 때 DTP7H 리소스 해제
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] OnClosed event started");

                // DTP-7H 컨트롤러 정리
                if (_dtp7HController != null)
                {
                    _dtp7HController.Disconnect();
                    _dtp7HController = null;
                    System.Diagnostics.Debug.WriteLine("[MainWindow] DTP-7H controller cleaned up");
                }

                // 가상 키보드 정리 (필요한 경우)
                // VirtualKeyboardManager 자체적으로 정리됨

                // 타이머 정리
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer = null;
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Timer cleaned up");
                }

                System.Diagnostics.Debug.WriteLine("[MainWindow] Resource cleanup complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error during resource cleanup: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }
        #endregion

        // MainWindow.xaml.cs에 추가해야 할 누락된 이벤트 핸들러들

        #region Menu Event Handlers - 누락된 핸들러들
        /// <summary>
        /// About 메뉴 클릭
        /// </summary>
        private void About_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var aboutMessage = "TeachingPendant v1.0\n\n" +
                                  "웨이퍼 반송 로봇 제어 시스템\n" +
                                  "Copyright © 2025\n\n" +
                                  "Built with .NET Framework and WPF";

                MessageBox.Show(aboutMessage, "About TeachingPendant",
                               MessageBoxButton.OK, MessageBoxImage.Information);

                Logger.Info("MainWindow", "About_Click", "About 대화상자 표시됨");
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "About_Click", "About 대화상자 표시 실패", ex);
            }
        }

        /// <summary>
        /// Exit 메뉴 클릭
        /// </summary>
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("프로그램을 종료하시겠습니까?", "종료 확인",
                                           MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Logger.Info("MainWindow", "Exit_Click", "사용자가 프로그램 종료 선택");
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "Exit_Click", "프로그램 종료 처리 실패", ex);
                Application.Current.Shutdown(); // 오류가 있어도 강제 종료
            }
        }

        /// <summary>
        /// 새 레시피 메뉴 클릭
        /// </summary>
        private void NewRecipe_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] 새 레시피 생성");

                // 기존 레시피가 수정되었으면 저장할지 확인
                // TODO: 레시피 편집기와 연동하여 수정 여부 확인

                Logger.Info("MainWindow", "NewRecipe_Click", "새 레시피 생성 요청");
                AlarmMessageManager.ShowCustomMessage("새 레시피를 생성합니다", AlarmCategory.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "NewRecipe_Click", "새 레시피 생성 실패", ex);
            }
        }

        /// <summary>
        /// 레시피 열기 메뉴 클릭
        /// </summary>
        private void OpenRecipe_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] 레시피 열기");

                var openDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "레시피 파일 열기",
                    Filter = "Recipe Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = "json"
                };

                if (openDialog.ShowDialog() == true)
                {
                    var filePath = openDialog.FileName;
                    Logger.Info("MainWindow", "OpenRecipe_Click", $"레시피 파일 열기: {filePath}");
                    AlarmMessageManager.ShowCustomMessage($"레시피를 로드합니다: {System.IO.Path.GetFileName(filePath)}", AlarmCategory.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "OpenRecipe_Click", "레시피 열기 실패", ex);
            }
        }

        /// <summary>
        /// 레시피 저장 메뉴 클릭
        /// </summary>
        private void SaveRecipe_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] 레시피 저장");

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "레시피 파일 저장",
                    Filter = "Recipe Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = "json"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var filePath = saveDialog.FileName;
                    Logger.Info("MainWindow", "SaveRecipe_Click", $"레시피 파일 저장: {filePath}");
                    AlarmMessageManager.ShowCustomMessage($"레시피를 저장합니다: {System.IO.Path.GetFileName(filePath)}", AlarmCategory.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "SaveRecipe_Click", "레시피 저장 실패", ex);
            }
        }

        /// <summary>
        /// 로그 뷰어 메뉴 클릭
        /// </summary>
        private void Menu_LogViewer_Click(object sender, RoutedEventArgs e)
        {
            ShowErrorLogViewer();
        }

        /// <summary>
        /// 시스템 설정 메뉴 클릭
        /// </summary>
        private void Menu_SystemSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] 시스템 설정 열기");
                Logger.Info("MainWindow", "Menu_SystemSettings_Click", "시스템 설정 창 열기 요청");
                AlarmMessageManager.ShowCustomMessage("시스템 설정 기능은 구현 예정입니다", AlarmCategory.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "Menu_SystemSettings_Click", "시스템 설정 열기 실패", ex);
            }
        }

        /// <summary>
        /// 하드웨어 연결 메뉴 클릭
        /// </summary>
        private void Menu_HardwareConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] 하드웨어 연결 관리");
                Logger.Info("MainWindow", "Menu_HardwareConnection_Click", "하드웨어 연결 관리 요청");
                AlarmMessageManager.ShowCustomMessage("하드웨어 연결 관리 기능은 구현 예정입니다", AlarmCategory.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "Menu_HardwareConnection_Click", "하드웨어 연결 관리 실패", ex);
            }
        }

        /// <summary>
        /// 진단 도구 메뉴 클릭
        /// </summary>
        private void Menu_DiagnosticTools_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] 진단 도구 열기");
                Logger.Info("MainWindow", "Menu_DiagnosticTools_Click", "진단 도구 열기 요청");
                AlarmMessageManager.ShowCustomMessage("진단 도구 기능은 구현 예정입니다", AlarmCategory.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "Menu_DiagnosticTools_Click", "진단 도구 열기 실패", ex);
            }
        }

        /// <summary>
        /// 에러 로그 뷰어 표시 (ErrorLogViewer 통합용)
        /// </summary>
        private void ShowErrorLogViewer()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] 에러 로그 뷰어 열기");

                // 새 창에서 에러 로그 뷰어 열기
                var logViewerWindow = new Window
                {
                    Title = "TeachingPendant - 로그 뷰어",
                    Width = 1200,
                    Height = 800,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Icon = this.Icon // 메인 윈도우와 같은 아이콘 사용
                };

                // ErrorLogViewer UserControl을 창의 Content로 설정
                var errorLogViewer = new UI.Views.ErrorLogViewer();
                logViewerWindow.Content = errorLogViewer;

                // 창 표시
                logViewerWindow.Show();

                Logger.Info("MainWindow", "ShowErrorLogViewer", "에러 로그 뷰어 창 열림");
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "ShowErrorLogViewer", "에러 로그 뷰어 열기 실패", ex);
                AlarmMessageManager.ShowCustomMessage("로그 뷰어를 열 수 없습니다", AlarmCategory.Error);
            }
        }
        #endregion

        #region RecipeHub Integration
        /// <summary>
        /// RecipeHub 인스턴스
        /// </summary>
        private RecipeHub _recipeHub;

        /// <summary>
        /// RecipeHub 초기화 (MainWindow 로드 시 호출)
        /// </summary>
        private async void InitializeRecipeHub()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] RecipeHub 초기화 시작");

                // RecipeHub 인스턴스 가져오기
                _recipeHub = RecipeHub.Instance;

                // RecipeHub 이벤트 구독
                _recipeHub.StatusChanged += RecipeHub_StatusChanged;
                _recipeHub.StepExecutionStarted += RecipeHub_StepExecutionStarted;
                _recipeHub.StepExecutionCompleted += RecipeHub_StepExecutionCompleted;
                _recipeHub.ExecutionCompleted += RecipeHub_ExecutionCompleted;
                _recipeHub.ErrorOccurred += RecipeHub_ErrorOccurred;

                // RecipeHub 비동기 초기화
                var initSuccess = await _recipeHub.InitializeAsync();

                if (initSuccess)
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] RecipeHub 초기화 성공");
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_INITIALIZED, "레시피 시스템이 준비되었습니다");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] RecipeHub 초기화 실패");
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "레시피 시스템 초기화 실패");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] RecipeHub 초기화 오류: {ex.Message}");
                Logger.Error("MainWindow", "InitializeRecipeHub", "RecipeHub 초기화 실패", ex);
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "레시피 시스템 오류");
            }
        }

        /// <summary>
        /// RecipeHub 정리 (MainWindow 닫힐 때 호출)
        /// </summary>
        private void CleanupRecipeHub()
        {
            try
            {
                if (_recipeHub != null)
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] RecipeHub 정리 시작");

                    // 이벤트 구독 해제
                    _recipeHub.StatusChanged -= RecipeHub_StatusChanged;
                    _recipeHub.StepExecutionStarted -= RecipeHub_StepExecutionStarted;
                    _recipeHub.StepExecutionCompleted -= RecipeHub_StepExecutionCompleted;
                    _recipeHub.ExecutionCompleted -= RecipeHub_ExecutionCompleted;
                    _recipeHub.ErrorOccurred -= RecipeHub_ErrorOccurred;

                    // RecipeHub 리소스 정리
                    _recipeHub.Dispose();
                    _recipeHub = null;

                    System.Diagnostics.Debug.WriteLine("[MainWindow] RecipeHub 정리 완료");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] RecipeHub 정리 오류: {ex.Message}");
                Logger.Error("MainWindow", "CleanupRecipeHub", "RecipeHub 정리 실패", ex);
            }
        }
        #endregion

        #region RecipeHub Event Handlers
        /// <summary>
        /// RecipeHub 상태 변경 이벤트
        /// </summary>
        private void RecipeHub_StatusChanged(object sender, RecipeSystemStatusChangedEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    // 메인 UI에 레시피 상태 표시
                    UpdateRecipeStatusInUI(e.NewStatus);

                    System.Diagnostics.Debug.WriteLine($"[MainWindow] 레시피 상태 변경: {e.NewStatus}");
                });
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "RecipeHub_StatusChanged", "레시피 상태 변경 이벤트 처리 실패", ex);
            }
        }

        /// <summary>
        /// RecipeHub 스텝 실행 시작 이벤트
        /// </summary>
        private void RecipeHub_StepExecutionStarted(object sender, RecipeStepExecutionEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    // UI에 현재 실행 중인 스텝 표시
                    var statusMessage = $"실행 중: {e.Step.Description} (Step {e.StepIndex + 1})";
                    AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, statusMessage);

                    System.Diagnostics.Debug.WriteLine($"[MainWindow] 스텝 실행 시작: {e.Step.Description}");
                });
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "RecipeHub_StepExecutionStarted", "스텝 실행 시작 이벤트 처리 실패", ex);
            }
        }

        /// <summary>
        /// RecipeHub 스텝 실행 완료 이벤트
        /// </summary>
        private void RecipeHub_StepExecutionCompleted(object sender, RecipeStepExecutionEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (e.Success == true)
                    {
                        var statusMessage = $"완료: {e.Step.Description}";
                        AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, statusMessage);
                    }
                    else
                    {
                        var statusMessage = $"실패: {e.Step.Description}";
                        AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, statusMessage);
                    }

                    System.Diagnostics.Debug.WriteLine($"[MainWindow] 스텝 실행 완료: {e.Step.Description} - {(e.Success == true ? "성공" : "실패")}");
                });
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "RecipeHub_StepExecutionCompleted", "스텝 실행 완료 이벤트 처리 실패", ex);
            }
        }

        /// <summary>
        /// RecipeHub 실행 완료 이벤트
        /// </summary>
        private void RecipeHub_ExecutionCompleted(object sender, RecipeExecutionCompletedEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (e.Success)
                    {
                        AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "레시피 실행이 성공적으로 완료되었습니다");
                        System.Diagnostics.Debug.WriteLine("[MainWindow] 레시피 실행 성공");
                    }
                    else
                    {
                        var errorMessage = string.IsNullOrEmpty(e.ErrorMessage) ? "알 수 없는 오류" : e.ErrorMessage;
                        AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"레시피 실행 실패: {errorMessage}");
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] 레시피 실행 실패: {errorMessage}");
                    }

                    // UI 상태 업데이트
                    UpdateRecipeStatusInUI(_recipeHub.Status);
                });
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "RecipeHub_ExecutionCompleted", "실행 완료 이벤트 처리 실패", ex);
            }
        }

        /// <summary>
        /// RecipeHub 오류 발생 이벤트
        /// </summary>
        private void RecipeHub_ErrorOccurred(object sender, RecipeErrorEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    var errorMessage = $"레시피 오류 [{e.ErrorCode}]: {e.ErrorMessage}";
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, errorMessage);

                    System.Diagnostics.Debug.WriteLine($"[MainWindow] 레시피 오류: {errorMessage}");

                    // 심각한 오류인 경우 추가 처리
                    if (e.ErrorCode.Contains("HARDWARE") || e.ErrorCode.Contains("SAFETY"))
                    {
                        // 긴급 정지 또는 추가 안전 조치
                        HandleCriticalRecipeError(e);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "RecipeHub_ErrorOccurred", "오류 이벤트 처리 실패", ex);
            }
        }
        #endregion

        #region Recipe UI Helper Methods
        /// <summary>
        /// UI에 레시피 상태 업데이트
        /// </summary>
        /// <param name="status">새로운 상태</param>
        private void UpdateRecipeStatusInUI(RecipeSystemStatus status)
        {
            try
            {
                // 상태에 따른 UI 업데이트
                switch (status)
                {
                    case RecipeSystemStatus.Idle:
                        // 버튼 상태 등 UI 업데이트
                        break;

                    case RecipeSystemStatus.Loading:
                        // 로딩 표시
                        break;

                    case RecipeSystemStatus.Ready:
                        // 실행 준비됨 표시
                        break;

                    case RecipeSystemStatus.Executing:
                        // 실행 중 표시
                        break;

                    case RecipeSystemStatus.Paused:
                        // 일시정지 표시
                        break;

                    case RecipeSystemStatus.Error:
                        // 오류 상태 표시
                        break;

                    case RecipeSystemStatus.Completed:
                        // 완료 상태 표시
                        break;
                }

                // 알람 메시지에 상태 정보 표시
                var statusText = GetStatusDisplayText(status);
                if (!string.IsNullOrEmpty(statusText))
                {
                    txtAlarmMessage.Text = $"레시피: {statusText}";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "UpdateRecipeStatusInUI", "레시피 상태 UI 업데이트 실패", ex);
            }
        }

        /// <summary>
        /// 상태를 UI 표시용 텍스트로 변환
        /// </summary>
        /// <param name="status">레시피 상태</param>
        /// <returns>표시용 텍스트</returns>
        private string GetStatusDisplayText(RecipeSystemStatus status)
        {
            switch (status)
            {
                case RecipeSystemStatus.Idle: return "대기 중";
                case RecipeSystemStatus.Loading: return "로딩 중";
                case RecipeSystemStatus.Ready: return "실행 준비됨";
                case RecipeSystemStatus.Executing: return "실행 중";
                case RecipeSystemStatus.Paused: return "일시정지";
                case RecipeSystemStatus.Error: return "오류";
                case RecipeSystemStatus.Completed: return "완료";
                default: return "";
            }
        }

        /// <summary>
        /// 치명적인 레시피 오류 처리
        /// </summary>
        /// <param name="errorArgs">오류 정보</param>
        private void HandleCriticalRecipeError(RecipeErrorEventArgs errorArgs)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] 치명적 레시피 오류 처리: {errorArgs.ErrorCode}");

                // 하드웨어 관련 오류
                if (errorArgs.ErrorCode.Contains("HARDWARE"))
                {
                    // 로봇 긴급 정지
                    if (_robotController != null)
                    {
                        Task.Run(async () => await _robotController.StopAsync());
                    }
                }

                // 안전 시스템 관련 오류  
                if (errorArgs.ErrorCode.Contains("SAFETY"))
                {
                    // 전체 시스템 안전 모드 전환
                    GlobalModeManager.SetMode(GlobalMode.Emergency);
                }

                // 사용자에게 중요 알림
                MessageBox.Show(
                    $"심각한 오류가 발생했습니다.\n\n" +
                    $"오류 코드: {errorArgs.ErrorCode}\n" +
                    $"오류 메시지: {errorArgs.ErrorMessage}\n\n" +
                    $"시스템을 점검해주세요.",
                    "치명적 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "HandleCriticalRecipeError", "치명적 오류 처리 실패", ex);
            }
        }

        /// <summary>
        /// 테스트용 레시피 실행 (F9 키 등에서 호출)
        /// </summary>
        private async void TestRecipeExecution()
        {
            try
            {
                if (_recipeHub == null)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "레시피 시스템이 초기화되지 않았습니다");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[MainWindow] 테스트 레시피 실행");

                // 간단한 테스트 레시피 생성
                var testRecipe = CreateTestRecipe();

                // 레시피 로드
                var loadSuccess = await _recipeHub.LoadRecipeAsync(testRecipe);
                if (!loadSuccess)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "테스트 레시피 로드 실패");
                    return;
                }

                // 레시피 실행
                var executeSuccess = await _recipeHub.StartExecutionAsync();
                if (executeSuccess)
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] 테스트 레시피 실행 시작됨");
                }
                else
                {
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "테스트 레시피 실행 실패");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "TestRecipeExecution", "테스트 레시피 실행 실패", ex);
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "테스트 레시피 실행 중 오류 발생");
            }
        }

        /// <summary>
        /// 간단한 테스트 레시피 생성
        /// </summary>
        /// <returns>테스트용 TransferRecipe</returns>
        private TransferRecipe CreateTestRecipe()
        {
            var testRecipe = new TransferRecipe("테스트 레시피", "RecipeHub 테스트용 레시피");

            // 몇 개의 기본 스텝 추가
            testRecipe.AddStep(new RecipeStep
            {
                StepNumber = 1,
                Type = StepType.Move,
                Description = "안전 위치로 이동",
                TargetPosition = new Position(100, 0, 50),
                Speed = 30,
                TeachingGroup = "Group1",
                LocationName = "P1"
            });

            testRecipe.AddStep(new RecipeStep
            {
                StepNumber = 2,
                Type = StepType.Pick,
                Description = "웨이퍼 픽업",
                TargetPosition = new Position(120, 45, 30),
                Speed = 20,
                TeachingGroup = "Group1",
                LocationName = "P2"
            });

            testRecipe.AddStep(new RecipeStep
            {
                StepNumber = 3,
                Type = StepType.Place,
                Description = "웨이퍼 배치",
                TargetPosition = new Position(150, 90, 40),
                Speed = 25,
                TeachingGroup = "Group1",
                LocationName = "P3"
            });

            return testRecipe;
        }
        #endregion
    }
}