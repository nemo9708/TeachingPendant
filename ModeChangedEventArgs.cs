using System;
using TeachingPendant.Manager;

namespace TeachingPendant
{
    /// <summary>
    /// 모드 변경 이벤트 인자
    /// </summary>
    public class ModeChangedEventArgs : EventArgs
    {
        public GlobalMode NewMode { get; }
        public GlobalMode OldMode { get; }

        public bool IsManualMode { get => NewMode == GlobalMode.Manual; }
        public string ModeName { get => NewMode.ToString(); }

        public ModeChangedEventArgs(GlobalMode newMode, GlobalMode oldMode)
        {
            NewMode = newMode;
            OldMode = oldMode;
        }
    }
}