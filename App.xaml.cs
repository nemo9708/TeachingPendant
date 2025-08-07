using System;
using System.Windows;
using TeachingPendant.Logging;
using TeachingPendant.Manager;
using TeachingPendant.UserManagement.Services;

namespace TeachingPendant
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// 가상 키보드 시스템 통합 버전 (현재 가상키보드 비활성화)
    /// </summary>
    public partial class App : Application
    {
        #region Constants
        private const string CLASS_NAME = "App";
        #endregion

        #region Private Fields
        private MainWindow _mainWindow = null;
        #endregion

        #region Application Lifecycle
        /// <summary>
        /// 애플리케이션 시작 시 초기화
        /// </summary>
        /// <param name="e">시작 인자</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 애플리케이션 시작...");

                // 1. 로깅 시스템 초기화
                InitializeLoggingSystem();

                // 2. 기본 예외 처리 등록
                this.DispatcherUnhandledException += App_DispatcherUnhandledException;
                this.Exit += App_Exit;

                // 3. 전역 매니저들 초기화
                InitializeGlobalManagers();

                // 4. 메인 윈도우 생성
                _mainWindow = new MainWindow();

                // 6. 사용자 관리 시스템 초기화
                InitializeUserManagement();

                // 7. 레시피 시스템 초기화 (비동기)
                _ = InitializeRecipeSystemAsync();

                // 8. 메인 윈도우 표시
                _mainWindow.Show();

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 애플리케이션 시작 완료");
                Logger.Info(CLASS_NAME, "OnStartup", "TeachingPendant 애플리케이션 시작 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 애플리케이션 시작 실패: {ex.Message}");

                string errorMessage = $"애플리케이션 초기화 중 오류가 발생했습니다:\n{ex.Message}\n\n애플리케이션을 종료합니다.";
                MessageBox.Show(errorMessage, "시작 오류", MessageBoxButton.OK, MessageBoxImage.Error);

                this.Shutdown(1);
            }
        }

        /// <summary>
        /// 애플리케이션 종료 시 정리
        /// </summary>
        /// <param name="e">종료 인자</param>
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 애플리케이션 종료 중...");
                Logger.Info(CLASS_NAME, "OnExit", "TeachingPendant 애플리케이션 종료 시작");

                // 2. 전역 매니저들 정리
                CleanupGlobalManagers();

                // 3. 로깅 시스템 종료
                CleanupLoggingSystem();

                base.OnExit(e);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 애플리케이션 종료 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 애플리케이션 종료 중 오류: {ex.Message}");
            }
        }
        #endregion

        #region Initialization Methods
        /// <summary>
        /// 로깅 시스템 초기화
        /// </summary>
        private void InitializeLoggingSystem()
        {
            try
            {
                var logConfig = new LogManager.LogConfiguration();

#if DEBUG
                logConfig.DevelopmentMode = true;
                logConfig.MinimumLevel = LogLevel.Debug;
                logConfig.EnableConsoleOutput = true;
#else
                logConfig.DevelopmentMode = false;
                logConfig.MinimumLevel = LogLevel.Info;
                logConfig.EnableConsoleOutput = false;
#endif

                logConfig.LogApplicationLifecycle = true;
                logConfig.MaxFileSizeMB = 10;
                logConfig.MaxFileAgeDays = 30;

                LogManager.Initialize(logConfig);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로깅 시스템 초기화 완료");
                Logger.Info(CLASS_NAME, "InitializeLoggingSystem", "로깅 시스템 초기화 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로깅 시스템 초기화 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 전역 매니저들 초기화
        /// </summary>
        private void InitializeGlobalManagers()
        {
            try
            {
                // GlobalModeManager 초기화 (이미 정적으로 초기화되지만 명시적 호출)
                var currentMode = GlobalModeManager.CurrentMode;

                // PersistentDataManager 초기화는 MainWindow에서 수행

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 전역 매니저 초기화 완료 - 현재 모드: {currentMode}");
                Logger.Info(CLASS_NAME, "InitializeGlobalManagers", $"전역 매니저 초기화 완료 - 모드: {currentMode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 전역 매니저 초기화 실패: {ex.Message}");
                Logger.Error(CLASS_NAME, "InitializeGlobalManagers", "전역 매니저 초기화 실패", ex);
            }
        }

        /// <summary>
        /// 사용자 관리 시스템 초기화
        /// </summary>
        private async void InitializeUserManagement()
        {
            try
            {
                // UserManager 비동기 초기화 (기본 admin 계정 생성)
                bool initSuccess = await UserManager.InitializeAsync();

                if (initSuccess)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 관리 시스템 초기화 완료");
                    Logger.Info(CLASS_NAME, "InitializeUserManagement", "사용자 관리 시스템 초기화 완료");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 관리 시스템 초기화 실패");
                    Logger.Warning(CLASS_NAME, "InitializeUserManagement", "사용자 관리 시스템 초기화 실패");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 관리 시스템 초기화 오류: {ex.Message}");
                Logger.Error(CLASS_NAME, "InitializeUserManagement", "사용자 관리 시스템 초기화 오류", ex);
            }
        }

        /// <summary>
        /// 레시피 시스템 초기화 (비동기)
        /// </summary>
        private async System.Threading.Tasks.Task InitializeRecipeSystemAsync()
        {
            try
            {
                // 레시피 시스템 비동기 초기화
                // RecipeSystemTestHelper 또는 다른 초기화 로직 호출
                await System.Threading.Tasks.Task.Run(() =>
                {
                    // 레시피 폴더 생성, 기본 템플릿 생성 등
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 레시피 시스템 초기화 시작");
                });

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 레시피 시스템 초기화 완료");
                Logger.Info(CLASS_NAME, "InitializeRecipeSystemAsync", "레시피 시스템 초기화 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 레시피 시스템 초기화 실패: {ex.Message}");
                Logger.Error(CLASS_NAME, "InitializeRecipeSystemAsync", "레시피 시스템 초기화 실패", ex);
            }
        }
        #endregion

        #region Cleanup Methods

        /// <summary>
        /// 전역 매니저들 정리
        /// </summary>
        private void CleanupGlobalManagers()
        {
            try
            {
                // 필요한 매니저들의 정리 작업 수행
                // 현재는 특별한 정리가 필요하지 않음

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 전역 매니저 정리 완료");
                Logger.Info(CLASS_NAME, "CleanupGlobalManagers", "전역 매니저 정리 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 전역 매니저 정리 실패: {ex.Message}");
                Logger.Error(CLASS_NAME, "CleanupGlobalManagers", "전역 매니저 정리 실패", ex);
            }
        }

        /// <summary>
        /// 로깅 시스템 정리
        /// </summary>
        private void CleanupLoggingSystem()
        {
            try
            {
                Logger.Info(CLASS_NAME, "CleanupLoggingSystem", "TeachingPendant 애플리케이션 종료");
                LogManager.Shutdown();
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로깅 시스템 정리 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로깅 시스템 정리 실패: {ex.Message}");
            }
        }
        #endregion

        #region Exception Handling
        /// <summary>
        /// 처리되지 않은 예외 처리
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 처리되지 않은 예외 발생: {e.Exception.Message}");

                // 로깅 시도
                Logger.Critical(CLASS_NAME, "DispatcherException", "처리되지 않은 예외", e.Exception);

                string errorMessage = $"예상치 못한 오류가 발생했습니다:\n{e.Exception.Message}\n\n애플리케이션이 종료됩니다.";

                MessageBox.Show(errorMessage, "치명적 오류", MessageBoxButton.OK, MessageBoxImage.Error);

                e.Handled = true;
                this.Shutdown(1);
            }
            catch (Exception cleanupEx)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 예외 처리 중 추가 오류: {cleanupEx.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// 앱 종료 이벤트 처리
        /// </summary>
        private void App_Exit(object sender, ExitEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] App_Exit 이벤트 - 종료 코드: {e.ApplicationExitCode}");

                // OnExit에서 이미 정리 작업을 수행하므로 여기서는 로그만 기록
                Logger.Info(CLASS_NAME, "App_Exit", $"애플리케이션 종료 이벤트 - 코드: {e.ApplicationExitCode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] App_Exit 처리 중 오류: {ex.Message}");
            }
        }
        #endregion

        #region Static Methods
        /// <summary>
        /// 강제 종료 (외부에서 호출 가능)
        /// </summary>
        public static void ForceShutdown()
        {
            try
            {
                if (Application.Current != null)
                {
                    System.Diagnostics.Debug.WriteLine("[App] 강제 종료 요청");
                    Application.Current.Shutdown(0);
                }
                else
                {
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] 강제 종료 실패: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// 현재 메인 윈도우 반환
        /// </summary>
        /// <returns>메인 윈도우 인스턴스</returns>
        public static MainWindow GetMainWindow()
        {
            try
            {
                return (Application.Current as App)?._mainWindow;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] 메인 윈도우 가져오기 실패: {ex.Message}");
                return null;
            }
        }
        #endregion
    }
}