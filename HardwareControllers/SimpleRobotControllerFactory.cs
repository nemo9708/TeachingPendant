using System;
using System.Threading.Tasks;

namespace TeachingPendant.HardwareControllers
{
    /// <summary>
    /// 간단한 로봇 컨트롤러 팩토리 - 에러 수정 버전
    /// 기존 코드와 충돌하지 않는 기본적인 컨트롤러 관리
    /// </summary>
    public static class SimpleRobotControllerFactory
    {
        #region Private Fields
        private static IRobotController _currentController;
        private static bool _isInitialized = false;
        private static HardwareDetectionResult _lastDetectionResult;
        private static readonly object _lockObject = new object();

        private const string CLASS_NAME = "SimpleRobotControllerFactory";
        #endregion

        #region Events
        /// <summary>
        /// 컨트롤러 변경 이벤트 (고유한 이름으로 변경)
        /// </summary>
        public static event EventHandler<SimpleControllerChangedEventArgs> ControllerChanged;
        #endregion

        #region Properties
        /// <summary>
        /// 현재 활성 로봇 컨트롤러
        /// </summary>
        public static IRobotController CurrentController
        {
            get
            {
                lock (_lockObject)
                {
                    if (_currentController == null)
                    {
                        // 기본적으로 시뮬레이션 컨트롤러 생성
                        CreateSimulationController();
                    }
                    return _currentController;
                }
            }
        }

