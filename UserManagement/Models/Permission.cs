using System;
using System.Collections.Generic;
using System.Linq;

namespace TeachingPendant.UserManagement.Models
{
    /// <summary>
    /// 웨이퍼 반송 로봇 시스템의 세부 권한 관리 클래스
    /// 각 기능별로 세분화된 권한을 정의하고 관리
    /// </summary>
    public class Permission
    {
        #region Constants
        private static readonly string CLASS_NAME = "Permission";

        // 권한 카테고리 상수
        public const string CATEGORY_SCREEN = "Screen";
        public const string CATEGORY_ROBOT = "Robot";
        public const string CATEGORY_TEACHING = "Teaching";
        public const string CATEGORY_RECIPE = "Recipe";
        public const string CATEGORY_SYSTEM = "System";
        public const string CATEGORY_SAFETY = "Safety";
        public const string CATEGORY_USER = "User";
        public const string CATEGORY_DATA = "Data";
        #endregion

        #region Properties
        /// <summary>
        /// 권한 고유 ID
        /// </summary>
        public string PermissionId { get; set; }

        /// <summary>
        /// 권한 카테고리 (Screen, Robot, Teaching 등)
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 권한 이름
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 권한 설명
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 권한 허용 여부
        /// </summary>
        public bool IsAllowed { get; set; }

        /// <summary>
        /// 권한 생성일
        /// </summary>
        public DateTime CreatedAt { get; set; }
        #endregion

        #region Constructors
        /// <summary>
        /// 기본 생성자
        /// </summary>
        public Permission()
        {
            CreatedAt = DateTime.Now;
            IsAllowed = false; // 기본적으로 거부
        }

        /// <summary>
        /// 권한 생성용 생성자
        /// </summary>
        /// <param name="permissionId">권한 ID</param>
        /// <param name="category">카테고리</param>
        /// <param name="name">권한 이름</param>
        /// <param name="description">설명</param>
        /// <param name="isAllowed">허용 여부</param>
        public Permission(string permissionId, string category, string name, string description, bool isAllowed = false)
            : this()
        {
            try
            {
                PermissionId = permissionId;
                Category = category;
                Name = name;
                Description = description;
                IsAllowed = isAllowed;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 권한 생성: {permissionId} ({name})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 권한 생성 실패: {ex.Message}");
                throw;
            }
        }
        #endregion

        #region Static Permission Definitions
        /// <summary>
        /// 시스템의 모든 기본 권한들을 정의
        /// 프로젝트 지식 기반으로 실제 필요한 권한들 구성
        /// </summary>
        /// <returns>기본 권한 목록</returns>
        public static List<Permission> GetDefaultPermissions()
        {
            try
            {
                var permissions = new List<Permission>();

                // 화면 접근 권한 (현재 MainWindow 버튼 기반)
                permissions.AddRange(GetScreenPermissions());

                // 로봇 제어 권한 (Movement 시스템 기반)
                permissions.AddRange(GetRobotPermissions());

                // Teaching 시스템 권한
                permissions.AddRange(GetTeachingPermissions());

                // 레시피 시스템 권한 (7단계 완성된 시스템 기반)
                permissions.AddRange(GetRecipePermissions());

                // 시스템 관리 권한
                permissions.AddRange(GetSystemPermissions());

                // 안전 시스템 권한 (SafetySystem 기반)
                permissions.AddRange(GetSafetyPermissions());

                // 사용자 관리 권한
                permissions.AddRange(GetUserManagementPermissions());

                // 데이터 관리 권한
                permissions.AddRange(GetDataManagementPermissions());

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 기본 권한 {permissions.Count}개 생성 완료");
                return permissions;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 기본 권한 생성 실패: {ex.Message}");
                return new List<Permission>();
            }
        }

        /// <summary>
        /// 화면 접근 권한들 (MainWindow 기반)
        /// </summary>
        private static List<Permission> GetScreenPermissions()
        {
            return new List<Permission>
            {
                new Permission("SCREEN_MOVEMENT", CATEGORY_SCREEN, "Movement 화면", "로봇 동작 제어 화면 접근"),
                new Permission("SCREEN_MONITOR", CATEGORY_SCREEN, "Monitor 화면", "시스템 모니터링 화면 접근"),
                new Permission("SCREEN_TEACHING", CATEGORY_SCREEN, "Teaching 화면", "로봇 티칭 화면 접근"),
                new Permission("SCREEN_IO", CATEGORY_SCREEN, "I/O 화면", "입출력 제어 화면 접근"),
                new Permission("SCREEN_SYSTEM", CATEGORY_SCREEN, "System 화면", "시스템 설정 화면 접근"),
                new Permission("SCREEN_SETTING", CATEGORY_SCREEN, "Setting 화면", "설정 관리 화면 접근"),
                new Permission("SCREEN_ERROR_LOG", CATEGORY_SCREEN, "Error Log 화면", "오류 로그 화면 접근"),
                new Permission("SCREEN_HELP", CATEGORY_SCREEN, "Help 화면", "도움말 화면 접근"),
                new Permission("SCREEN_FILE_LOAD", CATEGORY_SCREEN, "File Load 화면", "파일 로드 화면 접근"),
                new Permission("SCREEN_MAPPING", CATEGORY_SCREEN, "Mapping 화면", "웨이퍼 매핑 화면 접근")
            };
        }

