using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A lightweight readable item for water-surface exploration. The visual model
/// can be replaced freely; title and body remain gameplay data.
/// </summary>
[DisallowMultipleComponent]
public sealed class SurfaceDocument : MonoBehaviour
{
    [SerializeField] private string documentTitle = "深海作业记录";
    [TextArea(4, 16)] [SerializeField] private string documentBody = "记录内容。";
    [SerializeField] private bool hideWorldObjectWhenRead = true;
    [SerializeField] private UnityEvent onOpened = new();
    [SerializeField] private UnityEvent onClosed = new();

    public string DocumentTitle => documentTitle;
    public string DocumentBody => documentBody;
    public bool HasBeenRead { get; private set; }
    public UnityEvent OnOpened => onOpened;
    public UnityEvent OnClosed => onClosed;

    public void Open()
    {
        HasBeenRead = true;
        onOpened?.Invoke();
        if (hideWorldObjectWhenRead)
            gameObject.SetActive(false);
    }

    public void Close()
    {
        onClosed?.Invoke();
    }

    public void Configure(string title, string body)
    {
        documentTitle = string.IsNullOrWhiteSpace(title) ? "未命名记录" : title;
        documentBody = body ?? string.Empty;
    }
}
