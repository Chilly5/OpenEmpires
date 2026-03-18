mod api;
mod config;
mod models;
mod services;

use std::collections::HashMap;
use std::sync::Arc;
use std::time::{Instant, SystemTime, UNIX_EPOCH};
use tokio::sync::{mpsc, RwLock};
use uuid::Uuid;

use std::net::SocketAddr;

use axum::{
    routing::{get, post},
    Router,
};
use tower_http::cors::{Any, CorsLayer};
use tower_http::services::ServeDir;

use crate::api::{analytics, dashboard::{self, broadcast_dashboard_update}, handlers, ws};
use crate::config::Config;
use crate::models::{DashboardMessage, GameMode, Player, ServerMessage};
use crate::models::dashboard_messages::{ServerLoad, QueueStatus, QueueHistoryPoint, game_mode_to_string};
use crate::services::metrics::ServerLoadMetrics;
use crate::services::{GeoLocationService, MatchmakingEvent, Matchmaker, MetricsCollector, MetricsDb, QueueManager, RelayManager, SessionManager};

pub struct AppState {
    pub players: RwLock<HashMap<String, Player>>,
    pub queue_manager: Arc<QueueManager>,
    pub session_manager: Arc<SessionManager>,
    pub relay_manager: Arc<RelayManager>,
    pub metrics: Arc<MetricsCollector>,
    pub metrics_db: Arc<MetricsDb>,
    pub ws_connections: RwLock<HashMap<Uuid, mpsc::UnboundedSender<ServerMessage>>>,
    pub dashboard_clients: RwLock<Vec<mpsc::UnboundedSender<DashboardMessage>>>,
    pub player_ips: RwLock<HashMap<Uuid, String>>,
    pub geolocation: Arc<GeoLocationService>,
}

impl AppState {
    pub async fn new() -> Arc<Self> {
        let database_url = std::env::var("DATABASE_URL")
            .expect("DATABASE_URL environment variable must be set");

        let metrics_db = MetricsDb::new(&database_url)
            .await
            .expect("Failed to initialize metrics database");

        Arc::new(Self {
            players: RwLock::new(HashMap::new()),
            queue_manager: QueueManager::new(),
            session_manager: SessionManager::new(),
            relay_manager: RelayManager::new(),
            metrics: MetricsCollector::new(),
            metrics_db: Arc::new(metrics_db),
            ws_connections: RwLock::new(HashMap::new()),
            dashboard_clients: RwLock::new(Vec::new()),
            player_ips: RwLock::new(HashMap::new()),
            geolocation: GeoLocationService::new(),
        })
    }
}

#[tokio::main]
async fn main() {
    dotenvy::dotenv().ok();

    tracing_subscriber::fmt()
        .with_env_filter(
            tracing_subscriber::EnvFilter::try_from_default_env()
                .unwrap_or_else(|_| "info".into()),
        )
        .init();

    let config = Config::from_env();
    let state = AppState::new().await;

    // Load persisted metrics from database on startup
    state.metrics.load_from_db(&state.metrics_db).await;

    let (shutdown_tx, shutdown_rx) = mpsc::channel::<()>(1);
    let (match_tx, mut match_rx) = mpsc::channel::<MatchmakingEvent>(100);

    let matchmaker = Arc::new(Matchmaker::new(
        state.queue_manager.clone(),
        state.session_manager.clone(),
        state.metrics.clone(),
    ));

    let matchmaker_handle = {
        let matchmaker = matchmaker.clone();
        tokio::spawn(async move {
            matchmaker.run(config.matchmaking_interval_ms, shutdown_rx, match_tx).await;
        })
    };

    let match_handler = {
        let state = state.clone();
        tokio::spawn(async move {
            while let Some(event) = match_rx.recv().await {
                match event {
                    MatchmakingEvent::NewMatch(session) => {
                        ws::handle_match_found(state.clone(), session).await;
                    }
                    MatchmakingEvent::PlayerJoined { session, new_player, team_id } => {
                        ws::handle_player_joined(state.clone(), session, new_player, team_id).await;
                    }
                }
            }
        })
    };

    // Spawn metrics snapshot task
    let metrics_task = {
        let state = state.clone();
        tokio::spawn(async move {
            let mut interval = tokio::time::interval(tokio::time::Duration::from_secs(1));
            let mut db_persist_counter: u64 = 0;
            loop {
                interval.tick().await;
                collect_metrics_snapshot(&state, db_persist_counter % 60 == 0).await;
                db_persist_counter = db_persist_counter.wrapping_add(1);
            }
        })
    };

    let cors = CorsLayer::new()
        .allow_origin(Any)
        .allow_methods(Any)
        .allow_headers(Any);

    let app = Router::new()
        .route("/health", get(handlers::health))
        .route("/api/auth/login", post(handlers::login))
        .route("/api/queue/join", post(handlers::join_queue))
        .route("/api/queue/leave", post(handlers::leave_queue))
        .route("/ws", get(ws::ws_handler))
        .route("/api/dashboard/status", get(dashboard::get_dashboard_status))
        .route("/api/dashboard/history", get(dashboard::get_dashboard_history))
        .route("/api/dashboard/history/matches", get(dashboard::get_match_history))
        .route("/api/dashboard/history/daily", get(dashboard::get_daily_stats))
        .route("/api/analytics/single-player", post(analytics::report_single_player_session))
        .route("/ws/dashboard", get(dashboard::dashboard_ws_handler))
        .nest_service("/dashboard", ServeDir::new("static/dashboard"))
        .layer(cors)
        .with_state(state);

    let addr = format!("0.0.0.0:{}", config.server_port);
    tracing::info!("Starting matchmaking server on {}", addr);

    let listener = tokio::net::TcpListener::bind(&addr).await.unwrap();

    let server = axum::serve(
        listener,
        app.into_make_service_with_connect_info::<SocketAddr>(),
    );

    tokio::select! {
        result = server => {
            if let Err(e) = result {
                tracing::error!("Server error: {}", e);
            }
        }
        _ = tokio::signal::ctrl_c() => {
            tracing::info!("Shutting down...");
            let _ = shutdown_tx.send(()).await;
        }
    }

    matchmaker_handle.abort();
    match_handler.abort();
    metrics_task.abort();
}

