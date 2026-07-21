using System;
using System.Collections;
using UnityEngine;

namespace TrainGuessPrototype
{
    public enum TrainGuessSequenceState
    {
        OpeningCameraAnimation,
        IntroExploration,
        CameraFeedSwitch,
        PlatformExploration,
        JumpPrompt,
        Invitation,
        FirstGuess,
        FirstTrain,
        SecondGuess,
        SecondTrain,
        FinalGuess,
        MuseumTrain,
        FastTrain,
        Ending
    }

    public sealed class TrainGuessDirector : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] Camera cctvCamera;
        [SerializeField] Animation introCameraAnimation;
        [SerializeField] AnimationClip introCameraClip;
        [SerializeField] Transform platformCameraAnchor;
        [SerializeField] Transform platformEntry;
        [SerializeField] CctvPlayerController player;
        [SerializeField] TrainGuessUI ui;
        [SerializeField] TrainActor train;
        [SerializeField] GameObject mysteryManVisual;
        [SerializeField] GameObject endingNewspaperAndHat;

        [Header("Sequence Positions")]
        [SerializeField] float introExitX = -13.2f;
        [SerializeField] float jumpPromptX = 16.2f;
        [SerializeField] Vector3 trainStart = new Vector3(-48f, -0.2f, 0f);
        [SerializeField] Vector3 trainStop = new Vector3(0f, -0.2f, 0f);
        [SerializeField] Vector3 trainEnd = new Vector3(48f, -0.2f, 0f);

        [Header("Story Clock")]
        [SerializeField] int startingDay = 1;
        [SerializeField, Range(0, 23)] int startingHour = 11;
        [SerializeField, Range(0, 59)] int startingMinute = 47;

        float storyMinutes;
        bool freezeClock;

        public TrainGuessSequenceState State { get; private set; }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            storyMinutes = startingDay * 1440f + startingHour * 60f + startingMinute;
            endingNewspaperAndHat.SetActive(false);
            train.gameObject.SetActive(false);
            player.CanMove = false;
            player.SetMovementCamera(cctvCamera);
            ui.SetFeed("CAM 01  WAITING HALL");

            yield return RunOpening();
            yield return RunPlatformSequence();
        }

        void Update()
        {
            if (!freezeClock)
                storyMinutes += Time.deltaTime / 60f;

            int absoluteMinutes = Mathf.FloorToInt(storyMinutes);
            int day = absoluteMinutes / 1440;
            int minuteOfDay = Wrap(absoluteMinutes, 1440);
            int second = Mathf.FloorToInt((storyMinutes - Mathf.Floor(storyMinutes)) * 60f);
            ui.SetTimecode(day, minuteOfDay / 60, minuteOfDay % 60, second);
        }

        IEnumerator RunOpening()
        {
            State = TrainGuessSequenceState.OpeningCameraAnimation;
            if (introCameraAnimation != null && introCameraClip != null)
            {
                introCameraAnimation.Play(introCameraClip.name);
                yield return new WaitForSeconds(introCameraClip.length);
            }

            State = TrainGuessSequenceState.IntroExploration;
            player.CanMove = true;
            yield return new WaitUntil(() => player.transform.position.x >= introExitX);

            State = TrainGuessSequenceState.CameraFeedSwitch;
            player.CanMove = false;
            yield return CutToPlatformCamera();
            player.Teleport(platformEntry.position, platformEntry.rotation);
            player.CanMove = true;
        }

        IEnumerator RunPlatformSequence()
        {
            State = TrainGuessSequenceState.PlatformExploration;
            yield return new WaitUntil(() => player.transform.position.x >= jumpPromptX);
            player.CanMove = false;

            State = TrainGuessSequenceState.JumpPrompt;
            bool confirmed = false;
            while (!confirmed)
            {
                ui.ShowDialogue("准备跳下？", "确认", "取消");
                int decision = 0;
                yield return new WaitUntil(() => (decision = ui.ConsumeDecision()) != 0);
                ui.HideDialogue();

                if (decision > 0)
                {
                    confirmed = true;
                }
                else
                {
                    yield return new WaitForSecondsRealtime(1.35f);
                }
            }

            State = TrainGuessSequenceState.Invitation;
            yield return ShowLine("玩个游戏？");
            yield return ShowLine("我们猜猜下一班车何时要来，要去往何方。");

            State = TrainGuessSequenceState.FirstGuess;
            TrainGuessSelection firstGuess = default;
            yield return SelectTrain(GetCurrentHour(), Wrap(GetCurrentMinute() + 3, 60), "PARK", false, result => firstGuess = result);
            yield return AdvanceClockTo(firstGuess, 3.2f);

            State = TrainGuessSequenceState.FirstTrain;
            train.Configure(firstGuess.Hour, firstGuess.Minute, firstGuess.Destination, DestinationColor(firstGuess.Destination));
            yield return RunRegularStoppingTrain(trainStart, trainEnd);
            yield return ShowLine("再猜一次。猜中三次，应该就会发生什么。");

            State = TrainGuessSequenceState.SecondGuess;
            TrainGuessSelection secondGuess = default;
            yield return SelectTrain(GetCurrentHour(), Wrap(GetCurrentMinute() + 4, 60), "CITY", false, result => secondGuess = result);
            yield return AdvanceClockTo(secondGuess, 3.2f);

            State = TrainGuessSequenceState.SecondTrain;
            train.Configure(secondGuess.Hour, secondGuess.Minute, secondGuess.Destination, DestinationColor(secondGuess.Destination));
            yield return RunRegularStoppingTrain(trainEnd, trainStart);
            yield return ShowLine("这是最后一次。");

            State = TrainGuessSequenceState.FinalGuess;
            TrainGuessSelection finalGuess = default;
            yield return SelectTrain(12, 0, "MUSEUM", true, result => finalGuess = result);
            yield return AdvanceClockTo(finalGuess, 4.8f);

            State = TrainGuessSequenceState.MuseumTrain;
            train.Configure(12, 0, "MUSEUM", DestinationColor("MUSEUM"));
            train.gameObject.SetActive(true);
            train.transform.position = trainStart;
            yield return MoveActiveTrain(trainStart, trainStop, 5.4f, null);
            yield return ShowLine("上车吧。在这里，你会找到想要的东西的。");
            yield return new WaitForSecondsRealtime(1.1f);
            player.gameObject.SetActive(false);
            freezeClock = true;
            storyMinutes = Mathf.Floor(storyMinutes / 1440f) * 1440f + 12f * 60f;
            yield return MoveActiveTrain(trainStop, trainEnd, 4.2f, null);
            train.gameObject.SetActive(false);

            yield return new WaitForSecondsRealtime(3.0f);
            State = TrainGuessSequenceState.FastTrain;
            train.Configure(12, 0, "NO SERVICE", new Color(0.72f, 0.08f, 0.06f));
            yield return MoveTrain(trainEnd, trainStart, 1.35f, SwapMysteryManForHat);

            State = TrainGuessSequenceState.Ending;
            ui.SetFeed("CAM 02  PLATFORM");
        }

        IEnumerator CutToPlatformCamera()
        {
            ui.SetStatic(1f);
            yield return new WaitForSecondsRealtime(0.08f);

            if (introCameraAnimation != null)
                introCameraAnimation.Stop();
            cctvCamera.transform.SetPositionAndRotation(platformCameraAnchor.position, platformCameraAnchor.rotation);
            cctvCamera.fieldOfView = 44f;
            ui.SetFeed("CAM 02  PLATFORM");

            ui.SetStatic(0.62f);
            yield return new WaitForSecondsRealtime(0.06f);
            ui.SetStatic(0f);
        }

        IEnumerator ShowLine(string line)
        {
            ui.ShowDialogue(line, "继续");
            int decision = 0;
            yield return new WaitUntil(() => (decision = ui.ConsumeDecision()) > 0);
            ui.HideDialogue();
        }

        IEnumerator SelectTrain(int hour, int minute, string destination, bool locked, Action<TrainGuessSelection> onSelected)
        {
            ui.OpenSelector(hour, minute, destination, locked);
            TrainGuessSelection result = default;
            yield return new WaitUntil(() => ui.TryConsumeSelection(out result));
            onSelected?.Invoke(result);
        }

        IEnumerator AdvanceClockTo(TrainGuessSelection selection, float duration)
        {
            int targetMinuteOfDay = selection.Hour * 60 + selection.Minute;
            float currentDayStart = Mathf.Floor(storyMinutes / 1440f) * 1440f;
            float target = currentDayStart + targetMinuteOfDay;
            if (target <= storyMinutes + 0.01f)
                target += 1440f;

            float start = storyMinutes;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                storyMinutes = Mathf.Lerp(start, target, t);
                yield return null;
            }

            storyMinutes = target;
        }

        IEnumerator MoveTrain(Vector3 from, Vector3 to, float duration, Action midpointAction)
        {
            train.gameObject.SetActive(true);
            train.transform.position = from;
            yield return MoveActiveTrain(from, to, duration, midpointAction);
            train.gameObject.SetActive(false);
        }

        IEnumerator RunRegularStoppingTrain(Vector3 approachFrom, Vector3 departureTo)
        {
            train.gameObject.SetActive(true);
            train.transform.position = approachFrom;
            yield return MoveActiveTrain(approachFrom, trainStop, 4.8f, null);
            yield return new WaitForSecondsRealtime(3.2f);
            yield return MoveActiveTrain(trainStop, departureTo, 4.2f, null);
            train.gameObject.SetActive(false);
        }

        IEnumerator MoveActiveTrain(Vector3 from, Vector3 to, float duration, Action midpointAction)
        {
            bool midpointInvoked = false;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float raw = Mathf.Clamp01(elapsed / duration);
                float t = raw * raw * (3f - 2f * raw);
                train.transform.position = Vector3.LerpUnclamped(from, to, t);

                if (!midpointInvoked && raw >= 0.46f)
                {
                    midpointInvoked = true;
                    midpointAction?.Invoke();
                }

                yield return null;
            }

            train.transform.position = to;
            if (!midpointInvoked)
                midpointAction?.Invoke();
        }

        void SwapMysteryManForHat()
        {
            mysteryManVisual.SetActive(false);
            endingNewspaperAndHat.SetActive(true);
        }

        int GetCurrentHour()
        {
            return Wrap(Mathf.FloorToInt(storyMinutes), 1440) / 60;
        }

        int GetCurrentMinute()
        {
            return Wrap(Mathf.FloorToInt(storyMinutes), 60);
        }

        static Color DestinationColor(string destination)
        {
            switch (destination)
            {
                case "PARK":
                    return new Color(0.18f, 0.48f, 0.26f);
                case "CITY":
                    return new Color(0.18f, 0.34f, 0.62f);
                case "MOUNTAIN":
                    return new Color(0.46f, 0.38f, 0.28f);
                case "MUSEUM":
                    return new Color(0.62f, 0.10f, 0.12f);
                default:
                    return new Color(0.52f, 0.12f, 0.10f);
            }
        }

        static int Wrap(int value, int modulo)
        {
            value %= modulo;
            return value < 0 ? value + modulo : value;
        }
    }
}
