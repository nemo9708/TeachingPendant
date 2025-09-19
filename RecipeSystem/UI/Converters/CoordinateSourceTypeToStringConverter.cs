using System;
using System.Globalization;
using System.Windows.Data;
using TeachingPendant.RecipeSystem.Models;

namespace TeachingPendant.RecipeSystem.UI.Converters
{
    /// <summary>
    /// CoordinateSourceType 값을 콤보박스 Tag 문자열("Setup"/"Teaching")로 변환하는 컨버터
    /// </summary>
    public class CoordinateSourceTypeToStringConverter : IValueConverter
    {
        /// <summary>
        /// 열거형 값을 문자열 태그로 변환
        /// </summary>
        /// <param name="value">CoordinateSourceType 값</param>
        /// <param name="targetType">대상 타입</param>
        /// <param name="parameter">매개변수</param>
        /// <param name="culture">문화권 정보</param>
        /// <returns>"Setup" 또는 "Teaching" 문자열</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CoordinateSourceType sourceType)
            {
                switch (sourceType)
                {
                    case CoordinateSourceType.Setup:
                        return "Setup";
                    case CoordinateSourceType.Teaching:
                        return "Teaching";
                }
            }

            // 기본값: Teaching
            return "Teaching";
        }

        /// <summary>
        /// 문자열 태그를 CoordinateSourceType으로 역변환
        /// </summary>
        /// <param name="value">"Setup" 또는 "Teaching" 문자열</param>
        /// <param name="targetType">대상 타입</param>
        /// <param name="parameter">매개변수</param>
        /// <param name="culture">문화권 정보</param>
        /// <returns>CoordinateSourceType 값</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text && Enum.TryParse(text, out CoordinateSourceType result))
            {
                return result;
            }

            // 예상치 못한 입력은 기존 값 유지
            return Binding.DoNothing;
        }
    }
}