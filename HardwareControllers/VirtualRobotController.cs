using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using TeachingPendant.Safety;
using TeachingPendant.Manager;

namespace TeachingPendant.HardwareControllers
{
    /// <summary>
    /// 가상 로봇 컨트롤러 (시뮬레이션)
    /// 실제 하드웨어 없이도 로봇 동작을 시뮬레이션하는 컨트롤러
    /// </summary>
    public class VirtualRobotController : IRobotController, IDisposable
    {
        #region Private Fields
        private bool _isConnected = false;
        private bool _isMoving = false;
        private bool _isHomed = false;
        private bool _vacuumOn = false;
        private Position _currentPosition;
        private RobotStatus _currentStatus;
        private int _currentSpeed = 50;
        private DispatcherTimer _simulationTimer;
        private readonly object _lockObject = new object();
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
                    return _isConnected;
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
        public VirtualRobotController()
        {
            InitializeVirtualRobot();
            System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Virtual robot controller created");
        }
        #endregion

        #region Initialization
        private void InitializeVirtualRobot()
        {
            _currentPosition = new Position(0, 0, 0);
            _currentStatus = new RobotStatus();

            // 시뮬레이션 타이머 설정 (100ms 간격)
            _simulationTimer = new DispatcherTimer();
            _simulationTimer.Interval = TimeSpan.FromMilliseconds(100);
            _simulationTimer.Tick += SimulationTimer_Tick;
        }
        #endregion

        #region Connection Management
        public async Task<bool> ConnectAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Attempting to connect virtual robot...");

                // 시뮬레이션: 연결 시간 모방
                await Task.Delay(500);

                lock (_lockObject)
                {
                    _isConnected = true;
                    _isHomed = false;
                    _vacuumOn = false;
                    _currentPosition = new Position(0, 0, 0);

                    // 시뮬레이션 타이머 시작
                    _simulationTimer.Start();
                }

                UpdateStatus();

