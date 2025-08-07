using System;
using System.Collections.Generic;

namespace TeachingPendant.Manager
{
    /// <summary>
    /// Teaching UI와 Movement UI 간 데이터 공유를 담당하는 중앙 관리 클래스
    /// </summary>
    public static class SharedDataManager
    {
        #region Fields
        // 그룹별 Cassette 데이터 저장소
        private static Dictionary<string, Dictionary<int, CassetteStageData>> _cassetteData
            = new Dictionary<string, Dictionary<int, CassetteStageData>>();

        // 그룹별 Stage 데이터 저장소
        private static Dictionary<string, Dictionary<int, CassetteStageData>> _stageData
            = new Dictionary<string, Dictionary<int, CassetteStageData>>();
        #endregion

        #region Events
        /// <summary>
        /// Cassette 데이터가 업데이트될 때 발생하는 이벤트
        /// </summary>
        public static event EventHandler<DataUpdatedEventArgs> CassetteDataUpdated;

        /// <summary>
        /// Stage 데이터가 업데이트될 때 발생하는 이벤트
        /// </summary>
        public static event EventHandler<DataUpdatedEventArgs> StageDataUpdated;
        #endregion

        #region Data Classes
        /// <summary>
        /// Cassette/Stage 데이터를 저장하는 공통 클래스
        /// </summary>
        public class CassetteStageData
        {
            public decimal PositionA { get; set; } = 0.00m;
            public decimal PositionT { get; set; } = 0.00m;
            public decimal PositionZ { get; set; } = 0.00m;
            public int SlotCount { get; set; } = 1;
            public int Pitch { get; set; } = 1;
            public int PickOffset { get; set; } = 1;
            public int PickDown { get; set; } = 1;
            public int PickUp { get; set; } = 1;
            public int PlaceDown { get; set; } = 1;
            public int PlaceUp { get; set; } = 1;

            public CassetteStageData() { }

            public CassetteStageData(decimal posA, decimal posT, decimal posZ, int slotCount, int pitch)
            {
                PositionA = posA;
                PositionT = posT;
                PositionZ = posZ;
                SlotCount = slotCount;
                Pitch = pitch;
            }
        }

        /// <summary>
        /// 데이터 업데이트 이벤트 인자
        /// </summary>
        public class DataUpdatedEventArgs : EventArgs
        {
            public string GroupName { get; }
            public int ItemNumber { get; }
            public CassetteStageData Data { get; }

            public DataUpdatedEventArgs(string groupName, int itemNumber, CassetteStageData data)
            {
                GroupName = groupName;
                ItemNumber = itemNumber;
                Data = data;
            }
        }
        #endregion

        #region Public Methods - Cassette Management
        /// <summary>
        /// Cassette 데이터 업데이트
        /// </summary>
        public static void UpdateCassetteData(string groupName, int cassetteNumber,
            decimal posA, decimal posT, decimal posZ, int slotCount, int pitch)
        {
            if (string.IsNullOrEmpty(groupName) || cassetteNumber <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"SharedDataManager: Invalid cassette update parameters - Group: {groupName}, Number: {cassetteNumber}");
                return;
            }

            // 그룹 데이터 초기화 (필요시)
            if (!_cassetteData.ContainsKey(groupName))
            {
                _cassetteData[groupName] = new Dictionary<int, CassetteStageData>();
            }

            // 데이터 생성 또는 업데이트
            if (!_cassetteData[groupName].ContainsKey(cassetteNumber))
            {
                _cassetteData[groupName][cassetteNumber] = new CassetteStageData();
            }

            var data = _cassetteData[groupName][cassetteNumber];
            data.PositionA = posA;
            data.PositionT = posT;
            data.PositionZ = posZ;
            data.SlotCount = slotCount;
            data.Pitch = pitch;

            System.Diagnostics.Debug.WriteLine($"SharedDataManager: Cassette data updated - {groupName} Cassette {cassetteNumber}: A={posA}, T={posT}, Z={posZ}, Slots={slotCount}");

            // 이벤트 발생
            CassetteDataUpdated?.Invoke(null, new DataUpdatedEventArgs(groupName, cassetteNumber, data));
        }

        /// <summary>
        /// Cassette 데이터 조회
        /// </summary>
        public static CassetteStageData GetCassetteData(string groupName, int cassetteNumber)
        {
            if (string.IsNullOrEmpty(groupName) || cassetteNumber <= 0)
            {
                return null;
            }

            if (_cassetteData.ContainsKey(groupName) &&
                _cassetteData[groupName].ContainsKey(cassetteNumber))
            {
                return _cassetteData[groupName][cassetteNumber];
            }

            // 데이터가 없으면 기본값으로 새로 생성
            var defaultData = new CassetteStageData();
            UpdateCassetteData(groupName, cassetteNumber,
                defaultData.PositionA, defaultData.PositionT, defaultData.PositionZ,
                defaultData.SlotCount, defaultData.Pitch);

            return defaultData;
        }
        #endregion

