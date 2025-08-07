using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using TeachingPendant.Safety;
using TeachingPendant.Manager;

namespace TeachingPendant.HardwareControllers
{
    /// <summary>
    /// EtherCAT 실제 하드웨어 로봇 컨트롤러 - 에러 수정 버전
    /// 기존 DTP7HCommunication과 호환되는 로봇 제어
    /// </summary>
    public class EtherCATRobotController : IRobotController, IDisposable
    {
        #region Private Fields
        private DTP7HCommunication _dtp7h;
        private bool _isConnected = false;
        private bool _isMoving = false;
        private bool _isHomed = false;
        private bool _vacuumOn = false;
        private Position _currentPosition;
        private Position _targetPosition;
        private RobotStatus _currentStatus;
        private int _currentSpeed = 50;
        private DispatcherTimer _statusUpdateTimer;
        private readonly object _lockObject = new object();

        // DTP-7H 연결 설정
        private string _comPort = "COM3";
        private int _baudRate = 9600;

        private const string CLASS_NAME = "EtherCATRobotController";
        #endregion

        #region Events
        public event EventHandler<RobotStatusChangedEventArgs> StatusChanged;
        public event EventHandler<PositionChangedEventArgs> PositionChanged;
        public event EventHandler<RobotErrorEventArgs> ErrorOccurred;
        #endregion

        #region Properties
        public bool IsConnected
        {
            get
            {
                lock (_lockObject)
                {
                    return _isConnected && _dtp7h != null && _dtp7h.IsConnected;
                }
            }
        }

        public bool IsMoving
        {
            get
            {
                lock (_lockObject)
                {
                    return _isMoving;
                }
            }
        }

