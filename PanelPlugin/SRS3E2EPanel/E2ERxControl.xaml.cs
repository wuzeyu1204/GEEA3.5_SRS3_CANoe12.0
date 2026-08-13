using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SRS3.E2E.PanelControl.Models;
using Vector.PanelControlPlugin;

namespace SRS3.E2E.PanelControl
{
    [System.Drawing.ToolboxBitmap(typeof(E2ERxControl), "SRS3.E2E.PanelControl.Resources.E2EControl.png")]
    public partial class E2ERxControl : UserControl, IPluginPanelControl, IProvidesSupportedDataTypes, INotifyPropertyChanged
    {
        private const int BridgeLength = 224;
        private const int ProtocolVersion = 2;
        private const int SelectedIndex = 1;
        private const int Can1ReadyIndex = 3;
        private const int CanFd3ReadyIndex = 4;
        private const int SelectedDeltaIndex = 8;
        private const int SelectedAckIndex = 19;
        private const int StatusBase = 20;
        private const int FrameCountBase = 35;
        private const int AgeBase = 50;
        private const int CounterBase = 65;
        private const int CrcRxBase = 80;
        private const int CrcCalcBase = 95;
        private const int UbBase = 110;
        private const int ValidBase = 125;
        private const int ErrorBase = 140;
        private const int PayloadBase = 155;

        private IExchangeSymbolValue symbolValue;
        private System.Windows.Forms.Control externalControl;
        private readonly DispatcherTimer pollTimer;
        private RxGroupRow currentGroup;
        private string currentNetwork;
        private bool initializeCalled;
        private bool bridgeConnected;
        private bool can1Ready;
        private bool canFd3Ready;
        private bool readingBridge;
        private int selectedDelta;
        private string bindingType = "not assigned";
        private string title = "SRS3 E2E Rx Control";

