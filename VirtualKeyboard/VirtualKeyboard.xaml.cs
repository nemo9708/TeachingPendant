using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TeachingPendant.VirtualKeyboard
{
    public partial class VirtualKeyboard : Window
    {
        #region Private Fields
        private bool _isShiftPressed = false;
        private bool _isNumberMode = false;

        // 🔥 텍스트 입력 콜백 함수
        private Action<string> _textInputCallback;
        private Action _backspaceCallback;
        #endregion

        #region Constructor
        public VirtualKeyboard()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            PositionKeyboard();

            // 🔥 창이 포커스를 받지 않도록 설정 (중요!)
            this.Focusable = false;
            this.ShowActivated = false;
        }
        #endregion

        #region Callback Setup
        /// <summary>
        /// 🔥 텍스트 입력 콜백 설정
        /// </summary>
        public void SetTextInputCallback(Action<string> textInputCallback)
        {
            _textInputCallback = textInputCallback;
            System.Diagnostics.Debug.WriteLine("[VirtualKeyboard] 텍스트 입력 콜백 설정 완료");
        }

        /// <summary>
        /// 🔥 백스페이스 콜백 설정
        /// </summary>
        public void SetBackspaceCallback(Action backspaceCallback)
        {
            _backspaceCallback = backspaceCallback;
            System.Diagnostics.Debug.WriteLine("[VirtualKeyboard] 백스페이스 콜백 설정 완료");
        }
        #endregion

        #region Positioning
        /// <summary>
        /// 키보드를 화면 하단에 위치시킴
        /// </summary>
        private void PositionKeyboard()
        {
            try
            {
                // 화면 크기 가져오기
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;

                // 화면 하단 중앙에 배치
                this.Left = (screenWidth - this.Width) / 2;
                this.Top = screenHeight - this.Height - 50; // 하단에서 50픽셀 위
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] 위치 설정 오류: {ex.Message}");
            }
        }
        #endregion

        #region Button Event Handlers

        /// <summary>
        /// 일반 키 버튼 클릭 (문자, 숫자, 기호)
        /// </summary>
        private void KeyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button)
                {
                    string key = button.Content.ToString();

                    // Shift가 눌렸으면 대문자로 변환
                    if (_isShiftPressed && char.IsLetter(key[0]))
                    {
                        key = key.ToUpper();
                        _isShiftPressed = false; // Shift 해제
                        UpdateShiftButton();
                    }
                    else if (!_isShiftPressed && char.IsLetter(key[0]))
                    {
                        key = key.ToLower();
                    }

                    // 🔥 콜백을 통해 텍스트 전송
                    _textInputCallback?.Invoke(key);

                    System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] 키 입력: '{key}'");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] 키 입력 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 백스페이스 버튼 클릭
        /// </summary>
        private void BackspaceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[VirtualKeyboard] 백스페이스 버튼 클릭");

                // 🔥 VirtualKeyboardManager를 통해 백스페이스 처리
                VirtualKeyboardManager.SendBackspaceToFocusedControl();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] 백스페이스 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 엔터 버튼 클릭
        /// </summary>
        private void EnterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[VirtualKeyboard] 엔터 버튼 클릭");

                // 🔥 엔터키는 특별 처리 (추후 확장 가능)
                _textInputCallback?.Invoke("\r\n");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] 엔터 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 스페이스 바 클릭
        /// </summary>
        private void SpaceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[VirtualKeyboard] 스페이스 버튼 클릭");

                // 🔥 콜백을 통해 스페이스 전송
                _textInputCallback?.Invoke(" ");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] 스페이스 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// Shift 버튼 클릭
        /// </summary>
        private void ShiftButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isShiftPressed = !_isShiftPressed;
                UpdateShiftButton();
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] Shift 상태: {_isShiftPressed}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] Shift 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 숫자 모드 버튼 클릭
        /// </summary>
        private void NumberModeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[VirtualKeyboard] 숫자 모드 버튼 클릭");
                // 추후 숫자 모드 구현 시 사용
                MessageBox.Show("숫자 모드는 추후 구현 예정입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] 숫자 모드 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 닫기 버튼 클릭
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[VirtualKeyboard] 닫기 버튼 클릭");
                this.Hide();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] 닫기 오류: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Shift 버튼 UI 업데이트
        /// </summary>
        private void UpdateShiftButton()
        {
            try
            {
                // XAML에서 정의된 Shift 버튼 찾기
                var shiftButton = this.FindName("ShiftButton") as Button;
                if (shiftButton != null)
                {
                    if (_isShiftPressed)
                    {
                        shiftButton.Background = System.Windows.Media.Brushes.LightBlue;
                        shiftButton.Content = "SHIFT";
                    }
                    else
                    {
                        shiftButton.Background = System.Windows.Media.Brushes.LightGray;
                        shiftButton.Content = "Shift";
                    }
                }
                else
                {
                    // btnShift로 찾아보기
                    var btnShift = this.FindName("btnShift") as Button;
                    if (btnShift != null)
                    {
                        if (_isShiftPressed)
                        {
                            btnShift.Background = System.Windows.Media.Brushes.LightBlue;
                            btnShift.Content = "SHIFT";
                        }
                        else
                        {
                            btnShift.Background = System.Windows.Media.Brushes.LightGray;
                            btnShift.Content = "Shift";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] Shift 버튼 업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 🔥 더 이상 사용하지 않는 구 방식 메서드 (호환성 유지)
        /// </summary>
        private void SendTextToFocusedControl(string text)
        {
            try
            {
                // VirtualKeyboardManager의 콜백을 통해 처리
                _textInputCallback?.Invoke(text);
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] 레거시 메서드를 통한 텍스트 전송: '{text}'");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] 레거시 텍스트 전송 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 🔥 문자를 Key enum으로 변환 (더 이상 사용하지 않지만 호환성 유지)
        /// </summary>
        private Key CharToKey(char c)
        {
            if (c >= 'a' && c <= 'z')
                return (Key)((int)Key.A + (c - 'a'));
            if (c >= 'A' && c <= 'Z')
                return (Key)((int)Key.A + (c - 'A'));
            if (c >= '0' && c <= '9')
                return (Key)((int)Key.D0 + (c - '0'));

            // 특수 문자들
            switch (c)
            {
                case ' ': return Key.Space;
                case ',': return Key.OemComma;
                case '.': return Key.OemPeriod;
                default: return Key.None;
            }
        }

        /// <summary>
        /// 🔥 키 입력 시뮬레이션 (더 이상 사용하지 않지만 호환성 유지)
        /// </summary>
        private void SendKey(Key key)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] 레거시 키 시뮬레이션: {key}");
                // 더 이상 직접 키 시뮬레이션하지 않고 콜백을 통해 처리
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] 레거시 키 시뮬레이션 오류: {ex.Message}");
            }
        }

        #endregion

        #region Window Event Handlers
        /// <summary>
        /// 🔥 창 활성화 방지 (포커스 유지를 위해 중요)
        /// </summary>
        protected override void OnActivated(EventArgs e)
        {
            // 기본 활성화 동작을 하지 않음 (포커스 유지)
            System.Diagnostics.Debug.WriteLine("[VirtualKeyboard] 창 활성화 방지됨");
        }

        /// <summary>
        /// 창이 표시될 때
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                // 🔥 창이 포커스를 받지 않도록 윈도우 스타일 설정
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    // WS_EX_NOACTIVATE 스타일 추가
                    const int GWL_EXSTYLE = -20;
                    const int WS_EX_NOACTIVATE = 0x08000000;

                    SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_NOACTIVATE);
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboard] WS_EX_NOACTIVATE 스타일 적용됨");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboard] 윈도우 스타일 설정 오류: {ex.Message}");
            }
        }

        // Win32 API 함수들 (포커스 방지용)
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        #endregion
    }
}