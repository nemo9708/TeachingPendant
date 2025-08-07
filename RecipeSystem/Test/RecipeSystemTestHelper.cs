using System;
using System.Threading.Tasks;
using TeachingPendant.RecipeSystem.Models;
using TeachingPendant.RecipeSystem.Engine;
using TeachingPendant.RecipeSystem.Storage;
using TeachingPendant.RecipeSystem.Teaching;
using TeachingPendant.HardwareControllers;
using TeachingPendant.Safety;
using TeachingPendant.Alarm;

namespace TeachingPendant.RecipeSystem.Test
{
    /// <summary>
    /// 레시피 시스템 테스트 및 검증 클래스
    /// </summary>
    public static class RecipeSystemTestHelper
    {
        #region Public Test Methods
        /// <summary>
        /// 전체 레시피 시스템 테스트 실행
        /// </summary>
        /// <returns>테스트 성공 여부</returns>
        public static async Task<bool> RunCompleteSystemTestAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 레시피 시스템 통합 테스트 시작 ===");

                // 1. 기본 템플릿 생성 테스트
                bool templateTest = await TestTemplateCreation();
                System.Diagnostics.Debug.WriteLine($"템플릿 생성 테스트: {(templateTest ? "PASS" : "FAIL")}");

                // 2. Teaching 연동 테스트
                bool teachingTest = TestTeachingIntegration();
                System.Diagnostics.Debug.WriteLine($"Teaching 연동 테스트: {(teachingTest ? "PASS" : "FAIL")}");

                // 3. 레시피 생성 및 검증 테스트
                bool recipeTest = TestRecipeCreationAndValidation();
                System.Diagnostics.Debug.WriteLine($"레시피 생성/검증 테스트: {(recipeTest ? "PASS" : "FAIL")}");

                // 4. 파일 저장/로드 테스트
                bool fileTest = await TestFileOperations();
                System.Diagnostics.Debug.WriteLine($"파일 저장/로드 테스트: {(fileTest ? "PASS" : "FAIL")}");

                // 5. 레시피 엔진 시뮬레이션 테스트
                bool engineTest = await TestRecipeEngineSimulation();
                System.Diagnostics.Debug.WriteLine($"레시피 엔진 시뮬레이션 테스트: {(engineTest ? "PASS" : "FAIL")}");

                bool allTestsPassed = templateTest && teachingTest && recipeTest && fileTest && engineTest;

                System.Diagnostics.Debug.WriteLine($"=== 레시피 시스템 통합 테스트 완료: {(allTestsPassed ? "ALL PASS" : "SOME FAILED")} ===");

