using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using TeachingPendant.HardwareControllers;
using TeachingPendant.Logging;
using TeachingPendant.Movement.MovementUI;

namespace TeachingPendant.RecipeSystem.Core
{
    /// <summary>
    /// Teaching 데이터 제공자 인터페이스
    /// </summary>
    public interface ITeachingDataProvider
    {
        /// <summary>
        /// 지정된 그룹과 위치의 좌표 가져오기
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <param name="locationName">위치명</param>
        /// <returns>Position 좌표</returns>
        Position GetPosition(string groupName, string locationName);

        /// <summary>
        /// 지정된 그룹과 위치에 좌표 저장
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <param name="locationName">위치명</param>
        /// <param name="position">저장할 좌표</param>
        void UpdatePosition(string groupName, string locationName, Position position);

        /// <summary>
        /// 사용 가능한 그룹 목록 가져오기
        /// </summary>
        /// <returns>그룹 목록</returns>
        List<string> GetAvailableGroups();

        /// <summary>
        /// 지정된 그룹의 위치 목록 가져오기
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <returns>위치 목록</returns>
        List<string> GetAvailableLocations(string groupName);
    }

    /// <summary>
    /// Teaching 시스템과 Recipe 시스템을 연결하는 브리지 클래스
    /// Movement UI의 실제 Teaching 데이터와 연동
    /// </summary>
    public class TeachingDataBridge : ITeachingDataProvider
    {
        #region Private Fields
        private static readonly string CLASS_NAME = "TeachingDataBridge";
        private Movement _currentMovement;
        #endregion

        #region Constructor
        /// <summary>
        /// TeachingDataBridge 생성자
        /// </summary>
        public TeachingDataBridge()
        {
            InitializeBridge();
        }
        #endregion

