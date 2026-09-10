using System.Collections.Generic;
using Fragua.Imaging.ImageTrace.Vectorization.Segments;

namespace Fragua.Imaging.ImageTrace.Vectorization.TraceTypes
{
    internal class SegmentPath
    {
        public IReadOnlyList<Segment> Segments { get; set; }
    }
}
