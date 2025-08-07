using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization; // For double.Parse

namespace TeachingPendant.MovementUI
{
    // Helper struct for Cartesian coordinates
    public struct CartesianPoint3D
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public CartesianPoint3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// 유효성 검사 결과를 담는 클래스
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }


    // Movement UI와 Teaching UI 간 데이터 전달을 위한 헬퍼 클래스
    public static class MovementDataHelper
    {
        // Movement 인스턴스 참조
        private static Movement _movementInstance;

        // Movement 인스턴스 설정 메서드
        public static void SetMovementInstance(Movement movementInstance)
        {
            _movementInstance = movementInstance;
        }

        // 모든 PICK 위치 가져오기
        public static List<string> GetAllPickPositions()
        {
            // 실제 구현에서는 Movement 클래스에서 모든 PICK 위치 목록을 가져옴
            // 여기서는 예시로 기본 PICK 위치만 반환
            return new List<string> { "PICK1", "PICK2", "PICK3" };
        }

        // PICK P3 좌표 업데이트 (P2에서 P3로 변경)
        public static void UpdatePickP3Coordinates(string pickPosition, decimal positionA, decimal positionT, decimal positionZ)
        {
            if (_movementInstance != null)
            {
                // Movement 인스턴스에서 실제 좌표 업데이트 메서드 호출
                _movementInstance.UpdatePickP3Coordinates(pickPosition, positionA, positionT, positionZ);
            }
        }

        /// <summary>
        /// (A, T, Z) 문자열 배열 좌표를 직교 좌표계(X, Y, Z)로 변환합니다.
        /// </summary>
        public static CartesianPoint3D ConvertATZToCartesian(string[] atzPointData)
        {
            if (atzPointData == null || atzPointData.Length < 3)
            {
                System.Diagnostics.Debug.WriteLine("Error: ConvertATZToCartesian - Invalid point data array. Expected at least 3 elements for A, T, Z.");
                return new CartesianPoint3D(0, 0, 0);
            }

            try
            {
                double r = double.Parse(atzPointData[0], CultureInfo.InvariantCulture);
                double theta = double.Parse(atzPointData[1], CultureInfo.InvariantCulture);
                double z = double.Parse(atzPointData[2], CultureInfo.InvariantCulture);

                double thetaRadians = theta * Math.PI / 180.0;
                double x = r * Math.Cos(thetaRadians);
                double y = r * Math.Sin(thetaRadians);

                return new CartesianPoint3D(x, y, z);
            }
            catch (FormatException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: ConvertATZToCartesian - Parsing ATZ point data failed. Data: [{string.Join(", ", atzPointData)}]. Error: {ex.Message}");
                return new CartesianPoint3D(0, 0, 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: ConvertATZToCartesian - Unexpected error. Data: [{string.Join(", ", atzPointData)}]. Error: {ex.Message}");
                return new CartesianPoint3D(0, 0, 0);
            }
        }

        /// <summary>
        /// 두 (A, T, Z) 포인트 데이터 간의 유클리드 거리를 계산합니다.
        /// </summary>
        public static double CalculateDistanceBetweenATZPoints(string[] atzPoint1Data, string[] atzPoint2Data)
        {
            try
            {
                CartesianPoint3D p1Cartesian = ConvertATZToCartesian(atzPoint1Data);
                CartesianPoint3D p2Cartesian = ConvertATZToCartesian(atzPoint2Data);

                double deltaX = p2Cartesian.X - p1Cartesian.X;
                double deltaY = p2Cartesian.Y - p1Cartesian.Y;
                double deltaZ = p2Cartesian.Z - p1Cartesian.Z;

                return Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: CalculateDistanceBetweenATZPoints - Failed to calculate distance. Error: {ex.Message}");
                return -1.0;
            }
        }

        /// <summary>
        /// 주어진 가속도, 감속도, 이동 거리를 사용하여 이론적인 최고 속도 (c)를 계산합니다.
        /// </summary>
        public static double CalculateMaximumSpeedC(double acceleration, double deceleration, double travelDistanceL)
        {
            if (acceleration <= 0 || deceleration <= 0 || travelDistanceL < 0)
            {
                return 0;
            }

            if (travelDistanceL == 0)
            {
                return 0;
            }

            double numerator = 2 * acceleration * deceleration * travelDistanceL;
            double denominator = acceleration + deceleration;

            if (denominator == 0) return 0;

            double speedSquared = numerator / denominator;

            return speedSquared < 0 ? 0 : Math.Sqrt(speedSquared);
        }

        /// <summary>
        /// 주어진 좌표가 Setup에 정의된 소프트 리미트 범위 내에 있는지 확인합니다.
        /// </summary>
        public static ValidationResult CheckSoftLimits(decimal targetA, decimal targetT, decimal targetZ)
        {
            // A축 리미트 확인
            if (targetA < SetupUI.Setup.SoftLimitA1 || targetA > SetupUI.Setup.SoftLimitA2)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = $"A-axis limit exceeded. Range: ({SetupUI.Setup.SoftLimitA1} ~ {SetupUI.Setup.SoftLimitA2})" };
            }

            // T축 리미트 확인
            if (targetT < SetupUI.Setup.SoftLimitT1 || targetT > SetupUI.Setup.SoftLimitT2)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = $"T-axis limit exceeded. Range: ({SetupUI.Setup.SoftLimitT1} ~ {SetupUI.Setup.SoftLimitT2})" };
            }

            // Z축 리미트 확인
            if (targetZ < SetupUI.Setup.SoftLimitZ1 || targetZ > SetupUI.Setup.SoftLimitZ2)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = $"Z-axis limit exceeded. Range: ({SetupUI.Setup.SoftLimitZ1} ~ {SetupUI.Setup.SoftLimitZ2})" };
            }

            return new ValidationResult { IsValid = true, ErrorMessage = string.Empty };
        }

        /// <summary>
        /// 암 링크 길이와 A축 소프트 리미트를 기반으로 총 회전 각도를 계산합니다.
        /// </summary>
        /// <param name="linkLength">암의 링크 길이 (L)</param>
        /// <param name="l_min">A축 소프트 리미트 최소값</param>
        /// <param name="l_max">A축 소프트 리미트 최대값</param>
        /// <returns>계산된 총 회전 각도 (degrees)</returns>
        public static double CalculateFullStrokeAngle(decimal linkLength, decimal l_min, decimal l_max)
        {
            // 유효하지 않은 값에 대한 예외 처리
            if (linkLength <= 0 || l_max > linkLength || l_min < -linkLength)
            {
                return 0.0; // 계산 불가
            }

            try
            {
                // 아크코사인(arccos)을 사용하여 각도를 라디안 단위로 계산
                double theta_min_rad = Math.Acos((double)(l_min / linkLength));
                double theta_max_rad = Math.Acos((double)(l_max / linkLength));

                // 두 각도의 차이를 구하여 총 회전 범위를 계산
                double angle_diff_rad = Math.Abs(theta_max_rad - theta_min_rad);

                // 라디안을 각도(degree)로 변환하여 반환
                return angle_diff_rad * 180.0 / Math.PI;
            }
            catch (Exception)
            {
                // 계산 중 오류 발생 시 (예: l_min > linkLength)
                return 0.0;
            }
        }
    }
}
