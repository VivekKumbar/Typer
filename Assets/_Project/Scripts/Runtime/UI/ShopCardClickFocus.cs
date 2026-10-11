using UnityEngine;
using UnityEngine.EventSystems;

// Helper component placed on each shop item card.
// If a player taps on a side/peeking card that is not currently centered,
// it smoothly snaps the shop scroll to focus that card.
public class ShopCardClickFocus : MonoBehaviour, IPointerClickHandler
{
    private ShopCardSnapScroll snapScroll;
    private int myIndex;

    public void Init(ShopCardSnapScroll scroller, int index)
    {
        snapScroll = scroller;
        myIndex = index;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (snapScroll == null) return;

        // If this card is not the active centered card, tap focuses it!
        if (snapScroll.CurrentIndex != myIndex)
        {
            snapScroll.SnapTo(myIndex);
        }
    }
}
