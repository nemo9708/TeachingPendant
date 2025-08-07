using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TeachingPendant.Alarm;

namespace TeachingPendant.WaferMapping
{
    /// <summary>
    /// WaferMappingWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WaferMappingWindow : Window
    {
        #region Fields
        private string _currentGroup = "Group1";
        private int _currentCassetteId = 1;
        private Button[,] _slotButtons = new Button[5, 5]; // 5x5 그리드
        private Dictionary<int, Button> _slotButtonMap = new Dictionary<int, Button>();
        #endregion

        #region Constructor
        public WaferMappingWindow()
        {
            InitializeComponent();
            InitializeUI();
            SubscribeToEvents();
            SetDefaultGroup(); // ← 추가
            RefreshCassetteList();
            CreateSlotButtons();
            RefreshMappingDisplay();
        }

        /// <summary>
        /// Group1을 기본값으로 설정
        /// </summary>
        private void SetDefaultGroup()
        {
            _currentGroup = "Group1";

            // ComboBox에서 Group1 선택
            foreach (ComboBoxItem item in cmbGroup.Items)
            {
                if (item.Content.ToString() == "Group1")
                {
                    cmbGroup.SelectedItem = item;
                    break;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Default group set to: {_currentGroup}");
        }
        #endregion

        #region Initialization
        private void InitializeUI()
        {
            // 이벤트 핸들러 연결
            btnStartMapping.Click += BtnStartMapping_Click;
            btnRefresh.Click += BtnRefresh_Click;
            btnClose.Click += BtnClose_Click;
            cmbGroup.SelectionChanged += CmbGroup_SelectionChanged;
            cmbCassette.SelectionChanged += CmbCassette_SelectionChanged;

            // 기본값 설정
            cmbGroup.SelectedItem = "Group1";  // 이미 Group1이 기본값이었음
            _currentGroup = "Group1";
        }

        private void SubscribeToEvents()
        {
            // 웨이퍼 매핑 시스템 이벤트 구독
            WaferMappingSystem.MappingCompleted += WaferMappingSystem_MappingCompleted;
            WaferMappingSystem.WaferStatusChanged += WaferMappingSystem_WaferStatusChanged;
        }

        private void CreateSlotButtons()
        {
            gridCassetteSlots.Children.Clear();
            _slotButtonMap.Clear();

            int slotNumber = 1;
            for (int row = 0; row < 5; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    var button = new Button
                    {
                        Content = $"S{slotNumber}",
                        Tag = slotNumber,
                        Width = 30,        // 60 → 30으로 절반
                        Height = 30,       // 60 → 30으로 절반
                        Margin = new Thickness(1), // 2 → 1로 줄임
                        FontSize = 8,      // 10 → 8로 줄임
                        FontWeight = FontWeights.Bold
                    };

                    // 버튼 클릭 이벤트
                    button.Click += SlotButton_Click;

                    // 그리드에 배치
                    Grid.SetRow(button, row);
                    Grid.SetColumn(button, col);
                    gridCassetteSlots.Children.Add(button);

                    // 참조 저장
                    _slotButtons[row, col] = button;
                    _slotButtonMap[slotNumber] = button;

                    slotNumber++;
                }
            }

            System.Diagnostics.Debug.WriteLine("25개 슬롯 버튼 생성 완료");
        }

        private void RefreshCassetteList()
        {
            cmbCassette.Items.Clear();

            // Teaching UI에서 설정된 카세트 정보 가져오기
            try
            {
                // TeachingUI의 정적 데이터에서 현재 그룹의 카세트 정보 조회
                var teachingData = TeachingPendant.TeachingUI.Teaching.GetPersistentData();

                if (teachingData?.GroupItemData != null &&
                    teachingData.GroupItemData.ContainsKey(_currentGroup))
                {
                    var groupData = teachingData.GroupItemData[_currentGroup];

                    // 카세트만 필터링하여 추가
                    var cassettes = groupData.Keys
                        .Where(key => key.StartsWith("Cassette "))
                        .OrderBy(key => ExtractNumber(key))
                        .ToList();

                    foreach (var cassetteName in cassettes)
                    {
                        int cassetteNumber = ExtractNumber(cassetteName);

                        // 단순하게 Cassette1, Cassette2 형식으로만 표시
                        var item = new ComboBoxItem
                        {
                            Content = $"Cassette{cassetteNumber}",
                            Tag = cassetteNumber  // 실제 카세트 번호 저장
                        };
                        cmbCassette.Items.Add(item);
                    }

                    System.Diagnostics.Debug.WriteLine($"Found {cassettes.Count} cassettes in {_currentGroup}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"No teaching data found for {_currentGroup}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading cassette data: {ex.Message}");
            }

            // 카세트가 없으면 기본 카세트 추가
            if (cmbCassette.Items.Count == 0)
            {
                for (int i = 1; i <= 4; i++) // 기본적으로 4개 카세트
                {
                    var item = new ComboBoxItem
                    {
                        Content = $"Cassette{i}",
                        Tag = i
                    };
                    cmbCassette.Items.Add(item);
                }
                System.Diagnostics.Debug.WriteLine("No teaching data found, using default cassettes");
            }

            if (cmbCassette.Items.Count > 0)
            {
                cmbCassette.SelectedIndex = 0; // 첫 번째 카세트 선택
                _currentCassetteId = 1;
            }
        }

        /// <summary>
        /// 문자열에서 숫자 추출 (예: "Cassette 3" → 3)
        /// </summary>
        private int ExtractNumber(string text)
        {
            var numbers = System.Text.RegularExpressions.Regex.Matches(text, @"\d+");
            if (numbers.Count > 0 && int.TryParse(numbers[0].Value, out int number))
            {
                return number;
            }
            return 1; // 기본값
        }
        #endregion

        #region Event Handlers
        private void CmbGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbGroup.SelectedItem is ComboBoxItem selectedItem)
            {
                string newGroup = selectedItem.Content.ToString();
                if (_currentGroup != newGroup)
                {
                    _currentGroup = newGroup;
                    System.Diagnostics.Debug.WriteLine($"Group changed to: {_currentGroup}");

                    // 그룹 변경 시 카세트 목록 새로고침
                    RefreshCassetteList();

                    // 첫 번째 카세트로 자동 선택 및 매핑 표시 갱신
                    if (cmbCassette.Items.Count > 0)
                    {
                        cmbCassette.SelectedIndex = 0;
                        // CmbCassette_SelectionChanged에서 RefreshMappingDisplay 호출됨
                    }
                    else
                    {
                        // 카세트가 없으면 기본 표시
                        RefreshMappingDisplay();
                    }
                }
            }
        }

        private void CmbCassette_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbCassette.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is int cassetteId)
            {
                _currentCassetteId = cassetteId;

                // Teaching 데이터에서 실제 슬롯 수 가져와서 매핑 초기화
                InitializeMappingWithTeachingData();
                RefreshMappingDisplay();

                System.Diagnostics.Debug.WriteLine($"카세트 변경: Cassette{_currentCassetteId}");
            }
        }

