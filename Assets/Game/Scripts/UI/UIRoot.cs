using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LostAndFound
{
    /// <summary>Builds the screen-space UI: dialogue, tag card, hints, inspect bar, discovery cards,
    /// Agnes's notes, the day card and the fader. Menus register themselves as modal.</summary>
    public class UIRoot : MonoBehaviour
    {
        public static UIRoot I { get; private set; }
        public static bool ModalOpen => I != null && I.modalCount > 0;
        public Canvas canvas;
        public RectTransform root;
        public DialogueBox dialogue;
        public TagCard tagCard;
        public HintBar hint;
        public InspectBar inspectBar;
        public HotspotMarker hotspotMarker;
        public DiscoveryFX discovery;
        public NotePopup note;
        public DayCard dayCard;
        public Fader fader;
        public TurnArrows arrows;
        public RulesPeek rulesPeek;
        int modalCount;

        public void PushModal() => modalCount++;
        public void PopModal() => modalCount = Mathf.Max(0, modalCount - 1);

        void Awake()
        {
            I = this;
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            if (Application.isBatchMode && Camera.main != null)
            {
                // headless editor captures render a camera, not the screen: draw the UI through it
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera.main;
                canvas.planeDistance = 0.1f;
            }
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            root = (RectTransform)transform;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            arrows = TurnArrows.Create(root);
            hotspotMarker = HotspotMarker.Create(root);
            discovery = DiscoveryFX.Create(root);
            tagCard = TagCard.Create(root);
            rulesPeek = RulesPeek.Create(root);
            inspectBar = InspectBar.Create(root);
            hint = HintBar.Create(root);
            dialogue = DialogueBox.Create(root);
            note = NotePopup.Create(root);
            fader = Fader.Create(root);
            dayCard = DayCard.Create(root);
        }
    }
}
