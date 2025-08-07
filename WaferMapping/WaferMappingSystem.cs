using System;
using System.Collections.Generic;
using System.Linq;
using TeachingPendant.Alarm;

namespace TeachingPendant.WaferMapping
{
    /// <summary>
    /// 웨이퍼 상태 열거형
    /// </summary>
    public enum WaferStatus
    {
        Empty,      // 빈 슬롯
        Present,    // 정상 웨이퍼 존재
        Crossed,    // 웨이퍼가 기울어짐 (크로스)
        Double,     // 2개 이상의 웨이퍼가 겹쳐짐
        Unknown     // 매핑되지 않음 (초기 상태)
    }

    /// <summary>
    /// 웨이퍼 슬롯 정보 클래스
    /// </summary>
    public class WaferSlotInfo
    {
        public int SlotNumber { get; set; }
        public WaferStatus Status { get; set; } = WaferStatus.Unknown;
        public double Thickness { get; set; } = 0.0; // μm 단위
        public string WaferId { get; set; } = string.Empty;
        public DateTime LastChecked { get; set; } = DateTime.MinValue;
        public bool IsTransferTarget { get; set; } = false; // 이송 대상인지

        /// <summary>
        /// 슬롯이 비어있는지 확인
        /// </summary>
        public bool IsEmpty => Status == WaferStatus.Empty;

        /// <summary>
        /// 슬롯에 정상 웨이퍼가 있는지 확인
        /// </summary>
        public bool HasValidWafer => Status == WaferStatus.Present;

        /// <summary>
        /// 슬롯에 문제가 있는지 확인
        /// </summary>
        public bool HasError => Status == WaferStatus.Crossed || Status == WaferStatus.Double;
    }

    /// <summary>
    /// 카세트 매핑 정보 클래스
    /// </summary>
    public class CassetteMapping
    {
        public int CassetteId { get; set; }
        public string CassetteName { get; set; } = string.Empty;
        public int TotalSlots { get; set; } = 25; // 기본 25슬롯
        public Dictionary<int, WaferSlotInfo> Slots { get; set; } = new Dictionary<int, WaferSlotInfo>();
        public DateTime LastMappingTime { get; set; } = DateTime.MinValue;
        public bool IsMappingCompleted { get; set; } = false;
        public string GroupName { get; set; } = "Group1";

        /// <summary>
        /// 빈 슬롯 개수
        /// </summary>
        public int EmptySlotCount => Slots.Values.Count(s => s.IsEmpty);

        /// <summary>
        /// 웨이퍼가 있는 슬롯 개수
        /// </summary>
        public int OccupiedSlotCount => Slots.Values.Count(s => s.HasValidWafer);

        /// <summary>
        /// 에러가 있는 슬롯 개수
        /// </summary>
        public int ErrorSlotCount => Slots.Values.Count(s => s.HasError);

        /// <summary>
        /// 매핑되지 않은 슬롯 개수
        /// </summary>
        public int UnknownSlotCount => Slots.Values.Count(s => s.Status == WaferStatus.Unknown);
    }

    /// <summary>
    /// 웨이퍼 매핑 시스템 메인 클래스
    /// </summary>
    public static class WaferMappingSystem
    {
        #region Fields
        private static Dictionary<string, Dictionary<int, CassetteMapping>> _groupCassetteMappings =
            new Dictionary<string, Dictionary<int, CassetteMapping>>();

        private static readonly object _lockObject = new object();
        #endregion

        #region Events
        /// <summary>
        /// 매핑 완료 시 발생하는 이벤트
        /// </summary>
        public static event EventHandler<MappingCompletedEventArgs> MappingCompleted;

        /// <summary>
        /// 웨이퍼 상태 변경 시 발생하는 이벤트
        /// </summary>
        public static event EventHandler<WaferStatusChangedEventArgs> WaferStatusChanged;
        #endregion

        #region Public Methods
        /// <summary>
        /// 카세트 매핑 초기화
        /// </summary>
        public static void InitializeCassetteMapping(string groupName, int cassetteId, int totalSlots = 25)
        {
            lock (_lockObject)
            {
                if (!_groupCassetteMappings.ContainsKey(groupName))
                {
                    _groupCassetteMappings[groupName] = new Dictionary<int, CassetteMapping>();
                }

                var cassetteMapping = new CassetteMapping
                {
                    CassetteId = cassetteId,
                    CassetteName = $"Cassette{cassetteId}",
                    TotalSlots = totalSlots,
                    GroupName = groupName,
                    LastMappingTime = DateTime.Now
                };

                // 모든 슬롯을 Unknown 상태로 초기화
                for (int slot = 1; slot <= totalSlots; slot++)
                {
                    cassetteMapping.Slots[slot] = new WaferSlotInfo
                    {
                        SlotNumber = slot,
                        Status = WaferStatus.Unknown
                    };
                }

                _groupCassetteMappings[groupName][cassetteId] = cassetteMapping;

                System.Diagnostics.Debug.WriteLine($"Wafer mapping initialized: {groupName} Cassette{cassetteId} ({totalSlots} slots)");
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_INITIALIZED,
                    $"Cassette mapping initialized: {groupName} Cassette{cassetteId}");
            }
        }

