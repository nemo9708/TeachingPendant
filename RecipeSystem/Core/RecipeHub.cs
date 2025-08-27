using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using TeachingPendant.RecipeSystem.Models;
using TeachingPendant.RecipeSystem.Engine;
using TeachingPendant.HardwareControllers;
using TeachingPendant.Teaching;
using TeachingPendant.Logging;
using TeachingPendant.Alarm;

namespace TeachingPendant.RecipeSystem.Core
{
    /// <summary>
    /// 레시피 시스템 상태
    /// </summary>
    public enum RecipeSystemStatus
    {
        /// <summary>
        /// 대기 중
        /// </summary>
        Idle,

        /// <summary>
        /// 레시피 로딩 중
        /// </summary>
        Loading,

        /// <summary>
        /// 실행 준비됨
        /// </summary>
        Ready,

        /// <summary>
        /// 실행 중
        /// </summary>
        Executing,

        /// <summary>
        /// 일시정지
        /// </summary>
        Paused,

        /// <summary>
        /// 오류 상태
        /// </summary>
        Error,

        /// <summary>
        /// 실행 완료
        /// </summary>
        Completed
    }

    /// <summary>
    /// 레시피 시스템 중앙 관리자
    /// 모든 레시피 관련 컴포넌트를 통합하고 하드웨어와 연동
    /// </summary>
    public class RecipeHub : INotifyPropertyChanged, IDisposable
    {
        #region Private Fields
        private static readonly string CLASS_NAME = "RecipeHub";
        private static RecipeHub _instance;
        private static readonly object _lock = new object();

        private TransferRecipe _activeRecipe;
        private RecipeSystemStatus _status = RecipeSystemStatus.Idle;
        private IRobotController _robotController;
        private RecipeEngine _recipeEngine;
        private bool _isHardwareConnected = false;
        private string _statusMessage = "시스템 준비";
        private int _currentStepIndex = 0;
        private int _totalSteps = 0;
        private double _executionProgress = 0.0;
        private bool _isDisposed = false;

        // Teaching 시스템 연동
        private ITeachingDataProvider _teachingDataProvider;

        // 실행 통계
        private DateTime _executionStartTime;
        private int _completedSteps = 0;
        private int _errorCount = 0;
        #endregion

        #region Singleton Pattern
        /// <summary>
        /// RecipeHub 싱글톤 인스턴스
        /// </summary>
        public static RecipeHub Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new RecipeHub();
                        }
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// RecipeHub 생성자 (private - 싱글톤)
        /// </summary>
        private RecipeHub()
        {
            InitializeHub();
        }
        #endregion

        #region Public Properties
        /// <summary>
        /// 현재 활성 레시피
        /// </summary>
        public TransferRecipe ActiveRecipe
        {
            get => _activeRecipe;
            private set
            {
                if (_activeRecipe != value)
                {
                    _activeRecipe = value;
                    OnPropertyChanged(nameof(ActiveRecipe));
                    UpdateTotalSteps();
                }
            }
        }

