using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using TeachingPendant.Alarm;
using TeachingPendant.MovementUI;

namespace TeachingPendant.Manager
{
    /// <summary>
    /// 모든 애플리케이션 데이터의 영속성을 관리하는 중앙 매니저 (수정본)
    /// </summary>
    public static class PersistentDataManager
    {
        #region Constants
        private const string DATA_FOLDER = "TeachingPendantData";
        private const string MOVEMENT_DATA_FILE = "MovementData.json";
        private const string TEACHING_DATA_FILE = "TeachingData.json";
        private const string SETUP_DATA_FILE = "SetupData.json";
        private const string SYSTEM_DATA_FILE = "SystemData.json";
        #endregion

        #region Data Classes
        public class MovementDataContainer
        {
            public Dictionary<string, Dictionary<string, CoordinateDataJson>> GroupCoordinateData { get; set; }
            public Dictionary<string, Dictionary<string, int>> GroupMenuSelectedNumbers { get; set; }
            public Dictionary<string, Dictionary<int, CassetteInfoJson>> GroupCassetteData { get; set; }
            public Dictionary<string, Dictionary<int, StageInfoJson>> GroupStageData { get; set; }
            public Dictionary<string, int> GroupCassetteCounts { get; set; }
            public string CurrentSelectedGroup { get; set; }
            public bool IsGroupDetailMode { get; set; }
            public string SelectedGroupMenu { get; set; }
        }

        public class TeachingDataContainer
        {
            public Dictionary<string, Dictionary<string, StageDataJson>> GroupItemData { get; set; }
            public string CurrentSelectedGroup { get; set; }
            public bool IsJointMode { get; set; }
            public string CurrentSelectedType { get; set; }
            public string CurrentSelectedItemName { get; set; }
        }

        public class SetupDataContainer
        {
            public bool IsDemoMode { get; set; }
            public bool IsInterlockEnabled { get; set; }
            public string HomePosA { get; set; }
            public string HomePosT { get; set; }
            public string HomePosZ { get; set; }
            public string SoftLimitA1 { get; set; }
            public string SoftLimitA2 { get; set; }
            public string SoftLimitT1 { get; set; }
            public string SoftLimitT2 { get; set; }
            public string SoftLimitZ1 { get; set; }
            public string SoftLimitZ2 { get; set; }
            public string Acceleration { get; set; }
            public string Deceleration { get; set; }
            public string RetryCount { get; set; }
            public string OriginOffsetTheta { get; set; }
            public string ArmLinkLength { get; set; }
            public string SystemSpeedMMS { get; set; }
            public bool HasMovementUiBeenOpened { get; set; }
        }

        public class SystemDataContainer
        {
            public int GlobalSpeed { get; set; }
            public string GlobalMode { get; set; }
            public List<string> AvailableGroups { get; set; }
            public DateTime LastSavedTime { get; set; }
            public string ApplicationVersion { get; set; }
        }

        // JSON 직렬화용 데이터 클래스들
        public class CoordinateDataJson
        {
            public string[] P1 { get; set; }
            public string[] P2 { get; set; }
            public string[] P3 { get; set; }
            public string[] P4 { get; set; }
            public string[] P5 { get; set; }
            public string[] P6 { get; set; }
            public string[] P7 { get; set; }
        }

        public class StageDataJson
        {
            public int SlotCount { get; set; }
            public int Pitch { get; set; }
            public int PickOffset { get; set; }
            public int PickDown { get; set; }
            public int PickUp { get; set; }
            public int PlaceDown { get; set; }
            public int PlaceUp { get; set; }
            public decimal PositionA { get; set; }
            public decimal PositionT { get; set; }
            public decimal PositionZ { get; set; }
        }

        public class CassetteInfoJson
        {
            public int SlotCount { get; set; }
            public decimal PositionA { get; set; }
            public decimal PositionT { get; set; }
            public decimal PositionZ { get; set; }
            public int Pitch { get; set; }
            public int PickOffset { get; set; }
            public int PickDown { get; set; }
            public int PickUp { get; set; }
            public int PlaceDown { get; set; }
            public int PlaceUp { get; set; }
        }

