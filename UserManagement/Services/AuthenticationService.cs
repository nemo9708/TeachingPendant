using System;
using System.Security.Cryptography;
using System.Text;

namespace TeachingPendant.UserManagement.Services
{
    /// <summary>
    /// 인증 결과 정보
    /// </summary>
    public class AuthenticationResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public Models.User User { get; set; }
        public DateTime AuthenticationTime { get; set; }

        public AuthenticationResult()
        {
            AuthenticationTime = DateTime.Now;
        }

        public static AuthenticationResult Success(Models.User user)
        {
            return new AuthenticationResult
            {
                IsSuccess = true,
                User = user,
                ErrorMessage = null
            };
        }

        public static AuthenticationResult Failure(string errorMessage)
        {
            return new AuthenticationResult
            {
                IsSuccess = false,
                User = null,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// 사용자 인증을 담당하는 서비스 클래스
    /// 비밀번호 해싱, 검증, 로그인 처리 등을 수행
    /// </summary>
    public static class AuthenticationService
    {
        #region Private Fields
        private static readonly string CLASS_NAME = "AuthenticationService";

        // Salt 길이 (바이트)
        private const int SALT_SIZE = 32;

        // 해시 길이 (바이트)
        private const int HASH_SIZE = 32;

        // 해싱 반복 횟수 (PBKDF2)
        private const int ITERATIONS = 10000;

        // 비밀번호 최소 길이
        private const int MIN_PASSWORD_LENGTH = 6;

        // 비밀번호 최대 길이
        private const int MAX_PASSWORD_LENGTH = 50;
        #endregion

        #region Password Hashing
        /// <summary>
        /// 비밀번호를 안전하게 해싱 (PBKDF2 + Salt)
        /// </summary>
        /// <param name="password">원본 비밀번호</param>
        /// <returns>해시된 비밀번호 (Salt 포함)</returns>
        public static string HashPassword(string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(password))
                    throw new ArgumentException("비밀번호는 비어있을 수 없습니다.");

                if (!IsPasswordValid(password))
                    throw new ArgumentException("비밀번호가 보안 정책을 만족하지 않습니다.");

                // Salt 생성
                byte[] salt = new byte[SALT_SIZE];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                }

                // PBKDF2로 해싱
                byte[] hash = GeneratePasswordHash(password, salt, ITERATIONS, HASH_SIZE);

                // Salt + Hash를 Base64로 인코딩
                byte[] combined = new byte[SALT_SIZE + HASH_SIZE];
                Array.Copy(salt, 0, combined, 0, SALT_SIZE);
                Array.Copy(hash, 0, combined, SALT_SIZE, HASH_SIZE);

                string result = Convert.ToBase64String(combined);
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 해싱 완료");

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 해싱 실패: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 비밀번호 검증
        /// </summary>
        /// <param name="password">입력된 비밀번호</param>
        /// <param name="hashedPassword">저장된 해시된 비밀번호</param>
        /// <returns>일치 여부</returns>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hashedPassword))
                    return false;

                // Base64 디코딩
                byte[] combined = Convert.FromBase64String(hashedPassword);
                if (combined.Length != SALT_SIZE + HASH_SIZE)
                    return false;

                // Salt와 Hash 분리
                byte[] salt = new byte[SALT_SIZE];
                byte[] storedHash = new byte[HASH_SIZE];
                Array.Copy(combined, 0, salt, 0, SALT_SIZE);
                Array.Copy(combined, SALT_SIZE, storedHash, 0, HASH_SIZE);

                // 입력된 비밀번호로 해시 생성
                byte[] computedHash = GeneratePasswordHash(password, salt, ITERATIONS, HASH_SIZE);

                // 해시 비교 (타이밍 공격 방지)
                bool isMatch = CompareHashesSecurely(storedHash, computedHash);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 검증: {(isMatch ? "성공" : "실패")}");
                return isMatch;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 검증 중 오류: {ex.Message}");
                return false; // 오류 시 안전하게 실패 처리
            }
        }

