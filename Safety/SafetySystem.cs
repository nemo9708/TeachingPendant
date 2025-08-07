using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using TeachingPendant.Alarm;

namespace TeachingPendant.Safety
{
    /// <summary>
    /// 안전 상태 열거형
    /// </summary>
    public enum SafetyStatus
    {
        /// <summary>안전 상태 - 모든 시스템 정상</summary>
        Safe,
        /// <summary>경고 상태 - 주의 필요</summary>
        Warning,
        /// <summary>위험 상태 - 즉시 확인 필요</summary>
        Dangerous,
        /// <summary>비상정지 상태</summary>
        EmergencyStop
    }

    /// <summary>
    /// 인터록 상태 열거형
    /// </summary>
    public enum InterlockStatus
    {
        /// <summary>인터록 열림 - 문이 열려있음 (안전하지 않음)</summary>
        Open,
        /// <summary>인터록 닫힘 - 문이 닫혀있음 (안전함)</summary>
        Closed,
        /// <summary>인터록 센서 오류</summary>
        SensorError,
        /// <summary>인터록 상태 불명</summary>
        Unknown
    }

    /// <summary>
    /// 인터록 장치 정보 클래스
    /// </summary>
    public class InterlockDevice
    {
        public string DeviceName { get; set; }
        public string Location { get; set; }
        public InterlockStatus Status { get; set; }
        public DateTime LastStatusChange { get; set; }
        public bool IsEnabled { get; set; }
        public string Description { get; set; }

        public InterlockDevice(string deviceName, string location, string description = "")
        {
            DeviceName = deviceName;
            Location = location;
            Description = description;
            Status = InterlockStatus.Unknown;
            LastStatusChange = DateTime.Now;
            IsEnabled = true;
        }
    }

    /// <summary>
    /// 안전 상태 변경 이벤트 인수
    /// </summary>
    public class SafetyStatusChangedEventArgs : EventArgs
    {
        public SafetyStatus OldStatus { get; set; }
        public SafetyStatus NewStatus { get; set; }
        public string Reason { get; set; }
        public DateTime ChangeTime { get; set; }

        public SafetyStatusChangedEventArgs(SafetyStatus oldStatus, SafetyStatus newStatus, string reason)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
            Reason = reason;
            ChangeTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 비상정지 이벤트 인수
    /// </summary>
    public class EmergencyStopEventArgs : EventArgs
    {
        public string Reason { get; set; }
        public DateTime TriggerTime { get; set; }
        public string TriggeredBy { get; set; }

