using System.Windows.Controls;

namespace VESCO
{
    public class TimelineController
    {
        private const double MinTimelineWidth = 200;
        private const double ZoomFactor = 1.2;
        private const double DefaultBufferMinutes = 10;

        public Timeline.Timeline Timeline { get; }
        public Canvas TimelineCanvas { get; }
        public double BufferDuration { get; }

        public TimelineController(double fps, Canvas timelineCanvas, double bufferMinutes = DefaultBufferMinutes)
        {
            Timeline = new Timeline.Timeline(fps);
            TimelineCanvas = timelineCanvas;
            BufferDuration = bufferMinutes * 60;
        }

        public long GetTotalFramesWithBuffer()
        {
            return Timeline.GetTotalFrames() + (long)(BufferDuration * Timeline.Fps);
        }

        public long PositionToFrame(double xPosition)
        {
            double clampedX = Math.Clamp(xPosition, 0, TimelineCanvas.Width);
            long totalFrames = GetTotalFramesWithBuffer();
            return (long)((clampedX / TimelineCanvas.Width) * totalFrames);
        }

        public double FrameToPosition(long frame)
        {
            long totalFrames = GetTotalFramesWithBuffer();
            return (frame / (double)totalFrames) * TimelineCanvas.Width;
        }

        public void ZoomIn()
        {
            TimelineCanvas.Width *= ZoomFactor;
        }

        public void ZoomOut()
        {
            TimelineCanvas.Width = Math.Max(MinTimelineWidth, TimelineCanvas.Width / ZoomFactor);
        }
    }
}