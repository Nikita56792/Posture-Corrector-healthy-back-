using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MessageContainer : MonoBehaviour
{
    public Chat Chat;
    public RectTransform ContainerObject;
    public GameObject MessagePrefab;
    public GameObject ChatOwnerMessagePrefab;

    private readonly List<MessagePresenter> _presenters = new List<MessagePresenter>();
    private List<Message> _allMessages = new List<Message>();
    private bool isLoading = false;

    private void Start()
    {
        Chat ??= FindObjectOfType<Chat>();

        isLoading = true;
        _allMessages = ChatStorage.LoadChat();

        foreach (var message in _allMessages)
            AddMessage(message);

        isLoading = false;
    }

    private void OnDestroy()
    {
        foreach (MessagePresenter presenter in _presenters)
            presenter.OnMessageDelete -= DeleteMessage;
    }

    public void AddMessage(Message message)
    {
        MessagePresenter presenter = InstantiatePresenter(message);
        presenter.OnMessageDelete += DeleteMessage;

        if (!isLoading)
        {
            _allMessages.Add(message);
            ChatStorage.SaveChat(_allMessages);
        }
    }

    private MessagePresenter InstantiatePresenter(Message message)
    {
        GameObject prefab = message.Sender == Chat.Owner
            ? ChatOwnerMessagePrefab
            : MessagePrefab;

        MessagePresenter presenter = Instantiate(prefab, ContainerObject).GetComponent<MessagePresenter>();

        MessagePresenter lastMessage = _presenters.LastOrDefault();
        if (lastMessage && lastMessage.Message.Sender == message.Sender)
            lastMessage.Redraw(asLast: false);

        presenter.Message = message;
        _presenters.Add(presenter);

        return presenter;
    }

    public void ClearMessagesRuntime()
    {
        foreach (var presenter in _presenters)
        {
            presenter.OnMessageDelete -= DeleteMessage;
            Destroy(presenter.gameObject);
        }

        _presenters.Clear();
        _allMessages.Clear();
    }

    private void DeleteMessage(Message message)
    {
        MessagePresenter presenter = _presenters.FirstOrDefault(o => o.Message == message);
        if (!presenter)
            return;

        RedrawPreviousIfNeeded(presenter);
        DestroyMessagePresenter(presenter);

        _allMessages.Remove(message);
        ChatStorage.SaveChat(_allMessages);
    }

    private void DestroyMessagePresenter(MessagePresenter presenter)
    {
        presenter.OnMessageDelete -= DeleteMessage;
        _presenters.Remove(presenter);
        Destroy(presenter.gameObject);
    }

    private void RedrawPreviousIfNeeded(MessagePresenter presenter)
    {
        int index = _presenters.IndexOf(presenter);

        MessagePresenter previous = ValidIndex(index - 1) ? _presenters[index - 1] : null;
        MessagePresenter next = ValidIndex(index + 1) ? _presenters[index + 1] : null;

        if (ShouldRedrawPrevious())
            previous.Redraw(asLast: true);

        bool ShouldRedrawPrevious() =>
            previous && (!next || next.Message.Sender != presenter.Message.Sender);
    }

    private bool ValidIndex(int index) =>
        index >= 0 && index < _presenters.Count;
}
