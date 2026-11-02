using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Struikrovers.Presentation
{
    /// One thing the player can pick. GlowCards makes the face-up cards inside it pulse (a pawn row).
    public readonly struct SelectionEntry
    {
        public readonly GameObject Object;
        public readonly bool GlowCards;

        public SelectionEntry(GameObject obj, bool glowCards)
        {
            Object = obj;
            GlowCards = glowCards;
        }
    }

    /// Dims the screen and lifts chosen objects above the dim with a temporary nested Canvas,
    /// so layouts, prefabs and hierarchy stay untouched. Hovering keeps working on lifted objects.
    /// It only shows and reports clicks; the turn state decides what a click means.
    public sealed class SelectionLayer : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Canvas dimCanvas;     // on the Dim object (Image + Canvas + GraphicRaycaster)
        [SerializeField] private Canvas bannerCanvas;  // on the Banner object
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private int baseOrder = 100;

        private sealed class Lifted
        {
            public GameObject Object;
            public Canvas Canvas;
            public GraphicRaycaster Raycaster;
            public bool AddedCanvas, AddedRaycaster, OldOverride, OldRaycasterEnabled;
            public int OldOrder;
            public SelectionTarget Target;
            public CardView Card;
            public readonly List<SelectionTarget> Forwarders = new List<SelectionTarget>();
            public readonly List<CardView> Glowing = new List<CardView>();
        }

        private readonly List<Lifted> lifted = new List<Lifted>();
        private readonly List<GameObject> overlays = new List<GameObject>();
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

        /// Objects that must stay readable above everything while selecting (the card detail popup).
        /// They are lifted without a raycaster, so they never block the pointer.
        public void SetOverlays(params GameObject[] objects)
        {
            overlays.Clear();
            if (objects == null) return;

            foreach (GameObject overlay in objects)
                if (overlay != null) overlays.Add(overlay);
        }

        /// Pick one of the lifted targets. An entry without an object is skipped but keeps its index.
        /// With canCancel the banner shows a Cancel button and 'cancelled' runs when it is used.
        public void BeginPick(string title, string hint, IReadOnlyList<SelectionEntry> targets,
                              bool canCancel, Action<int> picked, Action cancelled)
        {
            Begin(title, hint, targets, canCancel, false, true);
            onPicked = picked;
            onCancel = cancelled;
        }

        /// Show lifted objects, not clickable, with a Continue button (Peek).
        public void BeginReveal(string title, string hint, IReadOnlyList<SelectionEntry> targets, Action done)
        {
            Begin(title, hint, targets, false, true, false);
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
            if (cancelled != null) cancelled();
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

        private void Begin(string title, string hint, IReadOnlyList<SelectionEntry> targets,
                           bool cancellable, bool hasContinue, bool interactive)
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

            foreach (GameObject overlay in overlays)
                lifted.Add(Lift(overlay, baseOrder + 3, false, null, false));

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].Object == null) continue;
                int index = i;
                lifted.Add(Lift(targets[i].Object, baseOrder + 1, interactive,
                () => OnTargetClicked(index), targets[i].GlowCards));
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

        private static Lifted Lift(GameObject go, int order, bool interactive, Action clicked, bool glowCards)
        {
            var item = new Lifted { Object = go };

            item.Canvas = go.GetComponent<Canvas>();
            if (item.Canvas == null) { item.Canvas = go.AddComponent<Canvas>(); item.AddedCanvas = true; }

            item.OldOverride = item.Canvas.overrideSorting;
            item.OldOrder = item.Canvas.sortingOrder;
            SetOrder(item.Canvas, order);

            if (!interactive)
                return item;

            item.Raycaster = go.GetComponent<GraphicRaycaster>();
            if (item.Raycaster == null) { item.Raycaster = go.AddComponent<GraphicRaycaster>(); item.AddedRaycaster = true; }

            item.OldRaycasterEnabled = item.Raycaster.enabled;
            item.Raycaster.enabled = true;

            if (clicked != null)
            {
                item.Target = go.AddComponent<SelectionTarget>();
                item.Target.Clicked = clicked;

                // A panel or a pawn row is clicked through the cards inside it, and a card
                // handles the click itself. Every card inside passes the click on.
                foreach (CardView inner in go.GetComponentsInChildren<CardView>(true))
                {
                    if (inner.gameObject == go) continue;

                    SelectionTarget forward = inner.gameObject.AddComponent<SelectionTarget>();
                    forward.Clicked = clicked;
                    item.Forwarders.Add(forward);

                    if (glowCards && inner.Card != null)
                    {
                        inner.SetSelectable(true);
                        item.Glowing.Add(inner);
                    }
                }
            }

            item.Card = go.GetComponent<CardView>();
            if (item.Card != null) item.Card.SetSelectable(true);

            return item;
        }

        private static void Restore(Lifted item)
        {
            if (item.Object == null) return;

            if (item.Card != null) item.Card.SetSelectable(false);

            foreach (CardView glowing in item.Glowing)
                if (glowing != null) glowing.SetSelectable(false);

            foreach (SelectionTarget forward in item.Forwarders)
                if (forward != null) Destroy(forward);

            if (item.Target != null) Destroy(item.Target);

            // Components we added are removed at once, not at the end of the frame. Otherwise a selection
            // that ends and starts again in one frame (Peek) finds a canvas that is about to die.
            // The raycaster goes first, because it needs the canvas.
            if (item.Raycaster != null)
            {
                if (item.AddedRaycaster) DestroyImmediate(item.Raycaster);
                else item.Raycaster.enabled = item.OldRaycasterEnabled;
            }

            if (item.Canvas != null)
            {
                if (item.AddedCanvas)
                {
                    DestroyImmediate(item.Canvas);
                }
                else
                {
                    item.Canvas.overrideSorting = item.OldOverride;
                    item.Canvas.sortingOrder = item.OldOrder;
                }
            }
        }

        private void OnDisable() { if (IsActive) End(); }
    }

    /// Added at runtime to anything that can be picked, and to the cards inside a picked panel or row.
    internal sealed class SelectionTarget : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && Clicked != null)
                Clicked();
        }
    }
}