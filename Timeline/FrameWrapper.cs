using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace VESCO.Timeline
{
    class FrameWrapper
    {
        public BitmapSource frame { get; }
        public double opacity { get; }
        public int x { get; }
        public int y { get; }
        public double scale { get; }

        public FrameWrapper(BitmapSource frame, double opacity, int x, int y, double scale)
        {
            this.frame = frame;
            this.opacity = opacity;
            this.x = x;
            this.y = y;
            this.scale = scale;
        }

    }
}
