using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace TeachingPendant.HardwareControllers
{
    /// <summary>
    /// 하드웨어 감지기 - COM 포트 자동 감지 및 관리
    /// DTP-7H 팬던트와 로봇 컨트롤러 자동 검색 기능
    /// </summary>
    public static class HardwareDetector
    {
        #region Constants
        private const string CLASS_NAME = "HardwareDetector";

        // DTP-7H 식별 정보
        private static readonly string[] DTP7H_VENDOR_IDS = { "0403", "10C4", "067B" }; // FTDI, Silicon Labs, Prolific
        private static readonly string[] DTP7H_PRODUCT_IDS = { "6001", "EA60", "2303" };
        private static readonly string[] DTP7H_DESCRIPTIONS = { "USB Serial Port", "DTP-7H", "DAINCUBE" };
        #endregion

        #region Hardware Detection Events
        /// <summary>
        /// 하드웨어 감지 이벤트
        /// </summary>
        public static event EventHandler<HardwareDetectedEventArgs> HardwareDetected;

        /// <summary>
        /// 하드웨어 연결 해제 이벤트
        /// </summary>
        public static event EventHandler<HardwareDisconnectedEventArgs> HardwareDisconnected;
        #endregion

        #region COM Port Detection

        /// <summary>
        /// 모든 사용 가능한 COM 포트 조회
        /// </summary>
        /// <returns>COM 포트 정보 리스트</returns>
        public static List<ComPortInfo> GetAvailableComPorts()
        {
            try
            {
                var comPorts = new List<ComPortInfo>();
                string[] portNames = SerialPort.GetPortNames();

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Searching for available COM ports... ({portNames.Length} found)");

                foreach (string portName in portNames)
                {
                    var portInfo = GetComPortInfo(portName);
                    if (portInfo != null)
                    {
                        comPorts.Add(portInfo);
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] COM port found: {portName} - {portInfo.Description}");
                    }
                }

                return comPorts.OrderBy(p => p.PortName).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to retrieve COM ports: {ex.Message}");
                return new List<ComPortInfo>();
            }
        }

        /// <summary>
        /// 특정 COM 포트의 상세 정보 조회
        /// </summary>
        /// <param name="portName">포트명</param>
        /// <returns>COM 포트 정보</returns>
        private static ComPortInfo GetComPortInfo(string portName)
        {
            try
            {
                // WMI를 통해 COM 포트 상세 정보 조회
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'"))
                {
                    foreach (ManagementObject queryObj in searcher.Get())
                    {
                        string caption = queryObj["Caption"]?.ToString() ?? "";

                        if (caption.Contains($"({portName})"))
                        {
                            return new ComPortInfo
                            {
                                PortName = portName,
                                Description = caption,
                                DeviceID = queryObj["DeviceID"]?.ToString() ?? "",
                                Manufacturer = queryObj["Manufacturer"]?.ToString() ?? "",
                                Service = queryObj["Service"]?.ToString() ?? "",
                                IsAvailable = IsPortAvailable(portName)
                            };
                        }
                    }
                }

                // WMI 조회 실패 시 기본 정보 생성
                return new ComPortInfo
                {
                    PortName = portName,
                    Description = $"Serial Port ({portName})",
                    DeviceID = "Unknown",
                    Manufacturer = "Unknown",
                    Service = "Serial",
                    IsAvailable = IsPortAvailable(portName)
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to retrieve info for {portName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// COM 포트 사용 가능 여부 확인
        /// </summary>
        /// <param name="portName">포트명</param>
        /// <returns>사용 가능 여부</returns>
        private static bool IsPortAvailable(string portName)
        {
            try
            {
                using (var port = new SerialPort(portName))
                {
                    port.Open();
                    return true;
                }
            }
            catch
            {
                return false; // 포트가 사용 중이거나 접근 불가
            }
        }

        #endregion

        #region DTP-7H Detection

        /// <summary>
        /// DTP-7H 팬던트 자동 감지
        /// </summary>
        /// <returns>DTP-7H가 연결된 COM 포트 정보</returns>
        public static async Task<ComPortInfo> DetectDTP7HAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting DTP-7H auto-detection...");

                var availablePorts = GetAvailableComPorts();

                if (availablePorts.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] No available COM ports found");
                    return null;
                }

                // 1단계: 하드웨어 식별자로 필터링
                var candidatePorts = FilterDTP7HCandidates(availablePorts);

                if (candidatePorts.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] No DTP-7H candidate ports found, testing all ports");
                    candidatePorts = availablePorts;
                }

                // 2단계: 통신 테스트로 실제 DTP-7H 확인
                foreach (var port in candidatePorts)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Testing DTP-7H communication: {port.PortName}");

                    if (await TestDTP7HCommunicationAsync(port.PortName))
                    {
                        port.DeviceType = HardwareDeviceType.DTP7H;
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] DTP-7H detection successful: {port.PortName}");

                        // 하드웨어 감지 이벤트 발생
                        HardwareDetected?.Invoke(null, new HardwareDetectedEventArgs
                        {
                            DeviceType = HardwareDeviceType.DTP7H,
                            PortInfo = port,
                            DetectedAt = DateTime.Now
                        });

                        return port;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Could not find DTP-7H");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] DTP-7H detection failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// DTP-7H 후보 포트 필터링
        /// </summary>
        /// <param name="allPorts">모든 COM 포트</param>
        /// <returns>DTP-7H 후보 포트 리스트</returns>
        private static List<ComPortInfo> FilterDTP7HCandidates(List<ComPortInfo> allPorts)
        {
            var candidates = new List<ComPortInfo>();

            foreach (var port in allPorts)
            {
                // 디바이스 ID에서 Vendor ID, Product ID 추출 및 확인
                bool isCandidate = false;

                foreach (string vendorId in DTP7H_VENDOR_IDS)
                {
                    if (port.DeviceID.ToUpper().Contains($"VID_{vendorId}"))
                    {
                        isCandidate = true;
                        break;
                    }
                }

                // 설명에서 DTP-7H 관련 키워드 확인
                if (!isCandidate)
                {
                    foreach (string description in DTP7H_DESCRIPTIONS)
                    {
                        if (port.Description.ToUpper().Contains(description.ToUpper()))
                        {
                            isCandidate = true;
                            break;
                        }
                    }
                }

                if (isCandidate)
                {
                    candidates.Add(port);
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] DTP-7H candidate port: {port.PortName} - {port.Description}");
                }
            }

            return candidates;
        }

        /// <summary>
        /// DTP-7H 통신 테스트
        /// </summary>
        /// <param name="portName">테스트할 포트명</param>
        /// <returns>DTP-7H 통신 성공 여부</returns>
        private static async Task<bool> TestDTP7HCommunicationAsync(string portName)
        {
            SerialPort testPort = null;

            try
            {
                // DTP-7H 표준 설정으로 포트 열기
                testPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
                testPort.ReadTimeout = 1000;
                testPort.WriteTimeout = 1000;
                testPort.DtrEnable = true;
                testPort.RtsEnable = true;

                testPort.Open();

                // 포트 안정화 대기
                await Task.Delay(100);

                // DTP-7H 식별 명령 전송 (LED 상태 조회)
                byte[] identifyCommand = CreateDTP7HIdentifyCommand();
                testPort.Write(identifyCommand, 0, identifyCommand.Length);

                // 응답 대기
                await Task.Delay(200);

                // 응답 확인
                if (testPort.BytesToRead > 0)
                {
                    byte[] response = new byte[testPort.BytesToRead];
                    int bytesRead = testPort.Read(response, 0, response.Length);

                    // DTP-7H 응답 패턴 검증
                    if (ValidateDTP7HResponse(response, bytesRead))
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] DTP-7H response validation successful: {portName}");
                        return true;
                    }
                }

                // 추가 테스트: 간단한 LED 제어 명령
                byte[] ledTestCommand = CreateLEDTestCommand();
                testPort.Write(ledTestCommand, 0, ledTestCommand.Length);

                await Task.Delay(100);

                // LED 명령에 대한 ACK 확인
                if (testPort.BytesToRead > 0)
                {
                    byte[] ledResponse = new byte[testPort.BytesToRead];
                    testPort.Read(ledResponse, 0, ledResponse.Length);

                    // 간단한 ACK 패턴 확인
                    if (ledResponse.Length > 0 && (ledResponse[0] == 0x06 || ledResponse[0] == 0x02))
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] DTP-7H LED test successful: {portName}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] {portName} DTP-7H communication test failed: {ex.Message}");
                return false;
            }
            finally
            {
                try
                {
                    if (testPort != null && testPort.IsOpen)
                    {
                        testPort.Close();
                    }
                    testPort?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to release test port: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// DTP-7H 식별 명령 생성
        /// </summary>
        /// <returns>식별 명령 바이트 배열</returns>
        private static byte[] CreateDTP7HIdentifyCommand()
        {
            // DTP-7H 상태 조회 명령 (팬던트 API 매뉴얼 기준)
            var command = new List<byte>();

            command.Add(0x02); // STX
            command.Add(0x03); // 길이
            command.Add(0xFF); // 상태 조회 명령
            command.Add(0x00); // 파라미터
            command.Add(0x00); // 파라미터

            // CRC16 계산 (간단한 XOR 체크섬으로 대체)
            byte checksum = 0;
            for (int i = 1; i < command.Count; i++)
            {
                checksum ^= command[i];
            }
            command.Add(checksum);
            command.Add(0x03); // ETX

            return command.ToArray();
        }

        /// <summary>
        /// LED 테스트 명령 생성
        /// </summary>
        /// <returns>LED 테스트 명령 바이트 배열</returns>
        private static byte[] CreateLEDTestCommand()
        {
            // 좌측 LED1 파란색 점등 명령
            var command = new List<byte>();

            command.Add(0x02); // STX
            command.Add(0x04); // 길이
            command.Add(0xC1); // 좌측 LED1 주소
            command.Add(0x31); // 파란색 값
            command.Add(0x00); // 추가 파라미터

            // 체크섬
            byte checksum = 0;
            for (int i = 1; i < command.Count; i++)
            {
                checksum ^= command[i];
            }
            command.Add(checksum);
            command.Add(0x03); // ETX

            return command.ToArray();
        }

        /// <summary>
        /// DTP-7H 응답 검증
        /// </summary>
        /// <param name="response">응답 데이터</param>
        /// <param name="length">응답 길이</param>
        /// <returns>유효한 DTP-7H 응답 여부</returns>
        private static bool ValidateDTP7HResponse(byte[] response, int length)
        {
            try
            {
                if (length < 3)
                    return false;

                // DTP-7H 응답 패턴 확인
                // STX(0x02)로 시작하거나 ACK(0x06)로 시작하는 경우
                if (response[0] == 0x02 || response[0] == 0x06)
                {
                    return true;
                }

                // 특정 DTP-7H 응답 패턴 확인
                if (length >= 4 && response[0] == 0x02 && response[length - 1] == 0x03)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] DTP-7H response validation failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Robot Controller Detection

        /// <summary>
        /// 로봇 컨트롤러 자동 감지
        /// </summary>
        /// <returns>로봇 컨트롤러가 연결된 COM 포트 정보</returns>
        public static async Task<ComPortInfo> DetectRobotControllerAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting robot controller auto-detection...");

                var availablePorts = GetAvailableComPorts();

                // DTP-7H가 아닌 포트들 중에서 로봇 컨트롤러 검색
                foreach (var port in availablePorts)
                {
                    if (await TestRobotControllerCommunicationAsync(port.PortName))
                    {
                        port.DeviceType = HardwareDeviceType.RobotController;
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Robot controller detection successful: {port.PortName}");

                        // 하드웨어 감지 이벤트 발생
                        HardwareDetected?.Invoke(null, new HardwareDetectedEventArgs
                        {
                            DeviceType = HardwareDeviceType.RobotController,
                            PortInfo = port,
                            DetectedAt = DateTime.Now
                        });

                        return port;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Could not find robot controller");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Robot controller detection failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 로봇 컨트롤러 통신 테스트
        /// </summary>
        /// <param name="portName">테스트할 포트명</param>
        /// <returns>로봇 컨트롤러 통신 성공 여부</returns>
        private static async Task<bool> TestRobotControllerCommunicationAsync(string portName)
        {
            SerialPort testPort = null;

            try
            {
                // 로봇 컨트롤러 표준 설정 (일반적으로 115200 또는 9600)
                int[] baudRates = { 115200, 9600, 19200, 38400 };

                foreach (int baudRate in baudRates)
                {
                    try
                    {
                        testPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                        testPort.ReadTimeout = 500;
                        testPort.WriteTimeout = 500;

                        testPort.Open();
                        await Task.Delay(100);

                        // 로봇 컨트롤러 상태 조회 명령
                        byte[] statusCommand = { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0x84, 0x0A };
                        testPort.Write(statusCommand, 0, statusCommand.Length);

                        await Task.Delay(200);

                        if (testPort.BytesToRead > 0)
                        {
                            byte[] response = new byte[testPort.BytesToRead];
                            testPort.Read(response, 0, response.Length);

                            if (ValidateRobotControllerResponse(response))
                            {
                                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Robot controller communication successful: {portName} @{baudRate}");
                                return true;
                            }
                        }

                        testPort.Close();
                        testPort.Dispose();
                        testPort = null;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] {portName} @{baudRate} test failed: {ex.Message}");

                        try
                        {
                            if (testPort != null && testPort.IsOpen)
                            {
                                testPort.Close();
                            }
                            testPort?.Dispose();
                            testPort = null;
                        }
                        catch { }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] {portName} robot controller test failed: {ex.Message}");
                return false;
            }
            finally
            {
                try
                {
                    if (testPort != null && testPort.IsOpen)
                    {
                        testPort.Close();
                    }
                    testPort?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to release test port: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 로봇 컨트롤러 응답 검증
        /// </summary>
        /// <param name="response">응답 데이터</param>
        /// <returns>유효한 로봇 컨트롤러 응답 여부</returns>
        private static bool ValidateRobotControllerResponse(byte[] response)
        {
            try
            {
                if (response.Length < 3)
                    return false;

                // Modbus RTU 응답 패턴 확인
                if (response[0] == 0x01 && response[1] == 0x03)
                {
                    return true;
                }

                // 기타 로봇 컨트롤러 응답 패턴
                if (response.Length >= 4 && response[0] == 0xAA)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Robot controller response validation failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Complete Hardware Detection

        /// <summary>
        /// 전체 하드웨어 자동 감지
        /// </summary>
        /// <returns>감지된 하드웨어 정보</returns>
        public static async Task<HardwareDetectionResult> DetectAllHardwareAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting full hardware auto-detection...");

                var result = new HardwareDetectionResult();

                // DTP-7H 감지
                var dtp7hPort = await DetectDTP7HAsync();
                if (dtp7hPort != null)
                {
                    result.DTP7HPort = dtp7hPort;
                    result.HasDTP7H = true;
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] DTP-7H detected: {dtp7hPort.PortName}");
                }

                // 로봇 컨트롤러 감지
                var robotPort = await DetectRobotControllerAsync();
                if (robotPort != null)
                {
                    result.RobotControllerPort = robotPort;
                    result.HasRobotController = true;
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Robot controller detected: {robotPort.PortName}");
                }

                // 전체 포트 정보
                result.AllPorts = GetAvailableComPorts();
                result.DetectionTime = DateTime.Now;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Hardware detection complete - DTP-7H: {result.HasDTP7H}, Robot: {result.HasRobotController}");

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Full hardware detection failed: {ex.Message}");
                return new HardwareDetectionResult { DetectionTime = DateTime.Now };
            }
        }

        #endregion

        #region Hardware Monitoring

        /// <summary>
        /// 하드웨어 연결 상태 모니터링 시작
        /// </summary>
        public static void StartHardwareMonitoring()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Starting hardware monitoring");

                // WMI 이벤트 모니터링으로 USB 디바이스 변경 감지
                var insertQuery = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2");
                var removeQuery = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 3");

                // 실제 구현에서는 ManagementEventWatcher를 사용하여 USB 연결/해제 이벤트 모니터링
                // 현재는 간단한 폴링 방식으로 구현

                Task.Run(async () => await MonitorHardwareChangesAsync());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Failed to start hardware monitoring: {ex.Message}");
            }
        }

        /// <summary>
        /// 하드웨어 변경 모니터링 (폴링 방식)
        /// </summary>
        private static async Task MonitorHardwareChangesAsync()
        {
            var lastPorts = new List<string>();

            while (true)
            {
                try
                {
                    var currentPorts = SerialPort.GetPortNames().ToList();

                    // 새로 연결된 포트 감지
                    var newPorts = currentPorts.Except(lastPorts).ToList();
                    foreach (var newPort in newPorts)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] New COM port detected: {newPort}");

                        // 새 포트에 대해 하드웨어 타입 확인
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(1000); // 포트 안정화 대기

                            if (await TestDTP7HCommunicationAsync(newPort))
                            {
                                var portInfo = GetComPortInfo(newPort);
                                if (portInfo != null)
                                {
                                    portInfo.DeviceType = HardwareDeviceType.DTP7H;
                                    HardwareDetected?.Invoke(null, new HardwareDetectedEventArgs
                                    {
                                        DeviceType = HardwareDeviceType.DTP7H,
                                        PortInfo = portInfo,
                                        DetectedAt = DateTime.Now
                                    });
                                }
                            }
                        });
                    }

                    // 연결 해제된 포트 감지
                    var removedPorts = lastPorts.Except(currentPorts).ToList();
                    foreach (var removedPort in removedPorts)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] COM port disconnected: {removedPort}");

                        HardwareDisconnected?.Invoke(null, new HardwareDisconnectedEventArgs
                        {
                            PortName = removedPort,
                            DisconnectedAt = DateTime.Now
                        });
                    }

                    lastPorts = currentPorts;

                    // 5초마다 확인
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Error during hardware monitoring: {ex.Message}");
                    await Task.Delay(10000); // 오류 시 10초 대기
                }
            }
        }

        #endregion
    }

    #region Data Classes and Enums

    /// <summary>
    /// COM 포트 정보
    /// </summary>
    public class ComPortInfo
    {
        public string PortName { get; set; }
        public string Description { get; set; }
        public string DeviceID { get; set; }
        public string Manufacturer { get; set; }
        public string Service { get; set; }
        public bool IsAvailable { get; set; }
        public HardwareDeviceType DeviceType { get; set; } = HardwareDeviceType.Unknown;

        public override string ToString()
        {
            return $"{PortName} - {Description} ({DeviceType})";
        }
    }

    /// <summary>
    /// 하드웨어 디바이스 타입
    /// </summary>
    public enum HardwareDeviceType
    {
        Unknown = 0,
        DTP7H = 1,
        RobotController = 2,
        GenericSerial = 3
    }

    /// <summary>
    /// 하드웨어 감지 결과
    /// </summary>
    public class HardwareDetectionResult
    {
        public ComPortInfo DTP7HPort { get; set; }
        public ComPortInfo RobotControllerPort { get; set; }
        public List<ComPortInfo> AllPorts { get; set; } = new List<ComPortInfo>();
        public bool HasDTP7H { get; set; }
        public bool HasRobotController { get; set; }
        public DateTime DetectionTime { get; set; }

        public bool HasAnyHardware => HasDTP7H || HasRobotController;
    }

    /// <summary>
    /// 하드웨어 감지 이벤트 인수
    /// </summary>
    public class HardwareDetectedEventArgs : EventArgs
    {
        public HardwareDeviceType DeviceType { get; set; }
        public ComPortInfo PortInfo { get; set; }
        public DateTime DetectedAt { get; set; }
    }

    /// <summary>
    /// 하드웨어 연결 해제 이벤트 인수
    /// </summary>
    public class HardwareDisconnectedEventArgs : EventArgs
    {
        public string PortName { get; set; }
        public DateTime DisconnectedAt { get; set; }
    }

    #endregion
}