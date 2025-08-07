using System;
using System.Globalization; // 숫자 변환 시 CultureInfo 사용 가능
using System.Windows;
using TeachingPendant.Alarm;
using TeachingPendant.SetupUI; // Setup 클래스 접근을 위해 추가

namespace TeachingPendant
{
    public partial class SpeedParameterWindow : Window
    {
        #region Constructor
        public SpeedParameterWindow()
        {
            try
            {
                InitializeComponent();
                this.Loaded += SpeedParameterWindow_Loaded;
                // Closing 이벤트는 이미 기본 생성자에 있음
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SpeedParameterWindow Constructor error: {ex.Message}");
                MessageBox.Show($"SpeedParameterWindow 생성 오류: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SpeedParameterWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadAndDisplayCurrentParameters(); // 현재 저장된 파라미터 로드 및 표시
                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                    "Speed Parameter window opened - Ready for configuration");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SpeedParameterWindow_Loaded error: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Speed Parameter 초기화 오류: {ex.Message}");
            }
        }

        // InitializeSpeedParameters 메서드는 LoadAndDisplayCurrentParameters로 대체 또는 통합
        // private void InitializeSpeedParameters() { ... } 
        #endregion

        #region Private Helper Methods
        private void LoadAndDisplayCurrentParameters()
        {
            // Setup 클래스에 저장된 정적 값들을 읽어와 TextBox에 표시
            txtAcceleration.Text = Setup.Acceleration.ToString(CultureInfo.InvariantCulture);
            txtDeceleration.Text = Setup.Deceleration.ToString(CultureInfo.InvariantCulture);
            System.Diagnostics.Debug.WriteLine($"Loaded parameters: Accel={Setup.Acceleration}, Decel={Setup.Deceleration}");
        }

        private bool ValidateAndParseInputs(out double acceleration, out double deceleration)
        {
            acceleration = 0;
            deceleration = 0;

            bool isAccelValid = double.TryParse(txtAcceleration.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out acceleration);
            if (!isAccelValid || acceleration <= 0) // 가속도는 0보다 커야 함 (예시)
            {
                AlarmMessageManager.ShowAlarm(Alarms.INVALID_VALUE, "Invalid Acceleration value. Must be a positive number.");
                txtAcceleration.Focus();
                return false;
            }

            bool isDecelValid = double.TryParse(txtDeceleration.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out deceleration);
            if (!isDecelValid || deceleration <= 0) // 감속도도 0보다 커야 함 (예시)
            {
                AlarmMessageManager.ShowAlarm(Alarms.INVALID_VALUE, "Invalid Deceleration value. Must be a positive number.");
                txtDeceleration.Focus();
                return false;
            }
            return true;
        }
        #endregion

        #region Event Handlers
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ValidateAndParseInputs(out double accel, out double decel))
                {
                    // 유효한 값이면 Setup 클래스의 정적 프로퍼티를 통해 값 업데이트
                    Setup.Acceleration = accel;
                    Setup.Deceleration = decel;

                    AlarmMessageManager.ShowAlarm(Alarms.POSITION_SAVED,
                        $"Speed parameters applied: Accel={accel}, Decel={decel}");
                    System.Diagnostics.Debug.WriteLine($"Applied parameters: Accel={Setup.Acceleration}, Decel={Setup.Deceleration}");
                    // (선택) 성공적으로 적용 후 창을 닫고 싶다면 this.Close(); 추가
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyButton_Click error: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Apply error: {ex.Message}");
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 기본값으로 리셋 (Setup 클래스에 기본값을 정의하거나 여기서 직접 설정)
                double defaultAccel = 1000.0; // 예시 기본값
                double defaultDecel = 1000.0; // 예시 기본값

                Setup.Acceleration = defaultAccel;
                Setup.Deceleration = defaultDecel;

                // UI 업데이트
                LoadAndDisplayCurrentParameters();

                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                    "Speed parameters reset to default values.");
                System.Diagnostics.Debug.WriteLine($"Reset parameters: Accel={Setup.Acceleration}, Decel={Setup.Deceleration}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResetButton_Click error: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Reset error: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 변경사항이 있지만 적용하지 않은 경우 사용자에게 확인하는 로직 추가 가능
                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Speed Parameter window closed");
                this.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CloseButton_Click error: {ex.Message}");
                this.Close(); // 예외 발생 시에도 창은 닫히도록
            }
        }
        #endregion

        #region Window Events
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // AlarmMessageManager는 MainWindow 또는 CommonFrame에서 관리되므로 여기서는 중복 호출 방지
            // AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Speed Parameter configuration window closed");
            System.Diagnostics.Debug.WriteLine("SpeedParameterWindow instance closed.");
        }
        #endregion
    }
}
