using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using TeachingPendant.RecipeSystem.Models;
using TeachingPendant.HardwareControllers;
using TeachingPendant.Safety;
using TeachingPendant.Manager;
using TeachingPendant.Alarm;
using TeachingPendant.Teaching;
using TeachingPendant.WaferMapping;

namespace TeachingPendant.RecipeSystem.Engine
{
    /// <summary>
    /// 레시피 실행 상태
    /// </summary>
    public enum RecipeExecutionState
    {
        Idle,           // 대기 중
        Running,        // 실행 중
        Paused,         // 일시정지
        Stopping,       // 정지 중
        Completed,      // 완료
        Error,          // 오류
        Cancelled       // 취소됨
    }

    /// <summary>
    /// 레시피 실행 엔진
    /// IRobotController를 사용하여 레시피의 각 스텝을 순차적으로 실행
    /// </summary>
    public class RecipeEngine : IDisposable
    {
        #region Private Fields
        private IRobotController _robotController;
        private TransferRecipe _currentRecipe;
        private RecipeExecutionState _currentState = RecipeExecutionState.Idle;
        private int _currentStepIndex = 0;
        private bool _isPaused = false;
        private bool _isDisposed = false;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly object _lockObject = new object();
        private DispatcherTimer _statusUpdateTimer;

        // 실행 통계
        private DateTime _executionStartTime;
        private DateTime _currentStepStartTime;
        private int _totalStepsExecuted = 0;
        private int _totalErrorsOccurred = 0;
        #endregion

        #region Events
        /// <summary>
        /// 레시피 실행 상태가 변경될 때 발생
        /// </summary>
        public event EventHandler<RecipeStateChangedEventArgs> StateChanged;

        /// <summary>
        /// 스텝 실행이 시작될 때 발생
        /// </summary>
        public event EventHandler<RecipeStepStartedEventArgs> StepStarted;

        /// <summary>
        /// 스텝 실행이 완료될 때 발생
        /// </summary>
        public event EventHandler<RecipeStepCompletedEventArgs> StepCompleted;

        /// <summary>
        /// 레시피 실행이 완료될 때 발생
        /// </summary>
        public event EventHandler<RecipeCompletedEventArgs> RecipeCompleted;

        /// <summary>
        /// 레시피 실행 중 오류가 발생할 때 발생
        /// </summary>
        public event EventHandler<RecipeErrorEventArgs> RecipeError;

        /// <summary>
        /// 실행 진행 상황이 업데이트될 때 발생
        /// </summary>
        public event EventHandler<RecipeProgressEventArgs> ProgressUpdated;
        #endregion

