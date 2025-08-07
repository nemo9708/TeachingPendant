// =============================================================================
// RecipeSystem/Teaching/TeachingDataIntegration.cs
// Teaching UI 데이터와 레시피 시스템 연동
// =============================================================================
using System;
using System.Collections.Generic;
using TeachingPendant.RecipeSystem.Models;
using TeachingPendant.HardwareControllers;

namespace TeachingPendant.RecipeSystem.Teaching
{
    /// <summary>
    /// Teaching UI와 Recipe 시스템 간 데이터 연동 클래스
    /// </summary>
    public static class TeachingDataIntegration
    {
        #region Public Methods
        /// <summary>
        /// Teaching 그룹 데이터로부터 기본 레시피 생성
        /// </summary>
        /// <param name="groupName">Teaching 그룹명</param>
        /// <param name="transferPattern">반송 패턴</param>
        /// <returns>생성된 레시피</returns>
        public static TransferRecipe CreateRecipeFromTeachingGroup(string groupName, TransferPattern transferPattern)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] Teaching 그룹에서 레시피 생성: {groupName}, 패턴: {transferPattern}");

                var recipe = new TransferRecipe();
                recipe.RecipeName = $"{groupName} - {transferPattern} 레시피";
                recipe.Description = $"Teaching {groupName} 그룹 데이터를 기반으로 생성된 {transferPattern} 레시피";

                // 기본 스텝들 추가
                recipe.AddStep(new RecipeStep(StepType.Home, "시작 전 홈 위치 이동"));
                recipe.AddStep(new RecipeStep(StepType.CheckSafety, "시작 전 안전 확인"));

                // 패턴별 스텝 생성
                switch (transferPattern)
                {
                    case TransferPattern.SingleWafer:
                        AddSingleWaferSteps(recipe, groupName);
                        break;

                    case TransferPattern.SequentialBatch:
                        AddSequentialBatchSteps(recipe, groupName, 5);
                        break;

                    case TransferPattern.FullTransfer:
                        AddFullTransferSteps(recipe, groupName, 25);
                        break;

                    case TransferPattern.CustomPattern:
                        AddCustomPatternSteps(recipe, groupName);
                        break;
                }

