using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TeachingPendant.Alarm;
using TeachingPendant.Manager;
using TeachingPendant.MovementUI;

namespace TeachingPendant
{
    public partial class RemoteControlWindow : Window
    {
        #region Fields
        private Dictionary<string, RemoteMenuState> _menuStates;
        private DispatcherTimer _executionTimer;
        private readonly SolidColorBrush _onBrush = new SolidColorBrush(Colors.LightGreen);
        private readonly SolidColorBrush _offBrush = new SolidColorBrush(Colors.LightPink);

        // 순차 실행 관련 필드
        private bool _isSequenceRunning = false;
        private string[] _sequenceOrder = { "CPick", "CPlace", "SPlace", "SPick" };
        private int _currentSequenceIndex = 0;
        private int _currentPointIndex = 0;

        // 좌표 이동 관련 필드
        private decimal _currentR = 0.00m;
        private decimal _currentT = 0.00m;
        private decimal _currentA = 0.00m;
        private decimal _targetR = 0.00m;
        private decimal _targetT = 0.00m;
        private decimal _targetA = 0.00m;
        private const decimal COORDINATE_STEP = 1.0m;

        // Wait 기능 추가
        private bool _isWaiting = false;
        private DateTime _waitStartTime;
        private const int WAIT_SECONDS = 1; // 1초 대기
        #endregion

        #region Data Classes
        private class RemoteMenuState
        {
            public bool IsRunning { get; set; } = false;
            public int CurrentPointIndex { get; set; } = 0; // 0=P1, 1=P2, ..., 6=P7
            public TextBlock StatusText { get; set; }
            public Button ControlButton { get; set; }
        }
        #endregion

        #region Constructor
        public RemoteControlWindow()
        {
            InitializeComponent();
            InitializeRemoteControl();
            InitializeAlarmManager();
            this.Loaded += RemoteControlWindow_Loaded;
            this.Closing += RemoteControlWindow_Closing;
        }

        private void InitializeRemoteControl()
        {
            // 메뉴 상태 초기화
            _menuStates = new Dictionary<string, RemoteMenuState>
            {
                { "CPick", new RemoteMenuState { StatusText = txtCPickStatus, ControlButton = btnCPickControl } },
                { "CPlace", new RemoteMenuState { StatusText = txtCPlaceStatus, ControlButton = btnCPlaceControl } },
                { "SPick", new RemoteMenuState { StatusText = txtSPickStatus, ControlButton = btnSPickControl } },
                { "SPlace", new RemoteMenuState { StatusText = txtSPlaceStatus, ControlButton = btnSPlaceControl } }
            };

            // 실행 타이머 초기화 - Speed에 따라 간격 조정
            _executionTimer = new DispatcherTimer();
            UpdateRemoteTimerInterval(); // Speed 적용
            _executionTimer.Tick += ExecutionTimer_Tick;

            // Speed 변경 이벤트 구독
            GlobalSpeedManager.SpeedChanged += OnRemoteSpeedChanged;

            // 모든 메뉴 초기 상태 설정
            foreach (var menuState in _menuStates.Values)
            {
                UpdateMenuDisplay(menuState);
            }
        }

        // Remote 타이머 간격 업데이트 메서드
        private void UpdateRemoteTimerInterval()
        {
            if (_executionTimer != null)
            {
                // 최종 속도 = System Speed × Speed Control %
                double actualSpeedMMS = SetupUI.Setup.GetActualSpeedMMS();

                double intervalMs = 10.0 * (100.0 / actualSpeedMMS);
                intervalMs = Math.Max(1, Math.Min(100, intervalMs));

                _executionTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);

                System.Diagnostics.Debug.WriteLine($"Remote 타이머 간격: {intervalMs}ms (System: {SetupUI.Setup.SystemSpeedMMS}mm/s × Control: {GlobalSpeedManager.CurrentSpeed}% = {actualSpeedMMS:F1}mm/s)");
            }
        }

        // Speed 변경 이벤트 핸들러 추가
        private void OnRemoteSpeedChanged(object sender, int newSpeed)
        {
            System.Diagnostics.Debug.WriteLine($"Remote: Speed 변경됨 {newSpeed}% - 타이머 간격 업데이트");
            UpdateRemoteTimerInterval();
        }

        private void InitializeAlarmManager()
        {
            if (txtAlarmMessage != null)
            {
                AlarmMessageManager.SetAlarmTextBlock(txtAlarmMessage);
            }
        }

