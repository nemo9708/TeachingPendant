using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TeachingPendant.RecipeSystem.Models;
using TeachingPendant.RecipeSystem.Storage;
using TeachingPendant.UserManagement.Models;
using TeachingPendant.UserManagement.Services;
using TeachingPendant.Logging;
using TeachingPendant.Alarm;

namespace TeachingPendant.RecipeSystem.UI.Views
{
    /// <summary>
    /// 레시피 관리자 UI - 권한 기반 레시피 목록 관리
    /// CommonFrame 패턴을 기반으로 한 사용자 친화적 인터페이스
    /// </summary>
    public partial class RecipeManager : UserControl, INotifyPropertyChanged
    {
        #region Private Fields
        private static readonly string CLASS_NAME = "RecipeManager";
        private ObservableCollection<TransferRecipe> _allRecipes;
        private ObservableCollection<TransferRecipe> _filteredRecipes;
        private TransferRecipe _selectedRecipe;
        private UserRole _currentUserRole;
        private string _searchText = "";
        private int _selectedFilterIndex = 0;
        #endregion

        #region Public Properties
        /// <summary>
        /// 현재 표시되는 레시피 목록
        /// </summary>
        public ObservableCollection<TransferRecipe> FilteredRecipes
        {
            get => _filteredRecipes;
            set
            {
                _filteredRecipes = value;
                OnPropertyChanged(nameof(FilteredRecipes));
            }
        }

        /// <summary>
        /// 선택된 레시피
        /// </summary>
        public TransferRecipe SelectedRecipe
        {
            get => _selectedRecipe;
            set
            {
                _selectedRecipe = value;
                OnPropertyChanged(nameof(SelectedRecipe));
                UpdateButtonStates();
                UpdateSelectedInfo();
            }
        }
        #endregion

        #region Constructor
        public RecipeManager()
        {
            InitializeComponent();
            InitializeData();
            InitializePermissions();
            LoadRecipeList();
        }
        #endregion

        #region Initialization Methods
        /// <summary>
        /// 데이터 초기화
        /// </summary>
        private void InitializeData()
        {
            try
            {
                _allRecipes = new ObservableCollection<TransferRecipe>();
                _filteredRecipes = new ObservableCollection<TransferRecipe>();

                // ListView 데이터 바인딩
                lvRecipes.ItemsSource = _filteredRecipes;

                // DataContext 설정
                this.DataContext = this;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 데이터 초기화 완료");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "InitializeData", "데이터 초기화 실패", ex);
            }
        }

        /// <summary>
        /// 권한 기반 UI 초기화
        /// </summary>
        private void InitializePermissions()
        {
            try
            {
                // 현재 사용자 권한 확인
                _currentUserRole = UserSession.IsLoggedIn ? UserSession.CurrentUserRole : UserRole.Guest;

                // 권한별 버튼 표시/숨김
                UpdatePermissionBasedUI();

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 권한 초기화 완료: {_currentUserRole.GetDescription()}");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "InitializePermissions", "권한 초기화 실패", ex);

                // 안전상 Guest 권한으로 설정
                _currentUserRole = UserRole.Guest;
                UpdatePermissionBasedUI();
            }
        }

