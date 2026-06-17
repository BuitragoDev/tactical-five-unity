using UnityEngine;
using UnityEngine.UIElements;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [SerializeField] private Texture2D cursorDefault;
    [SerializeField] private Texture2D cursorHand;
    [SerializeField] private Vector2 defaultHotspot = Vector2.zero;
    [SerializeField] private Vector2 handHotspot    = Vector2.zero;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (cursorDefault == null)
            cursorDefault = Resources.Load<Texture2D>("Icons/cursor_default");
        if (cursorHand == null)
            cursorHand = Resources.Load<Texture2D>("Icons/cursor_hand");
        SetDefaultCursor();
    }

    public void SetDefaultCursor()
    {
        UnityEngine.Cursor.SetCursor(cursorDefault, defaultHotspot, CursorMode.Auto);
    }

    public void SetHandCursor()
    {
        UnityEngine.Cursor.SetCursor(cursorHand, handHotspot, CursorMode.Auto);
    }

    public void RegisterHandCursor(VisualElement element)
    {
        element.RegisterCallback<MouseEnterEvent>(_ => SetHandCursor(), TrickleDown.TrickleDown);
        element.RegisterCallback<MouseLeaveEvent>(_ => SetDefaultCursor(), TrickleDown.TrickleDown);
    }
}