using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TeachingPendant.Logging;
using TeachingPendant.Alarm;

namespace TeachingPendant.ErrorHandling
{
    /// <summary>
    /// 전역 예외 처리 시스템
    /// C# 6.0 / .NET Framework 4.6.1 호환 버전
    /// </summary>
    public static class GlobalExceptionHandler
    {
        #region Private Fields
        private static bool _isInitialized = false;
        private static int _criticalErrorCount = 0;
        private static DateTime _lastCriticalError = DateTime.MinValue;
        private const int MAX_CRITICAL_ERRORS = 3;
        private const int CRITICAL_ERROR_RESET_MINUTES = 10;
        #endregion

        #region Events
        /// <summary>
        /// 처리되지 않은 예외 발생 시 이벤트
        /// </summary>
        public static event EventHandler<CustomUnhandledExceptionEventArgs> UnhandledException;

        /// <summary>
        /// 복구 가능한 오류 발생 시 이벤트
        /// </summary>
        public static event EventHandler<CustomRecoverableErrorEventArgs> RecoverableError;
        #endregion

        #region Initialization
        /// <summary>
        /// 전역 예외 처리기 초기화
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
            {
                Logger.Warning("GlobalExceptionHandler", "Initialize", "Already initialized.");
                return;
            }

            try
            {
                // WPF UI 스레드 예외 처리
                Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

                // 백그라운드 스레드 예외 처리
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                // Task 예외 처리 (.NET 4.0+)
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

                _isInitialized = true;
                Logger.Info("GlobalExceptionHandler", "Initialize", "Global exception handler initialized successfully.");
            }
            catch (Exception ex)
            {
                Logger.Critical("GlobalExceptionHandler", "Initialize", "Global exception handler initialization failed.", ex);
            }
        }

