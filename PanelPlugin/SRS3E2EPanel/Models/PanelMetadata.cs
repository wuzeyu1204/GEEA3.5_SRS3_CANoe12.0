using System.Collections.ObjectModel;

namespace SRS3.E2E.PanelControl.Models
{
    public static class PanelMetadata
    {
        public static ObservableCollection<OptionItem> CreateUbOptions()
        {
            return new ObservableCollection<OptionItem>
            {
                new OptionItem(0, "自动 Auto"),
                new OptionItem(1, "强制 0"),
                new OptionItem(2, "强制 1")
            };
        }

        public static ObservableCollection<PduDefinition> CreatePdus()
        {
            return new ObservableCollection<PduDefinition>
            {
                new PduDefinition(
                    0, "0x040", "VehMtnSt", "ZCUDZCUDCAN1Fr01", 15, 0x0036, 15, 6, 7,
                    new SignalDefinition(
                        "VehMtnSt", "VehMtnStVehMtnSt", 3, 1.0, 0.0, 0.0, 7.0, "", 0,
                        new OptionItem(0, "Unknown"),
                        new OptionItem(1, "Standstill 1"),
                        new OptionItem(2, "Standstill 2"),
                        new OptionItem(3, "Standstill 3"),
                        new OptionItem(4, "Rolling Forward 1"),
                        new OptionItem(5, "Rolling Forward 2"),
                        new OptionItem(6, "Rolling Backward 1"),
                        new OptionItem(7, "Rolling Backward 2"))),

                new PduDefinition(
                    1, "0x050", "PreCrashFrontData", "ZCUDZCUDCAN1Fr47", 10, 0x0411, 23, 47, 40,
                    new SignalDefinition("ClosingVelocity", "PreCrashFrontDataClosingVelocity", 8, 0.167, 0.0, 0.0, 42.585, "m/s", 0),
                    new SignalDefinition(
                        "ObjectClass", "PreCrashFrontDataObjectClass", 3, 1.0, 0.0, 0.0, 7.0, "", 0,
                        new OptionItem(0, "Unknown"),
                        new OptionItem(1, "Car"),
                        new OptionItem(2, "Truck"),
                        new OptionItem(3, "Pedestrian"),
                        new OptionItem(6, "Sensor Error")),
                    new SignalDefinition("OverLap", "PreCrashFrontDataOverLap", 8, 1.0, -100.0, -100.0, 100.0, "%", 100),
                    new SignalDefinition("TimeToImpact", "PreCrashFrontDataTimeToImpact", 8, 0.004, 0.0, 0.0, 1.02, "s", 250)),

                new PduDefinition(
                    2, "0x076", "VehSpdLgt", "ZCUDZCUDCAN1Fr37", 20, 0x0037, 39, 47, 23,
                    new SignalDefinition("VehSpdLgtA", "VehSpdLgtA", 15, 0.00391, 0.0, 0.0, 125.0027, "m/s", 0),
                    new SignalDefinition(
                        "VehSpdLgtQf", "VehSpdLgtQf", 2, 1.0, 0.0, 0.0, 3.0, "", 1,
                        new OptionItem(0, "Undefined Accuracy"),
                        new OptionItem(1, "Temporarily Undefined"),
                        new OptionItem(2, "Accuracy Out of Specification"),
                        new OptionItem(3, "Accurate Data"))),

                new PduDefinition(
                    3, "0x0F0", "VehModMngtGlbSafe1", "ZCUDZCUDCAN1Fr04", 30, 0x0074, 39, 15, 7,
                    new SignalDefinition(
                        "CarModSts1", "VehModMngtGlbSafe1CarModSts1", 3, 1.0, 0.0, 0.0, 7.0, "", 0,
                        new OptionItem(0, "Normal"),
                        new OptionItem(1, "Transport"),
                        new OptionItem(2, "Factory"),
                        new OptionItem(3, "Crash"),
                        new OptionItem(5, "Dyno")),
                    new SignalDefinition("CarModSubtypWd", "VehModMngtGlbSafe1CarModSubtypWd", 3, 1.0, 0.0, 0.0, 7.0, "Unitless", 0),
                    new SignalDefinition("EgyLvlElecMai", "VehModMngtGlbSafe1EgyLvlElecMai", 4, 1.0, 0.0, 0.0, 15.0, "Unitless", 0),
                    new SignalDefinition("EgyLvlElecSubt", "VehModMngtGlbSafe1EgyLvlElecSubt", 4, 1.0, 0.0, 0.0, 15.0, "Unitless", 0),
                    new SignalDefinition(
                        "FltEgyCnsWdSts", "VehModMngtGlbSafe1FltEgyCnsWdSts", 1, 1.0, 0.0, 0.0, 1.0, "", 0,
                        new OptionItem(0, "No Fault"),
                        new OptionItem(1, "Fault")),
                    new SignalDefinition("PwrLvlElecMai", "VehModMngtGlbSafe1PwrLvlElecMai", 4, 1.0, 0.0, 0.0, 15.0, "Unitless", 0),
                    new SignalDefinition("PwrLvlElecSubt", "VehModMngtGlbSafe1PwrLvlElecSubt", 4, 1.0, 0.0, 0.0, 15.0, "Unitless", 0),
                    new SignalDefinition(
                        "UsgModSts", "VehModMngtGlbSafe1UsgModSts", 4, 1.0, 0.0, 0.0, 15.0, "", 1,
                        new OptionItem(0, "Abandoned"),
                        new OptionItem(1, "Inactive"),
                        new OptionItem(2, "Convenience"),
                        new OptionItem(11, "Active"),
                        new OptionItem(13, "Driving"))),

                new PduDefinition(
                    4, "0x390", "PassAirbLampStsRec", "ZCUDZCUDCAN1Fr10", 800, 0x0474, 55, 43, 63,
                    new SignalDefinition(
                        "PassAirbLampSts", "PassAirbLampStsRecPassAirbLampSt", 2, 1.0, 0.0, 0.0, 3.0, "", 1,
                        new OptionItem(0, "Reserved 1"),
                        new OptionItem(1, "Lamp OK"),
                        new OptionItem(2, "Lamp Not OK"),
                        new OptionItem(3, "Reserved 2")))
            };
        }
    }
}