        /// <summary>
        /// PBKDF2를 사용한 비밀번호 해시 생성
        /// </summary>
        /// <param name="password">비밀번호</param>
        /// <param name="salt">Salt</param>
        /// <param name="iterations">반복 횟수</param>
        /// <param name="hashSize">해시 크기</param>
        /// <returns>해시 바이트 배열</returns>
        private static byte[] GeneratePasswordHash(string password, byte[] salt, int iterations, int hashSize)
        {
            try
            {
                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations))
                {
                    return pbkdf2.GetBytes(hashSize);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] PBKDF2 해시 생성 실패: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 해시 비교 (타이밍 공격 방지)
        /// </summary>
        /// <param name="hash1">첫 번째 해시</param>
        /// <param name="hash2">두 번째 해시</param>
        /// <returns>일치 여부</returns>
        private static bool CompareHashesSecurely(byte[] hash1, byte[] hash2)
        {
            try
            {
                if (hash1.Length != hash2.Length)
                    return false;

                int result = 0;
                for (int i = 0; i < hash1.Length; i++)
                {
                    result |= hash1[i] ^ hash2[i];
                }
                return result == 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 해시 비교 실패: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Password Policy
        /// <summary>
        /// 비밀번호 보안 정책 검증
        /// </summary>
        /// <param name="password">검증할 비밀번호</param>
        /// <returns>유효성 여부</returns>
        public static bool IsPasswordValid(string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(password))
                    return false;

                // 길이 검증
                if (password.Length < MIN_PASSWORD_LENGTH || password.Length > MAX_PASSWORD_LENGTH)
                    return false;

                // 기본 복잡성 검증 (최소 요구사항)
                bool hasLetter = false;
                bool hasDigit = false;

                foreach (char c in password)
                {
                    if (char.IsLetter(c))
                        hasLetter = true;
                    if (char.IsDigit(c))
                        hasDigit = true;

                    if (hasLetter && hasDigit)
                        break;
                }

                return hasLetter && hasDigit;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 정책 검증 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 비밀번호 정책 설명 반환
        /// </summary>
        /// <returns>정책 설명 문자열</returns>
        public static string GetPasswordPolicy()
        {
            return $"비밀번호 정책:\n" +
                   $"• 길이: {MIN_PASSWORD_LENGTH}-{MAX_PASSWORD_LENGTH}자\n" +
                   $"• 영문자와 숫자를 포함해야 함\n" +
                   $"• 특수문자 사용 권장";
        }

        /// <summary>
        /// 비밀번호 강도 평가
        /// </summary>
        /// <param name="password">평가할 비밀번호</param>
        /// <returns>강도 (0-100)</returns>
        public static int GetPasswordStrength(string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(password))
                    return 0;

                int score = 0;

                // 길이 점수 (최대 30점)
                if (password.Length >= MIN_PASSWORD_LENGTH)
                    score += Math.Min(30, password.Length * 3);

                // 복잡성 점수
                bool hasLower = false, hasUpper = false, hasDigit = false, hasSpecial = false;

                foreach (char c in password)
                {
                    if (char.IsLower(c)) hasLower = true;
                    if (char.IsUpper(c)) hasUpper = true;
                    if (char.IsDigit(c)) hasDigit = true;
                    if (!char.IsLetterOrDigit(c)) hasSpecial = true;
                }

                if (hasLower) score += 10;
                if (hasUpper) score += 15;
                if (hasDigit) score += 15;
                if (hasSpecial) score += 20;

                // 다양성 보너스
                if ((hasLower ? 1 : 0) + (hasUpper ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSpecial ? 1 : 0) >= 3)
                    score += 10;

                return Math.Min(100, score);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 강도 평가 실패: {ex.Message}");
                return 0;
            }
        }
        #endregion

        #region Authentication
        /// <summary>
        /// 사용자 인증 (로그인)
        /// </summary>
        /// <param name="userId">사용자 ID</param>
        /// <param name="password">비밀번호</param>
        /// <param name="userProvider">사용자 데이터 제공자 함수</param>
        /// <returns>인증 결과</returns>
        public static AuthenticationResult Authenticate(string userId, string password, Func<string, Models.User> userProvider)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 인증 시도: {userId}");

