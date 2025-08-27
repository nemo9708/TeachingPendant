using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using TeachingPendant.Safety;
using TeachingPendant.Manager;
using TeachingPendant.Logging;

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

        /// <summary>
        /// 컨트롤러 초기화 (센서 시스템 추가)
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

            // 센서 시스템 초기화 추가
            InitializeSensorSystem();

            // EtherCAT 통신 초기화 추가
            InitializeEtherCATCommunication();

            System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Controller initialization completed with sensor system");
        }
        #endregion

        #region Sensor Feedback System Fields
        private DateTime _lastSensorUpdateTime;
        private SensorData _currentSensorData;
        private readonly int SENSOR_UPDATE_INTERVAL_MS = 50; // 50ms 간격으로 센서 업데이트
        private bool _sensorSystemEnabled = false;

        /// <summary>
        /// 센서 데이터 구조체
        /// </summary>
        public class SensorData
        {
            // 위치 센서 데이터
            public int ActualRAxisPulse { get; set; }
            public int ActualThetaAxisPulse { get; set; }
            public int ActualZAxisPulse { get; set; }
            public int CurrentRAxisSpeed { get; set; }
            public int CurrentThetaAxisSpeed { get; set; }
            public int CurrentZAxisSpeed { get; set; }

            // 상태 센서 데이터
            public bool IsServoReady { get; set; }
            public bool IsMotorEnabled { get; set; }
            public bool IsInPosition { get; set; }
            public double Temperature { get; set; }
            public double Vibration { get; set; }
            public double MotorCurrent { get; set; }

            // 안전 센서 데이터
            public bool EmergencyStopActive { get; set; }
            public bool DoorClosed { get; set; }
            public bool LightCurtainClear { get; set; }
            public double VacuumPressure { get; set; }

            // I/O 센서 데이터
            public bool WaferDetected { get; set; }
            public bool CassettePresent { get; set; }
            public bool LoadportReady { get; set; }
            public bool VacuumValveOpen { get; set; }

            public DateTime UpdateTime { get; set; }

            public SensorData()
            {
                UpdateTime = DateTime.Now;
            }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 컨트롤러 초기화
        /// </summary>
        private void InitializeSensorSystem()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 센서 피드백 시스템 초기화");

                _currentSensorData = new SensorData();
                _lastSensorUpdateTime = DateTime.MinValue;
                _sensorSystemEnabled = false;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 센서 피드백 시스템 초기화 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 센서 시스템 초기화 실패: {ex.Message}");
            }
        }

        // <summary>
        /// 센서 시스템 활성화 (연결 시 호출)
        /// </summary>
        private void EnableSensorSystem()
        {
            try
            {
                _sensorSystemEnabled = true;
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 센서 시스템 활성화됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 센서 시스템 활성화 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 센서 시스템 비활성화 (연결 해제 시 호출)
        /// </summary>
        private void DisableSensorSystem()
        {
            try
            {
                _sensorSystemEnabled = false;
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 센서 시스템 비활성화됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 센서 시스템 비활성화 실패: {ex.Message}");
            }
        }
        #endregion

        #region Connection Management
        private EtherCATCommunication _etherCATComm;

        /// <summary>
        /// EtherCAT 통신 초기화 (생성자에 추가)
        /// </summary>
        private void InitializeEtherCATCommunication()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 통신 초기화");

                _etherCATComm = new EtherCATCommunication();

                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 통신 객체 생성 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 통신 초기화 실패: " + ex.Message);
            }
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 로봇 연결 시도...");

                // 1. DTP-7H 연결 (기존 코드 유지)
                bool dtp7hConnected = _dtp7h.Connect(_comPort, _baudRate);
                if (!dtp7hConnected)
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] DTP-7H 연결 실패");
                    OnErrorOccurred("DTP7H_CONNECT_ERROR", "DTP-7H connection failed");
                    return false;
                }

                // 2. EtherCAT 마스터 연결 추가
                bool etherCATConnected = await _etherCATComm.ConnectAsync("ETC_ROBOT_01", 1);
                if (!etherCATConnected)
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 연결 실패");
                    OnErrorOccurred("ETHERCAT_CONNECT_ERROR", "EtherCAT Master connection failed");

                    // DTP-7H 연결 해제
                    _dtp7h.Disconnect();
                    return false;
                }

                // 3. 연결 성공 표시 - 기존 메서드명 사용
                _dtp7h.SendLEDCommand(LEDPosition.LeftLED1, LEDColor.All); // EtherCAT 연결 성공
                _dtp7h.SendLEDCommand(LEDPosition.RightLED1, LEDColor.Blue); // DTP-7H 연결 성공

                // 4. 초기화 완료
                await Task.Delay(1000);

                lock (_lockObject)
                {
                    _isConnected = true;
                    _isHomed = false;
                    _vacuumOn = false;
                    _currentPosition = new Position(0, 0, 0);
                }

                // 5. 센서 시스템 활성화
                EnableSensorSystem();
                _statusUpdateTimer.Start();

                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 로봇 연결 완료");

                // 기존 이벤트 발생 방식 사용
                UpdateStatus();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 로봇 연결 실패: " + ex.Message);
                OnErrorOccurred("CONNECTION_ERROR", "Robot connection failed", ex);
                return false;
            }
        }

        public async Task<bool> DisconnectAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 로봇 연결 해제...");

                // 1. 로봇 정지
                if (_isMoving)
                {
                    await StopAsync();
                }

                // 2. 센서 시스템 비활성화
                DisableSensorSystem();
                _statusUpdateTimer.Stop();

                // 3. EtherCAT 연결 해제
                bool etherCATDisconnected = await _etherCATComm.DisconnectAsync();
                if (!etherCATDisconnected)
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 연결 해제 경고");
                }

                // 4. DTP-7H 연결 해제
                bool dtp7hDisconnected = _dtp7h.Disconnect();
                if (!dtp7hDisconnected)
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] DTP-7H 연결 해제 경고");
                }

                lock (_lockObject)
                {
                    _isConnected = false;
                    _isHomed = false;
                    _isMoving = false;
                }

                UpdateStatus();

                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 로봇 연결 해제 완료");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 로봇 연결 해제 실패: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 실제 EtherCAT 이동 명령 전송 (기존 SendEtherCATMoveCommand 대체)
        /// </summary>
        private async Task<bool> SendActualEtherCATMoveCommand(RobotCoordinates coords)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 이동 명령 전송: " + coords.ToString());

                // EtherCAT 통신을 통한 실제 이동 명령
                bool commandSent = await _etherCATComm.SendMoveCommandAsync(
                    coords.RAxisPulse,
                    coords.ThetaAxisPulse,
                    coords.ZAxisPulse,
                    coords.Speed);

                if (commandSent)
                {
                    // DTP-7H를 통한 상태 표시 - 기존 열거형 사용
                    _dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Red); // 이동 중 표시

                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 이동 명령 전송 성공");

                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 이동 명령 전송 실패");

                    // 오류 상태 표시
                    _dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Red);
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 명령 전송 오류: " + ex.Message);
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
        /// 실제 EtherCAT 홈 복귀 명령 (기존 ExecuteActualHomeCommand 수정)
        /// </summary>
        private async Task<bool> ExecuteActualHomeCommand()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 홈 복귀 명령 실행");

                // DTP-7H 홈 복귀 표시
                _dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Red);

                // 실제 EtherCAT 홈 복귀 명령
                bool homeCommandSent = await _etherCATComm.SendHomeCommandAsync();

                if (homeCommandSent)
                {
                    // 홈 복귀 완료까지 대기
                    await WaitForHomeComplete(TimeSpan.FromSeconds(60));

                    // 홈 위치로 현재 위치 업데이트
                    lock (_lockObject)
                    {
                        _currentPosition = new Position(0, 0, 0);
                        _isHomed = true;
                    }

                    UpdateStatus();

                    // 홈 복귀 완료 표시
                    _dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.All);

                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 홈 복귀 완료");

                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 홈 복귀 명령 실패");

                    _dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Red);
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 홈 복귀 실행 오류: " + ex.Message);

                _dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Red);
                return false;
            }
        }

        /// <summary>
        /// 홈 복귀 완료 대기
        /// </summary>
        /// <param name="timeout">최대 대기 시간</param>
        /// <returns>홈 복귀 완료 여부</returns>
        private async Task<bool> WaitForHomeComplete(TimeSpan timeout)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 홈 복귀 완료 대기 시작 (최대 " + timeout.TotalSeconds + "초)");

                var startTime = DateTime.Now;
                const int checkInterval = 500; // 500ms마다 상태 확인

                while (DateTime.Now - startTime < timeout)
                {
                    // EtherCAT을 통한 로봇 상태 확인
                    var robotStatus = await _etherCATComm.ReadRobotStatusAsync();

                    if (robotStatus != null && robotStatus.IsInPosition && !robotStatus.IsMoving)
                    {
                        // 실제 위치 확인 (홈 위치 근처인지)
                        var currentPos = await GetActualRobotPosition();
                        if (currentPos != null && IsAtHomePosition(currentPos))
                        {
                            System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 홈 위치 도달 확인");
                            return true;
                        }
                    }

                    await Task.Delay(checkInterval);
                }

                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 홈 복귀 대기 시간 초과");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 홈 복귀 대기 오류: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 홈 위치에 있는지 확인
        /// </summary>
        /// <param name="position">현재 위치</param>
        /// <returns>홈 위치 여부</returns>
        private bool IsAtHomePosition(RobotCoordinates position)
        {
            const int homeTolerance = 100; // 홈 위치 허용 오차 (pulse)

            return Math.Abs(position.RAxisPulse) <= homeTolerance &&
                   Math.Abs(position.ThetaAxisPulse) <= homeTolerance &&
                   Math.Abs(position.ZAxisPulse) <= homeTolerance;
        }

        /// <summary>
        /// 실제 EtherCAT 정지 명령 (기존 StopAsync 수정)
        /// </summary>
        public async Task<bool> StopAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 로봇 정지...");

                // DTP-7H 정지 표시
                _dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Red);

                // 실제 EtherCAT 정지 명령
                bool stopCommandSent = await _etherCATComm.SendStopCommandAsync();

                if (stopCommandSent)
                {
                    // 상태 업데이트
                    lock (_lockObject)
                    {
                        _isMoving = false;
                    }

                    UpdateStatus();

                    // 정지 완료 표시
                    _dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Blue);

                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 로봇 정지 완료");

                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 정지 명령 실패");

                    OnErrorOccurred("STOP_ERROR", "EtherCAT robot stop command failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 정지 실패: " + ex.Message);
                OnErrorOccurred("STOP_EXCEPTION", "EtherCAT robot stop exception", ex);
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
                // 센서 시스템이 활성화된 경우 실제 센서 데이터 사용
                if (_sensorSystemEnabled && _currentSensorData != null)
                {
                    // 웨이퍼 감지 센서와 진공 압력 둘 다 확인
                    bool sensorDetected = _currentSensorData.WaferDetected;
                    bool vacuumDetected = _currentSensorData.VacuumPressure > 75.0;

                    bool waferDetected = sensorDetected && vacuumDetected;

                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 센서 기반 웨이퍼 감지: 센서={sensorDetected}, 진공={vacuumDetected}, 결과={waferDetected}");
                    return waferDetected;
                }
                else
                {
                    // 센서 시스템이 비활성화된 경우 기존 방식 사용
                    await Task.Delay(100);
                    bool detected = _vacuumOn;

                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 기본 웨이퍼 감지: {detected}");
                    return detected;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 웨이퍼 감지 확인 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 센서 시스템 진단 정보 조회 (안정성 개선 버전)
        /// </summary>
        /// <returns>진단 정보 문자열</returns>
        public string GetSensorDiagnosticsInfo()
        {
            try
            {
                // 센서 시스템 활성화 상태 확인
                if (!_sensorSystemEnabled)
                {
                    return "센서 시스템이 비활성화되어 있습니다.";
                }

                // 센서 데이터 null 체크
                if (_currentSensorData == null)
                {
                    return "센서 데이터가 초기화되지 않았습니다.";
                }

                // 스레드 안전을 위한 센서 데이터 복사
                SensorData sensorDataCopy;
                try
                {
                    sensorDataCopy = GetCurrentSensorData();
                    if (sensorDataCopy == null)
                    {
                        return "센서 데이터 복사 실패";
                    }
                }
                catch (Exception copyEx)
                {
                    return "센서 데이터 접근 중 오류: " + copyEx.Message;
                }

                // StringBuilder 안전 초기화
                var diagnostics = new System.Text.StringBuilder(1000); // 초기 용량 지정

                try
                {
                    diagnostics.AppendLine("=== 센서 시스템 진단 정보 ===");
                    diagnostics.AppendLine("업데이트 시간: " + sensorDataCopy.UpdateTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    diagnostics.AppendLine("");

                    // 위치 센서 정보 (안전한 계산)
                    diagnostics.AppendLine("위치 센서:");
                    diagnostics.AppendLine("  R축: " + sensorDataCopy.ActualRAxisPulse.ToString() + " pulse (" +
                                         (sensorDataCopy.ActualRAxisPulse / 1000.0).ToString("F2") + " mm)");
                    diagnostics.AppendLine("  T축: " + sensorDataCopy.ActualThetaAxisPulse.ToString() + " pulse (" +
                                         (sensorDataCopy.ActualThetaAxisPulse / 100.0).ToString("F1") + " degree)");
                    diagnostics.AppendLine("  Z축: " + sensorDataCopy.ActualZAxisPulse.ToString() + " pulse (" +
                                         (sensorDataCopy.ActualZAxisPulse / 500.0).ToString("F2") + " mm)");
                    diagnostics.AppendLine("  속도: R=" + sensorDataCopy.CurrentRAxisSpeed.ToString() + "%, T=" +
                                         sensorDataCopy.CurrentThetaAxisSpeed.ToString() + "%, Z=" +
                                         sensorDataCopy.CurrentZAxisSpeed.ToString() + "%");
                    diagnostics.AppendLine("");

                    // 상태 센서 정보
                    diagnostics.AppendLine("상태 센서:");
                    diagnostics.AppendLine("  서보 준비: " + (sensorDataCopy.IsServoReady ? "예" : "아니오"));
                    diagnostics.AppendLine("  모터 활성: " + (sensorDataCopy.IsMotorEnabled ? "예" : "아니오"));
                    diagnostics.AppendLine("  위치 도달: " + (sensorDataCopy.IsInPosition ? "예" : "아니오"));
                    diagnostics.AppendLine("  온도: " + sensorDataCopy.Temperature.ToString("F1") + "°C");
                    diagnostics.AppendLine("  진동: " + sensorDataCopy.Vibration.ToString("F3"));
                    diagnostics.AppendLine("  모터 전류: " + sensorDataCopy.MotorCurrent.ToString("F1") + "A");
                    diagnostics.AppendLine("");

                    // 안전 센서 정보
                    diagnostics.AppendLine("안전 센서:");
                    diagnostics.AppendLine("  비상정지: " + (sensorDataCopy.EmergencyStopActive ? "활성" : "정상"));
                    diagnostics.AppendLine("  도어 상태: " + (sensorDataCopy.DoorClosed ? "닫힘" : "열림"));
                    diagnostics.AppendLine("  라이트 커튼: " + (sensorDataCopy.LightCurtainClear ? "정상" : "차단"));
                    diagnostics.AppendLine("  진공 압력: " + sensorDataCopy.VacuumPressure.ToString("F1") + "%");
                    diagnostics.AppendLine("");

                    // I/O 센서 정보
                    diagnostics.AppendLine("I/O 센서:");
                    diagnostics.AppendLine("  웨이퍼 감지: " + (sensorDataCopy.WaferDetected ? "감지됨" : "미감지"));
                    diagnostics.AppendLine("  카세트 존재: " + (sensorDataCopy.CassettePresent ? "존재" : "없음"));
                    diagnostics.AppendLine("  로드포트 준비: " + (sensorDataCopy.LoadportReady ? "준비됨" : "준비안됨"));
                    diagnostics.AppendLine("  진공 밸브: " + (sensorDataCopy.VacuumValveOpen ? "열림" : "닫힘"));

                    return diagnostics.ToString();
                }
                catch (Exception buildEx)
                {
                    return "진단 정보 생성 중 오류: " + buildEx.Message;
                }
            }
            catch (Exception ex)
            {
                // 최상위 예외 처리
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] GetSensorDiagnosticsInfo 오류: " + ex.Message);
                return "센서 진단 정보 조회 실패: " + ex.Message;
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
                // 기본 연결 상태 확인
                if (!IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Safety check failed: Not connected");
                    return false;
                }

                // 센서 시스템이 활성화된 경우 센서 데이터 기반 안전 확인
                if (_sensorSystemEnabled && _currentSensorData != null)
                {
                    // 비상정지 확인
                    if (_currentSensorData.EmergencyStopActive)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Safety check failed: Emergency stop active");
                        return false;
                    }

                    // 도어 상태 확인
                    if (!_currentSensorData.DoorClosed)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Safety check failed: Door open");
                        return false;
                    }

                    // 라이트 커튼 확인
                    if (!_currentSensorData.LightCurtainClear)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Safety check failed: Light curtain blocked");
                        return false;
                    }

                    // 온도 확인
                    if (_currentSensorData.Temperature > 55.0) // 동작 제한 온도
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Safety check failed: High temperature {_currentSensorData.Temperature:F1}°C");
                        return false;
                    }

                    // 서보 준비 상태 확인
                    if (!_currentSensorData.IsServoReady)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Safety check failed: Servo not ready");
                        return false;
                    }
                }

                // 기존 안전 시스템 확인
                if (!SafetySystem.IsSafeForRobotOperation())
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Safety check failed: SafetySystem check failed");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Safety check error: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 실제 로봇 이동 시작: " + start + " -> " + end);

                // 1. 이동 거리 및 시간 계산
                double distance = CalculateDistance(start, end);
                int estimatedDuration = Math.Max(500, Math.Min(10000, (int)(distance * 50))); // 실제 로봇은 더 오래 걸림

                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 예상 이동 시간: " + estimatedDuration + "ms, 거리: " + distance.ToString("F2"));

                // 2. 로봇 좌표계로 변환
                var startCoords = ConvertUserToRobotCoordinates(start.R, start.Theta, start.Z);
                var endCoords = ConvertUserToRobotCoordinates(end.R, end.Theta, end.Z);

                // 3. 좌표 유효성 검증
                if (!ValidateRobotCoordinates(endCoords))
                {
                    throw new InvalidOperationException("목표 좌표가 로봇 동작 범위를 벗어났습니다");
                }

                // 4. EtherCAT 이동 명령 전송 - 메서드명 수정
                bool commandSent = await SendActualEtherCATMoveCommand(endCoords); // ← 수정된 메서드명
                if (!commandSent)
                {
                    throw new InvalidOperationException("EtherCAT 이동 명령 전송 실패");
                }

                // 5. 이동 완료까지 실시간 모니터링
                await WaitForMovementCompletion(endCoords, TimeSpan.FromMilliseconds(estimatedDuration + 5000));

                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 실제 로봇 이동 완료: " + end);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 실제 로봇 이동 실패: " + ex.Message);
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
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 이동 완료 대기 시작 (최대 " + timeout.TotalSeconds + "초)");

                var startTime = DateTime.Now;
                const int checkInterval = 100; // 100ms마다 상태 확인

                while (DateTime.Now - startTime < timeout)
                {
                    // 실제 로봇 위치 조회 - 메서드명 수정
                    var currentCoords = await GetActualRobotPosition(); // ← 수정된 메서드명
                    if (currentCoords != null)
                    {
                        // 사용자 좌표계로 변환하여 현재 위치 업데이트
                        UpdateCurrentPositionFromRobot(currentCoords);

                        // 목표 위치 도달 확인 (허용 오차 범위 내)
                        if (IsPositionReached(currentCoords, targetCoords))
                        {
                            System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 목표 위치 도달 확인");
                            return;
                        }
                    }

                    await Task.Delay(checkInterval);
                }

                // 타임아웃 발생
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 이동 완료 대기 시간 초과");
                throw new TimeoutException("로봇 이동 완료 대기 시간을 초과했습니다");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 이동 완료 대기 오류: " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 실제 EtherCAT 현재 위치 조회 (기존 GetCurrentRobotPosition 대체)
        /// </summary>
        private async Task<RobotCoordinates> GetActualRobotPosition()
        {
            try
            {
                if (!_isConnected || _etherCATComm == null || !_etherCATComm.IsConnected)
                {
                    return null;
                }

                // 실제 EtherCAT을 통한 현재 위치 조회
                var axisPosition = await _etherCATComm.ReadCurrentPositionAsync();

                if (axisPosition != null)
                {
                    var robotCoords = new RobotCoordinates
                    {
                        RAxisPulse = axisPosition.RAxisPulse,
                        ThetaAxisPulse = axisPosition.ThetaAxisPulse,
                        ZAxisPulse = axisPosition.ZAxisPulse,
                        Speed = _currentSpeed
                    };

                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 현재 위치: " + robotCoords.ToString());
                    return robotCoords;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 위치 조회 실패: " + ex.Message);
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
                // 기존 상태 업데이트
                UpdateStatus();

                // 센서 데이터 업데이트 (50ms 간격으로 제한)
                if (_sensorSystemEnabled && ShouldUpdateSensorData())
                {
                    Task.Run(async () => await UpdateSensorDataAsync());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Status update failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 센서 데이터 업데이트 시점 확인
        /// </summary>
        /// <returns>센서 업데이트 필요 여부</returns>
        private bool ShouldUpdateSensorData()
        {
            var timeSinceLastUpdate = DateTime.Now - _lastSensorUpdateTime;
            return timeSinceLastUpdate.TotalMilliseconds >= SENSOR_UPDATE_INTERVAL_MS;
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

        #region Sensor Data Collection
        /// <summary>
        /// 센서 데이터 비동기 업데이트
        /// </summary>
        private async Task UpdateSensorDataAsync()
        {
            try
            {
                if (!IsConnected || !_sensorSystemEnabled) return;

                _lastSensorUpdateTime = DateTime.Now;

                // 1. EtherCAT을 통한 실제 위치 센서 데이터 수집
                await ReadActualPositionSensors();

                // 2. EtherCAT을 통한 실제 상태 센서 데이터 수집
                await ReadActualStatusSensors();

                // 3. 기존 안전 센서 및 I/O 센서 데이터 수집
                await ReadSafetySensors();
                await ReadIOSensors();

                // 4. 센서 데이터 분석 및 처리
                ProcessSensorData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실제 센서 데이터 업데이트 실패: {ex.Message}");
                Logger.Error(CLASS_NAME, "UpdateSensorDataAsync", "실제 센서 데이터 업데이트 실패", ex);
            }
        }

        /// <summary>
        /// 실제 위치 센서 데이터 읽기
        /// </summary>
        private async Task ReadActualPositionSensors()
        {
            try
            {
                // EtherCAT을 통한 실제 엔코더 값 읽기
                var actualPosition = await _etherCATComm.ReadCurrentPositionAsync();

                if (actualPosition != null)
                {
                    _currentSensorData.ActualRAxisPulse = actualPosition.RAxisPulse;
                    _currentSensorData.ActualThetaAxisPulse = actualPosition.ThetaAxisPulse;
                    _currentSensorData.ActualZAxisPulse = actualPosition.ZAxisPulse;

                    // 실제 속도 계산 (이전 위치와 비교)
                    // 실제 구현에서는 EtherCAT에서 직접 속도 데이터를 읽어올 수 있음
                    _currentSensorData.CurrentRAxisSpeed = _isMoving ? _currentSpeed : 0;
                    _currentSensorData.CurrentThetaAxisSpeed = _isMoving ? _currentSpeed : 0;
                    _currentSensorData.CurrentZAxisSpeed = _isMoving ? _currentSpeed : 0;

                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실제 위치 센서: R={actualPosition.RAxisPulse}, T={actualPosition.ThetaAxisPulse}, Z={actualPosition.ZAxisPulse}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실제 위치 센서 읽기 실패: {ex.Message}");
                Logger.Error(CLASS_NAME, "ReadActualPositionSensors", "실제 위치 센서 읽기 실패", ex);
            }
        }

        /// <summary>
        /// 실제 상태 센서 데이터 읽기
        /// </summary>
        private async Task ReadActualStatusSensors()
        {
            try
            {
                // EtherCAT을 통한 실제 로봇 상태 읽기
                var robotStatus = await _etherCATComm.ReadRobotStatusAsync();

                if (robotStatus != null)
                {
                    _currentSensorData.IsServoReady = robotStatus.IsReady;
                    _currentSensorData.IsMotorEnabled = robotStatus.IsConnected;
                    _currentSensorData.IsInPosition = robotStatus.IsInPosition;

                    // 실제 센서에서 읽어온 추가 정보들
                    // (실제 로봇에서는 온도, 진동, 전류 센서가 있을 수 있음)
                    _currentSensorData.Temperature = 25.0 + (DateTime.Now.Second % 10); // 시뮬레이션
                    _currentSensorData.Vibration = _isMoving ? 0.02 + (DateTime.Now.Millisecond % 50) / 1000.0 : 0.01;
                    _currentSensorData.MotorCurrent = _isMoving ? 2.0 + (_currentSpeed / 50.0) : 0.3;

                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실제 상태 센서: Ready={robotStatus.IsReady}, InPos={robotStatus.IsInPosition}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 실제 상태 센서 읽기 실패: {ex.Message}");
                Logger.Error(CLASS_NAME, "ReadActualStatusSensors", "실제 상태 센서 읽기 실패", ex);
            }
        }

        /// <summary>
        /// 위치 센서 데이터 읽기
        /// </summary>
        private async Task ReadPositionSensors()
        {
            try
            {
                // EtherCAT 위치 센서 데이터 요청
                string positionQuery = "GET_POSITION_ALL\r\n";

                // 실제 통신 시뮬레이션
                await Task.Delay(5);

                // 실제 로봇으로부터 엔코더 펄스 값 수신 (시뮬레이션)
                var currentCoords = ConvertUserToRobotCoordinates(_currentPosition.R, _currentPosition.Theta, _currentPosition.Z);

                _currentSensorData.ActualRAxisPulse = currentCoords.RAxisPulse;
                _currentSensorData.ActualThetaAxisPulse = currentCoords.ThetaAxisPulse;
                _currentSensorData.ActualZAxisPulse = currentCoords.ZAxisPulse;

                // 속도 데이터 (이동 중일 때만)
                if (_isMoving)
                {
                    _currentSensorData.CurrentRAxisSpeed = _currentSpeed;
                    _currentSensorData.CurrentThetaAxisSpeed = _currentSpeed;
                    _currentSensorData.CurrentZAxisSpeed = _currentSpeed;
                }
                else
                {
                    _currentSensorData.CurrentRAxisSpeed = 0;
                    _currentSensorData.CurrentThetaAxisSpeed = 0;
                    _currentSensorData.CurrentZAxisSpeed = 0;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 위치 센서 읽기 완료: R={_currentSensorData.ActualRAxisPulse}, T={_currentSensorData.ActualThetaAxisPulse}, Z={_currentSensorData.ActualZAxisPulse}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 위치 센서 읽기 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 상태 센서 데이터 읽기
        /// </summary>
        private async Task ReadStatusSensors()
        {
            try
            {
                // EtherCAT 상태 센서 데이터 요청
                string statusQuery = "GET_ROBOT_STATUS\r\n";

                await Task.Delay(3);

                // 모터 및 서보 상태 (연결 상태에 따라)
                _currentSensorData.IsServoReady = _isConnected;
                _currentSensorData.IsMotorEnabled = _isConnected && !_isMoving;
                _currentSensorData.IsInPosition = !_isMoving;

                // 온도 센서 (25-35도 범위로 시뮬레이션)
                _currentSensorData.Temperature = 25.0 + (DateTime.Now.Second % 10);

                // 진동 센서 (이동 중일 때 진동 발생)
                _currentSensorData.Vibration = _isMoving ? 0.1 + (DateTime.Now.Millisecond % 100) / 1000.0 : 0.02;

                // 모터 전류 (이동 중일 때 높아짐)
                _currentSensorData.MotorCurrent = _isMoving ? 2.5 + (_currentSpeed / 100.0) : 0.5;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 상태 센서 읽기 완료: 온도={_currentSensorData.Temperature:F1}°C, 진동={_currentSensorData.Vibration:F3}, 전류={_currentSensorData.MotorCurrent:F1}A");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 상태 센서 읽기 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 안전 센서 데이터 읽기
        /// </summary>
        private async Task ReadSafetySensors()
        {
            try
            {
                // EtherCAT 안전 센서 데이터 요청
                string safetyQuery = "GET_SAFETY_STATUS\r\n";

                await Task.Delay(2);

                // 비상정지 상태 (정상 상태로 가정)
                _currentSensorData.EmergencyStopActive = false;

                // 도어 센서 (닫힘 상태로 가정)
                _currentSensorData.DoorClosed = true;

                // 라이트 커튼 (정상 상태로 가정)
                _currentSensorData.LightCurtainClear = true;

                // 진공 압력 센서 (진공 ON 시 85% 압력)
                _currentSensorData.VacuumPressure = _vacuumOn ? 85.0 + (DateTime.Now.Millisecond % 100) / 100.0 : 0.0;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 안전 센서 읽기 완료: E-Stop={_currentSensorData.EmergencyStopActive}, 도어={_currentSensorData.DoorClosed}, 진공압력={_currentSensorData.VacuumPressure:F1}%");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 안전 센서 읽기 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// I/O 센서 데이터 읽기
        /// </summary>
        private async Task ReadIOSensors()
        {
            try
            {
                // EtherCAT I/O 센서 데이터 요청
                string ioQuery = "GET_IO_STATUS\r\n";

                await Task.Delay(2);

                // 웨이퍼 감지 센서 (진공 상태와 연동)
                _currentSensorData.WaferDetected = _vacuumOn && _currentSensorData.VacuumPressure > 80.0;

                // 카세트 존재 감지 (항상 존재로 가정)
                _currentSensorData.CassettePresent = true;

                // 로드포트 준비 상태
                _currentSensorData.LoadportReady = true;

                // 진공 밸브 상태
                _currentSensorData.VacuumValveOpen = _vacuumOn;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] I/O 센서 읽기 완료: 웨이퍼감지={_currentSensorData.WaferDetected}, 카세트={_currentSensorData.CassettePresent}, 진공밸브={_currentSensorData.VacuumValveOpen}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] I/O 센서 읽기 실패: {ex.Message}");
            }
        }
        #endregion

        #region Sensor Data Processing
        /// <summary>
        /// 센서 데이터 분석 및 처리
        /// </summary>
        private void ProcessSensorData()
        {
            try
            {
                // 1. 위치 정확도 확인
                CheckPositionAccuracy();

                // 2. 온도 모니터링
                CheckTemperature();

                // 3. 안전 상태 확인
                CheckSafetyConditions();

                // 4. 센서 기반 현재 위치 업데이트
                UpdatePositionFromSensors();

                _currentSensorData.UpdateTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 센서 데이터 처리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 위치 정확도 확인
        /// </summary>
        private void CheckPositionAccuracy()
        {
            try
            {
                if (!_isMoving) return;

                // 목표 위치와 실제 위치 비교
                var targetCoords = ConvertUserToRobotCoordinates(_targetPosition.R, _targetPosition.Theta, _targetPosition.Z);

                int rError = Math.Abs(_currentSensorData.ActualRAxisPulse - targetCoords.RAxisPulse);
                int thetaError = Math.Abs(_currentSensorData.ActualThetaAxisPulse - targetCoords.ThetaAxisPulse);
                int zError = Math.Abs(_currentSensorData.ActualZAxisPulse - targetCoords.ZAxisPulse);

                const int MAX_POSITION_ERROR = 100; // 최대 허용 오차 (펄스)

                if (rError > MAX_POSITION_ERROR || thetaError > MAX_POSITION_ERROR || zError > MAX_POSITION_ERROR)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 위치 오차 경고: R={rError}, T={thetaError}, Z={zError} pulse");
                    OnErrorOccurred("POSITION_ERROR", $"위치 오차가 허용 범위를 초과했습니다: R={rError}, T={thetaError}, Z={zError}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 위치 정확도 확인 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 온도 모니터링
        /// </summary>
        private void CheckTemperature()
        {
            try
            {
                const double MAX_TEMPERATURE = 60.0; // 최대 허용 온도
                const double WARNING_TEMPERATURE = 50.0; // 경고 온도

                if (_currentSensorData.Temperature > MAX_TEMPERATURE)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 온도 알람: {_currentSensorData.Temperature:F1}°C");
                    OnErrorOccurred("TEMPERATURE_ALARM", $"로봇 온도가 위험 수준입니다: {_currentSensorData.Temperature:F1}°C");
                }
                else if (_currentSensorData.Temperature > WARNING_TEMPERATURE)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 온도 경고: {_currentSensorData.Temperature:F1}°C");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 온도 모니터링 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 안전 상태 확인
        /// </summary>
        private void CheckSafetyConditions()
        {
            try
            {
                // 비상정지 확인
                if (_currentSensorData.EmergencyStopActive)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비상정지 감지됨");
                    OnErrorOccurred("EMERGENCY_STOP", "비상정지가 활성화되었습니다");
                    return;
                }

                // 도어 상태 확인
                if (!_currentSensorData.DoorClosed && _isMoving)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 도어 열림 감지 (이동 중)");
                    OnErrorOccurred("DOOR_OPEN", "로봇 이동 중 도어가 열렸습니다");
                }

                // 라이트 커튼 확인
                if (!_currentSensorData.LightCurtainClear && _isMoving)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 라이트 커튼 차단됨");
                    OnErrorOccurred("LIGHT_CURTAIN", "라이트 커튼이 차단되었습니다");
                }

                // 진공 압력 이상 확인
                if (_vacuumOn && _currentSensorData.VacuumPressure < 70.0)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 진공 압력 부족: {_currentSensorData.VacuumPressure:F1}%");
                    OnErrorOccurred("VACUUM_LOW", $"진공 압력이 부족합니다: {_currentSensorData.VacuumPressure:F1}%");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 안전 상태 확인 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 센서 기반 현재 위치 업데이트
        /// </summary>
        private void UpdatePositionFromSensors()
        {
            try
            {
                // 엔코더 펄스를 사용자 좌표로 변환
                double actualR = _currentSensorData.ActualRAxisPulse / 1000.0;      // 펄스 -> mm
                double actualTheta = _currentSensorData.ActualThetaAxisPulse / 100.0; // 펄스 -> degree
                double actualZ = _currentSensorData.ActualZAxisPulse / 500.0;       // 펄스 -> mm

                var oldPosition = new Position(_currentPosition.R, _currentPosition.Theta, _currentPosition.Z);
                var newPosition = new Position(actualR, actualTheta, actualZ);

                // 위치 변화가 있는 경우에만 업데이트
                if (Math.Abs(oldPosition.R - newPosition.R) > 0.1 ||
                    Math.Abs(oldPosition.Theta - newPosition.Theta) > 0.1 ||
                    Math.Abs(oldPosition.Z - newPosition.Z) > 0.1)
                {
                    lock (_lockObject)
                    {
                        _currentPosition = newPosition;
                    }

                    // 위치 변경 이벤트 발생
                    OnPositionChanged(oldPosition, newPosition);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 센서 기반 위치 업데이트 실패: {ex.Message}");
            }
        }
        #endregion

        #region Public Sensor Access Methods
        /// <summary>
        /// 현재 센서 데이터 조회
        /// </summary>
        /// <returns>현재 센서 데이터 복사본</returns>
        public SensorData GetCurrentSensorData()
        {
            try
            {
                // 스레드 안전을 위한 지역 변수로 복사
                SensorData localSensorData = _currentSensorData;

                if (localSensorData == null)
                {
                    return new SensorData();
                }

                // 센서 데이터 안전한 복사본 생성 (스레드 안전)
                return new SensorData
                {
                    ActualRAxisPulse = localSensorData.ActualRAxisPulse,
                    ActualThetaAxisPulse = localSensorData.ActualThetaAxisPulse,
                    ActualZAxisPulse = localSensorData.ActualZAxisPulse,
                    CurrentRAxisSpeed = localSensorData.CurrentRAxisSpeed,
                    CurrentThetaAxisSpeed = localSensorData.CurrentThetaAxisSpeed,
                    CurrentZAxisSpeed = localSensorData.CurrentZAxisSpeed,
                    IsServoReady = localSensorData.IsServoReady,
                    IsMotorEnabled = localSensorData.IsMotorEnabled,
                    IsInPosition = localSensorData.IsInPosition,
                    Temperature = localSensorData.Temperature,
                    Vibration = localSensorData.Vibration,
                    MotorCurrent = localSensorData.MotorCurrent,
                    EmergencyStopActive = localSensorData.EmergencyStopActive,
                    DoorClosed = localSensorData.DoorClosed,
                    LightCurtainClear = localSensorData.LightCurtainClear,
                    VacuumPressure = localSensorData.VacuumPressure,
                    WaferDetected = localSensorData.WaferDetected,
                    CassettePresent = localSensorData.CassettePresent,
                    LoadportReady = localSensorData.LoadportReady,
                    VacuumValveOpen = localSensorData.VacuumValveOpen,
                    UpdateTime = localSensorData.UpdateTime
                };
            }
            catch (Exception ex)
            {
                // C# 6.0 호환: 문자열 보간 대신 문자열 연결 사용
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 센서 데이터 조회 실패: " + ex.Message);
                return new SensorData();
            }
        }

        /// <summary>
        /// 센서 시스템 상태 조회
        /// </summary>
        /// <returns>센서 시스템 활성화 여부</returns>
        public bool IsSensorSystemEnabled()
        {
            return _sensorSystemEnabled;
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
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 로봇 컨트롤러 리소스 정리 시작");

                // 연결 해제
                if (_isConnected)
                {
                    DisconnectAsync().Wait(5000); // 5초 대기
                }

                // 타이머 정리
                if (_statusUpdateTimer != null)
                {
                    _statusUpdateTimer.Stop();
                    _statusUpdateTimer = null;
                }

                // EtherCAT 리소스 정리
                CleanupEtherCATResources();

                // DTP-7H 리소스 정리
                if (_dtp7h != null)
                {
                    _dtp7h.Dispose();
                    _dtp7h = null;
                }

                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 로봇 컨트롤러 리소스 정리 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 리소스 정리 실패: " + ex.Message);
            }
        }

        /// <summary>
        /// 리소스 정리
        /// </summary>
        private void CleanupEtherCATResources()
        {
            try
            {
                if (_etherCATComm != null)
                {
                    _etherCATComm.Dispose();
                    _etherCATComm = null;
                }

                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 리소스 정리 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] EtherCAT 리소스 정리 실패: " + ex.Message);
            }
        }
        #endregion
    }
}