        /// <summary>
        /// 현재 하드웨어 모드 여부
        /// </summary>
        public static bool IsHardwareMode
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentController is EtherCATRobotController;
                }
            }
        }

        /// <summary>
        /// 초기화 완료 여부
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                lock (_lockObject)
                {
                    return _isInitialized;
                }
            }
        }

        /// <summary>
        /// 마지막 하드웨어 감지 결과
        /// </summary>
        public static HardwareDetectionResult LastDetectionResult
        {
            get
            {
                lock (_lockObject)
                {
                    return _lastDetectionResult;
                }
            }
        }
        #endregion

        #region Initialization

        /// <summary>
        /// 팩토리 초기화 - 기존 HardwareDetector 사용
        /// </summary>
        /// <returns>초기화 성공 여부</returns>
        public static async Task<bool> InitializeAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting factory initialization...");

                // 기존 HardwareDetector 사용
                HardwareDetectionResult hardwareInfo = await HardwareDetector.DetectAllHardwareAsync();

                lock (_lockObject)
                {
                    _lastDetectionResult = hardwareInfo;
                }

                bool success = false;

                if (hardwareInfo.HasDTP7H)
                {
                    // DTP-7H가 있으면 하드웨어 모드 시도
                    success = await CreateHardwareControllerAsync(hardwareInfo.DTP7HPort.PortName);

                    if (!success)
                    {
                        // 하드웨어 연결 실패 시 시뮬레이션 모드
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Hardware connection failed, switching to simulation mode");
                        success = CreateSimulationController();
                    }
                }
                else
                {
                    // DTP-7H가 없으면 시뮬레이션 모드
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] DTP-7H not detected, using simulation mode");
                    success = CreateSimulationController();
                }

                if (success)
                {
                    lock (_lockObject)
                    {
                        _isInitialized = true;
                    }

                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Factory initialization complete - Mode: {(IsHardwareMode ? "Hardware" : "Simulation")}");
                }

                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Initialization failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Controller Creation

        /// <summary>
        /// 시뮬레이션 컨트롤러 생성
        /// </summary>
        /// <returns>생성 성공 여부</returns>
        private static bool CreateSimulationController()
        {
            try
            {
                IRobotController oldController = null;

                lock (_lockObject)
                {
                    oldController = _currentController;
                }

                // 새 시뮬레이션 컨트롤러 생성
                VirtualRobotController newController = new VirtualRobotController();

                // 컨트롤러 교체
                lock (_lockObject)
                {
                    _currentController = newController;
                }

                // 기존 컨트롤러 해제
                DisposeOldController(oldController);

                // 이벤트 발생
                OnControllerChanged(oldController, newController);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Simulation controller created successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Simulation controller creation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 하드웨어 컨트롤러 생성
        /// </summary>
        /// <param name="dtp7hPort">DTP-7H 포트명</param>
        /// <returns>생성 성공 여부</returns>
        private static async Task<bool> CreateHardwareControllerAsync(string dtp7hPort)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Attempting to create hardware controller: {dtp7hPort}");

                IRobotController oldController = null;

                lock (_lockObject)
                {
                    oldController = _currentController;
                }

                // 새 하드웨어 컨트롤러 생성
                EtherCATRobotController newController = new EtherCATRobotController();

                // 연결 시도
                bool connected = await newController.ConnectAsync();

                if (connected)
                {
                    // 컨트롤러 교체
                    lock (_lockObject)
                    {
                        _currentController = newController;
                    }

                    // 기존 컨트롤러 해제
                    DisposeOldController(oldController);

                    // 이벤트 발생
                    OnControllerChanged(oldController, newController);

                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Hardware controller created successfully");
                    return true;
                }
                else
                {
                    // 연결 실패 시 컨트롤러 해제
                    newController.Dispose();
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Hardware controller connection failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Hardware controller creation failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Manual Control

        /// <summary>
        /// 하드웨어 모드로 전환
        /// </summary>
        /// <returns>전환 성공 여부</returns>
        public static async Task<bool> SwitchToHardwareModeAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Attempting to switch to hardware mode");

                if (IsHardwareMode)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Already in hardware mode");
                    return true;
                }

                // 기존 HardwareDetector로 DTP-7H 재감지
                ComPortInfo dtp7hPort = await HardwareDetector.DetectDTP7HAsync();

                if (dtp7hPort == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] DTP-7H not found");
                    return false;
                }

                // 하드웨어 컨트롤러 생성
                bool success = await CreateHardwareControllerAsync(dtp7hPort.PortName);

                if (success)
                {
                    // 감지 결과 업데이트
                    lock (_lockObject)
                    {
                        if (_lastDetectionResult != null)
                        {
                            _lastDetectionResult.DTP7HPort = dtp7hPort;
                            _lastDetectionResult.HasDTP7H = true;
                        }
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to switch to hardware mode: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 시뮬레이션 모드로 전환
        /// </summary>
        /// <returns>전환 성공 여부</returns>
        public static bool SwitchToSimulationMode()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Attempting to switch to simulation mode");

                if (!IsHardwareMode)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Already in simulation mode");
                    return true;
                }

                // 시뮬레이션 컨트롤러 생성
                bool success = CreateSimulationController();
                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to switch to simulation mode: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 하드웨어 상태 새로고침
        /// </summary>
        /// <returns>새로고침 성공 여부</returns>
        public static async Task<bool> RefreshHardwareAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Refreshing hardware");

                // 기존 HardwareDetector로 하드웨어 재감지
                HardwareDetectionResult hardwareInfo = await HardwareDetector.DetectAllHardwareAsync();

                lock (_lockObject)
                {
                    _lastDetectionResult = hardwareInfo;
                }

                if (hardwareInfo.HasDTP7H && !IsHardwareMode)
                {
                    // DTP-7H가 새로 감지되고 현재 시뮬레이션 모드인 경우
                    return await CreateHardwareControllerAsync(hardwareInfo.DTP7HPort.PortName);
                }
                else if (!hardwareInfo.HasDTP7H && IsHardwareMode)
                {
                    // DTP-7H가 사라지고 현재 하드웨어 모드인 경우
                    return CreateSimulationController();
                }

                return true; // 변경 사항 없음
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Hardware refresh failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Status Information

        /// <summary>
        /// 현재 상태 정보 조회
        /// </summary>
        /// <returns>상태 정보 문자열</returns>
        public static string GetStatusInfo()
        {
            try
            {
                lock (_lockObject)
                {
                    string controllerType = _currentController?.GetType().Name ?? "None";
                    bool isConnected = _currentController?.IsConnected ?? false;

                    return $"Initialized: {_isInitialized}, Mode: {(IsHardwareMode ? "Hardware" : "Simulation")}, " +
                           $"Controller: {controllerType}, Connected: {isConnected}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to get status info: {ex.Message}");
                return "Failed to get status";
            }
        }

        /// <summary>
        /// 사용 가능한 COM 포트 목록 조회 - 기존 HardwareDetector 사용
        /// </summary>
        /// <returns>COM 포트 정보 리스트</returns>
        public static System.Collections.Generic.List<ComPortInfo> GetAvailableComPorts()
        {
            try
            {
                return HardwareDetector.GetAvailableComPorts();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to get COM ports: {ex.Message}");
                return new System.Collections.Generic.List<ComPortInfo>();
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// 기존 컨트롤러 해제
        /// </summary>
        /// <param name="controller">해제할 컨트롤러</param>
        private static void DisposeOldController(IRobotController controller)
        {
            if (controller != null)
            {
                try
                {
                    if (controller.IsConnected)
                    {
                        // 비동기 연결 해제를 동기로 실행 (C# 6.0 호환)
                        Task disconnectTask = controller.DisconnectAsync();
                        disconnectTask.Wait(2000); // 최대 2초 대기
                    }

                    if (controller is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Error while disposing old controller: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 컨트롤러 변경 이벤트 발생
        /// </summary>
        /// <param name="oldController">기존 컨트롤러</param>
        /// <param name="newController">새 컨트롤러</param>
        private static void OnControllerChanged(IRobotController oldController, IRobotController newController)
        {
            try
            {
                ControllerChanged?.Invoke(null, new SimpleControllerChangedEventArgs
                {
                    OldController = oldController,
                    NewController = newController,
                    IsHardwareMode = newController is EtherCATRobotController,
                    ChangedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to handle ControllerChanged event: {ex.Message}");
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// 팩토리 정리
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting factory cleanup");

                IRobotController controller = null;

                lock (_lockObject)
                {
                    controller = _currentController;
                    _currentController = null;
                    _isInitialized = false;
                    _lastDetectionResult = null;
                }

                // 컨트롤러 해제
                DisposeOldController(controller);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Factory cleanup complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Factory cleanup failed: {ex.Message}");
            }
        }

        #endregion
    }

    #region Event Args (고유한 이름으로 변경)

    /// <summary>
    /// Simple 컨트롤러 변경 이벤트 인수 (충돌 방지용)
    /// </summary>
    public class SimpleControllerChangedEventArgs : EventArgs
    {
        public IRobotController OldController { get; set; }
        public IRobotController NewController { get; set; }
        public bool IsHardwareMode { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    #endregion
}