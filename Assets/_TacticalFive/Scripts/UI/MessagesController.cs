using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class MessagesController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Messages;
    private VisualElement _messagesBody;
    private List<MessageData> _messages;
    protected override void CacheReferences()
    {
        _messagesBody = _root.Q<VisualElement>("MessagesBody");
    }
    protected override void LoadData()
    {
        base.LoadData();

        
        

        
        
        _messages = DatabaseManager.Instance.GetMessages(_manager.id);
    }
    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Messages] RefreshHeader error: {ex.Message}"); }
        BuildMessages();
    }

    void BuildMessages()
    {
        _messagesBody.Clear();

        if (_messages == null || _messages.Count == 0)
        {
            var empty = new VisualElement();
            empty.AddToClassList("messages-empty");
            var lbl = new Label("No hay mensajes en la bandeja de entrada.");
            lbl.AddToClassList("messages-empty-text");
            empty.Add(lbl);
            _messagesBody.Add(empty);
            return;
        }

        // Sort by date descending (newest first)
        var sorted = _messages.OrderByDescending(m => m.created_at).ToList();

        foreach (var message in sorted)
        {
            var card = CreateMessageCard(message);
            _messagesBody.Add(card);

            // Mark as read when viewing
            if (message.is_read == 0)
                DatabaseManager.Instance.MarkMessageRead(message.id);
        }
    }

    VisualElement CreateMessageCard(MessageData message)
    {
        var card = new VisualElement();
        card.AddToClassList("message-card");
        if (message.is_read == 0)
            card.AddToClassList("message-card--unread");

        // Header: title + delete button
        var header = new VisualElement();
        header.AddToClassList("message-card-header");

        var title = new Label(message.title);
        title.AddToClassList("message-card-title");
        header.Add(title);

        var deleteBtn = new Button();
        deleteBtn.AddToClassList("message-card-delete");
        var trashTex = Resources.Load<Texture2D>("Icons/papelera");
        if (trashTex != null)
            deleteBtn.style.backgroundImage = new StyleBackground(trashTex);
        var msgId = message.id;
        deleteBtn.clicked += () => { PlayClick(); DeleteMessage(msgId); };
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(deleteBtn);
        header.Add(deleteBtn);

        card.Add(header);

        // Date
        var date = new Label();
        date.AddToClassList("message-card-date");
        try
        {
            date.text = System.DateTime.Parse(message.game_date).ToString("dd/MM/yyyy");
        }
        catch
        {
            date.text = message.game_date ?? "";
        }
        card.Add(date);

        // Body
        var body = new Label(message.body);
        body.AddToClassList("message-card-body");
        card.Add(body);

        return card;
    }

    void DeleteMessage(int messageId)
    {
        DatabaseManager.Instance.DeleteMessage(messageId);
        LoadData();
        BuildMessages();
    }
}
