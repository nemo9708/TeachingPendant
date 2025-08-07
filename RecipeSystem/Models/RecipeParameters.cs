using System;
using System.ComponentModel;
using Newtonsoft.Json;
using TeachingPendant.RecipeSystem.Models;

namespace TeachingPendant.RecipeSystem.Models
{
    /// <summary>
    /// 레시피 실행 시 사용되는 전역 매개변수
    /// </summary>
    public class RecipeParameters : INotifyPropertyChanged
    {
        #region Private Fields
        private int _defaultSpeed = 50;
        private int _pickSpeed = 30;
        private int _placeSpeed = 30;
        private int _homeSpeed = 80;
        private int _pickHeight = 5;
        private int _placeHeight = 5;
        private int _safeHeight = 100;
        private int _pickDelayMs = 500;
        private int _placeDelayMs = 500;
        private bool _useVacuum = true;
        private bool _checkSafetyBeforeEachStep = true;
        private bool _pauseOnError = true;
        private int _retryCount = 3;
        private int _retryDelayMs = 1000;
        #endregion

        #region Speed Parameters
        /// <summary>
        /// 기본 이동 속도 (1-100%)
        /// </summary>
        public int DefaultSpeed
        {
            get => _defaultSpeed;
            set
            {
                var newValue = Math.Max(1, Math.Min(100, value));
                if (_defaultSpeed != newValue)
                {
                    _defaultSpeed = newValue;
                    OnPropertyChanged(nameof(DefaultSpeed));
                }
            }
        }

        /// <summary>
        /// Pick 동작 속도 (1-100%)
        /// </summary>
        public int PickSpeed
        {
            get => _pickSpeed;
            set
            {
                var newValue = Math.Max(1, Math.Min(100, value));
                if (_pickSpeed != newValue)
                {
                    _pickSpeed = newValue;
                    OnPropertyChanged(nameof(PickSpeed));
                }
            }
        }

        /// <summary>
        /// Place 동작 속도 (1-100%)
        /// </summary>
        public int PlaceSpeed
        {
            get => _placeSpeed;
            set
            {
                var newValue = Math.Max(1, Math.Min(100, value));
                if (_placeSpeed != newValue)
                {
                    _placeSpeed = newValue;
                    OnPropertyChanged(nameof(PlaceSpeed));
                }
            }
        }

        /// <summary>
        /// 홈 이동 속도 (1-100%)
        /// </summary>
        public int HomeSpeed
        {
            get => _homeSpeed;
            set
            {
                var newValue = Math.Max(1, Math.Min(100, value));
                if (_homeSpeed != newValue)
                {
                    _homeSpeed = newValue;
                    OnPropertyChanged(nameof(HomeSpeed));
                }
            }
        }
        #endregion

        #region Height Parameters
        /// <summary>
        /// Pick 시 하강 높이 (mm)
        /// </summary>
        public int PickHeight
        {
            get => _pickHeight;
            set
            {
                var newValue = Math.Max(0, Math.Min(50, value));
                if (_pickHeight != newValue)
                {
                    _pickHeight = newValue;
                    OnPropertyChanged(nameof(PickHeight));
                }
            }
        }

        /// <summary>
        /// Place 시 하강 높이 (mm)
        /// </summary>
        public int PlaceHeight
        {
            get => _placeHeight;
            set
            {
                var newValue = Math.Max(0, Math.Min(50, value));
                if (_placeHeight != newValue)
                {
                    _placeHeight = newValue;
                    OnPropertyChanged(nameof(PlaceHeight));
                }
            }
        }

        /// <summary>
        /// 안전 이동 높이 (mm)
        /// </summary>
        public int SafeHeight
        {
            get => _safeHeight;
            set
            {
                var newValue = Math.Max(50, Math.Min(200, value));
                if (_safeHeight != newValue)
                {
                    _safeHeight = newValue;
                    OnPropertyChanged(nameof(SafeHeight));
                }
            }
        }
        #endregion

