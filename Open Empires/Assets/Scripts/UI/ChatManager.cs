using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public enum ChatChannel { All, Team }

    public struct ChatMessage
    {
        public string SenderName;
        public Color SenderColor;
        public string Text;
        public ChatChannel Channel;
        public bool IsSystem;
        public int SenderPlayerId; // -1 for system messages
    }

    public static class ChatManager
    {
        private const int MaxMessages = 50;
        private static readonly List<ChatMessage> messages = new List<ChatMessage>(MaxMessages);

        public static IReadOnlyList<ChatMessage> Messages => messages;
        public static event Action<ChatMessage> OnMessageAdded;

        public static void AddMessage(ChatMessage msg)
        {
            if (messages.Count >= MaxMessages)
                messages.RemoveAt(0);
            messages.Add(msg);
            OnMessageAdded?.Invoke(msg);
        }

        public static void AddSystemMessage(string text)
        {
            AddMessage(new ChatMessage
            {
                SenderName = "System",
                SenderColor = new Color(0.9f, 0.85f, 0.4f),
                Text = text,
                Channel = ChatChannel.All,
                IsSystem = true,
                SenderPlayerId = -1
            });
        }

        public static void Clear() => messages.Clear();
    }
}
