using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SRS3.E2E.PanelControl.Models;
using Vector.PanelControlPlugin;

namespace SRS3.E2E.PanelControl
{
    [System.Drawing.ToolboxBitmap(typeof(E2EConsoleControl), "SRS3.E2E.PanelControl.Resources.E2EControl.png")]
    public partial class E2EConsoleControl : UserControl, IPluginPanelControl, IProvidesSupportedDataTypes, INotifyPropertyChanged
    {
        private const int TxBridgeLength = 64;
        private const int UnifiedBridgeLength = 320;
        private const int ProtocolVersion = 2;
        private const int SelectedPduIndex = 1;
        private const int UbCommandIndex = 3;
        private const int ProtectCommandIndex = 4;
        private const int ConfigCommandIndex = 6;
        private const int StateIndex = 8;
        private const int CounterIndex = 9;
        private const int DataIdIndex = 10;
        private const int CrcCalcIndex = 11;
        private const int CrcTxIndex = 12;
        private const int UbIndex = 13;
        private const int InputRawBase = 14;
        private const int FinalRawBase = 22;
        private const int ProtectedFramesIndex = 30;
        private const int BridgeActiveIndex = 31;
        private const int ProtectBase = 32;
        private const int UbBase = 37;
        private const int PduStateBase = 42;
        private const int PduCountBase = 47;
        private const int FaultTypeIndex = 52;
        private const int FaultModeIndex = 53;
        private const int FaultParameterIndex = 54;
        private const int FaultDurationIndex = 55;
        private const int FaultCommandIndex = 56;
        private const int FaultSequenceIndex = 57;
        private const int FaultActiveTypeIndex = 58;
        private const int FaultRemainingIndex = 59;
        private const int FaultAppliedIndex = 60;
        private const int FaultResultIndex = 61;
        private const int FaultActiveMaskIndex = 62;
        private const int FaultAckIndex = 63;

        private IExchangeSymbolValue symbolValue;
        private System.Windows.Forms.Control externalControl;
        private HwndSource hwndSource;
        private readonly DispatcherTimer pollTimer;
        private PduDefinition currentPdu;
        private bool initializeCalled;
        private bool bridgeConnected;
        private bool bridgeReady;
        private bool readingBridge;
        private int pendingConfigurationPdu = -1;
        private bool pendingProtectionEnabled;
        private int pendingUbMode;
        private string bindingType = "not assigned";
        private int bindingLength;
        private string bridgeError = string.Empty;
        private int state;
        private int counter;
        private int dataId;
        private int crcCalculated;
        private int crcTransmitted;
        private int ub;
        private int protectedFrames;
        private int selectedFaultType = 1;
        private int faultMode;
        private string faultParameterText = "0x01";
        private string faultDurationText = "1";
        private int activeFaultType;
        private int faultRemaining;
        private int faultApplied;
        private int faultResult;
        private int faultSequence;
        private int faultAck;
        private string title = "SRS3 E2E Test Console";

