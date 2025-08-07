using System;

namespace TeachingPendant.UserManagement.Models
{
    /// <summary>
    /// 사용자 정보를 관리하는 모델 클래스
    /// C# 6.0 호환 버전으로 작성
    /// </summary>
    public class User
    {
        #region Private Fields
        private static readonly string CLASS_NAME = "User";
        private string _userId;
        private string _userName;
        private string _passwordHash;
        private UserRole _role;
        private DateTime _createdAt;
        private DateTime _lastLoginAt;
        private bool _isActive;
        private int _failedLoginCount;
        private DateTime? _passwordChangedAt;
        private bool _mustChangePassword;
        #endregion

        #region Properties
        /// <summary>
        /// 사용자 고유 ID (로그인용)
        /// </summary>
        public string UserId
        {
            get { return _userId; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("사용자 ID는 비어있을 수 없습니다.");
                if (value.Length < 3 || value.Length > 20)
                    throw new ArgumentException("사용자 ID는 3-20자 사이여야 합니다.");
                _userId = value.Trim().ToLower();
            }
        }

        /// <summary>
        /// 사용자 표시 이름
        /// </summary>
        public string UserName
        {
            get { return _userName; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("사용자 이름은 비어있을 수 없습니다.");
                if (value.Length > 50)
                    throw new ArgumentException("사용자 이름은 50자를 초과할 수 없습니다.");
                _userName = value.Trim();
            }
        }

        /// <summary>
        /// 비밀번호 해시값 (SHA256 + Salt)
        /// </summary>
        public string PasswordHash
        {
            get { return _passwordHash; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("비밀번호 해시는 비어있을 수 없습니다.");
                _passwordHash = value;
            }
        }

        /// <summary>
        /// 사용자 권한 역할
        /// </summary>
        public UserRole Role
        {
            get { return _role; }
            set { _role = value; }
        }

        /// <summary>
        /// 계정 생성일
        /// </summary>
        public DateTime CreatedAt
        {
            get { return _createdAt; }
            set { _createdAt = value; }
        }

        /// <summary>
        /// 마지막 로그인 시간
        /// </summary>
        public DateTime LastLoginAt
        {
            get { return _lastLoginAt; }
            set { _lastLoginAt = value; }
        }

        /// <summary>
        /// 계정 활성화 상태
        /// </summary>
        public bool IsActive
        {
            get { return _isActive; }
            set { _isActive = value; }
        }

        /// <summary>
        /// 로그인 실패 횟수 (보안)
        /// </summary>
        public int FailedLoginCount
        {
            get { return _failedLoginCount; }
            set { _failedLoginCount = Math.Max(0, value); }
        }

        /// <summary>
        /// 비밀번호 변경일
        /// </summary>
        public DateTime? PasswordChangedAt
        {
            get { return _passwordChangedAt; }
            set { _passwordChangedAt = value; }
        }

        /// <summary>
        /// 비밀번호 변경 강제 여부
        /// </summary>
        public bool MustChangePassword
        {
            get { return _mustChangePassword; }
            set { _mustChangePassword = value; }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// 기본 생성자
        /// </summary>
        public User()
        {
            _createdAt = DateTime.Now;
            _lastLoginAt = DateTime.MinValue;
            _isActive = true;
            _failedLoginCount = 0;
            _mustChangePassword = true; // 초기 계정은 비밀번호 변경 강제
            _role = UserRole.Operator; // 기본 역할
        }

        /// <summary>
        /// 사용자 생성용 생성자
        /// </summary>
        /// <param name="userId">사용자 ID</param>
        /// <param name="userName">사용자 이름</param>
        /// <param name="passwordHash">비밀번호 해시</param>
        /// <param name="role">사용자 역할</param>
        public User(string userId, string userName, string passwordHash, UserRole role)
            : this()
        {
            try
            {
                UserId = userId;
                UserName = userName;
                PasswordHash = passwordHash;
                Role = role;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 새 사용자 생성: {userId} ({userName})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 생성 실패: {ex.Message}");
                throw;
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 계정 잠금 상태 확인 (5회 이상 실패시 잠금)
        /// </summary>
        /// <returns>잠금 여부</returns>
        public bool IsLocked()
        {
            return _failedLoginCount >= 5;
        }

        /// <summary>
        /// 로그인 성공시 호출
        /// </summary>
        public void OnLoginSuccess()
        {
            try
            {
                _lastLoginAt = DateTime.Now;
                _failedLoginCount = 0; // 실패 횟수 초기화

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그인 성공: {_userId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그인 성공 처리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 로그인 실패시 호출
        /// </summary>
        public void OnLoginFailure()
        {
            try
            {
                _failedLoginCount++;
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그인 실패: {_userId}, 실패 횟수: {_failedLoginCount}");

                if (IsLocked())
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 계정 잠금: {_userId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로그인 실패 처리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 계정 잠금 해제 (관리자용)
        /// </summary>
        public void UnlockAccount()
        {
            try
            {
                _failedLoginCount = 0;
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 계정 잠금 해제: {_userId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 계정 잠금 해제 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 비밀번호 변경 완료 처리
        /// </summary>
        public void OnPasswordChanged()
        {
            try
            {
                _passwordChangedAt = DateTime.Now;
                _mustChangePassword = false;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 변경 완료: {_userId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 변경 처리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 비밀번호 만료 여부 확인 (90일)
        /// </summary>
        /// <returns>만료 여부</returns>
        public bool IsPasswordExpired()
        {
            try
            {
                if (!_passwordChangedAt.HasValue)
                    return true; // 한번도 변경 안한 경우 만료

                var daysSinceChange = (DateTime.Now - _passwordChangedAt.Value).TotalDays;
                return daysSinceChange > 90;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 만료 확인 실패: {ex.Message}");
                return true; // 오류시 안전하게 만료 처리
            }
        }

        /// <summary>
        /// 사용자 정보 유효성 검증
        /// </summary>
        /// <returns>유효 여부</returns>
        public bool IsValid()
        {
            try
            {
                return !string.IsNullOrWhiteSpace(_userId) &&
                       !string.IsNullOrWhiteSpace(_userName) &&
                       !string.IsNullOrWhiteSpace(_passwordHash) &&
                       _userId.Length >= 3 && _userId.Length <= 20 &&
                       _userName.Length <= 50;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 유효성 검증 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 문자열 표현
        /// </summary>
        /// <returns>사용자 정보 문자열</returns>
        public override string ToString()
        {
            return $"{_userId} ({_userName}) - {_role} [{(_isActive ? "활성" : "비활성")}]";
        }
        #endregion
    }
}