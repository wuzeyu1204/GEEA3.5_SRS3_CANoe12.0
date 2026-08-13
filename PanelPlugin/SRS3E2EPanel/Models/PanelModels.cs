using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Media;

namespace SRS3.E2E.PanelControl.Models
{
    public sealed class OptionItem
    {
        public OptionItem(int value, string text)
        {
            Value = value;
            Text = text;
        }

        public int Value { get; private set; }
        public string Text { get; private set; }
    }

    public sealed class SignalDefinition
    {
        public SignalDefinition(
            string name,
            string dbcSignal,
            int bitLength,
            double factor,
            double offset,
            double minimum,
            double maximum,
            string unit,
            int defaultRaw,
            params OptionItem[] enumOptions)
        {
            Name = name;
            DbcSignal = dbcSignal;
            BitLength = bitLength;
            Factor = factor;
            Offset = offset;
            Minimum = minimum;
            Maximum = maximum;
            Unit = unit;
            DefaultRaw = defaultRaw;
            EnumOptions = new ReadOnlyCollection<OptionItem>(enumOptions ?? new OptionItem[0]);
        }

        public string Name { get; private set; }
        public string DbcSignal { get; private set; }
        public int BitLength { get; private set; }
        public double Factor { get; private set; }
        public double Offset { get; private set; }
        public double Minimum { get; private set; }
        public double Maximum { get; private set; }
        public string Unit { get; private set; }
        public int DefaultRaw { get; private set; }
        public ReadOnlyCollection<OptionItem> EnumOptions { get; private set; }
        public bool IsEnum { get { return EnumOptions.Count > 0; } }
        public bool IsNumeric { get { return !IsEnum; } }

        public string RangeText
        {
            get
            {
                if (IsEnum)
                {
                    return "Enum / " + BitLength.ToString(CultureInfo.InvariantCulture) + " bit";
                }

                return Minimum.ToString("0.####", CultureInfo.InvariantCulture)
                    + " .. "
                    + Maximum.ToString("0.####", CultureInfo.InvariantCulture)
                    + (string.IsNullOrEmpty(Unit) ? string.Empty : " " + Unit);
            }
        }
    }

    public sealed class PduDefinition : BindableBase
    {
        private int sendMode;
        public PduDefinition(
            int index,
            string canId,
            string group,
            string frame,
            int cycleMs,
            int dataId,
            int crcStartBit,
            int counterStartBit,
            int ubStartBit,
            params SignalDefinition[] signals)
        {
            Index = index;
            CanId = canId;
            Group = group;
            Frame = frame;
            CycleMs = cycleMs;
            DataId = dataId;
            CrcStartBit = crcStartBit;
            CounterStartBit = counterStartBit;
            UbStartBit = ubStartBit;
            Signals = new ReadOnlyCollection<SignalDefinition>(signals);
        }

        public int Index { get; private set; }
        public string CanId { get; private set; }
        public string Group { get; private set; }
        public string Frame { get; private set; }
        public int CycleMs { get; private set; }
        public int DataId { get; private set; }
        public int CrcStartBit { get; private set; }
        public int CounterStartBit { get; private set; }
        public int UbStartBit { get; private set; }
        public ReadOnlyCollection<SignalDefinition> Signals { get; private set; }
        public int RowNumber { get { return Index + 1; } }
        public string TriggerText { get { return "PDU-IL"; } }
        public string TimingText { get { return "周期 " + CycleMs.ToString(CultureInfo.InvariantCulture) + " ms"; } }
        public int Length { get { return 8; } }
        public string DataIdHex { get { return "0x" + DataId.ToString("X4", CultureInfo.InvariantCulture); } }
        public int CrcByteIndex { get { return CrcStartBit / 8; } }
        public int CounterByteIndex { get { return CounterStartBit / 8; } }
        public int UbByteIndex { get { return UbStartBit / 8; } }