        public E2EConsoleControl()
        {
            Pdus = PanelMetadata.CreatePdus();
            UbOptions = PanelMetadata.CreateUbOptions();
            FaultOptions = new ObservableCollection<OptionItem>
            {
                new OptionItem(1, "破坏CRC"), new OptionItem(2, "冻结Counter"),
                new OptionItem(3, "Counter=15"), new OptionItem(4, "Counter跳变"),
                new OptionItem(5, "错误DataID参与CRC"), new OptionItem(6, "强制UB=0"),
                new OptionItem(7, "破坏Payload"), new OptionItem(8, "抑制发送事件")
            };
            FaultModeOptions = new ObservableCollection<OptionItem>
            {
                new OptionItem(0, "仅下一帧"), new OptionItem(1, "指定帧数"), new OptionItem(2, "持续生效")
            };
            InputPayloadBytes = new ObservableCollection<PayloadByte>(Enumerable.Range(0, 8).Select(i => new PayloadByte(i)));
            FinalPayloadBytes = new ObservableCollection<PayloadByte>(Enumerable.Range(0, 8).Select(i => new PayloadByte(i)));
            pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            pollTimer.Tick += delegate { ReadBridgeValue(); };
            InitializeComponent();
            DataContext = this;
            CurrentPdu = Pdus.First(item => item.Index == 2);
            ResetDisconnectedData();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<PduDefinition> Pdus { get; private set; }
        public ObservableCollection<OptionItem> UbOptions { get; private set; }
        public ObservableCollection<OptionItem> FaultOptions { get; private set; }
        public ObservableCollection<OptionItem> FaultModeOptions { get; private set; }
        public ObservableCollection<PayloadByte> InputPayloadBytes { get; private set; }
        public ObservableCollection<PayloadByte> FinalPayloadBytes { get; private set; }

        public PduDefinition CurrentPdu
        {
            get { return currentPdu; }
            set
            {
                if (value == null || value == currentPdu) return;
                currentPdu = value;
                ResetSelectedTelemetry();
                WriteSelectedPdu();
                UpdatePayloadRoles();
                Raise("CurrentPdu"); RaiseMetrics(); RaiseFaultState(); RaiseConfigurationState();
            }
        }

        public int SelectedFaultType
        {
            get { return selectedFaultType; }
            set
            {
                if (selectedFaultType == value) return;
                selectedFaultType = value;
                faultParameterText = value == 7 ? "0x0001" : value == 4 ? "2" : "0x01";
                Raise("SelectedFaultType"); Raise("FaultParameterText"); RaiseFaultState();
            }
        }
        public int FaultMode { get { return faultMode; } set { if (faultMode == value) return; faultMode = value; Raise("FaultMode"); Raise("FaultDurationEnabled"); } }
        public string FaultParameterText { get { return faultParameterText; } set { faultParameterText = value; Raise("FaultParameterText"); } }
        public string FaultDurationText { get { return faultDurationText; } set { faultDurationText = value; Raise("FaultDurationText"); } }
        public bool FaultDurationEnabled { get { return FaultMode == 1; } }
        public bool CanArmFault
        {
            get
            {
                bool needsProtection = SelectedFaultType >= 2 && SelectedFaultType <= 6;
                return bridgeReady && faultSequence == faultAck && CurrentPdu != null && (!needsProtection || CurrentPdu.ProtectionEnabled);
            }
        }
        public bool CanClearFault { get { return bridgeReady && activeFaultType != 0 && faultSequence == faultAck; } }
        public bool CanClearAllFaults { get { return bridgeReady && Pdus.Any(item => item.FaultActive) && faultSequence == faultAck; } }
        public bool CanConfigure { get { return bridgeReady && pendingConfigurationPdu < 0; } }
        public bool IsConfigurationLocked { get { return !CanConfigure; } }

        public string ProtectionState { get { return StateToText(state); } }
        public Brush StateBrush { get { return StateToBrush(state); } }
        public string CounterText { get { return "0x" + counter.ToString("X", CultureInfo.InvariantCulture) + " (" + counter + ")"; } }
        public string CrcText { get { return "0x" + crcCalculated.ToString("X2") + " / 0x" + crcTransmitted.ToString("X2"); } }
        public string UbText { get { return ub == 0 ? "0 未激活" : "1 已激活"; } }
        public string DataIdText { get { return "0x" + (dataId == 0 && CurrentPdu != null ? CurrentPdu.DataId : dataId).ToString("X4"); } }
        public string ProtectedFramesText { get { return protectedFrames.ToString(CultureInfo.InvariantCulture); } }
        public string E2eLayoutText { get { return CurrentPdu == null ? "--" : CurrentPdu.E2eLayoutText; } }
        public string FaultStatusText
        {
            get
            {
                if (!bridgeReady) return "桥接未就绪：故障命令不会写入。";
                if (activeFaultType == 0)
                {
                    if (faultResult == 4) return "上次命令被拒绝：该故障要求当前PDU启用E2E保护。";
                    return "未注入故障。Panel只等待PDU IG后续事件，不会主动创建报文。";
                }
                string remaining = faultRemaining < 0 ? "持续" : faultRemaining + "帧";
                return "当前：" + FaultTypeToText(activeFaultType) + "；剩余 " + remaining + "；已作用 " + faultApplied + " 次。";
            }
        }

        public string BridgeStatus
        {
            get
            {
                if (!initializeCalled) return "未绑定";
                if (!bridgeConnected) return "绑定无效 / 需要Int32[320]";
                if (!bridgeReady) return "已绑定 / 等待CAPL";
                return "TX保护 + RX监控 已就绪";
            }
        }
        public Brush BridgeStatusBackground { get { return bridgeReady ? BrushFrom("#E7F3EB") : BrushFrom("#FFF4DA"); } }
        public Brush BridgeStatusBorder { get { return bridgeReady ? BrushFrom("#6DA781") : BrushFrom("#D0A237"); } }
        public Brush BridgeStatusForeground { get { return bridgeReady ? BrushFrom("#28653E") : BrushFrom("#765A17"); } }
        public string BridgeDiagnostic
        {
            get
            {
                if (!string.IsNullOrEmpty(bridgeError)) return bridgeError;
                if (!initializeCalled) return "尚未分配 CANoe Symbol。";
                if (symbolValue == null) return "SymbolValue 为 null。";
                if (symbolValue.SymbolDataType != ExchangeSymbolDataType.LongArray) return "已分配类型：" + bindingType + "；要求 LongArray。";
                if (bindingLength < UnifiedBridgeLength) return "LongArray 长度：" + bindingLength + "；统一控制台要求至少 " + UnifiedBridgeLength + "。";
                if (!bridgeConnected) return "Bridge 协议版本不匹配；要求 v" + ProtocolVersion + "。";
                if (!bridgeReady) return "Symbol 已连接，等待 CAPL 将 BridgeActive 置 1。";
                return "统一 Tx/Rx Bridge 已连接。";
            }
        }
        public string FooterText
        {
            get
            {
                if (!bridgeConnected) return "安全状态：请绑定 SRS3_E2E::WpfBridge::PanelBridge（Int32[320]）。";
                if (!bridgeReady) return "安全状态：等待CAPL保护适配器；Panel不会触发或周期发送任何PDU。";
                return "运行边界：PDU IG负责数值与触发；本控制台只保护/故障处理其后续事件，并监控接收结果。";
            }
        }
        public Brush FooterBrush { get { return bridgeReady ? BrushFrom("#365F45") : BrushFrom("#8A6512"); } }

        private void ReadBridgeValue()
        {
            try
            {
                if (symbolValue == null || symbolValue.SymbolDataType != ExchangeSymbolDataType.LongArray)
                {
                    bindingLength = 0; bridgeError = string.Empty; bridgeConnected = false; bridgeReady = false;
                    ResetDisconnectedData(); RaiseMetrics(); RaiseFaultState(); RaiseStatus(); return;
                }
                int[] values = symbolValue.LongArray.ToArray();
                bindingLength = values.Length; bridgeError = string.Empty;
                bridgeConnected = values.Length >= UnifiedBridgeLength && values[0] == ProtocolVersion;
                bridgeReady = bridgeConnected && values[0] == ProtocolVersion && values[BridgeActiveIndex] == 1;
                if (!bridgeConnected)
                {
                    ResetDisconnectedData();
                    RaiseMetrics(); RaiseFaultState(); RaiseStatus();
                    return;
                }

                readingBridge = true;
                for (int index = 0; index < Pdus.Count; index++)
                {
                    PduDefinition pdu = Pdus[index];
                    bool keepEditedValue = pdu.IsConfigurationDirty || pdu.ConfigurationPending;
                    pdu.ProtectionEnabled = values[ProtectBase + index] != 0;
                    pdu.UbMode = values[UbBase + index];
                    if (!keepEditedValue) pdu.AcceptReadbackConfiguration();
                    pdu.ProtectionState = values[PduStateBase + index];
                    pdu.ProtectedFrames = values[PduCountBase + index];
                    pdu.FaultActive = (values[FaultActiveMaskIndex] & (1 << index)) != 0;
                }
                if (CurrentPdu != null && values[DataIdIndex] == CurrentPdu.DataId)
                {
                    state = values[StateIndex]; counter = values[CounterIndex]; dataId = values[DataIdIndex];
                    crcCalculated = values[CrcCalcIndex] & 0xFF; crcTransmitted = values[CrcTxIndex] & 0xFF;
                    ub = values[UbIndex] != 0 ? 1 : 0; protectedFrames = values[ProtectedFramesIndex];
                    for (int index = 0; index < 8; index++)
                    {
                        InputPayloadBytes[index].Value = values[InputRawBase + index] & 0xFF;
                        FinalPayloadBytes[index].Value = values[FinalRawBase + index] & 0xFF;
                    }
                }
                activeFaultType = values[FaultActiveTypeIndex]; faultRemaining = values[FaultRemainingIndex];
                faultApplied = values[FaultAppliedIndex]; faultResult = values[FaultResultIndex]; faultAck = values[FaultAckIndex];
                if (faultSequence == 0 && values[FaultCommandIndex] == 0) faultSequence = faultAck;
                readingBridge = false;
                UpdateConfigurationAcknowledgement(values);
                RaiseMetrics(); RaiseFaultState(); RaiseStatus(); RaiseConfigurationState();
            }
            catch (Exception exception) { readingBridge = false; bridgeConnected = false; bridgeReady = false; bridgeError = exception.GetType().Name + ": " + exception.Message; ResetDisconnectedData(); RaiseMetrics(); RaiseStatus(); }
        }

        private void WriteSelectedPdu()
        {
            if (readingBridge || !bridgeConnected || symbolValue == null || CurrentPdu == null || symbolValue.SymbolDataType != ExchangeSymbolDataType.LongArray) return;
            int[] values = symbolValue.LongArray.ToArray();
            if (values.Length < UnifiedBridgeLength || values[SelectedPduIndex] == CurrentPdu.Index) return;
            values[SelectedPduIndex] = CurrentPdu.Index;
            symbolValue.LongArray = values;
        }

        private void ApplyConfigurationButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            PduDefinition pdu = button == null ? null : button.DataContext as PduDefinition;
            if (!CanConfigure || pdu == null || symbolValue == null || symbolValue.SymbolDataType != ExchangeSymbolDataType.LongArray) return;
            CurrentPdu = pdu;
            int[] values = symbolValue.LongArray.ToArray();
            if (values.Length < TxBridgeLength) return;
            pendingConfigurationPdu = pdu.Index;
            pendingProtectionEnabled = pdu.RequestedProtectionEnabled;
            pendingUbMode = pdu.RequestedUbMode;
            pdu.ConfigurationPending = true;
            values[SelectedPduIndex] = pendingConfigurationPdu;
            values[ProtectCommandIndex] = pendingProtectionEnabled ? 1 : 0;
            values[UbCommandIndex] = pendingUbMode;
            values[ConfigCommandIndex] = 1;
            symbolValue.LongArray = values;
            RaiseStatus();
        }

