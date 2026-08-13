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
    [System.Drawing.ToolboxBitmap(typeof(E2ETxControl), "SRS3.E2E.PanelControl.Resources.E2EControl.png")]
    public partial class E2ETxControl : UserControl, IPluginPanelControl, IProvidesSupportedDataTypes, INotifyPropertyChanged
    {
        private const int BridgeLength = 64;
        private const int BridgeProtocolVersion = 1;
        private const int IndexProtocolVersion = 0;
        private const int IndexSelectedPdu = 1;
        private const int IndexOverrideMode = 2;
        private const int IndexUbMode = 3;
        private const int IndexAutoProtect = 4;
        private const int IndexGlobalEnable = 5;
        private const int IndexCommand = 6;
        private const int IndexState = 8;
        private const int IndexCounter = 9;
        private const int IndexDataId = 10;
        private const int IndexCrc = 11;
        private const int IndexActualUb = 12;
        private const int IndexSignalCount = 13;
        private const int IndexRawBase = 14;
        private const int IndexUseRawBase = 22;
        private const int IndexValidBase = 30;
        private const int IndexBridgeReady = 38;
        private const int IndexPayloadBase = 39;
        private const int IndexSendModeBase = 47;

        private IExchangeSymbolValue symbolValue;
        private System.Windows.Forms.Control externalControl;
        private HwndSource hwndSource;
        private readonly DispatcherTimer bridgePollTimer;
        private PduDefinition currentPdu;
        private ObservableCollection<SignalRow> signalRows;
        private readonly ObservableCollection<SignalRow>[] signalEditors;
        private readonly int[] overrideModeDrafts;
        private readonly int[] ubModeDrafts;
        private readonly bool[] autoProtectDrafts;
        private readonly bool[] signalDraftDirty;
        private readonly bool[] controlDraftDirty;
        private int overrideMode;
        private int ubMode;
        private bool autoProtect;
        private bool globalEnable;
        private bool bridgeConnected;
        private bool bridgeReady;
        private bool initializeCalled;
        private string bindingType = "not assigned";
        private bool readingBridge;
        private bool selectionPending;
        private bool signalEditorDirty;
        private bool controlEditorDirty;
        private string protectionState;
        private int counter;
        private int dataId;
        private int crc;
        private int updateBit;
        private string title = "SRS3 E2E Tx Control";

        public E2ETxControl()
        {
            Pdus = PanelMetadata.CreatePdus();
            OverrideOptions = PanelMetadata.CreateOverrideOptions();
            UbOptions = PanelMetadata.CreateUbOptions();
            PayloadBytes = new ObservableCollection<PayloadByte>(Enumerable.Range(0, 8).Select(index => new PayloadByte(index)));
            signalEditors = Pdus
                .Select(pdu => new ObservableCollection<SignalRow>(pdu.Signals.Select(definition => new SignalRow(definition))))
                .ToArray();
            overrideModeDrafts = new int[Pdus.Count];
            ubModeDrafts = new int[Pdus.Count];
            autoProtectDrafts = Enumerable.Repeat(true, Pdus.Count).ToArray();
            signalDraftDirty = new bool[Pdus.Count];
            controlDraftDirty = new bool[Pdus.Count];
            foreach (ObservableCollection<SignalRow> editor in signalEditors)
            {
                foreach (SignalRow row in editor)
                {
                    row.PropertyChanged += SignalRow_PropertyChanged;
                }
            }
            signalRows = new ObservableCollection<SignalRow>();
            overrideMode = 0;
            ubMode = 0;
            autoProtect = true;
            protectionState = "预览";
            bridgePollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            bridgePollTimer.Tick += BridgePollTimer_Tick;

            InitializeComponent();
            DataContext = this;
            CurrentPdu = Pdus.First(pdu => pdu.Index == 2);
            /* Constructor selection is not a user edit. Allow the first live
             * bridge snapshot to populate its application values. */
            signalEditorDirty = false;
            selectionPending = false;

            Loaded += E2ETxControl_Loaded;
            Unloaded += E2ETxControl_Unloaded;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<PduDefinition> Pdus { get; private set; }
        public ObservableCollection<OptionItem> OverrideOptions { get; private set; }
        public ObservableCollection<OptionItem> UbOptions { get; private set; }
        public ObservableCollection<PayloadByte> PayloadBytes { get; private set; }

        public PduDefinition CurrentPdu
        {
            get { return currentPdu; }
            set
            {
                if (value == null || currentPdu == value)
                {
                    return;
                }

                currentPdu = value;
                SignalRows = signalEditors[currentPdu.Index];
                overrideMode = overrideModeDrafts[currentPdu.Index];
                ubMode = ubModeDrafts[currentPdu.Index];
                autoProtect = autoProtectDrafts[currentPdu.Index];
                /* DataID acknowledgement blocks stale bridge data. Until the
                 * operator edits a row, the newly selected PDU may therefore
                 * accept its own live Raw values after CAPL acknowledges it. */
                signalEditorDirty = signalDraftDirty[currentPdu.Index];
                controlEditorDirty = controlDraftDirty[currentPdu.Index];
                selectionPending = !readingBridge;
                dataId = currentPdu.DataId;
                ProtectionState = "已停止";
                Counter = 0;
                crc = 0;
                UpdateBit = 0;
                foreach (PayloadByte payloadByte in PayloadBytes)
                {
                    payloadByte.Value = 0;
                }
                UpdatePayloadRoles();
                WriteSelectedPduToBridge();
                RaisePanelPropertyChanged("CurrentPdu");
                RaisePanelPropertyChanged("OverrideMode");
                RaisePanelPropertyChanged("UbMode");
                RaisePanelPropertyChanged("AutoProtect");
                RaisePanelPropertyChanged("DataIdText");
                RaisePanelPropertyChanged("CrcText");
                RaisePanelPropertyChanged("E2eLayoutText");
            }
        }

        public ObservableCollection<SignalRow> SignalRows
        {
            get { return signalRows; }
            private set
            {
                signalRows = value;
                RaisePanelPropertyChanged("SignalRows");
            }
        }

        public int OverrideMode
        {
            get { return overrideMode; }
            set
            {
                if (overrideMode == value) return;
                overrideMode = value;
                if (!readingBridge && CurrentPdu != null)
                {
                    overrideModeDrafts[CurrentPdu.Index] = value;
                    controlEditorDirty = true;
                    controlDraftDirty[CurrentPdu.Index] = true;
                }
                RaisePanelPropertyChanged("OverrideMode");
            }
        }

        public int UbMode
        {
            get { return ubMode; }
            set
            {
                if (ubMode == value) return;
                ubMode = value;
                if (!readingBridge && CurrentPdu != null)
                {
                    ubModeDrafts[CurrentPdu.Index] = value;
                    controlEditorDirty = true;
                    controlDraftDirty[CurrentPdu.Index] = true;
                }
                RaisePanelPropertyChanged("UbMode");
            }
        }

        public bool AutoProtect
        {
            get { return autoProtect; }
            set
            {
                if (autoProtect == value) return;
                autoProtect = value;
                if (!readingBridge && CurrentPdu != null)
                {
                    autoProtectDrafts[CurrentPdu.Index] = value;
                    controlDraftDirty[CurrentPdu.Index] = true;
                }
                RaisePanelPropertyChanged("AutoProtect");
                WriteAutoProtectToBridge();
            }
        }

        public bool GlobalEnable
        {
            get { return globalEnable; }
            private set
            {
                if (globalEnable == value) return;
                globalEnable = value;
                RaisePanelPropertyChanged("GlobalEnable");
                RaiseSafetyState();
            }
        }

        public string ProtectionState
        {
            get { return protectionState; }
            private set
            {
                if (protectionState == value) return;
                protectionState = value;
                RaisePanelPropertyChanged("ProtectionState");
            }
        }

        public int Counter
        {
            get { return counter; }
            private set
            {
                counter = value;
                RaisePanelPropertyChanged("Counter");
                RaisePanelPropertyChanged("CounterText");
            }
        }

        public string CounterText
        {
            get { return "0x" + counter.ToString("X1", CultureInfo.InvariantCulture) + "  (" + counter.ToString(CultureInfo.InvariantCulture) + ")"; }
        }

        public string DataIdText
        {
            get { return "0x" + dataId.ToString("X4", CultureInfo.InvariantCulture) + "  (" + dataId.ToString(CultureInfo.InvariantCulture) + ")"; }
        }

        public string CrcText
        {
            get { return "0x" + crc.ToString("X2", CultureInfo.InvariantCulture) + "  (" + crc.ToString(CultureInfo.InvariantCulture) + ")"; }
        }

        public int UpdateBit
        {
            get { return updateBit; }
            private set
            {
                updateBit = value;
                RaisePanelPropertyChanged("UpdateBit");
                RaisePanelPropertyChanged("UpdateBitText");
            }
        }

        public string UpdateBitText
        {
            get { return updateBit == 0 ? "0  未激活" : "1  已激活"; }
        }

        public string E2eLayoutText
        {
            get { return CurrentPdu == null ? string.Empty : CurrentPdu.E2eLayoutText; }
        }

        public bool CanApply { get { return bridgeConnected && bridgeReady && GlobalEnable; } }
        public bool CanRestore { get { return bridgeConnected && bridgeReady; } }
        public bool CanStopAll { get { return bridgeConnected && bridgeReady; } }
        public bool CanToggleGlobal { get { return bridgeConnected && bridgeReady; } }

        public string BridgeStatus
        {
            get
            {
                if (!initializeCalled) return "预览 / 未绑定符号";
                if (bindingType == ExchangeSymbolDataType.Unknown.ToString()) return "已绑定 / 等待数据";
                if (!bridgeConnected) return "绑定无效 / " + bindingType;
                if (!bridgeReady) return "桥接已连接 / 未就绪";
                return GlobalEnable ? "发送已使能 / 待命" : "控制就绪 / 未使能";
            }
        }

        public Brush BridgeStatusBackground
        {
            get
            {
                if (!initializeCalled) return CreateBrush(0xEE, 0xF0, 0xF2);
                if (bindingType == ExchangeSymbolDataType.Unknown.ToString()) return CreateBrush(0xFF, 0xF6, 0xDD);
                if (!bridgeConnected) return CreateBrush(0xFD, 0xEA, 0xE8);
                if (!bridgeReady) return CreateBrush(0xFF, 0xF6, 0xDD);
                return GlobalEnable ? CreateBrush(0xE8, 0xF4, 0xEC) : CreateBrush(0xE8, 0xF0, 0xF7);
            }
        }

        public Brush BridgeStatusBorder
        {
            get
            {
                if (!initializeCalled) return CreateBrush(0xA9, 0xB1, 0xB8);
                if (bindingType == ExchangeSymbolDataType.Unknown.ToString()) return CreateBrush(0xD0, 0xA2, 0x37);
                if (!bridgeConnected) return CreateBrush(0xC4, 0x59, 0x4F);
                if (!bridgeReady) return CreateBrush(0xD0, 0xA2, 0x37);
                return GlobalEnable ? CreateBrush(0x62, 0x9A, 0x73) : CreateBrush(0x6C, 0x91, 0xAF);
            }
        }

        public Brush BridgeStatusForeground
        {
            get
            {
                if (!initializeCalled) return CreateBrush(0x4E, 0x57, 0x5F);
                if (bindingType == ExchangeSymbolDataType.Unknown.ToString()) return CreateBrush(0x6F, 0x55, 0x14);
                if (!bridgeConnected) return CreateBrush(0x9E, 0x35, 0x2D);
                if (!bridgeReady) return CreateBrush(0x6F, 0x55, 0x14);
                return GlobalEnable ? CreateBrush(0x2F, 0x6B, 0x43) : CreateBrush(0x2E, 0x5D, 0x82);
            }
        }

        public string SafetyMessage
        {
            get
            {
                if (!initializeCalled) return "尚未绑定CANoe符号；当前选择和编辑仅用于本地预览。";
                if (bindingType == ExchangeSymbolDataType.Unknown.ToString()) return "PanelBridge已绑定；等待CANoe测量数据类型激活。";
                if (!bridgeConnected) return "当前符号类型为" + bindingType + "；PanelBridge必须是Int32[64]。";
                if (!bridgeReady) return "数组符号已绑定，但CAPL桥接尚未声明就绪。";
                if (!GlobalEnable) return "发送使能为0；5个受控PDU在TxPending中被拦截。";
                return "发送已使能但不会自动发帧；执行当前只改变所选PDU，其他运行ID保持不变。";
            }
        }

        public string FooterMessage
        {
            get
            {
                if (!initializeCalled) return "安全状态：未绑定符号；可以预览选择，但不能修改发送PDU。";
                if (bindingType == ExchangeSymbolDataType.Unknown.ToString()) return "安全状态：符号已绑定；请启动测量并等待LongArray激活。";
                if (!bridgeConnected) return "安全状态：符号类型无效；请绑定SRS3_E2E::WpfBridge::PanelBridge。";
                if (!bridgeReady) return "安全状态：CAPL桥接尚未就绪，动作按钮保持禁用。";
                if (!GlobalEnable) return "安全状态：控制已就绪、发送使能为0；5个受控PDU不会上总线。";
                return "待命状态：可逐个启动多个ID并行发送；PDU表显示各ID状态，停止全部用于统一收口。";
            }
        }

        public Brush FooterForeground
        {
            get
            {
                if (!initializeCalled || !bridgeConnected) return CreateBrush(0xA8, 0x3A, 0x31);
                if (!bridgeReady || bindingType == ExchangeSymbolDataType.Unknown.ToString()) return CreateBrush(0x7A, 0x5C, 0x14);
                return GlobalEnable ? CreateBrush(0x32, 0x6B, 0x45) : CreateBrush(0x5E, 0x67, 0x6F);
            }
        }

        public ExchangeSymbolDataType SupportedDataTypes
        {
            get { return ExchangeSymbolDataType.LongArray; }
        }

        public string ControlName
        {
            get { return "E2E Tx WPF Control"; }
        }

        public System.Windows.Forms.Control ExternalControl
        {
            get
            {
                if (externalControl == null)
                {
                    externalControl = new System.Windows.Forms.Integration.ElementHost
                    {
                        Dock = System.Windows.Forms.DockStyle.Fill,
                        Child = this
                    };
                }
                return externalControl;
            }
        }

        public IExchangeSymbolValue SymbolValue
        {
            get { return symbolValue; }
            set { symbolValue = value; }
        }

        public IList<string> SupportedProperties
        {
            get { return new List<string> { "Title" }; }
        }

        public bool SupportsPropertyBackColor { get { return true; } }
        public bool SupportsPropertyForeColor { get { return true; } }

        public System.Drawing.Color ControlBackColor
        {
            get
            {
                SolidColorBrush brush = Background as SolidColorBrush;
                return brush == null
                    ? System.Drawing.Color.White
                    : System.Drawing.Color.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B);
            }
            set { Background = new SolidColorBrush(Color.FromArgb(value.A, value.R, value.G, value.B)); }
        }

        public System.Drawing.Color ControlForeColor
        {
            get
            {
                SolidColorBrush brush = Foreground as SolidColorBrush;
                return brush == null
                    ? System.Drawing.Color.Black
                    : System.Drawing.Color.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B);
            }
            set { Foreground = new SolidColorBrush(Color.FromArgb(value.A, value.R, value.G, value.B)); }
        }

        public bool Enabled
        {
            // CANoe disables a plugin host when its symbol cannot be resolved.
            // Keep navigation/input available for diagnosis; Apply/Restore have
            // their own bridge-ready and GlobalEnable safety gates.
            get { return true; }
            set { ExternalControl.Enabled = true; }
        }

        public bool Visible
        {
            get { return ExternalControl.Visible; }
            set { ExternalControl.Visible = value; }
        }

        [Category("E2E Control Settings")]
        [DisplayName("Title")]
        public string Title
        {
            get { return title; }
            set { title = value ?? string.Empty; }
        }

        public void Initialize(IExchangeSymbolValue value)
        {
            if (symbolValue != null)
            {
                symbolValue.ValueChanged -= OnRxValue;
            }

            SymbolValue = value;
            initializeCalled = true;
            bindingType = value == null ? "null" : value.SymbolDataType.ToString();
            bridgeConnected = value != null && value.SymbolDataType == ExchangeSymbolDataType.LongArray;
            bridgeReady = false;

            if (value != null)
            {
                symbolValue.ValueChanged += OnRxValue;
                ReadBridgeValue();
            }

            RaiseSafetyState();
        }

        public bool SerializeSupportedProperties(out string serializationString)
        {
            serializationString = Title.Replace(";", string.Empty) + ";";
            return true;
        }

        public bool DeserializeSupportedProperties(string serializationString)
        {
            string[] values = (serializationString ?? string.Empty).Split(';');
            if (values.Length > 0 && !string.IsNullOrEmpty(values[0]))
            {
                Title = values[0];
            }
            return true;
        }

        private void OnRxValue(object sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ReadBridgeValue));
                return;
            }
            ReadBridgeValue();
        }

        private void ReadBridgeValue()
        {
            try
            {
                if (symbolValue == null || symbolValue.SymbolDataType != ExchangeSymbolDataType.LongArray)
                {
                    bindingType = symbolValue == null ? "null" : symbolValue.SymbolDataType.ToString();
                    bridgeConnected = false;
                    bridgeReady = false;
                    RaiseSafetyState();
                    return;
                }

                bindingType = symbolValue.SymbolDataType.ToString();
                bridgeConnected = true;

                int[] values = symbolValue.LongArray.ToArray();
                if (values.Length < BridgeLength)
                {
                    bridgeReady = false;
                    RaiseSafetyState();
                    return;
                }

                readingBridge = true;
                try
                {
                    int selectedPdu = values[IndexSelectedPdu];
                    PduDefinition selected = Pdus.FirstOrDefault(item => item.Index == selectedPdu);
                    if (selected != null)
                    {
                        CurrentPdu = selected;
                    }

                    GlobalEnable = values[IndexGlobalEnable] != 0;
                    bridgeReady = values[IndexProtocolVersion] == BridgeProtocolVersion && values[IndexBridgeReady] == 1;

                    /* WPF writes the selected index before CAPL has had a
                     * TxPending callback. DataID is the CAPL acknowledgement
                     * that status/payload now belong to that selection. */
                    if (selectionPending && values[IndexDataId] == CurrentPdu.DataId)
                    {
                        selectionPending = false;
                    }

                    if (!selectionPending)
                    {
                        ProtectionState = StateToText(values[IndexState]);
                        Counter = values[IndexCounter];
                        dataId = values[IndexDataId] == 0 ? CurrentPdu.DataId : values[IndexDataId];
                        crc = values[IndexCrc] & 0xFF;
                        UpdateBit = values[IndexActualUb] != 0 ? 1 : 0;

                        if (!signalEditorDirty)
                        {
                            int signalCount = Math.Min(Math.Min(values[IndexSignalCount], SignalRows.Count), 8);
                            for (int index = 0; index < signalCount; index++)
                            {
                                SignalRows[index].RawValue = values[IndexRawBase + index];
                                SignalRows[index].UseRaw = values[IndexUseRawBase + index] != 0;
                                SignalRows[index].InputValid = values[IndexValidBase + index] != 0;
                            }
                        }

                        for (int index = 0; index < PayloadBytes.Count; index++)
                        {
                            PayloadBytes[index].Value = values[IndexPayloadBase + index] & 0xFF;
                        }
                    }

                    for (int index = 0; index < Pdus.Count && index < 5; index++)
                    {
                        Pdus[index].SendMode = values[IndexSendModeBase + index];
                    }
                }
                finally
                {
                    readingBridge = false;
                }

                RaisePanelPropertyChanged("DataIdText");
                RaisePanelPropertyChanged("CrcText");
                RaiseSafetyState();
            }
            catch
            {
                bridgeReady = false;
                RaiseSafetyState();
            }
        }

        private static string StateToText(int state)
        {
            switch (state)
            {
                case 0: return "已停止";
                case 1: return "连续发送 / 已保护";
                case 2: return "发送待生效";
                case 3: return "输入无效 / 已拦截";
                case 4: return "单帧已发送";
                case 5: return "连续发送 / 未保护";
                case 6: return "单帧已发送 / 未保护";
                default: return "状态 " + state.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void UpdatePayloadRoles()
        {
            if (currentPdu == null)
            {
                return;
            }

            foreach (PayloadByte payloadByte in PayloadBytes)
            {
                payloadByte.SetRole(
                    payloadByte.Index == currentPdu.CrcByteIndex,
                    currentPdu.CrcStartBit,
                    payloadByte.Index == currentPdu.CounterByteIndex,
                    currentPdu.CounterStartBit,
                    payloadByte.Index == currentPdu.UbByteIndex,
                    currentPdu.UbStartBit);
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (CanApply)
            {
                SendBridgeCommand(1);
            }
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (CanRestore)
            {
                SendBridgeCommand(2);
            }
        }

        private void StopAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (CanStopAll)
            {
                SendBridgeCommand(4);
            }
        }

        private void SendBridgeCommand(int command)
        {
            if (symbolValue == null || symbolValue.SymbolDataType != ExchangeSymbolDataType.LongArray)
            {
                return;
            }

            int[] values = symbolValue.LongArray.ToArray();
            if (values.Length < BridgeLength)
            {
                return;
            }

            values[IndexProtocolVersion] = BridgeProtocolVersion;
            values[IndexSelectedPdu] = CurrentPdu.Index;
            values[IndexOverrideMode] = OverrideMode;
            values[IndexUbMode] = UbMode;
            values[IndexAutoProtect] = AutoProtect ? 1 : 0;
            values[IndexCommand] = command;
            values[IndexSignalCount] = Math.Min(SignalRows.Count, 8);

            for (int index = 0; index < 8; index++)
            {
                if (index < SignalRows.Count)
                {
                    values[IndexRawBase + index] = SignalRows[index].RawValue;
                    values[IndexUseRawBase + index] = SignalRows[index].UseRaw ? 1 : 0;
                    values[IndexValidBase + index] = SignalRows[index].InputValid ? 1 : 0;
                }
                else
                {
                    values[IndexRawBase + index] = 0;
                    values[IndexUseRawBase + index] = 0;
                    values[IndexValidBase + index] = 0;
                }
            }

            symbolValue.LongArray = values;
            signalEditorDirty = false;
            controlEditorDirty = false;
            signalDraftDirty[CurrentPdu.Index] = false;
            controlDraftDirty[CurrentPdu.Index] = false;
        }

        private void GlobalEnable_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            if (checkBox == null || !CanToggleGlobal)
            {
                return;
            }

            SendGlobalEnableRequest(checkBox.IsChecked == true);
        }

        private void SendGlobalEnableRequest(bool enabled)
        {
            if (symbolValue == null || symbolValue.SymbolDataType != ExchangeSymbolDataType.LongArray)
            {
                return;
            }

            int[] values = symbolValue.LongArray.ToArray();
            if (values.Length < BridgeLength)
            {
                return;
            }

            values[IndexGlobalEnable] = enabled ? 1 : 0;
            values[IndexCommand] = 3;
            symbolValue.LongArray = values;
        }

        private void SignalRow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!readingBridge)
            {
                signalEditorDirty = true;
                if (CurrentPdu != null && SignalRows.Contains(sender as SignalRow))
                {
                    signalDraftDirty[CurrentPdu.Index] = true;
                }
            }
        }

        private void WriteSelectedPduToBridge()
        {
            if (readingBridge || !bridgeConnected || symbolValue == null || CurrentPdu == null ||
                symbolValue.SymbolDataType != ExchangeSymbolDataType.LongArray)
            {
                return;
            }

            int[] values = symbolValue.LongArray.ToArray();
            if (values.Length < BridgeLength || values[IndexSelectedPdu] == CurrentPdu.Index)
            {
                return;
            }

            values[IndexSelectedPdu] = CurrentPdu.Index;
            symbolValue.LongArray = values;
        }

        private void WriteAutoProtectToBridge()
        {
            if (readingBridge || !bridgeConnected || symbolValue == null ||
                symbolValue.SymbolDataType != ExchangeSymbolDataType.LongArray)
            {
                return;
            }

            int[] values = symbolValue.LongArray.ToArray();
            int requested = AutoProtect ? 1 : 0;
            if (values.Length < BridgeLength || values[IndexAutoProtect] == requested)
            {
                return;
            }

            values[IndexAutoProtect] = requested;
            symbolValue.LongArray = values;
        }

        private void RaiseSafetyState()
        {
            RaisePanelPropertyChanged("BridgeStatus");
            RaisePanelPropertyChanged("BridgeStatusBackground");
            RaisePanelPropertyChanged("BridgeStatusBorder");
            RaisePanelPropertyChanged("BridgeStatusForeground");
            RaisePanelPropertyChanged("SafetyMessage");
            RaisePanelPropertyChanged("FooterMessage");
            RaisePanelPropertyChanged("FooterForeground");
            RaisePanelPropertyChanged("CanApply");
            RaisePanelPropertyChanged("CanRestore");
            RaisePanelPropertyChanged("CanStopAll");
            RaisePanelPropertyChanged("CanToggleGlobal");
        }

        private static Brush CreateBrush(byte red, byte green, byte blue)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }

        private void RaisePanelPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void E2ETxControl_Loaded(object sender, RoutedEventArgs e)
        {
            bridgePollTimer.Start();
            if (hwndSource != null)
            {
                return;
            }

            hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProc);
            }
        }

        private void E2ETxControl_Unloaded(object sender, RoutedEventArgs e)
        {
            bridgePollTimer.Stop();
            if (hwndSource != null)
            {
                hwndSource.RemoveHook(WndProc);
            }
            hwndSource = null;
        }

        private void BridgePollTimer_Tick(object sender, EventArgs e)
        {
            if (symbolValue != null)
            {
                ReadBridgeValue();
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WmGetDlgCode = 0x0087;
            const int DlgcWantAllKeys = 0x0004;
            if (message == WmGetDlgCode)
            {
                handled = true;
                return new IntPtr(DlgcWantAllKeys);
            }
            return IntPtr.Zero;
        }
    }
}
