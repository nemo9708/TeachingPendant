using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TeachingPendant.Logging
{
    /// <summary>
    /// 로깅 시스템 전체를 관리하는 클래스
    /// 애플리케이션 시작/종료, 설정 관리, 로그 파일 관리 등
    /// </summary>
    public static class LogManager
    {
        #region Private Fields
        private static bool _isConfigured = false;
        private static LogConfiguration _config;
        #endregion

        #region Configuration Class
        /// <summary>
        /// 로깅 시스템 설정 클래스
        /// </summary>
        public class LogConfiguration
        {
            /// <summary>
            /// 최소 로그 레벨 (기본값: Info)
            /// </summary>
            public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

            /// <summary>
            /// 로그 디렉토리 경로 (null이면 기본값 사용)
            /// </summary>
            public string LogDirectory { get; set; } = null;

            /// <summary>
            /// 최대 파일 크기 MB (기본값: 10MB)
            /// </summary>
            public int MaxFileSizeMB { get; set; } = 10;

            /// <summary>
            /// 파일 보관 일수 (기본값: 30일)
            /// </summary>
            public int MaxFileAgeDays { get; set; } = 30;

            /// <summary>
            /// 개발 모드 여부 (Debug 로그 포함)
            /// </summary>
            public bool DevelopmentMode { get; set; } = false;

            /// <summary>
            /// 콘솔 출력 여부 (Debug 빌드에서만 유효)
            /// </summary>
            public bool EnableConsoleOutput { get; set; } = true;

            /// <summary>
            /// 애플리케이션 시작/종료 시 자동 로그 기록 여부
            /// </summary>
            public bool LogApplicationLifecycle { get; set; } = true;
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 로깅 시스템 초기화 (MainWindow.xaml.cs에서 호출)
        /// </summary>
        /// <param name="config">로깅 설정 (null이면 기본값 사용)</param>
        public static void Initialize(LogConfiguration config = null)
        {
            if (_isConfigured)
            {
                System.Diagnostics.Debug.WriteLine("LogManager has already been initialized.");
                return;
            }

            try
            {
                // 기본 설정 사용
                _config = config ?? new LogConfiguration();

                // 개발 모드면 Debug 레벨까지 로깅
                if (_config.DevelopmentMode)
                {
                    _config.MinimumLevel = LogLevel.Debug;
                }

                // Logger 초기화
                Logger.Initialize(_config.MinimumLevel, _config.LogDirectory);

                _isConfigured = true;

                // 시스템 시작 로그
                if (_config.LogApplicationLifecycle)
                {
                    Logger.Info("System", "Startup", "TeachingPendant application starting");
                    Logger.Info("System", "Startup", $"Log settings - Level: {_config.MinimumLevel}, Directory: {Logger.GetLogDirectory()}");
                    Logger.Info("System", "Startup", $"Development Mode: {_config.DevelopmentMode}");
                }

                System.Diagnostics.Debug.WriteLine("LogManager initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogManager initialization failed: {ex.Message}");
                // 로깅 실패해도 애플리케이션은 정상 동작해야 함
            }
        }

        /// <summary>
        /// 로깅 시스템 종료 (MainWindow.xaml.cs에서 호출)
        /// </summary>
        public static void Shutdown()
        {
            if (!_isConfigured)
                return;

            try
            {
                if (_config.LogApplicationLifecycle)
                {
                    Logger.Info("System", "Shutdown", "TeachingPendant application shutting down");
                }

                Logger.Shutdown();
                _isConfigured = false;

                System.Diagnostics.Debug.WriteLine("LogManager shutdown complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during LogManager shutdown: {ex.Message}");
            }
        }
        #endregion

        #region File Management
        /// <summary>
        /// 로그 파일 목록 반환
        /// </summary>
        /// <returns>로그 파일 정보 목록</returns>
        public static List<LogFileInfo> GetLogFiles()
        {
            var logFiles = new List<LogFileInfo>();

            try
            {
                var logDirectory = Logger.GetLogDirectory();
                if (!Directory.Exists(logDirectory))
                    return logFiles;

                var files = Directory.GetFiles(logDirectory, "TeachingPendant_*.log")
                                     .OrderByDescending(f => new FileInfo(f).CreationTime);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    logFiles.Add(new LogFileInfo
                    {
                        FileName = fileInfo.Name,
                        FullPath = fileInfo.FullName,
                        CreationTime = fileInfo.CreationTime,
                        LastWriteTime = fileInfo.LastWriteTime,
                        SizeBytes = fileInfo.Length,
                        SizeMB = Math.Round(fileInfo.Length / (1024.0 * 1024.0), 2)
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("LogManager", "GetLogFiles", "Failed to retrieve log file list", ex);
            }

            return logFiles;
        }

        /// <summary>
        /// 로그 파일 내용 읽기
        /// </summary>
        /// <param name="filePath">로그 파일 경로</param>
        /// <param name="maxLines">최대 읽을 라인 수 (0이면 전체)</param>
        /// <returns>로그 내용</returns>
        public static string ReadLogFile(string filePath, int maxLines = 0)
        {
            try
            {
                if (!File.Exists(filePath))
                    return "File not found.";

                var lines = File.ReadAllLines(filePath);

                if (maxLines > 0 && lines.Length > maxLines)
                {
                    // 최신 라인들만 반환
                    lines = lines.Skip(lines.Length - maxLines).ToArray();
                }

                return string.Join(Environment.NewLine, lines);
            }
            catch (Exception ex)
            {
                Logger.Error("LogManager", "ReadLogFile", $"Failed to read log file: {filePath}", ex);
                return $"Failed to read file: {ex.Message}";
            }
        }

        /// <summary>
        /// 로그 파일 삭제
        /// </summary>
        /// <param name="filePath">삭제할 로그 파일 경로</param>
        /// <returns>삭제 성공 여부</returns>
        public static bool DeleteLogFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.Info("LogManager", "DeleteLogFile", $"Log file deleted: {Path.GetFileName(filePath)}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("LogManager", "DeleteLogFile", $"Failed to delete log file: {filePath}", ex);
                return false;
            }
        }

        /// <summary>
        /// 오래된 로그 파일 일괄 정리
        /// </summary>
        /// <param name="days">삭제할 파일의 기준 일수</param>
        /// <returns>삭제된 파일 수</returns>
        public static int CleanupOldLogs(int days = 30)
        {
            int deletedCount = 0;

            try
            {
                var logDirectory = Logger.GetLogDirectory();
                if (!Directory.Exists(logDirectory))
                    return 0;

                var cutoffDate = DateTime.Now.AddDays(-days);
                var files = Directory.GetFiles(logDirectory, "TeachingPendant_*.log");

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                }

                Logger.Info("LogManager", "CleanupOldLogs", $"Old log file cleanup complete: {deletedCount} files deleted");
            }
            catch (Exception ex)
            {
                Logger.Error("LogManager", "CleanupOldLogs", "Error during log file cleanup", ex);
            }

            return deletedCount;
        }
        #endregion

        #region Statistics
        /// <summary>
        /// 로그 통계 정보 반환
        /// </summary>
        /// <returns>로그 통계</returns>
        public static LogStatistics GetLogStatistics()
        {
            var stats = new LogStatistics();

            try
            {
                var logFiles = GetLogFiles();
                stats.TotalFiles = logFiles.Count;
                stats.TotalSizeMB = Math.Round(logFiles.Sum(f => f.SizeMB), 2);
                stats.OldestLogDate = logFiles.Count > 0 ? logFiles.Min(f => f.CreationTime) : DateTime.MinValue;
                stats.NewestLogDate = logFiles.Count > 0 ? logFiles.Max(f => f.LastWriteTime) : DateTime.MinValue;

                // 현재 설정 정보
                stats.CurrentLogLevel = Logger.MinimumLogLevel;
                stats.LogDirectory = Logger.GetLogDirectory();
                stats.CurrentLogFile = Logger.GetCurrentLogFilePath();
            }
            catch (Exception ex)
            {
                Logger.Error("LogManager", "GetLogStatistics", "Failed to retrieve log statistics", ex);
            }

            return stats;
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// 현재 로깅 설정 반환
        /// </summary>
        public static LogConfiguration GetCurrentConfiguration()
        {
            return _config ?? new LogConfiguration();
        }

        /// <summary>
        /// 로깅 시스템 초기화 상태 확인
        /// </summary>
        public static bool IsConfigured => _isConfigured;

        /// <summary>
        /// 개발 모드 여부 확인
        /// </summary>
        public static bool IsDevelopmentMode => _config?.DevelopmentMode ?? false;

        /// <summary>
        /// 로그 디렉토리 열기 (Windows 탐색기)
        /// </summary>
        public static void OpenLogDirectory()
        {
            try
            {
                var logDirectory = Logger.GetLogDirectory();
                if (Directory.Exists(logDirectory))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logDirectory);
                    Logger.Info("LogManager", "OpenLogDirectory", "Opening log directory");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("LogManager", "OpenLogDirectory", "Failed to open log directory", ex);
            }
        }
        #endregion
    }

    #region Support Classes
    /// <summary>
    /// 로그 파일 정보 클래스
    /// </summary>
    public class LogFileInfo
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public long SizeBytes { get; set; }
        public double SizeMB { get; set; }
    }

    /// <summary>
    /// 로그 통계 정보 클래스
    /// </summary>
    public class LogStatistics
    {
        public int TotalFiles { get; set; }
        public double TotalSizeMB { get; set; }
        public DateTime OldestLogDate { get; set; }
        public DateTime NewestLogDate { get; set; }
        public LogLevel CurrentLogLevel { get; set; }
        public string LogDirectory { get; set; }
        public string CurrentLogFile { get; set; }
    }
    #endregion
}