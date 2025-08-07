using System;
using System.Windows;
using System.Windows.Controls;
using TeachingPendant.RecipeSystem.UI.Views;
using TeachingPendant.UserManagement.Services;
using TeachingPendant.Logging;

namespace TeachingPendant.RecipeSystem.UI.Integration
{
    /// <summary>
    /// RecipeRunner를 CommonFrame에 통합하기 위한 헬퍼 클래스
    /// </summary>
    public static class RecipeRunnerIntegration
    {
        private static RecipeRunner _currentRecipeRunner;

        /// <summary>
        /// CommonFrame에 RecipeRunner 메뉴 항목 추가
        /// </summary>
        /// <param name="parentMenu">상위 메뉴 (예: Recipe 메뉴)</param>
        public static void AddRecipeRunnerMenuItem(Menu parentMenu)
        {
            try
            {
                // Recipe 메뉴 찾기 또는 생성
                MenuItem recipeMenu = FindOrCreateRecipeMenu(parentMenu);

                // RecipeRunner 메뉴 항목 생성
                var runnerMenuItem = new MenuItem
                {
                    Header = "레시피 실행기",
                    Tag = "RECIPE_EXECUTE" // 필요한 권한
                };
                runnerMenuItem.Click += RecipeRunnerMenuItem_Click;

                // 구분선 추가 (다른 메뉴 항목이 있는 경우)
                if (recipeMenu.Items.Count > 0)
                {
                    recipeMenu.Items.Add(new Separator());
                }

                recipeMenu.Items.Add(runnerMenuItem);

                Logger.Info("RecipeRunnerIntegration", "AddRecipeRunnerMenuItem", "RecipeRunner 메뉴 항목이 추가되었습니다.");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunnerIntegration", "AddRecipeRunnerMenuItem", "RecipeRunner 메뉴 항목 추가 중 오류 발생", ex);
            }
        }

