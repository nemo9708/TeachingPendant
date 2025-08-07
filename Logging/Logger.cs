using System;
using System.Diagnostics;
using TeachingPendant.Alarm;

namespace TeachingPendant.Logging
{
    /// <summary>
    /// 메인 로거 클래스 - 전체 로깅 시스템의 진입점
    /// 기존 AlarmMessageManager와 연동하여 UI 메시지는 그대로 유지하면서 파일 로깅 추가
    /// </summary>
    public static class Logger
    {
        #region Private Fields
        private static FileLogWriter _fileWriter;
        private static LogLevel _minimumLogLevel = LogLevel.Info;
        private static bool _isInitialized = false;
        private static readonly object _initLock = new object();
        #endregion

        #region Initialization
        /// <summary>
        /// 로거 초기화 (애플리케이션 시작 시 한 번만 호출)
        /// </summary>
        /// <param name="minimumLogLevel">기록할 최소 로그 레벨</param>
        /// <param name="logDirectory">로그 디렉토리 (null이면 기본값 사용)</param>
        public static void Initialize(LogLevel minimumLogLevel = LogLevel.Info, string logDirectory = null)
        {
            lock (_initLock)
            {
                if (_isInitialized)
                {
                    // 여기서는 System.Diagnostics.Debug를 사용하므로 문제가 없습니다.
                    System.Diagnostics.Debug.WriteLine("Logger has already been initialized.");
                    return;
                }

                try
                {
                    _minimumLogLevel = minimumLogLevel;
                    _fileWriter = new FileLogWriter(logDirectory);
                    _isInitialized = true;

                    // 초기화 성공 로그
                    Info("Logger", "Initialize", $"Logging system initialized successfully - Minimum level: {minimumLogLevel}");
                    Info("Logger", "Initialize", $"Log directory: {_fileWriter.GetLogDirectory()}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Logger initialization failed: {ex.Message}");
                    // 초기화 실패해도 애플리케이션은 계속 실행되어야 함
                }
            }
        }

        /// <summary>
        /// 로거 종료 (애플리케이션 종료 시 호출)
        /// </summary>
        public static void Shutdown()
        {
            lock (_initLock)
            {
                if (_isInitialized)
                {
                    Info("Logger", "Shutdown", "Shutting down logging system...");
                    _fileWriter?.Dispose();
                    _fileWriter = null;
                    _isInitialized = false;
                }
            }
        }

        /// <summary>
        /// 최소 로그 레벨 변경
        /// </summary>
        public static void SetMinimumLogLevel(LogLevel level)
        {
            _minimumLogLevel = level;
            Info("Logger", "SetMinimumLogLevel", $"Minimum log level changed: {level}");
        }
        #endregion

        #region Public Logging Methods
        /// <summary>
        /// Debug 레벨 로그 기록
        /// </summary>
        /// <param name="module">모듈명 (예: "Teaching", "Movement")</param>
        /// <param name="method">메서드명</param>
        /// <param name="message">로그 메시지</param>
        /// <param name="context">추가 컨텍스트 정보</param>
        public static void LogDebug(string module, string method, string message, string context = null)
        {
            WriteLog(LogLevel.Debug, module, method, message, null, context);
        }

        /// <summary>
        /// Info 레벨 로그 기록 및 UI 메시지 표시
        /// </summary>
        /// <param name="module">모듈명</param>
        /// <param name="method">메서드명</param>
        /// <param name="message">로그 메시지</param>
        /// <param name="showInUI">UI에 메시지 표시 여부 (기본값: false)</param>
        /// <param name="context">추가 컨텍스트 정보</param>
        public static void Info(string module, string method, string message, bool showInUI = false, string context = null)
        {
            WriteLog(LogLevel.Info, module, method, message, null, context);

            // UI에 표시하려면 기존 AlarmMessageManager 사용
            if (showInUI)
            {
                AlarmMessageManager.ShowCustomMessage(message, AlarmCategory.Information);
            }
        }

        /// <summary>
        /// Warning 레벨 로그 기록 및 UI 경고 표시
        /// </summary>
        /// <param name="module">모듈명</param>
        /// <param name="method">메서드명</param>
        /// <param name="message">로그 메시지</param>
        /// <param name="showInUI">UI에 경고 표시 여부 (기본값: true)</param>
        /// <param name="context">추가 컨텍스트 정보</param>
        public static void Warning(string module, string method, string message, bool showInUI = true, string context = null)
        {
            WriteLog(LogLevel.Warning, module, method, message, null, context);

            // 경고는 기본적으로 UI에 표시
            if (showInUI)
            {
                AlarmMessageManager.ShowCustomMessage(message, AlarmCategory.Warning);
            }
        }

