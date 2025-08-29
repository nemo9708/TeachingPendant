using System;

namespace TeachingPendant.UI.Views
{
    /// <summary>
    /// 로그 항목을 나타내는 모델 클래스
    /// ErrorLogViewer에서 로그 데이터를 표시하기 위해 사용
    /// </summary>
    public class LogEntry
    {
        #region Private Fields
        private DateTime _timeStamp;
        private string _level;
        private string _module;
        private string _method;
        private string _message;
        private string _exception;
        #endregion

        #region Public Properties
        /// <summary>
        /// 로그 기록 시간
        /// </summary>
        public DateTime TimeStamp
        {
            get { return _timeStamp; }
            set { _timeStamp = value; }
        }

        /// <summary>
        /// 로그 레벨 ERROR, WARNING, INFO, DEBUG
        /// </summary>
        public string Level
        {
            get { return _level ?? ""; }
            set { _level = value; }
        }

        /// <summary>
        /// 모듈명
        /// </summary>
        public string Module
        {
            get { return _module ?? ""; }
            set { _module = value; }
        }

        /// <summary>
        /// 메서드명
        /// </summary>
        public string Method
        {
            get { return _method ?? ""; }
            set { _method = value; }
        }

        /// <summary>
        /// 로그 메시지
        /// </summary>
        public string Message
        {
            get { return _message ?? ""; }
            set { _message = value; }
        }

        /// <summary>
        /// 예외 정보
        /// </summary>
        public string Exception
        {
            get { return _exception ?? ""; }
            set { _exception = value; }
        }

        /// <summary>
        /// UI 표시용 시간 문자열
        /// </summary>
        public string TimeStampDisplay
        {
            get { return _timeStamp.ToString("MM-dd HH:mm:ss"); }
        }

        /// <summary>
        /// 로그 레벨 우선순위 정렬용
        /// </summary>
        public int LevelPriority
        {
            get
            {
                if (string.IsNullOrEmpty(_level))
                    return 0;

                var upperLevel = _level.ToUpperInvariant();
                if (upperLevel == "ERROR")
                    return 4;
                if (upperLevel == "WARNING")
                    return 3;
                if (upperLevel == "INFO")
                    return 2;
                if (upperLevel == "DEBUG")
                    return 1;

                return 0;
            }
        }

        /// <summary>
        /// 미리보기용 요약 정보
        /// </summary>
        public string Summary
        {
            get
            {
                var msg = _message;
                if (!string.IsNullOrEmpty(msg) && msg.Length > 100)
                {
                    msg = msg.Substring(0, 100) + "...";
                }
                return $"[{_level}] {_module}.{_method}: {msg}";
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// 기본 생성자
        /// </summary>
        public LogEntry()
        {
            _timeStamp = DateTime.Now;
            _level = "";
            _module = "";
            _method = "";
            _message = "";
            _exception = "";
        }

        /// <summary>
        /// 매개변수 생성자
        /// </summary>
        /// <param name="timeStamp">로그 시간</param>
        /// <param name="level">로그 레벨</param>
        /// <param name="module">모듈명</param>
        /// <param name="method">메서드명</param>
        /// <param name="message">메시지</param>
        /// <param name="exception">예외 정보</param>
        public LogEntry(DateTime timeStamp, string level, string module, string method, string message, string exception = null)
        {
            _timeStamp = timeStamp;
            _level = level ?? "";
            _module = module ?? "";
            _method = method ?? "";
            _message = message ?? "";
            _exception = exception ?? "";
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 문자열 표현
        /// </summary>
        /// <returns>로그 항목의 문자열 표현</returns>
        public override string ToString()
        {
            var result = $"[{_timeStamp:yyyy-MM-dd HH:mm:ss}] [{_level}] [{_module}] [{_method}] {_message}";
            if (!string.IsNullOrEmpty(_exception))
            {
                result += $" [Exception: {_exception}]";
            }
            return result;
        }

        /// <summary>
        /// 두 LogEntry 객체가 동일한지 비교
        /// </summary>
        /// <param name="obj">비교 대상 객체</param>
        /// <returns>동일 여부</returns>
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            var other = (LogEntry)obj;
            return _timeStamp == other._timeStamp &&
                   _level == other._level &&
                   _module == other._module &&
                   _method == other._method &&
                   _message == other._message;
        }

        /// <summary>
        /// 해시코드 생성
        /// </summary>
        /// <returns>해시코드</returns>
        public override int GetHashCode()
        {
            // C# 6.0 호환 해시코드 생성
            int hash = 17;
            hash = hash * 31 + _timeStamp.GetHashCode();
            hash = hash * 31 + (_level?.GetHashCode() ?? 0);
            hash = hash * 31 + (_module?.GetHashCode() ?? 0);
            hash = hash * 31 + (_method?.GetHashCode() ?? 0);
            hash = hash * 31 + (_message?.GetHashCode() ?? 0);
            return hash;
        }
        #endregion
    }
}