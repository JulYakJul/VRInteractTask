using System;
using System.Collections.Generic;
using System.Linq;
using BNG;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VRInteract.UI
{
    [DisallowMultipleComponent]
    /// <summary>
    /// Управление вводом VR-клавиатуры
    /// </summary>
    public class VRKeyboardRuntimeInput : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private bool createDisplayIfMissing = true;
        [SerializeField] private Vector3 displayLocalPosition = new Vector3(0f, 0.18f, 0.02f);
        [SerializeField] private Vector2 displaySize = new Vector2(520f, 80f);
        [SerializeField] private int displayFontSize = 36;

        [Header("Joystick navigation")]
        [SerializeField] private bool enableJoystickNavigation = true;
        [SerializeField] private ControllerHand navigationHand = ControllerHand.Right;
        [SerializeField] private float axisDeadzone = 0.55f;
        [SerializeField] private float repeatDelaySeconds = 0.18f;

        [Header("Confirm (press key)")]
        [SerializeField] private ConfirmBinding confirm = ConfirmBinding.PrimaryButtonOrTrigger;

        [Header("Mode Toggle")]
        [SerializeField] private bool showModeIndicator = true;

        public enum InputMode
        {
            Ray,        // Классический режим (луч)
            Joystick    // Навигация джойстиком
        }

        public InputMode CurrentMode { get; private set; } = InputMode.Ray;

        private VRKeyboard keyboard;
        private InputField inputField;

        private readonly List<UnityEngine.UI.Button> keyButtons = new();
        private readonly List<RectTransform> keyRects = new();
        private int selectedIndex = -1;
        private float nextRepeatTime;
        private Outline currentOutline;
        private UnityEngine.UI.Button currentButton;

        private bool lastToggleButtonState = false;
        private Text modeIndicatorText;

        public enum ConfirmBinding
        {
            PrimaryButtonOrTrigger,
            PrimaryButtonOnly,
            TriggerOnly
        }

        private void Awake()
        {
            keyboard = GetComponent<VRKeyboard>();
            if (keyboard == null)
            {
                enabled = false;
                return;
            }

            EnsureEventSystemExists();
            EnsureInputFieldAttached();
            CacheKeys();
            SelectBestInitialKey();

            // Индикатор режима
            if (showModeIndicator)
            {
                CreateModeIndicator();
            }

            // Начальный режим
            SetInputMode(InputMode.Ray);
        }

        private void OnEnable()
        {
            nextRepeatTime = 0f;
        }

        private void Update()
        {
            // Проверка нажатия кнопки переключения режима (кнопка A)
            CheckModeToggle();

            if (CurrentMode != InputMode.Joystick || !enableJoystickNavigation)
            {
                return;
            }

            var input = InputBridge.Instance;
            if (input == null)
            {
                return;
            }

            var axis = navigationHand == ControllerHand.Left ? input.LeftThumbstickAxis : input.RightThumbstickAxis;
            var axisMove = ReadAxisMove(axis);
            if (axisMove != Vector2Int.zero && Time.unscaledTime >= nextRepeatTime)
            {
                MoveSelection(axisMove);
                nextRepeatTime = Time.unscaledTime + repeatDelaySeconds;
            }

            if (ReadConfirm(input))
            {
                PressSelectedKey();
            }
        }

        /// <summary>
        /// Проверка состояния переключения режима ввода
        /// </summary>
        private void CheckModeToggle()
        {
            var input = InputBridge.Instance;
            if (input == null) return;

            // Проверка нажатия кнопки A (или X для левого контроллера)
            bool togglePressed = navigationHand == ControllerHand.Right
                ? input.AButtonDown
                : input.XButtonDown;

            if (togglePressed && !lastToggleButtonState)
            {
                // Переключение режима
                ToggleInputMode();
            }

            lastToggleButtonState = togglePressed;
        }

        /// <summary>
        /// Переключение режима ввода между лучом и джойстиком
        /// </summary>
        public void ToggleInputMode()
        {
            InputMode newMode = CurrentMode == InputMode.Ray ? InputMode.Joystick : InputMode.Ray;
            SetInputMode(newMode);

            Debug.Log($"VR Keyboard mode switched to: {newMode}");
        }

        /// <summary>
        /// Установка активного режима ввода
        /// </summary>
        /// <param name="mode">Целевой режим ввода</param>
        public void SetInputMode(InputMode mode)
        {
            if (CurrentMode == mode) return;

            CurrentMode = mode;

            if (currentOutline != null)
            {
                currentOutline.enabled = false;
            }

            SetButtonsInteractable(mode == InputMode.Ray);

            // Обновление индикатора
            UpdateModeIndicator();
        }

        /// <summary>
        /// Установка состояния интерактивности для кнопок клавиатуры
        /// </summary>
        /// <param name="interactable">Флаг доступности кнопок</param>
        private void SetButtonsInteractable(bool interactable)
        {
            foreach (var button in keyButtons)
            {
                if (button != null)
                {
                    button.interactable = interactable;
                }
            }
        }

        /// <summary>
        /// Создание визуального индикатора текущего режима ввода
        /// </summary>
        private void CreateModeIndicator()
        {
            var indicatorGO = new GameObject("ModeIndicator");
            indicatorGO.transform.SetParent(transform, false);
            indicatorGO.transform.localPosition = new Vector3(0f, 0.25f, 0.02f);
            indicatorGO.transform.localRotation = Quaternion.identity;
            indicatorGO.transform.localScale = Vector3.one * 0.0018f;

            var canvas = indicatorGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            indicatorGO.AddComponent<GraphicRaycaster>();

            var canvasRect = indicatorGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(300f, 40f);

            var bg = indicatorGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.7f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(indicatorGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            modeIndicatorText = textGO.AddComponent<Text>();
            modeIndicatorText.alignment = TextAnchor.MiddleCenter;
            modeIndicatorText.fontSize = 24;
            modeIndicatorText.color = Color.yellow;
            modeIndicatorText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            modeIndicatorText.supportRichText = true;

            UpdateModeIndicator();
        }

        /// <summary>
        /// Обновление отображения индикатора режима
        /// </summary>
        private void UpdateModeIndicator()
        {
            if (modeIndicatorText == null) return;

            string modeText = CurrentMode == InputMode.Ray
                ? "<color=#00FF00>Mode: RAY</color> (Press A for Joystick)"
                : "<color=#FFFF00>Mode: JOYSTICK</color> (Press A for Ray)";

            modeIndicatorText.text = modeText;
        }

        /// <summary>
        /// Проверка наличия EventSystem в сцене и создание при необходимости
        /// </summary>
        private void EnsureEventSystemExists()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        /// <summary>
        /// Проверка наличия InputField и привязка к клавиатуре
        /// </summary>
        private void EnsureInputFieldAttached()
        {
            if (keyboard.AttachedInputField != null)
            {
                inputField = keyboard.AttachedInputField;
                return;
            }

            inputField = GetComponentInChildren<InputField>(true);
            if (inputField != null)
            {
                keyboard.AttachToInputField(inputField);
                return;
            }

            if (!createDisplayIfMissing)
            {
                return;
            }

            inputField = CreateWorldSpaceInputField();
            keyboard.AttachToInputField(inputField);
            inputField.Select();
        }

        /// <summary>
        /// Создание InputField в мировом пространстве для отображения ввода
        /// </summary>
        /// <returns>Созданный компонент InputField</returns>
        private InputField CreateWorldSpaceInputField()
        {
            var root = new GameObject("VRKeyboardDisplay");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = displayLocalPosition;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * 0.0018f;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<GraphicRaycaster>();

            var canvasRect = root.GetComponent<RectTransform>();
            canvasRect.sizeDelta = displaySize;

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(root.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bg = bgGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            var inputGO = new GameObject("InputField");
            inputGO.transform.SetParent(root.transform, false);
            var inputRect = inputGO.AddComponent<RectTransform>();
            inputRect.anchorMin = Vector2.zero;
            inputRect.anchorMax = Vector2.one;
            inputRect.offsetMin = new Vector2(16, 12);
            inputRect.offsetMax = new Vector2(-16, -12);
            var inputImage = inputGO.AddComponent<Image>();
            inputImage.color = new Color(1f, 1f, 1f, 0.06f);

            var field = inputGO.AddComponent<InputField>();

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(inputGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 6);
            textRect.offsetMax = new Vector2(-10, -6);
            var text = textGO.AddComponent<Text>();
            text.supportRichText = false;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.fontSize = displayFontSize;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(inputGO.transform, false);
            var phRect = placeholderGO.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(10, 6);
            phRect.offsetMax = new Vector2(-10, -6);
            var placeholder = placeholderGO.AddComponent<Text>();
            placeholder.text = "Type on VR Keyboard…";
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.fontSize = displayFontSize;
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            placeholder.font = text.font;

            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;

            return field;
        }

        /// <summary>
        /// Кэширование ссылок на кнопки клавиш для навигации
        /// </summary>
        private void CacheKeys()
        {
            keyButtons.Clear();
            keyRects.Clear();

            var keys = GetComponentsInChildren<VRKeyboardKey>(true);
            foreach (var k in keys)
            {
                if (k == null)
                {
                    continue;
                }

                var b = k.GetComponent<UnityEngine.UI.Button>();
                var rt = k.GetComponent<RectTransform>();
                if (b == null || rt == null || !b.interactable)
                {
                    continue;
                }

                keyButtons.Add(b);
                keyRects.Add(rt);
            }
        }

        /// <summary>
        /// Выбор и активация начальной клавиши для навигации
        /// </summary>
        private void SelectBestInitialKey()
        {
            if (keyButtons.Count == 0)
            {
                return;
            }

            if (selectedIndex < 0 || selectedIndex >= keyButtons.Count)
            {
                selectedIndex = 0;
            }

            ApplySelection(selectedIndex);
        }

        /// <summary>
        /// Определение направления перемещения по оси джойстика с учётом мёртвой зоны
        /// </summary>
        /// <param name="axis">Значение оси джойстика</param>
        /// <returns>Направление перемещения в виде дискретных координат</returns>
        private Vector2Int ReadAxisMove(Vector2 axis)
        {
            if (axis.sqrMagnitude < axisDeadzone * axisDeadzone)
            {
                return Vector2Int.zero;
            }

            if (Mathf.Abs(axis.x) > Mathf.Abs(axis.y))
            {
                return new Vector2Int(axis.x > 0 ? 1 : -1, 0);
            }

            return new Vector2Int(0, axis.y > 0 ? 1 : -1);
        }

        /// <summary>
        /// Проверка нажатия кнопки подтверждения согласно текущим настройкам
        /// </summary>
        /// <param name="input">Экземпляр InputBridge для чтения ввода</param>
        /// <returns>Факт нажатия кнопки подтверждения</returns>
        private bool ReadConfirm(InputBridge input)
        {
            bool primaryDown = input.AButtonDown || input.XButtonDown;
            bool triggerDown = input.RightTriggerDown || input.LeftTriggerDown;

            return confirm switch
            {
                ConfirmBinding.PrimaryButtonOnly => primaryDown,
                ConfirmBinding.TriggerOnly => triggerDown,
                _ => primaryDown || triggerDown
            };
        }

        /// <summary>
        /// Перемещение выделения по клавиатуре в указанном направлении
        /// </summary>
        /// <param name="dir">Направление перемещения (дискретные координаты)</param>
        private void MoveSelection(Vector2Int dir)
        {
            if (keyButtons.Count == 0)
            {
                return;
            }

            if (selectedIndex < 0 || selectedIndex >= keyButtons.Count)
            {
                selectedIndex = 0;
                ApplySelection(selectedIndex);
                return;
            }

            var currentPos = transform.InverseTransformPoint(keyRects[selectedIndex].position);
            currentPos.z = 0f;
            var localDir = new Vector3(dir.x, dir.y, 0f).normalized;

            int bestIdx = -1;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < keyRects.Count; i++)
            {
                if (i == selectedIndex)
                {
                    continue;
                }

                var candidatePos = transform.InverseTransformPoint(keyRects[i].position);
                candidatePos.z = 0f;
                var d = candidatePos - currentPos;
                d.z = 0f;
                if (d.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                var dn = d.normalized;
                float alignment = Vector3.Dot(dn, localDir);
                if (alignment < 0.35f)
                {
                    continue;
                }

                float distance = d.magnitude;
                float score = alignment * 2.0f - distance * 0.02f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                }
            }

            if (bestIdx >= 0)
            {
                selectedIndex = bestIdx;
                ApplySelection(selectedIndex);
            }
        }

        /// <summary>
        /// Применение выделения к указанной клавише с визуальным отображением
        /// </summary>
        /// <param name="idx">Индекс целевой клавиши в кэшированном списке</param>
        private void ApplySelection(int idx)
        {
            if (idx < 0 || idx >= keyButtons.Count)
            {
                return;
            }

            if (currentOutline != null)
            {
                currentOutline.enabled = false;
            }

            currentButton = keyButtons[idx];
            var go = currentButton.gameObject;

            currentOutline = go.GetComponent<Outline>();
            if (currentOutline == null)
            {
                currentOutline = go.AddComponent<Outline>();
                currentOutline.effectColor = new Color(1f, 0.92f, 0.2f, 0.9f);
                currentOutline.effectDistance = new Vector2(2f, -2f);
            }

            currentOutline.enabled = true;

            if (inputField != null && !inputField.isFocused)
            {
                inputField.Select();
            }
        }

        /// <summary>
        /// Активация выбранной клавиши и обработка её нажатия
        /// </summary>
        private void PressSelectedKey()
        {
            if (currentButton == null)
            {
                return;
            }

            currentButton.onClick?.Invoke();

            if (inputField != null && !inputField.isFocused)
            {
                inputField.Select();
            }
        }
    }
}