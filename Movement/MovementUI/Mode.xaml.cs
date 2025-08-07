using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TeachingPendant.Alarm;

namespace TeachingPendant.MovementUI
{
    // 모드 선택 이벤트 인자
    public class ModeSelectedEventArgs : EventArgs
    {
        public string SelectedMode { get; private set; }

        public ModeSelectedEventArgs(string selectedMode)
        {
            SelectedMode = selectedMode;
        }
    }

    public partial class Mode : UserControl
    {
        // 선택된 모드를 외부로 알리는 이벤트
        public event EventHandler<ModeSelectedEventArgs> ModeSelected;

        // 현재 선택된 모드
        private string _currentMode = "Jog";

        // 버튼 색상
        private SolidColorBrush _selectedBrush = new SolidColorBrush(Colors.LightBlue);
        private SolidColorBrush _normalBrush = new SolidColorBrush(Colors.LightGray);

        public Mode()
        {
            InitializeComponent();

            // 초기 버튼 상태 설정 - Jog 모드가 기본
            UpdateButtonStatus();
        }

        // Jog 버튼 클릭 이벤트
        private void btnJog_Click(object sender, RoutedEventArgs e)
        {
            AlarmMessageManager.ShowCustomMessage("Jog button clicked", AlarmCategory.Information);

            // 현재 모드와 다를 때만 실행
            if (_currentMode != "Jog")
            {
                _currentMode = "Jog";
                UpdateButtonStatus();

                // 이벤트 발생
                ModeSelected?.Invoke(this, new ModeSelectedEventArgs(_currentMode));

                // 알람 메시지 표시
                AlarmMessageManager.ShowAlarm(Alarms.MODE_CHANGED, "Jog mode activated");

                // 부모 창 닫기 (팝업인 경우)
                Window.GetWindow(this)?.Close();
            }
            else
            {
                // 이미 Jog 모드인 경우에도 이벤트 발생하도록 강제 처리
                ModeSelected?.Invoke(this, new ModeSelectedEventArgs(_currentMode));

                // 알람 메시지 표시
                AlarmMessageManager.ShowAlarm(Alarms.MODE_CHANGED, "Already in Jog mode");

                // 부모 창 닫기
                Window.GetWindow(this)?.Close();
            }
        }

        // Inching 버튼 클릭 이벤트
        private void btnInching_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMode != "Inching")
            {
                _currentMode = "Inching";
                UpdateButtonStatus();

                // 이벤트 발생
                ModeSelected?.Invoke(this, new ModeSelectedEventArgs(_currentMode));

                // 알람 메시지 표시
                AlarmMessageManager.ShowAlarm(Alarms.MODE_CHANGED, "Inching mode activated");

                // 부모 창 닫기 (팝업인 경우)
                Window.GetWindow(this)?.Close();
            }
        }

        // Abs 버튼 클릭 이벤트
        private void btnAbs_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMode != "Abs")
            {
                _currentMode = "Abs";
                UpdateButtonStatus();

                // 이벤트 발생
                ModeSelected?.Invoke(this, new ModeSelectedEventArgs(_currentMode));

                // 알람 메시지 표시
                AlarmMessageManager.ShowAlarm(Alarms.MODE_CHANGED, "Absolute coordinate mode activated");

                // 부모 창 닫기 (팝업인 경우)
                Window.GetWindow(this)?.Close();
            }
        }

        // Total 버튼 클릭 이벤트
        private void btnTotal_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMode != "Total")
            {
                _currentMode = "Total";
                UpdateButtonStatus();

                // 이벤트 발생
                ModeSelected?.Invoke(this, new ModeSelectedEventArgs(_currentMode));

                // 알람 메시지 표시
                AlarmMessageManager.ShowAlarm(Alarms.MODE_CHANGED, "Total mode activated");

                // 부모 창 닫기 (팝업인 경우)
                Window.GetWindow(this)?.Close();
            }
        }

        // 버튼 상태 업데이트
        private void UpdateButtonStatus()
        {
            // 모든 버튼 기본 상태로
            btnJog.Background = _normalBrush;
            btnInching.Background = _normalBrush;
            btnAbs.Background = _normalBrush;
            btnTotal.Background = _normalBrush;

            // 선택된 버튼 강조
            switch (_currentMode)
            {
                case "Jog":
                    btnJog.Background = _selectedBrush;
                    break;
                case "Inching":
                    btnInching.Background = _selectedBrush;
                    break;
                case "Abs":
                    btnAbs.Background = _selectedBrush;
                    break;
                case "Total":
                    btnTotal.Background = _selectedBrush;
                    break;
            }
        }
    }
}
