using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    // ========== ENUMS ==========

    [Serializable]
    public enum GameMode
    {
        OneVsOne,
        TwoVsTwo,
        ThreeVsThree,
        FourVsFour
    }

    [Serializable]
    public enum MatchState
    {
        WaitingForReady,
        Starting,
        InProgress,
        Finished
    }

    // ========== DATA STRUCTURES ==========

    [Serializable]
    public class TeamPlayer
    {
        public string player_id;
        public string username;
        public int game_player_id;
        public bool is_ready;
        public int civilization;
    }

    [Serializable]
    public class Team
    {
        public int team_id;
        public TeamPlayer[] players;
    }

    [Serializable]
    public class ConnectionInfo
    {
        public string type; // "P2P" or "Relay"
        public string host_player_id;
        public string host_address;
        public string relay_session_id;
    }

    [Serializable]
    public class GameCommandPayload
    {
        public int frame;
        public string command_type;
        public string payload; // JSON string
    }

    // ========== CLIENT → SERVER MESSAGES ==========

    public abstract class ClientMessage
    {
        public abstract string ToJson();
    }

    [Serializable]
    public class AuthenticateMessage : ClientMessage
    {
        public string token;

        public AuthenticateMessage(string token)
        {
            this.token = token;
        }

        public override string ToJson()
        {
            return $"{{\"type\":\"Authenticate\",\"data\":{{\"token\":\"{token}\"}}}}";
        }
    }

    [Serializable]
    public class JoinQueueMessage : ClientMessage
    {
        public GameMode game_mode;
        public int civilization;

        public JoinQueueMessage(GameMode mode, int civilization)
        {
            this.game_mode = mode;
            this.civilization = civilization;
        }

        public override string ToJson()
        {
            return $"{{\"type\":\"JoinQueue\",\"data\":{{\"game_mode\":\"{game_mode}\",\"civilization\":{civilization}}}}}";
        }
    }

    [Serializable]
    public class LeaveQueueMessage : ClientMessage
    {
        public override string ToJson()
        {
            return "{\"type\":\"LeaveQueue\"}";
        }
    }

    [Serializable]
    public class ReadyMessage : ClientMessage
    {
        public override string ToJson()
        {
            return "{\"type\":\"Ready\"}";
        }
    }

    [Serializable]
    public class SetHostAddressMessage : ClientMessage
    {
        public string address;

        public SetHostAddressMessage(string address)
        {
            this.address = address;
        }

        public override string ToJson()
        {
            return $"{{\"type\":\"SetHostAddress\",\"data\":{{\"address\":\"{address}\"}}}}";
        }
    }

    [Serializable]
    public class GameCommandMessage : ClientMessage
    {
        public int frame;
        public string command_type;
        public string payload;

        public GameCommandMessage(int frame, string commandType, string payload)
        {
            this.frame = frame;
            this.command_type = commandType;
            this.payload = payload;
        }

        public override string ToJson()
        {
            // Escape the payload JSON string so Rust treats it as a string
            string escapedPayload = payload.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"{{\"type\":\"GameCommand\",\"data\":{{\"command\":{{\"frame\":{frame},\"command_type\":\"{command_type}\",\"payload\":\"{escapedPayload}\"}}}}}}";
        }
    }

    [Serializable]
    public class LeaveMatchMessage : ClientMessage
    {
        public override string ToJson()
        {
            return "{\"type\":\"LeaveMatch\"}";
        }
    }

    [Serializable]
    public class PingMessage : ClientMessage
    {
        public long timestamp;

        public PingMessage(long timestamp)
        {
            this.timestamp = timestamp;
        }

        public override string ToJson()
        {
            return $"{{\"type\":\"Ping\",\"data\":{{\"timestamp\":{timestamp}}}}}";
        }
    }

    [Serializable]
    public class ReportPingMessage : ClientMessage
    {
        public uint ping_ms;

        public ReportPingMessage(uint pingMs)
        {
            this.ping_ms = pingMs;
        }

        public override string ToJson()
        {
            return $"{{\"type\":\"ReportPing\",\"data\":{{\"ping_ms\":{ping_ms}}}}}";
        }
    }

    [Serializable]
    public class TabVisibilityMessage : ClientMessage
    {
        public bool visible;

        public TabVisibilityMessage(bool visible)
        {
            this.visible = visible;
        }

        public override string ToJson()
        {
            return $"{{\"type\":\"TabVisibility\",\"data\":{{\"visible\":{(visible ? "true" : "false")}}}}}";
        }
    }

    // ========== SERVER → CLIENT MESSAGES ==========

    public abstract class ServerMessage
    {
        public abstract string Type { get; }
    }

    [Serializable]
    public class AuthenticatedMessage : ServerMessage
    {
        public override string Type => "Authenticated";
        public string player_id;
        public string username;
    }

    [Serializable]
    public class AuthErrorMessage : ServerMessage
    {
        public override string Type => "AuthError";
        public string message;
    }

    [Serializable]
    public class QueueJoinedMessage : ServerMessage
    {
        public override string Type => "QueueJoined";
        public GameMode game_mode;
        public int position;
    }

    [Serializable]
    public class QueueLeftMessage : ServerMessage
    {
        public override string Type => "QueueLeft";
    }

    [Serializable]
    public class QueueUpdateMessage : ServerMessage
    {
        public override string Type => "QueueUpdate";
        public int position;
    }

    [Serializable]
    public class QueuePlayersUpdateMessage : ServerMessage
    {
        public override string Type => "QueuePlayersUpdate";
        public string[] players;
    }

    [Serializable]
    public class MatchFoundMessage : ServerMessage
    {
        public override string Type => "MatchFound";
        public string match_id;
        public GameMode game_mode;
        public Team[] teams;
        public ConnectionInfo connection_info;
        public int your_game_player_id;
    }

    [Serializable]
    public class PlayerReadyMessage : ServerMessage
    {
        public override string Type => "PlayerReady";
        public string player_id;
    }

    [Serializable]
    public class HostAddressSetMessage : ServerMessage
    {
        public override string Type => "HostAddressSet";
        public string address;
    }

    [Serializable]
    public class MatchStartingMessage : ServerMessage
    {
        public override string Type => "MatchStarting";
        public string match_id;
    }

    [Serializable]
    public class MatchStateChangedMessage : ServerMessage
    {
        public override string Type => "MatchStateChanged";
        public MatchState state;
    }

    [Serializable]
    public class ServerGameCommandMessage : ServerMessage
    {
        public override string Type => "GameCommand";
        public int from_player_id;
        public GameCommandPayload command;
    }

    [Serializable]
    public class PlayerDisconnectedMessage : ServerMessage
    {
        public override string Type => "PlayerDisconnected";
        public string player_id;
    }

    [Serializable]
    public class ErrorMessage : ServerMessage
    {
        public override string Type => "Error";
        public string message;
    }

    [Serializable]
    public class PongMessage : ServerMessage
    {
        public override string Type => "Pong";
        public long timestamp;
    }

    [Serializable]
    public class PlayerPingMessage : ServerMessage
    {
        public override string Type => "PlayerPing";
        public int game_player_id;
        public int ping_ms;
    }

    [Serializable]
    public class PlayerJoinedMatchMessage : ServerMessage
    {
        public override string Type => "PlayerJoinedMatch";
        public string player_id;
        public string username;
        public int game_player_id;
        public int team_id;
        public int civilization;
    }

    [Serializable]
    public class ChatServerMessage : ServerMessage
    {
        public override string Type => "ChatMessage";
        public int from_player_id;
        public string from_username;
        public string channel;
        public string text;
    }

    // ========== CLIENT CHAT MESSAGE ==========

    [Serializable]
    public class ChatClientMessage : ClientMessage
    {
        public string channel;
        public string text;

        public ChatClientMessage(string channel, string text)
        {
            this.channel = channel;
            this.text = text;
        }

        public override string ToJson()
        {
            string escapedText = text.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"{{\"type\":\"ChatMessage\",\"data\":{{\"channel\":\"{channel}\",\"text\":\"{escapedText}\"}}}}";
        }
    }

    // ========== MESSAGE PARSER ==========

    public static class ServerMessageParser
    {
        [Serializable]
        private class MessageWrapper
        {
            public string type;
            public string data;
        }

        public static ServerMessage Parse(string json)
        {
            try
            {
                // Extract type first
                int typeStart = json.IndexOf("\"type\":\"") + 8;
                int typeEnd = json.IndexOf("\"", typeStart);
                string type = json.Substring(typeStart, typeEnd - typeStart);

                // Extract data portion if present
                int dataStart = json.IndexOf("\"data\":");
                string dataJson = "{}";
                if (dataStart >= 0)
                {
                    dataStart += 7;
                    int depth = 0;
                    int dataEnd = dataStart;
                    bool inString = false;

                    for (int i = dataStart; i < json.Length; i++)
                    {
                        char c = json[i];
                        if (c == '\"' && (i == 0 || json[i - 1] != '\\'))
                            inString = !inString;
                        else if (!inString)
                        {
                            if (c == '{' || c == '[') depth++;
                            else if (c == '}' || c == ']') depth--;

                            if (depth == 0 && (c == '}' || c == ']'))
                            {
                                dataEnd = i + 1;
                                break;
                            }
                        }
                    }
                    dataJson = json.Substring(dataStart, dataEnd - dataStart);
                }

                return type switch
                {
                    "Authenticated" => ParseAuthenticated(dataJson),
                    "AuthError" => ParseAuthError(dataJson),
                    "QueueJoined" => ParseQueueJoined(dataJson),
                    "QueueLeft" => new QueueLeftMessage(),
                    "QueueUpdate" => ParseQueueUpdate(dataJson),
                    "QueuePlayersUpdate" => ParseQueuePlayersUpdate(dataJson),
                    "MatchFound" => ParseMatchFound(dataJson),
                    "PlayerReady" => ParsePlayerReady(dataJson),
                    "HostAddressSet" => ParseHostAddressSet(dataJson),
                    "MatchStarting" => ParseMatchStarting(dataJson),
                    "MatchStateChanged" => ParseMatchStateChanged(dataJson),
                    "GameCommand" => ParseGameCommand(dataJson),
                    "PlayerDisconnected" => ParsePlayerDisconnected(dataJson),
                    "Error" => ParseError(dataJson),
                    "Pong" => ParsePong(dataJson),
                    "PlayerPing" => ParsePlayerPing(dataJson),
                    "ChatMessage" => ParseChatMessage(dataJson),
                    "PlayerJoinedMatch" => JsonUtility.FromJson<PlayerJoinedMatchMessage>(dataJson),
                    _ => null
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"[ServerMessageParser] Failed to parse: {e.Message}\nJSON: {json}");
                return null;
            }
        }

        private static AuthenticatedMessage ParseAuthenticated(string json)
        {
            var msg = JsonUtility.FromJson<AuthenticatedMessage>(json);
            return msg;
        }

        private static AuthErrorMessage ParseAuthError(string json)
        {
            return JsonUtility.FromJson<AuthErrorMessage>(json);
        }

        private static QueueJoinedMessage ParseQueueJoined(string json)
        {
            var msg = JsonUtility.FromJson<QueueJoinedMessage>(json);
            msg.game_mode = ParseGameMode(json);
            return msg;
        }

        private static QueueUpdateMessage ParseQueueUpdate(string json)
        {
            return JsonUtility.FromJson<QueueUpdateMessage>(json);
        }

        private static QueuePlayersUpdateMessage ParseQueuePlayersUpdate(string json)
        {
            // JsonUtility can't deserialize string arrays directly, so parse manually
            var msg = new QueuePlayersUpdateMessage();
            var list = new List<string>();
            int arrStart = json.IndexOf('[');
            int arrEnd = json.LastIndexOf(']');
            if (arrStart >= 0 && arrEnd > arrStart)
            {
                string arrContent = json.Substring(arrStart + 1, arrEnd - arrStart - 1).Trim();
                if (arrContent.Length > 0)
                {
                    // Split by commas outside of quotes
                    bool inQuote = false;
                    int start = 0;
                    for (int i = 0; i < arrContent.Length; i++)
                    {
                        char c = arrContent[i];
                        if (c == '"') inQuote = !inQuote;
                        else if (c == ',' && !inQuote)
                        {
                            list.Add(ExtractString(arrContent.Substring(start, i - start)));
                            start = i + 1;
                        }
                    }
                    list.Add(ExtractString(arrContent.Substring(start)));
                }
            }
            msg.players = list.ToArray();
            return msg;
        }

        private static string ExtractString(string s)
        {
            s = s.Trim();
            if (s.StartsWith("\"") && s.EndsWith("\""))
                s = s.Substring(1, s.Length - 2);
            return s;
        }

        private static GameMode ParseGameMode(string json)
        {
            int start = json.IndexOf("\"game_mode\":\"");
            if (start >= 0)
            {
                start += 13; // length of "game_mode":"
                int end = json.IndexOf("\"", start);
                string value = json.Substring(start, end - start);
                return value switch
                {
                    "OneVsOne" => GameMode.OneVsOne,
                    "TwoVsTwo" => GameMode.TwoVsTwo,
                    "ThreeVsThree" => GameMode.ThreeVsThree,
                    "FourVsFour" => GameMode.FourVsFour,
                    _ => GameMode.OneVsOne,
                };
            }
            return GameMode.OneVsOne;
        }

        private static MatchFoundMessage ParseMatchFound(string json)
        {
            var msg = JsonUtility.FromJson<MatchFoundMessage>(json);
            msg.game_mode = ParseGameMode(json);
            return msg;
        }

        private static PlayerReadyMessage ParsePlayerReady(string json)
        {
            return JsonUtility.FromJson<PlayerReadyMessage>(json);
        }

        private static HostAddressSetMessage ParseHostAddressSet(string json)
        {
            return JsonUtility.FromJson<HostAddressSetMessage>(json);
        }

        private static MatchStartingMessage ParseMatchStarting(string json)
        {
            return JsonUtility.FromJson<MatchStartingMessage>(json);
        }

        private static MatchStateChangedMessage ParseMatchStateChanged(string json)
        {
            return JsonUtility.FromJson<MatchStateChangedMessage>(json);
        }

        private static ServerGameCommandMessage ParseGameCommand(string json)
        {
            return JsonUtility.FromJson<ServerGameCommandMessage>(json);
        }

        private static PlayerDisconnectedMessage ParsePlayerDisconnected(string json)
        {
            return JsonUtility.FromJson<PlayerDisconnectedMessage>(json);
        }

        private static ErrorMessage ParseError(string json)
        {
            return JsonUtility.FromJson<ErrorMessage>(json);
        }

        private static PongMessage ParsePong(string json)
        {
            return JsonUtility.FromJson<PongMessage>(json);
        }

        private static PlayerPingMessage ParsePlayerPing(string json)
        {
            return JsonUtility.FromJson<PlayerPingMessage>(json);
        }

        private static ChatServerMessage ParseChatMessage(string json)
        {
            return JsonUtility.FromJson<ChatServerMessage>(json);
        }
    }
}
