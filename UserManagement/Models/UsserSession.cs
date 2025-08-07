using System;
using System.Collections.Generic;
using System.Linq;

namespace TeachingPendant.UserManagement.Models
{
    /// <summary>
    /// 사용자 세션 변경 이벤트 인자
    /// </summary>
    public class UserSessionChangedEventArgs : EventArgs
    {
        public User OldUser { get; set; }
        public User NewUser { get; set; }
        public DateTime ChangeTime { get; set; }
        public string ChangeReason { get; set; }

        public UserSessionChangedEventArgs(User oldUser, User newUser, string changeReason)
        {
            OldUser = oldUser;
            NewUser = newUser;
            ChangeTime = DateTime.Now;
            ChangeReason = changeReason;
        }
    }

    /// <summary>
    /// 현재 로그인한 사용자의 세션 정보를 관리하는 전역 클래스
    /// GlobalModeManager 패턴을 참고하여 설계
    /// </summary>
    public static class UserSession
    {
        #region Private Fields
        private static readonly string CLASS_NAME = "UserSession";
        private static User _currentUser = null;
        private static DateTime _loginTime = DateTime.MinValue;
        private static DateTime _lastActivityTime = DateTime.MinValue;
        private static List<Permission> _currentPermissions = new List<Permission>();
        private static readonly object _lockObject = new object();

        // 세션 타임아웃 설정 (분)
        private static readonly int SESSION_TIMEOUT_MINUTES = 30;
        #endregion

        #region Events
        /// <summary>
        /// 사용자 세션이 변경될 때 발생하는 이벤트
        /// </summary>
        public static event EventHandler<UserSessionChangedEventArgs> UserSessionChanged;

        /// <summary>
        /// 세션 타임아웃이 발생할 때 발생하는 이벤트
        /// </summary>
        public static event EventHandler SessionTimeout;
        #endregion

