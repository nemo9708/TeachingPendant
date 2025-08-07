using System;

namespace TeachingPendant.Logging
{
    /// <summary>
    /// 개별 로그 엔트리를 나타내는 데이터 클래스
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// 로그 발생 시간 (정확한 밀리초까지)
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 로그 레벨
        /// </summary>
        public LogLevel Level { get; set; }

        /// <summary>
        /// 발생한 모듈명 (예: "Teaching", "Movement", "Monitor")
        /// </summary>
        public string Module { get; set; }

        /// <summary>
        /// 발생한 메서드명 (예: "SaveCurrentData", "ConnectRobot")
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 로그 메시지
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 예외 정보 (오류 로그인 경우)
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 현재 스레드 ID
        /// </summary>
        public int ThreadId { get; set; }

        /// <summary>
        /// 추가 컨텍스트 정보 (선택사항)
        /// </summary>
        public string Context { get; set; }

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public LogEntry()
        {
            Timestamp = DateTime.Now;
            ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// 편의 생성자
        /// </summary>
        public LogEntry(LogLevel level, string module, string method, string message, Exception exception = null)
            : this()
        {
            Level = level;
            Module = module ?? "Unknown";
            Method = method ?? "Unknown";
            Message = message ?? "";
            Exception = exception;
        }

        /// <summary>
        /// 표준 로그 포맷으로 문자열 변환
        /// [2025-06-19 16:30:45.123] [INFO] [Teaching] [SaveCurrentData] Position saved: Group1-Cassette1 Slot=15
        /// </summary>
        public override string ToString()
        {
            var timestampStr = Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var levelStr = Level.ToShortString();
            var moduleStr = (Module ?? "Unknown").PadRight(12);
            var methodStr = (Method ?? "Unknown").PadRight(20);
            
            var baseLog = $"[{timestampStr}] [{levelStr}] [{moduleStr}] [{methodStr}] {Message}";

            // 예외 정보가 있으면 추가
            if (Exception != null)
            {
                baseLog += $"\n    Exception: {Exception.GetType().Name}: {Exception.Message}";
                
                // 중요한 예외의 경우 스택 트레이스도 포함
                if (Level >= LogLevel.Error)
                {
                    baseLog += $"\n    StackTrace: {Exception.StackTrace}";
                }
            }

            // 컨텍스트 정보가 있으면 추가
            if (!string.IsNullOrEmpty(Context))
            {
                baseLog += $"\n    Context: {Context}";
            }

            return baseLog;
        }

        /// <summary>
        /// UI 표시용 간단한 포맷
        /// 16:30:45 [INFO] Teaching: Position saved
        /// </summary>
        public string ToUIString()
        {
            var timeStr = Timestamp.ToString("HH:mm:ss");
            var levelStr = Level.ToShortString().Trim();
            
            return $"{timeStr} [{levelStr}] {Module}: {Message}";
        }

        /// <summary>
        /// CSV 형태로 변환 (로그 내보내기용)
        /// </summary>
        public string ToCsvString()
        {
            var timestamp = Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var level = Level.ToString();
            var module = EscapeCsv(Module ?? "");
            var method = EscapeCsv(Method ?? "");
            var message = EscapeCsv(Message ?? "");
            var exceptionInfo = Exception != null ? EscapeCsv(Exception.Message) : "";
            
            return $"{timestamp},{level},{module},{method},{message},{exceptionInfo},{ThreadId}";
        }

        /// <summary>
        /// CSV용 문자열 이스케이프
        /// </summary>
        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            // 콤마, 따옴표, 줄바꿈이 있으면 따옴표로 감싸고 내부 따옴표는 두 개로 변경
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        /// <summary>
        /// CSV 헤더 반환
        /// </summary>
        public static string GetCsvHeader()
        {
            return "Timestamp,Level,Module,Method,Message,Exception,ThreadId";
        }
    }
}