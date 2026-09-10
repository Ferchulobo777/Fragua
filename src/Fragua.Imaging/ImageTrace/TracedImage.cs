using System.Collections.Generic;
using Fragua.Imaging.ImageTrace.Vectorization.TraceTypes;

namespace Fragua.Imaging.ImageTrace
{
    internal class TracedImage
    {
        public int Width { get; }
        public int Height { get; }
        public Dictionary<ColorReference, Layer<SegmentPath>> Layers { get; }

        public TracedImage(Dictionary<ColorReference, Layer<SegmentPath>> layers, int width, int height)
        {
            Width = width;
            Height = height;
            Layers = layers;
        }
    }
}