        /// <summary>
        /// 권한별 UI 요소 제어
        /// </summary>
        private void UpdatePermissionBasedUI()
        {
            try
            {
                // 모든 버튼 기본적으로 숨김
                btnExecute.Visibility = Visibility.Collapsed;
                btnEdit.Visibility = Visibility.Collapsed;
                btnCopy.Visibility = Visibility.Collapsed;
                btnNew.Visibility = Visibility.Collapsed;
                btnDelete.Visibility = Visibility.Collapsed;
                btnBackup.Visibility = Visibility.Collapsed;
                btnTemplate.Visibility = Visibility.Collapsed;

                // 권한별 버튼 표시
                switch (_currentUserRole)
                {
                    case UserRole.Guest:
                        // Guest는 조회만 가능
                        break;

                    case UserRole.Operator:
                        // Operator는 실행 가능
                        btnExecute.Visibility = Visibility.Visible;
                        break;

                    case UserRole.Engineer:
                        // Engineer는 실행 + 편집/생성 가능
                        btnExecute.Visibility = Visibility.Visible;
                        btnEdit.Visibility = Visibility.Visible;
                        btnCopy.Visibility = Visibility.Visible;
                        btnNew.Visibility = Visibility.Visible;
                        break;

                    case UserRole.Administrator:
                        // Administrator는 모든 기능 가능
                        btnExecute.Visibility = Visibility.Visible;
                        btnEdit.Visibility = Visibility.Visible;
                        btnCopy.Visibility = Visibility.Visible;
                        btnNew.Visibility = Visibility.Visible;
                        btnDelete.Visibility = Visibility.Visible;
                        btnBackup.Visibility = Visibility.Visible;
                        btnTemplate.Visibility = Visibility.Visible;
                        break;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 권한별 UI 업데이트 완료: {_currentUserRole}");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "UpdatePermissionBasedUI", "권한별 UI 업데이트 실패", ex);
            }
        }
        #endregion

        #region Recipe Loading Methods
        /// <summary>
        /// 레시피 목록 로드
        /// </summary>
        private async void LoadRecipeList()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 레시피 목록 로드 시작");

                // 기존 목록 클리어
                _allRecipes.Clear();

                // 파일에서 레시피 목록 로드
                var recipeFiles = RecipeFileManager.GetRecipeFileList();

                foreach (var fileInfo in recipeFiles)
                {
                    var recipe = await RecipeFileManager.LoadRecipeAsync(fileInfo.FileName);
                    if (recipe != null)
                    {
                        _allRecipes.Add(recipe);
                    }
                }

                // 필터 적용
                ApplyFilter();

                // 카운트 업데이트
                UpdateRecipeCount();

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 레시피 {_allRecipes.Count}개 로드 완료");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "LoadRecipeList", "레시피 목록 로드 실패", ex);
                MessageBox.Show("레시피 목록을 불러오는 중 오류가 발생했습니다.", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 필터 적용
        /// </summary>
        private void ApplyFilter()
        {
            try
            {
                var filteredList = _allRecipes.AsEnumerable();

                // 검색 텍스트 필터
                if (!string.IsNullOrEmpty(_searchText))
                {
                    filteredList = filteredList.Where(r =>
                        r.RecipeName.Contains(_searchText) ||
                        r.Description.Contains(_searchText));
                }

                // 카테고리 필터
                switch (_selectedFilterIndex)
                {
                    case 1: // 활성화된 레시피
                        filteredList = filteredList.Where(r => r.IsEnabled);
                        break;
                    case 2: // 비활성화된 레시피
                        filteredList = filteredList.Where(r => !r.IsEnabled);
                        break;
                    case 3: // 최근 생성순
                        filteredList = filteredList.OrderByDescending(r => r.CreatedDate);
                        break;
                    case 4: // 최근 수정순
                        filteredList = filteredList.OrderByDescending(r => r.ModifiedDate);
                        break;
                    default: // 모든 레시피
                        break;
                }

                // 필터된 목록 업데이트
                FilteredRecipes.Clear();
                foreach (var recipe in filteredList)
                {
                    FilteredRecipes.Add(recipe);
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 필터 적용 완료: {FilteredRecipes.Count}개 표시");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ApplyFilter", "필터 적용 실패", ex);
            }
        }

        /// <summary>
        /// 레시피 개수 정보 업데이트
        /// </summary>
        private void UpdateRecipeCount()
        {
            try
            {
                txtRecipeCount.Text = $"총 {FilteredRecipes.Count}개 레시피";
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "UpdateRecipeCount", "레시피 개수 업데이트 실패", ex);
            }
        }

        /// <summary>
        /// 선택된 레시피 정보 업데이트
        /// </summary>
        private void UpdateSelectedInfo()
        {
            try
            {
                if (SelectedRecipe != null)
                {
                    txtSelectedInfo.Text = $"선택: {SelectedRecipe.RecipeName} ({SelectedRecipe.StepCount}스텝)";
                }
                else
                {
                    txtSelectedInfo.Text = "선택된 레시피 없음";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "UpdateSelectedInfo", "선택 정보 업데이트 실패", ex);
            }
        }

        /// <summary>
        /// 버튼 활성화 상태 업데이트
        /// </summary>
        private void UpdateButtonStates()
        {
            try
            {
                bool hasSelection = SelectedRecipe != null;

                btnExecute.IsEnabled = hasSelection && SelectedRecipe?.IsEnabled == true;
                btnEdit.IsEnabled = hasSelection;
                btnCopy.IsEnabled = hasSelection;
                btnDelete.IsEnabled = hasSelection;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "UpdateButtonStates", "버튼 상태 업데이트 실패", ex);
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 검색 텍스트 변경 이벤트
        /// </summary>
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _searchText = txtSearch.Text ?? "";
                ApplyFilter();
                UpdateRecipeCount();
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "txtSearch_TextChanged", "검색 필터 적용 실패", ex);
            }
        }

