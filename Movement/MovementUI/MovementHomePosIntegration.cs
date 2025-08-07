using System;
using System.Collections.Generic;
using TeachingPendant.Alarm;

namespace TeachingPendant.MovementUI
{
    /// <summary>
    /// Movement UI와 Setup HomePos 간의 연동을 담당하는 헬퍼 클래스
    /// </summary>
    public static class MovementHomePosIntegration
    {
        private static Movement _movementInstance;

        /// <summary>
        /// Movement 인스턴스 등록
        /// </summary>
        public static void RegisterMovementInstance(Movement movementInstance)
        {
            _movementInstance = movementInstance;
            System.Diagnostics.Debug.WriteLine("Movement instance registered for HomePos integration");
        }

        /// <summary>
        /// Movement 인스턴스 등록 해제
        /// </summary>
        public static void UnregisterMovementInstance()
        {
            _movementInstance = null;
            System.Diagnostics.Debug.WriteLine("Movement instance unregistered from HomePos integration");
        }

        /// <summary>
        /// 모든 메뉴(CPick, CPlace, SPick, SPlace)의 P1에 HomePos 적용
        /// </summary>
        public static void ApplyHomePosToAllMenuP1(decimal posA, decimal posT, decimal posZ)
        {
            if (_movementInstance == null)
            {
                System.Diagnostics.Debug.WriteLine("Movement instance not registered. Cannot apply HomePos.");
                return;
            }

            try
            {
                var targetMenus = new[] { "CPick", "CPlace", "SPick", "SPlace" };

                foreach (string menuType in targetMenus)
                {
                    _movementInstance.ApplyHomePosToMenuP1(menuType, posA, posT, posZ);
                }

                AlarmMessageManager.ShowAlarm(Alarms.POSITION_LOADED,
                    $"HomePos applied to all Movement P1 coordinates: A={posA:F2}, T={posT:F2}, Z={posZ:F2}");
            }
            catch (Exception ex)
            {
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                    $"Failed to apply HomePos to Movement: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error applying HomePos: {ex.Message}");
            }
        }
    }
}
