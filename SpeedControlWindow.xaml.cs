using System;
using System.Windows;
using System.Windows.Controls;
using TeachingPendant.Alarm;
using TeachingPendant.Manager;

namespace TeachingPendant
{
    public partial class SpeedControlWindow : Window
    {
        #region Fields
        private bool _isUpdatingFromSlider = false;
        private bool _isUpdatingFromTextBox = false;
        #endregion

        #region Events
        /// <summary>
        /// 속도 변경 이벤트
        /// </summary>
        public event EventHandler<int> SpeedChanged;
        #endregion

        #region Constructor
        public SpeedControlWindow()
        {
            try
            {
                InitializeComponent();

                // 로드 완료 후 초기화
                this.Loaded += SpeedControlWindow_Loaded;
                this.Closing += SpeedControlWindow_Closing;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SpeedControlWindow Constructor error: {ex.Message}");
                MessageBox.Show($"SpeedControlWindow 생성 오류: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SpeedControlWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                InitializeSpeed();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SpeedControlWindow_Loaded error: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Speed Control 초기화 오류: {ex.Message}");
            }
        }

        private void SpeedControlWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 정리 작업
            SpeedChanged = null;
            System.Diagnostics.Debug.WriteLine("SpeedControlWindow cleanup completed");
        }

        private void InitializeSpeed()
        {
            // 컨트롤 존재 확인
            if (speedSlider == null || txtSpeedValue == null)
            {
                System.Diagnostics.Debug.WriteLine("Controls not found in SpeedControlWindow");
                return;
            }

            // 현재 글로벌 속도 설정 가져오기
            int currentSpeed = GlobalSpeedManager.CurrentSpeed;

            _isUpdatingFromSlider = true;
            _isUpdatingFromTextBox = true;

            speedSlider.Value = currentSpeed;
            txtSpeedValue.Text = currentSpeed.ToString();

            _isUpdatingFromSlider = false;
            _isUpdatingFromTextBox = false;

            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                $"Speed Control opened - Current: {currentSpeed}%");
        }
        #endregion

        #region Event Handlers
        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingFromSlider || txtSpeedValue == null) return;

            try
            {
                _isUpdatingFromTextBox = true;

                int speedValue = (int)speedSlider.Value;
                txtSpeedValue.Text = speedValue.ToString();

                _isUpdatingFromTextBox = false;

                // 실시간으로 속도 업데이트
                UpdateSpeed(speedValue);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SpeedSlider_ValueChanged error: {ex.Message}");
                _isUpdatingFromTextBox = false;
            }
        }

        private void TxtSpeedValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingFromTextBox || speedSlider == null || txtSpeedValue == null) return;

            try
            {
                if (int.TryParse(txtSpeedValue.Text, out int speedValue))
                {
                    // 범위 체크
                    if (speedValue < 1) speedValue = 1;
                    if (speedValue > 200) speedValue = 200;

                    _isUpdatingFromSlider = true;

                    speedSlider.Value = speedValue;

                    // 텍스트박스 값도 범위에 맞게 수정
                    if (txtSpeedValue.Text != speedValue.ToString())
                    {
                        txtSpeedValue.Text = speedValue.ToString();
                        txtSpeedValue.SelectionStart = txtSpeedValue.Text.Length;
                    }

                    _isUpdatingFromSlider = false;

                    // 실시간으로 속도 업데이트
                    UpdateSpeed(speedValue);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TxtSpeedValue_TextChanged error: {ex.Message}");
                _isUpdatingFromSlider = false;
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (speedSlider == null || txtSpeedValue == null) return;

                _isUpdatingFromSlider = true;
                _isUpdatingFromTextBox = true;

                speedSlider.Value = 100;
                txtSpeedValue.Text = "100";

                _isUpdatingFromSlider = false;
                _isUpdatingFromTextBox = false;

                UpdateSpeed(100);
                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Speed reset to 100%");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResetButton_Click error: {ex.Message}");
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (speedSlider == null) return;

                int currentSpeed = (int)speedSlider.Value;
                UpdateSpeed(currentSpeed);
                AlarmMessageManager.ShowAlarm(Alarms.POSITION_SAVED,
                    $"Speed applied: {currentSpeed}% - All Movement coordinates updated");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyButton_Click error: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Speed Control closed");
                this.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CloseButton_Click error: {ex.Message}");
                this.Close();
            }
        }
        #endregion

        #region Private Methods
        private void UpdateSpeed(int speedValue)
        {
            // 글로벌 속도 설정 업데이트
            GlobalSpeedManager.SetSpeed(speedValue);

            // 속도 변경 이벤트 발생
            SpeedChanged?.Invoke(this, speedValue);

            System.Diagnostics.Debug.WriteLine($"Speed updated to {speedValue}%");
        }
        #endregion

        #region Window Events
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "Speed Control window closed");
        }
        #endregion
    }
}
