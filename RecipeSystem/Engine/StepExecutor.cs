// =============================================================================
// RecipeSystem/Engine/StepExecutor.cs
// 개별 스텝 실행을 담당하는 전용 클래스
// =============================================================================
using System;
using System.Threading;
using System.Threading.Tasks;
using TeachingPendant.RecipeSystem.Models;
using TeachingPendant.HardwareControllers;
using TeachingPendant.Safety;
using TeachingPendant.Manager;

namespace TeachingPendant.RecipeSystem.Engine
{
    /// <summary>
    /// 레시피 스텝 실행 전용 클래스
    /// RecipeEngine에서 호출되어 각 스텝의 세부 실행을 담당
    /// </summary>
    public class StepExecutor
    {
        #region Private Fields
        private readonly IRobotController _robotController;
        private readonly RecipeParameters _parameters;
        #endregion

        #region Constructor
        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="robotController">로봇 컨트롤러</param>
        /// <param name="parameters">레시피 매개변수</param>
        public StepExecutor(IRobotController robotController, RecipeParameters parameters)
        {
            _robotController = robotController ?? throw new ArgumentNullException(nameof(robotController));
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 고급 Pick 스텝 실행 (여러 단계로 구성)
        /// </summary>
        /// <param name="step">Pick 스텝</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>실행 성공 여부</returns>
        public async Task<bool> ExecuteAdvancedPickAsync(RecipeStep step, CancellationToken cancellationToken)
        {
            try
            {
                var position = step.TargetPosition;
                int speed = step.Speed > 0 ? step.Speed : _parameters.PickSpeed;

                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 고급 Pick 실행 시작: {position}");

                // 1. 안전 높이 확보
                var approachPos = new Position(position.R, position.Theta, _parameters.SafeHeight);
                if (!await MoveWithSpeedAsync(approachPos, speed, cancellationToken))
                {
                    return false;
                }

                // 2. Pick 위치 상공으로 이동
                var abovePickPos = new Position(position.R, position.Theta, position.Z + _parameters.PickHeight + 10);
                if (!await MoveWithSpeedAsync(abovePickPos, speed, cancellationToken))
                {
                    return false;
                }

                // 3. 진공 준비 (미리 ON)
                if (_parameters.UseVacuum)
                {
                    await _robotController.SetVacuumAsync(true);
                    await Task.Delay(200, cancellationToken); // 진공 안정화 대기
                }

                // 4. Pick 위치로 하강
                var pickPos = new Position(position.R, position.Theta, position.Z - _parameters.PickHeight);
                if (!await MoveWithSpeedAsync(pickPos, Math.Min(speed, 30), cancellationToken)) // 하강은 느리게
                {
                    return false;
                }

                // 5. 웨이퍼 접촉 확인 (실제로는 센서 피드백)
                await Task.Delay(100, cancellationToken);

                // 6. Pick 동작 실행
                bool pickResult = await _robotController.PickAsync();
                if (!pickResult)
                {
                    System.Diagnostics.Debug.WriteLine("[StepExecutor] Pick 동작 실패");
                    return false;
                }

                // 7. Pick 후 안정화 대기
                if (_parameters.PickDelayMs > 0)
                {
                    await Task.Delay(_parameters.PickDelayMs, cancellationToken);
                }

                // 8. 안전 높이로 상승
                if (!await MoveWithSpeedAsync(approachPos, speed, cancellationToken))
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("[StepExecutor] 고급 Pick 완료");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 고급 Pick 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 고급 Place 스텝 실행 (여러 단계로 구성)
        /// </summary>
        /// <param name="step">Place 스텝</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>실행 성공 여부</returns>
        public async Task<bool> ExecuteAdvancedPlaceAsync(RecipeStep step, CancellationToken cancellationToken)
        {
            try
            {
                var position = step.TargetPosition;
                int speed = step.Speed > 0 ? step.Speed : _parameters.PlaceSpeed;

                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 고급 Place 실행 시작: {position}");

                // 1. 안전 높이 확보
                var approachPos = new Position(position.R, position.Theta, _parameters.SafeHeight);
                if (!await MoveWithSpeedAsync(approachPos, speed, cancellationToken))
                {
                    return false;
                }

                // 2. Place 위치 상공으로 이동
                var abovePlacePos = new Position(position.R, position.Theta, position.Z + _parameters.PlaceHeight + 10);
                if (!await MoveWithSpeedAsync(abovePlacePos, speed, cancellationToken))
                {
                    return false;
                }

                // 3. Place 위치로 하강
                var placePos = new Position(position.R, position.Theta, position.Z - _parameters.PlaceHeight);
                if (!await MoveWithSpeedAsync(placePos, Math.Min(speed, 30), cancellationToken)) // 하강은 느리게
                {
                    return false;
                }

                // 4. 웨이퍼 접촉 확인
                await Task.Delay(100, cancellationToken);

                // 5. Place 동작 실행 (진공 OFF)
                bool placeResult = await _robotController.PlaceAsync();
                if (!placeResult)
                {
                    System.Diagnostics.Debug.WriteLine("[StepExecutor] Place 동작 실패");
                    return false;
                }

                // 6. Place 후 안정화 대기
                if (_parameters.PlaceDelayMs > 0)
                {
                    await Task.Delay(_parameters.PlaceDelayMs, cancellationToken);
                }

                // 7. 약간 상승 후 진공 완전 OFF 확인
                var slightUpPos = new Position(position.R, position.Theta, position.Z + 5);
                await MoveWithSpeedAsync(slightUpPos, 20, cancellationToken);

                if (_parameters.UseVacuum)
                {
                    await _robotController.SetVacuumAsync(false);
                    await Task.Delay(200, cancellationToken); // 진공 해제 안정화
                }

                // 8. 안전 높이로 상승
                if (!await MoveWithSpeedAsync(approachPos, speed, cancellationToken))
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("[StepExecutor] 고급 Place 완료");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 고급 Place 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 안전한 이동 실행 (SoftLimit 확인 포함)
        /// </summary>
        /// <param name="position">목표 위치</param>
        /// <param name="speed">이동 속도</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>이동 성공 여부</returns>
        public async Task<bool> ExecuteSafeMoveAsync(Position position, int speed, CancellationToken cancellationToken)
        {
            try
            {
                // 1. SoftLimit 확인
                if (!SafetySystem.IsWithinSoftLimits(position.R, position.Theta, position.Z))
                {
                    System.Diagnostics.Debug.WriteLine($"[StepExecutor] SoftLimit 범위 초과: {position}");
                    return false;
                }

                // 2. 안전 상태 확인
                if (_parameters.CheckSafetyBeforeEachStep)
                {
                    if (!SafetySystem.IsSafeForRobotOperation())
                    {
                        System.Diagnostics.Debug.WriteLine("[StepExecutor] 안전 조건 미충족");
                        return false;
                    }
                }

                // 3. 이동 실행
                return await MoveWithSpeedAsync(position, speed, cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 안전 이동 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 다중 위치 순차 이동 (경로 최적화 포함)
        /// </summary>
        /// <param name="positions">이동할 위치 목록</param>
        /// <param name="speed">이동 속도</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>모든 이동 성공 여부</returns>
        public async Task<bool> ExecuteMultiMoveAsync(Position[] positions, int speed, CancellationToken cancellationToken)
        {
            try
            {
                if (positions == null || positions.Length == 0)
                {
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 다중 이동 실행: {positions.Length}개 위치");

                for (int i = 0; i < positions.Length; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return false;
                    }

                    var position = positions[i];

                    System.Diagnostics.Debug.WriteLine($"[StepExecutor] 다중 이동 {i + 1}/{positions.Length}: {position}");

                    if (!await ExecuteSafeMoveAsync(position, speed, cancellationToken))
                    {
                        System.Diagnostics.Debug.WriteLine($"[StepExecutor] 다중 이동 실패: 위치 {i + 1}");
                        return false;
                    }

                    // 중간 위치에서 잠깐 대기 (안정화)
                    if (i < positions.Length - 1)
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }

                System.Diagnostics.Debug.WriteLine("[StepExecutor] 다중 이동 완료");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 다중 이동 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 조건부 스텝 실행 (센서 피드백 기반)
        /// </summary>
        /// <param name="step">실행할 스텝</param>
        /// <param name="condition">실행 조건 함수</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>실행 성공 여부</returns>
        public async Task<bool> ExecuteConditionalStepAsync(RecipeStep step, Func<bool> condition, CancellationToken cancellationToken)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 조건부 스텝 실행: {step.Description}");

                // 조건 확인
                if (condition?.Invoke() != true)
                {
                    System.Diagnostics.Debug.WriteLine("[StepExecutor] 실행 조건 미충족 - 스텝 건너뛰기");
                    return true; // 조건 미충족은 오류가 아님
                }

                // 조건 충족 시 스텝 실행
                switch (step.Type)
                {
                    case StepType.Pick:
                        return await ExecuteAdvancedPickAsync(step, cancellationToken);

                    case StepType.Place:
                        return await ExecuteAdvancedPlaceAsync(step, cancellationToken);

                    case StepType.Move:
                        return await ExecuteSafeMoveAsync(step.TargetPosition, step.Speed, cancellationToken);

                    default:
                        System.Diagnostics.Debug.WriteLine($"[StepExecutor] 조건부 실행 미지원 스텝 타입: {step.Type}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 조건부 스텝 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 정밀 Pick 스텝 실행 (센서 피드백 기반)
        /// </summary>
        /// <param name="step">Pick 스텝</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>실행 성공 여부</returns>
        public async Task<bool> ExecutePrecisionPickAsync(RecipeStep step, CancellationToken cancellationToken)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 정밀 Pick 실행: {step.Description}");

                var position = step.TargetPosition;
                int speed = step.Speed > 0 ? step.Speed : _parameters.PickSpeed;

                // 1. 안전 높이로 접근
                var safePos = new Position(position.R, position.Theta, _parameters.SafeHeight);
                if (!await MoveWithSpeedAsync(safePos, speed, cancellationToken))
                {
                    return false;
                }

                // 2. 서서히 하강하면서 센서 확인
                double currentZ = _parameters.SafeHeight;
                double targetZ = position.Z;
                double stepSize = 2.0; // 2mm씩 하강

                while (currentZ > targetZ)
                {
                    currentZ = Math.Max(targetZ, currentZ - stepSize);

                    var intermediatePos = new Position(position.R, position.Theta, currentZ);
                    if (!await MoveWithSpeedAsync(intermediatePos, 20, cancellationToken)) // 느린 속도
                    {
                        return false;
                    }

                    // 센서 확인 (실제로는 압력 센서나 거리 센서 사용)
                    await Task.Delay(50, cancellationToken);

                    // TODO: 실제 센서 피드백 확인
                    // if (SensorManager.IsContactDetected()) break;
                }

                // 3. 진공 ON 및 Pick 동작
                if (_parameters.UseVacuum)
                {
                    await _robotController.SetVacuumAsync(true);
                    await Task.Delay(300, cancellationToken); // 진공 안정화
                }

                bool pickResult = await _robotController.PickAsync();
                if (!pickResult)
                {
                    return false;
                }

                // 4. 안전 높이로 상승
                await MoveWithSpeedAsync(safePos, speed, cancellationToken);

                System.Diagnostics.Debug.WriteLine("[StepExecutor] 정밀 Pick 완료");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 정밀 Pick 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 정밀 Place 스텝 실행 (센서 피드백 기반)
        /// </summary>
        /// <param name="step">Place 스텝</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>실행 성공 여부</returns>
        public async Task<bool> ExecutePrecisionPlaceAsync(RecipeStep step, CancellationToken cancellationToken)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 정밀 Place 실행: {step.Description}");

                var position = step.TargetPosition;
                int speed = step.Speed > 0 ? step.Speed : _parameters.PlaceSpeed;

                // 1. 안전 높이로 접근
                var safePos = new Position(position.R, position.Theta, _parameters.SafeHeight);
                if (!await MoveWithSpeedAsync(safePos, speed, cancellationToken))
                {
                    return false;
                }

                // 2. 서서히 하강하면서 센서 확인
                double currentZ = _parameters.SafeHeight;
                double targetZ = position.Z;
                double stepSize = 1.5; // 1.5mm씩 하강

                while (currentZ > targetZ)
                {
                    currentZ = Math.Max(targetZ, currentZ - stepSize);

                    var intermediatePos = new Position(position.R, position.Theta, currentZ);
                    if (!await MoveWithSpeedAsync(intermediatePos, 15, cancellationToken)) // 매우 느린 속도
                    {
                        return false;
                    }

                    // 센서 확인
                    await Task.Delay(100, cancellationToken);

                    // TODO: 실제 센서 피드백 확인
                    // if (SensorManager.IsContactDetected()) break;
                }

                // 3. Place 동작 실행
                bool placeResult = await _robotController.PlaceAsync();
                if (!placeResult)
                {
                    return false;
                }

                // 4. 진공 해제 및 안정화
                if (_parameters.UseVacuum)
                {
                    await Task.Delay(200, cancellationToken);
                    await _robotController.SetVacuumAsync(false);
                    await Task.Delay(300, cancellationToken);
                }

                // 5. 안전 높이로 상승
                await MoveWithSpeedAsync(safePos, speed, cancellationToken);

                System.Diagnostics.Debug.WriteLine("[StepExecutor] 정밀 Place 완료");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 정밀 Place 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 경로 최적화 이동 (여러 위치를 효율적으로 이동)
        /// </summary>
        /// <param name="positions">이동할 위치 목록</param>
        /// <param name="speed">이동 속도</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>이동 성공 여부</returns>
        public async Task<bool> ExecuteOptimizedPathAsync(Position[] positions, int speed, CancellationToken cancellationToken)
        {
            try
            {
                if (positions == null || positions.Length == 0)
                {
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 경로 최적화 이동: {positions.Length}개 위치");

                // 간단한 경로 최적화 (거리 기반)
                var optimizedPositions = OptimizePath(positions);

                // 최적화된 경로로 이동
                return await ExecuteMultiMoveAsync(optimizedPositions, speed, cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 경로 최적화 이동 오류: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Private Helper Methods
        /// <summary>
        /// 속도 설정과 함께 이동 실행
        /// </summary>
        private async Task<bool> MoveWithSpeedAsync(Position position, int speed, CancellationToken cancellationToken)
        {
            try
            {
                // GlobalSpeedManager에 속도 설정
                GlobalSpeedManager.SetSpeed(Math.Max(1, Math.Min(100, speed)));

                // 이동 실행
                return await _robotController.MoveToAsync(position.R, position.Theta, position.Z);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 속도 설정 이동 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 간단한 경로 최적화 (가장 가까운 위치 우선)
        /// </summary>
        private Position[] OptimizePath(Position[] positions)
        {
            try
            {
                if (positions.Length <= 1)
                {
                    return positions;
                }

                // 현재 위치를 시작점으로 가정 (0, 0, 0)
                var currentPos = new Position(0, 0, 0);
                var optimized = new Position[positions.Length];
                var visited = new bool[positions.Length];

                for (int i = 0; i < positions.Length; i++)
                {
                    int nearestIndex = -1;
                    double minDistance = double.MaxValue;

                    // 방문하지 않은 위치 중 가장 가까운 위치 찾기
                    for (int j = 0; j < positions.Length; j++)
                    {
                        if (visited[j]) continue;

                        double distance = CalculateDistance(currentPos, positions[j]);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            nearestIndex = j;
                        }
                    }

                    if (nearestIndex >= 0)
                    {
                        optimized[i] = positions[nearestIndex];
                        visited[nearestIndex] = true;
                        currentPos = positions[nearestIndex];
                    }
                }

                System.Diagnostics.Debug.WriteLine("[StepExecutor] 경로 최적화 완료");
                return optimized;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 경로 최적화 오류: {ex.Message}");
                return positions; // 최적화 실패 시 원본 반환
            }
        }

        /// <summary>
        /// 두 위치 간의 거리 계산 (3D 유클리드 거리)
        /// </summary>
        private double CalculateDistance(Position pos1, Position pos2)
        {
            try
            {
                // 극좌표를 직교좌표로 변환 후 거리 계산
                double x1 = pos1.R * Math.Cos(pos1.Theta * Math.PI / 180);
                double y1 = pos1.R * Math.Sin(pos1.Theta * Math.PI / 180);
                double z1 = pos1.Z;

                double x2 = pos2.R * Math.Cos(pos2.Theta * Math.PI / 180);
                double y2 = pos2.R * Math.Sin(pos2.Theta * Math.PI / 180);
                double z2 = pos2.Z;

                return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2) + Math.Pow(z2 - z1, 2));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StepExecutor] 거리 계산 오류: {ex.Message}");
                return double.MaxValue;
            }
        }
        #endregion
    }
}