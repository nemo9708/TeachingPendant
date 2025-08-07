using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Management;
using TeachingPendant.Manager;

namespace TeachingPendant.HardwareControllers
{
    /// <summary>
    /// 로봇 컨트롤러 팩토리 - 하드웨어 자동 감지 통합
    /// 시뮬레이션/실제 하드웨어 모드 자동 전환
    /// </summary>
    public static class RobotControllerFactory
    {
        #region Private Fields
        private static IRobotController _currentController;
        private static bool _isInitialized = false;
        private static readonly object _lockObject = new object();
        private static HardwareDetectionResult _lastDetectionResult;

        private const string CLASS_NAME = "RobotControllerFactory";
        #endregion

        #region Events
        /// <summary>
        /// 컨트롤러 변경 이벤트
        /// </summary>
        public static event EventHandler<ControllerChangedEventArgs> ControllerChanged;

        /// <summary>
        /// 하드웨어 상태 변경 이벤트
        /// </summary>
        public static event EventHandler<HardwareStatusChangedEventArgs> HardwareStatusChanged;
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
                        // 동기적으로 초기화 (비동기는 별도 메서드 사용)
                        InitializeControllerSync();
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

        #region Public Methods - 에러 해결을 위한 필수 메서드들

        /// <summary>
        /// 현재 활성 컨트롤러 조회 - RecipeEngine에서 호출
        /// </summary>
        /// <returns>현재 컨트롤러</returns>
        public static IRobotController GetCurrentController()
        {
            lock (_lockObject)
            {
                if (_currentController == null)
                {
                    // 동기적으로 초기화
                    InitializeControllerSync();
                }
                return _currentController;
            }
        }

        /// <summary>
        /// 새 컨트롤러 생성 - RecipeEngine에서 호출
        /// </summary>
        /// <returns>생성된 컨트롤러</returns>
        public static IRobotController CreateController()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] New controller creation requested");

                // 기본적으로 VirtualRobotController 생성
                var controller = new VirtualRobotController();

                lock (_lockObject)
                {
                    var oldController = _currentController;
                    _currentController = controller;

                    // 컨트롤러 변경 이벤트 발생
                    ControllerChanged?.Invoke(null, new ControllerChangedEventArgs
                    {
                        OldController = oldController,
                        NewController = controller,
                        IsHardwareMode = false,
                        ChangedAt = DateTime.Now
                    });
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] New simulation controller created successfully");
                return controller;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Controller creation failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 동기 초기화 - RecipeSystemTestHelper에서 호출
        /// </summary>
        /// <returns>초기화 성공 여부</returns>
        public static bool Initialize()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting synchronous initialization");

