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
            long draggedClipEnd = targetFrame + draggedClipLength;

            for (int trackIndex = 0; trackIndex < _timelineController.Timeline.VideoTracks.Count; trackIndex++)
            {
                var track = _timelineController.Timeline.VideoTracks[trackIndex];
                foreach (var clip in track.Clips)
                {
                    if (clip == draggedClip)
                        continue;

                    long otherClipStart = clip.TimelineStart;
                    long otherClipEnd = otherClipStart + (long)(clip.Length * (_timelineController.Timeline.Fps / clip.Source.FPS));
                    EvaluateSnapCandidate(
                        clip,
                        targetFrame,
                        draggedClipLength,
                        draggedClipEnd,
                        otherClipStart,
                        otherClipEnd,
                        ref snappedFrame,
                        ref minDistance,
                        ref bestSnapTarget);
                }
            }

            for (int trackIndex = 0; trackIndex < _timelineController.Timeline.AudioTracks.Count; trackIndex++)
            {
                var track = _timelineController.Timeline.AudioTracks[trackIndex];
                foreach (var clip in track.Clips)
                {
                    if (clip == draggedClip)
                        continue;

                    long otherClipStart = clip.TimelineStart;
                    long otherClipEnd = otherClipStart + (long)(clip.Duration * _timelineController.Timeline.Fps);
                    EvaluateSnapCandidate(
                        clip,
                        targetFrame,
                        draggedClipLength,
                        draggedClipEnd,
                        otherClipStart,
                        otherClipEnd,
                        ref snappedFrame,
                        ref minDistance,
                        ref bestSnapTarget);
                }
            }

            if (minDistance <= SnapThresholdFrames && bestSnapTarget != null)
            {
                _snapTargetClip = bestSnapTarget;
                _snapTargetFrame = snappedFrame;
                Debug.WriteLine($"Snap detected: {minDistance} frames away, snapped to {snappedFrame}");
                return Math.Max(0, snappedFrame);
            }

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

        private static void EvaluateSnapCandidate(
            Clip candidate,
            long targetFrame,
            long draggedClipLength,
            long draggedClipEnd,
            long candidateStart,
            long candidateEnd,
            ref long snappedFrame,
            ref long minDistance,
            ref Clip? bestSnapTarget)
        {
            long distanceToStart = Math.Abs(draggedClipEnd - candidateStart);
            if (distanceToStart < minDistance && distanceToStart <= SnapThresholdFrames)
            {
                minDistance = distanceToStart;
                snappedFrame = candidateStart - draggedClipLength;
                bestSnapTarget = candidate;
            }

            long distanceToEnd = Math.Abs(targetFrame - candidateEnd);
            if (distanceToEnd < minDistance && distanceToEnd <= SnapThresholdFrames)
            {
                minDistance = distanceToEnd;
                snappedFrame = candidateEnd;
                bestSnapTarget = candidate;
            }
        }
    }
}
