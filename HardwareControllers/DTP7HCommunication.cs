using System;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Threading;

namespace TeachingPendant.HardwareControllers
{
    /// <summary>
    /// DTP-7H 하드웨어 LED/부저 제어 클래스
    /// 시리얼 통신 방식과 키보드 이벤트 방식 모두 지원
    /// </summary>
    public class DTP7HCommunication : IDisposable
    {
        #region Fields

        private SerialPort _serialPort;
        private bool _isConnected;

        #endregion

        #region Properties

        /// <summary>
        /// 연결 상태
        /// </summary>
        public bool IsConnected => _isConnected && _serialPort?.IsOpen == true;

        #endregion

        #region Constants - DTP-7H API 매뉴얼 기반

        // 시리얼 통신 상수
        private const byte STX = 0x02;
        private const byte ETX = 0x03;
        private const byte MOD_SET = 0x11;
        private const byte SEL_LED = 0x3A;
        private const byte SEL_BUZZ = 0x3B;
        private const byte DATA_RESERVED = 0x20;
        private const int PACKET_SIZE = 9;

        // LED 색상 (DATA2)
        private const byte LED_OFF = 0x30;
        private const byte LED_BLUE = 0x31;
        private const byte LED_RED = 0x32;
        private const byte LED_ALL = 0x33;

        #endregion

        #region Windows API for Keyboard Events (시리얼 데몬 없이 사용)

        /// <summary>
        /// Windows API keybd_event 함수
        /// </summary>
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYUP = 0x0002;    // 키 릴리즈

        // DTP7H 키보드 이벤트 매핑 상수 (API 매뉴얼 DTP7H 모델 기준)
        // Left LED - 모든 색상 지원
        private const byte LEFT_LED1_BLUE_KEY = 0xC1;
        private const byte LEFT_LED1_RED_KEY = 0xC2;
        private const byte LEFT_LED1_ALL_KEY = 0xC3;

        private const byte LEFT_LED2_BLUE_KEY = 0xC4;
        private const byte LEFT_LED2_RED_KEY = 0xC5;
        private const byte LEFT_LED2_ALL_KEY = 0xC6;

        private const byte LEFT_LED3_BLUE_KEY = 0xC7;
        private const byte LEFT_LED3_RED_KEY = 0xC8;
        private const byte LEFT_LED3_ALL_KEY = 0xC9;

        // Right LED - 완전히 확인됨
        private const byte RIGHT_LED1_BLUE_KEY = 0xCA;
        private const byte RIGHT_LED1_RED_KEY = 0xCB;
        private const byte RIGHT_LED1_ALL_KEY = 0xCC;

        private const byte RIGHT_LED2_BLUE_KEY = 0xCD;
        private const byte RIGHT_LED2_RED_KEY = 0xCE;
        private const byte RIGHT_LED2_ALL_KEY = 0xCF;

        private const byte RIGHT_LED3_BLUE_KEY = 0xD0;
        private const byte RIGHT_LED3_RED_KEY = 0xD1;
        private const byte RIGHT_LED3_ALL_KEY = 0xD2;

        // 부저
        private const byte BUZZER_KEY = 0xD3;

        #endregion

        #region Constructor

        /// <summary>
        /// DTP7HCommunication 생성자
        /// </summary>
        public DTP7HCommunication()
        {
            _isConnected = false;
        }

        #endregion

        #region Connection Management

        /// <summary>
        /// 시리얼 포트 연결
        /// </summary>
        /// <param name="portName">포트 이름 (예: "COM5")</param>
        /// <param name="baudRate">보드레이트 (기본값: 115200)</param>
        /// <returns>연결 성공 여부</returns>
        public bool Connect(string portName, int baudRate = 115200)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }

                _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                _serialPort.Open();

                _isConnected = _serialPort.IsOpen;

