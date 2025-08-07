using System;
using System.IO;
using System.Windows;
using TeachingPendant.Logging;

namespace TeachingPendant.ErrorHandling
{
    /// <summary>
    /// ErrorDialog.xaml에 대한 상호 작용 논리
    /// 사용자 친화적인 에러 다이얼로그
    /// C# 6.0 / .NET Framework 4.6.1 호환 버전 - 완전판
    /// </summary>
    public partial class ErrorDialog : Window
    {
        #region Private Fields
        private ErrorInfo _errorInfo;
        private ErrorDialogResult _result = ErrorDialogResult.Close;
        #endregion

        #region Constructor
        public ErrorDialog(ErrorInfo errorInfo)
        {
            InitializeComponent();
            _errorInfo = errorInfo;
            InitializeDialog();
        }
        #endregion

        #region Properties
        /// <summary>
        /// 사용자가 선택한 결과
        /// </summary>
        public ErrorDialogResult Result
        {
            get { return _result; }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 다이얼로그 초기화
        /// </summary>
        private void InitializeDialog()
        {
            try
            {
                // 시간 표시
                txtErrorTime.Text = _errorInfo.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

                // 오류 유형에 따른 아이콘 설정
                SetErrorIcon();

                // 메시지 설정
                txtUserMessage.Text = _errorInfo.UserMessage;
                txtSuggestedAction.Text = _errorInfo.SuggestedAction;

                // 기술적 세부사항
                txtExceptionType.Text = _errorInfo.Exception.GetType().FullName;
                txtTechnicalMessage.Text = _errorInfo.TechnicalMessage;
                txtLocation.Text = GetExceptionLocation();

                // 복구 가능 여부에 따른 버튼 표시
                ConfigureButtons();

                Logger.Info("ErrorDialog", "InitializeDialog", "에러 다이얼로그 초기화 완료");
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorDialog", "InitializeDialog", "다이얼로그 초기화 중 오류", ex);
            }
        }

        /// <summary>
        /// 오류 유형에 따른 아이콘 설정
        /// </summary>
        private void SetErrorIcon()
        {
            try
            {
                if (_errorInfo.IsRecoverable)
                {
                    txtErrorTitle.Text = "주의 - 복구 가능한 오류";
                    // 아이콘 숨김 처리 (SystemIcons 대신)
                    imgErrorIcon.Visibility = Visibility.Collapsed;
                }
                else
                {
                    txtErrorTitle.Text = "치명적 오류";
                    // 아이콘 숨김 처리 (SystemIcons 대신)
                    imgErrorIcon.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorDialog", "SetErrorIcon", "아이콘 설정 실패", ex);
                imgErrorIcon.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 예외 발생 위치 추출
        /// </summary>
        private string GetExceptionLocation()
        {
            try
            {
                if (_errorInfo.Exception.StackTrace != null)
                {
                    var lines = _errorInfo.Exception.StackTrace.Split('\n');
                    if (lines.Length > 0)
                    {
                        var firstLine = lines[0].Trim();
                        var atIndex = firstLine.IndexOf("at ");
                        var inIndex = firstLine.IndexOf(" in ");

                        if (atIndex >= 0 && inIndex > atIndex)
                        {
                            return firstLine.Substring(atIndex + 3, inIndex - atIndex - 3);
                        }
                        else if (atIndex >= 0)
                        {
                            return firstLine.Substring(atIndex + 3);
                        }
                    }
                }

                return "알 수 없는 위치";
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorDialog", "GetExceptionLocation", "위치 추출 실패", ex);
                return "위치 정보 없음";
            }
        }

        /// <summary>
        /// 복구 가능 여부에 따른 버튼 구성
        /// </summary>
        private void ConfigureButtons()
        {
            if (_errorInfo.IsRecoverable)
            {
                // 복구 가능한 오류 - 다시 시도, 무시 버튼 표시
                btnRetry.Visibility = Visibility.Visible;
                btnIgnore.Visibility = Visibility.Visible;
                btnClose.Content = "종료";
            }
            else
            {
                // 치명적 오류 - 종료 버튼만 표시
                btnRetry.Visibility = Visibility.Collapsed;
                btnIgnore.Visibility = Visibility.Collapsed;
                btnClose.Content = "애플리케이션 종료";
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 세부사항 복사 버튼 클릭
        /// </summary>
        private void BtnCopyDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var details = GenerateErrorReport();
                Clipboard.SetText(details);

                MessageBox.Show("오류 세부사항이 클립보드에 복사되었습니다.",
                    "복사 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                Logger.Info("ErrorDialog", "CopyDetails", "오류 세부사항 클립보드 복사 완료");
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorDialog", "CopyDetails", "클립보드 복사 실패", ex);
                MessageBox.Show("클립보드 복사 중 오류가 발생했습니다.",
                    "복사 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 다시 시도 버튼 클릭
        /// </summary>
        private void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            _result = ErrorDialogResult.Retry;
            Logger.Info("ErrorDialog", "Retry", "사용자가 다시 시도 선택");

            // 추가 옵션 처리
            ProcessAdditionalOptions();

            this.DialogResult = true;
            this.Close();
        }

        /// <summary>
        /// 무시하고 계속 버튼 클릭
        /// </summary>
        private void BtnIgnore_Click(object sender, RoutedEventArgs e)
        {
            _result = ErrorDialogResult.Ignore;
            Logger.Info("ErrorDialog", "Ignore", "사용자가 무시하고 계속 선택");

            // 추가 옵션 처리
            ProcessAdditionalOptions();

            this.DialogResult = true;
            this.Close();
        }

        /// <summary>
        /// 종료 버튼 클릭
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _result = ErrorDialogResult.Close;
            Logger.Info("ErrorDialog", "Close", "사용자가 애플리케이션 종료 선택");

            // 추가 옵션 처리
            ProcessAdditionalOptions();

            this.DialogResult = false;
            this.Close();
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// 오류 보고서 생성
        /// </summary>
        private string GenerateErrorReport()
        {
            var report = "=== TeachingPendant 오류 보고서 ===" + Environment.NewLine;
            report += "발생 시간: " + _errorInfo.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine;
            report += "복구 가능: " + (_errorInfo.IsRecoverable ? "예" : "아니오") + Environment.NewLine;
            report += Environment.NewLine;

            report += "=== 사용자 메시지 ===" + Environment.NewLine;
            report += _errorInfo.UserMessage + Environment.NewLine;
            report += Environment.NewLine;

            report += "=== 권장 조치 ===" + Environment.NewLine;
            report += _errorInfo.SuggestedAction + Environment.NewLine;
            report += Environment.NewLine;

            report += "=== 기술적 세부사항 ===" + Environment.NewLine;
            report += "예외 유형: " + _errorInfo.Exception.GetType().FullName + Environment.NewLine;
            report += "메시지: " + _errorInfo.Exception.Message + Environment.NewLine;
            report += "발생 위치: " + GetExceptionLocation() + Environment.NewLine;
            report += Environment.NewLine;

            if (!string.IsNullOrEmpty(_errorInfo.Exception.StackTrace))
            {
                report += "=== 스택 트레이스 ===" + Environment.NewLine;
                report += _errorInfo.Exception.StackTrace + Environment.NewLine;
            }

            report += Environment.NewLine;
            report += "=== 시스템 정보 ===" + Environment.NewLine;
            report += "OS: " + Environment.OSVersion.ToString() + Environment.NewLine;
            report += ".NET 버전: " + Environment.Version.ToString() + Environment.NewLine;
            report += "작업 디렉토리: " + Environment.CurrentDirectory + Environment.NewLine;
            report += "로그 디렉토리: " + Logger.GetLogDirectory() + Environment.NewLine;

            return report;
        }

        /// <summary>
        /// 추가 옵션 처리
        /// </summary>
        private void ProcessAdditionalOptions()
        {
            try
            {
                // 오류 보고서 생성
                if (chkSendReport.IsChecked == true)
                {
                    SaveErrorReport();
                }

                // 로그 폴더 열기
                if (chkOpenLogFolder.IsChecked == true)
                {
                    OpenLogFolder();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorDialog", "ProcessAdditionalOptions", "추가 옵션 처리 중 오류", ex);
            }
        }

        /// <summary>
        /// 오류 보고서 파일로 저장
        /// </summary>
        private void SaveErrorReport()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = "ErrorReport_" + timestamp + ".txt";
                var logDir = Logger.GetLogDirectory();
                var reportPath = Path.Combine(logDir, fileName);

                var report = GenerateErrorReport();
                File.WriteAllText(reportPath, report);

                Logger.Info("ErrorDialog", "SaveErrorReport", "오류 보고서 저장 완료: " + reportPath);

                MessageBox.Show("오류 보고서가 저장되었습니다:\n" + reportPath,
                    "보고서 저장", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorDialog", "SaveErrorReport", "오류 보고서 저장 실패", ex);
                MessageBox.Show("오류 보고서 저장 중 문제가 발생했습니다.",
                    "저장 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 로그 폴더 열기
        /// </summary>
        private void OpenLogFolder()
        {
            try
            {
                var logDir = Logger.GetLogDirectory();
                if (Directory.Exists(logDir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logDir);
                    Logger.Info("ErrorDialog", "OpenLogFolder", "로그 폴더 열기 완료");
                }
                else
                {
                    MessageBox.Show("로그 폴더를 찾을 수 없습니다: " + logDir,
                        "폴더 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ErrorDialog", "OpenLogFolder", "로그 폴더 열기 실패", ex);
                MessageBox.Show("로그 폴더를 열 수 없습니다.",
                    "열기 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion
    }

    #region Enums
    /// <summary>
    /// 에러 다이얼로그 결과
    /// </summary>
    public enum ErrorDialogResult
    {
        /// <summary>
        /// 다시 시도
        /// </summary>
        Retry,

        /// <summary>
        /// 무시하고 계속
        /// </summary>
        Ignore,

        /// <summary>
        /// 애플리케이션 종료
        /// </summary>
        Close
    }
    #endregion

    #region Extension Methods - 제거됨
    // SystemIcons 관련 코드 제거
    #endregion
}