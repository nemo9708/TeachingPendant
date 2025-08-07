using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TeachingPendant.RecipeSystem.Models;
using TeachingPendant.RecipeSystem.Teaching;
using TeachingPendant.Manager;
using TeachingPendant.HardwareControllers;

namespace TeachingPendant.Teaching
{
    /// <summary>
    /// Teaching UI와 Recipe 시스템 연동을 위한 ViewModel
    /// Teaching 데이터를 Recipe 형태로 변환하고 관리하는 역할 담당
    /// </summary>
    public class TeachingViewModel : INotifyPropertyChanged
    {
        #region Private Fields
        private string _currentSelectedGroup = "Group1";
        private string _currentSelectedType = "";
        private string _currentSelectedItemName = "";
        private bool _isJointMode = true;
        private TeachingUI.Teaching.StageData _currentStageData;
        private bool _isDataModified = false;

        // Teaching UI에서 사용하는 데이터 구조와 동일하게 유지
        private Dictionary<string, Dictionary<string, TeachingUI.Teaching.StageData>> _groupItemData;

        // Recipe 시스템과 연동을 위한 컬렉션
        private ObservableCollection<string> _availableGroups;
        private ObservableCollection<TeachingLocationItem> _availableLocations;
        private ObservableCollection<RecipeTemplateItem> _availableRecipeTemplates;
        #endregion

        #region Public Properties
        /// <summary>
        /// 현재 선택된 그룹명
        /// </summary>
        public string CurrentSelectedGroup
        {
            get => _currentSelectedGroup;
            set
            {
                if (_currentSelectedGroup != value)
                {
                    _currentSelectedGroup = value;
                    OnPropertyChanged(nameof(CurrentSelectedGroup));
                    OnGroupChanged();
                }
            }
        }

        /// <summary>
        /// 현재 선택된 타입 (Stage 또는 Cassette)
        /// </summary>
        public string CurrentSelectedType
        {
            get => _currentSelectedType;
            set
            {
                if (_currentSelectedType != value)
                {
                    _currentSelectedType = value;
                    OnPropertyChanged(nameof(CurrentSelectedType));
                    OnTypeChanged();
                }
            }
        }

        /// <summary>
        /// 현재 선택된 아이템명
        /// </summary>
        public string CurrentSelectedItemName
        {
            get => _currentSelectedItemName;
            set
            {
                if (_currentSelectedItemName != value)
                {
                    _currentSelectedItemName = value;
                    OnPropertyChanged(nameof(CurrentSelectedItemName));
                    OnItemChanged();
                }
            }
        }

        /// <summary>
        /// 좌표 모드 (true: Joint, false: Cartesian)
        /// </summary>
        public bool IsJointMode
        {
            get => _isJointMode;
            set
            {
                if (_isJointMode != value)
                {
                    _isJointMode = value;
                    OnPropertyChanged(nameof(IsJointMode));
                    OnCoordinateModeChanged();
                }
            }
        }

        /// <summary>
        /// 현재 스테이지 데이터
        /// </summary>
        public TeachingUI.Teaching.StageData CurrentStageData
        {
            get => _currentStageData;
            set
            {
                if (_currentStageData != value)
                {
                    _currentStageData = value;
                    OnPropertyChanged(nameof(CurrentStageData));
                }
            }
        }

        /// <summary>
        /// 데이터 수정 여부
        /// </summary>
        public bool IsDataModified
        {
            get => _isDataModified;
            set
            {
                if (_isDataModified != value)
                {
                    _isDataModified = value;
                    OnPropertyChanged(nameof(IsDataModified));
                }
            }
        }

        /// <summary>
        /// 사용 가능한 그룹 목록
        /// </summary>
        public ObservableCollection<string> AvailableGroups
        {
            get => _availableGroups;
            set
            {
                _availableGroups = value;
                OnPropertyChanged(nameof(AvailableGroups));
            }
        }

