use serde::{Deserialize, Serialize};
use std::time::Instant;
use uuid::Uuid;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum GameMode {
    OneVsOne,
    TwoVsTwo,
    ThreeVsThree,
    FourVsFour,
    SinglePlayer,
}

impl GameMode {
    pub fn players_required(&self) -> usize {
        match self {
            GameMode::OneVsOne => 2,
            GameMode::TwoVsTwo => 4,
            GameMode::ThreeVsThree => 6,
            GameMode::FourVsFour => 8,
            GameMode::SinglePlayer => 1,
        }
    }

    pub fn team_size(&self) -> usize {
        match self {
            GameMode::OneVsOne => 1,
            GameMode::TwoVsTwo => 2,
            GameMode::ThreeVsThree => 3,
            GameMode::FourVsFour => 4,
            GameMode::SinglePlayer => 1,
        }
    }

    pub fn uses_relay(&self) -> bool {
        match self {
            GameMode::SinglePlayer => false,
            _ => true, // All multiplayer modes use relay
        }
    }
}

#[derive(Debug, Clone)]
pub struct QueueEntry {
    pub player_id: Uuid,
    pub username: String,
    pub game_mode: GameMode,
    pub joined_at: Instant,
    pub civilization: u8,
}

impl QueueEntry {
    pub fn new(player_id: Uuid, username: String, game_mode: GameMode, civilization: u8) -> Self {
        Self {
            player_id,
            username,
            game_mode,
            joined_at: Instant::now(),
            civilization,
        }
    }
}
