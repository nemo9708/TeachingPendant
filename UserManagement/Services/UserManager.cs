using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TeachingPendant.UserManagement.Models;
using TeachingPendant.UserManagement.Services;

namespace TeachingPendant.UserManagement.Services
{
    /// <summary>
    /// 사용자 데이터 컨테이너 (JSON 저장용)
    /// PersistentDataManager 패턴을 따라 설계
    /// </summary>
    public class UserDataContainer
    {
        public List<User> Users { get; set; }
        public DateTime LastUpdated { get; set; }
        public string DataVersion { get; set; }
        public int TotalUsers { get; set; }

        public UserDataContainer()
        {
            Users = new List<User>();
            LastUpdated = DateTime.Now;
            DataVersion = "1.0";
            TotalUsers = 0;
        }
    }

    /// <summary>
    /// 사용자 CRUD 작업을 담당하는 관리 클래스
    /// PersistentDataManager 패턴을 참고하여 JSON 파일로 데이터 관리
    /// </summary>
    public static class UserManager
    {
        #region Private Fields
        private static readonly string CLASS_NAME = "UserManager";

        // 파일 경로 설정 (PersistentDataManager 패턴 따라함)
        private const string DATA_FOLDER = "TeachingPendantData";
        private const string USER_DATA_FILE = "UserData.json";

        // 메모리 캐시
        private static List<User> _users = new List<User>();
        private static bool _isInitialized = false;
        private static readonly object _lockObject = new object();
        #endregion

