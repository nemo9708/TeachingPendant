using System;
using System.Collections.Generic;
using System.ComponentModel; // ← 이것 추가!
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading; // ← 이것 추가!
using TeachingPendant.Alarm;
using TeachingPendant.MovementUI;
using TeachingPendant.SetupUI;
using TeachingPendant.TeachingUI;
using TeachingPendant.Manager;
using TeachingPendant.MonitorUI;
using TeachingPendant.UserManagement.Models;   // UserRole, User
using TeachingPendant.UserManagement.Services; // UserSession  
using TeachingPendant.Logging;
using TeachingPendant.RecipeSystem.Models;     // TransferRecipe를 위해 추가
using TeachingPendant.RecipeSystem.UI.Views;

namespace TeachingPendant
{
    public partial class CommonFrame : Window
    {
        #region Fields
        private string _currentScreenName;
        private readonly Dictionary<string, Func<UserControl>> _screenFactories;
        private DispatcherTimer _timer; // ← 이것 추가!
        #endregion

        #region Constructor
        public CommonFrame(string screenName)
        {
            InitializeComponent();
            _currentScreenName = screenName;
            _screenFactories = InitializeScreenFactories();
            InitializeUI();
            SubscribeToEvents();
            UpdateBottomButtonsForCurrentScreen();
            LoadScreenContent(screenName);
            this.Closing += CommonFrame_Closing;
            AlarmMessageManager.ShowAlarm(Alarms.POSITION_LOADED, $"{screenName} screen loaded");
        }

        private Dictionary<string, Func<UserControl>> InitializeScreenFactories()
        {
            return new Dictionary<string, Func<UserControl>>
    {
        { "Movement", () => new MovementUI.Movement() },
        { "Teaching", () => new TeachingPendant.TeachingUI.Teaching() },
        { "Monitor", () => new MonitorUI.Monitor() },
        { "Setting", () => new SetupUI.Setup() },
        { "Mapping", () => CreateMappingPlaceholder() },
        
        // Recipe 시스템 화면
        { "RecipeRunner", () => new TeachingPendant.RecipeSystem.UI.Views.RecipeRunner() },
        { "RecipeEditor", () => CreateRecipeEditor() },
        
        // ErrorLogViewer 추가 - 권한 확인 포함
        { "Error Log", () => CreateErrorLogViewer() },

        { "I/O", () => CreateIOControl() },
        { "System", () => CreatePlaceholderContent("System features will be implemented here") },
        { "File Load", () => CreatePlaceholderContent("File Load features will be implemented here") },
        { "HELP", () => CreatePlaceholderContent("Help features will be implemented here") }
    };
        }

        /// <summary>
        /// ErrorLogViewer 생성 (권한 확인 포함)
        /// </summary>
        private UserControl CreateErrorLogViewer()
        {
            try
            {
                // 권한 확인 - Error Log는 Operator 이상 접근 가능
                if (!UserSession.IsLoggedIn || UserSession.CurrentUser.Role < UserRole.Operator)
                {
                    Logger.Warning("CommonFrame", "CreateErrorLogViewer", "Error Log 접근 권한 부족");
                    return CreatePlaceholderContent(
                        "Error Log 접근 권한이 없습니다.\n\n" +
                        "Operator 이상의 권한이 필요합니다.\n" +
                        "관리자에게 문의하세요.");
                }

                // ErrorLogViewer 인스턴스 생성
                var errorLogViewer = new TeachingPendant.UI.Views.ErrorLogViewer();

                Logger.Info("CommonFrame", "CreateErrorLogViewer", "Error Log Viewer가 생성되었습니다.");
                AlarmMessageManager.ShowCustomMessage("Error Log Viewer가 로드되었습니다.", AlarmCategory.Information);

                return errorLogViewer;
            }
            catch (Exception ex)
            {
                Logger.Error("CommonFrame", "CreateErrorLogViewer", "Error Log Viewer 생성 실패", ex);
                AlarmMessageManager.ShowCustomMessage("Error Log Viewer 로드에 실패했습니다.", AlarmCategory.Error);

                // 오류 발생 시 대체 내용 반환
                return CreatePlaceholderContent(
                    "Error Log Viewer 로드 실패\n\n" +
                    $"오류: {ex.Message}\n\n" +
                    "시스템 관리자에게 문의하세요.");
            }
        }

