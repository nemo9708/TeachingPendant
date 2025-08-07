using System;

namespace TeachingPendant.Manager
{
    /// <summary>
    /// 전체 애플리케이션의 속도 설정을 관리하는 글로벌 매니저
    /// </summary>
    public static class GlobalSpeedManager
    {
        #region Fields
        private static int _currentSpeed = 100; // 기본 속도 100%
        private const double CONVERSION_FACTOR = 1.4; // 펜던트 속도를 실제 명령 속도로 변환하는 계수
        #endregion

        #region Events
        /// <summary>
        /// 속도가 변경될 때 발생하는 이벤트
        /// </summary>
        public static event EventHandler<int> SpeedChanged;
        #endregion

        #region Properties
        /// <summary>
        /// 현재 설정된 속도 (1-200%)
        /// </summary>
        public static int CurrentSpeed
        {
            get { return _currentSpeed; }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 글로벌 속도 설정
        /// </summary>
        /// <param name="speed">설정할 속도 (1-200%)</param>
        public static void SetSpeed(int speed)
        {
            // 속도 범위 제한
            int validSpeed = Math.Max(1, Math.Min(200, speed));

            if (_currentSpeed != validSpeed)
            {
                int previousSpeed = _currentSpeed;
                _currentSpeed = validSpeed;

                System.Diagnostics.Debug.WriteLine($"GlobalSpeedManager: Speed changed from {previousSpeed}% to {_currentSpeed}%");

                // 이벤트 발생
                SpeedChanged?.Invoke(null, _currentSpeed);
            }
        }

        /// <summary>
        /// 펜던트 속도 설정을 실제 명령 속도로 변환
        /// </summary>
        /// <param name="theoreticalSpeed">이론적 계산된 속도</param>
        /// <returns>최종 명령 속도</returns>
        public static int ApplyPendantSpeedSetting(int theoreticalSpeed)
        {
            if (theoreticalSpeed <= 0)
            {
                return 0;
            }

            // 1. 펜던트 속도 비율 적용 (예: 50%이면 0.5배)
            double speedWithPendantRatio = theoreticalSpeed * (_currentSpeed / 100.0);

            // 2. 변환 계수 적용
            double finalSpeed = speedWithPendantRatio * CONVERSION_FACTOR;

            // 3. 정수로 반올림
            int result = (int)Math.Round(finalSpeed);

            System.Diagnostics.Debug.WriteLine($"GlobalSpeedManager: Applied speed conversion - Theoretical: {theoreticalSpeed}, Pendant: {_currentSpeed}%, Final: {result}");

            return Math.Max(0, result);
        }

        /// <summary>
        /// 속도를 기본값(100%)으로 리셋
        /// </summary>
        public static void ResetToDefault()
        {
            SetSpeed(100);
        }

        /// <summary>
        /// 현재 속도 설정 정보를 디버그 출력
        /// </summary>
        public static void ShowDebugInfo()
        {
            System.Diagnostics.Debug.WriteLine($"=== GlobalSpeedManager Status ===");
            System.Diagnostics.Debug.WriteLine($"Current Speed: {_currentSpeed}%");
            System.Diagnostics.Debug.WriteLine($"Conversion Factor: {CONVERSION_FACTOR}");
            System.Diagnostics.Debug.WriteLine($"Speed Event Subscribers: {SpeedChanged?.GetInvocationList()?.Length ?? 0}");
        }
        #endregion
    }
}