async fn collect_metrics_snapshot(state: &Arc<AppState>, persist_to_db: bool) {
    let now = Instant::now();

    // Get queue data
    let queue_entries = state.queue_manager.get_queue_entries().await;
    let queue_sizes: HashMap<GameMode, usize> = queue_entries
        .iter()
        .map(|(mode, entries)| (*mode, entries.len()))
        .collect();

    let longest_wait_ms: HashMap<GameMode, u64> = queue_entries
        .iter()
        .map(|(mode, entries)| {
            let max_wait = entries
                .iter()
                .map(|e| now.duration_since(e.joined_at).as_millis() as u64)
                .max()
                .unwrap_or(0);
            (*mode, max_wait)
        })
        .collect();

    // Record queue snapshot (in-memory)
    state
        .metrics
        .record_queue_snapshot(queue_sizes.clone(), longest_wait_ms.clone())
        .await;

    // Persist to database every 60 seconds
    if persist_to_db {
        let timestamp = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_secs() as i64;

        let mode_1v1 = *queue_sizes.get(&GameMode::OneVsOne).unwrap_or(&0) as i32;
        let mode_2v2 = *queue_sizes.get(&GameMode::TwoVsTwo).unwrap_or(&0) as i32;
        let mode_3v3 = *queue_sizes.get(&GameMode::ThreeVsThree).unwrap_or(&0) as i32;
        let mode_4v4 = *queue_sizes.get(&GameMode::FourVsFour).unwrap_or(&0) as i32;

        let longest_wait_1v1 = longest_wait_ms.get(&GameMode::OneVsOne).copied().map(|v| v as i64);
        let longest_wait_2v2 = longest_wait_ms.get(&GameMode::TwoVsTwo).copied().map(|v| v as i64);
        let longest_wait_3v3 = longest_wait_ms.get(&GameMode::ThreeVsThree).copied().map(|v| v as i64);
        let longest_wait_4v4 = longest_wait_ms.get(&GameMode::FourVsFour).copied().map(|v| v as i64);

        if let Err(e) = state.metrics_db.insert_queue_snapshot(
            timestamp,
            mode_1v1,
            mode_2v2,
            mode_3v3,
            mode_4v4,
            longest_wait_1v1,
            longest_wait_2v2,
            longest_wait_3v3,
            longest_wait_4v4,
        ).await {
            tracing::error!("Failed to persist queue snapshot to DB: {}", e);
        }
    }

    // Update server load
    let players_in_queue: usize = queue_sizes.values().sum();
    let players_in_match = state.session_manager.get_players_in_matches().await;
    let session_counts = state.session_manager.get_session_counts().await;
    let active_connections = state.ws_connections.read().await.len();

    let load = ServerLoadMetrics {
        active_connections,
        players_in_queue,
        players_in_match,
        active_sessions: session_counts.total,
    };
    state.metrics.update_server_load(load).await;

    // Broadcast load update to dashboard clients
    let dashboard_load = ServerLoad {
        active_connections,
        players_in_queue,
        players_in_match,
        active_sessions: session_counts.total,
    };
    broadcast_dashboard_update(state, DashboardMessage::LoadUpdate(dashboard_load)).await;

    // Broadcast queue update to dashboard clients
    let now_instant = Instant::now();
    let queue_entries_for_broadcast = state.queue_manager.get_queue_entries().await;
    let queues: Vec<QueueStatus> = [
        GameMode::OneVsOne,
        GameMode::TwoVsTwo,
        GameMode::ThreeVsThree,
        GameMode::FourVsFour,
    ]
    .iter()
    .map(|mode| {
        let entries = queue_entries_for_broadcast.get(mode).cloned().unwrap_or_default();
        let player_count = entries.len();
        let (avg_wait_ms, longest_wait_ms) = if entries.is_empty() {
            (0, 0)
        } else {
            let waits: Vec<u64> = entries
                .iter()
                .map(|e| now_instant.duration_since(e.joined_at).as_millis() as u64)
                .collect();
            let total: u64 = waits.iter().sum();
            (total / waits.len() as u64, waits.iter().copied().max().unwrap_or(0))
        };
        QueueStatus {
            game_mode: game_mode_to_string(mode),
            player_count,
            avg_wait_ms,
            longest_wait_ms,
        }
    })
    .collect();

    // Create queue history point for this snapshot
    let timestamp = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_millis() as u64;
    let sizes: HashMap<String, usize> = queues
        .iter()
        .map(|q| (q.game_mode.clone(), q.player_count))
        .collect();
    let queue_history_point = QueueHistoryPoint { timestamp, sizes };

    broadcast_dashboard_update(
        state,
        DashboardMessage::QueueUpdate {
            queues,
            queue_history_point,
        },
    )
    .await;
}