        /// <summary>
        /// Recipe Editor 생성 (권한 확인 포함)
        /// </summary>
        private UserControl CreateRecipeEditor()
        {
            try
            {
                // 권한 확인
                if (!UserSession.IsLoggedIn || UserSession.CurrentUser.Role < UserRole.Engineer)
                {
                    // 권한 부족 시 안내 화면 반환
                    return CreatePlaceholderContent(
                        "Recipe Editor 접근 권한이 없습니다.\n\n" +
                        "Engineer 이상의 권한이 필요합니다.\n" +
                        "관리자에게 문의하세요.");
                }

                // RecipeEditor 인스턴스 생성
                var recipeEditor = new TeachingPendant.RecipeSystem.UI.Views.RecipeEditor();

                Logger.Info("CommonFrame", "CreateRecipeEditor", "Recipe Editor가 생성되었습니다.");
                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "Recipe Editor 로드 완료");

                return recipeEditor;
            }
            catch (Exception ex)
            {
                Logger.Error("CommonFrame", "CreateRecipeEditor", "Recipe Editor 생성 중 오류 발생", ex);
                AlarmMessageManager.ShowAlarm(Alarms.UI_ERROR, $"Recipe Editor 로드 실패: {ex.Message}");

                return CreatePlaceholderContent(
                    $"Recipe Editor 로드 실패\n\n" +
                    $"오류: {ex.Message}\n\n" +
                    "시스템 관리자에게 문의하세요.");
            }
        }

