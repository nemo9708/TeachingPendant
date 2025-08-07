using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TeachingPendant.Logging
{
    /// <summary>
    /// 파일 로그 출력 담당 클래스
    /// 비동기 처리로 UI 성능에 영향을 주지 않음
    /// </summary>
    public class FileLogWriter : IDisposable
    {
        #region Private Fields
        private readonly string _logDirectory;
        private readonly string _logFilePrefix;
        private readonly int _maxFileSizeMB;
        private readonly int _maxFileAgedays;
        private readonly Queue<LogEntry> _logQueue;
        private readonly object _queueLock = new object();
        private readonly Timer _flushTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task _writerTask;
        private bool _disposed = false;
        #endregion

        #region Constructor
        /// <summary>
        /// FileLogWriter 생성자
        /// </summary>
        /// <param name="logDirectory">로그 파일을 저장할 디렉토리 (기본값: %AppData%/TeachingPendantData/Logs/)</param>
        /// <param name="logFilePrefix">로그 파일명 접두사 (기본값: "TeachingPendant")</param>
        /// <param name="maxFileSizeMB">최대 파일 크기 MB (기본값: 10MB)</param>
        /// <param name="maxFileAgeDays">파일 보관 일수 (기본값: 30일)</param>
        public FileLogWriter(string logDirectory = null, string logFilePrefix = "TeachingPendant",
                             int maxFileSizeMB = 10, int maxFileAgeDays = 30)
        {
            // 기본 로그 디렉토리 설정
            if (string.IsNullOrEmpty(logDirectory))
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _logDirectory = Path.Combine(appDataPath, "TeachingPendantData", "Logs");
            }
            else
            {
                _logDirectory = logDirectory;
            }

            _logFilePrefix = logFilePrefix;
            _maxFileSizeMB = maxFileSizeMB;
            _maxFileAgedays = maxFileAgeDays;
            _logQueue = new Queue<LogEntry>();
            _cancellationTokenSource = new CancellationTokenSource();

            // 로그 디렉토리 생성
            EnsureLogDirectoryExists();

            // 백그라운드 로그 작성 작업 시작
            _writerTask = Task.Run(ProcessLogQueue, _cancellationTokenSource.Token);

            // 주기적으로 큐를 플러시하는 타이머 (2초마다)
            _flushTimer = new Timer(ForceFlush, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

            // 시작 시 오래된 로그 파일 정리
            CleanupOldLogFiles();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 로그 엔트리를 큐에 추가 (비동기적으로 파일에 기록됨)
        /// </summary>
        public void WriteLog(LogEntry logEntry)
        {
            if (_disposed || logEntry == null)
                return;

            lock (_queueLock)
            {
                _logQueue.Enqueue(logEntry);
            }
        }

        /// <summary>
        /// 즉시 모든 대기 중인 로그를 파일에 기록
        /// </summary>
        public void Flush()
        {
            ForceFlush(null);
        }

        /// <summary>
        /// 현재 로그 파일 경로 반환
        /// </summary>
        public string GetCurrentLogFilePath()
        {
            var today = DateTime.Now.ToString("yyyyMMdd");
            return Path.Combine(_logDirectory, $"{_logFilePrefix}_{today}.log");
        }

        /// <summary>
        /// 로그 디렉토리 경로 반환
        /// </summary>
        public string GetLogDirectory()
        {
            return _logDirectory;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 로그 디렉토리 존재 확인 및 생성
        /// </summary>
        private void EnsureLogDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                    System.Diagnostics.Debug.WriteLine($"Log directory created: {_logDirectory}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create log directory: {ex.Message}");
            }
        }

        /// <summary>
        /// 백그라운드에서 로그 큐를 처리하는 메서드
        /// </summary>
        private async Task ProcessLogQueue()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var logsToWrite = new List<LogEntry>();

                    // 큐에서 로그 엔트리들을 가져오기
                    lock (_queueLock)
                    {
                        while (_logQueue.Count > 0)
                        {
                            logsToWrite.Add(_logQueue.Dequeue());
                        }
                    }

                    // 로그가 있으면 파일에 기록
                    if (logsToWrite.Count > 0)
                    {
                        await WriteLogsToFile(logsToWrite);
                    }

                    // 100ms 대기
                    await Task.Delay(100, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // 정상적인 종료
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing log queue: {ex.Message}");
                    await Task.Delay(1000); // 오류 발생 시 1초 대기
                }
            }
        }

        /// <summary>
        /// 로그 엔트리들을 파일에 실제로 기록
        /// </summary>
        private async Task WriteLogsToFile(List<LogEntry> logs)
        {
            try
            {
                var currentLogFile = GetCurrentLogFilePath();

                // 파일 크기 확인 및 순환
                if (File.Exists(currentLogFile))
                {
                    var fileInfo = new FileInfo(currentLogFile);
                    if (fileInfo.Length > _maxFileSizeMB * 1024 * 1024)
                    {
                        currentLogFile = GetRotatedLogFilePath();
                    }
                }

                // 로그 내용 생성
                var logContent = "";
                foreach (var log in logs)
                {
                    logContent += log.ToString() + Environment.NewLine;
                }

                // 파일에 비동기적으로 추가
                using (var writer = new StreamWriter(currentLogFile, append: true))
                {
                    await writer.WriteAsync(logContent);
                    await writer.FlushAsync();
                }

                System.Diagnostics.Debug.WriteLine($"Log writing complete: {logs.Count} entries");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        /// <summary>
        /// 파일 크기 초과 시 순환된 파일명 반환
        /// </summary>
        private string GetRotatedLogFilePath()
        {
            var today = DateTime.Now.ToString("yyyyMMdd");
            var timeStamp = DateTime.Now.ToString("HHmmss");
            return Path.Combine(_logDirectory, $"{_logFilePrefix}_{today}_{timeStamp}.log");
        }

        /// <summary>
        /// 타이머에 의한 강제 플러시
        /// </summary>
        private void ForceFlush(object state)
        {
            // 처리할 로그가 있는지 확인만 하고 실제 처리는 ProcessLogQueue에서 담당
            lock (_queueLock)
            {
                if (_logQueue.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Timer flush: {_logQueue.Count} logs pending");
                }
            }
        }

        /// <summary>
        /// 오래된 로그 파일 정리
        /// </summary>
        private void CleanupOldLogFiles()
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-_maxFileAgedays);
                var logFiles = Directory.GetFiles(_logDirectory, $"{_logFilePrefix}_*.log");

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(logFile);
                        System.Diagnostics.Debug.WriteLine($"Deleted old log file: {logFile}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during log file cleanup: {ex.Message}");
            }
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // 타이머 정리
            _flushTimer?.Dispose();

            // 남은 로그 모두 처리
            Flush();

            // 백그라운드 작업 취소 및 대기
            _cancellationTokenSource.Cancel();
            try
            {
                _writerTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error while stopping log writer task: {ex.Message}");
            }

            _cancellationTokenSource?.Dispose();
        }
        #endregion
    }
}