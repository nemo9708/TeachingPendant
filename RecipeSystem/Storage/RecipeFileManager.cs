using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TeachingPendant.RecipeSystem.Models;

namespace TeachingPendant.RecipeSystem.Storage
{
    /// <summary>
    /// 레시피 파일 관리 클래스
    /// JSON 형태로 레시피를 저장/로드하고 백업 기능 제공
    /// </summary>
    public static class RecipeFileManager
    {
        #region Constants
        private const string RECIPE_FOLDER = "Recipes";
        private const string RECIPE_EXTENSION = ".recipe.json";
        private const string TEMPLATE_FOLDER = "RecipeTemplates";
        private const string BACKUP_FOLDER = "RecipeBackups";
        #endregion

        #region Properties
        /// <summary>
        /// 레시피 저장 폴더 경로
        /// </summary>
        public static string RecipeFolderPath
        {
            get
            {
                string baseFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TeachingPendantData");
                return Path.Combine(baseFolder, RECIPE_FOLDER);
            }
        }

        /// <summary>
        /// 레시피 템플릿 폴더 경로
        /// </summary>
        public static string TemplateFolderPath
        {
            get
            {
                string baseFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TeachingPendantData");
                return Path.Combine(baseFolder, TEMPLATE_FOLDER);
            }
        }

        /// <summary>
        /// 레시피 백업 폴더 경로
        /// </summary>
        public static string BackupFolderPath
        {
            get
            {
                string baseFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TeachingPendantData");
                return Path.Combine(baseFolder, BACKUP_FOLDER);
            }
        }
        #endregion

