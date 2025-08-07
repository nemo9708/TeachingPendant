using System;

namespace TeachingPendant.Logging
{
    /// <summary>
    /// 로그 레벨 정의
    /// 숫자가 높을수록 더 중요한 로그
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 개발 시에만 사용하는 상세한 디버깅 정보
        /// 예: 변수 값, 메서드 진입/종료, 내부 상태
        /// </summary>
        Debug = 0,
        
        /// <summary>
        /// 일반적인 정보성 로그
        /// 예: 사용자 액션, 시스템 상태 변경, 정상 작업 완료
        /// </summary>
        Info = 1,
        
        /// <summary>
        /// 경고 - 비정상적이지만 시스템은 계속 실행 가능
        /// 예: 잘못된 입력값, 예상치 못한 상태, 성능 저하
        /// </summary>
        Warning = 2,
        
        /// <summary>
        /// 오류 - 기능 실패, 예외 발생하지만 전체 시스템은 동작
        /// 예: 파일 저장 실패, 네트워크 연결 실패, 데이터 파싱 오류
        /// </summary>
        Error = 3,
        
        /// <summary>
        /// 치명적 오류 - 시스템 전체에 심각한 영향
        /// 예: 메모리 부족, 하드웨어 연결 끊김, 복구 불가능한 상태
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// LogLevel 확장 메서드
    /// </summary>
    public static class LogLevelExtensions
    {
        /// <summary>
        /// LogLevel을 문자열로 변환 (고정 길이 5자리)
        /// </summary>
        public static string ToShortString(this LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return "DEBUG";
                case LogLevel.Info:
                    return "INFO ";
                case LogLevel.Warning:
                    return "WARN ";
                case LogLevel.Error:
                    return "ERROR";
                case LogLevel.Critical:
                    return "CRIT ";
                default:
                    return "UNKN ";
            }
        }

        /// <summary>
        /// LogLevel에 따른 색상 코드 반환 (UI 표시용)
        /// </summary>
        public static string GetColorCode(this LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return "#808080";    // 회색
                case LogLevel.Info:
                    return "#000000";    // 검정
                case LogLevel.Warning:
                    return "#FF8C00";    // 주황색
                case LogLevel.Error:
                    return "#FF0000";    // 빨강
                case LogLevel.Critical:
                    return "#8B0000";    // 어두운 빨강
                default:
                    return "#000000";
            }
        }

        /// <summary>
        /// LogLevel 우선순위 비교
        /// </summary>
        public static bool IsEqualOrHigher(this LogLevel level, LogLevel compareLevel)
        {
            return (int)level >= (int)compareLevel;
        }
    }
}