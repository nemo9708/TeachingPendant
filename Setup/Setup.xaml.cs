using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TeachingPendant.Alarm;
using TeachingPendant.Manager;
using System.Linq;
using TeachingPendant;

namespace TeachingPendant.SetupUI
{
    public partial class Setup : UserControl
    {
        #region Fields (Instance)
        private readonly SolidColorBrush _activeBrush = new SolidColorBrush(Colors.LightGreen);
        private readonly SolidColorBrush _inactiveBrush = new SolidColorBrush(Colors.LightGray);
        #endregion

        #region Persistent Storage (Static)
        private static bool _persistentIsDemoMode = true;
        private static bool _persistentIsInterlockEnabled = false;
        private static string _persistentHomePosA = "0.00";
        private static string _persistentHomePosT = "0.00";
        private static string _persistentHomePosZ = "0.00";
        private static string _persistentSoftLimitA1 = "-180.00";
        private static string _persistentSoftLimitA2 = "180.00";
        private static string _persistentSoftLimitT1 = "-90.00";
        private static string _persistentSoftLimitT2 = "90.00";
        private static string _persistentSoftLimitZ1 = "0.00";
        private static string _persistentSoftLimitZ2 = "100.00";
        private static string _persistentAcceleration = "1000";
        private static string _persistentDeceleration = "1000";
        private static string _persistentRetryCount = "3";
        private static string _persistentOriginOffsetTheta = "30.00";
        private static string _persistentArmLinkLength = "300.00";
        private static string _persistentSystemSpeedMMS = "100.0"; // mm/s 단위로 변경 (기본값 100mm/s)
        private static bool _hasMovementUiBeenOpened = false;
        #endregion

        #region Constructor and Initialization
        public Setup()
        {
            InitializeComponent();
            LoadPersistentData();
            UpdateButtonStates();
            SubscribeToGlobalSpeedEvents();
            AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_INITIALIZED, "Setup UI initialized with all preserved settings");
        }

        public static bool IsMovementInstanceRegistered => _movementInstance != null;
        public static bool HasMovementUiBeenOpenedOnce => _hasMovementUiBeenOpened;

        private void LoadPersistentData()
        {
            txtHomePosA.Text = _persistentHomePosA;
            txtHomePosT.Text = _persistentHomePosT;
            txtHomePosZ.Text = _persistentHomePosZ;
            txtSoftLimitA1.Text = _persistentSoftLimitA1;
            txtSoftLimitA2.Text = _persistentSoftLimitA2;
            txtSoftLimitT1.Text = _persistentSoftLimitT1;
            txtSoftLimitT2.Text = _persistentSoftLimitT2;
            txtSoftLimitZ1.Text = _persistentSoftLimitZ1;
            txtSoftLimitZ2.Text = _persistentSoftLimitZ2;
            txtRetryCount.Text = _persistentRetryCount;

            // System Speed를 mm/s로 로드
            txtSystemSpeed.Text = _persistentSystemSpeedMMS;

            if (txtOriginOffsetTheta != null) txtOriginOffsetTheta.Text = _persistentOriginOffsetTheta;
            if (txtArmLinkLength != null) txtArmLinkLength.Text = _persistentArmLinkLength;

            AttachTextBoxEvents();

            System.Diagnostics.Debug.WriteLine($"Setup data loaded - System Speed: {_persistentSystemSpeedMMS} mm/s, Speed Control: {GlobalSpeedManager.CurrentSpeed}%");
            UpdateCalculatedStrokeAngle();
        }

        private void SaveToPersistentStorage()
        {
            _persistentHomePosA = txtHomePosA?.Text ?? "0.00";
            _persistentHomePosT = txtHomePosT?.Text ?? "0.00";
            _persistentHomePosZ = txtHomePosZ?.Text ?? "0.00";
            _persistentSoftLimitA1 = txtSoftLimitA1?.Text ?? "-180.00";
            _persistentSoftLimitA2 = txtSoftLimitA2?.Text ?? "180.00";
            _persistentSoftLimitT1 = txtSoftLimitT1?.Text ?? "-90.00";
            _persistentSoftLimitT2 = txtSoftLimitT2?.Text ?? "90.00";
            _persistentSoftLimitZ1 = txtSoftLimitZ1?.Text ?? "0.00";
            _persistentSoftLimitZ2 = txtSoftLimitZ2?.Text ?? "100.00"; // .Text 제거!
            _persistentRetryCount = txtRetryCount?.Text ?? "3";

            // System Speed를 static 프로퍼티를 통해 저장
            if (txtSystemSpeed != null)
            {
                SystemSpeedMMS = double.TryParse(txtSystemSpeed.Text, out double speed) ? speed : 100.0;
            }

            if (txtOriginOffsetTheta != null) _persistentOriginOffsetTheta = txtOriginOffsetTheta.Text;
            if (txtArmLinkLength != null) _persistentArmLinkLength = txtArmLinkLength.Text;

            // 파일에도 자동 저장 추가
            TriggerAutoSave();

            System.Diagnostics.Debug.WriteLine($"Setup data saved - System Speed: {SystemSpeedMMS} mm/s");
        }