        #region Properties
        /// <summary>
        /// 현재 실행 상태
        /// </summary>
        public RecipeExecutionState CurrentState
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentState;
                }
            }
            private set
            {
                lock (_lockObject)
                {
                    if (_currentState != value)
                    {
                        var oldState = _currentState;
                        _currentState = value;
                        OnStateChanged(oldState, value);
                    }
                }
            }
        }

        /// <summary>
        /// 현재 실행 중인 레시피
        /// </summary>
        public TransferRecipe CurrentRecipe
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentRecipe;
                }
            }
        }

        /// <summary>
        /// 현재 실행 중인 스텝 인덱스
        /// </summary>
        public int CurrentStepIndex
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentStepIndex;
                }
            }
        }

        /// <summary>
        /// 현재 실행 중인 스텝
        /// </summary>
        public RecipeStep CurrentStep
        {
            get
            {
                lock (_lockObject)
                {
                    if (_currentRecipe?.Steps != null &&
                        _currentStepIndex >= 0 &&
                        _currentStepIndex < _currentRecipe.Steps.Count)
                    {
                        return _currentRecipe.Steps[_currentStepIndex];
                    }
                    return null;
                }
            }
        }

        /// <summary>
        /// 실행 진행률 (0-100%)
        /// </summary>
        public double ProgressPercentage
        {
            get
            {
                lock (_lockObject)
                {
                    if (_currentRecipe?.Steps == null || _currentRecipe.Steps.Count == 0)
                        return 0;

                    return (double)_totalStepsExecuted / _currentRecipe.Steps.Count * 100.0;
                }
            }
        }

        /// <summary>
        /// 실행 경과 시간
        /// </summary>
        public TimeSpan ElapsedTime
        {
            get
            {
                if (_executionStartTime == default(DateTime))
                    return TimeSpan.Zero;

                return DateTime.Now - _executionStartTime;
            }
        }

        /// <summary>
        /// 로봇 컨트롤러 연결 상태
        /// </summary>
        public bool IsRobotConnected => _robotController?.IsConnected ?? false;
        #endregion

        #region Constructor & Initialization
        /// <summary>
        /// 기본 생성자
        /// </summary>
        public RecipeEngine()
        {
            InitializeEngine();
            System.Diagnostics.Debug.WriteLine("[RecipeEngine] 레시피 엔진 생성됨");
        }

        /// <summary>
        /// 로봇 컨트롤러를 지정하는 생성자
        /// </summary>
        /// <param name="robotController">사용할 로봇 컨트롤러</param>
        public RecipeEngine(IRobotController robotController) : this()
        {
            _robotController = robotController;
            System.Diagnostics.Debug.WriteLine("[RecipeEngine] 레시피 엔진 생성됨 (로봇 컨트롤러 지정)");
        }

        /// <summary>
        /// 엔진 초기화
        /// </summary>
        private void InitializeEngine()
        {
            // 상태 업데이트 타이머 설정
            _statusUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500) // 0.5초마다 업데이트
            };
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;

            // 기본 로봇 컨트롤러 설정 (팩토리에서 가져오기)
            try
            {
                _robotController = RobotControllerFactory.GetCurrentController();
                if (_robotController == null)
                {
                    _robotController = RobotControllerFactory.CreateController();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 로봇 컨트롤러 초기화 실패: {ex.Message}");
            }
        }
        #endregion

        #region Public Methods - Recipe Execution
        /// <summary>
        /// 레시피 실행 시작
        /// </summary>
        /// <param name="recipe">실행할 레시피</param>
        /// <returns>실행 시작 성공 여부</returns>
        public async Task<bool> ExecuteRecipeAsync(TransferRecipe recipe)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 레시피 실행 시작: {recipe?.RecipeName}");

                // 실행 전 검증
                if (!ValidateBeforeExecution(recipe))
                {
                    return false;
                }

                // 상태 초기화
                lock (_lockObject)
                {
                    _currentRecipe = recipe;
                    _currentStepIndex = 0;
                    _totalStepsExecuted = 0;
                    _totalErrorsOccurred = 0;
                    _isPaused = false;
                    _executionStartTime = DateTime.Now;
                }

                CurrentState = RecipeExecutionState.Running;
                _cancellationTokenSource = new CancellationTokenSource();
                _statusUpdateTimer.Start();

                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, $"레시피 실행 시작: {recipe.RecipeName}");

                // 비동기 실행
                _ = Task.Run(async () => await ExecuteRecipeInternalAsync(_cancellationTokenSource.Token));

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 레시피 실행 시작 실패: {ex.Message}");
                CurrentState = RecipeExecutionState.Error;
                OnRecipeError("EXECUTION_START_ERROR", $"레시피 실행 시작 실패: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 레시피 실행 일시정지
        /// </summary>
        public void PauseExecution()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_currentState == RecipeExecutionState.Running)
                    {
                        _isPaused = true;
                        CurrentState = RecipeExecutionState.Paused;
                        System.Diagnostics.Debug.WriteLine("[RecipeEngine] 레시피 실행 일시정지");
                        AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "레시피 실행이 일시정지되었습니다");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 일시정지 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 레시피 실행 재개
        /// </summary>
        public void ResumeExecution()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_currentState == RecipeExecutionState.Paused)
                    {
                        _isPaused = false;
                        CurrentState = RecipeExecutionState.Running;
                        System.Diagnostics.Debug.WriteLine("[RecipeEngine] 레시피 실행 재개");
                        AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "레시피 실행이 재개되었습니다");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 재개 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 레시피 실행 정지
        /// </summary>
        public async Task StopExecutionAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[RecipeEngine] 레시피 실행 정지 요청");

                CurrentState = RecipeExecutionState.Stopping;

                // 취소 토큰 활성화
                _cancellationTokenSource?.Cancel();

                // 로봇 정지
                if (_robotController != null)
                {
                    await _robotController.StopAsync();
                }

                CurrentState = RecipeExecutionState.Cancelled;
                _statusUpdateTimer.Stop();

                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "레시피 실행이 정지되었습니다");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 정지 실패: {ex.Message}");
                CurrentState = RecipeExecutionState.Error;
            }
        }

        /// <summary>
        /// 특정 스텝부터 실행 재시작
        /// </summary>
        /// <param name="stepIndex">시작할 스텝 인덱스</param>
        /// <returns>재시작 성공 여부</returns>
        public async Task<bool> RestartFromStepAsync(int stepIndex)
        {
            try
            {
                if (_currentRecipe == null || stepIndex < 0 || stepIndex >= _currentRecipe.Steps.Count)
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 스텝 {stepIndex}부터 재시작");

                // 현재 실행 정지
                await StopExecutionAsync();

                // 새로운 인덱스로 재시작
                lock (_lockObject)
                {
                    _currentStepIndex = stepIndex;
                    _totalStepsExecuted = stepIndex;
                }

                return await ExecuteRecipeAsync(_currentRecipe);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 스텝 재시작 실패: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Private Methods - Core Execution
        /// <summary>
        /// 레시피 실행 메인 루프
        /// </summary>
        /// <param name="cancellationToken">취소 토큰</param>
        private async Task ExecuteRecipeInternalAsync(CancellationToken cancellationToken)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[RecipeEngine] 레시피 실행 내부 루프 시작");

                while (_currentStepIndex < _currentRecipe.Steps.Count && !cancellationToken.IsCancellationRequested)
                {
                    // 일시정지 확인
                    while (_isPaused && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(100, cancellationToken);
                    }

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var currentStep = _currentRecipe.Steps[_currentStepIndex];

                    // 비활성화된 스텝 건너뛰기
                    if (!currentStep.IsEnabled)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 스텝 {_currentStepIndex + 1} 비활성화됨 - 건너뛰기");
                        _currentStepIndex++;
                        _totalStepsExecuted++;
                        continue;
                    }

                    // 스텝 실행
                    bool stepResult = await ExecuteStepAsync(currentStep, cancellationToken);

                    if (!stepResult)
                    {
                        // 스텝 실행 실패 처리
                        await HandleStepFailure(currentStep);

                        if (_currentRecipe.Parameters.PauseOnError)
                        {
                            PauseExecution();
                            return;
                        }
                    }

                    _currentStepIndex++;
                    _totalStepsExecuted++;
                }

                // 실행 완료 처리
                if (!cancellationToken.IsCancellationRequested)
                {
                    CurrentState = RecipeExecutionState.Completed;
                    OnRecipeCompleted(true, "레시피 실행이 성공적으로 완료되었습니다");
                    AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "레시피 실행 완료");
                }

                _statusUpdateTimer.Stop();
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[RecipeEngine] 레시피 실행이 취소됨");
                CurrentState = RecipeExecutionState.Cancelled;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 레시피 실행 중 오류: {ex.Message}");
                CurrentState = RecipeExecutionState.Error;
                OnRecipeError("EXECUTION_ERROR", "레시피 실행 중 오류 발생", ex);
            }
        }

        /// <summary>
        /// 개별 스텝 실행
        /// </summary>
        /// <param name="step">실행할 스텝</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>실행 성공 여부</returns>
        private async Task<bool> ExecuteStepAsync(RecipeStep step, CancellationToken cancellationToken)
        {
            try
            {
                _currentStepStartTime = DateTime.Now;
                OnStepStarted(step);

                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 스텝 실행 시작: {step.StepNumber} - {step.Type} - {step.Description}");

                // 안전 확인 (필요한 경우)
                if (_currentRecipe.Parameters.CheckSafetyBeforeEachStep)
                {
                    if (!CheckSafetyBeforeStep())
                    {
                        OnStepCompleted(step, false, "안전 조건 미충족");
                        return false;
                    }
                }

                // Teaching 좌표 로드 (필요한 경우)
                if (!string.IsNullOrEmpty(step.TeachingGroupName) && !string.IsNullOrEmpty(step.TeachingLocationName))
                {
                    step.LoadCoordinatesFromTeaching();
                }

                bool result = false;

                // 스텝 타입별 실행
                switch (step.Type)
                {
                    case StepType.Move:
                        result = await ExecuteMoveStep(step, cancellationToken);
                        break;

                    case StepType.Pick:
                        result = await ExecutePickStep(step, cancellationToken);
                        break;

                    case StepType.Place:
                        result = await ExecutePlaceStep(step, cancellationToken);
                        break;

                    case StepType.Home:
                        result = await ExecuteHomeStep(step, cancellationToken);
                        break;

                    case StepType.Wait:
                        result = await ExecuteWaitStep(step, cancellationToken);
                        break;

                    case StepType.CheckSafety:
                        result = await ExecuteSafetyCheckStep(step, cancellationToken);
                        break;

                    default:
                        System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 알 수 없는 스텝 타입: {step.Type}");
                        result = false;
                        break;
                }

                var executionTime = DateTime.Now - _currentStepStartTime;
                OnStepCompleted(step, result, result ? "성공" : "실패", executionTime);

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 스텝 실행 오류: {ex.Message}");
                var executionTime = DateTime.Now - _currentStepStartTime;
                OnStepCompleted(step, false, $"오류: {ex.Message}", executionTime);
                return false;
            }
        }

        /// <summary>
        /// Move 스텝 실행
        /// </summary>
        private async Task<bool> ExecuteMoveStep(RecipeStep step, CancellationToken cancellationToken)
        {
            try
            {
                if (_robotController == null)
                {
                    System.Diagnostics.Debug.WriteLine("[RecipeEngine] 로봇 컨트롤러가 없음");
                    return false;
                }

                var position = step.TargetPosition;
                int speed = step.Speed > 0 ? step.Speed : _currentRecipe.Parameters.DefaultSpeed;

                // GlobalSpeedManager에 속도 설정
                GlobalSpeedManager.SetSpeed(speed);

                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 이동 실행: R={position.R}, θ={position.Theta}, Z={position.Z}, 속도={speed}%");

                bool result = await _robotController.MoveToAsync(position.R, position.Theta, position.Z);

                if (result)
                {
                    System.Diagnostics.Debug.WriteLine("[RecipeEngine] 이동 완료");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[RecipeEngine] 이동 실패");
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] Move 스텝 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pick 스텝 실행
        /// </summary>
        private async Task<bool> ExecutePickStep(RecipeStep step, CancellationToken cancellationToken)
        {
            try
            {
                if (_robotController == null) return false;

                var position = step.TargetPosition;
                int speed = step.Speed > 0 ? step.Speed : _currentRecipe.Parameters.PickSpeed;

                // GlobalSpeedManager에 속도 설정
                GlobalSpeedManager.SetSpeed(speed);

                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] Pick 실행: {position}, 속도={speed}%");

                // 1. 안전 높이로 이동
                var safePos = new Position(position.R, position.Theta, _currentRecipe.Parameters.SafeHeight);
                if (!await _robotController.MoveToAsync(safePos.R, safePos.Theta, safePos.Z))
                {
                    return false;
                }

                // 2. Pick 위치로 이동
                if (!await _robotController.MoveToAsync(position.R, position.Theta, position.Z))
                {
                    return false;
                }

                // 3. Pick 동작 실행
                bool pickResult = await _robotController.PickAsync();

                if (pickResult)
                {
                    // 4. Pick 후 대기
                    if (_currentRecipe.Parameters.PickDelayMs > 0)
                    {
                        await Task.Delay(_currentRecipe.Parameters.PickDelayMs, cancellationToken);
                    }

                    // 5. 안전 높이로 상승
                    await _robotController.MoveToAsync(position.R, position.Theta, _currentRecipe.Parameters.SafeHeight);
                }

                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] Pick 완료: {(pickResult ? "성공" : "실패")}");
                return pickResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] Pick 스텝 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Place 스텝 실행
        /// </summary>
        private async Task<bool> ExecutePlaceStep(RecipeStep step, CancellationToken cancellationToken)
        {
            try
            {
                if (_robotController == null) return false;

                var position = step.TargetPosition;
                int speed = step.Speed > 0 ? step.Speed : _currentRecipe.Parameters.PlaceSpeed;

                // GlobalSpeedManager에 속도 설정
                GlobalSpeedManager.SetSpeed(speed);

                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] Place 실행: {position}, 속도={speed}%");

                // 1. 안전 높이로 이동
                var safePos = new Position(position.R, position.Theta, _currentRecipe.Parameters.SafeHeight);
                if (!await _robotController.MoveToAsync(safePos.R, safePos.Theta, safePos.Z))
                {
                    return false;
                }

                // 2. Place 위치로 이동
                if (!await _robotController.MoveToAsync(position.R, position.Theta, position.Z))
                {
                    return false;
                }

                // 3. Place 동작 실행
                bool placeResult = await _robotController.PlaceAsync();

                if (placeResult)
                {
                    // 4. Place 후 대기
                    if (_currentRecipe.Parameters.PlaceDelayMs > 0)
                    {
                        await Task.Delay(_currentRecipe.Parameters.PlaceDelayMs, cancellationToken);
                    }

                    // 5. 안전 높이로 상승
                    await _robotController.MoveToAsync(position.R, position.Theta, _currentRecipe.Parameters.SafeHeight);
                }

                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] Place 완료: {(placeResult ? "성공" : "실패")}");
                return placeResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] Place 스텝 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Home 스텝 실행
        /// </summary>
        private async Task<bool> ExecuteHomeStep(RecipeStep step, CancellationToken cancellationToken)
        {
            try
            {
                if (_robotController == null) return false;

                int speed = step.Speed > 0 ? step.Speed : _currentRecipe.Parameters.HomeSpeed;
                GlobalSpeedManager.SetSpeed(speed);

                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] Home 실행, 속도={speed}%");

                bool result = await _robotController.HomeAsync();

                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] Home 완료: {(result ? "성공" : "실패")}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] Home 스텝 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Wait 스텝 실행
        /// </summary>
        private async Task<bool> ExecuteWaitStep(RecipeStep step, CancellationToken cancellationToken)
        {
            try
            {
                int waitTime = step.WaitTimeMs > 0 ? step.WaitTimeMs : 1000;
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 대기 실행: {waitTime}ms");

                await Task.Delay(waitTime, cancellationToken);

                System.Diagnostics.Debug.WriteLine("[RecipeEngine] 대기 완료");
                return true;
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[RecipeEngine] 대기 취소됨");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] Wait 스텝 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 안전 확인 스텝 실행
        /// </summary>
        private async Task<bool> ExecuteSafetyCheckStep(RecipeStep step, CancellationToken cancellationToken)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[RecipeEngine] 안전 확인 실행");

                await Task.Delay(100); // 안전 시스템 상태 안정화 대기

                bool safetyResult = CheckSafetyBeforeStep();

                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 안전 확인 완료: {(safetyResult ? "안전" : "위험")}");
                return safetyResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 안전 확인 스텝 실행 오류: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// 실행 전 검증
        /// </summary>
        private bool ValidateBeforeExecution(TransferRecipe recipe)
        {
            try
            {
                if (recipe == null)
                {
                    OnRecipeError("VALIDATION_ERROR", "레시피가 null입니다", null);
                    return false;
                }

                if (_currentState == RecipeExecutionState.Running)
                {
                    OnRecipeError("VALIDATION_ERROR", "이미 다른 레시피가 실행 중입니다", null);
                    return false;
                }

                var validation = recipe.Validate();
                if (!validation.IsValid)
                {
                    string errorMessage = string.Join(", ", validation.ErrorMessages);
                    OnRecipeError("VALIDATION_ERROR", $"레시피 검증 실패: {errorMessage}", null);
                    return false;
                }

                if (_robotController == null)
                {
                    OnRecipeError("VALIDATION_ERROR", "로봇 컨트롤러가 초기화되지 않았습니다", null);
                    return false;
                }

                if (!_robotController.IsConnected)
                {
                    OnRecipeError("VALIDATION_ERROR", "로봇 컨트롤러가 연결되지 않았습니다", null);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                OnRecipeError("VALIDATION_ERROR", $"검증 중 오류 발생: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 스텝 실행 전 안전 확인
        /// </summary>
        private bool CheckSafetyBeforeStep()
        {
            try
            {
                return SafetySystem.IsSafeForRobotOperation();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 안전 확인 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 스텝 실행 실패 처리
        /// </summary>
        private async Task HandleStepFailure(RecipeStep step)
        {
            try
            {
                _totalErrorsOccurred++;

                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 스텝 실행 실패 처리: {step.Description}");

                // 재시도 로직
                int retryCount = _currentRecipe.Parameters.RetryCount;
                for (int i = 0; i < retryCount; i++)
                {
                    System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 스텝 재시도 {i + 1}/{retryCount}");

                    await Task.Delay(_currentRecipe.Parameters.RetryDelayMs);

                    bool retryResult = await ExecuteStepAsync(step, _cancellationTokenSource.Token);
                    if (retryResult)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 스텝 재시도 성공");
                        return;
                    }
                }

                OnRecipeError("STEP_FAILURE", $"스텝 실행 실패 (재시도 {retryCount}회 후): {step.Description}", null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 스텝 실패 처리 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 상태 업데이트 타이머 처리
        /// </summary>
        private void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                OnProgressUpdated();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 상태 업데이트 오류: {ex.Message}");
            }
        }
        #endregion

        #region Event Handlers
        private void OnStateChanged(RecipeExecutionState oldState, RecipeExecutionState newState)
        {
            try
            {
                StateChanged?.Invoke(this, new RecipeStateChangedEventArgs(oldState, newState));
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] 상태 변경: {oldState} → {newState}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] StateChanged 이벤트 오류: {ex.Message}");
            }
        }

        private void OnStepStarted(RecipeStep step)
        {
            try
            {
                StepStarted?.Invoke(this, new RecipeStepStartedEventArgs(step, _currentStepIndex));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] StepStarted 이벤트 오류: {ex.Message}");
            }
        }

        private void OnStepCompleted(RecipeStep step, bool success, string message, TimeSpan? executionTime = null)
        {
            try
            {
                StepCompleted?.Invoke(this, new RecipeStepCompletedEventArgs(step, _currentStepIndex, success, message, executionTime));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] StepCompleted 이벤트 오류: {ex.Message}");
            }
        }

        private void OnRecipeCompleted(bool success, string message)
        {
            try
            {
                var stats = new RecipeExecutionStatistics
                {
                    TotalSteps = _currentRecipe?.Steps?.Count ?? 0,
                    ExecutedSteps = _totalStepsExecuted,
                    ErrorCount = _totalErrorsOccurred,
                    TotalExecutionTime = ElapsedTime
                };

                RecipeCompleted?.Invoke(this, new RecipeCompletedEventArgs(_currentRecipe, success, message, stats));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] RecipeCompleted 이벤트 오류: {ex.Message}");
            }
        }

        private void OnRecipeError(string errorCode, string message, Exception exception)
        {
            try
            {
                RecipeError?.Invoke(this, new RecipeErrorEventArgs(errorCode, message, exception, CurrentStep, _currentStepIndex));
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] RecipeError 이벤트 오류: {ex.Message}");
            }
        }

        private void OnProgressUpdated()
        {
            try
            {
                ProgressUpdated?.Invoke(this, new RecipeProgressEventArgs
                {
                    CurrentStepIndex = _currentStepIndex,
                    TotalSteps = _currentRecipe?.Steps?.Count ?? 0,
                    ProgressPercentage = ProgressPercentage,
                    ElapsedTime = ElapsedTime,
                    CurrentStep = CurrentStep,
                    ExecutionState = CurrentState
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] ProgressUpdated 이벤트 오류: {ex.Message}");
            }
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            if (_isDisposed) return;

            try
            {
                _statusUpdateTimer?.Stop();
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();

                System.Diagnostics.Debug.WriteLine("[RecipeEngine] 레시피 엔진 정리됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeEngine] Dispose 오류: {ex.Message}");
            }

            _isDisposed = true;
        }
        #endregion
    }
}