                // 입력 검증
                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 인증 실패: 빈 입력값");
                    return AuthenticationResult.Failure("사용자 ID와 비밀번호를 입력해주세요.");
                }

                // 사용자 조회
                Models.User user = null;
                try
                {
                    user = userProvider?.Invoke(userId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 조회 실패: {ex.Message}");
                    return AuthenticationResult.Failure("사용자 정보를 조회할 수 없습니다.");
                }

                if (user == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 인증 실패: 사용자 없음 - {userId}");
                    return AuthenticationResult.Failure("존재하지 않는 사용자입니다.");
                }

                // 계정 상태 확인
                if (!user.IsActive)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 인증 실패: 비활성화된 계정 - {userId}");
                    return AuthenticationResult.Failure("비활성화된 계정입니다.");
                }

                // 비밀번호 검증
                bool passwordMatch = VerifyPassword(password, user.PasswordHash);
                if (!passwordMatch)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 인증 실패: 잘못된 비밀번호 - {userId}");
                    return AuthenticationResult.Failure("잘못된 비밀번호입니다.");
                }

                // 비밀번호 만료 확인
                if (user.IsPasswordExpired())
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 인증 성공하지만 비밀번호 만료 - {userId}");
                    user.OnLoginSuccess(); // 성공 처리는 해줌
                    return AuthenticationResult.Failure("비밀번호가 만료되었습니다. 비밀번호를 변경해주세요.");
                }

                // 강제 비밀번호 변경 확인
                if (user.MustChangePassword)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 인증 성공하지만 비밀번호 변경 필요 - {userId}");
                    user.OnLoginSuccess(); // 성공 처리는 해줌
                    return AuthenticationResult.Failure("비밀번호 변경이 필요합니다.");
                }

                // 인증 성공
                user.OnLoginSuccess();
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 인증 성공: {userId} ({user.UserName})");

                return AuthenticationResult.Success(user);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 인증 처리 중 오류: {ex.Message}");
                return AuthenticationResult.Failure("인증 처리 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 비밀번호 변경
        /// </summary>
        /// <param name="user">사용자</param>
        /// <param name="currentPassword">현재 비밀번호</param>
        /// <param name="newPassword">새 비밀번호</param>
        /// <returns>변경 성공 여부</returns>
        public static AuthenticationResult ChangePassword(Models.User user, string currentPassword, string newPassword)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 변경 시도: {user?.UserId}");

                if (user == null)
                    return AuthenticationResult.Failure("사용자 정보가 없습니다.");

                // 현재 비밀번호 검증
                if (!VerifyPassword(currentPassword, user.PasswordHash))
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 변경 실패: 현재 비밀번호 불일치 - {user.UserId}");
                    return AuthenticationResult.Failure("현재 비밀번호가 일치하지 않습니다.");
                }

                // 새 비밀번호 정책 검증
                if (!IsPasswordValid(newPassword))
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 변경 실패: 정책 위반 - {user.UserId}");
                    return AuthenticationResult.Failure($"새 비밀번호가 정책을 만족하지 않습니다.\n{GetPasswordPolicy()}");
                }

                // 같은 비밀번호 사용 방지
                if (VerifyPassword(newPassword, user.PasswordHash))
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 변경 실패: 기존과 동일 - {user.UserId}");
                    return AuthenticationResult.Failure("새 비밀번호는 현재 비밀번호와 달라야 합니다.");
                }

                // 비밀번호 해싱 및 업데이트
                string newHashedPassword = HashPassword(newPassword);
                user.PasswordHash = newHashedPassword;
                user.OnPasswordChanged();

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 변경 성공: {user.UserId}");
                return AuthenticationResult.Success(user);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 변경 중 오류: {ex.Message}");
                return AuthenticationResult.Failure("비밀번호 변경 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 관리자용 비밀번호 리셋
        /// </summary>
        /// <param name="user">사용자</param>
        /// <param name="tempPassword">임시 비밀번호</param>
        /// <returns>리셋 성공 여부</returns>
        public static AuthenticationResult ResetPassword(Models.User user, string tempPassword)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 리셋 시도: {user?.UserId}");

                if (user == null)
                    return AuthenticationResult.Failure("사용자 정보가 없습니다.");

                if (!IsPasswordValid(tempPassword))
                    return AuthenticationResult.Failure($"임시 비밀번호가 정책을 만족하지 않습니다.\n{GetPasswordPolicy()}");

                // 비밀번호 해싱 및 업데이트
                string hashedPassword = HashPassword(tempPassword);
                user.PasswordHash = hashedPassword;
                user.MustChangePassword = true; // 강제 변경 설정

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 리셋 성공: {user.UserId}");
                return AuthenticationResult.Success(user);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 리셋 중 오류: {ex.Message}");
                return AuthenticationResult.Failure("비밀번호 리셋 중 오류가 발생했습니다.");
            }
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// 임시 비밀번호 생성
        /// </summary>
        /// <param name="length">길이 (기본 8자)</param>
        /// <returns>임시 비밀번호</returns>
        public static string GenerateTemporaryPassword(int length = 8)
        {
            try
            {
                const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
                var result = new StringBuilder();
                byte[] randomBytes = new byte[length];

                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(randomBytes);

                    for (int i = 0; i < length; i++)
                    {
                        result.Append(chars[randomBytes[i] % chars.Length]);
                    }
                }

                // 정책 만족을 위해 최소 1개의 숫자 보장
                if (result.ToString().IndexOfAny("23456789".ToCharArray()) == -1)
                {
                    result[result.Length - 1] = "23456789"[randomBytes[0] % 8];
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 임시 비밀번호 생성 완료");
                return result.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 임시 비밀번호 생성 실패: {ex.Message}");
                return "Temp123"; // 폴백
            }
        }

        /// <summary>
        /// 인증 서비스 상태 확인
        /// </summary>
        /// <returns>상태 정보</returns>
        public static string GetServiceStatus()
        {
            try
            {
                return $"인증 서비스 상태:\n" +
                       $"• 해싱 알고리즘: PBKDF2\n" +
                       $"• Salt 크기: {SALT_SIZE} bytes\n" +
                       $"• 해시 크기: {HASH_SIZE} bytes\n" +
                       $"• 반복 횟수: {ITERATIONS:N0}\n" +
                       $"• 비밀번호 정책: {MIN_PASSWORD_LENGTH}-{MAX_PASSWORD_LENGTH}자, 영문+숫자";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 서비스 상태 조회 실패: {ex.Message}");
                return "서비스 상태 조회 실패";
            }
        }
        #endregion
    }
}