        private void UpdateConfigurationAcknowledgement(int[] values)
        {
            if (pendingConfigurationPdu < 0 || values[ConfigCommandIndex] != 0) return;
            int appliedPdu = pendingConfigurationPdu;
            bool matched = (values[ProtectBase + appliedPdu] != 0) == pendingProtectionEnabled
                && values[UbBase + appliedPdu] == pendingUbMode;
            Pdus[appliedPdu].ConfigurationPending = false;
            pendingConfigurationPdu = -1;
            if (matched) Pdus[appliedPdu].AcceptReadbackConfiguration();
            RaiseStatus();
        }

        private void SendFaultCommand(int command)
        {
            if (!bridgeReady || symbolValue == null || CurrentPdu == null || symbolValue.SymbolDataType != ExchangeSymbolDataType.LongArray) return;
            int[] values = symbolValue.LongArray.ToArray();
            if (values.Length < UnifiedBridgeLength) return;
            int parameter; int duration;
            if (!TryParseInteger(FaultParameterText, out parameter)) parameter = 1;
            if (!TryParseInteger(FaultDurationText, out duration)) duration = 1;
            duration = Math.Max(1, Math.Min(100000, duration));
            faultSequence++;
            values[SelectedPduIndex] = CurrentPdu.Index;
            values[FaultTypeIndex] = SelectedFaultType; values[FaultModeIndex] = FaultMode;
            values[FaultParameterIndex] = parameter; values[FaultDurationIndex] = duration;
            values[FaultSequenceIndex] = faultSequence; values[FaultCommandIndex] = command;
            symbolValue.LongArray = values;
            RaiseFaultState();
        }