        /// <summary>
        /// Teaching 데이터를 기반으로 매핑 초기화
        /// </summary>
        private void InitializeMappingWithTeachingData()
        {
            try
            {
                var teachingData = TeachingPendant.TeachingUI.Teaching.GetPersistentData();

                if (teachingData?.GroupItemData != null &&
                    teachingData.GroupItemData.ContainsKey(_currentGroup))
                {
                    var groupData = teachingData.GroupItemData[_currentGroup];
                    string cassetteKey = $"Cassette {_currentCassetteId}";

                    if (groupData.ContainsKey(cassetteKey))
                    {
                        int slotCount = groupData[cassetteKey].SlotCount;

                        // 실제 슬롯 수로 매핑 초기화
                        WaferMappingSystem.InitializeCassetteMapping(_currentGroup, _currentCassetteId, slotCount);

                        System.Diagnostics.Debug.WriteLine($"매핑 초기화 (Teaching 연동): {_currentGroup} Cassette{_currentCassetteId} ({slotCount} slots)");
                        return;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Teaching에서 {cassetteKey} 데이터를 찾을 수 없음");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Teaching에서 {_currentGroup} 그룹 데이터를 찾을 수 없음");
                }

                // Teaching 데이터가 없으면 기본값 사용
                WaferMappingSystem.InitializeCassetteMapping(_currentGroup, _currentCassetteId, 25);
                System.Diagnostics.Debug.WriteLine($"기본 매핑 초기화: {_currentGroup} Cassette{_currentCassetteId} (25 slots)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"매핑 초기화 오류: {ex.Message}");
                // 오류 시 기본값으로 초기화
                WaferMappingSystem.InitializeCassetteMapping(_currentGroup, _currentCassetteId, 25);
            }
        }

        /// <summary>
        /// 그룹 선택 업데이트
        /// </summary>
        /// <param name="groupName">그룹명</param>
        private void UpdateGroupSelection(string groupName)
        {
            try
            {
                // ComboBox에서 해당 그룹 선택
                foreach (ComboBoxItem item in cmbGroup.Items)
                {
                    if (item.Content.ToString() == groupName)
                    {
                        cmbGroup.SelectedItem = item;
                        break;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"그룹 선택 업데이트: {groupName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"그룹 선택 업데이트 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 카세트 선택 업데이트
        /// </summary>
        /// <param name="cassetteId">카세트 ID</param>
        private void UpdateCassetteSelection(int cassetteId)
        {
            try
            {
                // 카세트 리스트 새로고침
                RefreshCassetteList();

                // 해당 카세트 선택
                string cassetteText = $"Cassette {cassetteId}";
                for (int i = 0; i < cmbCassette.Items.Count; i++)
                {
                    if (cmbCassette.Items[i].ToString() == cassetteText)
                    {
                        cmbCassette.SelectedIndex = i;
                        break;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"카세트 선택 업데이트: {cassetteText}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"카세트 선택 업데이트 실패: {ex.Message}");
            }
        }

        private void BtnStartMapping_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Teaching 데이터 기반으로 매핑 초기화
                InitializeMappingWithTeachingData();

                // 매핑 실행
                bool success = WaferMappingSystem.StartMapping(_currentGroup, _currentCassetteId);

                if (success)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                        $"Mapping started for {_currentGroup} Cassette{_currentCassetteId}");
                }
                else
                {
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                        "Failed to start mapping");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Mapping start error: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                    $"Mapping error: {ex.Message}");
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshMappingDisplay();
            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Mapping display refreshed");
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SlotButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int slotNumber)
            {
                var mapping = WaferMappingSystem.GetCassetteMapping(_currentGroup, _currentCassetteId);
                if (mapping != null && mapping.Slots.ContainsKey(slotNumber))
                {
                    var slot = mapping.Slots[slotNumber];
                    ShowSlotDetails(slot);
                }
            }
        }

        // 웨이퍼 매핑 시스템 이벤트 핸들러
        private void WaferMappingSystem_MappingCompleted(object sender, MappingCompletedEventArgs e)
        {
            if (e.GroupName == _currentGroup && e.CassetteId == _currentCassetteId)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RefreshMappingDisplay();
                    AlarmMessageManager.ShowAlarm(Alarms.OPERATION_COMPLETED,
                        $"Mapping completed - {e.OccupiedSlots} wafers found");
                }));
            }
        }

        private void WaferMappingSystem_WaferStatusChanged(object sender, WaferStatusChangedEventArgs e)
        {
            if (e.GroupName == _currentGroup && e.CassetteId == _currentCassetteId)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateSlotButton(e.SlotNumber);
                    UpdateStatusSummary();
                }));
            }
        }
        #endregion

        #region Display Methods
        private void RefreshMappingDisplay()
        {
            var mapping = WaferMappingSystem.GetCassetteMapping(_currentGroup, _currentCassetteId);

            if (mapping == null)
            {
                // 매핑이 없으면 모든 슬롯을 Unknown으로 표시
                foreach (var button in _slotButtonMap.Values)
                {
                    SetSlotButtonStyle(button, WaferStatus.Unknown);
                }
                UpdateStatusSummary(0, 0, 0, 0, "Not mapped");
                return;
            }

            // 각 슬롯 버튼 업데이트
            foreach (var kvp in mapping.Slots)
            {
                int slotNumber = kvp.Key;
                var slot = kvp.Value;

                if (_slotButtonMap.ContainsKey(slotNumber))
                {
                    UpdateSlotButton(slotNumber);
                }
            }

            // 상태 요약 업데이트
            UpdateStatusSummary(
                mapping.TotalSlots,
                mapping.OccupiedSlotCount,
                mapping.EmptySlotCount,
                mapping.ErrorSlotCount,
                mapping.LastMappingTime.ToString("yyyy-MM-dd HH:mm:ss")
            );
        }

        private void UpdateSlotButton(int slotNumber)
        {
            if (!_slotButtonMap.ContainsKey(slotNumber)) return;

            var mapping = WaferMappingSystem.GetCassetteMapping(_currentGroup, _currentCassetteId);
            if (mapping == null || !mapping.Slots.ContainsKey(slotNumber)) return;

            var slot = mapping.Slots[slotNumber];
            var button = _slotButtonMap[slotNumber];

            SetSlotButtonStyle(button, slot.Status);

            // 버튼 텍스트 업데이트
            string buttonText = $"S{slotNumber}";
            if (slot.Status == WaferStatus.Present && !string.IsNullOrEmpty(slot.WaferId))
            {
                buttonText = slot.WaferId.Length > 6 ? slot.WaferId.Substring(0, 6) : slot.WaferId;
            }
            button.Content = buttonText;
        }

        private void SetSlotButtonStyle(Button button, WaferStatus status)
        {
            switch (status)
            {
                case WaferStatus.Empty:
                    button.Background = Brushes.LightGray;
                    button.BorderBrush = Brushes.Black;
                    button.Foreground = Brushes.Black;
                    break;
                case WaferStatus.Present:
                    button.Background = Brushes.LightGreen;
                    button.BorderBrush = Brushes.DarkGreen;
                    button.Foreground = Brushes.Black;
                    break;
                case WaferStatus.Crossed:
                    button.Background = Brushes.Orange;
                    button.BorderBrush = Brushes.DarkOrange;
                    button.Foreground = Brushes.Black;
                    break;
                case WaferStatus.Double:
                    button.Background = Brushes.Red;
                    button.BorderBrush = Brushes.DarkRed;
                    button.Foreground = Brushes.White;
                    break;
                case WaferStatus.Unknown:
                default:
                    button.Background = Brushes.White;
                    button.BorderBrush = Brushes.Gray;
                    button.Foreground = Brushes.Black;
                    break;
            }
        }

        private void UpdateStatusSummary()
        {
            var mapping = WaferMappingSystem.GetCassetteMapping(_currentGroup, _currentCassetteId);
            if (mapping != null)
            {
                UpdateStatusSummary(
                    mapping.TotalSlots,
                    mapping.OccupiedSlotCount,
                    mapping.EmptySlotCount,
                    mapping.ErrorSlotCount,
                    mapping.LastMappingTime.ToString("yyyy-MM-dd HH:mm:ss")
                );
            }
        }

        private void UpdateStatusSummary(int total, int occupied, int empty, int error, string lastMapping)
        {
            txtTotalSlots.Text = total.ToString();
            txtOccupied.Text = occupied.ToString();
            txtEmpty.Text = empty.ToString();
            txtError.Text = error.ToString();
            txtLastMapping.Text = lastMapping;
        }

        private void ShowSlotDetails(WaferSlotInfo slot)
        {
            string statusText = $"Slot {slot.SlotNumber} Details:\n\n";
            statusText += $"Status: {slot.Status}\n";
            statusText += $"Wafer ID: {(string.IsNullOrEmpty(slot.WaferId) ? "None" : slot.WaferId)}\n";
            statusText += $"Thickness: {slot.Thickness:F1} μm\n";
            statusText += $"Last Checked: {slot.LastChecked:yyyy-MM-dd HH:mm:ss}\n";
            statusText += $"Transfer Target: {(slot.IsTransferTarget ? "Yes" : "No")}";

            MessageBox.Show(statusText, $"Slot {slot.SlotNumber} Information",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion

        #region Window Events
        protected override void OnClosed(EventArgs e)
        {
            // 이벤트 구독 해제
            WaferMappingSystem.MappingCompleted -= WaferMappingSystem_MappingCompleted;
            WaferMappingSystem.WaferStatusChanged -= WaferMappingSystem_WaferStatusChanged;

            base.OnClosed(e);
            System.Diagnostics.Debug.WriteLine("WaferMappingWindow closed");
        }
        #endregion
    }
}