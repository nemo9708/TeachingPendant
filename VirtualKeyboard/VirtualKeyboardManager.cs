using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TeachingPendant.VirtualKeyboard
{
    public static class VirtualKeyboardManager
    {
        #region Private Fields
        private static VirtualKeyboard _virtualKeyboard;
        private static List<Window> _registeredWindows = new List<Window>();

        // 🔥 포커스된 컨트롤을 기억하기 위한 필드 추가
        private static WeakReference _lastFocusedTextControl = new WeakReference(null);
        #endregion

        #region Public Properties
        /// <summary>
        /// 현재 포커스된 텍스트 컨트롤 (TextBox 또는 PasswordBox)
        /// </summary>
        public static UIElement CurrentFocusedTextControl
        {
            get
            {
                if (_lastFocusedTextControl != null && _lastFocusedTextControl.IsAlive)
                {
                    return _lastFocusedTextControl.Target as UIElement;
                }
                return null;
            }
            private set
            {
                _lastFocusedTextControl = new WeakReference(value);
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] 포커스된 컨트롤 업데이트: {value?.GetType().Name ?? "null"}");
            }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 여러 윈도우에서 가상 키보드 매니저 초기화 (개선된 버전)
        /// </summary>
        public static void Initialize(Window window)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] {window.GetType().Name} 초기화 시작...");

                if (window != null && !_registeredWindows.Contains(window))
                {
                    // 윈도우를 등록된 목록에 추가
                    _registeredWindows.Add(window);

                    // 이벤트 연결
                    window.PreviewGotKeyboardFocus += OnTextControlGotFocus;
                    window.PreviewLostKeyboardFocus += OnTextControlLostFocus;
                    window.Closed += OnWindowClosed;

                    System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ✅ {window.GetType().Name} 이벤트 연결 완료");
                }

                // 첫 번째 윈도우일 때만 테스트 표시
                if (_registeredWindows.Count == 1)
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] 🧪 테스트용 키보드 표시");
                    TestShow();
                }

                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ✅ {window.GetType().Name} 초기화 완료 (총 {_registeredWindows.Count}개 윈도우)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ 초기화 오류: {ex.Message}");
            }
        }
        #endregion

        #region Keyboard Management
        /// <summary>
        /// 테스트용 키보드 표시
        /// </summary>
        public static void TestShow()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] 🧪 테스트 표시 시작");

                if (_virtualKeyboard == null)
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] 새 키보드 인스턴스 생성");
                    _virtualKeyboard = new VirtualKeyboard();

                    // 🔥 키보드에 텍스트 입력 콜백 설정
                    _virtualKeyboard.SetTextInputCallback(SendTextToFocusedControl);
                    _virtualKeyboard.SetBackspaceCallback(SendBackspaceToFocusedControl);
                }

                if (!_virtualKeyboard.IsVisible)
                {
                    _virtualKeyboard.Show();
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ✅ 키보드 표시됨");
                }

                // 3초 후 자동 숨김
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(3);
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    _virtualKeyboard?.Hide();
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] 🧪 테스트 키보드 숨김");
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ 테스트 표시 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 가상 키보드 표시
        /// </summary>
        public static void Show()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] 📱 키보드 표시 요청");

                if (_virtualKeyboard == null)
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] 새 키보드 인스턴스 생성");
                    _virtualKeyboard = new VirtualKeyboard();

                    // 🔥 키보드에 텍스트 입력 콜백 설정
                    _virtualKeyboard.SetTextInputCallback(SendTextToFocusedControl);
                    _virtualKeyboard.SetBackspaceCallback(SendBackspaceToFocusedControl);
                }

                if (!_virtualKeyboard.IsVisible)
                {
                    _virtualKeyboard.Show();
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ✅ 키보드 표시 성공");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ⚠️ 키보드가 이미 표시중");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ 키보드 표시 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 가상 키보드 숨김
        /// </summary>
        public static void Hide()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] 📱 키보드 숨김 요청");

                if (_virtualKeyboard != null && _virtualKeyboard.IsVisible)
                {
                    _virtualKeyboard.Hide();
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ✅ 키보드 숨김 성공");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ⚠️ 키보드가 이미 숨겨짐 또는 null");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ 키보드 숨김 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 수동 키보드 토글 (테스트용)
        /// </summary>
        public static void Toggle()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] 🔄 키보드 토글");

                if (_virtualKeyboard != null && _virtualKeyboard.IsVisible)
                {
                    Hide();
                }
                else
                {
                    Show();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ 키보드 토글 오류: {ex.Message}");
            }
        }
        #endregion

        #region Text Input Handling
        /// <summary>
        /// 🔥 기억된 포커스 컨트롤에 텍스트 전송 (핵심 수정 부분)
        /// </summary>
        public static void SendTextToFocusedControl(string text)
        {
            try
            {
                var targetControl = CurrentFocusedTextControl;

                if (targetControl == null)
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ⚠️ 포커스된 텍스트 컨트롤이 없음");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] 텍스트 입력: '{text}' → {targetControl.GetType().Name}");

                // TextBox 처리
                if (targetControl is TextBox textBox)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            int caretIndex = textBox.CaretIndex;
                            textBox.Text = textBox.Text.Insert(caretIndex, text);
                            textBox.CaretIndex = caretIndex + text.Length;

                            // 🔥 포커스를 다시 TextBox로 설정
                            textBox.Focus();

                            System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ✅ TextBox에 텍스트 입력 완료: '{text}'");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ TextBox 텍스트 입력 오류: {ex.Message}");
                        }
                    }));
                }
                // PasswordBox 처리
                else if (targetControl is PasswordBox passwordBox)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            passwordBox.Password += text;

                            // 🔥 포커스를 다시 PasswordBox로 설정
                            passwordBox.Focus();

                            System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ✅ PasswordBox에 텍스트 입력 완료: '{text}'");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ PasswordBox 텍스트 입력 오류: {ex.Message}");
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ 텍스트 전송 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 🔥 백스페이스 처리
        /// </summary>
        public static void SendBackspaceToFocusedControl()
        {
            try
            {
                var targetControl = CurrentFocusedTextControl;

                if (targetControl == null)
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ⚠️ 포커스된 텍스트 컨트롤이 없음 (백스페이스)");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] 백스페이스 처리 → {targetControl.GetType().Name}");

                // TextBox 처리
                if (targetControl is TextBox textBox)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (textBox.Text.Length > 0 && textBox.CaretIndex > 0)
                            {
                                int caretIndex = textBox.CaretIndex;
                                textBox.Text = textBox.Text.Remove(caretIndex - 1, 1);
                                textBox.CaretIndex = caretIndex - 1;
                            }

                            // 포커스 유지
                            textBox.Focus();

                            System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ✅ TextBox 백스페이스 처리 완료");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ TextBox 백스페이스 오류: {ex.Message}");
                        }
                    }));
                }
                // PasswordBox 처리
                else if (targetControl is PasswordBox passwordBox)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (passwordBox.Password.Length > 0)
                            {
                                passwordBox.Password = passwordBox.Password.Substring(0, passwordBox.Password.Length - 1);
                            }

                            // 포커스 유지
                            passwordBox.Focus();

                            System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ✅ PasswordBox 백스페이스 처리 완료");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ PasswordBox 백스페이스 오류: {ex.Message}");
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ 백스페이스 처리 오류: {ex.Message}");
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 텍스트 컨트롤 포커스 획득 이벤트
        /// </summary>
        private static void OnTextControlGotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] 🎯 포커스 획득 이벤트 ({sender.GetType().Name})");
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] 새 포커스: {e.NewFocus?.GetType().Name ?? "null"}");

                if (e.NewFocus is TextBox textBox)
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ✅ TextBox 포커스 - 키보드 표시");
                    CurrentFocusedTextControl = textBox; // 포커스된 컨트롤 기억
                    Show();
                }
                else if (e.NewFocus is PasswordBox passwordBox)
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ✅ PasswordBox 포커스 - 키보드 표시");
                    CurrentFocusedTextControl = passwordBox; // 포커스된 컨트롤 기억
                    Show();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ⚠️ 텍스트 컨트롤이 아님: {e.NewFocus?.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ 포커스 획득 처리 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 텍스트 컨트롤 포커스 해제 이벤트
        /// </summary>
        private static void OnTextControlLostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] 🎯 포커스 해제 이벤트 ({sender.GetType().Name})");
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] 새 포커스: {e.NewFocus?.GetType().Name ?? "null"}");

                // 가상 키보드 버튼으로 포커스가 이동한 경우에는 키보드를 숨기지 않음
                if (e.NewFocus != null &&
                    (_virtualKeyboard != null && IsVirtualKeyboardElement(e.NewFocus)))
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ⚠️ 가상 키보드 버튼으로 포커스 이동 - 키보드 유지");
                    return;
                }

                // 다른 텍스트 입력 컨트롤로 이동하지 않은 경우에만 키보드 숨김
                if (!(e.NewFocus is TextBox) && !(e.NewFocus is PasswordBox))
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ✅ 텍스트 컨트롤이 아님 - 키보드 숨김");

                    // 딜레이를 주어서 버튼 클릭이 완료된 후 숨김
                    var timer = new System.Windows.Threading.DispatcherTimer();
                    timer.Interval = TimeSpan.FromMilliseconds(100);
                    timer.Tick += (s, args) =>
                    {
                        timer.Stop();

                        // 다시 한번 확인 후 숨김
                        if (!(Keyboard.FocusedElement is TextBox) && !(Keyboard.FocusedElement is PasswordBox))
                        {
                            Hide();
                            CurrentFocusedTextControl = null; // 포커스 컨트롤 초기화
                        }
                    };
                    timer.Start();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ⚠️ 다른 텍스트 컨트롤로 이동 - 키보드 유지");

                    // 새로운 텍스트 컨트롤로 포커스 업데이트
                    if (e.NewFocus is TextBox || e.NewFocus is PasswordBox)
                    {
                        CurrentFocusedTextControl = e.NewFocus as UIElement;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ 포커스 해제 처리 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 요소가 가상 키보드의 일부인지 확인
        /// </summary>
        private static bool IsVirtualKeyboardElement(IInputElement element)
        {
            try
            {
                if (_virtualKeyboard == null || element == null)
                    return false;

                // DependencyObject로 캐스팅
                if (element is DependencyObject depObj)
                {
                    // 부모를 타고 올라가면서 VirtualKeyboard 윈도우인지 확인
                    DependencyObject parent = depObj;
                    while (parent != null)
                    {
                        if (parent == _virtualKeyboard)
                            return true;

                        parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ 가상 키보드 요소 확인 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 윈도우 닫기 이벤트
        /// </summary>
        private static void OnWindowClosed(object sender, EventArgs e)
        {
            try
            {
                if (sender is Window window)
                {
                    System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] 🚪 {window.GetType().Name} 윈도우 닫힘");

                    // 등록된 윈도우 목록에서 제거
                    _registeredWindows.Remove(window);

                    // 모든 윈도우가 닫혔으면 키보드도 정리
                    if (_registeredWindows.Count == 0 && _virtualKeyboard != null)
                    {
                        _virtualKeyboard.Close();
                        _virtualKeyboard = null;
                        CurrentFocusedTextControl = null; // 포커스 컨트롤 초기화
                        System.Diagnostics.Debug.WriteLine("[VirtualKeyboardManager] ✅ 모든 윈도우 닫힘 - 키보드 정리 완료");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualKeyboardManager] ❌ 윈도우 닫기 처리 오류: {ex.Message}");
            }
        }
        #endregion
    }
}