        /// <summary>
        /// 시스템 상태
        /// </summary>
        public RecipeSystemStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                    OnStatusChanged(new RecipeSystemStatusChangedEventArgs(_status));
                }
            }
        }

        /// <summary>
        /// 하드웨어 연결 상태
        /// </summary>
        public bool IsHardwareConnected
        {
            get => _isHardwareConnected;
            private set
            {
                if (_isHardwareConnected != value)
                {
                    _isHardwareConnected = value;
                    OnPropertyChanged(nameof(IsHardwareConnected));
                }
            }
        }

        /// <summary>
        /// 상태 메시지
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged(nameof(StatusMessage));
                }
            }
        }

        /// <summary>
        /// 현재 실행 중인 스텝 인덱스
        /// </summary>
        public int CurrentStepIndex
        {
            get => _currentStepIndex;
            private set
            {
                if (_currentStepIndex != value)
                {
                    _currentStepIndex = value;
                    OnPropertyChanged(nameof(CurrentStepIndex));
                    UpdateExecutionProgress();
                }
            }
        }

        /// <summary>
        /// 전체 스텝 수
        /// </summary>
        public int TotalSteps
        {
            get => _totalSteps;
            private set
            {
                if (_totalSteps != value)
                {
                    _totalSteps = value;
                    OnPropertyChanged(nameof(TotalSteps));
                    UpdateExecutionProgress();
                }
            }
        }

        /// <summary>
        /// 실행 진행률 (0.0 ~ 1.0)
        /// </summary>
        public double ExecutionProgress
        {
            get => _executionProgress;
            private set
            {
                if (Math.Abs(_executionProgress - value) > 0.001)
                {
                    _executionProgress = value;
                    OnPropertyChanged(nameof(ExecutionProgress));
                }
            }
        }

        /// <summary>
        /// 실행 가능 상태
        /// </summary>
        public bool CanExecute => ActiveRecipe != null &&
                                  IsHardwareConnected &&
                                  (Status == RecipeSystemStatus.Ready || Status == RecipeSystemStatus.Paused);

        /// <summary>
        /// 일시정지 가능 상태
        /// </summary>
        public bool CanPause => Status == RecipeSystemStatus.Executing;

        /// <summary>
        /// 정지 가능 상태
        /// </summary>
        public bool CanStop => Status == RecipeSystemStatus.Executing || Status == RecipeSystemStatus.Paused;
        #endregion

        #region Events
        /// <summary>
        /// 시스템 상태 변경 이벤트
        /// </summary>
        public event EventHandler<RecipeSystemStatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// 스텝 실행 시작 이벤트
        /// </summary>
        public event EventHandler<RecipeStepExecutionEventArgs> StepExecutionStarted;

        /// <summary>
        /// 스텝 실행 완료 이벤트
        /// </summary>
        public event EventHandler<RecipeStepExecutionEventArgs> StepExecutionCompleted;

        /// <summary>
        /// 레시피 실행 완료 이벤트
        /// </summary>
        public event EventHandler<RecipeExecutionCompletedEventArgs> ExecutionCompleted;

        /// <summary>
        /// 오류 발생 이벤트
        /// </summary>
        public event EventHandler<RecipeErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// PropertyChanged 이벤트
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Public Methods
        /// <summary>
        /// 레시피 허브 초기화
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                Logger.Info(CLASS_NAME, "InitializeAsync", "레시피 허브 초기화 시작");

                // 하드웨어 연결 확인
                await CheckHardwareConnection();

                // Teaching 시스템 연동 초기화
                InitializeTeachingIntegration();

                // 레시피 엔진 초기화
                InitializeRecipeEngine();

                Status = RecipeSystemStatus.Idle;
                StatusMessage = "레시피 시스템 준비 완료";

                Logger.Info(CLASS_NAME, "InitializeAsync", "레시피 허브 초기화 완료");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "InitializeAsync", "레시피 허브 초기화 실패", ex);
                Status = RecipeSystemStatus.Error;
                StatusMessage = "초기화 실패";
                return false;
            }
        }

        /// <summary>
        /// 레시피 로드
        /// </summary>
        /// <param name="recipe">로드할 레시피</param>
        public async Task<bool> LoadRecipeAsync(TransferRecipe recipe)
        {
            try
            {
                if (recipe == null)
                {
                    throw new ArgumentNullException(nameof(recipe));
                }

                Logger.Info(CLASS_NAME, "LoadRecipeAsync", $"레시피 로드: {recipe.RecipeName}");
                Status = RecipeSystemStatus.Loading;
                StatusMessage = $"레시피 로딩 중: {recipe.RecipeName}";

                // 기존 실행 중단
                if (Status == RecipeSystemStatus.Executing)
                {
                    await StopExecutionAsync();
                }

                // 레시피 검증
                if (!await ValidateRecipeAsync(recipe))
                {
                    throw new InvalidOperationException("레시피 검증 실패");
                }

                // 레시피 설정
                ActiveRecipe = recipe;
                CurrentStepIndex = 0;
                _completedSteps = 0;
                _errorCount = 0;

                Status = RecipeSystemStatus.Ready;
                StatusMessage = $"레시피 준비됨: {recipe.RecipeName}";

                Logger.Info(CLASS_NAME, "LoadRecipeAsync", $"레시피 로드 완료: {recipe.RecipeName}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "LoadRecipeAsync", "레시피 로드 실패", ex);
                Status = RecipeSystemStatus.Error;
                StatusMessage = "레시피 로드 실패";
                OnErrorOccurred(new RecipeErrorEventArgs("RECIPE_LOAD_ERROR", ex.Message, ex));
                return false;
            }
        }

        /// <summary>
        /// 레시피 실행 시작
        /// </summary>
        public async Task<bool> StartExecutionAsync()
        {
            try
            {
                if (!CanExecute)
                {
                    Logger.Warning(CLASS_NAME, "StartExecutionAsync", "실행 조건이 충족되지 않음");
                    return false;
                }

                Logger.Info(CLASS_NAME, "StartExecutionAsync", $"레시피 실행 시작: {ActiveRecipe.RecipeName}");

                Status = RecipeSystemStatus.Executing;
                StatusMessage = "레시피 실행 중";
                _executionStartTime = DateTime.Now;

                // 레시피 엔진으로 실행
                var success = await _recipeEngine.ExecuteRecipeAsync(ActiveRecipe, CancellationToken.None);

                if (success)
                {
                    Status = RecipeSystemStatus.Completed;
                    StatusMessage = "레시피 실행 완료";
                    OnExecutionCompleted(new RecipeExecutionCompletedEventArgs(true, null));
                }
                else
                {
                    Status = RecipeSystemStatus.Error;
                    StatusMessage = "레시피 실행 실패";
                    OnExecutionCompleted(new RecipeExecutionCompletedEventArgs(false, "실행 중 오류 발생"));
                }

                Logger.Info(CLASS_NAME, "StartExecutionAsync", $"레시피 실행 완료: {success}");
                return success;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "StartExecutionAsync", "레시피 실행 실패", ex);
                Status = RecipeSystemStatus.Error;
                StatusMessage = "실행 중 오류 발생";
                OnErrorOccurred(new RecipeErrorEventArgs("RECIPE_EXECUTION_ERROR", ex.Message, ex));
                return false;
            }
        }

        /// <summary>
        /// 레시피 실행 일시정지
        /// </summary>
        public async Task<bool> PauseExecutionAsync()
        {
            try
            {
                if (!CanPause)
                {
                    Logger.Warning(CLASS_NAME, "PauseExecutionAsync", "일시정지 조건이 충족되지 않음");
                    return false;
                }

                Logger.Info(CLASS_NAME, "PauseExecutionAsync", "레시피 실행 일시정지");

                var success = await _recipeEngine.PauseExecutionAsync();
                if (success)
                {
                    Status = RecipeSystemStatus.Paused;
                    StatusMessage = "레시피 실행 일시정지됨";
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "PauseExecutionAsync", "일시정지 실패", ex);
                OnErrorOccurred(new RecipeErrorEventArgs("RECIPE_PAUSE_ERROR", ex.Message, ex));
                return false;
            }
        }

        /// <summary>
        /// 레시피 실행 재개
        /// </summary>
        public async Task<bool> ResumeExecutionAsync()
        {
            try
            {
                if (Status != RecipeSystemStatus.Paused)
                {
                    Logger.Warning(CLASS_NAME, "ResumeExecutionAsync", "재개 조건이 충족되지 않음");
                    return false;
                }

                Logger.Info(CLASS_NAME, "ResumeExecutionAsync", "레시피 실행 재개");

                var success = await _recipeEngine.ResumeExecutionAsync();
                if (success)
                {
                    Status = RecipeSystemStatus.Executing;
                    StatusMessage = "레시피 실행 중";
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ResumeExecutionAsync", "재개 실패", ex);
                OnErrorOccurred(new RecipeErrorEventArgs("RECIPE_RESUME_ERROR", ex.Message, ex));
                return false;
            }
        }

        /// <summary>
        /// 레시피 실행 정지
        /// </summary>
        public async Task<bool> StopExecutionAsync()
        {
            try
            {
                if (!CanStop)
                {
                    Logger.Warning(CLASS_NAME, "StopExecutionAsync", "정지 조건이 충족되지 않음");
                    return false;
                }

                Logger.Info(CLASS_NAME, "StopExecutionAsync", "레시피 실행 정지");

                var success = await _recipeEngine.StopExecutionAsync();
                if (success)
                {
                    Status = RecipeSystemStatus.Idle;
                    StatusMessage = "레시피 실행 정지됨";
                    CurrentStepIndex = 0;
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "StopExecutionAsync", "정지 실패", ex);
                OnErrorOccurred(new RecipeErrorEventArgs("RECIPE_STOP_ERROR", ex.Message, ex));
                return false;
            }
        }

        /// <summary>
        /// Teaching 좌표 가져오기
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <param name="locationName">위치명</param>
        /// <returns>Position 좌표</returns>
        public Position GetTeachingPosition(string groupName, string locationName)
        {
            try
            {
                return _teachingDataProvider?.GetPosition(groupName, locationName) ??
                       new Position(100, 0, 50); // 기본 안전 위치
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "GetTeachingPosition", $"Teaching 좌표 가져오기 실패: {groupName}.{locationName}", ex);
                return new Position(100, 0, 50); // 기본 안전 위치
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 허브 초기화
        /// </summary>
        private void InitializeHub()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 허브 초기화 시작");

                Status = RecipeSystemStatus.Idle;
                StatusMessage = "초기화 중";

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 허브 초기화 완료");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "InitializeHub", "허브 초기화 실패", ex);
            }
        }

        /// <summary>
        /// 하드웨어 연결 확인
        /// </summary>
        private async Task CheckHardwareConnection()
        {
            try
            {
                // RobotControllerFactory를 통해 현재 컨트롤러 가져오기
                _robotController = RobotControllerFactory.GetCurrentController();

                if (_robotController != null)
                {
                    IsHardwareConnected = _robotController.IsConnected;
                    Logger.Info(CLASS_NAME, "CheckHardwareConnection", $"로봇 컨트롤러 연결 상태: {IsHardwareConnected}");
                }
                else
                {
                    IsHardwareConnected = false;
                    Logger.Warning(CLASS_NAME, "CheckHardwareConnection", "로봇 컨트롤러를 찾을 수 없음");
                }

                await Task.Delay(100); // 연결 상태 안정화
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "CheckHardwareConnection", "하드웨어 연결 확인 실패", ex);
                IsHardwareConnected = false;
            }
        }

        /// <summary>
        /// Teaching 시스템 연동 초기화
        /// </summary>
        private void InitializeTeachingIntegration()
        {
            try
            {
                _teachingDataProvider = new TeachingDataBridge();
                Logger.Info(CLASS_NAME, "InitializeTeachingIntegration", "Teaching 시스템 연동 초기화 완료");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "InitializeTeachingIntegration", "Teaching 시스템 연동 초기화 실패", ex);
            }
        }

        /// <summary>
        /// 레시피 엔진 초기화
        /// </summary>
        private void InitializeRecipeEngine()
        {
            try
            {
                _recipeEngine = new RecipeEngine(_robotController);

                // 레시피 엔진 이벤트 구독
                _recipeEngine.StepExecuting += OnStepExecuting;
                _recipeEngine.StepCompleted += OnStepCompleted;
                _recipeEngine.ExecutionError += OnRecipeEngineError;

                Logger.Info(CLASS_NAME, "InitializeRecipeEngine", "레시피 엔진 초기화 완료");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "InitializeRecipeEngine", "레시피 엔진 초기화 실패", ex);
            }
        }

        /// <summary>
        /// 레시피 검증
        /// </summary>
        private async Task<bool> ValidateRecipeAsync(TransferRecipe recipe)
        {
            try
            {
                if (recipe.StepCount == 0)
                {
                    Logger.Warning(CLASS_NAME, "ValidateRecipeAsync", "레시피에 스텝이 없음");
                    return false;
                }

                // Teaching 좌표 검증
                foreach (var step in recipe.Steps)
                {
                    if (!string.IsNullOrEmpty(step.TeachingGroup) && !string.IsNullOrEmpty(step.LocationName))
                    {
                        var position = GetTeachingPosition(step.TeachingGroup, step.LocationName);
                        if (position == null)
                        {
                            Logger.Warning(CLASS_NAME, "ValidateRecipeAsync",
                                $"Teaching 좌표를 찾을 수 없음: {step.TeachingGroup}.{step.LocationName}");
                        }
                    }
                }

                await Task.Delay(50); // 검증 시뮬레이션
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ValidateRecipeAsync", "레시피 검증 실패", ex);
                return false;
            }
        }

        /// <summary>
        /// 전체 스텝 수 업데이트
        /// </summary>
        private void UpdateTotalSteps()
        {
            TotalSteps = ActiveRecipe?.StepCount ?? 0;
        }

        /// <summary>
        /// 실행 진행률 업데이트
        /// </summary>
        private void UpdateExecutionProgress()
        {
            if (TotalSteps > 0)
            {
                ExecutionProgress = (double)CurrentStepIndex / TotalSteps;
            }
            else
            {
                ExecutionProgress = 0.0;
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 스텝 실행 시작 이벤트 핸들러
        /// </summary>
        private void OnStepExecuting(object sender, RecipeStepExecutingEventArgs e)
        {
            try
            {
                CurrentStepIndex = e.StepIndex;
                StatusMessage = $"실행 중: {e.Step.Description}";

                OnStepExecutionStarted(new RecipeStepExecutionEventArgs(e.Step, e.StepIndex, DateTime.Now));
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "OnStepExecuting", "스텝 실행 시작 이벤트 처리 실패", ex);
            }
        }

        /// <summary>
        /// 스텝 실행 완료 이벤트 핸들러
        /// </summary>
        private void OnStepCompleted(object sender, RecipeStepCompletedEventArgs e)
        {
            try
            {
                _completedSteps++;
                StatusMessage = $"완료: {e.Step.Description}";

                OnStepExecutionCompleted(new RecipeStepExecutionEventArgs(e.Step, e.StepIndex, DateTime.Now, e.Success));
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "OnStepCompleted", "스텝 완료 이벤트 처리 실패", ex);
            }
        }

        /// <summary>
        /// 레시피 엔진 오류 이벤트 핸들러
        /// </summary>
        private void OnRecipeEngineError(object sender, RecipeEngineErrorEventArgs e)
        {
            try
            {
                _errorCount++;
                Status = RecipeSystemStatus.Error;
                StatusMessage = $"오류: {e.ErrorMessage}";

                OnErrorOccurred(new RecipeErrorEventArgs("RECIPE_ENGINE_ERROR", e.ErrorMessage, e.Exception));
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "OnRecipeEngineError", "레시피 엔진 오류 이벤트 처리 실패", ex);
            }
        }
        #endregion

        #region Event Triggers
        /// <summary>
        /// StatusChanged 이벤트 발생
        /// </summary>
        protected virtual void OnStatusChanged(RecipeSystemStatusChangedEventArgs e)
        {
            StatusChanged?.Invoke(this, e);
        }

        /// <summary>
        /// StepExecutionStarted 이벤트 발생
        /// </summary>
        protected virtual void OnStepExecutionStarted(RecipeStepExecutionEventArgs e)
        {
            StepExecutionStarted?.Invoke(this, e);
        }

        /// <summary>
        /// StepExecutionCompleted 이벤트 발생
        /// </summary>
        protected virtual void OnStepExecutionCompleted(RecipeStepExecutionEventArgs e)
        {
            StepExecutionCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// ExecutionCompleted 이벤트 발생
        /// </summary>
        protected virtual void OnExecutionCompleted(RecipeExecutionCompletedEventArgs e)
        {
            ExecutionCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// ErrorOccurred 이벤트 발생
        /// </summary>
        protected virtual void OnErrorOccurred(RecipeErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
        }

        /// <summary>
        /// PropertyChanged 이벤트 발생
        /// </summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// 리소스 해제
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            try
            {
                Logger.Info(CLASS_NAME, "Dispose", "RecipeHub 리소스 정리 시작");

                // 실행 중이면 정지
                if (Status == RecipeSystemStatus.Executing || Status == RecipeSystemStatus.Paused)
                {
                    StopExecutionAsync().Wait(5000);
                }

                // 레시피 엔진 정리
                if (_recipeEngine != null)
                {
                    _recipeEngine.StepExecuting -= OnStepExecuting;
                    _recipeEngine.StepCompleted -= OnStepCompleted;
                    _recipeEngine.ExecutionError -= OnRecipeEngineError;
                    _recipeEngine.Dispose();
                    _recipeEngine = null;
                }

                _isDisposed = true;
                Logger.Info(CLASS_NAME, "Dispose", "RecipeHub 리소스 정리 완료");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "Dispose", "리소스 정리 중 오류 발생", ex);
            }
        }
        #endregion
    }
}