using System;
using System.Threading;

namespace TeachingPendant.HardwareControllers
{
    /// <summary>
    /// DTP7HCommunication 확장 클래스
    /// 기존 SendLEDCommand, SendBuzzerCommand를 활용한 로봇 상태 표시
    /// </summary>
    public static class DTP7HRobotExtension
    {
        #region Robot Status Display Extension Methods

        /// <summary>
        /// 연결 상태 표시 (기존 LED 제어 활용)
        /// </summary>
        /// <param name="dtp7h">DTP7H 통신 객체</param>
        /// <param name="isConnected">연결 상태</param>
        /// <returns>표시 성공 여부</returns>
        public static bool ShowConnectionStatus(this DTP7HCommunication dtp7h, bool isConnected)
        {
            try
            {
                if (!dtp7h.IsConnected)
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] Showing connection status: {(isConnected ? "Connected" : "Disconnected")}");

                if (isConnected)
                {
                    // 연결 성공 표시 (좌측 LED1 파란색)
                    dtp7h.SendLEDCommand(LEDPosition.LeftLED1, LEDColor.Blue);

                    // 연결 확인 부저
                    dtp7h.SendBuzzerCommand(true);
                    Thread.Sleep(100);
                    dtp7h.SendBuzzerCommand(false);
                }
                else
                {
                    // 연결 해제 표시 (모든 LED OFF)
                    dtp7h.SendLEDCommand(LEDPosition.LeftLED1, LEDColor.Off);
                    dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Off);
                    dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Off);
                    dtp7h.SendLEDCommand(LEDPosition.RightLED1, LEDColor.Off);
                    dtp7h.SendLEDCommand(LEDPosition.RightLED2, LEDColor.Off);
                    dtp7h.SendLEDCommand(LEDPosition.RightLED3, LEDColor.Off);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] Failed to show connection status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 로봇 이동 표시 (기존 LED 제어 활용)
        /// </summary>
        /// <param name="dtp7h">DTP7H 통신 객체</param>
        /// <param name="r">반지름 좌표</param>
        /// <param name="theta">각도 좌표</param>
        /// <param name="z">높이 좌표</param>
        /// <returns>이동 표시 성공 여부</returns>
        public static bool ShowRobotMoveCommand(this DTP7HCommunication dtp7h, double r, double theta, double z)
        {
            try
            {
                if (!dtp7h.IsConnected)
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] Showing robot move: R={r}, Theta={theta}, Z={z}");

                // 이동 시작 표시
                dtp7h.SendLEDCommand(LEDPosition.RightLED1, LEDColor.Blue);
                Thread.Sleep(100);

                // 이동 진행 표시
                dtp7h.SendLEDCommand(LEDPosition.RightLED2, LEDColor.Blue);
                Thread.Sleep(100);

                // 이동 완료 표시
                dtp7h.SendLEDCommand(LEDPosition.RightLED3, LEDColor.Blue);
                Thread.Sleep(100);

                // 모든 LED OFF
                dtp7h.SendLEDCommand(LEDPosition.RightLED1, LEDColor.Off);
                dtp7h.SendLEDCommand(LEDPosition.RightLED2, LEDColor.Off);
                dtp7h.SendLEDCommand(LEDPosition.RightLED3, LEDColor.Off);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] Failed to show robot move: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 로봇 홈 이동 표시 (기존 LED 제어 활용)
        /// </summary>
        /// <param name="dtp7h">DTP7H 통신 객체</param>
        /// <returns>홈 이동 표시 성공 여부</returns>
        public static bool ShowRobotHomeCommand(this DTP7HCommunication dtp7h)
        {
            try
            {
                if (!dtp7h.IsConnected)
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("[DTP7HRobotExtension] Showing robot home command");

                // 홈 이동 시작 표시 (좌측 LED 순차 점등)
                dtp7h.SendLEDCommand(LEDPosition.LeftLED1, LEDColor.Red);
                Thread.Sleep(200);
                dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Red);
                Thread.Sleep(200);
                dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Red);
                Thread.Sleep(200);

