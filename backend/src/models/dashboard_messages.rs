use serde::{Deserialize, Serialize};
use std::collections::HashMap;

use crate::models::GameMode;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct QueueStatus {
    pub game_mode: String,
    pub player_count: usize,
    pub avg_wait_ms: u64,
    pub longest_wait_ms: u64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ServerLoad {
    pub active_connections: usize,
    pub players_in_queue: usize,
    pub players_in_match: usize,
    pub active_sessions: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MatchStats {
    pub in_progress: HashMap<String, usize>,
    pub created_this_hour: usize,
    pub total_created: usize,
    pub average_duration_ms: Option<u64>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct QueueHistoryPoint {
    pub timestamp: u64,
    pub sizes: HashMap<String, usize>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MatchesPerMinute {
    pub minute: u64,
    pub count: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ParticipantLocation {
    pub country_code: String,
    pub username: String,
    pub city: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RecentMatchEvent {
    pub match_id: Option<String>,
    pub timestamp: u64,
    pub wall_clock_time: u64,
    pub game_mode: String,
    pub event_type: String,
    pub duration_ms: Option<u64>,
    pub result: Option<String>,
    pub participant_locations: Vec<ParticipantLocation>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ActiveMatch {
    pub match_id: String,
    pub game_mode: String,
    pub start_time: u64,  // Unix timestamp when match started (all players ready)
    pub created_time: u64, // Unix timestamp when match was created
    pub participant_locations: Vec<ParticipantLocation>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DailyMatchCountDto {
    pub date: String,
    pub count: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ServerFailureDto {
    pub timestamp: u64,
    pub wall_clock_time: u64,
    pub failure_type: String,
    pub description: String,
    pub match_id: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ServerHealth {
    pub failure_counts: HashMap<String, usize>,
    pub recent_failures: Vec<ServerFailureDto>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DashboardState {
    pub server_load: ServerLoad,
    pub queues: Vec<QueueStatus>,
    pub match_stats: MatchStats,
    pub queue_history: Vec<QueueHistoryPoint>,
    pub matches_per_minute: Vec<MatchesPerMinute>,
    pub recent_events: Vec<RecentMatchEvent>,
    pub active_matches: Vec<ActiveMatch>,
    pub daily_match_counts: Vec<DailyMatchCountDto>,
    pub server_health: ServerHealth,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", content = "data")]
pub enum DashboardMessage {
    FullState(DashboardState),
    QueueUpdate {
        queues: Vec<QueueStatus>,
        queue_history_point: QueueHistoryPoint,
    },
    LoadUpdate(ServerLoad),
    MatchEvent(RecentMatchEvent),
    FailureEvent(ServerFailureDto),
}

pub fn game_mode_to_string(mode: &GameMode) -> String {
    match mode {
        GameMode::OneVsOne => "1v1".to_string(),
        GameMode::TwoVsTwo => "2v2".to_string(),
        GameMode::ThreeVsThree => "3v3".to_string(),
        GameMode::FourVsFour => "4v4".to_string(),
        GameMode::SinglePlayer => "single_player".to_string(),
    }
}

pub fn match_result_to_string(result: &crate::services::metrics::MatchResult) -> String {
    match result {
        crate::services::metrics::MatchResult::Victory { winning_team } => {
            format!("Victory (Team {})", winning_team)
        }
        crate::services::metrics::MatchResult::Surrender => "Surrender".to_string(),
        crate::services::metrics::MatchResult::Draw => "Draw".to_string(),
        crate::services::metrics::MatchResult::Abandoned => "Abandoned".to_string(),
    }
}
