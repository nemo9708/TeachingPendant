using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using TeachingPendant.HardwareControllers;

namespace TeachingPendant.RecipeSystem.Models
{
    /// <summary>
    /// 스텝 좌표의 출처를 표시
    /// </summary>
    public enum CoordinateSourceType
    {
        /// <summary>Setup 화면의 HomePos 좌표</summary>
        Setup,
        /// <summary>Teaching 화면에서 불러온 좌표</summary>
        Teaching
    }

    /// <summary>
    /// 레시피의 개별 실행 스텝
    /// 로봇의 각 동작(이동, Pick, Place 등)을 정의
    /// </summary>
    public class RecipeStep : INotifyPropertyChanged
    {
        #region Private Fields
        private int _stepNumber = 1;
        private StepType _type = StepType.Move;
        private string _description = "";
        private bool _isEnabled = true;
        private Position _targetPosition = new Position();
        private int _speed = 50;
        private double _estimatedDuration = 1.0;
        private Dictionary<string, object> _parameters = new Dictionary<string, object>();
        private CoordinateSourceType _coordinateSource = CoordinateSourceType.Teaching;
        #endregion

        #region Public Properties
        /// <summary>
        /// 스텝 번호 (실행 순서)
        /// </summary>
        public int StepNumber
        {
            get => _stepNumber;
            set
            {
                if (_stepNumber != value)
                {
                    _stepNumber = value;
                    OnPropertyChanged(nameof(StepNumber));
                }
            }
        }

