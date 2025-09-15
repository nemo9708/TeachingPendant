using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TeachingPendant.RecipeSystem.Models;
using TeachingPendant.HardwareControllers;
using TeachingPendant.RecipeSystem.Storage;
using TeachingPendant.RecipeSystem.Teaching;
using TeachingPendant.UserManagement.Models;
using TeachingPendant.UserManagement.Services;
using TeachingPendant.Logging;
using TeachingPendant.Alarm;
using TeachingPendant.SetupUI;

namespace TeachingPendant.RecipeSystem.UI.Views
{
    /// <summary>
    /// 레시피 편집기 UI - 드래그&드롭 기반 스텝 편집 및 Teaching 연동
    /// Visual Studio 디자이너와 유사한 직관적 편집 환경
    /// </summary>
    public partial class RecipeEditor : UserControl, INotifyPropertyChanged
    {
        #region Private Fields
        private static readonly string CLASS_NAME = "RecipeEditor";
        private TransferRecipe _currentRecipe;
        private RecipeStep _selectedStep;
        private bool _isDragging = false;
        private bool _isModified = false;
        private Point _dragStartPoint;
        private List<string> _availableTeachingGroups;
        private List<string> _availableTeachingLocations;
        private UserRole _currentUserRole;
        #endregion

        #region Public Properties - 기존 프로퍼티들
        /// <summary>
        /// 현재 편집 중인 레시피
        /// </summary>
        public TransferRecipe CurrentRecipe
        {
            get { return _currentRecipe; }
            set
            {
                _currentRecipe = value;
                OnPropertyChanged("CurrentRecipe");
                LoadRecipeForEditing();
            }
        }

        /// <summary>
        /// 선택된 스텝
        /// </summary>
        public RecipeStep SelectedStep
        {
            get { return _selectedStep; }
            set
            {
                _selectedStep = value;
                OnPropertyChanged("SelectedStep");
                UpdateStepDetailsPanel();
            }
        }

        /// <summary>
        /// 수정 여부
        /// </summary>
        public bool IsModified
        {
            get { return _isModified; }
            set
            {
                _isModified = value;
                OnPropertyChanged("IsModified");
                UpdateModifiedIndicator();
            }
        }
        #endregion

        #region Constructor
        public RecipeEditor()
        {
            InitializeComponent();
            InitializeEditor();
        }
        #endregion

        #region Initialization Methods
        /// <summary>
        /// 편집기 초기화
        /// </summary>
        private void InitializeEditor()
        {
            try
            {
                // 현재 사용자 권한 확인
                _currentUserRole = UserSession.IsLoggedIn ?
                    UserSession.CurrentUser.Role : UserRole.Guest;

                // 권한 기반 UI 초기화
                InitializeUIForUserRole();

                // Teaching 시스템 연동 초기화
                InitializeTeachingIntegration();

                // 기본 레시피 생성 (편집기 시작 시)
                CreateNewRecipe();

                // Setup의 HomePos 변경 시 첫 번째 스텝 좌표 갱신
                Setup.HomePosChanged += Setup_HomePosChanged;
                this.Unloaded += (s, e) => Setup.HomePosChanged -= Setup_HomePosChanged;

                // 초기 상태 설정
                IsModified = false;
                SelectedStep = null;

                Logger.Info(CLASS_NAME, "InitializeEditor",
                    $"Recipe Editor initialized successfully (Role: {_currentUserRole})");

                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE,
                    "Recipe Editor is ready.");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "InitializeEditor", "Recipe Editor initialization failed", ex);

                // 오류 시에도 기본 상태로 설정
                CreateNewRecipe();
                IsModified = false;

                MessageBox.Show($"An error occurred during Recipe Editor initialization.\n" +
                                $"Basic functionality is still available.\n\n" +
                                $"Error: {ex.Message}",
                                "Initialization Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 사용자 권한에 따른 UI 초기화
        /// </summary>
        private void InitializeUIForUserRole()
        {
            try
            {
                // 권한별 기능 제한
                switch (_currentUserRole)
                {
                    case UserRole.Guest:
                        // Guest는 읽기 전용
                        SetEditorReadOnly(true);
                        break;

                    case UserRole.Operator:
                        // Operator는 기본 편집 가능, 고급 기능 제한
                        SetEditorReadOnly(false);
                        EnableAdvancedFeatures(false);
                        break;

                    case UserRole.Engineer:
                    case UserRole.Administrator:
                        // Engineer 이상은 모든 기능 사용 가능
                        SetEditorReadOnly(false);
                        EnableAdvancedFeatures(true);
                        break;
                }

                Logger.Info(CLASS_NAME, "InitializeUIForUserRole",
                    $"UI permission setup complete: {_currentUserRole}");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "InitializeUIForUserRole", "UI permission setup failed", ex);
            }
        }

