using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using System.IO;
using TeachingPendant.Manager;

namespace TeachingPendant.MovementUI
{
    public partial class PhysicsTestResultWindow : Window
    {
        private List<SegmentPhysicsResultDisplay> _results;
        private string _groupName;
        private string _menuName;
        private double _acceleration;
        private double _deceleration;

        public PhysicsTestResultWindow(List<Movement.SegmentPhysicsResult> results,
            string groupName, string menuName, double acceleration, double deceleration)
        {
            InitializeComponent();

            _groupName = groupName;
            _menuName = menuName;
            _acceleration = acceleration;
            _deceleration = deceleration;

            txtGroup.Text = groupName;
            txtMenu.Text = menuName;
            txtAcceleration.Text = acceleration.ToString("F1");
            txtDeceleration.Text = deceleration.ToString("F1");
            txtPendantSpeed.Text = $"{GlobalSpeedManager.CurrentSpeed}%";
            txtTestDate.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            _results = results.Select(r => new SegmentPhysicsResultDisplay
            {
                SegmentName = r.SegmentName,
                StartPoint = r.StartPoint,
                EndPoint = r.EndPoint,
                Distance = r.Distance,
                TheoreticalMaxSpeed = r.TheoreticalMaxSpeed,
                FinalCommandSpeed = r.FinalCommandSpeed,
                StatusText = GetStatusText(r),
                ErrorMessage = !string.IsNullOrEmpty(r.LimitErrorMessage) ? r.LimitErrorMessage : (r.ErrorMessage ?? "")
            }).ToList();

            dgResults.ItemsSource = _results;
            this.Title = $"Physics Test Results - {groupName} {menuName}";
            ShowSummaryInfo();
        }

        private string GetStatusText(Movement.SegmentPhysicsResult result)
        {
            if (!string.IsNullOrEmpty(result.LimitErrorMessage)) return "Limit Error";
            if (!string.IsNullOrEmpty(result.ErrorMessage)) return "Calc Error";
            if (!result.IsValidSegment) return "Skip";
            if (result.Distance < 0.001) return "Too Short";
            return "Valid";
        }

        private void ShowSummaryInfo()
        {
            int validCount = _results.Count(r => r.StatusText == "Valid");
            int errorCount = _results.Count(r => r.StatusText.Contains("Error"));
            int skipCount = _results.Count(r => r.StatusText == "Skip" || r.StatusText == "Too Short");

            string summaryMessage = $"Calculation completed: Total {_results.Count} segments " +
                                   $"(Valid: {validCount}, Skip: {skipCount}, Error: {errorCount})";

            System.Diagnostics.Debug.WriteLine(summaryMessage);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ExportToCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"PhysicsTest_{_groupName}_{_menuName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportResultsToCsv(saveFileDialog.FileName);
                    MessageBox.Show($"Results successfully saved to:\n{saveFileDialog.FileName}",
                        "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error occurred while saving CSV: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportResultsToCsv(string filePath)
        {
            using (var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                // 헤더 정보
                writer.WriteLine("Physics Test Results");
                writer.WriteLine($"Group,{_groupName}");
                writer.WriteLine($"Menu,{_menuName}");
                writer.WriteLine($"Acceleration,{_acceleration}");
                writer.WriteLine($"Deceleration,{_deceleration}");
                writer.WriteLine($"Pendant Speed,{GlobalSpeedManager.CurrentSpeed}%");
                writer.WriteLine($"Test Date,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine();

                // 통계 정보
                int validCount = _results.Count(r => r.StatusText == "Valid");
                int errorCount = _results.Count(r => r.StatusText.Contains("Error"));
                int skipCount = _results.Count(r => r.StatusText == "Skip" || r.StatusText == "Too Short");

                writer.WriteLine("Summary");
                writer.WriteLine($"Total Segments,{_results.Count}");
                writer.WriteLine($"Valid Segments,{validCount}");
                writer.WriteLine($"Skipped Segments,{skipCount}");
                writer.WriteLine($"Error Segments,{errorCount}");
                writer.WriteLine();

                // 테이블 헤더
                writer.WriteLine("Segment,Start Point,End Point,Distance(mm),Theoretical Max Speed,Final Command Speed,Status,Error Message");

                // 데이터 행
                foreach (var result in _results)
                {
                    writer.WriteLine($"{result.SegmentName}," +
                                   $"\"{result.StartPoint}\"," +
                                   $"\"{result.EndPoint}\"," +
                                   $"{result.Distance:F2}," +
                                   $"{result.TheoreticalMaxSpeed:F2}," +
                                   $"{result.FinalCommandSpeed}," +
                                   $"{result.StatusText}," +
                                   $"\"{result.ErrorMessage.Replace("\"", "\"\"")}\"");
                }

                // 계산 공식 정보 추가
                writer.WriteLine();
                writer.WriteLine("Calculation Formulas");
                writer.WriteLine("Distance Calculation,Euclidean distance in 3D space (converted from A-T-Z to X-Y-Z)");
                writer.WriteLine("Theoretical Max Speed,c = sqrt((2*a*b*L)/(a+b))");
                writer.WriteLine("Final Command Speed,Applied with pendant speed setting and 1.4x conversion factor");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            System.Diagnostics.Debug.WriteLine("PhysicsTestResultWindow closed");
        }
    }

    /// <summary>
    /// DataGrid 표시용 결과 클래스
    /// </summary>
    public class SegmentPhysicsResultDisplay
    {
        public string SegmentName { get; set; }
        public string StartPoint { get; set; }
        public string EndPoint { get; set; }
        public double Distance { get; set; }
        public double TheoreticalMaxSpeed { get; set; }
        public int FinalCommandSpeed { get; set; }
        public string StatusText { get; set; }
        public string ErrorMessage { get; set; }
    }
}