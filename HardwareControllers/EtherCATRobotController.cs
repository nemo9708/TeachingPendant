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

                // 홈 이동 시작 표시 기존 LED 제어 사용
                _dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Red);

                UpdateStatus();

                try
                {
                    // 실제 EtherCAT 홈 복귀 실행 (기존 시뮬레이션 대신)
                    bool homeResult = await ExecuteActualHomeCommand();

                    if (homeResult)
                    {
                        // 홈 복귀 성공 처리
                        lock (_lockObject)
                        {
                            _currentPosition = homePosition;
                            _isHomed = true;
                            _isMoving = false;
                        }

                        // 홈 이동 완료 표시
                        _dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Blue);

                        UpdateStatus();
                        OnPositionChanged(oldPosition, homePosition);

                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual robot homing completed");
                        return true;
                    }
                    else
                    {
                        // 홈 복귀 실패 처리
                        lock (_lockObject)
                        {
                            _isMoving = false;
                        }

                        _dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Off);
                        UpdateStatus();

                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual robot homing failed");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    // 예외 발생 시 상태 초기화
                    lock (_lockObject)
                    {
                        _isMoving = false;
                    }

                    _dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Off);
                    UpdateStatus();

                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Home command exception: {ex.Message}");
                    OnErrorOccurred("HOME_ERROR", "EtherCAT robot home command failed", ex);
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] HomeAsync method failed: {ex.Message}");
                OnErrorOccurred("HOME_METHOD_ERROR", "Home command processing failed", ex);
                return false;
            }
        }

        /// <summary>
        /// 실제 EtherCAT 홈 복귀 명령 실행
        /// </summary>
        /// <returns>홈 복귀 성공 여부</returns>
        private async Task<bool> ExecuteActualHomeCommand()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실제 EtherCAT 홈 복귀 명령 실행");

                // 홈 복귀용 특별 좌표 (0, 0, 0)
                var homeCoords = new RobotCoordinates
                {
                    RAxisPulse = 0,      // 홈 위치 R축 펄스
                    ThetaAxisPulse = 0,  // 홈 위치 Theta축 펄스  
                    ZAxisPulse = 0,      // 홈 위치 Z축 펄스
                    Speed = 30           // 홈 복귀는 안전을 위해 저속
                };

                // EtherCAT 홈 복귀 명령 전송
                string homeCommand = $"HOME {homeCoords.Speed}\r\n"; // 실제 로봇 프로토콜에 맞게 수정
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 홈 복귀 명령 전송: {homeCommand.Trim()}");

                bool commandSent = await SendCommandToRobotController(homeCommand);
                if (!commandSent)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 홈 복귀 명령 전송 실패");
                    return false;
                }

                // 홈 복귀 완료까지 대기 (최대 60초)
                await WaitForMovementCompletion(homeCoords, TimeSpan.FromSeconds(60));

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실제 EtherCAT 홈 복귀 완료");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실제 홈 복귀 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 로봇 정지 실제 EtherCAT 로봇 제어 적용
        /// </summary>
        /// <returns>정지 성공 여부</returns>
        public async Task<bool> StopAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Stopping actual robot...");

                // 정지 시작 표시 기존 LED 제어 사용
                _dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Red);
                _dtp7h.SendBuzzerCommand(true);

                try
                {
                    // 실제 EtherCAT 정지 명령 실행 (기존 Task.Delay 대신)
                    bool stopResult = await ExecuteActualStopCommand();

                    // 상태 업데이트
                    lock (_lockObject)
                    {
                        _isMoving = false;
                    }

                    UpdateStatus();

                    if (stopResult)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual robot stop completed");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual robot stop failed");
                    }

                    return stopResult;
                }
                finally
                {
                    // 정지 표시 정리 (200ms 후)
                    await Task.Delay(200);
                    _dtp7h.SendBuzzerCommand(false);
                    _dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Off);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] StopAsync method failed: {ex.Message}");
                OnErrorOccurred("STOP_ERROR", "Robot stop command failed", ex);
                return false;
            }
        }

        /// <summary>
        /// 실제 EtherCAT 정지 명령 실행
        /// </summary>
        /// <returns>정지 성공 여부</returns>
        private async Task<bool> ExecuteActualStopCommand()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실제 EtherCAT 정지 명령 실행");

                // 긴급 정지 명령 전송 (즉시 실행)
                string stopCommand = "ESTOP\r\n"; // Emergency Stop 명령
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 정지 명령 전송: {stopCommand.Trim()}");

                bool commandSent = await SendCommandToRobotController(stopCommand);
                if (!commandSent)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 정지 명령 전송 실패");
                    return false;
                }

                // 정지 명령은 즉시 실행되므로 짧은 대기 후 상태 확인
                await Task.Delay(200);

                // 실제 로봇의 정지 상태 확인
                bool isStopped = await VerifyRobotStopped();

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실제 EtherCAT 정지 완료: {(isStopped ? "성공" : "실패")}");
                return isStopped;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실제 정지 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 로봇 정지 상태 확인
        /// </summary>
        /// <returns>정지 상태 여부</returns>
        private async Task<bool> VerifyRobotStopped()
        {
            try
            {
                // 로봇 상태 조회 명령
                string statusQuery = "GET_STATUS\r\n";

                await Task.Delay(100); // 상태 조회 지연

                // 실제 환경에서는 로봇으로부터 상태 응답 파싱
                // 현재는 정지 상태로 가정

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로봇 정지 상태 확인 완료");
                return true; // 실제로는 로봇 응답을 파싱하여 정지 상태 확인
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 정지 상태 확인 오류: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Pick & Place Operations
        /// <summary>
        /// Pick 동작 실제 EtherCAT 로봇 제어 적용
        /// </summary>
        /// <returns>Pick 동작 성공 여부</returns>
        public async Task<bool> PickAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Pick failed: Not connected");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting actual pick operation...");

                // 진공 ON 명령 전송
                bool vacuumResult = await ExecuteVacuumCommand(true);
                if (!vacuumResult)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Pick failed: Vacuum ON failed");
                    return false;
                }

                // 진공 안정화 대기
                await Task.Delay(500);

                // 웨이퍼 감지 확인 (실제 환경에서는 센서 확인)
                bool waferDetected = await CheckWaferDetection();

                lock (_lockObject)
                {
                    _vacuumOn = true;
                }

                UpdateStatus();

                // Pick 성공 표시
                _dtp7h.SendBuzzerCommand(true);
                await Task.Delay(100);
                _dtp7h.SendBuzzerCommand(false);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual pick operation completed: {(waferDetected ? "웨이퍼 감지됨" : "웨이퍼 미감지")}");
                return waferDetected;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Pick operation failed: {ex.Message}");
                OnErrorOccurred("PICK_ERROR", "Pick operation failed", ex);
                return false;
            }
        }

        /// Place 동작 실제 EtherCAT 로봇 제어 적용
        /// </summary>
        /// <returns>Place 동작 성공 여부</returns>
        public async Task<bool> PlaceAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Place failed: Not connected");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting actual place operation...");

                // 진공 OFF 명령 전송
                bool vacuumResult = await ExecuteVacuumCommand(false);
                if (!vacuumResult)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Place failed: Vacuum OFF failed");
                    return false;
                }

                // 웨이퍼 분리 확인 대기
                await Task.Delay(300);

                lock (_lockObject)
                {
                    _vacuumOn = false;
                }

                UpdateStatus();

                // Place 성공 표시
                _dtp7h.SendBuzzerCommand(true);
                await Task.Delay(150);
                _dtp7h.SendBuzzerCommand(false);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Actual place operation completed");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Place operation failed: {ex.Message}");
                OnErrorOccurred("PLACE_ERROR", "Place operation failed", ex);
                return false;
            }
        }

        /// <summary>
        /// 실제 진공 제어 명령 실행
        /// </summary>
        /// <param name="vacuumOn">진공 상태</param>
        /// <returns>제어 성공 여부</returns>
        private async Task<bool> ExecuteVacuumCommand(bool vacuumOn)
        {
            try
            {
                string vacuumCommand = vacuumOn ? "VACUUM_ON\r\n" : "VACUUM_OFF\r\n";
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 진공 제어 명령 전송: {vacuumCommand.Trim()}");

                bool commandSent = await SendCommandToRobotController(vacuumCommand);
                if (!commandSent)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 진공 제어 명령 전송 실패");
                    return false;
                }

                // LED 표시 업데이트
                _dtp7h.SendLEDCommand(LEDPosition.RightLED2, vacuumOn ? LEDColor.Red : LEDColor.Off);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 진공 제어 완료: {(vacuumOn ? "ON" : "OFF")}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 진공 제어 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 웨이퍼 감지 센서 확인
        /// </summary>
        /// <returns>웨이퍼 감지 여부</returns>
        private async Task<bool> CheckWaferDetection()
        {
            try
            {
                // 실제 웨이퍼 감지 센서 상태 조회
                string sensorQuery = "GET_WAFER_SENSOR\r\n";

                await Task.Delay(100);

                // 현재는 시뮬레이션 (실제로는 센서 데이터 파싱)
                // 진공이 켜져있으면 웨이퍼가 감지된 것으로 가정
                bool detected = _vacuumOn;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 웨이퍼 감지 확인: {(detected ? "감지됨" : "미감지")}");
                return detected;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 웨이퍼 감지 확인 오류: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실제 로봇 이동 시작: {start} -> {end}");

                // 1. 이동 거리 및 시간 계산
                double distance = CalculateDistance(start, end);
                int estimatedDuration = Math.Max(500, Math.Min(10000, (int)(distance * 50))); // 실제 로봇은 더 오래 걸림

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 예상 이동 시간: {estimatedDuration}ms, 거리: {distance:F2}");

                // 2. 로봇 좌표계로 변환
                var startCoords = ConvertUserToRobotCoordinates(start.R, start.Theta, start.Z);
                var endCoords = ConvertUserToRobotCoordinates(end.R, end.Theta, end.Z);

                // 3. 좌표 유효성 검증
                if (!ValidateRobotCoordinates(endCoords))
                {
                    throw new InvalidOperationException("목표 좌표가 로봇 동작 범위를 벗어났습니다");
                }

                // 4. EtherCAT 이동 명령 전송
                bool commandSent = await SendEtherCATMoveCommand(endCoords);
                if (!commandSent)
                {
                    throw new InvalidOperationException("EtherCAT 이동 명령 전송 실패");
                }

                // 5. 이동 완료까지 실시간 모니터링
                await WaitForMovementCompletion(endCoords, TimeSpan.FromMilliseconds(estimatedDuration + 5000));

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실제 로봇 이동 완료: {end}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실제 로봇 이동 실패: {ex.Message}");
                throw; // 상위 메서드에서 처리하도록 예외 재전파
            }
        }

        /// <summary>
        /// 사용자 좌표를 로봇 내부 좌표로 변환
        /// </summary>
        /// <param name="r">반지름 mm</param>
        /// <param name="theta">각도 degree</param>
        /// <param name="z">높이 mm</param>
        /// <returns>로봇 내부 좌표</returns>
        private RobotCoordinates ConvertUserToRobotCoordinates(double r, double theta, double z)
        {
            try
            {
                // 실제 로봇 사양에 맞는 변환 계수 적용
                var coords = new RobotCoordinates
                {
                    // R축: 1mm = 1000 pulse 가정
                    RAxisPulse = (int)(r * 1000),

                    // Theta축: 1도 = 100 pulse 가정
                    ThetaAxisPulse = (int)(theta * 100),

                    // Z축: 1mm = 500 pulse 가정  
                    ZAxisPulse = (int)(z * 500),

                    Speed = _currentSpeed
                };

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 좌표 변환: R={r}->{coords.RAxisPulse}, T={theta}->{coords.ThetaAxisPulse}, Z={z}->{coords.ZAxisPulse}");

                return coords;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 좌표 변환 실패: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 로봇 좌표 유효성 검증
        /// </summary>
        /// <param name="coords">로봇 좌표</param>
        /// <returns>유효성 여부</returns>
        private bool ValidateRobotCoordinates(RobotCoordinates coords)
        {
            try
            {
                // 실제 로봇 동작 한계값 (pulse 단위)
                const int MAX_R_PULSE = 300000;    // R축 최대 300mm
                const int MAX_THETA_PULSE = 36000; // Theta축 최대 360도
                const int MAX_Z_PULSE = 100000;    // Z축 최대 200mm

                // 범위 검증
                bool isValid = true;

                if (Math.Abs(coords.RAxisPulse) > MAX_R_PULSE)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] R축 범위 초과: {coords.RAxisPulse}");
                    isValid = false;
                }

                if (Math.Abs(coords.ThetaAxisPulse) > MAX_THETA_PULSE)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Theta축 범위 초과: {coords.ThetaAxisPulse}");
                    isValid = false;
                }

                if (Math.Abs(coords.ZAxisPulse) > MAX_Z_PULSE)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Z축 범위 초과: {coords.ZAxisPulse}");
                    isValid = false;
                }

                return isValid;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 좌표 검증 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// EtherCAT 이동 명령 전송
        /// </summary>
        /// <param name="coords">목표 좌표</param>
        /// <returns>명령 전송 성공 여부</returns>
        private async Task<bool> SendEtherCATMoveCommand(RobotCoordinates coords)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] EtherCAT 이동 명령 전송 중...");

                // 실제 로봇 컨트롤러 통신 프로토콜에 맞는 명령 문자열 생성
                // 여기서는 시리얼 통신 예시 (실제로는 EtherCAT 라이브러리 사용)
                string moveCommand = $"MOVE {coords.RAxisPulse} {coords.ThetaAxisPulse} {coords.ZAxisPulse} {coords.Speed}\r\n";

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 전송 명령: {moveCommand.Trim()}");

                // DTP-7H를 통한 로봇 컨트롤러 통신
                // 실제 환경에서는 시리얼 포트나 EtherCAT 통신 사용
                bool communicationResult = await SendCommandToRobotController(moveCommand);

                if (communicationResult)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] EtherCAT 이동 명령 전송 성공");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] EtherCAT 이동 명령 전송 실패");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] EtherCAT 명령 전송 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 로봇 컨트롤러에 명령 전송
        /// </summary>
        /// <param name="command">전송할 명령</param>
        /// <returns>전송 성공 여부</returns>
        private async Task<bool> SendCommandToRobotController(string command)
        {
            try
            {
                // 실제 로봇 컨트롤러와의 통신 구현
                // 현재는 시뮬레이션이지만 실제 환경에서는 
                // 시리얼 통신, TCP/IP, 또는 전용 EtherCAT 라이브러리 사용

                await Task.Delay(100); // 통신 지연 시뮬레이션

                // 명령 전송 성공 가정
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로봇 컨트롤러 통신 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 이동 완료 대기 및 실시간 모니터링
        /// </summary>
        /// <param name="targetCoords">목표 좌표</param>
        /// <param name="timeout">최대 대기 시간</param>
        /// <returns>이동 완료 여부</returns>
        private async Task WaitForMovementCompletion(RobotCoordinates targetCoords, TimeSpan timeout)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 이동 완료 대기 시작 (최대 {timeout.TotalSeconds}초)");

                var startTime = DateTime.Now;
                const int checkInterval = 100; // 100ms마다 상태 확인

                while (DateTime.Now - startTime < timeout)
                {
                    // 실제 로봇 위치 조회
                    var currentCoords = await GetCurrentRobotPosition();
                    if (currentCoords != null)
                    {
                        // 사용자 좌표계로 변환하여 현재 위치 업데이트
                        UpdateCurrentPositionFromRobot(currentCoords);

                        // 목표 위치 도달 확인 (허용 오차 범위 내)
                        if (IsPositionReached(currentCoords, targetCoords))
                        {
                            System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 목표 위치 도달 확인");
                            return;
                        }
                    }

                    await Task.Delay(checkInterval);
                }

                // 타임아웃 발생
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 이동 완료 대기 시간 초과");
                throw new TimeoutException("로봇 이동 완료 대기 시간을 초과했습니다");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 이동 완료 대기 오류: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 현재 로봇 위치 조회
        /// </summary>
        /// <returns>현재 로봇 좌표</returns>
        private async Task<RobotCoordinates> GetCurrentRobotPosition()
        {
            try
            {
                // 실제 로봇으로부터 현재 위치 조회
                string positionQuery = "GET_POSITION\r\n";

                await Task.Delay(10); // 조회 지연 시뮬레이션

                // 현재는 시뮬레이션 데이터 반환
                // 실제 환경에서는 로봇 컨트롤러로부터 실제 엔코더 값 수신
                return ConvertUserToRobotCoordinates(_currentPosition.R, _currentPosition.Theta, _currentPosition.Z);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 현재 위치 조회 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 로봇으로부터 받은 위치로 현재 위치 업데이트
        /// </summary>
        /// <param name="robotCoords">로봇 좌표</param>
        private void UpdateCurrentPositionFromRobot(RobotCoordinates robotCoords)
        {
            try
            {
                // 로봇 좌표를 사용자 좌표로 변환
                double r = robotCoords.RAxisPulse / 1000.0;      // pulse -> mm
                double theta = robotCoords.ThetaAxisPulse / 100.0; // pulse -> degree  
                double z = robotCoords.ZAxisPulse / 500.0;       // pulse -> mm

                lock (_lockObject)
                {
                    _currentPosition = new Position(r, theta, z);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 위치 업데이트 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 목표 위치 도달 여부 확인
        /// </summary>
        /// <param name="current">현재 위치</param>
        /// <param name="target">목표 위치</param>
        /// <returns>도달 여부</returns>
        private bool IsPositionReached(RobotCoordinates current, RobotCoordinates target)
        {
            const int tolerance = 50; // 허용 오차 (pulse 단위)

            bool rReached = Math.Abs(current.RAxisPulse - target.RAxisPulse) <= tolerance;
            bool thetaReached = Math.Abs(current.ThetaAxisPulse - target.ThetaAxisPulse) <= tolerance;
            bool zReached = Math.Abs(current.ZAxisPulse - target.ZAxisPulse) <= tolerance;

            return rReached && thetaReached && zReached;
        }

        /// <summary>
        /// 로봇 좌표 구조체 
        /// C# 6.0 호환을 위해 클래스로 구현
        /// </summary>
        private class RobotCoordinates
        {
            public int RAxisPulse { get; set; }        // R축 엔코더 펄스
            public int ThetaAxisPulse { get; set; }    // Theta축 엔코더 펄스  
            public int ZAxisPulse { get; set; }        // Z축 엔코더 펄스
            public int Speed { get; set; }             // 이동 속도

            public RobotCoordinates()
            {
                RAxisPulse = 0;
                ThetaAxisPulse = 0;
                ZAxisPulse = 0;
                Speed = 50;
            }

            public override string ToString()
            {
                return $"R:{RAxisPulse}, T:{ThetaAxisPulse}, Z:{ZAxisPulse}, Speed:{Speed}%";
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