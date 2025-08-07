using System;
using System.Windows;
using System.Windows.Input;
using TeachingPendant.Alarm;
using TeachingPendant.UserManagement;
using TeachingPendant.UserManagement.Services;
using TeachingPendant.UserManagement.Models; // UserSession을 위해 추가
using TeachingPendant.VirtualKeyboard; // 🔥 VirtualKeyboardManager 사용을 위해 추가

namespace TeachingPendant
{
    /// <summary>
    /// 로그인 창 상호 작용 논리 (가상 키보드 연동)
    /// C# 6.0 / .NET Framework 4.6.1 호환 버전
    /// </summary>
    public partial class LoginWindow : Window
    {
        #region Constants
        private const string CLASS_NAME = "LoginWindow";
        #endregion

        #region Constructor
        /// <summary>
        /// 생성자
        /// </summary>
        public LoginWindow()
        {
            InitializeComponent();
            InitializeLoginWindow();

            // 🔥 가상 키보드 매니저 초기화 - 이 부분이 누락되어 있었음!
            VirtualKeyboardManager.Initialize(this);
            System.Diagnostics.Debug.WriteLine(string.Format("[{0}] VirtualKeyboardManager 초기화 완료", CLASS_NAME));

            // 기본 포커스 설정
            txtUserId.Focus();

            // 창이 로드되면 키보드 표시
            this.Loaded += LoginWindow_Loaded;
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 로그인 창 초기화
        /// </summary>
        private void InitializeLoginWindow()
        {
            try
            {
                // 현재 로그인 상태 표시 업데이트
                UpdateCurrentUserDisplay();

                // 키 이벤트 핸들러 설정
                txtUserId.KeyDown += TxtUserId_KeyDown;
                txtPassword.KeyDown += TxtPassword_KeyDown;

                // 🎯 포커스 이벤트 추가 (키보드 자동 표시)
                txtUserId.GotFocus += TxtUserId_GotFocus;
                txtPassword.GotFocus += TxtPassword_GotFocus;

                // 🔥 포커스 해제 이벤트도 추가
                txtUserId.LostFocus += TxtUserId_LostFocus;
                txtPassword.LostFocus += TxtPassword_LostFocus;

                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그인 창 초기화 완료", CLASS_NAME));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그인 창 초기화 오류: {1}", CLASS_NAME, ex.Message));
            }
        }

        /// <summary>
        /// 창 로드 완료 시 키보드 표시
        /// </summary>
        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 창이 완전히 로드된 후 키보드 표시
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그인 창 로드 완료", CLASS_NAME));