        public EmergencyStopEventArgs(string reason, string triggeredBy = "System")
        {
            Reason = reason;
            TriggeredBy = triggeredBy;
            TriggerTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 인터록 상태 변경 이벤트 인수
    /// </summary>
    public class InterlockStatusChangedEventArgs : EventArgs
    {
        public string DeviceName { get; set; }
        public InterlockStatus OldStatus { get; set; }
        public InterlockStatus NewStatus { get; set; }
        public DateTime ChangeTime { get; set; }

        public InterlockStatusChangedEventArgs(string deviceName, InterlockStatus oldStatus, InterlockStatus newStatus)
        {
            DeviceName = deviceName;
            OldStatus = oldStatus;
            NewStatus = newStatus;
            ChangeTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 반도체 웨이퍼 반송 로봇을 위한 안전 시스템
    /// 인터록 개폐유무 감지 기능 포함
    /// </summary>
    public static class SafetySystem
    {
        #region Private Fields
        private static bool _isInitialized = false;
        private static SafetyStatus _currentStatus = SafetyStatus.Safe;
        private static Dictionary<string, InterlockDevice> _interlockDevices;
        private static DispatcherTimer _monitoringTimer;
        private static readonly object _lockObject = new object();
        private const string CLASS_NAME = "SafetySystem";
        #endregion

        #region Public Properties
        /// <summary>
        /// 안전 시스템 초기화 여부
        /// </summary>
        public static bool IsInitialized
        {
            get { return _isInitialized; }
        }

        /// <summary>
        /// 현재 안전 상태
        /// </summary>
        public static SafetyStatus CurrentStatus
        {
            get { return _currentStatus; }
        }

        /// <summary>
        /// 모든 인터록이 안전한지 확인
        /// </summary>
        public static bool AllInterlocksSecure
        {
            get
            {
                lock (_lockObject)
                {
                    if (_interlockDevices == null) return false;

                    foreach (var device in _interlockDevices.Values)
                    {
                        if (device.IsEnabled && device.Status != InterlockStatus.Closed)
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// 안전 상태 변경 이벤트
        /// </summary>
        public static event EventHandler<SafetyStatusChangedEventArgs> SafetyStatusChanged;

        /// <summary>
        /// 비상정지 발생 이벤트
        /// </summary>
        public static event EventHandler<EmergencyStopEventArgs> EmergencyStopTriggered;

        /// <summary>
        /// 인터록 상태 변경 이벤트
        /// </summary>
        public static event EventHandler<InterlockStatusChangedEventArgs> InterlockStatusChanged;
        #endregion

        #region Initialization and Shutdown
        /// <summary>
        /// 안전 시스템 초기화
        /// </summary>
        public static void Initialize()
        {
            try
            {
                if (_isInitialized)
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 이미 초기화되었습니다.");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 안전 시스템 초기화 시작");

                lock (_lockObject)
                {
                    // 인터록 장치 딕셔너리 초기화
                    _interlockDevices = new Dictionary<string, InterlockDevice>();

                    // 기본 인터록 장치들 등록
                    RegisterDefaultInterlockDevices();

                    // 모니터링 타이머 초기화
                    InitializeMonitoringTimer();

                    // 초기 안전 상태 설정
                    _currentStatus = SafetyStatus.Safe;

                    _isInitialized = true;
                }

                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 안전 시스템 초기화 완료");

                // 초기화 완료 알림
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_INITIALIZED, "안전 시스템이 초기화되었습니다.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 안전 시스템 초기화 실패: " + ex.Message);
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "안전 시스템 초기화 실패: " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 기본 인터록 장치들 등록
        /// </summary>
        private static void RegisterDefaultInterlockDevices()
        {
            try
            {
                // 챔버 인터록 장치들 등록
                RegisterInterlockDevice("Chamber1_Door", "챔버1", "챔버1 메인 도어 인터록");
                RegisterInterlockDevice("Chamber2_Door", "챔버2", "챔버2 메인 도어 인터록");
                RegisterInterlockDevice("LoadPort1_Door", "로드포트1", "로드포트1 도어 인터록");
                RegisterInterlockDevice("LoadPort2_Door", "로드포트2", "로드포트2 도어 인터록");

                // 안전 패널 및 게이트
                RegisterInterlockDevice("Safety_Panel", "안전패널", "메인 안전 패널 인터록");
                RegisterInterlockDevice("Emergency_Gate", "비상게이트", "비상 안전 게이트 인터록");

                // 서비스 도어
                RegisterInterlockDevice("Service_Door", "서비스도어", "장비 서비스 도어 인터록");

                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 기본 인터록 장치 " + _interlockDevices.Count + "개 등록 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 기본 인터록 장치 등록 실패: " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 모니터링 타이머 초기화
        /// </summary>
        private static void InitializeMonitoringTimer()
        {
            try
            {
                _monitoringTimer = new DispatcherTimer();
                _monitoringTimer.Interval = TimeSpan.FromMilliseconds(500);
                _monitoringTimer.Tick += MonitoringTimer_Tick;
                _monitoringTimer.Start();

                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 모니터링 타이머 시작 (500ms 간격)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 모니터링 타이머 초기화 실패: " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 안전 시스템 종료
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 안전 시스템 종료 시작");

                lock (_lockObject)
                {
                    if (_monitoringTimer != null)
                    {
                        _monitoringTimer.Stop();
                        _monitoringTimer.Tick -= MonitoringTimer_Tick;
                        _monitoringTimer = null;
                    }

                    if (_interlockDevices != null)
                    {
                        _interlockDevices.Clear();
                        _interlockDevices = null;
                    }

                    _isInitialized = false;
                }

                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 안전 시스템 종료 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 안전 시스템 종료 중 오류: " + ex.Message);
            }
        }
        #endregion

        #region Interlock Device Management
        /// <summary>
        /// 인터록 장치 등록
        /// </summary>
        /// <param name="deviceName">장치명</param>
        /// <param name="location">위치</param>
        /// <param name="description">설명</param>
        public static void RegisterInterlockDevice(string deviceName, string location, string description = "")
        {
            try
            {
                if (string.IsNullOrEmpty(deviceName))
                {
                    throw new ArgumentException("장치명은 필수입니다.", "deviceName");
                }

                lock (_lockObject)
                {
                    if (_interlockDevices == null)
                    {
                        _interlockDevices = new Dictionary<string, InterlockDevice>();
                    }

                    var device = new InterlockDevice(deviceName, location, description);
                    _interlockDevices[deviceName] = device;

                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 장치 등록: " + deviceName + " (" + location + ")");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 장치 등록 실패: " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 인터록 장치 제거
        /// </summary>
        /// <param name="deviceName">장치명</param>
        /// <returns>제거 성공 여부</returns>
        public static bool UnregisterInterlockDevice(string deviceName)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_interlockDevices != null && _interlockDevices.ContainsKey(deviceName))
                    {
                        _interlockDevices.Remove(deviceName);
                        System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 장치 제거: " + deviceName);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 장치 제거 실패: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 인터록 장치 활성화/비활성화
        /// </summary>
        /// <param name="deviceName">장치명</param>
        /// <param name="enabled">활성화 여부</param>
        /// <returns>설정 성공 여부</returns>
        public static bool SetInterlockDeviceEnabled(string deviceName, bool enabled)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_interlockDevices != null && _interlockDevices.ContainsKey(deviceName))
                    {
                        _interlockDevices[deviceName].IsEnabled = enabled;

                        string status = enabled ? "활성화" : "비활성화";
                        System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 장치 " + status + ": " + deviceName);

                        AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE,
                            "인터록 장치 " + deviceName + " " + status + "됨");

                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 장치 설정 실패: " + ex.Message);
                return false;
            }
        }
        #endregion

        #region Interlock Status Management
        /// <summary>
        /// 인터록 상태 업데이트 (실제 하드웨어에서 호출)
        /// </summary>
        /// <param name="deviceName">장치명</param>
        /// <param name="status">새로운 상태</param>
        public static void UpdateInterlockStatus(string deviceName, InterlockStatus status)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_interlockDevices != null && _interlockDevices.ContainsKey(deviceName))
                    {
                        var device = _interlockDevices[deviceName];
                        var oldStatus = device.Status;

                        if (oldStatus != status)
                        {
                            device.Status = status;
                            device.LastStatusChange = DateTime.Now;

                            System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 상태 변경: " + deviceName + " " + oldStatus + " -> " + status);

                            // 상태 변경 이벤트 발생
                            var handler = InterlockStatusChanged;
                            if (handler != null)
                            {
                                handler(null, new InterlockStatusChangedEventArgs(deviceName, oldStatus, status));
                            }

                            // 안전 상태 재평가
                            EvaluateSafetyStatus();

                            // 상태 메시지 표시
                            ShowInterlockStatusMessage(deviceName, status);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 상태 업데이트 실패: " + ex.Message);
            }
        }

        /// <summary>
        /// 인터록 상태 조회
        /// </summary>
        /// <param name="deviceName">장치명</param>
        /// <returns>인터록 상태</returns>
        public static InterlockStatus GetInterlockStatus(string deviceName)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_interlockDevices != null && _interlockDevices.ContainsKey(deviceName))
                    {
                        return _interlockDevices[deviceName].Status;
                    }
                }
                return InterlockStatus.Unknown;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 상태 조회 실패: " + ex.Message);
                return InterlockStatus.Unknown;
            }
        }

        /// <summary>
        /// 특정 인터록이 안전한지 확인
        /// </summary>
        /// <param name="deviceName">장치명</param>
        /// <returns>안전 여부</returns>
        public static bool IsInterlockSecure(string deviceName)
        {
            var status = GetInterlockStatus(deviceName);
            return status == InterlockStatus.Closed;
        }

        /// <summary>
        /// 인터록 상태 메시지 표시
        /// </summary>
        /// <param name="deviceName">장치명</param>
        /// <param name="status">상태</param>
        private static void ShowInterlockStatusMessage(string deviceName, InterlockStatus status)
        {
            try
            {
                string message = "";
                string alarmCode = "";

                switch (status)
                {
                    case InterlockStatus.Closed:
                        message = deviceName + " 인터록 닫힘 (안전)";
                        alarmCode = Alarms.STATUS_UPDATE;
                        break;
                    case InterlockStatus.Open:
                        message = deviceName + " 인터록 열림 (위험)";
                        alarmCode = Alarms.WARNING;
                        break;
                    case InterlockStatus.SensorError:
                        message = deviceName + " 인터록 센서 오류";
                        alarmCode = Alarms.SYSTEM_ERROR;
                        break;
                    case InterlockStatus.Unknown:
                        message = deviceName + " 인터록 상태 불명";
                        alarmCode = Alarms.WARNING;
                        break;
                }

                AlarmMessageManager.ShowAlarm(alarmCode, message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 메시지 표시 실패: " + ex.Message);
            }
        }
        #endregion

        #region Safety Status Management
        /// <summary>
        /// 안전 상태 평가 및 업데이트
        /// </summary>
        private static void EvaluateSafetyStatus()
        {
            try
            {
                var newStatus = CalculateSafetyStatus();
                var oldStatus = _currentStatus;

                if (oldStatus != newStatus)
                {
                    _currentStatus = newStatus;

                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 안전 상태 변경: " + oldStatus + " -> " + newStatus);

                    // 상태 변경 이벤트 발생
                    var handler1 = SafetyStatusChanged;
                    if (handler1 != null)
                    {
                        handler1(null, new SafetyStatusChangedEventArgs(oldStatus, newStatus, "인터록 상태 변경"));
                    }

                    // 비상정지 상태인 경우 비상정지 이벤트 발생
                    if (newStatus == SafetyStatus.EmergencyStop)
                    {
                        var handler2 = EmergencyStopTriggered;
                        if (handler2 != null)
                        {
                            handler2(null, new EmergencyStopEventArgs("인터록 안전 조건 위반"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 안전 상태 평가 실패: " + ex.Message);
            }
        }

        /// <summary>
        /// 현재 조건에 따른 안전 상태 계산
        /// </summary>
        /// <returns>계산된 안전 상태</returns>
        private static SafetyStatus CalculateSafetyStatus()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_interlockDevices == null || _interlockDevices.Count == 0)
                    {
                        return SafetyStatus.Warning;
                    }

                    int openCount = 0;
                    int errorCount = 0;
                    int enabledCount = 0;

                    foreach (var device in _interlockDevices.Values)
                    {
                        if (!device.IsEnabled) continue;

                        enabledCount++;

                        switch (device.Status)
                        {
                            case InterlockStatus.Open:
                                openCount++;
                                break;
                            case InterlockStatus.SensorError:
                            case InterlockStatus.Unknown:
                                errorCount++;
                                break;
                        }
                    }

                    // 안전 상태 결정 로직
                    if (errorCount > 0)
                    {
                        return SafetyStatus.EmergencyStop;
                    }
                    else if (openCount > 0)
                    {
                        return SafetyStatus.Dangerous;
                    }
                    else if (enabledCount == 0)
                    {
                        return SafetyStatus.Warning;
                    }
                    else
                    {
                        return SafetyStatus.Safe;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 안전 상태 계산 실패: " + ex.Message);
                return SafetyStatus.EmergencyStop;
            }
        }
        #endregion

        #region Simulation and Testing
        /// <summary>
        /// 시뮬레이션을 위한 인터록 상태 시뮬레이션
        /// 실제 하드웨어 연동 전까지 사용
        /// </summary>
        private static void SimulateInterlockStates()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_interlockDevices == null) return;

                    // 시뮬레이션: 대부분의 인터록은 닫힌 상태로 유지
                    foreach (var device in _interlockDevices.Values)
                    {
                        if (device.Status == InterlockStatus.Unknown)
                        {
                            // 시뮬레이션에서는 기본적으로 닫힌 상태로 설정
                            UpdateInterlockStatus(device.DeviceName, InterlockStatus.Closed);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 시뮬레이션 실패: " + ex.Message);
            }
        }

        /// <summary>
        /// 테스트용 인터록 상태 강제 설정
        /// </summary>
        /// <param name="deviceName">장치명</param>
        /// <param name="status">설정할 상태</param>
        public static void ForceInterlockStatus(string deviceName, InterlockStatus status)
        {
            System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 테스트용 인터록 상태 강제 설정: " + deviceName + " -> " + status);
            UpdateInterlockStatus(deviceName, status);
        }

        /// <summary>
        /// 모든 인터록을 안전 상태로 설정 (테스트용)
        /// </summary>
        public static void SetAllInterlocksSecure()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_interlockDevices == null) return;

                    var deviceNames = _interlockDevices.Keys.ToList();
                    foreach (var deviceName in deviceNames)
                    {
                        UpdateInterlockStatus(deviceName, InterlockStatus.Closed);
                    }
                }

                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 모든 인터록을 안전 상태로 설정 완료");
                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "모든 인터록이 안전 상태로 설정되었습니다.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 안전 설정 실패: " + ex.Message);
            }
        }

        /// <summary>
        /// 특정 인터록을 위험 상태로 설정 (테스트용)
        /// </summary>
        /// <param name="deviceName">장치명</param>
        public static void SetInterlockUnsafe(string deviceName)
        {
            System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 테스트용 인터록 위험 설정: " + deviceName);
            UpdateInterlockStatus(deviceName, InterlockStatus.Open);
        }
        #endregion

        #region Monitoring Timer
        /// <summary>
        /// 모니터링 타이머 틱 이벤트
        /// </summary>
        private static void MonitoringTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // 시뮬레이션 환경에서는 자동으로 상태 시뮬레이션
                SimulateInterlockStates();

                // 실제 환경에서는 여기서 하드웨어 I/O를 읽어서 상태 업데이트
                // TODO: 실제 하드웨어 연동 시 구현
                // ReadHardwareInterlockStates();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 모니터링 타이머 처리 중 오류: " + ex.Message);
            }
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// 인터록 시스템 상태 요약 정보 반환
        /// </summary>
        /// <returns>상태 요약 문자열</returns>
        public static string GetInterlockSystemSummary()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_interlockDevices == null || _interlockDevices.Count == 0)
                    {
                        return "인터록 장치가 등록되지 않음";
                    }

                    int totalCount = _interlockDevices.Count;
                    int enabledCount = 0;
                    int closedCount = 0;
                    int openCount = 0;
                    int errorCount = 0;

                    foreach (var device in _interlockDevices.Values)
                    {
                        if (device.IsEnabled)
                        {
                            enabledCount++;

                            switch (device.Status)
                            {
                                case InterlockStatus.Closed:
                                    closedCount++;
                                    break;
                                case InterlockStatus.Open:
                                    openCount++;
                                    break;
                                case InterlockStatus.SensorError:
                                case InterlockStatus.Unknown:
                                    errorCount++;
                                    break;
                            }
                        }
                    }

                    return "인터록 시스템: 전체 " + totalCount +
                        "개, 활성 " + enabledCount + "개, 안전 " + closedCount +
                        "개, 열림 " + openCount + "개, 오류 " + errorCount + "개";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 시스템 요약 생성 실패: " + ex.Message);
                return "인터록 시스템 상태 확인 실패";
            }
        }