        /// <summary>
        /// 편집기 읽기 전용 모드 설정
        /// </summary>
        private void SetEditorReadOnly(bool isReadOnly)
        {
            try
            {
                // 스텝 팔레트 버튼들 비활성화
                btnAddHome.IsEnabled = !isReadOnly;
                btnAddMove.IsEnabled = !isReadOnly;
                btnAddPick.IsEnabled = !isReadOnly;
                btnAddPlace.IsEnabled = !isReadOnly;
                btnAddWait.IsEnabled = !isReadOnly;
                btnAddCheckSafety.IsEnabled = !isReadOnly;
                btnAddPickSequence.IsEnabled = !isReadOnly;
                btnAddSafetyCheck.IsEnabled = !isReadOnly;

                // 편집 버튼들 비활성화
                btnSave.IsEnabled = !isReadOnly;
                btnValidate.IsEnabled = true; // 검증은 항상 가능

                // 속성 편집 비활성화
                txtRecipeNameEdit.IsReadOnly = isReadOnly;
                txtRecipeDescriptionEdit.IsReadOnly = isReadOnly;
                btnApplyTeaching.IsEnabled = !isReadOnly && cmbTeachingLocation.SelectedItem != null;

                if (isReadOnly)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.WARNING,
                        "Read-only mode for the current user role.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "SetEditorReadOnly", "Failed to set read-only mode", ex);
            }
        }

        /// <summary>
        /// 고급 기능 활성화/비활성화
        /// </summary>
        private void EnableAdvancedFeatures(bool enable)
        {
            try
            {
                // 템플릿 기능은 Engineer 이상만 사용 가능
                btnAddPickSequence.IsEnabled = enable;
                btnAddSafetyCheck.IsEnabled = enable;

                // Teaching 연동은 Engineer 이상만 사용 가능
                cmbTeachingGroup.IsEnabled = enable;
                cmbTeachingLocation.IsEnabled = enable;
                btnApplyTeaching.IsEnabled = enable && cmbTeachingLocation.SelectedItem != null;

                if (!enable && _currentUserRole == UserRole.Operator)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.WARNING,
                        "Some advanced features require Engineer privileges.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "EnableAdvancedFeatures", "Failed to set advanced features", ex);
            }
        }

        /// <summary>
        /// Teaching 시스템 연동 초기화
        /// </summary>
        private void InitializeTeachingIntegration()
        {
            try
            {
                // Teaching 그룹 목록 로드
                LoadTeachingGroups();

                Logger.Info(CLASS_NAME, "InitializeTeachingIntegration",
                    "Teaching system integration initialized.");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "InitializeTeachingIntegration",
                    "Teaching system integration initialization failed", ex);

                // Teaching 연동 실패 시 기본값으로 설정
                _availableTeachingGroups = new List<string> { "Group1" };
                _availableTeachingLocations = new List<string> { "P1", "P2", "P3", "P4", "P5", "P6", "P7" };

                cmbTeachingGroup.ItemsSource = _availableTeachingGroups;
                cmbTeachingLocation.ItemsSource = _availableTeachingLocations;
            }
        }