        #region Timing Parameters
        /// <summary>
        /// Pick 후 대기 시간 (ms)
        /// </summary>
        public int PickDelayMs
        {
            get => _pickDelayMs;
            set
            {
                var newValue = Math.Max(0, Math.Min(5000, value));
                if (_pickDelayMs != newValue)
                {
                    _pickDelayMs = newValue;
                    OnPropertyChanged(nameof(PickDelayMs));
                }
            }
        }

        /// <summary>
        /// Place 후 대기 시간 (ms)
        /// </summary>
        public int PlaceDelayMs
        {
            get => _placeDelayMs;
            set
            {
                var newValue = Math.Max(0, Math.Min(5000, value));
                if (_placeDelayMs != newValue)
                {
                    _placeDelayMs = newValue;
                    OnPropertyChanged(nameof(PlaceDelayMs));
                }
            }
        }
        #endregion

        #region Safety Parameters
        /// <summary>
        /// 진공 사용 여부
        /// </summary>
        public bool UseVacuum
        {
            get => _useVacuum;
            set
            {
                if (_useVacuum != value)
                {
                    _useVacuum = value;
                    OnPropertyChanged(nameof(UseVacuum));
                }
            }
        }

        /// <summary>
        /// 각 스텝 실행 전 안전 확인 여부
        /// </summary>
        public bool CheckSafetyBeforeEachStep
        {
            get => _checkSafetyBeforeEachStep;
            set
            {
                if (_checkSafetyBeforeEachStep != value)
                {
                    _checkSafetyBeforeEachStep = value;
                    OnPropertyChanged(nameof(CheckSafetyBeforeEachStep));
                }
            }
        }

        /// <summary>
        /// 오류 발생 시 일시정지 여부
        /// </summary>
        public bool PauseOnError
        {
            get => _pauseOnError;
            set
            {
                if (_pauseOnError != value)
                {
                    _pauseOnError = value;
                    OnPropertyChanged(nameof(PauseOnError));
                }
            }
        }
        #endregion

        #region Error Handling Parameters
        /// <summary>
        /// 오류 시 재시도 횟수
        /// </summary>
        public int RetryCount
        {
            get => _retryCount;
            set
            {
                var newValue = Math.Max(0, Math.Min(10, value));
                if (_retryCount != newValue)
                {
                    _retryCount = newValue;
                    OnPropertyChanged(nameof(RetryCount));
                }
            }
        }