                if (allTestsPassed)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "레시피 시스템 테스트 모두 통과");
                }
                else
                {
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "레시피 시스템 테스트 일부 실패");
                }

                return allTestsPassed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"레시피 시스템 테스트 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 단순 레시피 실행 테스트 (시뮬레이션)
        /// </summary>
        /// <returns>테스트 성공 여부</returns>
        public static async Task<bool> RunSimpleRecipeTestAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 단순 레시피 실행 테스트 ===");

                // 테스트용 단순 레시피 생성
                var testRecipe = CreateSimpleTestRecipe();

                // 레시피 엔진 생성
                using (var engine = new RecipeEngine())
                {
                    // 이벤트 구독
                    engine.StateChanged += (sender, e) =>
                        System.Diagnostics.Debug.WriteLine($"상태 변경: {e.OldState} → {e.NewState}");

                    engine.StepCompleted += (sender, e) =>
                        System.Diagnostics.Debug.WriteLine($"스텝 완료: {e.Step.Description} - {(e.Success ? "성공" : "실패")}");

                    // 레시피 실행
                    bool startResult = await engine.ExecuteRecipeAsync(testRecipe);
                    if (!startResult)
                    {
                        System.Diagnostics.Debug.WriteLine("레시피 실행 시작 실패");
                        return false;
                    }

                    // 실행 완료 대기 (최대 30초)
                    int waitCount = 0;
                    while (engine.CurrentState == RecipeExecutionState.Running && waitCount < 60)
                    {
                        await Task.Delay(500);
                        waitCount++;
                    }

                    bool testPassed = engine.CurrentState == RecipeExecutionState.Completed;
                    System.Diagnostics.Debug.WriteLine($"단순 레시피 테스트: {(testPassed ? "PASS" : "FAIL")}");

                    return testPassed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"단순 레시피 테스트 오류: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Private Test Methods
        /// <summary>
        /// 템플릿 생성 테스트
        /// </summary>
        private static async Task<bool> TestTemplateCreation()
        {
            try
            {
                await RecipeFileManager.CreateDefaultTemplatesAsync();

                // 템플릿 파일 존재 확인
                string templatePath = RecipeFileManager.TemplateFolderPath;
                bool singleExists = System.IO.File.Exists(System.IO.Path.Combine(templatePath, "SingleWafer_Template.recipe.json"));
                bool batchExists = System.IO.File.Exists(System.IO.Path.Combine(templatePath, "BatchWafer_Template.recipe.json"));
                bool fullExists = System.IO.File.Exists(System.IO.Path.Combine(templatePath, "FullTransfer_Template.recipe.json"));
                bool customExists = System.IO.File.Exists(System.IO.Path.Combine(templatePath, "CustomPattern_Template.recipe.json"));

                return singleExists && batchExists && fullExists && customExists;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"템플릿 생성 테스트 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Teaching 연동 테스트
        /// </summary>
        private static bool TestTeachingIntegration()
        {
            try
            {
                // 1. Teaching 위치 목록 조회 테스트
                var locations = TeachingDataIntegration.GetAvailableLocations("Group1");
                if (locations.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("Teaching 위치 목록 조회 실패");
                    return false;
                }

                // 2. Teaching 좌표 변환 테스트
                var p1Position = TeachingDataIntegration.GetPositionFromTeaching("Group1", "P1");
                var p4Position = TeachingDataIntegration.GetPositionFromTeaching("Group1", "P4");

                if (p1Position == null || p4Position == null)
                {
                    System.Diagnostics.Debug.WriteLine("Teaching 좌표 변환 실패");
                    return false;
                }

                // 3. Teaching 기반 레시피 생성 테스트
                var recipe = TeachingDataIntegration.CreateRecipeFromTeachingGroup("Group1", TransferPattern.SingleWafer);
                if (recipe == null || recipe.Steps.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("Teaching 기반 레시피 생성 실패");
                    return false;
                }

                // 4. 레시피 좌표 업데이트 테스트
                bool updateResult = TeachingDataIntegration.UpdateRecipeCoordinatesFromTeaching(recipe);
                if (!updateResult)
                {
                    System.Diagnostics.Debug.WriteLine("레시피 좌표 업데이트 실패");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("Teaching 연동 테스트 모든 항목 통과");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teaching 연동 테스트 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 레시피 생성 및 검증 테스트
        /// </summary>
        private static bool TestRecipeCreationAndValidation()
        {
            try
            {
                // 1. 기본 레시피 생성 테스트
                var recipe = new TransferRecipe("테스트 레시피", "테스트용 레시피");
                recipe.AddStep(new RecipeStep(StepType.Home, "홈 이동"));
                recipe.AddStep(new RecipeStep(StepType.Move, "이동 테스트"));

                if (recipe.StepCount != 2)
                {
                    System.Diagnostics.Debug.WriteLine("레시피 스텝 추가 실패");
                    return false;
                }

                // 2. 레시피 검증 테스트
                var validation = recipe.Validate();
                if (!validation.IsValid)
                {
                    System.Diagnostics.Debug.WriteLine($"레시피 검증 실패: {string.Join(", ", validation.ErrorMessages)}");
                    return false;
                }

                // 3. 레시피 매개변수 검증 테스트
                var parameters = new RecipeParameters();
                parameters.DefaultSpeed = 150; // 범위 초과 값 설정

                var paramValidation = parameters.Validate();
                if (paramValidation.IsValid)
                {
                    System.Diagnostics.Debug.WriteLine("매개변수 검증이 범위 초과 값을 감지하지 못함");
                    return false;
                }

                // 4. JSON 직렬화/역직렬화 테스트
                string json = recipe.ToJson();
                var deserializedRecipe = TransferRecipe.FromJson(json);

                if (deserializedRecipe.RecipeName != recipe.RecipeName ||
                    deserializedRecipe.StepCount != recipe.StepCount)
                {
                    System.Diagnostics.Debug.WriteLine("JSON 직렬화/역직렬화 실패");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("레시피 생성/검증 테스트 모든 항목 통과");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"레시피 생성/검증 테스트 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 파일 저장/로드 테스트
        /// </summary>
        private static async Task<bool> TestFileOperations()
        {
            try
            {
                // 1. 테스트 레시피 생성
                var testRecipe = CreateTestRecipe();

                // 2. 레시피 저장 테스트
                string testFileName = $"TestRecipe_{DateTime.Now:yyyyMMdd_HHmmss}";
                bool saveResult = await RecipeFileManager.SaveRecipeAsync(testRecipe, testFileName);
                if (!saveResult)
                {
                    System.Diagnostics.Debug.WriteLine("레시피 저장 실패");
                    return false;
                }

                // 3. 레시피 로드 테스트
                var loadedRecipe = await RecipeFileManager.LoadRecipeAsync(testFileName);
                if (loadedRecipe == null)
                {
                    System.Diagnostics.Debug.WriteLine("레시피 로드 실패");
                    return false;
                }

                // 4. 데이터 일치 확인
                if (loadedRecipe.RecipeName != testRecipe.RecipeName ||
                    loadedRecipe.StepCount != testRecipe.StepCount)
                {
                    System.Diagnostics.Debug.WriteLine("저장/로드된 레시피 데이터 불일치");
                    return false;
                }

                // 5. 파일 목록 조회 테스트
                var fileList = RecipeFileManager.GetRecipeFileList();
                bool testFileFound = false;
                foreach (var file in fileList)
                {
                    if (file.FileName.Contains("TestRecipe"))
                    {
                        testFileFound = true;
                        break;
                    }
                }

                if (!testFileFound)
                {
                    System.Diagnostics.Debug.WriteLine("저장된 테스트 파일이 목록에서 발견되지 않음");
                    return false;
                }

                // 6. 파일 삭제 테스트
                bool deleteResult = RecipeFileManager.DeleteRecipe(testFileName);
                if (!deleteResult)
                {
                    System.Diagnostics.Debug.WriteLine("레시피 파일 삭제 실패");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("파일 저장/로드 테스트 모든 항목 통과");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"파일 저장/로드 테스트 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 레시피 엔진 시뮬레이션 테스트
        /// </summary>
        private static async Task<bool> TestRecipeEngineSimulation()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("레시피 엔진 시뮬레이션 테스트 시작");

                var testRecipe = CreateSimpleTestRecipe();
                bool engineTestPassed = false;
                bool stepsExecuted = false;

                using (var engine = new RecipeEngine())
                {
                    // 이벤트 핸들러 설정
                    engine.StateChanged += (sender, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"엔진 상태 변경: {e.OldState} → {e.NewState}");
                    };

                    engine.StepCompleted += (sender, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"스텝 완료: {e.StepIndex + 1}. {e.Step.Description} - {(e.Success ? "성공" : "실패")}");
                        stepsExecuted = true;
                    };

                    engine.RecipeCompleted += (sender, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"레시피 완료: {(e.Success ? "성공" : "실패")} - {e.Message}");
                        engineTestPassed = e.Success;
                    };

                    // 레시피 실행
                    bool startResult = await engine.ExecuteRecipeAsync(testRecipe);
                    if (!startResult)
                    {
                        System.Diagnostics.Debug.WriteLine("레시피 엔진 시작 실패");
                        return false;
                    }

                    // 실행 상태 모니터링 (최대 20초 대기)
                    int waitCount = 0;
                    while (engine.CurrentState == RecipeExecutionState.Running && waitCount < 40)
                    {
                        await Task.Delay(500);
                        waitCount++;

                        if (waitCount % 4 == 0) // 2초마다 진행상황 출력
                        {
                            System.Diagnostics.Debug.WriteLine($"실행 진행률: {engine.ProgressPercentage:F1}%, 현재 스텝: {engine.CurrentStepIndex + 1}/{testRecipe.StepCount}");
                        }
                    }

                    // 결과 확인
                    if (engine.CurrentState == RecipeExecutionState.Running)
                    {
                        System.Diagnostics.Debug.WriteLine("레시피 실행 시간 초과 - 강제 정지");
                        await engine.StopExecutionAsync();
                        return false;
                    }

                    bool testPassed = engineTestPassed && stepsExecuted;
                    System.Diagnostics.Debug.WriteLine($"레시피 엔진 시뮬레이션 테스트: {(testPassed ? "PASS" : "FAIL")}");
                    return testPassed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"레시피 엔진 시뮬레이션 테스트 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 테스트용 레시피 생성
        /// </summary>
        private static TransferRecipe CreateTestRecipe()
        {
            var recipe = new TransferRecipe("파일 테스트 레시피", "파일 저장/로드 테스트를 위한 레시피");

            recipe.AddStep(new RecipeStep(StepType.Home, "홈 위치로 이동"));
            recipe.AddStep(new RecipeStep(StepType.CheckSafety, "안전 상태 확인"));

            var moveStep = new RecipeStep(StepType.Move, "테스트 위치로 이동")
            {
                TargetPosition = new Position(100, 45, 50),
                Speed = 50
            };
            recipe.AddStep(moveStep);

            recipe.AddStep(new RecipeStep(StepType.Wait, "1초 대기") { WaitTimeMs = 1000 });
            recipe.AddStep(new RecipeStep(StepType.Home, "홈 복귀"));

            return recipe;
        }

        /// <summary>
        /// 간단한 테스트 레시피 생성
        /// </summary>
        private static TransferRecipe CreateSimpleTestRecipe()
        {
            var recipe = new TransferRecipe("간단 테스트 레시피", "엔진 테스트를 위한 간단한 레시피");

            // 매개변수 설정 (빠른 테스트를 위해)
            recipe.Parameters.DefaultSpeed = 80;
            recipe.Parameters.CheckSafetyBeforeEachStep = false; // 빠른 실행을 위해 비활성화

            recipe.AddStep(new RecipeStep(StepType.Home, "시작 홈 이동"));
            recipe.AddStep(new RecipeStep(StepType.Wait, "짧은 대기") { WaitTimeMs = 200 });

            var moveStep1 = new RecipeStep(StepType.Move, "위치 1로 이동")
            {
                TargetPosition = new Position(100, 0, 50),
                Speed = 80
            };
            recipe.AddStep(moveStep1);

            recipe.AddStep(new RecipeStep(StepType.Wait, "중간 대기") { WaitTimeMs = 300 });

            var moveStep2 = new RecipeStep(StepType.Move, "위치 2로 이동")
            {
                TargetPosition = new Position(150, 90, 60),
                Speed = 70
            };
            recipe.AddStep(moveStep2);

            recipe.AddStep(new RecipeStep(StepType.Home, "완료 홈 복귀"));

            return recipe;
        }
        #endregion

        #region Public Utility Methods
        /// <summary>
        /// 레시피 시스템 초기화 및 설정
        /// </summary>
        public static async Task InitializeRecipeSystemAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[RecipeSystemTestHelper] 레시피 시스템 초기화 시작");

                // 1. 기본 템플릿 생성
                await RecipeFileManager.CreateDefaultTemplatesAsync();

                // 2. 로봇 컨트롤러 팩토리 초기화
                RobotControllerFactory.Initialize();

                // 3. 시스템 설정 확인
                bool systemReady = await VerifySystemReadiness();

                if (systemReady)
                {
                    System.Diagnostics.Debug.WriteLine("[RecipeSystemTestHelper] 레시피 시스템 초기화 완료");
                    AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "레시피 시스템이 준비되었습니다");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[RecipeSystemTestHelper] 레시피 시스템 초기화 일부 실패");
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, "레시피 시스템 초기화 중 일부 문제 발생");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeSystemTestHelper] 레시피 시스템 초기화 오류: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"레시피 시스템 초기화 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 시스템 준비 상태 확인
        /// </summary>
        /// <returns>시스템 준비 여부</returns>
        public static async Task<bool> VerifySystemReadiness()
        {
            try
            {
                // 1. 로봇 컨트롤러 확인
                var controller = RobotControllerFactory.GetCurrentController();
                if (controller == null)
                {
                    System.Diagnostics.Debug.WriteLine("로봇 컨트롤러가 초기화되지 않음");
                    return false;
                }

                // 2. 안전 시스템 확인
                bool safetyReady = SafetySystem.IsSafeForRobotOperation();
                if (!safetyReady)
                {
                    System.Diagnostics.Debug.WriteLine("안전 시스템이 준비되지 않음");
                    // 시뮬레이션 모드에서는 경고만 출력
                }

                // 3. 파일 시스템 확인
                bool foldersReady = System.IO.Directory.Exists(RecipeFileManager.RecipeFolderPath) &&
                                   System.IO.Directory.Exists(RecipeFileManager.TemplateFolderPath);

                if (!foldersReady)
                {
                    System.Diagnostics.Debug.WriteLine("레시피 폴더 구조가 준비되지 않음");
                    return false;
                }

                // 4. Teaching 시스템 연동 확인 (기본적인 확인만)
                var testLocations = TeachingDataIntegration.GetAvailableLocations("Group1");
                if (testLocations.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("Teaching 시스템 연동에 문제가 있을 수 있음");
                    // 경고만 출력하고 계속 진행
                }

                System.Diagnostics.Debug.WriteLine("시스템 준비 상태 확인 완료");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"시스템 준비 상태 확인 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 레시피 시스템 데모 실행
        /// </summary>
        /// <returns>데모 실행 성공 여부</returns>
        public static async Task<bool> RunRecipeSystemDemoAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 레시피 시스템 데모 시작 ===");

                // 1. Teaching 기반 레시피 생성 데모
                var demoRecipe = TeachingDataIntegration.CreateRecipeFromTeachingGroup("Group1", TransferPattern.SingleWafer);
                System.Diagnostics.Debug.WriteLine($"데모 레시피 생성됨: {demoRecipe.RecipeName}, {demoRecipe.StepCount}개 스텝");

                // 2. 레시피 좌표 업데이트
                TeachingDataIntegration.UpdateRecipeCoordinatesFromTeaching(demoRecipe);
                System.Diagnostics.Debug.WriteLine("Teaching 좌표로 레시피 업데이트 완료");

                // 3. 레시피 검증
                var validation = demoRecipe.Validate();
                if (!validation.IsValid)
                {
                    System.Diagnostics.Debug.WriteLine($"레시피 검증 실패: {string.Join(", ", validation.ErrorMessages)}");
                    return false;
                }

                // 4. 레시피 저장
                string demoFileName = $"Demo_Recipe_{DateTime.Now:yyyyMMdd_HHmmss}";
                bool saveResult = await RecipeFileManager.SaveRecipeAsync(demoRecipe, demoFileName);
                System.Diagnostics.Debug.WriteLine($"레시피 저장: {(saveResult ? "성공" : "실패")}");

                // 5. 시뮬레이션 실행
                bool execResult = await ExecuteDemoRecipe(demoRecipe);
                System.Diagnostics.Debug.WriteLine($"레시피 실행 데모: {(execResult ? "성공" : "실패")}");

                bool demoSuccess = saveResult && execResult;
                System.Diagnostics.Debug.WriteLine($"=== 레시피 시스템 데모 완료: {(demoSuccess ? "SUCCESS" : "FAILED")} ===");

                if (demoSuccess)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.STATUS_UPDATE, "레시피 시스템 데모가 성공적으로 완료되었습니다");
                }

                return demoSuccess;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"레시피 시스템 데모 오류: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"레시피 시스템 데모 실행 중 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 데모 레시피 실행
        /// </summary>
        private static async Task<bool> ExecuteDemoRecipe(TransferRecipe recipe)
        {
            try
            {
                using (var engine = new RecipeEngine())
                {
                    bool executionCompleted = false;
                    bool executionSuccessful = false;

                    // 이벤트 핸들러
                    engine.RecipeCompleted += (sender, e) =>
                    {
                        executionCompleted = true;
                        executionSuccessful = e.Success;
                        System.Diagnostics.Debug.WriteLine($"데모 레시피 완료: {e.Message}");
                    };

                    engine.ProgressUpdated += (sender, e) =>
                    {
                        if (e.CurrentStepIndex % 2 == 0) // 2스텝마다 출력
                        {
                            System.Diagnostics.Debug.WriteLine($"데모 진행: {e.ProgressPercentage:F0}% ({e.CurrentStepIndex + 1}/{e.TotalSteps})");
                        }
                    };

                    // 실행 시작
                    bool startResult = await engine.ExecuteRecipeAsync(recipe);
                    if (!startResult)
                    {
                        return false;
                    }

                    // 완료 대기 (최대 30초)
                    int waitCount = 0;
                    while (!executionCompleted && waitCount < 60)
                    {
                        await Task.Delay(500);
                        waitCount++;
                    }

                    return executionCompleted && executionSuccessful;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"데모 레시피 실행 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 성능 테스트 실행
        /// </summary>
        /// <returns>성능 테스트 결과</returns>
        public static async Task<PerformanceTestResult> RunPerformanceTestAsync()
        {
            var result = new PerformanceTestResult();

            try
            {
                System.Diagnostics.Debug.WriteLine("=== 레시피 시스템 성능 테스트 시작 ===");

                // 1. 레시피 생성 성능 테스트
                var startTime = DateTime.Now;
                var testRecipe = TeachingDataIntegration.CreateRecipeFromTeachingGroup("Group1", TransferPattern.FullTransfer);
                result.RecipeCreationTime = DateTime.Now - startTime;

                // 2. 레시피 저장 성능 테스트
                startTime = DateTime.Now;
                await RecipeFileManager.SaveRecipeAsync(testRecipe, "PerformanceTest");
                result.RecipeSaveTime = DateTime.Now - startTime;

                // 3. 레시피 로드 성능 테스트
                startTime = DateTime.Now;
                var loadedRecipe = await RecipeFileManager.LoadRecipeAsync("PerformanceTest");
                result.RecipeLoadTime = DateTime.Now - startTime;

                // 4. 레시피 검증 성능 테스트
                startTime = DateTime.Now;
                var validation = loadedRecipe.Validate();
                result.RecipeValidationTime = DateTime.Now - startTime;

                // 5. 메모리 사용량 확인
                var beforeGC = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var afterGC = GC.GetTotalMemory(false);
                result.MemoryUsage = beforeGC - afterGC;

                result.IsSuccess = true;
                result.TestStepCount = testRecipe.StepCount;

                System.Diagnostics.Debug.WriteLine($"성능 테스트 완료:");
                System.Diagnostics.Debug.WriteLine($"  레시피 생성: {result.RecipeCreationTime.TotalMilliseconds:F1}ms");
                System.Diagnostics.Debug.WriteLine($"  레시피 저장: {result.RecipeSaveTime.TotalMilliseconds:F1}ms");
                System.Diagnostics.Debug.WriteLine($"  레시피 로드: {result.RecipeLoadTime.TotalMilliseconds:F1}ms");
                System.Diagnostics.Debug.WriteLine($"  레시피 검증: {result.RecipeValidationTime.TotalMilliseconds:F1}ms");
                System.Diagnostics.Debug.WriteLine($"  메모리 사용량: {result.MemoryUsage / 1024:F1} KB");

                // 정리
                RecipeFileManager.DeleteRecipe("PerformanceTest");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"성능 테스트 오류: {ex.Message}");
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// 스트레스 테스트 실행
        /// </summary>
        /// <param name="testCount">테스트 반복 횟수</param>
        /// <returns>스트레스 테스트 성공 여부</returns>
        public static async Task<bool> RunStressTestAsync(int testCount = 100)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== 레시피 시스템 스트레스 테스트 시작 ({testCount}회) ===");

                int successCount = 0;
                int failCount = 0;

                for (int i = 0; i < testCount; i++)
                {
                    try
                    {
                        // 레시피 생성
                        var recipe = CreateSimpleTestRecipe();
                        recipe.RecipeName = $"StressTest_{i}";

                        // 저장/로드 테스트
                        string fileName = $"StressTest_{i}";
                        bool saveResult = await RecipeFileManager.SaveRecipeAsync(recipe, fileName);
                        var loadedRecipe = await RecipeFileManager.LoadRecipeAsync(fileName);

                        if (saveResult && loadedRecipe != null)
                        {
                            successCount++;
                        }
                        else
                        {
                            failCount++;
                        }

                        // 정리
                        RecipeFileManager.DeleteRecipe(fileName);

                        // 진행 상황 출력 (10회마다)
                        if ((i + 1) % 10 == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"스트레스 테스트 진행: {i + 1}/{testCount}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"스트레스 테스트 {i} 실패: {ex.Message}");
                        failCount++;
                    }
                }

                double successRate = (double)successCount / testCount * 100;
                System.Diagnostics.Debug.WriteLine($"스트레스 테스트 완료: 성공 {successCount}/{testCount} ({successRate:F1}%)");

                return successRate >= 95; // 95% 이상 성공 시 통과
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"스트레스 테스트 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 레시피 시스템 상태 진단
        /// </summary>
        /// <returns>진단 결과</returns>
        public static async Task<SystemDiagnosticResult> RunSystemDiagnosticAsync()
        {
            var result = new SystemDiagnosticResult();

            try
            {
                System.Diagnostics.Debug.WriteLine("=== 레시피 시스템 상태 진단 시작 ===");

                // 1. 폴더 구조 확인
                result.FoldersExist = System.IO.Directory.Exists(RecipeFileManager.RecipeFolderPath) &&
                                     System.IO.Directory.Exists(RecipeFileManager.TemplateFolderPath) &&
                                     System.IO.Directory.Exists(RecipeFileManager.BackupFolderPath);

                // 2. 템플릿 파일 확인
                var templateList = RecipeFileManager.GetTemplateList();
                result.TemplateCount = templateList.Count;
                result.TemplatesReady = templateList.Count >= 4;

                // 3. 레시피 파일 개수 확인
                var recipeList = RecipeFileManager.GetRecipeFileList();
                result.RecipeCount = recipeList.Count;

                // 4. 백업 파일 개수 확인
                var backupList = RecipeFileManager.GetBackupList();
                result.BackupCount = backupList.Count;

                // 5. 로봇 컨트롤러 상태 확인
                var controller = RobotControllerFactory.GetCurrentController();
                result.RobotControllerReady = controller != null;

                // 6. 안전 시스템 상태 확인
                result.SafetySystemReady = SafetySystem.IsSafeForRobotOperation();

                // 7. Teaching 시스템 연동 상태 확인
                var locations = TeachingDataIntegration.GetAvailableLocations("Group1");
                result.TeachingSystemReady = locations.Count > 0;

                // 8. 전체 시스템 상태 계산
                result.OverallHealth = CalculateOverallHealth(result);

                System.Diagnostics.Debug.WriteLine($"시스템 진단 완료:");
                System.Diagnostics.Debug.WriteLine($"  폴더 구조: {(result.FoldersExist ? "정상" : "오류")}");
                System.Diagnostics.Debug.WriteLine($"  템플릿: {result.TemplateCount}개 ({(result.TemplatesReady ? "정상" : "부족")})");
                System.Diagnostics.Debug.WriteLine($"  레시피 파일: {result.RecipeCount}개");
                System.Diagnostics.Debug.WriteLine($"  백업 파일: {result.BackupCount}개");
                System.Diagnostics.Debug.WriteLine($"  로봇 컨트롤러: {(result.RobotControllerReady ? "정상" : "오류")}");
                System.Diagnostics.Debug.WriteLine($"  안전 시스템: {(result.SafetySystemReady ? "정상" : "주의")}");
                System.Diagnostics.Debug.WriteLine($"  Teaching 연동: {(result.TeachingSystemReady ? "정상" : "오류")}");
                System.Diagnostics.Debug.WriteLine($"  전체 상태: {result.OverallHealth:F1}%");

                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"시스템 진단 오류: {ex.Message}");
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// 전체 시스템 상태 계산
        /// </summary>
        private static double CalculateOverallHealth(SystemDiagnosticResult result)
        {
            double score = 0;
            int totalChecks = 6;

            if (result.FoldersExist) score += 1;
            if (result.TemplatesReady) score += 1;
            if (result.RobotControllerReady) score += 1;
            if (result.SafetySystemReady) score += 0.5; // 시뮬레이션 모드에서는 절반 점수
            if (result.TeachingSystemReady) score += 1;
            score += 0.5; // 기본 점수

            return (score / totalChecks) * 100;
        }
        #endregion
    }

    /// <summary>
    /// 성능 테스트 결과 클래스
    /// </summary>
    public class PerformanceTestResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = "";
        public TimeSpan RecipeCreationTime { get; set; }
        public TimeSpan RecipeSaveTime { get; set; }
        public TimeSpan RecipeLoadTime { get; set; }
        public TimeSpan RecipeValidationTime { get; set; }
        public long MemoryUsage { get; set; }
        public int TestStepCount { get; set; }
    }

    /// <summary>
    /// 시스템 진단 결과 클래스
    /// </summary>
    public class SystemDiagnosticResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = "";
        public bool FoldersExist { get; set; }
        public int TemplateCount { get; set; }
        public bool TemplatesReady { get; set; }
        public int RecipeCount { get; set; }
        public int BackupCount { get; set; }
        public bool RobotControllerReady { get; set; }
        public bool SafetySystemReady { get; set; }
        public bool TeachingSystemReady { get; set; }
        public double OverallHealth { get; set; }
    }
}