        /// <summary>
        /// 주어진 좌표가 소프트 리미트 범위 내에 있는지 확인
        /// </summary>
        /// <param name="positionA">A축 좌표</param>
        /// <param name="positionT">T축 좌표</param>
        /// <param name="positionZ">Z축 좌표</param>
        /// <returns>범위 내 여부</returns>
        public static bool IsWithinSoftLimits(double positionA, double positionT, double positionZ)
        {
            try
            {
                // Setup에서 소프트 리미트 값 가져오기
                var limitA1 = (double)SetupUI.Setup.SoftLimitA1;
                var limitA2 = (double)SetupUI.Setup.SoftLimitA2;
                var limitT1 = (double)SetupUI.Setup.SoftLimitT1;
                var limitT2 = (double)SetupUI.Setup.SoftLimitT2;
                var limitZ1 = (double)SetupUI.Setup.SoftLimitZ1;
                var limitZ2 = (double)SetupUI.Setup.SoftLimitZ2;

                // A축 범위 확인
                if (positionA < limitA1 || positionA > limitA2)
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] A축 소프트 리미트 초과: " + positionA + " (범위: " + limitA1 + " ~ " + limitA2 + ")");
                    return false;
                }

                // T축 범위 확인
                if (positionT < limitT1 || positionT > limitT2)
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] T축 소프트 리미트 초과: " + positionT + " (범위: " + limitT1 + " ~ " + limitT2 + ")");
                    return false;
                }

                // Z축 범위 확인
                if (positionZ < limitZ1 || positionZ > limitZ2)
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] Z축 소프트 리미트 초과: " + positionZ + " (범위: " + limitZ1 + " ~ " + limitZ2 + ")");
                    return false;
                }

                // 모든 축이 범위 내에 있음
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 소프트 리미트 확인 실패: " + ex.Message);
                return false; // 오류 시 안전하지 않은 것으로 판단
            }
        }

        /// <summary>
        /// 특정 위치의 모든 인터록 상태 확인 (완전한 구현)
        /// </summary>
        /// <param name="location">위치명</param>
        /// <returns>해당 위치의 모든 인터록이 안전한지 여부</returns>
        public static bool IsLocationSecure(string location)
        {
            try
            {
                if (string.IsNullOrEmpty(location))
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 위치명이 null이거나 비어있음");
                    return false;
                }

                lock (_lockObject)
                {
                    if (_interlockDevices == null || _interlockDevices.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 장치가 등록되지 않음");
                        return false;
                    }

                    bool hasDevicesInLocation = false;
                    bool allSecure = true;

                    // 해당 위치의 모든 인터록 장치 확인
                    foreach (var device in _interlockDevices.Values)
                    {
                        if (!device.IsEnabled) continue;

                        // 위치명 매칭 (대소문자 구분 없이 포함 확인)
                        if (device.Location.IndexOf(location, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            device.DeviceName.IndexOf(location, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            hasDevicesInLocation = true;

                            if (device.Status != InterlockStatus.Closed)
                            {
                                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 위치 " + location + "의 인터록 " + device.DeviceName + " 상태: " + device.Status);
                                allSecure = false;
                            }
                        }
                    }

                    // 해당 위치에 인터록 장치가 없는 경우
                    if (!hasDevicesInLocation)
                    {
                        System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 위치 " + location + "에 등록된 인터록 장치가 없음");

                        // 특정 위치명에 대한 기본 매핑으로 확인
                        switch (location.ToLower())
                        {
                            case "chamber1":
                            case "챔버1":
                                return IsChamberSecure(1);
                            case "chamber2":
                            case "챔버2":
                                return IsChamberSecure(2);
                            case "loadport1":
                            case "로드포트1":
                                return IsLoadPortSecure(1);
                            case "loadport2":
                            case "로드포트2":
                                return IsLoadPortSecure(2);
                            default:
                                return true; // 매핑되지 않은 위치는 기본적으로 안전으로 간주
                        }
                    }

                    return allSecure;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 위치별 안전성 확인 실패: " + ex.Message);
                return false; // 오류 시 안전하지 않은 것으로 판단
            }
        }

        /// <summary>
        /// 등록된 모든 인터록 장치 목록 반환
        /// </summary>
        /// <returns>인터록 장치 목록</returns>
        public static List<InterlockDevice> GetAllInterlockDevices()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_interlockDevices == null)
                    {
                        return new List<InterlockDevice>();
                    }

                    var deviceList = new List<InterlockDevice>();
                    foreach (var device in _interlockDevices.Values)
                    {
                        var newDevice = new InterlockDevice(device.DeviceName, device.Location, device.Description);
                        newDevice.Status = device.Status;
                        newDevice.LastStatusChange = device.LastStatusChange;
                        newDevice.IsEnabled = device.IsEnabled;
                        deviceList.Add(newDevice);
                    }

                    return deviceList;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 장치 목록 조회 실패: " + ex.Message);
                return new List<InterlockDevice>();
            }
        }

        /// <summary>
        /// 안전하지 않은 인터록 장치 목록 반환
        /// </summary>
        /// <returns>안전하지 않은 인터록 장치 목록</returns>
        public static List<InterlockDevice> GetUnsafeInterlockDevices()
        {
            try
            {
                lock (_lockObject)
                {
                    var unsafeDevices = new List<InterlockDevice>();

                    if (_interlockDevices == null) return unsafeDevices;

                    foreach (var device in _interlockDevices.Values)
                    {
                        if (device.IsEnabled && device.Status != InterlockStatus.Closed)
                        {
                            var newDevice = new InterlockDevice(device.DeviceName, device.Location, device.Description);
                            newDevice.Status = device.Status;
                            newDevice.LastStatusChange = device.LastStatusChange;
                            newDevice.IsEnabled = device.IsEnabled;
                            unsafeDevices.Add(newDevice);
                        }
                    }

                    return unsafeDevices;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 안전하지 않은 장치 목록 조회 실패: " + ex.Message);
                return new List<InterlockDevice>();
            }
        }

        /// <summary>
        /// 인터록 장치 정보 조회
        /// </summary>
        /// <param name="deviceName">장치명</param>
        /// <returns>인터록 장치 정보</returns>
        public static InterlockDevice GetInterlockDevice(string deviceName)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_interlockDevices != null && _interlockDevices.ContainsKey(deviceName))
                    {
                        var device = _interlockDevices[deviceName];
                        var newDevice = new InterlockDevice(device.DeviceName, device.Location, device.Description);
                        newDevice.Status = device.Status;
                        newDevice.LastStatusChange = device.LastStatusChange;
                        newDevice.IsEnabled = device.IsEnabled;
                        return newDevice;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 인터록 장치 정보 조회 실패: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 챔버별 인터록 상태 확인
        /// </summary>
        /// <param name="chamberNumber">챔버 번호 (1, 2)</param>
        /// <returns>챔버 인터록이 안전한지 여부</returns>
        public static bool IsChamberSecure(int chamberNumber)
        {
            string deviceName = "Chamber" + chamberNumber + "_Door";
            return IsInterlockSecure(deviceName);
        }

        /// <summary>
        /// 로드포트별 인터록 상태 확인
        /// </summary>
        /// <param name="loadPortNumber">로드포트 번호 (1, 2)</param>
        /// <returns>로드포트 인터록이 안전한지 여부</returns>
        public static bool IsLoadPortSecure(int loadPortNumber)
        {
            string deviceName = "LoadPort" + loadPortNumber + "_Door";
            return IsInterlockSecure(deviceName);
        }

        /// <summary>
        /// 전체 시스템이 로봇 작업을 위해 안전한지 확인
        /// </summary>
        /// <returns>로봇 작업 가능 여부</returns>
        public static bool IsSafeForRobotOperation()
        {
            try
            {
                // 1. 전체 안전 상태 확인
                if (_currentStatus != SafetyStatus.Safe)
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 로봇 작업 불가: 안전 상태 " + _currentStatus);
                    return false;
                }

                // 2. 모든 인터록 확인
                if (!AllInterlocksSecure)
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 로봇 작업 불가: 인터록 미안전");
                    return false;
                }

                // 3. 핵심 장치들 개별 확인
                string[] criticalDevices = { "Chamber1_Door", "Chamber2_Door", "Safety_Panel" };
                for (int i = 0; i < criticalDevices.Length; i++)
                {
                    string device = criticalDevices[i];
                    if (!IsInterlockSecure(device))
                    {
                        System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 로봇 작업 불가: " + device + " 미안전");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 로봇 작업 안전성 확인 실패: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 인터록 시스템 진단 정보 반환
        /// </summary>
        /// <returns>진단 정보 문자열</returns>
        public static string GetDiagnosticInfo()
        {
            try
            {
                var info = new System.Text.StringBuilder();
                info.AppendLine("=== SafetySystem 진단 정보 ===");
                info.AppendLine("초기화 상태: " + _isInitialized);
                info.AppendLine("현재 안전 상태: " + _currentStatus);
                info.AppendLine("모니터링 타이머: " + (_monitoringTimer != null ? "활성" : "비활성"));
                info.AppendLine("");

                lock (_lockObject)
                {
                    if (_interlockDevices != null)
                    {
                        info.AppendLine("등록된 인터록 장치: " + _interlockDevices.Count + "개");
                        foreach (var device in _interlockDevices.Values)
                        {
                            string enabledStatus = device.IsEnabled ? "활성" : "비활성";
                            info.AppendLine("  - " + device.DeviceName + " (" + device.Location + "): " + device.Status + " [" + enabledStatus + "]");
                        }
                    }
                    else
                    {
                        info.AppendLine("인터록 장치: 미초기화");
                    }
                }

                info.AppendLine("");
                info.AppendLine("전체 인터록 안전: " + AllInterlocksSecure);
                info.AppendLine("로봇 작업 가능: " + IsSafeForRobotOperation());

                return info.ToString();
            }
            catch (Exception ex)
            {
                return "진단 정보 생성 실패: " + ex.Message;
            }
        }
        #endregion

        #region Emergency Methods
        /// <summary>
        /// 수동 비상정지 트리거
        /// </summary>
        /// <param name="reason">비상정지 사유</param>
        /// <param name="triggeredBy">트리거한 사용자/시스템</param>
        public static void TriggerEmergencyStop(string reason, string triggeredBy = "Manual")
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 수동 비상정지 트리거: " + reason + " by " + triggeredBy);

                // 안전 상태를 비상정지로 변경
                var oldStatus = _currentStatus;
                _currentStatus = SafetyStatus.EmergencyStop;

                // 안전 상태 변경 이벤트 발생
                var handler1 = SafetyStatusChanged;
                if (handler1 != null)
                {
                    handler1(null, new SafetyStatusChangedEventArgs(oldStatus, _currentStatus, reason));
                }

                // 비상정지 이벤트 발생
                var handler2 = EmergencyStopTriggered;
                if (handler2 != null)
                {
                    handler2(null, new EmergencyStopEventArgs(reason, triggeredBy));
                }

                // 알람 메시지 표시
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "비상정지 발생: " + reason);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 비상정지 트리거 실패: " + ex.Message);
            }
        }

        /// <summary>
        /// 비상정지 해제 (리셋)
        /// </summary>
        /// <param name="resetBy">리셋 수행자</param>
        /// <returns>리셋 성공 여부</returns>
        public static bool ResetEmergencyStop(string resetBy = "Manual")
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 비상정지 리셋 시도 by " + resetBy);

                if (_currentStatus != SafetyStatus.EmergencyStop)
                {
                    System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 비상정지 상태가 아님: " + _currentStatus);
                    return false;
                }

                // 인터록 상태 재평가
                EvaluateSafetyStatus();

                // 여전히 비상정지 상태라면 리셋 실패
                if (_currentStatus == SafetyStatus.EmergencyStop)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.WARNING, "안전 조건이 충족되지 않아 비상정지를 해제할 수 없습니다.");
                    return false;
                }

                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "비상정지가 해제되었습니다. (by " + resetBy + ")");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[" + CLASS_NAME + "] 비상정지 리셋 실패: " + ex.Message);
                return false;
            }
        }
        #endregion
    }
}