        private void RemoteControlWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Remote Control window opened");
        }

        private void RemoteControlWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            GlobalSpeedManager.SpeedChanged -= OnRemoteSpeedChanged;

            // 모든 실행 중인 메뉴 및 순차 실행 정지
            _isSequenceRunning = false;
            StopAllIndividualMenus();
            _executionTimer.Stop();
            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Remote Control window closed");
        }
        #endregion

        #region Menu Control Events
        private void CPickControl_Click(object sender, RoutedEventArgs e)
        {
            ToggleMenuExecution("CPick");
        }

        private void CPlaceControl_Click(object sender, RoutedEventArgs e)
        {
            ToggleMenuExecution("CPlace");
        }

        private void SPickControl_Click(object sender, RoutedEventArgs e)
        {
            ToggleMenuExecution("SPick");
        }

        private void SPlaceControl_Click(object sender, RoutedEventArgs e)
        {
            ToggleMenuExecution("SPlace");
        }

        private void StartSequence_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateAutoMode()) return;

            // 순차 실행 시작
            _isSequenceRunning = true;
            _currentSequenceIndex = 0; // CPick부터 시작
            _currentPointIndex = 0;    // P1부터 시작

            // 모든 개별 메뉴 정지
            StopAllIndividualMenus();

            // 첫 번째 목표 설정
            SetNewTarget(_sequenceOrder[_currentSequenceIndex], _currentPointIndex + 1);

            _executionTimer.Start();
            UpdateSequenceButtonsState();

            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Sequential execution started - Smooth movement to CPick P1");
        }

        private void StopSequence_Click(object sender, RoutedEventArgs e)
        {
            _isSequenceRunning = false;
            _currentSequenceIndex = 0;
            _currentPointIndex = 0;

            _executionTimer.Stop();
            UpdateSequenceButtonsState();

            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Sequential execution stopped");
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion

        #region Menu Execution Logic
        private void ToggleMenuExecution(string menuName)
        {
            if (!_menuStates.ContainsKey(menuName)) return;

            var menuState = _menuStates[menuName];

            if (menuState.IsRunning)
            {
                // 실행 중이면 정지
                StopMenuExecution(menuName);
            }
            else
            {
                // 정지 중이면 시작
                if (!ValidateAutoMode()) return;
                StartMenuExecution(menuName);

                // 다른 메뉴가 실행 중이 아니면 타이머 시작
                if (!_executionTimer.IsEnabled)
                {
                    _executionTimer.Start();
                }
            }
        }

        private bool ValidateAutoMode()
        {
            if (GlobalModeManager.CurrentMode != GlobalMode.Auto)
            {
                var result = MessageBox.Show(
                    "Remote execution requires Auto mode.\nSwitch to Auto mode?",
                    "Mode Change Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    GlobalModeManager.SetMode(GlobalMode.Auto);
                    AlarmMessageManager.ShowAlarm(Alarms.MODE_CHANGED, "Switched to Auto mode for remote execution");
                    return true;
                }
                else
                {
                    AlarmMessageManager.ShowAlarm(Alarms.OPERATION_LIMIT, "Remote execution cancelled - Auto mode required");
                    return false;
                }
            }
            return true;
        }

        private void StartMenuExecution(string menuName)
        {
            if (!_menuStates.ContainsKey(menuName)) return;

            var menuState = _menuStates[menuName];
            menuState.IsRunning = true;
            menuState.CurrentPointIndex = 0; // P1부터 시작

            // 첫 번째 목표 설정
            SetNewTarget(menuName, menuState.CurrentPointIndex + 1);

            UpdateMenuDisplay(menuState);
            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, $"{menuName} smooth movement started");
        }

        private void StopMenuExecution(string menuName)
        {
            if (!_menuStates.ContainsKey(menuName)) return;

            var menuState = _menuStates[menuName];
            menuState.IsRunning = false;
            menuState.CurrentPointIndex = 0;

            UpdateMenuDisplay(menuState);
            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, $"{menuName} remote execution stopped");

            // 모든 메뉴가 정지되면 타이머도 정지
            CheckAndStopTimer();
        }

        private void StopAllIndividualMenus()
        {
            foreach (string menuName in _menuStates.Keys)
            {
                var menuState = _menuStates[menuName];
                menuState.IsRunning = false;
                menuState.CurrentPointIndex = 0;
                UpdateMenuDisplay(menuState);
            }
        }

        private void UpdateSequenceButtonsState()
        {
            if (btnStartSequence != null && btnStopSequence != null)
            {
                btnStartSequence.IsEnabled = !_isSequenceRunning;
                btnStopSequence.IsEnabled = _isSequenceRunning;
            }
        }

        private void CheckAndStopTimer()
        {
            bool anyRunning = false;
            foreach (var menuState in _menuStates.Values)
            {
                if (menuState.IsRunning)
                {
                    anyRunning = true;
                    break;
                }
            }

            if (!anyRunning)
            {
                _executionTimer.Stop();
            }
        }
        #endregion

        #region Timer and Position Updates
        private void ExecutionTimer_Tick(object sender, EventArgs e)
        {
            // 대기 중인지 먼저 확인
            if (_isWaiting)
            {
                // 1초 경과했는지 확인
                if ((DateTime.Now - _waitStartTime).TotalSeconds >= WAIT_SECONDS)
                {
                    _isWaiting = false;

                    if (_isSequenceRunning)
                    {
                        MoveToNextSequencePoint();
                    }
                    else
                    {
                        MoveToNextIndividualPoint();
                    }
                }
                return; // 대기 중이면 좌표 이동하지 않음
            }

            if (_isSequenceRunning)
            {
                ExecuteSequentialMovement();
            }
            else
            {
                ExecuteIndividualMenus();
            }
        }

        private void ExecuteSequentialMovement()
        {
            // 목표 좌표에 도달했는지 확인
            bool rReached = MoveToTarget(ref _currentR, _targetR);
            bool tReached = MoveToTarget(ref _currentT, _targetT);
            bool aReached = MoveToTarget(ref _currentA, _targetA);

            // 현재 좌표를 Monitor에 전송
            NotifyCurrentCoordinate();

            // 모든 축이 목표에 도달했으면 대기 시작
            if (rReached && tReached && aReached)
            {
                StartWait("Sequential");
            }
        }

        private void ExecuteIndividualMenus()
        {
            foreach (var kvp in _menuStates)
            {
                string menuName = kvp.Key;
                var menuState = kvp.Value;

                if (menuState.IsRunning)
                {
                    // 목표 좌표에 도달했는지 확인
                    bool rReached = MoveToTarget(ref _currentR, _targetR);
                    bool tReached = MoveToTarget(ref _currentT, _targetT);
                    bool aReached = MoveToTarget(ref _currentA, _targetA);

                    // 현재 좌표를 Monitor에 전송
                    NotifyCurrentCoordinate();

                    // 모든 축이 목표에 도달했으면 대기 시작
                    if (rReached && tReached && aReached)
                    {
                        StartWait(menuName);
                    }
                    break; // 하나의 메뉴만 실행
                }
            }
        }

        // 대기 시작 메서드 추가
        private void StartWait(string executionType)
        {
            _isWaiting = true;
            _waitStartTime = DateTime.Now;

            string currentMenu, pointName;

            if (executionType == "Sequential")
            {
                currentMenu = _sequenceOrder[_currentSequenceIndex];
                pointName = $"P{_currentPointIndex + 1}";
            }
            else
            {
                currentMenu = executionType;
                var menuState = _menuStates[executionType];
                pointName = $"P{menuState.CurrentPointIndex + 1}";
            }

            AlarmMessageManager.ShowAlarm(Alarms.POSITION_LOADED,
                $"Reached {currentMenu} {pointName} - Waiting 1 second...");

            System.Diagnostics.Debug.WriteLine($"Remote: 포인트 도달 후 1초 대기 시작: {currentMenu} {pointName}");
        }

        // 목표 좌표로 1씩 이동
        private bool MoveToTarget(ref decimal current, decimal target)
        {
            if (Math.Abs(current - target) <= COORDINATE_STEP)
            {
                current = target; // 목표 도달
                return true;
            }

            if (current < target)
                current += COORDINATE_STEP;
            else
                current -= COORDINATE_STEP;

            return false;
        }

        // 순차 실행에서 다음 포인트로 이동
        private void MoveToNextSequencePoint()
        {
            _currentPointIndex++;

            // P7 완료 시 다음 메뉴로
            if (_currentPointIndex > 6) // P7 완료
            {
                _currentPointIndex = 0;
                _currentSequenceIndex++;

                // 모든 메뉴 완료 시 처음부터 반복
                if (_currentSequenceIndex >= _sequenceOrder.Length)
                {
                    _currentSequenceIndex = 0;
                }
            }

            // 새로운 목표 좌표 설정
            SetNewTarget(_sequenceOrder[_currentSequenceIndex], _currentPointIndex + 1);
        }

        // 개별 메뉴에서 다음 포인트로 이동
        private void MoveToNextIndividualPoint()
        {
            // 현재 실행 중인 메뉴 찾기
            foreach (var kvp in _menuStates)
            {
                if (kvp.Value.IsRunning)
                {
                    var menuState = kvp.Value;
                    string menuName = kvp.Key;

                    menuState.CurrentPointIndex++;

                    // P7 완료 시 P1으로 돌아가서 반복
                    if (menuState.CurrentPointIndex > 6)
                    {
                        menuState.CurrentPointIndex = 0;
                    }

                    // 새로운 목표 좌표 설정
                    SetNewTarget(menuName, menuState.CurrentPointIndex + 1);
                    UpdateMenuDisplay(menuState);
                    break;
                }
            }
        }

        // 새로운 목표 좌표 설정
        private void SetNewTarget(string menuName, int pointNumber)
        {
            try
            {
                // Movement에서 현재 선택된 그룹 가져오기
                string currentGroup = MovementUI.Movement.GetCurrentSelectedGroupForRemote();

                // Movement에서 실제 좌표 가져오기
                string[] pointCoords = MovementUI.Movement.GetPointCoordinatesForRemote(currentGroup, menuName, pointNumber);

                // 실제 좌표로 목표 설정
                if (decimal.TryParse(pointCoords[0], out decimal coordR) &&
                    decimal.TryParse(pointCoords[1], out decimal coordT) &&
                    decimal.TryParse(pointCoords[2], out decimal coordA))
                {
                    _targetR = coordR;
                    _targetT = coordT;
                    _targetA = coordA;

                    System.Diagnostics.Debug.WriteLine($"실제 좌표 로드: {currentGroup} {menuName} P{pointNumber} - R:{_targetR}, T:{_targetT}, A:{_targetA}");
                }
                else
                {
                    // 파싱 실패 시 기본값
                    _targetR = 0.00m;
                    _targetT = 0.00m;
                    _targetA = 0.00m;

                    System.Diagnostics.Debug.WriteLine($"좌표 파싱 실패, 기본값 사용: {menuName} P{pointNumber}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetNewTarget 오류: {ex.Message}");

                // 오류 시 기본값
                _targetR = 0.00m;
                _targetT = 0.00m;
                _targetA = 0.00m;
            }

            // 구간 정보를 Monitor에 전송
            MovementUI.Movement.NotifySectionChanged(menuName, $"P{pointNumber}", true);
        }

        // 현재 좌표를 Monitor에 전송
        private void NotifyCurrentCoordinate()
        {
            MovementUI.Movement.NotifyCoordinateChanged(_currentR, _currentT, _currentA);
        }

        private void NotifyMovementCoordinate(string menuName, int pointNumber)
        {
            try
            {
                // 예시 좌표 계산
                decimal sampleR = pointNumber * 10.0m;
                decimal sampleT = pointNumber * 5.0m;
                decimal sampleA = pointNumber * 2.0m;

                // Movement의 정적 메서드 호출
                MovementUI.Movement.NotifyCoordinateChanged(sampleR, sampleT, sampleA);
                MovementUI.Movement.NotifySectionChanged(menuName, $"P{pointNumber}", true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NotifyMovementCoordinate error: {ex.Message}");
            }
        }

        private void UpdateMenuDisplay(RemoteMenuState menuState)
        {
            if (menuState.IsRunning)
            {
                // 실행 중 상태
                menuState.StatusText.Text = "ON";
                menuState.StatusText.Foreground = Brushes.Green;
                menuState.ControlButton.Content = "OFF";
                menuState.ControlButton.Background = _offBrush;
            }
            else
            {
                // 정지 상태
                menuState.StatusText.Text = "OFF";
                menuState.StatusText.Foreground = Brushes.Red;
                menuState.ControlButton.Content = "ON";
                menuState.ControlButton.Background = _onBrush;
            }
        }
        #endregion
    }
}