        #region Public Methods - Recipe File Operations
        /// <summary>
        /// 레시피를 파일로 저장
        /// </summary>
        /// <param name="recipe">저장할 레시피</param>
        /// <param name="fileName">파일명 (확장자 제외)</param>
        /// <returns>저장 성공 여부</returns>
        public static async Task<bool> SaveRecipeAsync(TransferRecipe recipe, string fileName = null)
        {
            try
            {
                if (recipe == null)
                {
                    System.Diagnostics.Debug.WriteLine("[RecipeFileManager] 저장할 레시피가 null입니다");
                    return false;
                }

                EnsureFoldersExist();

                // 파일명 생성
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = GenerateFileName(recipe.RecipeName);
                }

                string filePath = Path.Combine(RecipeFolderPath, fileName + RECIPE_EXTENSION);

                // 기존 파일 백업
                if (File.Exists(filePath))
                {
                    await CreateBackupAsync(filePath);
                }

                // 레시피 수정 날짜 업데이트
                recipe.ModifiedDate = DateTime.Now;

                // JSON 직렬화 및 저장 (C# 6.0 호환)
                string json = JsonConvert.SerializeObject(recipe, Formatting.Indented);
                await Task.Run(() => File.WriteAllText(filePath, json));

                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 레시피 저장 완료: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 레시피 저장 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 전체 백업 생성 (동기 버전)
        /// </summary>
        /// <returns>백업 성공 여부</returns>
        public static bool CreateBackup()
        {
            try
            {
                EnsureFoldersExist();

                var recipeFiles = Directory.GetFiles(RecipeFolderPath, "*" + RECIPE_EXTENSION);
                string backupFolderName = $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                string backupPath = Path.Combine(BackupFolderPath, backupFolderName);

                Directory.CreateDirectory(backupPath);

                foreach (var file in recipeFiles)
                {
                    string fileName = Path.GetFileName(file);
                    string destPath = Path.Combine(backupPath, fileName);
                    File.Copy(file, destPath);
                }

                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 전체 백업 완료: {backupFolderName}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 전체 백업 실패: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// 레시피 로드 (동기 버전)
        /// </summary>
        /// <param name="fileName">파일명</param>
        /// <returns>로드된 레시피</returns>
        public static TransferRecipe LoadRecipe(string fileName)
        {
            try
            {
                return LoadRecipeAsync(fileName).Result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] LoadRecipe 동기 호출 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 템플릿 로드 (동기 버전)
        /// </summary>
        /// <param name="templateName">템플릿 이름</param>
        /// <returns>로드된 템플릿</returns>
        public static TransferRecipe LoadTemplate(string templateName)
        {
            try
            {
                return LoadTemplateAsync(templateName).Result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] LoadTemplate 동기 호출 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 레시피 저장 (동기 버전)
        /// </summary>
        /// <param name="recipe">저장할 레시피</param>
        /// <param name="fileName">파일명</param>
        /// <returns>저장 성공 여부</returns>
        public static bool SaveRecipe(TransferRecipe recipe, string fileName = null)
        {
            try
            {
                return SaveRecipeAsync(recipe, fileName).Result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] SaveRecipe 동기 호출 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 파일에서 레시피 로드
        /// </summary>
        /// <param name="fileName">파일명 (확장자 포함 또는 제외)</param>
        /// <returns>로드된 레시피 또는 null</returns>
        public static async Task<TransferRecipe> LoadRecipeAsync(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    return null;
                }

                // 확장자 처리
                if (!fileName.EndsWith(RECIPE_EXTENSION))
                {
                    fileName += RECIPE_EXTENSION;
                }

                string filePath = Path.Combine(RecipeFolderPath, fileName);

                if (!File.Exists(filePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 레시피 파일이 존재하지 않음: {filePath}");
                    return null;
                }

                // C# 6.0 호환 방식
                string json = await Task.Run(() => File.ReadAllText(filePath));
                var recipe = JsonConvert.DeserializeObject<TransferRecipe>(json);

                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 레시피 로드 완료: {recipe?.RecipeName}");
                return recipe;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 레시피 로드 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 모든 레시피 파일 목록 가져오기
        /// </summary>
        /// <returns>레시피 파일 정보 목록</returns>
        public static List<RecipeFileInfo> GetRecipeFileList()
        {
            var fileList = new List<RecipeFileInfo>();

            try
            {
                EnsureFoldersExist();

                var files = Directory.GetFiles(RecipeFolderPath, "*" + RECIPE_EXTENSION);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var recipeInfo = new RecipeFileInfo
                        {
                            FileName = Path.GetFileNameWithoutExtension(file).Replace(".recipe", ""),
                            FilePath = file,
                            FileSize = fileInfo.Length,
                            CreatedDate = fileInfo.CreationTime,
                            ModifiedDate = fileInfo.LastWriteTime
                        };

                        // 레시피 메타 정보 추출 (파일 전체 로드 없이)
                        string jsonPreview = File.ReadAllText(file);
                        if (jsonPreview.Length > 200)
                        {
                            jsonPreview = jsonPreview.Substring(0, 200);
                        }

                        // 간단한 정규식으로 RecipeName 추출
                        var nameMatch = System.Text.RegularExpressions.Regex.Match(jsonPreview, "\"RecipeName\"\\s*:\\s*\"([^\"]+)\"");
                        if (nameMatch.Success)
                        {
                            recipeInfo.RecipeName = nameMatch.Groups[1].Value;
                        }

                        fileList.Add(recipeInfo);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 파일 정보 추출 실패: {file}, {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 레시피 파일 목록 조회: {fileList.Count}개");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 레시피 목록 조회 실패: {ex.Message}");
            }

            return fileList;
        }

        /// <summary>
        /// 레시피 파일 삭제
        /// </summary>
        /// <param name="fileName">파일명</param>
        /// <returns>삭제 성공 여부</returns>
        public static bool DeleteRecipe(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    return false;
                }

                // 확장자 처리
                if (!fileName.EndsWith(RECIPE_EXTENSION))
                {
                    fileName += RECIPE_EXTENSION;
                }

                string filePath = Path.Combine(RecipeFolderPath, fileName);

                if (File.Exists(filePath))
                {
                    // 삭제 전 백업
                    CreateBackupAsync(filePath).Wait();
                    File.Delete(filePath);

                    System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 레시피 삭제 완료: {fileName}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 레시피 삭제 실패: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Template Management
        /// <summary>
        /// 기본 레시피 템플릿 생성
        /// </summary>
        public static async Task CreateDefaultTemplatesAsync()
        {
            try
            {
                EnsureFoldersExist();

                await CreateSingleWaferTemplate();
                await CreateBatchWaferTemplate();
                await CreateFullTransferTemplate();
                await CreateCustomPatternTemplate();

                System.Diagnostics.Debug.WriteLine("[RecipeFileManager] 기본 템플릿 생성 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 템플릿 생성 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 단일 웨이퍼 반송 템플릿
        /// </summary>
        private static async Task CreateSingleWaferTemplate()
        {
            var template = new TransferRecipe("단일 웨이퍼 반송", "P1에서 P4로 웨이퍼 1개를 반송하는 기본 템플릿");

            template.AddStep(new RecipeStep(StepType.Home, "홈 위치로 이동"));
            template.AddStep(new RecipeStep(StepType.CheckSafety, "안전 상태 확인"));

            var pickStep = new RecipeStep(StepType.Pick, "P1에서 웨이퍼 집기")
            {
                TeachingGroupName = "Group1",
                TeachingLocationName = "P1"
            };
            template.AddStep(pickStep);

            var placeStep = new RecipeStep(StepType.Place, "P4에 웨이퍼 놓기")
            {
                TeachingGroupName = "Group1",
                TeachingLocationName = "P4"
            };
            template.AddStep(placeStep);

            template.AddStep(new RecipeStep(StepType.Home, "완료 후 홈 복귀"));

            string filePath = Path.Combine(TemplateFolderPath, "SingleWafer_Template.recipe.json");
            string json = JsonConvert.SerializeObject(template, Formatting.Indented);
            await Task.Run(() => File.WriteAllText(filePath, json)); // 수정된 부분
        }

        /// <summary>
        /// 5개 웨이퍼 배치 반송 템플릿
        /// </summary>
        private static async Task CreateBatchWaferTemplate()
        {
            var template = new TransferRecipe("5개 웨이퍼 배치 반송", "P1~P5에서 P4로 웨이퍼 5개를 순차 반송");
            template.AddStep(new RecipeStep(StepType.Home, "홈 위치로 이동"));
            template.AddStep(new RecipeStep(StepType.CheckSafety, "안전 상태 확인"));

            for (int i = 1; i <= 5; i++)
            {
                var pickStep = new RecipeStep(StepType.Pick, $"P{i}에서 웨이퍼 {i} 집기")
                {
                    TeachingGroupName = "Group1",
                    TeachingLocationName = $"P{i}"
                };
                template.AddStep(pickStep);

                var placeStep = new RecipeStep(StepType.Place, $"P4에 웨이퍼 {i} 놓기")
                {
                    TeachingGroupName = "Group1",
                    TeachingLocationName = "P4"
                };
                template.AddStep(placeStep);

                if (i < 5)
                {
                    template.AddStep(new RecipeStep(StepType.Wait, $"웨이퍼 {i} 완료 후 대기") { WaitTimeMs = 500 });
                }
            }

            template.AddStep(new RecipeStep(StepType.Home, "완료 후 홈 복귀"));

            string filePath = Path.Combine(TemplateFolderPath, "BatchWafer_Template.recipe.json");
            string json = JsonConvert.SerializeObject(template, Formatting.Indented);

            // C# 6.0 호환 방식으로 수정
            await Task.Run(() => File.WriteAllText(filePath, json));
        }

        /// <summary>
        /// 25개 웨이퍼 전체 반송 템플릿
        /// </summary>
        private static async Task CreateFullTransferTemplate()
        {
            var template = new TransferRecipe("전체 웨이퍼 반송", "25개 웨이퍼 전체 반송 템플릿");

            template.AddStep(new RecipeStep(StepType.Home, "홈 위치로 이동"));
            template.AddStep(new RecipeStep(StepType.CheckSafety, "시작 전 안전 상태 확인"));

            // 25개 웨이퍼 반송 스텝 생성
            for (int waferNum = 1; waferNum <= 25; waferNum++)
            {
                template.AddStep(new RecipeStep(StepType.Pick, $"웨이퍼 {waferNum} 픽업"));
                template.AddStep(new RecipeStep(StepType.Place, $"웨이퍼 {waferNum} 배치"));

                // 5개마다 안전 확인
                if (waferNum % 5 == 0)
                {
                    template.AddStep(new RecipeStep(StepType.CheckSafety, $"{waferNum}개 완료 후 안전 확인"));
                }
            }

            template.AddStep(new RecipeStep(StepType.Home, "전체 작업 완료 후 홈 복귀"));

            string filePath = Path.Combine(TemplateFolderPath, "FullTransfer_Template.recipe.json");
            string json = JsonConvert.SerializeObject(template, Formatting.Indented);

            // C# 6.0 호환 방식으로 수정
            await Task.Run(() => File.WriteAllText(filePath, json));
        }

        /// <summary>
        /// 사용자 정의 패턴 템플릿
        /// </summary>
        private static async Task CreateCustomPatternTemplate()
        {
            var template = new TransferRecipe("사용자 정의 패턴", "사용자가 편집할 수 있는 빈 템플릿");

            template.AddStep(new RecipeStep(StepType.Home, "홈 위치로 이동"));
            template.AddStep(new RecipeStep(StepType.CheckSafety, "안전 상태 확인"));

            // 빈 Move 스텝 (사용자가 편집)
            template.AddStep(new RecipeStep(StepType.Move, "사용자 정의 이동 1"));
            template.AddStep(new RecipeStep(StepType.Move, "사용자 정의 이동 2"));

            template.AddStep(new RecipeStep(StepType.Home, "완료 후 홈 복귀"));

            string filePath = Path.Combine(TemplateFolderPath, "CustomPattern_Template.recipe.json");
            string json = JsonConvert.SerializeObject(template, Formatting.Indented);

            // C# 6.0 호환 방식으로 수정
            await Task.Run(() => File.WriteAllText(filePath, json));
        }

        /// <summary>
        /// 템플릿에서 레시피 로드
        /// </summary>
        /// <param name="templateName">템플릿 이름</param>
        /// <returns>로드된 레시피 또는 null</returns>
        public static async Task<TransferRecipe> LoadTemplateAsync(string templateName)
        {
            try
            {
                if (string.IsNullOrEmpty(templateName))
                {
                    return null;
                }

                // 확장자 처리
                if (!templateName.EndsWith(RECIPE_EXTENSION))
                {
                    templateName += RECIPE_EXTENSION;
                }

                string filePath = Path.Combine(TemplateFolderPath, templateName);

                if (!File.Exists(filePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 템플릿 파일이 존재하지 않음: {filePath}");
                    return null;
                }

                // C# 6.0 호환 방식으로 수정
                string json = await Task.Run(() => File.ReadAllText(filePath));
                var template = JsonConvert.DeserializeObject<TransferRecipe>(json);

                // 템플릿을 새 레시피로 변환 (ID 새로 생성)
                if (template != null)
                {
                    template.RecipeId = Guid.NewGuid().ToString();
                    template.CreatedDate = DateTime.Now;
                    template.ModifiedDate = DateTime.Now;
                }

                return template;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 템플릿 로드 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 템플릿 목록 가져오기 (동기 버전)
        /// </summary>
        /// <returns>템플릿 이름 목록</returns>
        public static List<string> GetTemplateList()
        {
            try
            {
                EnsureFoldersExist();

                var templateFiles = Directory.GetFiles(TemplateFolderPath, "*" + RECIPE_EXTENSION);
                var templateNames = new List<string>();

                foreach (var file in templateFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    // .recipe 부분까지 제거
                    if (fileName.EndsWith(".recipe"))
                    {
                        fileName = fileName.Substring(0, fileName.Length - 7);
                    }
                    templateNames.Add(fileName);
                }

                return templateNames;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 템플릿 목록 조회 실패: {ex.Message}");
                return new List<string>();
            }
        }
        #endregion

        #region Backup Management
        /// <summary>
        /// 백업 파일 목록 가져오기
        /// </summary>
        /// <returns>백업 파일 정보 목록</returns>
        public static List<RecipeFileInfo> GetBackupList()
        {
            var backupList = new List<RecipeFileInfo>();

            try
            {
                EnsureFoldersExist();

                var files = Directory.GetFiles(BackupFolderPath, "*" + RECIPE_EXTENSION);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var backupInfo = new RecipeFileInfo
                        {
                            FileName = Path.GetFileNameWithoutExtension(file).Replace(".recipe", ""),
                            FilePath = file,
                            FileSize = fileInfo.Length,
                            CreatedDate = fileInfo.CreationTime,
                            ModifiedDate = fileInfo.LastWriteTime
                        };

                        backupList.Add(backupInfo);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 백업 파일 정보 추출 실패: {file}, {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 백업 목록 조회: {backupList.Count}개");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 백업 목록 조회 실패: {ex.Message}");
            }

            return backupList;
        }

        /// <summary>
        /// 백업에서 레시피 복원
        /// </summary>
        /// <param name="backupFileName">백업 파일명</param>
        /// <param name="restoreFileName">복원할 파일명</param>
        /// <returns>복원 성공 여부</returns>
        public static async Task<bool> RestoreFromBackupAsync(string backupFileName, string restoreFileName)
        {
            try
            {
                if (!backupFileName.EndsWith(RECIPE_EXTENSION))
                {
                    backupFileName += RECIPE_EXTENSION;
                }

                if (!restoreFileName.EndsWith(RECIPE_EXTENSION))
                {
                    restoreFileName += RECIPE_EXTENSION;
                }

                string backupPath = Path.Combine(BackupFolderPath, backupFileName);
                string restorePath = Path.Combine(RecipeFolderPath, restoreFileName);

                if (!File.Exists(backupPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 백업 파일이 존재하지 않음: {backupPath}");
                    return false;
                }

                // 현재 파일이 있으면 백업
                if (File.Exists(restorePath))
                {
                    await CreateBackupAsync(restorePath);
                }

                // 백업에서 복원
                await Task.Run(() => File.Copy(backupPath, restorePath, true));

                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 백업 복원 완료: {backupFileName} → {restoreFileName}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 백업 복원 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 오래된 백업 파일 정리
        /// </summary>
        /// <param name="keepDays">보관할 일수</param>
        /// <returns>정리된 파일 수</returns>
        public static int CleanupOldBackups(int keepDays = 30)
        {
            int cleanedCount = 0;

            try
            {
                EnsureFoldersExist();

                var cutoffDate = DateTime.Now.AddDays(-keepDays);
                var files = Directory.GetFiles(BackupFolderPath, "*" + RECIPE_EXTENSION);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoffDate)
                        {
                            File.Delete(file);
                            cleanedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 백업 파일 삭제 실패: {file}, {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 오래된 백업 파일 정리 완료: {cleanedCount}개");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 백업 파일 정리 실패: {ex.Message}");
            }

            return cleanedCount;
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// 필요한 폴더들 생성
        /// </summary>
        private static void EnsureFoldersExist()
        {
            Directory.CreateDirectory(RecipeFolderPath);
            Directory.CreateDirectory(TemplateFolderPath);
            Directory.CreateDirectory(BackupFolderPath);
        }

        /// <summary>
        /// 안전한 파일명 생성
        /// </summary>
        private static string GenerateFileName(string recipeName)
        {
            if (string.IsNullOrEmpty(recipeName))
            {
                return $"Recipe_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            // 파일명에 사용할 수 없는 문자 제거
            string fileName = recipeName;
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            return $"{fileName}_{DateTime.Now:yyyyMMdd_HHmmss}";
        }

        /// <summary>
        /// 파일 백업 생성
        /// </summary>
        private static async Task CreateBackupAsync(string originalFilePath)
        {
            try
            {
                if (!File.Exists(originalFilePath))
                    return;

                string fileName = Path.GetFileName(originalFilePath);
                string backupFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_backup_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(fileName)}";
                string backupPath = Path.Combine(BackupFolderPath, backupFileName);

                await Task.Run(() => File.Copy(originalFilePath, backupPath));

                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 백업 생성: {backupPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeFileManager] 백업 생성 실패: {ex.Message}");
            }
        }
        #endregion
    }

    /// <summary>
    /// 레시피 파일 정보 클래스
    /// </summary>
    public class RecipeFileInfo
    {
        public string FileName { get; set; } = "";
        public string RecipeName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        public string FileSizeDisplay => FormatFileSize(FileSize);

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            return $"{bytes / (1024 * 1024):F1} MB";
        }
    }
}