        /// <summary>
        /// Error 레벨 로그 기록 및 UI 오류 표시
        /// </summary>
        /// <param name="module">모듈명</param>
        /// <param name="method">메서드명</param>
        /// <param name="message">로그 메시지</param>
        /// <param name="exception">예외 정보</param>
        /// <param name="showInUI">UI에 오류 표시 여부 (기본값: true)</param>
        /// <param name="context">추가 컨텍스트 정보</param>
        public static void Error(string module, string method, string message, Exception exception = null, bool showInUI = true, string context = null)
        {
            WriteLog(LogLevel.Error, module, method, message, exception, context);

            // 오류는 기본적으로 UI에 표시
            if (showInUI)
            {
                var displayMessage = exception != null ? $"{message}: {exception.Message}" : message;
                AlarmMessageManager.ShowCustomMessage(displayMessage, AlarmCategory.Error);
            }
        }

        /// <summary>
        /// Critical 레벨 로그 기록 및 UI 치명적 오류 표시
        /// </summary>
        /// <param name="module">모듈명</param>
        /// <param name="method">메서드명</param>
        /// <param name="message">로그 메시지</param>
        /// <param name="exception">예외 정보</param>
        /// <param name="context">추가 컨텍스트 정보</param>
        public static void Critical(string module, string method, string message, Exception exception = null, string context = null)
        {
            WriteLog(LogLevel.Critical, module, method, message, exception, context);

            // 치명적 오류는 항상 UI에 표시
            var displayMessage = exception != null ? $"[CRITICAL] {message}: {exception.Message}" : $"[CRITICAL] {message}";
            AlarmMessageManager.ShowCustomMessage(displayMessage, AlarmCategory.Error);
        }
        #endregion

        #region Convenience Methods
        /// <summary>
        /// 예외 발생 시 자동으로 Error 로그 기록
        /// try-catch 블록에서 사용하기 편리한 메서드
        /// </summary>
        /// <param name="module">모듈명</param>
        /// <param name="method">메서드명</param>
        /// <param name="exception">발생한 예외</param>
        /// <param name="additionalMessage">추가 메시지</param>
        /// <param name="showInUI">UI에 표시 여부</param>
        public static void LogException(string module, string method, Exception exception, string additionalMessage = null, bool showInUI = true)
        {
            var message = string.IsNullOrEmpty(additionalMessage)
                ? $"Exception occurred: {exception.GetType().Name}"
                : $"{additionalMessage} - Exception: {exception.GetType().Name}";

            Error(module, method, message, exception, showInUI);
        }

        /// <summary>
        /// 사용자 액션 로그 (Info 레벨, UI 표시)
        /// 사용자가 버튼 클릭, 데이터 입력 등의 작업을 했을 때 사용
        /// </summary>
        /// <param name="module">모듈명</param>
        /// <param name="action">수행한 액션</param>
        /// <param name="details">상세 정보</param>
        public static void UserAction(string module, string action, string details = null)
        {
            var message = string.IsNullOrEmpty(details) ? $"User action: {action}" : $"User action: {action} - {details}";
            Info(module, "UserAction", message, showInUI: true);
        }

        /// <summary>
        /// 시스템 상태 변경 로그 (Info 레벨, UI 표시)
        /// 모드 변경, 연결 상태 변경 등에 사용
        /// </summary>
        /// <param name="module">모듈명</param>
        /// <param name="statusChange">상태 변경 내용</param>
        /// <param name="details">상세 정보</param>
        public static void StatusChange(string module, string statusChange, string details = null)
        {
            var message = string.IsNullOrEmpty(details) ? $"Status change: {statusChange}" : $"Status change: {statusChange} - {details}";
            Info(module, "StatusChange", message, showInUI: true);
        }

        /// <summary>
        /// 데이터 작업 성공 로그 (Info 레벨, UI 표시)
        /// 저장, 로드, 전송 성공 등에 사용
        /// </summary>
        /// <param name="module">모듈명</param>
        /// <param name="operation">수행한 작업</param>
        /// <param name="target">작업 대상</param>
        public static void OperationSuccess(string module, string operation, string target = null)
        {
            var message = string.IsNullOrEmpty(target) ? $"{operation} successful" : $"{operation} successful: {target}";
            Info(module, "Operation", message, showInUI: true);

            // 기존 AlarmMessageManager의 성공 알람도 함께 표시
            AlarmMessageManager.ShowAlarm(Alarms.OPERATION_COMPLETED, message);
        }

