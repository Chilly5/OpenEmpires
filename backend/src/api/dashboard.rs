use axum::{
    extract::{
        ws::{Message, WebSocket, WebSocketUpgrade},
        Query, State,
    },
    response::IntoResponse,
    Json,
};
use serde::Deserialize;
use futures::{SinkExt, StreamExt};
use std::collections::HashMap;
use std::sync::Arc;
use std::time::Instant;

use crate::models::dashboard_messages::{
    game_mode_to_string, ActiveMatch, DailyMatchCountDto, DashboardMessage,
    DashboardState, MatchStats, MatchesPerMinute, ParticipantLocation, QueueHistoryPoint,
    QueueStatus, RecentMatchEvent, ServerFailureDto, ServerHealth, ServerLoad,
};
use crate::models::GameMode;
use crate::AppState;

pub async fn dashboard_ws_handler(
    ws: WebSocketUpgrade,
    State(state): State<Arc<AppState>>,
) -> impl IntoResponse {
    ws.on_upgrade(|socket| handle_dashboard_socket(socket, state))
}

async fn handle_dashboard_socket(socket: WebSocket, state: Arc<AppState>) {
    let (mut sender, mut receiver) = socket.split();

    // Send initial full state
    let full_state = build_dashboard_state(&state).await;
    let msg = DashboardMessage::FullState(full_state);
    if let Ok(json) = serde_json::to_string(&msg) {
        if sender.send(Message::Text(json.into())).await.is_err() {
            return;
        }
    }

    // Create a channel for sending updates
    let (tx, mut rx) = tokio::sync::mpsc::unbounded_channel::<DashboardMessage>();

    // Spawn task to forward channel messages to WebSocket
    let send_task = tokio::spawn(async move {
        while let Some(msg) = rx.recv().await {
            if let Ok(json) = serde_json::to_string(&msg) {
                if sender.send(Message::Text(json.into())).await.is_err() {
                    break;
                }
            }
        }
    });

    // Register this dashboard client
    {
        let mut clients = state.dashboard_clients.write().await;
        clients.push(tx.clone());
    }

    // Keep connection alive, handle incoming messages
    while let Some(msg) = receiver.next().await {
        match msg {
            Ok(Message::Close(_)) => break,
            Err(_) => break,
            _ => continue,
        }
    }

    // Remove client on disconnect
    {
        let mut clients = state.dashboard_clients.write().await;
        clients.retain(|c| !c.is_closed());
    }

    send_task.abort();
}

pub async fn get_dashboard_status(State(state): State<Arc<AppState>>) -> impl IntoResponse {
    let dashboard_state = build_dashboard_state(&state).await;
    Json(dashboard_state)
}

pub async fn get_dashboard_history(State(state): State<Arc<AppState>>) -> impl IntoResponse {
    let queue_history = state.metrics.get_queue_history().await;

    let history: Vec<QueueHistoryPoint> = queue_history
        .into_iter()
        .map(|snapshot| QueueHistoryPoint {
            timestamp: snapshot.timestamp,
            sizes: snapshot
                .queue_sizes
                .into_iter()
                .map(|(mode, size)| (game_mode_to_string(&mode), size))
                .collect(),
        })
        .collect();

    Json(history)
}

