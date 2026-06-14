using UnityEngine;
using UnityEngine.EventSystems;
using MoreMountains.Feedbacks;

/// <summary>
/// Drop on any UI Button (or interactable) to play Feel feedbacks on hover and click.
///   hoverFeedback  : gentle scale-up / glow  (also fires on keyboard/gamepad Select)
///   exitFeedback   : scales back down        (also fires on Deselect)
///   clickFeedback  : punch + UI sound + brief screen flash
///
/// Tip: put the hover/click MMF_Players ON the button itself and have their Scale feedbacks
/// target this same RectTransform, so each button animates itself.
/// </summary>
public class UIFeedbackButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    ISelectHandler, IDeselectHandler
{
    [SerializeField] private MMF_Player hoverFeedback;
    [SerializeField] private MMF_Player exitFeedback;
    [SerializeField] private MMF_Player clickFeedback;

    public void OnPointerEnter(PointerEventData e) => Play(hoverFeedback);
    public void OnPointerExit(PointerEventData e)  => Play(exitFeedback);
    public void OnPointerClick(PointerEventData e) => Play(clickFeedback);
    public void OnSelect(BaseEventData e)          => Play(hoverFeedback);
    public void OnDeselect(BaseEventData e)        => Play(exitFeedback);

    private static void Play(MMF_Player p) { if (p != null) p.PlayFeedbacks(); }
}
