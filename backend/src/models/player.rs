use serde::{Deserialize, Serialize};
use uuid::Uuid;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum PlayerState {
    Idle,
    InQueue,
    InMatch,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Player {
    pub id: Uuid,
    pub username: String,
    pub session_token: String,
    pub state: PlayerState,
}

impl Player {
    pub fn new(username: String) -> Self {
        Self {
            id: Uuid::new_v4(),
            username,
            session_token: Uuid::new_v4().to_string(),
            state: PlayerState::Idle,
        }
    }
}
