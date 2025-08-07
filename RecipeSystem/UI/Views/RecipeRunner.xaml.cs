using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TeachingPendant.RecipeSystem.Models;
using TeachingPendant.RecipeSystem.Engine;
using TeachingPendant.Manager;
using TeachingPendant.Logging;
using TeachingPendant.UserManagement.Services;

namespace TeachingPendant.RecipeSystem.UI.Views
{
    /// <summary>
    /// 레시피 실행기 사용자 컨트롤 (완전 개선된 최종 버전)
    /// RecipeEngine과 연동하여 완전한 실시간 레시피 실행 모니터링 제공
    /// </summary>
    public partial class RecipeRunner : UserControl, IDisposable
    {
        #region Private Fields
        private RecipeEngine _recipeEngine;
        private TransferRecipe _currentRecipe;
        private ObservableCollection<StepExecutionViewModel> _stepViewModels;
        private DispatcherTimer _uiUpdateTimer;
        private DateTime _executionStartTime;
        private DateTime _currentStepStartTime;
        private int _completedStepsCount = 0;
        private int _errorCount = 0;
        private bool _isExecuting = false;
        private bool _isDisposed = false;

        #endregion

        #region Enhanced Progress Monitoring Fields
        // 향상된 실시간 모니터링을 위한 추가 필드
        private DispatcherTimer _progressUpdateTimer;
        private DateTime _lastProgressUpdateTime;
        private double _lastProgressValue = 0;
        private double _progressVelocity = 0; // 진행 속도 (% per second)
        private Queue<ProgressDataPoint> _progressHistory;
        private TimeSpan _smoothingInterval = TimeSpan.FromSeconds(5); // 5초간 평균으로 부드러운 예측
        private bool _isProgressTrackingActive = false;
        #endregion

        #region Constructor
        /// <summary>
        /// RecipeRunner 생성자
        /// </summary>
        public RecipeRunner()
        {
            InitializeComponent();
            InitializeRecipeRunner();
            Logger.Info("RecipeRunner", "Constructor", "RecipeRunner UI has been initialized.");
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 레시피 실행기 초기화 (향상된 버전)
        /// </summary>
        private void InitializeRecipeRunner()
        {
            try
            {
                // 스텝 뷰모델 컬렉션 초기화
                _stepViewModels = new ObservableCollection<StepExecutionViewModel>();

                // lstSteps가 실제 존재하는지 확인 후 설정
                if (lstSteps != null)
                    lstSteps.ItemsSource = _stepViewModels;

                // RecipeEngine 초기화
                _recipeEngine = new RecipeEngine();
                SetupRecipeEngineEvents();

                // 기존 UI 업데이트 타이머 설정 (500ms)
                _uiUpdateTimer = new DispatcherTimer();
                _uiUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
                _uiUpdateTimer.Tick += UiUpdateTimer_Tick;

                // 향상된 진행률 모니터링 초기화 (200ms)
                InitializeEnhancedProgressMonitoring();

                // 초기 UI 상태 설정
                UpdateUIState();
                ClearExecutionLog();

                Logger.Info("RecipeRunner", "InitializeRecipeRunner", "RecipeRunner initialized successfully (including enhanced progress monitoring)");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "InitializeRecipeRunner", "RecipeRunner initialization failed", ex);
            }
        }

        /// <summary>
        /// RecipeEngine 이벤트 설정
        /// </summary>
        private void SetupRecipeEngineEvents()
        {
            try
            {
                if (_recipeEngine != null)
                {
                    _recipeEngine.StateChanged += OnRecipeStateChanged;
                    _recipeEngine.StepStarted += OnStepStarted;
                    _recipeEngine.StepCompleted += OnStepCompleted;
                    _recipeEngine.RecipeCompleted += OnRecipeCompleted;
                    _recipeEngine.RecipeError += OnRecipeError;
                    _recipeEngine.ProgressUpdated += OnProgressUpdated;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "SetupRecipeEngineEvents", "Failed to set up RecipeEngine events", ex);
            }
        }
        #endregion

        #region Enhanced Progress Monitoring Methods
        /// <summary>
        /// 향상된 진행률 모니터링 초기화
        /// </summary>
        private void InitializeEnhancedProgressMonitoring()
        {
            try
            {
                // 진행률 이력 큐 초기화 (최근 30개 데이터포인트 저장)
                _progressHistory = new Queue<ProgressDataPoint>(30);

                // 전용 진행률 업데이트 타이머 설정
                _progressUpdateTimer = new DispatcherTimer();
                _progressUpdateTimer.Interval = TimeSpan.FromMilliseconds(200); // 200ms마다 더 세밀하게 업데이트
                _progressUpdateTimer.Tick += ProgressUpdateTimer_Tick;

                _lastProgressUpdateTime = DateTime.Now;

                System.Diagnostics.Debug.WriteLine("Enhanced progress monitoring initialized");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "InitializeEnhancedProgressMonitoring", "Failed to initialize enhanced progress monitoring", ex);
            }
        }