async fn build_dashboard_state(state: &Arc<AppState>) -> DashboardState {
    // Get queue data
    let queue_entries = state.queue_manager.get_queue_entries().await;
    let now = Instant::now();

    let mut queues = Vec::new();
    for mode in [
        GameMode::OneVsOne,
        GameMode::TwoVsTwo,
        GameMode::ThreeVsThree,
        GameMode::FourVsFour,
    ] {
        let entries = queue_entries.get(&mode).cloned().unwrap_or_default();
        let player_count = entries.len();

        let (avg_wait_ms, longest_wait_ms) = if entries.is_empty() {
            (0, 0)
        } else {
            let waits: Vec<u64> = entries
                .iter()
                .map(|e| now.duration_since(e.joined_at).as_millis() as u64)
                .collect();
            let total: u64 = waits.iter().sum();
            let avg = total / waits.len() as u64;
            let max = waits.iter().copied().max().unwrap_or(0);
            (avg, max)
        };

        queues.push(QueueStatus {
            game_mode: game_mode_to_string(&mode),
            player_count,
            avg_wait_ms,
            longest_wait_ms,
        });
    }

    // Get server load
    let server_load = state.metrics.get_server_load().await;
    let load = ServerLoad {
        active_connections: server_load.active_connections,
        players_in_queue: server_load.players_in_queue,
        players_in_match: server_load.players_in_match,
        active_sessions: server_load.active_sessions,
    };

    // Get match stats
    let matches_in_progress = state.metrics.get_matches_in_progress().await;
    let in_progress: HashMap<String, usize> = matches_in_progress
        .into_iter()
        .map(|(mode, count)| (game_mode_to_string(&mode), count))
        .collect();
    let created_this_hour = state.metrics.get_matches_created_last_hour().await;
    let total_created = state.metrics.get_total_matches_created().await;
    let average_duration_ms = state.metrics.get_average_match_duration_ms().await;

    let match_stats = MatchStats {
        in_progress,
        created_this_hour,
        total_created,
        average_duration_ms,
    };

    // Get queue history
    let queue_history_raw = state.metrics.get_queue_history().await;
    let queue_history: Vec<QueueHistoryPoint> = queue_history_raw
        .into_iter()
        .map(|snapshot| QueueHistoryPoint {
            timestamp: snapshot.timestamp,
            sizes: snapshot
                .queue_sizes
                .into_iter()
                .map(|(mode, size)| (game_mode_to_string(&mode), size))
                .collect(),
        })
        .collect();

    // Get matches per minute
    let matches_per_min_raw = state.metrics.get_matches_per_minute_history().await;
    let matches_per_minute: Vec<MatchesPerMinute> = matches_per_min_raw
        .into_iter()
        .map(|(minute, count)| MatchesPerMinute { minute, count })
        .collect();

    // Get recent events from database (persisted storage)
    let recent_events: Vec<RecentMatchEvent> = match state.metrics_db.get_recent_match_events(50).await {
        Ok(records) => records
            .into_iter()
            .map(|record| {
                // Parse JSON arrays from DB
                let countries: Vec<String> = serde_json::from_str(&record.participant_countries)
                    .unwrap_or_default();
                let usernames: Vec<String> = record.participant_usernames
                    .as_ref()
                    .and_then(|s| serde_json::from_str(s).ok())
                    .unwrap_or_default();
                let cities: Vec<String> = record.participant_cities
                    .as_ref()
                    .and_then(|s| serde_json::from_str(s).ok())
                    .unwrap_or_default();

                // Build participant locations
                let participant_locations: Vec<ParticipantLocation> = countries
                    .iter()
                    .zip(usernames.iter().chain(std::iter::repeat(&String::new())))
                    .zip(cities.iter().chain(std::iter::repeat(&String::new())))
                    .map(|((cc, username), city)| ParticipantLocation {
                        country_code: cc.clone(),
                        username: username.clone(),
                        city: if city.is_empty() { None } else { Some(city.clone()) },
                    })
                    .collect();

                // Convert result string to display format
                let result_str = record.result.as_ref().map(|r| match r.as_str() {
                    "victory" => {
                        if let Some(team) = record.winning_team {
                            format!("Victory (Team {})", team)
                        } else {
                            "Victory".to_string()
                        }
                    }
                    "surrender" => "Surrender".to_string(),
                    "draw" => "Draw".to_string(),
                    "abandoned" => "Abandoned".to_string(),
                    other => other.to_string(),
                });

                RecentMatchEvent {
                    match_id: Some(record.match_id),
                    timestamp: record.timestamp as u64,
                    wall_clock_time: record.timestamp as u64,
                    game_mode: record.game_mode,
                    event_type: record.event_type,
                    duration_ms: record.duration_ms.map(|d| d as u64),
                    result: result_str,
                    participant_locations,
                }
            })
            .collect(),
        Err(e) => {
            tracing::error!("Failed to get recent match events from DB: {}", e);
            Vec::new()
        }
    };

    // Get active matches
    let active_matches_raw = state.metrics.get_active_matches().await;
    let active_matches: Vec<ActiveMatch> = active_matches_raw
        .into_iter()
        .map(|m| {
            let participant_locations: Vec<ParticipantLocation> = m
                .participant_countries
                .iter()
                .zip(m.participant_usernames.iter().chain(std::iter::repeat(&String::new())))
                .zip(m.participant_cities.iter().chain(std::iter::repeat(&String::new())))
                .map(|((cc, username), city)| ParticipantLocation {
                    country_code: cc.clone(),
                    username: username.clone(),
                    city: if city.is_empty() { None } else { Some(city.clone()) },
                })
                .collect();

            ActiveMatch {
                match_id: m.match_id.to_string(),
                game_mode: game_mode_to_string(&m.game_mode),
                start_time: m.start_time.unwrap_or(0),
                created_time: m.created_time,
                participant_locations,
            }
        })
        .collect();

    // Get daily match counts
    let daily_counts_raw = state.metrics.get_daily_match_counts().await;
    let daily_match_counts: Vec<DailyMatchCountDto> = daily_counts_raw
        .into_iter()
        .map(|dc| DailyMatchCountDto {
            date: dc.date,
            count: dc.count,
        })
        .collect();

    // Get server health
    let failure_counts = state.metrics.get_failure_counts().await;
    let recent_failures_raw = state.metrics.get_recent_failures(20).await;
    let recent_failures: Vec<ServerFailureDto> = recent_failures_raw
        .into_iter()
        .map(|f| ServerFailureDto {
            timestamp: f.timestamp,
            wall_clock_time: f.wall_clock_time,
            failure_type: f.failure_type.to_string(),
            description: f.description,
            match_id: f.match_id.map(|id| id.to_string()),
        })
        .collect();

    let server_health = ServerHealth {
        failure_counts,
        recent_failures,
    };

    DashboardState {
        server_load: load,
        queues,
        match_stats,
        queue_history,
        matches_per_minute,
        recent_events,
        active_matches,
        daily_match_counts,
        server_health,
    }
}

