using System;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using TeachingPendant.HardwareControllers;

namespace TeachingPendant.Windows
{
    public partial class ComPortSettingsWindow : Window
    {
        private DTP7HCommunication _dtp7HController;

        public string SelectedPort { get; private set; }
        public int SelectedBaudRate { get; private set; }
        public bool IsAutoMode { get; private set; }
        public bool DialogResult { get; private set; }

        public ComPortSettingsWindow(DTP7HCommunication controller)
        {
            InitializeComponent();
            _dtp7HController = controller;
            LoadComPorts();
            UpdateStatus();
        }

        /// <summary>
        /// 사용 가능한 COM 포트 로드
        /// </summary>
        private void LoadComPorts()
        {
            try
            {
                ComPortComboBox.Items.Clear();
                string[] ports = SerialPort.GetPortNames();

                foreach (string port in ports.OrderBy(p => p))
                {
                    ComPortComboBox.Items.Add(port);
                }

                if (ComPortComboBox.Items.Count > 0)
                {
                    // COM5가 있으면 선택, 없으면 첫 번째 선택
                    var com5 = ComPortComboBox.Items.Cast<string>().FirstOrDefault(p => p == "COM5");
                    ComPortComboBox.SelectedItem = com5 ?? ComPortComboBox.Items[0];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"COM 포트 조회 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 연결 상태 업데이트
        /// </summary>
        private void UpdateStatus()
        {
            if (_dtp7HController != null && _dtp7HController.IsConnected)
            {
                StatusTextBlock.Text = "연결됨";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                ConnectButton.Content = "CLOSE";
            }
            else
            {
                StatusTextBlock.Text = "연결 안됨";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                ConnectButton.Content = "OPEN";
            }
        }

        /// <summary>
        /// 연결/해제 버튼 클릭
        /// </summary>
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dtp7HController.IsConnected)
                {
                    // 연결 해제
                    _dtp7HController.Disconnect();
                    MessageBox.Show("연결이 해제되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // 연결 시도
                    if (ComPortComboBox.SelectedItem == null)
                    {
                        MessageBox.Show("COM 포트를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    string selectedPort = ComPortComboBox.SelectedItem.ToString();
                    int baudRate = int.Parse(BaudRateComboBox.Text);

                    bool success = _dtp7HController.Connect(selectedPort, baudRate);

                    if (success)
                    {
                        MessageBox.Show($"{selectedPort}에 연결되었습니다.", "연결 성공",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"{selectedPort} 연결에 실패했습니다.", "연결 실패",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"연결 처리 중 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 저장 버튼 클릭
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ComPortComboBox.SelectedItem != null)
                {
                    SelectedPort = ComPortComboBox.SelectedItem.ToString();
                    SelectedBaudRate = int.Parse(BaudRateComboBox.Text);
                    IsAutoMode = AutoRadioButton.IsChecked == true;

                    MessageBox.Show("설정이 저장되었습니다.", "저장 완료",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 저장 중 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 포트 새로고침 버튼 클릭
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadComPorts();
            MessageBox.Show("COM 포트 목록을 새로고침했습니다.", "새로고침",
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 확인 버튼 클릭
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close();
        }

        /// <summary>
        /// 취소 버튼 클릭
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }
    }
}