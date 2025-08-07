using System;
using System.Collections.Generic;
using System.Linq;
using TeachingPendant.Manager;
using TeachingPendant.UserManagement.Models;
using TeachingPendant.Safety;

namespace TeachingPendant.UserManagement.Services
{
    /// <summary>
    /// 권한 체크 결과 정보
    /// </summary>
    public class PermissionCheckResult
    {
        public bool IsAllowed { get; set; }
        public string DenialReason { get; set; }
        public string RequiredPermission { get; set; }
        public string RequiredRole { get; set; }
        public string RequiredMode { get; set; }

        public PermissionCheckResult(bool isAllowed, string denialReason = null)
        {
            IsAllowed = isAllowed;
            DenialReason = denialReason;
        }

        public static PermissionCheckResult Allow()
        {
            return new PermissionCheckResult(true);
        }

        public static PermissionCheckResult Deny(string reason)
        {
            return new PermissionCheckResult(false, reason);
        }

        public static PermissionCheckResult DenyWithRole(string reason, string requiredRole)
        {
            var result = new PermissionCheckResult(false, reason);
            result.RequiredRole = requiredRole;
            return result;
        }

        public static PermissionCheckResult DenyWithMode(string reason, string requiredMode)
        {
            var result = new PermissionCheckResult(false, reason);
            result.RequiredMode = requiredMode;
            return result;
        }

        public static PermissionCheckResult DenyWithPermission(string reason, string requiredPermission)
        {
            var result = new PermissionCheckResult(false, reason);
            result.RequiredPermission = requiredPermission;
            return result;
        }
    }

    /// <summary>
    /// 권한 체크를 담당하는 중앙 유틸리티 클래스
    /// 기존 ValidateScreenAccess 패턴을 확장하여 사용자 권한까지 통합 관리
    /// </summary>
    public static class PermissionChecker
    {
        #region Private Fields
        private static readonly string CLASS_NAME = "PermissionChecker";

        // Emergency 모드에서 허용되는 화면들 (기존 MainWindow 패턴 유지)
        private static readonly string[] EMERGENCY_ALLOWED_SCREENS =
        {
            "Monitor", "System", "Error Log", "HELP"
        };
        #endregion

        #region Screen Access Permission Checks
        /// <summary>
        /// 화면 접근 권한 종합 체크 (기존 ValidateScreenAccess 개선 버전)
        /// 모드 제한 + 사용자 권한을 통합적으로 검사
        /// </summary>
        /// <param name="screenName">화면 이름</param>
        /// <returns>권한 체크 결과</returns>
        public static PermissionCheckResult CheckScreenAccess(string screenName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 화면 접근 권한 체크: {screenName}");

                if (string.IsNullOrWhiteSpace(screenName))
                    return PermissionCheckResult.Deny("유효하지 않은 화면 이름입니다.");

                // 1. 모드별 접근 제한 확인 (기존 로직 유지)
                var modeCheckResult = CheckModeAccess(screenName);
                if (!modeCheckResult.IsAllowed)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 모드 제한으로 접근 거부: {screenName}");
                    return modeCheckResult;
                }

                // 2. 사용자 권한 확인 (새로 추가)
                var userCheckResult = CheckUserScreenPermission(screenName);
                if (!userCheckResult.IsAllowed)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 권한 부족으로 접근 거부: {screenName}");
                    return userCheckResult;
                }

