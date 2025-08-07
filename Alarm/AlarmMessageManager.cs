using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TeachingPendant.Alarm
{
    /// <summary>
    /// 알람 카테고리 정의
    /// </summary>
    public enum AlarmCategory
    {
        Information = 'A',  // 정보 메시지 (A001~A999)
        Warning = 'B',      // 경고 메시지 (B001~B999)
        Error = 'C',        // 오류 메시지 (C001~C999)
        Success = 'D',      // 성공 메시지 (D001~D999)
        System = 'E'        // 시스템 메시지 (E001~E999)
    }

    /// <summary>
    /// 알람 정의 상수 클래스
    /// </summary>
    public static class Alarms
    {
        // Information 알람 (A001~A999)
        public const string POSITION_LOADED = "A001";
        public const string USER_ACTION = "A002";
        public const string STATUS_UPDATE = "A003";

        // Warning 알람 (B001~B999)
        public const string INVALID_VALUE = "B001";
        public const string UNEXPECTED_STATE = "B002";
        public const string OPERATION_LIMIT = "B003";
        public const string WARNING = "B004";
        public const string ACCESS_DENIED = "B005";

        // Error 알람 (C001~C999)
        public const string SYSTEM_ERROR = "C001";
        public const string PARSING_ERROR = "C002";
        public const string UI_ERROR = "C003";
        public const string DATA_ERROR = "C004";

        // Success 알람 (D001~D999)
        public const string POSITION_SAVED = "D001";
        public const string OPERATION_COMPLETED = "D002";
        public const string SETUP_COMPLETED = "D003";

        // System 알람 (E001~E999)
        public const string SYSTEM_INITIALIZED = "E001";
        public const string MODE_CHANGED = "E002";
        public const string CONFIGURATION_CHANGED = "E003";

        /// <summary>
        /// 알람 ID에서 카테고리 추출
        /// </summary>
        public static AlarmCategory GetCategory(string alarmId)
        {
            if (string.IsNullOrEmpty(alarmId) || alarmId.Length < 1)
                return AlarmCategory.Information;

            return (AlarmCategory)alarmId[0];
        }
    }

    /// <summary>
    /// 메시지 관리 클래스 (수정본)
    /// </summary>
    public static class AlarmMessageManager
    {
        #region Private Fields
        private static TextBlock _alramTextBlock;
        private static DispatcherTimer _messageTimer;
        private const int MESSAGE_DISPLAY_SECONDS = 3;
        private const string DEFAULT_MESSAGE = "Alarm:";

        // 알람 메시지 사전
        private static readonly Dictionary<string, string> _alarmMessages = new Dictionary<string, string>
        {
            // Information 알람
            { Alarms.POSITION_LOADED, "Position loaded" },
            { Alarms.USER_ACTION, "User action performed" },
            { Alarms.STATUS_UPDATE, "Status updated" },
            
            // Warning 알람
            { Alarms.INVALID_VALUE, "Invalid value detected" },
            { Alarms.UNEXPECTED_STATE, "Unexpected state occurred" },
            { Alarms.OPERATION_LIMIT, "Operation limit reached" },
            { Alarms.WARNING, "Warning occurred" },
            
            // Error 알람
            { Alarms.SYSTEM_ERROR, "System error occurred" },
            { Alarms.PARSING_ERROR, "Parsing error" },
            { Alarms.UI_ERROR, "UI error" },
            { Alarms.DATA_ERROR, "Data error" },
            
            // Success 알람
            { Alarms.POSITION_SAVED, "Position saved successfully" },
            { Alarms.OPERATION_COMPLETED, "Operation completed" },
            { Alarms.SETUP_COMPLETED, "Setup completed" },
            
            // System 알람
            { Alarms.SYSTEM_INITIALIZED, "System initialized" },
            { Alarms.MODE_CHANGED, "Mode changed" },
            { Alarms.CONFIGURATION_CHANGED, "Configuration changed" }
        };
        #endregion

        #region Public Methods
        /// <summary>
        /// Alarm 텍스트블록 설정 (강화됨)
        /// </summary>
        public static void SetAlarmTextBlock(TextBlock textBlock)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== AlarmMessageManager: SetAlarmTextBlock Called ===");
                System.Diagnostics.Debug.WriteLine($"Is TextBlock null: {textBlock == null}");

                _alramTextBlock = textBlock;

                if (_alramTextBlock != null)
                {
                    // 기본 메시지 설정
                    _alramTextBlock.Text = DEFAULT_MESSAGE;
                    System.Diagnostics.Debug.WriteLine("AlarmTextBlock default message set successfully");

                    // 타이머 초기화
                    InitializeTimer();
                    System.Diagnostics.Debug.WriteLine("AlarmMessageManager timer initialized successfully");

                    // 즉시 테스트 메시지 표시
                    ShowAlarm(Alarms.SYSTEM_INITIALIZED, "AlarmMessageManager Ready");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: SetAlarmTextBlock - textBlock is null!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetAlarmTextBlock error: {ex.Message}");
            }
        }

        /// <summary>
        /// 알람 ID로 메시지 표시
        /// </summary>
        public static void ShowAlarm(string alarmId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== ShowAlarm called: {alarmId} ===");

                if (!ValidateComponents(alarmId))
                {
                    System.Diagnostics.Debug.WriteLine("ValidateComponents failed");
                    return;
                }

                var message = GetAlarmMessage(alarmId);
                var formattedMessage = FormatMessage(alarmId, message);

                DisplayMessage(formattedMessage);

                System.Diagnostics.Debug.WriteLine($"ShowAlarm complete: {formattedMessage}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowAlarm error: {ex.Message}");
            }
        }

        /// <summary>
        /// 추가 메시지와 함께 알람 표시
        /// </summary>
        public static void ShowAlarm(string alarmId, string additionalMessage)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== ShowAlarm (with additional message) called: {alarmId} - {additionalMessage} ===");

                if (!ValidateComponents(alarmId))
                {
                    System.Diagnostics.Debug.WriteLine("ValidateComponents failed");
                    return;
                }

                var baseMessage = GetAlarmMessage(alarmId);
                var combinedMessage = $"{baseMessage} - {additionalMessage}";
                var formattedMessage = FormatMessage(alarmId, combinedMessage);

                DisplayMessage(formattedMessage);

                System.Diagnostics.Debug.WriteLine($"ShowAlarm (with additional message) complete: {formattedMessage}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowAlarm (with additional message) error: {ex.Message}");
            }
        }

        /// <summary>
        /// 사용자 정의 메시지 표시
        /// </summary>
        public static void ShowCustomMessage(string message, AlarmCategory category = AlarmCategory.Information)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== ShowCustomMessage called: {message} ({category}) ===");

                if (!ValidateComponents("CUSTOM"))
                {
                    System.Diagnostics.Debug.WriteLine("ValidateComponents failed");
                    return;
                }

                var formattedMessage = $"Alarm: [{category}] {message}";
                DisplayMessage(formattedMessage);

                System.Diagnostics.Debug.WriteLine($"ShowCustomMessage complete: {formattedMessage}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowCustomMessage error: {ex.Message}");
            }
        }

        /// <summary>
        /// 직접 메시지 표시 (디버깅용)
        /// </summary>
        public static void ShowDirectMessage(string message)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== ShowDirectMessage called: {message} ===");

                if (_alramTextBlock != null)
                {
                    _alramTextBlock.Text = message;
                    System.Diagnostics.Debug.WriteLine($"Direct message displayed successfully: {message}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: _alramTextBlock is null!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowDirectMessage error: {ex.Message}");
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 타이머 초기화 (강화됨)
        /// </summary>
        private static void InitializeTimer()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== InitializeTimer starting ===");

                if (_messageTimer != null)
                {
                    _messageTimer.Stop();
                    _messageTimer.Tick -= MessageTimer_Tick; // 이벤트 해제
                    _messageTimer = null;
                    System.Diagnostics.Debug.WriteLine("Existing timer cleaned up successfully");
                }

                // UI 스레드에서 타이머 생성
                if (_alramTextBlock != null && _alramTextBlock.Dispatcher.CheckAccess())
                {
                    // 현재 UI 스레드에서 실행 중
                    CreateTimer();
                }
                else if (_alramTextBlock != null)
                {
                    // 다른 스레드에서 실행 중 - UI 스레드로 전환
                    _alramTextBlock.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CreateTimer();
                    }), System.Windows.Threading.DispatcherPriority.Normal);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Cannot create timer because _alramTextBlock is null");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ InitializeTimer failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Create timer on UI thread
        /// </summary>
        private static void CreateTimer()
        {
            try
            {
                _messageTimer = new System.Windows.Threading.DispatcherTimer();
                _messageTimer.Interval = TimeSpan.FromSeconds(MESSAGE_DISPLAY_SECONDS);
                _messageTimer.Tick += MessageTimer_Tick;

                System.Diagnostics.Debug.WriteLine($"✓ Timer created successfully - Interval: {MESSAGE_DISPLAY_SECONDS} seconds");
                System.Diagnostics.Debug.WriteLine($"Timer Dispatcher: {_messageTimer.Dispatcher != null}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateTimer error: {ex.Message}");
            }
        }

        /// <summary>
        /// 타이머 이벤트 핸들러
        /// </summary>
        private static void MessageTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== MessageTimer_Tick occurred ===");

                if (_messageTimer != null)
                {
                    _messageTimer.Stop();
                    System.Diagnostics.Debug.WriteLine("Timer stopped");
                }

                if (_alramTextBlock != null)
                {
                    _alramTextBlock.Text = DEFAULT_MESSAGE;
                    System.Diagnostics.Debug.WriteLine($"✓ Reverted to default message: {DEFAULT_MESSAGE}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: MessageTimer_Tick - _alramTextBlock is null");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ MessageTimer_Tick error: {ex.Message}");
            }
        }

        /// <summary>Aram
        /// 컴포넌트 유효성 검사 (강화됨)
        /// </summary>
        private static bool ValidateComponents(string alarmId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== ValidateComponents: {alarmId} ===");

                if (string.IsNullOrEmpty(alarmId))
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: alarmId is null or empty!");
                    return false;
                }

                if (_alramTextBlock == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: _alramTextBlock is null!");
                    System.Diagnostics.Debug.WriteLine("SetAlarmTextBlock was not called or failed.");
                    return false;
                }

                if (_messageTimer == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: _messageTimer is null!");
                    System.Diagnostics.Debug.WriteLine("Attempting to re-initialize the timer...");
                    InitializeTimer();

                    if (_messageTimer == null)
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: Timer initialization failed!");
                        return false;
                    }
                }

                System.Diagnostics.Debug.WriteLine("ValidateComponents passed");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ValidateComponents error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 알람 메시지 가져오기
        /// </summary>
        private static string GetAlarmMessage(string alarmId)
        {
            return _alarmMessages.ContainsKey(alarmId)
                ? _alarmMessages[alarmId]
                : "Unknown alarm";
        }

        /// <summary>
        /// 메시지 포맷팅
        /// </summary>
        private static string FormatMessage(string alarmId, string message)
        {
            return $"Alarm: [{alarmId}] {message}";
        }

        /// <summary>
        /// 메시지 표시 (강화됨)
        /// </summary>
        private static void DisplayMessage(string formattedMessage)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== DisplayMessage: {formattedMessage} ===");

                if (_alramTextBlock == null)
                {
                    System.Diagnostics.Debug.WriteLine("✗ ERROR: _alramTextBlock is null!");
                    return;
                }

                // 타이머 정지
                if (_messageTimer != null)
                {
                    _messageTimer.Stop();
                    System.Diagnostics.Debug.WriteLine("Stopping existing timer");
                }

                // UI 스레드에서 안전하게 메시지 표시
                if (_alramTextBlock.Dispatcher.CheckAccess())
                {
                    // 현재 UI 스레드
                    SetMessageAndStartTimer(formattedMessage);
                }
                else
                {
                    // 다른 스레드 - Dispatcher 사용
                    _alramTextBlock.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        SetMessageAndStartTimer(formattedMessage);
                    }), System.Windows.Threading.DispatcherPriority.Normal);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ DisplayMessage error: {ex.Message}");
            }
        }

        /// <summary>
        /// Set message and start timer on UI thread
        /// </summary>
        private static void SetMessageAndStartTimer(string message)
        {
            try
            {
                // 메시지 설정
                _alramTextBlock.Text = message;

                // 강제 UI 새로고침 추가
                _alramTextBlock.InvalidateVisual();
                _alramTextBlock.UpdateLayout();

                // 부모 컨테이너도 새로고침
                var parent = _alramTextBlock.Parent as System.Windows.FrameworkElement;
                if (parent != null)
                {
                    parent.InvalidateVisual();
                    parent.UpdateLayout();
                }

                System.Diagnostics.Debug.WriteLine($"✓ Message displayed successfully (UI forced refresh): {message}");

                // 타이머 재시작
                if (_messageTimer != null)
                {
                    _messageTimer.Start();
                    System.Diagnostics.Debug.WriteLine("✓ Timer restarted");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠ WARNING: Timer is null, cannot restart");
                    InitializeTimer();
                    if (_messageTimer != null)
                    {
                        _messageTimer.Start();
                        System.Diagnostics.Debug.WriteLine("✓ Timer recreated and started");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetMessageAndStartTimer error: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 상태 디버깅 정보 표시
        /// </summary>
        public static void ShowDebugStatus()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== AlarmMessageManager Debug Status ===");
                System.Diagnostics.Debug.WriteLine($"Is _alramTextBlock null: {_alramTextBlock == null}");
                System.Diagnostics.Debug.WriteLine($"Is _messageTimer null: {_messageTimer == null}");

                if (_alramTextBlock != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Current text: {_alramTextBlock.Text}");
                }

                if (_messageTimer != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Is timer enabled: {_messageTimer.IsEnabled}");
                    System.Diagnostics.Debug.WriteLine($"Timer interval: {_messageTimer.Interval.TotalSeconds} seconds");
                }

                System.Diagnostics.Debug.WriteLine($"Number of registered alarm messages: {_alarmMessages.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowDebugStatus error: {ex.Message}");
            }
        }

        /// <summary>
        /// 강제 초기화 (문제 해결용)
        /// </summary>
        public static void ForceInitialize(TextBlock textBlock)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== ForceInitialize starting ===");

                // 모든 것을 정리하고 다시 시작
                if (_messageTimer != null)
                {
                    _messageTimer.Stop();
                    _messageTimer.Tick -= MessageTimer_Tick;
                    _messageTimer = null;
                }

                _alramTextBlock = null;

                // 다시 설정
                SetAlarmTextBlock(textBlock);

                System.Diagnostics.Debug.WriteLine("ForceInitialize complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ForceInitialize error: {ex.Message}");
            }
        }
        #endregion
    }
}