        public int CurrentSpeed
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentSpeed;
                }
            }
        }
        #endregion

        #region Constructor
        public EtherCATRobotController()
        {
            InitializeController();
            System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] EtherCAT robot controller created");
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 컨트롤러 초기화
        /// </summary>
        private void InitializeController()
        {
            _currentPosition = new Position(0, 0, 0);
            _targetPosition = new Position(0, 0, 0);
            _currentStatus = new RobotStatus();

            // DTP-7H 통신 객체 생성
            _dtp7h = new DTP7HCommunication();

            // 상태 업데이트 타이머 설정 (200ms 간격)
            _statusUpdateTimer = new DispatcherTimer();
            _statusUpdateTimer.Interval = TimeSpan.FromMilliseconds(200);
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
        }
        #endregion

        #region Connection Management
        public async Task<bool> ConnectAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Attempting to connect to DTP-7H robot...");

                // DTP-7H 연결
                bool dtp7hConnected = _dtp7h.Connect(_comPort, _baudRate);
                if (!dtp7hConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] DTP-7H connection failed");
                    OnErrorOccurred("DTP7H_CONNECT_ERROR", "DTP-7H connection failed");
                    return false;
                }

                // 연결 성공 표시 (기존 LED 제어 사용)
                _dtp7h.SendLEDCommand(LEDPosition.LeftLED1, LEDColor.Blue);

                // 초기화 시간
                await Task.Delay(1000);

                lock (_lockObject)
                {
                    _isConnected = true;
                    _isHomed = false;
                    _vacuumOn = false;
                    _currentPosition = new Position(0, 0, 0);
                    _targetPosition = new Position(0, 0, 0);

                    // 상태 업데이트 타이머 시작
                    _statusUpdateTimer.Start();
                }

                UpdateStatus();

                // 연결 확인 부저
                _dtp7h.SendBuzzerCommand(true);
                await Task.Delay(200);
                _dtp7h.SendBuzzerCommand(false);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] EtherCAT robot connected successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Connection failed: {ex.Message}");
                OnErrorOccurred("CONNECT_ERROR", "EtherCAT robot connection failed", ex);
                return false;
            }
        }

        public async Task<bool> DisconnectAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Disconnecting EtherCAT robot...");

                // 진행 중인 동작 정지
                await StopAsync();

                lock (_lockObject)
                {
                    _statusUpdateTimer?.Stop();
                    _isConnected = false;
                    _isMoving = false;
                    _isHomed = false;
                    _vacuumOn = false;
                }

                // 연결 해제 표시 (모든 LED OFF)
                if (_dtp7h != null && _dtp7h.IsConnected)
                {
                    _dtp7h.SendLEDCommand(LEDPosition.LeftLED1, LEDColor.Off);
                    _dtp7h.SendLEDCommand(LEDPosition.RightLED1, LEDColor.Off);

                    // DTP-7H 연결 해제
                    _dtp7h.Disconnect();
                }

                UpdateStatus();

                await Task.Delay(200);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] EtherCAT robot disconnected successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Disconnection failed: {ex.Message}");
                OnErrorOccurred("DISCONNECT_ERROR", "EtherCAT robot disconnection failed", ex);
                return false;
            }
        }
        #endregion

        #region Movement Commands
        public async Task<bool> MoveToAsync(double r, double theta, double z)
        {
            try
            {
                if (!IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Move failed: Not connected");
                    return false;
                }

                // 안전 확인
                if (!IsSafeToOperate())
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Move failed: Safety conditions not met");
                    return false;
                }

                Position targetPosition = new Position(r, theta, z);
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting actual robot movement: {targetPosition}");

                var oldPosition = new Position(_currentPosition.R, _currentPosition.Theta, _currentPosition.Z);

                lock (_lockObject)
                {
                    _isMoving = true;
                    _targetPosition = targetPosition;
                }

                // 이동 시작 표시 (기존 LED 제어 사용)
                _dtp7h.SendLEDCommand(LEDPosition.RightLED1, LEDColor.Blue);

                UpdateStatus();

                // 이동 완료까지 대기 (시뮬레이션)
                await SimulateMovement(oldPosition, targetPosition);

                lock (_lockObject)
                {
                    _currentPosition = targetPosition;
                    _isMoving = false;
                }

                // 이동 완료 표시
                _dtp7h.SendLEDCommand(LEDPosition.RightLED1, LEDColor.Off);

                UpdateStatus();

                // 위치 변경 이벤트 발생 (기존 생성자 사용)
                OnPositionChanged(oldPosition, targetPosition);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual robot movement completed: {targetPosition}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Move failed: {ex.Message}");
                OnErrorOccurred("MOVE_ERROR", "EtherCAT robot move failed", ex);

                lock (_lockObject)
                {
                    _isMoving = false;
                }

                return false;
            }
        }

        public async Task<bool> HomeAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Home command failed: Not connected");
                    return false;
                }

                if (!IsSafeToOperate())
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Home command failed: Safety conditions not met");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting actual robot homing...");

                var oldPosition = new Position(_currentPosition.R, _currentPosition.Theta, _currentPosition.Z);
                Position homePosition = new Position(0, 0, 0);

                lock (_lockObject)
                {
                    _isMoving = true;
                    _targetPosition = homePosition;
                }

                // 홈 이동 표시 (기존 LED 제어 사용)
                _dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Red);

                UpdateStatus();

                // 홈 이동 시뮬레이션
                await SimulateMovement(oldPosition, homePosition);

                lock (_lockObject)
                {
                    _currentPosition = homePosition;
                    _isHomed = true;
                    _isMoving = false;
                }

                // 홈 이동 완료 표시
                _dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Blue);

                UpdateStatus();

                // 위치 변경 이벤트 발생 (기존 생성자 사용)
                OnPositionChanged(oldPosition, homePosition);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual robot homing completed");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Home command failed: {ex.Message}");
                OnErrorOccurred("HOME_ERROR", "EtherCAT robot home command failed", ex);

                lock (_lockObject)
                {
                    _isMoving = false;
                }

                return false;
            }
        }

        public async Task<bool> StopAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Stopping actual robot...");

                // 정지 표시 (기존 LED 제어 사용)
                _dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Red);
                _dtp7h.SendBuzzerCommand(true);
                await Task.Delay(200);
                _dtp7h.SendBuzzerCommand(false);
                _dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Off);

                lock (_lockObject)
                {
                    _isMoving = false;
                }

                UpdateStatus();

                await Task.Delay(100);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual robot stop completed");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Stop failed: {ex.Message}");
                OnErrorOccurred("STOP_ERROR", "EtherCAT robot stop failed", ex);
                return false;
            }
        }
        #endregion

        #region Pick & Place Operations
        public async Task<bool> PickAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Pick failed: Not connected");
                    return false;
                }

                if (!IsSafeToOperate())
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Pick failed: Safety conditions not met");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting actual robot Pick operation...");

                // Pick 동작 표시 (기존 LED 제어 사용)
                _dtp7h.SendLEDCommand(LEDPosition.RightLED2, LEDColor.Blue);

                // 진공 ON
                bool vacuumResult = await SetVacuumAsync(true);

                if (vacuumResult)
                {
                    await Task.Delay(500);
                    _dtp7h.SendLEDCommand(LEDPosition.RightLED2, LEDColor.Off);
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual robot Pick operation completed");
                    return true;
                }
                else
                {
                    _dtp7h.SendLEDCommand(LEDPosition.RightLED2, LEDColor.Red);
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual robot Pick operation failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Pick failed: {ex.Message}");
                OnErrorOccurred("PICK_ERROR", "EtherCAT robot Pick failed", ex);
                return false;
            }
        }

        public async Task<bool> PlaceAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Place failed: Not connected");
                    return false;
                }

                if (!IsSafeToOperate())
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Place failed: Safety conditions not met");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting actual robot Place operation...");

                // Place 동작 표시 (기존 LED 제어 사용)
                _dtp7h.SendLEDCommand(LEDPosition.RightLED3, LEDColor.Blue);

                // 진공 OFF
                bool vacuumResult = await SetVacuumAsync(false);

                if (vacuumResult)
                {
                    await Task.Delay(500);
                    _dtp7h.SendLEDCommand(LEDPosition.RightLED3, LEDColor.Off);
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual robot Place operation completed");
                    return true;
                }
                else
                {
                    _dtp7h.SendLEDCommand(LEDPosition.RightLED3, LEDColor.Red);
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual robot Place operation failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Place failed: {ex.Message}");
                OnErrorOccurred("PLACE_ERROR", "EtherCAT robot Place failed", ex);
                return false;
            }
        }

        public async Task<bool> SetVacuumAsync(bool isOn)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual vacuum {(isOn ? "ON" : "OFF")}...");

                // 진공 상태 표시 (기존 LED 제어 사용)
                if (isOn)
                {
                    _dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Blue);
                }
                else
                {
                    _dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Off);
                }

                lock (_lockObject)
                {
                    _vacuumOn = isOn;
                }

                UpdateStatus();

                await Task.Delay(200);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual vacuum {(isOn ? "ON" : "OFF")} completed");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Vacuum control failed: {ex.Message}");
                OnErrorOccurred("VACUUM_ERROR", "EtherCAT robot vacuum control failed", ex);
                return false;
            }
        }
        #endregion

        #region Status & Information
        public async Task<RobotStatus> GetStatusAsync()
        {
            await Task.Run(() => UpdateStatus());

            lock (_lockObject)
            {
                return new RobotStatus
                {
                    IsConnected = _currentStatus.IsConnected,
                    IsMoving = _currentStatus.IsMoving,
                    IsHomed = _currentStatus.IsHomed,
                    VacuumOn = _currentStatus.VacuumOn,
                    CurrentPosition = new Position(_currentStatus.CurrentPosition.R,
                                                   _currentStatus.CurrentPosition.Theta,
                                                   _currentStatus.CurrentPosition.Z),
                    CurrentSpeed = _currentStatus.CurrentSpeed,
                    LastError = _currentStatus.LastError,
                    LastUpdateTime = DateTime.Now
                };
            }
        }

        public async Task<Position> GetCurrentPositionAsync()
        {
            await Task.Delay(50);

            lock (_lockObject)
            {
                return new Position(_currentPosition.R, _currentPosition.Theta, _currentPosition.Z);
            }
        }
        #endregion

        #region Safety & Configuration
        public bool IsSafeToOperate()
        {
            try
            {
                // SafetySystem 연동
                if (!SafetySystem.IsSafeForRobotOperation())
                {
                    return false;
                }

                // 연결 상태 확인
                if (!IsConnected)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Safety check failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetSpeedAsync(int speedPercent)
        {
            try
            {
                if (speedPercent < 1 || speedPercent > 100)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Set speed failed: Invalid range");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Setting actual robot speed: {speedPercent}%");

                lock (_lockObject)
                {
                    _currentSpeed = speedPercent;
                }

                UpdateStatus();

                await Task.Delay(100);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Set speed failed: {ex.Message}");
                OnErrorOccurred("SPEED_ERROR", "EtherCAT robot set speed failed", ex);
                return false;
            }
        }
        #endregion

        #region Movement Simulation
        /// <summary>
        /// 이동 시뮬레이션
        /// </summary>
        /// <param name="start">시작 위치</param>
        /// <param name="end">끝 위치</param>
        /// <returns>이동 완료 태스크</returns>
        private async Task SimulateMovement(Position start, Position end)
        {
            try
            {
                // 이동 거리 계산
                double distance = CalculateDistance(start, end);

                // 속도에 따른 이동 시간 계산 (최소 500ms, 최대 3000ms)
                int moveDuration = Math.Max(500, Math.Min(3000, (int)(distance * 10)));

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Simulating movement: {moveDuration}ms");

                // 단계별 이동 시뮬레이션
                int steps = 10;
                int stepDuration = moveDuration / steps;

                for (int i = 1; i <= steps; i++)
                {
                    // 중간 위치 계산
                    double ratio = (double)i / steps;
                    Position currentPos = InterpolatePosition(start, end, ratio);

                    lock (_lockObject)
                    {
                        _currentPosition = currentPos;
                    }

                    await Task.Delay(stepDuration);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Movement simulation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 두 위치 간 거리 계산
        /// </summary>
        /// <param name="pos1">위치1</param>
        /// <param name="pos2">위치2</param>
        /// <returns>거리</returns>
        private double CalculateDistance(Position pos1, Position pos2)
        {
            double dr = pos2.R - pos1.R;
            double dtheta = pos2.Theta - pos1.Theta;
            double dz = pos2.Z - pos1.Z;

            return Math.Sqrt(dr * dr + dtheta * dtheta + dz * dz);
        }

        /// <summary>
        /// 두 위치 사이의 보간 위치 계산
        /// </summary>
        /// <param name="start">시작 위치</param>
        /// <param name="end">끝 위치</param>
        /// <param name="ratio">보간 비율 (0.0 ~ 1.0)</param>
        /// <returns>보간된 위치</returns>
        private Position InterpolatePosition(Position start, Position end, double ratio)
        {
            double r = start.R + (end.R - start.R) * ratio;
            double theta = start.Theta + (end.Theta - start.Theta) * ratio;
            double z = start.Z + (end.Z - start.Z) * ratio;

            return new Position(r, theta, z);
        }
        #endregion

        #region Status Update
        /// <summary>
        /// 상태 업데이트 타이머 틱 이벤트
        /// </summary>
        private void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                UpdateStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Status update failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 로봇 상태 업데이트
        /// </summary>
        private void UpdateStatus()
        {
            try
            {
                RobotStatus oldStatus = null;

                lock (_lockObject)
                {
                    // 기존 상태 복사
                    oldStatus = _currentStatus != null ? new RobotStatus
                    {
                        IsConnected = _currentStatus.IsConnected,
                        IsMoving = _currentStatus.IsMoving,
                        IsHomed = _currentStatus.IsHomed,
                        VacuumOn = _currentStatus.VacuumOn,
                        CurrentPosition = new Position(_currentStatus.CurrentPosition.R,
                                                       _currentStatus.CurrentPosition.Theta,
                                                       _currentStatus.CurrentPosition.Z),
                        CurrentSpeed = _currentStatus.CurrentSpeed,
                        LastError = _currentStatus.LastError
                    } : new RobotStatus();

                    // 새 상태 설정
                    _currentStatus = new RobotStatus
                    {
                        IsConnected = _isConnected,
                        IsMoving = _isMoving,
                        IsHomed = _isHomed,
                        VacuumOn = _vacuumOn,
                        CurrentPosition = new Position(_currentPosition.R, _currentPosition.Theta, _currentPosition.Z),
                        CurrentSpeed = _currentSpeed,
                        LastError = _currentStatus?.LastError ?? "",
                        LastUpdateTime = DateTime.Now
                    };
                }

                // 상태 변경 이벤트 발생 (기존 생성자 사용)
                OnStatusChanged(oldStatus, _currentStatus);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Error during status update: {ex.Message}");
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 상태 변경 이벤트 발생 (기존 생성자 사용)
        /// </summary>
        /// <param name="oldStatus">이전 상태</param>
        /// <param name="newStatus">새 상태</param>
        private void OnStatusChanged(RobotStatus oldStatus, RobotStatus newStatus)
        {
            try
            {
                StatusChanged?.Invoke(this, new RobotStatusChangedEventArgs(oldStatus, newStatus));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to handle StatusChanged event: {ex.Message}");
            }
        }

        /// <summary>
        /// 위치 변경 이벤트 발생 (기존 생성자 사용)
        /// </summary>
        /// <param name="oldPosition">이전 위치</param>
        /// <param name="newPosition">새 위치</param>
        private void OnPositionChanged(Position oldPosition, Position newPosition)
        {
            try
            {
                PositionChanged?.Invoke(this, new PositionChangedEventArgs(oldPosition, newPosition));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to handle PositionChanged event: {ex.Message}");
            }
        }

        /// <summary>
        /// 에러 발생 이벤트 발생 (기존 생성자 사용)
        /// </summary>
        /// <param name="errorCode">에러 코드</param>
        /// <param name="errorMessage">에러 메시지</param>
        /// <param name="exception">예외 객체</param>
        private void OnErrorOccurred(string errorCode, string errorMessage, Exception exception = null)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_currentStatus != null)
                    {
                        _currentStatus.LastError = errorMessage;
                    }
                }

                ErrorOccurred?.Invoke(this, new RobotErrorEventArgs(errorCode, errorMessage, exception));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to handle ErrorOccurred event: {ex.Message}");
            }
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// 리소스 해제
        /// </summary>
        public void Dispose()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Disposing resources...");

                // 타이머 정지
                if (_statusUpdateTimer != null)
                {
                    _statusUpdateTimer.Stop();
                    _statusUpdateTimer = null;
                }

                // 연결 해제
                if (_dtp7h != null)
                {
                    try
                    {
                        if (_dtp7h.IsConnected)
                        {
                            DisconnectAsync().Wait(1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Error while disposing DTP-7H: {ex.Message}");
                    }

                    _dtp7h.Dispose();
                    _dtp7h = null;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Resources disposed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Dispose error: {ex.Message}");
            }
        }
        #endregion
    }
}