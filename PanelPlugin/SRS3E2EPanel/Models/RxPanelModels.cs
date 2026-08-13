using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;

namespace SRS3.E2E.PanelControl.Models
{
    public sealed class RxEventRow
    {
        public RxEventRow(DateTime timestamp, RxGroupRow group, string previousState, string currentState, int counter, int errorCount)
        {
            TimeText = timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            Bus = group.Bus;
            CanId = group.CanId;
            Group = group.Group;
            Transition = previousState + " → " + currentState;
            Detail = "Counter " + counter.ToString(CultureInfo.InvariantCulture) + " · 异常累计 " + errorCount.ToString(CultureInfo.InvariantCulture);
        }
        public string TimeText { get; private set; }
        public string Bus { get; private set; }
        public string CanId { get; private set; }
        public string Group { get; private set; }
        public string Transition { get; private set; }
        public string Detail { get; private set; }
    }

    public sealed class RxElementDefinition
    {
        public RxElementDefinition(string name, string signal, int startBit, int bitLength, bool signed)
        {
            Name = name;
            Signal = signal;
            StartBit = startBit;
            BitLength = bitLength;
            Signed = signed;
        }

        public string Name { get; private set; }
        public string Signal { get; private set; }
        public int StartBit { get; private set; }
        public int BitLength { get; private set; }
        public bool Signed { get; private set; }
    }

    public sealed class RxElementRow
    {
        public RxElementRow(RxElementDefinition definition, long raw)
        {
            Name = definition.Name;
            Signal = definition.Signal;
            RawText = raw.ToString(CultureInfo.InvariantCulture) + " / 0x" + unchecked((ulong)raw).ToString("X", CultureInfo.InvariantCulture);
            LayoutText = "start " + definition.StartBit + " / " + definition.BitLength + " bit / Motorola" + (definition.Signed ? " / signed" : string.Empty);
        }
        public string Name { get; private set; }
        public string Signal { get; private set; }
        public string RawText { get; private set; }
        public string LayoutText { get; private set; }
    }

    public sealed class RxGroupRow : BindableBase
    {
        private int state;
        private int frames;
        private int ageMs;
        private int counter;
        private int crcRx;
        private int crcCalc;
        private int ub;
        private int validCount;
        private int errorCount;

        public RxGroupRow(int index, string bus, string canId, string frame, string group, int dlc,
            int cycleMs, int timeoutMs, int dataId, int maxDelta, int ubStartBit, int crcStartBit,
            int counterStartBit, RxElementDefinition[] elements)
        {
            Index = index; Bus = bus; CanId = canId; Frame = frame; Group = group; Dlc = dlc;
            CycleMs = cycleMs; TimeoutMs = timeoutMs; DataId = dataId; MaxDelta = maxDelta;
            UbStartBit = ubStartBit; CrcStartBit = crcStartBit; CounterStartBit = counterStartBit;
            Elements = new ReadOnlyCollection<RxElementDefinition>(elements);
        }

        public int Index { get; private set; }
        public string Bus { get; private set; }
        public string CanId { get; private set; }
        public string Frame { get; private set; }
        public string Group { get; private set; }
        public int Dlc { get; private set; }
        public int CycleMs { get; private set; }
        public int TimeoutMs { get; private set; }
        public int DataId { get; private set; }
        public int MaxDelta { get; private set; }
        public int UbStartBit { get; private set; }
        public int CrcStartBit { get; private set; }
        public int CounterStartBit { get; private set; }
        public ReadOnlyCollection<RxElementDefinition> Elements { get; private set; }
        public string TimingText { get { return CycleMs + " ms"; } }
        public int RowNumber { get { return Index + 1; } }
        public string DataIdText { get { return "0x" + DataId.ToString("X4", CultureInfo.InvariantCulture); } }
        public string LayoutText { get { return "CRC s" + CrcStartBit + " | CNT s" + CounterStartBit + " | UB s" + UbStartBit; } }

        public int State { get { return state; } }
        public int Frames { get { return frames; } }
        public int AgeMs { get { return ageMs; } }
        public int Counter { get { return counter; } }
        public int CrcRx { get { return crcRx; } }
        public int CrcCalc { get { return crcCalc; } }
        public int Ub { get { return ub; } }
        public int ValidCount { get { return validCount; } }
        public int ErrorCount { get { return errorCount; } }
        public string StateText { get { return RxState.Text(state); } }
        public Brush StateBrush { get { return RxState.Brush(state); } }

        public void Update(int newState, int newFrames, int newAge, int newCounter, int newCrcRx,
            int newCrcCalc, int newUb, int newValid, int newError)
        {
            state = newState; frames = newFrames; ageMs = newAge; counter = newCounter;
            crcRx = newCrcRx; crcCalc = newCrcCalc; ub = newUb; validCount = newValid; errorCount = newError;
            RaisePropertyChanged("State"); RaisePropertyChanged("Frames"); RaisePropertyChanged("AgeMs");
            RaisePropertyChanged("Counter"); RaisePropertyChanged("CrcRx"); RaisePropertyChanged("CrcCalc");
            RaisePropertyChanged("Ub"); RaisePropertyChanged("ValidCount"); RaisePropertyChanged("ErrorCount");
            RaisePropertyChanged("StateText"); RaisePropertyChanged("StateBrush");
        }
    }

    public sealed class RxPayloadByte
    {
        public RxPayloadByte(int index, int value, string role)
        { Index = index; Value = value; Role = role; }
        public int Index { get; private set; }
        public int Value { get; private set; }
        public string IndexText { get { return "B" + Index.ToString("D2", CultureInfo.InvariantCulture); } }
        public string ValueText { get { return "0x" + Value.ToString("X2", CultureInfo.InvariantCulture); } }
        public string Role { get; private set; }
        public string DisplayRole { get { return Role == "DATA" ? string.Empty : Role; } }
        public Brush RoleBrush
        {
            get
            {
                string color = Role.Contains("CRC") ? "#D84B43" : Role.Contains("CNT") ? "#4E7FD1" : Role.Contains("UB") ? "#D8A51F" : "#C6CCD1";
                Brush brush = (Brush)new BrushConverter().ConvertFromString(color);
                if (brush.CanFreeze) brush.Freeze();
                return brush;
            }
        }
    }

    internal static class RxState
    {
        private static readonly string[] Names = { "无数据", "初始帧", "正常", "允许丢帧", "重复帧", "序列错误", "CRC错误", "UB未激活", "超时", "Counter非法", "DLC错误", "规则错误" };
        internal static string Text(int value) { return value >= 0 && value < Names.Length ? Names[value] : "未知"; }
        internal static Brush Brush(int value)
        {
            string color = (value == 1 || value == 2 || value == 3) ? "#247A45" :
                (value == 0 || value == 7) ? "#6B737A" : (value == 4 ? "#B27A00" : "#C53B32");
            Brush brush = (Brush)new BrushConverter().ConvertFromString(color);
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }
    }
}
