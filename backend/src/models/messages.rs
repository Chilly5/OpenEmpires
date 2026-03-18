use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::{ConnectionInfo, GameMode, MatchState, Team};

#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq)]
#[serde(tag = "type", content = "data")]
pub enum MatchResult {
    Victory { winning_team: u8 },
    Surrender,
    Draw,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", content = "data")]
pub enum ClientMessage {
    Authenticate { token: String },
    JoinQueue { game_mode: GameMode, civilization: u8 },
    LeaveQueue,
    Ready,
    SetHostAddress { address: String },
    GameCommand { command: GameCommand },
    LeaveMatch,
    RequestReconnect,
    MatchEnded { result: MatchResult },
    Ping { timestamp: u64 },
    ReportPing { ping_ms: u32 },
    ChatMessage { channel: String, text: String },
    TabVisibility { visible: bool },
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", content = "data")]
pub enum ServerMessage {
    Authenticated {
        player_id: Uuid,
        username: String,
    },
    AuthError {
        message: String,
    },
    QueueJoined {
        game_mode: GameMode,
        position: usize,
    },
    QueueLeft,
    QueueUpdate {
        position: usize,
    },
    QueuePlayersUpdate {
        players: Vec<String>,
    },
    MatchFound {
        match_id: Uuid,
        game_mode: GameMode,
        teams: Vec<Team>,
        connection_info: ConnectionInfo,
        your_game_player_id: u8,
    },
    PlayerReady {
        player_id: Uuid,
    },
    HostAddressSet {
        address: String,
    },
    MatchStarting {
        match_id: Uuid,
    },
    MatchStateChanged {
        state: MatchState,
    },
    GameCommand {
        from_player_id: u8,
        command: GameCommand,
    },
    PlayerDisconnected {
        player_id: Uuid,
    },
    ReconnectAvailable {
        match_id: Uuid,
        game_mode: GameMode,
        teams: Vec<Team>,
        connection_info: ConnectionInfo,
        your_game_player_id: u8,
        command_history: Vec<ReconnectFrame>,
    },
    ReconnectUnavailable,
    PlayerReconnected {
        player_id: Uuid,
    },
    PlayerEjected {
        player_id: Uuid,
        game_player_id: u8,
    },
    Error {
        message: String,
    },
    Pong {
        timestamp: u64,
    },
    PlayerPing {
        game_player_id: u8,
        ping_ms: u32,
    },
    ChatMessage {
        from_player_id: u8,
        from_username: String,
        channel: String,
        text: String,
    },
    PlayerJoinedMatch {
        player_id: Uuid,
        username: String,
        game_player_id: u8,
        team_id: u8,
        civilization: u8,
    },
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ReconnectFrame {
    pub frame: u32,
    pub commands: Vec<ReconnectCommand>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ReconnectCommand {
    pub from_player_id: u8,
    pub command: GameCommand,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct GameCommand {
    pub frame: u32,
    pub command_type: String,
    pub payload: serde_json::Value,
}