        /// <summary>
        /// Teaching 데이터 로드
        /// </summary>
        private void LoadTeachingData()
        {
            try
            {
                // Teaching 그룹 목록 로드
                _availableTeachingGroups = TeachingDataIntegration.GetAvailableGroups();
                cmbTeachingGroup.ItemsSource = _availableTeachingGroups;
                cmbStepTeachingGroup.ItemsSource = _availableTeachingGroups;

                if (_availableTeachingGroups.Count > 0)
                {
                    cmbTeachingGroup.SelectedIndex = 0;
                    cmbStepTeachingGroup.SelectedIndex = 0;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Teaching data loaded: {_availableTeachingGroups.Count} groups");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "LoadTeachingData", "Failed to load Teaching data", ex);
            }
        }

        /// <summary>
        /// 새 레시피 생성
        /// </summary>
        private void CreateNewRecipe()
        {
            try
            {
                _currentRecipe = new TransferRecipe("New Recipe", "Custom user recipe");

                // 기본 스텝들 추가
                var homeStep = new RecipeStep(StepType.Home, "Start - Move to Home")
                {
                    CoordinateSource = CoordinateSourceType.Setup,
                    TargetPosition = new Position((double)Setup.HomePosA,
                                                  (double)Setup.HomePosT,
                                                  (double)Setup.HomePosZ)
                };
                _currentRecipe.AddStep(homeStep);
                _currentRecipe.AddStep(new RecipeStep(StepType.CheckSafety, "Check safety status"));

                LoadRecipeForEditing();
                IsModified = false;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] New recipe created successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "CreateNewRecipe", "Failed to create new recipe", ex);
            }
        }

        /// <summary>
        /// 레시피를 편집용으로 로드
        /// </summary>
        private void LoadRecipeForEditing()
        {
            try
            {
                if (_currentRecipe == null) return;

                // UI 업데이트
                txtRecipeName.Text = _currentRecipe.RecipeName;
                txtRecipeNameEdit.Text = _currentRecipe.RecipeName;
                txtRecipeDescriptionEdit.Text = _currentRecipe.Description;

                // 스텝 목록 업데이트
                RefreshStepList();

                // 정보 업데이트
                UpdateStatusInfo();

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Recipe loaded successfully: {_currentRecipe.RecipeName}");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "LoadRecipeForEditing", "Failed to load recipe", ex);
            }
        }
        #endregion

        #region Step Management Methods
        /// <summary>
        /// 새 스텝 추가
        /// </summary>
        private void AddNewStep(StepType stepType)
        {
            try
            {
                if (_currentRecipe == null) return;

                var newStep = new RecipeStep(stepType, GetDefaultDescription(stepType));

                // 기본 좌표 출처 설정
                if (stepType == StepType.Home)
                {
                    newStep.CoordinateSource = CoordinateSourceType.Setup;
                    newStep.TargetPosition = new Position((double)Setup.HomePosA,
                                                          (double)Setup.HomePosT,
                                                          (double)Setup.HomePosZ);
                }
                else
                {
                    newStep.CoordinateSource = CoordinateSourceType.Teaching;
                }

                // Teaching 정보 자동 설정
                if (cmbTeachingGroup.SelectedItem != null)
                {
                    newStep.TeachingGroupName = cmbTeachingGroup.SelectedItem.ToString();

                    if (cmbTeachingLocation.SelectedItem != null)
                    {
                        newStep.TeachingLocationName = cmbTeachingLocation.SelectedItem.ToString();
                    }
                }

                // 스텝을 레시피에 추가
                _currentRecipe.AddStep(newStep);

                // UI 업데이트
                RefreshStepList();
                UpdateStepNumbers();
                UpdateStatusInfo();
                IsModified = true;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] New step added: {stepType} (Total {_currentRecipe.StepCount} steps)");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "AddNewStep", "Failed to add new step", ex);
            }
        }

        private void RefreshStepList()
        {
            try
            {
                if (_currentRecipe?.Steps != null)
                {
                    // ItemsSource를 다시 설정하여 UI 업데이트 강제
                    icStepList.ItemsSource = null;
                    icStepList.ItemsSource = _currentRecipe.Steps;

                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Step list refreshed: {_currentRecipe.Steps.Count} steps");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "RefreshStepList", "Failed to refresh step list", ex);
            }
        }

        /// <summary>
        /// 스텝 타입별 기본 설명 반환
        /// </summary>
        private string GetDefaultDescription(StepType stepType)
        {
            switch (stepType)
            {
                case StepType.Home: return "Move to Home position";
                case StepType.Move: return "Move to specified position";
                case StepType.Pick: return "Pick wafer";
                case StepType.Place: return "Place wafer";
                case StepType.Wait: return "Wait";
                case StepType.CheckSafety: return "Check safety status";
                default: return stepType.ToString();
            }
        }

        /// <summary>
        /// 스텝 번호 업데이트
        /// </summary>
        private void UpdateStepNumbers()
        {
            try
            {
                if (_currentRecipe?.Steps == null) return;

                for (int i = 0; i < _currentRecipe.Steps.Count; i++)
                {
                    _currentRecipe.Steps[i].StepNumber = i + 1;
                }

                // UI 새로고침
                RefreshStepList();
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "UpdateStepNumbers", "Failed to update step numbers", ex);
            }
        }

        /// <summary>
        /// 스텝 위로 이동
        /// </summary>
        private void MoveStepUp(RecipeStep step)
        {
            try
            {
                if (_currentRecipe?.Steps == null) return;

                int index = _currentRecipe.Steps.IndexOf(step);
                if (index > 0)
                {
                    // 스텝 순서 바꾸기
                    _currentRecipe.Steps.RemoveAt(index);
                    _currentRecipe.Steps.Insert(index - 1, step);

                    // UI 업데이트
                    UpdateStepNumbers();
                    RefreshStepList();
                    IsModified = true;

                    AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                        $"Step {step.StepNumber} was moved up.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "MoveStepUp", "Failed to move step up", ex);
            }
        }

        /// <summary>
        /// 스텝 아래로 이동
        /// </summary>
        private void MoveStepDown(RecipeStep step)
        {
            try
            {
                if (_currentRecipe?.Steps == null) return;

                int index = _currentRecipe.Steps.IndexOf(step);
                if (index >= 0 && index < _currentRecipe.Steps.Count - 1)
                {
                    // 스텝 순서 바꾸기
                    _currentRecipe.Steps.RemoveAt(index);
                    _currentRecipe.Steps.Insert(index + 1, step);

                    // UI 업데이트
                    UpdateStepNumbers();
                    RefreshStepList();
                    IsModified = true;

                    AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                        $"Step {step.StepNumber} was moved down.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "MoveStepDown", "Failed to move step down", ex);
            }
        }

        /// <summary>
        /// 스텝 삭제
        /// </summary>
        private void DeleteStep(RecipeStep step)
        {
            try
            {
                var result = MessageBox.Show(
                    $"Step {step.StepNumber}: {step.Description}\n\nAre you sure you want to delete this step?",
                    "Delete Step Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 레시피에서 스텝 제거
                    _currentRecipe.RemoveStep(step.StepNumber);

                    // UI 업데이트
                    UpdateStepNumbers();
                    RefreshStepList();
                    UpdateStatusInfo();
                    IsModified = true;

                    // 선택된 스텝이었다면 선택 해제
                    if (SelectedStep == step)
                    {
                        SelectedStep = null;
                    }

                    AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                        "Step was deleted.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "DeleteStep", "Failed to delete step", ex);
                MessageBox.Show($"An error occurred while deleting the step: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region UI Update Methods
        /// <summary>
        /// 상태 정보 업데이트
        /// </summary>
        private void UpdateStatusInfo()
        {
            try
            {
                if (_currentRecipe == null) return;

                txtStepCount.Text = $"Total {_currentRecipe.StepCount} steps";
                txtEstimatedTime.Text = $"Est. Time: {_currentRecipe.EstimatedExecutionTime:F1}s";

                // 검증 상태 업데이트
                var validationResult = _currentRecipe.Validate();
                txtValidationStatus.Text = validationResult.IsValid ? "Validation Passed" : "Validation Required";
                txtValidationStatus.Foreground = validationResult.IsValid ?
                    Brushes.Green : Brushes.Red;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "UpdateStatusInfo", "Failed to update status info", ex);
            }
        }

        /// <summary>
        /// 수정 표시기 업데이트
        /// </summary>
        private void UpdateModifiedIndicator()
        {
            txtModifiedIndicator.Visibility = IsModified ?
                Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 현재 스텝이 좌표 편집을 지원하는지 여부 반환
        /// </summary>
        private bool CanEditCoordinates(RecipeStep step)
        {
            if (step == null) return false;

            switch (step.Type)
            {
                case StepType.Move:
                case StepType.Pick:
                case StepType.Place:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 스텝 세부 정보 패널 업데이트
        /// </summary>
        private void UpdateStepDetailsPanel()
        {
            try
            {
                if (SelectedStep != null)
                {
                    grdStepDetails.Visibility = Visibility.Visible;
                    txtNoStepSelected.Visibility = Visibility.Collapsed;

                    txtSelectedStepType.Text = SelectedStep.Type.ToString();
                    cmbStepTeachingGroup.SelectedItem = SelectedStep.TeachingGroupName;
                    cmbStepTeachingLocation.SelectedItem = SelectedStep.TeachingLocationName;
                    btnCoordEdit.IsEnabled = CanEditCoordinates(SelectedStep);
                }
                else
                {
                    grdStepDetails.Visibility = Visibility.Collapsed;
                    txtNoStepSelected.Visibility = Visibility.Visible;
                    btnCoordEdit.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "UpdateStepDetailsPanel", "Failed to update step details panel", ex);
            }
        }
        #endregion

        #region Event Handlers - Step Palette
        /// <summary>
        /// 스텝 추가 버튼 클릭
        /// </summary>
        private void btnAddStep_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.Tag == null) return;

                // 현재 레시피가 없으면 새로 생성
                if (_currentRecipe == null)
                {
                    CreateNewRecipe();
                }

                // Tag에서 StepType 파싱
                if (Enum.TryParse<StepType>(button.Tag.ToString(), out StepType stepType))
                {
                    AddNewStep(stepType);

                    // 사용자 피드백
                    AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                        $"A {GetStepTypeDisplayName(stepType)} step has been added.");

                    Logger.Info(CLASS_NAME, "btnAddStep_Click",
                        $"Step added: {stepType}");
                }
                else
                {
                    Logger.Warning(CLASS_NAME, "btnAddStep_Click",
                        $"Invalid step type: {button.Tag}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnAddStep_Click", "Failed to add step", ex);
                MessageBox.Show($"An error occurred while adding the step: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 템플릿 추가 버튼 클릭 이벤트
        /// </summary>
        private void btnAddTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.Tag == null) return;

                // 현재 레시피가 없으면 새로 생성
                if (_currentRecipe == null)
                {
                    CreateNewRecipe();
                }

                string templateType = button.Tag.ToString();

                switch (templateType)
                {
                    case "PickSequence":
                        AddPickSequenceTemplate();
                        break;

                    case "SafetySequence":
                        AddSafetySequenceTemplate();
                        break;

                    default:
                        Logger.Warning(CLASS_NAME, "btnAddTemplate_Click",
                            $"Unknown template type: {templateType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnAddTemplate_Click", "Failed to add template", ex);
                MessageBox.Show($"An error occurred while adding the template: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Teaching 그룹 선택 변경 이벤트
        /// </summary>
        private void cmbTeachingGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var comboBox = sender as ComboBox;
                if (comboBox?.SelectedItem == null) return;

                string selectedGroup = comboBox.SelectedItem.ToString();

                // 선택된 그룹의 위치 목록 로드
                LoadTeachingLocationsForGroup(selectedGroup);

                Logger.Info(CLASS_NAME, "cmbTeachingGroup_SelectionChanged",
                    $"Teaching group selected: {selectedGroup}");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "cmbTeachingGroup_SelectionChanged",
                    "Failed to handle Teaching group selection", ex);
            }
        }

        /// <summary>
        /// Teaching 위치 선택 변경
        /// </summary>
        private void cmbTeachingLocation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var comboBox = sender as ComboBox;

                // 좌표 적용 버튼 활성화/비활성화
                btnApplyTeaching.IsEnabled = (comboBox?.SelectedItem != null &&
                                              cmbTeachingGroup.SelectedItem != null);

                if (comboBox?.SelectedItem != null)
                {
                    Logger.Info(CLASS_NAME, "cmbTeachingLocation_SelectionChanged",
                        $"Teaching location selected: {comboBox.SelectedItem}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "cmbTeachingLocation_SelectionChanged",
                    "Failed to handle Teaching location selection", ex);
            }
        }

        /// <summary>
        /// Teaching 좌표 적용
        /// </summary>
        private void btnApplyTeaching_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SelectedStep == null)
                {
                    MessageBox.Show("Please select a step to apply coordinates to first.", "Notification",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (cmbTeachingGroup.SelectedItem == null || cmbTeachingLocation.SelectedItem == null)
                {
                    MessageBox.Show("Please select both a Teaching group and location.", "Notification",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string groupName = cmbTeachingGroup.SelectedItem.ToString();
                string locationName = cmbTeachingLocation.SelectedItem.ToString();

                // 선택된 스텝에 Teaching 정보 적용
                SelectedStep.TeachingGroupName = groupName;
                SelectedStep.TeachingLocationName = locationName;

                // Teaching 좌표 실제 로드 시도
                try
                {
                    SelectedStep.LoadCoordinatesFromTeaching();
                    IsModified = true;

                    MessageBox.Show($"Teaching coordinates have been applied.\n" +
                                    $"Group: {groupName}\n" +
                                    $"Location: {locationName}",
                                    "Applied Successfully", MessageBoxButton.OK, MessageBoxImage.Information);

                    AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE,
                        $"Teaching coordinates applied to step {SelectedStep.StepNumber}");
                }
                catch (Exception teachingEx)
                {
                    Logger.Warning(CLASS_NAME, "btnApplyTeaching_Click",
                        $"Failed to load Teaching coordinates: {teachingEx.Message}");

                    MessageBox.Show($"Failed to load Teaching coordinates.\n" +
                                    $"The group and location information have been saved.\n\n" +
                                    $"Error: {teachingEx.Message}",
                                    "Partially Applied", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnApplyTeaching_Click", "Failed to apply Teaching coordinates", ex);
                MessageBox.Show($"An error occurred while applying Teaching coordinates: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Template Methods

        /// <summary>
        /// Pick 시퀀스 템플릿 추가 (Pick → Move → Place)
        /// </summary>
        private void AddPickSequenceTemplate()
        {
            try
            {
                // 1. Pick 스텝
                var pickStep = new RecipeStep(StepType.Pick, "Pick Wafer")
                {
                    Speed = 30 // 안전한 속도로 설정
                };
                _currentRecipe.AddStep(pickStep);

                // 2. Move 스텝 (이동)
                var moveStep = new RecipeStep(StepType.Move, "Move to Target Position")
                {
                    Speed = 50
                };
                _currentRecipe.AddStep(moveStep);

                // 3. Place 스텝
                var placeStep = new RecipeStep(StepType.Place, "Place Wafer")
                {
                    Speed = 30 // 안전한 속도로 설정
                };
                _currentRecipe.AddStep(placeStep);

                UpdateStepNumbers();
                UpdateStatusInfo();
                IsModified = true;

                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                    "Pick Sequence template has been added (Pick → Move → Place)");

                Logger.Info(CLASS_NAME, "AddPickSequenceTemplate",
                    "Pick Sequence template added successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "AddPickSequenceTemplate", "Failed to add Pick Sequence template", ex);
                throw;
            }
        }

        /// <summary>
        /// 안전 체크 시퀀스 템플릿 추가 (Safety Check → Wait)
        /// </summary>
        private void AddSafetySequenceTemplate()
        {
            try
            {
                // 1. Safety Check 스텝
                var safetyStep = new RecipeStep(StepType.CheckSafety, "Safety Status Check")
                {
                    SafetyOptions = SafetyCheckOptions.All
                };
                _currentRecipe.AddStep(safetyStep);

                // 2. Wait 스텝 (안전 확인 후 안정화 대기)
                var waitStep = new RecipeStep(StepType.Wait, "Stabilization Wait")
                {
                    WaitTimeMs = 2000 // 2초 대기
                };
                _currentRecipe.AddStep(waitStep);

                UpdateStepNumbers();
                UpdateStatusInfo();
                IsModified = true;

                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                    "Safety Check Sequence template has been added (Safety → Wait)");

                Logger.Info(CLASS_NAME, "AddSafetySequenceTemplate",
                    "Safety Check Sequence template added successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "AddSafetySequenceTemplate", "Failed to add Safety Check Sequence template", ex);
                throw;
            }
        }

        /// <summary>
        /// 스텝 타입별 한글 표시명 반환
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
        #endregion

        #region Teaching Integration Methods

        /// <summary>
        /// Teaching 그룹 목록 로드
        /// </summary>
        private void LoadTeachingGroups()
        {
            try
            {
                // Teaching 시스템에서 사용 가능한 그룹 목록 가져오기
                _availableTeachingGroups = TeachingDataIntegration.GetAvailableGroups();

                cmbTeachingGroup.ItemsSource = _availableTeachingGroups;

                if (_availableTeachingGroups.Count > 0)
                {
                    cmbTeachingGroup.SelectedIndex = 0; // 첫 번째 그룹 자동 선택
                }

                Logger.Info(CLASS_NAME, "LoadTeachingGroups",
                    $"Loaded {_availableTeachingGroups.Count} Teaching groups");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "LoadTeachingGroups", "Failed to load Teaching groups", ex);

                // 오류 시 기본값 제공
                _availableTeachingGroups = new List<string> { "Group1" };
                cmbTeachingGroup.ItemsSource = _availableTeachingGroups;
                cmbTeachingGroup.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 지정된 그룹의 Teaching 위치 목록 로드
        /// </summary>
        private void LoadTeachingLocationsForGroup(string groupName)
        {
            try
            {
                // Teaching 시스템에서 해당 그룹의 위치 목록 가져오기
                _availableTeachingLocations = TeachingDataIntegration.GetAvailableLocations(groupName);

                cmbTeachingLocation.ItemsSource = _availableTeachingLocations;
                cmbTeachingLocation.SelectedIndex = -1; // 선택 해제

                btnApplyTeaching.IsEnabled = false; // 위치 선택 전까지 비활성화

                Logger.Info(CLASS_NAME, "LoadTeachingLocationsForGroup",
                    $"Loaded {_availableTeachingLocations.Count} locations for group '{groupName}'");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "LoadTeachingLocationsForGroup",
                    $"Failed to load locations for group '{groupName}'", ex);

                // 오류 시 기본값 제공
                _availableTeachingLocations = new List<string> { "P1", "P2", "P3", "P4", "P5", "P6", "P7" };
                cmbTeachingLocation.ItemsSource = _availableTeachingLocations;
            }
        }
        #endregion

        #region Event Handlers - Step Selection
        private void icStepList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                SelectedStep = icStepList.SelectedItem as RecipeStep;
                UpdateStepDetailsPanel();
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "icStepList_SelectionChanged", "Failed to handle step selection", ex);
            }
        }
        #endregion

        #region Event Handlers - Step List Actions
        /// <summary>
        /// 스텝 위로 이동 버튼
        /// </summary>
        private void btnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var step = button?.Tag as RecipeStep;

                if (step != null && _currentRecipe != null)
                {
                    MoveStepUp(step);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnMoveUp_Click", "Failed to move step up", ex);
            }
        }

        /// <summary>
        /// 스텝 아래로 이동 버튼
        /// </summary>
        private void btnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var step = button?.Tag as RecipeStep;

                if (step != null && _currentRecipe != null)
                {
                    MoveStepDown(step);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnMoveDown_Click", "Failed to move step down", ex);
            }
        }

        /// <summary>
        /// 스텝 삭제 버튼
        /// </summary>
        private void btnDeleteStep_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var step = button?.Tag as RecipeStep;

                if (step != null && _currentRecipe != null)
                {
                    DeleteStep(step);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnDeleteStep_Click", "Failed to delete step", ex);
            }
        }
        #endregion

        #region Event Handlers - Recipe Properties
        /// <summary>
        /// 레시피 이름 변경
        /// </summary>
        private void txtRecipeNameEdit_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var textBox = sender as TextBox;
                if (textBox != null && _currentRecipe != null)
                {
                    _currentRecipe.RecipeName = textBox.Text;
                    txtRecipeName.Text = textBox.Text;
                    IsModified = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "txtRecipeNameEdit_TextChanged", "Failed to change recipe name", ex);
            }
        }

        /// <summary>
        /// 레시피 설명 변경
        /// </summary>
        private void txtRecipeDescriptionEdit_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var textBox = sender as TextBox;
                if (textBox != null && _currentRecipe != null)
                {
                    _currentRecipe.Description = textBox.Text;
                    IsModified = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "txtRecipeDescriptionEdit_TextChanged", "Failed to change recipe description", ex);
            }
        }
        #endregion

        #region Event Handlers - Main Actions
        /// <summary>
        /// 레시피 검증
        /// </summary>
        private void btnValidate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentRecipe == null) return;

                var validation = _currentRecipe.Validate();

                if (validation.IsValid)
                {
                    txtValidationStatus.Text = "Validation Passed";
                    txtValidationStatus.Foreground = Brushes.Green;
                    MessageBox.Show("Recipe validation complete. No issues found.", "Validation Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    txtValidationStatus.Text = "Validation Failed";
                    txtValidationStatus.Foreground = Brushes.Red;

                    string errorMessage = "Please fix the following errors:\n\n" + string.Join("\n", validation.ErrorMessages);
                    MessageBox.Show(errorMessage, "Validation Failed",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnValidate_Click", "Recipe validation failed", ex);
                MessageBox.Show("An error occurred during recipe validation.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 레시피 저장
        /// </summary>
        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentRecipe == null) return;

                // 권한 확인
                if (_currentUserRole < UserRole.Engineer)
                {
                    MessageBox.Show("You do not have permission to save recipes.", "Permission Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 검증
                var validation = _currentRecipe.Validate();
                if (!validation.IsValid)
                {
                    var result = MessageBox.Show(
                        "The recipe contains errors. Do you want to save anyway?\n\n" +
                        string.Join("\n", validation.ErrorMessages),
                        "Validation Error",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.No) return;
                }

                // 저장
                bool saveResult = await RecipeFileManager.SaveRecipeAsync(_currentRecipe);

                if (saveResult)
                {
                    IsModified = false;
                    MessageBox.Show("The recipe has been saved.", "Save Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to save the recipe.", "Save Failed",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnSave_Click", "Failed to save recipe", ex);
                MessageBox.Show("An error occurred while saving the recipe.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Event Handlers - Drag & Drop (기본 구현)
        /// <summary>
        /// 스텝 항목 마우스 다운
        /// </summary>
        private void StepItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        /// <summary>
        /// 스텝 항목 마우스 이동
        /// </summary>
        private void StepItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point position = e.GetPosition(null);

                if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // TODO: 드래그&드롭 구현 (Phase 2 완료 후 확장)
                }
            }
        }

        /// <summary>
        /// 스텝 항목 마우스 업
        /// </summary>
        private void StepItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
        }

        /// <summary>
        /// 스텝 항목 드롭
        /// </summary>
        private void StepItem_Drop(object sender, DragEventArgs e)
        {
            // TODO: 드래그&드롭 구현 (Phase 2 완료 후 확장)
        }

        /// <summary>
        /// 스텝 항목 드래그 오버
        /// </summary>
        private void StepItem_DragOver(object sender, DragEventArgs e)
        {
            // TODO: 드래그&드롭 구현 (Phase 2 완료 후 확장)
        }
        #endregion

        #region Event Handlers - Template Actions
        /// <summary>
        /// 템플릿 불러오기
        /// </summary>
        private void btnLoadTemplate_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Phase 2 완료 후 템플릿 기능 구현
            MessageBox.Show("Load Template feature will be implemented later.", "Information",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 템플릿으로 저장
        /// </summary>
        private void btnSaveAsTemplate_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Phase 2 완료 후 템플릿 기능 구현
            MessageBox.Show("Save as Template feature will be implemented later.", "Information",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion

        #region Event Handlers - Input Validation
        /// <summary>
        /// 속도 입력 검증
        /// </summary>
        private void SpeedTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            try
            {
                // 숫자만 입력 허용 (1-100)
                if (!int.TryParse(e.Text, out int value))
                {
                    e.Handled = true;
                    return;
                }

                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
                    if (int.TryParse(newText, out int newValue))
                    {
                        if (newValue < 1 || newValue > 100)
                        {
                            e.Handled = true;
                            return;
                        }
                    }
                }

                // 유효한 입력이면 수정 상태로 표시
                IsModified = true;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "SpeedTextBox_PreviewTextInput", "Failed to validate speed input", ex);
                e.Handled = true;
            }
        }


        /// <summary>
        /// 스텝 설명 더블클릭 편집
        /// </summary>
        private void StepDescription_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    textBox.Background = Brushes.LightYellow;
                    textBox.Focus();
                    textBox.SelectAll();

                    // 포커스를 잃으면 원래 색상으로 복원
                    textBox.LostFocus += (s, args) =>
                    {
                        textBox.Background = Brushes.Transparent;
                        IsModified = true;
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "StepDescription_MouseDoubleClick", "Failed to edit step description", ex);
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 외부에서 레시피 로드
        /// </summary>
        public void LoadRecipe(TransferRecipe recipe)
        {
            try
            {
                if (recipe == null) return;

                CurrentRecipe = recipe;
                IsModified = false;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Loading external recipe: {recipe.RecipeName}");
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "LoadRecipe", "Failed to load external recipe", ex);
            }
        }

        /// <summary>
        /// 새 레시피 생성 (외부 호출용)
        /// </summary>
        public void CreateNew()
        {
            try
            {
                CreateNewRecipe();
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "CreateNew", "Failed to create new recipe", ex);
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

        #region Coordinate Editing
        // <summary>
        /// 지정된 번호의 스텝을 선택
        /// </summary>
        public void SelectStep(int stepNumber)
        {
            try
            {
                var step = _currentRecipe?.Steps?.FirstOrDefault(s => s.StepNumber == stepNumber);
                if (step != null)
                {
                    icStepList.SelectedItem = step;
                    icStepList.ScrollIntoView(step);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, nameof(SelectStep), "Failed to select step", ex);
            }
        }

        /// <summary>
        /// 좌표 출처(S/T) 변경 시 호출
        /// </summary>
        private void CoordinateSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (sender is ComboBox combo && combo.DataContext is RecipeStep step &&
                    combo.SelectedValue is CoordinateSourceType source)
                {
                    step.CoordinateSource = source;
                    if (source == CoordinateSourceType.Setup)
                    {
                        step.TargetPosition = new Position((double)Setup.HomePosA,
                                                           (double)Setup.HomePosT,
                                                           (double)Setup.HomePosZ);
                    }
                    else
                    {
                        step.LoadCoordinatesFromTeaching();
                    }
                    icStepList.Items.Refresh();
                    IsModified = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, nameof(CoordinateSource_SelectionChanged), "Failed to change coordinate source", ex);
            }
        }

        /// <summary>
        /// Setup에서 HomePos가 변경되면 첫 번째 스텝 좌표 업데이트
        /// </summary>
        private void Setup_HomePosChanged(object sender, HomePosChangedEventArgs e)
        {
            try
            {
                if (_currentRecipe?.Steps?.Count > 0)
                {
                    var firstStep = _currentRecipe.Steps[0];
                    if (firstStep.CoordinateSource == CoordinateSourceType.Setup)
                    {
                        firstStep.TargetPosition = new Position((double)e.PositionA,
                                                                (double)e.PositionT,
                                                                (double)e.PositionZ);
                        icStepList.Items.Refresh();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, nameof(Setup_HomePosChanged), "Failed to update HomePos", ex);
            }
        }


        /// <summary>
        /// 좌표 편집 버튼 클릭 이벤트
        /// </summary>
        private void btnCoordEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SelectedStep == null)
                {
                    MessageBox.Show("편집할 스텝을 먼저 선택하세요.", "알림",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                    if (!CanEditCoordinates(SelectedStep))
                    {
                        MessageBox.Show("선택한 스텝에서는 좌표를 편집할 수 없습니다.", "알림",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                var editor = new CoordinateEditWindow(SelectedStep.TargetPosition)
                {
                    Owner = Window.GetWindow(this)
                };

                if (editor.ShowDialog() == true)
                {
                    SelectedStep.TargetPosition = editor.EditedPosition;
                    icStepList.Items.Refresh();
                    IsModified = true;

                    Logger.Info(CLASS_NAME, "btnCoordEdit_Click",
                        string.Format("좌표 편집 적용 - Step {0}: {1}",
                            SelectedStep.StepNumber, editor.EditedPosition));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "btnCoordEdit_Click", "버튼 클릭 처리 실패", ex);
                MessageBox.Show("오류가 발생했습니다: " + ex.Message, "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}