        /// <summary>
        /// 사용 가능한 위치 목록
        /// </summary>
        public ObservableCollection<TeachingLocationItem> AvailableLocations
        {
            get => _availableLocations;
            set
            {
                _availableLocations = value;
                OnPropertyChanged(nameof(AvailableLocations));
            }
        }

        /// <summary>
        /// 사용 가능한 레시피 템플릿 목록
        /// </summary>
        public ObservableCollection<RecipeTemplateItem> AvailableRecipeTemplates
        {
            get => _availableRecipeTemplates;
            set
            {
                _availableRecipeTemplates = value;
                OnPropertyChanged(nameof(AvailableRecipeTemplates));
            }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// TeachingViewModel 생성자
        /// </summary>
        public TeachingViewModel()
        {
            InitializeViewModel();
            LoadTeachingData();
        }
        #endregion

        #region Initialization Methods
        /// <summary>
        /// ViewModel 초기화
        /// </summary>
        private void InitializeViewModel()
        {
            _groupItemData = new Dictionary<string, Dictionary<string, TeachingUI.Teaching.StageData>>();
            _availableGroups = new ObservableCollection<string>();
            _availableLocations = new ObservableCollection<TeachingLocationItem>();
            _availableRecipeTemplates = new ObservableCollection<RecipeTemplateItem>();

            // 기본 그룹 추가
            _availableGroups.Add("Group1");
            _availableGroups.Add("Group2");
            _availableGroups.Add("Group3");

            // 기본 레시피 템플릿 추가
            InitializeRecipeTemplates();

            System.Diagnostics.Debug.WriteLine("TeachingViewModel initialized successfully");
        }

        /// <summary>
        /// 레시피 템플릿 초기화
        /// </summary>
        private void InitializeRecipeTemplates()
        {
            _availableRecipeTemplates.Add(new RecipeTemplateItem
            {
                Name = "단일 웨이퍼 반송",
                Description = "하나의 웨이퍼를 Pick → Place 하는 기본 패턴",
                Pattern = TransferPattern.SingleWafer,
                IsEnabled = true
            });

            _availableRecipeTemplates.Add(new RecipeTemplateItem
            {
                Name = "순차 배치 반송",
                Description = "여러 웨이퍼를 순차적으로 배치 반송",
                Pattern = TransferPattern.SequentialBatch,
                IsEnabled = true
            });

            _availableRecipeTemplates.Add(new RecipeTemplateItem
            {
                Name = "전체 카세트 반송",
                Description = "카세트 전체를 한번에 반송하는 패턴",
                Pattern = TransferPattern.FullTransfer,
                IsEnabled = true
            });

            _availableRecipeTemplates.Add(new RecipeTemplateItem
            {
                Name = "사용자 정의 패턴",
                Description = "사용자가 직접 정의하는 커스텀 반송 패턴",
                Pattern = TransferPattern.CustomPattern,
                IsEnabled = true
            });
        }
        #endregion

        #region Data Loading Methods
        /// <summary>
        /// Teaching UI 데이터 로드
        /// </summary>
        private void LoadTeachingData()
        {
            try
            {
                // Teaching UI의 정적 데이터 가져오기
                var teachingData = TeachingPendant.TeachingUI.Teaching.GetPersistentData();

                if (teachingData != null)
                {
                    // 그룹 데이터 로드
                    LoadGroupData(teachingData);

                    // 현재 선택 상태 로드
                    LoadCurrentSelection(teachingData);

                    System.Diagnostics.Debug.WriteLine($"Teaching data loaded: {_groupItemData.Count} groups");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No teaching data found, using defaults");
                    LoadDefaultData();
                }

                // 위치 목록 업데이트
                UpdateAvailableLocations();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading teaching data: {ex.Message}");
                LoadDefaultData();
            }
        }

        /// <summary>
        /// 그룹 데이터 로드
        /// </summary>
        private void LoadGroupData(PersistentDataManager.TeachingDataContainer teachingData)
        {
            if (teachingData.GroupItemData != null)
            {
                _groupItemData.Clear();

                foreach (var group in teachingData.GroupItemData)
                {
                    _groupItemData[group.Key] = new Dictionary<string, TeachingUI.Teaching.StageData>();

                    foreach (var item in group.Value)
                    {
                        _groupItemData[group.Key][item.Key] = new TeachingUI.Teaching.StageData
                        {
                            SlotCount = item.Value.SlotCount,
                            Pitch = item.Value.Pitch,
                            PickOffset = item.Value.PickOffset,
                            PickDown = item.Value.PickDown,
                            PickUp = item.Value.PickUp,
                            PlaceDown = item.Value.PlaceDown,
                            PlaceUp = item.Value.PlaceUp,
                            PositionA = item.Value.PositionA,
                            PositionT = item.Value.PositionT,
                            PositionZ = item.Value.PositionZ
                        };
                    }
                }

                // 그룹 목록 업데이트
                _availableGroups.Clear();
                foreach (var groupName in _groupItemData.Keys)
                {
                    _availableGroups.Add(groupName);
                }
            }
        }

        /// <summary>
        /// 현재 선택 상태 로드
        /// </summary>
        private void LoadCurrentSelection(PersistentDataManager.TeachingDataContainer teachingData)
        {
            if (!string.IsNullOrEmpty(teachingData.CurrentSelectedGroup))
            {
                _currentSelectedGroup = teachingData.CurrentSelectedGroup;
            }

            _isJointMode = teachingData.IsJointMode;

            if (!string.IsNullOrEmpty(teachingData.CurrentSelectedType))
            {
                _currentSelectedType = teachingData.CurrentSelectedType;
            }

            if (!string.IsNullOrEmpty(teachingData.CurrentSelectedItemName))
            {
                _currentSelectedItemName = teachingData.CurrentSelectedItemName;
            }

            // 현재 스테이지 데이터 로드
            LoadCurrentStageData();
        }

        /// <summary>
        /// 기본 데이터 로드
        /// </summary>
        private void LoadDefaultData()
        {
            _groupItemData["Group1"] = new Dictionary<string, TeachingUI.Teaching.StageData>();
            _currentSelectedGroup = "Group1";
            _isJointMode = true;

            System.Diagnostics.Debug.WriteLine("Default teaching data loaded");
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 그룹 변경 시 호출
        /// </summary>
        private void OnGroupChanged()
        {
            System.Diagnostics.Debug.WriteLine($"Group changed to: {_currentSelectedGroup}");

            // 해당 그룹의 위치 목록 업데이트
            UpdateAvailableLocations();

            // 첫 번째 아이템 자동 선택
            if (_groupItemData.ContainsKey(_currentSelectedGroup) &&
                _groupItemData[_currentSelectedGroup].Count > 0)
            {
                var firstItem = _groupItemData[_currentSelectedGroup].First();
                CurrentSelectedItemName = firstItem.Key;
            }
        }

        /// <summary>
        /// 타입 변경 시 호출
        /// </summary>
        private void OnTypeChanged()
        {
            System.Diagnostics.Debug.WriteLine($"Type changed to: {_currentSelectedType}");
            UpdateAvailableLocations();
        }

        /// <summary>
        /// 아이템 변경 시 호출
        /// </summary>
        private void OnItemChanged()
        {
            System.Diagnostics.Debug.WriteLine($"Item changed to: {_currentSelectedItemName}");
            LoadCurrentStageData();
        }

        /// <summary>
        /// 좌표 모드 변경 시 호출
        /// </summary>
        private void OnCoordinateModeChanged()
        {
            System.Diagnostics.Debug.WriteLine($"Coordinate mode changed to: {(_isJointMode ? "Joint" : "Cartesian")}");
        }
        #endregion

        #region Core Methods
        /// <summary>
        /// 현재 스테이지 데이터 로드
        /// </summary>
        private void LoadCurrentStageData()
        {
            if (string.IsNullOrEmpty(_currentSelectedGroup) ||
                string.IsNullOrEmpty(_currentSelectedItemName))
            {
                CurrentStageData = new TeachingUI.Teaching.StageData();
                return;
            }

            if (_groupItemData.ContainsKey(_currentSelectedGroup) &&
                _groupItemData[_currentSelectedGroup].ContainsKey(_currentSelectedItemName))
            {
                CurrentStageData = _groupItemData[_currentSelectedGroup][_currentSelectedItemName];
                System.Diagnostics.Debug.WriteLine($"Loaded stage data for {_currentSelectedGroup}.{_currentSelectedItemName}");
            }
            else
            {
                CurrentStageData = new TeachingUI.Teaching.StageData();
                System.Diagnostics.Debug.WriteLine($"No data found for {_currentSelectedGroup}.{_currentSelectedItemName}, using defaults");
            }
        }

        /// <summary>
        /// 사용 가능한 위치 목록 업데이트
        /// </summary>
        private void UpdateAvailableLocations()
        {
            _availableLocations.Clear();

            if (string.IsNullOrEmpty(_currentSelectedGroup) ||
                !_groupItemData.ContainsKey(_currentSelectedGroup))
            {
                return;
            }

            var groupData = _groupItemData[_currentSelectedGroup];

            foreach (var item in groupData)
            {
                _availableLocations.Add(new TeachingLocationItem
                {
                    Name = item.Key,
                    GroupName = _currentSelectedGroup,
                    Data = item.Value,
                    IsValid = IsValidLocationData(item.Value),
                    DisplayName = $"{item.Key} ({GetLocationTypeDescription(item.Key)})"
                });
            }

            System.Diagnostics.Debug.WriteLine($"Updated available locations: {_availableLocations.Count} items");
        }

        /// <summary>
        /// 위치 데이터 유효성 확인
        /// </summary>
        private bool IsValidLocationData(TeachingUI.Teaching.StageData data)
        {
            if (data == null) return false;

            // 기본 좌표 유효성 확인
            return data.PositionA != 0 || data.PositionT != 0 || data.PositionZ != 0;
        }

        /// <summary>
        /// 위치 타입 설명 가져오기
        /// </summary>
        private string GetLocationTypeDescription(string locationName)
        {
            if (locationName.StartsWith("Stage"))
                return "스테이지";
            else if (locationName.StartsWith("Cassette"))
                return "카세트";
            else
                return "위치";
        }
        #endregion

        #region Recipe Integration Methods
        /// <summary>
        /// 현재 그룹 데이터로부터 레시피 생성
        /// </summary>
        public TransferRecipe CreateRecipeFromCurrentGroup(TransferPattern pattern)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Creating recipe from group: {_currentSelectedGroup}, pattern: {pattern}");

                var recipe = TeachingDataIntegration.CreateRecipeFromTeachingGroup(_currentSelectedGroup, pattern);

                if (recipe != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Recipe created successfully: {recipe.RecipeName}");
                    return recipe;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Recipe creation failed");
                    return CreateEmptyRecipe();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating recipe: {ex.Message}");
                return CreateEmptyRecipe();
            }
        }

        /// <summary>
        /// 빈 레시피 생성
        /// </summary>
        private TransferRecipe CreateEmptyRecipe()
        {
            return new TransferRecipe
            {
                RecipeName = "새 레시피",
                Description = "Teaching 데이터로부터 생성된 빈 레시피"
            };
        }

        /// <summary>
        /// 지정된 위치의 좌표 가져오기
        /// </summary>
        public Position GetPositionFromLocation(string locationName)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentSelectedGroup) ||
                    string.IsNullOrEmpty(locationName))
                {
                    return new Position(100, 0, 50); // 기본 안전 위치
                }