                // 홈 완료 표시 (모든 LED 파란색)
                dtp7h.SendLEDCommand(LEDPosition.LeftLED1, LEDColor.Blue);
                dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Blue);
                dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Blue);
                Thread.Sleep(500);

                // LED OFF
                dtp7h.SendLEDCommand(LEDPosition.LeftLED1, LEDColor.Off);
                dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Off);
                dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Off);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] Failed to show robot home command: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 로봇 정지 표시 (기존 LED, Buzzer 제어 활용)
        /// </summary>
        /// <param name="dtp7h">DTP7H 통신 객체</param>
        /// <returns>정지 표시 성공 여부</returns>
        public static bool ShowRobotStopCommand(this DTP7HCommunication dtp7h)
        {
            try
            {
                if (!dtp7h.IsConnected)
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("[DTP7HRobotExtension] Showing robot stop command");

                // 정지 표시 (모든 LED 빨간색 점멸)
                for (int i = 0; i < 3; i++)
                {
                    dtp7h.SendLEDCommand(LEDPosition.LeftLED1, LEDColor.Red);
                    dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Red);
                    dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Red);
                    dtp7h.SendLEDCommand(LEDPosition.RightLED1, LEDColor.Red);
                    dtp7h.SendLEDCommand(LEDPosition.RightLED2, LEDColor.Red);
                    dtp7h.SendLEDCommand(LEDPosition.RightLED3, LEDColor.Red);

                    // 부저도 함께 사용
                    dtp7h.SendBuzzerCommand(true);
                    Thread.Sleep(200);

                    dtp7h.SendLEDCommand(LEDPosition.LeftLED1, LEDColor.Off);
                    dtp7h.SendLEDCommand(LEDPosition.LeftLED2, LEDColor.Off);
                    dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Off);
                    dtp7h.SendLEDCommand(LEDPosition.RightLED1, LEDColor.Off);
                    dtp7h.SendLEDCommand(LEDPosition.RightLED2, LEDColor.Off);
                    dtp7h.SendLEDCommand(LEDPosition.RightLED3, LEDColor.Off);

                    dtp7h.SendBuzzerCommand(false);
                    Thread.Sleep(200);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] Failed to show robot stop command: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 진공 ON/OFF 표시 (기존 LED 제어 활용)
        /// </summary>
        /// <param name="dtp7h">DTP7H 통신 객체</param>
        /// <param name="isOn">진공 ON/OFF</param>
        /// <returns>진공 표시 성공 여부</returns>
        public static bool ShowVacuumCommand(this DTP7HCommunication dtp7h, bool isOn)
        {
            try
            {
                if (!dtp7h.IsConnected)
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] Showing vacuum {(isOn ? "ON" : "OFF")}");

                if (isOn)
                {
                    // 진공 ON 표시 (좌측 LED3 파란색)
                    dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Blue);
                }
                else
                {
                    // 진공 OFF 표시 (좌측 LED3 OFF)
                    dtp7h.SendLEDCommand(LEDPosition.LeftLED3, LEDColor.Off);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] Failed to show vacuum status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pick 동작 표시 (기존 LED 제어 활용)
        /// </summary>
        /// <param name="dtp7h">DTP7H 통신 객체</param>
        /// <returns>Pick 표시 성공 여부</returns>
        public static bool ShowPickOperation(this DTP7HCommunication dtp7h)
        {
            try
            {
                if (!dtp7h.IsConnected)
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("[DTP7HRobotExtension] Showing Pick operation");

                // Pick 시작 표시
                dtp7h.SendLEDCommand(LEDPosition.RightLED2, LEDColor.Blue);
                Thread.Sleep(200);

                // 진공 ON 표시
                dtp7h.ShowVacuumCommand(true);
                Thread.Sleep(300);

                // Pick 완료 표시
                dtp7h.SendLEDCommand(LEDPosition.RightLED2, LEDColor.Off);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] Failed to show Pick operation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Place 동작 표시 (기존 LED 제어 활용)
        /// </summary>
        /// <param name="dtp7h">DTP7H 통신 객체</param>
        /// <returns>Place 표시 성공 여부</returns>
        public static bool ShowPlaceOperation(this DTP7HCommunication dtp7h)
        {
            try
            {
                if (!dtp7h.IsConnected)
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("[DTP7HRobotExtension] Showing Place operation");

                // Place 시작 표시
                dtp7h.SendLEDCommand(LEDPosition.RightLED3, LEDColor.Blue);
                Thread.Sleep(200);

                // 진공 OFF 표시
                dtp7h.ShowVacuumCommand(false);
                Thread.Sleep(300);

                // Place 완료 표시
                dtp7h.SendLEDCommand(LEDPosition.RightLED3, LEDColor.Off);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] Failed to show Place operation: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// LED 패턴 테스트 (기존 LED 제어 활용)
        /// </summary>
        /// <param name="dtp7h">DTP7H 통신 객체</param>
        /// <returns>테스트 성공 여부</returns>
        public static bool TestLEDPattern(this DTP7HCommunication dtp7h)
        {
            try
            {
                if (!dtp7h.IsConnected)
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("[DTP7HRobotExtension] Starting LED pattern test");

                // 순차적 LED 테스트
                LEDPosition[] positions = {
                    LEDPosition.LeftLED1, LEDPosition.LeftLED2, LEDPosition.LeftLED3,
                    LEDPosition.RightLED1, LEDPosition.RightLED2, LEDPosition.RightLED3
                };

                LEDColor[] colors = { LEDColor.Blue, LEDColor.Red, LEDColor.All };

                foreach (LEDColor color in colors)
                {
                    foreach (LEDPosition position in positions)
                    {
                        dtp7h.SendLEDCommand(position, color);
                        Thread.Sleep(100);
                        dtp7h.SendLEDCommand(position, LEDColor.Off);
                    }
                }

                System.Diagnostics.Debug.WriteLine("[DTP7HRobotExtension] LED pattern test completed");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] LED pattern test failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 부저 테스트 (기존 Buzzer 제어 활용)
        /// </summary>
        /// <param name="dtp7h">DTP7H 통신 객체</param>
        /// <param name="durationMs">부저 지속 시간 (밀리초)</param>
        /// <returns>테스트 성공 여부</returns>
        public static bool TestBuzzer(this DTP7HCommunication dtp7h, int durationMs = 500)
        {
            try
            {
                if (!dtp7h.IsConnected)
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] Testing buzzer: {durationMs}ms");

                dtp7h.SendBuzzerCommand(true);
                Thread.Sleep(durationMs);
                dtp7h.SendBuzzerCommand(false);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] Buzzer test failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 로봇 상태 표시 조합 (기존 LED 제어 활용)
        /// </summary>
        /// <param name="dtp7h">DTP7H 통신 객체</param>
        /// <param name="isConnected">연결 상태</param>
        /// <param name="isMoving">이동 중 여부</param>
        /// <param name="vacuumOn">진공 상태</param>
        /// <returns>상태 표시 성공 여부</returns>
        public static bool ShowRobotStatus(this DTP7HCommunication dtp7h, bool isConnected, bool isMoving, bool vacuumOn)
        {
            try
            {
                if (!dtp7h.IsConnected)
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] Showing robot status: Connected={isConnected}, Moving={isMoving}, Vacuum={vacuumOn}");

                // 연결 상태 표시
                if (isConnected)
                {
                    dtp7h.SendLEDCommand(LEDPosition.LeftLED1, LEDColor.Blue);
                }
                else
                {
                    dtp7h.SendLEDCommand(LEDPosition.LeftLED1, LEDColor.Off);
                }

                // 이동 상태 표시
                if (isMoving)
                {
                    dtp7h.SendLEDCommand(LEDPosition.RightLED1, LEDColor.Blue);
                }
                else
                {
                    dtp7h.SendLEDCommand(LEDPosition.RightLED1, LEDColor.Off);
                }

                // 진공 상태 표시
                dtp7h.ShowVacuumCommand(vacuumOn);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DTP7HRobotExtension] Failed to show robot status: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}