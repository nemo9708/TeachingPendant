using System;

namespace TeachingPendant.Manager
{
    /// <summary>
    /// 전역 I/O 제어 매니저
    /// </summary>
    public static class IOController
    {
        /// <summary>
        /// I/O 상태 변경 이벤트
        /// </summary>
        public static event EventHandler<IOStateChangedEventArgs> IOStateChanged;

        /// <summary>
        /// Output 제어
        /// </summary>
        public static void SetOutput(string outputName, bool state)
        {
            System.Diagnostics.Debug.WriteLine($"IOController: {outputName} → {(state ? "ON" : "OFF")}");

            // Monitor에 I/O 변경 알림
            IOStateChanged?.Invoke(null, new IOStateChangedEventArgs(outputName, state, true));
        }

        /// <summary>
        /// Input 제어 (시뮬레이션용)
        /// </summary>
        public static void SetInput(string inputName, bool state)
        {
            System.Diagnostics.Debug.WriteLine($"IOController: {inputName} → {(state ? "ON" : "OFF")}");

            // Monitor에 I/O 변경 알림
            IOStateChanged?.Invoke(null, new IOStateChangedEventArgs(inputName, state, false));
        }
    }

    /// <summary>
    /// I/O 상태 변경 이벤트 인자
    /// </summary>
    public class IOStateChangedEventArgs : EventArgs
    {
        public string SignalName { get; }
        public bool IsActive { get; }
        public bool IsOutput { get; }

        public IOStateChangedEventArgs(string signalName, bool isActive, bool isOutput)
        {
            SignalName = signalName;
            IsActive = isActive;
            IsOutput = isOutput;
        }
    }
}