                var position = TeachingDataIntegration.GetPositionFromTeaching(_currentSelectedGroup, locationName);
                System.Diagnostics.Debug.WriteLine($"Retrieved position for {_currentSelectedGroup}.{locationName}: R={position.R}, T={position.Theta}, Z={position.Z}");

                return position;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting position: {ex.Message}");
                return new Position(100, 0, 50); // 기본 안전 위치
            }
        }

        /// <summary>
        /// 현재 그룹의 모든 유효한 위치 목록 가져오기
        /// </summary>
        public List<string> GetValidLocationNames()
        {
            var validLocations = new List<string>();

            if (string.IsNullOrEmpty(_currentSelectedGroup) ||
                !_groupItemData.ContainsKey(_currentSelectedGroup))
            {
                return validLocations;
            }

            var groupData = _groupItemData[_currentSelectedGroup];

            foreach (var item in groupData)
            {
                if (IsValidLocationData(item.Value))
                {
                    validLocations.Add(item.Key);
                }
            }

            System.Diagnostics.Debug.WriteLine($"Found {validLocations.Count} valid locations in {_currentSelectedGroup}");
            return validLocations;
        }

        /// <summary>
        /// Teaching 데이터 새로고침
        /// </summary>
        public void RefreshTeachingData()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Refreshing teaching data...");
                LoadTeachingData();
                IsDataModified = false;
                System.Diagnostics.Debug.WriteLine("Teaching data refreshed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing teaching data: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 Teaching 데이터 저장
        /// </summary>
        public async System.Threading.Tasks.Task SaveTeachingDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Saving teaching data...");

                // PersistentDataManager를 통해 저장
                await PersistentDataManager.SaveTeachingDataAsync();

                IsDataModified = false;
                System.Diagnostics.Debug.WriteLine("Teaching data saved successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving teaching data: {ex.Message}");
            }
        }
        #endregion

        #region Data Validation Methods
        /// <summary>
        /// 현재 데이터 유효성 검증
        /// </summary>
        public bool ValidateCurrentData()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentSelectedGroup))
                {
                    System.Diagnostics.Debug.WriteLine("Validation failed: No group selected");
                    return false;
                }

                if (_currentStageData == null)
                {
                    System.Diagnostics.Debug.WriteLine("Validation failed: No stage data");
                    return false;
                }

                // 기본 좌표 유효성 확인
                bool hasValidCoordinates = _currentStageData.PositionA != 0 ||
                                         _currentStageData.PositionT != 0 ||
                                         _currentStageData.PositionZ != 0;

                if (!hasValidCoordinates)
                {
                    System.Diagnostics.Debug.WriteLine("Validation warning: All coordinates are zero");
                }

                System.Diagnostics.Debug.WriteLine($"Data validation passed for {_currentSelectedGroup}.{_currentSelectedItemName}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Validation error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 레시피 생성 가능 여부 확인
        /// </summary>
        public bool CanCreateRecipe()
        {
            try
            {
                // 최소한 하나의 유효한 위치가 있어야 함
                var validLocations = GetValidLocationNames();
                bool hasValidLocations = validLocations.Count > 0;

                // 그룹 데이터가 존재해야 함
                bool hasGroupData = !string.IsNullOrEmpty(_currentSelectedGroup) &&
                                   _groupItemData.ContainsKey(_currentSelectedGroup);

                bool canCreate = hasValidLocations && hasGroupData;

                System.Diagnostics.Debug.WriteLine($"Can create recipe: {canCreate} (Valid locations: {validLocations.Count})");
                return canCreate;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking recipe creation capability: {ex.Message}");
                return false;
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

    #region Helper Classes
    /// <summary>
    /// Teaching 위치 항목을 나타내는 클래스
    /// </summary>
    public class TeachingLocationItem
    {
        public string Name { get; set; }
        public string GroupName { get; set; }
        public TeachingUI.Teaching.StageData Data { get; set; }
        public bool IsValid { get; set; }
        public string DisplayName { get; set; }
    }

    /// <summary>
    /// 레시피 템플릿 항목을 나타내는 클래스
    /// </summary>
    public class RecipeTemplateItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public TransferPattern Pattern { get; set; }
        public bool IsEnabled { get; set; }
        public string IconPath { get; set; }
    }
    #endregion
}