pub async fn broadcast_dashboard_update(state: &Arc<AppState>, message: DashboardMessage) {
    let clients = state.dashboard_clients.read().await;
    for client in clients.iter() {
        let _ = client.send(message.clone());
    }
}

#[derive(Debug, Deserialize)]
pub struct MatchHistoryQuery {
    #[serde(default = "default_days")]
    days: i64,
}

fn default_days() -> i64 {
    7
}

#[derive(Debug, serde::Serialize)]
pub struct MatchHistoryResponse {
    pub matches: Vec<MatchHistoryRecord>,
}

#[derive(Debug, serde::Serialize)]
pub struct MatchHistoryRecord {
    pub id: i64,
    pub match_id: String,
    pub event_type: String,
    pub game_mode: String,
    pub player_count: Option<i32>,
    pub duration_ms: Option<i64>,
    pub result: Option<String>,
    pub winning_team: Option<i32>,
    pub participant_countries: Vec<String>,
    pub timestamp: i64,
}

pub async fn get_match_history(
    State(state): State<Arc<AppState>>,
    Query(query): Query<MatchHistoryQuery>,
) -> impl IntoResponse {
    match state.metrics_db.get_match_history(query.days).await {
        Ok(records) => {
            let matches: Vec<MatchHistoryRecord> = records
                .into_iter()
                .map(|r| {
                    let countries: Vec<String> = serde_json::from_str(&r.participant_countries)
                        .unwrap_or_default();
                    MatchHistoryRecord {
                        id: r.id,
                        match_id: r.match_id,
                        event_type: r.event_type,
                        game_mode: r.game_mode,
                        player_count: r.player_count,
                        duration_ms: r.duration_ms,
                        result: r.result,
                        winning_team: r.winning_team,
                        participant_countries: countries,
                        timestamp: r.timestamp,
                    }
                })
                .collect();
            Json(MatchHistoryResponse { matches })
        }
        Err(e) => {
            tracing::error!("Failed to get match history: {}", e);
            Json(MatchHistoryResponse { matches: vec![] })
        }
    }
}

#[derive(Debug, serde::Serialize)]
pub struct DailyStatsResponse {
    pub daily_stats: Vec<DailyStatsRecord>,
}

#[derive(Debug, serde::Serialize)]
pub struct DailyStatsRecord {
    pub date: String,
    pub matches_created: i32,
    pub matches_completed: i32,
    pub avg_match_duration_ms: Option<i64>,
    pub avg_queue_wait_ms: Option<i64>,
}

pub async fn get_daily_stats(State(state): State<Arc<AppState>>) -> impl IntoResponse {
    match state.metrics_db.get_daily_stats().await {
        Ok(records) => {
            let daily_stats: Vec<DailyStatsRecord> = records
                .into_iter()
                .map(|r| DailyStatsRecord {
                    date: r.date,
                    matches_created: r.matches_created,
                    matches_completed: r.matches_completed,
                    avg_match_duration_ms: r.avg_match_duration_ms,
                    avg_queue_wait_ms: r.avg_queue_wait_ms,
                })
                .collect();
            Json(DailyStatsResponse { daily_stats })
        }
        Err(e) => {
            tracing::error!("Failed to get daily stats: {}", e);
            Json(DailyStatsResponse { daily_stats: vec![] })
        }
    }
}