        public int SendMode
        {
            get { return sendMode; }
            set
            {
                int bounded = Math.Max(0, Math.Min(2, value));
                if (sendMode == bounded) return;
                sendMode = bounded;
                RaisePropertyChanged("SendMode");
                RaisePropertyChanged("SendStatusText");
                RaisePropertyChanged("SendStatusForeground");
            }
        }

        public string SendStatusText
        {
            get
            {
                if (SendMode == 1) return "单帧待发";
                if (SendMode == 2) return "连续发送";
                return "已停止";
            }
        }

        public Brush SendStatusForeground
        {
            get
            {
                if (SendMode == 1) return CreateBrush("#8A6814");
                if (SendMode == 2) return CreateBrush("#28653E");
                return CreateBrush("#69727A");
            }
        }

        private static Brush CreateBrush(string color)
        {
            Brush brush = (Brush)new BrushConverter().ConvertFromString(color);
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }

        public string E2eLayoutText
        {
            get
            {
                return "CRC: Byte " + CrcByteIndex.ToString(CultureInfo.InvariantCulture)
                    + " / start " + CrcStartBit.ToString(CultureInfo.InvariantCulture)
                    + "    Counter: Byte " + CounterByteIndex.ToString(CultureInfo.InvariantCulture)
                    + " / start " + CounterStartBit.ToString(CultureInfo.InvariantCulture)
                    + "    UB: Byte " + UbByteIndex.ToString(CultureInfo.InvariantCulture)
                    + " / bit " + (UbStartBit % 8).ToString(CultureInfo.InvariantCulture);
            }
        }

        public string DisplayName
        {
            get { return CanId + "  " + Group; }
        }

        public string DetailText
        {
            get
            {
                return Frame + "  |  "
                    + CycleMs.ToString(CultureInfo.InvariantCulture) + " ms  |  DataID 0x"
                    + DataId.ToString("X4", CultureInfo.InvariantCulture);
            }
        }
    }

    public sealed class SignalRow : BindableBase
    {
        private int rawValue;
        private string physicalText;
        private bool useRaw;
        private bool inputValid;

        public SignalRow(SignalDefinition definition)
        {
            Definition = definition;
            rawValue = definition.DefaultRaw;
            physicalText = RawToPhysical(rawValue).ToString("0.#####", CultureInfo.InvariantCulture);
            inputValid = true;
        }

        public SignalDefinition Definition { get; private set; }
        public string Name { get { return Definition.Name; } }
        public string DbcSignal { get { return Definition.DbcSignal; } }
        public string RangeText { get { return Definition.RangeText; } }
        public string UnitText { get { return string.IsNullOrEmpty(Definition.Unit) ? "--" : Definition.Unit; } }
        public bool IsEnum { get { return Definition.IsEnum; } }
        public bool IsNumeric { get { return Definition.IsNumeric; } }
        public ReadOnlyCollection<OptionItem> EnumOptions { get { return Definition.EnumOptions; } }

        public int RawValue
        {
            get { return rawValue; }
            set
            {
                if (rawValue == value)
                {
                    return;
                }

                rawValue = value;
                physicalText = RawToPhysical(rawValue).ToString("0.#####", CultureInfo.InvariantCulture);
                RaisePropertyChanged("RawValue");
                RaisePropertyChanged("PhysicalText");
            }
        }

