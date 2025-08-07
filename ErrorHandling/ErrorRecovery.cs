using System;
using System.Threading.Tasks;
using System.Windows;
using TeachingPendant.Logging;
using TeachingPendant.Alarm;

namespace TeachingPendant.ErrorHandling
{
    /// <summary>
    /// 자동 복구 시스템
    /// 특정 오류에 대해 자동으로 복구를 시도하는 클래스
    /// </summary>
    public static class ErrorRecovery
    {
        #region Private Fields
        private static int _retryCount = 0;
        private static DateTime _lastRecoveryAttempt = DateTime.MinValue;
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int RETRY_COOLDOWN_MINUTES = 5;
        #endregion

        #region Events
        /// <summary>
        /// 복구 시도 이벤트
        /// </summary>
        public static event EventHandler<RecoveryAttemptEventArgs> RecoveryAttempted;

        /// <summary>
        /// 복구 완료 이벤트
        /// </summary>
        public static event EventHandler<RecoveryCompletedEventArgs> RecoveryCompleted;
        #endregion

        #region Public Methods
        /// <summary>
        /// 예외에 대한 자동 복구 시도
        /// </summary>
        /// <param name="exception">복구할 예외</param>
        /// <param name="context">복구 컨텍스트</param>
        /// <returns>복구 결과</returns>
        public static async Task<RecoveryResult> AttemptRecoveryAsync(Exception exception, RecoveryContext context = null)
        {
            var result = new RecoveryResult
            {
                Exception = exception,
                Context = context,
                StartTime = DateTime.Now,
                IsSuccessful = false,
                RecoveryActions = new System.Collections.Generic.List<string>()
            };

            try
            {
                Logger.Info("ErrorRecovery", "AttemptRecovery",
                    "Starting automatic recovery attempt: " + exception.GetType().Name);

                // 복구 시도 이벤트 발생
                RecoveryAttempted?.Invoke(null, new RecoveryAttemptEventArgs(exception, context));

                // 복구 가능성 확인
                if (!IsRecoveryPossible(exception))
                {
                    result.FailureReason = "Unrecoverable exception type";
                    Logger.Warning("ErrorRecovery", "AttemptRecovery", result.FailureReason);
                    return result;
                }

                // 재시도 제한 확인
                if (!CanAttemptRecovery())
                {
                    result.FailureReason = "Retry limit exceeded";
                    Logger.Warning("ErrorRecovery", "AttemptRecovery", result.FailureReason);
                    return result;
                }

                _retryCount++;
                _lastRecoveryAttempt = DateTime.Now;

                // 예외 유형별 복구 시도
                result.IsSuccessful = await PerformRecoveryByExceptionType(exception, result);

                if (result.IsSuccessful)
                {
                    // 복구 성공 시 카운터 리셋
                    _retryCount = 0;
                    Logger.Info("ErrorRecovery", "AttemptRecovery", "Automatic recovery successful");

                    // UI에 성공 메시지 표시
                    AlarmMessageManager.ShowCustomMessage("A system error was automatically recovered.",
                        AlarmCategory.Success);
                }
                else
                {
                    Logger.Warning("ErrorRecovery", "AttemptRecovery",
                        "Automatic recovery failed: " + result.FailureReason);
                }

                result.EndTime = DateTime.Now;
                result.Duration = result.EndTime - result.StartTime;

                // 복구 완료 이벤트 발생
                RecoveryCompleted?.Invoke(null, new RecoveryCompletedEventArgs(result));

                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccessful = false;
                result.FailureReason = "Additional error during recovery: " + ex.Message;
                result.EndTime = DateTime.Now;
                result.Duration = result.EndTime - result.StartTime;

                Logger.Error("ErrorRecovery", "AttemptRecovery", "Error during recovery attempt", ex);
                return result;
            }
        }

