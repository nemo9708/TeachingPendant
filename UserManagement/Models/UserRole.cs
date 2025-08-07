using System;
using System.ComponentModel;

namespace TeachingPendant.UserManagement.Models
{
    /// <summary>
    /// 웨이퍼 반송 로봇 시스템의 사용자 권한 역할
    /// 시스템의 보안과 운영 효율성을 위한 계층적 권한 구조
    /// </summary>
    public enum UserRole
    {
        /// <summary>
        /// 게스트 - 읽기 전용 모니터링
        /// Monitor 화면만 접근 가능
        /// 로봇 동작 불가, 설정 변경 불가
        /// </summary>
        [Description("게스트")]
        Guest = 0,

        /// <summary>
        /// 운영자 - 기본 운영 권한
        /// Movement, Monitor, Teaching, IO 접근 가능
        /// 레시피 실행 가능, 웨이퍼 매핑 가능
        /// 시스템 설정 변경 불가
        /// </summary>
        [Description("운영자")]
        Operator = 1,

        /// <summary>
        /// 엔지니어 - 고급 운영 권한
        /// 모든 화면 접근 가능 (관리자 기능 제외)
        /// Teaching 좌표 수정 가능
        /// 레시피 생성/편집 가능
        /// 시스템 설정 일부 수정 가능
        /// </summary>
        [Description("엔지니어")]
        Engineer = 2,

        /// <summary>
        /// 관리자 - 전체 시스템 권한
        /// 모든 기능 접근 가능
        /// 사용자 관리, 시스템 설정, 보안 정책
        /// 안전 시스템 설정, 하드웨어 구성
        /// 모든 데이터 백업/복원
        /// </summary>
        [Description("관리자")]
        Administrator = 3
    }

    /// <summary>
    /// UserRole enum 확장 메서드 및 유틸리티
    /// </summary>
    public static class UserRoleExtensions
    {
        private static readonly string CLASS_NAME = "UserRoleExtensions";

        /// <summary>
        /// UserRole의 Description 속성 값 반환
        /// </summary>
        /// <param name="role">사용자 역할</param>
        /// <returns>한글 설명</returns>
        public static string GetDescription(this UserRole role)
        {
            try
            {
                var field = role.GetType().GetField(role.ToString());
                if (field != null)
                {
                    var attribute = (DescriptionAttribute)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
                    if (attribute != null)
                    {
                        return attribute.Description;
                    }
                }
                return role.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Description 조회 실패: {ex.Message}");
                return role.ToString();
            }
        }

        /// <summary>
        /// 특정 화면에 대한 접근 권한 확인
        /// 현재 프로젝트의 ValidateScreenAccess와 연동 예정
        /// </summary>
        /// <param name="role">사용자 역할</param>
        /// <param name="screenName">화면 이름</param>
        /// <returns>접근 가능 여부</returns>
        public static bool CanAccessScreen(this UserRole role, string screenName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(screenName))
                    return false;

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 화면 접근 권한 확인: {role} -> {screenName}");

                switch (role)
                {
                    case UserRole.Guest:
                        // 게스트는 모니터링만 가능
                        return screenName.Equals("Monitor", StringComparison.OrdinalIgnoreCase);

                    case UserRole.Operator:
                        // 운영자는 기본 운영 화면 접근 가능
                        var operatorScreens = new[] { "Movement", "Monitor", "Teaching", "I/O", "File Load", "Mapping" };
                        return Array.Exists(operatorScreens, screen =>
                            screen.Equals(screenName, StringComparison.OrdinalIgnoreCase));

                    case UserRole.Engineer:
                        // 엔지니어는 관리자 전용 제외하고 모든 화면 접근 가능
                        var restrictedScreens = new[] { "User Management", "System Config", "Security" };
                        return !Array.Exists(restrictedScreens, screen =>
                            screen.Equals(screenName, StringComparison.OrdinalIgnoreCase));

                    case UserRole.Administrator:
                        // 관리자는 모든 화면 접근 가능
                        return true;

                    default:
                        System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 알 수 없는 역할: {role}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 화면 접근 권한 확인 실패: {ex.Message}");
                // 오류 시 안전하게 접근 거부
                return false;
            }
        }

        /// <summary>
        /// Teaching 좌표 수정 권한 확인
        /// </summary>
        /// <param name="role">사용자 역할</param>
        /// <returns>수정 가능 여부</returns>
        public static bool CanModifyTeaching(this UserRole role)
        {
            try
            {
                // 엔지니어 이상만 Teaching 좌표 수정 가능
                return role >= UserRole.Engineer;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Teaching 수정 권한 확인 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 레시피 편집 권한 확인
        /// </summary>
        /// <param name="role">사용자 역할</param>
        /// <returns>편집 가능 여부</returns>
        public static bool CanEditRecipe(this UserRole role)
        {
            try
            {
                // 운영자 이상은 레시피 편집 가능
                return role >= UserRole.Operator;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 레시피 편집 권한 확인 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 레시피 실행 권한 확인
        /// </summary>
        /// <param name="role">사용자 역할</param>
        /// <returns>실행 가능 여부</returns>
        public static bool CanExecuteRecipe(this UserRole role)
        {
            try
            {
                // 운영자 이상은 레시피 실행 가능
                return role >= UserRole.Operator;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 레시피 실행 권한 확인 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 로봇 수동 조작 권한 확인
        /// </summary>
        /// <param name="role">사용자 역할</param>
        /// <returns>조작 가능 여부</returns>
        public static bool CanControlRobot(this UserRole role)
        {
            try
            {
                // 운영자 이상은 로봇 수동 조작 가능
                return role >= UserRole.Operator;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로봇 조작 권한 확인 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 시스템 설정 변경 권한 확인
        /// </summary>
        /// <param name="role">사용자 역할</param>
        /// <returns>변경 가능 여부</returns>
        public static bool CanModifySystemSettings(this UserRole role)
        {
            try
            {
                // 관리자만 시스템 설정 변경 가능
                return role >= UserRole.Administrator;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 시스템 설정 권한 확인 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 사용자 관리 권한 확인
        /// </summary>
        /// <param name="role">사용자 역할</param>
        /// <returns>관리 가능 여부</returns>
        public static bool CanManageUsers(this UserRole role)
        {
            try
            {
                // 관리자만 사용자 관리 가능
                return role >= UserRole.Administrator;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 관리 권한 확인 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 안전 시스템 설정 권한 확인
        /// </summary>
        /// <param name="role">사용자 역할</param>
        /// <returns>설정 가능 여부</returns>
        public static bool CanModifySafetySettings(this UserRole role)
        {
            try
            {
                // 관리자만 안전 시스템 설정 변경 가능
                return role >= UserRole.Administrator;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 안전 시스템 권한 확인 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 역할별 권한 요약 정보 반환
        /// </summary>
        /// <param name="role">사용자 역할</param>
        /// <returns>권한 요약 문자열</returns>
        public static string GetPermissionSummary(this UserRole role)
        {
            try
            {
                switch (role)
                {
                    case UserRole.Guest:
                        return "모니터링만 가능 (읽기 전용)";

                    case UserRole.Operator:
                        return "기본 운영: 로봇 조작, 레시피 실행, 웨이퍼 매핑";

                    case UserRole.Engineer:
                        return "고급 운영: Teaching 수정, 레시피 편집, 시스템 진단";

                    case UserRole.Administrator:
                        return "전체 관리: 사용자 관리, 시스템 설정, 보안 정책";

                    default:
                        return "알 수 없는 권한";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 권한 요약 생성 실패: {ex.Message}");
                return "권한 정보 없음";
            }
        }
    }
}