        private void AttachTextBoxEvents()
        {
            var textBoxes = new[] {
                txtHomePosA, txtHomePosT, txtHomePosZ,
                txtSoftLimitA1, txtSoftLimitA2, txtSoftLimitT1,
                txtSoftLimitT2, txtSoftLimitZ1, txtSoftLimitZ2,
                txtRetryCount, txtOriginOffsetTheta, txtArmLinkLength
            };
            foreach (var textBox in textBoxes)
            {
                if (textBox != null)
                {
                    textBox.TextChanged += TextBox_TextChanged;
                }
            }
            System.Diagnostics.Debug.WriteLine("TextBox events attached for real-time saving");
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveToPersistentStorage();
            var textBox = sender as TextBox;

            if (textBox == txtArmLinkLength || textBox == txtSoftLimitA1 || textBox == txtSoftLimitA2)
            {
                UpdateCalculatedStrokeAngle();
            }

            if (textBox == txtHomePosA || textBox == txtHomePosT || textBox == txtHomePosZ)
            {
                if (_movementInstance == null)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.WARNING, "HomePos changed but Movement UI not connected - Open Movement UI for auto-sync");
                }
                OnHomePosChanged(HomePosA, HomePosT, HomePosZ);
            }
        }

        private void UpdateButtonStates()
        {
            btnDemoModeOn.Background = _persistentIsDemoMode ? _activeBrush : _inactiveBrush;
            btnDemoModeOff.Background = !_persistentIsDemoMode ? _activeBrush : _inactiveBrush;
            btnInterlockOn.Background = _persistentIsInterlockEnabled ? _activeBrush : _inactiveBrush;
            btnInterlockOff.Background = !_persistentIsInterlockEnabled ? _activeBrush : _inactiveBrush;
        }
        #endregion

        #region Global Speed Management
        private void SubscribeToGlobalSpeedEvents()
        {
            // GlobalSpeedManager 이벤트는 구독하지 않음!
            // System Speed가 주도권을 가짐
            AttachSystemSpeedEvents();
            this.Unloaded += Setup_Unloaded;
        }

        private void AttachSystemSpeedEvents()
        {
            if (txtSystemSpeed != null)
            {
                txtSystemSpeed.TextChanged += TxtSystemSpeed_TextChanged;
                txtSystemSpeed.LostFocus += TxtSystemSpeed_LostFocus;
                txtSystemSpeed.KeyDown += TxtSystemSpeed_KeyDown;
            }
        }

        private void TxtSystemSpeed_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (double.TryParse(textBox.Text, out double value))
            {
                // mm/s 값 범위 제한 (1.0 ~ 1000.0 mm/s)
                if (value < 1.0)
                {
                    textBox.Text = "1.0";
                    textBox.SelectionStart = textBox.Text.Length;
                }
                else if (value > 1000.0)
                {
                    textBox.Text = "1000.0";
                    textBox.SelectionStart = textBox.Text.Length;
                }
            }
        }

        private void TxtSystemSpeed_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateGlobalSpeedFromSystemSpeedMMS();
        }

        /// <summary>
        /// System Speed (mm/s) - static 프로퍼티로 변경
        /// </summary>
        public static double SystemSpeedMMS
        {
            get
            {
                if (double.TryParse(_persistentSystemSpeedMMS, out double speed))
                    return speed;
                return 100.0; // 기본값 100.0 mm/s
            }
            set
            {
                var validSpeed = Math.Max(1.0, Math.Min(1000.0, value));
                _persistentSystemSpeedMMS = validSpeed.ToString("F1");

                // 현재 활성화된 Setup 인스턴스가 있으면 UI도 업데이트
                UpdateActiveSetupUI(validSpeed);
            }
        }

        /// <summary>
        /// 활성화된 Setup UI 업데이트 (있는 경우)
        /// </summary>
        private static void UpdateActiveSetupUI(double speedValue)
        {
            // 현재 열려있는 Setup UI 찾아서 업데이트
            foreach (System.Windows.Window window in System.Windows.Application.Current.Windows)
            {
                if (window is CommonFrame frame && frame.MainContentArea?.Content is Setup setupControl)
                {
                    if (setupControl.txtSystemSpeed != null)
                    {
                        setupControl.txtSystemSpeed.Text = speedValue.ToString("F1");
                    }
                    break;
                }
            }
        }

        public static int SystemSpeed
        {
            get { return (int)Math.Round(SystemSpeedMMS); }
            set { SystemSpeedMMS = value; }
        }

        /// <summary>
        /// 최종 실제 속도 계산 (System Speed * Speed Control %)
        /// </summary>
        public static double GetActualSpeedMMS()
        {
            double systemSpeed = SystemSpeedMMS;
            double speedPercent = GlobalSpeedManager.CurrentSpeed / 100.0;
            double actualSpeed = systemSpeed * speedPercent;

            System.Diagnostics.Debug.WriteLine($"GetActualSpeedMMS: {systemSpeed}mm/s × {GlobalSpeedManager.CurrentSpeed}% = {actualSpeed:F1}mm/s");

            return actualSpeed;
        }

        private void TxtSystemSpeed_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                UpdateGlobalSpeedFromSystemSpeedMMS();
                var textBox = sender as TextBox;
                textBox?.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
                e.Handled = true;
            }
        }

        private void UpdateGlobalSpeedFromSystemSpeedMMS()
        {
            if (txtSystemSpeed != null && double.TryParse(txtSystemSpeed.Text, out double speedMMS))
            {
                // mm/s 범위 제한
                speedMMS = Math.Max(1.0, Math.Min(1000.0, speedMMS));
                txtSystemSpeed.Text = speedMMS.ToString("F1");

                // static 프로퍼티 업데이트
                SystemSpeedMMS = speedMMS;

                AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE,
                    $"System base speed set to {speedMMS:F1} mm/s");

                System.Diagnostics.Debug.WriteLine($"System Speed 기준값 변경: {speedMMS:F1} mm/s (Speed Control은 {GlobalSpeedManager.CurrentSpeed}% 유지)");
            }
        }

        private void Setup_Unloaded(object sender, RoutedEventArgs e)
        {
            SaveToPersistentStorage();
            System.Diagnostics.Debug.WriteLine("Setup UI - Data saved");
        }
        #endregion

        #region m/s <-> % 변환 메서드
        /// <summary>
        /// m/s를 %로 변환 (1.0 m/s = 100%)
        /// </summary>
        private int ConvertMMSToPercent(double speedMMS)
        {
            // 변환 공식: 100.0 mm/s = 100%
            // 50.0 mm/s = 50%, 200.0 mm/s = 200%
            int percent = (int)Math.Round(speedMMS);
            return Math.Max(1, Math.Min(200, percent)); // 1~200% 범위
        }

        /// <summary>
        /// %를 mm/s로 변환
        /// </summary>
        private double ConvertPercentToMMS(int speedPercent)
        {
            // 변환 공식: 100% = 100.0 mm/s
            double speedMMS = speedPercent;
            return Math.Max(1.0, Math.Min(1000.0, speedMMS)); // 1.0~1000.0 mm/s 범위
        }
        #endregion

        #region Mode Button Event Handlers
        private void DemoModeOn_Click(object sender, RoutedEventArgs e)
        {
            _persistentIsDemoMode = true;
            UpdateButtonStates();
            AlarmMessageManager.ShowAlarm(Alarms.MODE_CHANGED, "Demo Mode: ON - Saved permanently");
        }

        private void DemoModeOff_Click(object sender, RoutedEventArgs e)
        {
            _persistentIsDemoMode = false;
            UpdateButtonStates();
            AlarmMessageManager.ShowAlarm(Alarms.MODE_CHANGED, "Demo Mode: OFF - Saved permanently");
        }

        private void InterlockOn_Click(object sender, RoutedEventArgs e)
        {
            _persistentIsInterlockEnabled = true;
            UpdateButtonStates();
            AlarmMessageManager.ShowAlarm(Alarms.MODE_CHANGED, "Use Interlock: ON - Saved permanently");
        }

        private void InterlockOff_Click(object sender, RoutedEventArgs e)
        {
            _persistentIsInterlockEnabled = false;
            UpdateButtonStates();
            AlarmMessageManager.ShowAlarm(Alarms.MODE_CHANGED, "Use Interlock: OFF - Saved permanently");
        }
        #endregion

        #region Public Properties
        public bool IsDemoMode { get => _persistentIsDemoMode; set { _persistentIsDemoMode = value; UpdateButtonStates(); } }
        public bool IsInterlockEnabled { get => _persistentIsInterlockEnabled; set { _persistentIsInterlockEnabled = value; UpdateButtonStates(); } }

        public int RetryCount
        {
            get { if (int.TryParse(txtRetryCount?.Text, out int count)) return count; return 3; }
            set { if (txtRetryCount != null) { txtRetryCount.Text = value.ToString(); SaveToPersistentStorage(); } }
        }

        public static double Acceleration
        {
            get { if (double.TryParse(_persistentAcceleration, out double val)) return val; return 1000.0; }
            set { _persistentAcceleration = value.ToString(); }
        }

        public static double Deceleration
        {
            get { if (double.TryParse(_persistentDeceleration, out double val)) return val; return 1000.0; }
            set { _persistentDeceleration = value.ToString(); }
        }

        public static decimal OriginOffsetTheta
        {
            get
            {
                if (decimal.TryParse(_persistentOriginOffsetTheta, out decimal val)) return val;
                return 30.00m;
            }
        }

        public static decimal ArmLinkLength => decimal.TryParse(_persistentArmLinkLength, out var val) ? val : 0;
        public static decimal SoftLimitA1 => decimal.TryParse(_persistentSoftLimitA1, out var val) ? val : -180.00m;
        public static decimal SoftLimitA2 => decimal.TryParse(_persistentSoftLimitA2, out var val) ? val : 180.00m;
        public static decimal SoftLimitT1 => decimal.TryParse(_persistentSoftLimitT1, out var val) ? val : -90.00m;
        public static decimal SoftLimitT2 => decimal.TryParse(_persistentSoftLimitT2, out var val) ? val : 90.00m;
        public static decimal SoftLimitZ1 => decimal.TryParse(_persistentSoftLimitZ1, out var val) ? val : 0.00m;
        public static decimal SoftLimitZ2 => decimal.TryParse(_persistentSoftLimitZ2, out var val) ? val : 100.00m;
        #endregion

        #region HomePos Management and Movement Integration
        public static event EventHandler<HomePosChangedEventArgs> HomePosChanged;
        private static MovementUI.Movement _movementInstance;

        public static void RegisterMovementInstance(MovementUI.Movement movementInstance)
        {
            _movementInstance = movementInstance;
            _hasMovementUiBeenOpened = true;
            System.Diagnostics.Debug.WriteLine("Movement instance registered and permanent flag set.");
        }

        public static void UnregisterMovementInstance()
        {
            _movementInstance = null;
            System.Diagnostics.Debug.WriteLine("Movement instance unregistered from Setup");
        }

        public static decimal HomePosA { get { if (decimal.TryParse(_persistentHomePosA, out decimal val)) return val; return 0.00m; } }
        public static decimal HomePosT { get { if (decimal.TryParse(_persistentHomePosT, out decimal val)) return val; return 0.00m; } }
        public static decimal HomePosZ { get { if (decimal.TryParse(_persistentHomePosZ, out decimal val)) return val; return 0.00m; } }

        public static void SetHomePos(decimal posA, decimal posT, decimal posZ)
        {
            _persistentHomePosA = posA.ToString("F2");
            _persistentHomePosT = posT.ToString("F2");
            _persistentHomePosZ = posZ.ToString("F2");
            OnHomePosChanged(posA, posT, posZ);
        }

        private static void OnHomePosChanged(decimal posA, decimal posT, decimal posZ)
        {
            HomePosChanged?.Invoke(null, new HomePosChangedEventArgs(posA, posT, posZ));
            if (_movementInstance != null && GlobalModeManager.IsEditingAllowed)
            {
                ApplyHomePosToMovementP1();
            }
            var frame = Application.Current?.Windows.OfType<CommonFrame>().FirstOrDefault();
            frame?.NavigateToRecipeEditor(1);
        }

        private void ApplyHomePos_Click(object sender, RoutedEventArgs e)
        {
            // Movement UI에 한 번이라도 들어갔는지 확인
            if (!SetupUI.Setup.HasMovementUiBeenOpenedOnce)
            {
                MessageBox.Show("Movement UI is not connected.\nPlease open Movement UI first.",
                    "Movement UI Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                AlarmMessageManager.ShowAlarm(Alarms.WARNING,
                    "Cannot apply HomePos - Movement UI not connected");
                return;
            }

            // Movement UI에 들어간 적이 있으면 현재 연결 상태와 관계없이 적용
            ApplyHomePosToMovementP1();
            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                "HomePos manually applied to all Movement P1 coordinates");
        }

        public static void ApplyHomePosToMovementP1()
        {
            decimal homeA = HomePosA;
            decimal homeT = HomePosT;
            decimal homeZ = HomePosZ;

            try
            {
                // 현재 활성화된 Movement 인스턴스가 있으면 직접 적용
                if (_movementInstance != null)
                {
                    System.Diagnostics.Debug.WriteLine("Movement instance is active - applying directly");
                    var targetMenus = new[] { "CPick", "CPlace", "SPick", "SPlace" };
                    foreach (string menuType in targetMenus)
                    {
                        _movementInstance.ApplyHomePosToMenuP1(menuType, homeA, homeT, homeZ);
                    }
                    AlarmMessageManager.ShowAlarm(Alarms.POSITION_LOADED,
                        $"HomePos applied to active Movement P1: A={homeA:F2}, T={homeT:F2}, Z={homeZ:F2}");
                }
                else
                {
                    // Movement 인스턴스가 없으면 정적 데이터에 직접 저장
                    System.Diagnostics.Debug.WriteLine("Movement instance not found - saving to static data");
                    ApplyHomePosToStaticData(homeA, homeT, homeZ);
                    AlarmMessageManager.ShowAlarm(Alarms.POSITION_LOADED,
                        $"HomePos saved to Movement data: A={homeA:F2}, T={homeT:F2}, Z={homeZ:F2} (will apply when Movement opens)");
                }
            }
            catch (Exception ex)
            {
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                    $"Failed to apply HomePos to Movement: {ex.Message}");
            }
        }

        /// <summary>
        /// Movement의 정적 데이터에 직접 HomePos를 적용하는 메서드
        /// </summary>
        private static void ApplyHomePosToStaticData(decimal homeA, decimal homeT, decimal homeZ)
        {
            // Movement.xaml.cs의 정적 데이터에 접근해야 합니다
            // 이 부분은 Movement 클래스에 정적 메서드를 추가해야 합니다
            MovementUI.Movement.ApplyHomePosToStaticData(homeA, homeT, homeZ);
        }

        private void UpdateCalculatedStrokeAngle()
        {
            if (txtCalculatedStrokeAngle == null) return;

            var linkLength = ArmLinkLength;
            var limitA1 = SoftLimitA1; // A축 최소값
            var limitA2 = SoftLimitA2; // A축 최대값

            if (linkLength > 0)
            {
                double calculatedAngle = MovementUI.MovementDataHelper.CalculateFullStrokeAngle(linkLength, limitA1, limitA2);
                txtCalculatedStrokeAngle.Text = $"{calculatedAngle:F2}°";
            }
            else
            {
                txtCalculatedStrokeAngle.Text = "-"; // 계산 불가
            }
        }
        #endregion

        #region Data Persistence Integration
        /// <summary>
        /// PersistentDataManager에서 데이터를 로드하여 Setup UI에 적용
        /// </summary>
        public static void LoadFromPersistentData(PersistentDataManager.SetupDataContainer data)
        {
            if (data == null)
            {
                System.Diagnostics.Debug.WriteLine("Setup: No persistent data to load, using defaults");
                return;
            }

            try
            {
                // 모드 설정 로드
                _persistentIsDemoMode = data.IsDemoMode;
                _persistentIsInterlockEnabled = data.IsInterlockEnabled;

                // HomePos 로드
                if (!string.IsNullOrEmpty(data.HomePosA))
                    _persistentHomePosA = data.HomePosA;
                if (!string.IsNullOrEmpty(data.HomePosT))
                    _persistentHomePosT = data.HomePosT;
                if (!string.IsNullOrEmpty(data.HomePosZ))
                    _persistentHomePosZ = data.HomePosZ;

                // SoftLimit 로드
                if (!string.IsNullOrEmpty(data.SoftLimitA1))
                    _persistentSoftLimitA1 = data.SoftLimitA1;
                if (!string.IsNullOrEmpty(data.SoftLimitA2))
                    _persistentSoftLimitA2 = data.SoftLimitA2;
                if (!string.IsNullOrEmpty(data.SoftLimitT1))
                    _persistentSoftLimitT1 = data.SoftLimitT1;
                if (!string.IsNullOrEmpty(data.SoftLimitT2))
                    _persistentSoftLimitT2 = data.SoftLimitT2;
                if (!string.IsNullOrEmpty(data.SoftLimitZ1))
                    _persistentSoftLimitZ1 = data.SoftLimitZ1;
                if (!string.IsNullOrEmpty(data.SoftLimitZ2))
                    _persistentSoftLimitZ2 = data.SoftLimitZ2;

                // 파라미터 설정 로드
                if (!string.IsNullOrEmpty(data.Acceleration))
                    _persistentAcceleration = data.Acceleration;
                if (!string.IsNullOrEmpty(data.Deceleration))
                    _persistentDeceleration = data.Deceleration;
                if (!string.IsNullOrEmpty(data.RetryCount))
                    _persistentRetryCount = data.RetryCount;
                if (!string.IsNullOrEmpty(data.OriginOffsetTheta))
                    _persistentOriginOffsetTheta = data.OriginOffsetTheta;
                if (!string.IsNullOrEmpty(data.ArmLinkLength))
                    _persistentArmLinkLength = data.ArmLinkLength;
                if (!string.IsNullOrEmpty(data.SystemSpeedMMS))
                    _persistentSystemSpeedMMS = data.SystemSpeedMMS;

                // Movement UI 방문 플래그 로드
                _hasMovementUiBeenOpened = data.HasMovementUiBeenOpened;

                System.Diagnostics.Debug.WriteLine("Setup: Persistent data loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Setup: Error loading persistent data: {ex.Message}");
            }
        }

        /// <summary>
        /// Setup UI의 현재 데이터를 PersistentDataManager용 형태로 변환
        /// </summary>
        public static PersistentDataManager.SetupDataContainer GetPersistentData()
        {
            try
            {
                var data = new PersistentDataManager.SetupDataContainer
                {
                    // 모드 설정
                    IsDemoMode = _persistentIsDemoMode,
                    IsInterlockEnabled = _persistentIsInterlockEnabled,

                    // HomePos
                    HomePosA = _persistentHomePosA,
                    HomePosT = _persistentHomePosT,
                    HomePosZ = _persistentHomePosZ,

                    // SoftLimit
                    SoftLimitA1 = _persistentSoftLimitA1,
                    SoftLimitA2 = _persistentSoftLimitA2,
                    SoftLimitT1 = _persistentSoftLimitT1,
                    SoftLimitT2 = _persistentSoftLimitT2,
                    SoftLimitZ1 = _persistentSoftLimitZ1,
                    SoftLimitZ2 = _persistentSoftLimitZ2,

                    // 파라미터 설정
                    Acceleration = _persistentAcceleration,
                    Deceleration = _persistentDeceleration,
                    RetryCount = _persistentRetryCount,
                    OriginOffsetTheta = _persistentOriginOffsetTheta,
                    ArmLinkLength = _persistentArmLinkLength,
                    SystemSpeedMMS = _persistentSystemSpeedMMS,

                    // Movement UI 방문 플래그
                    HasMovementUiBeenOpened = _hasMovementUiBeenOpened
                };

                System.Diagnostics.Debug.WriteLine("Setup: Persistent data prepared for saving");
                return data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Setup: Error preparing persistent data: {ex.Message}");
                return new PersistentDataManager.SetupDataContainer();
            }
        }

        /// <summary>
        /// 실시간 자동 저장 트리거
        /// </summary>
        private static async void TriggerAutoSave()
        {
            try
            {
                await PersistentDataManager.AutoSaveAsync(PersistentDataManager.DataType.Setup);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Setup: Auto-save error: {ex.Message}");
            }
        }
        #endregion
    }

    public class HomePosChangedEventArgs : EventArgs
    {
        public decimal PositionA { get; }
        public decimal PositionT { get; }
        public decimal PositionZ { get; }

        public HomePosChangedEventArgs(decimal posA, decimal posT, decimal posZ)
        {
            PositionA = posA;
            PositionT = posT;
            PositionZ = posZ;
        }
    }
}