        /// <summary>
        /// I/O 제어 화면 생성
        /// </summary>
        private UserControl CreateIOControl()
        {
            try
            {
                // Monitor UI를 재사용하되 I/O 모드로 설정
                var monitor = new MonitorUI.Monitor();

                // Monitor에 I/O 전용 모드 설정 (만약 Monitor 클래스에 이런 기능이 있다면)
                // monitor.SetIOMode(true);

                return monitor;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"I/O 제어 화면 생성 실패: {ex.Message}");
                return CreatePlaceholderContent("I/O Control - Feature will be implemented");
            }
        }

        // Mapping 전용 플레이스홀더 생성
        /// <summary>
        /// Auto-open mapping window on load
        /// </summary>
        private UserControl CreateMappingPlaceholder()
        {
            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            stackPanel.Children.Add(new TextBlock
            {
                Text = "Loading Wafer Mapping System...",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var userControl = new UserControl { Content = stackPanel };

            // UserControl이 로드되면 자동으로 매핑 창 열기
            userControl.Loaded += (sender, e) =>
            {
                try
                {
                    // 짧은 지연 후 자동으로 매핑 창 열기
                    var timer = new DispatcherTimer();
                    timer.Interval = TimeSpan.FromMilliseconds(100);
                    timer.Tick += (s, args) =>
                    {
                        timer.Stop();

                        var mappingWindow = new TeachingPendant.WaferMapping.WaferMappingWindow
                        {
                            Owner = Window.GetWindow(userControl),
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        };

                        mappingWindow.ShowDialog();

                        // 매핑 창이 닫히면 메인으로 돌아가기
                        if (Window.GetWindow(userControl) is CommonFrame commonFrame)
                        {
                            commonFrame.Close();
                        }
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                        $"Failed to open Wafer Mapping: {ex.Message}");
                }
            };

            return userControl;
        }

        private void InitializeUI()
        {
            btnCurrentScreen.Content = _currentScreenName;
            AlarmMessageManager.SetAlarmTextBlock(txtAlarmMessage);
        }

        private void SubscribeToEvents()
        {
            GlobalModeManager.ModeChanged += GlobalModeManager_ModeChanged;
            GlobalSpeedManager.SpeedChanged += GlobalSpeedManager_SpeedChanged; // ← 이것 추가!
            UpdateRadioButtonsForCurrentMode();
        }

        // ← GlobalSpeedManager_SpeedChanged 이벤트 핸들러 추가!
        private void GlobalSpeedManager_SpeedChanged(object sender, int newSpeed)
        {
            // 속도 변경 시 처리할 로직 (필요에 따라 구현)
            System.Diagnostics.Debug.WriteLine($"Speed changed in CommonFrame: {newSpeed}%");
        }

        private void CommonFrame_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"CommonFrame closing: {_currentScreenName}");

                // 이벤트 구독 해제
                GlobalModeManager.ModeChanged -= GlobalModeManager_ModeChanged;
                GlobalSpeedManager.SpeedChanged -= GlobalSpeedManager_SpeedChanged;

                // UserControl 정리
                if (MainContentArea?.Content is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                MainContentArea.Content = null;

                // 타이머 정리
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer = null;
                }

                System.Diagnostics.Debug.WriteLine("CommonFrame cleanup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during CommonFrame closing: {ex.Message}");
            }
        }
        #endregion

        #region Screen Content Management
        private void LoadScreenContent(string screenName)
        {
            try
            {
                if (screenName == "Setting")
                {
                    if (!PreCheckMovementForSetting())
                    {
                        SwitchToScreen("Movement");
                        return;
                    }
                }

                if (_screenFactories.TryGetValue(screenName, out var factory))
                {
                    MainContentArea.Content = factory();
                }
                else
                {
                    MainContentArea.Content = CreatePlaceholderContent($"{screenName} - Feature to be implemented");
                }
            }
            catch (Exception ex)
            {
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Failed to load {screenName}: {ex.Message}");
            }
        }

        private bool PreCheckMovementForSetting()
        {
            bool hasOpenedOnce = SetupUI.Setup.HasMovementUiBeenOpenedOnce;
            if (!hasOpenedOnce)
            {
                var result = MessageBox.Show(
                    this,
                    "To use full Setup features, you must visit the Movement UI first.\n\nYou will be redirected to the Movement UI.",
                    "Movement UI Required",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.OK)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Redirecting to Movement UI for Setup preparation");
                    return false;
                }
                else
                {
                    AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Setup access cancelled by user.");
                    this.Close();
                    return false;
                }
            }
            return true;
        }

        private UserControl CreatePlaceholderContent(string message)
        {
            return new UserControl { Content = new TextBlock { Text = message, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 16, FontStyle = FontStyles.Italic, Foreground = Brushes.DarkGray } };
        }

        private void SwitchToScreen(string screenName)
        {
            _currentScreenName = screenName;
            btnCurrentScreen.Content = screenName;
            UpdateBottomButtonsForCurrentScreen();
            LoadScreenContent(screenName);
            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, $"Switched to {screenName} screen");
        }

        /// <summary>
        /// Recipe Editor로 이동 후 지정된 스텝 선택
        /// </summary>
        /// <param name="stepNumber">선택할 스텝 번호</param>
        public void NavigateToRecipeEditor(int stepNumber = 1)
        {
            SwitchToScreen("RecipeEditor");
            if (MainContentArea.Content is RecipeEditor editor)
            {
                editor.SelectStep(stepNumber);
            }
        }

        private void UpdateBottomButtonsForCurrentScreen()
        {
            if (BottomButtonGrid == null) return;
            BottomButtonGrid.Children.Clear();
            BottomButtonGrid.ColumnDefinitions.Clear();
            if (_currentScreenName == "Setting")
            {
                SetupSettingBottomButtons(BottomButtonGrid);
            }
            else
            {
                SetupDefaultBottomButtons(BottomButtonGrid);
            }
        }

        private void SetupSettingBottomButtons(Grid bottomGrid)
        {
            for (int i = 0; i < 4; i++) bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var btn1 = new Button { Content = "Menu" }; btn1.Click += SettingMenuButton_Click; Grid.SetColumn(btn1, 0);
            var btn2 = new Button { Content = "Speed" }; btn2.Click += SettingSpeedButton_Click; Grid.SetColumn(btn2, 1);
            var btn3 = new Button { Content = "Speed Para" }; btn3.Click += SettingSpeedParaButton_Click; Grid.SetColumn(btn3, 2);
            var btn4 = new Button { Content = "Limit Status" }; btn4.Click += SettingLimitStatusButton_Click; Grid.SetColumn(btn4, 3);
            bottomGrid.Children.Add(btn1); bottomGrid.Children.Add(btn2); bottomGrid.Children.Add(btn3); bottomGrid.Children.Add(btn4);
        }

        private void SetupDefaultBottomButtons(Grid bottomGrid)
        {
            for (int i = 0; i < 4; i++)
                bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var btn1 = new Button { Content = "Menu" };
            btn1.Click += MenuButton_Click;
            Grid.SetColumn(btn1, 0);

            var btn2 = new Button { Content = "Speed" };
            btn2.Click += SpeedButton_Click;
            Grid.SetColumn(btn2, 1);

            // 세 번째 버튼 - RecipeRunner에서는 Recipe Editor로 변경
            var btn3 = new Button();
            if (_currentScreenName == "RecipeRunner")
            {
                btn3.Content = "Recipe Editor";  // Mode → Recipe Editor로 변경
                btn3.Click += RecipeEditorButton_Click;  // 새로운 이벤트 핸들러
            }
            else if (_currentScreenName == "Monitor")
            {
                btn3.Content = "I/O";  // Monitor에서는 I/O로 변경
                btn3.Click += IOButton_Click;
            }
            else if (_currentScreenName == "RecipeEditor" || _currentScreenName == "Movement")
            {
                btn3.Content = "Teaching";  // RecipeEditor와 Movement에서 Teaching 버튼 표시
                btn3.Click += TeachingButton_Click;
            }
            else
            {
                btn3.Content = "Mode";  // 다른 화면에서는 기존 Mode 유지
                btn3.Click += ModeButton_Click;
            }
            Grid.SetColumn(btn3, 2);

            var btn4 = new Button();
            if (_currentScreenName == "Monitor")
            {
                btn4.Content = "Remote";
                btn4.Click += RemoteButton_Click;
            }
            else if (_currentScreenName == "RecipeEditor" || _currentScreenName == "Movement")
            {
                btn4.Content = "Setup";  // RecipeEditor와 Movement에서 Setup 버튼 표시
                btn4.Click += SetupButton_Click;
            }
            else
            {
                btn4.Content = "Calculator";
                btn4.Click += CalculatorButton_Click;
            }
            Grid.SetColumn(btn4, 3);

            bottomGrid.Children.Add(btn1);
            bottomGrid.Children.Add(btn2);
            bottomGrid.Children.Add(btn3);
            bottomGrid.Children.Add(btn4);
        }

        /// <summary>
        /// RecipeRunner 화면에서 Recipe Editor 버튼 클릭 이벤트
        /// </summary>
        private void RecipeEditorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Recipe Editor 버튼 클릭됨! ===");

                // 권한 확인 (Engineer 이상만 Recipe 편집 가능)
                if (!UserSession.IsLoggedIn || UserSession.CurrentUser.Role < UserRole.Engineer)
                {
                    MessageBox.Show(this,
                        "Recipe 편집 권한이 없습니다.\n\nEngineer 이상의 권한이 필요합니다.",
                        "권한 부족", MessageBoxButton.OK, MessageBoxImage.Warning);
                    AlarmMessageManager.ShowAlarm(Alarms.ACCESS_DENIED, "Recipe Editor 접근 권한 부족");
                    return;
                }

                // Recipe Editor 화면으로 전환
                SwitchToScreen("RecipeEditor");
                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Recipe Editor 화면으로 전환되었습니다.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Recipe Editor 버튼 클릭 오류: {ex.Message}");
                MessageBox.Show(this,
                    $"Recipe Editor 화면 전환 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                AlarmMessageManager.ShowAlarm(Alarms.UI_ERROR, $"Recipe Editor 전환 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// Monitor 화면에서 I/O 버튼 클릭 이벤트
        /// </summary>
        private void IOButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== IOButton_Click 호출됨!");

                // Monitor 화면에서 I/O 기능 토글
                if (MainContentArea?.Content is MonitorUI.Monitor monitor)
                {
                    // Monitor에 I/O 패널 토글 기능이 있다면 호출
                    // monitor.ToggleIOPanel(); // Monitor 클래스에 이 메서드가 있다면

                    AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "I/O 패널이 활성화되었습니다.");
                }
                else
                {
                    // 별도 I/O 화면으로 전환
                    SwitchToScreen("I/O");
                    AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "I/O 화면으로 전환되었습니다.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IOButton_Click 오류: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.UI_ERROR, $"I/O 기능 활성화 실패: {ex.Message}");
            }
        }

        // CommonFrame에서 Remote 버튼 클릭 처리 (간단한 방법)
        private void RemoteButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== RemoteButton_Click 호출됨! ===");

            try
            {
                if (MainContentArea?.Content is MonitorUI.Monitor monitor)
                {
                    System.Diagnostics.Debug.WriteLine("Monitor 인스턴스 발견, HandleRemoteRequest 호출 시도...");
                    monitor.HandleRemoteRequest();
                    System.Diagnostics.Debug.WriteLine("HandleRemoteRequest 호출 완료");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Monitor 인스턴스를 찾을 수 없음!");
                    AlarmMessageManager.ShowAlarm(Alarms.UNEXPECTED_STATE,
                        "Monitor not loaded");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoteButton_Click 오류: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                    $"Remote error: {ex.Message}");
            }
        }
        #endregion

        #region Event Handlers (Default and Setting)
        private void MenuButton_Click(object sender, RoutedEventArgs e) { MenuOverlay.Visibility = Visibility.Visible; }
        private void SettingMenuButton_Click(object sender, RoutedEventArgs e) { MenuOverlay.Visibility = Visibility.Visible; }
        private void SpeedButton_Click(object sender, RoutedEventArgs e) { OpenSpeedWindow(); }
        private void SettingSpeedButton_Click(object sender, RoutedEventArgs e) { OpenSpeedWindow(); }

        private void OpenSpeedWindow()
        {
            var speedWindow = new SpeedControlWindow { Owner = this };
            speedWindow.ShowDialog();
        }

        private void SettingSpeedParaButton_Click(object sender, RoutedEventArgs e) { new SpeedParameterWindow { Owner = this }.ShowDialog(); }
        private void ModeButton_Click(object sender, RoutedEventArgs e) { /* To be implemented */ }
        private void CalculatorButton_Click(object sender, RoutedEventArgs e) { /* To be implemented */ }
        private void TeachingButton_Click(object sender, RoutedEventArgs e) { SwitchToScreen("Teaching"); }
        private void SetupButton_Click(object sender, RoutedEventArgs e) { SwitchToScreen("Setting"); }
        private void SettingLimitStatusButton_Click(object sender, RoutedEventArgs e) { /* To be implemented */ }
        #endregion

        #region Menu Option Event Handlers
        private void MenuOption_Click(object sender, RoutedEventArgs e)
        {
            CloseMenuAndShowMessage("Option selected - Feature to be implemented");
        }

        private void MenuMapping_Click(object sender, RoutedEventArgs e)
        {
            CloseMenuAndShowMessage("Mapping selected - Feature to be implemented");
        }

        private void MenuSetup_Click(object sender, RoutedEventArgs e)
        {
            MenuOverlay.Visibility = Visibility.Collapsed;
            SwitchToScreen("Setting");
        }

        private void MenuMonitor_Click(object sender, RoutedEventArgs e)
        {
            MenuOverlay.Visibility = Visibility.Collapsed;
            SwitchToScreen("Monitor");
        }

        private void MenuTeaching_Click(object sender, RoutedEventArgs e)
        {
            MenuOverlay.Visibility = Visibility.Collapsed;
            SwitchToScreen("Teaching");
        }

        private void MenuMovement_Click(object sender, RoutedEventArgs e)
        {
            MenuOverlay.Visibility = Visibility.Collapsed;
            SwitchToScreen("Movement");
        }

        private void MenuSave_Click(object sender, RoutedEventArgs e)
        {
            CloseMenuAndShowMessage("Save selected - Feature to be implemented");
        }

        private void MenuLoad_Click(object sender, RoutedEventArgs e)
        {
            CloseMenuAndShowMessage("Load selected - Feature to be implemented");
        }

        private void MenuJog_Click(object sender, RoutedEventArgs e)
        {
            CloseMenuAndShowMessage("Jog selected - Feature to be implemented");
        }

        private void MenuJogSpeed_Click(object sender, RoutedEventArgs e)
        {
            CloseMenuAndShowMessage("Jog Speed selected - Feature to be implemented");
        }

        private void MenuAbs_Click(object sender, RoutedEventArgs e)
        {
            CloseMenuAndShowMessage("Abs selected - Feature to be implemented");
        }

        private void MenuTotal_Click(object sender, RoutedEventArgs e)
        {
            CloseMenuAndShowMessage("Total selected - Feature to be implemented");
        }

        private void MenuClose_Click(object sender, RoutedEventArgs e)
        {
            MenuOverlay.Visibility = Visibility.Collapsed;
            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Menu closed");
        }

        private void CloseMenuAndShowMessage(string message)
        {
            MenuOverlay.Visibility = Visibility.Collapsed;
            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, message);
        }
        #endregion

        #region Global Mode Management
        private void rbManual_Click(object sender, RoutedEventArgs e) { if (rbManual.IsChecked == true) GlobalModeManager.SetMode(GlobalMode.Manual); }
        private void rbAuto_Click(object sender, RoutedEventArgs e) { if (rbAuto.IsChecked == true) GlobalModeManager.SetMode(GlobalMode.Auto); }
        private void rbEmg_Click(object sender, RoutedEventArgs e) { if (rbEmg.IsChecked == true) GlobalModeManager.SetMode(GlobalMode.Emergency); }

        private void GlobalModeManager_ModeChanged(object sender, ModeChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateRadioButtonsForCurrentMode();
                AlarmMessageManager.ShowAlarm(Alarms.MODE_CHANGED, $"{GlobalModeManager.GetModeMessage(e.NewMode)} - {_currentScreenName}");
            }));
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

        #region Window Closing Methods
        /// <summary>
        /// CommonFrame 창이 닫힐 때의 처리 - 수정된 버전 (중복 제거)
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"OnClosing called for: {_currentScreenName}");

                // 강제로 창 닫기 허용
                e.Cancel = false;

                // 정리 작업은 OnClosed에서 수행
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnClosing: {ex.Message}");
                e.Cancel = false; // 에러가 나도 닫기
            }
            finally
            {
                base.OnClosing(e);
            }
        }

        /// <summary>
        /// 창이 완전히 닫힌 후 정리 작업
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"OnClosed called for: {_currentScreenName}");

                // 이벤트 구독 해제
                GlobalModeManager.ModeChanged -= GlobalModeManager_ModeChanged;
                GlobalSpeedManager.SpeedChanged -= GlobalSpeedManager_SpeedChanged;

                // UserControl 정리
                if (MainContentArea?.Content is IDisposable disposableContent)
                {
                    try
                    {
                        disposableContent.Dispose();
                        System.Diagnostics.Debug.WriteLine("UserControl disposed successfully");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error disposing UserControl: {ex.Message}");
                    }
                }

                // UserControl 참조 해제
                if (MainContentArea != null)
                {
                    MainContentArea.Content = null;
                }

                // 타이머 정리
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer = null;
                }

                System.Diagnostics.Debug.WriteLine("CommonFrame cleanup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during CommonFrame cleanup: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        // MainWindow로 돌아가는 버튼 클릭 시
        private void MainMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainMenu button clicked");
                this.Close(); // 단순하게 창 닫기
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MainMenu_Click: {ex.Message}");

                // 에러가 나도 강제로 닫기
                try
                {
                    this.Hide();
                    this.Close();
                }
                catch
                {
                    // 최후의 수단 - App.ForceShutdown() 호출 전에 App 클래스가 구현되어 있는지 확인
                    System.Diagnostics.Debug.WriteLine("Critical error - forcing application shutdown");
                    Application.Current.Shutdown();
                }
            }
        }
        #endregion

        #region Recipe System Integration Methods

        /// <summary>
        /// RecipeRunner로 전환하면서 특정 레시피 로드
        /// </summary>
        /// <param name="recipe">로드할 레시피</param>
        public void SwitchToRecipeRunnerWithRecipe(TransferRecipe recipe)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[CommonFrame] RecipeRunner로 전환 with recipe: {recipe?.RecipeName}");

                // RecipeRunner 화면으로 전환
                SwitchToScreen("RecipeRunner");

                // RecipeRunner에 레시피 로드
                var recipeRunner = MainContentArea?.Content as TeachingPendant.RecipeSystem.UI.Views.RecipeRunner;
                if (recipeRunner != null && recipe != null)
                {
                    recipeRunner.LoadRecipe(recipe);
                    System.Diagnostics.Debug.WriteLine($"[CommonFrame] RecipeRunner에 레시피 로드 완료: {recipe.RecipeName}");
                }

                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE,
                    $"RecipeRunner 전환 완료: {recipe?.RecipeName ?? "Unknown"}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CommonFrame] RecipeRunner 전환 실패: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.UI_ERROR, $"RecipeRunner 전환 실패: {ex.Message}");

                // 실패 시 기본 RecipeRunner로라도 전환
                SwitchToScreen("RecipeRunner");
            }
        }

        /// <summary>
        /// RecipeEditor로 전환하면서 특정 레시피 로드
        /// </summary>
        /// <param name="recipe">편집할 레시피</param>
        public void SwitchToRecipeEditorWithRecipe(TransferRecipe recipe)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[CommonFrame] RecipeEditor로 전환 with recipe: {recipe?.RecipeName}");

                // RecipeEditor 화면으로 전환
                SwitchToScreen("RecipeEditor");

                // RecipeEditor에 레시피 로드
                var recipeEditor = MainContentArea?.Content as TeachingPendant.RecipeSystem.UI.Views.RecipeEditor;
                if (recipeEditor != null && recipe != null)
                {
                    recipeEditor.LoadRecipe(recipe);
                    System.Diagnostics.Debug.WriteLine($"[CommonFrame] RecipeEditor에 레시피 로드 완료: {recipe.RecipeName}");
                }

                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE,
                    $"RecipeEditor 전환 완료: {recipe?.RecipeName ?? "Unknown"}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CommonFrame] RecipeEditor 전환 실패: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.UI_ERROR, $"RecipeEditor 전환 실패: {ex.Message}");

                // 실패 시 기본 RecipeEditor로라도 전환
                SwitchToScreen("RecipeEditor");
            }
        }

        /// <summary>
        /// RecipeManager 새로고침 (다른 화면에서 레시피 변경 시 호출)
        /// </summary>
        public void RefreshRecipeManager()
        {
            try
            {
                var recipeManager = MainContentArea?.Content as TeachingPendant.RecipeSystem.UI.Views.RecipeManager;
                if (recipeManager != null)
                {
                    recipeManager.RefreshRecipeList();
                    System.Diagnostics.Debug.WriteLine("[CommonFrame] RecipeManager 새로고침 완료");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CommonFrame] RecipeManager 새로고침 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 활성화된 Recipe 관련 화면 확인
        /// </summary>
        /// <returns>활성화된 Recipe 화면 타입</returns>
        public string GetCurrentRecipeScreen()
        {
            try
            {
                var content = MainContentArea?.Content;

                if (content is TeachingPendant.RecipeSystem.UI.Views.RecipeRunner)
                    return "RecipeRunner";
                else if (content is TeachingPendant.RecipeSystem.UI.Views.RecipeEditor)
                    return "RecipeEditor";
                else if (content is TeachingPendant.RecipeSystem.UI.Views.RecipeManager)
                    return "RecipeManager";
                else
                    return "None";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CommonFrame] Recipe 화면 타입 확인 실패: {ex.Message}");
                return "Error";
            }
        }

        #endregion
    }
}