        #region Properties
        /// <summary>
        /// 현재 로그인한 사용자 (읽기 전용)
        /// </summary>
        public static User CurrentUser
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentUser;
                }
            }
        }

        /// <summary>
        /// 사용자가 로그인되어 있는지 여부
        /// </summary>
        public static bool IsLoggedIn
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentUser != null && !IsSessionExpired();
                }
            }
        }

        /// <summary>
        /// 현재 사용자의 역할
        /// </summary>
        public static UserRole CurrentUserRole
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentUser?.Role ?? UserRole.Guest;
                }
            }
        }

        /// <summary>
        /// 현재 사용자 이름
        /// </summary>
        public static string CurrentUserName
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentUser?.UserName ?? "Guest";
                }
            }
        }

        /// <summary>
        /// 현재 사용자 ID
        /// </summary>
        public static string CurrentUserId
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentUser?.UserId ?? "guest";
                }
            }
        }

        /// <summary>
        /// 로그인 시간
        /// </summary>
        public static DateTime LoginTime
        {
            get
            {
                lock (_lockObject)
                {
                    return _loginTime;
                }
            }
        }

        /// <summary>
        /// 마지막 활동 시간
        /// </summary>
        public static DateTime LastActivityTime
        {
            get
            {
                lock (_lockObject)
                {
                    return _lastActivityTime;
                }
            }
        }

        /// <summary>
        /// 세션 지속 시간
        /// </summary>
        public static TimeSpan SessionDuration
        {
            get
            {
                lock (_lockObject)
                {
                    if (_currentUser == null) return TimeSpan.Zero;
                    return DateTime.Now - _loginTime;
                }
            }
        }

        /// <summary>
        /// 세션 만료까지 남은 시간
        /// </summary>
        public static TimeSpan TimeUntilExpiry
        {
            get
            {
                lock (_lockObject)
                {
                    if (_currentUser == null) return TimeSpan.Zero;
                    var expiryTime = _lastActivityTime.AddMinutes(SESSION_TIMEOUT_MINUTES);
                    var remaining = expiryTime - DateTime.Now;
                    return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                }
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 사용자 로그인 처리
        /// </summary>
        /// <param name="user">로그인할 사용자</param>
        /// <returns>로그인 성공 여부</returns>
        public static bool Login(User user)
        {
            try
            {
                if (user == null || !user.IsValid())
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그인 실패: 유효하지 않은 사용자 정보");
                    return false;
                }

                if (user.IsLocked())
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그인 실패: 계정 잠금 상태 - {user.UserId}");
                    return false;
                }

                if (!user.IsActive)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그인 실패: 비활성화된 계정 - {user.UserId}");
                    return false;
                }

                User oldUser;
                lock (_lockObject)
                {
                    oldUser = _currentUser;
                    _currentUser = user;
                    _loginTime = DateTime.Now;
                    _lastActivityTime = DateTime.Now;

                    // 사용자 권한 로드
                    LoadUserPermissions(user);
                }

                // 사용자 로그인 성공 처리
                user.OnLoginSuccess();

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그인 성공: {user.UserId} ({user.UserName}) - {user.Role}");

                // 이벤트 발생
                try
                {
                    UserSessionChanged?.Invoke(null, new UserSessionChangedEventArgs(oldUser, user, "Login"));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그인 이벤트 발생 실패: {ex.Message}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그인 처리 중 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 사용자 로그아웃 처리
        /// </summary>
        /// <param name="reason">로그아웃 이유</param>
        public static void Logout(string reason = "Manual Logout")
        {
            try
            {
                User oldUser;
                lock (_lockObject)
                {
                    oldUser = _currentUser;
                    _currentUser = null;
                    _loginTime = DateTime.MinValue;
                    _lastActivityTime = DateTime.MinValue;
                    _currentPermissions.Clear();
                }

                if (oldUser != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그아웃: {oldUser.UserId} - {reason}");

                    // 이벤트 발생
                    try
                    {
                        UserSessionChanged?.Invoke(null, new UserSessionChangedEventArgs(oldUser, null, reason));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그아웃 이벤트 발생 실패: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그아웃 처리 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 사용자 활동 갱신 (세션 타임아웃 방지)
        /// </summary>
        public static void UpdateActivity()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_currentUser != null)
                    {
                        _lastActivityTime = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 활동 갱신 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 세션 만료 확인
        /// </summary>
        /// <returns>만료 여부</returns>
        public static bool IsSessionExpired()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_currentUser == null) return true;

                    var timeSinceLastActivity = DateTime.Now - _lastActivityTime;
                    return timeSinceLastActivity.TotalMinutes > SESSION_TIMEOUT_MINUTES;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 세션 만료 확인 실패: {ex.Message}");
                return true; // 오류 시 안전하게 만료 처리
            }
        }

        /// <summary>
        /// 세션 만료 처리
        /// </summary>
        public static void HandleSessionExpiry()
        {
            try
            {
                if (IsSessionExpired() && _currentUser != null)
                {
                    var expiredUser = _currentUser;
                    Logout("Session Timeout");

                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 세션 만료로 자동 로그아웃: {expiredUser.UserId}");

                    // 세션 타임아웃 이벤트 발생
                    try
                    {
                        SessionTimeout?.Invoke(null, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 세션 타임아웃 이벤트 발생 실패: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 세션 만료 처리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 특정 권한 보유 여부 확인
        /// </summary>
        /// <param name="permissionId">권한 ID</param>
        /// <returns>권한 보유 여부</returns>
        public static bool HasPermission(string permissionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(permissionId)) return false;

                lock (_lockObject)
                {
                    if (_currentUser == null) return false;

                    // 세션 만료 확인
                    if (IsSessionExpired())
                    {
                        HandleSessionExpiry();
                        return false;
                    }

                    // 활동 갱신
                    UpdateActivity();

                    // 권한 확인
                    return _currentPermissions.Any(p =>
                        string.Equals(p.PermissionId, permissionId, StringComparison.OrdinalIgnoreCase) &&
                        p.IsAllowed);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 권한 확인 실패: {ex.Message}");
                return false; // 오류 시 안전하게 거부
            }
        }

        /// <summary>
        /// 화면 접근 권한 확인 (기존 ValidateScreenAccess와 연동 예정)
        /// </summary>
        /// <param name="screenName">화면 이름</param>
        /// <returns>접근 가능 여부</returns>
        public static bool CanAccessScreen(string screenName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(screenName)) return false;

                lock (_lockObject)
                {
                    if (_currentUser == null) return false;

                    // UserRole의 확장 메서드 사용
                    return _currentUser.Role.CanAccessScreen(screenName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 화면 접근 권한 확인 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 현재 사용자의 모든 권한 조회
        /// </summary>
        /// <returns>권한 목록</returns>
        public static List<Permission> GetCurrentPermissions()
        {
            try
            {
                lock (_lockObject)
                {
                    return new List<Permission>(_currentPermissions);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 권한 목록 조회 실패: {ex.Message}");
                return new List<Permission>();
            }
        }

        /// <summary>
        /// 세션 정보 요약
        /// </summary>
        /// <returns>세션 정보 문자열</returns>
        public static string GetSessionSummary()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_currentUser == null)
                        return "로그인되지 않음";

                    var duration = SessionDuration;
                    var remaining = TimeUntilExpiry;

                    return $"{_currentUser.UserName} ({_currentUser.Role}) | " +
                           $"세션: {duration:hh\\:mm\\:ss} | " +
                           $"남은시간: {remaining:hh\\:mm\\:ss}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 세션 요약 생성 실패: {ex.Message}");
                return "세션 정보 오류";
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 사용자 권한 로드
        /// </summary>
        /// <param name="user">사용자</param>
        private static void LoadUserPermissions(User user)
        {
            try
            {
                _currentPermissions.Clear();

                // 모든 기본 권한 가져오기
                var allPermissions = Permission.GetDefaultPermissions();

                // 사용자 역할에 따른 권한 설정
                foreach (var permission in allPermissions)
                {
                    permission.IsAllowed = DeterminePermissionForRole(user.Role, permission);
                    _currentPermissions.Add(permission);
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 권한 로드 완료: {user.UserId} - {_currentPermissions.Count(p => p.IsAllowed)}개 권한 허용");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 권한 로드 실패: {ex.Message}");
                _currentPermissions.Clear();
            }
        }

        /// <summary>
        /// 역할별 권한 결정
        /// </summary>
        /// <param name="role">사용자 역할</param>
        /// <param name="permission">권한</param>
        /// <returns>허용 여부</returns>
        private static bool DeterminePermissionForRole(UserRole role, Permission permission)
        {
            try
            {
                switch (role)
                {
                    case UserRole.Guest:
                        // 게스트는 읽기 전용 권한만
                        return permission.Category == Permission.CATEGORY_SCREEN &&
                               (permission.PermissionId == "SCREEN_MONITOR" || permission.PermissionId == "SCREEN_HELP");

                    case UserRole.Operator:
                        // 운영자는 기본 운영 권한
                        return permission.Category == Permission.CATEGORY_SCREEN ||
                               permission.Category == Permission.CATEGORY_ROBOT ||
                               permission.Category == Permission.CATEGORY_RECIPE ||
                               (permission.Category == Permission.CATEGORY_TEACHING && permission.PermissionId == "TEACHING_VIEW") ||
                               (permission.Category == Permission.CATEGORY_SAFETY && permission.PermissionId == "SAFETY_VIEW");

                    case UserRole.Engineer:
                        // 엔지니어는 고급 권한 (관리자 전용 제외)
                        return permission.Category != Permission.CATEGORY_USER ||
                               !permission.PermissionId.Contains("SYSTEM_FIRMWARE") ||
                               !permission.PermissionId.Contains("SAFETY_OVERRIDE");

                    case UserRole.Administrator:
                        // 관리자는 모든 권한
                        return true;

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 역할별 권한 결정 실패: {ex.Message}");
                return false;
            }
        }
        #endregion
    }
}