        /// <summary>
        /// 로봇 제어 권한들 (Movement 시스템 기반)
        /// </summary>
        private static List<Permission> GetRobotPermissions()
        {
            return new List<Permission>
            {
                new Permission("ROBOT_MANUAL_CONTROL", CATEGORY_ROBOT, "로봇 수동 제어", "로봇 수동 조작 권한"),
                new Permission("ROBOT_AUTO_EXECUTION", CATEGORY_ROBOT, "로봇 자동 실행", "자동 시퀀스 실행 권한"),
                new Permission("ROBOT_JOG_CONTROL", CATEGORY_ROBOT, "로봇 JOG 제어", "JOG 이동 제어 권한"),
                new Permission("ROBOT_SPEED_CONTROL", CATEGORY_ROBOT, "로봇 속도 제어", "속도 설정 변경 권한"),
                new Permission("ROBOT_EMERGENCY_STOP", CATEGORY_ROBOT, "비상 정지", "비상 정지 버튼 사용 권한"),
                new Permission("ROBOT_HOME_RETURN", CATEGORY_ROBOT, "홈 복귀", "홈 포지션 이동 권한"),
                new Permission("ROBOT_POSITION_RESET", CATEGORY_ROBOT, "위치 리셋", "로봇 위치 초기화 권한")
            };
        }

        /// <summary>
        /// Teaching 시스템 권한들
        /// </summary>
        private static List<Permission> GetTeachingPermissions()
        {
            return new List<Permission>
            {
                new Permission("TEACHING_VIEW", CATEGORY_TEACHING, "Teaching 조회", "Teaching 데이터 조회 권한"),
                new Permission("TEACHING_MODIFY", CATEGORY_TEACHING, "Teaching 수정", "Teaching 좌표 수정 권한"),
                new Permission("TEACHING_CREATE", CATEGORY_TEACHING, "Teaching 생성", "새 Teaching 포인트 생성 권한"),
                new Permission("TEACHING_DELETE", CATEGORY_TEACHING, "Teaching 삭제", "Teaching 포인트 삭제 권한"),
                new Permission("TEACHING_EXPORT", CATEGORY_TEACHING, "Teaching 내보내기", "Teaching 데이터 내보내기 권한"),
                new Permission("TEACHING_IMPORT", CATEGORY_TEACHING, "Teaching 가져오기", "Teaching 데이터 가져오기 권한")
            };
        }

        /// <summary>
        /// 레시피 시스템 권한들 (7단계 완성 시스템 기반)
        /// </summary>
        private static List<Permission> GetRecipePermissions()
        {
            return new List<Permission>
            {
                new Permission("RECIPE_VIEW", CATEGORY_RECIPE, "레시피 조회", "레시피 목록 및 내용 조회 권한"),
                new Permission("RECIPE_EXECUTE", CATEGORY_RECIPE, "레시피 실행", "레시피 실행 권한"),
                new Permission("RECIPE_CREATE", CATEGORY_RECIPE, "레시피 생성", "새 레시피 생성 권한"),
                new Permission("RECIPE_EDIT", CATEGORY_RECIPE, "레시피 편집", "기존 레시피 수정 권한"),
                new Permission("RECIPE_DELETE", CATEGORY_RECIPE, "레시피 삭제", "레시피 삭제 권한"),
                new Permission("RECIPE_TEMPLATE", CATEGORY_RECIPE, "레시피 템플릿", "템플릿 생성 및 관리 권한"),
                new Permission("RECIPE_BACKUP", CATEGORY_RECIPE, "레시피 백업", "레시피 백업 및 복원 권한"),
                new Permission("RECIPE_PARAMETER", CATEGORY_RECIPE, "레시피 매개변수", "실행 매개변수 변경 권한")
            };
        }

        /// <summary>
        /// 시스템 관리 권한들
        /// </summary>
        private static List<Permission> GetSystemPermissions()
        {
            return new List<Permission>
            {
                new Permission("SYSTEM_CONFIG", CATEGORY_SYSTEM, "시스템 설정", "시스템 구성 설정 권한"),
                new Permission("SYSTEM_MODE_CHANGE", CATEGORY_SYSTEM, "모드 변경", "Manual/Auto/Emergency 모드 변경 권한"),
                new Permission("SYSTEM_CALIBRATION", CATEGORY_SYSTEM, "시스템 캘리브레이션", "시스템 캘리브레이션 권한"),
                new Permission("SYSTEM_MAINTENANCE", CATEGORY_SYSTEM, "시스템 유지보수", "유지보수 모드 접근 권한"),
                new Permission("SYSTEM_FIRMWARE", CATEGORY_SYSTEM, "펌웨어 업데이트", "펌웨어 업데이트 권한"),
                new Permission("SYSTEM_RESTART", CATEGORY_SYSTEM, "시스템 재시작", "시스템 재시작 권한"),
                new Permission("SYSTEM_SHUTDOWN", CATEGORY_SYSTEM, "시스템 종료", "시스템 종료 권한")
            };
        }