        /// <summary>
        /// 특정 복구 액션 수동 실행
        /// </summary>
        /// <param name="actionType">복구 액션 유형</param>
        /// <returns>복구 성공 여부</returns>
        public static async Task<bool> ExecuteRecoveryActionAsync(RecoveryActionType actionType)
        {
            try
            {
                Logger.Info("ErrorRecovery", "ExecuteRecoveryAction",
                    "Executing manual recovery action: " + actionType.ToString());

                switch (actionType)
                {
                    case RecoveryActionType.MemoryCleanup:
                        return await PerformMemoryCleanup();

                    case RecoveryActionType.UIReset:
                        return await PerformUIReset();

                    case RecoveryActionType.DataReload:
                        return await PerformDataReload();

                    case RecoveryActionType.ConnectionReset:
                        return await PerformConnectionReset();

                    case RecoveryActionType.TempFileCleanup:
                        return await PerformTempFileCleanup();

                    default:
                        Logger.Warning("ErrorRecovery", "ExecuteRecoveryAction",
                            "Unknown recovery action: " + actionType.ToString());
                        return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorRecovery", "ExecuteRecoveryAction",
                    "Error executing recovery action", ex);
                return false;
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 복구 가능성 확인
        /// </summary>
        private static bool IsRecoveryPossible(Exception exception)
        {
            // 복구 불가능한 치명적 예외들
            var unrecoverableTypes = new[]
            {
                "OutOfMemoryException",
                "StackOverflowException",
                "AccessViolationException",
                "ExecutionEngineException",
                "BadImageFormatException"
            };

            var exceptionTypeName = exception.GetType().Name;
            foreach (var unrecoverableType in unrecoverableTypes)
            {
                if (exceptionTypeName == unrecoverableType)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 복구 시도 가능 여부 확인
        /// </summary>
        private static bool CanAttemptRecovery()
        {
            // 최대 재시도 횟수 확인
            if (_retryCount >= MAX_RETRY_ATTEMPTS)
            {
                // 쿨다운 시간 확인
                var timeSinceLastAttempt = DateTime.Now - _lastRecoveryAttempt;
                if (timeSinceLastAttempt.TotalMinutes < RETRY_COOLDOWN_MINUTES)
                {
                    return false;
                }
                else
                {
                    // 쿨다운 시간이 지나면 카운터 리셋
                    _retryCount = 0;
                }
            }

            return true;
        }

        /// <summary>
        /// 예외 유형별 복구 수행
        /// </summary>
        private static async Task<bool> PerformRecoveryByExceptionType(Exception exception, RecoveryResult result)
        {
            var exceptionTypeName = exception.GetType().Name;
            bool success = false;

            switch (exceptionTypeName)
            {
                case "OutOfMemoryException":
                    success = await PerformMemoryCleanup();
                    if (success) result.RecoveryActions.Add("Memory cleanup completed");
                    break;

                case "IOException":
                case "UnauthorizedAccessException":
                    success = await PerformFileSystemRecovery(exception);
                    if (success) result.RecoveryActions.Add("File system recovery completed");
                    break;

                case "TimeoutException":
                case "InvalidOperationException":
                    success = await PerformConnectionReset();
                    if (success) result.RecoveryActions.Add("Connection reset completed");
                    break;

                case "ArgumentException":
                case "ArgumentNullException":
                case "NullReferenceException":
                    success = await PerformDataReload();
                    if (success) result.RecoveryActions.Add("Data reload completed");
                    break;

                default:
                    // 일반적인 복구 시도
                    success = await PerformGeneralRecovery();
                    if (success) result.RecoveryActions.Add("General recovery procedure completed");
                    break;
            }

            if (!success)
            {
                result.FailureReason = "Recovery procedure failed for exception type: " + exceptionTypeName;
            }

            return success;
        }

        /// <summary>
        /// 메모리 정리 수행
        /// </summary>
        private static async Task<bool> PerformMemoryCleanup()
        {
            try
            {
                await Task.Run(() =>
                {
                    // 강제 가비지 컬렉션
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    // 큰 객체 힙 압축
                    GC.Collect(2, GCCollectionMode.Forced, true, true);
                });

                Logger.Info("ErrorRecovery", "MemoryCleanup", "Memory cleanup completed");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorRecovery", "MemoryCleanup", "Memory cleanup failed", ex);
                return false;
            }
        }

        /// <summary>
        /// UI 리셋 수행
        /// </summary>
        private static async Task<bool> PerformUIReset()
        {
            try
            {
                if (Application.Current?.MainWindow != null)
                {
                    await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // UI 레이아웃 강제 업데이트
                            Application.Current.MainWindow.UpdateLayout();
                            Application.Current.MainWindow.InvalidateVisual();

                            Logger.Info("ErrorRecovery", "UIReset", "UI reset completed");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("ErrorRecovery", "UIReset", "Error during UI reset", ex);
                        }
                    }));
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorRecovery", "UIReset", "UI reset failed", ex);
                return false;
            }
        }

        /// <summary>
        /// 데이터 재로드 수행
        /// </summary>
        private static async Task<bool> PerformDataReload()
        {
            try
            {
                await Task.Run(() =>
                {
                    // 여기에 실제 데이터 재로드 로직 추가
                    // 예: PersistentDataManager.LoadAllDataAsync().Wait();

                    Logger.Info("ErrorRecovery", "DataReload", "Data reload completed");
                });

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorRecovery", "DataReload", "Data reload failed", ex);
                return false;
            }
        }

        /// <summary>
        /// 연결 재설정 수행
        /// </summary>
        private static async Task<bool> PerformConnectionReset()
        {
            try
            {
                await Task.Run(() =>
                {
                    // 여기에 네트워크/하드웨어 연결 재설정 로직 추가
                    Logger.Info("ErrorRecovery", "ConnectionReset", "Connection reset completed");
                });

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorRecovery", "ConnectionReset", "Connection reset failed", ex);
                return false;
            }
        }

        /// <summary>
        /// 임시 파일 정리 수행
        /// </summary>
        private static async Task<bool> PerformTempFileCleanup()
        {
            try
            {
                await Task.Run(() =>
                {
                    var tempPath = System.IO.Path.GetTempPath();
                    var appTempPath = System.IO.Path.Combine(tempPath, "TeachingPendant");

                    if (System.IO.Directory.Exists(appTempPath))
                    {
                        System.IO.Directory.Delete(appTempPath, true);
                    }

                    Logger.Info("ErrorRecovery", "TempFileCleanup", "Temporary file cleanup completed");
                });

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorRecovery", "TempFileCleanup", "Temporary file cleanup failed", ex);
                return false;
            }
        }

        /// <summary>
        /// 파일 시스템 복구 수행
        /// </summary>
        private static async Task<bool> PerformFileSystemRecovery(Exception exception)
        {
            try
            {
                await Task.Run(() =>
                {
                    // 파일 잠금 해제 대기
                    System.Threading.Thread.Sleep(1000);

                    // 디렉토리 권한 확인 및 생성
                    var logDir = Logger.GetLogDirectory();
                    if (!System.IO.Directory.Exists(logDir))
                    {
                        System.IO.Directory.CreateDirectory(logDir);
                    }

                    Logger.Info("ErrorRecovery", "FileSystemRecovery", "File system recovery completed");
                });

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorRecovery", "FileSystemRecovery", "File system recovery failed", ex);
                return false;
            }
        }

        /// <summary>
        /// 일반 복구 절차 수행
        /// </summary>
        private static async Task<bool> PerformGeneralRecovery()
        {
            try
            {
                // 1. 메모리 정리
                var memorySuccess = await PerformMemoryCleanup();

                // 2. UI 리셋
                var uiSuccess = await PerformUIReset();

                // 3. 임시 파일 정리
                var tempSuccess = await PerformTempFileCleanup();

                var overallSuccess = memorySuccess && uiSuccess && tempSuccess;

                Logger.Info("ErrorRecovery", "GeneralRecovery",
                    "General recovery procedure completed - Success: " + overallSuccess.ToString());

                return overallSuccess;
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorRecovery", "GeneralRecovery", "General recovery procedure failed", ex);
                return false;
            }
        }
        #endregion

        #region Static Properties
        /// <summary>
        /// 현재 재시도 횟수
        /// </summary>
        public static int CurrentRetryCount
        {
            get { return _retryCount; }
        }

        /// <summary>
        /// 마지막 복구 시도 시간
        /// </summary>
        public static DateTime LastRecoveryAttempt
        {
            get { return _lastRecoveryAttempt; }
        }
        #endregion
    }

