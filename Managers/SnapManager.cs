using System.Diagnostics;
using VESCO.Timeline;

namespace VESCO.Managers
{
    public class SnapManager
    {
        private readonly TimelineController _timelineController;
        private const long SnapThresholdFrames = 30;
        
        private Clip? _snapTargetClip = null;
        private long _snapTargetFrame = -1;

        public SnapManager(TimelineController timelineController)
        {
            _timelineController = timelineController;
        }

        public long GetSnappedFrame(Clip draggedClip, int draggedTrackIndex, long targetFrame, bool enableSnapping)
        {
            if (!enableSnapping || draggedClip == null)
            {
                ResetSnapState();
                return targetFrame;
            }

            long snappedFrame = targetFrame;
            long minDistance = long.MaxValue;
            Clip? bestSnapTarget = null;

            long draggedClipLength = GetClipLength(draggedClip, draggedTrackIndex);

            // Check all video tracks
            for (int trackIndex = 0; trackIndex < _timelineController.Timeline.VideoTracks.Count; trackIndex++)
            {
                var track = _timelineController.Timeline.VideoTracks[trackIndex];
                foreach (var clip in track.Clips)
                {
                    if (clip == draggedClip)
                        continue;

                    long otherClipStart = clip.TimelineStart;
                    long otherClipEnd = otherClipStart + (long)(clip.Length * (_timelineController.Timeline.Fps / clip.Source.FPS));

                    // Snap dragged clip end to other clip start
                    long draggedClipEnd = targetFrame + draggedClipLength;
                    long distanceToStart = Math.Abs(draggedClipEnd - otherClipStart);
                    if (distanceToStart < minDistance && distanceToStart <= SnapThresholdFrames)
                    {
                        minDistance = distanceToStart;
                        snappedFrame = otherClipStart - draggedClipLength;
                        bestSnapTarget = clip;
                    }

                    // Snap dragged clip start to other clip end
                    long distanceToEnd = Math.Abs(targetFrame - otherClipEnd);
                    if (distanceToEnd < minDistance && distanceToEnd <= SnapThresholdFrames)
                    {
                        minDistance = distanceToEnd;
                        snappedFrame = otherClipEnd;
                        bestSnapTarget = clip;
                    }
                }
            }

            // Check all audio tracks
            for (int trackIndex = 0; trackIndex < _timelineController.Timeline.AudioTracks.Count; trackIndex++)
            {
                var track = _timelineController.Timeline.AudioTracks[trackIndex];
                foreach (var clip in track.Clips)
                {
                    if (clip == draggedClip)
                        continue;

                    long otherClipStart = clip.TimelineStart;
                    long otherClipEnd = otherClipStart + (long)(clip.Duration * _timelineController.Timeline.Fps);

                    // Snap dragged clip end to other clip start
                    long draggedClipEnd = targetFrame + draggedClipLength;
                    long distanceToStart = Math.Abs(draggedClipEnd - otherClipStart);
                    if (distanceToStart < minDistance && distanceToStart <= SnapThresholdFrames)
                    {
                        minDistance = distanceToStart;
                        snappedFrame = otherClipStart - draggedClipLength;
                        bestSnapTarget = clip;
                    }

                    // Snap dragged clip start to other clip end
                    long distanceToEnd = Math.Abs(targetFrame - otherClipEnd);
                    if (distanceToEnd < minDistance && distanceToEnd <= SnapThresholdFrames)
                    {
                        minDistance = distanceToEnd;
                        snappedFrame = otherClipEnd;
                        bestSnapTarget = clip;
                    }
                }
            }

            // If we found a snap target, sticky snap to it
            if (minDistance <= SnapThresholdFrames && bestSnapTarget != null)
            {
                _snapTargetClip = bestSnapTarget;
                _snapTargetFrame = snappedFrame;
                Debug.WriteLine($"Snap detected: {minDistance} frames away, snapped to {snappedFrame}");
                return Math.Max(0, snappedFrame);
            }

            // If we were previously snapped and are still close enough, maintain snap
            if (_snapTargetClip != null && _snapTargetFrame >= 0)
            {
                long distanceFromLastSnap = Math.Abs(targetFrame - _snapTargetFrame);
                if (distanceFromLastSnap <= SnapThresholdFrames * 3)
                {
                    return Math.Max(0, _snapTargetFrame);
                }
            }

            ResetSnapState();
            return Math.Max(0, targetFrame);
        }

        public void ResetSnapState()
        {
            _snapTargetClip = null;
            _snapTargetFrame = -1;
        }

        private long GetClipLength(Clip clip, int trackIndex)
        {
            if (clip is VideoClip videoClip)
            {
                return (long)(videoClip.Length * (_timelineController.Timeline.Fps / videoClip.Source.FPS));
            }
            else if (clip is AudioClip audioClip)
            {
                return (long)(audioClip.Duration * _timelineController.Timeline.Fps);
            }

            return 0;
        }
    }
}