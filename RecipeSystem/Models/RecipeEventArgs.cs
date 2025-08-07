using System;
using TeachingPendant.RecipeSystem.Models;
using TeachingPendant.RecipeSystem.Engine;

namespace TeachingPendant.RecipeSystem.Models
{
    /// <summary>
    /// 레시피 상태 변경 이벤트 인수
    /// </summary>
    public class RecipeStateChangedEventArgs : EventArgs
    {
        public RecipeExecutionState OldState { get; }
        public RecipeExecutionState NewState { get; }

        public RecipeStateChangedEventArgs(RecipeExecutionState oldState, RecipeExecutionState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }

    /// <summary>
    /// 레시피 스텝 시작 이벤트 인수
    /// </summary>
    public class RecipeStepStartedEventArgs : EventArgs
    {
        public RecipeStep Step { get; }
        public int StepIndex { get; }

        public RecipeStepStartedEventArgs(RecipeStep step, int stepIndex)
        {
            Step = step;
            StepIndex = stepIndex;
        }
    }

    /// <summary>
    /// 레시피 스텝 완료 이벤트 인수
    /// </summary>
    public class RecipeStepCompletedEventArgs : EventArgs
    {
        public RecipeStep Step { get; }
        public int StepIndex { get; }
        public bool Success { get; }
        public string Message { get; }
        public TimeSpan? ExecutionTime { get; }

        public RecipeStepCompletedEventArgs(RecipeStep step, int stepIndex, bool success, string message, TimeSpan? executionTime = null)
        {
            Step = step;
            StepIndex = stepIndex;
            Success = success;
            Message = message;
            ExecutionTime = executionTime;
        }
    }

    /// <summary>
    /// 레시피 완료 이벤트 인수
    /// </summary>
    public class RecipeCompletedEventArgs : EventArgs
    {
        public TransferRecipe Recipe { get; }
        public bool Success { get; }
        public string Message { get; }
        public RecipeExecutionStatistics Statistics { get; }

        public RecipeCompletedEventArgs(TransferRecipe recipe, bool success, string message, RecipeExecutionStatistics statistics)
        {
            Recipe = recipe;
            Success = success;
            Message = message;
            Statistics = statistics;
        }
    }

    /// <summary>
    /// 레시피 진행 상황 이벤트 인수
    /// </summary>
    public class RecipeProgressEventArgs : EventArgs
    {
        public int CurrentStepIndex { get; set; }
        public int TotalSteps { get; set; }
        public double ProgressPercentage { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public RecipeStep CurrentStep { get; set; }
        public RecipeExecutionState ExecutionState { get; set; }
    }

    /// <summary>
    /// 레시피 오류 이벤트 인수
    /// </summary>
    public class RecipeErrorEventArgs : EventArgs
    {
        public string ErrorCode { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public RecipeStep CurrentStep { get; }
        public int StepIndex { get; }

        public RecipeErrorEventArgs(string errorCode, string message, Exception exception, RecipeStep currentStep, int stepIndex)
        {
            ErrorCode = errorCode;
            Message = message;
            Exception = exception;
            CurrentStep = currentStep;
            StepIndex = stepIndex;
        }
    }

    /// <summary>
    /// 레시피 실행 통계
    /// </summary>
    public class RecipeExecutionStatistics
    {
        public int TotalSteps { get; set; }
        public int ExecutedSteps { get; set; }
        public int ErrorCount { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public double SuccessRate => TotalSteps > 0 ? (double)(ExecutedSteps - ErrorCount) / TotalSteps * 100 : 0;
    }
}