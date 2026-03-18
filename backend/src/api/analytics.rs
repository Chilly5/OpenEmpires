use axum::{
    extract::State,
    http::StatusCode,
    response::IntoResponse,
    Json,
};
use serde::{Deserialize, Serialize};
use std::sync::Arc;
use std::time::{SystemTime, UNIX_EPOCH};
use uuid::Uuid;

use crate::models::GameMode;
use crate::services::metrics::{MatchEventType, MatchResult};
use crate::AppState;

#[derive(Debug, Deserialize)]
#[allow(dead_code)]
pub struct SinglePlayerSessionReport {
    pub session_id: String,
    pub client_id: Option<String>,
    pub duration_ms: i64,
    pub result: String,
}

#[derive(Debug, Serialize)]
pub struct AnalyticsResponse {
    pub success: bool,
    pub message: String,
}

const MIN_DURATION_MS: i64 = 10_000;      // 10 seconds
const MAX_DURATION_MS: i64 = 86_400_000;  // 24 hours

pub async fn report_single_player_session(
    State(state): State<Arc<AppState>>,
    Json(req): Json<SinglePlayerSessionReport>,
) -> impl IntoResponse {
    // Validate session_id
    let session_id = match Uuid::parse_str(&req.session_id) {
        Ok(id) => id,
        Err(_) => {
            return (
                StatusCode::BAD_REQUEST,
                Json(AnalyticsResponse {
                    success: false,
                    message: "Invalid session_id format".to_string(),
                }),
            )
                .into_response()
        }
    };

    // Validate duration
    if req.duration_ms < MIN_DURATION_MS {
        return (
            StatusCode::BAD_REQUEST,
            Json(AnalyticsResponse {
                success: false,
                message: format!("Duration too short (minimum {}ms)", MIN_DURATION_MS),
            }),
        )
            .into_response();
    }

    if req.duration_ms > MAX_DURATION_MS {
        return (
            StatusCode::BAD_REQUEST,
            Json(AnalyticsResponse {
                success: false,
                message: format!("Duration too long (maximum {}ms)", MAX_DURATION_MS),
            }),
        )
            .into_response();
    }

    // Parse result
    let result = match req.result.to_lowercase().as_str() {
        "victory" => MatchResult::Victory { winning_team: 0 },
        "defeat" => MatchResult::Victory { winning_team: 1 },
        "abandoned" => MatchResult::Abandoned,
        _ => {
            return (
                StatusCode::BAD_REQUEST,
                Json(AnalyticsResponse {
                    success: false,
                    message: "Invalid result (must be 'victory', 'defeat', or 'abandoned')".to_string(),
                }),
            )
                .into_response()
        }
    };

    let timestamp = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_secs() as i64;

    // Insert into database
    if let Err(e) = state
        .metrics_db
        .insert_match_event(
            session_id,
            MatchEventType::Completed,
            GameMode::SinglePlayer,
            Some(1), // player_count
            Some(req.duration_ms),
            Some(&result),
            &[],  // participant_countries (not tracked for single player)
            &[],  // participant_usernames
            &[],  // participant_cities
            timestamp,
        )
        .await
    {
        tracing::error!("Failed to insert single player session: {}", e);
        return (
            StatusCode::INTERNAL_SERVER_ERROR,
            Json(AnalyticsResponse {
                success: false,
                message: "Failed to record session".to_string(),
            }),
        )
            .into_response();
    }

    tracing::info!(
        "Recorded single player session: {} duration={}ms result={}",
        session_id,
        req.duration_ms,
        req.result
    );

    (
        StatusCode::OK,
        Json(AnalyticsResponse {
            success: true,
            message: "Session recorded".to_string(),
        }),
    )
        .into_response()
}
