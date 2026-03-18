use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::GameMode;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum MatchState {
    WaitingForReady,
    Starting,
    InProgress,
    Finished,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TeamPlayer {
    pub player_id: Uuid,
    pub username: String,
    pub game_player_id: u8,
    pub is_ready: bool,
    pub civilization: u8,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Team {
    pub team_id: u8,
    pub players: Vec<TeamPlayer>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum ConnectionInfo {
    P2P {
        host_player_id: Uuid,
        host_address: Option<String>,
    },
    Relay {
        relay_session_id: Uuid,
    },
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MatchSession {
    pub id: Uuid,
    pub game_mode: GameMode,
    pub teams: Vec<Team>,
    pub connection_info: ConnectionInfo,
    pub state: MatchState,
    pub is_open: bool,
}

impl MatchSession {
    pub fn new(game_mode: GameMode, teams: Vec<Team>) -> Self {
        let connection_info = if game_mode.uses_relay() {
            ConnectionInfo::Relay {
                relay_session_id: Uuid::new_v4(),
            }
        } else {
            let host_player_id = teams
                .first()
                .and_then(|t| t.players.first())
                .map(|p| p.player_id)
                .unwrap_or_else(Uuid::new_v4);
            ConnectionInfo::P2P {
                host_player_id,
                host_address: None,
            }
        };

        Self {
            id: Uuid::new_v4(),
            game_mode,
            teams,
            connection_info,
            state: MatchState::WaitingForReady,
            is_open: false,
        }
    }

    pub fn all_players(&self) -> Vec<&TeamPlayer> {
        self.teams.iter().flat_map(|t| &t.players).collect()
    }

    pub fn all_player_ids(&self) -> Vec<Uuid> {
        self.all_players().iter().map(|p| p.player_id).collect()
    }

    pub fn all_ready(&self) -> bool {
        self.all_players().iter().all(|p| p.is_ready)
    }

    pub fn set_player_ready(&mut self, player_id: Uuid) -> bool {
        for team in &mut self.teams {
            for player in &mut team.players {
                if player.player_id == player_id {
                    player.is_ready = true;
                    return true;
                }
            }
        }
        false
    }

    pub fn set_host_address(&mut self, address: String) {
        if let ConnectionInfo::P2P { host_address, .. } = &mut self.connection_info {
            *host_address = Some(address);
        }
    }
}
