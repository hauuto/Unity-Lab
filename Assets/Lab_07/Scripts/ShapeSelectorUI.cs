using UnityEngine;
using UnityEngine.UI;

// Lab 06 - Menu chon hinh. Nut dang chon duoc lam noi bat.
public class ShapeSelectorUI : MonoBehaviour
{
    public ARShapePlacer placer;
    public Button[] buttons;
    public Color normalColor = new Color(1f, 1f, 1f, 0.55f);  // nut chua chon: trang mo
    public Color selectedColor = new Color(1f, 1f, 1f, 1f);   // nut dang chon: dac

    void Start()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            int idx = i; // tranh closure bat bien vong lap
            buttons[i].onClick.AddListener(() => Select(idx));
        }
        Select(placer != null ? placer.selectedIndex : 0);
    }

    public void Select(int index)
    {
        if (placer != null) placer.SelectShape(index);
        for (int i = 0; i < buttons.Length; i++)
        {
            var img = buttons[i].GetComponent<Image>();
            if (img) img.color = (i == index) ? selectedColor : normalColor;
            buttons[i].transform.localScale = Vector3.one * (i == index ? 1.1f : 1f);
        }
    }
}