        #region Public Methods - Stage Management
        /// <summary>
        /// Stage 데이터 업데이트
        /// </summary>
        public static void UpdateStageData(string groupName, int stageNumber,
            decimal posA, decimal posT, decimal posZ, int slotCount, int pitch)
        {
            if (string.IsNullOrEmpty(groupName) || stageNumber <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"SharedDataManager: Invalid stage update parameters - Group: {groupName}, Number: {stageNumber}");
                return;
            }

            // 그룹 데이터 초기화 (필요시)
            if (!_stageData.ContainsKey(groupName))
            {
                _stageData[groupName] = new Dictionary<int, CassetteStageData>();
            }

            // 데이터 생성 또는 업데이트
            if (!_stageData[groupName].ContainsKey(stageNumber))
            {
                _stageData[groupName][stageNumber] = new CassetteStageData();
            }

            var data = _stageData[groupName][stageNumber];
            data.PositionA = posA;
            data.PositionT = posT;
            data.PositionZ = posZ;
            data.SlotCount = slotCount;
            data.Pitch = pitch;

            System.Diagnostics.Debug.WriteLine($"SharedDataManager: Stage data updated - {groupName} Stage {stageNumber}: A={posA}, T={posT}, Z={posZ}, Slots={slotCount}");

            // 이벤트 발생
            StageDataUpdated?.Invoke(null, new DataUpdatedEventArgs(groupName, stageNumber, data));
        }

        /// <summary>
        /// Stage 데이터 조회
        /// </summary>
        public static CassetteStageData GetStageData(string groupName, int stageNumber)
        {
            if (string.IsNullOrEmpty(groupName) || stageNumber <= 0)
            {
                return null;
            }

            if (_stageData.ContainsKey(groupName) &&
                _stageData[groupName].ContainsKey(stageNumber))
            {
                return _stageData[groupName][stageNumber];
            }

            // 데이터가 없으면 기본값으로 새로 생성
            var defaultData = new CassetteStageData();
            UpdateStageData(groupName, stageNumber,
                defaultData.PositionA, defaultData.PositionT, defaultData.PositionZ,
                defaultData.SlotCount, defaultData.Pitch);

            return defaultData;
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// 특정 그룹의 모든 데이터 정보를 디버그 출력
        /// </summary>
        public static void ShowDebugInfo(string groupName)
        {
            System.Diagnostics.Debug.WriteLine($"=== SharedDataManager Debug Info for {groupName} ===");

            // Cassette 데이터 출력
            if (_cassetteData.ContainsKey(groupName))
            {
                System.Diagnostics.Debug.WriteLine($"Cassettes in {groupName}:");
                foreach (var cassette in _cassetteData[groupName])
                {
                    var data = cassette.Value;
                    System.Diagnostics.Debug.WriteLine($"  Cassette {cassette.Key}: A={data.PositionA}, T={data.PositionT}, Z={data.PositionZ}, Slots={data.SlotCount}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"No cassette data found for {groupName}");
            }

            // Stage 데이터 출력
            if (_stageData.ContainsKey(groupName))
            {
                System.Diagnostics.Debug.WriteLine($"Stages in {groupName}:");
                foreach (var stage in _stageData[groupName])
                {
                    var data = stage.Value;
                    System.Diagnostics.Debug.WriteLine($"  Stage {stage.Key}: A={data.PositionA}, T={data.PositionT}, Z={data.PositionZ}, Slots={data.SlotCount}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"No stage data found for {groupName}");
            }
        }

        /// <summary>
        /// 모든 데이터 초기화 (테스트 목적)
        /// </summary>
        public static void ClearAllData()
        {
            _cassetteData.Clear();
            _stageData.Clear();
            System.Diagnostics.Debug.WriteLine("SharedDataManager: All data cleared");
        }

        /// <summary>
        /// 현재 저장된 그룹 목록 반환
        /// </summary>
        public static List<string> GetAvailableGroups()
        {
            var groups = new HashSet<string>();

            foreach (var group in _cassetteData.Keys)
            {
                groups.Add(group);
            }

            foreach (var group in _stageData.Keys)
            {
                groups.Add(group);
            }

            return new List<string>(groups);
        }
        #endregion
    }
}