        private void ArmFaultButton_Click(object sender, RoutedEventArgs e) { if (CanArmFault) SendFaultCommand(1); }
        private void ClearFaultButton_Click(object sender, RoutedEventArgs e) { if (CanClearFault) SendFaultCommand(2); }
        private void ClearAllFaultsButton_Click(object sender, RoutedEventArgs e) { if (CanClearAllFaults) SendFaultCommand(3); }

        private void UpdatePayloadRoles()
        {
            if (CurrentPdu == null) return;
            foreach (PayloadByte item in InputPayloadBytes) item.SetRole(false, -1, false, -1, false, -1);
            foreach (PayloadByte item in FinalPayloadBytes)
            {
                item.SetRole(item.Index == CurrentPdu.CrcByteIndex, CurrentPdu.CrcStartBit,
                    item.Index == CurrentPdu.CounterByteIndex, CurrentPdu.CounterStartBit,
                    item.Index == CurrentPdu.UbByteIndex, CurrentPdu.UbStartBit);
            }
        }

        private void ResetSelectedTelemetry()
        {
            state = 0; counter = 0; dataId = CurrentPdu == null ? 0 : CurrentPdu.DataId;
            crcCalculated = 0; crcTransmitted = 0; ub = 0; protectedFrames = 0;
            foreach (PayloadByte item in InputPayloadBytes) item.Value = 0;
            foreach (PayloadByte item in FinalPayloadBytes) item.Value = 0;
        }

