using System;
using System.Threading.Tasks;

namespace TeachingPendant.HardwareControllers
{
    /// <summary>
    /// 로봇 컨트롤러 공통 인터페이스
    /// 시뮬레이션(VirtualRobotController)과 실제 하드웨어(EtherCATRobotController) 모두 지원
    /// </summary>
    public interface IRobotController
    {
        #region Connection Management
        /// <summary>
        /// 로봇 컨트롤러 연결
        /// </summary>
        /// <returns>연결 성공 여부</returns>
        Task<bool> ConnectAsync();

        /// <summary>
        /// 로봇 컨트롤러 연결 해제
        /// </summary>
        /// <returns>연결 해제 성공 여부</returns>
        Task<bool> DisconnectAsync();

        /// <summary>
        /// 현재 연결 상태
        /// </summary>
        bool IsConnected { get; }
        #endregion

        #region Movement Commands
        /// <summary>
        /// 지정된 좌표로 이동 (R, θ, Z 좌표계)
        /// </summary>
        /// <param name="r">반지름 좌표 (mm)</param>
        /// <param name="theta">각도 좌표 (degree)</param>
        /// <param name="z">높이 좌표 (mm)</param>
        /// <returns>이동 성공 여부</returns>
        Task<bool> MoveToAsync(double r, double theta, double z);

        /// <summary>
        /// 홈 위치로 이동
        /// </summary>
        /// <returns>홈 이동 성공 여부</returns>
        Task<bool> HomeAsync();

        /// <summary>
        /// 로봇 정지
        /// </summary>
        /// <returns>정지 성공 여부</returns>
        Task<bool> StopAsync();
        #endregion

        #region Pick & Place Operations
        /// <summary>
        /// 웨이퍼 집기 동작
        /// </summary>
        /// <returns>Pick 동작 성공 여부</returns>
        Task<bool> PickAsync();

        /// <summary>
        /// 웨이퍼 놓기 동작
        /// </summary>
        /// <returns>Place 동작 성공 여부</returns>
        Task<bool> PlaceAsync();

        /// <summary>
        /// 진공 ON/OFF 제어
        /// </summary>
        /// <param name="isOn">진공 ON/OFF</param>
        /// <returns>진공 제어 성공 여부</returns>
        Task<bool> SetVacuumAsync(bool isOn);
        #endregion

        #region Status & Information
        /// <summary>
        /// 로봇 상태 조회
        /// </summary>
        /// <returns>현재 로봇 상태</returns>
        Task<RobotStatus> GetStatusAsync();

        /// <summary>
        /// 현재 위치 조회
        /// </summary>
        /// <returns>현재 위치 정보</returns>
        Task<Position> GetCurrentPositionAsync();

        /// <summary>
        /// 로봇이 현재 이동 중인지 확인
        /// </summary>
        /// <returns>이동 중 여부</returns>
        bool IsMoving { get; }
        #endregion

        #region Events
        /// <summary>
        /// 로봇 상태 변경 이벤트
        /// </summary>
        event EventHandler<RobotStatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// 위치 변경 이벤트
        /// </summary>
        event EventHandler<PositionChangedEventArgs> PositionChanged;

        /// <summary>
        /// 오류 발생 이벤트
        /// </summary>
        event EventHandler<RobotErrorEventArgs> ErrorOccurred;
        #endregion

        #region Safety & Configuration
        /// <summary>
        /// 안전 상태 확인
        /// </summary>
        /// <returns>안전 상태 여부</returns>
        bool IsSafeToOperate();

        /// <summary>
        /// 속도 설정
        /// </summary>
        /// <param name="speedPercent">속도 퍼센트 (1-100)</param>
        /// <returns>속도 설정 성공 여부</returns>
        Task<bool> SetSpeedAsync(int speedPercent);

        /// <summary>
        /// 현재 속도 조회
        /// </summary>
        /// <returns>현재 속도 퍼센트</returns>
        int CurrentSpeed { get; }
        #endregion
    }

    #region Data Classes
    /// <summary>
    /// 3D 위치 정보 클래스
    /// </summary>
    public class Position
    {
        public double R { get; set; }      // 반지름 좌표 (mm)
        public double Theta { get; set; }  // 각도 좌표 (degree)
        public double Z { get; set; }      // 높이 좌표 (mm)

        public Position()
        {
            R = 0;
            Theta = 0;
            Z = 0;
        }

        public Position(double r, double theta, double z)
        {
            R = r;
            Theta = theta;
            Z = z;
        }

        public override string ToString()
        {
            return $"R:{R:F2}, θ:{Theta:F1}°, Z:{Z:F2}";
        }
    }

    /// <summary>
    /// 로봇 상태 정보 클래스
    /// </summary>
    public class RobotStatus
    {
        public bool IsConnected { get; set; }           // 연결 상태
        public bool IsMoving { get; set; }              // 이동 중 여부
        public bool IsHomed { get; set; }               // 홈 완료 여부
        public bool VacuumOn { get; set; }              // 진공 상태
        public Position CurrentPosition { get; set; }   // 현재 위치
        public int CurrentSpeed { get; set; }           // 현재 속도 (%)
        public string LastError { get; set; }           // 마지막 오류 메시지
        public DateTime LastUpdateTime { get; set; }    // 마지막 업데이트 시간

        public RobotStatus()
        {
            IsConnected = false;
            IsMoving = false;
            IsHomed = false;
            VacuumOn = false;
            CurrentPosition = new Position();
            CurrentSpeed = 50;
            LastError = "";
            LastUpdateTime = DateTime.Now;
        }
    }
    #endregion

    #region Event Args Classes
    /// <summary>
    /// 로봇 상태 변경 이벤트 인자
    /// </summary>
    public class RobotStatusChangedEventArgs : EventArgs
    {
        public RobotStatus OldStatus { get; }
        public RobotStatus NewStatus { get; }
        public DateTime ChangeTime { get; }

        public RobotStatusChangedEventArgs(RobotStatus oldStatus, RobotStatus newStatus)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
            ChangeTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 위치 변경 이벤트 인자
    /// </summary>
    public class PositionChangedEventArgs : EventArgs
    {
        public Position OldPosition { get; }
        public Position NewPosition { get; }
        public DateTime ChangeTime { get; }

        public PositionChangedEventArgs(Position oldPosition, Position newPosition)
        {
            OldPosition = oldPosition;
            NewPosition = newPosition;
            ChangeTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 로봇 오류 이벤트 인자
    /// </summary>
    public class RobotErrorEventArgs : EventArgs
    {
        public string ErrorCode { get; }
        public string ErrorMessage { get; }
        public Exception Exception { get; }
        public DateTime ErrorTime { get; }

        public RobotErrorEventArgs(string errorCode, string errorMessage, Exception exception = null)
        {
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            Exception = exception;
            ErrorTime = DateTime.Now;
        }
    }
    #endregion

    #region Enums
    /// <summary>
    /// 로봇 동작 모드
    /// </summary>
    public enum RobotMode
    {
        Manual,     // 수동 모드
        Auto,       // 자동 모드
        Teaching,   // 티칭 모드
        Emergency   // 비상 모드
    }

    /// <summary>
    /// 로봇 상태
    /// </summary>
    public enum RobotState
    {
        Disconnected,   // 연결 안됨
        Connected,      // 연결됨
        Initializing,   // 초기화 중
        Ready,          // 준비됨
        Moving,         // 이동 중
        Picking,        // Pick 중
        Placing,        // Place 중
        Error,          // 오류 상태
        Emergency       // 비상 정지
    }
    #endregion
}