                lock (_lockObject)
                {
                    if (_isInitialized)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Already initialized");
                        return true;
                    }
                }

                // 동기 초기화 수행
                InitializeControllerSync();

                lock (_lockObject)
                {
                    _isInitialized = true;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Synchronous initialization complete");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Synchronous initialization failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 비동기 초기화 - 하드웨어 자동 감지 포함
        /// </summary>
        /// <returns>초기화 성공 여부</returns>
        public static async Task<bool> InitializeAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting asynchronous initialization of Robot Controller Factory...");

                // 하드웨어 감지
                var detectionResult = await HardwareDetector.DetectAllHardwareAsync();

                lock (_lockObject)
                {
                    _lastDetectionResult = detectionResult;
                }

                // 감지 결과에 따라 컨트롤러 생성
                bool controllerCreated = await CreateControllerBasedOnDetectionAsync(detectionResult);

                if (controllerCreated)
                {
                    // 하드웨어 모니터링 시작
                    StartHardwareMonitoring();

                    lock (_lockObject)
                    {
                        _isInitialized = true;
                    }

                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Robot Controller Factory initialization complete - Mode: {(IsHardwareMode ? "Hardware" : "Simulation")}");

                    // 하드웨어 상태 변경 이벤트 발생
                    HardwareStatusChanged?.Invoke(null, new HardwareStatusChangedEventArgs
                    {
                        IsHardwareMode = IsHardwareMode,
                        DetectionResult = detectionResult,
                        StatusChangedAt = DateTime.Now
                    });

                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Robot controller creation failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Asynchronous initialization failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 동기 초기화 (기본 시뮬레이션 모드)
        /// </summary>
        private static void InitializeControllerSync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Synchronous initialization of robot controller (Simulation Mode)");

                // 하드웨어 감지 없이 시뮬레이션 모드로 시작
                var controller = new VirtualRobotController();

                lock (_lockObject)
                {
                    _currentController = controller;
                    _isInitialized = true;
                }

                // 컨트롤러 변경 이벤트 발생
                ControllerChanged?.Invoke(null, new ControllerChangedEventArgs
                {
                    OldController = null,
                    NewController = controller,
                    IsHardwareMode = false,
                    ChangedAt = DateTime.Now
                });

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Simulation controller created successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Synchronous initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 감지 결과 기반 컨트롤러 생성
        /// </summary>
        /// <param name="detectionResult">하드웨어 감지 결과</param>
        /// <returns>컨트롤러 생성 성공 여부</returns>
        private static async Task<bool> CreateControllerBasedOnDetectionAsync(HardwareDetectionResult detectionResult)
        {
            try
            {
                IRobotController newController = null;

                if (detectionResult != null && detectionResult.HasAnyHardware)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Hardware detected, attempting to create EtherCAT controller");

                    // 하드웨어가 감지된 경우 EtherCAT 컨트롤러 시도
                    try
                    {
                        newController = new EtherCATRobotController();
                        bool connected = await newController.ConnectAsync();

                        if (!connected)
                        {
                            System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] EtherCAT connection failed, falling back to simulation mode");
                            if (newController is IDisposable disposable)
                            {
                                disposable.Dispose();
                            }
                            newController = new VirtualRobotController();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] EtherCAT controller creation failed: {ex.Message}, falling back to simulation mode");
                        newController = new VirtualRobotController();
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] No hardware detected, creating simulation controller");
                    newController = new VirtualRobotController();
                }

                if (newController != null)
                {
                    lock (_lockObject)
                    {
                        var oldController = _currentController;
                        _currentController = newController;

                        // 컨트롤러 변경 이벤트 발생
                        ControllerChanged?.Invoke(null, new ControllerChangedEventArgs
                        {
                            OldController = oldController,
                            NewController = newController,
                            IsHardwareMode = newController is EtherCATRobotController,
                            ChangedAt = DateTime.Now
                        });
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to create controller based on detection result: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Hardware Monitoring

        /// <summary>
        /// 하드웨어 모니터링 시작
        /// </summary>
        private static void StartHardwareMonitoring()
        {
            try
            {
                // 하드웨어 감지 이벤트 구독
                HardwareDetector.HardwareDetected += OnHardwareDetected;
                HardwareDetector.HardwareDisconnected += OnHardwareDisconnected;

                // 하드웨어 모니터링 시작
                HardwareDetector.StartHardwareMonitoring();

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Hardware monitoring started");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to start hardware monitoring: {ex.Message}");
            }
        }

        /// <summary>
        /// 하드웨어 감지 이벤트 핸들러
        /// </summary>
        private static void OnHardwareDetected(object sender, HardwareDetectedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Hardware detected: {e.DeviceType} on {e.PortInfo.PortName}");

                // 하드웨어 감지 시 컨트롤러 전환 로직 (필요시 구현)
                if (e.DeviceType == HardwareDeviceType.DTP7H || e.DeviceType == HardwareDeviceType.RobotController)
                {
                    // 현재 시뮬레이션 모드인 경우에만 하드웨어 모드로 전환
                    lock (_lockObject)
                    {
                        if (!IsHardwareMode)
                        {
                            System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Requesting switch to hardware mode");
                            // 비동기 전환 작업을 별도 Task로 실행
                            Task.Run(async () => await SwitchToHardwareModeAsync());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to handle hardware detection event: {ex.Message}");
            }
        }

        /// <summary>
        /// 하드웨어 연결 해제 이벤트 핸들러
        /// </summary>
        private static void OnHardwareDisconnected(object sender, HardwareDisconnectedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Hardware disconnected: {e.PortName}");

                // 하드웨어 연결 해제 시 시뮬레이션 모드로 전환 로직 (필요시 구현)
                lock (_lockObject)
                {
                    if (IsHardwareMode)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Requesting switch to simulation mode");
                        // 비동기 전환 작업을 별도 Task로 실행
                        Task.Run(async () => await SwitchToSimulationModeAsync());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to handle hardware disconnection event: {ex.Message}");
            }
        }

        /// <summary>
        /// 하드웨어 모드로 전환
        /// </summary>
        private static async Task SwitchToHardwareModeAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting switch to hardware mode");

                var newController = new EtherCATRobotController();
                bool connected = await newController.ConnectAsync();

                if (connected)
                {
                    IRobotController oldController = null;
                    lock (_lockObject)
                    {
                        oldController = _currentController;
                        _currentController = newController;
                    }

                    // 이전 컨트롤러 정리
                    if (oldController != null)
                    {
                        try
                        {
                            if (oldController.IsConnected)
                            {
                                await oldController.DisconnectAsync();
                            }
                            if (oldController is IDisposable disposable)
                            {
                                disposable.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Error while cleaning up previous controller: {ex.Message}");
                        }
                    }

                    // 컨트롤러 변경 이벤트 발생
                    ControllerChanged?.Invoke(null, new ControllerChangedEventArgs
                    {
                        OldController = oldController,
                        NewController = newController,
                        IsHardwareMode = true,
                        ChangedAt = DateTime.Now
                    });

                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Switch to hardware mode complete");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to switch to hardware mode - unable to connect");
                    if (newController is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Error while switching to hardware mode: {ex.Message}");
            }
        }

        /// <summary>
        /// 시뮬레이션 모드로 전환
        /// </summary>
        private static async Task SwitchToSimulationModeAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting switch to simulation mode");

                var newController = new VirtualRobotController();
                IRobotController oldController = null;

                lock (_lockObject)
                {
                    oldController = _currentController;
                    _currentController = newController;
                }

                // 이전 컨트롤러 정리
                if (oldController != null)
                {
                    try
                    {
                        if (oldController.IsConnected)
                        {
                            await oldController.DisconnectAsync();
                        }
                        if (oldController is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Error while cleaning up previous controller: {ex.Message}");
                    }
                }

                // 컨트롤러 변경 이벤트 발생
                ControllerChanged?.Invoke(null, new ControllerChangedEventArgs
                {
                    OldController = oldController,
                    NewController = newController,
                    IsHardwareMode = false,
                    ChangedAt = DateTime.Now
                });

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Switch to simulation mode complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Error while switching to simulation mode: {ex.Message}");
            }
        }

        #endregion

        #region Controller Management

        /// <summary>
        /// 컨트롤러 강제 교체
        /// </summary>
        /// <param name="newController">새 컨트롤러</param>
        /// <returns>교체 성공 여부</returns>
        public static async Task<bool> ReplaceControllerAsync(IRobotController newController)
        {
            try
            {
                if (newController == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Controller replacement failed: null controller");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting controller replacement: {newController.GetType().Name}");

                IRobotController oldController = null;
                lock (_lockObject)
                {
                    oldController = _currentController;
                    _currentController = newController;
                }

                // 이전 컨트롤러 정리
                if (oldController != null)
                {
                    try
                    {
                        if (oldController.IsConnected)
                        {
                            await oldController.DisconnectAsync();
                        }
                        if (oldController is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Error while cleaning up previous controller: {ex.Message}");
                    }
                }

                // 컨트롤러 변경 이벤트 발생
                ControllerChanged?.Invoke(null, new ControllerChangedEventArgs
                {
                    OldController = oldController,
                    NewController = newController,
                    IsHardwareMode = newController is EtherCATRobotController,
                    ChangedAt = DateTime.Now
                });

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Controller replacement complete");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Controller replacement failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 하드웨어 재감지 및 컨트롤러 갱신
        /// </summary>
        /// <returns>재감지 성공 여부</returns>
        public static async Task<bool> RefreshHardwareAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting hardware re-detection");

                // 하드웨어 재감지
                var detectionResult = await HardwareDetector.DetectAllHardwareAsync();

                lock (_lockObject)
                {
                    _lastDetectionResult = detectionResult;
                }

                // 감지 결과에 따라 컨트롤러 재생성
                bool refreshed = await CreateControllerBasedOnDetectionAsync(detectionResult);

                if (refreshed)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Hardware re-detection complete - Mode: {(IsHardwareMode ? "Hardware" : "Simulation")}");
                }

                return refreshed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Hardware re-detection failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Status and Information

        /// <summary>
        /// 현재 하드웨어 상태 정보 조회
        /// </summary>
        /// <returns>하드웨어 상태 정보</returns>
        public static HardwareStatusInfo GetHardwareStatus()
        {
            try
            {
                lock (_lockObject)
                {
                    return new HardwareStatusInfo
                    {
                        IsInitialized = _isInitialized,
                        IsHardwareMode = IsHardwareMode,
                        ControllerType = _currentController?.GetType().Name ?? "None",
                        IsControllerConnected = _currentController?.IsConnected ?? false,
                        LastDetectionResult = _lastDetectionResult,
                        StatusTime = DateTime.Now
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to get hardware status: {ex.Message}");
                return new HardwareStatusInfo { StatusTime = DateTime.Now };
            }
        }

        /// <summary>
        /// 사용 가능한 COM 포트 목록 조회
        /// </summary>
        /// <returns>COM 포트 정보 리스트</returns>
        public static List<ComPortInfo> GetAvailableComPorts()
        {
            try
            {
                return HardwareDetector.GetAvailableComPorts();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to get COM ports: {ex.Message}");
                return new List<ComPortInfo>();
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// 팩토리 정리 및 리소스 해제
        /// </summary>
        public static async Task CleanupAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting factory cleanup");

                // 이벤트 구독 해제
                HardwareDetector.HardwareDetected -= OnHardwareDetected;
                HardwareDetector.HardwareDisconnected -= OnHardwareDisconnected;

                // 현재 컨트롤러 해제
                IRobotController controller = null;

                lock (_lockObject)
                {
                    controller = _currentController;
                    _currentController = null;
                    _isInitialized = false;
                    _lastDetectionResult = null;
                }

                if (controller != null)
                {
                    try
                    {
                        if (controller.IsConnected)
                        {
                            await controller.DisconnectAsync();
                        }

                        if (controller is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Error during controller cleanup: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Factory cleanup complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Factory cleanup failed: {ex.Message}");
            }
        }

        #endregion
    }

    #region Event Args and Status Classes

    /// <summary>
    /// 컨트롤러 변경 이벤트 인수
    /// </summary>
    public class ControllerChangedEventArgs : EventArgs
    {
        public IRobotController OldController { get; set; }
        public IRobotController NewController { get; set; }
        public bool IsHardwareMode { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    /// <summary>
    /// 하드웨어 상태 변경 이벤트 인수
    /// </summary>
    public class HardwareStatusChangedEventArgs : EventArgs
    {
        public bool IsHardwareMode { get; set; }
        public HardwareDetectionResult DetectionResult { get; set; }
        public DateTime StatusChangedAt { get; set; }
    }

    /// <summary>
    /// 하드웨어 상태 정보
    /// </summary>
    public class HardwareStatusInfo
    {
        public bool IsInitialized { get; set; }
        public bool IsHardwareMode { get; set; }
        public string ControllerType { get; set; }
        public bool IsControllerConnected { get; set; }
        public HardwareDetectionResult LastDetectionResult { get; set; }
        public DateTime StatusTime { get; set; }

        public override string ToString()
        {
            return $"Hardware: {(IsHardwareMode ? "ON" : "OFF")}, Controller: {ControllerType}, Connected: {IsControllerConnected}";
        }
    }

    #endregion
}