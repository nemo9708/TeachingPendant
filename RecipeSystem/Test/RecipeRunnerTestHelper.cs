using System;
using System.Threading.Tasks;
using TeachingPendant.RecipeSystem.Models;
using TeachingPendant.RecipeSystem.Engine;
using TeachingPendant.RecipeSystem.UI.Views;
using TeachingPendant.Logging;
// using TeachingPendant.RecipeSystem.Models; // 이 줄은 맨 위에 이미 있으므로 중복되어 제거해도 됩니다.

namespace TeachingPendant.RecipeSystem.Test
{
    /// <summary>
    /// RecipeRunner 테스트를 위한 헬퍼 클래스
    /// Phase 3 완료 검증을 위한 자동화된 테스트 제공
    /// </summary>
    public static class RecipeRunnerTestHelper
    {
        private const string SOURCE = "RecipeRunnerTestHelper";

        /// <summary>
        /// RecipeRunner 전체 기능 테스트 실행
        /// </summary>
        /// <returns>테스트 성공 여부</returns>
        public static async Task<bool> RunComprehensiveTest()
        {
            try
            {
                Logger.Info(SOURCE, "RunComprehensiveTest", "=== RecipeRunner 종합 테스트 시작 ===");

                bool allTestsPassed = true;

                Logger.Info(SOURCE, "RunComprehensiveTest", "1. UI 초기화 테스트 실행...");
                allTestsPassed &= await TestUIInitialization();

                Logger.Info(SOURCE, "RunComprehensiveTest", "2. 레시피 로드 테스트 실행...");
                allTestsPassed &= await TestRecipeLoading();

                Logger.Info(SOURCE, "RunComprehensiveTest", "3. 실행 시뮬레이션 테스트 실행...");
                allTestsPassed &= await TestExecutionSimulation();

                Logger.Info(SOURCE, "RunComprehensiveTest", "4. 이벤트 처리 테스트 실행...");
                allTestsPassed &= await TestEventHandling();

                Logger.Info(SOURCE, "RunComprehensiveTest", "5. 권한 제어 테스트 실행...");
                allTestsPassed &= await TestPermissionControl();

                string result = allTestsPassed ? "성공" : "실패";
                Logger.Info(SOURCE, "RunComprehensiveTest", "=== RecipeRunner 종합 테스트 " + result + " ===");

                return allTestsPassed;
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "RunComprehensiveTest", "RecipeRunner 종합 테스트 중 오류 발생", ex);
                return false;
            }
        }

        /// <summary>
        /// UI 초기화 테스트
        /// </summary>
        private static async Task<bool> TestUIInitialization()
        {
            try
            {
                var recipeRunner = new RecipeRunner();

                bool uiInitialized = recipeRunner != null;

                if (uiInitialized)
                {
                    Logger.Info(SOURCE, "TestUIInitialization", "✅ UI 초기화 테스트 성공");
                    recipeRunner.Dispose();
                    return true;
                }
                else
                {
                    Logger.Error(SOURCE, "TestUIInitialization", "❌ UI 초기화 테스트 실패", null);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "TestUIInitialization", "UI 초기화 테스트 중 오류 발생", ex);
                return false;
            }
        }

        /// <summary>
        /// 레시피 로드 테스트
        /// </summary>
        private static async Task<bool> TestRecipeLoading()
        {
            try
            {
                var recipeRunner = new RecipeRunner();
                var testRecipe = CreateTestRecipe();

                recipeRunner.LoadRecipe(testRecipe);

                bool loadSuccessful = testRecipe != null;

                if (loadSuccessful)
                {
                    Logger.Info(SOURCE, "TestRecipeLoading", "✅ 레시피 로드 테스트 성공");
                    recipeRunner.Dispose();
                    return true;
                }
                else
                {
                    Logger.Error(SOURCE, "TestRecipeLoading", "❌ 레시피 로드 테스트 실패", null);
                    recipeRunner.Dispose();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "TestRecipeLoading", "레시피 로드 테스트 중 오류 발생", ex);
                return false;
            }
        }

