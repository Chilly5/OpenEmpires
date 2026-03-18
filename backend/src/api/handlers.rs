use axum::{
    extract::{ConnectInfo, State},
    http::StatusCode,
    response::IntoResponse,
    Json,
};
use serde::{Deserialize, Serialize};
use std::net::SocketAddr;
use std::sync::Arc;

use crate::models::{GameMode, Player};
use crate::AppState;

#[derive(Debug, Deserialize)]
pub struct LoginRequest {
    pub username: String,
}

#[derive(Debug, Serialize)]
pub struct LoginResponse {
    pub player_id: String,
    pub token: String,
    pub username: String,
}

#[derive(Debug, Deserialize)]
pub struct JoinQueueRequest {
    pub token: String,
    pub game_mode: GameMode,
    #[serde(default)]
    pub civilization: u8,
}

#[derive(Debug, Serialize)]
pub struct JoinQueueResponse {
    pub position: usize,
    pub game_mode: GameMode,
}

#[derive(Debug, Deserialize)]
pub struct LeaveQueueRequest {
    pub token: String,
}

#[derive(Debug, Serialize)]
pub struct MessageResponse {
    pub message: String,
}

pub async fn login(
    State(state): State<Arc<AppState>>,
    Json(req): Json<LoginRequest>,
) -> impl IntoResponse {
    if req.username.trim().is_empty() {
        return (
            StatusCode::BAD_REQUEST,
            Json(MessageResponse {
                message: "Username cannot be empty".to_string(),
            }),
        )
            .into_response();
    }

    if req.username.len() > 32 {
        return (
            StatusCode::BAD_REQUEST,
            Json(MessageResponse {
                message: "Username too long (max 32 characters)".to_string(),
            }),
        )
            .into_response();
    }

    let player = Player::new(req.username.trim().to_string());
    let response = LoginResponse {
        player_id: player.id.to_string(),
        token: player.session_token.clone(),
        username: player.username.clone(),
    };

    state.players.write().await.insert(player.session_token.clone(), player);

    (StatusCode::OK, Json(response)).into_response()
}

pub async fn join_queue(
    State(state): State<Arc<AppState>>,
    ConnectInfo(addr): ConnectInfo<SocketAddr>,
    Json(req): Json<JoinQueueRequest>,
) -> impl IntoResponse {
    let player = {
        let players = state.players.read().await;
        players.get(&req.token).cloned()
    };

    let player = match player {
        Some(p) => p,
        None => {
            return (
                StatusCode::UNAUTHORIZED,
                Json(MessageResponse {
                    message: "Invalid token".to_string(),
                }),
            )
                .into_response()
        }
    };

    // Store player IP for geolocation
    state.player_ips.write().await.insert(player.id, addr.ip().to_string());

    let entry = crate::models::QueueEntry::new(player.id, player.username.clone(), req.game_mode, req.civilization);
    let position = state.queue_manager.add_to_queue(entry).await;

    {
        let mut players = state.players.write().await;
        if let Some(p) = players.get_mut(&req.token) {
            p.state = crate::models::PlayerState::InQueue;
        }
    }

    (
        StatusCode::OK,
        Json(JoinQueueResponse {
            position,
            game_mode: req.game_mode,
        }),
    )
        .into_response()
}

pub async fn leave_queue(
    State(state): State<Arc<AppState>>,
    Json(req): Json<LeaveQueueRequest>,
) -> impl IntoResponse {
    let player = {
        let players = state.players.read().await;
        players.get(&req.token).cloned()
    };

    let player = match player {
        Some(p) => p,
        None => {
            return (
                StatusCode::UNAUTHORIZED,
                Json(MessageResponse {
                    message: "Invalid token".to_string(),
                }),
            )
                .into_response()
        }
    };

    state.queue_manager.remove_from_queue(player.id).await;

    {
        let mut players = state.players.write().await;
        if let Some(p) = players.get_mut(&req.token) {
            p.state = crate::models::PlayerState::Idle;
        }
    }

    (
        StatusCode::OK,
        Json(MessageResponse {
            message: "Left queue".to_string(),
        }),
    )
        .into_response()
}

pub async fn health() -> impl IntoResponse {
    (StatusCode::OK, Json(MessageResponse { message: "OK".to_string() }))
}