        private void ResetDisconnectedData()
        {
            readingBridge = true;
            foreach (PduDefinition pdu in Pdus)
            {
                pdu.ProtectionEnabled = false;
                pdu.UbMode = 0;
                pdu.ConfigurationPending = false;
                pdu.AcceptReadbackConfiguration();
                pdu.ProtectionState = 0;
                pdu.ProtectedFrames = 0;
                pdu.FaultActive = false;
            }
            activeFaultType = 0; faultRemaining = 0; faultApplied = 0; faultResult = 0; faultAck = 0;
            ResetSelectedTelemetry();
            readingBridge = false;
        }

        private static bool TryParseInteger(string text, out int value)
        {
            value = 0; if (string.IsNullOrWhiteSpace(text)) return false; string trimmed = text.Trim();
            return trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? int.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
                : int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
        private static string StateToText(int value)
        {
            switch (value) { case 1: return "已保护"; case 3: return "DLC/布局错误"; case 6: return "直通"; case 7: return "故障已作用"; case 8: return "事件已抑制"; default: return "等待PDU IG"; }
        }
        private static Brush StateToBrush(int value) { return value == 1 ? BrushFrom("#247A45") : (value == 3 || value == 7 || value == 8) ? BrushFrom("#C53B32") : BrushFrom("#69727A"); }
        private static string FaultTypeToText(int value)
        {
            switch (value) { case 1: return "破坏CRC"; case 2: return "冻结Counter"; case 3: return "Counter=15"; case 4: return "Counter跳变"; case 5: return "错误DataID"; case 6: return "UB=0"; case 7: return "破坏Payload"; case 8: return "抑制发送事件"; default: return "未注入"; }
        }
        private static Brush BrushFrom(string color) { Brush brush = (Brush)new BrushConverter().ConvertFromString(color); if (brush.CanFreeze) brush.Freeze(); return brush; }
        private void Raise(string name) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name)); }
        private void RaiseMetrics() { foreach (string name in new[] { "ProtectionState", "StateBrush", "CounterText", "CrcText", "UbText", "DataIdText", "ProtectedFramesText", "E2eLayoutText" }) Raise(name); }
        private void RaiseFaultState() { foreach (string name in new[] { "CanArmFault", "CanClearFault", "CanClearAllFaults", "FaultStatusText" }) Raise(name); }
        private void RaiseConfigurationState() { Raise("CanConfigure"); Raise("IsConfigurationLocked"); }
        private void RaiseStatus() { foreach (string name in new[] { "BridgeStatus", "BridgeStatusBackground", "BridgeStatusBorder", "BridgeStatusForeground", "BridgeDiagnostic", "FooterText", "FooterBrush", "CanConfigure", "IsConfigurationLocked" }) Raise(name); RaiseFaultState(); RaiseConfigurationState(); }

        public ExchangeSymbolDataType SupportedDataTypes { get { return ExchangeSymbolDataType.LongArray; } }
        public string ControlName { get { return "SRS3 E2E Test Console"; } }
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
            bridgeConnected = false;
            if (value != null) { value.ValueChanged += OnValueChanged; ReadBridgeValue(); }
            UnifiedRxControl.Initialize(value);
            RaiseStatus();
        }
        private void OnValueChanged(object sender, EventArgs e) { if (Dispatcher.CheckAccess()) ReadBridgeValue(); else Dispatcher.BeginInvoke(new Action(ReadBridgeValue)); }
        public bool SerializeSupportedProperties(out string serializationString) { serializationString = Title.Replace(";", string.Empty) + ";"; return true; }
        public bool DeserializeSupportedProperties(string serializationString) { string[] values = (serializationString ?? string.Empty).Split(';'); if (values.Length > 0 && !string.IsNullOrEmpty(values[0])) Title = values[0]; return true; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            pollTimer.Start(); hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource != null) hwndSource.AddHook(WndProc);
        }
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            pollTimer.Stop(); if (hwndSource != null) hwndSource.RemoveHook(WndProc); hwndSource = null;
        }
        private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == 0x0087) { handled = true; return new IntPtr(0x0004); }
            return IntPtr.Zero;
        }
    }
}
