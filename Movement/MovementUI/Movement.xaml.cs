using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TeachingPendant.Alarm;
using TeachingPendant.SetupUI;
using TeachingPendant.Manager;
using System.Windows.Threading;
using TeachingPendant.Safety;
using TeachingPendant.RecipeSystem.Models;
using TeachingPendant.MonitorUI;
using System.Linq;

namespace TeachingPendant.MovementUI
{
    public partial class Movement : UserControl
    {
        #region Constants
        private const int STAGE_COUNT_FIXED = 16;
        private const int CASSETTE_COUNT_FIXED = 16;
        #endregion

        #region Fields
        private List<string> _groupList = new List<string>();
        private string _currentSelectedGroup = string.Empty;
        private bool _isGroupDetailMode = false;
        private string _selectedGroupMenu = string.Empty;

        private readonly SolidColorBrush _activeBrush = new SolidColorBrush(Colors.LightBlue);
        private readonly SolidColorBrush _inactiveBrush = new SolidColorBrush(Colors.LightGray);

        // 데이터 저장소
        private Dictionary<string, Dictionary<string, int>> _groupMenuSelectedNumbers = new Dictionary<string, Dictionary<string, int>>();
        private Dictionary<string, Dictionary<int, CassetteInfo>> _groupCassetteData = new Dictionary<string, Dictionary<int, CassetteInfo>>();
        private Dictionary<string, Dictionary<int, StageInfo>> _groupStageData = new Dictionary<string, Dictionary<int, StageInfo>>();
        private Dictionary<string, Dictionary<string, CoordinateData>> _groupCoordinateData = new Dictionary<string, Dictionary<string, CoordinateData>>();
        private Dictionary<string, int> _groupCassetteCounts = new Dictionary<string, int>();
        #endregion

        #region Auto Execution Fields
        private DispatcherTimer _autoExecutionTimer;
        private bool _isAutoExecutionRunning = false;
        private string[] _executionSequence = { "CPick", "CPlace", "SPlace", "SPick" };
        private int _currentSequenceIndex = 0;
        private int _currentPointIndex = 0; // P1=0, P2=1, ..., P7=6
        private decimal _currentR = 0.00m;
        private decimal _currentT = 0.00m;
        private decimal _currentA = 0.00m;
        private decimal _targetR, _targetT, _targetA;
        private const decimal COORDINATE_STEP = 1.0m; // 1씩 증가
        private const decimal COORDINATE_TOLERANCE = 0.1m; // 도달 허용 오차 0.1

        // Wait 기능 추가
        private bool _isWaiting = false;
        private DateTime _waitStartTime;
        private const int WAIT_SECONDS = 1; // 1초 대기
        #endregion

        #region Persistent Storage (Static)
        // 앱 전체에서 유지되는 정적 데이터
        private static Dictionary<string, Dictionary<string, CoordinateData>> _persistentGroupCoordinateData = new Dictionary<string, Dictionary<string, CoordinateData>>();
        private static Dictionary<string, Dictionary<string, int>> _persistentGroupMenuSelectedNumbers = new Dictionary<string, Dictionary<string, int>>();
        private static Dictionary<string, Dictionary<int, CassetteInfo>> _persistentGroupCassetteData = new Dictionary<string, Dictionary<int, CassetteInfo>>();
        private static Dictionary<string, Dictionary<int, StageInfo>> _persistentGroupStageData = new Dictionary<string, Dictionary<int, StageInfo>>();
        private static Dictionary<string, int> _persistentGroupCassetteCounts = new Dictionary<string, int>();

        // 현재 인스턴스 상태 (정적 저장)
        private static string _persistentCurrentSelectedGroup = "Group1";
        private static bool _persistentIsGroupDetailMode = false;
        private static string _persistentSelectedGroupMenu = string.Empty;

        // Remote 실행 상태 (static)
        public static bool IsRemoteExecutionRunning { get; private set; } = false;
        #endregion

        #region Data Classes
        public class CoordinateData
        {
            public string[] P1 { get; set; } = new string[] { "0.00", "0.00", "0.00", "100" };
            public string[] P2 { get; set; } = new string[] { "0.00", "0.00", "0.00", "100" };
            public string[] P3 { get; set; } = new string[] { "0.00", "0.00", "0.00", "100" };
            public string[] P4 { get; set; } = new string[] { "0.00", "0.00", "0.00", "100" };
            public string[] P5 { get; set; } = new string[] { "0.00", "0.00", "0.00", "100" };
            public string[] P6 { get; set; } = new string[] { "0.00", "0.00", "0.00", "100" };
            public string[] P7 { get; set; } = new string[] { "0.00", "0.00", "0.00", "100" };
        }

        public class CassetteInfo
        {
            public int SlotCount { get; set; } = 1;
            public decimal PositionA { get; set; } = 0.00m;
            public decimal PositionT { get; set; } = 0.00m;
            public decimal PositionZ { get; set; } = 0.00m;
            public int Pitch { get; set; } = 1;
            public int PickOffset { get; set; } = 1;
            public int PickDown { get; set; } = 1;
            public int PickUp { get; set; } = 1;
            public int PlaceDown { get; set; } = 1;
            public int PlaceUp { get; set; } = 1;

            public CassetteInfo() { }

            public CassetteInfo(decimal posA, decimal posT, decimal posZ, int slotCount,
                int pitch, int pickOffset, int pickDown, int pickUp, int placeDown, int placeUp)
            {
                PositionA = posA; PositionT = posT; PositionZ = posZ; SlotCount = slotCount;
                Pitch = pitch; PickOffset = pickOffset; PickDown = pickDown; PickUp = pickUp;
                PlaceDown = placeDown; PlaceUp = placeUp;
            }
        }

        public class StageInfo
        {
            public int SlotCount { get; set; } = 1;
            public decimal PositionA { get; set; } = 0.00m;
            public decimal PositionT { get; set; } = 0.00m;
            public decimal PositionZ { get; set; } = 0.00m;
            public int Pitch { get; set; } = 1;
            public int PickOffset { get; set; } = 1;
            public int PickDown { get; set; } = 1;
            public int PickUp { get; set; } = 1;
            public int PlaceDown { get; set; } = 1;
            public int PlaceUp { get; set; } = 1;

            public StageInfo() { }

            public StageInfo(decimal posA, decimal posT, decimal posZ, int slotCount,
                int pitch, int pickOffset, int pickDown, int pickUp, int placeDown, int placeUp)
            {
                PositionA = posA; PositionT = posT; PositionZ = posZ; SlotCount = slotCount;
                Pitch = pitch; PickOffset = pickOffset; PickDown = pickDown; PickUp = pickUp;
                PlaceDown = placeDown; PlaceUp = placeUp;
            }
        }

        public class SegmentPhysicsResult
        {
            public string SegmentName { get; set; }
            public string StartPoint { get; set; }
            public string EndPoint { get; set; }
            public double Distance { get; set; }
            public double TheoreticalMaxSpeed { get; set; }
            public int FinalCommandSpeed { get; set; }
            public bool IsValidSegment { get; set; }
            public string ErrorMessage { get; set; }
            public string LimitErrorMessage { get; set; }
        }
        #endregion

        #region Constructor and Initialization
        public Movement()
        {
            InitializeComponent();
            SubscribeToEvents();
            LoadPersistentData();
            InitializeGroupData();
            InitializeAutoExecution();
            RegisterInstance(this);

            // Teaching 데이터 변경 이벤트 구독
            SubscribeToTeachingEvents();

            System.Diagnostics.Debug.WriteLine("Movement: 생성자 완료 및 인스턴스 등록됨");

            // Speed 변경 이벤트 구독
            GlobalSpeedManager.SpeedChanged += OnGlobalSpeedChanged;

            this.Loaded += Movement_Loaded;
            this.Unloaded += Movement_Unloaded;

            System.Diagnostics.Debug.WriteLine("=== Attempting to register Setup in the Movement constructor. ===");
            try
            {
                Setup.RegisterMovementInstance(this);
                System.Diagnostics.Debug.WriteLine("Movement 인스턴스 Setup 등록 성공!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement 인스턴스 Setup 등록 실패: {ex.Message}");
            }

            AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_INITIALIZED,
                "Movement UI initialized with HomePos integration");

            this.KeyDown += Movement_KeyDown;
            this.Focusable = true;

            // Movement_Loaded 이벤트에서 포커스 설정
            this.Loaded += (s, e) => this.Focus();
        }

        // Speed 변경 이벤트 핸들러
        private void OnGlobalSpeedChanged(object sender, int newSpeed)
        {
            System.Diagnostics.Debug.WriteLine($"Movement: Speed changed to {newSpeed}% - updating timer interval.");
            UpdateTimerInterval();
        }

        private void Movement_Unloaded(object sender, RoutedEventArgs e)
        {
            // 이벤트 해체
            TeachingPendant.TeachingUI.Teaching.DataCountUpdated -= Teaching_DataCountUpdated;
            GlobalSpeedManager.SpeedChanged -= GlobalSpeedManager_SpeedChanged;
            SharedDataManager.CassetteDataUpdated -= SharedDataManager_CassetteDataUpdated;
            SharedDataManager.StageDataUpdated -= SharedDataManager_StageDataUpdated;
            GlobalSpeedManager.SpeedChanged -= OnGlobalSpeedChanged;

            UnsubscribeFromHomePosEvents();

            // Teaching 이벤트 구독 해제
            UnsubscribeFromTeachingEvents();

            // 인스턴스 등록 해제
            UnregisterInstance();

            // Setup에도 등록 해제를 알려 메모리 누수를 방지
            Setup.UnregisterMovementInstance();

            System.Diagnostics.Debug.WriteLine("Movement UI events unsubscribed and HomePos integration unregistered");
        }

        #region HomePos Integration
        /// <summary>
        /// HomePos 이벤트 구독
        /// </summary>
        private void SubscribeToHomePosEvents()
        {
            SetupUI.Setup.HomePosChanged += Setup_HomePosChanged;
        }

        /// <summary>
        /// HomePos 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromHomePosEvents()
        {
            SetupUI.Setup.HomePosChanged -= Setup_HomePosChanged;
        }

        /// <summary>
        /// Setup에서 HomePos 변경 시 호출되는 이벤트 핸들러
        /// </summary>
        private void Setup_HomePosChanged(object sender, SetupUI.HomePosChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Movement UI received HomePos change: A={e.PositionA}, T={e.PositionT}, Z={e.PositionZ}");

            // 현재 편집 모드일 때만 자동 적용
            if (!GlobalModeManager.IsEditingAllowed)
            {
                AlarmMessageManager.ShowAlarm(Alarms.OPERATION_LIMIT,
                    "HomePos auto-apply is only available in Manual mode");
                return;
            }

            // 모든 메뉴의 P1에 HomePos 적용
            ApplyHomePosToAllMenusP1(e.PositionA, e.PositionT, e.PositionZ);
        }

        /// <summary>
        /// 특정 메뉴의 P1에 HomePos 적용 (외부에서 호출 가능)
        /// </summary>
        public void ApplyHomePosToMenuP1(string menuType, decimal posA, decimal posT, decimal posZ)
        {
            System.Diagnostics.Debug.WriteLine($"=== Movement.ApplyHomePosToMenuP1 호출됨 ===");
            System.Diagnostics.Debug.WriteLine($"MenuType: {menuType}, 좌표: A={posA}, T={posT}, Z={posZ}");

            if (string.IsNullOrEmpty(menuType) ||
                (menuType != "CPick" && menuType != "CPlace" && menuType != "SPick" && menuType != "SPlace"))
            {
                System.Diagnostics.Debug.WriteLine($"Invalid menu type for HomePos application: {menuType}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"현재 그룹 목록 수: {_groupList?.Count ?? 0}");

            int updatedGroupCount = 0; // 업데이트된 그룹 수 카운트

            // 모든 그룹에 대해 적용
            foreach (string groupName in _groupList)
            {
                System.Diagnostics.Debug.WriteLine($"그룹 {groupName}의 {menuType} P1에 HomePos 적용 중...");

                EnsureGroupDataInitialized(groupName);

                if (!_groupCoordinateData[groupName].ContainsKey(menuType))
                {
                    _groupCoordinateData[groupName][menuType] = new CoordinateData();
                    System.Diagnostics.Debug.WriteLine($"{groupName} - {menuType} 새 CoordinateData 생성됨");
                }

                // P1에 HomePos 좌표 적용
                var coordData = _groupCoordinateData[groupName][menuType];
                string oldP1A = coordData.P1[0];
                string oldP1T = coordData.P1[1];
                string oldP1Z = coordData.P1[2];

                coordData.P1[0] = posA.ToString("F2");  // A/R 좌표
                coordData.P1[1] = posT.ToString("F2");  // T 좌표
                coordData.P1[2] = posZ.ToString("F2");  // Z 좌표
                                                        // P1[3] (Speed)는 기존 값 유지

                System.Diagnostics.Debug.WriteLine($"{groupName} - {menuType} P1 변경: ({oldP1A}, {oldP1T}, {oldP1Z}) → ({posA:F2}, {posT:F2}, {posZ:F2})");
                updatedGroupCount++;
            }

            // 현재 표시된 메뉴가 변경된 메뉴와 같으면 UI 업데이트
            System.Diagnostics.Debug.WriteLine($"현재 선택된 메뉴: {_selectedGroupMenu}, 변경 대상 메뉴: {menuType}");
            System.Diagnostics.Debug.WriteLine($"상세 모드: {_isGroupDetailMode}, 좌표 영역 표시: {CoordinateScrollViewer.Visibility == Visibility.Visible}");

            if (_selectedGroupMenu == menuType && _isGroupDetailMode &&
                CoordinateScrollViewer.Visibility == Visibility.Visible)
            {
                System.Diagnostics.Debug.WriteLine("현재 표시 중인 메뉴와 일치. UI 업데이트 수행...");
                LoadCoordinatesForMenu(menuType);
                AlarmMessageManager.ShowAlarm(Alarms.POSITION_LOADED,
                    $"{_currentSelectedGroup} - {menuType} P1 updated with HomePos");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("현재 표시 중인 메뉴와 다름. UI 업데이트 스킵.");
            }

            SaveToPersistentStorage();
            System.Diagnostics.Debug.WriteLine($"HomePos 적용 완료: {updatedGroupCount}개 그룹의 {menuType} P1 업데이트됨");
        }

        /// <summary>
        /// 모든 메뉴의 P1에 HomePos 적용
        /// </summary>
        private void ApplyHomePosToAllMenusP1(decimal posA, decimal posT, decimal posZ)
        {
            var targetMenus = new[] { "CPick", "CPlace", "SPick", "SPlace" };

            foreach (string menuType in targetMenus)
            {
                ApplyHomePosToMenuP1(menuType, posA, posT, posZ);
            }

            AlarmMessageManager.ShowAlarm(Alarms.POSITION_LOADED,
                $"HomePos applied to all menu P1 coordinates: A={posA:F2}, T={posT:F2}, Z={posZ:F2}");
        }
        #endregion

        private void Movement_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F12)
            {
                System.Diagnostics.Debug.WriteLine("=== F12 key pressed! ===");
                TestPhysicsCalculations();
                e.Handled = true; // 이벤트 처리 완료
            }
            else if (e.Key == System.Windows.Input.Key.F11)
            {
                ForceUpdateP3FromCurrentCassette(); // 디버깅용
            }
        }

        private void InitializeAlarmManager()
        {
            if (txtAlarmMessage != null)
            {
                AlarmMessageManager.SetAlarmTextBlock(txtAlarmMessage);
            }
        }

        private void SubscribeToEvents()
        {
            TeachingPendant.TeachingUI.Teaching.DataCountUpdated += Teaching_DataCountUpdated;
            GlobalSpeedManager.SpeedChanged += GlobalSpeedManager_SpeedChanged;
            SharedDataManager.CassetteDataUpdated += SharedDataManager_CassetteDataUpdated;
            SharedDataManager.StageDataUpdated += SharedDataManager_StageDataUpdated;
        }

        private void SharedDataManager_CassetteDataUpdated(object sender, SharedDataManager.DataUpdatedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"=== Movement UI: SharedDataManager_CassetteDataUpdated 수신 ===");
            System.Diagnostics.Debug.WriteLine($"Group: {e.GroupName}, Cassette: {e.ItemNumber}");
            System.Diagnostics.Debug.WriteLine($"Position: ({e.Data.PositionA}, {e.Data.PositionT}, {e.Data.PositionZ}), SlotCount: {e.Data.SlotCount}");

            EnsureGroupDataInitialized(e.GroupName);

            if (!_groupCassetteData.ContainsKey(e.GroupName))
            {
                _groupCassetteData[e.GroupName] = new Dictionary<int, CassetteInfo>();
            }
            if (!_groupCassetteData[e.GroupName].ContainsKey(e.ItemNumber))
            {
                _groupCassetteData[e.GroupName][e.ItemNumber] = new CassetteInfo();
            }

            var cassetteInfo = _groupCassetteData[e.GroupName][e.ItemNumber];
            cassetteInfo.PositionA = e.Data.PositionA;
            cassetteInfo.PositionT = e.Data.PositionT;
            cassetteInfo.PositionZ = e.Data.PositionZ;
            cassetteInfo.SlotCount = e.Data.SlotCount;
            cassetteInfo.Pitch = e.Data.Pitch;