        public string PhysicalText
        {
            get { return physicalText; }
            set
            {
                if (physicalText == value)
                {
                    return;
                }

                physicalText = value;
                double physical;
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out physical))
                {
                    rawValue = PhysicalToRaw(physical);
                    RaisePropertyChanged("RawValue");
                }
                RaisePropertyChanged("PhysicalText");
            }
        }

        public bool UseRaw
        {
            get { return useRaw; }
            set
            {
                if (useRaw == value)
                {
                    return;
                }

                useRaw = value;
                RaisePropertyChanged("UseRaw");
                RaisePropertyChanged("UsePhysical");
            }
        }

        public bool UsePhysical { get { return !UseRaw; } }

        public bool InputValid
        {
            get { return inputValid; }
            set
            {
                if (inputValid == value)
                {
                    return;
                }

                inputValid = value;
                RaisePropertyChanged("InputValid");
                RaisePropertyChanged("ValidityText");
            }
        }

        public string ValidityText { get { return InputValid ? "●  有效" : "●  无效"; } }

        private double RawToPhysical(int raw)
        {
            return (raw * Definition.Factor) + Definition.Offset;
        }

        private int PhysicalToRaw(double physical)
        {
            if (Definition.Factor == 0.0)
            {
                return 0;
            }

            double bounded = Math.Max(Definition.Minimum, Math.Min(Definition.Maximum, physical));
            return (int)Math.Round((bounded - Definition.Offset) / Definition.Factor, MidpointRounding.AwayFromZero);
        }
    }

    public sealed class PayloadByte : BindableBase
    {
        private int value;
        private string roleText;
        private string roleDetail;
        private Brush roleBackground;
        private Brush roleBorder;
        private Brush roleForeground;

        public PayloadByte(int index)
        {
            Index = index;
            SetRole(false, -1, false, -1, false, -1);
        }

        public int Index { get; private set; }

        public int Value
        {
            get { return value; }
            set
            {
                int bounded = Math.Max(0, Math.Min(255, value));
                if (this.value == bounded)
                {
                    return;
                }

                this.value = bounded;
                RaisePropertyChanged("Value");
                RaisePropertyChanged("HexValue");
            }
        }

        public string HexValue
        {
            get { return "0x" + Value.ToString("X2", CultureInfo.InvariantCulture); }
        }

        public string RoleText { get { return roleText; } }
        public string RoleDetail { get { return roleDetail; } }
        public Brush RoleBackground { get { return roleBackground; } }
        public Brush RoleBorder { get { return roleBorder; } }
        public Brush RoleForeground { get { return roleForeground; } }

        public void SetRole(bool isCrc, int crcStartBit, bool isCounter, int counterStartBit, bool isUb, int ubStartBit)
        {
            string[] roles = new[]
            {
                isCrc ? "CRC" : null,
                isCounter ? "CNT" : null,
                isUb ? "UB" : null
            }.Where(role => role != null).ToArray();

            string[] starts = new[]
            {
                isCrc ? "s" + crcStartBit.ToString(CultureInfo.InvariantCulture) : null,
                isCounter ? "s" + counterStartBit.ToString(CultureInfo.InvariantCulture) : null,
                isUb ? "s" + ubStartBit.ToString(CultureInfo.InvariantCulture) : null
            }.Where(start => start != null).ToArray();

            roleText = roles.Length == 0 ? "DATA" : string.Join(" + ", roles);
            roleDetail = starts.Length == 0 ? string.Empty : string.Join(" / ", starts);

            if (isCrc && !isCounter && !isUb)
            {
                SetRoleColors("#FFF8F7", "#C65A50", "#A83A31");
            }
            else if (isCounter && isUb)
            {
                SetRoleColors("#F2F7FB", "#5B8DB3", "#285E87");
            }
            else if (isCounter)
            {
                SetRoleColors("#F2F7FB", "#5B8DB3", "#285E87");
            }
            else if (isUb)
            {
                SetRoleColors("#FFF9E8", "#D7AB32", "#725A15");
            }
            else
            {
                SetRoleColors("#F8F9FA", "#C7CDD2", "#525A61");
            }

            RaisePropertyChanged("RoleText");
            RaisePropertyChanged("RoleDetail");
            RaisePropertyChanged("RoleBackground");
            RaisePropertyChanged("RoleBorder");
            RaisePropertyChanged("RoleForeground");
        }

        private void SetRoleColors(string background, string border, string foreground)
        {
            roleBackground = (Brush)new BrushConverter().ConvertFromString(background);
            roleBorder = (Brush)new BrushConverter().ConvertFromString(border);
            roleForeground = (Brush)new BrushConverter().ConvertFromString(foreground);
        }
    }
}
