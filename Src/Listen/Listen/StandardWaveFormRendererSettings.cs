using System.Drawing;

namespace NAudio.WaveFormRenderer
{
    public class StandardWaveFormRendererSettings : WaveFormRendererSettings
    {
        public StandardWaveFormRendererSettings()
        {
            PixelsPerPeak = 1;
            SpacerPixels = 0;
            TopPeakPen = Pens.MediumPurple;
            BottomPeakPen = Pens.MediumPurple;
        }


        public override Pen TopPeakPen { get; set; }

        // not needed
        public override Pen TopSpacerPen { get; set; }
        
        public override Pen BottomPeakPen { get; set; }
        
        // not needed
        public override Pen BottomSpacerPen { get; set; }
    }
}