        /// <summary>
        /// 진행률 추적 시작
        /// </summary>
        private void StartProgressTracking()
        {
            try
            {
                _isProgressTrackingActive = true;
                _progressHistory.Clear();
                _lastProgressValue = 0;
                _progressVelocity = 0;
                _lastProgressUpdateTime = DateTime.Now;

                // 타이머 시작
                _progressUpdateTimer?.Start();

                System.Diagnostics.Debug.WriteLine("Progress tracking started");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "StartProgressTracking", "Failed to start progress tracking", ex);
            }
        }

        /// <summary>
        /// 진행률 추적 중지
        /// </summary>
        private void StopProgressTracking()
        {
            try
            {
                _isProgressTrackingActive = false;
                _progressUpdateTimer?.Stop();

                System.Diagnostics.Debug.WriteLine("Progress tracking stopped");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "StopProgressTracking", "Failed to stop progress tracking", ex);
            }
        }

        /// <summary>
        /// 진행률 업데이트 타이머 이벤트 (200ms마다 실행)
        /// </summary>
        private void ProgressUpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!_isProgressTrackingActive || _recipeEngine == null)
                    return;

                // 현재 진행률 가져오기
                double currentProgress = _recipeEngine.ProgressPercentage;
                DateTime currentTime = DateTime.Now;

                // 진행률 데이터 포인트 추가
                AddProgressDataPoint(currentProgress, currentTime);

                // 진행 속도 계산
                CalculateProgressVelocity();

                // UI 업데이트 (메인 스레드에서 실행)
                UpdateProgressUI(currentProgress, currentTime);

                // 이전 값들 업데이트
                _lastProgressValue = currentProgress;
                _lastProgressUpdateTime = currentTime;
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "ProgressUpdateTimer_Tick", "Progress update timer error", ex);
            }
        }

        /// <summary>
        /// 진행률 데이터 포인트 추가
        /// </summary>
        private void AddProgressDataPoint(double progress, DateTime timestamp)
        {
            try
            {
                // 새 데이터 포인트 추가
                _progressHistory.Enqueue(new ProgressDataPoint(progress, timestamp));

                // 오래된 데이터 제거 (5초보다 오래된 것)
                while (_progressHistory.Count > 0)
                {
                    var oldest = _progressHistory.Peek();
                    if (timestamp - oldest.Timestamp > _smoothingInterval)
                    {
                        _progressHistory.Dequeue();
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "AddProgressDataPoint", "Failed to add progress data point", ex);
            }
        }

        /// <summary>
        /// 진행 속도 계산 (부드러운 예측을 위한 이동 평균 사용)
        /// </summary>
        private void CalculateProgressVelocity()
        {
            try
            {
                if (_progressHistory.Count < 2)
                {
                    _progressVelocity = 0;
                    return;
                }

                // 최근 데이터들을 사용해서 진행 속도 계산
                var dataPoints = _progressHistory.ToArray();
                var latest = dataPoints[dataPoints.Length - 1];
                var earliest = dataPoints[0];

                double timeDiffSeconds = (latest.Timestamp - earliest.Timestamp).TotalSeconds;
                double progressDiff = latest.Progress - earliest.Progress;

                if (timeDiffSeconds > 0)
                {
                    _progressVelocity = progressDiff / timeDiffSeconds; // % per second
                }
                else
                {
                    _progressVelocity = 0;
                }

                // 음수 속도 방지 (역행하지 않음)
                if (_progressVelocity < 0)
                    _progressVelocity = 0;
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "CalculateProgressVelocity", "Failed to calculate progress velocity", ex);
                _progressVelocity = 0;
            }
        }

        /// <summary>
        /// 향상된 진행률 UI 업데이트
        /// </summary>
        private void UpdateProgressUI(double currentProgress, DateTime currentTime)
        {
            try
            {
                // 진행률 바 업데이트 (부드러운 애니메이션)
                UpdateProgressBarSmooth(currentProgress);

                // 진행률 텍스트 업데이트
                UpdateProgressText(currentProgress);

                // 경과 시간 업데이트
                UpdateElapsedTime(currentTime);

                // 향상된 예상 완료 시간 계산
                UpdateEstimatedCompletionTime(currentProgress, currentTime);

                // 진행 속도 표시 업데이트
                UpdateProgressVelocityDisplay();
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "UpdateProgressUI", "Failed to update progress UI", ex);
            }
        }

        /// <summary>
        /// 부드러운 진행률 바 업데이트
        /// </summary>
        private void UpdateProgressBarSmooth(double targetProgress)
        {
            try
            {
                if (progressBar == null) return;

                // 현재 값과 목표 값의 차이 계산
                double currentValue = progressBar.Value;
                double difference = targetProgress - currentValue;

                // 차이가 작으면 부드럽게 이동, 크면 즉시 이동
                if (Math.Abs(difference) < 2.0) // 2% 미만 차이
                {
                    // 부드러운 이동 (선형 보간)
                    double smoothedValue = currentValue + (difference * 0.3); // 30% 비율로 접근
                    progressBar.Value = Math.Max(0, Math.Min(100, smoothedValue));
                }
                else
                {
                    // 큰 차이는 즉시 이동
                    progressBar.Value = Math.Max(0, Math.Min(100, targetProgress));
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "UpdateProgressBarSmooth", "Failed to update progress bar smoothly", ex);
            }
        }

        /// <summary>
        /// 진행률 텍스트 업데이트
        /// </summary>
        private void UpdateProgressText(double currentProgress)
        {
            try
            {
                if (txtProgressText == null || _currentRecipe == null) return;

                int currentStep = _recipeEngine?.CurrentStepIndex + 1 ?? 0;
                int totalSteps = _currentRecipe.StepCount;

                txtProgressText.Text = $"{currentStep} / {totalSteps} ({currentProgress:F1}%)";
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "UpdateProgressText", "Failed to update progress text", ex);
            }
        }

        /// <summary>
        /// 경과 시간 업데이트
        /// </summary>
        private void UpdateElapsedTime(DateTime currentTime)
        {
            try
            {
                if (txtElapsedTime == null) return;

                TimeSpan elapsed = currentTime - _executionStartTime;
                txtElapsedTime.Text = $"Elapsed Time: {elapsed:hh\\:mm\\:ss}";
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "UpdateElapsedTime", "Failed to update elapsed time", ex);
            }
        }

        /// <summary>
        /// 향상된 예상 완료 시간 계산
        /// </summary>
        private void UpdateEstimatedCompletionTime(double currentProgress, DateTime currentTime)
        {
            try
            {
                if (txtEstimatedTime == null) return;

                // 최소 5% 진행 후 예상 시간 계산
                if (currentProgress < 5.0)
                {
                    txtEstimatedTime.Text = "Est. Completion: Calculating...";
                    return;
                }

                TimeSpan elapsed = currentTime - _executionStartTime;

                // 방법 1: 단순 비례 계산
                double estimatedTotalSeconds = elapsed.TotalSeconds * 100.0 / currentProgress;
                TimeSpan estimatedTotal = TimeSpan.FromSeconds(estimatedTotalSeconds);
                TimeSpan remainingSimple = estimatedTotal - elapsed;

                // 방법 2: 진행 속도 기반 계산 (더 정확함)
                TimeSpan remainingVelocityBased = TimeSpan.MaxValue;
                if (_progressVelocity > 0.001) // 0.001% per second 이상일 때만
                {
                    double remainingProgress = 100.0 - currentProgress;
                    double remainingSeconds = remainingProgress / _progressVelocity;
                    remainingVelocityBased = TimeSpan.FromSeconds(remainingSeconds);
                }

                // 두 방법 중 더 합리적인 값 선택
                TimeSpan finalRemaining;
                if (remainingVelocityBased != TimeSpan.MaxValue &&
                    Math.Abs((remainingVelocityBased - remainingSimple).TotalSeconds) < remainingSimple.TotalSeconds * 0.5)
                {
                    // 속도 기반 계산이 단순 계산과 50% 이내 차이면 속도 기반 사용
                    finalRemaining = remainingVelocityBased;
                }
                else
                {
                    // 그렇지 않으면 단순 계산 사용
                    finalRemaining = remainingSimple;
                }

                // 음수 시간 방지
                if (finalRemaining.TotalSeconds < 0)
                    finalRemaining = TimeSpan.Zero;

                // 너무 큰 시간 방지 (24시간 초과)
                if (finalRemaining.TotalHours > 24)
                    finalRemaining = TimeSpan.FromHours(24);

                txtEstimatedTime.Text = $"Est. Completion: {finalRemaining:hh\\:mm\\:ss}";
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "UpdateEstimatedCompletionTime", "Failed to calculate estimated completion time", ex);
                if (txtEstimatedTime != null)
                    txtEstimatedTime.Text = "Est. Completion: Calculation Error";
            }
        }

        /// <summary>
        /// 진행 속도 표시 업데이트
        /// </summary>
        private void UpdateProgressVelocityDisplay()
        {
            try
            {
                // 진행 속도를 디버그 콘솔에만 출력 (실제 UI 컨트롤은 에러 방지를 위해 제거)
                System.Diagnostics.Debug.WriteLine($"Progress velocity: {_progressVelocity:F3}%/second");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "UpdateProgressVelocityDisplay", "Failed to update progress velocity display", ex);
            }
        }

        /// <summary>
        /// 레시피 실행 시작 시 진행률 모니터링 활성화
        /// </summary>
        private void OnRecipeExecutionStarted()
        {
            try
            {
                _executionStartTime = DateTime.Now;
                StartProgressTracking();

                System.Diagnostics.Debug.WriteLine("Recipe execution started, progress tracking activated");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "OnRecipeExecutionStarted", "Failed to handle recipe execution start", ex);
            }
        }

        /// <summary>
        /// 레시피 실행 종료 시 진행률 모니터링 비활성화
        /// </summary>
        private void OnRecipeExecutionStopped()
        {
            try
            {
                StopProgressTracking();

                System.Diagnostics.Debug.WriteLine("Recipe execution stopped, progress tracking deactivated");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "OnRecipeExecutionStopped", "Failed to handle recipe execution stop", ex);
            }
        }
        #endregion

        #region Recipe Engine Event Handlers
        /// <summary>
        /// 레시피 상태 변경 이벤트 핸들러 (향상된 버전)
        /// </summary>
        private void OnRecipeStateChanged(object sender, RecipeStateChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Recipe state changed: {e.OldState} → {e.NewState}");

                    // 실행 상태에 따른 진행률 모니터링 제어
                    switch (e.NewState)
                    {
                        case RecipeExecutionState.Running:
                            _isExecuting = true;
                            _uiUpdateTimer.Start();
                            OnRecipeExecutionStarted(); // 향상된 진행률 추적 시작
                            break;

                        case RecipeExecutionState.Completed:
                        case RecipeExecutionState.Error:
                        case RecipeExecutionState.Cancelled:
                        case RecipeExecutionState.Idle:
                            _isExecuting = false;
                            _uiUpdateTimer.Stop();
                            OnRecipeExecutionStopped(); // 향상된 진행률 추적 종료
                            break;

                        case RecipeExecutionState.Paused:
                            // 일시정지 시에는 모니터링 유지하되 업데이트 빈도 낮춤
                            if (_progressUpdateTimer != null)
                                _progressUpdateTimer.Interval = TimeSpan.FromMilliseconds(1000); // 1초로 변경
                            break;
                    }

                    // 실행 상태 UI 업데이트
                    UpdateExecutionStatus(e.NewState);
                    UpdateUIState();
                }
                catch (Exception ex)
                {
                    Logger.Error("RecipeRunner", "OnRecipeStateChanged", "Error occurred while handling recipe state change", ex);
                }
            }));
        }

        /// <summary>
        /// 스텝 시작 이벤트 핸들러
        /// </summary>
        private void OnStepStarted(object sender, RecipeStepStartedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _currentStepStartTime = DateTime.Now;

                    // 현재 스텝 하이라이트
                    UpdateCurrentStep(e.StepIndex);

                    // 실행 로그 추가
                    AddExecutionLog("▶ Starting Step " + (e.StepIndex + 1).ToString() + ": " + e.Step.Description);

                    // 현재 스텝 정보 업데이트 (안전한 방식으로)
                    UpdateCurrentStepInfo(e.Step, e.StepIndex);
                }
                catch (Exception ex)
                {
                    Logger.Error("RecipeRunner", "OnStepStarted", "Error occurred while handling step start", ex);
                }
            }));
        }

        /// <summary>
        /// 스텝 완료 이벤트 핸들러
        /// </summary>
        private void OnStepCompleted(object sender, RecipeStepCompletedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // 스텝 상태 업데이트
                    var stepViewModel = _stepViewModels.FirstOrDefault(s => s.StepNumber == e.StepIndex + 1);
                    if (stepViewModel != null)
                    {
                        stepViewModel.IsCompleted = e.Success;
                        stepViewModel.HasError = !e.Success;
                        // C# 6.0 호환: null 조건부 연산자 대신 삼항 연산자 사용
                        stepViewModel.ExecutionTimeText = e.ExecutionTime.HasValue ?
                            e.ExecutionTime.Value.ToString(@"mm\:ss\.ff") : "--:--";
                        stepViewModel.StatusIcon = e.Success ? "✅" : "❌";
                    }

                    // 통계 업데이트
                    if (e.Success)
                    {
                        _completedStepsCount++;
                    }
                    else
                    {
                        _errorCount++;
                    }

                    UpdateExecutionStatistics();

                    // 실행 로그 추가
                    string statusText = e.Success ? "Completed" : "Failed";
                    string timeText = e.ExecutionTime.HasValue ?
                        e.ExecutionTime.Value.ToString(@"mm\:ss\.ff") : "--:--";
                    AddExecutionLog("Step " + (e.StepIndex + 1).ToString() + " " + statusText + " (" + timeText + "): " + e.Message);
                }
                catch (Exception ex)
                {
                    Logger.Error("RecipeRunner", "OnStepCompleted", "Error occurred while handling step completion", ex);
                }
            }));
        }

        /// <summary>
        /// 레시피 완료 이벤트 핸들러
        /// </summary>
        private void OnRecipeCompleted(object sender, RecipeCompletedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _isExecuting = false;
                    _uiUpdateTimer.Stop();

                    string statusText = e.Success ? "successfully completed" : "finished with errors";
                    AddExecutionLog("=== Recipe execution " + statusText + " ===");
                    AddExecutionLog("Total Execution Time: " + e.Statistics.TotalExecutionTime.ToString(@"hh\:mm\:ss"));
                    AddExecutionLog("Success Rate: " + e.Statistics.SuccessRate.ToString("F1") + "%");

                    // 완료 알림
                    string title = e.Success ? "Execution Complete" : "Execution Failed";
                    MessageBoxImage icon = e.Success ? MessageBoxImage.Information : MessageBoxImage.Warning;
                    MessageBox.Show("Recipe execution has " + statusText + ".\n\n" + e.Message,
                                    title, MessageBoxButton.OK, icon);

                    // 모든 스텝의 현재 실행 표시 제거
                    foreach (var step in _stepViewModels)
                    {
                        step.IsCurrentStep = false;
                    }

                    UpdateUIState();
                }
                catch (Exception ex)
                {
                    Logger.Error("RecipeRunner", "OnRecipeCompleted", "Error occurred while handling recipe completion", ex);
                }
            }));
        }

        /// <summary>
        /// 레시피 오류 이벤트 핸들러
        /// </summary>
        private void OnRecipeError(object sender, RecipeErrorEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _errorCount++;
                    AddExecutionLog("❌ Error Occurred [" + e.ErrorCode + "]: " + e.Message);

                    if (e.CurrentStep != null)
                    {
                        AddExecutionLog("Error at Step: Step " + (e.StepIndex + 1).ToString() + " - " + e.CurrentStep.Description);
                    }

                    UpdateExecutionStatistics();

                    // 심각한 오류의 경우 사용자에게 알림
                    if (e.ErrorCode.Contains("SAFETY") || e.ErrorCode.Contains("EMERGENCY"))
                    {
                        MessageBox.Show("A critical error has occurred:\n" + e.Message,
                                        "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("RecipeRunner", "OnRecipeError", "Error occurred while handling error event", ex);
                }
            }));
        }

        /// <summary>
        /// 진행 상황 업데이트 이벤트 핸들러 (기존 방식과 병행)
        /// </summary>
        private void OnProgressUpdated(object sender, RecipeProgressEventArgs e)
        {
            // 기존 이벤트 기반 업데이트는 유지 (호환성을 위해)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // 기본 진행률 정보 업데이트 (폴백용)
                    if (!_isProgressTrackingActive)
                    {
                        // 향상된 모니터링이 비활성 상태일 때만 기존 방식 사용
                        if (progressBar != null)
                            progressBar.Value = e.ProgressPercentage;

                        if (txtProgressText != null)
                            txtProgressText.Text = $"{e.CurrentStepIndex} / {e.TotalSteps} ({e.ProgressPercentage:F1}%)";

                        if (txtElapsedTime != null)
                            txtElapsedTime.Text = $"Elapsed Time: {e.ElapsedTime:hh\\:mm\\:ss}";
                    }

                    // 향상된 모니터링과 관계없이 항상 업데이트되는 정보들
                    if (e.ProgressPercentage > 5 && txtEstimatedTime != null && !_isProgressTrackingActive)
                    {
                        // 향상된 모니터링이 비활성일 때만 기존 계산 방식 사용
                        var estimatedTotal = TimeSpan.FromTicks((long)(e.ElapsedTime.Ticks * 100 / e.ProgressPercentage));
                        var remaining = estimatedTotal - e.ElapsedTime;
                        txtEstimatedTime.Text = $"Est. Completion: {remaining:hh\\:mm\\:ss}";
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("RecipeRunner", "OnProgressUpdated", "Error occurred while updating progress", ex);
                }
            }));
        }
        #endregion

        #region UI Update Methods
        /// <summary>
        /// UI 업데이트 타이머 이벤트 (향상된 버전)
        /// </summary>
        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // 안전 상태 업데이트 (안전한 방식으로)
                UpdateSafetyStatus();

                // 시스템 상태 업데이트
                UpdateSystemStatus();

                // 향상된 진행률 모니터링이 비활성일 때만 기본 진행률 업데이트
                if (_recipeEngine != null && _isExecuting && !_isProgressTrackingActive)
                {
                    if (progressBar != null)
                        progressBar.Value = _recipeEngine.ProgressPercentage;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "UiUpdateTimer_Tick", "UI update timer error", ex);
            }
        }

        /// <summary>
        /// UI 상태 업데이트 (안전한 버전)
        /// </summary>
        private void UpdateUIState()
        {
            try
            {
                bool canExecute = _currentRecipe != null && !_isExecuting;
                bool canStop = _isExecuting;
                bool canPause = _isExecuting && _recipeEngine?.CurrentState == RecipeExecutionState.Running;

                // 실제 존재하는 버튼들만 업데이트
                if (btnStartExecution != null) btnStartExecution.IsEnabled = canExecute;
                if (btnStopExecution != null) btnStopExecution.IsEnabled = canStop;
                if (btnPauseExecution != null) btnPauseExecution.IsEnabled = canPause;

                // btnResume는 XAML에 없으므로 btnPauseExecution으로 대체하거나 별도 처리
                // 일시정지 상태일 때는 버튼 텍스트를 변경하는 방식으로 처리

                // 디버그 정보 출력
                System.Diagnostics.Debug.WriteLine($"UI State - CanExecute: {canExecute}, CanStop: {canStop}, CanPause: {canPause}");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "UpdateUIState", "Failed to update UI state", ex);
            }
        }

        /// <summary>
        /// 실행 상태 표시 업데이트 (안전한 버전)
        /// </summary>
        private void UpdateExecutionStatus(RecipeExecutionState state)
        {
            try
            {
                Color statusColor;
                string statusText;

                switch (state)
                {
                    case RecipeExecutionState.Idle:
                        statusColor = Color.FromRgb(0x95, 0xA5, 0xA6); // Gray
                        statusText = "Idle";
                        break;
                    case RecipeExecutionState.Running:
                        statusColor = Color.FromRgb(0x34, 0x98, 0xDB); // Blue
                        statusText = "Running";
                        break;
                    case RecipeExecutionState.Paused:
                        statusColor = Color.FromRgb(0xF3, 0x9C, 0x12); // Orange
                        statusText = "Paused";
                        break;
                    case RecipeExecutionState.Stopping:
                        statusColor = Color.FromRgb(0x8E, 0x44, 0xAD); // Purple
                        statusText = "Stopping";
                        break;
                    case RecipeExecutionState.Completed:
                        statusColor = Color.FromRgb(0x27, 0xAE, 0x60); // Green
                        statusText = "Completed";
                        break;
                    case RecipeExecutionState.Error:
                        statusColor = Color.FromRgb(0xE7, 0x4C, 0x3C); // Red
                        statusText = "Error";
                        break;
                    case RecipeExecutionState.Cancelled:
                        statusColor = Color.FromRgb(0x95, 0xA5, 0xA6); // Gray
                        statusText = "Cancelled";
                        break;
                    default:
                        statusColor = Color.FromRgb(0x95, 0xA5, 0xA6);
                        statusText = "Unknown";
                        break;
                }

                // 실제 존재하는 UI 컨트롤만 업데이트
                if (statusIndicator != null)
                    statusIndicator.Fill = new SolidColorBrush(statusColor);
                if (txtExecutionStatus != null)
                    txtExecutionStatus.Text = statusText;

                // 디버그 로그
                System.Diagnostics.Debug.WriteLine($"Execution status updated: {statusText}");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "UpdateExecutionStatus", "Error occurred while updating execution status", ex);
            }
        }

        /// <summary>
        /// 현재 스텝 표시 업데이트
        /// </summary>
        private void UpdateCurrentStep(int stepIndex)
        {
            try
            {
                // 모든 스텝의 현재 상태 초기화
                foreach (var step in _stepViewModels)
                {
                    step.IsCurrentStep = false;
                }

                // 현재 스텝 하이라이트
                if (stepIndex >= 0 && stepIndex < _stepViewModels.Count)
                {
                    _stepViewModels[stepIndex].IsCurrentStep = true;
                }

                System.Diagnostics.Debug.WriteLine($"Current step updated: {stepIndex + 1}");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "UpdateCurrentStep", "Failed to update current step", ex);
            }
        }

        /// <summary>
        /// 현재 스텝 정보 업데이트 (안전한 버전)
        /// </summary>
        private void UpdateCurrentStepInfo(RecipeStep step, int stepIndex)
        {
            try
            {
                // XAML에 해당 컨트롤이 없으므로 실행 로그에만 출력
                AddExecutionLog($"Step {stepIndex + 1}: {step.Description} (Type: {step.Type})");

                // 디버그 로그
                System.Diagnostics.Debug.WriteLine($"Current step info: Step {stepIndex + 1} - {step.Description}");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "UpdateCurrentStepInfo", "Failed to update current step info", ex);
            }
        }

        /// <summary>
        /// 안전 상태 업데이트 (안전한 버전)
        /// </summary>
        private void UpdateSafetyStatus()
        {
            try
            {
                // 기본적으로 안전 상태로 가정 (실제 안전 시스템과 연동 시 수정 필요)
                bool isSystemSafe = true; // SafetySystem.IsSystemSafe() 대신 기본값

                // XAML에 해당 컨트롤이 없으므로 로그에만 기록
                if (!isSystemSafe)
                {
                    AddExecutionLog("⚠️ Safety system in warning state");
                }

                // 디버그 로그
                System.Diagnostics.Debug.WriteLine($"Safety status: {(isSystemSafe ? "Safe" : "Warning")}");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "UpdateSafetyStatus", "Failed to update safety status", ex);
            }
        }

        /// <summary>
        /// 시스템 상태 업데이트
        /// </summary>
        private void UpdateSystemStatus()
        {
            try
            {
                // 시스템 상태를 로그에 기록
                string status = "Normal";
                if (_isExecuting)
                    status = "Executing";
                else if (_errorCount > 0)
                    status = "Error Occurred";

                // 로그에 시스템 상태 기록
                System.Diagnostics.Debug.WriteLine($"System Status: {status}");

                // 실행 통계 업데이트
                UpdateExecutionStatistics();
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "UpdateSystemStatus", "Failed to update system status", ex);
            }
        }

        /// <summary>
        /// 실행 통계 업데이트
        /// </summary>
        private void UpdateExecutionStatistics()
        {
            try
            {
                // XAML에 해당 컨트롤들이 없으므로 로그와 디버그 출력으로 대체
                if (_currentRecipe != null)
                {
                    double successRate = _currentRecipe.StepCount > 0 ?
                        (_completedStepsCount * 100.0 / _currentRecipe.StepCount) : 0;

                    // 통계 정보를 로그에 기록
                    AddExecutionLog($"📊 Stats - Completed: {_completedStepsCount}, Errors: {_errorCount}, Success Rate: {successRate:F1}%");
                }

                // 디버그 출력
                System.Diagnostics.Debug.WriteLine($"Statistics - Completed: {_completedStepsCount}, Errors: {_errorCount}");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "UpdateExecutionStatistics", "Failed to update execution statistics", ex);
            }
        }
        #endregion

        #region Recipe Management
        /// <summary>
        /// 레시피 로드
        /// </summary>
        public void LoadRecipe(TransferRecipe recipe)
        {
            try
            {
                if (recipe == null)
                {
                    Logger.Warning("RecipeRunner", "LoadRecipe", "Recipe to load is null.");
                    return;
                }

                _currentRecipe = recipe;

                // UI 업데이트 (안전한 방식으로)
                if (txtRecipeName != null) txtRecipeName.Text = recipe.RecipeName;
                if (txtStepCount != null) txtStepCount.Text = "Total " + recipe.StepCount.ToString() + " steps";

                // 스텝 뷰모델 생성
                CreateStepViewModels();

                // 진행률 초기화
                if (progressBar != null) progressBar.Value = 0;
                if (txtProgressText != null) txtProgressText.Text = "0 / " + recipe.StepCount.ToString() + " (0%)";
                if (txtElapsedTime != null) txtElapsedTime.Text = "Elapsed Time: 00:00:00";
                if (txtEstimatedTime != null) txtEstimatedTime.Text = "Est. Completion: --:--:--";

                // 통계 초기화
                _completedStepsCount = 0;
                _errorCount = 0;
                UpdateExecutionStatistics();

                // UI 상태 업데이트
                UpdateUIState();

                AddExecutionLog("Recipe loaded: " + recipe.RecipeName);
                Logger.Info("RecipeRunner", "LoadRecipe", "Recipe loaded: " + recipe.RecipeName);
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "LoadRecipe", "Error occurred while loading recipe", ex);
                MessageBox.Show("An error occurred while loading the recipe: " + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 스텝 뷰모델 컬렉션 생성
        /// </summary>
        private void CreateStepViewModels()
        {
            try
            {
                _stepViewModels.Clear();

                // C# 6.0 호환: null 조건부 연산자 대신 null 체크 사용
                if (_currentRecipe != null && _currentRecipe.Steps != null)
                {
                    for (int i = 0; i < _currentRecipe.Steps.Count; i++)
                    {
                        var step = _currentRecipe.Steps[i];
                        var viewModel = new StepExecutionViewModel
                        {
                            StepNumber = i + 1,
                            Step = step,
                            StepTypeDisplayName = GetStepTypeDisplayName(step.Type),
                            StepTypeIcon = GetStepTypeIcon(step.Type),
                            IsCurrentStep = false,
                            IsCompleted = false,
                            HasError = false,
                            ExecutionTimeText = "--:--",
                            StatusIcon = "⏳"
                        };

                        _stepViewModels.Add(viewModel);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "CreateStepViewModels", "Failed to create step view models", ex);
            }
        }

        /// <summary>
        /// 스텝 타입 표시명 가져오기
        /// </summary>
        private string GetStepTypeDisplayName(StepType stepType)
        {
            switch (stepType)
            {
                case StepType.Home: return "Home Move";
                case StepType.Move: return "Move";
                case StepType.Pick: return "Pick";
                case StepType.Place: return "Place";
                case StepType.Wait: return "Wait";
                case StepType.CheckSafety: return "Safety Check";
                default: return stepType.ToString();
            }
        }

        /// <summary>
        /// 스텝 타입 아이콘 가져오기
        /// </summary>
        private string GetStepTypeIcon(StepType stepType)
        {
            switch (stepType)
            {
                case StepType.Home: return "🏠";
                case StepType.Move: return "➡️";
                case StepType.Pick: return "⬇️";
                case StepType.Place: return "⬆️";
                case StepType.Wait: return "⏱️";
                case StepType.CheckSafety: return "🛡️";
                default: return "❓";
            }
        }
        #endregion

        #region Button Event Handlers - 추가 필요한 이벤트 핸들러들
        /// <summary>
        /// 실행 시작 버튼 클릭 이벤트
        /// </summary>
        private async void btnStartExecution_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 권한 확인 - RECIPE_EXECUTE 권한 필요
                if (!PermissionChecker.HasPermission("RECIPE_EXECUTE"))
                {
                    MessageBox.Show("You do not have permission to execute recipes.", "Permission Denied",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await StartRecipeExecutionAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "btnStartExecution_Click", "Error occurred while starting recipe execution", ex);
                AddExecutionLog($"Error starting execution: {ex.Message}");
            }
        }

        /// <summary>
        /// 실행 중지 버튼 클릭 이벤트
        /// </summary>
        private async void btnStopExecution_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("Are you sure you want to stop recipe execution?", "Stop Execution",
                                              MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    await StopRecipeExecutionAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "btnStopExecution_Click", "Error occurred while stopping recipe", ex);
                AddExecutionLog("Error stopping execution: " + ex.Message);
            }
        }

        /// <summary>
        /// 일시정지 버튼 클릭 이벤트 (일시정지/재개 토글)
        /// </summary>
        private void btnPauseExecution_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_recipeEngine.CurrentState == RecipeExecutionState.Running)
                {
                    _recipeEngine.PauseExecution();
                    AddExecutionLog("Recipe execution paused.");
                    if (btnPauseExecution != null)
                        btnPauseExecution.Content = "▶ Resume";
                }
                else if (_recipeEngine.CurrentState == RecipeExecutionState.Paused)
                {
                    _recipeEngine.ResumeExecution();
                    AddExecutionLog("Recipe execution resumed.");
                    if (btnPauseExecution != null)
                        btnPauseExecution.Content = "⏸ Pause";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "btnPauseExecution_Click", "Error occurred during pause/resume", ex);
                AddExecutionLog("Pause/Resume error: " + ex.Message);
            }
        }

        /// <summary>
        /// 긴급 정지 버튼 클릭 이벤트
        /// </summary>
        private async void btnEmergencyStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("Are you sure you want to perform an emergency stop?\nThis action will immediately halt all operations.",
                                             "Emergency Stop", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    AddExecutionLog("🚨 Emergency stop requested!");
                    await _recipeEngine.StopExecutionAsync();
                    AddExecutionLog("🚨 Emergency stop has been executed!");

                    // UI 즉시 업데이트
                    _isExecuting = false;
                    UpdateUIState();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "btnEmergencyStop_Click", "Error occurred during emergency stop", ex);
                AddExecutionLog("Emergency stop error: " + ex.Message);
            }
        }

        /// <summary>
        /// 로그 지우기 버튼 클릭 이벤트
        /// </summary>
        private void btnClearLog_Click(object sender, RoutedEventArgs e)
        {
            ClearExecutionLog();
        }
        #endregion

        #region Recipe Control Methods - 수정된 버전
        /// <summary>
        /// 레시피 실행 시작 (공개 메서드)
        /// </summary>
        public async Task<bool> StartRecipeExecutionAsync()
        {
            try
            {
                if (_currentRecipe == null)
                {
                    AddExecutionLog("❌ No recipe to execute.");
                    MessageBox.Show("There is no recipe to execute.", "Notification", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                // 실행 시작 준비
                _executionStartTime = DateTime.Now;
                _completedStepsCount = 0;
                _errorCount = 0;

                // 진행률 초기화
                if (progressBar != null) progressBar.Value = 0;
                if (txtProgressText != null) txtProgressText.Text = $"0 / {_currentRecipe.StepCount} (0%)";
                if (txtElapsedTime != null) txtElapsedTime.Text = "Elapsed Time: 00:00:00";
                if (txtEstimatedTime != null) txtEstimatedTime.Text = "Est. Completion: Calculating...";

                // 실행 로그 초기화
                ClearExecutionLog();
                AddExecutionLog("=== Starting Recipe Execution ===");
                AddExecutionLog($"Recipe Name: {_currentRecipe.RecipeName}");
                AddExecutionLog($"Total Steps: {_currentRecipe.StepCount}");

                // 레시피 실행 시작 (올바른 메서드 호출)
                bool result = await _recipeEngine.ExecuteRecipeAsync(_currentRecipe);

                if (!result)
                {
                    AddExecutionLog("❌ Failed to start recipe execution");
                    MessageBox.Show("Could not start recipe execution.", "Execution Failed",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "StartRecipeExecutionAsync", "Error occurred while starting recipe execution", ex);
                AddExecutionLog($"❌ Error during execution: {ex.Message}");
                MessageBox.Show($"An error occurred during recipe execution: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 레시피 선택 버튼 클릭 이벤트
        /// </summary>
        private void btnSelectRecipe_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 권한 확인 - RECIPE_VIEW 권한 필요
                if (!PermissionChecker.HasPermission("RECIPE_VIEW"))
                {
                    MessageBox.Show("You do not have permission to view recipes.", "Permission Denied",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // RecipeSelectionDialog가 없다면 간단한 MessageBox로 대체
                MessageBox.Show("Recipe selection feature will be implemented.\nLoading a test recipe for now.",
                                "Select Recipe", MessageBoxButton.OK, MessageBoxImage.Information);

                // 테스트용 레시피 생성 및 로드
                var testRecipe = CreateTestRecipe();
                if (testRecipe != null)
                {
                    LoadRecipe(testRecipe);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "btnSelectRecipe_Click", "Error occurred while selecting recipe", ex);
                MessageBox.Show("An error occurred while selecting a recipe: " + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 테스트용 레시피 생성
        /// </summary>
        private TransferRecipe CreateTestRecipe()
        {
            try
            {
                var recipe = new TransferRecipe
                {
                    RecipeName = "Test Recipe_" + DateTime.Now.ToString("HHmmss"),
                    Description = "Simple test recipe",
                    CreatedBy = "System",
                    CreatedDate = DateTime.Now,
                    IsEnabled = true
                };

                // 영어 테스트 스텝들 추가
                recipe.Steps.Add(new RecipeStep
                {
                    Type = StepType.Home,
                    Description = "Move to Home Position",
                    IsEnabled = true
                });

                recipe.Steps.Add(new RecipeStep
                {
                    Type = StepType.Wait,
                    Description = "Wait 2 seconds",
                    WaitTimeMs = 2000,
                    IsEnabled = true
                });

                recipe.Steps.Add(new RecipeStep
                {
                    Type = StepType.CheckSafety,
                    Description = "Check Safety Status",
                    IsEnabled = true
                });

                // 추가 스텝들도 영어로 변경
                recipe.Steps.Add(new RecipeStep
                {
                    Type = StepType.Move,
                    Description = "Move to Position",
                    IsEnabled = true
                });

                return recipe;
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "CreateTestRecipe", "Test recipe creation failed", ex);
                return null;
            }
        }

        /// <summary>
        /// 레시피 실행 중지 (공개 메서드)
        /// </summary>
        public async Task<bool> StopRecipeExecutionAsync()
        {
            try
            {
                if (_recipeEngine != null && _isExecuting)
                {
                    AddExecutionLog("User requested stop...");
                    await _recipeEngine.StopExecutionAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "StopRecipeExecutionAsync", "Error occurred while stopping recipe", ex);
                AddExecutionLog($"❌ Error while stopping recipe: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 레시피 일시정지 (공개 메서드)
        /// </summary>
        public bool PauseRecipeExecutionAsync()
        {
            try
            {
                if (_recipeEngine != null && _isExecuting)
                {
                    AddExecutionLog("User requested pause...");
                    _recipeEngine.PauseExecution();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "PauseRecipeExecutionAsync", "Error occurred while pausing recipe", ex);
                AddExecutionLog($"❌ Error while pausing recipe: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 레시피 재개 (공개 메서드)
        /// </summary>
        public bool ResumeRecipeExecutionAsync()
        {
            try
            {
                if (_recipeEngine != null && _recipeEngine.CurrentState == RecipeExecutionState.Paused)
                {
                    AddExecutionLog("User requested resume...");
                    _recipeEngine.ResumeExecution();

                    // 일시정지에서 재개 시 타이머 간격 복원
                    if (_progressUpdateTimer != null)
                        _progressUpdateTimer.Interval = TimeSpan.FromMilliseconds(200);

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "ResumeRecipeExecutionAsync", "Error occurred while resuming recipe", ex);
                AddExecutionLog($"❌ Error while resuming recipe: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Execution Log Management
        /// <summary>
        /// 실행 로그 추가
        /// </summary>
        private void AddExecutionLog(string message)
        {
            try
            {
                if (txtExecutionLog != null)
                {
                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    string logEntry = $"[{timestamp}] {message}";

                    txtExecutionLog.AppendText(logEntry + Environment.NewLine);
                    txtExecutionLog.ScrollToEnd();
                }
                else
                {
                    // UI 컨트롤이 없으면 디버그 콘솔에 출력
                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    System.Diagnostics.Debug.WriteLine($"[{timestamp}] {message}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "AddExecutionLog", "Error occurred while adding execution log", ex);
            }
        }

        /// <summary>
        /// 실행 로그 초기화
        /// </summary>
        private void ClearExecutionLog()
        {
            try
            {
                if (txtExecutionLog != null)
                {
                    txtExecutionLog.Clear();
                }

                AddExecutionLog("=== RecipeRunner Execution Log ===");
                AddExecutionLog($"Current Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                AddExecutionLog("System ready");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "ClearExecutionLog", "Error occurred while clearing execution log", ex);
            }
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// 리소스 정리 (향상된 버전)
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            try
            {
                // 진행률 추적 중지
                StopProgressTracking();

                // 기존 타이머 정리
                if (_uiUpdateTimer != null)
                {
                    _uiUpdateTimer.Stop();
                    _uiUpdateTimer = null;
                }

                // 향상된 진행률 타이머 정리
                if (_progressUpdateTimer != null)
                {
                    _progressUpdateTimer.Stop();
                    _progressUpdateTimer = null;
                }

                // RecipeEngine 정리
                if (_recipeEngine != null)
                {
                    // 이벤트 핸들러 해제
                    _recipeEngine.StateChanged -= OnRecipeStateChanged;
                    _recipeEngine.StepStarted -= OnStepStarted;
                    _recipeEngine.StepCompleted -= OnStepCompleted;
                    _recipeEngine.RecipeCompleted -= OnRecipeCompleted;
                    _recipeEngine.RecipeError -= OnRecipeError;
                    _recipeEngine.ProgressUpdated -= OnProgressUpdated;

                    _recipeEngine.Dispose();
                    _recipeEngine = null;
                }

                Logger.Info("RecipeRunner", "Dispose", "RecipeRunner disposed (including enhanced monitoring)");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunner", "Dispose", "Error occurred while disposing RecipeRunner", ex);
            }

            _isDisposed = true;
        }
        #endregion

        #region Helper Classes
        /// <summary>
        /// 진행률 데이터 포인트
        /// </summary>
        private class ProgressDataPoint
        {
            public double Progress { get; }
            public DateTime Timestamp { get; }

            public ProgressDataPoint(double progress, DateTime timestamp)
            {
                Progress = progress;
                Timestamp = timestamp;
            }
        }
        #endregion

    }

    #region ViewModel Classes
    /// <summary>
    /// 스텝 실행 상태를 표시하기 위한 뷰모델 클래스
    /// INotifyPropertyChanged를 구현하지 않은 단순 모델 (C# 6.0 호환)
    /// </summary>
    public class StepExecutionViewModel
    {
        public int StepNumber { get; set; }
        public RecipeStep Step { get; set; }
        public string StepTypeDisplayName { get; set; }
        public string StepTypeIcon { get; set; }
        public bool IsCurrentStep { get; set; }
        public bool IsCompleted { get; set; }
        public bool HasError { get; set; }
        public string ExecutionTimeText { get; set; }
        public string StatusIcon { get; set; }
    }
    #endregion
}