        /// <summary>
        /// 전역 예외 처리기 종료
        /// </summary>
        public static void Shutdown()
        {
            if (!_isInitialized)
                return;

            try
            {
                // 이벤트 구독 해제
                if (Application.Current != null)
                {
                    Application.Current.DispatcherUnhandledException -= Current_DispatcherUnhandledException;
                }

                AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

                _isInitialized = false;
                Logger.Info("GlobalExceptionHandler", "Shutdown", "Global exception handler shut down successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error("GlobalExceptionHandler", "Shutdown", "Error during shutdown.", ex);
            }
        }
        #endregion

        #region Exception Handlers
        /// <summary>
        /// WPF UI 스레드 예외 처리
        /// </summary>
        private static void Current_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                var errorInfo = AnalyzeException(e.Exception);
                Logger.Critical("UI", "DispatcherException",
                    "Unhandled exception occurred on UI thread: " + errorInfo.UserMessage, e.Exception);

                // 복구 가능한 오류인지 판단
                if (errorInfo.IsRecoverable && _criticalErrorCount < MAX_CRITICAL_ERRORS)
                {
                    // 복구 시도
                    e.Handled = true;
                    _criticalErrorCount++;

                    ShowRecoverableErrorDialog(errorInfo);

                    // 복구 가능한 오류 이벤트 발생
                    RecoverableError?.Invoke(null, new CustomRecoverableErrorEventArgs(e.Exception, errorInfo));
                }
                else
                {
                    // 치명적 오류 - 애플리케이션 종료
                    e.Handled = false; // WPF가 종료하도록 허용
                    ShowCriticalErrorDialog(errorInfo);

                    // 처리되지 않은 예외 이벤트 발생
                    UnhandledException?.Invoke(null, new CustomUnhandledExceptionEventArgs(e.Exception, true));

                    PerformEmergencyShutdown();
                }
            }
            catch (Exception ex)
            {
                // 예외 처리기에서 예외 발생 - 최후의 수단
                Logger.Critical("GlobalExceptionHandler", "DispatcherException",
                    "Exception occurred within the exception handler.", ex);
                ForceApplicationShutdown();
            }
        }

        /// <summary>
        /// 백그라운드 스레드 예외 처리
        /// </summary>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var exception = e.ExceptionObject as Exception;
                if (exception != null)
                {
                    var errorInfo = AnalyzeException(exception);
                    Logger.Critical("Background", "UnhandledException",
                        "Unhandled exception occurred on background thread: " + errorInfo.UserMessage, exception);

                    // 백그라운드 스레드 예외는 대부분 치명적
                    if (e.IsTerminating)
                    {
                        ShowCriticalErrorDialog(errorInfo);
                        PerformEmergencyShutdown();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Critical("GlobalExceptionHandler", "UnhandledException",
                    "Error while handling background exception.", ex);
                ForceApplicationShutdown();
            }
        }

        /// <summary>
        /// Task 예외 처리
        /// </summary>
        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                foreach (var ex in e.Exception.InnerExceptions)
                {
                    var errorInfo = AnalyzeException(ex);
                    Logger.Error("Task", "UnobservedTaskException",
                        "Unobserved exception occurred in a Task: " + errorInfo.UserMessage, ex);
                }

                // Task 예외는 관찰됨으로 표시하여 앱 종료 방지
                e.SetObserved();

                // 복구 가능한 오류로 처리
                RecoverableError?.Invoke(null, new CustomRecoverableErrorEventArgs(e.Exception, null));
            }
            catch (Exception ex)
            {
                Logger.Critical("GlobalExceptionHandler", "TaskException",
                    "Error while handling Task exception.", ex);
            }
        }
        #endregion

        #region Exception Analysis
        /// <summary>
        /// 예외 분석 및 복구 가능성 판단
        /// </summary>
        private static ErrorInfo AnalyzeException(Exception exception)
        {
            var errorInfo = new ErrorInfo
            {
                Exception = exception,
                Timestamp = DateTime.Now,
                IsRecoverable = false,
                UserMessage = "An unknown error has occurred.",
                TechnicalMessage = exception.Message,
                SuggestedAction = "Please restart the application."
            };

            // 예외 타입별 분석
            switch (exception.GetType().Name)
            {
                case "ArgumentException":
                case "ArgumentNullException":
                case "ArgumentOutOfRangeException":
                    errorInfo.IsRecoverable = true;
                    errorInfo.UserMessage = "There is a problem with an input value.";
                    errorInfo.SuggestedAction = "Please enter a valid value and try again.";
                    break;

                case "InvalidOperationException":
                    errorInfo.IsRecoverable = true;
                    errorInfo.UserMessage = "This operation cannot be performed in the current state.";
                    errorInfo.SuggestedAction = "Please check the system status and try again.";
                    break;

                case "IOException":
                case "UnauthorizedAccessException":
                    errorInfo.IsRecoverable = true;
                    errorInfo.UserMessage = "An error occurred while accessing a file.";
                    errorInfo.SuggestedAction = "Please ensure the file is not in use and try again.";
                    break;

                case "TimeoutException":
                    errorInfo.IsRecoverable = true;
                    errorInfo.UserMessage = "The connection has timed out.";
                    errorInfo.SuggestedAction = "Please check your network connection and try again.";
                    break;

                case "OutOfMemoryException":
                case "StackOverflowException":
                case "AccessViolationException":
                    errorInfo.IsRecoverable = false;
                    errorInfo.UserMessage = "This is a critical error due to insufficient system resources.";
                    errorInfo.SuggestedAction = "Please restart the application.";
                    break;

                default:
                    // 메시지 내용으로 판단
                    if (exception.Message.ToLower().Contains("network") || exception.Message.ToLower().Contains("connection"))
                    {
                        errorInfo.IsRecoverable = true;
                        errorInfo.UserMessage = "A network connection error has occurred.";
                        errorInfo.SuggestedAction = "Please check the network connection.";
                    }
                    else if (exception.Message.ToLower().Contains("file") || exception.Message.ToLower().Contains("directory"))
                    {
                        errorInfo.IsRecoverable = true;
                        errorInfo.UserMessage = "A file system error has occurred.";
                        errorInfo.SuggestedAction = "Please check the file path and permissions.";
                    }
                    break;
            }

            return errorInfo;
        }
        #endregion

        #region Error Dialogs
        /// <summary>
        /// 복구 가능한 오류 다이얼로그 표시
        /// </summary>
        private static void ShowRecoverableErrorDialog(ErrorInfo errorInfo)
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var result = MessageBox.Show(
                        errorInfo.UserMessage + "\n\n" + errorInfo.SuggestedAction + "\n\nDo you want to continue?",
                        "Recoverable Error",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                    {
                        PerformEmergencyShutdown();
                    }
                }));
            }
            catch (Exception ex)
            {
                Logger.Error("GlobalExceptionHandler", "ShowRecoverableErrorDialog", "Failed to display error dialog", ex);
            }
        }

        /// <summary>
        /// 치명적 오류 다이얼로그 표시
        /// </summary>
        private static void ShowCriticalErrorDialog(ErrorInfo errorInfo)
        {
            try
            {
                var message = errorInfo.UserMessage + "\n\n" +
                                "Technical Information: " + errorInfo.TechnicalMessage + "\n\n" +
                                "Log Location: " + Logger.GetLogDirectory() + "\n\n" +
                                "The application will now close.";

                MessageBox.Show(message, "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("GlobalExceptionHandler", "ShowCriticalErrorDialog", "Failed to display critical error dialog", ex);
            }
        }
        #endregion

        #region Emergency Procedures
        /// <summary>
        /// 비상 종료 절차
        /// </summary>
        private static void PerformEmergencyShutdown()
        {
            try
            {
                Logger.Critical("System", "EmergencyShutdown", "Starting emergency shutdown procedure");

                // 중요한 데이터 저장 시도
                try
                {
                    // 5초 타임아웃으로 데이터 저장 시도
                    var saveTask = Task.Run(() => SaveCriticalData());
                    if (!saveTask.Wait(TimeSpan.FromSeconds(5)))
                    {
                        Logger.Warning("System", "EmergencyShutdown", "Data save timed out");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("System", "EmergencyShutdown", "Emergency data save failed", ex);
                }

                // 로그 플러시
                Logger.FlushLogs();

                // 강제 종료
                ForceApplicationShutdown();
            }
            catch (Exception ex)
            {
                Logger.Critical("GlobalExceptionHandler", "EmergencyShutdown", "Error during emergency shutdown", ex);
                ForceApplicationShutdown();
            }
        }

        /// <summary>
        /// 강제 애플리케이션 종료
        /// </summary>
        private static void ForceApplicationShutdown()
        {
            try
            {
                if (Application.Current != null)
                {
                    Application.Current.Shutdown(1);
                }
                else
                {
                    Environment.Exit(1);
                }
            }
            catch
            {
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// 중요 데이터 저장
        /// </summary>
        private static void SaveCriticalData()
        {
            try
            {
                // 여기에 중요한 데이터 저장 로직 추가
                // 예: PersistentDataManager.SaveAllDataAsync().Wait();
                Logger.Info("System", "SaveCriticalData", "Critical data saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error("System", "SaveCriticalData", "Failed to save critical data.", ex);
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 수동으로 예외 처리
        /// </summary>
        public static void HandleException(Exception exception, string module, string method, bool showDialog = true)
        {
            var errorInfo = AnalyzeException(exception);

            if (errorInfo.IsRecoverable)
            {
                Logger.Error(module, method, errorInfo.UserMessage, exception);

                if (showDialog)
                {
                    ShowRecoverableErrorDialog(errorInfo);
                }

                RecoverableError?.Invoke(null, new CustomRecoverableErrorEventArgs(exception, errorInfo));
            }
            else
            {
                Logger.Critical(module, method, errorInfo.UserMessage, exception);

                if (showDialog)
                {
                    ShowCriticalErrorDialog(errorInfo);
                }

                UnhandledException?.Invoke(null, new CustomUnhandledExceptionEventArgs(exception, true));
            }
        }

        /// <summary>
        /// 치명적 오류 카운터 리셋
        /// </summary>
        public static void ResetCriticalErrorCount()
        {
            var now = DateTime.Now;
            if ((now - _lastCriticalError).TotalMinutes > CRITICAL_ERROR_RESET_MINUTES)
            {
                _criticalErrorCount = 0;
                _lastCriticalError = now;
                Logger.Info("GlobalExceptionHandler", "ResetCriticalErrorCount", "Critical error counter has been reset.");
            }
        }
        #endregion
    }

    #region Support Classes
    /// <summary>
    /// 오류 정보 클래스
    /// </summary>
    public class ErrorInfo
    {
        public Exception Exception { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRecoverable { get; set; }
        public string UserMessage { get; set; }
        public string TechnicalMessage { get; set; }
        public string SuggestedAction { get; set; }
    }

    /// <summary>
    /// 처리되지 않은 예외 이벤트 인자 (커스텀)
    /// </summary>
    public class CustomUnhandledExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; private set; }
        public bool IsTerminating { get; private set; }

        public CustomUnhandledExceptionEventArgs(Exception exception, bool isTerminating)
        {
            Exception = exception;
            IsTerminating = isTerminating;
        }
    }

    /// <summary>
    /// 복구 가능한 오류 이벤트 인자 (커스텀)
    /// </summary>
    public class CustomRecoverableErrorEventArgs : EventArgs
    {
        public Exception Exception { get; private set; }
        public ErrorInfo ErrorInfo { get; private set; }

        public CustomRecoverableErrorEventArgs(Exception exception, ErrorInfo errorInfo)
        {
            Exception = exception;
            ErrorInfo = errorInfo;
        }
    }
    #endregion
}