        /// <summary>
        /// 스텝 유형
        /// </summary>
        public StepType Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged(nameof(Type));
                    UpdateEstimatedDuration();
                }
            }
        }

        /// <summary>
        /// 스텝 설명
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value ?? "";
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        /// <summary>
        /// 스텝 활성화 여부
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        /// <summary>
        /// 목표 위치 (Move, Pick, Place 스텝에서 사용)
        /// </summary>
        public Position TargetPosition
        {
            get => _targetPosition;
            set
            {
                if (_targetPosition != value)
                {
                    _targetPosition = value ?? new Position();
                    OnPropertyChanged(nameof(TargetPosition));
                }
            }
        }

        /// <summary>
        /// 실행 속도 (1-100%)
        /// </summary>
        public int Speed
        {
            get => _speed;
            set
            {
                if (_speed != value)
                {
                    _speed = Math.Max(1, Math.Min(100, value));
                    OnPropertyChanged(nameof(Speed));
                    UpdateEstimatedDuration();
                }
            }
        }

        /// <summary>
        /// 예상 실행 시간 (초)
        /// </summary>
        public double EstimatedDuration
        {
            get => _estimatedDuration;
            set
            {
                if (Math.Abs(_estimatedDuration - value) > 0.01)
                {
                    _estimatedDuration = Math.Max(0.1, value);
                    OnPropertyChanged(nameof(EstimatedDuration));
                }
            }
        }

        /// <summary>
        /// 추가 매개변수 (스텝별 고유 설정)
        /// </summary>
        public Dictionary<string, object> Parameters
        {
            get => _parameters;
            set
            {
                if (_parameters != value)
                {
                    _parameters = value ?? new Dictionary<string, object>();
                    OnPropertyChanged(nameof(Parameters));
                }
            }
        }

        /// <summary>
        /// Teaching UI에서 참조할 그룹명 (선택사항)
        /// </summary>
        public string TeachingGroupName { get; set; } = "";

        /// <summary>
        /// Teaching UI에서 참조할 위치명 (선택사항, 예: "P1", "P2")
        /// </summary>
        public string TeachingLocationName { get; set; } = "";

        /// <summary>
        /// 현재 좌표가 어떤 출처에서 왔는지(S: Setup, T: Teaching)
        /// </summary>
        public CoordinateSourceType CoordinateSource
        {
            get => _coordinateSource;
            set
            {
                if (_coordinateSource != value)
                {
                    _coordinateSource = value;
                    OnPropertyChanged(nameof(CoordinateSource));
                }
            }
        }

        /// <summary>
        /// 스텝 생성 시간
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 대기 시간 (Wait 스텝에서 사용, 밀리초)
        /// </summary>
        public int WaitTimeMs { get; set; } = 1000;

        /// <summary>
        /// 안전 확인 옵션 (CheckSafety 스텝에서 사용)
        /// </summary>
        public SafetyCheckOptions SafetyOptions { get; set; } = SafetyCheckOptions.All;
        #endregion

        #region Constructors
        /// <summary>
        /// 기본 생성자
        /// </summary>
        public RecipeStep()
        {
            _parameters = new Dictionary<string, object>();
            _targetPosition = new Position();
            UpdateEstimatedDuration();
        }

        /// <summary>
        /// 스텝 타입을 지정하는 생성자
        /// </summary>
        /// <param name="type">스텝 타입</param>
        /// <param name="description">스텝 설명</param>
        public RecipeStep(StepType type, string description = "") : this()
        {
            Type = type;
            Description = description;
        }

        /// <summary>
        /// 이동 스텝 생성자
        /// </summary>
        /// <param name="position">목표 위치</param>
        /// <param name="speed">이동 속도</param>
        /// <param name="description">스텝 설명</param>
        public RecipeStep(Position position, int speed = 50, string description = "") : this()
        {
            Type = StepType.Move;
            TargetPosition = position;
            Speed = speed;
            Description = description;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 스텝 유효성 검증
        /// </summary>
        /// <returns>검증 결과</returns>
        public RecipeValidationResult Validate()
        {
            var result = new RecipeValidationResult();

            try
            {
                // 공통 검증
                if (Speed < 1 || Speed > 100)
                {
                    result.AddError("속도는 1-100% 범위여야 합니다.");
                }

                // 스텝 타입별 검증
                switch (Type)
                {
                    case StepType.Move:
                    case StepType.Pick:
                    case StepType.Place:
                        ValidatePositionStep(result);
                        break;

                    case StepType.Wait:
                        if (WaitTimeMs < 0)
                        {
                            result.AddError("대기 시간은 0 이상이어야 합니다.");
                        }
                        break;

                    case StepType.Home:
                    case StepType.CheckSafety:
                        // 추가 검증 불필요
                        break;

                    default:
                        result.AddError($"알 수 없는 스텝 타입: {Type}");
                        break;
                }

                result.IsValid = result.ErrorMessages.Count == 0;
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeStep] 스텝 검증 오류: {ex.Message}");
                result.AddError($"검증 중 오류 발생: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Teaching 데이터로부터 실제 좌표 설정
        /// </summary>
        /// <returns>좌표 설정 성공 여부</returns>
        public bool LoadCoordinatesFromTeaching()
        {
            try
            {
                if (string.IsNullOrEmpty(TeachingGroupName) || string.IsNullOrEmpty(TeachingLocationName))
                {
                    return false;
                }

                // Teaching UI에서 좌표 데이터 가져오기
                // 실제 구현 시 TeachingDataManager에서 좌표 조회
                // 현재는 시뮬레이션 좌표 사용

                System.Diagnostics.Debug.WriteLine($"[RecipeStep] Teaching 좌표 로드: {TeachingGroupName} - {TeachingLocationName}");

                // TODO: 실제 Teaching 데이터 연동
                // var teachingData = TeachingDataManager.GetPositionData(TeachingGroupName, TeachingLocationName);
                // if (teachingData != null)
                // {
                //     TargetPosition = new Position(teachingData.R, teachingData.Theta, teachingData.Z);
                //     return true;
                // }

                // 임시 시뮬레이션 좌표
                switch (TeachingLocationName.ToUpper())
                {
                    case "P1": TargetPosition = new Position(100, 0, 50); break;
                    case "P2": TargetPosition = new Position(100, 51.4, 50); break;
                    case "P3": TargetPosition = new Position(100, 102.8, 50); break;
                    case "P4": TargetPosition = new Position(200, 180, 100); break;
                    case "P5": TargetPosition = new Position(100, 257.2, 50); break;
                    case "P6": TargetPosition = new Position(100, 308.6, 50); break;
                    case "P7": TargetPosition = new Position(100, 360, 50); break;
                    default: return false;
                }

                System.Diagnostics.Debug.WriteLine($"[RecipeStep] Teaching 좌표 설정됨: {TargetPosition}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeStep] Teaching 좌표 로드 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 스텝을 JSON 문자열로 직렬화
        /// </summary>
        /// <returns>JSON 문자열</returns>
        public string ToJson()
        {
            try
            {
                return JsonConvert.SerializeObject(this, Formatting.Indented);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeStep] JSON 직렬화 실패: {ex.Message}");
                return "{}";
            }
        }

        /// <summary>
        /// JSON 문자열로부터 스텝 역직렬화
        /// </summary>
        /// <param name="json">JSON 문자열</param>
        /// <returns>스텝 객체</returns>
        public static RecipeStep FromJson(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return new RecipeStep();

                return JsonConvert.DeserializeObject<RecipeStep>(json) ?? new RecipeStep();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeStep] JSON 역직렬화 실패: {ex.Message}");
                return new RecipeStep();
            }
        }

        /// <summary>
        /// 스텝 복제 (깊은 복사)
        /// </summary>
        /// <returns>복제된 스텝</returns>
        public RecipeStep Clone()
        {
            try
            {
                // JSON 직렬화/역직렬화를 통한 깊은 복사
                string json = this.ToJson();
                var cloned = RecipeStep.FromJson(json);

                // 스텝 번호는 추가 시 자동 설정되므로 초기화
                cloned.StepNumber = 0;
                cloned.CreatedTime = DateTime.Now;

                return cloned;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeStep] Clone 실패: {ex.Message}");

                // 실패 시 수동 복사
                var cloned = new RecipeStep(this.Type, this.Description)
                {
                    Speed = this.Speed,
                    IsEnabled = this.IsEnabled,
                    WaitTimeMs = this.WaitTimeMs,
                    SafetyOptions = this.SafetyOptions,
                    TeachingGroupName = this.TeachingGroupName,
                    TeachingLocationName = this.TeachingLocationName,
                    TargetPosition = this.TargetPosition != null ?
                        new Position(this.TargetPosition.R, this.TargetPosition.Theta, this.TargetPosition.Z) :
                        new Position()
                };

                // 매개변수 복사
                if (this.Parameters != null)
                {
                    cloned.Parameters = new Dictionary<string, object>();
                    foreach (var param in this.Parameters)
                    {
                        cloned.Parameters[param.Key] = param.Value;
                    }
                }

                return cloned;
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 위치 기반 스텝 검증
        /// </summary>
        /// <param name="result">검증 결과</param>
        private void ValidatePositionStep(RecipeValidationResult result)
        {
            if (TargetPosition == null)
            {
                result.AddError("목표 위치가 설정되지 않았습니다.");
                return;
            }

            // 좌표 범위 검증 (임시 - 실제로는 SafetySystem에서 검증)
            if (TargetPosition.R < 0 || TargetPosition.R > 1000)
            {
                result.AddError($"R 좌표가 유효 범위를 벗어남: {TargetPosition.R}");
            }

            if (TargetPosition.Theta < 0 || TargetPosition.Theta >= 360)
            {
                result.AddError($"θ 좌표가 유효 범위를 벗어남: {TargetPosition.Theta}");
            }

            if (TargetPosition.Z < 0 || TargetPosition.Z > 500)
            {
                result.AddError($"Z 좌표가 유효 범위를 벗어남: {TargetPosition.Z}");
            }
        }

        /// <summary>
        /// 스텝 타입에 따른 예상 실행 시간 자동 계산
        /// </summary>
        private void UpdateEstimatedDuration()
        {
            try
            {
                double baseDuration = 0;

                switch (Type)
                {
                    case StepType.Move:
                        // 이동 거리와 속도에 따른 계산 (임시)
                        baseDuration = 2.0 + (100.0 - Speed) * 0.03;
                        break;

                    case StepType.Pick:
                        baseDuration = 3.0 + (100.0 - Speed) * 0.02;
                        break;

                    case StepType.Place:
                        baseDuration = 3.0 + (100.0 - Speed) * 0.02;
                        break;

                    case StepType.Home:
                        baseDuration = 5.0 + (100.0 - Speed) * 0.05;
                        break;

                    case StepType.Wait:
                        baseDuration = WaitTimeMs / 1000.0;
                        break;

                    case StepType.CheckSafety:
                        baseDuration = 0.5;
                        break;

                    default:
                        baseDuration = 1.0;
                        break;
                }

                EstimatedDuration = baseDuration;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeStep] 예상 시간 계산 실패: {ex.Message}");
                EstimatedDuration = 1.0;
            }
        }
        #endregion

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

    /// <summary>
    /// 스텝 실행 타입 열거형
    /// </summary>
    public enum StepType
    {
        /// <summary>
        /// 지정 위치로 이동
        /// </summary>
        Move,

        /// <summary>
        /// 웨이퍼 집기 (진공 ON + 상승)
        /// </summary>
        Pick,

        /// <summary>
        /// 웨이퍼 놓기 (하강 + 진공 OFF)
        /// </summary>
        Place,

        /// <summary>
        /// 홈 위치로 이동
        /// </summary>
        Home,

        /// <summary>
        /// 지정 시간 대기
        /// </summary>
        Wait,

        /// <summary>
        /// 안전 상태 확인
        /// </summary>
        CheckSafety
    }

    /// <summary>
    /// 안전 확인 옵션
    /// </summary>
    [Flags]
    public enum SafetyCheckOptions
    {
        None = 0,
        Interlock = 1,
        SoftLimit = 2,
        RobotStatus = 4,
        All = Interlock | SoftLimit | RobotStatus
    }
}