        /// <summary>
        /// 데이터 작업 실패 로그 (Error 레벨, UI 표시)
        /// </summary>
        /// <param name="module">모듈명</param>
        /// <param name="operation">실패한 작업</param>
        /// <param name="target">작업 대상</param>
        /// <param name="reason">실패 이유</param>
        public static void OperationFailed(string module, string operation, string target = null, string reason = null)
        {
            var message = $"{operation} failed";
            if (!string.IsNullOrEmpty(target)) message += $": {target}";
            if (!string.IsNullOrEmpty(reason)) message += $" - {reason}";

            Error(module, "Operation", message, showInUI: true);
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// 현재 로그 파일 경로 반환
        /// </summary>
        public static string GetCurrentLogFilePath()
        {
            return _fileWriter?.GetCurrentLogFilePath() ?? "Logger not initialized";
        }

        /// <summary>
        /// 로그 디렉토리 경로 반환
        /// </summary>
        public static string GetLogDirectory()
        {
            return _fileWriter?.GetLogDirectory() ?? "Logger not initialized";
        }

        /// <summary>
        /// 즉시 모든 대기 중인 로그를 파일에 기록
        /// </summary>
        public static void FlushLogs()
        {
            _fileWriter?.Flush();
        }

        /// <summary>
        /// 로거 초기화 상태 확인
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// 현재 최소 로그 레벨 반환
        /// </summary>
        public static LogLevel MinimumLogLevel => _minimumLogLevel;
        #endregion

        #region Private Methods
        /// <summary>
        /// 실제 로그 기록 처리
        /// </summary>
        private static void WriteLog(LogLevel level, string module, string method, string message, Exception exception, string context)
        {
            try
            {
                // 최소 로그 레벨 확인
                if (level < _minimumLogLevel)
                    return;

                // 로거가 초기화되지 않았으면 자동 초기화 시도
                if (!_isInitialized)
                {
                    Initialize();
                }

                // Debug 출력 (기존 방식 유지)
                var debugMessage = $"[{level.ToShortString()}] {module}.{method}: {message}";
                if (exception != null)
                {
                    debugMessage += $" - Exception: {exception.Message}";
                }
                System.Diagnostics.Debug.WriteLine(debugMessage);

                // 파일 로깅
                if (_fileWriter != null)
                {
                    var logEntry = new LogEntry(level, module, method, message, exception)
                    {
                        Context = context
                    };
                    _fileWriter.WriteLog(logEntry);
                }
            }
            catch (Exception ex)
            {
                // 로깅 자체에서 오류 발생 시 Debug 출력만 수행
                System.Diagnostics.Debug.WriteLine($"An error occurred during logging: {ex.Message}");
            }
        }
        #endregion

        #region Integration with existing code
        /// <summary>
        /// 기존 AlarmMessageManager.ShowAlarm 호출을 Logger로 전환하기 위한 헬퍼 메서드
        /// 기존 코드를 점진적으로 마이그레이션할 때 사용
        /// </summary>
        /// <param name="module">모듈명</param>
        /// <param name="method">메서드명</param>
        /// <param name="alarmId">기존 알람 ID</param>
        /// <param name="message">메시지</param>
        public static void ShowAlarmWithLogging(string module, string method, string alarmId, string message)
        {
            // 알람 카테고리에 따라 적절한 로그 레벨 결정
            var category = Alarms.GetCategory(alarmId);
            switch (category)
            {
                case AlarmCategory.Information:
                case AlarmCategory.Success:
                    Info(module, method, message, showInUI: false); // UI는 ShowAlarm에서 처리
                    break;
                case AlarmCategory.Warning:
                    Warning(module, method, message, showInUI: false);
                    break;
                case AlarmCategory.Error:
                    Error(module, method, message, showInUI: false);
                    break;
                case AlarmCategory.System:
                    Info(module, method, message, showInUI: false);
                    break;
            }

            // 기존 UI 표시는 그대로 유지
            AlarmMessageManager.ShowAlarm(alarmId, message);
        }

        /// <summary>
        /// 기존 System.Diagnostics.Debug.WriteLine을 Logger로 전환하기 위한 헬퍼 메서드
        /// </summary>
        /// <param name="module">모듈명</param>
        /// <param name="method">메서드명</param>
        /// <param name="debugMessage">기존 디버그 메시지</param>
        public static void DebugReplace(string module, string method, string debugMessage)
        {
            LogDebug(module, method, debugMessage);
        }
        #endregion
    }
}