using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using TeachingPendant.Alarm;
using TeachingPendant.Manager;
using TeachingPendant.MovementUI;
using System.Linq;
using TeachingPendant.Safety;
using System.Collections.Generic;

namespace TeachingPendant.MonitorUI
{
    public partial class Monitor : UserControl
    {
        #region Fields
        private DispatcherTimer _updateTimer;
        private ObservableCollection<IOSignal> _inputSignals;
        private ObservableCollection<IOSignal> _outputSignals;
        private DateTime _startTime;
        private bool _isRemoteMode = false;
        #endregion

        #region Constructor
        public Monitor()
        {
            try
            {
                InitializeComponent();

                // IOController 이벤트 구독 추가
                IOController.IOStateChanged += IOController_IOStateChanged;
                System.Diagnostics.Debug.WriteLine("Monitor: InitializeComponent completed");

                InitializeFields();
                InitializeAlarmManager();
                InitializeIOSignals();
                SubscribeToEvents();
                SubscribeToMovementEvents();
                SubscribeToSafetyEvents();
                InitializeRealTimeUpdate();

                this.Loaded += Monitor_Loaded;
                this.Unloaded += Monitor_Unloaded;
                System.Diagnostics.Debug.WriteLine("Monitor: Constructor completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Monitor constructor error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("StackTrace: " + ex.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// SafetySystem 이벤트 구독
        /// </summary>
        private void SubscribeToSafetyEvents()
        {
            try
            {
                if (SafetySystem.IsInitialized)
                {
                    SafetySystem.SafetyStatusChanged += SafetySystem_SafetyStatusChanged;
                    SafetySystem.InterlockStatusChanged += SafetySystem_InterlockStatusChanged;
                    SafetySystem.EmergencyStopTriggered += SafetySystem_EmergencyStopTriggered;

                    System.Diagnostics.Debug.WriteLine("Monitor: SafetySystem 이벤트 구독 완료");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Monitor: SafetySystem 미초기화로 이벤트 구독 실패");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Monitor: SafetySystem 이벤트 구독 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 안전 상태 변경 이벤트 처리
        /// </summary>
        private void SafetySystem_SafetyStatusChanged(object sender, SafetyStatusChangedEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateSafetyStatusDisplay(e.NewStatus);
                    //UpdateSafetyRelatedIO(e.NewStatus);

                    System.Diagnostics.Debug.WriteLine($"Monitor: 안전 상태 변경 - {e.OldStatus} → {e.NewStatus}");
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Monitor: 안전 상태 변경 처리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 인터록 상태 변경 이벤트 처리
        /// </summary>
        private void SafetySystem_InterlockStatusChanged(object sender, InterlockStatusChangedEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    //UpdateInterlockDisplay(e.DeviceName, e.NewStatus);

                    // 인터록 상태를 I/O로 표시
                    string ioName = GetIONameForInterlock(e.DeviceName);
                    if (!string.IsNullOrEmpty(ioName))
                    {
                        bool isSecure = e.NewStatus == InterlockStatus.Closed;
                        SetInputState(ioName, isSecure);
                    }

                    System.Diagnostics.Debug.WriteLine($"Monitor: 인터록 상태 변경 - {e.DeviceName}: {e.OldStatus} → {e.NewStatus}");
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Monitor: 인터록 상태 변경 처리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 비상정지 이벤트 처리
        /// </summary>
        private void SafetySystem_EmergencyStopTriggered(object sender, EmergencyStopEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 비상정지 시 모든 출력 OFF
                    TurnOffAllOutputs();

                    // 비상정지 상태 표시
                    //UpdateEmergencyStopDisplay(true);

                    // 알람 표시
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                        $"비상정지 발생: {e.Reason}");

                    System.Diagnostics.Debug.WriteLine($"Monitor: 비상정지 발생 - {e.Reason}");
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Monitor: 비상정지 처리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 안전 상태 표시 업데이트
        /// </summary>
        private void UpdateSafetyStatusDisplay(SafetyStatus status)
        {
            try
            {
                // txtRobotStatus 업데이트 (기존 UI 요소 활용)
                if (txtRobotStatus != null)
                {
                    switch (status)
                    {
                        case SafetyStatus.Safe:
                            txtRobotStatus.Text = "SAFE";
                            txtRobotStatus.Foreground = Brushes.Green;
                            break;
                        case SafetyStatus.Warning:
                            txtRobotStatus.Text = "WARNING";
                            txtRobotStatus.Foreground = Brushes.Orange;
                            break;
                        case SafetyStatus.Dangerous:
                            txtRobotStatus.Text = "DANGER";
                            txtRobotStatus.Foreground = Brushes.Red;
                            break;
                        case SafetyStatus.EmergencyStop:
                            txtRobotStatus.Text = "E-STOP";
                            txtRobotStatus.Foreground = Brushes.Red;
                            break;
                    }
                }

                // txtSystemMode 업데이트
                if (txtSystemMode != null)
                {
                    if (status == SafetyStatus.EmergencyStop)
                    {
                        txtSystemMode.Text = "EMERGENCY";
                        txtSystemMode.Foreground = Brushes.Red;
                    }
                    else
                    {
                        txtSystemMode.Text = GlobalModeManager.CurrentModeName;
                        txtSystemMode.Foreground = Brushes.Blue;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Monitor: 안전 상태 표시 업데이트 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 인터록 장치명에 대응하는 I/O 신호명 반환
        /// </summary>
        private string GetIONameForInterlock(string deviceName)
        {
            var mappings = new Dictionary<string, string>
    {
        { "Chamber1_Door", "Chamber1 Door" },
        { "Chamber2_Door", "Chamber2 Door" },
        { "LoadPort1_Door", "LoadPort1 Door" },
        { "LoadPort2_Door", "LoadPort2 Door" },
        { "Safety_Panel", "Safety Panel" },
        { "Emergency_Gate", "Emergency Gate" },
        { "Service_Door", "Service Door" }
    };

            return mappings.ContainsKey(deviceName) ? mappings[deviceName] : "";
        }

        /// <summary>
        /// 모든 출력 신호 OFF
        /// </summary>
        private void TurnOffAllOutputs()
        {
            try
            {
                foreach (var output in _outputSignals)
                {
                    if (output.IsActive)
                    {
                        output.IsActive = false;
                        IOController.SetOutput(output.Description, false);
                        System.Diagnostics.Debug.WriteLine($"Monitor: 출력 OFF - {output.Description}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Monitor: 모든 출력 OFF 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// IOController에서 I/O 변경 알림 수신
        /// </summary>
        private void IOController_IOStateChanged(object sender, IOStateChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.IsOutput)
                {
                    // Output 신호 업데이트
                    var output = _outputSignals?.FirstOrDefault(o => o.Description == e.SignalName);
                    if (output != null)
                    {
                        output.IsActive = e.IsActive;
                        System.Diagnostics.Debug.WriteLine($"Monitor: Output {e.SignalName} → {e.IsActive}");
                    }
                }
                else
                {
                    // Input 신호 업데이트
                    var input = _inputSignals?.FirstOrDefault(i => i.Description == e.SignalName);
                    if (input != null)
                    {
                        input.IsActive = e.IsActive;
                        System.Diagnostics.Debug.WriteLine($"Monitor: Input {e.SignalName} → {e.IsActive}");
                    }
                }

                // 의미 기반 I/O 매칭도 업데이트
                UpdateMeaningfulIOMapping();
            }));
        }

        // Movement 이벤트 구독
        private void SubscribeToMovementEvents()
        {
            // static 이벤트이므로 클래스명으로 접근
            MovementUI.Movement.CurrentCoordinateChanged += Movement_CurrentCoordinateChanged;
            MovementUI.Movement.CurrentSectionChanged += Movement_CurrentSectionChanged;

            // Remote 상태변경
            MovementUI.Movement.RemoteExecutionStatusChanged += Movement_RemoteExecutionStatusChanged;
        }

        #region Remote 기능 구현

        /// <summary>
        /// Remote 모드 시작
        /// </summary>
        private void StartRemoteMode()
        {
            System.Diagnostics.Debug.WriteLine("=== StartRemoteMode 진입 ===");

            try
            {
                System.Diagnostics.Debug.WriteLine("MovementUI.Movement.StartRemoteExecution() 호출 시도...");
                MovementUI.Movement.StartRemoteExecution();
                System.Diagnostics.Debug.WriteLine("MovementUI.Movement.StartRemoteExecution() 호출 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartRemoteExecution 오류: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

                MessageBox.Show("Movement UI must be open to use Remote control.\n\nPlease open Movement UI first.",
                    "Movement UI Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                AlarmMessageManager.ShowAlarm(Alarms.OPERATION_LIMIT,
                    "Remote requires Movement UI - Please open Movement first");
            }
        }

        /// <summary>
        /// Remote 모드 정지
        /// </summary>
        private void StopRemoteMode()
        {
            MovementUI.Movement.StopRemoteExecution();
            System.Diagnostics.Debug.WriteLine("Monitor: Remote 모드 정지 요청됨");
        }

        /// <summary>
        /// Movement에서 Remote 상태 변경 알림 받음
        /// </summary>
        private void Movement_RemoteExecutionStatusChanged(object sender, bool isRunning)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _isRemoteMode = isRunning;
                UpdateRemoteButtonState();
            }));
        }

        /// <summary>
        /// Remote 버튼 상태 업데이트
        /// </summary>
        private void UpdateRemoteButtonState()
        {
            // 상태만 업데이트 (UI 업데이트는 CommonFrame에서 처리)
            System.Diagnostics.Debug.WriteLine($"Remote mode changed: {_isRemoteMode}");
        }

        /// <summary>
        /// Movement UI가 열려있는지 확인
        /// </summary>
        private bool IsMovementUIOpen()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is CommonFrame frame)
                {
                    if (frame.MainContentArea?.Content is MovementUI.Movement)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion

        // Movement 좌표 변경 이벤트 핸들러
        private void Movement_CurrentCoordinateChanged(object sender, Movement.MovementCoordinateEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (txtCurrentA != null) txtCurrentA.Text = e.PositionR.ToString("F2");
                if (txtCurrentT != null) txtCurrentT.Text = e.PositionT.ToString("F2");
                if (txtCurrentZ != null) txtCurrentZ.Text = e.PositionA.ToString("F2");
            }));
        }

        // Movement 구간 변경 이벤트 핸들러  
        private void Movement_CurrentSectionChanged(object sender, Movement.MovementSectionEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (txtCurrentSection != null)
                {
                    txtCurrentSection.Text = e.FullSectionName;
                    txtCurrentSection.Foreground = e.IsRunning ? Brushes.DarkGreen : Brushes.Red;
                }
            }));
        }

        private void Monitor_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_INITIALIZED,
                    "Monitor UI loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Monitor_Loaded error: " + ex.Message);
            }
        }
        #endregion

        #region System Integration Testing (5단계)

        /// <summary>
        /// 전체 SafetySystem 통합 테스트 실행
        /// </summary>
        public void RunComprehensiveSafetyTest()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 전체 SafetySystem 통합 테스트 시작 ===");

                // 1. Movement UI 안전 연동 테스트
                TestMovementSafetyIntegration();

                // 2. Teaching UI 안전 연동 테스트  
                TestTeachingSafetyIntegration();

                // 3. Monitor UI 안전 표시 테스트
                TestMonitorSafetyDisplay();

                // 4. 통합 시나리오 테스트
                TestIntegratedSafetyScenarios();

                System.Diagnostics.Debug.WriteLine("=== 전체 SafetySystem 통합 테스트 완료 ===");

                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_INITIALIZED,
                    "SafetySystem 통합 테스트가 성공적으로 완료되었습니다.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SafetySystem 통합 테스트 실패: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                    "SafetySystem 통합 테스트 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// Movement UI 안전 연동 테스트
        /// </summary>
        private void TestMovementSafetyIntegration()
        {
            System.Diagnostics.Debug.WriteLine("--- Movement UI 안전 연동 테스트 ---");

            // Movement UI의 안전 확인 메서드들이 올바르게 작동하는지 확인
            bool movementSafe = SafetySystem.IsSafeForRobotOperation();
            System.Diagnostics.Debug.WriteLine($"Movement 로봇 작업 안전성: {movementSafe}");

            // 개별 챔버/로드포트 안전성 테스트
            bool chamber1Safe = SafetySystem.IsChamberSecure(1);
            bool chamber2Safe = SafetySystem.IsChamberSecure(2);
            bool loadPort1Safe = SafetySystem.IsLoadPortSecure(1);
            bool loadPort2Safe = SafetySystem.IsLoadPortSecure(2);

            System.Diagnostics.Debug.WriteLine($"챔버1: {chamber1Safe}, 챔버2: {chamber2Safe}");
            System.Diagnostics.Debug.WriteLine($"로드포트1: {loadPort1Safe}, 로드포트2: {loadPort2Safe}");
        }

        /// <summary>
        /// Teaching UI 안전 연동 테스트
        /// </summary>
        private void TestTeachingSafetyIntegration()
        {
            System.Diagnostics.Debug.WriteLine("--- Teaching UI 안전 연동 테스트 ---");

            // SoftLimit 테스트
            bool withinLimits = SafetySystem.IsWithinSoftLimits(100, 45, 50);
            System.Diagnostics.Debug.WriteLine($"SoftLimit 범위 내 좌표 테스트: {withinLimits}");

            // 범위 초과 테스트
            bool outsideLimits = SafetySystem.IsWithinSoftLimits(1000, 45, 50);
            System.Diagnostics.Debug.WriteLine($"SoftLimit 범위 초과 좌표 테스트: {outsideLimits}");

            // 위치별 안전성 테스트
            bool locationSafe = SafetySystem.IsLocationSecure("Chamber1");
            System.Diagnostics.Debug.WriteLine($"위치별 안전성 테스트: {locationSafe}");
        }

        /// <summary>
        /// Monitor UI 안전 표시 테스트
        /// </summary>
        private void TestMonitorSafetyDisplay()
        {
            System.Diagnostics.Debug.WriteLine("--- Monitor UI 안전 표시 테스트 ---");

            // 현재 안전 상태 표시 확인
            var currentStatus = SafetySystem.CurrentStatus;
            System.Diagnostics.Debug.WriteLine($"현재 안전 상태: {currentStatus}");

            // 인터록 요약 정보 확인
            string summary = SafetySystem.GetInterlockSystemSummary();
            System.Diagnostics.Debug.WriteLine($"인터록 요약: {summary}");

            // I/O 신호 동기화 확인
            bool allSecure = SafetySystem.AllInterlocksSecure;
            System.Diagnostics.Debug.WriteLine($"모든 인터록 안전: {allSecure}");
        }

        /// <summary>
        /// 통합 시나리오 테스트
        /// </summary>
        private void TestIntegratedSafetyScenarios()
        {
            System.Diagnostics.Debug.WriteLine("--- 통합 시나리오 테스트 ---");

            // 시나리오 1: 정상 상태에서 모든 기능 작동 확인
            System.Diagnostics.Debug.WriteLine("시나리오 1: 정상 상태 테스트");
            SafetySystem.SetAllInterlocksSecure();
            bool scenario1Pass = SafetySystem.IsSafeForRobotOperation();
            System.Diagnostics.Debug.WriteLine($"정상 상태 시나리오: {(scenario1Pass ? "PASS" : "FAIL")}");

            // 시나리오 2: 인터록 열림 상태 테스트
            System.Diagnostics.Debug.WriteLine("시나리오 2: 인터록 열림 테스트");
            SafetySystem.SetInterlockUnsafe("Chamber1_Door");
            bool scenario2Pass = !SafetySystem.IsSafeForRobotOperation();
            System.Diagnostics.Debug.WriteLine($"인터록 열림 시나리오: {(scenario2Pass ? "PASS" : "FAIL")}");

            // 정상 상태로 복구
            SafetySystem.SetAllInterlocksSecure();
            System.Diagnostics.Debug.WriteLine("모든 인터록 정상 상태로 복구");
        }

        #endregion

        #region Monitor_Unloaded 수정 - Remote 이벤트 구독 해제
        private void Monitor_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 기존 이벤트 구독 해제...
                GlobalModeManager.ModeChanged -= GlobalModeManager_ModeChanged;
                GlobalSpeedManager.SpeedChanged -= GlobalSpeedManager_SpeedChanged;

                // Movement 이벤트 구독 해제
                MovementUI.Movement.CurrentCoordinateChanged -= Movement_CurrentCoordinateChanged;
                MovementUI.Movement.CurrentSectionChanged -= Movement_CurrentSectionChanged;

                // Remote 이벤트 구독 해제
                MovementUI.Movement.RemoteExecutionStatusChanged -= Movement_RemoteExecutionStatusChanged;

                // I/O 이벤트 구독 해제
                IOController.IOStateChanged -= IOController_IOStateChanged;

                // 타이머 중지
                if (_updateTimer != null)
                {
                    _updateTimer.Stop();
                    _updateTimer = null;
                }

                System.Diagnostics.Debug.WriteLine("Monitor UI events unsubscribed and cleanup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Monitor_Unloaded error: " + ex.Message);
            }
        }
        #endregion