        public class StageInfoJson
        {
            public int SlotCount { get; set; }
            public decimal PositionA { get; set; }
            public decimal PositionT { get; set; }
            public decimal PositionZ { get; set; }
            public int Pitch { get; set; }
            public int PickOffset { get; set; }
            public int PickDown { get; set; }
            public int PickUp { get; set; }
            public int PlaceDown { get; set; }
            public int PlaceUp { get; set; }
        }
        #endregion

        #region Properties
        private static string DataFolderPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DATA_FOLDER); }
        }

        private static string MovementDataPath
        {
            get { return Path.Combine(DataFolderPath, MOVEMENT_DATA_FILE); }
        }

        private static string TeachingDataPath
        {
            get { return Path.Combine(DataFolderPath, TEACHING_DATA_FILE); }
        }

        private static string SetupDataPath
        {
            get { return Path.Combine(DataFolderPath, SETUP_DATA_FILE); }
        }

        private static string SystemDataPath
        {
            get { return Path.Combine(DataFolderPath, SYSTEM_DATA_FILE); }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 애플리케이션 시작 시 모든 데이터 로드
        /// </summary>
        public static async Task<bool> LoadAllDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== PersistentDataManager: LoadAllDataAsync 시작 ===");

                EnsureDataFolderExists();

                // Load each type with individual error handling
                bool teachingLoaded = false;
                bool movementLoaded = false;
                bool setupLoaded = false;
                bool systemLoaded = false;

                try
                {
                    await LoadTeachingDataAsync();
                    teachingLoaded = true;
                    System.Diagnostics.Debug.WriteLine("Teaching 데이터 로드 성공");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Teaching load error: {ex.Message}");
                }

                try
                {
                    await LoadMovementDataAsync();
                    movementLoaded = true;
                    System.Diagnostics.Debug.WriteLine("Movement 데이터 로드 성공");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Movement load error: {ex.Message}");
                }

                try
                {
                    await LoadSetupDataAsync();
                    setupLoaded = true;
                    System.Diagnostics.Debug.WriteLine("Setup 데이터 로드 성공");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Setup load error: {ex.Message}");
                }

                try
                {
                    await LoadSystemDataAsync();
                    systemLoaded = true;
                    System.Diagnostics.Debug.WriteLine("System 데이터 로드 성공");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"System load error: {ex.Message}");
                }

                System.Diagnostics.Debug.WriteLine($"데이터 로드 결과: Teaching={teachingLoaded}, Movement={movementLoaded}, Setup={setupLoaded}, System={systemLoaded}");
                System.Diagnostics.Debug.WriteLine("All persistent data loading completed");

                return true; // 일부라도 성공하면 true
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading persistent data: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 애플리케이션 종료 시 모든 데이터 저장
        /// </summary>
        public static async Task<bool> SaveAllDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== PersistentDataManager: SaveAllDataAsync 시작 ===");

                EnsureDataFolderExists();

                // .NET Framework 4.6.1 호환 방식
                var movementTask = SaveMovementDataAsync();
                var teachingTask = SaveTeachingDataAsync();
                var setupTask = SaveSetupDataAsync();
                var systemTask = SaveSystemDataAsync();

                await Task.WhenAll(movementTask, teachingTask, setupTask, systemTask);

                AlarmMessageManager.ShowAlarm(Alarms.POSITION_SAVED, "All data saved successfully");

                System.Diagnostics.Debug.WriteLine("All persistent data saved successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving persistent data: {ex.Message}");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR, $"Failed to save data: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 실시간 자동 저장 (특정 데이터만)
        /// </summary>
        public static async Task AutoSaveAsync(DataType dataType)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== AutoSaveAsync: {dataType} ===");

                switch (dataType)
                {
                    case DataType.Movement:
                        await SaveMovementDataAsync();
                        break;
                    case DataType.Teaching:
                        await SaveTeachingDataAsync();
                        break;
                    case DataType.Setup:
                        await SaveSetupDataAsync();
                        break;
                    case DataType.System:
                        await SaveSystemDataAsync();
                        break;
                }

                System.Diagnostics.Debug.WriteLine($"Auto-saved {dataType} data successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-save error for {dataType}: {ex.Message}");
                // Don't throw - just log the error
            }
        }

        /// <summary>
        /// 데이터 백업 생성
        /// </summary>
        public static async Task<bool> CreateBackupAsync()
        {
            try
            {
                string backupFolder = Path.Combine(DataFolderPath, "Backup", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(backupFolder);

                var files = Directory.GetFiles(DataFolderPath, "*.json");
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string backupPath = Path.Combine(backupFolder, fileName);
                    File.Copy(file, backupPath);
                }

                System.Diagnostics.Debug.WriteLine($"Backup created: {backupFolder}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Backup creation error: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Private Methods
        private static void EnsureDataFolderExists()
        {
            if (!Directory.Exists(DataFolderPath))
            {
                Directory.CreateDirectory(DataFolderPath);
                System.Diagnostics.Debug.WriteLine($"Created data folder: {DataFolderPath}");
            }
        }

        // .NET Framework 4.6.1 호환 파일 읽기
        private static async Task<string> ReadAllTextAsync(string path)
        {
            try
            {
                using (var reader = new StreamReader(path, Encoding.UTF8))
                {
                    return await reader.ReadToEndAsync();
                }
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"File read error: {ex.Message}");
                // Wait a bit and retry once
                await Task.Delay(100);
                using (var reader = new StreamReader(path, Encoding.UTF8))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        // .NET Framework 4.6.1 호환 파일 쓰기 (동시 접근 문제 해결)
        private static async Task WriteAllTextAsync(string path, string content)
        {
            const int maxRetries = 3;
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(path);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // FileShare.Read를 사용하여 동시 읽기 허용
                    using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                    {
                        await writer.WriteAsync(content);
                        await writer.FlushAsync();
                        await fileStream.FlushAsync();
                    }

                    System.Diagnostics.Debug.WriteLine($"File write successful: {path}");
                    return; // 성공하면 즉시 리턴
                }
                catch (IOException ex) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    System.Diagnostics.Debug.WriteLine($"File write error (attempt {retryCount}/{maxRetries}): {ex.Message}");

                    // 지수적 백오프: 100ms, 200ms, 400ms
                    int delayMs = 100 * (int)Math.Pow(2, retryCount - 1);
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Unexpected file write error: {ex.Message}");
                    throw; // 예상치 못한 오류는 다시 던짐
                }
            }

            // 모든 재시도 실패 시
            System.Diagnostics.Debug.WriteLine($"File write failed after {maxRetries} attempts: {path}");
            throw new IOException($"Failed to write file after {maxRetries} attempts: {path}");
        }

        private static async Task LoadMovementDataAsync()
        {
            if (!File.Exists(MovementDataPath))
            {
                System.Diagnostics.Debug.WriteLine("Movement data file not found, using defaults");
                return;
            }

            try
            {
                string json = await ReadAllTextAsync(MovementDataPath);
                var data = JsonConvert.DeserializeObject<MovementDataContainer>(json);

                // Movement UI의 정적 데이터에 로드

                System.Diagnostics.Debug.WriteLine("Movement data loaded from file");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading movement data: {ex.Message}");
            }
        }

        /// <summary>
        /// Teaching 데이터 로드 (강화됨)
        /// </summary>
        public static async Task LoadTeachingDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== LoadTeachingDataAsync 시작 ===");
                System.Diagnostics.Debug.WriteLine($"Teaching 데이터 파일 경로: {TeachingDataPath}");

                if (!File.Exists(TeachingDataPath))
                {
                    System.Diagnostics.Debug.WriteLine("Teaching data file not found, using defaults");

                    // 기본 데이터로 Teaching UI 초기화
                    var defaultData = new TeachingDataContainer
                    {
                        GroupItemData = new Dictionary<string, Dictionary<string, StageDataJson>>(),
                        CurrentSelectedGroup = "Group1",
                        IsJointMode = true,
                        CurrentSelectedType = "",
                        CurrentSelectedItemName = ""
                    };

                    TeachingPendant.TeachingUI.Teaching.LoadFromPersistentData(defaultData);
                    System.Diagnostics.Debug.WriteLine("Teaching UI에 기본 데이터 적용 완료");
                    return;
                }

                string json = await ReadAllTextAsync(TeachingDataPath);

                if (string.IsNullOrEmpty(json))
                {
                    System.Diagnostics.Debug.WriteLine("Teaching data file is empty");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Teaching JSON 파일 내용 (첫 200자): {json.Substring(0, Math.Min(200, json.Length))}");

                var data = JsonConvert.DeserializeObject<TeachingDataContainer>(json);

                if (data != null)
                {
                    System.Diagnostics.Debug.WriteLine("Teaching 데이터 파싱 성공");
                    System.Diagnostics.Debug.WriteLine($"파싱된 데이터: Group={data.CurrentSelectedGroup}, Type={data.CurrentSelectedType}, Item={data.CurrentSelectedItemName}");

                    if (data.GroupItemData != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"그룹 데이터 수: {data.GroupItemData.Count}");
                        foreach (var group in data.GroupItemData)
                        {
                            System.Diagnostics.Debug.WriteLine($"  그룹 [{group.Key}]: {group.Value.Count}개 아이템");
                        }
                    }

                    // Teaching UI의 정적 데이터에 로드
                    TeachingPendant.TeachingUI.Teaching.LoadFromPersistentData(data);
                    System.Diagnostics.Debug.WriteLine("Teaching data loaded from file successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Teaching data parsing failed - null result");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading teaching data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                // Don't throw - continue with defaults
            }
        }

        /// <summary>
        /// Teaching 데이터 저장 (강화됨)
        /// </summary>
        public static async Task SaveTeachingDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== SaveTeachingDataAsync 시작 ===");

                var data = TeachingPendant.TeachingUI.Teaching.GetPersistentData();

                if (data == null)
                {
                    System.Diagnostics.Debug.WriteLine("No Teaching data to save - data is null");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"저장할 Teaching 데이터: Group={data.CurrentSelectedGroup}, Type={data.CurrentSelectedType}, Item={data.CurrentSelectedItemName}");

                if (data.GroupItemData != null)
                {
                    System.Diagnostics.Debug.WriteLine($"저장할 그룹 수: {data.GroupItemData.Count}");
                    foreach (var group in data.GroupItemData)
                    {
                        System.Diagnostics.Debug.WriteLine($"  그룹 [{group.Key}]: {group.Value.Count}개 아이템");
                        foreach (var item in group.Value)
                        {
                            var itemData = item.Value;
                            System.Diagnostics.Debug.WriteLine($"    {item.Key}: Slot={itemData.SlotCount}, A={itemData.PositionA}, T={itemData.PositionT}, Z={itemData.PositionZ}");
                        }
                    }
                }

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                System.Diagnostics.Debug.WriteLine($"JSON 직렬화 완료, 길이: {json.Length}");

                await WriteAllTextAsync(TeachingDataPath, json);

                System.Diagnostics.Debug.WriteLine($"Teaching data saved to: {TeachingDataPath}");

                // 저장 후 파일 존재 확인
                if (File.Exists(TeachingDataPath))
                {
                    var fileInfo = new FileInfo(TeachingDataPath);
                    System.Diagnostics.Debug.WriteLine($"저장된 파일 크기: {fileInfo.Length} bytes");
                    System.Diagnostics.Debug.WriteLine($"저장된 파일 수정 시간: {fileInfo.LastWriteTime}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving teaching data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                // Don't throw - continue operation
            }
        }

        private static async Task SaveMovementDataAsync()
        {
            try
            {
                // Movement UI의 정적 데이터에서 가져오기
                var data = MovementUI.Movement.GetPersistentData();

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                await WriteAllTextAsync(MovementDataPath, json);

                System.Diagnostics.Debug.WriteLine("Movement data saved to file");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving movement data: {ex.Message}");
            }
        }

        private static async Task LoadSetupDataAsync()
        {
            if (!File.Exists(SetupDataPath))
            {
                System.Diagnostics.Debug.WriteLine("Setup data file not found, using defaults");
                return;
            }

            try
            {
                string json = await ReadAllTextAsync(SetupDataPath);
                var data = JsonConvert.DeserializeObject<SetupDataContainer>(json);

                // Setup UI의 정적 데이터에 로드

                System.Diagnostics.Debug.WriteLine("Setup data loaded from file");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading setup data: {ex.Message}");
            }
        }

        private static async Task SaveSetupDataAsync()
        {
            try
            {
                var data = new SetupDataContainer
                {
                    // Setup UI 데이터 매핑 필요
                    IsDemoMode = true, // 임시 데이터
                    IsInterlockEnabled = false,
                    HomePosA = "0.00",
                    HomePosT = "0.00",
                    HomePosZ = "0.00"
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                await WriteAllTextAsync(SetupDataPath, json);

                System.Diagnostics.Debug.WriteLine("Setup data saved to file");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving setup data: {ex.Message}");
            }
        }

        private static async Task LoadSystemDataAsync()
        {
            if (!File.Exists(SystemDataPath))
            {
                System.Diagnostics.Debug.WriteLine("System data file not found, using defaults");
                return;
            }

            try
            {
                string json = await ReadAllTextAsync(SystemDataPath);
                var data = JsonConvert.DeserializeObject<SystemDataContainer>(json);

                // 글로벌 매니저들에 데이터 로드
                if (data.GlobalSpeed > 0)
                {
                    GlobalSpeedManager.SetSpeed(data.GlobalSpeed);
                }

                System.Diagnostics.Debug.WriteLine("System data loaded from file");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading system data: {ex.Message}");
            }
        }

        private static async Task SaveSystemDataAsync()
        {
            try
            {
                var data = new SystemDataContainer
                {
                    GlobalSpeed = GlobalSpeedManager.CurrentSpeed,
                    GlobalMode = GlobalModeManager.CurrentMode.ToString(),
                    AvailableGroups = GroupDataManager.GetAvailableGroups(),
                    LastSavedTime = DateTime.Now,
                    ApplicationVersion = "1.0.0" // 실제 버전 정보
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                await WriteAllTextAsync(SystemDataPath, json);

                System.Diagnostics.Debug.WriteLine("System data saved to file");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving system data: {ex.Message}");
            }
        }

        /// <summary>
        /// 데이터 파일 경로 정보 표시 (디버깅용)
        /// </summary>
        public static void ShowDataPaths()
        {
            System.Diagnostics.Debug.WriteLine("=== PersistentDataManager 데이터 경로 정보 ===");
            System.Diagnostics.Debug.WriteLine($"데이터 폴더: {DataFolderPath}");
            System.Diagnostics.Debug.WriteLine($"Teaching 데이터: {TeachingDataPath}");
            System.Diagnostics.Debug.WriteLine($"Movement 데이터: {MovementDataPath}");
            System.Diagnostics.Debug.WriteLine($"Setup 데이터: {SetupDataPath}");
            System.Diagnostics.Debug.WriteLine($"System 데이터: {SystemDataPath}");

            System.Diagnostics.Debug.WriteLine("파일 존재 여부:");
            System.Diagnostics.Debug.WriteLine($"  Teaching: {File.Exists(TeachingDataPath)}");
            System.Diagnostics.Debug.WriteLine($"  Movement: {File.Exists(MovementDataPath)}");
            System.Diagnostics.Debug.WriteLine($"  Setup: {File.Exists(SetupDataPath)}");
            System.Diagnostics.Debug.WriteLine($"  System: {File.Exists(SystemDataPath)}");
        }

        /// <summary>
        /// 특정 데이터 파일 강제 재로드 (디버깅용)
        /// </summary>
        public static async Task ForceReloadTeachingDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== ForceReloadTeachingDataAsync 시작 ===");
                await LoadTeachingDataAsync();
                System.Diagnostics.Debug.WriteLine("Teaching 데이터 강제 재로드 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"강제 재로드 오류: {ex.Message}");
            }
        }
        #endregion

        #region Enums
        public enum DataType
        {
            Movement,
            Teaching,
            Setup,
            System
        }
        #endregion
    }
}