        /// <summary>
        /// 안전 시스템 권한들 (SafetySystem 기반)
        /// </summary>
        private static List<Permission> GetSafetyPermissions()
        {
            return new List<Permission>
            {
                new Permission("SAFETY_VIEW", CATEGORY_SAFETY, "안전 상태 조회", "안전 시스템 상태 조회 권한"),
                new Permission("SAFETY_INTERLOCK", CATEGORY_SAFETY, "인터록 관리", "인터록 장치 관리 권한"),
                new Permission("SAFETY_LIMIT", CATEGORY_SAFETY, "안전 한계 설정", "소프트 리미트 설정 권한"),
                new Permission("SAFETY_OVERRIDE", CATEGORY_SAFETY, "안전 오버라이드", "안전 시스템 임시 해제 권한"),
                new Permission("SAFETY_CONFIG", CATEGORY_SAFETY, "안전 설정", "안전 시스템 구성 변경 권한"),
                new Permission("SAFETY_RESET", CATEGORY_SAFETY, "안전 시스템 리셋", "안전 시스템 초기화 권한")
            };
        }

        /// <summary>
        /// 사용자 관리 권한들
        /// </summary>
        private static List<Permission> GetUserManagementPermissions()
        {
            return new List<Permission>
            {
                new Permission("USER_VIEW", CATEGORY_USER, "사용자 조회", "사용자 목록 조회 권한"),
                new Permission("USER_CREATE", CATEGORY_USER, "사용자 생성", "새 사용자 계정 생성 권한"),
                new Permission("USER_MODIFY", CATEGORY_USER, "사용자 수정", "사용자 정보 수정 권한"),
                new Permission("USER_DELETE", CATEGORY_USER, "사용자 삭제", "사용자 계정 삭제 권한"),
                new Permission("USER_ROLE_ASSIGN", CATEGORY_USER, "역할 할당", "사용자 역할 변경 권한"),
                new Permission("USER_PASSWORD_RESET", CATEGORY_USER, "비밀번호 리셋", "사용자 비밀번호 초기화 권한"),
                new Permission("USER_UNLOCK", CATEGORY_USER, "계정 잠금 해제", "잠긴 계정 해제 권한"),
                new Permission("USER_SESSION", CATEGORY_USER, "세션 관리", "사용자 세션 관리 권한")
            };
        }

        /// <summary>
        /// 데이터 관리 권한들
        /// </summary>
        private static List<Permission> GetDataManagementPermissions()
        {
            return new List<Permission>
            {
                new Permission("DATA_BACKUP", CATEGORY_DATA, "데이터 백업", "시스템 데이터 백업 권한"),
                new Permission("DATA_RESTORE", CATEGORY_DATA, "데이터 복원", "백업 데이터 복원 권한"),
                new Permission("DATA_EXPORT", CATEGORY_DATA, "데이터 내보내기", "데이터 파일 내보내기 권한"),
                new Permission("DATA_IMPORT", CATEGORY_DATA, "데이터 가져오기", "외부 데이터 가져오기 권한"),
                new Permission("DATA_DELETE", CATEGORY_DATA, "데이터 삭제", "시스템 데이터 삭제 권한"),
                new Permission("LOG_VIEW", CATEGORY_DATA, "로그 조회", "시스템 로그 조회 권한"),
                new Permission("LOG_CLEAR", CATEGORY_DATA, "로그 삭제", "시스템 로그 삭제 권한")
            };
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 권한 유효성 검증
        /// </summary>
        /// <returns>유효 여부</returns>
        public bool IsValid()
        {
            try
            {
                return !string.IsNullOrWhiteSpace(PermissionId) &&
                       !string.IsNullOrWhiteSpace(Category) &&
                       !string.IsNullOrWhiteSpace(Name);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 권한 유효성 검증 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 권한 복제
        /// </summary>
        /// <returns>복제된 권한</returns>
        public Permission Clone()
        {
            try
            {
                return new Permission(PermissionId, Category, Name, Description, IsAllowed);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 권한 복제 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 문자열 표현
        /// </summary>
        /// <returns>권한 정보 문자열</returns>
        public override string ToString()
        {
            return $"{Category}.{Name} [{(IsAllowed ? "허용" : "거부")}]";
        }

        /// <summary>
        /// 권한 비교 (ID 기준)
        /// </summary>
        /// <param name="obj">비교 대상</param>
        /// <returns>같은 권한 여부</returns>
        public override bool Equals(object obj)
        {
            try
            {
                var other = obj as Permission;
                if (other == null) return false;
                return string.Equals(PermissionId, other.PermissionId, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 권한 비교 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 해시코드 반환
        /// </summary>
        /// <returns>해시코드</returns>
        public override int GetHashCode()
        {
            try
            {
                return PermissionId?.GetHashCode() ?? 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 해시코드 생성 실패: {ex.Message}");
                return 0;
            }
        }
        #endregion
    }
}