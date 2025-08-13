using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TeachingPendant.Alarm;
using TeachingPendant.MovementUI;
using TeachingPendant.Manager;
using TeachingPendant.WaferMapping;

namespace TeachingPendant.TeachingUI
{
    public partial class Teaching : UserControl
    {
        #region Constants
        private const int MAX_CASSETTE_COUNT = 16;
        private const int MAX_STAGE_COUNT = 16;
        #endregion

        #region Fields
        private string _currentSelectedGroup = "Group1";
        private bool _isJointMode = true;
        private string _currentSelectedType = "";
        private string _currentSelectedItemName = "";

        private Dictionary<string, Dictionary<string, StageData>> _groupItemData = new Dictionary<string, Dictionary<string, StageData>>();

        // 자동 저장용 타이머 추가
        private System.Windows.Threading.DispatcherTimer _autoSaveTimer;
        #endregion

        #region Persistent Storage (Static)
        // 모든 Teaching 인스턴스가 공유하는 정적 데이터
        private static Dictionary<string, Dictionary<string, StageData>> _persistentGroupItemData = new Dictionary<string, Dictionary<string, StageData>>();

        // 현재 인스턴스의 상태도 정적으로 저장
        private static string _persistentCurrentSelectedGroup = "Group1";
        private static bool _persistentIsJointMode = true;
        private static string _persistentCurrentSelectedType = "";
        private static string _persistentCurrentSelectedItemName = "";
        #endregion

        #region Events
        public static event EventHandler<CoordinateUpdateEventArgs> CoordinateUpdated;
        public static event EventHandler<DataCountUpdateEventArgs> DataCountUpdated;
        #endregion

        #region Data Classes
        public class StageData
        {
            public int SlotCount { get; set; } = 1;
            public int Pitch { get; set; } = 1;
            public int PickOffset { get; set; } = 1;
            public int PickDown { get; set; } = 1;
            public int PickUp { get; set; } = 1;
            public int PlaceDown { get; set; } = 1;
            public int PlaceUp { get; set; } = 1;
            public decimal PositionA { get; set; } = 0.00m;
            public decimal PositionT { get; set; } = 0.00m;
            public decimal PositionZ { get; set; } = 0.00m;
        }

        public class DataCountUpdateEventArgs : EventArgs
        {
            public int CassetteCount { get; }
            public int StageCount { get; }

            public DataCountUpdateEventArgs(int cassetteCount, int stageCount)
            {
                CassetteCount = cassetteCount;
                StageCount = stageCount;
            }
        }
        #endregion

        #region Constructor and Initialization
        // 생성자
        public Teaching()
        {
            InitializeComponent();

            // Alarm 관리자 초기화 (먼저 수행)
            InitializeAlarmManager();

            // 이벤트 구독
            SubscribeToEvents();

            // 데이터 로드 및 초기화
            LoadPersistentData();
            InitializeGroups();
            InitializeCoordinateMode();
            RestorePreviousSelection();

            // UI 이벤트 연결
            AttachTextBoxEvents();

            // Movement UI에 데이터 카운트 알림
            NotifyDataCountToMovementUI();

            // 현재 상태 표시
            UpdateCurrentGroupAndSelectionDisplay();

            // 언로드 이벤트 등록
            this.Unloaded += Teaching_Unloaded;

            // 새로 추가: Loaded 이벤트 등록
            this.Loaded += Teaching_Loaded;

            System.Diagnostics.Debug.WriteLine("Teaching UI initialized successfully");

            // Alarm 메시지 테스트
            AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_INITIALIZED, "Teaching UI initialized");

            // 새로 추가: 생성자에서 바로 Alarm 테스트
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Simple immediate test
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    System.Diagnostics.Debug.WriteLine("=== Teaching Alarm delayed test started ===");
                    AlarmMessageManager.ShowCustomMessage("Teaching Alarm Test Success", AlarmCategory.Information);
                };
                timer.Start();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// Teaching UI 로드 완료 후 추가 Alarm 테스트 (새로 추가할 메서드)
        /// </summary>
        private void Teaching_Loaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== Teaching_Loaded event triggered ===");