        /// <summary>
        /// RecipeRunner 메뉴 항목 클릭 이벤트
        /// </summary>
        private static void RecipeRunnerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowRecipeRunner();
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunnerIntegration", "RecipeRunnerMenuItem_Click", "RecipeRunner 메뉴 클릭 처리 중 오류 발생", ex);
                MessageBox.Show("레시피 실행기를 열 수 없습니다: " + ex.Message,
                               "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// RecipeRunner 화면 표시
        /// </summary>
        public static void ShowRecipeRunner()
        {
            try
            {
                // 권한 확인
                if (!PermissionChecker.HasPermission("RECIPE_VIEW"))
                {
                    MessageBox.Show("레시피 조회 권한이 없습니다.", "권한 부족",
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 기존 RecipeRunner가 있으면 정리
                if (_currentRecipeRunner != null)
                {
                    _currentRecipeRunner.Dispose();
                    _currentRecipeRunner = null;
                }

                // 새 RecipeRunner 생성
                _currentRecipeRunner = new RecipeRunner();

                // CommonFrame의 메인 콘텐츠 영역에 표시
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    // CommonFrame에서 메인 콘텐츠 영역 찾기
                    var contentPresenter = FindContentPresenter(mainWindow);
                    if (contentPresenter != null)
                    {
                        contentPresenter.Content = _currentRecipeRunner;
                        Logger.Info("RecipeRunnerIntegration", "ShowRecipeRunner", "RecipeRunner가 표시되었습니다.");
                    }
                    else
                    {
                        // ContentPresenter를 찾을 수 없는 경우 새 창으로 표시
                        ShowRecipeRunnerInNewWindow();
                    }
                }
                else
                {
                    ShowRecipeRunnerInNewWindow();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunnerIntegration", "ShowRecipeRunner", "RecipeRunner 표시 중 오류 발생", ex);
                MessageBox.Show("레시피 실행기를 표시할 수 없습니다: " + ex.Message,
                               "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// RecipeRunner를 새 창에서 표시
        /// </summary>
        private static void ShowRecipeRunnerInNewWindow()
        {
            try
            {
                var window = new Window
                {
                    Title = "레시피 실행기",
                    Width = 1200,
                    Height = 800,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = _currentRecipeRunner
                };

                // 창 닫힐 때 리소스 정리
                window.Closed += (s, e) =>
                {
                    if (_currentRecipeRunner != null)
                    {
                        _currentRecipeRunner.Dispose();
                        _currentRecipeRunner = null;
                    }
                };

                window.Show();
                Logger.Info("RecipeRunnerIntegration", "ShowRecipeRunnerInNewWindow", "RecipeRunner가 새 창에서 표시되었습니다.");
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunnerIntegration", "ShowRecipeRunnerInNewWindow", "RecipeRunner 새 창 표시 중 오류 발생", ex);
            }
        }

        /// <summary>
        /// Recipe 메뉴 찾기 또는 생성
        /// </summary>
        private static MenuItem FindOrCreateRecipeMenu(Menu parentMenu)
        {
            try
            {
                // 기존 Recipe 메뉴 찾기
                foreach (var item in parentMenu.Items)
                {
                    if (item is MenuItem menuItem &&
                        (menuItem.Header.ToString().Contains("Recipe") ||
                         menuItem.Header.ToString().Contains("레시피")))
                    {
                        return menuItem;
                    }
                }

                // Recipe 메뉴가 없으면 생성
                var recipeMenu = new MenuItem
                {
                    Header = "레시피"
                };
                parentMenu.Items.Add(recipeMenu);

                return recipeMenu;
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunnerIntegration", "FindOrCreateRecipeMenu", "Recipe 메뉴 찾기/생성 중 오류 발생", ex);
                return null;
            }
        }

        /// <summary>
        /// ContentPresenter 찾기 (CommonFrame에서 메인 콘텐츠 영역)
        /// </summary>
        private static ContentPresenter FindContentPresenter(DependencyObject parent)
        {
            try
            {
                if (parent == null) return null;

                // 현재 객체가 ContentPresenter인지 확인
                if (parent is ContentPresenter presenter &&
                    presenter.Name == "MainContentPresenter") // CommonFrame에서 정의된 이름
                {
                    return presenter;
                }

                // 자식 요소들에서 재귀 검색
                int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < childCount; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                    var result = FindContentPresenter(child);
                    if (result != null)
                    {
                        return result;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunnerIntegration", "FindContentPresenter", "ContentPresenter 찾기 중 오류 발생", ex);
                return null;
            }
        }

        /// <summary>
        /// 특정 레시피로 RecipeRunner 시작
        /// </summary>
        /// <param name="recipe">실행할 레시피</param>
        public static void StartWithRecipe(TeachingPendant.RecipeSystem.Models.TransferRecipe recipe)
        {
            try
            {
                ShowRecipeRunner();

                if (_currentRecipeRunner != null && recipe != null)
                {
                    _currentRecipeRunner.LoadRecipe(recipe);
                    Logger.Info("RecipeRunnerIntegration", "StartWithRecipe", "RecipeRunner가 특정 레시피로 시작되었습니다: " + recipe.RecipeName);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunnerIntegration", "StartWithRecipe", "특정 레시피로 RecipeRunner 시작 중 오류 발생", ex);
            }
        }

        /// <summary>
        /// 현재 활성화된 RecipeRunner 인스턴스 반환
        /// </summary>
        public static RecipeRunner GetCurrentRecipeRunner()
        {
            return _currentRecipeRunner;
        }

        /// <summary>
        /// RecipeRunner 리소스 정리
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                if (_currentRecipeRunner != null)
                {
                    _currentRecipeRunner.Dispose();
                    _currentRecipeRunner = null;
                    Logger.Info("RecipeRunnerIntegration", "Cleanup", "RecipeRunner 리소스가 정리되었습니다.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunnerIntegration", "Cleanup", "RecipeRunner 정리 중 오류 발생", ex);
            }
        }
    }

    /// <summary>
    /// RecipeRunner 관련 유틸리티 메서드
    /// </summary>
    public static class RecipeRunnerUtilities
    {
        /// <summary>
        /// 사용자 권한에 따른 RecipeRunner 기능 제한 확인
        /// </summary>
        /// <returns>사용 가능한 기능 레벨</returns>
        public static RecipeRunnerAccessLevel GetAccessLevel()
        {
            try
            {
                if (PermissionChecker.HasPermission("RECIPE_EXECUTE"))
                {
                    return RecipeRunnerAccessLevel.FullAccess;
                }
                else if (PermissionChecker.HasPermission("RECIPE_VIEW"))
                {
                    return RecipeRunnerAccessLevel.ViewOnly;
                }
                else
                {
                    return RecipeRunnerAccessLevel.NoAccess;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunnerUtilities", "GetAccessLevel", "RecipeRunner 접근 레벨 확인 중 오류 발생", ex);
                return RecipeRunnerAccessLevel.NoAccess;
            }
        }

        /// <summary>
        /// RecipeRunner가 현재 실행 중인지 확인
        /// </summary>
        public static bool IsRecipeRunning()
        {
            try
            {
                var currentRunner = RecipeRunnerIntegration.GetCurrentRecipeRunner();
                return currentRunner != null; // 실제로는 실행 상태를 확인해야 함
            }
            catch (Exception ex)
            {
                Logger.Error("RecipeRunnerUtilities", "IsRecipeRunning", "RecipeRunner 실행 상태 확인 중 오류 발생", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// RecipeRunner 접근 레벨 정의
    /// </summary>
    public enum RecipeRunnerAccessLevel
    {
        NoAccess,    // 접근 불가
        ViewOnly,    // 조회만 가능
        FullAccess   // 모든 기능 사용 가능
    }
}