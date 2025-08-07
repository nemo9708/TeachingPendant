using System;
using System.Globalization;
using System.Windows.Data;
using TeachingPendant.RecipeSystem.Models;

namespace TeachingPendant.RecipeSystem.UI.Converters
{
    /// <summary>
    /// StepType을 아이콘 문자열로 변환하는 컨버터
    /// 각 스텝 타입별로 적절한 이모지 아이콘 반환
    /// </summary>
    public class StepTypeToIconConverter : IValueConverter
    {
        /// <summary>
        /// StepType을 아이콘 문자열로 변환
        /// </summary>
        /// <param name="value">StepType 값</param>
        /// <param name="targetType">대상 타입</param>
        /// <param name="parameter">매개변수</param>
        /// <param name="culture">문화권 정보</param>
        /// <returns>아이콘 문자열</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is StepType stepType)
                {
                    switch (stepType)
                    {
                        case StepType.Home:
                            return "🏠";
                        case StepType.Move:
                            return "➡️";
                        case StepType.Pick:
                            return "⬇️";
                        case StepType.Place:
                            return "⬆️";
                        case StepType.Wait:
                            return "⏱️";
                        case StepType.CheckSafety:
                            return "🛡️";
                        default:
                            return "❓";
                    }
                }
                return "❓";
            }
            catch (Exception)
            {
                // 변환 실패 시 기본 아이콘 반환
                return "❓";
            }
        }

        /// <summary>
        /// 아이콘 문자열을 StepType으로 역변환 (사용하지 않음)
        /// </summary>
        /// <param name="value">아이콘 문자열</param>
        /// <param name="targetType">대상 타입</param>
        /// <param name="parameter">매개변수</param>
        /// <param name="culture">문화권 정보</param>
        /// <returns>역변환된 값</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("StepTypeToIconConverter는 양방향 변환을 지원하지 않습니다.");
        }
    }
}