                System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Virtual robot connected successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Connection failed: {ex.Message}");
                OnErrorOccurred("CONNECT_ERROR", "Virtual robot connection failed", ex);
                return false;
            }
        }

        public async Task<bool> DisconnectAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Disconnecting virtual robot...");

                lock (_lockObject)
                {
                    _simulationTimer?.Stop();
                    _isConnected = false;
                    _isMoving = false;
                    _isHomed = false;
                    _vacuumOn = false;
                }

                UpdateStatus();

                // 시뮬레이션: 연결 해제 시간 모방
                await Task.Delay(200);

                System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Virtual robot disconnected successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Disconnection failed: {ex.Message}");
                OnErrorOccurred("DISCONNECT_ERROR", "Virtual robot disconnection failed", ex);
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
                    System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Move failed: Not connected");
                    return false;
                }

                // 안전 확인
                if (!IsSafeToOperate())
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Move failed: Safety conditions not met");
                    return false;
                }

                Position targetPosition = new Position(r, theta, z);
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Starting movement: {targetPosition}");

                var oldPosition = new Position(_currentPosition.R, _currentPosition.Theta, _currentPosition.Z);

                lock (_lockObject)
                {
                    _isMoving = true;
                }
                UpdateStatus();

                // 이동 시뮬레이션
                await SimulateMovement(targetPosition);

                lock (_lockObject)
                {
                    _currentPosition = targetPosition;
                    _isMoving = false;
                }

                UpdateStatus();
                OnPositionChanged(oldPosition, _currentPosition);

                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Movement complete: {_currentPosition}");
                return true;
            }
            catch (Exception ex)
            {
                lock (_lockObject)
                {
                    _isMoving = false;
                }
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Movement failed: {ex.Message}");
                OnErrorOccurred("MOVE_ERROR", "Virtual robot movement failed", ex);
                return false;
            }
        }

        public async Task<bool> HomeAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Home command failed: Not connected");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Starting home command...");

                var oldPosition = new Position(_currentPosition.R, _currentPosition.Theta, _currentPosition.Z);
                Position homePosition = new Position(0, 0, 0);

                lock (_lockObject)
                {
                    _isMoving = true;
                }
                UpdateStatus();

                // 홈 이동 시뮬레이션 (조금 더 오래 걸림)
                await SimulateMovement(homePosition, isHoming: true);

                lock (_lockObject)
                {
                    _currentPosition = homePosition;
                    _isMoving = false;
                    _isHomed = true;
                }

                UpdateStatus();
                OnPositionChanged(oldPosition, _currentPosition);

                System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Home command complete");
                return true;
            }
            catch (Exception ex)
            {
                lock (_lockObject)
                {
                    _isMoving = false;
                }
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Home command failed: {ex.Message}");
                OnErrorOccurred("HOME_ERROR", "Virtual robot home command failed", ex);
                return false;
            }
        }

        public async Task<bool> StopAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Stopping robot...");

                lock (_lockObject)
                {
                    _isMoving = false;
                }

                UpdateStatus();

                // 정지 시뮬레이션
                await Task.Delay(100);

                System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Robot stop complete");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Stop failed: {ex.Message}");
                OnErrorOccurred("STOP_ERROR", "Virtual robot stop failed", ex);
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
                    System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Pick failed: Not connected");
                    return false;
                }

                if (!IsSafeToOperate())
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Pick failed: Safety conditions not met");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Starting Pick operation...");

                // Pick 시뮬레이션
                await SetVacuumAsync(true);
                await Task.Delay(500); // Pick 시간 시뮬레이션

                System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Pick operation complete");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Pick failed: {ex.Message}");
                OnErrorOccurred("PICK_ERROR", "Virtual robot Pick failed", ex);
                return false;
            }
        }

        public async Task<bool> PlaceAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Place failed: Not connected");
                    return false;
                }

                if (!IsSafeToOperate())
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Place failed: Safety conditions not met");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Starting Place operation...");

                // Place 시뮬레이션
                await Task.Delay(300); // Place 시간 시뮬레이션
                await SetVacuumAsync(false);

                System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Place operation complete");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Place failed: {ex.Message}");
                OnErrorOccurred("PLACE_ERROR", "Virtual robot Place failed", ex);
                return false;
            }
        }

        public async Task<bool> SetVacuumAsync(bool isOn)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Vacuum {(isOn ? "ON" : "OFF")}...");

                lock (_lockObject)
                {
                    _vacuumOn = isOn;
                }

                UpdateStatus();

                // 진공 동작 시뮬레이션
                await Task.Delay(200);

                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Vacuum {(isOn ? "ON" : "OFF")} complete");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Vacuum control failed: {ex.Message}");
                OnErrorOccurred("VACUUM_ERROR", "Virtual robot vacuum control failed", ex);
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
            await Task.Delay(10); // 시뮬레이션 딜레이

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
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Safety check failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetSpeedAsync(int speedPercent)
        {
            try
            {
                if (speedPercent < 1 || speedPercent > 100)
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Set speed failed: Invalid range");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Setting speed: {speedPercent}%");

                lock (_lockObject)
                {
                    _currentSpeed = speedPercent;
                }

                UpdateStatus();

                // 속도 설정 시뮬레이션
                await Task.Delay(100);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Set speed failed: {ex.Message}");
                OnErrorOccurred("SPEED_ERROR", "Virtual robot set speed failed", ex);
                return false;
            }
        }
        #endregion

        #region Simulation Methods
        private async Task SimulateMovement(Position targetPosition, bool isHoming = false)
        {
            int movementTime = isHoming ? 2000 : 1000; // 홈은 더 오래 걸림
            int steps = movementTime / 50; // 50ms 간격으로 업데이트

            Position startPosition = new Position(_currentPosition.R, _currentPosition.Theta, _currentPosition.Z);

            for (int i = 0; i <= steps; i++)
            {
                double progress = (double)i / steps;

                // 부드러운 이동을 위한 ease-in-out 곡선
                progress = EaseInOutCubic(progress);

                lock (_lockObject)
                {
                    _currentPosition.R = Lerp(startPosition.R, targetPosition.R, progress);
                    _currentPosition.Theta = Lerp(startPosition.Theta, targetPosition.Theta, progress);
                    _currentPosition.Z = Lerp(startPosition.Z, targetPosition.Z, progress);
                }

                await Task.Delay(50);
            }
        }

        private double Lerp(double start, double end, double t)
        {
            return start + (end - start) * t;
        }

        private double EaseInOutCubic(double t)
        {
            return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        }

        private void SimulationTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // 시뮬레이션에서는 위치에 약간의 노이즈 추가 (실제 로봇처럼)
                if (IsConnected && !IsMoving)
                {
                    lock (_lockObject)
                    {
                        // 매우 작은 노이즈 추가 (실제 로봇의 미세한 진동 모방)
                        double noise = (DateTime.Now.Millisecond % 10 - 5) * 0.01;
                        // 노이즈는 실제로 위치를 변경하지는 않음 (시각적 효과만)
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Simulation timer error: {ex.Message}");
            }
        }
        #endregion

        #region Private Helper Methods
        private void UpdateStatus()
        {
            var oldStatus = _currentStatus != null ?
                new RobotStatus
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

            lock (_lockObject)
            {
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

            OnStatusChanged(oldStatus, _currentStatus);
        }

        private void OnStatusChanged(RobotStatus oldStatus, RobotStatus newStatus)
        {
            try
            {
                StatusChanged?.Invoke(this, new RobotStatusChangedEventArgs(oldStatus, newStatus));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] StatusChanged event error: {ex.Message}");
            }
        }

        private void OnPositionChanged(Position oldPosition, Position newPosition)
        {
            try
            {
                PositionChanged?.Invoke(this, new PositionChangedEventArgs(oldPosition, newPosition));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] PositionChanged event error: {ex.Message}");
            }
        }

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
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] ErrorOccurred event error: {ex.Message}");
            }
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Disposing resources...");

                DisconnectAsync().Wait(1000);

                if (_simulationTimer != null)
                {
                    _simulationTimer.Stop();
                    _simulationTimer = null;
                }

                System.Diagnostics.Debug.WriteLine("[VirtualRobotController] Resources disposed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualRobotController] Dispose error: {ex.Message}");
            }
        }
        #endregion
    }
}