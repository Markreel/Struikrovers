using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Struikrovers.Presentation
{
    /// Dims the screen and lifts chosen objects above the dim with a nested Canvas, so
    /// layouts, prefabs and hierarchy stay untouched. Hovering keeps working on lifted objects.
    public sealed class SelectionLayer : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Canvas dimCanvas;     // on the Dim object (Image + Canvas + GraphicRaycaster)
        [SerializeField] private Canvas bannerCanvas;  // on the Banner object
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button continueButton;
        [Tooltip("Card detail popup, privacy overlay and so on. They are put above everything here.")]
        [SerializeField] private Canvas[] alwaysOnTop;
        [SerializeField] private int baseOrder = 100;

        private sealed class Lifted
        {
            public GameObject Object;
            public Canvas Canvas;
            public GraphicRaycaster Raycaster;
            public bool AddedCanvas, AddedRaycaster, OldOverride;
            public int OldOrder;
            public SelectionTarget Target;
        }

        private readonly List<Lifted> lifted = new List<Lifted>();
        private Action<int> onPicked;
        private Action onCancel;
        private Action onContinue;
        private bool allowCancel;

        public bool IsActive { get; private set; }

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
            if (continueButton != null) continueButton.onClick.AddListener(Continue);
        }

        private void Update()
        {
            // Uses the old Input class. Tell me if the project only uses the new Input System.
            if (IsActive && allowCancel && Input.GetKeyDown(KeyCode.Escape))
                Cancel();
        }

        /// Pick one of the lifted targets. Cancel (button or Escape) calls 'cancelled'.
        public void BeginPick(string title, string hint, IReadOnlyList<GameObject> targets,
                              Action<int> picked, Action cancelled)
        {
            Begin(title, hint, targets, true, false);
            onPicked = picked;
            onCancel = cancelled;
        }

        /// Show lifted objects with a Continue button (Peek). Space is handled by the caller.
        public void BeginReveal(string title, string hint, IReadOnlyList<GameObject> targets, Action done)
        {
            Begin(title, hint, targets, false, true);
            onContinue = done;
        }

        public void Continue()
        {
            if (!IsActive || onContinue == null) return;
            Action done = onContinue;
            End();
            done();
        }

        public void Cancel()
        {
            if (!IsActive || !allowCancel) return;
            Action cancelled = onCancel;
            End();
            cancelled?.Invoke();
        }

        public void End()
        {
            foreach (Lifted item in lifted)
                Restore(item);

            lifted.Clear();
            onPicked = null; onCancel = null; onContinue = null;
            IsActive = false;
            if (root != null) root.SetActive(false);
        }

        private void Begin(string title, string hint, IReadOnlyList<GameObject> targets,
                           bool cancellable, bool hasContinue)
        {
            End();
            IsActive = true;
            allowCancel = cancellable;

            root.SetActive(true);
            titleLabel.text = title;
            hintLabel.text = hint;
            cancelButton.gameObject.SetActive(cancellable);
            continueButton.gameObject.SetActive(hasContinue);

            SetOrder(dimCanvas, baseOrder);
            SetOrder(bannerCanvas, baseOrder + 2);

            if (alwaysOnTop != null)
                foreach (Canvas canvas in alwaysOnTop)
                    if (canvas != null) SetOrder(canvas, baseOrder + 3);

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null) continue;
                int index = i;
                lifted.Add(Lift(targets[i], baseOrder + 1, () => OnTargetClicked(index)));
            }
        }

        private void OnTargetClicked(int index)
        {
            if (!IsActive || onPicked == null) return;
            Action<int> picked = onPicked;
            End();
            picked(index);
        }

        private static void SetOrder(Canvas canvas, int order)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
        }

        private static Lifted Lift(GameObject go, int order, Action clicked)
        {
            var item = new Lifted { Object = go };

            item.Canvas = go.GetComponent<Canvas>();
            if (item.Canvas == null) { item.Canvas = go.AddComponent<Canvas>(); item.AddedCanvas = true; }

            item.OldOverride = item.Canvas.overrideSorting;
            item.OldOrder = item.Canvas.sortingOrder;
            SetOrder(item.Canvas, order);

            item.Raycaster = go.GetComponent<GraphicRaycaster>();
            if (item.Raycaster == null) { item.Raycaster = go.AddComponent<GraphicRaycaster>(); item.AddedRaycaster = true; }

            if (clicked != null)
            {
                item.Target = go.AddComponent<SelectionTarget>();
                item.Target.Clicked = clicked;
            }

            return item;
        }

        private static void Restore(Lifted item)
        {
            if (item.Object == null) return;

            if (item.Target != null) Destroy(item.Target);
            if (item.AddedRaycaster) Destroy(item.Raycaster);

            if (item.AddedCanvas) Destroy(item.Canvas); // after the raycaster
            else { item.Canvas.overrideSorting = item.OldOverride; item.Canvas.sortingOrder = item.OldOrder; }
        }

        private void OnDisable() { if (IsActive) End(); }
    }

    /// Added at runtime to anything that can be picked.
    internal sealed class SelectionTarget : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Clicked?.Invoke();
        }
    }
}