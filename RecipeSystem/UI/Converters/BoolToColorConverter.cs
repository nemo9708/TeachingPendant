using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TeachingPendant.RecipeSystem.UI.Converters
{
    /// <summary>
    /// Boolean 값을 색상으로 변환하는 컨버터
    /// 활성화된 레시피는 녹색, 비활성화된 레시피는 회색으로 표시
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        /// <summary>
        /// Boolean 값을 Color로 변환
        /// </summary>
        /// <param name="value">Boolean 값</param>
        /// <param name="targetType">대상 타입</param>
        /// <param name="parameter">매개변수</param>
        /// <param name="culture">문화권 정보</param>
        /// <returns>변환된 Color</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is bool isEnabled)
                {
                    return isEnabled ? Colors.LightGreen : Colors.Gray;
                }
                return Colors.Gray;
            }
            catch (Exception)
            {
                // 변환 실패 시 기본 색상 반환
                return Colors.Gray;
            }
        }

        /// <summary>
        /// Color를 Boolean으로 역변환 (사용하지 않음)
        /// </summary>
        /// <param name="value">Color 값</param>
        /// <param name="targetType">대상 타입</param>
        /// <param name="parameter">매개변수</param>
        /// <param name="culture">문화권 정보</param>
        /// <returns>역변환된 값</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("BoolToColorConverter는 양방향 변환을 지원하지 않습니다.");
        }
    }
}