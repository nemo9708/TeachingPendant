using System;
using System.Windows;
using TeachingPendant.HardwareControllers;

namespace TeachingPendant
{
    public partial class CoordinateEditWindow : Window
    {
        public Position EditedPosition { get; private set; }

        public CoordinateEditWindow(Position initialPosition)
        {
            InitializeComponent();
            try
            {
                txtR.Text = initialPosition?.R.ToString("F0") ?? "0";
                txtTheta.Text = initialPosition?.Theta.ToString("F0") ?? "0";
                txtZ.Text = initialPosition?.Z.ToString("F0") ?? "0";
            }
            catch (Exception ex)
            {
                MessageBox.Show("초기 좌표 로드 실패: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (double.TryParse(txtR.Text, out double r) &&
                    double.TryParse(txtTheta.Text, out double theta) &&
                    double.TryParse(txtZ.Text, out double z))
                {
                    EditedPosition = new Position(r, theta, z);
                    DialogResult = true;
                }
                else
                {
                    MessageBox.Show("유효한 좌표 값을 입력하세요.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("좌표 설정 중 오류: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}