                System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] Connection successful: {portName} @ {baudRate}");
                return _isConnected;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] Connection failed: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// 시리얼 포트 연결 해제
        /// </summary>
        public void Disconnect()
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                _isConnected = false;

                System.Diagnostics.Debug.WriteLine("[DTP7HCommunication] Disconnected");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] Disconnection error: {ex.Message}");
            }
        }

        #endregion

        #region 시리얼 통신 방식 (시리얼 데몬 사용)

        /// <summary>
        /// LED 제어 명령 전송 (시리얼 통신 방식)
        /// </summary>
        /// <param name="position">LED 위치</param>
        /// <param name="color">LED 색상</param>
        /// <returns>전송 성공 여부</returns>
        public bool SendLEDCommand(LEDPosition position, LEDColor color)
        {
            if (!IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("[DTP7HCommunication] Not connected");
                return false;
            }

            try
            {
                byte[] packet = new byte[PACKET_SIZE];
                packet[0] = STX;
                packet[1] = MOD_SET;
                packet[2] = SEL_LED;
                packet[3] = GetLEDPosition(position);
                packet[4] = GetLEDColorValue(color);
                packet[5] = DATA_RESERVED;

                ushort crc = CalculateCRC16(packet, 6);
                packet[6] = (byte)((crc >> 8) & 0xFF);
                packet[7] = (byte)(crc & 0xFF);
                packet[8] = ETX;

                _serialPort.Write(packet, 0, packet.Length);

                System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] LED packet sent: {position} {color}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] LED control failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 부저 제어 명령 전송 (시리얼 통신 방식)
        /// </summary>
        /// <param name="isOn">부저 켜기/끄기</param>
        /// <returns>전송 성공 여부</returns>
        public bool SendBuzzerCommand(bool isOn)
        {
            if (!IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("[DTP7HCommunication] Not connected");
                return false;
            }

            try
            {
                byte[] packet = new byte[PACKET_SIZE];
                packet[0] = STX;
                packet[1] = MOD_SET;
                packet[2] = SEL_BUZZ;
                packet[3] = 0x64; // 부저 위치 (고정값)
                packet[4] = (byte)(isOn ? 0x31 : 0x30);
                packet[5] = DATA_RESERVED;

                ushort crc = CalculateCRC16(packet, 6);
                packet[6] = (byte)((crc >> 8) & 0xFF);
                packet[7] = (byte)(crc & 0xFF);
                packet[8] = ETX;

                _serialPort.Write(packet, 0, packet.Length);

                System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] Buzzer packet sent: {(isOn ? "ON" : "OFF")}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] Buzzer control failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 키보드 이벤트 방식 (시리얼 데몬 없이 사용)

        /// <summary>
        /// 시리얼 데몬 없이 LED 제어 (키보드 이벤트 방식)
        /// </summary>
        /// <param name="position">LED 위치</param>
        /// <param name="color">LED 색상</param>
        /// <param name="durationMs">켜진 상태 유지 시간 (밀리초)</param>
        /// <returns>제어 성공 여부</returns>
        public bool SendLEDCommandDirect(LEDPosition position, LEDColor color, int durationMs = 1000)
        {
            try
            {
                byte keyCode = GetLEDKeyCode(position, color);
                if (keyCode == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] Keyboard event LED control failed: Unsupported combination {position} {color}");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] Keyboard event LED control: {position} {color} (0x{keyCode:X2})");

                // LED ON
                keybd_event(keyCode, 0, 0, UIntPtr.Zero);
                Thread.Sleep(10); // API 매뉴얼 권장 딜레이

                if (durationMs > 0)
                {
                    Thread.Sleep(durationMs);

                    // LED OFF
                    keybd_event(keyCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] Keyboard event LED control failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 시리얼 데몬 없이 부저 제어 (키보드 이벤트 방식)
        /// </summary>
        /// <param name="durationMs">부저 지속 시간 (밀리초)</param>
        /// <returns>제어 성공 여부</returns>
        public bool SendBuzzerCommandDirect(int durationMs = 500)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] Keyboard event buzzer control: {durationMs}ms");

                // 부저 ON
                keybd_event(BUZZER_KEY, 0, 0, UIntPtr.Zero);
                Thread.Sleep(10);

                Thread.Sleep(durationMs);

                // 부저 OFF
                keybd_event(BUZZER_KEY, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] Keyboard event buzzer control failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 지원되는 LED만 순차 테스트 (시리얼 데몬 없이)
        /// </summary>
        public bool TestAllLEDsDirect()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[DTP7HCommunication] Starting direct test of supported LEDs");

                // 모든 LED 위치
                LEDPosition[] allPositions =
                {
                    LEDPosition.LeftLED1, LEDPosition.LeftLED2, LEDPosition.LeftLED3,
                    LEDPosition.RightLED1, LEDPosition.RightLED2, LEDPosition.RightLED3
                };

                LEDColor[] colors = { LEDColor.Blue, LEDColor.Red, LEDColor.All };

                foreach (LEDPosition position in allPositions)
                {
                    foreach (LEDColor color in colors)
                    {
                        SendLEDCommandDirect(position, color, 300);
                        Thread.Sleep(100);
                    }
                }

                System.Diagnostics.Debug.WriteLine("[DTP7HCommunication] Direct test of supported LEDs completed");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] Direct LED test failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 간단한 Right LED1 테스트
        /// </summary>
        public bool TestRightLED1Direct()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[DTP7HCommunication] Testing Right LED1");

                // Blue
                SendLEDCommandDirect(LEDPosition.RightLED1, LEDColor.Blue, 500);
                Thread.Sleep(200);

                // Red  
                SendLEDCommandDirect(LEDPosition.RightLED1, LEDColor.Red, 500);
                Thread.Sleep(200);

                // All
                SendLEDCommandDirect(LEDPosition.RightLED1, LEDColor.All, 500);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] Right LED1 test failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// LED 위치 값 매핑 (시리얼 통신용)
        /// </summary>
        private byte GetLEDPosition(LEDPosition position)
        {
            switch (position)
            {
                case LEDPosition.LeftLED1: return 0xC1;
                case LEDPosition.LeftLED2: return 0xC4;
                case LEDPosition.LeftLED3: return 0xC7;
                case LEDPosition.RightLED1: return 0xCA;
                case LEDPosition.RightLED2: return 0xCD;
                case LEDPosition.RightLED3: return 0xD0;
                default: return 0xC1;
            }
        }

        /// <summary>
        /// LED 색상 값 매핑 (시리얼 통신용)
        /// </summary>
        private byte GetLEDColorValue(LEDColor color)
        {
            switch (color)
            {
                case LEDColor.Off: return 0x30;
                case LEDColor.Blue: return 0x31;
                case LEDColor.Red: return 0x32;
                case LEDColor.All: return 0x33;
                default: return 0x30;
            }
        }

        /// <summary>
        /// LED 위치와 색상에 따른 키코드 반환 (키보드 이벤트용)
        /// API 매뉴얼 DTP7H 모델 기준으로 모든 LED 지원
        /// </summary>
        private byte GetLEDKeyCode(LEDPosition position, LEDColor color)
        {
            switch (position)
            {
                case LEDPosition.LeftLED1:
                    switch (color)
                    {
                        case LEDColor.Blue: return LEFT_LED1_BLUE_KEY;
                        case LEDColor.Red: return LEFT_LED1_RED_KEY;
                        case LEDColor.All: return LEFT_LED1_ALL_KEY;
                        default: return 0;
                    }

                case LEDPosition.LeftLED2:
                    switch (color)
                    {
                        case LEDColor.Blue: return LEFT_LED2_BLUE_KEY;
                        case LEDColor.Red: return LEFT_LED2_RED_KEY;
                        case LEDColor.All: return LEFT_LED2_ALL_KEY;
                        default: return 0;
                    }

                case LEDPosition.LeftLED3:
                    switch (color)
                    {
                        case LEDColor.Blue: return LEFT_LED3_BLUE_KEY;
                        case LEDColor.Red: return LEFT_LED3_RED_KEY;
                        case LEDColor.All: return LEFT_LED3_ALL_KEY;
                        default: return 0;
                    }

                case LEDPosition.RightLED1:
                    switch (color)
                    {
                        case LEDColor.Blue: return RIGHT_LED1_BLUE_KEY;
                        case LEDColor.Red: return RIGHT_LED1_RED_KEY;
                        case LEDColor.All: return RIGHT_LED1_ALL_KEY;
                        default: return 0;
                    }

                case LEDPosition.RightLED2:
                    switch (color)
                    {
                        case LEDColor.Blue: return RIGHT_LED2_BLUE_KEY;
                        case LEDColor.Red: return RIGHT_LED2_RED_KEY;
                        case LEDColor.All: return RIGHT_LED2_ALL_KEY;
                        default: return 0;
                    }

                case LEDPosition.RightLED3:
                    switch (color)
                    {
                        case LEDColor.Blue: return RIGHT_LED3_BLUE_KEY;
                        case LEDColor.Red: return RIGHT_LED3_RED_KEY;
                        case LEDColor.All: return RIGHT_LED3_ALL_KEY;
                        default: return 0;
                    }

                default:
                    return 0;
            }
        }

        /// <summary>
        /// CRC16 체크섬 계산
        /// </summary>
        private ushort CalculateCRC16(byte[] data, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc = (ushort)((crc >> 1) ^ 0xA001);
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
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
                Disconnect();

                if (_serialPort != null)
                {
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HCommunication] Dispose error: {ex.Message}");
            }
        }

        #endregion
    }

    #region Enums

    /// <summary>
    /// LED 위치 열거형
    /// </summary>
    public enum LEDPosition
    {
        LeftLED1,
        LeftLED2,
        LeftLED3,
        RightLED1,
        RightLED2,
        RightLED3
    }

    /// <summary>
    /// LED 색상 열거형
    /// </summary>
    public enum LEDColor
    {
        Off,
        Blue,
        Red,
        All
    }

    #endregion
}