        public E2ERxControl()
        {
            Groups = RxPanelMetadata.CreateGroups();
            NetworkOptions = new ObservableCollection<string>(new[] { "CAN1", "CANFD3" });
            VisibleGroups = new ObservableCollection<RxGroupRow>();
            ElementRows = new ObservableCollection<RxElementRow>();
            PayloadBytes = new ObservableCollection<RxPayloadByte>();
            currentNetwork = "CAN1";
            pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            pollTimer.Tick += delegate { ReadBridgeValue(); };
            InitializeComponent();
            DataContext = this;
            RebuildVisibleGroups();
            Loaded += delegate { pollTimer.Start(); };
            Unloaded += delegate { pollTimer.Stop(); };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<RxGroupRow> Groups { get; private set; }
        public ObservableCollection<string> NetworkOptions { get; private set; }
        public ObservableCollection<RxGroupRow> VisibleGroups { get; private set; }
        public ObservableCollection<RxElementRow> ElementRows { get; private set; }
        public ObservableCollection<RxPayloadByte> PayloadBytes { get; private set; }

        public string CurrentNetwork
        {
            get { return currentNetwork; }
            set
            {
                if (string.IsNullOrEmpty(value) || currentNetwork == value) return;
                currentNetwork = value;
                Raise("CurrentNetwork");
                RebuildVisibleGroups();
                RaiseStatus();
            }
        }

        public RxGroupRow CurrentGroup
        {
            get { return currentGroup; }
            set
            {
                if (value == null || currentGroup == value) return;
                currentGroup = value;
                selectedDelta = 0;
                WriteSelectedGroup();
                RebuildDetail(null);
                Raise("CurrentGroup");
                RaiseDetail();
            }
        }

        public string SelectedStateText { get { return currentGroup == null ? "无选择" : currentGroup.StateText; } }
        public Brush SelectedStateBrush { get { return currentGroup == null ? BrushFrom("#6B737A") : currentGroup.StateBrush; } }
        public string CounterText { get { return currentGroup == null ? "--" : currentGroup.Counter + " / Δ" + selectedDelta; } }
        public string CrcText { get { return currentGroup == null ? "--" : "0x" + currentGroup.CrcRx.ToString("X2") + " / 0x" + currentGroup.CrcCalc.ToString("X2"); } }
        public Brush CrcBrush { get { return currentGroup == null || currentGroup.State == 0 ? BrushFrom("#6B737A") : currentGroup.CrcRx == currentGroup.CrcCalc ? BrushFrom("#247A45") : BrushFrom("#C53B32"); } }
        public string UbText { get { return currentGroup == null ? "--" : currentGroup.Ub + (currentGroup.Ub == 0 ? " 未激活" : " 激活"); } }
        public string AgeText { get { return currentGroup == null ? "--" : currentGroup.AgeMs + " / " + currentGroup.TimeoutMs + " ms"; } }
        public string CountText { get { return currentGroup == null ? "--" : currentGroup.ValidCount + " / " + currentGroup.ErrorCount; } }
        public string LayoutText { get { return currentGroup == null ? "--" : currentGroup.LayoutText; } }
        public int PayloadColumnCount { get { return currentGroup == null ? 8 : currentGroup.Dlc <= 8 ? Math.Max(1, currentGroup.Dlc) : 16; } }
        public string PayloadSummaryText
        {
            get
            {
                if (currentGroup == null) return "--";
                return currentGroup.Dlc + " bytes · 红=CRC  蓝=Counter  黄=UB";
            }
        }
        public string GroupSummaryText
        {
            get { return VisibleGroups.Count + " Groups · " + (IsCurrentNetworkReady ? "实时监听" : "静态定义"); }
        }

        public string NetworkReadyText { get { return IsCurrentNetworkReady ? "监听节点已就绪" : "监听节点未配置/未启动"; } }
        public Brush NetworkReadyBrush { get { return IsCurrentNetworkReady ? BrushFrom("#247A45") : BrushFrom("#B06C00"); } }
        private bool IsCurrentNetworkReady { get { return currentNetwork == "CAN1" ? can1Ready : canFd3Ready; } }

        public string BridgeStatus
        {
            get
            {
                if (!initializeCalled) return "未绑定";
                if (bindingType == ExchangeSymbolDataType.Unknown.ToString()) return "已绑定 / 等待测量";
                if (!bridgeConnected) return "绑定无效";
                return IsCurrentNetworkReady ? "RX MONITOR READY" : "BRIDGE READY / CHANNEL OFF";
            }
        }
        public Brush BridgeStatusBackground { get { return IsCurrentNetworkReady ? BrushFrom("#E7F3EB") : BrushFrom("#FFF4DA"); } }
        public Brush BridgeStatusBorder { get { return IsCurrentNetworkReady ? BrushFrom("#6DA781") : BrushFrom("#D0A237"); } }
        public Brush BridgeStatusForeground { get { return IsCurrentNetworkReady ? BrushFrom("#28653E") : BrushFrom("#765A17"); } }
        public string FooterText
        {
            get
            {
                if (!bridgeConnected) return "只读安全状态：请将控件绑定到 SRS3_E2E::WpfBridge::RxBridge（Int32[224]）。";
                if (!IsCurrentNetworkReady) return "只读安全状态：当前通道没有 Ready 的 CAPL 接收节点；本 Panel 不发送任何报文。";
                return "接收监视中：状态由最后接收帧计算；超时阈值=max(50 ms, 4×周期)；本 Panel 不控制发送。";
            }
        }
        public Brush FooterBrush { get { return IsCurrentNetworkReady ? BrushFrom("#365F45") : BrushFrom("#8A6512"); } }

        private void RebuildVisibleGroups()
        {
            VisibleGroups.Clear();
            foreach (RxGroupRow row in Groups.Where(item => item.Bus == currentNetwork)) VisibleGroups.Add(row);
            CurrentGroup = VisibleGroups.FirstOrDefault();
        }

        private void RebuildDetail(int[] values)
        {
            ElementRows.Clear();
            PayloadBytes.Clear();
            if (currentGroup == null) return;
            byte[] payload = new byte[currentGroup.Dlc];
            bool acknowledged = values != null && values.Length >= BridgeLength && values[SelectedAckIndex] == currentGroup.Index;
            for (int index = 0; index < payload.Length; index++)
            {
                payload[index] = acknowledged ? (byte)(values[PayloadBase + index] & 0xFF) : (byte)0;
                string role = string.Empty;
                if (index == currentGroup.CrcStartBit / 8) role = "CRC";
                if (index == currentGroup.CounterStartBit / 8) role += (role.Length == 0 ? string.Empty : "+") + "CNT";
                if (index == currentGroup.UbStartBit / 8) role += (role.Length == 0 ? string.Empty : "+") + "UB";
                if (role.Length == 0) role = "DATA";
                PayloadBytes.Add(new RxPayloadByte(index, payload[index], role));
            }
            foreach (RxElementDefinition element in currentGroup.Elements)
            {
                uint raw = ReadMotorola(payload, element.StartBit, element.BitLength);
                long display = ToSigned(raw, element.BitLength, element.Signed);
                ElementRows.Add(new RxElementRow(element, display));
            }
            Raise("PayloadBytes"); Raise("ElementRows");
        }

        private static uint ReadMotorola(byte[] data, int startBit, int bitLength)
        {
            uint raw = 0; int position = startBit;
            for (int bit = 0; bit < bitLength; bit++)
            {
                int byteIndex = position / 8; int bitIndex = position % 8;
                if (byteIndex < 0 || byteIndex >= data.Length) return 0;
                raw = (raw << 1) | (uint)((data[byteIndex] >> bitIndex) & 1);
                position = position % 8 == 0 ? position + 15 : position - 1;
            }
            return raw;
        }

        private static long ToSigned(uint raw, int bitLength, bool signed)
        {
            if (!signed || bitLength <= 0) return raw;
            if (bitLength == 32) return unchecked((int)raw);
            uint sign = 1u << (bitLength - 1);
            return (raw & sign) == 0 ? raw : (long)raw - (1L << bitLength);
        }

        private void ReadBridgeValue()
        {
            try
            {
                if (symbolValue == null || symbolValue.SymbolDataType != ExchangeSymbolDataType.LongArray)
                {
                    bindingType = symbolValue == null ? "null" : symbolValue.SymbolDataType.ToString();
                    bridgeConnected = false; can1Ready = false; canFd3Ready = false; RaiseStatus(); return;
                }
                int[] values = symbolValue.LongArray.ToArray();
                if (values.Length < BridgeLength) { bridgeConnected = false; RaiseStatus(); return; }
                bindingType = symbolValue.SymbolDataType.ToString(); bridgeConnected = values[0] == ProtocolVersion;
                can1Ready = values[Can1ReadyIndex] != 0; canFd3Ready = values[CanFd3ReadyIndex] != 0;
                readingBridge = true;
                for (int index = 0; index < Groups.Count; index++)
                {
                    Groups[index].Update(values[StatusBase + index], values[FrameCountBase + index], values[AgeBase + index],
                        values[CounterBase + index], values[CrcRxBase + index], values[CrcCalcBase + index], values[UbBase + index],
                        values[ValidBase + index], values[ErrorBase + index]);
                }
                if (currentGroup != null && values[SelectedAckIndex] == currentGroup.Index) selectedDelta = values[SelectedDeltaIndex];
                RebuildDetail(values);
                readingBridge = false;
                RaiseDetail(); RaiseStatus();
            }
            catch { bridgeConnected = false; readingBridge = false; RaiseStatus(); }
        }

        private void WriteSelectedGroup()
        {
            if (readingBridge || currentGroup == null || symbolValue == null || symbolValue.SymbolDataType != ExchangeSymbolDataType.LongArray) return;
            int[] values = symbolValue.LongArray.ToArray();
            if (values.Length < BridgeLength || values[SelectedIndex] == currentGroup.Index) return;
            values[SelectedIndex] = currentGroup.Index;
            symbolValue.LongArray = values;
        }

        private void RaiseDetail()
        {
            foreach (string name in new[] { "SelectedStateText", "SelectedStateBrush", "CounterText", "CrcText", "CrcBrush", "UbText", "AgeText", "CountText", "LayoutText", "PayloadColumnCount", "PayloadSummaryText" }) Raise(name);
        }
        private void RaiseStatus()
        {
            foreach (string name in new[] { "NetworkReadyText", "NetworkReadyBrush", "BridgeStatus", "BridgeStatusBackground", "BridgeStatusBorder", "BridgeStatusForeground", "FooterText", "FooterBrush", "GroupSummaryText" }) Raise(name);
        }
        private void Raise(string name) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name)); }
        private static Brush BrushFrom(string color) { Brush brush = (Brush)new BrushConverter().ConvertFromString(color); if (brush.CanFreeze) brush.Freeze(); return brush; }

        public ExchangeSymbolDataType SupportedDataTypes { get { return ExchangeSymbolDataType.LongArray; } }
        public string ControlName { get { return "E2E Rx WPF Control"; } }
        public System.Windows.Forms.Control ExternalControl { get { if (externalControl == null) externalControl = new System.Windows.Forms.Integration.ElementHost { Dock = System.Windows.Forms.DockStyle.Fill, Child = this }; return externalControl; } }
        public IExchangeSymbolValue SymbolValue { get { return symbolValue; } set { symbolValue = value; } }
        public IList<string> SupportedProperties { get { return new List<string> { "Title" }; } }
        public bool SupportsPropertyBackColor { get { return true; } }
        public bool SupportsPropertyForeColor { get { return true; } }
        public System.Drawing.Color ControlBackColor { get { return System.Drawing.Color.White; } set { Background = new SolidColorBrush(Color.FromArgb(value.A, value.R, value.G, value.B)); } }
        public System.Drawing.Color ControlForeColor { get { return System.Drawing.Color.Black; } set { Foreground = new SolidColorBrush(Color.FromArgb(value.A, value.R, value.G, value.B)); } }
        public bool Enabled { get { return true; } set { ExternalControl.Enabled = true; } }
        public bool Visible { get { return ExternalControl.Visible; } set { ExternalControl.Visible = value; } }
        [Category("E2E Control Settings")][DisplayName("Title")]
        public string Title { get { return title; } set { title = value ?? string.Empty; } }
        public void Initialize(IExchangeSymbolValue value)
        {
            if (symbolValue != null) symbolValue.ValueChanged -= OnValueChanged;
            symbolValue = value; initializeCalled = true; bindingType = value == null ? "null" : value.SymbolDataType.ToString();
            bridgeConnected = value != null && value.SymbolDataType == ExchangeSymbolDataType.LongArray;
            if (value != null) { value.ValueChanged += OnValueChanged; ReadBridgeValue(); }
            RaiseStatus();
        }
        private void OnValueChanged(object sender, EventArgs args) { if (Dispatcher.CheckAccess()) ReadBridgeValue(); else Dispatcher.BeginInvoke(new Action(ReadBridgeValue)); }
        public bool SerializeSupportedProperties(out string serializationString) { serializationString = Title.Replace(";", string.Empty) + ";"; return true; }
        public bool DeserializeSupportedProperties(string serializationString) { string[] values = (serializationString ?? string.Empty).Split(';'); if (values.Length > 0 && !string.IsNullOrEmpty(values[0])) Title = values[0]; return true; }
    }
}