        /// <summary>
        /// 필터 선택 변경 이벤트
        /// </summary>
        private void cmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _selectedFilterIndex = cmbFilter.SelectedIndex;
                ApplyFilter();
                UpdateRecipeCount();
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "cmbFilter_SelectionChanged", "카테고리 필터 적용 실패", ex);
            }
        }

        /// <summary>
        /// 새로고침 버튼 클릭
        /// </summary>
        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadRecipeList();
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnRefresh_Click", "새로고침 실패", ex);
                MessageBox.Show("새로고침 중 오류가 발생했습니다.", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 레시피 선택 변경 이벤트
        /// </summary>
        private void lvRecipes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                SelectedRecipe = lvRecipes.SelectedItem as TransferRecipe;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "lvRecipes_SelectionChanged", "레시피 선택 처리 실패", ex);
            }
        }

        /// <summary>
        /// 레시피 더블클릭 - 편집 또는 실행
        /// </summary>
        private void lvRecipes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (SelectedRecipe == null) return;

                // 권한에 따라 다른 동작
                if (_currentUserRole >= UserRole.Engineer)
                {
                    // 엔지니어 이상은 편집
                    btnEdit_Click(sender, new RoutedEventArgs());
                }
                else if (_currentUserRole >= UserRole.Operator && SelectedRecipe.IsEnabled)
                {
                    // 운영자는 실행
                    btnExecute_Click(sender, new RoutedEventArgs());
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "lvRecipes_MouseDoubleClick", "더블클릭 처리 실패", ex);
            }
        }
        #endregion

        #region Button Event Handlers - 다음 단계에서 구현
        private void btnExecute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SelectedRecipe == null)
                {
                    MessageBox.Show("실행할 레시피를 선택하세요.", "알림",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (!SelectedRecipe.IsEnabled)
                {
                    MessageBox.Show("비활성화된 레시피는 실행할 수 없습니다.", "알림",
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 권한 확인
                if (!PermissionChecker.HasPermission("RECIPE_EXECUTE"))
                {
                    MessageBox.Show("레시피 실행 권한이 없습니다.", "권한 부족",
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // RecipeRunner로 전환하면서 선택된 레시피 전달
                var result = MessageBox.Show(
                    $"레시피 '{SelectedRecipe.RecipeName}'을 실행하시겠습니까?\n\n" +
                    $"스텝 수: {SelectedRecipe.StepCount}개\n" +
                    $"예상 시간: {SelectedRecipe.EstimatedExecutionTime:F1}초",
                    "레시피 실행 확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // RecipeRunner 화면으로 전환하면서 레시피 전달
                    OpenRecipeRunnerWithRecipe(SelectedRecipe);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnExecute_Click", "레시피 실행 실패", ex);
                MessageBox.Show($"레시피 실행 중 오류가 발생했습니다: {ex.Message}",
                               "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 레시피 편집 버튼 클릭
        /// </summary>
        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SelectedRecipe == null)
                {
                    MessageBox.Show("편집할 레시피를 선택하세요.", "알림",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 권한 확인
                if (!UserSession.IsLoggedIn || UserSession.CurrentUser.Role < UserRole.Engineer)
                {
                    MessageBox.Show("레시피 편집 권한이 없습니다.\nEngineer 이상의 권한이 필요합니다.",
                                   "권한 부족", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // RecipeEditor 화면으로 전환하면서 레시피 전달
                OpenRecipeEditorWithRecipe(SelectedRecipe);
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnEdit_Click", "레시피 편집 실패", ex);
                MessageBox.Show($"레시피 편집 중 오류가 발생했습니다: {ex.Message}",
                               "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 레시피 복사 버튼 클릭
        /// </summary>
        private void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SelectedRecipe == null)
                {
                    MessageBox.Show("복사할 레시피를 선택하세요.", "알림",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 복사된 레시피 생성
                var copiedRecipe = SelectedRecipe.Clone();
                copiedRecipe.RecipeName = $"{SelectedRecipe.RecipeName}_Copy";
                copiedRecipe.Description = $"복사본: {SelectedRecipe.Description}";
                copiedRecipe.CreatedDate = DateTime.Now;
                copiedRecipe.ModifiedDate = DateTime.Now;

                // 파일로 저장
                string fileName = $"{copiedRecipe.RecipeName}_{DateTime.Now:yyyyMMdd_HHmmss}.recipe";
                bool saveResult = RecipeFileManager.SaveRecipe(copiedRecipe, fileName);

                if (saveResult)
                {
                    // 목록에 추가
                    _allRecipes.Add(copiedRecipe);
                    ApplyFilter();
                    UpdateRecipeCount();

                    // 복사된 레시피 선택
                    SelectedRecipe = copiedRecipe;

                    MessageBox.Show($"레시피가 복사되었습니다.\n새 이름: {copiedRecipe.RecipeName}",
                                   "복사 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                    AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE,
                        $"레시피 복사 완료: {copiedRecipe.RecipeName}");
                }
                else
                {
                    MessageBox.Show("레시피 복사에 실패했습니다.", "복사 실패",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnCopy_Click", "레시피 복사 실패", ex);
                MessageBox.Show($"레시피 복사 중 오류가 발생했습니다: {ex.Message}",
                               "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 새 레시피 생성 버튼 클릭
        /// </summary>
        private void btnNew_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 권한 확인
                if (!UserSession.IsLoggedIn || UserSession.CurrentUser.Role < UserRole.Engineer)
                {
                    MessageBox.Show("새 레시피 생성 권한이 없습니다.\nEngineer 이상의 권한이 필요합니다.",
                                   "권한 부족", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 새 레시피 생성
                var newRecipe = new TransferRecipe
                {
                    RecipeName = $"새_레시피_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Description = "새로 생성된 레시피",
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now,
                    IsEnabled = true
                };

                // RecipeEditor 화면으로 전환하면서 새 레시피 전달
                OpenRecipeEditorWithRecipe(newRecipe);
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnNew_Click", "새 레시피 생성 실패", ex);
                MessageBox.Show($"새 레시피 생성 중 오류가 발생했습니다: {ex.Message}",
                               "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 레시피 삭제 버튼 클릭
        /// </summary>
        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SelectedRecipe == null)
                {
                    MessageBox.Show("삭제할 레시피를 선택하세요.", "알림",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 권한 확인
                if (!UserSession.IsLoggedIn || UserSession.CurrentUser.Role < UserRole.Administrator)
                {
                    MessageBox.Show("레시피 삭제 권한이 없습니다.\nAdministrator 권한이 필요합니다.",
                                   "권한 부족", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 삭제 확인
                var result = MessageBox.Show(
                    $"레시피 '{SelectedRecipe.RecipeName}'을 완전히 삭제하시겠습니까?\n\n" +
                    "이 작업은 되돌릴 수 없습니다.",
                    "레시피 삭제 확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // 파일 삭제
                    bool deleteResult = RecipeFileManager.DeleteRecipe(SelectedRecipe.RecipeName);

                    if (deleteResult)
                    {
                        // 목록에서 제거
                        _allRecipes.Remove(SelectedRecipe);
                        ApplyFilter();
                        UpdateRecipeCount();

                        // 선택 해제
                        SelectedRecipe = null;

                        MessageBox.Show("레시피가 삭제되었습니다.", "삭제 완료",
                                       MessageBoxButton.OK, MessageBoxImage.Information);

                        AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "레시피 삭제 완료");
                    }
                    else
                    {
                        MessageBox.Show("레시피 삭제에 실패했습니다.", "삭제 실패",
                                       MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnDelete_Click", "레시피 삭제 실패", ex);
                MessageBox.Show($"레시피 삭제 중 오류가 발생했습니다: {ex.Message}",
                               "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 백업 버튼 클릭
        /// </summary>
        private void btnBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 권한 확인
                if (!UserSession.IsLoggedIn || UserSession.CurrentUser.Role < UserRole.Administrator)
                {
                    MessageBox.Show("백업 권한이 없습니다.\nAdministrator 권한이 필요합니다.",
                                   "권한 부족", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 전체 레시피 백업
                var result = MessageBox.Show(
                    "모든 레시피를 백업하시겠습니까?\n\n" +
                    "백업은 현재 날짜와 시간으로 저장됩니다.",
                    "레시피 백업",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    bool backupResult = RecipeFileManager.CreateBackup();

                    if (backupResult)
                    {
                        MessageBox.Show("모든 레시피가 백업되었습니다.", "백업 완료",
                                       MessageBoxButton.OK, MessageBoxImage.Information);

                        AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "레시피 백업 완료");
                    }
                    else
                    {
                        MessageBox.Show("백업에 실패했습니다.", "백업 실패",
                                       MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnBackup_Click", "백업 실패", ex);
                MessageBox.Show($"백업 중 오류가 발생했습니다: {ex.Message}",
                               "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 템플릿 관리 버튼 클릭
        /// </summary>
        private void btnTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 권한 확인
                if (!UserSession.IsLoggedIn || UserSession.CurrentUser.Role < UserRole.Engineer)
                {
                    MessageBox.Show("템플릿 관리 권한이 없습니다.\nEngineer 이상의 권한이 필요합니다.",
                                   "권한 부족", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 템플릿 선택 대화상자 표시
                ShowTemplateSelectionDialog();
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnTemplate_Click", "템플릿 관리 실패", ex);
                MessageBox.Show($"템플릿 관리 중 오류가 발생했습니다: {ex.Message}",
                               "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Helper Methods for Screen Navigation

        /// <summary>
        /// RecipeRunner 화면으로 전환하면서 레시피 전달
        /// </summary>
        private void OpenRecipeRunnerWithRecipe(TransferRecipe recipe)
        {
            try
            {
                // CommonFrame을 통해 RecipeRunner로 전환
                var parentWindow = Window.GetWindow(this) as CommonFrame;
                if (parentWindow != null)
                {
                    parentWindow.SwitchToRecipeRunnerWithRecipe(recipe);
                }
                else
                {
                    // CommonFrame을 찾을 수 없는 경우 기본 전환
                    System.Diagnostics.Debug.WriteLine("[RecipeManager] CommonFrame을 찾을 수 없음, 기본 전환 시도");

                    // 현재 창에서 직접 RecipeRunner 찾기 시도
                    FindAndLoadRecipeRunner(recipe);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "OpenRecipeRunnerWithRecipe", "RecipeRunner 전환 실패", ex);
                MessageBox.Show($"RecipeRunner 전환 중 오류가 발생했습니다: {ex.Message}",
                               "전환 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 직접 RecipeRunner 찾아서 레시피 로드 (fallback 방법)
        /// </summary>
        private void FindAndLoadRecipeRunner(TransferRecipe recipe)
        {
            try
            {
                // 현재 애플리케이션에서 RecipeRunner 찾기
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is CommonFrame commonFrame)
                    {
                        commonFrame.SwitchToRecipeRunnerWithRecipe(recipe);
                        return;
                    }
                }

                // 찾지 못한 경우 알림
                MessageBox.Show("RecipeRunner를 찾을 수 없습니다.\n메인 메뉴에서 Recipe Runner를 선택해주세요.",
                               "화면 전환 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "FindAndLoadRecipeRunner", "RecipeRunner 찾기 실패", ex);
            }
        }


        /// <summary>
        /// RecipeEditor 화면으로 전환하면서 레시피 전달
        /// </summary>
        private void OpenRecipeEditorWithRecipe(TransferRecipe recipe)
        {
            try
            {
                // CommonFrame을 통해 RecipeEditor로 전환
                var parentWindow = Window.GetWindow(this) as CommonFrame;
                if (parentWindow != null)
                {
                    parentWindow.SwitchToRecipeEditorWithRecipe(recipe);
                }
                else
                {
                    // CommonFrame을 찾을 수 없는 경우 기본 전환
                    System.Diagnostics.Debug.WriteLine("[RecipeManager] CommonFrame을 찾을 수 없음, 기본 전환 시도");

                    // 현재 창에서 직접 RecipeEditor 찾기 시도
                    FindAndLoadRecipeEditor(recipe);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "OpenRecipeEditorWithRecipe", "RecipeEditor 전환 실패", ex);
                MessageBox.Show($"RecipeEditor 전환 중 오류가 발생했습니다: {ex.Message}",
                               "전환 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FindAndLoadRecipeEditor(TransferRecipe recipe)
        {
            try
            {
                // 현재 애플리케이션에서 RecipeEditor 찾기
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is CommonFrame commonFrame)
                    {
                        commonFrame.SwitchToRecipeEditorWithRecipe(recipe);
                        return;
                    }
                }

                // 찾지 못한 경우 알림
                MessageBox.Show("RecipeEditor를 찾을 수 없습니다.\n메인 메뉴에서 Recipe Editor를 선택해주세요.",
                               "화면 전환 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "FindAndLoadRecipeEditor", "RecipeEditor 찾기 실패", ex);
            }
        }

        /// <summary>
        /// 템플릿 선택 대화상자 표시
        /// </summary>
        private void ShowTemplateSelectionDialog()
        {
            try
            {
                var templateNames = RecipeFileManager.GetTemplateList();

                if (templateNames.Count == 0)
                {
                    MessageBox.Show("사용 가능한 템플릿이 없습니다.", "템플릿 없음",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 간단한 템플릿 선택 (실제로는 별도 대화상자 구현 필요)
                string templateList = string.Join("\n", templateNames.Select((t, i) => $"{i + 1}. {t}"));

                var result = MessageBox.Show(
                    $"사용 가능한 템플릿:\n\n{templateList}\n\n" +
                    "템플릿에서 새 레시피를 생성하시겠습니까?",
                    "템플릿 선택",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 첫 번째 템플릿 사용 (실제로는 사용자 선택 구현 필요)
                    CreateRecipeFromTemplate(templateNames[0]);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "ShowTemplateSelectionDialog", "템플릿 선택 대화상자 실패", ex);
                throw;
            }
        }

        /// <summary>
        /// 템플릿으로부터 새 레시피 생성
        /// </summary>
        private void CreateRecipeFromTemplate(string templateName)
        {
            try
            {
                var templateRecipe = RecipeFileManager.LoadTemplate(templateName);

                if (templateRecipe != null)
                {
                    // 템플릿을 기반으로 새 레시피 생성
                    var newRecipe = templateRecipe.Clone();
                    newRecipe.RecipeName = $"템플릿_{templateName}_{DateTime.Now:yyyyMMdd_HHmmss}";
                    newRecipe.Description = $"템플릿 '{templateName}'에서 생성됨";
                    newRecipe.CreatedDate = DateTime.Now;
                    newRecipe.ModifiedDate = DateTime.Now;

                    // RecipeEditor로 전환
                    OpenRecipeEditorWithRecipe(newRecipe);
                }
                else
                {
                    MessageBox.Show($"템플릿 '{templateName}' 로드에 실패했습니다.", "템플릿 오류",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "CreateRecipeFromTemplate", $"템플릿 '{templateName}' 레시피 생성 실패", ex);
                throw;
            }
        }

        #endregion

        #region Public Methods for External Access

        /// <summary>
        /// 외부에서 레시피 목록 새로고침 요청 (RecipeEditor에서 저장 후 호출)
        /// </summary>
        public void RefreshRecipeList()
        {
            try
            {
                LoadRecipeList();
                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "레시피 목록이 새로고침되었습니다.");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "RefreshRecipeList", "외부 새로고침 실패", ex);
            }
        }

        /// <summary>
        /// 특정 레시피를 목록에서 선택
        /// </summary>
        /// <param name="recipeName">선택할 레시피 이름</param>
        public void SelectRecipeByName(string recipeName)
        {
            try
            {
                if (string.IsNullOrEmpty(recipeName)) return;

                var recipe = FilteredRecipes.FirstOrDefault(r => r.RecipeName == recipeName);
                if (recipe != null)
                {
                    SelectedRecipe = recipe;

                    // ListView에서도 선택 (UI 동기화)
                    if (lvRecipes != null)
                    {
                        lvRecipes.SelectedItem = recipe;
                        lvRecipes.ScrollIntoView(recipe);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "SelectRecipeByName", $"레시피 선택 실패: {recipeName}", ex);
            }
        }

        /// <summary>
        /// 새로운 레시피가 생성되었을 때 목록에 추가
        /// </summary>
        /// <param name="recipe">추가할 레시피</param>
        public void AddNewRecipeToList(TransferRecipe recipe)
        {
            try
            {
                if (recipe == null) return;

                // 기존 목록에 추가
                _allRecipes.Add(recipe);
                ApplyFilter();
                UpdateRecipeCount();

                // 새로 추가된 레시피 선택
                SelectedRecipe = recipe;

                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE,
                    $"새 레시피가 목록에 추가되었습니다: {recipe.RecipeName}");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "AddNewRecipeToList", "새 레시피 추가 실패", ex);
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}