        #region Initialization
        private void InitializeFields()
        {
            _startTime = DateTime.Now;
            _inputSignals = new ObservableCollection<IOSignal>();
            _outputSignals = new ObservableCollection<IOSignal>();
        }

        private void InitializeAlarmManager()
        {
            try
            {
                if (txtAlarmMessage != null)
                {
                    AlarmMessageManager.SetAlarmTextBlock(txtAlarmMessage);
                    System.Diagnostics.Debug.WriteLine("AlarmManager initialized successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("InitializeAlarmManager error: " + ex.Message);
            }
        }

        private void InitializeIOSignals()
        {
            try
            {
                // 입력 신호 초기화 (DI01~DI16)
                for (int i = 1; i <= 16; i++)
                {
                    _inputSignals.Add(new IOSignal
                    {
                        Name = string.Format("DI{0:D2}", i),
                        Description = GetInputDescription(i),
                        IsActive = false,
                        IsOutput = false
                    });
                }

                // 출력 신호 초기화 (DO01~DO16)
                for (int i = 1; i <= 16; i++)
                {
                    _outputSignals.Add(new IOSignal
                    {
                        Name = string.Format("DO{0:D2}", i),
                        Description = GetOutputDescription(i),
                        IsActive = false,
                        IsOutput = true
                    });
                }

                // UI 바인딩
                this.Loaded += (s, e) =>
                {
                    if (InputSignalList != null)
                        InputSignalList.ItemsSource = _inputSignals;
                    if (OutputSignalList != null)
                        OutputSignalList.ItemsSource = _outputSignals;
                };

                System.Diagnostics.Debug.WriteLine("IO Signals initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("InitializeIOSignals error: " + ex.Message);
            }
        }

        private string GetInputDescription(int index)
        {
            switch (index)
            {
                case 1: return "Emergency Stop";
                case 2: return "Start Button";
                case 3: return "Stop Button";
                case 4: return "Reset Button";
                case 5: return "Home Sensor";
                case 6: return "Safety Gate";
                case 7: return "Air Pressure";
                case 8: return "Vacuum Sensor";
                default: return string.Format("Input {0}", index);
            }
        }

        private string GetOutputDescription(int index)
        {
            switch (index)
            {
                case 1: return "Robot Enable";
                case 2: return "Vacuum ON";
                case 3: return "Vacuum OFF";
                case 4: return "Cylinder Extend";
                case 5: return "Cylinder Retract";
                case 6: return "Green Light";
                case 7: return "Red Light";
                case 8: return "Buzzer";
                default: return string.Format("Output {0}", index);
            }
        }

        private void InitializeRealTimeUpdate()
        {
            try
            {
                _updateTimer = new DispatcherTimer();
                _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
                _updateTimer.Tick += UpdateTimer_Tick;
                _updateTimer.Start();
                System.Diagnostics.Debug.WriteLine("Real-time update initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("InitializeRealTimeUpdate error: " + ex.Message);
            }
        }

        private void SubscribeToEvents()
        {
            try
            {
                GlobalModeManager.ModeChanged += GlobalModeManager_ModeChanged;
                GlobalSpeedManager.SpeedChanged += GlobalSpeedManager_SpeedChanged;
                System.Diagnostics.Debug.WriteLine("Events subscribed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SubscribeToEvents error: " + ex.Message);
            }
        }
        #endregion

        #region Real-time Update
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                UpdateRobotStatus();
                UpdateIOStatus();
                UpdateSystemStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateTimer_Tick error: " + ex.Message);
            }
        }

        private void UpdateRobotStatus()
        {
            try
            {
                if (txtCurrentSpeed != null) txtCurrentSpeed.Text = string.Format("{0}%", GlobalSpeedManager.CurrentSpeed);
                if (txtRobotStatus != null) txtRobotStatus.Text = GetRobotStatusText();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateRobotStatus error: " + ex.Message);
            }
        }

        private void UpdateIOStatus()
        {
            try
            {
                if (_inputSignals != null && _outputSignals != null)
                {
                    // 의미 기반 I/O 매칭
                    UpdateMeaningfulIOMapping();

                    System.Diagnostics.Debug.WriteLine("의미 기반 I/O 매칭 완료");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateIOStatus error: " + ex.Message);
            }
        }

        /// <summary>
        /// 의미에 맞는 I/O 매칭 처리
        /// </summary>
        private void UpdateMeaningfulIOMapping()
        {
            // 1. 로봇 Enable 상태
            if (GetOutputState("Robot Enable")) // DO01 ON
            {
                SetInputState("Home Sensor", true);     // DI05 ON (로봇이 활성화되면 홈 센서 활성)
                SetInputState("Safety Gate", true);     // DI06 ON (안전 게이트 닫힘)
            }
            else
            {
                SetInputState("Home Sensor", false);    // DI05 OFF
                SetInputState("Safety Gate", false);    // DI06 OFF  
            }

            // 2. 진공 시스템 (핵심 로직)
            bool vacuumOn = GetOutputState("Vacuum ON");   // DO02
            bool vacuumOff = GetOutputState("Vacuum OFF"); // DO03

            if (vacuumOn && !vacuumOff)
            {
                SetInputState("Vacuum Sensor", true);   // DI08 ON (진공 감지)
                SetInputState("Air Pressure", true);    // DI07 ON (공기압 정상)
            }
            else if (vacuumOff || !vacuumOn)
            {
                SetInputState("Vacuum Sensor", false);  // DI08 OFF (진공 없음)
            }

            // 3. 실린더 상태
            bool cylinderExtend = GetOutputState("Cylinder Extend");   // DO04
            bool cylinderRetract = GetOutputState("Cylinder Retract"); // DO05

            // 실린더는 한 번에 하나만 동작 (상호 배타적)
            if (cylinderExtend && !cylinderRetract)
            {
                // 확장 시 약간의 시뮬레이션 딜레이 후 완료 신호
                SetInputState("Air Pressure", true);    // DI07 ON (공압 사용 중)
            }
            else if (cylinderRetract && !cylinderExtend)
            {
                SetInputState("Air Pressure", true);    // DI07 ON (공압 사용 중)
            }
            else
            {
                // 둘 다 OFF이거나 충돌 시
                if (!vacuumOn) // 진공이 사용 중이 아닐 때만
                {
                    SetInputState("Air Pressure", false); // DI07 OFF
                }
            }

            // 4. 표시등과 버저
            bool greenLight = GetOutputState("Green Light"); // DO06
            bool redLight = GetOutputState("Red Light");     // DO07
            bool buzzer = GetOutputState("Buzzer");          // DO08

            // 비상정지는 빨간불이나 버저가 켜지면 활성화
            if (redLight || buzzer)
            {
                SetInputState("Emergency Stop", true);  // DI01 ON (비상 상황)
                SetInputState("Start Button", false);   // DI02 OFF (시작 불가)
                SetInputState("Stop Button", true);     // DI03 ON (정지 활성)
            }
            else if (greenLight)
            {
                SetInputState("Emergency Stop", false); // DI01 OFF (정상)
                SetInputState("Start Button", true);    // DI02 ON (시작 가능)
                SetInputState("Stop Button", false);    // DI03 OFF
                SetInputState("Reset Button", false);   // DI04 OFF
            }

            // 5. 리셋 버튼 (모든 출력이 OFF일 때 활성화)
            bool anyOutputActive = false;
            for (int i = 0; i < _outputSignals.Count; i++)
            {
                if (_outputSignals[i].IsActive)
                {
                    anyOutputActive = true;
                    break;
                }
            }

            if (!anyOutputActive)
            {
                SetInputState("Reset Button", true);     // DI04 ON (리셋 가능)
            }
            else
            {
                SetInputState("Reset Button", false);    // DI04 OFF
            }
        }

        /// <summary>
        /// Output 상태 가져오기 (이름으로)
        /// </summary>
        private bool GetOutputState(string outputName)
        {
            var output = _outputSignals?.FirstOrDefault(o => o.Description == outputName);
            return output?.IsActive ?? false;
        }

        /// <summary>
        /// Input 상태 설정 (이름으로)
        /// </summary>
        private void SetInputState(string inputName, bool state)
        {
            var input = _inputSignals?.FirstOrDefault(i => i.Description == inputName);
            if (input != null)
            {
                input.IsActive = state;
            }
        }

        private void UpdateSystemStatus()
        {
            try
            {
                if (txtSystemMode != null) txtSystemMode.Text = GlobalModeManager.CurrentModeName;
                if (txtErrorCount != null) txtErrorCount.Text = "0";
                if (txtWarningCount != null) txtWarningCount.Text = "0";

                if (txtUptime != null)
                {
                    var uptime = DateTime.Now - _startTime;
                    txtUptime.Text = string.Format("{0:D2}:{1:D2}:{2:D2}", uptime.Hours, uptime.Minutes, uptime.Seconds);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateSystemStatus error: " + ex.Message);
            }
        }
        #endregion

        #region Event Handlers
        private void OutputButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button != null && button.Tag is IOSignal)
                {
                    var signal = button.Tag as IOSignal;
                    ToggleOutput(signal);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("OutputButton_Click error: " + ex.Message);
            }
        }

        private void GlobalModeManager_ModeChanged(object sender, ModeChangedEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateSystemStatus()));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GlobalModeManager_ModeChanged error: " + ex.Message);
            }
        }

        private void GlobalSpeedManager_SpeedChanged(object sender, int newSpeed)
        {
            // 속도 변경 시 UI 업데이트는 UpdateTimer_Tick에서 처리됨
        }
        #endregion

        #region I/O Control
        private void ToggleOutput(IOSignal signal)
        {
            try
            {
                if (!GlobalModeManager.IsEditingAllowed)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.OPERATION_LIMIT,
                        "Output control only available in Manual mode");
                    return;
                }

                signal.IsActive = !signal.IsActive;
                SendOutputSignal(signal.Name, signal.IsActive);

                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                    string.Format("{0} turned {1} (Input will sync automatically)",
                        signal.Name, signal.IsActive ? "ON" : "OFF"));

                System.Diagnostics.Debug.WriteLine($"Output {signal.Name} → {(signal.IsActive ? "ON" : "OFF")}, Input 동기화 예정");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ToggleOutput error: " + ex.Message);
            }
        }

        private void SendOutputSignal(string signalName, bool state)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("Hardware: {0} = {1}", signalName, state));
        }
        #endregion

        #region Simulation Methods
        private string SimulateCurrentPosition(string axis)
        {
            var time = DateTime.Now.Millisecond;
            switch (axis)
            {
                case "A": return (Math.Sin(time * 0.01) * 50).ToString("F2");
                case "T": return (Math.Cos(time * 0.008) * 30).ToString("F2");
                case "Z": return (Math.Sin(time * 0.005) * 20 + 50).ToString("F2");
                default: return "0.00";
            }
        }

        private string GetRobotStatusText()
        {
            switch (GlobalModeManager.CurrentMode)
            {
                case GlobalMode.Manual: return "READY";
                case GlobalMode.Auto: return "AUTO";
                case GlobalMode.Emergency: return "EMERGENCY";
                default: return "UNKNOWN";
            }
        }
        #endregion

        /// <summary>
        /// CommonFrame에서 Remote 요청을 처리하는 public 메서드
        /// </summary>
        /// <summary>
        /// CommonFrame에서 Remote 요청을 처리하는 public 메서드 (수정된 버전)
        /// </summary>
        public void HandleRemoteRequest()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== HandleRemoteRequest 호출됨! ===");

                // Remote Control 팝업창 열기
                var remoteWindow = new RemoteControlWindow
                {
                    Owner = Window.GetWindow(this)
                };

                remoteWindow.ShowDialog();

                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                    "Remote Control window opened");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HandleRemoteRequest 오류: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                    $"Remote control error: {ex.Message}");
            }
        }
    }

    #region Data Classes
    public class IOSignal : INotifyPropertyChanged
    {
        private bool _isActive;

        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsOutput { get; set; }

        public bool IsActive
        {
            get { return _isActive; }
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
    #endregion

    #region Converters
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool)
            {
                bool isActive = (bool)value;
                return new SolidColorBrush(isActive ? Colors.LimeGreen : Colors.Gray);
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToOnOffConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool)
            {
                bool isActive = (bool)value;
                return isActive ? "ON" : "OFF";
            }
            return "OFF";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    #endregion
}