        /// <summary>
        /// 카세트 매핑 실행 (시뮬레이션)
        /// </summary>
        public static bool StartMapping(string groupName, int cassetteId)
        {
            lock (_lockObject)
            {
                var mapping = GetCassetteMapping(groupName, cassetteId);
                if (mapping == null)
                {
                    AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_ERROR,
                        $"Cassette mapping not found: {groupName} Cassette{cassetteId}");
                    return false;
                }

                // 이미 매핑이 완료된 경우 재매핑 여부 확인
                if (mapping.IsMappingCompleted)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Cassette {cassetteId} is already mapped.\n\nDo you want to re-map it?",
                        "Re-mapping Confirmation",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result == System.Windows.MessageBoxResult.No)
                    {
                        AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                            "Mapping cancelled by user");
                        return false;
                    }
                }

                // 매핑 진행률 표시 시뮬레이션
                AlarmMessageManager.ShowAlarm(Alarms.SYSTEM_INITIALIZED,
                    "Starting wafer mapping... Please wait");

                // 시뮬레이션: 실제처럼 시간이 걸리는 것처럼 보이게
                System.Threading.Thread.Sleep(500); // 0.5초 대기

                // 실제 매핑 시뮬레이션 수행
                PerformMappingSimulation(mapping);

                mapping.IsMappingCompleted = true;
                mapping.LastMappingTime = DateTime.Now;

                System.Diagnostics.Debug.WriteLine($"Mapping completed: {groupName} Cassette{cassetteId} - " +
                    $"Occupied: {mapping.OccupiedSlotCount}, Empty: {mapping.EmptySlotCount}, Error: {mapping.ErrorSlotCount}");

                // 이벤트 발생
                MappingCompleted?.Invoke(null, new MappingCompletedEventArgs
                {
                    GroupName = groupName,
                    CassetteId = cassetteId,
                    TotalSlots = mapping.TotalSlots,
                    OccupiedSlots = mapping.OccupiedSlotCount,
                    EmptySlots = mapping.EmptySlotCount,
                    ErrorSlots = mapping.ErrorSlotCount
                });

                AlarmMessageManager.ShowAlarm(Alarms.OPERATION_COMPLETED,
                    $"Mapping completed - Found {mapping.OccupiedSlotCount} wafers, {mapping.ErrorSlotCount} errors");

                return true;
            }
        }

        /// <summary>
        /// 실제 같은 매핑 시뮬레이션 수행
        /// </summary>
        private static void PerformMappingSimulation(CassetteMapping mapping)
        {
            // 시뮬레이션 시나리오 선택 (현실적인 패턴들)
            var scenarios = new[]
            {
                "FullLoad",     // 거의 가득 찬 카세트
                "HalfLoad",     // 절반 정도
                "LowLoad",      // 적게 들어있음
                "Production"    // 실제 생산 패턴
            };

            var random = new Random();
            string selectedScenario = scenarios[random.Next(scenarios.Length)];

            // 모든 슬롯을 빈 상태로 시작
            foreach (var slot in mapping.Slots.Values)
            {
                slot.Status = WaferStatus.Empty;
                slot.LastChecked = DateTime.Now;
                slot.WaferId = string.Empty;
                slot.Thickness = 0.0;
            }

            // 시나리오별 웨이퍼 배치
            int targetWaferCount = GetTargetWaferCount(selectedScenario, mapping.TotalSlots);
            PlaceWafersInPattern(mapping, targetWaferCount, selectedScenario);

            System.Diagnostics.Debug.WriteLine($"Applied mapping scenario: {selectedScenario} with {targetWaferCount} wafers");
        }

        /// <summary>
        /// 시나리오별 목표 웨이퍼 개수 결정
        /// </summary>
        private static int GetTargetWaferCount(string scenario, int totalSlots)
        {
            var random = new Random();

            switch (scenario)
            {
                case "FullLoad":
                    return random.Next(totalSlots - 3, totalSlots + 1); // 22~25개
                case "HalfLoad":
                    return random.Next(10, 16); // 10~15개
                case "LowLoad":
                    return random.Next(3, 8); // 3~7개
                case "Production":
                    return random.Next(15, 22); // 15~21개 (일반적인 생산 수준)
                default:
                    return random.Next(5, totalSlots - 2);
            }
        }

        /// <summary>
        /// 패턴에 따라 웨이퍼 배치
        /// </summary>
        private static void PlaceWafersInPattern(CassetteMapping mapping, int waferCount, string scenario)
        {
            var random = new Random();
            var availableSlots = Enumerable.Range(1, mapping.TotalSlots).ToList();

            // 시나리오별 배치 패턴
            switch (scenario)
            {
                case "FullLoad":
                    // 연속적으로 배치 (실제 로딩 패턴)
                    PlaceWafersSequentially(mapping, waferCount, availableSlots, random);
                    break;
                case "Production":
                    // 일부 빈 슬롯을 의도적으로 남김 (불량 제거 후)
                    PlaceWafersWithGaps(mapping, waferCount, availableSlots, random);
                    break;
                default:
                    // 랜덤 배치
                    PlaceWafersRandomly(mapping, waferCount, availableSlots, random);
                    break;
            }
        }

        /// <summary>
        /// 연속적으로 웨이퍼 배치
        /// </summary>
        private static void PlaceWafersSequentially(CassetteMapping mapping, int waferCount,
            List<int> availableSlots, Random random)
        {
            int startSlot = random.Next(1, mapping.TotalSlots - waferCount + 2);

            for (int i = 0; i < waferCount; i++)
            {
                int slotNumber = startSlot + i;
                if (slotNumber <= mapping.TotalSlots)
                {
                    PlaceWaferInSlot(mapping.Slots[slotNumber], slotNumber, random);
                }
            }
        }

        /// <summary>
        /// 의도적인 빈 공간과 함께 웨이퍼 배치
        /// </summary>
        private static void PlaceWafersWithGaps(CassetteMapping mapping, int waferCount,
            List<int> availableSlots, Random random)
        {
            // 3~5개 구간으로 나누어 배치
            int sections = random.Next(3, 6);
            int wafersPerSection = waferCount / sections;
            int remainingWafers = waferCount % sections;

            int currentSlot = 1;
            for (int section = 0; section < sections; section++)
            {
                int sectionWafers = wafersPerSection + (section < remainingWafers ? 1 : 0);

                // 각 섹션에 웨이퍼 배치
                for (int i = 0; i < sectionWafers && currentSlot <= mapping.TotalSlots; i++)
                {
                    PlaceWaferInSlot(mapping.Slots[currentSlot], currentSlot, random);
                    currentSlot++;
                }

                // 섹션 간 간격
                currentSlot += random.Next(1, 3);
            }
        }

        /// <summary>
        /// 랜덤하게 웨이퍼 배치
        /// </summary>
        private static void PlaceWafersRandomly(CassetteMapping mapping, int waferCount,
            List<int> availableSlots, Random random)
        {
            for (int i = 0; i < waferCount && availableSlots.Count > 0; i++)
            {
                int randomIndex = random.Next(availableSlots.Count);
                int slotNumber = availableSlots[randomIndex];
                availableSlots.RemoveAt(randomIndex);

                PlaceWaferInSlot(mapping.Slots[slotNumber], slotNumber, random);
            }
        }

        /// <summary>
        /// 특정 슬롯에 웨이퍼 배치
        /// </summary>
        private static void PlaceWaferInSlot(WaferSlotInfo slot, int slotNumber, Random random)
        {
            // 97% 확률로 정상, 2% 크로스, 1% 더블
            int statusRandom = random.Next(100);
            if (statusRandom < 97)
            {
                slot.Status = WaferStatus.Present;
                slot.Thickness = 775.0 + random.NextDouble() * 50.0; // 775~825μm
                slot.WaferId = $"W{DateTime.Now:yyyyMMdd}{slotNumber:D2}";
            }
            else if (statusRandom < 99)
            {
                slot.Status = WaferStatus.Crossed;
                slot.Thickness = 775.0;
                slot.WaferId = $"ERR{slotNumber:D2}";
            }
            else
            {
                slot.Status = WaferStatus.Double;
                slot.Thickness = 1550.0; // 2배 두께
                slot.WaferId = $"DBL{slotNumber:D2}";
            }

            slot.LastChecked = DateTime.Now;
        }

        /// <summary>
        /// 특정 슬롯의 웨이퍼 상태 변경
        /// </summary>
        public static bool SetWaferStatus(string groupName, int cassetteId, int slotNumber, WaferStatus status, string waferId = "")
        {
            lock (_lockObject)
            {
                var mapping = GetCassetteMapping(groupName, cassetteId);
                if (mapping == null || !mapping.Slots.ContainsKey(slotNumber))
                {
                    return false;
                }

                var slot = mapping.Slots[slotNumber];
                var oldStatus = slot.Status;

                slot.Status = status;
                slot.LastChecked = DateTime.Now;

                if (!string.IsNullOrEmpty(waferId))
                {
                    slot.WaferId = waferId;
                }

                // 이벤트 발생
                WaferStatusChanged?.Invoke(null, new WaferStatusChangedEventArgs
                {
                    GroupName = groupName,
                    CassetteId = cassetteId,
                    SlotNumber = slotNumber,
                    OldStatus = oldStatus,
                    NewStatus = status,
                    WaferId = waferId
                });

                System.Diagnostics.Debug.WriteLine($"Wafer status changed: {groupName} C{cassetteId} Slot{slotNumber} {oldStatus}→{status}");
                return true;
            }
        }

        /// <summary>
        /// 카세트 매핑 정보 조회
        /// </summary>
        public static CassetteMapping GetCassetteMapping(string groupName, int cassetteId)
        {
            lock (_lockObject)
            {
                if (_groupCassetteMappings.ContainsKey(groupName) &&
                    _groupCassetteMappings[groupName].ContainsKey(cassetteId))
                {
                    return _groupCassetteMappings[groupName][cassetteId];
                }
                return null;
            }
        }

        /// <summary>
        /// 그룹의 모든 카세트 매핑 조회
        /// </summary>
        public static Dictionary<int, CassetteMapping> GetGroupMappings(string groupName)
        {
            lock (_lockObject)
            {
                if (_groupCassetteMappings.ContainsKey(groupName))
                {
                    return new Dictionary<int, CassetteMapping>(_groupCassetteMappings[groupName]);
                }
                return new Dictionary<int, CassetteMapping>();
            }
        }

        /// <summary>
        /// 다음 이송 가능한 웨이퍼 슬롯 찾기
        /// </summary>
        public static WaferSlotInfo GetNextAvailableWafer(string groupName, int cassetteId)
        {
            var mapping = GetCassetteMapping(groupName, cassetteId);
            if (mapping == null) return null;

            // 정상 상태이면서 아직 이송되지 않은 웨이퍼 찾기
            return mapping.Slots.Values
                .Where(s => s.HasValidWafer && !s.IsTransferTarget)
                .OrderBy(s => s.SlotNumber)
                .FirstOrDefault();
        }

        /// <summary>
        /// 다음 빈 슬롯 찾기
        /// </summary>
        public static WaferSlotInfo GetNextEmptySlot(string groupName, int cassetteId)
        {
            var mapping = GetCassetteMapping(groupName, cassetteId);
            if (mapping == null) return null;

            return mapping.Slots.Values
                .Where(s => s.IsEmpty)
                .OrderBy(s => s.SlotNumber)
                .FirstOrDefault();
        }

        /// <summary>
        /// 매핑 상태 요약 정보 조회
        /// </summary>
        public static string GetMappingSummary(string groupName, int cassetteId)
        {
            var mapping = GetCassetteMapping(groupName, cassetteId);
            if (mapping == null)
            {
                return "Mapping not found";
            }

            if (!mapping.IsMappingCompleted)
            {
                return "Mapping not completed";
            }

            return $"Total: {mapping.TotalSlots} | Occupied: {mapping.OccupiedSlotCount} | " +
                   $"Empty: {mapping.EmptySlotCount} | Error: {mapping.ErrorSlotCount}";
        }

        /// <summary>
        /// 모든 매핑 데이터 초기화
        /// </summary>
        public static void ClearAllMappings()
        {
            lock (_lockObject)
            {
                _groupCassetteMappings.Clear();
                System.Diagnostics.Debug.WriteLine("All wafer mappings cleared");
                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION, "All wafer mappings cleared");
            }
        }
        #endregion

        /// <summary>
        /// 카세트 매핑 초기화 (다시 매핑 가능하게)
        /// </summary>
        public static bool ResetMapping(string groupName, int cassetteId)
        {
            lock (_lockObject)
            {
                var mapping = GetCassetteMapping(groupName, cassetteId);
                if (mapping == null) return false;

                // 모든 슬롯을 Unknown 상태로 리셋
                foreach (var slot in mapping.Slots.Values)
                {
                    slot.Status = WaferStatus.Unknown;
                    slot.WaferId = string.Empty;
                    slot.Thickness = 0.0;
                    slot.LastChecked = DateTime.MinValue;
                    slot.IsTransferTarget = false;
                }

                mapping.IsMappingCompleted = false;
                mapping.LastMappingTime = DateTime.MinValue;

                AlarmMessageManager.ShowAlarm(Alarms.USER_ACTION,
                    $"Mapping reset for {groupName} Cassette{cassetteId}");

                return true;
            }
        }

        /// <summary>
        /// 웨이퍼 이송 후 상태 업데이트
        /// </summary>
        public static bool TransferWafer(string sourceGroup, int sourceCassette, int sourceSlot,
            string destGroup, int destCassette, int destSlot)
        {
            lock (_lockObject)
            {
                var sourceMapping = GetCassetteMapping(sourceGroup, sourceCassette);
                var destMapping = GetCassetteMapping(destGroup, destCassette);

                if (sourceMapping == null || destMapping == null) return false;
                if (!sourceMapping.Slots.ContainsKey(sourceSlot) || !destMapping.Slots.ContainsKey(destSlot)) return false;

                var sourceSlotInfo = sourceMapping.Slots[sourceSlot];
                var destSlotInfo = destMapping.Slots[destSlot];

                // 소스에 웨이퍼가 있고, 목적지가 비어있는지 확인
                if (!sourceSlotInfo.HasValidWafer || !destSlotInfo.IsEmpty)
                {
                    return false;
                }

                // 웨이퍼 이송
                destSlotInfo.Status = sourceSlotInfo.Status;
                destSlotInfo.WaferId = sourceSlotInfo.WaferId;
                destSlotInfo.Thickness = sourceSlotInfo.Thickness;
                destSlotInfo.LastChecked = DateTime.Now;

                // 소스 슬롯 비우기
                sourceSlotInfo.Status = WaferStatus.Empty;
                sourceSlotInfo.WaferId = string.Empty;
                sourceSlotInfo.Thickness = 0.0;
                sourceSlotInfo.LastChecked = DateTime.Now;
                sourceSlotInfo.IsTransferTarget = false;

                System.Diagnostics.Debug.WriteLine($"Wafer transferred: {sourceGroup} C{sourceCassette} S{sourceSlot} → {destGroup} C{destCassette} S{destSlot}");
                return true;
            }
        }

        /// <summary>
        /// 매핑 통계 정보 조회
        /// </summary>
        public static MappingStatistics GetMappingStatistics(string groupName)
        {
            lock (_lockObject)
            {
                var stats = new MappingStatistics();

                if (!_groupCassetteMappings.ContainsKey(groupName))
                    return stats;

                foreach (var cassette in _groupCassetteMappings[groupName].Values)
                {
                    stats.TotalCassettes++;
                    if (cassette.IsMappingCompleted)
                    {
                        stats.MappedCassettes++;
                        stats.TotalWafers += cassette.OccupiedSlotCount;
                        stats.TotalEmptySlots += cassette.EmptySlotCount;
                        stats.TotalErrorWafers += cassette.ErrorSlotCount;
                    }
                }

                return stats;
            }
        }

        /// <summary>
        /// 매핑 통계 정보 클래스
        /// </summary>
        public class MappingStatistics
        {
            public int TotalCassettes { get; set; } = 0;
            public int MappedCassettes { get; set; } = 0;
            public int TotalWafers { get; set; } = 0;
            public int TotalEmptySlots { get; set; } = 0;
            public int TotalErrorWafers { get; set; } = 0;

            public double MappingCompletionRate => TotalCassettes > 0 ? (double)MappedCassettes / TotalCassettes * 100 : 0;
        }

    }

    #region Event Args Classes
    /// <summary>
    /// 매핑 완료 이벤트 인자
    /// </summary>
    public class MappingCompletedEventArgs : EventArgs
    {
        public string GroupName { get; set; }
        public int CassetteId { get; set; }
        public int TotalSlots { get; set; }
        public int OccupiedSlots { get; set; }
        public int EmptySlots { get; set; }
        public int ErrorSlots { get; set; }
    }

    /// <summary>
    /// 웨이퍼 상태 변경 이벤트 인자
    /// </summary>
    public class WaferStatusChangedEventArgs : EventArgs
    {
        public string GroupName { get; set; }
        public int CassetteId { get; set; }
        public int SlotNumber { get; set; }
        public WaferStatus OldStatus { get; set; }
        public WaferStatus NewStatus { get; set; }
        public string WaferId { get; set; }
    }
    #endregion
}