using System;
using System.Collections.Generic;

namespace GodexIndustrial
{
    [Serializable]
    public class LabelTemplate
    {
        public string Name { get; set; }
        public string Header { get; set; }
        public int ColumnCount { get; set; }
        public List<int> XOffsets { get; set; }
        public int YOffset { get; set; }
        public int FontSize { get; set; }
        public int LabelWidth { get; set; }
        public int LabelLength { get; set; }
        public int LabelGap { get; set; }
        public int Darkness { get; set; }
        public int Rotation { get; set; }
        public int PrintSpeed { get; set; }

        public LabelTemplate()
        {
            XOffsets = new List<int>();
            ColumnCount = 1;
            FontSize = 27;
            PrintSpeed = 2;
            LabelWidth = 40;
            LabelLength = 6;
            LabelGap = 3;
            Darkness = 12;
            Rotation = 0;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