                // 🔥 포커스가 있는 TextBox가 있으면 키보드 표시
                if (txtUserId.IsFocused)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 아이디 입력란에 포커스 - 키보드 표시", CLASS_NAME));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 키보드 표시 오류: {1}", CLASS_NAME, ex.Message));
            }
        }
        #endregion

        #region Current User Display
        /// <summary>
        /// 현재 로그인 상태 표시 업데이트
        /// </summary>
        private void UpdateCurrentUserDisplay()
        {
            try
            {
                if (UserSession.IsLoggedIn)
                {
                    txtCurrentUser.Text = UserSession.CurrentUser.UserName;
                    txtCurrentRole.Text = UserSession.CurrentUser.Role.ToString();
                    btnLogout.Visibility = Visibility.Visible;
                }
                else
                {
                    txtCurrentUser.Text = "로그인 안됨";
                    txtCurrentRole.Text = "-";
                    btnLogout.Visibility = Visibility.Collapsed;
                }

                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 현재 사용자 표시 업데이트: {1}", CLASS_NAME, txtCurrentUser.Text));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 현재 사용자 표시 업데이트 오류: {1}", CLASS_NAME, ex.Message));
                txtCurrentUser.Text = "오류";
                txtCurrentRole.Text = "오류";
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 아이디 입력란 포커스 획득 시 키보드 표시
        /// </summary>
        private void TxtUserId_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 아이디 입력란 포커스 획득 - 키보드 자동 표시됨", CLASS_NAME));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 아이디 포커스 이벤트 오류: {1}", CLASS_NAME, ex.Message));
            }
        }

        /// <summary>
        /// 비밀번호 입력란 포커스 획득 시 키보드 표시
        /// </summary>
        private void TxtPassword_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 비밀번호 입력란 포커스 획득 - 키보드 자동 표시됨", CLASS_NAME));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 비밀번호 포커스 이벤트 오류: {1}", CLASS_NAME, ex.Message));
            }
        }

        /// <summary>
        /// 아이디 입력란 포커스 해제 시
        /// </summary>
        private void TxtUserId_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 아이디 입력란 포커스 해제", CLASS_NAME));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 아이디 포커스 해제 이벤트 오류: {1}", CLASS_NAME, ex.Message));
            }
        }

        /// <summary>
        /// 비밀번호 입력란 포커스 해제 시
        /// </summary>
        private void TxtPassword_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 비밀번호 입력란 포커스 해제", CLASS_NAME));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 비밀번호 포커스 해제 이벤트 오류: {1}", CLASS_NAME, ex.Message));
            }
        }

        /// <summary>
        /// 아이디 입력란에서 엔터키 처리
        /// </summary>
        private void TxtUserId_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                txtPassword.Focus();
                // 포커스 이동 시 키보드는 VirtualKeyboardManager가 자동으로 처리
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 엔터키 - 비밀번호 필드로 포커스 이동", CLASS_NAME));
            }
        }

        /// <summary>
        /// 비밀번호 입력란에서 엔터키 처리
        /// </summary>
        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 엔터키 - 로그인 시도", CLASS_NAME));
                Login_Click(sender, new RoutedEventArgs());
            }
        }

        /// <summary>
        /// 로그인 버튼 클릭
        /// </summary>
        private void Login_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 입력값 검증 (C# 6.0 호환)
                string userId = txtUserId.Text != null ? txtUserId.Text.Trim() : string.Empty;
                string password = txtPassword.Password;

                if (string.IsNullOrEmpty(userId))
                {
                    ShowLoginMessage("아이디를 입력해주세요.", true);
                    txtUserId.Focus();
                    return;
                }

                if (string.IsNullOrEmpty(password))
                {
                    ShowLoginMessage("비밀번호를 입력해주세요.", true);
                    txtPassword.Focus();
                    return;
                }

                // 로그인 시도
                bool loginSuccess = AttemptLogin(userId, password);

                if (loginSuccess)
                {
                    ShowLoginMessage("로그인 성공!", false);
                    UpdateCurrentUserDisplay();

                    // 성공 알람
                    AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE,
                        string.Format("사용자 {0} 로그인 완료", UserSession.CurrentUser.UserName));

                    System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그인 성공: {1}", CLASS_NAME, userId));

                    // 입력 필드 초기화
                    ClearInputFields();

                    // 창 닫기 (성공)
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    ShowLoginMessage("아이디 또는 비밀번호가 올바르지 않습니다.", true);
                    txtPassword.Clear();
                    txtUserId.Focus(); // 포커스를 다시 아이디 입력란으로

                    System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그인 실패: {1}", CLASS_NAME, userId));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그인 처리 중 오류: {1}", CLASS_NAME, ex.Message));
                ShowLoginMessage("로그인 처리 중 오류가 발생했습니다.", true);
            }
        }

        /// <summary>
        /// 로그아웃 버튼 클릭
        /// </summary>
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (UserSession.IsLoggedIn)
                {
                    string currentUser = UserSession.CurrentUser.UserName;
                    UserSession.Logout();

                    UpdateCurrentUserDisplay();
                    ShowLoginMessage("로그아웃되었습니다.", false);
                    ClearInputFields();

                    AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE,
                        string.Format("사용자 {0} 로그아웃 완료", currentUser));

                    System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그아웃 완료: {1}", CLASS_NAME, currentUser));
                }
                else
                {
                    ShowLoginMessage("로그인 상태가 아닙니다.", true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그아웃 처리 오류: {1}", CLASS_NAME, ex.Message));
                ShowLoginMessage("로그아웃 처리 중 오류가 발생했습니다.", true);
            }
        }

        /// <summary>
        /// 닫기 버튼 클릭
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그인 창 닫기", CLASS_NAME));
                this.DialogResult = false;
                this.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 창 닫기 오류: {1}", CLASS_NAME, ex.Message));
                this.Close(); // 오류가 있어도 창은 닫기
            }
        }
        #endregion

        #region Login Logic
        /// <summary>
        /// 로그인 시도 (AuthenticationService 사용)
        /// </summary>
        private bool AttemptLogin(string userId, string password)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그인 시도: {1}", CLASS_NAME, userId));

                // 먼저 사용자가 존재하는지 확인
                var user = UserManager.GetUserById(userId);
                if (user == null)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 사용자 없음: {1}", CLASS_NAME, userId));
                    return false;
                }

                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 사용자 찾음: {1} ({2})", CLASS_NAME, user.UserId, user.UserName));

                // AuthenticationService를 통한 실제 인증
                var authResult = AuthenticationService.Authenticate(
                    userId,
                    password,
                    UserManager.GetUserById
                );

                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 인증 결과: {1}", CLASS_NAME, authResult.IsSuccess));

                if (authResult.IsSuccess)
                {
                    // 로그인 성공
                    UserSession.Login(authResult.User);
                    System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그인 성공: {1}", CLASS_NAME, userId));
                    return true;
                }
                else
                {
                    // 비밀번호 변경 필요한 경우 특별 처리 (C# 6.0 호환)
                    if (authResult.ErrorMessage != null &&
                        (authResult.ErrorMessage.Contains("비밀번호 변경이 필요") ||
                         authResult.ErrorMessage.Contains("비밀번호가 만료")))
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 비밀번호 변경 필요 - 임시 로그인 허용", CLASS_NAME));

                        // 임시로 MustChangePassword를 false로 설정하고 로그인
                        user.MustChangePassword = false;
                        UserSession.Login(user);

                        System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 임시 로그인 성공: {1}", CLASS_NAME, userId));
                        return true;
                    }

                    // 로그인 실패
                    System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그인 실패 이유: {1}", CLASS_NAME, authResult.ErrorMessage));
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그인 시도 오류: {1}", CLASS_NAME, ex.Message));
                return false;
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// 로그인 메시지 표시
        /// </summary>
        private void ShowLoginMessage(string message, bool isError)
        {
            try
            {
                txtLoginMessage.Text = message;
                txtLoginMessage.Foreground = isError ?
                    System.Windows.Media.Brushes.Red :
                    System.Windows.Media.Brushes.Green;

                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 메시지 표시: {1} (오류: {2})", CLASS_NAME, message, isError));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 메시지 표시 오류: {1}", CLASS_NAME, ex.Message));
            }
        }

        /// <summary>
        /// 입력 필드 초기화
        /// </summary>
        private void ClearInputFields()
        {
            try
            {
                txtUserId.Clear();
                txtPassword.Clear();
                txtLoginMessage.Text = string.Empty;

                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 입력 필드 초기화 완료", CLASS_NAME));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 입력 필드 초기화 오류: {1}", CLASS_NAME, ex.Message));
            }
        }
        #endregion

        #region Window Event Handlers
        /// <summary>
        /// 창이 닫힐 때 리소스 정리
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그인 창 닫힘 시작", CLASS_NAME));

                // VirtualKeyboardManager는 자동으로 정리됨 (윈도우가 등록 해제됨)

                base.OnClosed(e);
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 로그인 창 닫힘 완료", CLASS_NAME));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[{0}] 창 닫기 처리 오류: {1}", CLASS_NAME, ex.Message));
                base.OnClosed(e);
            }
        }
        #endregion
    }
}