                // 완료 스텝
                recipe.AddStep(new RecipeStep(StepType.Home, "작업 완료 후 홈 복귀"));

                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] 레시피 생성 완료: {recipe.StepCount}개 스텝");
                return recipe;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] 레시피 생성 실패: {ex.Message}");
                return new TransferRecipe("오류 발생", $"레시피 생성 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// Teaching 좌표를 실제 Position으로 변환
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <param name="locationName">위치명 (P1, P2, etc.)</param>
        /// <returns>변환된 Position 또는 기본값</returns>
        public static Position GetPositionFromTeaching(string groupName, string locationName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] Teaching 좌표 조회: {groupName}.{locationName}");

                // TODO: 실제 Teaching UI 데이터와 연동
                // 현재는 Teaching UI의 실제 데이터 구조를 모르므로 시뮬레이션 좌표 사용

                // 실제 구현 시 다음과 같이 사용:
                // var teachingData = TeachingPendant.TeachingUI.Teaching.GetPositionData(groupName, locationName);
                // if (teachingData != null)
                // {
                //     return new Position(teachingData.R, teachingData.Theta, teachingData.Z);
                // }

                // 시뮬레이션 좌표 (실제 Teaching 데이터가 연동되면 제거)
                return GetSimulationPosition(locationName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] Teaching 좌표 조회 실패: {ex.Message}");
                return new Position(100, 0, 50); // 기본 안전 위치
            }
        }

        /// <summary>
        /// Teaching UI에 저장된 모든 위치 목록 가져오기
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <returns>위치명 목록</returns>
        public static List<string> GetAvailableLocations(string groupName)
        {
            try
            {
                // TODO: 실제 Teaching UI에서 위치 목록 가져오기
                // var locations = TeachingPendant.TeachingUI.Teaching.GetLocationList(groupName);

                // 현재는 표준 위치 목록 반환
                return new List<string> { "P1", "P2", "P3", "P4", "P5", "P6", "P7" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] 위치 목록 조회 실패: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// 레시피의 Teaching 참조를 실제 좌표로 업데이트
        /// </summary>
        /// <param name="recipe">업데이트할 레시피</param>
        /// <returns>업데이트 성공 여부</returns>
        public static bool UpdateRecipeCoordinatesFromTeaching(TransferRecipe recipe)
        {
            try
            {
                if (recipe?.Steps == null) return false;

                int updatedCount = 0;

                foreach (var step in recipe.Steps)
                {
                    if (!string.IsNullOrEmpty(step.TeachingGroupName) && !string.IsNullOrEmpty(step.TeachingLocationName))
                    {
                        var position = GetPositionFromTeaching(step.TeachingGroupName, step.TeachingLocationName);
                        step.TargetPosition = position;
                        updatedCount++;

                        System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] 좌표 업데이트: {step.TeachingLocationName} → {position}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] 레시피 좌표 업데이트 완료: {updatedCount}개 스텝");
                return updatedCount > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] 레시피 좌표 업데이트 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Teaching 그룹 목록 가져오기
        /// </summary>
        /// <returns>사용 가능한 그룹 목록</returns>
        public static List<string> GetAvailableGroups()
        {
            try
            {
                // TODO: 실제 Teaching UI에서 그룹 목록 가져오기
                // var groups = TeachingPendant.TeachingUI.Teaching.GetGroupList();

                // 현재는 표준 그룹 목록 반환
                return new List<string> { "Group1", "Group2", "Group3" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] 그룹 목록 조회 실패: {ex.Message}");
                return new List<string> { "Group1" };
            }
        }

        /// <summary>
        /// Teaching 데이터 유효성 확인
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <param name="locationName">위치명</param>
        /// <returns>유효한 데이터 여부</returns>
        public static bool IsValidTeachingData(string groupName, string locationName)
        {
            try
            {
                if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(locationName))
                {
                    return false;
                }

                // TODO: 실제 Teaching UI에서 데이터 존재 확인
                // return TeachingPendant.TeachingUI.Teaching.HasPositionData(groupName, locationName);

                // 현재는 기본 위치들만 유효한 것으로 판단
                var availableLocations = GetAvailableLocations(groupName);
                return availableLocations.Contains(locationName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] Teaching 데이터 유효성 확인 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 현재 Teaching UI 선택 상태 가져오기
        /// </summary>
        /// <returns>현재 선택된 그룹 정보</returns>
        public static TeachingSelectionInfo GetCurrentTeachingSelection()
        {
            try
            {
                // TODO: 실제 Teaching UI에서 현재 선택 상태 가져오기
                // var selection = TeachingPendant.TeachingUI.Teaching.GetCurrentSelection();

                // 현재는 기본값 반환
                return new TeachingSelectionInfo
                {
                    GroupName = "Group1",
                    LocationName = "P1",
                    IsValid = true
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] 현재 선택 상태 조회 실패: {ex.Message}");
                return new TeachingSelectionInfo
                {
                    GroupName = "Group1",
                    LocationName = "P1",
                    IsValid = false
                };
            }
        }

        /// <summary>
        /// Teaching 데이터를 레시피 스텝으로 변환
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <param name="locationName">위치명</param>
        /// <param name="stepType">스텝 타입</param>
        /// <param name="description">스텝 설명</param>
        /// <returns>변환된 레시피 스텝</returns>
        public static RecipeStep CreateStepFromTeachingData(string groupName, string locationName, StepType stepType, string description = "")
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] Teaching 데이터로 스텝 생성: {groupName}.{locationName}, {stepType}");

                var step = new RecipeStep(stepType, description);
                step.TeachingGroupName = groupName;
                step.TeachingLocationName = locationName;

                // 즉시 좌표 로드
                step.LoadCoordinatesFromTeaching();

                return step;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] Teaching 스텝 생성 실패: {ex.Message}");
                return new RecipeStep(stepType, $"오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 여러 위치를 한번에 스텝으로 변환
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <param name="locationNames">위치명 목록</param>
        /// <param name="stepType">스텝 타입</param>
        /// <returns>변환된 스텝 목록</returns>
        public static List<RecipeStep> CreateStepsFromMultipleLocations(string groupName, List<string> locationNames, StepType stepType)
        {
            var steps = new List<RecipeStep>();

            try
            {
                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] 다중 위치 스텝 생성: {groupName}, {locationNames.Count}개 위치");

                foreach (var locationName in locationNames)
                {
                    string description = $"{stepType} - {locationName}";
                    var step = CreateStepFromTeachingData(groupName, locationName, stepType, description);
                    steps.Add(step);
                }

                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] 다중 위치 스텝 생성 완료: {steps.Count}개");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] 다중 위치 스텝 생성 실패: {ex.Message}");
            }

            return steps;
        }
        #endregion

        #region Private Methods - Pattern Generation
        /// <summary>
        /// 단일 웨이퍼 반송 스텝 추가
        /// </summary>
        private static void AddSingleWaferSteps(TransferRecipe recipe, string groupName)
        {
            var pickStep = new RecipeStep(StepType.Pick, "P1에서 웨이퍼 집기")
            {
                TeachingGroupName = groupName,
                TeachingLocationName = "P1",
                Speed = 50
            };
            recipe.AddStep(pickStep);

            var placeStep = new RecipeStep(StepType.Place, "P4에 웨이퍼 놓기")
            {
                TeachingGroupName = groupName,
                TeachingLocationName = "P4",
                Speed = 40
            };
            recipe.AddStep(placeStep);
        }

        /// <summary>
        /// 순차 배치 반송 스텝 추가
        /// </summary>
        private static void AddSequentialBatchSteps(TransferRecipe recipe, string groupName, int waferCount)
        {
            for (int i = 1; i <= waferCount; i++)
            {
                var pickStep = new RecipeStep(StepType.Pick, $"P{i}에서 웨이퍼 {i} 집기")
                {
                    TeachingGroupName = groupName,
                    TeachingLocationName = $"P{i}",
                    Speed = 50
                };
                recipe.AddStep(pickStep);

                var placeStep = new RecipeStep(StepType.Place, $"P4에 웨이퍼 {i} 놓기")
                {
                    TeachingGroupName = groupName,
                    TeachingLocationName = "P4",
                    Speed = 40
                };
                recipe.AddStep(placeStep);

                // 중간 대기 (마지막 제외)
                if (i < waferCount)
                {
                    recipe.AddStep(new RecipeStep(StepType.Wait, $"웨이퍼 {i} 완료 후 대기") { WaitTimeMs = 300 });
                }
            }
        }

        /// <summary>
        /// 전체 반송 스텝 추가 (25개)
        /// </summary>
        private static void AddFullTransferSteps(TransferRecipe recipe, string groupName, int totalWaferCount)
        {
            for (int waferNum = 1; waferNum <= totalWaferCount; waferNum++)
            {
                // P1~P7 순환
                int sourcePos = ((waferNum - 1) % 7) + 1;

                var pickStep = new RecipeStep(StepType.Pick, $"웨이퍼 {waferNum} - P{sourcePos}에서 집기")
                {
                    TeachingGroupName = groupName,
                    TeachingLocationName = $"P{sourcePos}",
                    Speed = 60
                };
                recipe.AddStep(pickStep);

                var placeStep = new RecipeStep(StepType.Place, $"웨이퍼 {waferNum} - P4에 놓기")
                {
                    TeachingGroupName = groupName,
                    TeachingLocationName = "P4",
                    Speed = 50
                };
                recipe.AddStep(placeStep);

                // 10개마다 안전 확인
                if (waferNum % 10 == 0)
                {
                    recipe.AddStep(new RecipeStep(StepType.CheckSafety, $"{waferNum}개 완료 후 중간 안전 확인"));
                }
            }
        }

        /// <summary>
        /// 사용자 정의 패턴 스텝 추가
        /// </summary>
        private static void AddCustomPatternSteps(TransferRecipe recipe, string groupName)
        {
            // 빈 템플릿 - 사용자가 편집할 수 있도록
            recipe.AddStep(new RecipeStep(StepType.Move, "사용자 정의 이동 1")
            {
                TeachingGroupName = groupName,
                TeachingLocationName = "P1"
            });

            recipe.AddStep(new RecipeStep(StepType.Move, "사용자 정의 이동 2")
            {
                TeachingGroupName = groupName,
                TeachingLocationName = "P2"
            });

            recipe.AddStep(new RecipeStep(StepType.Wait, "사용자 정의 대기") { WaitTimeMs = 1000 });
        }

        /// <summary>
        /// 시뮬레이션 좌표 반환 (실제 Teaching 연동 전까지 사용)
        /// </summary>
        private static Position GetSimulationPosition(string locationName)
        {
            switch (locationName?.ToUpper())
            {
                case "P1": return new Position(100, 0, 50);
                case "P2": return new Position(100, 51.4, 50);
                case "P3": return new Position(100, 102.8, 50);
                case "P4": return new Position(200, 180, 100);
                case "P5": return new Position(100, 257.2, 50);
                case "P6": return new Position(100, 308.6, 50);
                case "P7": return new Position(100, 360, 50);
                default: return new Position(150, 0, 75); // 기본 위치
            }
        }

        /// <summary>
        /// 최적화된 반송 순서 생성
        /// </summary>
        /// <param name="sourcePositions">소스 위치 목록</param>
        /// <param name="targetPosition">타겟 위치</param>
        /// <returns>최적화된 순서</returns>
        private static List<string> OptimizeTransferOrder(List<string> sourcePositions, string targetPosition)
        {
            try
            {
                // 간단한 최적화 - 현재는 순차 반환
                // 실제로는 거리 기반 최적화 알고리즘 사용
                return new List<string>(sourcePositions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TeachingDataIntegration] 반송 순서 최적화 실패: {ex.Message}");
                return sourcePositions;
            }
        }
        #endregion
    }

    /// <summary>
    /// 웨이퍼 반송 패턴
    /// </summary>
    public enum TransferPattern
    {
        /// <summary>
        /// 단일 웨이퍼 (P1 → P4)
        /// </summary>
        SingleWafer,

        /// <summary>
        /// 순차 배치 (P1~P5 → P4)
        /// </summary>
        SequentialBatch,

        /// <summary>
        /// 전체 반송 (P1~P7 순환 25개 → P4)
        /// </summary>
        FullTransfer,

        /// <summary>
        /// 사용자 정의 패턴
        /// </summary>
        CustomPattern
    }

    /// <summary>
    /// Teaching 선택 정보
    /// </summary>
    public class TeachingSelectionInfo
    {
        public string GroupName { get; set; } = "";
        public string LocationName { get; set; } = "";
        public bool IsValid { get; set; } = false;
        public DateTime SelectionTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Teaching 데이터 정보
    /// </summary>
    public class TeachingDataInfo
    {
        public string GroupName { get; set; } = "";
        public string LocationName { get; set; } = "";
        public Position Position { get; set; } = new Position();
        public bool IsValid { get; set; } = false;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}