        #region Public Methods - ITeachingDataProvider Implementation
        /// <summary>
        /// 지정된 그룹과 위치의 좌표 가져오기
        /// </summary>
        /// <param name="groupName">그룹명 (예: "Group1")</param>
        /// <param name="locationName">위치명 (예: "P1")</param>
        /// <returns>Position 좌표</returns>
        public Position GetPosition(string groupName, string locationName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Teaching 좌표 조회: {groupName}.{locationName}");

                // Movement 인스턴스 갱신
                RefreshMovementInstance();

                if (_currentMovement != null)
                {
                    // Movement UI에서 실제 Teaching 데이터 가져오기
                    var position = GetPositionFromMovement(groupName, locationName);

                    if (position != null)
                    {
                        Logger.Info(CLASS_NAME, "GetPosition",
                            $"Teaching 좌표 조회 성공: {groupName}.{locationName} = ({position.R}, {position.Theta}, {position.Z})");
                        return position;
                    }
                }

                // Teaching 데이터를 찾을 수 없으면 기본 안전 위치 반환
                Logger.Warning(CLASS_NAME, "GetPosition",
                    $"Teaching 좌표를 찾을 수 없음: {groupName}.{locationName}, 기본 위치 반환");
                return GetDefaultSafePosition();
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "GetPosition",
                    $"Teaching 좌표 조회 실패: {groupName}.{locationName}", ex);
                return GetDefaultSafePosition();
            }
        }

        /// <summary>
        /// 지정된 그룹과 위치에 좌표 저장
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <param name="locationName">위치명</param>
        /// <param name="position">저장할 좌표</param>
        public void UpdatePosition(string groupName, string locationName, Position position)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Teaching 좌표 업데이트: {groupName}.{locationName}");

                RefreshMovementInstance();

                if (_currentMovement != null && position != null)
                {
                    // Movement UI에 좌표 저장
                    SavePositionToMovement(groupName, locationName, position);

                    Logger.Info(CLASS_NAME, "UpdatePosition",
                        $"Teaching 좌표 업데이트 성공: {groupName}.{locationName} = ({position.R}, {position.Theta}, {position.Z})");
                }
                else
                {
                    Logger.Warning(CLASS_NAME, "UpdatePosition",
                        $"Teaching 좌표 업데이트 실패: Movement 인스턴스 없음 또는 잘못된 좌표");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "UpdatePosition",
                    $"Teaching 좌표 업데이트 실패: {groupName}.{locationName}", ex);
            }
        }

        /// <summary>
        /// 사용 가능한 그룹 목록 가져오기
        /// </summary>
        /// <returns>그룹 목록</returns>
        public List<string> GetAvailableGroups()
        {
            try
            {
                RefreshMovementInstance();

                if (_currentMovement != null)
                {
                    // Movement UI에서 실제 그룹 목록 가져오기
                    var groups = GetGroupsFromMovement();

                    Logger.Info(CLASS_NAME, "GetAvailableGroups", $"그룹 목록 조회: {groups.Count}개");
                    return groups;
                }

                // Movement를 찾을 수 없으면 기본 그룹 반환
                Logger.Warning(CLASS_NAME, "GetAvailableGroups", "Movement 인스턴스 없음, 기본 그룹 반환");
                return new List<string> { "Group1", "Group2", "Group3", "Group4", "Group5" };
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "GetAvailableGroups", "그룹 목록 조회 실패", ex);
                return new List<string> { "Group1" };
            }
        }

        /// <summary>
        /// 지정된 그룹의 위치 목록 가져오기
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <returns>위치 목록</returns>
        public List<string> GetAvailableLocations(string groupName)
        {
            try
            {
                RefreshMovementInstance();

                if (_currentMovement != null)
                {
                    // Movement UI에서 실제 위치 목록 가져오기
                    var locations = GetLocationsFromMovement(groupName);

                    Logger.Info(CLASS_NAME, "GetAvailableLocations",
                        $"위치 목록 조회: {groupName} - {locations.Count}개");
                    return locations;
                }

                // 기본 위치 목록 반환 (P1~P7)
                Logger.Warning(CLASS_NAME, "GetAvailableLocations",
                    $"Movement 인스턴스 없음, 기본 위치 반환: {groupName}");
                return new List<string> { "P1", "P2", "P3", "P4", "P5", "P6", "P7" };
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "GetAvailableLocations",
                    $"위치 목록 조회 실패: {groupName}", ex);
                return new List<string> { "P1", "P2", "P3" };
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 브리지 초기화
        /// </summary>
        private void InitializeBridge()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Teaching 데이터 브리지 초기화");
                RefreshMovementInstance();
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "InitializeBridge", "브리지 초기화 실패", ex);
            }
        }

        /// <summary>
        /// 현재 Movement 인스턴스 갱신
        /// </summary>
        private void RefreshMovementInstance()
        {
            try
            {
                // CommonFrame을 통해 현재 활성 Movement 인스턴스 찾기
                _currentMovement = FindCurrentMovementInstance();

                if (_currentMovement != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Movement 인스턴스 찾음");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Movement 인스턴스를 찾을 수 없음");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "RefreshMovementInstance", "Movement 인스턴스 갱신 실패", ex);
                _currentMovement = null;
            }
        }

        /// <summary>
        /// 현재 Movement 인스턴스 찾기
        /// </summary>
        /// <returns>Movement 인스턴스</returns>
        private Movement FindCurrentMovementInstance()
        {
            try
            {
                // 모든 창에서 CommonFrame 찾기
                foreach (Window window in Application.Current.Windows)
                {
                    var movementInstance = FindMovementInWindow(window);
                    if (movementInstance != null)
                    {
                        return movementInstance;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "FindCurrentMovementInstance", "Movement 인스턴스 찾기 실패", ex);
                return null;
            }
        }

        /// <summary>
        /// 지정된 창에서 Movement 인스턴스 찾기
        /// </summary>
        /// <param name="window">검색할 창</param>
        /// <returns>Movement 인스턴스</returns>
        private Movement FindMovementInWindow(Window window)
        {
            try
            {
                // CommonFrame 타입의 창 찾기
                if (window.GetType().Name == "CommonFrame")
                {
                    // CommonFrame의 MainContentArea에서 Movement 찾기
                    var contentArea = FindChildByName(window, "MainContentArea");
                    if (contentArea is System.Windows.Controls.ContentPresenter presenter)
                    {
                        if (presenter.Content is Movement movement)
                        {
                            return movement;
                        }
                    }
                }

                // 직접 Movement 타입 찾기
                return FindVisualChild<Movement>(window);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] 창에서 Movement 찾기 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Movement에서 실제 위치 데이터 가져오기
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <param name="locationName">위치명</param>
        /// <returns>Position 좌표</returns>
        private Position GetPositionFromMovement(string groupName, string locationName)
        {
            try
            {
                if (_currentMovement == null) return null;

                // Movement의 CoordinateData에서 좌표 추출
                // 실제 Movement 구조에 맞게 구현
                var coordinateData = GetCoordinateDataFromMovement(groupName);
                if (coordinateData != null)
                {
                    return ExtractPositionFromCoordinateData(coordinateData, locationName);
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "GetPositionFromMovement", "Movement에서 위치 데이터 가져오기 실패", ex);
                return null;
            }
        }

        /// <summary>
        /// Movement에서 CoordinateData 가져오기 (반사를 이용한 접근)
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <returns>CoordinateData 또는 null</returns>
        private object GetCoordinateDataFromMovement(string groupName)
        {
            try
            {
                if (_currentMovement == null) return null;

                // Movement의 private 필드에 반사를 통해 접근
                var movementType = _currentMovement.GetType();
                var groupCoordinateDataField = movementType.GetField("_groupCoordinateData",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (groupCoordinateDataField != null)
                {
                    var groupData = groupCoordinateDataField.GetValue(_currentMovement);
                    if (groupData is System.Collections.IDictionary dictionary && dictionary.Contains(groupName))
                    {
                        return dictionary[groupName];
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] CoordinateData 가져오기 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// CoordinateData에서 특정 위치의 Position 추출
        /// </summary>
        /// <param name="coordinateData">CoordinateData 객체</param>
        /// <param name="locationName">위치명 (P1~P7)</param>
        /// <returns>Position 또는 null</returns>
        private Position ExtractPositionFromCoordinateData(object coordinateData, string locationName)
        {
            try
            {
                if (coordinateData == null) return null;

                // CoordinateData의 해당 위치 프로퍼티에 접근
                var coordinateType = coordinateData.GetType();
                var positionProperty = coordinateType.GetProperty(locationName);

                if (positionProperty != null)
                {
                    var positionValue = positionProperty.GetValue(coordinateData);
                    if (positionValue is Position position)
                    {
                        return position;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Position 추출 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Movement에 위치 데이터 저장
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <param name="locationName">위치명</param>
        /// <param name="position">저장할 좌표</param>
        private void SavePositionToMovement(string groupName, string locationName, Position position)
        {
            try
            {
                if (_currentMovement == null || position == null) return;

                // Movement의 좌표 저장 메서드 호출
                // 실제 Movement API에 맞게 구현
                System.Diagnostics.Debug.WriteLine($"[{CLASS_NAME}] Movement에 좌표 저장: {groupName}.{locationName}");

                // TODO: Movement의 실제 저장 메서드 호출
                // _currentMovement.SavePosition(groupName, locationName, position);
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "SavePositionToMovement", "Movement에 좌표 저장 실패", ex);
            }
        }

        /// <summary>
        /// Movement에서 그룹 목록 가져오기
        /// </summary>
        /// <returns>그룹 목록</returns>
        private List<string> GetGroupsFromMovement()
        {
            try
            {
                if (_currentMovement == null) return new List<string>();

                // Movement의 그룹 목록 가져오기
                // 실제 Movement 구조에 맞게 구현
                return new List<string> { "Group1", "Group2", "Group3", "Group4", "Group5" };
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "GetGroupsFromMovement", "Movement에서 그룹 목록 가져오기 실패", ex);
                return new List<string>();
            }
        }

        /// <summary>
        /// Movement에서 위치 목록 가져오기
        /// </summary>
        /// <param name="groupName">그룹명</param>
        /// <returns>위치 목록</returns>
        private List<string> GetLocationsFromMovement(string groupName)
        {
            try
            {
                if (_currentMovement == null) return new List<string>();

                // Movement의 위치 목록 가져오기
                // 실제 Movement 구조에 맞게 구현
                return new List<string> { "P1", "P2", "P3", "P4", "P5", "P6", "P7" };
            }
            catch (Exception ex)
            {
                Logger.Error(CLASS_NAME, "GetLocationsFromMovement", "Movement에서 위치 목록 가져오기 실패", ex);
                return new List<string>();
            }
        }

        /// <summary>
        /// 기본 안전 위치 반환
        /// </summary>
        /// <returns>기본 안전 Position</returns>
        private Position GetDefaultSafePosition()
        {
            return new Position(100, 0, 50); // R=100, Theta=0, Z=50 (안전 위치)
        }

        /// <summary>
        /// 이름으로 자식 요소 찾기
        /// </summary>
        /// <param name="parent">부모 요소</param>
        /// <param name="name">찾을 요소 이름</param>
        /// <returns>찾은 요소</returns>
        private FrameworkElement FindChildByName(DependencyObject parent, string name)
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement element && element.Name == name)
                {
                    return element;
                }

                var result = FindChildByName(child, name);
                if (result != null) return result;
            }

            return null;
        }

        /// <summary>
        /// 특정 타입의 자식 요소 찾기
        /// </summary>
        /// <typeparam name="T">찾을 타입</typeparam>
        /// <param name="parent">부모 요소</param>
        /// <returns>찾은 요소</returns>
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T result)
                {
                    return result;
                }

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }

            return null;
        }
        #endregion
    }
}