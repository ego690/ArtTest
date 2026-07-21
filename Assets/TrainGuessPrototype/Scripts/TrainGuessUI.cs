using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TrainGuessPrototype
{
    public readonly struct TrainGuessSelection
    {
        public readonly int Hour;
        public readonly int Minute;
        public readonly string Destination;

        public TrainGuessSelection(int hour, int minute, string destination)
        {
            Hour = Mathf.Clamp(hour, 0, 23);
            Minute = Mathf.Clamp(minute, 0, 59);
            Destination = destination;
        }
    }

    public sealed class TrainGuessUI : MonoBehaviour
    {
        enum SelectorField
        {
            Hour,
            Minute,
            Destination
        }

        [Header("CCTV Overlay")]
        [SerializeField] Text cameraLabel;
        [SerializeField] Text timecodeLabel;
        [SerializeField] Image staticOverlay;

        [Header("Dialogue")]
        [SerializeField] GameObject dialoguePanel;
        [SerializeField] Text dialogueText;
        [SerializeField] Button primaryButton;
        [SerializeField] Text primaryButtonLabel;
        [SerializeField] Button secondaryButton;
        [SerializeField] Text secondaryButtonLabel;

        [Header("Selector")]
        [SerializeField] RectTransform selectorPanel;
        [SerializeField] RectTransform hourColumn;
        [SerializeField] RectTransform minuteColumn;
        [SerializeField] RectTransform destinationColumn;
        [SerializeField] Text hourText;
        [SerializeField] Text minuteText;
        [SerializeField] Text destinationText;
        [SerializeField] Text selectorTitle;
        [SerializeField] Button hourButton;
        [SerializeField] Button minuteButton;
        [SerializeField] Button destinationButton;
        [SerializeField] Button selectorConfirmButton;

        readonly string[] destinations = { "PARK", "CITY", "MOUNTAIN", "MUSEUM" };

        SelectorField focusedField;
        int selectedHour = 11;
        int selectedMinute = 50;
        int selectedDestination;
        int decision;
        bool selectorSubmitted;
        bool selectorLocked;
        bool selectorAnimating;
        Coroutine selectorAnimation;

        public bool SelectorVisible => selectorPanel != null && selectorPanel.gameObject.activeSelf;

        void Awake()
        {
            ApplyRuntimeFont();

            primaryButton.onClick.AddListener(() => decision = 1);
            secondaryButton.onClick.AddListener(() => decision = -1);
            hourButton.onClick.AddListener(() => Focus(SelectorField.Hour));
            minuteButton.onClick.AddListener(() => Focus(SelectorField.Minute));
            destinationButton.onClick.AddListener(() => Focus(SelectorField.Destination));
            selectorConfirmButton.onClick.AddListener(SubmitSelector);

            HideDialogue();
            selectorPanel.gameObject.SetActive(false);
            SetStatic(0f);
        }

        void Update()
        {
            if (!SelectorVisible || selectorAnimating || selectorLocked)
                return;

            UpdatePointerFocus();
            HandleSelectorInput();
        }

        public void SetFeed(string label)
        {
            cameraLabel.text = label;
        }

        public void SetTimecode(int day, int hour, int minute, int second)
        {
            timecodeLabel.text = $"DAY {day:00}   {hour:00}:{minute:00}:{second:00}";
        }

        public void SetStatic(float alpha)
        {
            if (staticOverlay == null)
                return;

            Color color = staticOverlay.color;
            color.a = Mathf.Clamp01(alpha);
            staticOverlay.color = color;
            staticOverlay.raycastTarget = false;
        }

        public void ShowDialogue(string text, string primaryLabelText, string secondaryLabelText = null)
        {
            decision = 0;
            dialogueText.text = text;
            primaryButtonLabel.text = primaryLabelText;
            primaryButton.gameObject.SetActive(!string.IsNullOrEmpty(primaryLabelText));
            bool hasSecondary = !string.IsNullOrEmpty(secondaryLabelText);
            secondaryButton.gameObject.SetActive(hasSecondary);
            secondaryButtonLabel.text = hasSecondary ? secondaryLabelText : string.Empty;
            dialoguePanel.SetActive(true);
            primaryButton.Select();
        }

        public void HideDialogue()
        {
            if (dialoguePanel != null)
                dialoguePanel.SetActive(false);
            decision = 0;
        }

        public int ConsumeDecision()
        {
            int result = decision;
            decision = 0;
            return result;
        }

        public void OpenSelector(int initialHour, int initialMinute, string initialDestination, bool locked)
        {
            HideDialogue();
            selectedHour = Mathf.Clamp(initialHour, 0, 23);
            selectedMinute = Mathf.Clamp(initialMinute, 0, 59);
            selectedDestination = FindDestination(initialDestination);
            selectorLocked = locked;
            selectorSubmitted = false;
            selectorTitle.text = locked ? "FINAL GUESS" : "NEXT TRAIN";
            Focus(locked ? SelectorField.Destination : SelectorField.Hour);
            UpdateSelectorLabels();

            selectorPanel.gameObject.SetActive(true);
            if (selectorAnimation != null)
                StopCoroutine(selectorAnimation);
            selectorAnimation = StartCoroutine(AnimateSelectorIn());
        }

        public bool TryConsumeSelection(out TrainGuessSelection selection)
        {
            if (!selectorSubmitted)
            {
                selection = default;
                return false;
            }

            selectorSubmitted = false;
            selectorPanel.gameObject.SetActive(false);
            selection = new TrainGuessSelection(selectedHour, selectedMinute, destinations[selectedDestination]);
            return true;
        }

        public void CloseSelector()
        {
            selectorSubmitted = false;
            selectorPanel.gameObject.SetActive(false);
        }

        void ApplyRuntimeFont()
        {
            Font runtimeFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 36);
            if (runtimeFont == null)
                runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            foreach (Text text in GetComponentsInChildren<Text>(true))
                text.font = runtimeFont;
        }

        IEnumerator AnimateSelectorIn()
        {
            selectorAnimating = true;
            Vector2 target = Vector2.zero;
            Vector2 start = new Vector2(0f, 760f);
            selectorPanel.anchoredPosition = start;

            const float duration = 0.55f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                selectorPanel.anchoredPosition = Vector2.LerpUnclamped(start, target, t);
                yield return null;
            }

            selectorPanel.anchoredPosition = target;
            selectorAnimating = false;
            selectorConfirmButton.Select();
        }

        void UpdatePointerFocus()
        {
            if (Mouse.current == null)
                return;

            Vector2 pointer = Mouse.current.position.ReadValue();
            if (RectTransformUtility.RectangleContainsScreenPoint(hourColumn, pointer))
                Focus(SelectorField.Hour);
            else if (RectTransformUtility.RectangleContainsScreenPoint(minuteColumn, pointer))
                Focus(SelectorField.Minute);
            else if (RectTransformUtility.RectangleContainsScreenPoint(destinationColumn, pointer))
                Focus(SelectorField.Destination);
        }

        void HandleSelectorInput()
        {
            int verticalStep = 0;

            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.1f)
                    verticalStep = scroll > 0f ? 1 : -1;
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
                    verticalStep = 1;
                if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
                    verticalStep = -1;
                if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
                    CycleFocus(-1);
                if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame)
                    CycleFocus(1);
                if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                    SubmitSelector();
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current.dpad.up.wasPressedThisFrame)
                    verticalStep = 1;
                if (Gamepad.current.dpad.down.wasPressedThisFrame)
                    verticalStep = -1;
                if (Gamepad.current.dpad.left.wasPressedThisFrame)
                    CycleFocus(-1);
                if (Gamepad.current.dpad.right.wasPressedThisFrame)
                    CycleFocus(1);
                if (Gamepad.current.buttonSouth.wasPressedThisFrame)
                    SubmitSelector();
            }

            if (verticalStep != 0)
                StepSelection(verticalStep);
        }

        void Focus(SelectorField field)
        {
            focusedField = field;
            UpdateSelectorLabels();
        }

        void CycleFocus(int direction)
        {
            int next = ((int)focusedField + direction + 3) % 3;
            Focus((SelectorField)next);
        }

        void StepSelection(int direction)
        {
            switch (focusedField)
            {
                case SelectorField.Hour:
                    selectedHour = Wrap(selectedHour + direction, 24);
                    break;
                case SelectorField.Minute:
                    selectedMinute = Wrap(selectedMinute + direction, 60);
                    break;
                case SelectorField.Destination:
                    selectedDestination = Wrap(selectedDestination + direction, destinations.Length);
                    break;
            }

            UpdateSelectorLabels();
        }

        void UpdateSelectorLabels()
        {
            hourText.text = FormatNumberColumn(selectedHour, 24, focusedField == SelectorField.Hour);
            minuteText.text = FormatNumberColumn(selectedMinute, 60, focusedField == SelectorField.Minute);
            destinationText.text = FormatDestinationColumn(focusedField == SelectorField.Destination);
        }

        string FormatNumberColumn(int value, int modulo, bool focused)
        {
            int previous = Wrap(value - 1, modulo);
            int next = Wrap(value + 1, modulo);
            string marker = focused && !selectorLocked ? ">" : " ";
            return $"{previous:00}\n{marker} {value:00} {marker}\n{next:00}";
        }

        string FormatDestinationColumn(bool focused)
        {
            int previous = Wrap(selectedDestination - 1, destinations.Length);
            int next = Wrap(selectedDestination + 1, destinations.Length);
            string marker = focused && !selectorLocked ? ">" : " ";
            return $"{destinations[previous]}\n{marker} {destinations[selectedDestination]} {marker}\n{destinations[next]}";
        }

        void SubmitSelector()
        {
            if (!SelectorVisible || selectorAnimating)
                return;
            selectorSubmitted = true;
        }

        int FindDestination(string destination)
        {
            for (int i = 0; i < destinations.Length; i++)
            {
                if (string.Equals(destinations[i], destination, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }

        static int Wrap(int value, int modulo)
        {
            value %= modulo;
            return value < 0 ? value + modulo : value;
        }
    }
}
