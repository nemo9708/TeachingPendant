using System;

namespace TeachingPendant.Manager
{
    /// <summary>
    /// 티칭펜던트의 운영 모드를 정의합니다.
    /// </summary>
    public enum GlobalMode
    {
        Manual,
        Auto,
        Emergency
    }

    /// <summary>
    /// 티칭펜던트의 운영 모드를 관리하는 글로벌 매니저 (Enum 기반으로 리팩토링됨)
    /// </summary>
    public static class GlobalModeManager
    {
        #region Private Fields
        private static GlobalMode _currentMode = GlobalMode.Manual; // 기본값: Manual 모드
        #endregion

        #region Events
        /// <summary>
        /// 모드가 변경될 때 발생하는 이벤트
        /// </summary>
        public static event EventHandler<ModeChangedEventArgs> ModeChanged;
        #endregion

        #region Properties
        /// <summary>
        /// 현재 설정된 운영 모드
        /// </summary>
        public static GlobalMode CurrentMode
        {
            get { return _currentMode; }
        }

        /// <summary>
        /// 현재 Manual 모드인지 여부
        /// </summary>
        public static bool IsManualMode
        {
            get { return _currentMode == GlobalMode.Manual; }
        }

        /// <summary>
        /// 현재 Auto 모드인지 여부
        /// </summary>
        public static bool IsAutoMode
        {
            get { return _currentMode == GlobalMode.Auto; }
        }

        /// <summary>
        /// 편집이 허용되는지 여부 (Manual 모드일 때만 편집 가능)
        /// </summary>
        public static bool IsEditingAllowed
        {
            get { return _currentMode == GlobalMode.Manual; }
        }

        /// <summary>
        /// 현재 모드 이름 반환
        /// </summary>
        public static string CurrentModeName
        {
            get { return _currentMode.ToString(); }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 운영 모드를 설정합니다.
        /// </summary>
        /// <param name="newMode">설정할 새로운 모드</param>
        public static void SetMode(GlobalMode newMode)
        {
            if (_currentMode != newMode)
            {
                GlobalMode oldMode = _currentMode;
                _currentMode = newMode;
                System.Diagnostics.Debug.WriteLine($"GlobalModeManager: Switched from {oldMode} to {newMode}");

                // I/O 자동 제어
                UpdateIOForModeChange(newMode);

                try
                {
                    ModeChanged?.Invoke(null, new ModeChangedEventArgs(newMode, oldMode));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GlobalModeManager: Error firing ModeChanged event: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 모드 변경에 따른 I/O 자동 제어
        /// </summary>
        private static void UpdateIOForModeChange(GlobalMode newMode)
        {
            switch (newMode)
            {
                case GlobalMode.Manual:
                    IOController.SetOutput("Green Light", true);   // DO06 ON
                    IOController.SetOutput("Red Light", false);    // DO07 OFF
                    IOController.SetOutput("Buzzer", false);       // DO08 OFF
                    IOController.SetOutput("Robot Enable", true);  // DO01 ON
                    break;

                case GlobalMode.Auto:
                    IOController.SetOutput("Green Light", true);   // DO06 ON (Auto 준비)
                    IOController.SetOutput("Red Light", false);    // DO07 OFF
                    IOController.SetOutput("Robot Enable", true);  // DO01 ON
                    break;

                case GlobalMode.Emergency:
                    IOController.SetOutput("Green Light", false);  // DO06 OFF
                    IOController.SetOutput("Red Light", true);     // DO07 ON
                    IOController.SetOutput("Buzzer", true);        // DO08 ON
                    IOController.SetOutput("Robot Enable", false); // DO01 OFF
                    IOController.SetOutput("Vacuum OFF", true);    // DO03 ON (비상시 진공 해제)
                    break;
            }
        }

        /// <summary>
        /// 현재 모드에 대한 설명 메시지를 반환합니다.
        /// </summary>
        public static string GetModeMessage(GlobalMode mode)
        {
            switch (mode)
            {
                case GlobalMode.Manual:
                    return "Mode set to MANUAL";
                case GlobalMode.Auto:
                    return "Mode set to AUTO";
                case GlobalMode.Emergency:
                    return "EMERGENCY mode activated";
                default:
                    return "Unknown mode";
            }
        }
        #endregion
    }
}