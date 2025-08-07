using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace TeachingPendant.RecipeSystem.Models
{
    /// <summary>
    /// 웨이퍼 반송 레시피를 정의하는 최상위 클래스
    /// Teaching UI에서 설정된 좌표들을 기반으로 자동 작업을 수행
    /// </summary>
    public class TransferRecipe : INotifyPropertyChanged
    {
        #region Private Fields
        private string _recipeName = "";
        private string _description = "";
        private bool _isEnabled = true;
        private List<RecipeStep> _steps = new List<RecipeStep>();
        private RecipeParameters _parameters = new RecipeParameters();
        #endregion

        #region Public Properties
        /// <summary>
        /// 레시피 고유 ID (자동 생성)
        /// </summary>
        public string RecipeId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 레시피 이름
        /// </summary>
        public string RecipeName
        {
            get => _recipeName;
            set
            {
                if (_recipeName != value)
                {
                    _recipeName = value;
                    OnPropertyChanged(nameof(RecipeName));
                }
            }
        }

        /// <summary>
        /// 레시피 설명
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        /// <summary>
        /// 레시피 활성화 여부
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
        /// 레시피 실행 스텝 목록
        /// </summary>
        public List<RecipeStep> Steps
        {
            get => _steps;
            set
            {
                if (_steps != value)
                {
                    _steps = value ?? new List<RecipeStep>();
                    OnPropertyChanged(nameof(Steps));
                    OnPropertyChanged(nameof(StepCount));
                }
            }
        }

        /// <summary>
        /// 레시피 실행 매개변수
        /// </summary>
        public RecipeParameters Parameters
        {
            get => _parameters;
            set
            {
                if (_parameters != value)
                {
                    _parameters = value ?? new RecipeParameters();
                    OnPropertyChanged(nameof(Parameters));
                }
            }
        }

        /// <summary>
        /// 레시피 생성일시
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// 레시피 수정일시
        /// </summary>
        public DateTime ModifiedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// 레시피 생성자
        /// </summary>
        public string CreatedBy { get; set; } = Environment.UserName;

        /// <summary>
        /// 레시피 버전
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// 총 스텝 수 (읽기 전용)
        /// </summary>
        [JsonIgnore]
        public int StepCount => _steps?.Count ?? 0;

        /// <summary>
        /// 예상 실행 시간 (초)
        /// </summary>
        [JsonIgnore]
        public double EstimatedExecutionTime
        {
            get
            {
                if (_steps == null || _steps.Count == 0) return 0;

                double totalTime = 0;
                foreach (var step in _steps)
                {
                    totalTime += step.EstimatedDuration;
                }
                return totalTime;
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// 기본 생성자
        /// </summary>
        public TransferRecipe()
        {
            _steps = new List<RecipeStep>();
            _parameters = new RecipeParameters();
        }

        /// <summary>
        /// 이름과 설명을 지정하는 생성자
        /// </summary>
        /// <param name="recipeName">레시피 이름</param>
        /// <param name="description">레시피 설명</param>
        public TransferRecipe(string recipeName, string description) : this()
        {
            RecipeName = recipeName;
            Description = description;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 새 스텝을 레시피에 추가
        /// </summary>
        /// <param name="step">추가할 스텝</param>
        public void AddStep(RecipeStep step)
        {
            if (step == null) return;

            step.StepNumber = _steps.Count + 1;
            _steps.Add(step);
            ModifiedDate = DateTime.Now;
            OnPropertyChanged(nameof(Steps));
            OnPropertyChanged(nameof(StepCount));
            OnPropertyChanged(nameof(EstimatedExecutionTime));

            System.Diagnostics.Debug.WriteLine($"[TransferRecipe] 스텝 추가됨: {step.Type} - {step.Description}");
        }

        /// <summary>
        /// 지정된 위치에 스텝 삽입
        /// </summary>
        /// <param name="index">삽입할 위치</param>
        /// <param name="step">삽입할 스텝</param>
        public void InsertStep(int index, RecipeStep step)
        {
            if (step == null || index < 0 || index > _steps.Count) return;

            _steps.Insert(index, step);

            // 스텝 번호 재정렬
            for (int i = 0; i < _steps.Count; i++)
            {
                _steps[i].StepNumber = i + 1;
            }

            ModifiedDate = DateTime.Now;
            OnPropertyChanged(nameof(Steps));
            OnPropertyChanged(nameof(StepCount));
            OnPropertyChanged(nameof(EstimatedExecutionTime));

            System.Diagnostics.Debug.WriteLine($"[TransferRecipe] 스텝 삽입됨: 위치 {index}, {step.Type}");
        }

        /// <summary>
        /// 스텝 제거
        /// </summary>
        /// <param name="stepNumber">제거할 스텝 번호</param>
        /// <returns>제거 성공 여부</returns>
        public bool RemoveStep(int stepNumber)
        {
            var step = _steps.Find(s => s.StepNumber == stepNumber);
            if (step == null) return false;

            _steps.Remove(step);

            // 스텝 번호 재정렬
            for (int i = 0; i < _steps.Count; i++)
            {
                _steps[i].StepNumber = i + 1;
            }

            ModifiedDate = DateTime.Now;
            OnPropertyChanged(nameof(Steps));
            OnPropertyChanged(nameof(StepCount));
            OnPropertyChanged(nameof(EstimatedExecutionTime));

            System.Diagnostics.Debug.WriteLine($"[TransferRecipe] 스텝 제거됨: {stepNumber}");
            return true;
        }

        /// <summary>
        /// 레시피 유효성 검증
        /// </summary>
        /// <returns>검증 결과</returns>
        public RecipeValidationResult Validate()
        {
            var result = new RecipeValidationResult();

            try
            {
                // 기본 필드 검증
                if (string.IsNullOrWhiteSpace(RecipeName))
                {
                    result.AddError("레시피 이름이 필요합니다.");
                }

                if (_steps == null || _steps.Count == 0)
                {
                    result.AddError("최소 1개 이상의 스텝이 필요합니다.");
                    return result;
                }

                // 스텝 검증
                for (int i = 0; i < _steps.Count; i++)
                {
                    var stepValidation = _steps[i].Validate();
                    if (!stepValidation.IsValid)
                    {
                        result.AddError($"스텝 {i + 1}: {string.Join(", ", stepValidation.ErrorMessages)}");
                    }
                }

                // 논리적 검증
                ValidateStepSequence(result);

                result.IsValid = result.ErrorMessages.Count == 0;

                System.Diagnostics.Debug.WriteLine($"[TransferRecipe] 레시피 검증 완료: {(result.IsValid ? "성공" : "실패")}");

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransferRecipe] 레시피 검증 오류: {ex.Message}");
                result.AddError($"검증 중 오류 발생: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Teaching 좌표 데이터로부터 레시피 생성
        /// </summary>
        /// <param name="groupName">Teaching 그룹명</param>
        /// <param name="waferCount">반송할 웨이퍼 수</param>
        /// <returns>생성된 레시피</returns>
        public static TransferRecipe CreateFromTeachingData(string groupName, int waferCount)
        {
            try
            {
                var recipe = new TransferRecipe
                {
                    RecipeName = $"Auto Recipe {groupName}_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Description = $"Auto-generated recipe for {waferCount} wafers",
                    CreatedBy = "TeachingSystem",
                    CreatedDate = DateTime.Now,
                    IsEnabled = true
                };

                // 1. 홈 위치로 이동 (영어)
                recipe.AddStep(new RecipeStep
                {
                    Type = StepType.Home,
                    Description = "Move to Home Position",
                    IsEnabled = true
                });

                // 2. 웨이퍼 개수만큼 Pick & Place 스텝 생성 (영어)
                for (int i = 1; i <= waferCount; i++)
                {
                    // Pick 스텝 - P1~P7 순환 (영어)
                    string pickLocation = $"P{((i - 1) % 7) + 1}";
                    recipe.AddStep(new RecipeStep
                    {
                        Type = StepType.Pick,
                        Description = $"Pick Wafer {i} from {pickLocation}",
                        TeachingGroupName = groupName,
                        TeachingLocationName = pickLocation,
                        IsEnabled = true
                    });

                    // Place 스텝 - P4 고정 (영어)
                    recipe.AddStep(new RecipeStep
                    {
                        Type = StepType.Place,
                        Description = $"Place Wafer {i} to P4",
                        TeachingGroupName = groupName,
                        TeachingLocationName = "P4",
                        IsEnabled = true
                    });
                }

                // 3. 완료 후 홈 위치로 복귀 (영어)
                recipe.AddStep(new RecipeStep
                {
                    Type = StepType.Home,
                    Description = "Return to Home after completion",
                    IsEnabled = true
                });

                System.Diagnostics.Debug.WriteLine($"[TransferRecipe] Recipe created from Teaching data: {waferCount} wafers");
                return recipe;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransferRecipe] Failed to create recipe from Teaching data: {ex.Message}");
                return new TransferRecipe("Error", $"Recipe creation error: {ex.Message}");
            }
        }

        /// <summary>
        /// 레시피를 JSON 문자열로 직렬화
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
                System.Diagnostics.Debug.WriteLine($"[TransferRecipe] JSON 직렬화 실패: {ex.Message}");
                return "{}";
            }
        }

        /// <summary>
        /// JSON 문자열로부터 레시피 역직렬화
        /// </summary>
        /// <param name="json">JSON 문자열</param>
        /// <returns>레시피 객체</returns>
        public static TransferRecipe FromJson(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return new TransferRecipe();

                return JsonConvert.DeserializeObject<TransferRecipe>(json) ?? new TransferRecipe();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransferRecipe] JSON 역직렬화 실패: {ex.Message}");
                return new TransferRecipe("JSON 파싱 오류", $"레시피 로드 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 레시피 복제 (깊은 복사)
        /// </summary>
        /// <returns>복제된 레시피</returns>
        public TransferRecipe Clone()
        {
            try
            {
                // JSON 직렬화/역직렬화를 통한 깊은 복사
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                var cloned = JsonConvert.DeserializeObject<TransferRecipe>(json);

                // 고유 ID 새로 생성
                cloned.RecipeId = Guid.NewGuid().ToString();
                cloned.CreatedDate = DateTime.Now;
                cloned.ModifiedDate = DateTime.Now;

                return cloned;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransferRecipe] Clone 실패: {ex.Message}");

                // 실패 시 수동 복사
                var cloned = new TransferRecipe(this.RecipeName, this.Description)
                {
                    IsEnabled = this.IsEnabled,
                    Version = this.Version,
                    CreatedBy = this.CreatedBy
                };

                // 스텝들 복사
                if (this.Steps != null)
                {
                    foreach (var step in this.Steps)
                    {
                        cloned.AddStep(step.Clone());
                    }
                }

                return cloned;
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 스텝 시퀀스 논리적 검증
        /// </summary>
        /// <param name="result">검증 결과</param>
        private void ValidateStepSequence(RecipeValidationResult result)
        {
            bool hasPickWithoutPlace = false;
            bool hasPlaceWithoutPick = false;

            foreach (var step in _steps)
            {
                switch (step.Type)
                {
                    case StepType.Pick:
                        if (hasPickWithoutPlace)
                        {
                            result.AddWarning("연속된 Pick 동작이 감지되었습니다. Place 동작이 누락되었을 수 있습니다.");
                        }
                        hasPickWithoutPlace = true;
                        hasPlaceWithoutPick = false;
                        break;

                    case StepType.Place:
                        if (!hasPickWithoutPlace && !hasPlaceWithoutPick)
                        {
                            result.AddWarning("Pick 동작 없이 Place 동작이 실행됩니다.");
                        }
                        hasPickWithoutPlace = false;
                        hasPlaceWithoutPick = true;
                        break;

                    case StepType.Home:
                    case StepType.Move:
                    case StepType.Wait:
                    case StepType.CheckSafety:
                        // 이동 관련 동작은 Pick/Place 상태에 영향 없음
                        break;
                }
            }

            if (hasPickWithoutPlace)
            {
                result.AddWarning("마지막 Pick 동작 후 Place 동작이 없습니다.");
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
    /// 레시피 검증 결과 클래스
    /// </summary>
    public class RecipeValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> ErrorMessages { get; set; } = new List<string>();
        public List<string> WarningMessages { get; set; } = new List<string>();

        public void AddError(string message)
        {
            IsValid = false;
            ErrorMessages.Add(message);
        }

        public void AddWarning(string message)
        {
            WarningMessages.Add(message);
        }
    }
}