        /// <summary>
        /// 재시도 간격 (ms)
        /// </summary>
        public int RetryDelayMs
        {
            get => _retryDelayMs;
            set
            {
                var newValue = Math.Max(100, Math.Min(10000, value));
                if (_retryDelayMs != newValue)
                {
                    _retryDelayMs = newValue;
                    OnPropertyChanged(nameof(RetryDelayMs));
                }
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// 기본 생성자 (안전한 기본값 설정)
        /// </summary>
        public RecipeParameters()
        {
            // 기본값은 필드에서 이미 설정됨
        }

        /// <summary>
        /// 복사 생성자
        /// </summary>
        /// <param name="source">복사할 매개변수</param>
        public RecipeParameters(RecipeParameters source)
        {
            if (source == null) return;

            DefaultSpeed = source.DefaultSpeed;
            PickSpeed = source.PickSpeed;
            PlaceSpeed = source.PlaceSpeed;
            HomeSpeed = source.HomeSpeed;
            PickHeight = source.PickHeight;
            PlaceHeight = source.PlaceHeight;
            SafeHeight = source.SafeHeight;
            PickDelayMs = source.PickDelayMs;
            PlaceDelayMs = source.PlaceDelayMs;
            UseVacuum = source.UseVacuum;
            CheckSafetyBeforeEachStep = source.CheckSafetyBeforeEachStep;
            PauseOnError = source.PauseOnError;
            RetryCount = source.RetryCount;
            RetryDelayMs = source.RetryDelayMs;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 매개변수 유효성 검증
        /// </summary>
        /// <returns>검증 결과</returns>
        public RecipeValidationResult Validate()
        {
            var result = new RecipeValidationResult();

            try
            {
                // 속도 검증
                if (DefaultSpeed < 1 || DefaultSpeed > 100)
                    result.AddError($"기본 속도가 유효 범위를 벗어남: {DefaultSpeed}%");

                if (PickSpeed < 1 || PickSpeed > 100)
                    result.AddError($"Pick 속도가 유효 범위를 벗어남: {PickSpeed}%");

                if (PlaceSpeed < 1 || PlaceSpeed > 100)
                    result.AddError($"Place 속도가 유효 범위를 벗어남: {PlaceSpeed}%");

                if (HomeSpeed < 1 || HomeSpeed > 100)
                    result.AddError($"홈 속도가 유효 범위를 벗어남: {HomeSpeed}%");

                // 높이 검증
                if (PickHeight < 0 || PickHeight > 50)
                    result.AddError($"Pick 높이가 유효 범위를 벗어남: {PickHeight}mm");

                if (PlaceHeight < 0 || PlaceHeight > 50)
                    result.AddError($"Place 높이가 유효 범위를 벗어남: {PlaceHeight}mm");

                if (SafeHeight < 50 || SafeHeight > 200)
                    result.AddError($"안전 높이가 유효 범위를 벗어남: {SafeHeight}mm");

                // 시간 검증
                if (PickDelayMs < 0 || PickDelayMs > 5000)
                    result.AddError($"Pick 대기시간이 유효 범위를 벗어남: {PickDelayMs}ms");

                if (PlaceDelayMs < 0 || PlaceDelayMs > 5000)
                    result.AddError($"Place 대기시간이 유효 범위를 벗어남: {PlaceDelayMs}ms");

                if (RetryDelayMs < 100 || RetryDelayMs > 10000)
                    result.AddError($"재시도 간격이 유효 범위를 벗어남: {RetryDelayMs}ms");

                // 논리적 검증
                if (PickSpeed > DefaultSpeed + 20)
                    result.AddWarning("Pick 속도가 기본 속도보다 과도하게 높습니다.");

                if (PlaceSpeed > DefaultSpeed + 20)
                    result.AddWarning("Place 속도가 기본 속도보다 과도하게 높습니다.");

                result.IsValid = result.ErrorMessages.Count == 0;
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeParameters] 매개변수 검증 오류: {ex.Message}");
                result.AddError($"검증 중 오류 발생: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// 기본값으로 재설정
        /// </summary>
        public void ResetToDefaults()
        {
            DefaultSpeed = 50;
            PickSpeed = 30;
            PlaceSpeed = 30;
            HomeSpeed = 80;
            PickHeight = 5;
            PlaceHeight = 5;
            SafeHeight = 100;
            PickDelayMs = 500;
            PlaceDelayMs = 500;
            UseVacuum = true;
            CheckSafetyBeforeEachStep = true;
            PauseOnError = true;
            RetryCount = 3;
            RetryDelayMs = 1000;

            System.Diagnostics.Debug.WriteLine("[RecipeParameters] 매개변수가 기본값으로 재설정됨");
        }

        /// <summary>
        /// 매개변수를 JSON 문자열로 직렬화
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
                System.Diagnostics.Debug.WriteLine($"[RecipeParameters] JSON 직렬화 실패: {ex.Message}");
                return "{}";
            }
        }

        /// <summary>
        /// JSON 문자열로부터 매개변수 역직렬화
        /// </summary>
        /// <param name="json">JSON 문자열</param>
        /// <returns>매개변수 객체</returns>
        public static RecipeParameters FromJson(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return new RecipeParameters();

                return JsonConvert.DeserializeObject<RecipeParameters>(json) ?? new RecipeParameters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeParameters] JSON 역직렬화 실패: {ex.Message}");
                return new RecipeParameters();
            }
        }

        /// <summary>
        /// 매개변수 복제
        /// </summary>
        /// <returns>복제된 매개변수</returns>
        public RecipeParameters Clone()
        {
            return new RecipeParameters(this);
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
}