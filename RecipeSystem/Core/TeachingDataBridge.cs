using System;
using TeachingPendant.RecipeSystem.Models;
using TeachingPendant.RecipeSystem.Core;

namespace TeachingPendant.RecipeSystem.Core
{
    /// <summary>
    /// 레시피 시스템 상태 변경 이벤트 인자
    /// </summary>
    public class RecipeSystemStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 새로운 상태
        /// </summary>
        public RecipeSystemStatus NewStatus { get; }

        /// <summary>
        /// 상태 변경 시간
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="newStatus">새로운 상태</param>
        public RecipeSystemStatusChangedEventArgs(RecipeSystemStatus newStatus)
        {
            NewStatus = newStatus;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 레시피 스텝 실행 이벤트 인자
    /// </summary>
    public class RecipeStepExecutionEventArgs : EventArgs
    {
        /// <summary>
        /// 실행된 스텝
        /// </summary>
        public RecipeStep Step { get; }

        /// <summary>
        /// 스텝 인덱스
        /// </summary>
        public int StepIndex { get; }

        /// <summary>
        /// 실행 시간
        /// </summary>
        public DateTime ExecutionTime { get; }

        /// <summary>
        /// 실행 성공 여부 (완료 이벤트에서만 사용)
        /// </summary>
        public bool? Success { get; }

        /// <summary>
        /// 생성자 (시작 이벤트용)
        /// </summary>
        /// <param name="step">실행 스텝</param>
        /// <param name="stepIndex">스텝 인덱스</param>
        /// <param name="executionTime">실행 시간</param>
        public RecipeStepExecutionEventArgs(RecipeStep step, int stepIndex, DateTime executionTime)
        {
            Step = step;
            StepIndex = stepIndex;
            ExecutionTime = executionTime;
            Success = null;
        }

        /// <summary>
        /// 생성자 (완료 이벤트용)
        /// </summary>
        /// <param name="step">실행 스텝</param>
        /// <param name="stepIndex">스텝 인덱스</param>
        /// <param name="executionTime">실행 시간</param>
        /// <param name="success">실행 성공 여부</param>
        public RecipeStepExecutionEventArgs(RecipeStep step, int stepIndex, DateTime executionTime, bool success)
        {
            Step = step;
            StepIndex = stepIndex;
            ExecutionTime = executionTime;
            Success = success;
        }
    }

    /// <summary>
    /// 레시피 실행 완료 이벤트 인자
    /// </summary>
    public class RecipeExecutionCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// 실행 성공 여부
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// 오류 메시지 (실패 시)
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 완료 시간
        /// </summary>
        public DateTime CompletedTime { get; }

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="success">실행 성공 여부</param>
        /// <param name="errorMessage">오류 메시지</param>
        public RecipeExecutionCompletedEventArgs(bool success, string errorMessage = null)
        {
            Success = success;
            ErrorMessage = errorMessage;
            CompletedTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 레시피 오류 이벤트 인자
    /// </summary>
    public class RecipeErrorEventArgs : EventArgs
    {
        /// <summary>
        /// 오류 코드
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// 오류 메시지
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 예외 정보
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 오류 발생 시간
        /// </summary>
        public DateTime ErrorTime { get; }

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="errorCode">오류 코드</param>
        /// <param name="errorMessage">오류 메시지</param>
        /// <param name="exception">예외 정보</param>
        public RecipeErrorEventArgs(string errorCode, string errorMessage, Exception exception = null)
        {
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            Exception = exception;
            ErrorTime = DateTime.Now;
        }
    }
}