        #region Properties
        /// <summary>
        /// 사용자 데이터 파일 경로
        /// </summary>
        private static string DataFolderPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DATA_FOLDER); }
        }

        /// <summary>
        /// 사용자 데이터 파일 전체 경로
        /// </summary>
        private static string UserDataPath
        {
            get { return Path.Combine(DataFolderPath, USER_DATA_FILE); }
        }

        /// <summary>
        /// 총 사용자 수
        /// </summary>
        public static int TotalUserCount
        {
            get
            {
                lock (_lockObject)
                {
                    return _users.Count;
                }
            }
        }

        /// <summary>
        /// 활성 사용자 수
        /// </summary>
        public static int ActiveUserCount
        {
            get
            {
                lock (_lockObject)
                {
                    return _users.Count(u => u.IsActive);
                }
            }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// UserManager 초기화 (시스템 시작 시 호출)
        /// </summary>
        /// <returns>초기화 성공 여부</returns>
        public static async Task<bool> InitializeAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] UserManager 초기화 시작");

                lock (_lockObject)
                {
                    if (_isInitialized)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 이미 초기화됨");
                        return true;
                    }
                }

                // 데이터 폴더 생성
                EnsureDataFolderExists();

                // 사용자 데이터 로드
                bool loadSuccess = await LoadUsersFromFileAsync();

                // 기본 관리자 계정 확인/생성
                await EnsureDefaultAdminExistsAsync();

                lock (_lockObject)
                {
                    _isInitialized = true;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] UserManager 초기화 완료: {_users.Count}명 사용자 로드");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] UserManager 초기화 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 데이터 폴더 존재 확인 및 생성
        /// </summary>
        private static void EnsureDataFolderExists()
        {
            try
            {
                if (!Directory.Exists(DataFolderPath))
                {
                    Directory.CreateDirectory(DataFolderPath);
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 데이터 폴더 생성: {DataFolderPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 데이터 폴더 생성 실패: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 기본 관리자 계정 존재 확인 및 생성
        /// </summary>
        private static async Task EnsureDefaultAdminExistsAsync()
        {
            try
            {
                lock (_lockObject)
                {
                    // 관리자 계정이 이미 존재하는지 확인
                    if (_users.Any(u => u.Role == UserRole.Administrator))
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 관리자 계정 이미 존재");
                        return;
                    }
                }

                // 기본 관리자 계정 생성
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 기본 관리자 계정 생성 시작");

                string tempPassword = "admin123"; // 초기 비밀번호
                string hashedPassword = AuthenticationService.HashPassword(tempPassword);

                var adminUser = new User("admin", "시스템 관리자", hashedPassword, UserRole.Administrator)
                {
                    IsActive = true,
                    MustChangePassword = true, // 초기 로그인 시 비밀번호 변경 강제
                    CreatedAt = DateTime.Now
                };

                lock (_lockObject)
                {
                    _users.Add(adminUser);
                }

                // 파일에 저장
                await SaveUsersToFileAsync();

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 기본 관리자 계정 생성 완료: admin / {tempPassword}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 기본 관리자 계정 생성 실패: {ex.Message}");
                throw;
            }
        }
        #endregion

        #region File Operations
        /// <summary>
        /// 파일에서 사용자 데이터 로드
        /// </summary>
        /// <returns>로드 성공 여부</returns>
        private static async Task<bool> LoadUsersFromFileAsync()
        {
            try
            {
                if (!File.Exists(UserDataPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 데이터 파일 없음, 빈 목록으로 시작");
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 데이터 로드 시작: {UserDataPath}");

                string json = await ReadAllTextAsync(UserDataPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 데이터 파일이 비어있음");
                    return true;
                }

                var container = JsonConvert.DeserializeObject<UserDataContainer>(json);
                if (container?.Users != null)
                {
                    lock (_lockObject)
                    {
                        _users.Clear();
                        _users.AddRange(container.Users);
                    }

                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 데이터 로드 완료: {container.Users.Count}명");
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 데이터 파싱 실패");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 데이터 로드 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 사용자 데이터를 파일에 저장
        /// </summary>
        /// <returns>저장 성공 여부</returns>
        private static async Task<bool> SaveUsersToFileAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 데이터 저장 시작");

                UserDataContainer container;
                lock (_lockObject)
                {
                    container = new UserDataContainer
                    {
                        Users = new List<User>(_users),
                        LastUpdated = DateTime.Now,
                        DataVersion = "1.0",
                        TotalUsers = _users.Count
                    };
                }

                string json = JsonConvert.SerializeObject(container, Formatting.Indented);
                await WriteAllTextAsync(UserDataPath, json);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 데이터 저장 완료: {container.TotalUsers}명");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 데이터 저장 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 비동기 파일 읽기 (.NET Framework 4.6.1 호환)
        /// </summary>
        private static async Task<string> ReadAllTextAsync(string path)
        {
            return await Task.Run(() => File.ReadAllText(path, System.Text.Encoding.UTF8));
        }

        /// <summary>
        /// 비동기 파일 쓰기 (.NET Framework 4.6.1 호환)
        /// </summary>
        private static async Task WriteAllTextAsync(string path, string content)
        {
            await Task.Run(() => File.WriteAllText(path, content, System.Text.Encoding.UTF8));
        }
        #endregion

        #region User CRUD Operations
        /// <summary>
        /// 새 사용자 생성
        /// </summary>
        /// <param name="userId">사용자 ID</param>
        /// <param name="userName">사용자 이름</param>
        /// <param name="password">비밀번호</param>
        /// <param name="role">사용자 역할</param>
        /// <returns>생성 결과</returns>
        public static async Task<AuthenticationResult> CreateUserAsync(string userId, string userName, string password, UserRole role)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 생성 시도: {userId}");

                // 입력 검증
                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
                    return AuthenticationResult.Failure("모든 필드를 입력해주세요.");

                // 중복 ID 확인
                lock (_lockObject)
                {
                    if (_users.Any(u => string.Equals(u.UserId, userId, StringComparison.OrdinalIgnoreCase)))
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 생성 실패: 중복 ID - {userId}");
                        return AuthenticationResult.Failure("이미 존재하는 사용자 ID입니다.");
                    }
                }

                // 비밀번호 검증
                if (!AuthenticationService.IsPasswordValid(password))
                    return AuthenticationResult.Failure($"비밀번호가 정책을 만족하지 않습니다.\n{AuthenticationService.GetPasswordPolicy()}");

                // 사용자 생성
                string hashedPassword = AuthenticationService.HashPassword(password);
                var newUser = new User(userId, userName, hashedPassword, role)
                {
                    IsActive = true,
                    MustChangePassword = false,
                    CreatedAt = DateTime.Now
                };

                // 메모리에 추가
                lock (_lockObject)
                {
                    _users.Add(newUser);
                }

                // 파일에 저장
                bool saveSuccess = await SaveUsersToFileAsync();
                if (!saveSuccess)
                {
                    // 저장 실패 시 롤백
                    lock (_lockObject)
                    {
                        _users.RemoveAll(u => u.UserId == userId);
                    }
                    return AuthenticationResult.Failure("사용자 데이터 저장에 실패했습니다.");
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 생성 성공: {userId} ({userName})");
                return AuthenticationResult.Success(newUser);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 생성 중 오류: {ex.Message}");
                return AuthenticationResult.Failure("사용자 생성 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 사용자 ID로 사용자 조회
        /// </summary>
        /// <param name="userId">사용자 ID</param>
        /// <returns>사용자 객체 (없으면 null)</returns>
        public static User GetUserById(string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                    return null;

                lock (_lockObject)
                {
                    return _users.FirstOrDefault(u => string.Equals(u.UserId, userId, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 조회 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 모든 사용자 목록 조회
        /// </summary>
        /// <returns>사용자 목록</returns>
        public static List<User> GetAllUsers()
        {
            try
            {
                lock (_lockObject)
                {
                    return new List<User>(_users); // 복사본 반환
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 목록 조회 실패: {ex.Message}");
                return new List<User>();
            }
        }

        /// <summary>
        /// 역할별 사용자 목록 조회
        /// </summary>
        /// <param name="role">사용자 역할</param>
        /// <returns>해당 역할의 사용자 목록</returns>
        public static List<User> GetUsersByRole(UserRole role)
        {
            try
            {
                lock (_lockObject)
                {
                    return _users.Where(u => u.Role == role).ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 역할별 사용자 조회 실패: {ex.Message}");
                return new List<User>();
            }
        }

        /// <summary>
        /// 사용자 정보 업데이트
        /// </summary>
        /// <param name="updatedUser">업데이트된 사용자 정보</param>
        /// <returns>업데이트 성공 여부</returns>
        public static async Task<bool> UpdateUserAsync(User updatedUser)
        {
            try
            {
                if (updatedUser == null || !updatedUser.IsValid())
                    return false;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 업데이트: {updatedUser.UserId}");

                lock (_lockObject)
                {
                    var existingUser = _users.FirstOrDefault(u => u.UserId == updatedUser.UserId);
                    if (existingUser == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 업데이트할 사용자 없음: {updatedUser.UserId}");
                        return false;
                    }

                    // 사용자 정보 업데이트
                    int index = _users.IndexOf(existingUser);
                    _users[index] = updatedUser;
                }

                // 파일에 저장
                bool saveSuccess = await SaveUsersToFileAsync();
                if (saveSuccess)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 업데이트 성공: {updatedUser.UserId}");
                }

                return saveSuccess;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 업데이트 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 사용자 삭제
        /// </summary>
        /// <param name="userId">삭제할 사용자 ID</param>
        /// <returns>삭제 성공 여부</returns>
        public static async Task<bool> DeleteUserAsync(string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                    return false;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 삭제 시도: {userId}");

                User userToDelete = null;
                lock (_lockObject)
                {
                    userToDelete = _users.FirstOrDefault(u => string.Equals(u.UserId, userId, StringComparison.OrdinalIgnoreCase));
                    if (userToDelete == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 삭제할 사용자 없음: {userId}");
                        return false;
                    }

                    // 마지막 관리자 삭제 방지
                    var admins = _users.Where(u => u.Role == UserRole.Administrator && u.IsActive).ToList();
                    if (userToDelete.Role == UserRole.Administrator && admins.Count <= 1)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 마지막 관리자 삭제 시도 차단: {userId}");
                        return false;
                    }

                    _users.Remove(userToDelete);
                }

                // 파일에 저장
                bool saveSuccess = await SaveUsersToFileAsync();
                if (saveSuccess)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 삭제 성공: {userId}");
                }
                else
                {
                    // 저장 실패 시 롤백
                    lock (_lockObject)
                    {
                        _users.Add(userToDelete);
                    }
                }

                return saveSuccess;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 삭제 실패: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region User Management Operations
        /// <summary>
        /// 사용자 활성화/비활성화
        /// </summary>
        /// <param name="userId">사용자 ID</param>
        /// <param name="isActive">활성화 여부</param>
        /// <returns>변경 성공 여부</returns>
        public static async Task<bool> SetUserActiveAsync(string userId, bool isActive)
        {
            try
            {
                var user = GetUserById(userId);
                if (user == null) return false;

                // 마지막 관리자 비활성화 방지
                if (!isActive && user.Role == UserRole.Administrator)
                {
                    var activeAdmins = GetUsersByRole(UserRole.Administrator).Count(u => u.IsActive);
                    if (activeAdmins <= 1)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 마지막 관리자 비활성화 시도 차단: {userId}");
                        return false;
                    }
                }

                user.IsActive = isActive;
                return await UpdateUserAsync(user);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 활성화 상태 변경 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 사용자 역할 변경
        /// </summary>
        /// <param name="userId">사용자 ID</param>
        /// <param name="newRole">새 역할</param>
        /// <returns>변경 성공 여부</returns>
        public static async Task<bool> ChangeUserRoleAsync(string userId, UserRole newRole)
        {
            try
            {
                var user = GetUserById(userId);
                if (user == null) return false;

                // 마지막 관리자 역할 변경 방지
                if (user.Role == UserRole.Administrator && newRole != UserRole.Administrator)
                {
                    var activeAdmins = GetUsersByRole(UserRole.Administrator).Count(u => u.IsActive);
                    if (activeAdmins <= 1)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 마지막 관리자 역할 변경 시도 차단: {userId}");
                        return false;
                    }
                }

                user.Role = newRole;
                return await UpdateUserAsync(user);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 역할 변경 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 관리자용 비밀번호 리셋
        /// </summary>
        /// <param name="userId">사용자 ID</param>
        /// <param name="tempPassword">임시 비밀번호 (null이면 자동 생성)</param>
        /// <returns>리셋 결과</returns>
        public static async Task<AuthenticationResult> ResetUserPasswordAsync(string userId, string tempPassword = null)
        {
            try
            {
                var user = GetUserById(userId);
                if (user == null)
                    return AuthenticationResult.Failure("존재하지 않는 사용자입니다.");

                // 임시 비밀번호 생성 또는 검증
                if (string.IsNullOrWhiteSpace(tempPassword))
                {
                    tempPassword = AuthenticationService.GenerateTemporaryPassword();
                }

                // AuthenticationService를 통한 리셋
                var resetResult = AuthenticationService.ResetPassword(user, tempPassword);
                if (!resetResult.IsSuccess)
                    return resetResult;

                // 사용자 정보 업데이트
                bool updateSuccess = await UpdateUserAsync(user);
                if (!updateSuccess)
                    return AuthenticationResult.Failure("사용자 정보 업데이트에 실패했습니다.");

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 리셋 성공: {userId}, 임시 비밀번호: {tempPassword}");

                // 성공 결과에 임시 비밀번호 정보 포함
                var result = AuthenticationResult.Success(user);
                result.ErrorMessage = $"임시 비밀번호: {tempPassword}"; // 임시로 ErrorMessage 필드 활용
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 비밀번호 리셋 실패: {ex.Message}");
                return AuthenticationResult.Failure("비밀번호 리셋 중 오류가 발생했습니다.");
            }
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// 사용자 통계 정보 조회
        /// </summary>
        /// <returns>통계 정보</returns>
        public static string GetUserStatistics()
        {
            try
            {
                lock (_lockObject)
                {
                    int totalUsers = _users.Count;
                    int activeUsers = _users.Count(u => u.IsActive);
                    int adminCount = _users.Count(u => u.Role == UserRole.Administrator);
                    int engineerCount = _users.Count(u => u.Role == UserRole.Engineer);
                    int operatorCount = _users.Count(u => u.Role == UserRole.Operator);
                    int guestCount = _users.Count(u => u.Role == UserRole.Guest);

                    return $"사용자 통계:\n" +
                           $"• 전체: {totalUsers}명\n" +
                           $"• 활성: {activeUsers}명\n" +
                           $"• 관리자: {adminCount}명\n" +
                           $"• 엔지니어: {engineerCount}명\n" +
                           $"• 운영자: {operatorCount}명\n" +
                           $"• 게스트: {guestCount}명";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 통계 조회 실패: {ex.Message}");
                return "통계 조회 실패";
            }
        }

        /// <summary>
        /// 사용자 데이터 백업
        /// </summary>
        /// <param name="backupPath">백업 파일 경로</param>
        /// <returns>백업 성공 여부</returns>
        public static async Task<bool> BackupUserDataAsync(string backupPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(backupPath))
                    return false;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 데이터 백업 시작: {backupPath}");

                // 현재 사용자 데이터를 JSON으로 직렬화
                UserDataContainer container;
                lock (_lockObject)
                {
                    container = new UserDataContainer
                    {
                        Users = new List<User>(_users),
                        LastUpdated = DateTime.Now,
                        DataVersion = "1.0",
                        TotalUsers = _users.Count
                    };
                }

                string json = JsonConvert.SerializeObject(container, Formatting.Indented);
                await WriteAllTextAsync(backupPath, json);

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 데이터 백업 완료");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 데이터 백업 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 시스템 상태 확인
        /// </summary>
        /// <returns>상태 정보</returns>
        public static string GetSystemStatus()
        {
            try
            {
                return $"UserManager 상태:\n" +
                       $"• 초기화: {(_isInitialized ? "완료" : "미완료")}\n" +
                       $"• 데이터 파일: {(File.Exists(UserDataPath) ? "존재" : "없음")}\n" +
                       $"• 메모리 사용자: {TotalUserCount}명\n" +
                       $"• 활성 사용자: {ActiveUserCount}명\n" +
                       $"• 데이터 경로: {UserDataPath}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 시스템 상태 조회 실패: {ex.Message}");
                return "상태 조회 실패";
            }
        }

        /// <summary>
        /// 강제 데이터 동기화 (디버깅용)
        /// </summary>
        /// <returns>동기화 성공 여부</returns>
        public static async Task<bool> ForceSyncDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 강제 데이터 동기화 시작");

                bool result = await SaveUsersToFileAsync();
                if (result)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 강제 데이터 동기화 완료");
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 강제 데이터 동기화 실패: {ex.Message}");
                return false;
            }
        }
        #endregion
    }
}