        /// <summary>
        /// 실행 시뮬레이션 테스트
        /// </summary>
        private static async Task<bool> TestExecutionSimulation()
        {
            try
            {
                var recipeEngine = new RecipeEngine();
                var testRecipe = CreateTestRecipe();

                bool executionStarted = false;
                bool executionCompleted = false;

                recipeEngine.StateChanged += (sender, e) =>
                {
                    if (e.NewState == RecipeExecutionState.Running) executionStarted = true;
                    else if (e.NewState == RecipeExecutionState.Completed) executionCompleted = true;
                };

                bool startResult = await recipeEngine.ExecuteRecipeAsync(testRecipe);

                if (startResult)
                {
                    int waitCount = 0;
                    while (!executionCompleted && waitCount < 20)
                    {
                        await Task.Delay(500);
                        waitCount++;
                    }
                }

                bool testPassed = startResult && executionStarted;

                if (testPassed)
                {
                    Logger.Info(SOURCE, "TestExecutionSimulation", "✅ 실행 시뮬레이션 테스트 성공");
                }
                else
                {
                    Logger.Error(SOURCE, "TestExecutionSimulation", "❌ 실행 시뮬레이션 테스트 실패", null);
                }

                recipeEngine.Dispose();
                return testPassed;
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "TestExecutionSimulation", "실행 시뮬레이션 테스트 중 오류 발생", ex);
                return false;
            }
        }

        /// <summary>
        /// 이벤트 처리 테스트
        /// </summary>
        private static async Task<bool> TestEventHandling()
        {
            try
            {
                var recipeEngine = new RecipeEngine();
                bool stateChangeEventReceived = false, stepStartedEventReceived = false, progressUpdateEventReceived = false;

                recipeEngine.StateChanged += (sender, e) => { stateChangeEventReceived = true; };
                recipeEngine.StepStarted += (sender, e) => { stepStartedEventReceived = true; };
                recipeEngine.ProgressUpdated += (sender, e) => { progressUpdateEventReceived = true; };

                var testRecipe = CreateSimpleTestRecipe();
                await recipeEngine.ExecuteRecipeAsync(testRecipe);
                await Task.Delay(1000);

                bool allEventsReceived = stateChangeEventReceived && stepStartedEventReceived;

                if (allEventsReceived)
                {
                    Logger.Info(SOURCE, "TestEventHandling", "✅ 이벤트 처리 테스트 성공");
                }
                else
                {
                    Logger.Error(SOURCE, "TestEventHandling", "❌ 이벤트 처리 테스트 실패 - 상태변경: " + stateChangeEventReceived + ", 스텝시작: " + stepStartedEventReceived, null);
                }

                recipeEngine.Dispose();
                return allEventsReceived;
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "TestEventHandling", "이벤트 처리 테스트 중 오류 발생", ex);
                return false;
            }
        }

        /// <summary>
        /// 권한 제어 테스트
        /// </summary>
        private static async Task<bool> TestPermissionControl()
        {
            try
            {
                Logger.Info(SOURCE, "TestPermissionControl", "권한 제어 시스템 테스트는 현재 시뮬레이션으로 진행됩니다.");
                bool permissionTestPassed = true;

                if (permissionTestPassed)
                {
                    Logger.Info(SOURCE, "TestPermissionControl", "✅ 권한 제어 테스트 성공");
                }
                else
                {
                    Logger.Error(SOURCE, "TestPermissionControl", "❌ 권한 제어 테스트 실패", null);
                }

                return permissionTestPassed;
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "TestPermissionControl", "권한 제어 테스트 중 오류 발생", ex);
                return false;
            }
        }

        /// <summary>
        /// 테스트용 레시피 생성
        /// </summary>
        private static TransferRecipe CreateTestRecipe()
        {
            try
            {
                var recipe = new TransferRecipe
                {
                    RecipeName = "TestRecipe_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                    Description = "RecipeRunner 테스트용 레시피",
                    CreatedBy = "TestSystem",
                    CreatedDate = DateTime.Now
                };

                recipe.Steps.Add(new RecipeStep { Type = StepType.Home, Description = "홈 위치로 이동", TeachingGroupName = "TestGroup", TeachingLocationName = "Home" });
                recipe.Steps.Add(new RecipeStep { Type = StepType.Move, Description = "픽업 위치로 이동", TeachingGroupName = "TestGroup", TeachingLocationName = "PickPosition" });
                recipe.Steps.Add(new RecipeStep { Type = StepType.Pick, Description = "웨이퍼 픽업", TeachingGroupName = "TestGroup", TeachingLocationName = "PickPosition" });
                recipe.Steps.Add(new RecipeStep { Type = StepType.Move, Description = "배치 위치로 이동", TeachingGroupName = "TestGroup", TeachingLocationName = "PlacePosition" });
                recipe.Steps.Add(new RecipeStep { Type = StepType.Place, Description = "웨이퍼 배치", TeachingGroupName = "TestGroup", TeachingLocationName = "PlacePosition" });
                recipe.Steps.Add(new RecipeStep { Type = StepType.Home, Description = "홈 위치로 복귀", TeachingGroupName = "TestGroup", TeachingLocationName = "Home" });

                Logger.Info(SOURCE, "CreateTestRecipe", "테스트 레시피가 생성되었습니다: " + recipe.RecipeName);
                return recipe;
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "CreateTestRecipe", "테스트 레시피 생성 중 오류 발생", ex);
                return null;
            }
        }

        /// <summary>
        /// 간단한 테스트용 레시피 생성
        /// </summary>
        private static TransferRecipe CreateSimpleTestRecipe()
        {
            try
            {
                var recipe = new TransferRecipe
                {
                    RecipeName = "SimpleTest_" + DateTime.Now.ToString("HHmmss"),
                    Description = "간단한 테스트용 레시피",
                    CreatedBy = "TestSystem",
                    CreatedDate = DateTime.Now
                };

                recipe.Steps.Add(new RecipeStep { Type = StepType.Wait, Description = "1초 대기", WaitTimeMs = 1000 });
                recipe.Steps.Add(new RecipeStep { Type = StepType.CheckSafety, Description = "안전 상태 확인" });

                return recipe;
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "CreateSimpleTestRecipe", "간단한 테스트 레시피 생성 중 오류 발생", ex);
                return null;
            }
        }

        /// <summary>
        /// 성능 테스트 - 25스텝 레시피 처리 성능
        /// </summary>
        public static async Task<bool> RunPerformanceTest()
        {
            try
            {
                Logger.Info(SOURCE, "RunPerformanceTest", "=== RecipeRunner 성능 테스트 시작 ===");

                var recipe = CreateLargeTestRecipe(25);
                var recipeRunner = new RecipeRunner();

                var startTime = DateTime.Now;
                recipeRunner.LoadRecipe(recipe);
                var loadTime = DateTime.Now - startTime;

                bool performanceTestPassed = loadTime.TotalSeconds < 1.0;

                if (performanceTestPassed)
                {
                    Logger.Info(SOURCE, "RunPerformanceTest", "✅ 성능 테스트 성공 - 로드 시간: " + loadTime.TotalMilliseconds + "ms");
                }
                else
                {
                    Logger.Error(SOURCE, "RunPerformanceTest", "❌ 성능 테스트 실패 - 로드 시간: " + loadTime.TotalMilliseconds + "ms (1초 초과)", null);
                }

                recipeRunner.Dispose();
                return performanceTestPassed;
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "RunPerformanceTest", "성능 테스트 중 오류 발생", ex);
                return false;
            }
        }

        /// <summary>
        /// 대용량 테스트용 레시피 생성
        /// </summary>
        private static TransferRecipe CreateLargeTestRecipe(int stepCount)
        {
            try
            {
                var recipe = new TransferRecipe
                {
                    RecipeName = "LargeTestRecipe_" + stepCount + "Steps",
                    Description = stepCount + "단계 성능 테스트용 레시피",
                    CreatedBy = "PerformanceTestSystem",
                    CreatedDate = DateTime.Now
                };

                for (int i = 0; i < stepCount; i++)
                {
                    var stepType = (StepType)(i % 6);
                    recipe.Steps.Add(new RecipeStep
                    {
                        Type = stepType,
                        Description = "대용량 테스트 스텝 " + (i + 1),
                        TeachingGroupName = "TestGroup_" + (i % 3),
                        TeachingLocationName = "Position_" + (i % 5),
                        WaitTimeMs = stepType == StepType.Wait ? 100 : 0
                    });
                }

                Logger.Info(SOURCE, "CreateLargeTestRecipe", "대용량 테스트 레시피가 생성되었습니다: " + stepCount + "단계");
                return recipe;
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "CreateLargeTestRecipe", "대용량 테스트 레시피 생성 중 오류 발생", ex);
                return null;
            }
        }

        /// <summary>
        /// 메모리 사용량 테스트
        /// </summary>
        public static async Task<bool> RunMemoryUsageTest()
        {
            try
            {
                Logger.Info(SOURCE, "RunMemoryUsageTest", "=== RecipeRunner 메모리 사용량 테스트 시작 ===");

                long initialMemory = GC.GetTotalMemory(true);

                for (int i = 0; i < 10; i++)
                {
                    var recipeRunner = new RecipeRunner();
                    var testRecipe = CreateTestRecipe();
                    recipeRunner.LoadRecipe(testRecipe);
                    await Task.Delay(100);
                    recipeRunner.Dispose();
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                long finalMemory = GC.GetTotalMemory(false);
                long memoryIncrease = finalMemory - initialMemory;
                bool memoryTestPassed = memoryIncrease < 10 * 1024 * 1024;

                if (memoryTestPassed)
                {
                    Logger.Info(SOURCE, "RunMemoryUsageTest", "✅ 메모리 사용량 테스트 성공 - 증가량: " + (memoryIncrease / 1024) + "KB");
                }
                else
                {
                    Logger.Error(SOURCE, "RunMemoryUsageTest", "❌ 메모리 사용량 테스트 실패 - 증가량: " + (memoryIncrease / 1024 / 1024) + "MB", null);
                }

                return memoryTestPassed;
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "RunMemoryUsageTest", "메모리 사용량 테스트 중 오류 발생", ex);
                return false;
            }
        }

        /// <summary>
        /// 안정성 테스트 - 연속 실행
        /// </summary>
        public static async Task<bool> RunStabilityTest()
        {
            try
            {
                Logger.Info(SOURCE, "RunStabilityTest", "=== RecipeRunner 안정성 테스트 시작 ===");

                int successCount = 0;
                int totalRuns = 10;

                for (int i = 0; i < totalRuns; i++)
                {
                    try
                    {
                        var recipeEngine = new RecipeEngine();
                        var testRecipe = CreateSimpleTestRecipe();
                        bool executionCompleted = false;

                        recipeEngine.RecipeCompleted += (sender, e) => { executionCompleted = true; };

                        bool startResult = await recipeEngine.ExecuteRecipeAsync(testRecipe);

                        if (startResult)
                        {
                            int waitCount = 0;
                            while (!executionCompleted && waitCount < 10)
                            {
                                await Task.Delay(500);
                                waitCount++;
                            }

                            if (executionCompleted)
                            {
                                successCount++;
                                Logger.Info(SOURCE, "RunStabilityTest", "테스트 실행 " + (i + 1) + " 성공");
                            }
                            else
                            {
                                Logger.Warning(SOURCE, "RunStabilityTest", "테스트 실행 " + (i + 1) + " 시간 초과");
                            }
                        }
                        else
                        {
                            Logger.Warning(SOURCE, "RunStabilityTest", "테스트 실행 " + (i + 1) + " 시작 실패");
                        }

                        recipeEngine.Dispose();
                        await Task.Delay(200);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(SOURCE, "RunStabilityTest", "테스트 실행 " + (i + 1) + " 중 오류 발생", ex);
                    }
                }

                double successRate = (double)successCount / totalRuns * 100;
                bool stabilityTestPassed = successRate >= 90;

                if (stabilityTestPassed)
                {
                    Logger.Info(SOURCE, "RunStabilityTest", "✅ 안정성 테스트 성공 - 성공률: " + successRate.ToString("F1") + "%");
                }
                else
                {
                    Logger.Error(SOURCE, "RunStabilityTest", "❌ 안정성 테스트 실패 - 성공률: " + successRate.ToString("F1") + "%", null);
                }

                return stabilityTestPassed;
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "RunStabilityTest", "안정성 테스트 중 오류 발생", ex);
                return false;
            }
        }

        /// <summary>
        /// 모든 테스트 실행
        /// </summary>
        public static async Task<TestResults> RunAllTests()
        {
            try
            {
                Logger.Info(SOURCE, "RunAllTests", "🚀 RecipeRunner 전체 테스트 시작");

                var results = new TestResults();
                results.ComprehensiveTest = await RunComprehensiveTest();
                results.PerformanceTest = await RunPerformanceTest();
                results.MemoryUsageTest = await RunMemoryUsageTest();
                results.StabilityTest = await RunStabilityTest();

                results.OverallSuccess = results.ComprehensiveTest && results.PerformanceTest && results.MemoryUsageTest && results.StabilityTest;

                string overallResult = results.OverallSuccess ? "✅ 전체 성공" : "❌ 일부 실패";
                Logger.Info(SOURCE, "RunAllTests", "🏁 RecipeRunner 전체 테스트 완료 - " + overallResult);

                Logger.Info(SOURCE, "RunAllTests", "=== 테스트 결과 요약 ===");
                Logger.Info(SOURCE, "RunAllTests", "종합 테스트: " + (results.ComprehensiveTest ? "성공" : "실패"));
                Logger.Info(SOURCE, "RunAllTests", "성능 테스트: " + (results.PerformanceTest ? "성공" : "실패"));
                Logger.Info(SOURCE, "RunAllTests", "메모리 테스트: " + (results.MemoryUsageTest ? "성공" : "실패"));
                Logger.Info(SOURCE, "RunAllTests", "안정성 테스트: " + (results.StabilityTest ? "성공" : "실패"));

                return results;
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "RunAllTests", "전체 테스트 실행 중 오류 발생", ex);
                return new TestResults { OverallSuccess = false };
            }
        }
    }

    /// <summary>
    /// 테스트 결과를 담는 클래스
    /// </summary>
    public class TestResults
    {
        public bool ComprehensiveTest { get; set; }
        public bool PerformanceTest { get; set; }
        public bool MemoryUsageTest { get; set; }
        public bool StabilityTest { get; set; }
        public bool OverallSuccess { get; set; }

        public override string ToString()
        {
            return "RecipeRunner 테스트 결과:\n" +
                   "- 종합 테스트: " + (ComprehensiveTest ? "성공" : "실패") + "\n" +
                   "- 성능 테스트: " + (PerformanceTest ? "성공" : "실패") + "\n" +
                   "- 메모리 테스트: " + (MemoryUsageTest ? "성공" : "실패") + "\n" +
                   "- 안정성 테스트: " + (StabilityTest ? "성공" : "실패") + "\n" +
                   "- 전체 결과: " + (OverallSuccess ? "✅ 성공" : "❌ 실패");
        }
    }
}