                // 3. 안전 시스템 상태 확인 (일부 화면만)
                var safetyCheckResult = CheckSafetyRequirement(screenName);
                if (!safetyCheckResult.IsAllowed)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 안전 시스템 상태로 접근 거부: {screenName}");
                    return safetyCheckResult;
                }

                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 화면 접근 허용: {screenName}");
                return PermissionCheckResult.Allow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 화면 접근 권한 체크 중 오류: {ex.Message}");
                return PermissionCheckResult.Deny("권한 확인 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 모드별 화면 접근 제한 확인 (기존 ValidateScreenAccess 로직)
        /// </summary>
        /// <param name="screenName">화면 이름</param>
        /// <returns>모드 접근 결과</returns>
        private static PermissionCheckResult CheckModeAccess(string screenName)
        {
            try
            {
                var currentMode = GlobalModeManager.CurrentMode;

                // Emergency 모드에서는 특정 화면만 접근 가능
                if (currentMode == GlobalMode.Emergency)
                {
                    bool isAllowed = Array.Exists(EMERGENCY_ALLOWED_SCREENS,
                        screen => string.Equals(screen, screenName, StringComparison.OrdinalIgnoreCase));

                    if (!isAllowed)
                    {
                        return PermissionCheckResult.DenyWithMode(
                            $"Emergency 모드에서는 {screenName} 화면에 접근할 수 없습니다.",
                            "Manual 또는 Auto");
                    }
                }

                // Manual, Auto 모드에서는 모든 화면 접근 가능
                return PermissionCheckResult.Allow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 모드 접근 확인 실패: {ex.Message}");
                return PermissionCheckResult.Deny("모드 상태 확인 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 사용자 화면 권한 확인
        /// </summary>
        /// <param name="screenName">화면 이름</param>
        /// <returns>사용자 권한 결과</returns>
        private static PermissionCheckResult CheckUserScreenPermission(string screenName)
        {
            try
            {
                // 로그인 상태 확인
                if (!UserSession.IsLoggedIn)
                {
                    return PermissionCheckResult.DenyWithPermission("로그인이 필요합니다.", "로그인");
                }

                // 사용자 권한으로 화면 접근 확인
                bool canAccess = UserSession.CanAccessScreen(screenName);
                if (!canAccess)
                {
                    var requiredRole = GetMinimumRoleForScreen(screenName);
                    var result = PermissionCheckResult.DenyWithRole(
                        $"{screenName} 화면에 접근할 권한이 없습니다.",
                        requiredRole.GetDescription());
                    result.RequiredPermission = $"SCREEN_{screenName.ToUpper()}";
                    return result;
                }

                return PermissionCheckResult.Allow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 화면 권한 확인 실패: {ex.Message}");
                return PermissionCheckResult.Deny("사용자 권한 확인 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 안전 시스템 요구사항 확인
        /// </summary>
        /// <param name="screenName">화면 이름</param>
        /// <returns>안전 요구사항 결과</returns>
        private static PermissionCheckResult CheckSafetyRequirement(string screenName)
        {
            try
            {
                // 로봇 조작 관련 화면은 안전 시스템 확인 필요
                var safetyRequiredScreens = new[] { "Movement", "Teaching" };

                if (Array.Exists(safetyRequiredScreens,
                    screen => string.Equals(screen, screenName, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!SafetySystem.IsInitialized)
                    {
                        return PermissionCheckResult.Deny("안전 시스템이 초기화되지 않았습니다.");
                    }

                    if (!SafetySystem.IsSafeForRobotOperation())
                    {
                        return PermissionCheckResult.Deny("현재 안전 상태에서는 로봇 조작이 제한됩니다.");
                    }
                }

                return PermissionCheckResult.Allow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 안전 요구사항 확인 실패: {ex.Message}");
                return PermissionCheckResult.Deny("안전 시스템 확인 중 오류가 발생했습니다.");
            }
        }
        #endregion

        #region Functional Permission Checks
        /// <summary>
        /// 로봇 제어 권한 확인
        /// </summary>
        /// <param name="operationType">작업 유형 (Manual, Auto, Jog 등)</param>
        /// <returns>제어 권한 결과</returns>
        public static PermissionCheckResult CheckRobotControlPermission(string operationType)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로봇 제어 권한 체크: {operationType}");

                // 1. 로그인 확인
                if (!UserSession.IsLoggedIn)
                    return PermissionCheckResult.Deny("로그인이 필요합니다.");

                // 2. 사용자 역할 확인
                var currentRole = UserSession.CurrentUserRole;
                if (!currentRole.CanControlRobot())
                {
                    return PermissionCheckResult.DenyWithRole(
                        "로봇 제어 권한이 없습니다.",
                        UserRole.Operator.GetDescription());
                }

                // 3. 모드별 제한 확인
                var currentMode = GlobalModeManager.CurrentMode;
                if (currentMode == GlobalMode.Emergency)
                {
                    return PermissionCheckResult.DenyWithMode(
                        "Emergency 모드에서는 로봇 제어가 제한됩니다.",
                        "Manual 또는 Auto");
                }

                // 4. 자동 실행의 경우 추가 권한 확인
                if (string.Equals(operationType, "Auto", StringComparison.OrdinalIgnoreCase))
                {
                    if (!UserSession.HasPermission("ROBOT_AUTO_EXECUTION"))
                    {
                        return PermissionCheckResult.DenyWithPermission(
                            "자동 실행 권한이 없습니다.",
                            "ROBOT_AUTO_EXECUTION");
                    }
                }

                // 5. 안전 시스템 확인
                if (!SafetySystem.IsSafeForRobotOperation())
                {
                    return PermissionCheckResult.Deny("현재 안전 상태에서는 로봇 제어가 제한됩니다.");
                }

                return PermissionCheckResult.Allow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 로봇 제어 권한 체크 실패: {ex.Message}");
                return PermissionCheckResult.Deny("로봇 제어 권한 확인 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// Teaching 수정 권한 확인
        /// </summary>
        /// <returns>Teaching 수정 권한 결과</returns>
        public static PermissionCheckResult CheckTeachingModifyPermission()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Teaching 수정 권한 체크");

                // 1. 로그인 확인
                if (!UserSession.IsLoggedIn)
                    return PermissionCheckResult.Deny("로그인이 필요합니다.");

                // 2. 사용자 역할 확인
                var currentRole = UserSession.CurrentUserRole;
                if (!currentRole.CanModifyTeaching())
                {
                    return PermissionCheckResult.DenyWithRole(
                        "Teaching 수정 권한이 없습니다.",
                        UserRole.Engineer.GetDescription());
                }

                // 3. 편집 모드 확인
                if (!GlobalModeManager.IsEditingAllowed)
                {
                    return PermissionCheckResult.DenyWithMode(
                        "Manual 모드에서만 Teaching 수정이 가능합니다.",
                        "Manual");
                }

                // 4. 세부 권한 확인
                if (!UserSession.HasPermission("TEACHING_MODIFY"))
                {
                    return PermissionCheckResult.DenyWithPermission(
                        "Teaching 수정 권한이 없습니다.",
                        "TEACHING_MODIFY");
                }

                return PermissionCheckResult.Allow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Teaching 수정 권한 체크 실패: {ex.Message}");
                return PermissionCheckResult.Deny("Teaching 수정 권한 확인 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 레시피 실행 권한 확인
        /// </summary>
        /// <returns>레시피 실행 권한 결과</returns>
        public static PermissionCheckResult CheckRecipeExecutePermission()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 레시피 실행 권한 체크");

                // 1. 로그인 확인
                if (!UserSession.IsLoggedIn)
                    return PermissionCheckResult.Deny("로그인이 필요합니다.");

                // 2. 사용자 역할 확인
                var currentRole = UserSession.CurrentUserRole;
                if (!currentRole.CanExecuteRecipe())
                {
                    return PermissionCheckResult.DenyWithRole(
                        "레시피 실행 권한이 없습니다.",
                        UserRole.Operator.GetDescription());
                }

                // 3. 세부 권한 확인
                if (!UserSession.HasPermission("RECIPE_EXECUTE"))
                {
                    return PermissionCheckResult.DenyWithPermission(
                        "레시피 실행 권한이 없습니다.",
                        "RECIPE_EXECUTE");
                }

                // 4. 안전 시스템 확인
                if (!SafetySystem.IsSafeForRobotOperation())
                {
                    return PermissionCheckResult.Deny("현재 안전 상태에서는 레시피 실행이 제한됩니다.");
                }

                return PermissionCheckResult.Allow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 레시피 실행 권한 체크 실패: {ex.Message}");
                return PermissionCheckResult.Deny("레시피 실행 권한 확인 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 시스템 설정 변경 권한 확인
        /// </summary>
        /// <returns>시스템 설정 권한 결과</returns>
        public static PermissionCheckResult CheckSystemConfigPermission()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 시스템 설정 권한 체크");

                // 1. 로그인 확인
                if (!UserSession.IsLoggedIn)
                    return PermissionCheckResult.Deny("로그인이 필요합니다.");

                // 2. 관리자 권한 확인
                var currentRole = UserSession.CurrentUserRole;
                if (!currentRole.CanModifySystemSettings())
                {
                    return PermissionCheckResult.DenyWithRole(
                        "시스템 설정 변경 권한이 없습니다.",
                        UserRole.Administrator.GetDescription());
                }

                // 3. 세부 권한 확인
                if (!UserSession.HasPermission("SYSTEM_CONFIG"))
                {
                    return PermissionCheckResult.DenyWithPermission(
                        "시스템 설정 권한이 없습니다.",
                        "SYSTEM_CONFIG");
                }

                return PermissionCheckResult.Allow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 시스템 설정 권한 체크 실패: {ex.Message}");
                return PermissionCheckResult.Deny("시스템 설정 권한 확인 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 사용자 관리 권한 확인
        /// </summary>
        /// <returns>사용자 관리 권한 결과</returns>
        public static PermissionCheckResult CheckUserManagementPermission()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 관리 권한 체크");

                // 1. 로그인 확인
                if (!UserSession.IsLoggedIn)
                    return PermissionCheckResult.Deny("로그인이 필요합니다.");

                // 2. 관리자 권한 확인
                var currentRole = UserSession.CurrentUserRole;
                if (!currentRole.CanManageUsers())
                {
                    return PermissionCheckResult.DenyWithRole(
                        "사용자 관리 권한이 없습니다.",
                        UserRole.Administrator.GetDescription());
                }

                // 3. 세부 권한 확인
                if (!UserSession.HasPermission("USER_VIEW"))
                {
                    return PermissionCheckResult.DenyWithPermission(
                        "사용자 관리 권한이 없습니다.",
                        "USER_VIEW");
                }

                return PermissionCheckResult.Allow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 관리 권한 체크 실패: {ex.Message}");
                return PermissionCheckResult.Deny("사용자 관리 권한 확인 중 오류가 발생했습니다.");
            }
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// 화면별 최소 요구 역할 반환
        /// </summary>
        /// <param name="screenName">화면 이름</param>
        /// <returns>최소 요구 역할</returns>
        private static UserRole GetMinimumRoleForScreen(string screenName)
        {
            try
            {
                switch (screenName?.ToUpper())
                {
                    case "MONITOR":
                    case "HELP":
                        return UserRole.Guest;

                    case "MOVEMENT":
                    case "I/O":
                    case "FILE LOAD":
                    case "MAPPING":
                        return UserRole.Operator;

                    case "TEACHING":
                        return UserRole.Engineer;

                    case "SYSTEM":
                    case "SETTING":
                        return UserRole.Administrator;

                    default:
                        return UserRole.Operator; // 기본값
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 화면별 최소 역할 조회 실패: {ex.Message}");
                return UserRole.Administrator; // 안전하게 최고 권한 요구
            }
        }

        /// <summary>
        /// 권한 체크 결과를 사용자 친화적 메시지로 변환
        /// </summary>
        /// <param name="result">권한 체크 결과</param>
        /// <returns>사용자 메시지</returns>
        public static string GetUserFriendlyMessage(PermissionCheckResult result)
        {
            try
            {
                if (result.IsAllowed)
                    return "권한이 확인되었습니다.";

                var message = result.DenialReason ?? "접근이 거부되었습니다.";

                if (!string.IsNullOrWhiteSpace(result.RequiredRole))
                    message += $"\n필요한 권한: {result.RequiredRole}";

                if (!string.IsNullOrWhiteSpace(result.RequiredMode))
                    message += $"\n필요한 모드: {result.RequiredMode}";

                return message;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 사용자 메시지 생성 실패: {ex.Message}");
                return "권한 확인 중 오류가 발생했습니다.";
            }
        }

        /// <summary>
        /// 현재 사용자의 권한 요약 정보
        /// </summary>
        /// <returns>권한 요약</returns>
        public static string GetCurrentUserPermissionSummary()
        {
            try
            {
                if (!UserSession.IsLoggedIn)
                    return "로그인되지 않음";

                var currentUser = UserSession.CurrentUser;
                var currentMode = GlobalModeManager.CurrentMode;

                return $"사용자: {currentUser.UserName} ({currentUser.Role.GetDescription()})\n" +
                       $"모드: {currentMode}\n" +
                       $"편집 가능: {(GlobalModeManager.IsEditingAllowed ? "예" : "아니오")}\n" +
                       $"안전 상태: {(SafetySystem.IsSafeForRobotOperation() ? "안전" : "주의")}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 권한 요약 생성 실패: {ex.Message}");
                return "권한 정보 조회 실패";
            }
        }

        /// <summary>
        /// 빠른 권한 체크 (bool 반환)
        /// </summary>
        /// <param name="permissionId">권한 ID</param>
        /// <returns>권한 여부</returns>
        public static bool HasPermission(string permissionId)
        {
            try
            {
                return UserSession.IsLoggedIn && UserSession.HasPermission(permissionId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 빠른 권한 체크 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 빠른 화면 접근 체크 (bool 반환)
        /// </summary>
        /// <param name="screenName">화면 이름</param>
        /// <returns>접근 가능 여부</returns>
        public static bool CanAccessScreen(string screenName)
        {
            try
            {
                var result = CheckScreenAccess(screenName);
                return result.IsAllowed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 빠른 화면 접근 체크 실패: {ex.Message}");
                return false;
            }
        }
        #endregion


    }
}