            if (ShouldUpdateUIForSharedData(e.GroupName, e.ItemNumber, true))
            {
                System.Diagnostics.Debug.WriteLine("Movement UI: Cassette 데이터 변경으로 UI 업데이트 트리거!");
                UpdateCassetteStageDisplay();
                UpdateFixedCoordinatesFromData();
                LoadCoordinatesForMenu(_selectedGroupMenu);
            }
            SaveToPersistentStorage();
            System.Diagnostics.Debug.WriteLine("Movement UI: Cassette 데이터 처리 완료 및 영구 저장됨.");
        }

        private void SharedDataManager_StageDataUpdated(object sender, SharedDataManager.DataUpdatedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"=== Movement UI: SharedDataManager_StageDataUpdated 수신 ===");
            System.Diagnostics.Debug.WriteLine($"Group: {e.GroupName}, Stage: {e.ItemNumber}");
            System.Diagnostics.Debug.WriteLine($"Position: ({e.Data.PositionA}, {e.Data.PositionT}, {e.Data.PositionZ}), SlotCount: {e.Data.SlotCount}");

            EnsureGroupDataInitialized(e.GroupName);

            if (!_groupStageData.ContainsKey(e.GroupName))
            {
                _groupStageData[e.GroupName] = new Dictionary<int, StageInfo>();
            }
            if (!_groupStageData[e.GroupName].ContainsKey(e.ItemNumber))
            {
                _groupStageData[e.GroupName][e.ItemNumber] = new StageInfo();
            }

            var stageInfo = _groupStageData[e.GroupName][e.ItemNumber];
            stageInfo.PositionA = e.Data.PositionA;
            stageInfo.PositionT = e.Data.PositionT;
            stageInfo.PositionZ = e.Data.PositionZ;
            stageInfo.SlotCount = e.Data.SlotCount;
            stageInfo.Pitch = e.Data.Pitch;

            if (ShouldUpdateUIForSharedData(e.GroupName, e.ItemNumber, false)) // false for Stage
            {
                System.Diagnostics.Debug.WriteLine("Movement UI: Stage 데이터 변경으로 UI 업데이트 트리거!");
                UpdateCassetteStageDisplay();
                UpdateFixedCoordinatesFromData();
                LoadCoordinatesForMenu(_selectedGroupMenu);
            }
            SaveToPersistentStorage();
            System.Diagnostics.Debug.WriteLine("Movement UI: Stage 데이터 처리 완료 및 영구 저장됨.");
        }

        private bool ShouldUpdateUIForSharedData(string groupName, int itemNumber, bool isCassetteEvent)
        {
            if (!_isGroupDetailMode) return false;
            if (groupName != _currentSelectedGroup) return false;
            if (string.IsNullOrEmpty(_selectedGroupMenu) || _selectedGroupMenu == "Aligner") return false;
            if (!_groupMenuSelectedNumbers.ContainsKey(_currentSelectedGroup) ||
                !_groupMenuSelectedNumbers[_currentSelectedGroup].ContainsKey(_selectedGroupMenu)) return false;
            if (_groupMenuSelectedNumbers[_currentSelectedGroup][_selectedGroupMenu] != itemNumber) return false;

            bool currentMenuIsCassetteType = IsCassetteMenu(_selectedGroupMenu);
            if (currentMenuIsCassetteType != isCassetteEvent) return false;

            return true;
        }

        private void Movement_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Movement_Loaded 시작 ===");

                // 포커스 설정 (기존)
                this.Focus();

                // UI 요소 검증 추가
                ValidateUIElements();

                // 초기 Current 좌표 표시
                UpdateCurrentCoordinateDisplay();

                // 슬롯 UI 초기화
                UpdateSlotTrackingUI(1, 1);

                System.Diagnostics.Debug.WriteLine("=== Movement_Loaded 완료 ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement_Loaded 오류: {ex.Message}");
            }
        }

        // 타이머 간격 업데이트 메서드 추가
        private void UpdateTimerInterval()
        {
            if (_autoExecutionTimer != null)
            {
                // 기본 간격 10ms에서 속도에 따라 조정
                // 200% = 5ms (2배 빠름), 50% = 20ms (2배 느림)
                int currentSpeed = GlobalSpeedManager.CurrentSpeed;
                double intervalMs = 10.0 * (100.0 / currentSpeed);

                // 최소 1ms, 최대 100ms로 제한
                intervalMs = Math.Max(1, Math.Min(100, intervalMs));

                _autoExecutionTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);

                System.Diagnostics.Debug.WriteLine($"Movement 타이머 간격 업데이트: {intervalMs}ms (Speed: {currentSpeed}%)");
            }
        }

        // Start 버튼 이벤트
        private void AutoStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 권한 확인 - Auto 모드에서만 자동 실행 허용
                if (!GlobalModeManager.IsAutoMode)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.OPERATION_LIMIT,
                        "Auto execution requires Auto mode");
                    return;
                }

                // 이미 실행 중인 경우 중복 시작 방지
                if (_isAutoExecutionRunning)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                        "Auto execution is already running");
                    return;
                }

                StartAutoExecution();

                // I/O 자동 제어 추가
                IOController.SetOutput("Green Light", true);   // DO06 ON
                IOController.SetOutput("Red Light", false);    // DO07 OFF
                IOController.SetOutput("Buzzer", false);       // DO08 OFF

                System.Diagnostics.Debug.WriteLine("자동 실행 시작됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Movement: 자동 실행 시작 중 오류: " + ex.Message);
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "자동 실행 시작 중 오류가 발생했습니다.");
            }
        }

        // Stop 버튼 이벤트
        private void AutoStop_Click(object sender, RoutedEventArgs e)
        {
            StopAutoExecution();

            // I/O 자동 제어 추가
            IOController.SetOutput("Green Light", false);  // DO06 OFF
            IOController.SetOutput("Red Light", true);     // DO07 ON
            IOController.SetOutput("Buzzer", true);        // DO08 ON (정지 알림)
        }

        // 자동 실행 시작
        private void StartAutoExecution()
        {
            _isAutoExecutionRunning = true;
            _currentSequenceIndex = 0; // CPick부터 시작
            _currentPointIndex = 0;    // P1부터 시작

            // 초기 좌표를 Home Position 또는 0,0,0으로 설정
            _currentR = 0.00m;
            _currentT = 0.00m;
            _currentA = 0.00m;

            // 첫 번째 목표 좌표 설정 (CPick P1)
            SetNextTarget();

            // 타이머 시작
            _autoExecutionTimer.Start();

            UpdateAutoStatusDisplay();
            UpdateCurrentCoordinateDisplay(); // 즉시 한번 업데이트

            // Remote 상태 업데이트
            IsRemoteExecutionRunning = true;
            RemoteExecutionStatusChanged?.Invoke(this, true);

            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                "Auto execution started - CPick P1");

            System.Diagnostics.Debug.WriteLine("자동 실행 시작: CPick P1으로 이동 시작");
        }

        // 자동 실행 중지
        private void StopAutoExecution()
        {
            _isAutoExecutionRunning = false;
            _autoExecutionTimer.Stop();

            UpdateAutoStatusDisplay();

            // Remote 상태 업데이트 추가
            IsRemoteExecutionRunning = false;
            RemoteExecutionStatusChanged?.Invoke(this, false);

            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                "Auto execution stopped");
        }

        // 다음 목표 좌표 설정
        private void SetNextTarget()
        {
            string currentMenu = _executionSequence[_currentSequenceIndex];
            string pointName = $"P{_currentPointIndex + 1}";

            // 현재 그룹의 해당 메뉴에서 좌표 가져오기
            if (_groupCoordinateData.ContainsKey(_currentSelectedGroup) &&
                _groupCoordinateData[_currentSelectedGroup].ContainsKey(currentMenu))
            {
                var coordData = _groupCoordinateData[_currentSelectedGroup][currentMenu];
                string[] targetCoords = GetPointCoordinates(coordData, _currentPointIndex);

                if (decimal.TryParse(targetCoords[0], out _targetR) &&
                    decimal.TryParse(targetCoords[1], out _targetT) &&
                    decimal.TryParse(targetCoords[2], out _targetA))
                {
                    // 기본 목표 설정 완료
                    System.Diagnostics.Debug.WriteLine($"목표 좌표 설정: {currentMenu} {pointName} - R:{_targetR}, T:{_targetT}, A:{_targetA}");

                    // CPick/SPick의 P3에서만 슬롯별 Z 좌표 동적 계산
                    if ((currentMenu == "CPick" || currentMenu == "SPick") &&
                        _isSlotTrackingActive &&
                        _currentPointIndex == 2) // P3일 때만
                    {
                        var teachingData = GetTeachingDataForMenu(_currentSelectedGroup, currentMenu);
                        if (teachingData != null)
                        {
                            decimal slotZ = teachingData.PositionZ + ((_currentSlotNumber - 1) * teachingData.Pitch);
                            _targetA = slotZ; // A축이 Z축 역할
                            System.Diagnostics.Debug.WriteLine($"슬롯 {_currentSlotNumber} 동적 Z 좌표 적용: {slotZ:F2}");
                        }
                    }
                }
                else
                {
                    // 좌표 파싱 실패 시 기본값
                    _targetR = 0.00m;
                    _targetT = 0.00m;
                    _targetA = 0.00m;
                    System.Diagnostics.Debug.WriteLine($"좌표 파싱 실패, 기본값 사용: {currentMenu} {pointName}");
                }
            }
            else
            {
                // 메뉴 데이터가 없는 경우 기본값
                _targetR = 0.00m;
                _targetT = 0.00m;
                _targetA = 0.00m;
                System.Diagnostics.Debug.WriteLine($"메뉴 데이터 없음, 기본값 사용: {currentMenu} {pointName}");
            }

            // 자동 상태 표시 업데이트
            UpdateAutoStatusDisplay();
        }

        // 좌표 배열에서 특정 포인트 가져오기
        private string[] GetPointCoordinates(CoordinateData coordData, int pointIndex)
        {
            switch (pointIndex)
            {
                case 0: return coordData.P1;
                case 1: return coordData.P2;
                case 2: return coordData.P3;
                case 3: return coordData.P4;
                case 4: return coordData.P5;
                case 5: return coordData.P6;
                case 6: return coordData.P7;
                default: return new string[] { "0.00", "0.00", "0.00", "100" };
            }
        }

        // 자동 실행 타이머 이벤트
        /// <summary>
        /// 자동 실행 타이머 이벤트 (슬롯 추적 포함)
        /// </summary>
        private void AutoExecutionTimer_Tick(object sender, EventArgs e)
        {
            if (!_isAutoExecutionRunning) return;

            // 대기 중인지 확인
            if (_isWaiting)
            {
                // 1초 경과했는지 확인
                if ((DateTime.Now - _waitStartTime).TotalSeconds >= WAIT_SECONDS)
                {
                    _isWaiting = false;
                    MoveToNextPoint(); // 다음 포인트로 이동
                }
                return; // 대기 중이면 좌표 이동하지 않음
            }

            // 각 축별로 1씩 증가하여 목표에 도달
            bool rReached = MoveToTarget(ref _currentR, _targetR);
            bool tReached = MoveToTarget(ref _currentT, _targetT);
            bool aReached = MoveToTarget(ref _currentA, _targetA);

            // 현재 좌표 UI 업데이트 (매 틱마다 호출)
            UpdateCurrentCoordinateDisplay();

            // 모든 축이 목표에 도달했으면 처리 (단순화)
            if (rReached && tReached && aReached)
            {
                StartWait(); // 항상 1초 대기 후 MoveToNextPoint에서 처리
            }
        }

        // 대기 시작 메서드 추가
        private void StartWait()
        {
            _isWaiting = true;
            _waitStartTime = DateTime.Now;

            string currentMenu = _executionSequence[_currentSequenceIndex];
            string pointName = $"P{_currentPointIndex + 1}";

            AlarmMessageManager.ShowAlarm(Alarms.POSITION_LOADED,
                $"Reached {currentMenu} {pointName} - Waiting 1 second...");

            System.Diagnostics.Debug.WriteLine($"포인트 도달 후 1초 대기 시작: {currentMenu} {pointName}");
        }

        /// <summary>
        /// 목표 좌표로 이동 (허용 오차 0.1 사용)
        /// </summary>
        private bool MoveToTarget(ref decimal current, decimal target)
        {
            const decimal TOLERANCE = 0.1m; // 도달 허용 오차

            // 허용 오차 범위 내에 있으면 목표 도달
            if (Math.Abs(current - target) <= TOLERANCE)
            {
                current = target; // 정확히 목표 위치로 설정
                return true;
            }

            // 1씩 이동 (COORDINATE_STEP = 1.0m)
            if (current < target)
                current += COORDINATE_STEP;
            else
                current -= COORDINATE_STEP;

            return false;
        }

        // 다음 포인트로 이동
        private void MoveToNextPoint()
        {
            string currentMenu = _executionSequence[_currentSequenceIndex];

            // 모든 메뉴에서 P1 → P2 → P3 → ... → P7 순차 이동
            _currentPointIndex++;

            // CPick/SPick의 P3에서만 슬롯 처리 (다음 메뉴로 넘어가지 않음)
            if ((currentMenu == "CPick" || currentMenu == "SPick") &&
                _isSlotTrackingActive &&
                _currentPointIndex == 3) // P3 완료 후 (_currentPointIndex가 이미 +1 되어 3이 됨)
            {
                System.Diagnostics.Debug.WriteLine($"{currentMenu} P3 완료 - 슬롯 {_currentSlotNumber} 처리됨");
                MoveToNextSlot();
                ApplyP3CoordinatesForCurrentSlot(currentMenu);
                // 주의: 다음 메뉴로 넘어가지 않고 P4로 계속 진행
            }

            // P7까지 갔으면 다음 메뉴로
            if (_currentPointIndex >= 7) // P7 완료
            {
                _currentSequenceIndex = (_currentSequenceIndex + 1) % _executionSequence.Length;
                _currentPointIndex = 0; // 다음 메뉴는 P1부터

                string nextMenu = _executionSequence[_currentSequenceIndex];
                System.Diagnostics.Debug.WriteLine($"{currentMenu} P7 완료, 다음 메뉴로 이동: {nextMenu}");
            }

            SetNextTarget();
        }

        // 현재 좌표 표시 업데이트
        private void UpdateCurrentCoordinateDisplay()
        {
            try
            {
                // UI 스레드에서 실행되는지 확인
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(new Action(UpdateCurrentCoordinateDisplay));
                    return;
                }

                // Current 좌표 TextBlock들이 존재하는지 확인 후 업데이트
                if (txtCurrentR != null)
                {
                    txtCurrentR.Text = _currentR.ToString("F2");
                    System.Diagnostics.Debug.WriteLine($"txtCurrentR 업데이트: {_currentR:F2}");
                }

                if (txtCurrentT != null)
                {
                    txtCurrentT.Text = _currentT.ToString("F2");
                    System.Diagnostics.Debug.WriteLine($"txtCurrentT 업데이트: {_currentT:F2}");
                }

                if (txtCurrentA != null)
                {
                    txtCurrentA.Text = _currentA.ToString("F2");
                    System.Diagnostics.Debug.WriteLine($"txtCurrentA 업데이트: {_currentA:F2}");
                }

                // Monitor로 좌표 전달
                CurrentCoordinateChanged?.Invoke(this, new MovementCoordinateEventArgs(_currentR, _currentT, _currentA));

                // 디버그 로그
                System.Diagnostics.Debug.WriteLine($"Current 좌표 업데이트 완료: R={_currentR:F2}, T={_currentT:F2}, A={_currentA:F2}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateCurrentCoordinateDisplay 오류: {ex.Message}");
            }
        }

        // 자동 실행 상태 표시 업데이트
        private void UpdateAutoStatusDisplay()
        {
            try
            {
                if (txtAutoStatus != null)
                {
                    if (_isAutoExecutionRunning)
                    {
                        string currentMenu = _executionSequence[_currentSequenceIndex];
                        string status = $"Running - {currentMenu}";

                        // 슬롯 추적이 활성화된 경우 슬롯 정보 추가
                        if (_isSlotTrackingActive && currentMenu == "CPick")
                        {
                            status += $" (Slot {_currentSlotNumber})";
                        }

                        txtAutoStatus.Text = status;
                        txtAutoStatus.Foreground = new SolidColorBrush(Colors.Green);
                    }
                    else
                    {
                        txtAutoStatus.Text = "Stopped";
                        txtAutoStatus.Foreground = new SolidColorBrush(Colors.Red);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateAutoStatusDisplay 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 정적 데이터에 직접 HomePos를 적용하는 메서드 (Setup에서 호출용)
        /// </summary>
        public static void ApplyHomePosToStaticData(decimal homeA, decimal homeT, decimal homeZ)
        {
            System.Diagnostics.Debug.WriteLine("=== ApplyHomePosToStaticData called ===");
            System.Diagnostics.Debug.WriteLine($"HomePos: A={homeA}, T={homeT}, Z={homeZ}");

            var targetMenus = new[] { "CPick", "CPlace", "SPick", "SPlace" };
            var availableGroups = GroupDataManager.GetAvailableGroups();

            if (!availableGroups.Any())
            {
                availableGroups.Add("Group1");
                GroupDataManager.UpdateGroupList(new List<string>(availableGroups));
            }

            int updatedCount = 0;

            foreach (string groupName in availableGroups)
            {
                // 그룹 데이터 초기화
                if (!_persistentGroupCoordinateData.ContainsKey(groupName))
                {
                    _persistentGroupCoordinateData[groupName] = new Dictionary<string, CoordinateData>();
                }

                foreach (string menuType in targetMenus)
                {
                    // 메뉴 데이터 초기화
                    if (!_persistentGroupCoordinateData[groupName].ContainsKey(menuType))
                    {
                        _persistentGroupCoordinateData[groupName][menuType] = new CoordinateData();
                    }

                    var coordData = _persistentGroupCoordinateData[groupName][menuType];

                    // P1에 HomePos 적용
                    coordData.P1[0] = homeA.ToString("F2");  // A/R 좌표
                    coordData.P1[1] = homeT.ToString("F2");  // T 좌표  
                    coordData.P1[2] = homeZ.ToString("F2");  // Z 좌표
                                                             // P1[3] (Speed)는 기존 값 유지

                    updatedCount++;
                    System.Diagnostics.Debug.WriteLine($"{groupName} - {menuType} P1 static data updated: ({homeA:F2}, {homeT:F2}, {homeZ:F2})");
                }
            }

            System.Diagnostics.Debug.WriteLine($"Total {updatedCount} menu P1 coordinates updated with HomePos");
        }

        #endregion

        #region Physics Test Methods
        private void TestPhysicsCalculations()
        {
            System.Diagnostics.Debug.WriteLine("=== TestPhysicsCalculations method called ===");
            System.Diagnostics.Debug.WriteLine($"Current group: {_currentSelectedGroup}");
            System.Diagnostics.Debug.WriteLine($"Current menu: {_selectedGroupMenu}");

            try
            {
                if (string.IsNullOrEmpty(_currentSelectedGroup) || string.IsNullOrEmpty(_selectedGroupMenu))
                {
                    System.Diagnostics.Debug.WriteLine("Group or menu not selected!");
                    AlarmMessageManager.ShowAlarm(Alarms.WARNING, "Please select Group and Menu(CPick, CPlace, SPick, SPlace) first!");
                    return;
                }

                if (_selectedGroupMenu == "Aligner")
                {
                    AlarmMessageManager.ShowAlarm(Alarms.WARNING, "Aligner menu does not support physics test!");
                    return;
                }

                double accel = Setup.Acceleration;
                double decel = Setup.Deceleration;

                if (!_groupCoordinateData.ContainsKey(_currentSelectedGroup) ||
                    !_groupCoordinateData[_currentSelectedGroup].ContainsKey(_selectedGroupMenu))
                {
                    AlarmMessageManager.ShowAlarm(Alarms.WARNING, "No coordinate data found for current menu!");
                    return;
                }

                var coordData = _groupCoordinateData[_currentSelectedGroup][_selectedGroupMenu];
                var allPoints = new[] { coordData.P1, coordData.P2, coordData.P3, coordData.P4, coordData.P5, coordData.P6, coordData.P7 };

                var results = CalculateAllSegmentPhysics(allPoints, accel, decel);

                ShowDetailedTestResults(results, _currentSelectedGroup, _selectedGroupMenu, accel, decel);

                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                    $"{_currentSelectedGroup} {_selectedGroupMenu} - All segment physics test completed!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TestPhysicsCalculations error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                MessageBox.Show($"Test error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Physics test error: {ex.Message}");
            }
        }

        private List<SegmentPhysicsResult> CalculateAllSegmentPhysics(string[][] allPoints, double acceleration, double deceleration)
        {
            var results = new List<SegmentPhysicsResult>();

            for (int i = 0; i < allPoints.Length - 1; i++)
            {
                string[] startPoint = allPoints[i];
                string[] endPoint = allPoints[i + 1];

                if (startPoint == null || endPoint == null) continue;

                var result = new SegmentPhysicsResult
                {
                    SegmentName = $"P{i + 1} -> P{i + 2}",
                    StartPoint = $"({startPoint[0]}, {startPoint[1]}, {startPoint[2]})",
                    EndPoint = $"({endPoint[0]}, {endPoint[1]}, {endPoint[2]})",
                    LimitErrorMessage = string.Empty
                };

                try
                {
                    // 시작점 검사
                    if (decimal.TryParse(startPoint[0], out var p1A) && decimal.TryParse(startPoint[1], out var p1T) && decimal.TryParse(startPoint[2], out var p1Z))
                    {
                        var validationResult = MovementDataHelper.CheckSoftLimits(p1A, p1T, p1Z);
                        if (!validationResult.IsValid) result.LimitErrorMessage += $"Start point P{i + 1} Error: {validationResult.ErrorMessage}\n";
                    }

                    // 끝점 검사
                    if (decimal.TryParse(endPoint[0], out var p2A) && decimal.TryParse(endPoint[1], out var p2T) && decimal.TryParse(endPoint[2], out var p2Z))
                    {
                        var validationResult = MovementDataHelper.CheckSoftLimits(p2A, p2T, p2Z);
                        if (!validationResult.IsValid) result.LimitErrorMessage += $"End point P{i + 2} Error: {validationResult.ErrorMessage}";
                    }
                    result.LimitErrorMessage = result.LimitErrorMessage.Trim();

                    // 계산 로직은 리미트 에러가 없을 때만 유효하게 처리
                    if (string.IsNullOrEmpty(result.LimitErrorMessage))
                    {
                        double distance = MovementDataHelper.CalculateDistanceBetweenATZPoints(startPoint, endPoint);
                        double theoreticalMaxSpeed = MovementDataHelper.CalculateMaximumSpeedC(acceleration, deceleration, distance);
                        int finalCommandSpeed = GlobalSpeedManager.ApplyPendantSpeedSetting((int)Math.Round(theoreticalMaxSpeed));

                        result.Distance = distance;
                        result.TheoreticalMaxSpeed = theoreticalMaxSpeed;
                        result.FinalCommandSpeed = finalCommandSpeed;
                        result.IsValidSegment = distance > 0.001;
                    }
                    else // 리미트 에러가 있으면 계산 스킵
                    {
                        result.IsValidSegment = false;
                    }
                }
                catch (Exception ex)
                {
                    result.IsValidSegment = false;
                    result.ErrorMessage = ex.Message;
                }
                results.Add(result);
            }
            return results;
        }

        private void ShowDetailedTestResults(List<SegmentPhysicsResult> results, string groupName, string menuName, double accel, double decel)
        {
            try
            {
                // 네임스페이스를 명시적으로 지정
                var resultWindow = new TeachingPendant.MovementUI.PhysicsTestResultWindow(results, groupName, menuName, accel, decel)
                {
                    Owner = Window.GetWindow(this)
                };

                resultWindow.ShowDialog();

                System.Diagnostics.Debug.WriteLine($"Physics test result window opened for {groupName} {menuName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening physics result window: {ex.Message}");
                MessageBox.Show($"Error opening physics test result window: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Persistent Data Management
        private void LoadPersistentData()
        {
            CopyPersistentToInstance();

            _currentSelectedGroup = _persistentCurrentSelectedGroup;
            _isGroupDetailMode = _persistentIsGroupDetailMode;
            _selectedGroupMenu = _persistentSelectedGroupMenu;

            System.Diagnostics.Debug.WriteLine($"Persistent data loaded. Current group: {_currentSelectedGroup}, Detail mode: {_isGroupDetailMode}, Selected menu: {_selectedGroupMenu}");
        }

        private void CopyPersistentToInstance()
        {
            _groupCoordinateData = CloneDictionary(_persistentGroupCoordinateData, CloneCoordinateData);
            _groupMenuSelectedNumbers = CloneDictionary(_persistentGroupMenuSelectedNumbers);
            _groupCassetteData = CloneDictionary(_persistentGroupCassetteData, CloneCassetteInfo);
            _groupStageData = CloneDictionary(_persistentGroupStageData, CloneStageInfo);
            _groupCassetteCounts = new Dictionary<string, int>(_persistentGroupCassetteCounts);
        }

        private CassetteInfo CloneCassetteInfo(CassetteInfo source)
        {
            if (source == null) return null;
            return new CassetteInfo
            {
                SlotCount = source.SlotCount,
                PositionA = source.PositionA,
                PositionT = source.PositionT,
                PositionZ = source.PositionZ,
                Pitch = source.Pitch,
                PickOffset = source.PickOffset,
                PickDown = source.PickDown,
                PickUp = source.PickUp,
                PlaceDown = source.PlaceDown,
                PlaceUp = source.PlaceUp
            };
        }

        private StageInfo CloneStageInfo(StageInfo source)
        {
            if (source == null) return null;
            return new StageInfo
            {
                SlotCount = source.SlotCount,
                PositionA = source.PositionA,
                PositionT = source.PositionT,
                PositionZ = source.PositionZ,
                Pitch = source.Pitch,
                PickOffset = source.PickOffset,
                PickDown = source.PickDown,
                PickUp = source.PickUp,
                PlaceDown = source.PlaceDown,
                PlaceUp = source.PlaceUp
            };
        }

        private Dictionary<TKey, Dictionary<TKey2, TValue>> CloneDictionary<TKey, TKey2, TValue>(
            Dictionary<TKey, Dictionary<TKey2, TValue>> source,
            Func<TValue, TValue> cloneFunc = null)
        {
            var result = new Dictionary<TKey, Dictionary<TKey2, TValue>>();
            if (source != null)
            {
                foreach (var item in source)
                {
                    result[item.Key] = new Dictionary<TKey2, TValue>();
                    if (item.Value != null)
                    {
                        foreach (var subItem in item.Value)
                        {
                            result[item.Key][subItem.Key] = cloneFunc != null ? cloneFunc(subItem.Value) : subItem.Value;
                        }
                    }
                }
            }
            return result;
        }

        private Dictionary<TKey, TValue> CloneDictionary<TKey, TValue>(Dictionary<TKey, TValue> source)
        {
            return source == null ? new Dictionary<TKey, TValue>() : new Dictionary<TKey, TValue>(source);
        }

        private CoordinateData CloneCoordinateData(CoordinateData source)
        {
            return new CoordinateData
            {
                P1 = CloneCoordinateArray(source.P1),
                P2 = CloneCoordinateArray(source.P2),
                P3 = CloneCoordinateArray(source.P3),
                P4 = CloneCoordinateArray(source.P4),
                P5 = CloneCoordinateArray(source.P5),
                P6 = CloneCoordinateArray(source.P6),
                P7 = CloneCoordinateArray(source.P7)
            };
        }

        private string[] CloneCoordinateArray(string[] source)
        {
            return source == null
                ? new string[] { "0.00", "0.00", "0.00", "100" }
                : new string[] { source[0], source[1], source[2], source[3] };
        }

        private void SaveToPersistentStorage()
        {
            _persistentGroupCoordinateData = CloneDictionary(_groupCoordinateData, CloneCoordinateData);
            _persistentGroupMenuSelectedNumbers = CloneDictionary(_groupMenuSelectedNumbers);
            _persistentGroupCassetteData = CloneDictionary(_groupCassetteData, CloneCassetteInfo);
            _persistentGroupStageData = CloneDictionary(_groupStageData, CloneStageInfo);
            _persistentGroupCassetteCounts = new Dictionary<string, int>(_groupCassetteCounts);

            _persistentCurrentSelectedGroup = _currentSelectedGroup;
            _persistentIsGroupDetailMode = _isGroupDetailMode;
            _persistentSelectedGroupMenu = _selectedGroupMenu;

            System.Diagnostics.Debug.WriteLine($"Data saved to persistent storage. Group: {_currentSelectedGroup}, Menu: {_selectedGroupMenu}");
        }
        #endregion

        #region Group Management
        private void InitializeGroupData()
        {
            _groupList = GroupDataManager.GetAvailableGroups();
            if (!_groupList.Any())
            {
                _groupList.Add("Group1");
                GroupDataManager.UpdateGroupList(new List<string>(_groupList));
            }

            PopulateGroupControls();

            if (!string.IsNullOrEmpty(_persistentCurrentSelectedGroup) && _groupList.Contains(_persistentCurrentSelectedGroup))
            {
                _currentSelectedGroup = _persistentCurrentSelectedGroup;
            }
            else if (_groupList.Any())
            {
                _currentSelectedGroup = _groupList[0];
            }
            else
            {
                _currentSelectedGroup = "Group1";
                if (!_groupList.Contains("Group1")) _groupList.Add("Group1");
                GroupDataManager.UpdateGroupList(new List<string>(_groupList));
            }

            SetSelectedGroup(_currentSelectedGroup);
            foreach (string groupName in _groupList)
            {
                EnsureGroupDataInitialized(groupName);
            }

            System.Diagnostics.Debug.WriteLine($"Initialized with {_groupList.Count} groups. Current: {_currentSelectedGroup}");
        }

        private void PopulateGroupControls()
        {
            cmbGroups.Items.Clear();
            lstGroups.Items.Clear();

            foreach (string groupName in _groupList)
            {
                cmbGroups.Items.Add(groupName);
                lstGroups.Items.Add(groupName);
            }
        }

        private void SetSelectedGroup(string groupName)
        {
            if (_groupList.Contains(groupName))
            {
                cmbGroups.SelectedItem = groupName;
                lstGroups.SelectedItem = groupName;
            }
            else if (cmbGroups.Items.Count > 0)
            {
                cmbGroups.SelectedIndex = 0;
                lstGroups.SelectedIndex = 0;
                _currentSelectedGroup = cmbGroups.SelectedItem.ToString();
            }
        }

        private void EnsureGroupDataInitialized(string groupName)
        {
            if (string.IsNullOrEmpty(groupName)) return;

            InitializeGroupMenuSelectedNumbers(groupName);
            InitializeGroupCassetteData(groupName);
            InitializeGroupStageData(groupName);
            InitializeGroupCoordinateData(groupName);
            InitializeGroupCassetteCount(groupName);
        }

        private void InitializeGroupMenuSelectedNumbers(string groupName)
        {
            if (!_groupMenuSelectedNumbers.ContainsKey(groupName))
            {
                _groupMenuSelectedNumbers[groupName] = new Dictionary<string, int>
                {
                    { "CPick", 1 }, { "CPlace", 1 }, { "SPick", 1 }, { "SPlace", 2 }
                };
            }
        }

        private void InitializeGroupCassetteData(string groupName)
        {
            if (!_groupCassetteData.ContainsKey(groupName))
            {
                _groupCassetteData[groupName] = new Dictionary<int, CassetteInfo>();
            }
        }

        private void InitializeGroupStageData(string groupName)
        {
            if (!_groupStageData.ContainsKey(groupName))
            {
                _groupStageData[groupName] = new Dictionary<int, StageInfo>();
                for (int i = 1; i <= STAGE_COUNT_FIXED; i++)
                {
                    if (!_groupStageData[groupName].ContainsKey(i))
                        _groupStageData[groupName][i] = new StageInfo();
                }
            }
        }

        private void InitializeGroupCoordinateData(string groupName)
        {
            if (!_groupCoordinateData.ContainsKey(groupName) || _groupCoordinateData[groupName] == null)
            {
                _groupCoordinateData[groupName] = new Dictionary<string, CoordinateData>();
            }
            string[] menuTypes = { "CPick", "CPlace", "SPick", "SPlace", "Aligner" };
            foreach (var menuType in menuTypes)
            {
                if (!_groupCoordinateData[groupName].ContainsKey(menuType) || _groupCoordinateData[groupName][menuType] == null)
                {
                    _groupCoordinateData[groupName][menuType] = new CoordinateData();
                }
            }
        }

        private void InitializeGroupCassetteCount(string groupName)
        {
            if (!_groupCassetteCounts.ContainsKey(groupName))
            {
                _groupCassetteCounts[groupName] = CASSETTE_COUNT_FIXED;
            }
        }
        #endregion

        #region UI Management
        private void ShowGroupListView()
        {
            _isGroupDetailMode = false;
            SetPanelVisibility(GroupListPanel, GroupDetailPanel);
            SetCoordinateAreaVisibility(false);
            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Group list view displayed");
        }

        private void ShowGroupDetailView()
        {
            if (string.IsNullOrEmpty(_currentSelectedGroup)) return;

            _isGroupDetailMode = true;
            SetPanelVisibility(GroupDetailPanel, GroupListPanel);

            txtCurrentGroup.Text = _currentSelectedGroup.ToUpper();
            ResetGroupMenuButtonsState();
            SetCoordinateAreaVisibility(false);

            if (!string.IsNullOrEmpty(_selectedGroupMenu))
            {
                HandleGroupMenuSelection(_selectedGroupMenu, true);
            }
            else
            {
                AlarmMessageManager.ShowAlarm(Alarms.POSITION_LOADED,
                    $"Opened {_currentSelectedGroup} detail view. Select a menu.");
            }
        }

        private void SetPanelVisibility(FrameworkElement showPanel, FrameworkElement hidePanel)
        {
            showPanel.Visibility = Visibility.Visible;
            hidePanel.Visibility = Visibility.Collapsed;
        }

        private void SetCoordinateAreaVisibility(bool show)
        {
            if (show)
            {
                EmptyCoordinateArea.Visibility = Visibility.Collapsed;
                CoordinateScrollViewer.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyCoordinateArea.Visibility = Visibility.Visible;
                CoordinateScrollViewer.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Event Handlers - Group Selection
        private void GroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbGroups?.SelectedItem == null || e.AddedItems.Count == 0) return;

            string selectedGroup = e.AddedItems[0].ToString();
            HandleGroupSelection(selectedGroup, lstGroups);
        }

        private void GroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstGroups.SelectedItem != null && e.AddedItems.Count > 0)
            {
                string selectedGroup = e.AddedItems[0].ToString();
                HandleGroupSelection(selectedGroup, cmbGroups);
            }
        }

        private void HandleGroupSelection(string selectedGroup, Control otherControl)
        {
            if (_isGroupDetailMode && _currentSelectedGroup == selectedGroup)
            {
                SynchronizeGroupSelection(otherControl, selectedGroup);
                return;
            }

            SaveCurrentCoordinatesIfNeeded();

            _currentSelectedGroup = selectedGroup;
            EnsureGroupDataInitialized(_currentSelectedGroup);

            SynchronizeGroupSelection(otherControl, selectedGroup);

            if (_isGroupDetailMode)
            {
                _selectedGroupMenu = string.Empty;
                ShowGroupDetailView();
            }
            else
            {
                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, $"Group selected: {_currentSelectedGroup}");
            }
        }

        private void SynchronizeGroupSelection(Control control, string selectedGroup)
        {
            if (control is ComboBox comboBox && comboBox.SelectedItem?.ToString() != selectedGroup)
            {
                comboBox.SelectedItem = selectedGroup;
            }
            else if (control is ListBox listBox && listBox.SelectedItem?.ToString() != selectedGroup)
            {
                listBox.SelectedItem = selectedGroup;
            }
        }

        private void SaveCurrentCoordinatesIfNeeded()
        {
            if (_isGroupDetailMode && !string.IsNullOrEmpty(_selectedGroupMenu) &&
                CoordinateScrollViewer.Visibility == Visibility.Visible &&
                GlobalModeManager.IsEditingAllowed)
            {
                SaveCurrentCoordinates();
            }
        }
        #endregion

        #region Event Handlers - Group Management Buttons
        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSelectedGroup))
            {
                AlarmMessageManager.ShowAlarm(Alarms.UNEXPECTED_STATE, "Please select a group first");
                return;
            }
            EnsureGroupDataInitialized(_currentSelectedGroup);
            ShowGroupDetailView();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentCoordinatesIfNeeded();
            _selectedGroupMenu = string.Empty;
            ShowGroupListView();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateManualMode("Add only in Manual mode")) return;
            if (_groupList.Count >= 20)
            {
                AlarmMessageManager.ShowAlarm(Alarms.OPERATION_LIMIT, "Max 20 groups");
                return;
            }

            string newGroupName = GenerateUniqueGroupName();
            AddNewGroup(newGroupName);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateManualMode("Delete only in Manual mode")) return;
            if (lstGroups.SelectedItem == null)
            {
                AlarmMessageManager.ShowAlarm(Alarms.UNEXPECTED_STATE, "Select group to delete");
                return;
            }

            string groupToDelete = lstGroups.SelectedItem.ToString();
            if (_groupList.Count <= 1)
            {
                AlarmMessageManager.ShowAlarm(Alarms.OPERATION_LIMIT,
                    $"Cannot delete last group: {groupToDelete}");
                return;
            }

            if (ConfirmGroupDeletion(groupToDelete))
            {
                DeleteGroup(groupToDelete);
            }
        }

        private bool ValidateManualMode(string message)
        {
            if (!GlobalModeManager.IsEditingAllowed)
            {
                AlarmMessageManager.ShowAlarm(Alarms.OPERATION_LIMIT, message);
                return false;
            }
            return true;
        }

        private string GenerateUniqueGroupName()
        {
            int nextGroupNumber = 1;
            string newGroupName;
            do
            {
                newGroupName = $"Group{nextGroupNumber++}";
            } while (_groupList.Contains(newGroupName));
            return newGroupName;
        }

        private void AddNewGroup(string groupName)
        {
            _groupList.Add(groupName);
            EnsureGroupDataInitialized(groupName);

            lstGroups.Items.Add(groupName);
            cmbGroups.Items.Add(groupName);
            lstGroups.SelectedItem = groupName;

            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, $"Added new {groupName}");
            GroupDataManager.UpdateGroupList(new List<string>(_groupList));
        }

        private bool ConfirmGroupDeletion(string groupName)
        {
            return MessageBox.Show($"Delete '{groupName}' and its data?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        private void DeleteGroup(string groupToDelete)
        {
            int selectedIndex = lstGroups.SelectedIndex;

            RemoveGroupFromCollections(groupToDelete);
            RemoveGroupFromUI(groupToDelete, selectedIndex);

            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, $"Deleted {groupToDelete}");
            GroupDataManager.UpdateGroupList(new List<string>(_groupList));
        }

        private void RemoveGroupFromCollections(string groupName)
        {
            _groupList.Remove(groupName);
            _groupCoordinateData.Remove(groupName);
            _groupMenuSelectedNumbers.Remove(groupName);
            _groupCassetteData.Remove(groupName);
            _groupStageData.Remove(groupName);
            _groupCassetteCounts.Remove(groupName);
        }

        private void RemoveGroupFromUI(string groupName, int selectedIndex)
        {
            lstGroups.Items.RemoveAt(selectedIndex);
            cmbGroups.Items.Remove(groupName);

            if (lstGroups.Items.Count > 0)
            {
                int newIndex = selectedIndex < lstGroups.Items.Count ? selectedIndex : lstGroups.Items.Count - 1;
                lstGroups.SelectedIndex = newIndex;
            }
            else
            {
                _currentSelectedGroup = string.Empty;
                cmbGroups.SelectedItem = null;
            }
        }
        #endregion

        #region Event Handlers - Group Menu Selection
        private void CPick_Click(object sender, RoutedEventArgs e) => HandleGroupMenuSelection("CPick");
        private void CPlace_Click(object sender, RoutedEventArgs e) => HandleGroupMenuSelection("CPlace");
        private void SPick_Click(object sender, RoutedEventArgs e) => HandleGroupMenuSelection("SPick");
        private void SPlace_Click(object sender, RoutedEventArgs e) => HandleGroupMenuSelection("SPlace");
        private void Aligner_Click(object sender, RoutedEventArgs e) => HandleGroupMenuSelection("Aligner");

        private void HandleGroupMenuSelection(string menuType, bool isRestoring = false)
        {
            if (string.IsNullOrEmpty(_currentSelectedGroup))
            {
                AlarmMessageManager.ShowAlarm(Alarms.UNEXPECTED_STATE, "No group selected.");
                return;
            }
            if (!isRestoring && !ValidateMenuChange(menuType)) return;

            if (!isRestoring && !string.IsNullOrEmpty(_selectedGroupMenu) && _selectedGroupMenu != "Aligner")
            {
                SaveCurrentCoordinatesIfNeeded();
            }

            _selectedGroupMenu = menuType;
            UpdateGroupMenuButtonsState();

            if (menuType == "Aligner")
            {
                SetCoordinateAreaVisibility(false);
                _isSlotTrackingActive = false;
                UpdateSlotTrackingUI(1, 1);
                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, $"{_currentSelectedGroup} - Aligner selected");
            }
            else
            {
                ShowCoordinateAreaForMenu(menuType);

                // 슬롯 추적 초기화 (CPick/SPick에 대해서만)
                InitializeSlotTracking(menuType);
            }

            if (!isRestoring)
            {
                SaveToPersistentStorage();
            }
        }

        private bool ValidateMenuChange(string menuType)
        {
            if (!GlobalModeManager.IsEditingAllowed && menuType != _selectedGroupMenu)
            {
                AlarmMessageManager.ShowAlarm(Alarms.OPERATION_LIMIT, "Menu change is only allowed in Manual mode.");
                return false;
            }
            return true;
        }

        private void ShowCoordinateAreaForMenu(string menuType)
        {
            SetCoordinateAreaVisibility(true);
            LoadCoordinatesForMenu(menuType);
            InitializeComboBoxForCurrentMenu();
            SetCoordinateTextBoxesEditability(GlobalModeManager.IsEditingAllowed);
            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, $"{_currentSelectedGroup} - {menuType} selected");
        }
        #endregion

        #region Events for Monitor Integration
        /// <summary>
        /// Monitor로 현재 좌표 전달하는 이벤트
        /// </summary>
        public static event EventHandler<MovementCoordinateEventArgs> CurrentCoordinateChanged;

        /// <summary>
        /// Monitor로 현재 실행 구간 전달하는 이벤트  
        /// </summary>
        public static event EventHandler<MovementSectionEventArgs> CurrentSectionChanged;

        /// <summary>
        /// Remote 실행 상태 변경 이벤트
        /// </summary>
        public static event EventHandler<bool> RemoteExecutionStatusChanged;


        #endregion

        #region Remote Control Interface (Monitor에서 호출용)
        /// <summary>
        /// Monitor에서 Remote 시작 요청 - 인스턴스 독립적 버전
        /// </summary>
        public static void StartRemoteExecution()
        {
            System.Diagnostics.Debug.WriteLine("=== Remote start request received ===");

            try
            {
                // Remote 실행 전 안전 확인 추가
                if (!SafetySystem.IsInitialized)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                        "안전 시스템이 초기화되지 않아 Remote 실행을 시작할 수 없습니다.");
                    return;
                }

                if (!SafetySystem.IsSafeForRobotOperation())
                {
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                        "현재 안전 상태에서는 Remote 실행을 시작할 수 없습니다.");
                    return;
                }

                // Remote 실행 상태 변경
                IsRemoteExecutionRunning = true;

                // Monitor에 상태 알림
                RemoteExecutionStatusChanged?.Invoke(null, true);

                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                    "Remote execution started from Monitor - Safety verified");

                System.Diagnostics.Debug.WriteLine("Remote execution state activation complete with safety check.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("StartRemoteExecution Error: " + ex.Message);
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                    "Remote start error: " + ex.Message);
            }
        }

        /// <summary>
        /// Monitor에서 Remote 정지 요청 - 인스턴스 독립적 버전
        /// </summary>
        public static void StopRemoteExecution()
        {
            System.Diagnostics.Debug.WriteLine("=== Remote stop request received (Standalone version) ===");

            try
            {
                // Remote 실행 상태 변경
                IsRemoteExecutionRunning = false;

                // Monitor에 상태 알림
                RemoteExecutionStatusChanged?.Invoke(null, false);

                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                    "Remote execution stopped from Monitor - Independent mode");

                System.Diagnostics.Debug.WriteLine("Remote execution state deactivation complete.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StopRemoteExecution Error: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                    $"Remote stop error: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 활성화된 Movement 인스턴스 찾기
        /// </summary>
        private static Movement GetCurrentMovementInstance()
        {
            System.Diagnostics.Debug.WriteLine("=== GetCurrentMovementInstance Start ===");
            System.Diagnostics.Debug.WriteLine($"Number of Application.Current.Windows: {Application.Current.Windows.Count}");

            foreach (Window window in Application.Current.Windows)
            {
                System.Diagnostics.Debug.WriteLine($"Window Type: {window.GetType().Name}, Title: {window.Title}");

                if (window is CommonFrame frame)
                {
                    System.Diagnostics.Debug.WriteLine("CommonFrame Found!");
                    System.Diagnostics.Debug.WriteLine($"Is MainContentArea null? {frame.MainContentArea == null}");

                    if (frame.MainContentArea?.Content != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Content Type: {frame.MainContentArea.Content.GetType().Name}");

                        if (frame.MainContentArea.Content is Movement movement)
                        {
                            System.Diagnostics.Debug.WriteLine("Movement Instance Found!");
                            return movement;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("MainContentArea.Content is null");
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine("Movement instance not found.");
            return null;
        }
        #endregion

        #region Static Event Notification Methods (Remote Control Interface)
        /// <summary>
        /// Monitor 및 외부에서 좌표 변경을 알리는 정적 메서드
        /// </summary>
        public static void NotifyCoordinateChanged(decimal r, decimal t, decimal a)
        {
            CurrentCoordinateChanged?.Invoke(null, new MovementCoordinateEventArgs(r, t, a));
        }

        /// <summary>
        /// Monitor 및 외부에서 구간 변경을 알리는 정적 메서드
        /// </summary>
        public static void NotifySectionChanged(string menuName, string pointName, bool isRunning)
        {
            CurrentSectionChanged?.Invoke(null, new MovementSectionEventArgs(menuName, pointName, isRunning));
        }
        #endregion

        #region Event Args Classes
        /// <summary>
        /// Movement 좌표 변경 이벤트 인자
        /// </summary>
        public class MovementCoordinateEventArgs : EventArgs
        {
            public decimal PositionR { get; private set; }
            public decimal PositionT { get; private set; }
            public decimal PositionA { get; private set; }

            public MovementCoordinateEventArgs(decimal r, decimal t, decimal a)
            {
                PositionR = r;
                PositionT = t;
                PositionA = a;
            }
        }

        /// <summary>
        /// Movement 구간 변경 이벤트 인자
        /// </summary>
        public class MovementSectionEventArgs : EventArgs
        {
            public string MenuName { get; private set; }
            public string PointName { get; private set; }
            public bool IsRunning { get; private set; }

            // C# 6.0 호환: expression-bodied property 대신 일반 property
            public string FullSectionName
            {
                get
                {
                    return IsRunning ? string.Format("{0} {1}", MenuName, PointName) : "Stopped";
                }
            }

            public MovementSectionEventArgs(string menuName, string pointName, bool isRunning)
            {
                MenuName = menuName;
                PointName = pointName;
                IsRunning = isRunning;
            }
        }
        #endregion

        #region Button State Management
        private void UpdateGroupMenuButtonsState()
        {
            SetButtonBackground(btnCPick, _selectedGroupMenu == "CPick");
            SetButtonBackground(btnCPlace, _selectedGroupMenu == "CPlace");
            SetButtonBackground(btnSPick, _selectedGroupMenu == "SPick");
            SetButtonBackground(btnSPlace, _selectedGroupMenu == "SPlace");
            SetButtonBackground(btnAligner, _selectedGroupMenu == "Aligner");
        }

        private void ResetGroupMenuButtonsState()
        {
            SetButtonBackground(btnCPick, false);
            SetButtonBackground(btnCPlace, false);
            SetButtonBackground(btnSPick, false);
            SetButtonBackground(btnSPlace, false);
            SetButtonBackground(btnAligner, false);
        }

        private void SetButtonBackground(Button button, bool isActive)
        {
            if (button != null)
            {
                button.Background = isActive ? _activeBrush : _inactiveBrush;
            }
        }
        #endregion

        #region Cassette/Stage Management
        private void InitializeComboBoxForCurrentMenu()
        {
            if (cmbCassette == null || string.IsNullOrEmpty(_currentSelectedGroup) ||
                string.IsNullOrEmpty(_selectedGroupMenu) || _selectedGroupMenu == "Aligner" ||
                !_groupMenuSelectedNumbers.ContainsKey(_currentSelectedGroup)) return;

            EnsureGroupDataInitialized(_currentSelectedGroup);

            cmbCassette.Items.Clear();
            bool isCassetteMenu = IsCassetteMenu(_selectedGroupMenu);

            UpdateCassetteLabel(isCassetteMenu);
            PopulateCassetteComboBox(isCassetteMenu);
            SetSelectedCassetteItem();
        }

        private bool IsCassetteMenu(string menuType)
        {
            return menuType == "CPick" || menuType == "CPlace";
        }

        private void UpdateCassetteLabel(bool isCassetteMenu)
        {
            txtCassetteLabel.Text = isCassetteMenu ? "Cassette :" : "Stage :";
        }

        private void PopulateCassetteComboBox(bool isCassetteMenu)
        {
            int itemCount = isCassetteMenu
                ? (_groupCassetteCounts.ContainsKey(_currentSelectedGroup) ? _groupCassetteCounts[_currentSelectedGroup] : CASSETTE_COUNT_FIXED)
                : STAGE_COUNT_FIXED;

            for (int i = 1; i <= itemCount; i++)
            {
                cmbCassette.Items.Add(i);
            }
        }

        private void SetSelectedCassetteItem()
        {
            if (cmbCassette.Items.Count > 0)
            {
                if (!_groupMenuSelectedNumbers.ContainsKey(_currentSelectedGroup) ||
                    !_groupMenuSelectedNumbers[_currentSelectedGroup].ContainsKey(_selectedGroupMenu))
                {
                    EnsureGroupDataInitialized(_currentSelectedGroup);
                    if (!_groupMenuSelectedNumbers.ContainsKey(_currentSelectedGroup) ||
                        !_groupMenuSelectedNumbers[_currentSelectedGroup].ContainsKey(_selectedGroupMenu))
                    {
                        _groupMenuSelectedNumbers[_currentSelectedGroup][_selectedGroupMenu] = 1;
                    }
                }

                int previouslySelectedNumber = _groupMenuSelectedNumbers[_currentSelectedGroup][_selectedGroupMenu];

                if (cmbCassette.Items.Contains(previouslySelectedNumber))
                {
                    cmbCassette.SelectedItem = previouslySelectedNumber;
                }
                else if (cmbCassette.Items.Count > 0)
                {
                    cmbCassette.SelectedIndex = 0;
                    _groupMenuSelectedNumbers[_currentSelectedGroup][_selectedGroupMenu] = (int)cmbCassette.SelectedItem;
                }
            }
            UpdateCassetteStageDisplay();
        }

        private void CassetteComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            if (cmbCassette?.SelectedItem == null || string.IsNullOrEmpty(_currentSelectedGroup) || string.IsNullOrEmpty(_selectedGroupMenu)) return;

            if (!GlobalModeManager.IsEditingAllowed && e.RemovedItems.Count > 0)
            {
                cmbCassette.SelectedItem = e.RemovedItems[0];
                AlarmMessageManager.ShowAlarm(Alarms.OPERATION_LIMIT, "Change Cassette/Stage only in Manual mode.");
                return;
            }

            int newSelectedNumber = (int)e.AddedItems[0];
            int previousSelectedNumber = -1;
            if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is int prevNum)
            {
                previousSelectedNumber = prevNum;
            }
            else if (_groupMenuSelectedNumbers.ContainsKey(_currentSelectedGroup) && _groupMenuSelectedNumbers[_currentSelectedGroup].ContainsKey(_selectedGroupMenu))
            {
                previousSelectedNumber = _groupMenuSelectedNumbers[_currentSelectedGroup][_selectedGroupMenu];
            }

            if (newSelectedNumber == previousSelectedNumber && previousSelectedNumber != -1) return;

            if (IsStageMenu(_selectedGroupMenu))
            {
                string partnerMenu = _selectedGroupMenu == "SPick" ? "SPlace" : "SPick";
                int partnerStageNumber = _groupMenuSelectedNumbers[_currentSelectedGroup][partnerMenu];

                if (newSelectedNumber == partnerStageNumber)
                {
                    if (previousSelectedNumber != -1)
                    {
                        _groupMenuSelectedNumbers[_currentSelectedGroup][partnerMenu] = previousSelectedNumber;
                        AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                            $"{_currentSelectedGroup}: {partnerMenu} swapped to Stage {previousSelectedNumber} due to conflict.");
                    }
                }
            }

            _groupMenuSelectedNumbers[_currentSelectedGroup][_selectedGroupMenu] = newSelectedNumber;
            UpdateCassetteStageDisplay();

            string itemType = IsStageMenu(_selectedGroupMenu) ? "Stage" : "Cassette";
            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                $"{_currentSelectedGroup} - {_selectedGroupMenu} now targets {itemType} {newSelectedNumber}");

            SaveToPersistentStorage();
        }

        private bool ValidateCassetteSelection(SelectionChangedEventArgs e)
        {
            if (cmbCassette?.SelectedItem == null || string.IsNullOrEmpty(_currentSelectedGroup) ||
                string.IsNullOrEmpty(_selectedGroupMenu) ||
                e.AddedItems.Count == 0)
            {
                return false;
            }

            if (!GlobalModeManager.IsEditingAllowed && e.RemovedItems.Count > 0)
            {
                cmbCassette.SelectedItem = e.RemovedItems[0];
                AlarmMessageManager.ShowAlarm(Alarms.OPERATION_LIMIT, "Change Cassette/Stage only in Manual mode.");
                return false;
            }
            return true;
        }

        private void HandleStageSwapIfNeeded(int newSelectedNumber, int previousSelectedNumber)
        {
            bool isStageMenu = IsStageMenu(_selectedGroupMenu);
            if (isStageMenu && previousSelectedNumber > 0)
            {
                string partnerMenu = _selectedGroupMenu == "SPick" ? "SPlace" : "SPick";
                if (_groupMenuSelectedNumbers.ContainsKey(_currentSelectedGroup) &&
                    _groupMenuSelectedNumbers[_currentSelectedGroup].ContainsKey(partnerMenu))
                {
                    int partnerStageNumber = _groupMenuSelectedNumbers[_currentSelectedGroup][partnerMenu];

                    if (newSelectedNumber == partnerStageNumber)
                    {
                        _groupMenuSelectedNumbers[_currentSelectedGroup][partnerMenu] = previousSelectedNumber;
                        AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                            $"{_currentSelectedGroup}: {partnerMenu} swapped to Stage {previousSelectedNumber}.");
                    }
                }
            }
        }

        private bool IsStageMenu(string menuType)
        {
            return menuType == "SPick" || menuType == "SPlace";
        }

        private void UpdateCassetteStageDisplay()
        {
            if (!ValidateCassetteStageDisplay()) return;

            int selectedNumber = _groupMenuSelectedNumbers[_currentSelectedGroup][_selectedGroupMenu];
            bool isCassetteMenu = IsCassetteMenu(_selectedGroupMenu);

            UpdateCassetteStageInfo(selectedNumber, isCassetteMenu);

            UpdateFixedCoordinatesFromData();
            LoadCoordinatesForMenu(_selectedGroupMenu);
        }

        private bool ValidateCassetteStageDisplay()
        {
            return !string.IsNullOrEmpty(_currentSelectedGroup) &&
                   !string.IsNullOrEmpty(_selectedGroupMenu) && _selectedGroupMenu != "Aligner" &&
                   _groupMenuSelectedNumbers.ContainsKey(_currentSelectedGroup) &&
                   _groupMenuSelectedNumbers[_currentSelectedGroup].ContainsKey(_selectedGroupMenu);
        }

        private void UpdateCassetteStageInfo(int selectedNumber, bool isCassetteMenu)
        {
            if (isCassetteMenu)
            {
                UpdateCassetteInfo(selectedNumber);
            }
            else
            {
                UpdateStageInfo(selectedNumber);
            }
        }

        private void UpdateCassetteInfo(int selectedNumber)
        {
            SharedDataManager.CassetteStageData sharedData = null;
            try
            {
                sharedData = SharedDataManager.GetCassetteData(_currentSelectedGroup, selectedNumber);
                System.Diagnostics.Debug.WriteLine($"Movement UI: Successfully fetched Cassette {selectedNumber} for Group {_currentSelectedGroup} from SharedDataManager. A={sharedData?.PositionA}, T={sharedData?.PositionT}, Z={sharedData?.PositionZ}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement UI: Error getting CassetteData from SharedDataManager for {_currentSelectedGroup} - Cassette {selectedNumber}: {ex.Message}");
            }

            if (!_groupCassetteData.ContainsKey(_currentSelectedGroup))
                _groupCassetteData[_currentSelectedGroup] = new Dictionary<int, CassetteInfo>();
            if (!_groupCassetteData[_currentSelectedGroup].ContainsKey(selectedNumber))
                _groupCassetteData[_currentSelectedGroup][selectedNumber] = new CassetteInfo();

            var localCassetteInfo = _groupCassetteData[_currentSelectedGroup][selectedNumber];

            if (sharedData != null)
            {
                localCassetteInfo.PositionA = sharedData.PositionA;
                localCassetteInfo.PositionT = sharedData.PositionT;
                localCassetteInfo.PositionZ = sharedData.PositionZ;
                localCassetteInfo.SlotCount = sharedData.SlotCount;
                localCassetteInfo.Pitch = sharedData.Pitch;
                System.Diagnostics.Debug.WriteLine($"Movement UI: Updated local _groupCassetteData for Cassette {selectedNumber} using SharedDataManager data.");
            }

            SetCassetteStageDisplayValues(localCassetteInfo.SlotCount, localCassetteInfo.PositionA, localCassetteInfo.PositionT, localCassetteInfo.PositionZ);
        }

        private void UpdateStageInfo(int selectedNumber)
        {
            SharedDataManager.CassetteStageData sharedData = null;
            try
            {
                sharedData = SharedDataManager.GetStageData(_currentSelectedGroup, selectedNumber);
                System.Diagnostics.Debug.WriteLine($"Movement UI: Successfully fetched Stage {selectedNumber} for Group {_currentSelectedGroup} from SharedDataManager. A={sharedData?.PositionA}, T={sharedData?.PositionT}, Z={sharedData?.PositionZ}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement UI: Error getting StageData from SharedDataManager for {_currentSelectedGroup} - Stage {selectedNumber}: {ex.Message}");
            }

            if (!_groupStageData.ContainsKey(_currentSelectedGroup))
                _groupStageData[_currentSelectedGroup] = new Dictionary<int, StageInfo>();
            if (!_groupStageData[_currentSelectedGroup].ContainsKey(selectedNumber))
                _groupStageData[_currentSelectedGroup][selectedNumber] = new StageInfo();

            var localStageInfo = _groupStageData[_currentSelectedGroup][selectedNumber];

            if (sharedData != null)
            {
                localStageInfo.PositionA = sharedData.PositionA;
                localStageInfo.PositionT = sharedData.PositionT;
                localStageInfo.PositionZ = sharedData.PositionZ;
                localStageInfo.SlotCount = sharedData.SlotCount;
                localStageInfo.Pitch = sharedData.Pitch;
                System.Diagnostics.Debug.WriteLine($"Movement UI: Updated local _groupStageData for Stage {selectedNumber} using SharedDataManager data.");
            }
            SetCassetteStageDisplayValues(localStageInfo.SlotCount, localStageInfo.PositionA, localStageInfo.PositionT, localStageInfo.PositionZ);
        }

        private void SetCassetteStageDisplayValues(int slotCount, decimal posA, decimal posT, decimal posZ)
        {
            txtCassetteSlot.Text = slotCount.ToString();
            txtCassettePosition.Text = $"({posA:F2}, {posT:F2}, {posZ:F2})";
        }
        #endregion

        #region Coordinate Management
        private void UpdateFixedCoordinatesFromData()
        {
            if (!ValidateCoordinateUpdate()) return;

            int selectedItemNumber = _groupMenuSelectedNumbers[_currentSelectedGroup][_selectedGroupMenu];

            UpdateP3CoordinatesIfApplicable(selectedItemNumber);
            UpdateP5CoordinatesIfApplicable(selectedItemNumber);
        }

        private bool ValidateCoordinateUpdate()
        {
            return !string.IsNullOrEmpty(_currentSelectedGroup) &&
                   !string.IsNullOrEmpty(_selectedGroupMenu) && _selectedGroupMenu != "Aligner" &&
                   _groupMenuSelectedNumbers.ContainsKey(_currentSelectedGroup) &&
                   _groupMenuSelectedNumbers[_currentSelectedGroup].ContainsKey(_selectedGroupMenu);
        }

        private void UpdateP3CoordinatesIfApplicable(int selectedItemNumber)
        {
            if (_selectedGroupMenu == "CPick")
            {
                UpdateP3FromCassette(selectedItemNumber);
            }
            else if (_selectedGroupMenu == "SPlace")
            {
                UpdateP3FromStage(selectedItemNumber);
            }
        }

        private void UpdateP5CoordinatesIfApplicable(int selectedItemNumber)
        {
            if (_selectedGroupMenu == "CPlace")
            {
                UpdateP5FromCassette(selectedItemNumber);
            }
            else if (_selectedGroupMenu == "SPick")
            {
                UpdateP5FromStage(selectedItemNumber);
            }
        }
        private void UpdateCoordinateDataPoint(string pointField, decimal posA, decimal posT, decimal posZ)
        {
            if (_groupCoordinateData.ContainsKey(_currentSelectedGroup) &&
                _groupCoordinateData[_currentSelectedGroup].ContainsKey(_selectedGroupMenu))
            {
                var coordData = _groupCoordinateData[_currentSelectedGroup][_selectedGroupMenu];
                string[] targetArray = null;
                switch (pointField)
                {
                    case "P3": targetArray = coordData.P3; break;
                    case "P5": targetArray = coordData.P5; break;
                }
                if (targetArray != null)
                {
                    targetArray[0] = posA.ToString("F2");
                    targetArray[1] = posT.ToString("F2");
                    targetArray[2] = posZ.ToString("F2");
                }
            }
        }

        private void UpdateP3FromCassette(int selectedItemNumber)
        {
            if (_groupCassetteData.ContainsKey(_currentSelectedGroup) &&
                _groupCassetteData[_currentSelectedGroup].TryGetValue(selectedItemNumber, out CassetteInfo info))
            {
                UpdateCoordinateDataPoint("P3", info.PositionA, info.PositionT, info.PositionZ);
            }
        }

        private void UpdateP3FromStage(int selectedItemNumber)
        {
            if (_groupStageData.ContainsKey(_currentSelectedGroup) &&
                _groupStageData[_currentSelectedGroup].TryGetValue(selectedItemNumber, out StageInfo info))
            {
                UpdateCoordinateDataPoint("P3", info.PositionA, info.PositionT, info.PositionZ);
            }
        }

        private void UpdateP5FromCassette(int selectedItemNumber)
        {
            if (_groupCassetteData.ContainsKey(_currentSelectedGroup) &&
                _groupCassetteData[_currentSelectedGroup].TryGetValue(selectedItemNumber, out CassetteInfo info))
            {
                UpdateCoordinateDataPoint("P5", info.PositionA, info.PositionT, info.PositionZ);
            }
        }

        private void UpdateP5FromStage(int selectedItemNumber)
        {
            if (_groupStageData.ContainsKey(_currentSelectedGroup) &&
                _groupStageData[_currentSelectedGroup].TryGetValue(selectedItemNumber, out StageInfo info))
            {
                UpdateCoordinateDataPoint("P5", info.PositionA, info.PositionT, info.PositionZ);
            }
        }
        private void SetCoordinateTextBoxes(TextBox txtA, TextBox txtT, TextBox txtZ,
            decimal posA, decimal posT, decimal posZ)
        {
            if (txtA != null) txtA.Text = posA.ToString("F2");
            if (txtT != null) txtT.Text = posT.ToString("F2");
            if (txtZ != null) txtZ.Text = posZ.ToString("F2");
        }

        private void SetCoordinateTextBoxesEditability(bool isGenerallyEditable)
        {
            var editBrush = Brushes.White;
            var roBrush = new SolidColorBrush(Colors.LightGray);

            SetTextBoxEditability(txtP1A, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP1T, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP1Z, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP1Speed, false, roBrush, roBrush);

            SetTextBoxEditability(txtP2A, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP2T, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP2Z, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP2Speed, false, roBrush, roBrush);

            bool p3IsDerived = (_selectedGroupMenu == "CPick" || _selectedGroupMenu == "SPlace");
            SetTextBoxEditability(txtP3A, isGenerallyEditable && !p3IsDerived, editBrush, roBrush);
            SetTextBoxEditability(txtP3T, isGenerallyEditable && !p3IsDerived, editBrush, roBrush);
            SetTextBoxEditability(txtP3Z, isGenerallyEditable && !p3IsDerived, editBrush, roBrush);
            SetTextBoxEditability(txtP3Speed, false, roBrush, roBrush);

            SetTextBoxEditability(txtP4A, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP4T, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP4Z, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP4Speed, false, roBrush, roBrush);

            bool p5IsDerived = (_selectedGroupMenu == "CPlace" || _selectedGroupMenu == "SPick");
            SetTextBoxEditability(txtP5A, isGenerallyEditable && !p5IsDerived, editBrush, roBrush);
            SetTextBoxEditability(txtP5T, isGenerallyEditable && !p5IsDerived, editBrush, roBrush);
            SetTextBoxEditability(txtP5Z, isGenerallyEditable && !p5IsDerived, editBrush, roBrush);
            SetTextBoxEditability(txtP5Speed, false, roBrush, roBrush);

            SetTextBoxEditability(txtP6A, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP6T, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP6Z, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP6Speed, false, roBrush, roBrush);

            SetTextBoxEditability(txtP7A, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP7T, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP7Z, isGenerallyEditable, editBrush, roBrush);
            SetTextBoxEditability(txtP7Speed, false, roBrush, roBrush);
        }

        private void SetTextBoxEditability(TextBox textBox, bool editable, Brush editBrush, Brush readOnlyBrush)
        {
            if (textBox != null)
            {
                textBox.IsReadOnly = !editable;
                textBox.Background = editable ? editBrush : readOnlyBrush;
            }
        }

        private void SaveCurrentCoordinates()
        {
            if (!ValidateCoordinateSave()) return;

            var coordData = _groupCoordinateData[_currentSelectedGroup][_selectedGroupMenu];

            SaveCoordinateSet(new[] { txtP1A, txtP1T, txtP1Z, txtP1Speed }, coordData.P1);
            SaveCoordinateSet(new[] { txtP2A, txtP2T, txtP2Z, txtP2Speed }, coordData.P2);
            SaveCoordinateSet(new[] { txtP3A, txtP3T, txtP3Z, txtP3Speed }, coordData.P3);
            SaveCoordinateSet(new[] { txtP4A, txtP4T, txtP4Z, txtP4Speed }, coordData.P4);
            SaveCoordinateSet(new[] { txtP5A, txtP5T, txtP5Z, txtP5Speed }, coordData.P5);
            SaveCoordinateSet(new[] { txtP6A, txtP6T, txtP6Z, txtP6Speed }, coordData.P6);
            SaveCoordinateSet(new[] { txtP7A, txtP7T, txtP7Z, txtP7Speed }, coordData.P7);
            System.Diagnostics.Debug.WriteLine($"Coordinates saved to instance data for {_currentSelectedGroup} - {_selectedGroupMenu}");
        }

        private bool ValidateCoordinateSave()
        {
            return !string.IsNullOrEmpty(_currentSelectedGroup) &&
                   !string.IsNullOrEmpty(_selectedGroupMenu) && _selectedGroupMenu != "Aligner" &&
                   _groupCoordinateData.ContainsKey(_currentSelectedGroup) &&
                   _groupCoordinateData[_currentSelectedGroup].ContainsKey(_selectedGroupMenu);
        }

        private void SaveCoordinateSet(TextBox[] textBoxes, string[] coordinateArray)
        {
            for (int i = 0; i < 4 && i < textBoxes.Length; i++)
            {
                if (coordinateArray != null && i < coordinateArray.Length)
                {
                    coordinateArray[i] = textBoxes[i]?.Text ?? (i == 3 ? "100" : "0.00");
                }
            }
        }

        private void LoadCoordinatesForMenu(string menuType)
        {
            if (!ValidateCoordinateLoad(menuType))
            {
                ClearAllCoordinateTextBoxes();
                return;
            }

            EnsureGroupDataInitialized(_currentSelectedGroup);

            if (!_groupCoordinateData[_currentSelectedGroup].ContainsKey(menuType))
            {
                _groupCoordinateData[_currentSelectedGroup][menuType] = new CoordinateData();
            }

            var coordData = _groupCoordinateData[_currentSelectedGroup][menuType];

            LoadCoordinateSet(new[] { txtP1A, txtP1T, txtP1Z, txtP1Speed }, coordData.P1);
            LoadCoordinateSet(new[] { txtP2A, txtP2T, txtP2Z, txtP2Speed }, coordData.P2);
            LoadCoordinateSet(new[] { txtP3A, txtP3T, txtP3Z, txtP3Speed }, coordData.P3);
            LoadCoordinateSet(new[] { txtP4A, txtP4T, txtP4Z, txtP4Speed }, coordData.P4);
            LoadCoordinateSet(new[] { txtP5A, txtP5T, txtP5Z, txtP5Speed }, coordData.P5);
            LoadCoordinateSet(new[] { txtP6A, txtP6T, txtP6Z, txtP6Speed }, coordData.P6);
            LoadCoordinateSet(new[] { txtP7A, txtP7T, txtP7Z, txtP7Speed }, coordData.P7);

            System.Diagnostics.Debug.WriteLine($"Loaded coords for {_currentSelectedGroup} - {menuType}");
        }

        private void ClearAllCoordinateTextBoxes()
        {
            string[] defaultCoord = { "0.00", "0.00", "0.00", "100" };
            LoadCoordinateSet(new[] { txtP1A, txtP1T, txtP1Z, txtP1Speed }, defaultCoord);
            LoadCoordinateSet(new[] { txtP2A, txtP2T, txtP2Z, txtP2Speed }, defaultCoord);
            LoadCoordinateSet(new[] { txtP3A, txtP3T, txtP3Z, txtP3Speed }, defaultCoord);
            LoadCoordinateSet(new[] { txtP4A, txtP4T, txtP4Z, txtP4Speed }, defaultCoord);
            LoadCoordinateSet(new[] { txtP5A, txtP5T, txtP5Z, txtP5Speed }, defaultCoord);
            LoadCoordinateSet(new[] { txtP6A, txtP6T, txtP6Z, txtP6Speed }, defaultCoord);
            LoadCoordinateSet(new[] { txtP7A, txtP7T, txtP7Z, txtP7Speed }, defaultCoord);
        }

        private bool ValidateCoordinateLoad(string menuType)
        {
            if (string.IsNullOrEmpty(_currentSelectedGroup) ||
                string.IsNullOrEmpty(menuType) || menuType == "Aligner")
            {
                return false;
            }
            return _groupCoordinateData.ContainsKey(_currentSelectedGroup);
        }

        private void LoadCoordinateSet(TextBox[] textBoxes, string[] coordinateArray)
        {
            var defaultValues = new[] { "0.00", "0.00", "0.00", "100" };
            var valuesToLoad = coordinateArray;
            if (coordinateArray == null || coordinateArray.Length < 4)
            {
                valuesToLoad = defaultValues;
            }

            for (int i = 0; i < 4 && i < textBoxes.Length; i++)
            {
                if (textBoxes[i] != null)
                {
                    textBoxes[i].Text = (i < valuesToLoad.Length && valuesToLoad[i] != null) ? valuesToLoad[i] : defaultValues[i];
                }
            }
        }
        #endregion

        #region Save Functionality and Reverse Relationships
        private void SaveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateGroupSave()) return;

            try
            {
                SaveCurrentCoordinates();
                SaveToPersistentStorage();

                // 기존 알람 메시지 (변경 없음)
                AlarmMessageManager.ShowAlarm(Alarms.POSITION_SAVED,
                    $"{_currentSelectedGroup} - {_selectedGroupMenu} and related data saved. Will persist across sessions.");

                // 레시피 생성 시도 (실패해도 기존 저장 동작에 영향 없음)
                TryCreateRecipeFromSavedData();
            }
            catch (Exception ex)
            {
                // 기존 예외 처리 방식 그대로 유지
                AlarmMessageManager.ShowAlarm(Alarms.DATA_ERROR, $"Save error: {ex.Message}");
            }
        }

        /// <summary>
        /// 저장된 데이터를 기반으로 레시피 생성 시도
        /// 실패해도 Movement 저장에는 영향 없음
        /// </summary>
        private void TryCreateRecipeFromSavedData()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[Movement] 레시피 생성 시도 - 그룹: {_currentSelectedGroup}, 메뉴: {_selectedGroupMenu}");

                // 현재 그룹의 모든 메뉴 데이터 수집
                if (!_groupCoordinateData.ContainsKey(_currentSelectedGroup))
                {
                    System.Diagnostics.Debug.WriteLine("[Movement] 그룹 좌표 데이터가 없어 레시피 생성 건너뜀");
                    return;
                }

                var groupData = _groupCoordinateData[_currentSelectedGroup];
                var recipeData = CollectDataForRecipe(groupData);

                if (recipeData.ValidCoordinates.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[Movement] 유효한 좌표가 없어 레시피 생성 건너뜀");
                    return;
                }

                // 레시피 생성 및 저장
                var recipe = CreateRecipeFromMovementData(recipeData);
                bool success = SaveRecipeToFile(recipe);

                if (success)
                {
                    System.Diagnostics.Debug.WriteLine($"[Movement] 레시피 자동 생성 완료: {recipe.RecipeName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Movement] 레시피 생성 오류 (무시됨): {ex.Message}");
                // 레시피 생성 실패는 Movement 저장에 영향 주지 않음
            }
        }

        /// <summary>
        /// 레시피 생성용 데이터 클래스
        /// </summary>
        private class RecipeCreationData
        {
            public Dictionary<string, CoordinateData> ValidCoordinates { get; set; } = new Dictionary<string, CoordinateData>();
            public CassetteInfo CassetteInfo { get; set; }
            public StageInfo StageInfo { get; set; }
        }

        private bool ValidateGroupSave()
        {
            if (!GlobalModeManager.IsEditingAllowed)
            {
                AlarmMessageManager.ShowAlarm(Alarms.OPERATION_LIMIT, "Save only in Manual mode");
                return false;
            }

            if (string.IsNullOrEmpty(_currentSelectedGroup) || string.IsNullOrEmpty(_selectedGroupMenu) ||
                _selectedGroupMenu == "Aligner")
            {
                AlarmMessageManager.ShowAlarm(Alarms.UNEXPECTED_STATE, "Select a valid menu (CPick, CPlace, SPick, SPlace) to save coordinates.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Movement 그룹 데이터에서 레시피 생성용 데이터 수집
        /// 실제 _groupCoordinateData 구조 사용: [그룹][메뉴타입][CoordinateData]
        /// </summary>
        private RecipeCreationData CollectDataForRecipe(Dictionary<string, CoordinateData> groupData)
        {
            var recipeData = new RecipeCreationData();

            try
            {
                // CPick, CPlace, SPick, SPlace 메뉴별로 좌표 수집
                foreach (var menuData in groupData)
                {
                    string menuType = menuData.Key; // "CPick", "CPlace", "SPick", "SPlace"
                    CoordinateData coordData = menuData.Value;

                    // 유효한 좌표가 있는지 확인 (0이 아닌 값이 하나라도 있으면 유효)
                    if (IsValidCoordinateData(coordData))
                    {
                        recipeData.ValidCoordinates[menuType] = coordData;
                        System.Diagnostics.Debug.WriteLine($"[Movement] {menuType} 좌표 데이터 수집됨");
                    }
                }

                // 카세트/스테이지 정보 수집
                recipeData.CassetteInfo = GetCassetteInfoForCurrentGroup();
                recipeData.StageInfo = GetStageInfoForCurrentGroup();

                System.Diagnostics.Debug.WriteLine($"[Movement] 수집된 메뉴 수: {recipeData.ValidCoordinates.Count}개");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Movement] 데이터 수집 오류: {ex.Message}");
            }

            return recipeData;
        }

        /// <summary>
        /// CoordinateData가 유효한지 확인 (P1~P7 중 하나라도 0이 아닌 값이 있으면 유효)
        /// </summary>
        private bool IsValidCoordinateData(CoordinateData coordData)
        {
            if (coordData == null) return false;

            // 각 포인트를 개별적으로 확인
            return IsValidPoint(coordData.P1) || IsValidPoint(coordData.P2) ||
                   IsValidPoint(coordData.P3) || IsValidPoint(coordData.P4) ||
                   IsValidPoint(coordData.P5) || IsValidPoint(coordData.P6) ||
                   IsValidPoint(coordData.P7);
        }

        /// <summary>
        /// 현재 그룹의 카세트 정보 가져오기
        /// </summary>
        private CassetteInfo GetCassetteInfoForCurrentGroup()
        {
            try
            {
                if (_groupCassetteData.ContainsKey(_currentSelectedGroup))
                {
                    var cassetteDict = _groupCassetteData[_currentSelectedGroup];
                    if (cassetteDict.Count > 0)
                    {
                        // 첫 번째 카세트 정보 반환 (일반적으로 1번 카세트)
                        return cassetteDict.Values.First();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Movement] 카세트 정보 조회 오류: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 현재 그룹의 스테이지 정보 가져오기
        /// </summary>
        private StageInfo GetStageInfoForCurrentGroup()
        {
            try
            {
                if (_groupStageData.ContainsKey(_currentSelectedGroup))
                {
                    var stageDict = _groupStageData[_currentSelectedGroup];
                    if (stageDict.Count > 0)
                    {
                        // 첫 번째 스테이지 정보 반환 (일반적으로 1번 스테이지)
                        return stageDict.Values.First();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Movement] 스테이지 정보 조회 오류: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Movement 데이터를 기반으로 TransferRecipe 생성
        /// </summary>
        private TransferRecipe CreateRecipeFromMovementData(RecipeCreationData recipeData)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string recipeName = $"{_currentSelectedGroup}_Movement_{timestamp}";

            var recipe = new TransferRecipe(recipeName,
                $"{_currentSelectedGroup} 그룹 Movement 저장 시 자동 생성된 레시피");

            // 기본 시작 스텝들
            recipe.AddStep(new RecipeStep(StepType.Home, "시작 전 홈 위치 이동"));
            recipe.AddStep(new RecipeStep(StepType.CheckSafety, "안전 상태 확인"));

            // Movement 메뉴별 스텝 생성
            AddStepsFromMenuData(recipe, recipeData.ValidCoordinates);

            // 카세트/스테이지 관련 스텝 추가
            if (recipeData.CassetteInfo != null)
            {
                AddCassetteRelatedSteps(recipe, recipeData.CassetteInfo);
            }

            // 완료 스텝
            recipe.AddStep(new RecipeStep(StepType.Home, "작업 완료 후 홈 복귀"));

            System.Diagnostics.Debug.WriteLine($"[Movement] 레시피 생성 완료: {recipe.StepCount}개 스텝");
            return recipe;
        }

        /// <summary>
        /// Movement 메뉴 데이터를 기반으로 작업 스텝 생성
        /// CPick → CPlace, SPick → SPlace 패턴으로 생성
        /// </summary>
        private void AddStepsFromMenuData(TransferRecipe recipe, Dictionary<string, CoordinateData> menuData)
        {
            try
            {
                // CPick → CPlace 패턴
                if (menuData.ContainsKey("CPick") && menuData.ContainsKey("CPlace"))
                {
                    AddPickPlaceStepsFromCoordinates(recipe, "CPick", "CPlace",
                        menuData["CPick"], menuData["CPlace"], "카세트");
                }

                // SPick → SPlace 패턴  
                if (menuData.ContainsKey("SPick") && menuData.ContainsKey("SPlace"))
                {
                    AddPickPlaceStepsFromCoordinates(recipe, "SPick", "SPlace",
                        menuData["SPick"], menuData["SPlace"], "스테이지");
                }

                // 단일 메뉴만 있는 경우 이동 스텝만 추가
                foreach (var menu in menuData)
                {
                    if (!IsPartOfPickPlacePair(menu.Key))
                    {
                        AddSingleMoveSteps(recipe, menu.Key, menu.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Movement] 메뉴 스텝 생성 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// Pick-Place 쌍으로 작업 스텝 생성
        /// </summary>
        private void AddPickPlaceStepsFromCoordinates(TransferRecipe recipe, string pickMenu, string placeMenu,
            CoordinateData pickData, CoordinateData placeData, string workType)
        {
            var pickPoints = GetValidPointsFromCoordinateData(pickData);
            var placePoints = GetValidPointsFromCoordinateData(placeData);

            // 유효한 포인트들을 매칭하여 Pick-Place 스텝 생성
            int maxPairs = Math.Min(pickPoints.Count, placePoints.Count);

            for (int i = 0; i < maxPairs; i++)
            {
                string pickPoint = pickPoints[i];
                string placePoint = placePoints[i];

                // Pick 스텝
                var pickStep = new RecipeStep(StepType.Pick, $"{workType} {pickPoint}에서 웨이퍼 픽업");
                pickStep.TeachingGroupName = _currentSelectedGroup;
                pickStep.TeachingLocationName = $"{pickMenu}_{pickPoint}";
                recipe.AddStep(pickStep);

                // Place 스텝
                var placeStep = new RecipeStep(StepType.Place, $"{workType} {placePoint}에 웨이퍼 배치");
                placeStep.TeachingGroupName = _currentSelectedGroup;
                placeStep.TeachingLocationName = $"{placeMenu}_{placePoint}";
                recipe.AddStep(placeStep);

                // 안전 대기
                recipe.AddStep(new RecipeStep(StepType.Wait, $"{pickPoint}→{placePoint} 완료 후 대기")
                {
                    WaitTimeMs = 300
                });
            }

            System.Diagnostics.Debug.WriteLine($"[Movement] {workType} {pickMenu}→{placeMenu}: {maxPairs}개 쌍 생성");
        }

        /// <summary>
        /// CoordinateData에서 유효한 포인트 목록 추출 (P1~P7)
        /// </summary>
        private List<string> GetValidPointsFromCoordinateData(CoordinateData coordData)
        {
            var validPoints = new List<string>();

            // C# 6.0 호환 방식으로 각 포인트 개별 확인
            if (IsValidPoint(coordData.P1)) validPoints.Add("P1");
            if (IsValidPoint(coordData.P2)) validPoints.Add("P2");
            if (IsValidPoint(coordData.P3)) validPoints.Add("P3");
            if (IsValidPoint(coordData.P4)) validPoints.Add("P4");
            if (IsValidPoint(coordData.P5)) validPoints.Add("P5");
            if (IsValidPoint(coordData.P6)) validPoints.Add("P6");
            if (IsValidPoint(coordData.P7)) validPoints.Add("P7");

            return validPoints;
        }

        /// <summary>
        /// 개별 포인트 좌표가 유효한지 확인
        /// </summary>
        private bool IsValidPoint(string[] pointCoords)
        {
            if (pointCoords == null || pointCoords.Length < 3) return false;

            // R, T, Z 좌표 중 하나라도 0이 아니면 유효
            for (int i = 0; i < 3; i++)
            {
                if (decimal.TryParse(pointCoords[i], out decimal value) && value != 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Pick-Place 쌍에 속하는 메뉴인지 확인
        /// </summary>
        private bool IsPartOfPickPlacePair(string menuType)
        {
            return menuType == "CPick" || menuType == "CPlace" || menuType == "SPick" || menuType == "SPlace";
        }

        /// <summary>
        /// 단일 메뉴의 이동 스텝 추가
        /// </summary>
        private void AddSingleMoveSteps(TransferRecipe recipe, string menuType, CoordinateData coordData)
        {
            var validPoints = GetValidPointsFromCoordinateData(coordData);

            foreach (string point in validPoints)
            {
                var moveStep = new RecipeStep(StepType.Move, $"{menuType} {point} 위치로 이동");
                moveStep.TeachingGroupName = _currentSelectedGroup;
                moveStep.TeachingLocationName = $"{menuType}_{point}";
                recipe.AddStep(moveStep);
            }

            System.Diagnostics.Debug.WriteLine($"[Movement] {menuType} 단일 이동: {validPoints.Count}개 포인트");
        }

        /// <summary>
        /// 카세트 정보 기반 스텝 추가
        /// </summary>
        private void AddCassetteRelatedSteps(TransferRecipe recipe, CassetteInfo cassetteInfo)
        {
            try
            {
                if (cassetteInfo.SlotCount > 1)
                {
                    recipe.AddStep(new RecipeStep(StepType.CheckSafety,
                        $"카세트 매핑 확인 (슬롯 수: {cassetteInfo.SlotCount})"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Movement] 카세트 스텝 생성 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 레시피를 파일로 저장 (프로젝트 규칙에 맞는 경로)
        /// </summary>
        private bool SaveRecipeToFile(TransferRecipe recipe)
        {
            try
            {
                // Documents 폴더에 저장 (기존 다른 파일들과 동일한 패턴)
                string recipesFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "TeachingPendant_Recipes");

                if (!System.IO.Directory.Exists(recipesFolder))
                {
                    System.IO.Directory.CreateDirectory(recipesFolder);
                }

                // 확장자는 기존 규칙 따름
                string fileName = $"{recipe.RecipeName}.recipe.json";
                string filePath = System.IO.Path.Combine(recipesFolder, fileName);

                // JSON 직렬화 (Newtonsoft.Json 사용, 프로젝트에서 이미 사용 중)
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(recipe, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);

                System.Diagnostics.Debug.WriteLine($"[Movement] 레시피 저장 완료: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Movement] 레시피 저장 실패: {ex.Message}");
                return false;
            }
        }

        private void SaveAllGroupData()
        {
            System.Diagnostics.Debug.WriteLine($"Logging all data for group: {_currentSelectedGroup}");
        }
        #endregion

        #region Teaching UI Integration

        private void UpdateDataFromTeaching(TeachingPendant.TeachingUI.CoordinateUpdateEventArgs e)
        {
            EnsureGroupDataInitialized(e.GroupName);

            if (e.IsCassetteMode)
            {
                if (!_groupCassetteData[e.GroupName].ContainsKey(e.CassetteStageNumber))
                    _groupCassetteData[e.GroupName][e.CassetteStageNumber] = new CassetteInfo();

                var cassetteInfo = _groupCassetteData[e.GroupName][e.CassetteStageNumber];
                cassetteInfo.PositionA = e.PositionA;
                cassetteInfo.PositionT = e.PositionT;
                cassetteInfo.PositionZ = e.PositionZ;
                cassetteInfo.SlotCount = e.SlotCount;
                cassetteInfo.Pitch = e.Pitch;
            }
            else
            {
                if (!_groupStageData[e.GroupName].ContainsKey(e.CassetteStageNumber))
                    _groupStageData[e.GroupName][e.CassetteStageNumber] = new StageInfo();

                var stageInfo = _groupStageData[e.GroupName][e.CassetteStageNumber];
                stageInfo.PositionA = e.PositionA;
                stageInfo.PositionT = e.PositionT;
                stageInfo.PositionZ = e.PositionZ;
                stageInfo.SlotCount = e.SlotCount;
                stageInfo.Pitch = e.Pitch;
            }
        }

        public void ForceUpdateP3FromCurrentCassette()
        {
            if (string.IsNullOrEmpty(_currentSelectedGroup) || string.IsNullOrEmpty(_selectedGroupMenu))
            {
                System.Diagnostics.Debug.WriteLine("ForceUpdateP3: Group 또는 Menu가 선택되지 않음");
                return;
            }

            if (_selectedGroupMenu != "CPick")
            {
                System.Diagnostics.Debug.WriteLine($"ForceUpdateP3: 현재 메뉴가 CPick이 아님 (현재: {_selectedGroupMenu})");
                return;
            }

            if (!_groupMenuSelectedNumbers.ContainsKey(_currentSelectedGroup) ||
                !_groupMenuSelectedNumbers[_currentSelectedGroup].ContainsKey(_selectedGroupMenu))
            {
                System.Diagnostics.Debug.WriteLine("ForceUpdateP3: 선택된 Cassette 번호를 찾을 수 없음");
                return;
            }

            int selectedCassetteNumber = _groupMenuSelectedNumbers[_currentSelectedGroup][_selectedGroupMenu];
            System.Diagnostics.Debug.WriteLine($"ForceUpdateP3: Cassette {selectedCassetteNumber} 데이터로 P3 업데이트 시도");

            if (!_groupCassetteData.ContainsKey(_currentSelectedGroup) ||
                !_groupCassetteData[_currentSelectedGroup].ContainsKey(selectedCassetteNumber))
            {
                System.Diagnostics.Debug.WriteLine($"ForceUpdateP3: Cassette {selectedCassetteNumber} 데이터가 없음");
                return;
            }

            var cassetteInfo = _groupCassetteData[_currentSelectedGroup][selectedCassetteNumber];
            System.Diagnostics.Debug.WriteLine($"ForceUpdateP3: Cassette 데이터 - A={cassetteInfo.PositionA}, T={cassetteInfo.PositionT}, Z={cassetteInfo.PositionZ}");

            UpdateCoordinateDataPoint("P3", cassetteInfo.PositionA, cassetteInfo.PositionT, cassetteInfo.PositionZ);

            LoadCoordinatesForMenu(_selectedGroupMenu);

            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                $"P3 강제 업데이트 완료: Cassette {selectedCassetteNumber} -> ({cassetteInfo.PositionA}, {cassetteInfo.PositionT}, {cassetteInfo.PositionZ})");
        }

        private void UpdateUIIfMatching(TeachingPendant.TeachingUI.CoordinateUpdateEventArgs e)
        {
            if (ShouldUpdateUI(e))
            {
                UpdateCassetteStageDisplay();
            }
        }

        private bool ShouldUpdateUI(TeachingPendant.TeachingUI.CoordinateUpdateEventArgs e)
        {
            return _isGroupDetailMode &&
                   e.GroupName == _currentSelectedGroup &&
                   !string.IsNullOrEmpty(_selectedGroupMenu) && _selectedGroupMenu != "Aligner" &&
                   _groupMenuSelectedNumbers.ContainsKey(_currentSelectedGroup) &&
                   _groupMenuSelectedNumbers[_currentSelectedGroup].ContainsKey(_selectedGroupMenu) &&
                   _groupMenuSelectedNumbers[_currentSelectedGroup][_selectedGroupMenu] == e.CassetteStageNumber &&
                   IsCassetteMenu(_selectedGroupMenu) == e.IsCassetteMode;
        }

        private void Teaching_DataCountUpdated(object sender, TeachingPendant.TeachingUI.Teaching.DataCountUpdateEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentSelectedGroup))
            {
                _groupCassetteCounts[_currentSelectedGroup] = e.CassetteCount;

                if (_isGroupDetailMode && !string.IsNullOrEmpty(_selectedGroupMenu) && _selectedGroupMenu != "Aligner")
                {
                    InitializeComboBoxForCurrentMenu();
                }
                SaveToPersistentStorage();

                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE,
                    $"Movement UI: Data counts updated for group {_currentSelectedGroup} - Cassettes: {e.CassetteCount}. UI may refresh.");
            }
            else
            {
                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE,
                   $"Movement UI: Data counts received (C:{e.CassetteCount}, S:{e.StageCount}), but no group selected in Movement UI to apply changes.");
            }
        }
        #endregion

        #region External Interface Methods
        public void GenerateRandomAValues()
        {
            if (CoordinateScrollViewer.Visibility == Visibility.Visible && GlobalModeManager.IsEditingAllowed)
            {
                var random = new Random();
                var textBoxes = new[] { txtP1A, txtP2A, txtP3A, txtP4A, txtP5A, txtP6A, txtP7A };

                foreach (var textBox in textBoxes)
                {
                    if (textBox != null && !textBox.IsReadOnly)
                    {
                        textBox.Text = ((random.NextDouble() * 200) - 100).ToString("F2");
                    }
                }
                SaveCurrentCoordinates();
                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                    $"Random A values generated and saved for {_currentSelectedGroup} {_selectedGroupMenu}");
            }
            else
            {
                AlarmMessageManager.ShowAlarm(Alarms.UNEXPECTED_STATE,
                    "Select menu and be in Manual mode for random values.");
            }
        }

        public void UpdatePickP3Coordinates(string pickPosition, decimal positionA, decimal positionT, decimal positionZ)
        {
            if (!ValidatePickCoordinateUpdate()) return;

            int selectedItemNumber = _groupMenuSelectedNumbers[_currentSelectedGroup][_selectedGroupMenu];
            bool dataUpdated = false;
            if (_selectedGroupMenu == "CPick")
            {
                dataUpdated = UpdateCassetteCoordinates(selectedItemNumber, positionA, positionT, positionZ);
                if (dataUpdated) UpdateP3FromCassette(selectedItemNumber);
            }
            else if (_selectedGroupMenu == "SPick")
            {
                dataUpdated = UpdateStageCoordinates(selectedItemNumber, positionA, positionT, positionZ);
                if (dataUpdated) UpdateP5FromStage(selectedItemNumber);
            }

            if (dataUpdated)
            {
                if (_isGroupDetailMode && CoordinateScrollViewer.Visibility == Visibility.Visible)
                {
                    LoadCoordinatesForMenu(_selectedGroupMenu);
                }
                SaveToPersistentStorage();
                AlarmMessageManager.ShowAlarm(Alarms.POSITION_LOADED,
                    $"{_currentSelectedGroup} - {_selectedGroupMenu} item {selectedItemNumber} coordinates updated by helper.");
            }
            else
            {
                AlarmMessageManager.ShowAlarm(Alarms.UNEXPECTED_STATE,
                    $"Helper update failed: Menu is not applicable or data for item {selectedItemNumber} not found.");
            }
        }

        private void GlobalSpeedManager_SpeedChanged(object sender, int newSpeed)
        {
            if (_isGroupDetailMode && CoordinateScrollViewer.Visibility == Visibility.Visible &&
                !string.IsNullOrEmpty(_selectedGroupMenu) && _selectedGroupMenu != "Aligner")
            {
                RefreshDisplayedSegmentSpeeds();
            }
        }
        private void RefreshDisplayedSegmentSpeeds()
        {
            if (string.IsNullOrEmpty(_currentSelectedGroup) ||
                string.IsNullOrEmpty(_selectedGroupMenu) ||
                _selectedGroupMenu == "Aligner" ||
                !_groupCoordinateData.ContainsKey(_currentSelectedGroup) ||
                !_groupCoordinateData[_currentSelectedGroup].ContainsKey(_selectedGroupMenu))
            {
                ClearSpeedTextBoxes();
                return;
            }

            CoordinateData currentCoordData = _groupCoordinateData[_currentSelectedGroup][_selectedGroupMenu];
            var allPoints = new[] { currentCoordData.P1, currentCoordData.P2, currentCoordData.P3, currentCoordData.P4, currentCoordData.P5, currentCoordData.P6, currentCoordData.P7 };
            var speedTextBoxes = new[] { txtP1Speed, txtP2Speed, txtP3Speed, txtP4Speed, txtP5Speed, txtP6Speed, txtP7Speed };

            for (int i = 0; i < allPoints.Length - 1; i++)
            {
                if (allPoints[i] == null || allPoints[i + 1] == null) continue;

                int finalSpeed = CalculateFinalCommandSpeedForSegment(allPoints[i], allPoints[i + 1]);
                if (speedTextBoxes[i] != null)
                {
                    speedTextBoxes[i].Text = finalSpeed.ToString();
                }
                if (allPoints[i] != null && allPoints[i].Length > 3)
                {
                    allPoints[i][3] = finalSpeed.ToString();
                }
            }
            if (allPoints.Length > 1 && speedTextBoxes.Length > 0 && allPoints[allPoints.Length - 2] != null && allPoints[allPoints.Length - 2].Length > 3)
            {
                if (speedTextBoxes[allPoints.Length - 1] != null)
                {
                    if (speedTextBoxes[allPoints.Length - 2] != null)
                    {
                        speedTextBoxes[allPoints.Length - 1].Text = speedTextBoxes[allPoints.Length - 2].Text;
                    }
                    else
                    {
                        speedTextBoxes[allPoints.Length - 1].Text = "0";
                    }
                }
                if (allPoints[allPoints.Length - 1] != null && allPoints[allPoints.Length - 1].Length > 3)
                {
                    if (allPoints[allPoints.Length - 2] != null && allPoints[allPoints.Length - 2].Length > 3)
                    {
                        allPoints[allPoints.Length - 1][3] = allPoints[allPoints.Length - 2][3];
                    }
                    else
                    {
                        allPoints[allPoints.Length - 1][3] = "0";
                    }
                }
            }
            else if (speedTextBoxes.Length > 0 && speedTextBoxes[allPoints.Length - 1] != null)
            {
                speedTextBoxes[allPoints.Length - 1].Text = "N/A";
                if (allPoints[allPoints.Length - 1] != null && allPoints[allPoints.Length - 1].Length > 3)
                    allPoints[allPoints.Length - 1][3] = "0";
            }

            System.Diagnostics.Debug.WriteLine($"Displayed segment speeds refreshed for {_currentSelectedGroup} - {_selectedGroupMenu}");
        }

        private void ClearSpeedTextBoxes()
        {
            var speedTextBoxes = new[] { txtP1Speed, txtP2Speed, txtP3Speed, txtP4Speed, txtP5Speed, txtP6Speed, txtP7Speed };
            foreach (var textBox in speedTextBoxes)
            {
                if (textBox != null) textBox.Text = "0";
            }
        }

        private bool ValidatePickCoordinateUpdate()
        {
            if (string.IsNullOrEmpty(_currentSelectedGroup) || string.IsNullOrEmpty(_selectedGroupMenu) || _selectedGroupMenu == "Aligner")
            {
                AlarmMessageManager.ShowAlarm(Alarms.UNEXPECTED_STATE,
                    "No group/menu selected, or Aligner selected, to update coordinates via helper.");
                return false;
            }

            EnsureGroupDataInitialized(_currentSelectedGroup);

            if (!_groupMenuSelectedNumbers.ContainsKey(_currentSelectedGroup) ||
                !_groupMenuSelectedNumbers[_currentSelectedGroup].ContainsKey(_selectedGroupMenu))
            {
                AlarmMessageManager.ShowAlarm(Alarms.UNEXPECTED_STATE,
                    "Cannot determine selected item number for coordinate update.");
                return false;
            }

            return true;
        }

        private bool UpdateCassetteCoordinates(int selectedItemNumber, decimal positionA, decimal positionT, decimal positionZ)
        {
            if (_groupCassetteData.ContainsKey(_currentSelectedGroup) &&
                _groupCassetteData[_currentSelectedGroup].ContainsKey(selectedItemNumber))
            {
                var cassette = _groupCassetteData[_currentSelectedGroup][selectedItemNumber];
                cassette.PositionA = positionA;
                cassette.PositionT = positionT;
                cassette.PositionZ = positionZ;
                return true;
            }
            return false;
        }

        private bool UpdateStageCoordinates(int selectedItemNumber, decimal positionA, decimal positionT, decimal positionZ)
        {
            if (_groupStageData.ContainsKey(_currentSelectedGroup) &&
                _groupStageData[_currentSelectedGroup].ContainsKey(selectedItemNumber))
            {
                var stage = _groupStageData[_currentSelectedGroup][selectedItemNumber];
                stage.PositionA = positionA;
                stage.PositionT = positionT;
                stage.PositionZ = positionZ;
                return true;
            }
            return false;
        }
        #endregion

        #region Static Methods for Remote Control
        /// <summary>
        /// Remote Control에서 특정 그룹/메뉴의 좌표 데이터 가져오기
        /// </summary>
        public static CoordinateData GetCoordinateDataForRemote(string groupName, string menuType)
        {
            if (_persistentGroupCoordinateData.ContainsKey(groupName) &&
                _persistentGroupCoordinateData[groupName].ContainsKey(menuType))
            {
                return _persistentGroupCoordinateData[groupName][menuType];
            }

            // 데이터가 없으면 기본값 반환
            return new CoordinateData();
        }

        /// <summary>
        /// Remote Control에서 현재 선택된 그룹명 가져오기
        /// </summary>
        public static string GetCurrentSelectedGroupForRemote()
        {
            return _persistentCurrentSelectedGroup ?? "Group1";
        }

        /// <summary>
        /// Remote Control에서 특정 포인트의 좌표 가져오기
        /// </summary>
        public static string[] GetPointCoordinatesForRemote(string groupName, string menuType, int pointIndex)
        {
            var coordData = GetCoordinateDataForRemote(groupName, menuType);

            switch (pointIndex)
            {
                case 1: return coordData.P1;
                case 2: return coordData.P2;
                case 3: return coordData.P3;
                case 4: return coordData.P4;
                case 5: return coordData.P5;
                case 6: return coordData.P6;
                case 7: return coordData.P7;
                default: return new string[] { "0.00", "0.00", "0.00", "100" };
            }
        }
        #endregion

        #region Data Persistence Integration
        /// <summary>
        /// PersistentDataManager에서 데이터를 로드하여 Movement UI에 적용
        /// </summary>
        public static void LoadFromPersistentData(PersistentDataManager.MovementDataContainer data)
        {
            if (data == null)
            {
                System.Diagnostics.Debug.WriteLine("Movement: No persistent data to load, using defaults");
                return;
            }

            try
            {
                // 좌표 데이터 로드
                if (data.GroupCoordinateData != null)
                {
                    _persistentGroupCoordinateData.Clear();
                    foreach (var group in data.GroupCoordinateData)
                    {
                        _persistentGroupCoordinateData[group.Key] = new Dictionary<string, CoordinateData>();
                        foreach (var menu in group.Value)
                        {
                            _persistentGroupCoordinateData[group.Key][menu.Key] = new CoordinateData
                            {
                                P1 = menu.Value.P1 ?? new string[] { "0.00", "0.00", "0.00", "100" },
                                P2 = menu.Value.P2 ?? new string[] { "0.00", "0.00", "0.00", "100" },
                                P3 = menu.Value.P3 ?? new string[] { "0.00", "0.00", "0.00", "100" },
                                P4 = menu.Value.P4 ?? new string[] { "0.00", "0.00", "0.00", "100" },
                                P5 = menu.Value.P5 ?? new string[] { "0.00", "0.00", "0.00", "100" },
                                P6 = menu.Value.P6 ?? new string[] { "0.00", "0.00", "0.00", "100" },
                                P7 = menu.Value.P7 ?? new string[] { "0.00", "0.00", "0.00", "100" }
                            };
                        }
                    }
                }

                // 메뉴 선택 번호 로드
                if (data.GroupMenuSelectedNumbers != null)
                {
                    _persistentGroupMenuSelectedNumbers.Clear();
                    foreach (var group in data.GroupMenuSelectedNumbers)
                    {
                        _persistentGroupMenuSelectedNumbers[group.Key] = new Dictionary<string, int>(group.Value);
                    }
                }

                // 카세트 데이터 로드
                if (data.GroupCassetteData != null)
                {
                    _persistentGroupCassetteData.Clear();
                    foreach (var group in data.GroupCassetteData)
                    {
                        _persistentGroupCassetteData[group.Key] = new Dictionary<int, CassetteInfo>();
                        foreach (var cassette in group.Value)
                        {
                            _persistentGroupCassetteData[group.Key][cassette.Key] = new CassetteInfo
                            {
                                SlotCount = cassette.Value.SlotCount,
                                PositionA = cassette.Value.PositionA,
                                PositionT = cassette.Value.PositionT,
                                PositionZ = cassette.Value.PositionZ,
                                Pitch = cassette.Value.Pitch,
                                PickOffset = cassette.Value.PickOffset,
                                PickDown = cassette.Value.PickDown,
                                PickUp = cassette.Value.PickUp,
                                PlaceDown = cassette.Value.PlaceDown,
                                PlaceUp = cassette.Value.PlaceUp
                            };
                        }
                    }
                }

                // 스테이지 데이터 로드
                if (data.GroupStageData != null)
                {
                    _persistentGroupStageData.Clear();
                    foreach (var group in data.GroupStageData)
                    {
                        _persistentGroupStageData[group.Key] = new Dictionary<int, StageInfo>();
                        foreach (var stage in group.Value)
                        {
                            _persistentGroupStageData[group.Key][stage.Key] = new StageInfo
                            {
                                SlotCount = stage.Value.SlotCount,
                                PositionA = stage.Value.PositionA,
                                PositionT = stage.Value.PositionT,
                                PositionZ = stage.Value.PositionZ,
                                Pitch = stage.Value.Pitch,
                                PickOffset = stage.Value.PickOffset,
                                PickDown = stage.Value.PickDown,
                                PickUp = stage.Value.PickUp,
                                PlaceDown = stage.Value.PlaceDown,
                                PlaceUp = stage.Value.PlaceUp
                            };
                        }
                    }
                }

                // 카세트 개수 로드
                if (data.GroupCassetteCounts != null)
                {
                    _persistentGroupCassetteCounts.Clear();
                    foreach (var count in data.GroupCassetteCounts)
                    {
                        _persistentGroupCassetteCounts[count.Key] = count.Value;
                    }
                }

                // 현재 상태 로드
                if (!string.IsNullOrEmpty(data.CurrentSelectedGroup))
                {
                    _persistentCurrentSelectedGroup = data.CurrentSelectedGroup;
                }

                _persistentIsGroupDetailMode = data.IsGroupDetailMode;

                if (!string.IsNullOrEmpty(data.SelectedGroupMenu))
                {
                    _persistentSelectedGroupMenu = data.SelectedGroupMenu;
                }

                System.Diagnostics.Debug.WriteLine("Movement: Persistent data loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement: Error loading persistent data: {ex.Message}");
            }
        }

        /// <summary>
        /// Movement UI의 현재 데이터를 PersistentDataManager용 형태로 변환
        /// </summary>
        public static PersistentDataManager.MovementDataContainer GetPersistentData()
        {
            try
            {
                var data = new PersistentDataManager.MovementDataContainer();

                // 좌표 데이터 변환
                data.GroupCoordinateData = new Dictionary<string, Dictionary<string, PersistentDataManager.CoordinateDataJson>>();
                foreach (var group in _persistentGroupCoordinateData)
                {
                    data.GroupCoordinateData[group.Key] = new Dictionary<string, PersistentDataManager.CoordinateDataJson>();
                    foreach (var menu in group.Value)
                    {
                        data.GroupCoordinateData[group.Key][menu.Key] = new PersistentDataManager.CoordinateDataJson
                        {
                            P1 = menu.Value.P1,
                            P2 = menu.Value.P2,
                            P3 = menu.Value.P3,
                            P4 = menu.Value.P4,
                            P5 = menu.Value.P5,
                            P6 = menu.Value.P6,
                            P7 = menu.Value.P7
                        };
                    }
                }

                // 메뉴 선택 번호 변환
                data.GroupMenuSelectedNumbers = new Dictionary<string, Dictionary<string, int>>();
                foreach (var group in _persistentGroupMenuSelectedNumbers)
                {
                    data.GroupMenuSelectedNumbers[group.Key] = new Dictionary<string, int>(group.Value);
                }

                // 카세트 데이터 변환
                data.GroupCassetteData = new Dictionary<string, Dictionary<int, PersistentDataManager.CassetteInfoJson>>();
                foreach (var group in _persistentGroupCassetteData)
                {
                    data.GroupCassetteData[group.Key] = new Dictionary<int, PersistentDataManager.CassetteInfoJson>();
                    foreach (var cassette in group.Value)
                    {
                        data.GroupCassetteData[group.Key][cassette.Key] = new PersistentDataManager.CassetteInfoJson
                        {
                            SlotCount = cassette.Value.SlotCount,
                            PositionA = cassette.Value.PositionA,
                            PositionT = cassette.Value.PositionT,
                            PositionZ = cassette.Value.PositionZ,
                            Pitch = cassette.Value.Pitch,
                            PickOffset = cassette.Value.PickOffset,
                            PickDown = cassette.Value.PickDown,
                            PickUp = cassette.Value.PickUp,
                            PlaceDown = cassette.Value.PlaceDown,
                            PlaceUp = cassette.Value.PlaceUp
                        };
                    }
                }

                // 스테이지 데이터 변환
                data.GroupStageData = new Dictionary<string, Dictionary<int, PersistentDataManager.StageInfoJson>>();
                foreach (var group in _persistentGroupStageData)
                {
                    data.GroupStageData[group.Key] = new Dictionary<int, PersistentDataManager.StageInfoJson>();
                    foreach (var stage in group.Value)
                    {
                        data.GroupStageData[group.Key][stage.Key] = new PersistentDataManager.StageInfoJson
                        {
                            SlotCount = stage.Value.SlotCount,
                            PositionA = stage.Value.PositionA,
                            PositionT = stage.Value.PositionT,
                            PositionZ = stage.Value.PositionZ,
                            Pitch = stage.Value.Pitch,
                            PickOffset = stage.Value.PickOffset,
                            PickDown = stage.Value.PickDown,
                            PickUp = stage.Value.PickUp,
                            PlaceDown = stage.Value.PlaceDown,
                            PlaceUp = stage.Value.PlaceUp
                        };
                    }
                }

                // 카세트 개수 변환
                data.GroupCassetteCounts = new Dictionary<string, int>(_persistentGroupCassetteCounts);

                // 현재 상태 저장
                data.CurrentSelectedGroup = _persistentCurrentSelectedGroup;
                data.IsGroupDetailMode = _persistentIsGroupDetailMode;
                data.SelectedGroupMenu = _persistentSelectedGroupMenu;

                System.Diagnostics.Debug.WriteLine("Movement: Persistent data prepared for saving");
                return data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement: Error preparing persistent data: {ex.Message}");
                return new PersistentDataManager.MovementDataContainer();
            }
        }

        /// <summary>
        /// 실시간 자동 저장 트리거
        /// </summary>
        private static async void TriggerAutoSave()
        {
            try
            {
                await PersistentDataManager.AutoSaveAsync(PersistentDataManager.DataType.Movement);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement: Auto-save error: {ex.Message}");
            }
        }
        #endregion

        private int CalculateFinalCommandSpeedForSegment(string[] startPointData, string[] endPointData)
        {
            try
            {
                double acceleration = Setup.Acceleration;
                double deceleration = Setup.Deceleration;

                double travelDistanceL = MovementDataHelper.CalculateDistanceBetweenATZPoints(startPointData, endPointData);

                if (travelDistanceL < 0)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.DATA_ERROR, "Failed to calculate travel distance for speed command.");
                    return 0;
                }
                double maxSpeedC = MovementDataHelper.CalculateMaximumSpeedC(acceleration, deceleration, travelDistanceL);

                if (maxSpeedC <= 0 && travelDistanceL > 0)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.WARNING, $"Calculated theoretical max speed (c) is {maxSpeedC:F2}. Check Accel/Decel parameters.");
                }

                int finalCommandSpeed = GlobalSpeedManager.ApplyPendantSpeedSetting((int)Math.Round(maxSpeedC));

                System.Diagnostics.Debug.WriteLine(
                    $"Segment Speed Calculation: L={travelDistanceL:F2}, a={acceleration}, b={deceleration} -> c={maxSpeedC:F2} -> FinalCmdSpeed={finalCommandSpeed}");

                return finalCommandSpeed;
            }
            catch (Exception ex)
            {
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Error calculating final command speed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception in CalculateFinalCommandSpeedForSegment: {ex.Message}");
                return 0;
            }
        }

        public CoordinateData GetCurrentVisibleCoordinateData()
        {
            if (!string.IsNullOrEmpty(_currentSelectedGroup) &&
                !string.IsNullOrEmpty(_selectedGroupMenu) &&
                _groupCoordinateData.ContainsKey(_currentSelectedGroup) &&
                _groupCoordinateData[_currentSelectedGroup].ContainsKey(_selectedGroupMenu))
            {
                return _groupCoordinateData[_currentSelectedGroup][_selectedGroupMenu];
            }
            return null;
        }

        #region Safety Integration for Movement

        /// <summary>
        /// 로봇 동작 전 종합 안전 확인
        /// </summary>
        /// <returns>안전 여부</returns>
        private bool CheckSafetyBeforeMovement()
        {
            try
            {
                // 1. SafetySystem이 초기화되었는지 확인
                if (!SafetySystem.IsInitialized)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "안전 시스템이 초기화되지 않았습니다.");
                    return false;
                }

                // 2. 전체 안전 상태 확인
                if (SafetySystem.CurrentStatus != SafetyStatus.Safe)
                {
                    string statusMessage = "현재 안전 상태: " + SafetySystem.CurrentStatus;
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                        "안전 상태가 " + SafetySystem.CurrentStatus + "이므로 로봇 동작을 시작할 수 없습니다.");
                    System.Diagnostics.Debug.WriteLine("Movement: " + statusMessage);
                    return false;
                }

                // 3. 모든 인터록 확인
                if (!SafetySystem.AllInterlocksSecure)
                {
                    var unsafeDevices = SafetySystem.GetUnsafeInterlockDevices();
                    string deviceNames = "";
                    foreach (var device in unsafeDevices)
                    {
                        deviceNames += device.DeviceName + " ";
                    }

                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                        "다음 인터록이 안전하지 않습니다: " + deviceNames);
                    System.Diagnostics.Debug.WriteLine("Movement: 안전하지 않은 인터록 - " + deviceNames);
                    return false;
                }

                // 4. 로봇 작업 종합 안전성 확인
                if (!SafetySystem.IsSafeForRobotOperation())
                {
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                        "로봇 작업 조건이 충족되지 않았습니다.");
                    System.Diagnostics.Debug.WriteLine("Movement: 로봇 작업 조건 미충족");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("Movement: 모든 안전 확인 통과");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Movement 안전 확인 실패: " + ex.Message);
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "안전 확인 중 오류가 발생했습니다.");
                return false;
            }
        }

        /// <summary>
        /// 특정 메뉴에 따른 위치별 안전 확인
        /// </summary>
        /// <param name="menuName">메뉴명 (CPick, CPlace, SPick, SPlace)</param>
        /// <returns>안전 여부</returns>
        private bool CheckLocationSafetyForMenu(string menuName)
        {
            try
            {
                if (string.IsNullOrEmpty(menuName))
                {
                    System.Diagnostics.Debug.WriteLine("Movement: 메뉴명이 null이거나 비어있음");
                    return true; // 메뉴가 없으면 기본적으로 허용
                }

                switch (menuName)
                {
                    case "CPick":
                    case "CPlace":
                        // 챔버 관련 작업은 챔버 인터록 확인
                        bool chamber1Safe = SafetySystem.IsChamberSecure(1);
                        bool chamber2Safe = SafetySystem.IsChamberSecure(2);

                        if (!chamber1Safe || !chamber2Safe)
                        {
                            string unsafeChambers = "";
                            if (!chamber1Safe) unsafeChambers += "챔버1 ";
                            if (!chamber2Safe) unsafeChambers += "챔버2 ";

                            AlarmMessageManager.ShowAlarm(Alarms.WARNING,
                                unsafeChambers + "인터록이 안전하지 않습니다. 챔버 도어를 확인하세요.");
                            System.Diagnostics.Debug.WriteLine("Movement: " + unsafeChambers + "인터록 안전하지 않음");
                            return false;
                        }

                        System.Diagnostics.Debug.WriteLine("Movement: 챔버 인터록 안전 확인 통과");
                        break;

                    case "SPick":
                    case "SPlace":
                        // 스테이지 관련 작업은 로드포트 인터록 확인
                        bool loadPort1Safe = SafetySystem.IsLoadPortSecure(1);
                        bool loadPort2Safe = SafetySystem.IsLoadPortSecure(2);

                        if (!loadPort1Safe || !loadPort2Safe)
                        {
                            string unsafeLoadPorts = "";
                            if (!loadPort1Safe) unsafeLoadPorts += "로드포트1 ";
                            if (!loadPort2Safe) unsafeLoadPorts += "로드포트2 ";

                            AlarmMessageManager.ShowAlarm(Alarms.WARNING,
                                unsafeLoadPorts + "인터록이 안전하지 않습니다. 로드포트 도어를 확인하세요.");
                            System.Diagnostics.Debug.WriteLine("Movement: " + unsafeLoadPorts + "인터록 안전하지 않음");
                            return false;
                        }

                        System.Diagnostics.Debug.WriteLine("Movement: 로드포트 인터록 안전 확인 통과");
                        break;

                    default:
                        // 기본적으로 모든 위치 안전 확인
                        if (!SafetySystem.IsLocationSecure("챔버1") ||
                            !SafetySystem.IsLocationSecure("챔버2") ||
                            !SafetySystem.IsLocationSecure("로드포트1") ||
                            !SafetySystem.IsLocationSecure("로드포트2"))
                        {
                            AlarmMessageManager.ShowAlarm(Alarms.WARNING,
                                "일부 위치의 인터록이 안전하지 않습니다.");
                            System.Diagnostics.Debug.WriteLine("Movement: 일부 위치 인터록 안전하지 않음");
                            return false;
                        }
                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Movement 위치별 안전 확인 실패: " + ex.Message);
                return false; // 오류 시 안전하지 않은 것으로 판단
            }
        }

        /// <summary>
        /// 인터록 상태 표시 및 로깅
        /// </summary>
        private void ShowInterlockStatus()
        {
            try
            {
                if (!SafetySystem.IsInitialized)
                {
                    System.Diagnostics.Debug.WriteLine("Movement: SafetySystem 초기화되지 않음");
                    return;
                }

                string summary = SafetySystem.GetInterlockSystemSummary();
                System.Diagnostics.Debug.WriteLine("[Movement 인터록 상태] " + summary);

                // 현재 안전 상태도 함께 표시
                var currentStatus = SafetySystem.CurrentStatus;
                System.Diagnostics.Debug.WriteLine("[Movement 안전 상태] " + currentStatus);

                // 추가: UI에 상태 표시할 수 있는 부분 (선택사항)
                // 예시: 상태바, 라벨 등에 표시
                // if (lblSafetyStatus != null) 
                // {
                //     lblSafetyStatus.Content = "안전상태: " + currentStatus + " | " + summary;
                // }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Movement 인터록 상태 표시 실패: " + ex.Message);
            }
        }

        /// <summary>
        /// 안전 상태에 따른 동작 허용 여부 확인
        /// </summary>
        /// <param name="operationType">동작 유형 (Auto, Manual, Jog 등)</param>
        /// <returns>허용 여부</returns>
        private bool IsSafetyAllowedForOperation(string operationType)
        {
            try
            {
                if (!SafetySystem.IsInitialized)
                {
                    System.Diagnostics.Debug.WriteLine("Movement: SafetySystem 미초기화로 " + operationType + " 동작 차단");
                    return false;
                }

                var safetyStatus = SafetySystem.CurrentStatus;

                switch (safetyStatus)
                {
                    case SafetyStatus.Safe:
                        // 모든 동작 허용
                        return true;

                    case SafetyStatus.Warning:
                        // 수동 동작만 허용, 자동 동작은 차단
                        if (operationType == "Auto")
                        {
                            AlarmMessageManager.ShowAlarm(Alarms.WARNING,
                                "경고 상태에서는 자동 동작이 제한됩니다.");
                            return false;
                        }
                        return true;

                    case SafetyStatus.Dangerous:
                    case SafetyStatus.EmergencyStop:
                        // 모든 동작 차단
                        AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                            safetyStatus + " 상태에서는 로봇 동작이 금지됩니다.");
                        return false;

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Movement 안전 허용 확인 실패: " + ex.Message);
                return false;
            }
        }

        #endregion

        #region P3 Dynamic Coordinate Calculation
        /// <summary>
        /// P3에 Teaching 기본 좌표 설정 (UI 표시용만, 저장하지 않음)
        /// </summary>
        private void SetP3BaseCoordinates(string groupName, string menuType, TeachingStageInfo teachingData)
        {
            try
            {
                if (!_groupCoordinateData.ContainsKey(groupName) ||
                    !_groupCoordinateData[groupName].ContainsKey(menuType))
                {
                    System.Diagnostics.Debug.WriteLine($"좌표 데이터가 존재하지 않음: {groupName} - {menuType}");
                    return;
                }

                var coordData = _groupCoordinateData[groupName][menuType];

                // P3에 Teaching 좌표 설정 (첫 번째 슬롯 위치) - 임시로만 설정
                coordData.P3[0] = teachingData.PositionA.ToString("F2"); // A축
                coordData.P3[1] = teachingData.PositionT.ToString("F2"); // T축
                coordData.P3[2] = teachingData.PositionZ.ToString("F2"); // Z축 (기본 위치)
                                                                         // coordData.P3[3] = Speed는 기존 값 유지

                System.Diagnostics.Debug.WriteLine($"P3 기본 좌표 설정 (임시): A={teachingData.PositionA}, T={teachingData.PositionT}, Z={teachingData.PositionZ}");

                // 현재 UI에 표시 중인 메뉴라면 즉시 업데이트 (UI만 업데이트, 데이터는 저장 안함)
                if (_selectedGroupMenu == menuType)
                {
                    LoadCoordinatesForMenuWithoutSave(menuType);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetP3BaseCoordinates 오류: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 메뉴 좌표 로드 (저장하지 않는 버전)
        /// </summary>
        private void LoadCoordinatesForMenuWithoutSave(string menuType)
        {
            try
            {
                if (!ValidateCoordinateLoad(menuType)) return;

                var coordData = _groupCoordinateData[_currentSelectedGroup][menuType];

                // UI TextBox들만 업데이트 (데이터는 임시로 이미 설정됨)
                LoadCoordinateSet(new[] { txtP1A, txtP1T, txtP1Z, txtP1Speed }, coordData.P1);
                LoadCoordinateSet(new[] { txtP2A, txtP2T, txtP2Z, txtP2Speed }, coordData.P2);
                LoadCoordinateSet(new[] { txtP3A, txtP3T, txtP3Z, txtP3Speed }, coordData.P3);
                LoadCoordinateSet(new[] { txtP4A, txtP4T, txtP4Z, txtP4Speed }, coordData.P4);
                LoadCoordinateSet(new[] { txtP5A, txtP5T, txtP5Z, txtP5Speed }, coordData.P5);
                LoadCoordinateSet(new[] { txtP6A, txtP6T, txtP6Z, txtP6Speed }, coordData.P6);
                LoadCoordinateSet(new[] { txtP7A, txtP7T, txtP7Z, txtP7Speed }, coordData.P7);

                System.Diagnostics.Debug.WriteLine($"UI만 업데이트됨 (저장 안함): {_currentSelectedGroup} - {menuType}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCoordinatesForMenuWithoutSave 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// Teaching 데이터를 기반으로 P3 좌표의 슬롯별 동적 계산 기능 활성화
        /// </summary>
        private void EnableP3DynamicCalculation(string menuType, string groupName)
        {
            try
            {
                // CPick, SPick에만 적용 (실제 Pick 동작)
                if (menuType != "CPick" && menuType != "SPick")
                {
                    return;
                }

                var teachingData = GetTeachingDataForMenu(groupName, menuType);
                if (teachingData == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Teaching 데이터를 찾을 수 없음: {groupName} - {menuType}");
                    return;
                }

                // 저장된 P3 좌표를 복원하고 Teaching 데이터로 임시 덮어쓰기
                RestoreOriginalP3AndApplyTeaching(groupName, menuType, teachingData);

                System.Diagnostics.Debug.WriteLine($"P3 동적 계산 활성화: {menuType} - SlotCount={teachingData.SlotCount}, Pitch={teachingData.Pitch}");

                AlarmMessageManager.ShowAlarm(Alarms.OPERATION_COMPLETED,
                    $"{menuType} P3 동적 좌표 활성화 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnableP3DynamicCalculation 오류: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.DATA_ERROR, $"P3 동적 계산 설정 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 원본 P3 좌표 복원 후 Teaching 데이터 임시 적용
        /// </summary>
        private void RestoreOriginalP3AndApplyTeaching(string groupName, string menuType, TeachingStageInfo teachingData)
        {
            try
            {
                if (!_groupCoordinateData.ContainsKey(groupName) ||
                    !_groupCoordinateData[groupName].ContainsKey(menuType))
                {
                    System.Diagnostics.Debug.WriteLine($"좌표 데이터가 존재하지 않음: {groupName} - {menuType}");
                    return;
                }

                var coordData = _groupCoordinateData[groupName][menuType];

                // 원본 P3 좌표 백업 (필요시)
                string originalA = coordData.P3[0];
                string originalT = coordData.P3[1];
                string originalZ = coordData.P3[2];
                string originalSpeed = coordData.P3[3];

                // Teaching 데이터로 임시 설정 (메모리에만, 저장 안함)
                coordData.P3[0] = teachingData.PositionA.ToString("F2");
                coordData.P3[1] = teachingData.PositionT.ToString("F2");
                coordData.P3[2] = teachingData.PositionZ.ToString("F2");
                // Speed는 유지

                System.Diagnostics.Debug.WriteLine($"P3 임시 덮어쓰기: 원본({originalA}, {originalT}, {originalZ}) → Teaching({teachingData.PositionA}, {teachingData.PositionT}, {teachingData.PositionZ})");

                // UI만 업데이트 (저장하지 않음)
                if (_selectedGroupMenu == menuType)
                {
                    LoadCoordinatesForMenuWithoutSave(menuType);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestoreOriginalP3AndApplyTeaching 오류: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Teaching 시스템에서 메뉴별 데이터 가져오기
        /// </summary>
        private TeachingStageInfo GetTeachingDataForMenu(string groupName, string menuType)
        {
            try
            {
                // Teaching의 정적 데이터에 접근
                var teachingData = TeachingUI.Teaching.GetStageDataForGroup(groupName);

                if (teachingData == null || teachingData.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Teaching 그룹 데이터가 비어있음: {groupName}");
                    return null;
                }

                // CPick은 Cassette 데이터, SPick은 Stage 데이터 사용
                string targetType = menuType == "CPick" ? "Cassette" : "Stage";

                // 현재 선택된 번호에 해당하는 데이터 찾기
                int selectedNumber = GetSelectedNumberForMenu(groupName, menuType);
                string targetItemName = $"{targetType} {selectedNumber}";

                if (teachingData.ContainsKey(targetItemName))
                {
                    var data = teachingData[targetItemName];
                    return new TeachingStageInfo
                    {
                        SlotCount = data.SlotCount,
                        Pitch = data.Pitch,
                        PickOffset = data.PickOffset,
                        PickDown = data.PickDown,
                        PickUp = data.PickUp,
                        PlaceDown = data.PlaceDown,
                        PlaceUp = data.PlaceUp,
                        PositionA = data.PositionA,
                        PositionT = data.PositionT,
                        PositionZ = data.PositionZ,
                        ItemType = targetType
                    };
                }

                System.Diagnostics.Debug.WriteLine($"Teaching 데이터를 찾을 수 없음: {targetItemName}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTeachingDataForMenu 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 현재 메뉴에서 선택된 카세트/스테이지 번호 가져오기
        /// </summary>
        private int GetSelectedNumberForMenu(string groupName, string menuType)
        {
            try
            {
                if (_groupMenuSelectedNumbers.ContainsKey(groupName) &&
                    _groupMenuSelectedNumbers[groupName].ContainsKey(menuType))
                {
                    return _groupMenuSelectedNumbers[groupName][menuType];
                }

                return 1; // 기본값
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSelectedNumberForMenu 오류: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// 특정 슬롯 번호에 대한 P3 좌표 계산 (런타임 계산)
        /// </summary>
        public decimal[] CalculateP3CoordinatesForSlot(string groupName, string menuType, int slotNumber)
        {
            try
            {
                // Teaching 데이터 가져오기
                var teachingData = GetTeachingDataForMenu(groupName, menuType);
                if (teachingData == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Teaching 데이터 없음: {groupName} - {menuType}");
                    return new decimal[] { 0, 0, 0 };
                }

                // 슬롯 번호 유효성 검사
                if (slotNumber < 1 || slotNumber > teachingData.SlotCount)
                {
                    System.Diagnostics.Debug.WriteLine($"유효하지 않은 슬롯 번호: {slotNumber} (최대: {teachingData.SlotCount})");
                    return new decimal[] { 0, 0, 0 };
                }

                // 기본 위치에서 슬롯별 Z 좌표 계산
                decimal baseZ = teachingData.PositionZ;
                decimal slotZ = baseZ + ((slotNumber - 1) * teachingData.Pitch); // 1번 슬롯은 기본 위치

                decimal[] coordinates = new decimal[]
                {
            teachingData.PositionA, // A축 (고정)
            teachingData.PositionT, // T축 (고정)
            slotZ                   // Z축 (슬롯별 계산)
                };

                System.Diagnostics.Debug.WriteLine($"슬롯 {slotNumber} 좌표 계산: A={coordinates[0]}, T={coordinates[1]}, Z={coordinates[2]}");
                return coordinates;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CalculateP3CoordinatesForSlot 오류: {ex.Message}");
                return new decimal[] { 0, 0, 0 };
            }
        }

        /// <summary>
        /// 모든 슬롯에 대한 P3 좌표 목록 생성 (레시피 생성용)
        /// </summary>
        public List<decimal[]> GenerateAllSlotCoordinates(string groupName, string menuType)
        {
            var coordinateList = new List<decimal[]>();

            try
            {
                var teachingData = GetTeachingDataForMenu(groupName, menuType);
                if (teachingData == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Teaching 데이터 없음: {groupName} - {menuType}");
                    return coordinateList;
                }

                // 모든 슬롯에 대해 좌표 계산
                for (int slot = 1; slot <= teachingData.SlotCount; slot++)
                {
                    var coordinates = CalculateP3CoordinatesForSlot(groupName, menuType, slot);
                    coordinateList.Add(coordinates);
                }

                System.Diagnostics.Debug.WriteLine($"전체 슬롯 좌표 생성 완료: {coordinateList.Count}개 슬롯");
                return coordinateList;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerateAllSlotCoordinates 오류: {ex.Message}");
                return coordinateList;
            }
        }

        /// <summary>
        /// Teaching 데이터 변경시 P3 좌표 자동 업데이트
        /// </summary>
        private void OnTeachingDataUpdated(object sender, TeachingDataChangedEventArgs e)
        {
            try
            {
                // 현재 선택된 그룹과 일치하는 경우에만 처리
                if (e.GroupName != _currentSelectedGroup)
                {
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Teaching 데이터 업데이트 감지: {e.GroupName} - {e.ItemName}");

                // Cassette 데이터 변경시 CPick P3 업데이트
                if (e.ItemName.StartsWith("Cassette") && _selectedGroupMenu == "CPick")
                {
                    EnableP3DynamicCalculation("CPick", e.GroupName);
                }
                // Stage 데이터 변경시 SPick P3 업데이트
                else if (e.ItemName.StartsWith("Stage") && _selectedGroupMenu == "SPick")
                {
                    EnableP3DynamicCalculation("SPick", e.GroupName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnTeachingDataUpdated 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 메뉴 선택 변경시 P3 동적 계산 적용
        /// </summary>
        private void ApplyP3DynamicCalculationOnMenuChange(string menuType)
        {
            try
            {
                if (menuType == "CPick" || menuType == "SPick")
                {
                    EnableP3DynamicCalculation(menuType, _currentSelectedGroup);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyP3DynamicCalculationOnMenuChange 오류: {ex.Message}");
            }
        }
        #endregion

        #region Menu Selection Enhanced (메뉴 전환 시 원본 데이터 복원)
        /// <summary>
        /// 메뉴 선택 변경 시 처리 개선 (원본 데이터 복원 포함)
        /// </summary>
        private void OnMenuSelectionChangedEnhanced(string menuType)
        {
            try
            {
                // 이전 메뉴가 CPick/SPick이었다면 원본 데이터 복원
                if (_selectedGroupMenu == "CPick" || _selectedGroupMenu == "SPick")
                {
                    RestoreOriginalCoordinatesForMenu(_selectedGroupMenu);
                }

                // 기존 메뉴 변경 로직
                LoadCoordinatesForMenu(menuType);
                UpdateCassetteStageDisplay();

                // 새 메뉴가 CPick/SPick이면 Teaching 데이터 적용
                if (menuType == "CPick" || menuType == "SPick")
                {
                    EnableP3DynamicCalculation(menuType, _currentSelectedGroup);
                }

                System.Diagnostics.Debug.WriteLine($"Movement: 메뉴 변경 및 Teaching 연동 완료 - {menuType}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement: OnMenuSelectionChangedEnhanced 오류 - {ex.Message}");
            }
        }

        /// <summary>
        /// 특정 메뉴의 원본 좌표 데이터 복원 (저장된 값으로)
        /// </summary>
        private void RestoreOriginalCoordinatesForMenu(string menuType)
        {
            try
            {
                if (menuType != "CPick" && menuType != "SPick") return;

                // 영구 저장된 데이터에서 원본 좌표 복원
                if (_persistentGroupCoordinateData.ContainsKey(_currentSelectedGroup) &&
                    _persistentGroupCoordinateData[_currentSelectedGroup].ContainsKey(menuType))
                {
                    var originalData = _persistentGroupCoordinateData[_currentSelectedGroup][menuType];
                    var currentData = _groupCoordinateData[_currentSelectedGroup][menuType];

                    // P3만 원본으로 복원
                    Array.Copy(originalData.P3, currentData.P3, originalData.P3.Length);

                    System.Diagnostics.Debug.WriteLine($"{menuType} P3 원본 좌표 복원됨");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestoreOriginalCoordinatesForMenu 오류: {ex.Message}");
            }
        }
        #endregion

        #region Public Interface for External Access
        /// <summary>
        /// 외부에서 특정 슬롯의 P3 좌표를 요청할 때 사용
        /// </summary>
        public static decimal[] GetP3CoordinatesForSlot(string groupName, string menuType, int slotNumber)
        {
            try
            {
                var instance = GetCurrentInstance();
                if (instance != null)
                {
                    return instance.CalculateP3CoordinatesForSlot(groupName, menuType, slotNumber);
                }

                System.Diagnostics.Debug.WriteLine("Movement 인스턴스를 찾을 수 없음");
                return new decimal[] { 0, 0, 0 };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetP3CoordinatesForSlot 정적 메서드 오류: {ex.Message}");
                return new decimal[] { 0, 0, 0 };
            }
        }

        /// <summary>
        /// 외부에서 모든 슬롯 좌표를 요청할 때 사용
        /// </summary>
        public static List<decimal[]> GetAllSlotCoordinates(string groupName, string menuType)
        {
            try
            {
                var instance = GetCurrentInstance();
                if (instance != null)
                {
                    return instance.GenerateAllSlotCoordinates(groupName, menuType);
                }

                System.Diagnostics.Debug.WriteLine("Movement 인스턴스를 찾을 수 없음");
                return new List<decimal[]>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAllSlotCoordinates 정적 메서드 오류: {ex.Message}");
                return new List<decimal[]>();
            }
        }
        #endregion

        #region Data Classes for Teaching Integration
        /// <summary>
        /// Teaching 데이터 정보를 담는 클래스
        /// </summary>
        public class TeachingStageInfo
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
            public string ItemType { get; set; } = ""; // "Cassette" 또는 "Stage"
        }

        /// <summary>
        /// Teaching 데이터 변경 이벤트 인자
        /// </summary>
        public class TeachingDataChangedEventArgs : EventArgs
        {
            public string GroupName { get; private set; }
            public string ItemName { get; private set; }
            public TeachingStageInfo UpdatedData { get; private set; }

            public TeachingDataChangedEventArgs(string groupName, string itemName, TeachingStageInfo data)
            {
                GroupName = groupName;
                ItemName = itemName;
                UpdatedData = data;
            }
        }
        #endregion

        #region Static Instance Management
        // 현재 활성 Movement 인스턴스 관리
        private static Movement _currentInstance = null;

        /// <summary>
        /// 현재 활성 Movement 인스턴스 반환 (정적 메서드)
        /// </summary>
        /// <returns>현재 Movement 인스턴스</returns>
        public static Movement GetCurrentInstance()
        {
            try
            {
                // 기존에 등록된 인스턴스가 있고 유효하면 반환
                if (_currentInstance != null && _currentInstance.IsLoaded)
                {
                    System.Diagnostics.Debug.WriteLine("Movement: 기존 인스턴스 반환");
                    return _currentInstance;
                }

                // CommonFrame을 통해 현재 활성 Movement 인스턴스 찾기
                var frames = Application.Current?.Windows?.OfType<Window>()
                    ?.SelectMany(w => FindVisualChildren<CommonFrame>(w));

                if (frames != null)
                {
                    foreach (var frame in frames)
                    {
                        if (frame.MainContentArea?.Content is Movement movement)
                        {
                            _currentInstance = movement;
                            System.Diagnostics.Debug.WriteLine("Movement: Visual Tree에서 인스턴스 찾음");
                            return movement;
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine("Movement: 인스턴스를 찾을 수 없음");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement: GetCurrentInstance 오류 - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Movement 인스턴스 등록
        /// </summary>
        /// <param name="instance">등록할 Movement 인스턴스</param>
        public static void RegisterInstance(Movement instance)
        {
            try
            {
                _currentInstance = instance;
                System.Diagnostics.Debug.WriteLine("Movement: 인스턴스 등록됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement: RegisterInstance 오류 - {ex.Message}");
            }
        }

        /// <summary>
        /// Movement 인스턴스 등록 해제
        /// </summary>
        public static void UnregisterInstance()
        {
            try
            {
                _currentInstance = null;
                System.Diagnostics.Debug.WriteLine("Movement: 인스턴스 등록 해제됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement: UnregisterInstance 오류 - {ex.Message}");
            }
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

        #region Teaching Integration Methods
        /// <summary>
        /// Teaching과의 연동 초기화
        /// </summary>
        private void InitializeTeachingIntegration()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Movement: Teaching 연동 초기화 시작");

                // 현재 그룹의 CPick, SPick 메뉴에 대해 P3 동적 계산 적용
                var pickMenus = new[] { "CPick", "SPick" };

                foreach (string menuType in pickMenus)
                {
                    if (_groupCoordinateData.ContainsKey(_currentSelectedGroup) &&
                        _groupCoordinateData[_currentSelectedGroup].ContainsKey(menuType))
                    {
                        ApplyP3DynamicCalculationOnMenuChange(menuType);
                    }
                }

                System.Diagnostics.Debug.WriteLine("Movement: Teaching 연동 초기화 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement: Teaching 연동 초기화 오류 - {ex.Message}");
            }
        }

        /// <summary>
        /// Teaching 이벤트 구독
        /// </summary>
        private void SubscribeToTeachingEvents()
        {
            try
            {
                // Teaching의 데이터 변경 이벤트 구독 (향후 Teaching에서 이벤트 제공 시 사용)
                System.Diagnostics.Debug.WriteLine("Movement: Teaching 이벤트 구독 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement: Teaching 이벤트 구독 오류 - {ex.Message}");
            }
        }

        /// <summary>
        /// Teaching 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromTeachingEvents()
        {
            try
            {
                // Teaching 이벤트 구독 해제 로직
                System.Diagnostics.Debug.WriteLine("Movement: Teaching 이벤트 구독 해제 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement: Teaching 이벤트 구독 해제 오류 - {ex.Message}");
            }
        }

        /// <summary>
        /// 메뉴 선택 변경 시 Teaching 연동 처리 (기존 메서드 개선)
        /// </summary>
        private void OnMenuSelectionChanged(string menuType)
        {
            try
            {
                // 기존 메뉴 변경 로직...
                LoadCoordinatesForMenu(menuType);
                UpdateCassetteStageDisplay();

                // Teaching과의 P3 동적 계산 적용
                if (menuType == "CPick" || menuType == "SPick")
                {
                    ApplyP3DynamicCalculationOnMenuChange(menuType);
                }

                System.Diagnostics.Debug.WriteLine($"Movement: 메뉴 변경 및 Teaching 연동 완료 - {menuType}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement: OnMenuSelectionChanged 오류 - {ex.Message}");
            }
        }
        #endregion

        #region Teaching Data Integration Helper Methods
        /// <summary>
        /// 현재 그룹의 Teaching 데이터와 동기화
        /// </summary>
        private void SynchronizeWithTeachingData()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Movement: Teaching 데이터 동기화 시작 - {_currentSelectedGroup}");

                // Teaching에서 현재 그룹의 데이터 가져오기
                var teachingData = TeachingUI.Teaching.GetStageDataForGroup(_currentSelectedGroup);

                if (teachingData != null && teachingData.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Movement: Teaching 데이터 {teachingData.Count}개 발견");

                    // CPick, SPick 메뉴에 대해 P3 동적 계산 적용
                    if (_selectedGroupMenu == "CPick" || _selectedGroupMenu == "SPick")
                    {
                        EnableP3DynamicCalculation(_selectedGroupMenu, _currentSelectedGroup);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Movement: Teaching 데이터가 없음");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement: Teaching 데이터 동기화 오류 - {ex.Message}");
            }
        }

        /// <summary>
        /// Teaching 데이터 유효성 확인
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <param name="menuType">메뉴 타입</param>
        /// <returns>유효 여부</returns>
        private bool IsTeachingDataValid(string groupName, string menuType)
        {
            try
            {
                var teachingData = GetTeachingDataForMenu(groupName, menuType);
                return teachingData != null && teachingData.SlotCount > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Movement: Teaching 데이터 유효성 확인 오류 - {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Slot Tracking for Dynamic P3 Calculation
        // 슬롯 추적 변수들
        private int _currentSlotNumber = 1;
        private int _totalSlotCount = 1;
        private bool _isSlotTrackingActive = false;

        /// <summary>
        /// 슬롯 추적 초기화 (CPick 또는 SPick 선택 시)
        /// </summary>
        private void InitializeSlotTracking(string menuType)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"InitializeSlotTracking 호출: {menuType}");

                // Teaching 데이터 가져오기
                var teachingData = GetTeachingDataForMenu(_currentSelectedGroup, menuType);
                if (teachingData != null && teachingData.SlotCount > 1)
                {
                    _totalSlotCount = teachingData.SlotCount;
                    _currentSlotNumber = 1; // 시작은 항상 1번 슬롯
                    _isSlotTrackingActive = true;

                    System.Diagnostics.Debug.WriteLine($"슬롯 추적 초기화: {menuType} - 총 {_totalSlotCount}개 슬롯, 시작 슬롯: {_currentSlotNumber}");
                }
                else
                {
                    _totalSlotCount = 1;
                    _currentSlotNumber = 1;
                    _isSlotTrackingActive = false;

                    System.Diagnostics.Debug.WriteLine($"Teaching 데이터 없음, 기본 슬롯 설정: {menuType}");
                }

                // 슬롯 UI 업데이트
                UpdateSlotTrackingUI(_currentSlotNumber, _totalSlotCount);

                // 첫 번째 슬롯에 맞는 P3 좌표 계산 및 적용 (중요: 이 시점에서는 아직 Start하지 않음)
                if (_isSlotTrackingActive)
                {
                    ApplyP3CoordinatesForCurrentSlot(menuType);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeSlotTracking 오류: {ex.Message}");
                _isSlotTrackingActive = false;
                UpdateSlotTrackingUI(1, 1);
            }
        }

        /// <summary>
        /// 슬롯 추적 UI 업데이트
        /// </summary>
        private void UpdateSlotTrackingUI(int currentSlot, int totalSlots)
        {
            try
            {
                if (txtCurrentSlot != null)
                    txtCurrentSlot.Text = currentSlot.ToString();

                if (txtTotalSlots != null)
                    txtTotalSlots.Text = totalSlots.ToString();

                System.Diagnostics.Debug.WriteLine($"슬롯 UI 업데이트: {currentSlot}/{totalSlots}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSlotTrackingUI 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 슬롯에 맞는 P3 좌표 계산 및 적용
        /// </summary>
        private void ApplyP3CoordinatesForCurrentSlot(string menuType)
        {
            try
            {
                if (!_isSlotTrackingActive) return;

                // Teaching 데이터 가져오기
                var teachingData = GetTeachingDataForMenu(_currentSelectedGroup, menuType);
                if (teachingData == null) return;

                // 현재 슬롯의 P3 좌표 계산
                decimal slotZ = teachingData.PositionZ + ((_currentSlotNumber - 1) * teachingData.Pitch);

                // P3 좌표 업데이트 (임시로, 저장하지 않음)
                if (_groupCoordinateData.ContainsKey(_currentSelectedGroup) &&
                    _groupCoordinateData[_currentSelectedGroup].ContainsKey(menuType))
                {
                    var coordData = _groupCoordinateData[_currentSelectedGroup][menuType];

                    // P3 Z 좌표만 슬롯별로 업데이트
                    coordData.P3[0] = teachingData.PositionA.ToString("F2"); // A축
                    coordData.P3[1] = teachingData.PositionT.ToString("F2"); // T축
                    coordData.P3[2] = slotZ.ToString("F2"); // Z축 (슬롯별 계산)
                                                            // Speed는 기존 값 유지

                    System.Diagnostics.Debug.WriteLine($"슬롯 {_currentSlotNumber} P3 좌표 적용: A={teachingData.PositionA}, T={teachingData.PositionT}, Z={slotZ:F2}");

                    // UI 업데이트 (현재 선택된 메뉴인 경우)
                    if (_selectedGroupMenu == menuType)
                    {
                        LoadCoordinatesForMenuWithoutSave(menuType);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyP3CoordinatesForCurrentSlot 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 다음 슬롯으로 이동 (자동 실행 시 호출)
        /// </summary>
        public void MoveToNextSlot()
        {
            try
            {
                if (!_isSlotTrackingActive) return;

                if (_currentSlotNumber < _totalSlotCount)
                {
                    _currentSlotNumber++;
                    System.Diagnostics.Debug.WriteLine($"다음 슬롯으로 이동: {_currentSlotNumber}/{_totalSlotCount}");
                }
                else
                {
                    // 마지막 슬롯에 도달하면 1번으로 리셋
                    _currentSlotNumber = 1;
                    System.Diagnostics.Debug.WriteLine($"마지막 슬롯 완료, 1번으로 리셋: {_currentSlotNumber}/{_totalSlotCount}");
                }

                UpdateSlotTrackingUI(_currentSlotNumber, _totalSlotCount);

                // 새 슬롯에 맞는 P3 좌표 적용
                if (_selectedGroupMenu == "CPick" || _selectedGroupMenu == "SPick")
                {
                    ApplyP3CoordinatesForCurrentSlot(_selectedGroupMenu);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MoveToNextSlot 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 슬롯 리셋 (처음부터 시작)
        /// </summary>
        public void ResetSlotTracking()
        {
            try
            {
                _currentSlotNumber = 1;
                UpdateSlotTrackingUI(_currentSlotNumber, _totalSlotCount);

                // 첫 번째 슬롯 좌표 적용
                if (_isSlotTrackingActive && (_selectedGroupMenu == "CPick" || _selectedGroupMenu == "SPick"))
                {
                    ApplyP3CoordinatesForCurrentSlot(_selectedGroupMenu);
                }

                System.Diagnostics.Debug.WriteLine("슬롯 추적 리셋됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResetSlotTracking 오류: {ex.Message}");
            }
        }
        #endregion

        #region Auto Execution Enhanced with Slot Tracking
        private void InitializeAutoExecution()
        {
            _autoExecutionTimer = new DispatcherTimer();

            // Speed에 따라 타이머 간격 조정
            UpdateTimerInterval();

            // 이벤트 핸들러 연결 확인
            _autoExecutionTimer.Tick += AutoExecutionTimer_Tick;

            // 초기 상태 설정
            _currentR = 0.00m;
            _currentT = 0.00m;
            _currentA = 0.00m;

            UpdateAutoStatusDisplay();

            System.Diagnostics.Debug.WriteLine("Movement: AutoExecution 초기화 완료");
        }

        /// <summary>
        /// 목표 지점 도달 확인
        /// </summary>
        private bool HasReachedTarget()
        {
            const decimal tolerance = 0.1m;
            return Math.Abs(_currentR - _targetR) < tolerance &&
                   Math.Abs(_currentT - _targetT) < tolerance &&
                   Math.Abs(_currentA - _targetA) < tolerance;
        }
        #endregion

        #region UI 요소 초기화 확인 메서드 추가
        /// <summary>
        /// UI 요소들이 제대로 로드되었는지 확인
        /// </summary>
        private void ValidateUIElements()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== UI 요소 검증 시작 ===");

                if (txtCurrentR == null)
                    System.Diagnostics.Debug.WriteLine("경고: txtCurrentR이 null입니다");
                else
                    System.Diagnostics.Debug.WriteLine("txtCurrentR 정상 로드됨");

                if (txtCurrentT == null)
                    System.Diagnostics.Debug.WriteLine("경고: txtCurrentT가 null입니다");
                else
                    System.Diagnostics.Debug.WriteLine("txtCurrentT 정상 로드됨");

                if (txtCurrentA == null)
                    System.Diagnostics.Debug.WriteLine("경고: txtCurrentA가 null입니다");
                else
                    System.Diagnostics.Debug.WriteLine("txtCurrentA 정상 로드됨");

                if (txtCurrentSlot == null)
                    System.Diagnostics.Debug.WriteLine("경고: txtCurrentSlot이 null입니다");
                else
                    System.Diagnostics.Debug.WriteLine("txtCurrentSlot 정상 로드됨");

                if (txtTotalSlots == null)
                    System.Diagnostics.Debug.WriteLine("경고: txtTotalSlots가 null입니다");
                else
                    System.Diagnostics.Debug.WriteLine("txtTotalSlots 정상 로드됨");

                System.Diagnostics.Debug.WriteLine("=== UI 요소 검증 완료 ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ValidateUIElements 오류: {ex.Message}");
            }
        }
        #endregion
    }

    public static class GroupDataManager
    {
        private static List<string> _availableGroups = new List<string> { "Group1" };
        public static event EventHandler GroupListUpdated;

        public static List<string> GetAvailableGroups()
        {
            return new List<string>(_availableGroups);
        }

        public static void UpdateGroupList(List<string> groups)
        {
            _availableGroups = new List<string>(groups);
            if (!_availableGroups.Any())
            {
                _availableGroups.Add("Group1");
            }

            GroupListUpdated?.Invoke(null, EventArgs.Empty);
            System.Diagnostics.Debug.WriteLine($"GroupDataManager: List updated with {string.Join(", ", _availableGroups)}");
        }
    }

    #region Event Args Classes for Monitor Integration
    /// <summary>
    /// Movement 좌표 변경 이벤트 인자
    /// </summary>
    public class MovementCoordinateEventArgs : EventArgs
    {
        public decimal PositionR { get; }
        public decimal PositionT { get; }
        public decimal PositionA { get; }

        public MovementCoordinateEventArgs(decimal r, decimal t, decimal a)
        {
            PositionR = r;
            PositionT = t;
            PositionA = a;
        }
    }

    /// <summary>
    /// Movement 구간 변경 이벤트 인자
    /// </summary>
    public class MovementSectionEventArgs : EventArgs
    {
        public string MenuName { get; }
        public string PointName { get; }
        public bool IsRunning { get; }
        public string FullSectionName => IsRunning ? $"{MenuName} {PointName}" : "Stopped";

        public MovementSectionEventArgs(string menuName, string pointName, bool isRunning)
        {
            MenuName = menuName;
            PointName = pointName;
            IsRunning = isRunning;
        }
    }
    #endregion
}