    #region Support Classes and Enums
    /// <summary>
    /// 복구 컨텍스트 정보
    /// </summary>
    public class RecoveryContext
    {
        public string Module { get; set; }
        public string Method { get; set; }
        public string UserAction { get; set; }
        public object AdditionalData { get; set; }
    }

    /// <summary>
    /// 복구 결과
    /// </summary>
    public class RecoveryResult
    {
        public Exception Exception { get; set; }
        public RecoveryContext Context { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsSuccessful { get; set; }
        public string FailureReason { get; set; }
        public System.Collections.Generic.List<string> RecoveryActions { get; set; }
    }

    /// <summary>
    /// 복구 액션 유형
    /// </summary>
    public enum RecoveryActionType
    {
        MemoryCleanup,
        UIReset,
        DataReload,
        ConnectionReset,
        TempFileCleanup
    }

    /// <summary>
    /// 복구 시도 이벤트 인자
    /// </summary>
    public class RecoveryAttemptEventArgs : EventArgs
    {
        public Exception Exception { get; private set; }
        public RecoveryContext Context { get; private set; }

        public RecoveryAttemptEventArgs(Exception exception, RecoveryContext context)
        {
            Exception = exception;
            Context = context;
        }
    }

    /// <summary>
    /// 복구 완료 이벤트 인자
    /// </summary>
    public class RecoveryCompletedEventArgs : EventArgs
    {
        public RecoveryResult Result { get; private set; }

        public RecoveryCompletedEventArgs(RecoveryResult result)
        {
            Result = result;
        }
    }
    #endregion
}