            // Check Alarm status
            if (txtAlarmMessage != null)
            {
                System.Diagnostics.Debug.WriteLine("txtAlarmMessage is available in Loaded event");

                // Test direct message after a short delay
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    AlarmMessageManager.ShowCustomMessage("Teaching UI Fully Loaded", AlarmCategory.Success);
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ERROR: txtAlarmMessage is still null in Loaded event");
            }
        }

        /// <summary>
        /// Alarm 관리자 초기화 - 가장 먼저 수행
        /// </summary>
        private void InitializeAlarmManager()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Teaching: Alarm manager initialization started ===");

                // Find CommonFrame window
                var commonFrame = Window.GetWindow(this) as CommonFrame;
                if (commonFrame != null)
                {
                    // Access CommonFrame's txtAlarmMessage
                    var commonFrameAlarmTextBlock = commonFrame.FindName("txtAlarmMessage") as TextBlock;
                    if (commonFrameAlarmTextBlock != null)
                    {
                        AlarmMessageManager.SetAlarmTextBlock(commonFrameAlarmTextBlock);
                        System.Diagnostics.Debug.WriteLine("Teaching: Found and set CommonFrame's txtAlarmMessage");

                        // Test message
                        AlarmMessageManager.ShowCustomMessage("Teaching UI Connected to CommonFrame", AlarmCategory.Success);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: Cannot find txtAlarmMessage in CommonFrame");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Cannot find CommonFrame window");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: InitializeAlarmManager error: {ex.Message}");
            }
        }

        private void SubscribeToEvents()
        {
            GroupDataManager.GroupListUpdated += OnGroupListUpdated;
        }

        /// <summary>
        /// Teaching 언로드 시 이벤트 구독 해제
        /// </summary>
        private void Teaching_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Teaching_Unloaded 시작 ===");

                // 현재 편집 중인 데이터 강제 저장
                if (ValidateCurrentSelection())
                {
                    System.Diagnostics.Debug.WriteLine("Teaching_Unloaded: 현재 데이터 강제 저장");
                    SaveCurrentData();
                    SaveToPersistentStorage();
                }

                // 자동 저장 타이머 정리
                if (_autoSaveTimer != null)
                {
                    _autoSaveTimer.Stop();
                    _autoSaveTimer = null;
                    System.Diagnostics.Debug.WriteLine("Auto-save timer disposed");
                }

                // 이벤트 구독 해제
                GroupDataManager.GroupListUpdated -= OnGroupListUpdated;
                DetachTextBoxEvents();

                // 데이터 검증
                ValidatePersistentData();

                System.Diagnostics.Debug.WriteLine("Teaching UI safely unloaded with data preservation");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching_Unloaded 오류: {ex.Message}");
            }
        }

        private void InitializeCoordinateMode()
        {
            if (_persistentIsJointMode)
                UpdateUIForJointMode();
            else
                UpdateUIForCartesianMode();
        }

        /// <summary>
        /// 생성자에서 데이터 복원 강화
        /// </summary>
        private void RestorePreviousSelection()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== RestorePreviousSelection Debug ===");
                System.Diagnostics.Debug.WriteLine($"Restore target: Group={_persistentCurrentSelectedGroup}, Type={_persistentCurrentSelectedType}, Item={_persistentCurrentSelectedItemName}");

                _currentSelectedGroup = _persistentCurrentSelectedGroup;

                if (!string.IsNullOrEmpty(_persistentCurrentSelectedType) &&
                    !string.IsNullOrEmpty(_persistentCurrentSelectedItemName))
                {
                    _currentSelectedType = _persistentCurrentSelectedType;
                    _currentSelectedItemName = _persistentCurrentSelectedItemName;

                    // Try to select in UI
                    if (_currentSelectedType == "Cassette")
                    {
                        SelectListBoxItem(lstCassettes, _currentSelectedItemName);
                    }
                    else if (_currentSelectedType == "Stage")
                    {
                        SelectListBoxItem(lstStages, _currentSelectedItemName);
                    }

                    // Load data to UI
                    LoadItemData(_currentSelectedItemName);
                    System.Diagnostics.Debug.WriteLine("Selection and data restored");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No previous selection, using defaults");
                    SetDefaultSelection();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestorePreviousSelection error: {ex.Message}");
                SetDefaultSelection();
            }
        }

        private void SetDefaultSelection()
        {
            if (lstCassettes.Items.Count > 0)
            {
                lstCassettes.SelectedIndex = 0;
            }
        }

        private void SelectListBoxItem(ListBox listBox, string itemName)
        {
            foreach (ListBoxItem item in listBox.Items)
            {
                if (item.Content.ToString() == itemName)
                {
                    listBox.SelectedItem = item;
                    break;
                }
            }
        }
        #endregion

        #region Persistent Data Management
        /// <summary>
        /// 영구 데이터 로드 (데이터 보존 강화)
        /// </summary>
        private void LoadPersistentData()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Teaching: LoadPersistentData 시작 ===");

                // 정적 데이터를 인스턴스로 복사
                CopyPersistentToInstance();

                // 현재 상태 복원
                RestoreCurrentState();

                System.Diagnostics.Debug.WriteLine($"Teaching persistent data loaded. Current group: {_currentSelectedGroup}, Selected: {_currentSelectedType} {_currentSelectedItemName}, Joint mode: {_isJointMode}");

                // 데이터가 있는지 확인
                if (_persistentGroupItemData.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"로드된 그룹 수: {_persistentGroupItemData.Count}");
                    foreach (var group in _persistentGroupItemData)
                    {
                        System.Diagnostics.Debug.WriteLine($"  그룹 [{group.Key}]: {group.Value.Count}개 아이템");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("영구 데이터가 비어있음, 기본값으로 초기화");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadPersistentData 오류: {ex.Message}");
            }
        }

        private void CopyPersistentToInstance()
        {
            _groupItemData = new Dictionary<string, Dictionary<string, StageData>>();
            foreach (var group in _persistentGroupItemData)
            {
                _groupItemData[group.Key] = new Dictionary<string, StageData>();
                foreach (var item in group.Value)
                {
                    _groupItemData[group.Key][item.Key] = CloneStageData(item.Value);
                }
            }
        }

        private void RestoreCurrentState()
        {
            _currentSelectedGroup = _persistentCurrentSelectedGroup;
            _isJointMode = _persistentIsJointMode;
            _currentSelectedType = _persistentCurrentSelectedType;
            _currentSelectedItemName = _persistentCurrentSelectedItemName;
        }

        private StageData CloneStageData(StageData source)
        {
            return new StageData
            {
                SlotCount = source.SlotCount,
                Pitch = source.Pitch,
                PickOffset = source.PickOffset,
                PickDown = source.PickDown,
                PickUp = source.PickUp,
                PlaceDown = source.PlaceDown,
                PlaceUp = source.PlaceUp,
                PositionA = source.PositionA,
                PositionT = source.PositionT,
                PositionZ = source.PositionZ
            };
        }

        /// <summary>
        /// 영구 저장소에 저장 (데이터 보존 강화)
        /// </summary>
        private void SaveToPersistentStorage()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Teaching: SaveToPersistentStorage 시작 ===");

                CopyInstanceToPersistent();
                SaveCurrentState();

                // 파일에도 자동 저장 추가
                TriggerAutoSave();

                System.Diagnostics.Debug.WriteLine($"Teaching data saved to persistent storage. Group: {_currentSelectedGroup}, Selected: {_currentSelectedType} {_currentSelectedItemName}");

                // 정적 데이터 검증
                ValidatePersistentData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveToPersistentStorage 오류: {ex.Message}");
                throw; // 상위로 예외 전파
            }
        }

        private void CopyInstanceToPersistent()
        {
            _persistentGroupItemData.Clear();
            foreach (var group in _groupItemData)
            {
                _persistentGroupItemData[group.Key] = new Dictionary<string, StageData>();
                foreach (var item in group.Value)
                {
                    _persistentGroupItemData[group.Key][item.Key] = CloneStageData(item.Value);
                }
            }
        }

        private void SaveCurrentState()
        {
            _persistentCurrentSelectedGroup = _currentSelectedGroup;
            _persistentIsJointMode = _isJointMode;
            _persistentCurrentSelectedType = _currentSelectedType;
            _persistentCurrentSelectedItemName = _currentSelectedItemName;
        }
        #endregion

        #region Group Management
        private void InitializeGroups()
        {
            LoadGroupsFromMovementUI();
            EnsureGroupDataInitialized(_currentSelectedGroup);
        }

        private void LoadGroupsFromMovementUI()
        {
            var groups = GroupDataManager.GetAvailableGroups();
            if (!groups.Any())
            {
                groups.Add("Group1");
                GroupDataManager.UpdateGroupList(new List<string>(groups));
            }

            UpdateGroupComboBox(groups);
            System.Diagnostics.Debug.WriteLine($"Synchronized {groups.Count} groups from Movement UI. Current: {_currentSelectedGroup}");
        }

        private void UpdateGroupComboBox(List<string> groups)
        {
            string previouslySelectedGroup = _currentSelectedGroup;
            cmbGroups.Items.Clear();
            bool prevSelectedExists = false;

            foreach (string group in groups)
            {
                var comboItem = new ComboBoxItem { Content = group };
                cmbGroups.Items.Add(comboItem);
                if (group == previouslySelectedGroup)
                {
                    prevSelectedExists = true;
                }
            }

            SetSelectedGroup(previouslySelectedGroup, prevSelectedExists);
        }

        private void SetSelectedGroup(string groupName, bool exists)
        {
            if (exists)
            {
                SelectComboBoxItem(groupName);
            }
            else if (cmbGroups.Items.Count > 0)
            {
                cmbGroups.SelectedIndex = 0;
                _currentSelectedGroup = GetSelectedComboBoxContent();
            }
            else
            {
                AddDefaultGroup();
            }
        }

        private void SelectComboBoxItem(string groupName)
        {
            foreach (ComboBoxItem item in cmbGroups.Items)
            {
                if (item.Content.ToString() == groupName)
                {
                    cmbGroups.SelectedItem = item;
                    break;
                }
            }
        }

        private string GetSelectedComboBoxContent()
        {
            return (cmbGroups.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Group1";
        }

        private void AddDefaultGroup()
        {
            _currentSelectedGroup = "Group1";
            var defaultGroupItem = new ComboBoxItem { Content = "Group1" };
            cmbGroups.Items.Add(defaultGroupItem);
            cmbGroups.SelectedItem = defaultGroupItem;
        }

        private void EnsureGroupDataInitialized(string groupName)
        {
            if (string.IsNullOrEmpty(groupName)) return;

            if (!_groupItemData.ContainsKey(groupName))
            {
                _groupItemData[groupName] = new Dictionary<string, StageData>();
            }

            var currentGroupData = _groupItemData[groupName];
            InitializeStageData(currentGroupData);
            InitializeCassetteData(currentGroupData);
        }

        private void InitializeStageData(Dictionary<string, StageData> groupData)
        {
            for (int i = 1; i <= MAX_STAGE_COUNT; i++)
            {
                string stageName = $"Stage {i}";
                if (!groupData.ContainsKey(stageName))
                {
                    groupData[stageName] = new StageData();
                    System.Diagnostics.Debug.WriteLine($"{_currentSelectedGroup} - {stageName} initialized with default values");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"{_currentSelectedGroup} - {stageName} already exists, preserving data: Slot={groupData[stageName].SlotCount}");
                }
            }
        }

        private void InitializeCassetteData(Dictionary<string, StageData> groupData)
        {
            for (int i = 1; i <= MAX_CASSETTE_COUNT; i++)
            {
                string cassetteName = $"Cassette {i}";
                if (!groupData.ContainsKey(cassetteName))
                {
                    // 새로운 데이터만 기본값으로 초기화
                    groupData[cassetteName] = new StageData();
                    System.Diagnostics.Debug.WriteLine($"{_currentSelectedGroup} - {cassetteName} initialized with default values");
                }
                else
                {
                    // 기존 데이터 보존 - 여기서 덮어쓰지 않음
                    System.Diagnostics.Debug.WriteLine($"{_currentSelectedGroup} - {cassetteName} already exists, preserving data: Slot={groupData[cassetteName].SlotCount}");
                }
            }
        }

        private void OnGroupListUpdated(object sender, EventArgs e)
        {
            LoadGroupsFromMovementUI();
            AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "Group list synchronized with Movement UI");
        }
        #endregion

        #region Event Handlers - Group Selection
        private void GroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbGroups?.SelectedItem is ComboBoxItem selectedComboItem)
            {
                string newSelectedGroup = selectedComboItem.Content.ToString();
                if (_currentSelectedGroup != newSelectedGroup)
                {
                    HandleGroupChange(newSelectedGroup);
                }
            }
        }

        /// <summary>
        /// 그룹 변경 시 데이터 보존 강화
        /// </summary>
        private void HandleGroupChange(string newSelectedGroup)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== HandleGroupChange: {_currentSelectedGroup} → {newSelectedGroup} ===");

                // 현재 그룹의 데이터 강제 저장
                if (ValidateCurrentSelection())
                {
                    System.Diagnostics.Debug.WriteLine($"그룹 변경 전 데이터 저장: {_currentSelectedGroup} {_currentSelectedType} {_currentSelectedItemName}");
                    SaveCurrentData();
                }

                // 새 그룹으로 변경
                _currentSelectedGroup = newSelectedGroup;
                SaveToPersistentStorage();

                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, $"Group selected: {_currentSelectedGroup}");
                EnsureGroupDataInitialized(_currentSelectedGroup);

                SelectFirstCassette();
                UpdateCurrentGroupAndSelectionDisplay();
                NotifyDataCountToMovementUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HandleGroupChange 오류: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Group change error: {ex.Message}");
            }
        }

        private void SelectFirstCassette()
        {
            if (lstCassettes.Items.Count > 0)
            {
                lstCassettes.SelectedIndex = 0;
                lstStages.SelectedIndex = -1;
            }
        }
        #endregion

        #region Event Handlers - Item Selection
        private void CassetteListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstCassettes?.SelectedItem is ListBoxItem selectedItem && selectedItem.Content is string itemName)
            {
                HandleItemSelection("Cassette", itemName, lstStages);
            }
        }

        private void StageListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstStages?.SelectedItem is ListBoxItem selectedItem && selectedItem.Content is string itemName)
            {
                HandleItemSelection("Stage", itemName, lstCassettes);
            }
        }

        /// <summary>
        /// 아이템 선택 변경 시 이전 데이터 저장 보장
        /// </summary>
        private void HandleItemSelection(string itemType, string itemName, ListBox otherListBox)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== HandleItemSelection: {itemType} {itemName} ===");

                if (otherListBox != null) otherListBox.SelectedIndex = -1;

                // 동일한 아이템 재선택 시 데이터 저장 건너뛰기
                if (_currentSelectedType == itemType && _currentSelectedItemName == itemName)
                {
                    System.Diagnostics.Debug.WriteLine("Same item reselected, skipping save to prevent data corruption");
                    UpdateCurrentGroupAndSelectionDisplay();
                    LoadItemData(itemName);
                    return;
                }

                // 이전 선택 데이터 강제 저장 (다른 아이템일 때만)
                if (ValidateCurrentSelection() &&
                    !string.IsNullOrEmpty(_currentSelectedItemName) &&
                    _currentSelectedItemName != itemName)
                {
                    System.Diagnostics.Debug.WriteLine($"Saving previous item data: {_currentSelectedType} {_currentSelectedItemName}");
                    SaveCurrentData();
                }

                // 새 선택 적용
                _currentSelectedType = itemType;
                _currentSelectedItemName = itemName;

                SaveToPersistentStorage();
                UpdateCurrentGroupAndSelectionDisplay();
                LoadItemData(itemName);

                AlarmMessageManager.ShowAlarm(Alarms.POSITION_LOADED, $"{itemType} {itemName} - Data restored");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HandleItemSelection error: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Selection error: {ex.Message}");
            }
        }
        #endregion

        #region Data Management
        private void SaveCurrentData()
        {
            if (!ValidateCurrentSelection()) return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"=== SaveCurrentData: {_currentSelectedGroup} {_currentSelectedItemName} ===");

                var currentData = CreateStageDataFromUI();
                _groupItemData[_currentSelectedGroup][_currentSelectedItemName] = currentData;
                SaveToPersistentStorage();

                System.Diagnostics.Debug.WriteLine($"Teaching data saved and persisted for {_currentSelectedGroup} - {_currentSelectedItemName}");
                System.Diagnostics.Debug.WriteLine($"저장된 데이터: Slot={currentData.SlotCount}, A={currentData.PositionA}, T={currentData.PositionT}, Z={currentData.PositionZ}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveCurrentData 오류: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.DATA_ERROR, $"Save error for {_currentSelectedItemName}: {ex.Message}");
            }
        }

        private bool ValidateCurrentSelection()
        {
            return !string.IsNullOrEmpty(_currentSelectedGroup) &&
                   !string.IsNullOrEmpty(_currentSelectedType) &&
                   !string.IsNullOrEmpty(_currentSelectedItemName) &&
                   _groupItemData.ContainsKey(_currentSelectedGroup);
        }

        private StageData CreateStageDataFromUI()
        {
            return new StageData
            {
                SlotCount = ParseIntFromTextBox(txtSlotCount, 1),
                Pitch = ParseIntFromTextBox(txtPitch, 1),
                PickOffset = ParseIntFromTextBox(txtPickOffset, 1),
                PickDown = ParseIntFromTextBox(txtPickDown, 1),
                PickUp = ParseIntFromTextBox(txtPickUp, 1),
                PlaceDown = ParseIntFromTextBox(txtPlaceDown, 1),
                PlaceUp = ParseIntFromTextBox(txtPlaceUp, 1),
                PositionA = ParseDecimalFromTextBox(txtPositionA, 0.00m),
                PositionT = ParseDecimalFromTextBox(txtPositionT, 0.00m),
                PositionZ = ParseDecimalFromTextBox(txtPositionZ, 0.00m)
            };
        }

        private int ParseIntFromTextBox(TextBox textBox, int defaultValue)
        {
            return int.TryParse(textBox?.Text, out int result) ? result : defaultValue;
        }

        private decimal ParseDecimalFromTextBox(TextBox textBox, decimal defaultValue)
        {
            return decimal.TryParse(textBox?.Text, out decimal result) ? result : defaultValue;
        }

        /// <summary>
        /// LoadItemData 메서드 강화
        /// </summary>
        private void LoadItemData(string itemName)
        {
            System.Diagnostics.Debug.WriteLine($"=== LoadItemData START: {itemName} ===");
            System.Diagnostics.Debug.WriteLine($"Current Group: {_currentSelectedGroup}");
            System.Diagnostics.Debug.WriteLine($"Current Type: {_currentSelectedType}");

            if (!ValidateItemDataLoad(itemName))
            {
                System.Diagnostics.Debug.WriteLine("ValidateItemDataLoad FAILED, loading defaults");
                LoadDefaultValues();
                return;
            }

            // Check actual data before loading
            var dataToLoad = _groupItemData[_currentSelectedGroup][itemName];
            System.Diagnostics.Debug.WriteLine($"Data to load: Slot={dataToLoad.SlotCount}, A={dataToLoad.PositionA}, T={dataToLoad.PositionT}, Z={dataToLoad.PositionZ}");

            DetachTextBoxEvents();
            try
            {
                LoadDataToUI(dataToLoad);

                // Verify UI was updated correctly
                System.Diagnostics.Debug.WriteLine($"UI after load: SlotCount={txtSlotCount.Text}, PosA={txtPositionA.Text}");

                System.Diagnostics.Debug.WriteLine($"{_currentSelectedGroup} - {itemName} data loaded successfully");
            }
            finally
            {
                AttachTextBoxEvents();
                System.Diagnostics.Debug.WriteLine("TextBox events reattached");
            }
        }

        private bool ValidateItemDataLoad(string itemName)
        {
            return !string.IsNullOrEmpty(_currentSelectedGroup) &&
                   !string.IsNullOrEmpty(itemName) &&
                   _groupItemData.ContainsKey(_currentSelectedGroup) &&
                   _groupItemData[_currentSelectedGroup].ContainsKey(itemName);
        }

        private void LoadDataToUI(StageData data)
        {
            txtSlotCount.Text = data.SlotCount.ToString();
            txtPitch.Text = data.Pitch.ToString();
            txtPickOffset.Text = data.PickOffset.ToString();
            txtPickDown.Text = data.PickDown.ToString();
            txtPickUp.Text = data.PickUp.ToString();
            txtPlaceDown.Text = data.PlaceDown.ToString();
            txtPlaceUp.Text = data.PlaceUp.ToString();
            txtPositionA.Text = data.PositionA.ToString("F2");
            txtPositionT.Text = data.PositionT.ToString("F2");
            txtPositionZ.Text = data.PositionZ.ToString("F2");
        }

        private void LoadDefaultValues()
        {
            DetachTextBoxEvents();
            try
            {
                var defaultTextBoxes = new[] { txtSlotCount, txtPitch, txtPickOffset, txtPickDown, txtPickUp, txtPlaceDown, txtPlaceUp };
                foreach (var textBox in defaultTextBoxes)
                {
                    textBox.Text = "1";
                }

                var positionTextBoxes = new[] { txtPositionA, txtPositionT, txtPositionZ };
                foreach (var textBox in positionTextBoxes)
                {
                    textBox.Text = "0.00";
                }
            }
            finally
            {
                AttachTextBoxEvents();
            }
        }
        #endregion

        #region TextBox Event Management
        private void AttachTextBoxEvents()
        {
            var textBoxes = new[] { txtSlotCount, txtPitch, txtPickOffset, txtPickDown, txtPickUp, txtPlaceDown, txtPlaceUp, txtPositionA, txtPositionT, txtPositionZ };

            foreach (var textBox in textBoxes)
            {
                if (textBox != null)
                {
                    textBox.TextChanged += TextBox_TextChanged;
                }
            }

            System.Diagnostics.Debug.WriteLine("TextBox real-time save events attached");
        }

        private void DetachTextBoxEvents()
        {
            var textBoxes = new[] { txtSlotCount, txtPitch, txtPickOffset, txtPickDown, txtPickUp, txtPlaceDown, txtPlaceUp, txtPositionA, txtPositionT, txtPositionZ };

            foreach (var textBox in textBoxes)
            {
                if (textBox != null)
                {
                    textBox.TextChanged -= TextBox_TextChanged;
                }
            }
        }

        /// <summary>
        /// 데이터 자동 저장 강화 (TextBox 변경 시)
        /// </summary>
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            System.Diagnostics.Debug.WriteLine($"TextBox_TextChanged: {textBox?.Name} = '{textBox?.Text}'");

            if (ValidateCurrentSelection())
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Auto-saving for: {_currentSelectedGroup} - {_currentSelectedItemName}");

                    // Immediate memory save
                    SaveCurrentDataSilently();

                    // Stop existing timer
                    if (_autoSaveTimer != null)
                    {
                        _autoSaveTimer.Stop();
                        _autoSaveTimer = null;
                    }

                    // Start new timer for file save
                    _autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
                    _autoSaveTimer.Interval = TimeSpan.FromSeconds(2);
                    _autoSaveTimer.Tick += AutoSaveTimer_Tick;
                    _autoSaveTimer.Start();

                    System.Diagnostics.Debug.WriteLine("Auto-save timer started");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"TextBox_TextChanged error: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("TextBox_TextChanged: ValidateCurrentSelection FAILED");
            }
        }

        /// <summary>
        /// 자동 저장 타이머 이벤트 핸들러
        /// </summary>
        private void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Stop timer first
                if (_autoSaveTimer != null)
                {
                    _autoSaveTimer.Stop();
                    _autoSaveTimer = null;
                }

                // Fire and forget file save
                TriggerAutoSave();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoSaveTimer_Tick error: {ex.Message}");
            }
        }

        private void SaveCurrentDataSilently()
        {
            if (!ValidateCurrentSelection())
            {
                System.Diagnostics.Debug.WriteLine("SaveCurrentDataSilently: ValidateCurrentSelection FAILED");
                return;
            }

            try
            {
                var currentData = CreateStageDataFromUI();
                System.Diagnostics.Debug.WriteLine($"SaveCurrentDataSilently: Created data - Slot={currentData.SlotCount}, A={currentData.PositionA}");

                _groupItemData[_currentSelectedGroup][_currentSelectedItemName] = currentData;
                SaveToPersistentStorage();

                System.Diagnostics.Debug.WriteLine($"Teaching data auto-saved: {_currentSelectedGroup} - {_currentSelectedItemName}");

                // Verify the save
                var savedData = _groupItemData[_currentSelectedGroup][_currentSelectedItemName];
                System.Diagnostics.Debug.WriteLine($"Verified saved data: Slot={savedData.SlotCount}, A={savedData.PositionA}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Silent save error: {ex.Message}");
            }
        }
        #endregion

        #region Coordinate System Management
        private void ToggleCoordinateMode_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateCoordinateSystemChange()) return;

            if (_isJointMode) // 현재 Joint 모드이면 Cartesian으로 전환
            {
                SaveCurrentData();
                ConvertJointToCartesian();
                UpdateUIForCartesianMode();
                SaveToPersistentStorage();
                AlarmMessageManager.ShowAlarm(Alarms.MODE_CHANGED, "Cartesian mode activated and saved");
            }
            else // 현재 Cartesian 모드이면 Joint로 전환
            {
                SaveCurrentData();
                ConvertCartesianToJoint();
                UpdateUIForJointMode();
                SaveToPersistentStorage();
                AlarmMessageManager.ShowAlarm(Alarms.MODE_CHANGED, "Joint mode activated and saved");
            }
        }

        private bool ValidateCoordinateSystemChange()
        {
            if (!GlobalModeManager.IsEditingAllowed)
            {
                AlarmMessageManager.ShowAlarm(Alarms.OPERATION_LIMIT, "Coordinate system change is only available in Manual mode.");
                return false;
            }
            return true;
        }

        private void UpdateUIForJointMode()
        {
            _isJointMode = true;
            UpdateCoordinateLabelsForJoint();
            if (btnToggleCoordinateMode != null)
            {
                btnToggleCoordinateMode.Content = "Cartesian"; // 현재 Joint 모드이므로, 다음 전환될 모드인 Cartesian 표시
            }
            System.Diagnostics.Debug.WriteLine("UI updated for Joint mode (R, T, Z)");
        }

        private void UpdateUIForCartesianMode()
        {
            _isJointMode = false;
            UpdateCoordinateLabelsForCartesian();
            if (btnToggleCoordinateMode != null)
            {
                btnToggleCoordinateMode.Content = "Joint"; // 현재 Cartesian 모드이므로, 다음 전환될 모드인 Joint 표시
            }
            System.Diagnostics.Debug.WriteLine("UI updated for Cartesian mode (X, Y, Z)");
        }

        private void UpdateCoordinateLabelsForJoint()
        {
            if (lblPositionA != null) lblPositionA.Text = "R :";
            if (lblPositionT != null) lblPositionT.Text = "T(°) :";
            if (lblPositionZ != null) lblPositionZ.Text = "Z :";
        }

        private void UpdateCoordinateLabelsForCartesian()
        {
            if (lblPositionA != null) lblPositionA.Text = "X :";
            if (lblPositionT != null) lblPositionT.Text = "Y :";
            if (lblPositionZ != null) lblPositionZ.Text = "Z :";
        }

        private void ConvertJointToCartesian()
        {
            try
            {
                if (decimal.TryParse(txtPositionA?.Text, out decimal r) &&
                    decimal.TryParse(txtPositionT?.Text, out decimal logicalTheta) &&
                    decimal.TryParse(txtPositionZ?.Text, out decimal z))
                {
                    // Setup에 저장된 실제 원점 각도와 기준(30도)의 차이를 계산하여 오프셋으로 사용
                    decimal thetaOffset = SetupUI.Setup.OriginOffsetTheta - 30.0m;

                    // UI의 논리적 각도에 오프셋을 더해 실제 물리적 각도를 계산
                    decimal physicalTheta = logicalTheta + thetaOffset;

                    double thetaRadians = (double)physicalTheta * Math.PI / 180.0;

                    txtPositionA.Text = ((decimal)(r * (decimal)Math.Cos(thetaRadians))).ToString("F2");
                    txtPositionT.Text = ((decimal)(r * (decimal)Math.Sin(thetaRadians))).ToString("F2");
                    // Z는 그대로 유지
                }
            }
            catch (Exception ex)
            {
                AlarmMessageManager.ShowAlarm(Alarms.DATA_ERROR, string.Format("Conversion error: {0}", ex.Message));
            }
        }

        private void ConvertCartesianToJoint()
        {
            try
            {
                if (decimal.TryParse(txtPositionA?.Text, out decimal x) &&
                    decimal.TryParse(txtPositionT?.Text, out decimal y) &&
                    decimal.TryParse(txtPositionZ?.Text, out decimal z))
                {
                    txtPositionA.Text = ((decimal)Math.Sqrt((double)(x * x + y * y))).ToString("F2");

                    // 먼저 물리적 각도를 계산
                    decimal physicalTheta = (decimal)(Math.Atan2((double)y, (double)x) * 180.0 / Math.PI);
                    // Setup에 저장된 실제 원점 각도와 기준(30도)의 차이를 계산하여 오프셋으로 사용
                    decimal thetaOffset = SetupUI.Setup.OriginOffsetTheta - 30.0m;
                    // 물리적 각도에서 오프셋을 빼서 UI에 표시할 논리적 각도를 계산
                    decimal logicalTheta = physicalTheta - thetaOffset;

                    txtPositionT.Text = logicalTheta.ToString("F2"); // Z는 그대로 유지
                }
            }
            catch (Exception ex)
            {
                AlarmMessageManager.ShowAlarm(Alarms.DATA_ERROR, string.Format("Conversion error: {0}", ex.Message));
            }
        }
        #endregion

        #region Button Event Handlers
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== SaveButton_Click Debug ===");

            if (!ValidateSaveOperation())
            {
                return;
            }

            try
            {
                // Save current data to memory
                SaveCurrentData();

                var dataToUpdate = _groupItemData[_currentSelectedGroup][_currentSelectedItemName];
                int currentNumber = GetCurrentSelectedNumber();
                bool isCassetteMode = _currentSelectedType == "Cassette";

                System.Diagnostics.Debug.WriteLine($"Saving: Group={_currentSelectedGroup}, Type={_currentSelectedType}, Item={_currentSelectedItemName}");
                System.Diagnostics.Debug.WriteLine($"Data: SlotCount={dataToUpdate.SlotCount}, A={dataToUpdate.PositionA}, T={dataToUpdate.PositionT}, Z={dataToUpdate.PositionZ}");

                // Update SharedDataManager
                if (isCassetteMode)
                {
                    SharedDataManager.UpdateCassetteData(_currentSelectedGroup, currentNumber,
                        dataToUpdate.PositionA, dataToUpdate.PositionT, dataToUpdate.PositionZ,
                        dataToUpdate.SlotCount, dataToUpdate.Pitch);

                    InitializeWaferMappingForCassette(_currentSelectedGroup, currentNumber, dataToUpdate.SlotCount);
                }
                else
                {
                    SharedDataManager.UpdateStageData(_currentSelectedGroup, currentNumber,
                        dataToUpdate.PositionA, dataToUpdate.PositionT, dataToUpdate.PositionZ,
                        dataToUpdate.SlotCount, dataToUpdate.Pitch);
                }

                NotifyCoordinateUpdate(dataToUpdate, currentNumber, isCassetteMode);

                // Force save to static data and trigger file save
                SaveToPersistentStorage();

                // Debug: Verify data was saved
                System.Diagnostics.Debug.WriteLine("=== After Save Verification ===");
                ValidatePersistentData();

                string coordMode = _isJointMode ? "Joint" : "Cartesian";
                AlarmMessageManager.ShowAlarm(Alarms.POSITION_SAVED,
                    $"{_currentSelectedGroup} {_currentSelectedItemName} saved ({coordMode})");

                // Add Alarm test after successful save
                System.Diagnostics.Debug.WriteLine("=== Testing Alarm after save ===");

                // Test multiple message types with delay
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Save button test completed");
                }), System.Windows.Threading.DispatcherPriority.Background);

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveButton_Click error: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.DATA_ERROR, $"Save error: {ex.Message}");

                // Test Alarm even on error
                System.Diagnostics.Debug.WriteLine("=== Testing Alarm after error ===");
                AlarmMessageManager.ShowCustomMessage("Error occurred during save", AlarmCategory.Error);
            }
        }

        private bool ValidateSaveOperation()
        {
            if (!GlobalModeManager.IsEditingAllowed)
            {
                AlarmMessageManager.ShowAlarm(Alarms.OPERATION_LIMIT, "Save operation is only available in Manual mode");
                return false;
            }

            if (string.IsNullOrEmpty(_currentSelectedType) || string.IsNullOrEmpty(_currentSelectedItemName) || string.IsNullOrEmpty(_currentSelectedGroup))
            {
                AlarmMessageManager.ShowAlarm(Alarms.UNEXPECTED_STATE, "Please select Group, Cassette, or Stage first to save");
                return false;
            }

            return true;
        }

        private void NotifyCoordinateUpdate(StageData data, int currentNumber, bool isCassetteMode)
        {
            System.Diagnostics.Debug.WriteLine("=== NotifyCoordinateUpdate 호출 ===");
            System.Diagnostics.Debug.WriteLine($"CoordinateUpdated 이벤트 구독자 수: {CoordinateUpdated?.GetInvocationList()?.Length ?? 0}");

            try
            {
                CoordinateUpdated?.Invoke(this, new CoordinateUpdateEventArgs(
                    data.PositionA, data.PositionT, data.PositionZ,
                    data.SlotCount, data.Pitch, data.PickOffset,
                    data.PickDown, data.PickUp, data.PlaceDown, data.PlaceUp,
                    currentNumber, isCassetteMode, _currentSelectedGroup
                ));

                System.Diagnostics.Debug.WriteLine("CoordinateUpdated 이벤트 발생 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CoordinateUpdated 이벤트 발생 오류: {ex.Message}");
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSelectedType) || string.IsNullOrEmpty(_currentSelectedItemName))
            {
                AlarmMessageManager.ShowAlarm(Alarms.UNEXPECTED_STATE, "Please select Cassette or Stage first to load");
                return;
            }

            LoadItemData(_currentSelectedItemName);
            AlarmMessageManager.ShowAlarm(Alarms.POSITION_LOADED, $"{_currentSelectedGroup} - {_currentSelectedItemName} data reloaded");
        }

        /// <summary>
        /// 테스트 버튼 - 현재 데이터 상태 확인
        /// </summary>
        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== TEST Button - UI Refresh Testing ===");

            // Force UI refresh
            if (txtAlarmMessage != null)
            {
                // Test 1: Force invalidate visual
                txtAlarmMessage.InvalidateVisual();
                txtAlarmMessage.UpdateLayout();

                System.Diagnostics.Debug.WriteLine($"Current txtAlarmMessage.Text: '{txtAlarmMessage.Text}'");

                // Test 2: Force change with visual feedback
                txtAlarmMessage.Text = "TEST: Manual Text Change";
                txtAlarmMessage.InvalidateVisual();
                txtAlarmMessage.UpdateLayout();

                // Test 3: Background color test to see if UI updates
                txtAlarmMessage.Background = System.Windows.Media.Brushes.Yellow;

                // Test 4: After 2 seconds, test AlarmManager
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(2);
                timer.Tick += (timerSender, timerArgs) =>
                {
                    timer.Stop();

                    // Reset background
                    txtAlarmMessage.Background = System.Windows.Media.Brushes.Black;

                    // Test AlarmManager message
                    AlarmMessageManager.ShowCustomMessage("TEST: AlarmManager Message", AlarmCategory.Warning);

                    System.Diagnostics.Debug.WriteLine("TEST: AlarmManager message sent");
                };
                timer.Start();
            }

            System.Diagnostics.Debug.WriteLine("TEST Button completed");
        }
        #endregion

        #region Helper Methods
        private void UpdateCurrentGroupAndSelectionDisplay()
        {
            if (txtCurrentGroupAndSelection != null)
            {
                string displayText = _currentSelectedGroup;
                if (!string.IsNullOrEmpty(_currentSelectedType) && !string.IsNullOrEmpty(_currentSelectedItemName))
                {
                    displayText += $" - {_currentSelectedItemName}";
                }
                else
                {
                    displayText += " - Select Cassette or Stage";
                }
                txtCurrentGroupAndSelection.Text = displayText;
            }
        }

        private void NotifyDataCountToMovementUI()
        {
            DataCountUpdated?.Invoke(this, new DataCountUpdateEventArgs(MAX_CASSETTE_COUNT, MAX_STAGE_COUNT));
        }

        private int GetCurrentSelectedNumber()
        {
            if (string.IsNullOrEmpty(_currentSelectedItemName)) return 1;

            string[] parts = _currentSelectedItemName.Split(' ');
            if (parts.Length > 1 && int.TryParse(parts.Last(), out int number))
            {
                return number;
            }
            return 1;
        }
        #endregion

        #region Global Mode Management
        private void UpdateUIForCurrentMode()
        {
            bool isEditable = GlobalModeManager.IsEditingAllowed;

            SetAllInputTextBoxesEditability(isEditable);
            SetControlsEnabled(isEditable);
        }

        private void SetAllInputTextBoxesEditability(bool isEditable)
        {
            var backgroundBrush = isEditable ? Brushes.White : new SolidColorBrush(Colors.LightGray);
            var textBoxes = new[] { txtSlotCount, txtPitch, txtPickOffset, txtPickDown, txtPickUp, txtPlaceDown, txtPlaceUp, txtPositionA, txtPositionT, txtPositionZ };

            foreach (var textBox in textBoxes)
            {
                SetTextBoxEditability(textBox, isEditable, backgroundBrush);
            }
        }

        private void SetControlsEnabled(bool isEditable)
        {
            if (btnSave != null) btnSave.IsEnabled = isEditable;
            if (btnLoad != null) btnLoad.IsEnabled = true; // Load는 항상 가능
            if (lstCassettes != null) lstCassettes.IsEnabled = isEditable;
            if (lstStages != null) lstStages.IsEnabled = isEditable;
            if (btnToggleCoordinateMode != null) btnToggleCoordinateMode.IsEnabled = isEditable;
        }

        private void SetTextBoxEditability(TextBox textBox, bool isEditable, SolidColorBrush backgroundBrush)
        {
            if (textBox != null)
            {
                textBox.IsReadOnly = !isEditable;
                textBox.Background = backgroundBrush;
            }
        }
        #endregion

        #region Static Methods for Debugging
        public static void ValidatePersistentData()
        {
            System.Diagnostics.Debug.WriteLine("=== Teaching Data Validation ===");
            System.Diagnostics.Debug.WriteLine($"Static Group Count: {_persistentGroupItemData.Count}");
            System.Diagnostics.Debug.WriteLine($"Current Group: {_persistentCurrentSelectedGroup}");
            System.Diagnostics.Debug.WriteLine($"Current Selection: {_persistentCurrentSelectedType} {_persistentCurrentSelectedItemName}");
            System.Diagnostics.Debug.WriteLine($"Joint Mode: {_persistentIsJointMode}");

            foreach (var group in _persistentGroupItemData)
            {
                System.Diagnostics.Debug.WriteLine($"Group [{group.Key}]: {group.Value.Count} items");
                foreach (var item in group.Value)
                {
                    var data = item.Value;
                    System.Diagnostics.Debug.WriteLine($"  {item.Key}: Slot={data.SlotCount}, A={data.PositionA}, T={data.PositionT}, Z={data.PositionZ}");
                }
            }

            if (_persistentGroupItemData.ContainsKey(_persistentCurrentSelectedGroup) &&
                !string.IsNullOrEmpty(_persistentCurrentSelectedItemName) &&
                _persistentGroupItemData[_persistentCurrentSelectedGroup].ContainsKey(_persistentCurrentSelectedItemName))
            {
                var currentData = _persistentGroupItemData[_persistentCurrentSelectedGroup][_persistentCurrentSelectedItemName];
                System.Diagnostics.Debug.WriteLine($"Current Item Data Details:");
                System.Diagnostics.Debug.WriteLine($"  SlotCount: {currentData.SlotCount}, Pitch: {currentData.Pitch}");
                System.Diagnostics.Debug.WriteLine($"  Position: A={currentData.PositionA}, T={currentData.PositionT}, Z={currentData.PositionZ}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ERROR: No data found for current selection!");
            }
        }

        public static void ShowPersistentDataStatus()
        {
            System.Diagnostics.Debug.WriteLine("=== Teaching Persistent Data Status ===");
            System.Diagnostics.Debug.WriteLine($"Current Group: {_persistentCurrentSelectedGroup}");
            System.Diagnostics.Debug.WriteLine($"Current Selection: {_persistentCurrentSelectedType} {_persistentCurrentSelectedItemName}");
            System.Diagnostics.Debug.WriteLine($"Joint Mode: {_persistentIsJointMode}");
            System.Diagnostics.Debug.WriteLine($"Total Groups: {_persistentGroupItemData.Count}");

            foreach (var group in _persistentGroupItemData)
            {
                System.Diagnostics.Debug.WriteLine($"  {group.Key}: {group.Value.Count} items");
            }
        }

        public static void ClearPersistentData()
        {
            _persistentGroupItemData.Clear();
            _persistentCurrentSelectedGroup = "Group1";
            _persistentIsJointMode = true;
            _persistentCurrentSelectedType = "";
            _persistentCurrentSelectedItemName = "";
            System.Diagnostics.Debug.WriteLine("Teaching persistent data cleared");
        }
        #endregion

        #region Data Persistence Integration
        /// <summary>
        /// PersistentDataManager에서 데이터를 로드하여 Teaching UI에 적용
        /// </summary>
        public static void LoadFromPersistentData(PersistentDataManager.TeachingDataContainer data)
        {
            if (data == null)
            {
                System.Diagnostics.Debug.WriteLine("Teaching: No persistent data to load, using defaults");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("=== Teaching: LoadFromPersistentData 시작 ===");

                // 그룹별 아이템 데이터 로드
                if (data.GroupItemData != null)
                {
                    _persistentGroupItemData.Clear();
                    foreach (var group in data.GroupItemData)
                    {
                        _persistentGroupItemData[group.Key] = new Dictionary<string, StageData>();
                        foreach (var item in group.Value)
                        {
                            _persistentGroupItemData[group.Key][item.Key] = new StageData
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
                    System.Diagnostics.Debug.WriteLine($"로드된 그룹 수: {_persistentGroupItemData.Count}");
                }

                // 현재 상태 로드
                if (!string.IsNullOrEmpty(data.CurrentSelectedGroup))
                {
                    _persistentCurrentSelectedGroup = data.CurrentSelectedGroup;
                    System.Diagnostics.Debug.WriteLine($"로드된 현재 그룹: {_persistentCurrentSelectedGroup}");
                }

                _persistentIsJointMode = data.IsJointMode;
                System.Diagnostics.Debug.WriteLine($"로드된 좌표 모드: {(_persistentIsJointMode ? "Joint" : "Cartesian")}");

                if (!string.IsNullOrEmpty(data.CurrentSelectedType))
                {
                    _persistentCurrentSelectedType = data.CurrentSelectedType;
                    System.Diagnostics.Debug.WriteLine($"로드된 선택 타입: {_persistentCurrentSelectedType}");
                }

                if (!string.IsNullOrEmpty(data.CurrentSelectedItemName))
                {
                    _persistentCurrentSelectedItemName = data.CurrentSelectedItemName;
                    System.Diagnostics.Debug.WriteLine($"로드된 선택 아이템: {_persistentCurrentSelectedItemName}");
                }

                System.Diagnostics.Debug.WriteLine("Teaching: Persistent data loaded successfully");

                // 로드된 데이터 검증
                ValidatePersistentData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: Error loading persistent data: {ex.Message}");
            }
        }

        /// <summary>
        /// Teaching UI의 현재 데이터를 PersistentDataManager용 형태로 변환
        /// </summary>
        public static PersistentDataManager.TeachingDataContainer GetPersistentData()
        {
            try
            {
                var data = new PersistentDataManager.TeachingDataContainer();

                // 그룹별 아이템 데이터 변환
                data.GroupItemData = new Dictionary<string, Dictionary<string, PersistentDataManager.StageDataJson>>();
                foreach (var group in _persistentGroupItemData)
                {
                    data.GroupItemData[group.Key] = new Dictionary<string, PersistentDataManager.StageDataJson>();
                    foreach (var item in group.Value)
                    {
                        data.GroupItemData[group.Key][item.Key] = new PersistentDataManager.StageDataJson
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

                // 현재 상태 저장
                data.CurrentSelectedGroup = _persistentCurrentSelectedGroup;
                data.IsJointMode = _persistentIsJointMode;
                data.CurrentSelectedType = _persistentCurrentSelectedType;
                data.CurrentSelectedItemName = _persistentCurrentSelectedItemName;

                System.Diagnostics.Debug.WriteLine("Teaching: Persistent data prepared for saving");
                return data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: Error preparing persistent data: {ex.Message}");
                return new PersistentDataManager.TeachingDataContainer();
            }
        }

        /// <summary>
        /// 실시간 자동 저장 트리거 (동시 실행 방지)
        /// </summary>
        private static readonly object _autoSaveLock = new object();
        private static bool _isAutoSaving = false;

        private static async void TriggerAutoSave()
        {
            lock (_autoSaveLock)
            {
                if (_isAutoSaving)
                {
                    System.Diagnostics.Debug.WriteLine("Teaching: Auto-save already in progress, skipping");
                    return;
                }
                _isAutoSaving = true;
            }

            try
            {
                // Don't wait for this - fire and forget
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(100); // 짧은 지연으로 연속 호출 방지
                        await PersistentDataManager.AutoSaveAsync(PersistentDataManager.DataType.Teaching);
                        System.Diagnostics.Debug.WriteLine("Teaching: Auto-save completed successfully");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Teaching: Auto-save task error: {ex.Message}");
                    }
                    finally
                    {
                        lock (_autoSaveLock)
                        {
                            _isAutoSaving = false;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: Auto-save trigger error: {ex.Message}");
                lock (_autoSaveLock)
                {
                    _isAutoSaving = false;
                }
            }
        }
        #endregion

        #region Wafer Mapping Integration
        /// <summary>
        /// 카세트 데이터 저장 시 웨이퍼 매핑 초기화
        /// </summary>
        private void InitializeWaferMappingForCassette(string groupName, int cassetteNumber, int slotCount)
        {
            try
            {
                // 웨이퍼 매핑 시스템에 카세트 등록
                WaferMappingSystem.InitializeCassetteMapping(groupName, cassetteNumber, slotCount);

                System.Diagnostics.Debug.WriteLine($"Wafer mapping initialized for {groupName} Cassette{cassetteNumber} ({slotCount} slots)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing wafer mapping: {ex.Message}");
            }
        }

        /// <summary>
        /// 웨이퍼 매핑 상태 표시 UI 추가
        /// </summary>
        private void UpdateWaferMappingStatus()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentSelectedGroup) ||
                    string.IsNullOrEmpty(_currentSelectedItemName))
                {
                    return;
                }

                // 카세트 항목인지 확인
                if (!_currentSelectedItemName.StartsWith("Cassette"))
                {
                    return;
                }

                // 카세트 번호 추출
                if (int.TryParse(_currentSelectedItemName.Replace("Cassette ", ""), out int cassetteId))
                {
                    var mapping = WaferMappingSystem.GetCassetteMapping(_currentSelectedGroup, cassetteId);

                    if (mapping != null)
                    {
                        // 매핑 상태 정보 표시
                        string statusText = $"매핑 상태: ";

                        if (mapping.IsMappingCompleted)
                        {
                            statusText += $"완료 ({mapping.OccupiedSlotCount}/{mapping.TotalSlots} 웨이퍼)";

                            if (mapping.ErrorSlotCount > 0)
                            {
                                statusText += $" [에러: {mapping.ErrorSlotCount}개]";
                            }
                        }
                        else
                        {
                            statusText += "미완료";
                        }

                        // UI에 상태 표시 (기존 UI 요소 활용)
                        System.Diagnostics.Debug.WriteLine($"Teaching: {statusText}");

                        // 알람으로 상태 표시
                        if (mapping.IsMappingCompleted)
                        {
                            AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, statusText);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: 웨이퍼 매핑 상태 업데이트 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 웨이퍼 매핑 창 열기 버튼 기능 (Teaching UI에 추가)
        /// </summary>
        private void OpenWaferMappingWindow()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentSelectedGroup) ||
                    string.IsNullOrEmpty(_currentSelectedItemName))
                {
                    AlarmMessageManager.ShowAlarm(Alarms.UNEXPECTED_STATE,
                        "카세트를 먼저 선택해주세요.");
                    return;
                }

                // 카세트 항목인지 확인
                if (!_currentSelectedItemName.StartsWith("Cassette"))
                {
                    AlarmMessageManager.ShowAlarm(Alarms.UNEXPECTED_STATE,
                        "웨이퍼 매핑은 카세트 항목에서만 사용할 수 있습니다.");
                    return;
                }

                // 카세트 번호 추출
                if (int.TryParse(_currentSelectedItemName.Replace("Cassette ", ""), out int cassetteId))
                {
                    // 현재 카세트 데이터로 매핑 시스템 초기화
                    var currentData = GetCurrentStageData() ?? GetCurrentStageDataFromUI();
                    if (currentData != null)
                    {
                        WaferMappingSystem.InitializeCassetteMapping(_currentSelectedGroup, cassetteId, currentData.SlotCount);
                    }

                    // 웨이퍼 매핑 창 열기
                    var mappingWindow = new TeachingPendant.WaferMapping.WaferMappingWindow();

                    // 기존 InitializeMapping 호출 제거하고 직접 설정
                    // mappingWindow.InitializeMapping(_currentSelectedGroup, cassetteId); // <- 이 줄 제거

                    mappingWindow.ShowDialog();

                    // 매핑 완료 후 상태 업데이트
                    UpdateWaferMappingStatus();

                    AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                        $"웨이퍼 매핑 창이 {_currentSelectedGroup} {_currentSelectedItemName}에 대해 열렸습니다.");
                }
                else
                {
                    AlarmMessageManager.ShowAlarm(Alarms.DATA_ERROR,
                        "카세트 번호를 읽을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: 웨이퍼 매핑 창 열기 실패: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                    $"웨이퍼 매핑 창을 열 수 없습니다: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 선택된 스테이지 데이터 가져오기
        /// </summary>
        /// <returns>현재 StageData 또는 null</returns>
        private StageData GetCurrentStageData()
        {
            try
            {
                if (!ValidateCurrentSelection())
                {
                    System.Diagnostics.Debug.WriteLine("GetCurrentStageData: ValidateCurrentSelection 실패");
                    return null;
                }

                if (_groupItemData.ContainsKey(_currentSelectedGroup) &&
                    _groupItemData[_currentSelectedGroup].ContainsKey(_currentSelectedItemName))
                {
                    var stageData = _groupItemData[_currentSelectedGroup][_currentSelectedItemName];
                    System.Diagnostics.Debug.WriteLine($"GetCurrentStageData: {_currentSelectedGroup} - {_currentSelectedItemName} 데이터 반환");
                    System.Diagnostics.Debug.WriteLine($"  SlotCount: {stageData.SlotCount}, A: {stageData.PositionA}, T: {stageData.PositionT}, Z: {stageData.PositionZ}");
                    return stageData;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"GetCurrentStageData: 데이터를 찾을 수 없음 - {_currentSelectedGroup} {_currentSelectedItemName}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCurrentStageData 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 현재 UI에서 StageData 생성 (웨이퍼 매핑용)
        /// </summary>
        /// <returns>현재 UI 상태를 반영한 StageData</returns>
        private StageData GetCurrentStageDataFromUI()
        {
            try
            {
                var stageData = new StageData
                {
                    SlotCount = ParseIntFromTextBox(txtSlotCount, 1),
                    Pitch = ParseIntFromTextBox(txtPitch, 1),
                    PickOffset = ParseIntFromTextBox(txtPickOffset, 1),
                    PickDown = ParseIntFromTextBox(txtPickDown, 1),
                    PickUp = ParseIntFromTextBox(txtPickUp, 1),
                    PlaceDown = ParseIntFromTextBox(txtPlaceDown, 1),
                    PlaceUp = ParseIntFromTextBox(txtPlaceUp, 1),
                    PositionA = ParseDecimalFromTextBox(txtPositionA, 0.00m),
                    PositionT = ParseDecimalFromTextBox(txtPositionT, 0.00m),
                    PositionZ = ParseDecimalFromTextBox(txtPositionZ, 0.00m)
                };

                System.Diagnostics.Debug.WriteLine($"GetCurrentStageDataFromUI: UI에서 데이터 생성 완료");
                System.Diagnostics.Debug.WriteLine($"  SlotCount: {stageData.SlotCount}, A: {stageData.PositionA}, T: {stageData.PositionT}, Z: {stageData.PositionZ}");

                return stageData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCurrentStageDataFromUI 오류: {ex.Message}");
                return new StageData(); // 기본값 반환
            }
        }

        /// <summary>
        /// 웨이퍼 매핑 시스템 이벤트 구독 (Teaching UI 초기화 시 호출)
        /// </summary>
        private void SubscribeToWaferMappingEvents()
        {
            try
            {
                // 웨이퍼 매핑 완료 이벤트 구독
                WaferMappingSystem.MappingCompleted += (sender, e) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // 현재 선택된 카세트와 일치하는지 확인
                        if (e.GroupName == _currentSelectedGroup &&
                            _currentSelectedItemName == $"Cassette {e.CassetteId}")
                        {
                            UpdateWaferMappingStatus();

                            AlarmMessageManager.ShowAlarm(Alarms.OPERATION_COMPLETED,
                                $"웨이퍼 매핑 완료: {e.OccupiedSlots}개 웨이퍼 감지됨");
                        }
                    }));
                };

                // 웨이퍼 상태 변경 이벤트 구독
                WaferMappingSystem.WaferStatusChanged += (sender, e) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // 현재 선택된 카세트와 일치하는지 확인
                        if (e.GroupName == _currentSelectedGroup &&
                            _currentSelectedItemName == $"Cassette {e.CassetteId}")
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"Teaching: 웨이퍼 상태 변경 - Slot {e.SlotNumber}: {e.OldStatus} → {e.NewStatus}");
                        }
                    }));
                };

                System.Diagnostics.Debug.WriteLine("Teaching: 웨이퍼 매핑 이벤트 구독 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: 웨이퍼 매핑 이벤트 구독 실패: {ex.Message}");
            }
        }
        #endregion

        #region 어딘지 모르겠을때 일단 넣어보는 필드
        /// <summary>
        /// 자동 저장이 활성화된 경우에만 저장을 실행하는 헬퍼 메서드
        /// </summary>
        private void AutoSaveIfEnabled()
        {
            try
            {
                // 현재 선택된 데이터가 유효한지 확인
                if (!ValidateCurrentSelection())
                {
                    System.Diagnostics.Debug.WriteLine("Teaching: AutoSaveIfEnabled - ValidateCurrentSelection FAILED");
                    return;
                }

                // Manual 모드에서만 자동 저장 허용
                if (!GlobalModeManager.IsEditingAllowed)
                {
                    System.Diagnostics.Debug.WriteLine("Teaching: AutoSaveIfEnabled - Editing not allowed in current mode");
                    return;
                }

                // 디버그 로그
                System.Diagnostics.Debug.WriteLine($"Teaching: AutoSaveIfEnabled - 그룹: {_currentSelectedGroup}, 아이템: {_currentSelectedItemName}");

                // 메모리에 즉시 저장
                SaveCurrentDataSilently();

                // 파일에 지연 저장 (타이머 사용)
                StartAutoSaveTimer();

                System.Diagnostics.Debug.WriteLine("Teaching: AutoSaveIfEnabled 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: AutoSaveIfEnabled 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 자동 저장 타이머 시작 (중복 실행 방지)
        /// </summary>
        private void StartAutoSaveTimer()
        {
            try
            {
                // 기존 타이머가 있으면 정지
                if (_autoSaveTimer != null)
                {
                    _autoSaveTimer.Stop();
                    _autoSaveTimer = null;
                }

                // 새 타이머 생성 및 시작
                _autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
                _autoSaveTimer.Interval = TimeSpan.FromSeconds(2); // 2초 지연
                _autoSaveTimer.Tick += AutoSaveTimer_Tick;
                _autoSaveTimer.Start();

                System.Diagnostics.Debug.WriteLine("Teaching: Auto-save timer 시작됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: StartAutoSaveTimer 오류: {ex.Message}");
            }
        }

        #endregion

        #region Static Data Provider Methods for Movement Integration
        /// <summary>
        /// 그룹별 StageData 정보를 Movement에 제공 (정적 메서드)
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <returns>그룹의 모든 StageData 딕셔너리</returns>
        public static Dictionary<string, TeachingStageDataInfo> GetStageDataForGroup(string groupName)
        {
            var result = new Dictionary<string, TeachingStageDataInfo>();

            try
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: GetStageDataForGroup 호출됨 - {groupName}");

                if (string.IsNullOrEmpty(groupName))
                {
                    System.Diagnostics.Debug.WriteLine("Teaching: 그룹명이 비어있음");
                    return result;
                }

                // 정적 데이터에서 해당 그룹 검색
                if (_persistentGroupItemData.ContainsKey(groupName))
                {
                    var groupData = _persistentGroupItemData[groupName];

                    foreach (var item in groupData)
                    {
                        string itemName = item.Key; // "Cassette 1", "Stage 1" 등
                        var stageData = item.Value;

                        // TeachingStageDataInfo로 변환
                        var dataInfo = new TeachingStageDataInfo
                        {
                            ItemName = itemName,
                            SlotCount = stageData.SlotCount,
                            Pitch = stageData.Pitch,
                            PickOffset = stageData.PickOffset,
                            PickDown = stageData.PickDown,
                            PickUp = stageData.PickUp,
                            PlaceDown = stageData.PlaceDown,
                            PlaceUp = stageData.PlaceUp,
                            PositionA = stageData.PositionA,
                            PositionT = stageData.PositionT,
                            PositionZ = stageData.PositionZ,
                            ItemType = DetermineItemType(itemName)
                        };

                        result[itemName] = dataInfo;
                    }

                    System.Diagnostics.Debug.WriteLine($"Teaching: {groupName} 그룹에서 {result.Count}개 아이템 반환");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Teaching: {groupName} 그룹을 찾을 수 없음");
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: GetStageDataForGroup 오류 - {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// 현재 Movement 인스턴스 가져오기 (정적 메서드)
        /// </summary>
        /// <returns>현재 활성 Movement 인스턴스</returns>
        public static MovementUI.Movement GetCurrentInstance()
        {
            try
            {
                // CommonFrame을 통해 현재 활성 Movement 인스턴스 찾기
                var frames = Application.Current.Windows.OfType<Window>()
                    .SelectMany(w => FindVisualChildren<CommonFrame>(w));

                foreach (var frame in frames)
                {
                    if (frame.MainContentArea?.Content is MovementUI.Movement movement)
                    {
                        System.Diagnostics.Debug.WriteLine("Teaching: Movement 인스턴스를 찾았습니다");
                        return movement;
                    }
                }

                System.Diagnostics.Debug.WriteLine("Teaching: Movement 인스턴스를 찾을 수 없습니다");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: GetCurrentInstance 오류 - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 특정 그룹의 사용 가능한 위치 목록 반환 (정적 메서드)
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <returns>위치 목록</returns>
        public static List<string> GetAvailableLocations(string groupName)
        {
            try
            {
                var locations = new List<string>();

                if (_persistentGroupItemData.ContainsKey(groupName))
                {
                    var groupData = _persistentGroupItemData[groupName];
                    locations.AddRange(groupData.Keys);
                }

                // 기본 위치가 없으면 표준 위치 추가
                if (locations.Count == 0)
                {
                    locations.AddRange(new[] { "Cassette 1", "Stage 1" });
                }

                System.Diagnostics.Debug.WriteLine($"Teaching: {groupName} 사용 가능한 위치 {locations.Count}개");
                return locations;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: GetAvailableLocations 오류 - {ex.Message}");
                return new List<string> { "Cassette 1", "Stage 1" };
            }
        }

        /// <summary>
        /// 특정 위치의 좌표 정보 반환 (정적 메서드)
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <param name="locationName">위치명</param>
        /// <returns>위치 좌표 정보</returns>
        public static TeachingPositionInfo GetPositionInfo(string groupName, string locationName)
        {
            try
            {
                if (_persistentGroupItemData.ContainsKey(groupName) &&
                    _persistentGroupItemData[groupName].ContainsKey(locationName))
                {
                    var stageData = _persistentGroupItemData[groupName][locationName];

                    return new TeachingPositionInfo
                    {
                        GroupName = groupName,
                        LocationName = locationName,
                        PositionA = stageData.PositionA,
                        PositionT = stageData.PositionT,
                        PositionZ = stageData.PositionZ,
                        SlotCount = stageData.SlotCount,
                        Pitch = stageData.Pitch,
                        IsValid = true
                    };
                }

                System.Diagnostics.Debug.WriteLine($"Teaching: 위치 정보를 찾을 수 없음 - {groupName}.{locationName}");
                return new TeachingPositionInfo { IsValid = false };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: GetPositionInfo 오류 - {ex.Message}");
                return new TeachingPositionInfo { IsValid = false };
            }
        }

        /// <summary>
        /// Teaching 데이터 변경 이벤트 발생 (정적 메서드)
        /// </summary>
        /// <param name="groupName">변경된 그룹명</param>
        /// <param name="itemName">변경된 아이템명</param>
        /// <param name="updatedData">업데이트된 데이터</param>
        public static void NotifyDataChanged(string groupName, string itemName, StageData updatedData)
        {
            try
            {
                // Movement에 데이터 변경 알림
                var movementInstance = GetCurrentInstance();
                if (movementInstance != null)
                {
                    // Movement의 TeachingStageInfo 형태로 변환
                    var teachingInfo = new MovementUI.Movement.TeachingStageInfo
                    {
                        SlotCount = updatedData.SlotCount,
                        Pitch = updatedData.Pitch,
                        PickOffset = updatedData.PickOffset,
                        PickDown = updatedData.PickDown,
                        PickUp = updatedData.PickUp,
                        PlaceDown = updatedData.PlaceDown,
                        PlaceUp = updatedData.PlaceUp,
                        PositionA = updatedData.PositionA,
                        PositionT = updatedData.PositionT,
                        PositionZ = updatedData.PositionZ,
                        ItemType = DetermineItemType(itemName)
                    };

                    // Movement에 이벤트 전달
                    var eventArgs = new MovementUI.Movement.TeachingDataChangedEventArgs(groupName, itemName, teachingInfo);

                    // Movement의 이벤트 핸들러 호출 (리플렉션 사용)
                    var method = movementInstance.GetType().GetMethod("OnTeachingDataUpdated",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (method != null)
                    {
                        method.Invoke(movementInstance, new object[] { null, eventArgs });
                        System.Diagnostics.Debug.WriteLine($"Teaching: Movement에 데이터 변경 알림 전송 - {groupName}.{itemName}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Teaching: 데이터 변경 알림 - {groupName}.{itemName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: NotifyDataChanged 오류 - {ex.Message}");
            }
        }

        /// <summary>
        /// 모든 그룹 목록 반환 (정적 메서드)
        /// </summary>
        /// <returns>그룹명 리스트</returns>
        public static List<string> GetAllGroups()
        {
            try
            {
                var groups = new List<string>(_persistentGroupItemData.Keys);

                if (groups.Count == 0)
                {
                    groups.Add("Group1"); // 기본 그룹
                }

                System.Diagnostics.Debug.WriteLine($"Teaching: 전체 그룹 수 {groups.Count}개");
                return groups;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching: GetAllGroups 오류 - {ex.Message}");
                return new List<string> { "Group1" };
            }
        }
        #endregion

        #region Helper Methods for Data Provider
        /// <summary>
        /// 아이템명에서 타입 결정
        /// </summary>
        /// <param name="itemName">아이템명 (예: "Cassette 1", "Stage 1")</param>
        /// <returns>타입 문자열</returns>
        private static string DetermineItemType(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
                return "Unknown";

            if (itemName.StartsWith("Cassette", StringComparison.OrdinalIgnoreCase))
                return "Cassette";
            else if (itemName.StartsWith("Stage", StringComparison.OrdinalIgnoreCase))
                return "Stage";
            else
                return "Unknown";
        }

        /// <summary>
        /// Visual Tree에서 특정 타입의 자식 요소 찾기
        /// </summary>
        /// <typeparam name="T">찾을 요소 타입</typeparam>
        /// <param name="depObj">상위 DependencyObject</param>
        /// <returns>찾은 요소들</returns>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
        #endregion

        #region Data Classes for Movement Integration
        /// <summary>
        /// Movement에 제공할 Teaching 스테이지 데이터 정보
        /// </summary>
        public class TeachingStageDataInfo
        {
            public string ItemName { get; set; } = "";
            public string ItemType { get; set; } = ""; // "Cassette" 또는 "Stage"
            public int SlotCount { get; set; } = 1;
            public int Pitch { get; set; } = 1;
            public int PickOffset { get; set; } = 1;
            public int PickDown { get; set; } = 1;
            public int PickUp { get; set; } = 1;
            public int PlaceDown { get; set; } = 1;
            public int PlaceUp { get; set; } = 1;
            public decimal PositionA { get; set; } = 0.00m;
            public decimal PositionT { get; set; } = 0.00m;
            public decimal PositionZ { get; set; } = 0.00m;
        }

        /// <summary>
        /// Teaching 위치 정보
        /// </summary>
        public class TeachingPositionInfo
        {
            public string GroupName { get; set; } = "";
            public string LocationName { get; set; } = "";
            public decimal PositionA { get; set; } = 0.00m;
            public decimal PositionT { get; set; } = 0.00m;
            public decimal PositionZ { get; set; } = 0.00m;
            public int SlotCount { get; set; } = 1;
            public int Pitch { get; set; } = 1;
            public bool IsValid { get; set; } = false;
            public DateTime LastUpdated { get; set; } = DateTime.Now;
        }
        #endregion

        #region Enhanced Save Method with Movement Notification
        /// <summary>
        /// 데이터 저장 시 Movement에 알림 포함 (기존 SaveCurrentData 메서드 수정)
        /// </summary>
        private void SaveCurrentDataWithNotification()
        {
            try
            {
                if (!ValidateCurrentSelection())
                {
                    System.Diagnostics.Debug.WriteLine("SaveCurrentDataWithNotification: 유효성 검사 실패");
                    return;
                }

                // 기존 저장 로직
                var currentData = CreateStageDataFromUI();

                if (!_groupItemData[_currentSelectedGroup].ContainsKey(_currentSelectedItemName))
                {
                    _groupItemData[_currentSelectedGroup][_currentSelectedItemName] = new StageData();
                }

                _groupItemData[_currentSelectedGroup][_currentSelectedItemName] = currentData;
                SaveToPersistentStorage();

                // Movement에 데이터 변경 알림
                NotifyDataChanged(_currentSelectedGroup, _currentSelectedItemName, currentData);

                System.Diagnostics.Debug.WriteLine($"Teaching data saved and Movement notified for {_currentSelectedGroup} - {_currentSelectedItemName}");
                System.Diagnostics.Debug.WriteLine($"저장된 데이터: Slot={currentData.SlotCount}, Pitch={currentData.Pitch}, A={currentData.PositionA}, T={currentData.PositionT}, Z={currentData.PositionZ}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveCurrentDataWithNotification 오류: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.DATA_ERROR, $"Save error for {_currentSelectedItemName}: {ex.Message}");
            }
        }
        #endregion
    }

    /// <summary>
    /// 좌표 업데이트 이벤트 인자
    /// </summary>
    public class CoordinateUpdateEventArgs : EventArgs
    {
        public decimal PositionA { get; }
        public decimal PositionT { get; }
        public decimal PositionZ { get; }
        public int SlotCount { get; }
        public int Pitch { get; }
        public int PickOffset { get; }
        public int PickDown { get; }
        public int PickUp { get; }
        public int PlaceDown { get; }
        public int PlaceUp { get; }
        public int CassetteStageNumber { get; }
        public bool IsCassetteMode { get; }
        public string GroupName { get; }

        public CoordinateUpdateEventArgs(decimal positionA, decimal positionT, decimal positionZ,
            int slotCount, int pitch, int pickOffset, int pickDown, int pickUp, int placeDown, int placeUp,
            int cassetteStageNumber, bool isCassetteMode, string groupName)
        {
            PositionA = positionA; PositionT = positionT; PositionZ = positionZ;
            SlotCount = slotCount; Pitch = pitch; PickOffset = pickOffset;
            PickDown = pickDown; PickUp = pickUp; PlaceDown = placeDown; PlaceUp = placeUp;
            CassetteStageNumber = cassetteStageNumber; IsCassetteMode = isCassetteMode; GroupName = groupName;
        }
    }
}